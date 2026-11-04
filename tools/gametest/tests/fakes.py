"""测试替身：假的 TCP 桥接服务器（按 PROTOCOL.md 实现服务端）、假进程与假主机、临时游戏/仓库目录。"""

from __future__ import annotations

import json
import secrets
import socket
import sys
import tempfile
import threading
import time
from pathlib import Path

GAMETEST_DIR = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(GAMETEST_DIR))

from gametest.config import Config  # noqa: E402
from gametest.host import ProcInfo  # noqa: E402

DLL_BYTES = b"MZ fake greywarden dll"
BRIDGE_DLL_BYTES = b"MZ fake bridge dll"


class FakeBridge:
    """在 127.0.0.1 随机端口监听，一行一个 JSON。行为可由测试调整。"""

    def __init__(self, session: str, pid: int, info_path: Path, saves: set[str]):
        self.session = session
        self.pid = pid
        self.info_path = info_path
        self.token = secrets.token_hex(16)
        self.saves = set(saves)
        self.state = "starting"
        self.menu_after_status = 1   # 第几次 status 之后进入主菜单
        self.campaign_hours = 1000.0
        self.sentinels = {"CaseSettle": {"passes": 3, "triggers": 0}}
        self.requests: list[dict] = []
        self.hang_on: set[str] = set()      # 收到这些命令就不回复
        self.ignore_quit = False
        self.on_quit = None                 # 回调：模拟游戏退出
        self.bad_reply_on: set[str] = set()  # 回复错误 id
        self._status_calls = 0
        self._sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._sock.bind(("127.0.0.1", 0))
        self._sock.listen(4)
        self.port = self._sock.getsockname()[1]
        self._closed = False
        self._conns: list[socket.socket] = []
        threading.Thread(target=self._accept_loop, daemon=True).start()

    def write_info(self, **override) -> None:
        data = {"protocol": 1, "host": "127.0.0.1", "port": self.port, "token": self.token,
                "session": self.session, "pid": self.pid}
        data.update(override)
        self.info_path.parent.mkdir(parents=True, exist_ok=True)
        self.info_path.write_text(json.dumps(data), encoding="utf-8")

    def close(self) -> None:
        self._closed = True
        for c in self._conns:
            try:
                c.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass
            c.close()
        self._sock.close()

    def _accept_loop(self) -> None:
        while not self._closed:
            try:
                conn, _ = self._sock.accept()
            except OSError:
                return
            self._conns.append(conn)
            threading.Thread(target=self._serve, args=(conn,), daemon=True).start()

    def _serve(self, conn: socket.socket) -> None:
        authed = False
        buf = b""
        with conn:
            while not self._closed:
                try:
                    chunk = conn.recv(65536)
                except OSError:
                    return
                if not chunk:
                    return
                buf += chunk
                while b"\n" in buf:
                    line, _, buf = buf.partition(b"\n")
                    req = json.loads(line)
                    self.requests.append(req)
                    cmd, args, rid = req["cmd"], req.get("args", {}), req["id"]
                    if cmd in self.hang_on:
                        continue
                    if not authed and cmd != "hello":
                        self._send(conn, {"id": rid, "ok": False, "error": {"code": "unauthorized", "message": "先 hello"}})
                        return
                    reply = self._handle(cmd, args)
                    if cmd == "hello" and reply.get("ok"):
                        authed = True
                    reply["id"] = rid + 1000 if cmd in self.bad_reply_on else rid
                    self._send(conn, reply)
                    if cmd == "hello" and not reply.get("ok"):
                        return
                    if cmd == "quit" and not self.ignore_quit and self.on_quit:
                        self.on_quit()
                        return

    def _send(self, conn, obj) -> None:
        try:
            conn.sendall((json.dumps(obj, ensure_ascii=False) + "\n").encode("utf-8"))
        except OSError:
            pass

    def _handle(self, cmd: str, args: dict) -> dict:
        def ok(result):
            return {"ok": True, "result": result}

        def err(code, msg):
            return {"ok": False, "error": {"code": code, "message": msg}}

        if cmd == "hello":
            if args.get("token") != self.token or args.get("session") != self.session:
                return err("unauthorized", "令牌或会话不符")
            return ok({"protocol": 1, "pid": self.pid, "bridge_version": "test", "game_version": "v1.4.8"})
        if cmd == "status":
            self._status_calls += 1
            if self.state == "starting" and self._status_calls >= self.menu_after_status:
                self.state = "main_menu"
            res = {"state": self.state}
            if self.state == "campaign":
                res["campaign_time"] = {"hours": self.campaign_hours}
            return ok(res)
        if cmd == "load_save":
            name = args.get("name", "")
            if not name.startswith("gwp_test_"):
                return err("refused", "非测试存档")
            if name not in self.saves:
                return err("not_found", f"没有存档 {name}")
            if self.state not in ("main_menu", "campaign"):
                return err("invalid_state", self.state)
            self.state = "campaign"
            return ok({"name": name})
        if cmd == "save":
            if args["name"] in self.saves and not args.get("overwrite"):
                return err("exists", "存档已存在")
            self.saves.add(args["name"])
            return ok({"name": args["name"]})
        if cmd == "console":
            if args["command"].startswith("nope"):
                return err("unknown_command", "没有这个命令")
            return ok({"output": f"ran: {args['command']}"})
        if cmd == "advance":
            if self.state != "campaign":
                return err("invalid_state", self.state)
            before = self.campaign_hours
            self.campaign_hours += args["hours"]
            self.sentinels["CaseSettle"]["passes"] += 1
            return ok({"hours_requested": args["hours"], "hours_advanced": args["hours"],
                       "campaign_time_before": {"hours": before}, "campaign_time_after": {"hours": self.campaign_hours}})
        if cmd == "query":
            kind = args["kind"]
            if kind == "sentinels":
                return ok({"sentinels": json.loads(json.dumps(self.sentinels))})
            if kind == "cases":
                return ok({"cases": [{"id": "case_1", "state": "open"}]})
            if kind == "kinds":
                return ok({"kinds": ["cases", "envoys", "case_parties", "party", "sentinels", "kinds"]})
            return err("unknown_kind", kind)
        if cmd == "quit":
            return ok({"quitting": True})
        return err("unknown_command", cmd)


