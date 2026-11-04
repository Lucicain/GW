"""与操作系统打交道的一层：列进程、判断 DLL 是否被占用、启动和杀进程。

控制器只通过 `Host` 接口用它，单元测试换成假实现，所以 Linux 上也能跑全部测试。
"""

from __future__ import annotations

import csv
import io
import os
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol


@dataclass(frozen=True)
class ProcInfo:
    name: str
    pid: int


class ProcHandle(Protocol):
    pid: int

    def poll(self) -> int | None: ...

    def wait(self, timeout: float) -> int | None: ...

    def kill(self) -> None: ...


class Host(Protocol):
    def list_processes(self) -> list[ProcInfo]: ...

    def is_locked(self, path: Path) -> bool: ...

    def launch(self, argv: list[str], cwd: Path, env: dict[str, str]) -> ProcHandle: ...

    def process_name(self, pid: int) -> str | None: ...

    def kill_pid(self, pid: int) -> None: ...


class _PopenHandle:
    def __init__(self, popen: subprocess.Popen):
        self._popen = popen
        self.pid = popen.pid

    def poll(self) -> int | None:
        return self._popen.poll()

    def wait(self, timeout: float) -> int | None:
        try:
            return self._popen.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            return None

    def kill(self) -> None:
        if self._popen.poll() is not None:
            return
        if sys.platform == "win32":
            # /T 连同子进程（Watchdog 等）一起杀。
            subprocess.run(["taskkill", "/PID", str(self.pid), "/T", "/F"],
                           capture_output=True, check=False)
        if self._popen.poll() is None:
            self._popen.kill()


class RealHost:
    """真实系统。Windows 用 tasklist/taskkill，其他系统用 ps/kill（只为能在别处冒烟）。"""

    def list_processes(self) -> list[ProcInfo]:
        if sys.platform == "win32":
            out = subprocess.run(["tasklist", "/FO", "CSV", "/NH"], capture_output=True,
                                 text=True, check=True, errors="replace").stdout
            procs = []
            for row in csv.reader(io.StringIO(out)):
                if len(row) >= 2 and row[1].isdigit():
                    procs.append(ProcInfo(row[0], int(row[1])))
            return procs
        out = subprocess.run(["ps", "-A", "-o", "pid=,comm="], capture_output=True,
                             text=True, check=True).stdout
        procs = []
        for line in out.splitlines():
            parts = line.strip().split(None, 1)
            if len(parts) == 2 and parts[0].isdigit():
                procs.append(ProcInfo(os.path.basename(parts[1]), int(parts[0])))
        return procs

    def is_locked(self, path: Path) -> bool:
        # 游戏加载模组 DLL 后只允许共享读；以读写方式打开会因共享冲突失败。只打开不写，文件不变。
        try:
            with open(path, "r+b"):
                return False
        except PermissionError:
            return True

    def launch(self, argv: list[str], cwd: Path, env: dict[str, str]) -> ProcHandle:
        flags = 0
        if sys.platform == "win32":
            flags = subprocess.CREATE_NEW_PROCESS_GROUP  # type: ignore[attr-defined]
        popen = subprocess.Popen(argv, cwd=str(cwd), env=env, creationflags=flags,
                                 stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                 stderr=subprocess.DEVNULL)
        return _PopenHandle(popen)

    def process_name(self, pid: int) -> str | None:
        for proc in self.list_processes():
            if proc.pid == pid:
                return proc.name
        return None

    def kill_pid(self, pid: int) -> None:
        if sys.platform == "win32":
            subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], capture_output=True, check=False)
        else:
            import signal
            os.kill(pid, signal.SIGKILL)
