"""游戏会话控制器：启动、读档、执行命令、停止，所有失败都收敛成结构化结果。

每个公开方法返回 dict：成功 `{"ok": true, ...}`，失败 `{"ok": false, "error": {"code", "message"}, ...}`，
不向 MCP 层抛异常。命令超时或桥接断开视为游戏卡死：立刻杀进程，结果里带 `killed: true` 与证据。
"""

from __future__ import annotations

import json
import os
import secrets
import threading
import time
from pathlib import Path

from . import fences
from .config import Config
from .host import Host, ProcHandle
from .protocol import BridgeClient, BridgeError, BridgeInfo, read_bridge_info

SESSION_ENV = "GWP_TEST_BRIDGE_SESSION"


def _err(code: str, message: str, **extra) -> dict:
    out = {"ok": False, "error": {"code": code, "message": message}}
    out.update(extra)
    return out


class GameController:
    def __init__(self, cfg: Config, host: Host):
        self.cfg = cfg
        self.host = host
        self._lock = threading.RLock()
        # stop() 拿不到 _lock 时会和卡住的命令线程同时收尾；Windows 上两边同时删会话文件会撞上
        # 「删除挂起」报 PermissionError（本机 test_stop_while_another_command_hangs 偶发复现 2026-11-04），所以单独串行。
        self._clear_lock = threading.Lock()
        self._proc: ProcHandle | None = None
        self._pid: int | None = None
        self._session: str | None = None
        self._client: BridgeClient | None = None
        self._save: str | None = None
        self._modules: list[str] = []
        self._adopt_previous_session()

    # ---------- 会话状态 ----------

    def _alive(self) -> bool:
        if self._proc is not None:
            return self._proc.poll() is None
        if self._pid is not None:
            name = self.host.process_name(self._pid)
            return name is not None and fences.GAME_PROCESS_RE.search(name) is not None
        return False

    def _write_session_file(self) -> None:
        self.cfg.runtime_dir.mkdir(parents=True, exist_ok=True)
        self.cfg.session_file.write_text(json.dumps({
            "pid": self._pid, "session": self._session, "save": self._save, "modules": self._modules,
            "started_at": time.strftime("%Y-%m-%dT%H:%M:%S"),
        }, ensure_ascii=False, indent=2), encoding="utf-8")

    def _clear_session(self) -> None:
        with self._clear_lock:
            if self._client is not None:
                self._client.close()
            self._proc = None
            self._pid = None
            self._session = None
            self._client = None
            self._save = None
            self._modules = []
            # 强杀路径可能与卡住的命令线程同时收尾，文件已被另一边删掉不算错。
            self.cfg.session_file.unlink(missing_ok=True)

    def _adopt_previous_session(self) -> None:
        """MCP 服务重启后接回自己上次启动的测试游戏。

        只接回会话令牌对得上的进程：会话号经环境变量传给游戏，只有测试桥接模组会把它写回 bridge.json，
        所以用户自己开的游戏永远不会被接管或杀掉。
        """
        path = self.cfg.session_file
        if not path.is_file():
            return
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            # 依据：会话文件是本工具写的；写到一半被打断时内容不完整，按无会话处理并清掉。
            path.unlink()
            return
        pid, session = data.get("pid"), data.get("session")
        self._pid, self._session = pid, session
        info = None
        if isinstance(pid, int) and isinstance(session, str) and self._alive():
            info = read_bridge_info(self.cfg.bridge_info_path, session)
        if info is None or info.pid != pid:
            self._clear_session()
            return
        self._save = data.get("save")
        self._modules = list(data.get("modules") or [])

    # ---------- 证据 ----------

    def _evidence(self, pid: int | None, exit_code: int | None = None) -> dict:
        ev: dict = {}
        if exit_code is not None:
            ev["exit_code"] = exit_code
        if pid is not None and self.cfg.logs_dir.is_dir():
            logs = {}
            for log in sorted(self.cfg.logs_dir.glob(f"rgl_log*_{pid}.txt")):
                lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
                logs[log.name] = "\n".join(lines[-40:])[-self.cfg.evidence_chars:]
            if logs:
                ev["rgl_logs"] = logs
        return ev

    # ---------- 杀进程 ----------

    def _kill(self) -> dict:
        """强杀本工具启动或接回的游戏。只杀自己的进程。"""
        pid = self._pid
        exit_code = None
        if self._proc is not None:
            self._proc.kill()
            exit_code = self._proc.wait(10.0)
        elif pid is not None and self._alive():
            self.host.kill_pid(pid)
            deadline = time.monotonic() + 10.0
            while self._alive() and time.monotonic() < deadline:
                time.sleep(self.cfg.poll_interval_s)
        evidence = self._evidence(pid, exit_code)
        still = self._alive()
        if not still:
            self._clear_session()
        return {"pid": pid, "killed": not still, "evidence": evidence}

    def _fail_and_kill(self, stage: str, error: BridgeError | dict) -> dict:
        err = error.as_dict() if isinstance(error, BridgeError) else error
        killed = self._kill()
        return {"ok": False, "stage": stage, "error": err, "killed": killed["killed"], "pid": killed["pid"],
                "evidence": killed["evidence"]}

    # ---------- 桥接调用 ----------

    def _ensure_client(self) -> BridgeClient:
        if self._client is not None and self._client.connected:
            return self._client
        info = read_bridge_info(self.cfg.bridge_info_path, self._session)
        if info is None or info.pid != self._pid:
            raise BridgeError("bridge_missing", "找不到本会话的 bridge.json，桥接模组可能没加载")
        client = BridgeClient(info, self.cfg.max_line_bytes)
        client.connect(self.cfg.command_timeout_s)
        self._client = client
        return client

    def _call(self, cmd: str, args: dict | None = None, timeout: float | None = None) -> dict:
        """在运行中的会话上执行一条桥接命令。超时或断线即杀进程。"""
        with self._lock:
            if not self._alive():
                self._clear_session()
                return _err("not_running", "没有运行中的测试游戏，先 game_start")
            try:
                client = self._ensure_client()
                result = client.request(cmd, args, timeout or self.cfg.command_timeout_s)
            except BridgeError as exc:
                if exc.code in ("timeout", "disconnected", "protocol_error", "bridge_missing") or not self._alive():
                    return self._fail_and_kill(cmd, exc)
                return {"ok": False, "stage": cmd, "error": exc.as_dict()}
            except OSError as exc:
                # 依据：进程还在但桥接端口连不上，说明桥接已失效，游戏处于无法自动控制的状态。
                return self._fail_and_kill(cmd, {"code": "connect_failed", "message": str(exc)})
            return {"ok": True, "result": result}

    # ---------- 公开操作 ----------

    def status(self) -> dict:
        if not self._alive():
            with self._lock:
                self._clear_session()
                mods, error = fences.module_list(self.cfg, None)
                report = fences.check_launch(self.cfg, self.host, mods) if not error else None
            return {"ok": True, "running": False,
                    "can_start": bool(report and report.ok),
                    "fences": report.as_dict() if report else {"ok": False, "blockers": [error]}}
        res = self._call("status")
        if not res["ok"]:
            return res
        return {"ok": True, "running": True, "pid": self._pid, "save": self._save, "modules": self._modules,
                "result": res["result"]}

    def is_running(self) -> bool:
        return self._alive()

    def bridge_status(self) -> dict:
        return self._call("status")

    def start(self, save: str, modules: list[str] | None = None) -> dict:
        with self._lock:
            if self._alive():
                return _err("already_running", "测试游戏已在运行，先 game_stop", pid=self._pid)
            self._clear_session()
            reason = fences.check_save_name(save)
            if reason:
                return _err("refused", reason)
            mods, reason = fences.module_list(self.cfg, modules)
            if reason:
                return _err("refused", reason)
            reason = fences.check_save_exists(self.cfg, save)
            if reason:
                return _err("not_found", reason)
            report = fences.check_launch(self.cfg, self.host, mods)
            if not report.ok:
                return _err("refused", "启动前安全检查未通过：" + "；".join(report.blockers), fences=report.as_dict())

            started = time.monotonic()
            session = secrets.token_hex(16)
            env = dict(os.environ)
            env[SESSION_ENV] = session
            argv = fences.launch_argv(self.cfg, mods)
            proc = self.host.launch(argv, self.cfg.exe_path.parent, env)
            self._proc, self._pid, self._session = proc, proc.pid, session
            self._save, self._modules = save, mods
            self._write_session_file()
            timings: dict[str, float] = {}

            # 1. 等桥接写出本会话的 bridge.json
            info: BridgeInfo | None = None
            deadline = time.monotonic() + self.cfg.bridge_wait_s
            while info is None:
                code = proc.poll()
                if code is not None:
                    ev = self._evidence(proc.pid, code)
                    self._clear_session()
                    return {"ok": False, "stage": "wait_bridge", "killed": False, "evidence": ev,
                            "error": {"code": "exited", "message": f"游戏在桥接就绪前退出，退出码 {code}"}}
                try:
                    info = read_bridge_info(self.cfg.bridge_info_path, session)
                except BridgeError as exc:
                    return self._fail_and_kill("wait_bridge", exc)
                if info is None:
                    if time.monotonic() >= deadline:
                        return self._fail_and_kill("wait_bridge", {"code": "timeout", "message":
                                                   f"{self.cfg.bridge_wait_s:g} 秒内没等到 bridge.json"})
                    time.sleep(self.cfg.poll_interval_s)
            if info.pid != proc.pid:
                return self._fail_and_kill("wait_bridge", {"code": "protocol_error", "message":
                                           f"bridge.json 的 pid {info.pid} 不是本次启动的进程 {proc.pid}"})
            timings["bridge_ready_s"] = round(time.monotonic() - started, 2)

            # 2. 握手
            client = BridgeClient(info, self.cfg.max_line_bytes)
            try:
                hello = client.connect(self.cfg.command_timeout_s)
            except BridgeError as exc:
                return self._fail_and_kill("hello", exc)
            except OSError as exc:
                # 依据：bridge.json 已写出但监听尚未就绪或已关闭时，connect 被拒绝。
                return self._fail_and_kill("hello", {"code": "connect_failed", "message": str(exc)})
            self._client = client

            # 3. 等主菜单
            deadline = time.monotonic() + self.cfg.main_menu_wait_s
            state = None
            while state != "main_menu":
                try:
                    st = client.request("status", None, self.cfg.command_timeout_s)
                except BridgeError as exc:
                    return self._fail_and_kill("wait_main_menu", exc)
                state = st.get("state") if isinstance(st, dict) else None
                if state == "main_menu":
                    break
                if time.monotonic() >= deadline:
                    return self._fail_and_kill("wait_main_menu", {"code": "timeout", "message":
                                               f"{self.cfg.main_menu_wait_s:g} 秒内没到主菜单，最后状态 {state!r}"})
                time.sleep(self.cfg.poll_interval_s)
            timings["main_menu_s"] = round(time.monotonic() - started, 2)

            # 4. 读档：桥接在战役就绪后才回复
            try:
                loaded = client.request("load_save", {"name": save}, self.cfg.load_wait_s)
                st = client.request("status", None, self.cfg.command_timeout_s)
            except BridgeError as exc:
                return self._fail_and_kill("load_save", exc)
            if not isinstance(st, dict) or st.get("state") != "campaign":
                return self._fail_and_kill("load_save", {"code": "invalid_state", "message":
                                           f"读档后状态不是 campaign：{st!r}"})
            timings["campaign_ready_s"] = round(time.monotonic() - started, 2)
            return {"ok": True, "pid": proc.pid, "save": save, "modules": mods, "hello": hello,
                    "load": loaded, "result": st, "timings": timings, "warnings": report.warnings}

    def stop(self) -> dict:
        """先请桥接正常退出，等不到就强杀。

        另一条命令卡在锁里时不排队：直接强杀，卡住的命令会因断线收尾。
        """
        if not self._lock.acquire(timeout=2.0):
            killed = self._kill()
            return {"ok": killed["killed"], "method": "kill", "reason": "另一条命令正在执行，直接强杀", **killed}
        try:
            if not self._alive():
                self._clear_session()
                return {"ok": True, "was_running": False}
            pid = self._pid
            quit_error = None
            try:
                self._ensure_client().request("quit", None, 10.0)
            except (BridgeError, OSError) as exc:
                # 依据：游戏卡死或桥接没起来时 quit 发不出去或没回复；下面按等待超时强杀收尾。
                quit_error = str(exc)
            deadline = time.monotonic() + self.cfg.stop_wait_s
            while self._alive() and time.monotonic() < deadline:
                time.sleep(self.cfg.poll_interval_s)
            if not self._alive():
                code = self._proc.poll() if self._proc is not None else None
                self._clear_session()
                return {"ok": True, "was_running": True, "method": "quit", "pid": pid, "exit_code": code}
            killed = self._kill()
            return {"ok": killed["killed"], "was_running": True, "method": "kill", "quit_error": quit_error, **killed}
        finally:
            self._lock.release()

    def console(self, command: str) -> dict:
        reason = fences.check_console_command(command)
        if reason:
            return _err("refused", reason)
        return self._call("console", {"command": command.strip()})

    def advance(self, hours: float) -> dict:
        reason = fences.check_hours(hours, self.cfg)
        if reason:
            return _err("refused", reason)
        timeout = self.cfg.command_timeout_s + hours * self.cfg.advance_s_per_hour
        return self._call("advance", {"hours": hours}, timeout)

    def query(self, kind: str, args: dict | None = None) -> dict:
        reason = fences.check_query_kind(kind)
        if reason:
            return _err("refused", reason)
        if args is not None and not isinstance(args, dict):
            return _err("refused", "query 的 args 必须是对象")
        return self._call("query", {"kind": kind, "args": args or {}})

    def load_save(self, name: str) -> dict:
        reason = fences.check_save_name(name)
        if reason:
            return _err("refused", reason)
        res = self._call("load_save", {"name": name}, self.cfg.load_wait_s)
        if res["ok"]:
            self._save = name
            self._write_session_file()
        return res

    def save(self, name: str, overwrite: bool = False) -> dict:
        reason = fences.check_save_name(name)
        if reason:
            return _err("refused", reason)
        return self._call("save", {"name": name, "overwrite": bool(overwrite)})

    def shutdown(self) -> dict:
        """MCP 服务退出时调用：跑完关掉游戏（AGENTS.md「实机」）。"""
        return self.stop()