class FakeProc:
    def __init__(self, pid: int):
        self.pid = pid
        self.exit_code: int | None = None
        self.killed = False
        self.bridge: FakeBridge | None = None

    def poll(self):
        return self.exit_code

    def wait(self, timeout):
        deadline = time.monotonic() + timeout
        while self.exit_code is None and time.monotonic() < deadline:
            time.sleep(0.01)
        return self.exit_code

    def kill(self):
        self.killed = True
        self.exit(-9)

    def exit(self, code: int):
        if self.exit_code is None:
            self.exit_code = code
        if self.bridge:
            self.bridge.close()


class FakeHost:
    """mode：normal（正常启动桥接）、no_bridge（进程活着但不写 bridge.json）、crash（立刻退出）。"""

    def __init__(self, cfg: Config, saves=("gwp_test_smoke",)):
        self.cfg = cfg
        self.mode = "normal"
        self.extra_processes: list[ProcInfo] = []
        self.locked: set[Path] = set()
        self.launches: list[dict] = []
        self.procs: list[FakeProc] = []
        self.saves = set(saves)
        self.bridge_setup = None  # 回调：在桥接创建后、写 bridge.json 前调整它
        self._next_pid = 4000
        self.killed_pids: list[int] = []

    def list_processes(self):
        out = list(self.extra_processes)
        out += [ProcInfo("Bannerlord.exe", p.pid) for p in self.procs if p.exit_code is None]
        return out

    def is_locked(self, path):
        return Path(path) in self.locked

    def launch(self, argv, cwd, env):
        self._next_pid += 1
        proc = FakeProc(self._next_pid)
        self.procs.append(proc)
        self.launches.append({"argv": argv, "cwd": cwd, "session": env.get("GWP_TEST_BRIDGE_SESSION")})
        if self.mode == "crash":
            proc.exit_code = 3
        elif self.mode == "normal":
            bridge = FakeBridge(env["GWP_TEST_BRIDGE_SESSION"], proc.pid, self.cfg.bridge_info_path, self.saves)
            bridge.on_quit = lambda: proc.exit(0)
            proc.bridge = bridge
            if self.bridge_setup:
                self.bridge_setup(bridge)
            bridge.write_info()
        return proc

    def process_name(self, pid):
        for p in self.list_processes():
            if p.pid == pid:
                return p.name
        return None

    def kill_pid(self, pid):
        self.killed_pids.append(pid)
        for p in self.procs:
            if p.pid == pid:
                p.kill()


