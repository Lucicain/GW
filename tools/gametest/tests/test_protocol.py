import json
import socket
import threading
import unittest

from fakes import FakeBridge, make_env

from gametest.protocol import BridgeClient, BridgeError, BridgeInfo, read_bridge_info


class BridgeInfoTests(unittest.TestCase):
    def setUp(self):
        self.cfg = make_env(self)
        self.path = self.cfg.bridge_info_path

    def write(self, **data):
        base = {"protocol": 1, "host": "127.0.0.1", "port": 5000, "token": "t" * 32, "session": "s1", "pid": 9}
        base.update(data)
        self.path.write_text(json.dumps(base), encoding="utf-8")

    def test_reads_matching_session(self):
        self.write()
        info = read_bridge_info(self.path, "s1")
        self.assertEqual((info.port, info.pid), (5000, 9))

    def test_stale_session_is_ignored(self):
        self.write(session="old")
        self.assertIsNone(read_bridge_info(self.path, "s1"))

    def test_half_written_file_is_ignored(self):
        self.path.write_text('{"port": 50', encoding="utf-8")
        self.assertIsNone(read_bridge_info(self.path, "s1"))

    def test_bom_is_accepted(self):
        self.write()
        self.path.write_bytes(b"\xef\xbb\xbf" + self.path.read_bytes())
        self.assertIsNotNone(read_bridge_info(self.path, "s1"))

    def test_non_loopback_host_is_rejected(self):
        for host in ("0.0.0.0", "192.168.1.5", "localhost"):
            self.write(host=host)
            with self.assertRaises(BridgeError) as ctx:
                read_bridge_info(self.path, "s1")
            self.assertEqual(ctx.exception.code, "protocol_error")

    def test_bad_port_and_short_token_are_rejected(self):
        for bad in ({"port": 0}, {"port": "80"}, {"token": "short"}, {"pid": "9"}):
            self.write(**bad)
            with self.assertRaises(BridgeError, msg=str(bad)):
                read_bridge_info(self.path, "s1")


class ClientTests(unittest.TestCase):
    def setUp(self):
        self.cfg = make_env(self)
        self.bridge = FakeBridge("sess", 42, self.cfg.bridge_info_path, {"gwp_test_smoke"})
        self.addCleanup(self.bridge.close)

    def client(self, token=None):
        info = BridgeInfo(port=self.bridge.port, token=token or self.bridge.token, session="sess", pid=42, protocol=1)
        return BridgeClient(info, 1 << 20)

    def test_hello_then_commands(self):
        c = self.client()
        hello = c.connect(1.0)
        self.assertEqual(hello["pid"], 42)
        self.assertEqual(self.bridge.requests[0]["args"]["token"], self.bridge.token)
        self.assertEqual(self.bridge.requests[0]["args"]["protocol"], 1)
        self.assertEqual(c.request("status", None, 1.0)["state"], "main_menu")
        c.close()

    def test_wrong_token_is_unauthorized(self):
        c = self.client(token="x" * 32)
        with self.assertRaises(BridgeError) as ctx:
            c.connect(1.0)
        self.assertEqual(ctx.exception.code, "unauthorized")
        self.assertFalse(c.connected)

    def test_command_error_keeps_connection(self):
        c = self.client()
        c.connect(1.0)
        with self.assertRaises(BridgeError) as ctx:
            c.request("console", {"command": "nope"}, 1.0)
        self.assertEqual(ctx.exception.code, "unknown_command")
        self.assertTrue(c.connected)
        self.assertEqual(c.request("status", None, 1.0)["state"], "main_menu")
        c.close()

    def test_timeout(self):
        self.bridge.hang_on = {"advance"}
        c = self.client()
        c.connect(1.0)
        with self.assertRaises(BridgeError) as ctx:
            c.request("advance", {"hours": 1}, 0.2)
        self.assertEqual(ctx.exception.code, "timeout")
        self.assertFalse(c.connected)

    def test_mismatched_reply_id_is_protocol_error(self):
        self.bridge.bad_reply_on = {"status"}
        c = self.client()
        c.connect(1.0)
        with self.assertRaises(BridgeError) as ctx:
            c.request("status", None, 1.0)
        self.assertEqual(ctx.exception.code, "protocol_error")

    def test_request_ids_increase(self):
        c = self.client()
        c.connect(1.0)
        c.request("status", None, 1.0)
        ids = [r["id"] for r in self.bridge.requests]
        self.assertEqual(ids, sorted(set(ids)))
        c.close()


class RawServerTests(unittest.TestCase):
    """用最小的手写服务器检查畸形回复与超长行。"""

    def serve(self, payload: bytes):
        srv = socket.socket()
        srv.bind(("127.0.0.1", 0))
        srv.listen(1)
        self.addCleanup(srv.close)

        def run():
            conn, _ = srv.accept()
            with conn:
                conn.recv(65536)
                conn.sendall(payload)
                conn.recv(10)

        threading.Thread(target=run, daemon=True).start()
        return BridgeClient(BridgeInfo(srv.getsockname()[1], "t" * 32, "s", 1, 1), 1024)

    def test_invalid_json(self):
        c = self.serve(b"not json\n")
        with self.assertRaises(BridgeError) as ctx:
            c.connect(1.0)
        self.assertEqual(ctx.exception.code, "protocol_error")

    def test_oversized_line(self):
        c = self.serve(b"x" * 4096)
        with self.assertRaises(BridgeError) as ctx:
            c.connect(1.0)
        self.assertEqual(ctx.exception.code, "protocol_error")
        self.assertFalse(c.connected)

    def test_wrong_protocol_version(self):
        c = self.serve(b'{"id": 1, "ok": true, "result": {"protocol": 2}}\n')
        with self.assertRaises(BridgeError) as ctx:
            c.connect(1.0)
        self.assertEqual(ctx.exception.code, "protocol_error")


if __name__ == "__main__":
    unittest.main()
