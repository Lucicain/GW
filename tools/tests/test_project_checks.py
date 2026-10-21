"""Regressions for the ownership leak, dead-reference checks and change-impact parsing."""
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
    def test_handover_shore_comparison_has_one_owner(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = Path(directory) / "GwpCommon.cs"
            caller = Path(directory) / "GwpWardenDispatchBehavior.cs"
            source = "return first.IsCurrentlyAtSea == second.IsCurrentlyAtSea;\n"
            owner.write_text(source, encoding="utf-8")
            caller.write_text(source, encoding="utf-8")
            self.assertEqual([], hygiene.owner_violations([str(owner)]))
            violations = hygiene.owner_violations([str(caller)])
            self.assertEqual(1, len(violations))
            self.assertIn("GwpCommon.CanCompletePartyHandover", violations[0])
            caller.write_text(
                "return GwpCommon.CanCompletePartyHandover(first, second);\n"
                "// first.IsCurrentlyAtSea == second.IsCurrentlyAtSea\n",
                encoding="utf-8")
            self.assertEqual([], hygiene.owner_violations([str(caller)]))

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

    def test_player_case_war_has_one_owner(self):
        with tempfile.TemporaryDirectory() as directory:
            owner = Path(directory) / "PlayerBountyBehavior.Support.cs"
            caller = Path(directory) / "GwpCitySearchMission.cs"
            source = "DeclareWarAction.ApplyByDefault(player, target);\n"
            owner.write_text(source, encoding="utf-8")
            caller.write_text(source, encoding="utf-8")
            self.assertEqual([], hygiene.owner_violations([str(owner)]))
            violations = hygiene.owner_violations([str(caller)])
            self.assertEqual(1, len(violations))
            self.assertIn("DeclarePlayerCaseWar", violations[0])

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


def known_code():
    return freshness.KnownCode(
        files={"GwpStrength.cs", "GreyWardenPolicePurity/GwpStrength.cs",
               "PolicePatrolBehavior.Helpers.cs", "GreyWardenPolicePurity.csproj"},
        idents={"GwpStrength", "GwpTuning", "PolicePatrolBehavior", "GwpDiagnosticsEnabled"},
        members={"GwpStrength": {"Compare", "CaseTarget"}, "GwpTuning": {"LastStand"}})


class DeadReferenceTests(unittest.TestCase):
    def test_existing_files_types_members_and_build_names_pass(self):
        text = ("`GwpStrength.cs` `GwpStrength` `GwpStrength.Compare` `GwpStrength.Case*` "
                "`GwpTuning.LastStand` `PolicePatrolBehavior.Helpers` `GreyWardenPolicePurity.csproj` "
                "`GwpDiagnosticsEnabled` `Settlement.Position` `tools/*.ps1`\n")
        self.assertEqual([], freshness.dead_references(text, known_code()))

    def test_deleted_file_type_and_member_are_reported(self):
        text = "a\n`GwpLordLastStand.cs` `GwpChokePointPolicy` `GwpTuning.LordCombat` `GwpStrength.Gone*`\n"
        self.assertEqual(
            [(2, "GwpLordLastStand.cs"), (2, "GwpChokePointPolicy"),
             (2, "GwpTuning.LordCombat"), (2, "GwpStrength.Gone*")],
            freshness.dead_references(text, known_code()))

    def test_header_lines_are_left_to_the_coverage_check(self):
        self.assertEqual([], freshness.dead_references("> 覆盖源码：`GwpGone.cs`\n", known_code()))


class ChangeImpactTests(unittest.TestCase):
    impact = load("Check-ChangeImpact")

    def test_hunks_mark_added_lines_and_the_line_a_deletion_left(self):
        diff = ("+++ b/GreyWardenPolicePurity/GwpStrength.cs\n"
                "@@ -10,2 +10,3 @@\n"
                "@@ -40,5 +41,0 @@\n"
                "+++ /dev/null\n"
                "@@ -1,9 +0,0 @@\n")
        self.assertEqual({"GreyWardenPolicePurity/GwpStrength.cs": {10, 11, 12, 41}},
                         dict(self.impact.parse_hunks(diff)))

    def test_owner_is_the_named_type_not_the_files_allowed_to_use_it(self):
        owners = [("战力", r"x", "GwpStrength.cs|GwpFireswornParties.cs", "GwpStrength"),
                  ("灰袍身份", r"x", "GwpCommon.cs", "GwpCommon.IsGreyWarden*")]
        changed = ["GreyWardenPolicePurity::GwpStrength::Compare",
                   "GreyWardenPolicePurity::GwpCommon::FindHero",
                   "GreyWardenPolicePurity::GwpFireswornParties::Spawn"]
        self.assertEqual({("战力", "GwpStrength"): {"Compare"}},
                         self.impact.owner_hits(changed, owners))
        changed.append("GreyWardenPolicePurity::GwpCommon::IsGreyWardenHero")
        self.assertIn(("灰袍身份", "GwpCommon.IsGreyWarden*"), self.impact.owner_hits(changed, owners))


class StrictModeTests(unittest.TestCase):
    def run_strict(self, *, duplicate=False, hygiene_failed=False):
        with tempfile.TemporaryDirectory() as directory, contextlib.ExitStack() as stack:
            state = Path(directory)
            (state / "example.md").write_text(
                "> 覆盖源码：跨文件，不做自动核对\n"
                "仍在用 `GwpLordLastStand.cs`。\n", encoding="utf-8")
            stack.enter_context(patch.object(freshness, "STATE", state))
            stack.enter_context(patch.object(freshness, "git", return_value=""))
            stack.enter_context(patch.object(freshness, "KnownCode", return_value=known_code()))
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

    def test_dead_reference_is_advisory_even_in_strict_mode(self):
        result, output = self.run_strict()
        self.assertEqual(0, result)
        self.assertIn("example.md:2  `GwpLordLastStand.cs`", output)

    def test_existing_mechanical_gates_still_block(self):
        for inputs in ({"duplicate": True}, {"hygiene_failed": True}):
            with self.subTest(inputs=inputs):
                self.assertEqual(1, self.run_strict(**inputs)[0])


if __name__ == "__main__":
    unittest.main()
