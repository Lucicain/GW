"""硬性安全栅栏。

AGENTS.md「实机」（用户裁定 2026-11-04）：只在用户没在玩时启动游戏、只读写 `gwp_test_` 存档、
不操控屏幕键鼠；「部署与交付」：live 与仓库有差异不得开始实机测试。这里每一条都返回拒绝原因，
不抛异常，调用方把原因原样交给 AI。
"""

from __future__ import annotations

import hashlib
import os
import re
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

from .config import Config, REQUIRED_TAIL_MODULES
from .host import Host

SAVE_PREFIX = "gwp_test_"
_SAVE_RE = re.compile(r"^gwp_test_[A-Za-z0-9_-]{1,64}$")
_MODULE_RE = re.compile(r"^[A-Za-z0-9_.-]{1,80}$")
_QUERY_KIND_RE = re.compile(r"^[a-z][a-z0-9_]{0,40}$")
_SCENARIO_RE = re.compile(r"^[A-Za-z0-9_-]{1,64}$")

# build-and-deploy.md：游戏相关进程是 TaleWorlds.MountAndBlade.Launcher、Watchdog、Bannerlord。
# 按名字开头（Bannerlord*、TaleWorlds*）或全名 Watchdog 匹配：子串匹配 Launcher/Watchdog 会误伤
# EpicGamesLauncher、watchdogd 之类无关进程。
GAME_PROCESS_RE = re.compile(r"^(bannerlord|taleworlds)|^watchdog(\.exe)?$", re.IGNORECASE)

# 控制台里与存读档、退出相关的命令一律拒绝：存读档只能走 save/load_save（带名字栅栏），退出走 game_stop。
_CONSOLE_DENY_RE = re.compile(r"(save|load|quit|exit)", re.IGNORECASE)

# _Module 镜像比对的例外，与 tools/Verify-LiveModule.ps1 的 $repoOnly 一致。
_MIRROR_EXCLUDED_TOP = {"assets", "assetsources", "runtimedatacache", "assetpackages", "dsassetpackages", "obj"}


@dataclass
class FenceReport:
    blockers: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    facts: dict = field(default_factory=dict)

    @property
    def ok(self) -> bool:
        return not self.blockers

    def as_dict(self) -> dict:
        return {"ok": self.ok, "blockers": self.blockers, "warnings": self.warnings, "facts": self.facts}


# ---------- 名字类栅栏 ----------

def check_save_name(name: object) -> str | None:
    """返回拒绝原因；合法返回 None。名字不带扩展名，只允许字母数字下划线连字符，杜绝路径穿越。"""
    if not isinstance(name, str) or not name:
        return "存档名不能为空"
    if not name.startswith(SAVE_PREFIX):
        return f"存档名必须以 {SAVE_PREFIX} 开头，拒绝读写其他存档：{name!r}"
    if not _SAVE_RE.match(name):
        return f"存档名只能含字母、数字、下划线、连字符，不带扩展名，前缀后 1～64 个字符：{name!r}"
    return None


def check_console_command(command: object) -> str | None:
    if not isinstance(command, str) or not command.strip():
        return "控制台命令不能为空"
    if "\n" in command or "\r" in command:
        return "控制台命令不能含换行"
    name = command.strip().split(None, 1)[0]
    if _CONSOLE_DENY_RE.search(name):
        return f"控制台命令 {name!r} 涉及存读档或退出，必须改用 save / load_save / game_stop 工具"
    return None


def check_query_kind(kind: object) -> str | None:
    if not isinstance(kind, str) or not _QUERY_KIND_RE.match(kind):
        return f"query 的 kind 只能是小写字母开头的 [a-z0-9_]：{kind!r}"
    return None


def check_scenario_name(name: object) -> str | None:
    if not isinstance(name, str) or not _SCENARIO_RE.match(name):
        return f"场景名只能含字母、数字、下划线、连字符：{name!r}"
    return None


