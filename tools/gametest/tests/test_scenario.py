import json
import shutil
import unittest

from fakes import cleanup, GAMETEST_DIR, FakeHost, make_env

from gametest import scenario
from gametest.controller import GameController


class EvaluateTests(unittest.TestCase):
    def test_resolve_paths(self):
        caps = {"a": {"b": [10, {"c": "x"}], "dotted.key": 1}}
        self.assertEqual(scenario.resolve(caps, "a.b.0"), 10)
        self.assertEqual(scenario.resolve(caps, "a.b.1.c"), "x")
        self.assertEqual(scenario.resolve(caps, "a.b.-1.c"), "x")
        self.assertEqual(scenario.resolve(caps, ["a", "dotted.key"]), 1)
        self.assertIs(scenario.resolve(caps, "a.b.5"), scenario._MISSING)
        self.assertIs(scenario.resolve(caps, "a.b.x"), scenario._MISSING)
        self.assertIs(scenario.resolve(caps, "a.z"), scenario._MISSING)

    def test_ops(self):
        ev = scenario.evaluate
        self.assertEqual(ev("ge", 3, 3, 0)[0], True)
        self.assertEqual(ev("gt", 3, 3, 0)[0], False)
        self.assertEqual(ev("approx", 23.9, 24, 0.5)[0], True)
        self.assertEqual(ev("contains", [1, 2], 2, 0)[0], True)
        self.assertEqual(ev("contains", {"k": 1}, "k", 0)[0], True)
        self.assertEqual(ev("len_ge", [1, 2], 2, 0)[0], True)
        self.assertEqual(ev("exists", scenario._MISSING, None, 0)[0], False)
        self.assertEqual(ev("absent", scenario._MISSING, None, 0)[0], True)
        self.assertEqual(ev("truthy", 0, None, 0)[0], False)
        passed, why = ev("gt", "3", 1, 0)
        self.assertFalse(passed)
        self.assertEqual(why, "只能比较数字")
        passed, why = ev("gt", True, 0, 0)
        self.assertFalse(passed)
        self.assertEqual(ev("eq", scenario._MISSING, 1, 0), (False, "路径不存在"))

    def test_validation_catches_mistakes(self):
        bad = {
            "save": "not_test",
            "bogus": 1,
            "steps": [
                {"do": "explode"},
                {"do": "status", "assert": {}},
                {"assert": {"path": "later.x", "op": "eq", "value": 1}},
                {"do": "status", "as": "s"},
                {"assert": {"path": "s.x", "op": "nope", "value": 1}},
                {"assert": {"path": "s.x", "op": "eq"}},
                {"assert": {"path": "s.x", "op": "eq", "value": 1, "ref": "s.y"}},
            ],
        }
        errors = scenario.validate(bad)
        text = "\n".join(errors)
        for needle in ("gwp_test_", "未知顶层键：bogus", "steps[0].do", "steps[1] 必须恰好", "later",
                       "steps[4].assert.op", "steps[5].assert 必须恰好", "steps[6].assert 必须恰好"):
            self.assertIn(needle, text)
        self.assertEqual(scenario.validate({"steps": []}), ["steps 必须是非空数组"])

    def test_shipped_scenarios_are_valid(self):
        paths = sorted((GAMETEST_DIR / "scenarios").glob("*.json"))
        self.assertTrue(paths)
        for path in paths:
            self.assertEqual(scenario.validate(json.loads(path.read_text(encoding="utf-8"))), [], path.name)