def cleanup(ctl, host: FakeHost) -> None:
    """结束所有假进程并关掉控制器手里的连接，避免测试间残留套接字。"""
    for proc in host.procs:
        proc.exit(0)
    ctl._clear_session()


def make_env(test) -> Config:
    """建临时的游戏目录与仓库目录，构建与 live 一致，所有栅栏默认都能通过。"""
    tmp = Path(tempfile.mkdtemp(prefix="gwp_gametest_"))
    test.addCleanup(_rmtree, tmp)
    game, repo = tmp / "game", tmp / "repo"
    exe = game / "bin" / "Win64_Shipping_Client" / "Bannerlord.exe"
    exe.parent.mkdir(parents=True)
    exe.write_bytes(b"exe")
    for mod in ("Native", "SandBoxCore", "Sandbox", "StoryMode", "CustomBattle", "GreyWarden", "GreyWardenTestBridge"):
        d = game / "Modules" / mod
        d.mkdir(parents=True)
        (d / "SubModule.xml").write_text(f'<Module><Id value="{mod}"/></Module>', encoding="utf-8")
    # Harmony 放在创意工坊目录，验证工坊查找
    ws = tmp / "workshop" / "2859188632"
    ws.mkdir(parents=True)
    (ws / "SubModule.xml").write_text('<Module><Id value="Bannerlord.Harmony"/></Module>', encoding="utf-8")
    live_bin = game / "Modules" / "GreyWarden" / "bin" / "Win64_Shipping_Client"
    live_bin.mkdir(parents=True)
    (live_bin / "GreyWardenPolicePurity.dll").write_bytes(DLL_BYTES)
    bridge_bin = game / "Modules" / "GreyWardenTestBridge" / "bin" / "Win64_Shipping_Client"
    bridge_bin.mkdir(parents=True)
    (bridge_bin / "GreyWardenTestBridge.dll").write_bytes(BRIDGE_DLL_BYTES)
    obj = repo / "GreyWardenPolicePurity" / "obj" / "Debug"
    obj.mkdir(parents=True)
    (obj / "GreyWardenPolicePurity.dll").write_bytes(DLL_BYTES)
    bobj = repo / "tools" / "gametest" / "bridge" / "GreyWardenTestBridge" / "obj" / "Debug"
    bobj.mkdir(parents=True)
    (bobj / "GreyWardenTestBridge.dll").write_bytes(BRIDGE_DLL_BYTES)
    module = repo / "GreyWardenPolicePurity" / "_Module"
    (module / "ModuleData").mkdir(parents=True)
    (module / "SubModule.xml").write_text("<Module/>", encoding="utf-8")
    (module / "ModuleData" / "a.xml").write_text("<a/>", encoding="utf-8")
    (module / "Assets").mkdir()
    (module / "Assets" / "editor_only.bin").write_bytes(b"x")  # 编辑器目录不参与镜像比对
    live = game / "Modules" / "GreyWarden"
    (live / "ModuleData").mkdir()
    # GreyWarden 的 SubModule.xml 已在上面写过；改成与仓库一致
    (live / "SubModule.xml").write_text("<Module/>", encoding="utf-8")
    (live / "ModuleData" / "a.xml").write_text("<a/>", encoding="utf-8")
    saves = tmp / "saves"
    saves.mkdir()
    (saves / "gwp_test_smoke.sav").write_bytes(b"sav")
    (saves / "gwp_test_other.sav").write_bytes(b"sav")
    scen = tmp / "scenarios"
    scen.mkdir()
    return Config(game_dir=game, repo_root=repo, saves_dir=saves, logs_dir=tmp / "logs",
                  workshop_dir=tmp / "workshop", scenarios_dir=scen, runtime_dir=tmp / "runtime",
                  bridge_wait_s=2.0, main_menu_wait_s=2.0, load_wait_s=2.0, command_timeout_s=1.0,
                  advance_s_per_hour=0.0, stop_wait_s=1.0, poll_interval_s=0.02)


def _rmtree(path: Path) -> None:
    import shutil
    shutil.rmtree(path, ignore_errors=True)
