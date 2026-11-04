"""桥接协议的客户端一侧。定稿见 tools/gametest/PROTOCOL.md。

一行一个 UTF-8 JSON；一条连接上同一时间只有一个未完成请求。
"""

from __future__ import annotations

import json
import socket
import time
from dataclasses import dataclass
from pathlib import Path

from . import PROTOCOL_VERSION

LOOPBACK = "127.0.0.1"


class BridgeError(Exception):
    """桥接层失败的统一基类。code 与 PROTOCOL.md 的错误码一致，客户端自有的另加。"""

    def __init__(self, code: str, message: str, data: object = None):
        super().__init__(f"{code}: {message}")
        self.code = code
        self.message = message
        self.data = data

    def as_dict(self) -> dict:
        out = {"code": self.code, "message": self.message}
        if self.data is not None:
            out["data"] = self.data
        return out


class BridgeTimeout(BridgeError):
    def __init__(self, message: str):
        super().__init__("timeout", message)


class BridgeProtocolError(BridgeError):
    def __init__(self, message: str):
        super().__init__("protocol_error", message)


@dataclass(frozen=True)
class BridgeInfo:
    port: int
    token: str
    session: str
    pid: int
    protocol: int


def read_bridge_info(path: Path, expected_session: str | None) -> BridgeInfo | None:
    """读 bridge.json。文件不在、还没写完、或属于别的会话（旧文件）都返回 None。

    host 字段若存在必须是 127.0.0.1；不是就是协议违规，直接报错而不是忽略。
    """
    if not path.is_file():
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        # 依据：模组正在写文件时会读到半截内容；下一轮轮询再读。
        return None
    if not isinstance(data, dict):
        return None
    if expected_session is not None and data.get("session") != expected_session:
        return None
    host = data.get("host", LOOPBACK)
    if host != LOOPBACK:
        raise BridgeProtocolError(f"bridge.json 的 host 必须是 {LOOPBACK}：{host!r}")
    port, token, pid = data.get("port"), data.get("token"), data.get("pid")
    if not (isinstance(port, int) and 0 < port < 65536):
        raise BridgeProtocolError(f"bridge.json 的 port 不合法：{port!r}")
    if not (isinstance(token, str) and len(token) >= 16):
        raise BridgeProtocolError("bridge.json 的 token 缺失或少于 16 个字符")
    if not isinstance(pid, int):
        raise BridgeProtocolError(f"bridge.json 的 pid 不合法：{pid!r}")
    return BridgeInfo(port=port, token=token, session=str(data.get("session", "")), pid=pid,
                      protocol=int(data.get("protocol", 0)))


class BridgeClient:
    def __init__(self, info: BridgeInfo, max_line_bytes: int):
        self.info = info
        self.max_line_bytes = max_line_bytes
        self._sock: socket.socket | None = None
        self._buf = b""
        self._next_id = 1
        self.hello: dict | None = None

    @property
    def connected(self) -> bool:
        return self._sock is not None

    def connect(self, timeout: float) -> dict:
        """连接并握手，返回 hello 的结果。只连 127.0.0.1。"""
        self._sock = socket.create_connection((LOOPBACK, self.info.port), timeout=timeout)
        self._buf = b""
        result = self.request("hello", {"token": self.info.token, "session": self.info.session,
                                        "protocol": PROTOCOL_VERSION}, timeout)
        if not isinstance(result, dict) or result.get("protocol") != PROTOCOL_VERSION:
            self.close()
            raise BridgeProtocolError(f"桥接协议版本不符：期望 {PROTOCOL_VERSION}，收到 {result!r}")
        self.hello = result
        return result

    def close(self) -> None:
        if self._sock is not None:
            self._sock.close()
        self._sock = None
        self._buf = b""

    def request(self, cmd: str, args: dict | None, timeout: float):
        if self._sock is None:
            raise BridgeError("not_connected", "尚未连接桥接")
        req_id = self._next_id
        self._next_id += 1
        line = json.dumps({"id": req_id, "cmd": cmd, "args": args or {}}, ensure_ascii=False) + "\n"
        deadline = time.monotonic() + timeout
        try:
            self._sock.settimeout(timeout)
            self._sock.sendall(line.encode("utf-8"))
            raw = self._read_line(deadline)
        except BridgeError:
            self.close()
            raise
        except (socket.timeout, TimeoutError):
            self.close()
            raise BridgeTimeout(f"{cmd} 在 {timeout:g} 秒内没有回复")
        except OSError as exc:
            # 依据：游戏崩溃或退出时连接被对端重置，属于桥接断开，交给调用方收尾。
            self.close()
            raise BridgeError("disconnected", f"{cmd} 时连接断开：{exc}")
        try:
            reply = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            self.close()
            raise BridgeProtocolError(f"回复不是合法 JSON：{exc}")
        if not isinstance(reply, dict) or reply.get("id") != req_id or not isinstance(reply.get("ok"), bool):
            self.close()
            raise BridgeProtocolError(f"回复格式不对或 id 不匹配（期望 {req_id}）：{str(reply)[:200]}")
        if reply["ok"]:
            return reply.get("result")
        err = reply.get("error")
        if not isinstance(err, dict):
            raise BridgeProtocolError(f"ok=false 但没有 error 对象：{str(reply)[:200]}")
        if err.get("code") == "unauthorized":
            self.close()
        raise BridgeError(str(err.get("code", "error")), str(err.get("message", "")), err.get("data"))

    def _read_line(self, deadline: float) -> bytes:
        assert self._sock is not None
        while b"\n" not in self._buf:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise socket.timeout()
            self._sock.settimeout(remaining)
            chunk = self._sock.recv(65536)
            if not chunk:
                raise ConnectionResetError("桥接关闭了连接")
            self._buf += chunk
            if len(self._buf) > self.max_line_bytes and b"\n" not in self._buf:
                raise BridgeProtocolError(f"单行回复超过 {self.max_line_bytes} 字节")
        line, _, self._buf = self._buf.partition(b"\n")
        return line.rstrip(b"\r")