def check_hours(hours: object, cfg: Config) -> str | None:
    if isinstance(hours, bool) or not isinstance(hours, (int, float)):
        return "hours 必须是数字"
    if not 0 < hours <= cfg.max_advance_hours:
        return f"hours 必须在 (0, {cfg.max_advance_hours:g}] 内：{hours}"
    return None


# ---------- 模组列表与启动命令 ----------

def module_list(cfg: Config, extra: list[str] | None) -> tuple[list[str], str | None]:
    """拼出 _MODULES_ 用的有序列表。基础前置在前，额外模组居中，GreyWarden 与桥接永远在最后。"""
    extras = list(cfg.extra_modules) + list(extra or [])
    mods: list[str] = []
    for name in list(cfg.base_modules) + extras + list(REQUIRED_TAIL_MODULES):
        if not isinstance(name, str) or not _MODULE_RE.match(name):
            return [], f"模组名不合法：{name!r}"
        if name not in mods:
            mods.append(name)
    # 额外模组里如果重复写了尾部两个，去重后它们可能跑到前面；强制归位。
    mods = [m for m in mods if m not in REQUIRED_TAIL_MODULES] + list(REQUIRED_TAIL_MODULES)
    return mods, None


def launch_argv(cfg: Config, mods: list[str]) -> list[str]:
    return [str(cfg.exe_path), "/singleplayer", "_MODULES_*" + "*".join(mods) + "*_MODULES_"]


def _module_id(submodule_xml: Path) -> str | None:
    try:
        root = ET.parse(submodule_xml).getroot()
    except ET.ParseError:
        # 依据：工坊目录里可能有不合格的 SubModule.xml，它不是我们要找的模组，跳过即可。
        return None
    node = root.find("Id")
    return node.get("value") if node is not None else None


def find_missing_modules(cfg: Config, mods: list[str]) -> list[str]:
    missing = [m for m in mods if not (cfg.modules_dir / m / "SubModule.xml").is_file()]
    if not missing:
        return []
    workshop = cfg.resolved_workshop_dir
    found: set[str] = set()
    if workshop.is_dir():
        for xml in workshop.glob("*/SubModule.xml"):
            mid = _module_id(xml)
            if mid:
                found.add(mid)
    return [m for m in missing if m not in found]


# ---------- 进程与文件占用 ----------

def running_game_processes(host: Host) -> list[dict]:
    return [{"name": p.name, "pid": p.pid} for p in host.list_processes() if GAME_PROCESS_RE.search(p.name)]


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def newest_build(repo_root: Path, globs: tuple[str, ...]) -> tuple[Path | None, list[str]]:
    candidates = [p for g in globs for p in repo_root.glob(g) if p.is_file()]
    if not candidates:
        return None, []
    candidates.sort(key=lambda p: p.stat().st_mtime, reverse=True)
    return candidates[0], [str(p.relative_to(repo_root)) for p in candidates]


def _compare_dll(report: FenceReport, label: str, cfg: Config, globs: tuple[str, ...], live: Path,
                 hint: str) -> None:
    build, candidates = newest_build(cfg.repo_root, globs)
    if build is None:
        report.blockers.append(f"{label}：仓库里找不到构建产物（{', '.join(globs)}），{hint}")
        return
    if not live.is_file():
        report.blockers.append(f"{label}：live 缺 DLL：{live}")
        return
    repo_hash, live_hash = sha256(build), sha256(live)
    report.facts[label] = {"repo_build": str(build.relative_to(cfg.repo_root)), "repo_sha256": repo_hash,
                           "live": str(live), "live_sha256": live_hash, "candidates": candidates}
    if repo_hash != live_hash:
        report.blockers.append(f"{label}：live DLL 与仓库最新构建哈希不一致（仓库 {repo_hash[:12]}，live {live_hash[:12]}），"
                               "先构建部署再测")


