import os
import unittest
from pathlib import Path

from fakes import FakeHost, make_env

from gametest import fences
from gametest.config import config_from_mapping
from gametest.host import ProcInfo


class SaveNameTests(unittest.TestCase):
    def test_accepts_test_saves(self):
        for name in ("gwp_test_smoke", "gwp_test_a-b_1"):
            self.assertIsNone(fences.check_save_name(name), name)

    def test_rejects_everything_else(self):
        for name in ("", None, "smoke", "GWP_TEST_x", "gwp_test_", "gwp_test_x.sav", "gwp_test_../x",
                     "gwp_test_a/b", "gwp_test_a\\b", "gwp_test_ space", "x_gwp_test_y", "gwp_test_" + "a" * 65):
            self.assertIsNotNone(fences.check_save_name(name), repr(name))


class ConsoleTests(unittest.TestCase):
    def test_allows_ordinary_commands(self):
        self.assertIsNone(fences.check_console_command("campaign.add_gold_to_hero 1000"))

    def test_rejects_save_load_quit_and_multiline(self):
        for cmd in ("campaign.save_game x", "Campaign.LoadGame x", "game.quit", "exit", "", "   ",
                    "a.b 1\ncampaign.save x"):
            self.assertIsNotNone(fences.check_console_command(cmd), repr(cmd))


class LaunchCommandTests(unittest.TestCase):
    def test_argv_matches_the_documented_command(self):
        cfg = make_env(self)
        mods, error = fences.module_list(cfg, ["ButterLib"])
        self.assertIsNone(error)
        argv = fences.launch_argv(cfg, mods)
        self.assertEqual(argv[0], str(cfg.game_dir / "bin" / "Win64_Shipping_Client" / "Bannerlord.exe"))
        self.assertEqual(argv[1], "/singleplayer")
        self.assertEqual(argv[2], "_MODULES_*Bannerlord.Harmony*Native*SandBoxCore*Sandbox*StoryMode*CustomBattle"
                                  "*ButterLib*GreyWarden*GreyWardenTestBridge*_MODULES_")
        self.assertEqual(len(argv), 3)

    def test_greywarden_and_bridge_always_last(self):
        cfg = make_env(self)
        mods, _ = fences.module_list(cfg, ["GreyWardenTestBridge", "GreyWarden", "Extra"])
        self.assertEqual(mods[-3:], ["Extra", "GreyWarden", "GreyWardenTestBridge"])
        self.assertEqual(mods[0], "Bannerlord.Harmony")

    def test_rejects_module_names_that_could_break_the_argument(self):
        cfg = make_env(self)
        for bad in ("a*b", "a b", "../x", ""):
            _, error = fences.module_list(cfg, [bad])
            self.assertIsNotNone(error, bad)

    def test_xbox_layout_is_detected(self):
        cfg = make_env(self)
        xbox = cfg.game_dir / "bin" / "Gaming.Desktop.x64_Shipping_Client" / "Bannerlord.exe"
        xbox.parent.mkdir(parents=True)
        xbox.write_bytes(b"exe")
        (cfg.exe_path.parent.parent / "Win64_Shipping_Client" / "Bannerlord.exe").unlink()
        self.assertEqual(cfg.bin_folder, "Gaming.Desktop.x64_Shipping_Client")