class RunTests(unittest.TestCase):
    def setUp(self):
        self.cfg = make_env(self)
        shutil.copy(GAMETEST_DIR / "scenarios" / "smoke.json", self.cfg.scenarios_dir / "smoke.json")
        self.host = FakeHost(self.cfg)
        self.ctl = GameController(self.cfg, self.host)
        self.addCleanup(cleanup, self.ctl, self.host)

    def write(self, name, data):
        (self.cfg.scenarios_dir / f"{name}.json").write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")

    def test_smoke_scenario_passes_and_stops_game(self):
        res = scenario.run(self.ctl, self.cfg, "smoke")
        self.assertTrue(res["ok"], res)
        self.assertTrue(res["passed"], json.dumps(res, ensure_ascii=False, indent=1))
        self.assertEqual(res["failures"], [])
        self.assertEqual(res["stop"]["method"], "quit")
        self.assertFalse(self.ctl.is_running())
        self.assertIn("after", res["captures"])
        self.assertEqual(len(self.host.launches), 1)

    def test_keep_running(self):
        res = scenario.run(self.ctl, self.cfg, "smoke", keep_running=True)
        self.assertTrue(res["passed"])
        self.assertNotIn("stop", res)
        self.assertTrue(self.ctl.is_running())
        # 再跑一次：游戏已在运行，场景写了 save → 重新读档而不是再启动
        res = scenario.run(self.ctl, self.cfg, "smoke")
        self.assertTrue(res["passed"])
        self.assertEqual(len(self.host.launches), 1)

    def test_failed_assertion_reports_actual_and_expected(self):
        self.write("fail", {"save": "gwp_test_smoke", "steps": [
            {"do": "query", "args": {"kind": "sentinels"}, "as": "s"},
            {"assert": {"path": "s.sentinels.CaseSettle.triggers", "op": "eq", "value": 5, "message": "应触发 5 次"}},
            {"do": "advance", "args": {"hours": 1}},
        ]})
        res = scenario.run(self.ctl, self.cfg, "fail")
        self.assertTrue(res["ok"])
        self.assertFalse(res["passed"])
        f = res["failures"][0]
        self.assertEqual((f["actual"], f["expected"], f["message"]), (0, 5, "应触发 5 次"))
        self.assertEqual(len(res["steps"]), 2)  # 默认失败即停
        self.assertFalse(self.ctl.is_running())

    def test_continue_after_failure(self):
        self.write("cont", {"save": "gwp_test_smoke", "stop_on_failure": False, "steps": [
            {"do": "status", "as": "s"},
            {"assert": {"path": "s.state", "op": "eq", "value": "mission"}},
            {"assert": {"path": "s.state", "op": "eq", "value": "campaign"}},
        ]})
        res = scenario.run(self.ctl, self.cfg, "cont")
        self.assertFalse(res["passed"])
        self.assertEqual([s["passed"] for s in res["steps"]], [True, False, True])

    def test_step_error_fails_unless_expected(self):
        self.write("err", {"save": "gwp_test_smoke", "steps": [
            {"do": "console", "args": {"command": "nope.cmd"}, "expect_error": "unknown_command", "as": "e"},
            {"assert": {"path": "e.code", "op": "eq", "value": "unknown_command"}},
            {"do": "console", "args": {"command": "nope.again"}},
        ]})
        res = scenario.run(self.ctl, self.cfg, "err")
        self.assertFalse(res["passed"])
        self.assertEqual([s["passed"] for s in res["steps"]], [True, True, False])
        self.assertEqual(res["failures"][0]["error"]["code"], "unknown_command")

    def test_unexpected_success_fails(self):
        self.write("succ", {"save": "gwp_test_smoke", "steps": [
            {"do": "status", "expect_error": "invalid_state"}]})
        res = scenario.run(self.ctl, self.cfg, "succ")
        self.assertFalse(res["passed"])
        self.assertIn("命令却成功", res["steps"][0]["reason"])

    def test_timeout_kill_is_surfaced(self):
        self.host.bridge_setup = lambda b: b.hang_on.add("advance")
        self.cfg.command_timeout_s = 0.3
        self.write("hang", {"save": "gwp_test_smoke", "steps": [{"do": "advance", "args": {"hours": 1}}]})
        res = scenario.run(self.ctl, self.cfg, "hang")
        self.assertFalse(res["passed"])
        self.assertTrue(res["steps"][0]["killed"])
        self.assertEqual(res["stop"], {"ok": True, "was_running": False})

    def test_start_refusal_is_harness_failure(self):
        self.cfg.live_main_dll.write_bytes(b"MZ stale")
        res = scenario.run(self.ctl, self.cfg, "smoke")
        self.assertFalse(res["ok"])
        self.assertEqual((res["stage"], res["error"]["code"]), ("start", "refused"))
        self.assertEqual(self.host.launches, [])

    def test_no_save_and_not_running(self):
        self.write("nosave", {"steps": [{"do": "status"}]})
        res = scenario.run(self.ctl, self.cfg, "nosave")
        self.assertEqual(res["error"]["code"], "not_running")

    def test_unknown_and_invalid_scenarios(self):
        self.assertEqual(scenario.run(self.ctl, self.cfg, "nope")["error"]["code"], "not_found")
        self.assertEqual(scenario.run(self.ctl, self.cfg, "../x")["error"]["code"], "refused")
        (self.cfg.scenarios_dir / "broken.json").write_text("{", encoding="utf-8")
        self.assertEqual(scenario.run(self.ctl, self.cfg, "broken")["error"]["code"], "invalid_scenario")
        self.assertEqual(self.host.launches, [])

    def test_large_captures_are_clipped(self):
        self.cfg.evidence_chars = 50
        self.write("big", {"save": "gwp_test_smoke", "steps": [{"do": "query", "args": {"kind": "kinds"}, "as": "k"}]})
        res = scenario.run(self.ctl, self.cfg, "big")
        self.assertIsInstance(res["captures"]["k"], str)
        self.assertIn("截断", res["captures"]["k"])

    def test_list_scenarios(self):
        names = [s["name"] for s in scenario.list_scenarios(self.cfg)]
        self.assertEqual(names, ["smoke"])


if __name__ == "__main__":
    unittest.main()