def _mirror_diff(cfg: Config) -> tuple[list[str], list[str], int]:
    """_Module 下每个可部署文件必须在 live 存在且字节一致。先比大小，大小相同再比哈希。"""
    repo, live = cfg.repo_module, cfg.live_module
    missing, differing, count = [], [], 0
    for src in sorted(repo.rglob("*")):
        if not src.is_file():
            continue
        rel = src.relative_to(repo)
        if rel.parts[0].lower() in _MIRROR_EXCLUDED_TOP:
            continue
        count += 1
        dst = live / rel
        if not dst.is_file():
            missing.append(rel.as_posix())
        elif dst.stat().st_size != src.stat().st_size or sha256(dst) != sha256(src):
            differing.append(rel.as_posix())
    return missing, differing, count


def check_launch(cfg: Config, host: Host, mods: list[str]) -> FenceReport:
    """启动前的全部检查。任何一条不过都不启动。"""
    report = FenceReport()

    procs = running_game_processes(host)
    if procs:
        names = ", ".join(f"{p['name']}({p['pid']})" for p in procs)
        report.blockers.append(f"已有游戏相关进程在运行，用户可能在玩，拒绝启动：{names}")

    if not cfg.exe_path.is_file():
        report.blockers.append(f"找不到游戏可执行文件：{cfg.exe_path}")

    for dll in (cfg.live_main_dll, cfg.live_bridge_dll):
        if not dll.is_file():
            continue
        if not os.access(dll, os.W_OK):
            # 只读属性下以读写方式打开也会失败，分不清是否被占用；不猜，按拒绝处理并说清原因。
            report.blockers.append(f"live DLL 带只读属性，无法判断是否被占用：{dll}")
        elif host.is_locked(dll):
            report.blockers.append(f"live DLL 被占用（游戏或编辑器正在用），拒绝启动：{dll}")

    _compare_dll(report, "GreyWarden", cfg, cfg.main_dll_globs, cfg.live_main_dll, "先构建灰袍")
    _compare_dll(report, "GreyWardenTestBridge", cfg, cfg.bridge_dll_globs, cfg.live_bridge_dll,
                 "先构建并部署桥接模组")

    if cfg.repo_module.is_dir() and cfg.live_module.is_dir():
        missing, differing, count = _mirror_diff(cfg)
        report.facts["module_mirror"] = {"files_checked": count, "missing": missing[:20], "differing": differing[:20]}
        if missing or differing:
            report.blockers.append(f"_Module 与 live 不一致：缺 {len(missing)} 个、不同 {len(differing)} 个，"
                                   "按 build-and-deploy.md 先部署再测")
    elif not cfg.live_module.is_dir():
        report.blockers.append(f"找不到 live 模组目录：{cfg.live_module}")

    missing_mods = find_missing_modules(cfg, mods)
    if missing_mods:
        report.blockers.append(f"游戏里找不到这些模组（Modules 与创意工坊都没有）：{', '.join(missing_mods)}")

    build, _ = newest_build(cfg.repo_root, cfg.main_dll_globs)
    if build is not None:
        src_dir = cfg.repo_root / "GreyWardenPolicePurity"
        built = build.stat().st_mtime
        newer = [p.name for p in src_dir.glob("*.cs") if p.stat().st_mtime > built]
        if newer:
            report.warnings.append(f"有 {len(newer)} 个 .cs 比仓库构建新，测的可能不是工作树里的代码：{', '.join(sorted(newer)[:5])}")
    return report


def check_save_exists(cfg: Config, name: str) -> str | None:
    """存档目录存在时预检存档文件，免得白启动一次；目录不在（路径不同）就交给桥接判断。"""
    if not cfg.saves_dir.is_dir():
        return None
    if not (cfg.saves_dir / f"{name}.sav").is_file():
        return f"存档不存在：{cfg.saves_dir / (name + '.sav')}"
    return None