class LaunchFenceTests(unittest.TestCase):
    def setUp(self):
        self.cfg = make_env(self)
        self.host = FakeHost(self.cfg)
        self.mods, _ = fences.module_list(self.cfg, None)

    def check(self):
        return fences.check_launch(self.cfg, self.host, self.mods)

    def test_clean_environment_passes(self):
        report = self.check()
        self.assertTrue(report.ok, report.blockers)
        self.assertEqual(report.facts["GreyWarden"]["repo_sha256"], report.facts["GreyWarden"]["live_sha256"])
        self.assertEqual(report.facts["module_mirror"]["files_checked"], 2)

    def test_refuses_when_user_is_playing(self):
        for name in ("Bannerlord.exe", "TaleWorlds.MountAndBlade.Launcher.exe", "Watchdog.exe",
                     "Bannerlord.BLSE.Standalone.exe"):
            self.host.extra_processes = [ProcInfo(name, 77)]
            report = self.check()
            self.assertFalse(report.ok, name)
            self.assertIn("用户可能在玩", report.blockers[0])

    def test_unrelated_launchers_do_not_block(self):
        self.host.extra_processes = [ProcInfo("EpicGamesLauncher.exe", 5), ProcInfo("steam.exe", 6),
                                     ProcInfo("watchdogd", 7), ProcInfo("MyBannerlordTool.exe", 8)]
        self.assertTrue(self.check().ok)

    def test_refuses_when_live_dll_is_locked(self):
        for dll in (self.cfg.live_main_dll, self.cfg.live_bridge_dll):
            self.host.locked = {dll}
            report = self.check()
            self.assertFalse(report.ok)
            self.assertTrue(any("被占用" in b for b in report.blockers))

    @unittest.skipIf(os.name != "nt" and os.geteuid() == 0, "root 无视只读权限")
    def test_read_only_dll_is_not_reported_as_locked(self):
        self.cfg.live_main_dll.chmod(0o444)
        self.addCleanup(self.cfg.live_main_dll.chmod, 0o644)
        report = self.check()
        self.assertTrue(any("只读属性" in b for b in report.blockers))
        self.assertFalse(any("被占用（" in b for b in report.blockers))

    def test_refuses_on_main_dll_hash_mismatch(self):
        self.cfg.live_main_dll.write_bytes(b"MZ stale")
        report = self.check()
        self.assertFalse(report.ok)
        self.assertTrue(any(b.startswith("GreyWarden：live DLL 与仓库最新构建哈希不一致") for b in report.blockers))

    def test_refuses_on_bridge_dll_hash_mismatch(self):
        self.cfg.live_bridge_dll.write_bytes(b"MZ stale bridge")
        report = self.check()
        self.assertTrue(any(b.startswith("GreyWardenTestBridge：") for b in report.blockers))

    def test_newest_repo_build_wins(self):
        import os
        import time
        newer = self.cfg.repo_root / "GreyWardenPolicePurity" / "obj" / "Release" / "GreyWardenPolicePurity.dll"
        newer.parent.mkdir(parents=True)
        newer.write_bytes(b"MZ release build")
        future = time.time() + 100
        os.utime(newer, (future, future))
        report = self.check()
        self.assertFalse(report.ok)
        self.assertIn("Release", report.facts["GreyWarden"]["repo_build"])

    def test_refuses_without_repo_build(self):
        (self.cfg.repo_root / "GreyWardenPolicePurity" / "obj" / "Debug" / "GreyWardenPolicePurity.dll").unlink()
        report = self.check()
        self.assertTrue(any("找不到构建产物" in b for b in report.blockers))

    def test_refuses_when_live_dll_missing(self):
        self.cfg.live_bridge_dll.unlink()
        report = self.check()
        self.assertTrue(any("live 缺 DLL" in b for b in report.blockers))

    def test_refuses_on_module_mirror_difference(self):
        (self.cfg.live_module / "ModuleData" / "a.xml").write_text("<changed/>", encoding="utf-8")
        report = self.check()
        self.assertFalse(report.ok)
        self.assertEqual(report.facts["module_mirror"]["differing"], ["ModuleData/a.xml"])

    def test_refuses_on_module_mirror_missing_file(self):
        (self.cfg.live_module / "ModuleData" / "a.xml").unlink()
        report = self.check()
        self.assertEqual(report.facts["module_mirror"]["missing"], ["ModuleData/a.xml"])

    def test_editor_trees_are_not_mirrored(self):
        # 仓库 _Module/Assets 里有文件而 live 没有，不算缺失
        self.assertTrue(self.check().ok)

    def test_refuses_missing_module(self):
        self.mods, _ = fences.module_list(self.cfg, ["NoSuchMod"])
        report = self.check()
        self.assertTrue(any("NoSuchMod" in b for b in report.blockers))

    def test_workshop_module_is_found(self):
        self.assertEqual(fences.find_missing_modules(self.cfg, ["Bannerlord.Harmony"]), [])

    def test_missing_exe_refuses(self):
        self.cfg.exe_path.unlink()
        self.assertTrue(any("可执行文件" in b for b in self.check().blockers))

    def test_save_presence_precheck(self):
        self.assertIsNone(fences.check_save_exists(self.cfg, "gwp_test_smoke"))
        self.assertIsNotNone(fences.check_save_exists(self.cfg, "gwp_test_absent"))
        self.cfg.saves_dir = Path(self.cfg.saves_dir) / "not_there"
        self.assertIsNone(fences.check_save_exists(self.cfg, "gwp_test_absent"))


class ConfigTests(unittest.TestCase):
    def test_unknown_key_is_rejected(self):
        with self.assertRaises(ValueError):
            config_from_mapping({"gamedir": "x"})

    def test_paths_and_tuples_are_converted(self):
        cfg = config_from_mapping({"game_dir": "E:/Games/Bannerlord", "extra_modules": ["ButterLib"],
                                   "_comment": "ignored"})
        self.assertEqual(cfg.game_dir, Path("E:/Games/Bannerlord"))
        self.assertEqual(cfg.extra_modules, ("ButterLib",))
        self.assertEqual(cfg.resolved_workshop_dir, Path("E:/workshop/content/261550"))


if __name__ == "__main__":
    unittest.main()
