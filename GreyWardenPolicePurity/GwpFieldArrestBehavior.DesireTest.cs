#if GWP_DIAGNOSTICS
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFieldArrestBehavior
    {
        // TEMPORARY TEST SCAFFOLD. Delete this file, its registration/ClearState hooks
        // when the eight toolbox routes are accepted.
        private static int _testDesireNext;
        private bool _testDesireApplied;
        private static readonly GwpOffenderDesire[] TestDesires =
        {
            GwpOffenderDesire.PayInFull, GwpOffenderDesire.HaggleDown,
            GwpOffenderDesire.CunningHalf, GwpOffenderDesire.SurrenderSelf,
            GwpOffenderDesire.DeedsOnly, GwpOffenderDesire.BribeTheWarden,
            GwpOffenderDesire.DemandDuel, GwpOffenderDesire.RequestGrace
        };

        private void RegisterDesireTestDialogues(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("gwp_test_desire_open", "gwp_fa_charge_options", "gwp_test_desire_menu",
                GwpText.Get("{=gwp_test_desire_open}[Test] Skip the first layer and choose a desire."), ChargeCondition, null, 120);
            starter.AddDialogLine("gwp_test_desire_menu", "gwp_test_desire_menu", "gwp_test_desire_choices",
                GwpText.Get("{=gwp_test_desire_menu}[Test controls] Select the result to test. The normal settlement rules still apply."), null, null);
            starter.AddPlayerLine("gwp_test_desire_next", "gwp_test_desire_choices", "gwp_fa_layer2_demand",
                GwpText.Get("{=gwp_test_desire_next}[Test] Continue with the next desire."), null, () => StartSelectedDesireTest(-1));
            for (int i = 0; i < TestDesires.Length; i++)
            {
                int selected = i;
                starter.AddPlayerLine("gwp_test_desire_choose_" + i, "gwp_test_desire_choices", "gwp_fa_layer2_demand",
                    "[" + (i + 1) + "/8] " + TestDesireName(i), () => selected != 7 || CanOfferGrace, () => StartSelectedDesireTest(selected));
            }
            starter.AddPlayerLine("gwp_test_desire_back", "gwp_test_desire_choices", "gwp_fa_greeting",
                GwpText.Get("{=gwp_test_desire_back}[Test] Return to the normal conversation."), null, null);
        }

        private void StartSelectedDesireTest(int selected)
        {
            EndPersuasionConsequence();
            _enforcementAccepted = true;
            _paymentAccepted = false;
            _testDesireApplied = false;
            if (selected >= 0) _testDesireNext = selected;
            ApplyDesireSequenceTest();
        }

        private void ApplyDesireSequenceTest()
        {
            if (_testDesireApplied || !_enforcementAccepted || _offender == null) return;
            _testDesireApplied = true;
            int index = ((_testDesireNext % TestDesires.Length) + TestDesires.Length) % TestDesires.Length;
            if (index == 7 && !CanOfferGrace) index = 0;
            _desire = TestDesires[index];
            _testDesireNext = (index + 1) % TestDesires.Length;
            // Force only the proposed result. Do not edit permanent personality,
            // purse, army, persuasion difficulty, or settlement acceptance rules.
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_test_desire_sequence}[Test] Desire {VAR_1}/8: {VAR_2}. This encounter keeps these terms.",
                    "VAR_1", (index + 1).ToString(), "VAR_2", TestDesireName(index)), Colors.Cyan));
            GwpAiDiagnostics.WriteFieldArrest("DESIRE_SEQUENCE_TEST", "index=" + (index + 1)
                + "; offender=" + _offender.StringId + "; desire=" + _desire
                + "; gold=" + _offender.Gold + "; fine=" + _fine);
        }

        private static string TestDesireName(int index) => index switch
        {
            0 => GwpText.Get("{=gwp_test_tool_0}Full payment"),
            1 => GwpText.Get("{=gwp_test_tool_1}Bargain"),
            2 => GwpText.Get("{=gwp_test_tool_2}Conceal money"),
            3 => GwpText.Get("{=gwp_test_tool_3}Surrender"),
            4 => GwpText.Get("{=gwp_test_tool_4}Sacrifice soldiers"),
            5 => GwpText.Get("{=gwp_test_tool_5}Bribe"),
            6 => GwpText.Get("{=gwp_test_tool_6}Duel"),
            _ => GwpText.Get("{=gwp_test_tool_7}Three-day grace")
        };
    }
}
#endif
