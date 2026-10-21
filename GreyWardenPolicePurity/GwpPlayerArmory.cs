using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        internal static bool CanManagePlayerArmory => _instance != null &&
            Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()?.IsRecruitedByGreyWardens == true &&
            GwpRuntimeState.Player.Reputation >= GwpTuning.Bounty.RecruitmentReputationThreshold &&
            MobileParty.MainParty?.IsActive == true && MobileParty.MainParty.MapEvent == null;

        internal static ItemRoster PlayerArmoryItems => _instance != null && MobileParty.MainParty != null &&
            _instance._armories.TryGetValue(MobileParty.MainParty.StringId, out ItemRoster? ledger)
                ? ledger : new ItemRoster();

        internal static bool OpenPlayerArmory()
        {
            if (!CanManagePlayerArmory) return false;
            MobileParty party = MobileParty.MainParty;
            ItemRoster ledger = ItemsOf(_instance!._armories, party.StringId);
            InventoryScreenHelper.OpenScreenAsStash(ledger);
            InventoryState state = (InventoryState)Game.Current.GameStateManager.ActiveState;
            // 原版取消会恢复双方库存，但仍调用DoneLogicExtras；不要把取消当成提交。
            bool cancelled = false;
            state.InventoryLogic.AfterReset += (_, fromCancel) => cancelled = fromCancel;
            state.DoneLogicExtrasDelegate = () =>
            {
                if (!cancelled) _instance.TrimPlayerArmory(party);
            };
            return true;
        }

        private void TrimPlayerArmory(MobileParty party)
        {
            // 原版Stash负责实际存取与取消；只把同一配装规则判定的余量退回玩家背包。
            ArmoryPlan plan = BuildPlanFor(party.StringId);
            MoveAll(plan.Surplus, ItemsOf(_armories, party.StringId), party.ItemRoster);
            _plans.Remove(party.StringId);
        }
    }
}
