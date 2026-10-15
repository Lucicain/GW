"""Regressions for the ownership leak and advisory review-header validation."""
import contextlib
import importlib.util
import io
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import patch


TOOLS = Path(__file__).resolve().parents[1]


def load(name):
    spec = importlib.util.spec_from_file_location(name, TOOLS / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


hygiene = load("Check-CodeHygiene")
freshness = load("Check-StateFreshness")


class OwnershipTests(unittest.TestCase):
    def test_player_case_assignment_has_one_owner(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = Path(directory) / "GwpPlayerCaseAssignment.cs"
            caller = Path(directory) / "OtherBehavior.cs"
            source = ('PlayerBountyBehavior.IsBountyReservedTarget(party);\n'
                      'bounty?.HasCommissionAuthority(hero);\n'
                      'enforcement.IsActiveAtonementTarget(hero);\n')
            owner.write_text(source, encoding="utf-8")
            caller.write_text(source, encoding="utf-8")
            self.assertEqual([], hygiene.owner_violations([str(owner)]))
            self.assertEqual(3, len(hygiene.owner_violations([str(caller)])))

    def test_peace_has_one_owner(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = Path(directory) / "GwpPeace.cs"
            caller = Path(directory) / "OtherBehavior.cs"
            source = "FactionManager.SetNeutral(police, target);\nMakePeaceAction.Apply(player, target);\n"
            owner.write_text(source, encoding="utf-8")
            caller.write_text(source, encoding="utf-8")
            self.assertEqual([], hygiene.owner_violations([str(owner)]))
            self.assertEqual(2, len(hygiene.owner_violations([str(caller)])))

    def test_party_identity_has_one_owner(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = Path(directory) / "GwpPartyIdentity.cs"
            caller = Path(directory) / "OtherBehavior.cs"
            source = 'var id = manager.FindNextUniqueStringId<MobileParty>("watch1");\n'
            owner.write_text(source, encoding="utf-8")
            caller.write_text(source, encoding="utf-8")
            self.assertEqual([], hygiene.owner_violations([str(owner)]))
            self.assertEqual(1, len(hygiene.owner_violations([str(caller)])))

    def test_guard_experience_capacity_has_one_owner(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = Path(directory) / "GwpFireswornExperience.cs"
            caller = Path(directory) / "GwpFireswornRituals.cs"
            source = "MobilePartyHelper.CanTroopGainXp(party, troop, out int capacity);\n"
            owner.write_text(source, encoding="utf-8")
            caller.write_text(source, encoding="utf-8")
            self.assertEqual([], hygiene.owner_violations([str(owner)]))
            self.assertEqual(1, len(hygiene.owner_violations([str(caller)])))

    def test_raw_troop_power_belongs_only_to_strength_owner(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = Path(directory) / "GwpStrength.cs"
            caller = Path(directory) / "GwpFireswornParties.cs"
            source = "float s = count * model.GetTroopPower(troop, side, context, 0f);\n"
            owner.write_text(source, encoding="utf-8")
            caller.write_text(source, encoding="utf-8")
            self.assertEqual([], hygiene.owner_violations([str(owner)]))
            violations = hygiene.owner_violations([str(caller)])
            self.assertEqual(1, len(violations))
            self.assertIn("GwpStrength", violations[0])

    def test_comments_and_strings_do_not_look_like_calls(self):
        with tempfile.TemporaryDirectory() as directory:
            caller = Path(directory) / "Caller.cs"
            caller.write_text(
                '// model.GetTroopPower(troop)\n'
                '/* GetTroopPower (troop) */\n'
                'var label = "GetTroopPower(troop)";\n'
                'var strength = GwpStrength.LeaderlessRoster(troops);\n',
                encoding="utf-8")
            self.assertEqual([], hygiene.owner_violations([str(caller)]))
            with caller.open("a", encoding="utf-8") as output:
                output.write("var s = model.GetTroopPower \n (troop);\n")
            self.assertEqual(1, len(hygiene.owner_violations([str(caller)])))


class ReviewHeaderTests(unittest.TestCase):
    def test_optional_or_exact_header_is_valid(self):
        for text in ("# No review yet\n", "> 已复核至：`a898237`（2026-10-08）\n"):
            with self.subTest(text=text):
                self.assertEqual("", freshness.review_header_problem(text))

    def test_narrative_invalid_date_and_duplicate_are_reported(self):
        for value in ("2026-10-08 修了声音", "`a898237`（2026-10-08，已改）",
                      "`a898237`（2026-02-30）", "`a898237`（2026-10-08）\n"
                      "> 已复核至：`a898237`（2026-10-08）"):
            with self.subTest(value=value):
                self.assertTrue(freshness.review_header_problem("> 已复核至：" + value))

    def run_strict(self, *, duplicate=False, hygiene_failed=False):
        with tempfile.TemporaryDirectory() as directory, contextlib.ExitStack() as stack:
            state = Path(directory)
            (state / "example.md").write_text(
                "> 已复核至：`badc0de`（2026-10-08）\n"
                "> 覆盖源码：跨文件，不做自动核对\n", encoding="utf-8")
            stack.enter_context(patch.object(freshness, "STATE", state))
            stack.enter_context(patch.object(freshness, "git", return_value=""))
            for name in ("refresh_pending_rollup", "refresh_defect_rollup", "check_memory",
                         "check_leftovers", "check_scratch"):
                stack.enter_context(patch.object(freshness, name, return_value=[]))
            stack.enter_context(patch.object(freshness, "check_baseline", return_value=([], [])))
            stack.enter_context(patch.object(freshness, "check_journal",
                                             return_value=["duplicate"] if duplicate else []))
            stack.enter_context(patch.object(freshness.subprocess, "run",
                                             return_value=types.SimpleNamespace(returncode=int(hygiene_failed))))
            stack.enter_context(patch.object(freshness.sys, "argv", ["checker", "--strict"]))
            output = stack.enter_context(contextlib.redirect_stdout(io.StringIO()))
            result = freshness.main()
            return result, output.getvalue()

    def test_nonexistent_commit_is_advisory_even_in_strict_mode(self):
        result, output = self.run_strict()
        self.assertEqual(0, result)
        self.assertIn("复核提交 badc0de 不存在", output)

    def test_existing_mechanical_gates_still_block(self):
        for inputs in ({"duplicate": True}, {"hygiene_failed": True}):
            with self.subTest(inputs=inputs):
                self.assertEqual(1, self.run_strict(**inputs)[0])


if __name__ == "__main__":
    unittest.main()
