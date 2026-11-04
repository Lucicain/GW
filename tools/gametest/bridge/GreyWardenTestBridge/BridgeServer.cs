using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GreyWardenTestBridge
{
    /// <summary>
    /// 网络一侧：只在 127.0.0.1 上监听，收一行一个 JSON、做握手、把命令排进队列、把回复写回去。
    /// 这里的线程**不碰任何游戏对象**；命令由主线程在 <see cref="BridgeSubModule"/> 的 tick 里取出执行
    /// （PROTOCOL.md §3）。握手只用启动时缓存的常量，所以也在网络线程上答。
    /// </summary>
    internal sealed class BridgeServer
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly ConcurrentQueue<BridgeRequest> _inbox = new ConcurrentQueue<BridgeRequest>();
        private readonly object _sync = new object();
        private readonly string _token;
        private readonly string _session;
        private readonly JObject _helloResult;
        private BridgeConnection? _current;

        internal BridgeServer(string token, string session, JObject helloResult)
        {
            _token = token;
            _session = session;
            _helloResult = helloResult;
        }

        internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        /// <summary>端口 0 让系统分配；监听就绪后调用方才写 bridge.json。</summary>
        internal void Start()
        {
            _listener.Start();
            var thread = new Thread(AcceptLoop) { IsBackground = true, Name = "GwpTestBridge.Accept" };
            thread.Start();
        }

        internal bool TryDequeue(out BridgeRequest request) => _inbox.TryDequeue(out request);

        private void AcceptLoop()
        {
            // 监听器从不主动停止：游戏退出时进程连同后台线程一起结束，不需要处理关停时的异常。
            while (true)
            {
                TcpClient client = _listener.AcceptTcpClient();
                client.NoDelay = true;
                new BridgeConnection(this, client).Start();
            }
        }

        internal void Enqueue(BridgeRequest request) => _inbox.Enqueue(request);

        /// <summary>
        /// 握手。令牌定长比较；令牌或会话不符回 unauthorized，协议版本不符回 unsupported_protocol，
        /// 两种情况调用方都断开（PROTOCOL.md §4）。
        /// </summary>
        internal BridgeReply Hello(JObject args)
        {
            string token = StringArg(args, "token");
            string session = StringArg(args, "session");
            if (!FixedTimeEquals(token, _token) || !string.Equals(session, _session, StringComparison.Ordinal))
                return BridgeReply.Fail("unauthorized", "令牌或会话不符");
            JToken? protocol = args["protocol"];
            if (protocol == null || protocol.Type != JTokenType.Integer || protocol.Value<long>() != BridgeProtocol.Version)
                return BridgeReply.Fail("unsupported_protocol",
                    "桥接只支持协议版本 " + BridgeProtocol.Version + "，收到 " + (protocol?.ToString(Formatting.None) ?? "null"));
            return BridgeReply.Ok(_helloResult.DeepClone());
        }

        /// <summary>同一时间只服务一条连接：新连接握手成功后关闭旧连接（PROTOCOL.md §2）。</summary>
        internal void Promote(BridgeConnection connection)
        {
            BridgeConnection? previous;
            lock (_sync)
            {
                previous = _current;
                _current = connection;
            }

            if (previous != null && !ReferenceEquals(previous, connection)) previous.Close();
        }

        /// <summary>网络线程上读字符串参数：类型不对按空串处理，不抛异常（这里的异常没人接，会带走整个进程）。</summary>
        private static string StringArg(JObject args, string key) =>
            args[key] is JValue value && value.Value is string text ? text : string.Empty;

        private static bool FixedTimeEquals(string given, string expected)
        {
            // 长度不同也照样比完整个期望长度，耗时只取决于令牌长度。
            int diff = given.Length ^ expected.Length;
            for (int i = 0; i < expected.Length; i++)
            {
                char g = i < given.Length ? given[i] : '\0';
                diff |= g ^ expected[i];
            }

            return diff == 0;
        }
    }

    /// <summary>一条 TCP 连接：一个读线程、一个写线程。主线程只往写队列里放回复。</summary>
    internal sealed class BridgeConnection
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private readonly BridgeServer _server;
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly BlockingCollection<Outgoing> _outbox = new BlockingCollection<Outgoing>();
        private readonly object _sync = new object();
        private bool _closed;
        private bool _authenticated;

        internal BridgeConnection(BridgeServer server, TcpClient client)
        {
            _server = server;
            _client = client;
            _stream = client.GetStream();
        }

        internal void Start()
        {
            new Thread(ReadLoop) { IsBackground = true, Name = "GwpTestBridge.Read" }.Start();
            new Thread(WriteLoop) { IsBackground = true, Name = "GwpTestBridge.Write" }.Start();
        }

        /// <summary>
        /// 排一条回复。<paramref name="onDone"/> 在这行写出之后（或连接已关、写不出去时）在网络线程上调用，
        /// 只能设标志，不能碰游戏对象。
        /// </summary>
        internal void Send(JObject reply, Action? onDone = null, bool closeAfter = false)
        {
            var item = new Outgoing(reply.ToString(Formatting.None), onDone, closeAfter);
            lock (_sync)
            {
                if (!_closed)
                {
                    _outbox.Add(item);
                    return;
                }
            }

            onDone?.Invoke();
        }

        internal void Close()
        {
            lock (_sync)
            {
                if (_closed) return;
                _closed = true;
                _outbox.CompleteAdding();
            }

            _client.Close();
        }

        private void ReadLoop()
        {
            var line = new MemoryStream();
            byte[] chunk = new byte[64 * 1024];
            while (true)
            {
                int read;
                try
                {
                    read = _stream.Read(chunk, 0, chunk.Length);
                }
                catch (IOException)
                {
                    // 依据：设计（PROTOCOL.md §2）——新连接握手后本端主动关旧连接、Python 端超时后强杀游戏前
                    // 断开连接，阻塞中的 Read 都以 IOException 结束；这是连接的正常终点，不是故障。
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // 依据：同上，Close() 先于 Read 返回时套接字已释放。
                    break;
                }

                if (read == 0) break;
                int start = 0;
                for (int i = 0; i < read; i++)
                {
                    if (chunk[i] != (byte)'\n') continue;
                    line.Write(chunk, start, i - start);
                    start = i + 1;
                    if (line.Length > BridgeProtocol.MaxLineBytes)
                    {
                        Send(BridgeReply.Fail("bad_request", "单行超过 16 MiB").ToJson(JValue.CreateNull()), closeAfter: true);
                        return;
                    }

                    // 返回 false：已排好「回复后断开」，不再读；由写线程写完这条再关。
                    if (!HandleLine(line.ToArray())) return;

                    line.SetLength(0);
                }

                line.Write(chunk, start, read - start);
                if (line.Length > BridgeProtocol.MaxLineBytes)
                {
                    Send(BridgeReply.Fail("bad_request", "单行超过 16 MiB").ToJson(JValue.CreateNull()), closeAfter: true);
                    return;
                }
            }

            Close();
        }

        /// <summary>处理一行；返回 false 表示已排好「回复后断开」，读线程到此为止。</summary>
        private bool HandleLine(byte[] bytes)
        {
            string text = Utf8.GetString(bytes).TrimEnd('\r');
            if (text.Trim().Length == 0) return true;

            JObject request;
            try
            {
                request = JObject.Parse(text);
            }
            catch (JsonReaderException error)
            {
                // 依据：设计（PROTOCOL.md §2、§6）——请求不是合法 JSON 对象时按协议回 bad_request 再断开，
                // 这是对外部输入的校验结果，不吞错。
                ReplyAndClose(JValue.CreateNull(), BridgeReply.Fail("bad_request", "请求不是合法的 JSON 对象：" + error.Message));
                return false;
            }

            JToken? idToken = request["id"];
            JToken? cmdToken = request["cmd"];
            JToken? argsToken = request["args"];
            // 超出 long 的整数 Newtonsoft 存成 BigInteger，Value<long> 会抛异常，所以按装箱类型判断。
            if (!(idToken is JValue { Value: long }) || cmdToken == null || cmdToken.Type != JTokenType.String)
            {
                ReplyAndClose(idToken ?? JValue.CreateNull(), BridgeReply.Fail("bad_request", "请求必须带整数 id 和字符串 cmd"));
                return false;
            }

            long id = idToken.Value<long>();
            string command = cmdToken.Value<string>()!;
            JObject args;
            if (argsToken == null || argsToken.Type == JTokenType.Null) args = new JObject();
            else if (argsToken is JObject obj) args = obj;
            else
            {
                Send(BridgeReply.Fail("bad_request", "args 必须是对象").ToJson(id), closeAfter: !_authenticated);
                return _authenticated;
            }

            if (!_authenticated)
            {
                if (command != "hello")
                {
                    ReplyAndClose(id, BridgeReply.Fail("unauthorized", "每条连接的第一条请求必须是 hello"));
                    return false;
                }

                BridgeReply hello = _server.Hello(args);
                if (!hello.IsOk)
                {
                    ReplyAndClose(id, hello);
                    return false;
                }

                _authenticated = true;
                _server.Promote(this);
                Send(hello.ToJson(id));
                return true;
            }

            if (command == "hello")
            {
                Send(BridgeReply.Fail("bad_request", "本连接已经握过手").ToJson(id));
                return true;
            }

            _server.Enqueue(new BridgeRequest(this, id, command, args));
            return true;
        }

        private void ReplyAndClose(JToken id, BridgeReply reply) => Send(reply.ToJson(id), closeAfter: true);

        private void WriteLoop()
        {
            foreach (Outgoing item in _outbox.GetConsumingEnumerable())
            {
                byte[] bytes = Utf8.GetBytes(item.Line + "\n");
                try
                {
                    _stream.Write(bytes, 0, bytes.Length);
                }
                catch (IOException)
                {
                    // 依据：设计（PROTOCOL.md §2）——对端已断开（Python 端超时强杀、旧连接被新连接顶掉）时写失败，
                    // 这条回复已无人接收；连接到此结束。
                    item.OnDone?.Invoke();
                    Close();
                    return;
                }
                catch (ObjectDisposedException)
                {
                    // 依据：同上，Close() 已释放套接字。
                    item.OnDone?.Invoke();
                    return;
                }

                item.OnDone?.Invoke();
                if (item.CloseAfter)
                {
                    Close();
                    return;
                }
            }
        }

        private sealed class Outgoing
        {
            internal Outgoing(string line, Action? onDone, bool closeAfter)
            {
                Line = line;
                OnDone = onDone;
                CloseAfter = closeAfter;
            }

            internal string Line { get; }
            internal Action? OnDone { get; }
            internal bool CloseAfter { get; }
        }
    }
}
