using TaleWorlds.CampaignSystem;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchBehavior
    {
        private void RegisterSupportDialogue(CampaignGameStarter starter)
        {
            starter.AddDialogLine("gwp_cs_support_gate_start", "start", "gwp_cs_support_gate_options",
                GwpText.Get(GwpTextCatalog.gwp_cs_guard_challenge), () => Supported && Scene?.IsGuardTalk == true, null, 210);
            starter.AddPlayerLine("gwp_cs_support_explain", "gwp_cs_support_gate_options", "gwp_cs_support_gate_pass",
                "{=!}{GWP_CS_EXPLAIN}", PrepareExplain, null);
            starter.AddDialogLine("gwp_cs_support_gate_pass", "gwp_cs_support_gate_pass", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_support_gate), null, () => Scene?.OnGuardDecision(true, "warden_support"));
            starter.AddDialogLine("gwp_cs_support_hall_start", "start", "gwp_cs_support_hall_options",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_start), () => Supported && Scene?.IsHallTalk == true, null, 230);
            starter.AddPlayerLine("gwp_cs_support_hall_warrant", "gwp_cs_support_hall_options", "gwp_cs_hall_next",
                GwpText.Get(GwpTextCatalog.gwp_cs_support_warrant), null, BeginHallPersuasion);
            starter.AddPlayerLine("gwp_cs_support_hall_leave", "gwp_cs_support_hall_options", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_leave), null, () => Scene?.LeaveHall());
            starter.AddDialogLine("gwp_cs_support_hall_surrender", "gwp_cs_hall_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_support_surrender), () => Supported && _hallRound.IsSatisfied,
                () => { _hallRound.End(); Scene?.AcceptHallSurrender(); }, 120);
            starter.AddDialogLine("gwp_cs_support_hall_duel", "gwp_cs_hall_next", "gwp_cs_support_duel_options",
                GwpText.Get(GwpTextCatalog.gwp_cs_support_duel), () => Supported && _hallRound.IsFailed,
                () => _hallRound.End(), 120);
            starter.AddPlayerLine("gwp_cs_support_accept_duel", "gwp_cs_support_duel_options", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_duel_start), null, () => Scene?.ChooseHallCombat(true));
        }
    }
}
