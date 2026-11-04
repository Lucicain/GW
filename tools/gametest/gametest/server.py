"""MCP 服务（stdio）。每个工具一次调用办完一件事，返回结构化 JSON，不需要看屏幕。

兼容官方 `mcp` SDK 的 2.x（`MCPServer`）与 1.x（`FastMCP`），两者的 `@tool()` 与 `run("stdio")` 用法相同。
"""

from __future__ import annotations

import asyncio
from typing import Any

from . import scenario
from .config import Config
from .controller import GameController

INSTRUCTIONS = (
    "灰袍（GreyWarden）测试桥：在用户没在玩时启动《骑马与砍杀 II》，只读写 gwp_test_ 开头的测试存档，"
    "经游戏内桥接模组执行控制台命令、推进时间、查询案件/使者/办案队/部队快照与复发哨兵计数。"
    "不操控屏幕键鼠。先 game_status 看能否启动；用完 game_stop，跑完一定关掉游戏。"
    "能写成场景的检查优先用 run_scenario，一次调用跑完并返回证据。"
)


def _new_server(name: str):
    try:
        from mcp.server.mcpserver import MCPServer
    except ImportError:
        # 依据：用户机器上可能装的是 mcp 1.x，那里同一个类叫 FastMCP。
        from mcp.server.fastmcp import FastMCP
        return FastMCP(name, instructions=INSTRUCTIONS)
    return MCPServer(name, instructions=INSTRUCTIONS)


def build_server(controller: GameController, cfg: Config):
    server = _new_server("gwp-gametest")

    async def run(fn, *args):
        return await asyncio.to_thread(fn, *args)

    @server.tool()
    async def game_status() -> dict[str, Any]:
        """查看测试游戏状态。没在运行时同时给出启动前安全检查（can_start 与 blockers）；
        运行中给出桥接报告的 state（main_menu/loading/campaign/mission）、战役时间等。"""
        return await run(controller.status)

    @server.tool()
    async def game_start(save: str, modules: list[str] | None = None) -> dict[str, Any]:
        """启动游戏并读入测试存档，返回时已在战役中。save 必须以 gwp_test_ 开头（不带 .sav）。
        modules 是插在原版前置与 GreyWarden 之间的额外模组 Id（如 ButterLib），可省略。
        用户在玩、live DLL 被占用、live 与仓库构建不一致时拒绝启动；任一步超时会杀掉进程并附日志证据。"""
        return await run(controller.start, save, modules)

    @server.tool()
    async def game_stop() -> dict[str, Any]:
        """关闭测试游戏：先请桥接正常退出（不存档），等不到就强杀。只关本工具启动的进程。"""
        return await run(controller.stop)

    @server.tool()
    async def game_load(save: str) -> dict[str, Any]:
        """在运行中的测试游戏里读另一个测试存档（必须以 gwp_test_ 开头），返回时已在战役中。"""
        return await run(controller.load_save, save)

    @server.tool()
    async def game_save(name: str, overwrite: bool = False) -> dict[str, Any]:
        """把当前战役存成测试存档（必须以 gwp_test_ 开头）。同名存档已存在时，除非 overwrite=true，否则拒绝。"""
        return await run(controller.save, name, overwrite)

    @server.tool()
    async def run_console(command: str) -> dict[str, Any]:
        """执行一条原版控制台命令（经 CommandLineFunctionality.CallFunction），如
        "campaign.add_gold_to_hero 1000"。返回命令输出。与存读档、退出相关的命令会被拒绝。"""
        return await run(controller.console, command)

    @server.tool()
    async def advance(hours: float) -> dict[str, Any]:
        """把战役时间推进 hours 小时（游戏内），推进完才返回，结果含推进前后的战役时间。"""
        return await run(controller.advance, hours)

    @server.tool()
    async def query(kind: str, args: dict[str, Any] | None = None) -> dict[str, Any]:
        """读游戏内快照。kind：cases（案件）、envoys（使者）、case_parties（办案队）、party（部队，
        args={"id": 部队Id}）、sentinels（复发哨兵经过/触发计数）、kinds（桥接支持的全部 kind）。"""
        return await run(controller.query, kind, args)

    @server.tool()
    async def list_scenarios() -> dict[str, Any]:
        """列出 tools/gametest/scenarios/ 里的场景名与说明。"""
        return {"ok": True, "scenarios": scenario.list_scenarios(cfg)}

    @server.tool()
    async def run_scenario(name: str, keep_running: bool = False) -> dict[str, Any]:
        """跑一个场景（scenarios/<name>.json）：必要时启动游戏并读档，依次执行命令与断言，
        返回 passed、每步结果、失败的实际值/期望值与捕获的证据。默认跑完关游戏；keep_running=true 保留。"""
        return await run(scenario.run, controller, cfg, name, keep_running)

    return server


def main() -> None:
    from .config import load_config
    from .host import RealHost

    cfg = load_config()
    controller = GameController(cfg, RealHost())
    server = build_server(controller, cfg)
    try:
        server.run("stdio")
    finally:
        controller.shutdown()
