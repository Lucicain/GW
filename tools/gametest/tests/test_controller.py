import json
import time
import unittest

from fakes import cleanup, FakeHost, make_env

from gametest.controller import SESSION_ENV, GameController
from gametest.host import ProcInfo


class ControllerTests(unittest.TestCase):
    def setUp(self):
        self.cfg = make_env(self)
        self.host = FakeHost(self.cfg, saves=("gwp_test_smoke", "gwp_test_other"))
        self.ctl = GameController(self.cfg, self.host)
        self.addCleanup(cleanup, self.ctl, self.host)

    def start(self, save="gwp_test_smoke", modules=None):
        return self.ctl.start(save, modules)

    # ---------- 启动 ----------

    def test_start_reaches_campaign(self):
        res = self.start()
        self.assertTrue(res["ok"], res)
        self.assertEqual(res["result"]["state"], "campaign")
        self.assertEqual(res["save"], "gwp_test_smoke")
        launch = self.host.launches[0]
        self.assertEqual(launch["argv"][1], "/singleplayer")
        self.assertTrue(launch["argv"][2].endswith("*GreyWarden*GreyWardenTestBridge*_MODULES_"))
        self.assertEqual(launch["cwd"], self.cfg.exe_path.parent)
        self.assertEqual(len(launch["session"]), 32)
        cmds = [r["cmd"] for r in self.host.procs[0].bridge.requests]
        self.assertEqual(cmds[0], "hello")
        self.assertIn("load_save", cmds)
        self.assertTrue(self.ctl.is_running())
        session = json.loads(self.cfg.session_file.read_text(encoding="utf-8"))
        self.assertEqual(session["pid"], res["pid"])

    def test_refuses_non_test_save_before_launch(self):
        for save in ("autosave", "gwp_test_../evil", "gwp_test_x.sav"):
            res = self.start(save)
            self.assertFalse(res["ok"])
            self.assertEqual(res["error"]["code"], "refused")
        self.assertEqual(self.host.launches, [])

    def test_refuses_missing_save_before_launch(self):
        res = self.start("gwp_test_absent")
        self.assertEqual(res["error"]["code"], "not_found")
        self.assertEqual(self.host.launches, [])

    def test_refuses_while_user_plays(self):
        self.host.extra_processes = [ProcInfo("Bannerlord.exe", 1234)]
        res = self.start()
        self.assertEqual(res["error"]["code"], "refused")
        self.assertIn("用户可能在玩", res["error"]["message"])
        self.assertEqual(self.host.launches, [])

    def test_refuses_on_hash_mismatch(self):
        self.cfg.live_main_dll.write_bytes(b"MZ other")
        res = self.start()
        self.assertEqual(res["error"]["code"], "refused")
        self.assertFalse(res["fences"]["ok"])
        self.assertEqual(self.host.launches, [])

    def test_refuses_on_locked_dll(self):
        self.host.locked = {self.cfg.live_main_dll}
        self.assertEqual(self.start()["error"]["code"], "refused")
        self.assertEqual(self.host.launches, [])

    def test_refuses_second_start(self):
        self.assertTrue(self.start()["ok"])
        res = self.start()
        self.assertEqual(res["error"]["code"], "already_running")
        self.assertEqual(len(self.host.launches), 1)

    def test_bridge_never_appears_kills_process(self):
        self.host.mode = "no_bridge"
        self.cfg.bridge_wait_s = 0.2
        res = self.start()
        self.assertFalse(res["ok"])
        self.assertEqual((res["stage"], res["error"]["code"]), ("wait_bridge", "timeout"))
        self.assertTrue(res["killed"])
        self.assertTrue(self.host.procs[0].killed)
        self.assertFalse(self.ctl.is_running())
        self.assertFalse(self.cfg.session_file.exists())

    def test_stale_bridge_json_from_other_session_is_ignored(self):
        self.host.mode = "no_bridge"
        self.cfg.bridge_wait_s = 0.2
        self.cfg.bridge_info_path.write_text(json.dumps({"port": 1, "token": "t" * 32, "session": "old", "pid": 1}),
                                             encoding="utf-8")
        res = self.start()
        self.assertEqual(res["error"]["code"], "timeout")

    def test_crash_during_start_reports_exit_code_and_log(self):
        self.host.mode = "crash"
        self.cfg.logs_dir.mkdir()
        (self.cfg.logs_dir / "rgl_log_4001.txt").write_text("line1\nfatal: boom\n", encoding="utf-8")
        res = self.start()
        self.assertEqual(res["error"]["code"], "exited")
        self.assertEqual(res["evidence"]["exit_code"], 3)
        self.assertIn("fatal: boom", res["evidence"]["rgl_logs"]["rgl_log_4001.txt"])

    def test_bridge_pid_mismatch_kills(self):
        self.host.bridge_setup = lambda b: setattr(b, "pid", 999999)
        res = self.start()
        self.assertEqual(res["error"]["code"], "protocol_error")
        self.assertTrue(res["killed"])

    def test_non_loopback_bridge_is_refused(self):
        def setup(bridge):
            bridge.write_info = lambda **_: type(bridge).write_info(bridge, host="10.0.0.2")
        self.host.bridge_setup = setup
        res = self.start()
        self.assertEqual(res["error"]["code"], "protocol_error")
        self.assertTrue(res["killed"])

    def test_wrong_token_in_bridge_json_kills(self):
        # bridge.json 里的令牌与桥接校验的令牌不同 → hello 被拒
        def setup(bridge):
            bridge.write_info = lambda **_: type(bridge).write_info(bridge, token="y" * 32)
        self.host.bridge_setup = setup
        res = self.start()
        self.assertEqual((res["stage"], res["error"]["code"]), ("hello", "unauthorized"))
        self.assertTrue(res["killed"])

    def test_main_menu_timeout_kills(self):
        self.host.bridge_setup = lambda b: setattr(b, "menu_after_status", 10 ** 9)
        self.cfg.main_menu_wait_s = 0.2
        res = self.start()
        self.assertEqual((res["stage"], res["error"]["code"]), ("wait_main_menu", "timeout"))
        self.assertTrue(res["killed"])

    def test_load_failure_kills(self):
        self.host.saves = {"gwp_test_other"}  # 存档文件在，但桥接说读不了
        res = self.start()
        self.assertEqual((res["stage"], res["error"]["code"]), ("load_save", "not_found"))
        self.assertTrue(res["killed"])

    def test_load_hang_times_out_and_kills(self):
        self.host.bridge_setup = lambda b: b.hang_on.add("load_save")
        self.cfg.load_wait_s = 0.2
        res = self.start()
        self.assertEqual((res["stage"], res["error"]["code"]), ("load_save", "timeout"))
        self.assertTrue(self.host.procs[0].killed)

    # ---------- 运行中命令 ----------

    def test_commands_after_start(self):
        self.assertTrue(self.start()["ok"])
        st = self.ctl.status()
        self.assertTrue(st["running"])
        self.assertEqual(st["result"]["state"], "campaign")
        adv = self.ctl.advance(24)
        self.assertTrue(adv["ok"], adv)
        self.assertEqual(adv["result"]["hours_advanced"], 24)
        q = self.ctl.query("sentinels")
        self.assertEqual(q["result"]["sentinels"]["CaseSettle"]["passes"], 4)
        c = self.ctl.console("campaign.add_gold_to_hero 1000")
        self.assertEqual(c["result"]["output"], "ran: campaign.add_gold_to_hero 1000")
        self.assertTrue(self.ctl.save("gwp_test_new")["ok"])
        self.assertEqual(self.ctl.save("gwp_test_new")["error"]["code"], "exists")
        self.assertTrue(self.ctl.save("gwp_test_new", overwrite=True)["ok"])
        self.assertTrue(self.ctl.load_save("gwp_test_other")["ok"])

    def test_command_errors_keep_game(self):
        self.assertTrue(self.start()["ok"])
        res = self.ctl.console("nope.command")
        self.assertEqual(res["error"]["code"], "unknown_command")
        self.assertTrue(self.ctl.is_running())

    def test_fenced_commands_never_reach_bridge(self):
        self.assertTrue(self.start()["ok"])
        bridge = self.host.procs[0].bridge
        before = len(bridge.requests)
        self.assertEqual(self.ctl.console("campaign.save_game x")["error"]["code"], "refused")
        self.assertEqual(self.ctl.save("my_real_save")["error"]["code"], "refused")
        self.assertEqual(self.ctl.load_save("autosave")["error"]["code"], "refused")
        self.assertEqual(self.ctl.advance(-1)["error"]["code"], "refused")
        self.assertEqual(self.ctl.advance(True)["error"]["code"], "refused")
        self.assertEqual(self.ctl.advance(10 ** 6)["error"]["code"], "refused")
        self.assertEqual(self.ctl.query("Bad Kind")["error"]["code"], "refused")
        self.assertEqual(len(bridge.requests), before)

    def test_command_timeout_kills_game(self):
        self.assertTrue(self.start()["ok"])
        self.host.procs[0].bridge.hang_on.add("advance")
        self.cfg.command_timeout_s = 0.2
        res = self.ctl.advance(1)
        self.assertEqual(res["error"]["code"], "timeout")
        self.assertTrue(res["killed"])
        self.assertFalse(self.ctl.is_running())

    def test_commands_without_game(self):
        self.assertEqual(self.ctl.advance(1)["error"]["code"], "not_running")
        st = self.ctl.status()
        self.assertFalse(st["running"])
        self.assertTrue(st["can_start"], st)

    def test_game_crash_mid_session_is_reported(self):
        self.assertTrue(self.start()["ok"])
        self.host.procs[0].exit(-1)
        self.assertEqual(self.ctl.query("cases")["error"]["code"], "not_running")

    # ---------- 停止 ----------

    def test_stop_via_quit(self):
        self.assertTrue(self.start()["ok"])
        res = self.ctl.stop()
        self.assertEqual((res["ok"], res["method"]), (True, "quit"))
        self.assertFalse(self.host.procs[0].killed)
        self.assertFalse(self.ctl.is_running())
        self.assertFalse(self.cfg.session_file.exists())

    def test_stop_kills_when_quit_is_ignored(self):
        self.assertTrue(self.start()["ok"])
        self.host.procs[0].bridge.ignore_quit = True
        self.cfg.stop_wait_s = 0.2
        res = self.ctl.stop()
        self.assertEqual((res["ok"], res["method"]), (True, "kill"))
        self.assertTrue(self.host.procs[0].killed)

    def test_stop_kills_when_bridge_hangs(self):
        self.assertTrue(self.start()["ok"])
        self.host.procs[0].bridge.hang_on.add("quit")
        self.cfg.stop_wait_s = 0.1
        res = self.ctl.stop()
        self.assertEqual(res["method"], "kill")
        self.assertTrue(self.host.procs[0].killed)

    def test_stop_while_another_command_hangs(self):
        import threading
        self.assertTrue(self.start()["ok"])
        self.host.procs[0].bridge.hang_on.add("advance")
        self.cfg.command_timeout_s = 5.0
        out = {}
        t = threading.Thread(target=lambda: out.setdefault("adv", self.ctl.advance(1)))
        t.start()
        time.sleep(0.2)
        t0 = time.monotonic()
        res = self.ctl.stop()
        self.assertLess(time.monotonic() - t0, 4.0)
        self.assertEqual(res["method"], "kill")
        t.join(6)
        self.assertFalse(out["adv"]["ok"])
        self.assertTrue(self.host.procs[0].killed)

    def test_stop_without_game(self):
        self.assertEqual(self.ctl.stop(), {"ok": True, "was_running": False})

    # ---------- 会话接回 ----------

    def test_adopts_own_session_after_restart(self):
        res = self.start()
        self.assertTrue(res["ok"])
        ctl2 = GameController(self.cfg, self.host)
        self.assertTrue(ctl2.is_running())
        self.assertEqual(ctl2.query("cases")["result"]["cases"][0]["id"], "case_1")
        stop = ctl2.stop()
        self.assertEqual(stop["method"], "quit")

    def test_does_not_adopt_foreign_game(self):
        # 会话文件指向的 pid 现在是用户自己开的游戏：bridge.json 的会话对不上，不接管
        self.cfg.runtime_dir.mkdir()
        self.cfg.session_file.write_text(json.dumps({"pid": 1234, "session": "mine"}), encoding="utf-8")
        self.host.extra_processes = [ProcInfo("Bannerlord.exe", 1234)]
        ctl2 = GameController(self.cfg, self.host)
        self.assertFalse(ctl2.is_running())
        self.assertEqual(ctl2.stop(), {"ok": True, "was_running": False})
        self.assertEqual(self.host.killed_pids, [])
        self.assertFalse(self.cfg.session_file.exists())

    def test_session_env_name(self):
        self.assertEqual(SESSION_ENV, "GWP_TEST_BRIDGE_SESSION")


if __name__ == "__main__":
    unittest.main()
