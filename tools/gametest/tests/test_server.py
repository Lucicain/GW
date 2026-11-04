import asyncio
import importlib.util
import json
import shutil
import unittest

from fakes import cleanup, GAMETEST_DIR, FakeHost, make_env

from gametest.controller import GameController

HAVE_MCP = importlib.util.find_spec("mcp") is not None

EXPECTED_TOOLS = {"game_status", "game_start", "game_stop", "game_load", "game_save", "run_console",
                  "advance", "query", "list_scenarios", "run_scenario"}


def _structured(result):
    """兼容 SDK 2.x（CallToolResult）与 1.x（(content, structured) 元组）的返回形状。"""
    if isinstance(result, tuple):
        return result[1]
    structured = getattr(result, "structured_content", None)
    if structured is None:
        structured = getattr(result, "structuredContent", None)
    if structured is not None:
        return structured
    return json.loads(result.content[0].text)


@unittest.skipUnless(HAVE_MCP, "没装 mcp SDK")
class ServerTests(unittest.TestCase):
    def setUp(self):
        from gametest.server import build_server
        self.cfg = make_env(self)
        shutil.copy(GAMETEST_DIR / "scenarios" / "smoke.json", self.cfg.scenarios_dir / "smoke.json")
        self.host = FakeHost(self.cfg)
        self.ctl = GameController(self.cfg, self.host)
        self.server = build_server(self.ctl, self.cfg)
        self.addCleanup(cleanup, self.ctl, self.host)

    def call(self, tool, **args):
        return _structured(asyncio.run(self.server.call_tool(tool, args)))

    def test_tools_are_registered(self):
        tools = asyncio.run(self.server.list_tools())
        self.assertEqual({t.name for t in tools}, EXPECTED_TOOLS)
        for t in tools:
            self.assertTrue(t.description, t.name)

    def test_full_session_through_mcp(self):
        st = self.call("game_status")
        self.assertEqual((st["running"], st["can_start"]), (False, True))
        res = self.call("game_start", save="gwp_test_smoke")
        self.assertTrue(res["ok"], res)
        self.assertTrue(self.call("advance", hours=6)["ok"])
        self.assertIn("sentinels", self.call("query", kind="sentinels")["result"])
        self.assertTrue(self.call("run_console", command="campaign.add_gold_to_hero 5")["ok"])
        self.assertEqual(self.call("game_save", name="real_save")["error"]["code"], "refused")
        self.assertEqual(self.call("game_stop")["method"], "quit")

    def test_scenario_through_mcp(self):
        self.assertEqual(self.call("list_scenarios")["scenarios"][0]["name"], "smoke")
        res = self.call("run_scenario", name="smoke")
        self.assertTrue(res["passed"], res)


if __name__ == "__main__":
    unittest.main()
