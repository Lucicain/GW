"""测试桥配置。

默认值对应用户本机布局（AGENTS.md 与 docs/state/build-and-deploy.md）。本机差异写进
`tools/gametest/config.local.json`（不入库），或用环境变量 `GWP_GAME_DIR` 只改游戏目录。
"""

from __future__ import annotations

import json
import os
from dataclasses import dataclass, field, fields
from pathlib import Path

GAMETEST_DIR = Path(__file__).resolve().parent.parent
REPO_ROOT = GAMETEST_DIR.parent.parent

DEFAULT_GAME_DIR = r"D:\steam\steamapps\common\Mount & Blade II Bannerlord"

# 灰袍 SubModule.xml 声明的前置（Native/SandBoxCore/Sandbox/CustomBattle/StoryMode）。灰袍自带 0Harmony.dll，
# 不需要 Bannerlord.Harmony 等社区前置模组，和玩家只装 GreyWarden 时一致。
DEFAULT_BASE_MODULES = ("Native", "SandBoxCore", "Sandbox", "StoryMode", "CustomBattle")
# 这两个永远排在最后，game_start 的 modules 参数只能往它们前面插额外模组。
REQUIRED_TAIL_MODULES = ("GreyWarden", "GreyWardenTestBridge")

# 原版两种发行布局，与 GreyWardenPolicePurity.csproj 的 GameBinariesFolder 检测一致。
BIN_FOLDERS = ("Win64_Shipping_Client", "Gaming.Desktop.x64_Shipping_Client")


@dataclass
class Config:
    game_dir: Path = Path(DEFAULT_GAME_DIR)
    repo_root: Path = REPO_ROOT
    # 存档目录只用于启动前预检存档是否存在；目录不存在就跳过预检，由桥接模组报 not_found。
    saves_dir: Path = Path.home() / "Documents" / "Mount and Blade II Bannerlord" / "Game Saves"
    # 原版 rgl 日志目录，失败时按 pid 取日志尾作为证据。
    logs_dir: Path = Path(r"C:\ProgramData\Mount and Blade II Bannerlord\logs")
    # Steam 创意工坊模组目录；默认由游戏目录推出（steamapps/workshop/content/261550）。
    workshop_dir: Path | None = None
    base_modules: tuple[str, ...] = DEFAULT_BASE_MODULES
    extra_modules: tuple[str, ...] = ()
    # 仓库构建产物：相对 repo_root 的 glob，取最新的一个与 live 比哈希。
    main_dll_globs: tuple[str, ...] = ("GreyWardenPolicePurity/obj/**/GreyWardenPolicePurity.dll",)
    bridge_dll_globs: tuple[str, ...] = ("tools/gametest/bridge/**/obj/**/GreyWardenTestBridge.dll",)
    scenarios_dir: Path = GAMETEST_DIR / "scenarios"
    runtime_dir: Path = GAMETEST_DIR / ".runtime"
    # 超时（秒）。
    bridge_wait_s: float = 240.0   # 启动到桥接写出 bridge.json
    main_menu_wait_s: float = 240.0  # 握手后到主菜单
    load_wait_s: float = 300.0      # 读档到战役就绪
    command_timeout_s: float = 120.0
    advance_s_per_hour: float = 3.0  # advance 的额外超时：每小时游戏时间
    stop_wait_s: float = 60.0       # quit 之后等进程退出，超时强杀
    poll_interval_s: float = 0.5
    max_advance_hours: float = 24.0 * 100
    max_line_bytes: int = 16 * 1024 * 1024
    evidence_chars: int = 4000      # 场景证据里每个捕获结果的最大字符数

    @property
    def modules_dir(self) -> Path:
        return self.game_dir / "Modules"

    @property
    def bin_folder(self) -> str:
        for name in BIN_FOLDERS:
            if (self.game_dir / "bin" / name / "Bannerlord.exe").is_file():
                return name
        return BIN_FOLDERS[0]

    @property
    def exe_path(self) -> Path:
        return self.game_dir / "bin" / self.bin_folder / "Bannerlord.exe"

    @property
    def live_module(self) -> Path:
        return self.modules_dir / "GreyWarden"

    @property
    def live_main_dll(self) -> Path:
        return self.live_module / "bin" / self.bin_folder / "GreyWardenPolicePurity.dll"

    @property
    def bridge_module(self) -> Path:
        return self.modules_dir / "GreyWardenTestBridge"

    @property
    def live_bridge_dll(self) -> Path:
        return self.bridge_module / "bin" / self.bin_folder / "GreyWardenTestBridge.dll"

    @property
    def bridge_info_path(self) -> Path:
        return self.bridge_module / "bridge.json"

    @property
    def repo_module(self) -> Path:
        return self.repo_root / "GreyWardenPolicePurity" / "_Module"

    @property
    def resolved_workshop_dir(self) -> Path:
        if self.workshop_dir is not None:
            return self.workshop_dir
        # <steamapps>/common/<游戏> → <steamapps>/workshop/content/261550
        return self.game_dir.parent.parent / "workshop" / "content" / "261550"

    @property
    def session_file(self) -> Path:
        return self.runtime_dir / "session.json"


_PATH_FIELDS = {"game_dir", "repo_root", "saves_dir", "logs_dir", "workshop_dir", "scenarios_dir", "runtime_dir"}
_TUPLE_FIELDS = {"base_modules", "extra_modules", "main_dll_globs", "bridge_dll_globs"}


def config_from_mapping(data: dict, base: Config | None = None) -> Config:
    cfg = base or Config()
    known = {f.name for f in fields(Config)}
    for key, value in data.items():
        if key.startswith("_"):
            continue  # 允许 "_comment" 之类的注释键
        if key not in known:
            raise ValueError(f"未知配置项：{key}")
        if key in _PATH_FIELDS:
            value = None if value is None else Path(value)
        elif key in _TUPLE_FIELDS:
            value = tuple(value)
        setattr(cfg, key, value)
    return cfg


def load_config(path: Path | None = None) -> Config:
    cfg = Config()
    local = path or (GAMETEST_DIR / "config.local.json")
    if local.is_file():
        cfg = config_from_mapping(json.loads(local.read_text(encoding="utf-8")), cfg)
    env_dir = os.environ.get("GWP_GAME_DIR")
    if env_dir:
        cfg.game_dir = Path(env_dir)
    return cfg
