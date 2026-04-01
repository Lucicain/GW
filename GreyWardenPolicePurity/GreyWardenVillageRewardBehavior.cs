using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Village-backed stipend for positive player reputation.
    /// </summary>
    public sealed class GreyWardenVillageRewardBehavior : CampaignBehaviorBase
    {
        private const string StoredRewardKey = "GWPP_VillageRewardStored";
        private const string SelectedClaimKey = "GWPP_VillageRewardSelectedClaim";

        private int _storedReward;
        private int _selectedClaimAmount;

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(StoredRewardKey, ref _storedReward);
            dataStore.SyncData(SelectedClaimKey, ref _selectedClaimAmount);

            if (dataStore.IsLoading)
            {
                _storedReward = Math.Max(0, _storedReward);
                ClampSelection();
            }
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            _storedReward = 0;
            _selectedClaimAmount = 0;
        }

        private void OnDailyTick()
        {
            int reputation = GwpRuntimeState.Player.Reputation;
            if (reputation < 0)
            {
                ClearStoredReward();
                return;
            }

            if (reputation <= 0)
            {
                ClampSelection();
                return;
            }

            _storedReward += reputation * GwpTuning.VillageReward.DenarsPerReputationPerDay;
            EnsureDefaultSelection();
        }

        private void OnHourlyTick()
        {
            if (GwpRuntimeState.Player.Reputation < 0)
            {
                ClearStoredReward();
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(
                "village",
                "gwp_village_reward",
                "{GWP_VILLAGE_REWARD_OPTION}",
                VillageRewardOptionCondition,
                VillageRewardOptionConsequence,
                isLeave: false,
                index: 6);
        }

        private bool VillageRewardOptionCondition(MenuCallbackArgs args)
        {
            EnsureRewardMatchesReputation();

            Settlement? settlement = Settlement.CurrentSettlement;
            if (settlement?.IsVillage != true)
            {
                return false;
            }

            int reputation = GwpRuntimeState.Player.Reputation;
            if (reputation <= 0)
            {
                return false;
            }

            EnsureDefaultSelection();
            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            MBTextManager.SetTextVariable(
                "GWP_VILLAGE_REWARD_OPTION",
                $"领取村民酬谢（可领 {_storedReward} 第纳尔）");
            return true;
        }

        private void VillageRewardOptionConsequence(MenuCallbackArgs args)
        {
            EnsureRewardMatchesReputation();

            if (_storedReward <= 0)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage("现在还没有村民筹出的酬谢可领。", Colors.Yellow));
                return;
            }

            GreyWardenVillageRewardSliderScreen.Show(
                _storedReward,
                _selectedClaimAmount > 0 ? _selectedClaimAmount : _storedReward,
                ConfirmClaimFromSlider);
        }

        private void ConfirmClaimFromSlider(int amount)
        {
            _selectedClaimAmount = Math.Max(0, Math.Min(amount, _storedReward));
            ClaimReward(_selectedClaimAmount);
        }

        private void ClaimReward(int requestedAmount)
        {
            EnsureRewardMatchesReputation();

            int amount = Math.Max(0, Math.Min(requestedAmount, _storedReward));
            if (amount <= 0)
            {
                return;
            }

            Hero.MainHero.ChangeHeroGold(amount);
            _storedReward -= amount;
            ClampSelection();

            InformationManager.DisplayMessage(
                new InformationMessage(
                    $"你从村民酬谢中领取了 {amount} 第纳尔。剩余 {_storedReward} 第纳尔。",
                    Colors.Green));
        }

        private void EnsureRewardMatchesReputation()
        {
            if (GwpRuntimeState.Player.Reputation < 0)
            {
                ClearStoredReward();
            }
            else
            {
                ClampSelection();
            }
        }

        private void ClearStoredReward()
        {
            _storedReward = 0;
            _selectedClaimAmount = 0;
        }

        private void EnsureDefaultSelection()
        {
            ClampSelection();
            if (_storedReward > 0 && _selectedClaimAmount <= 0)
            {
                _selectedClaimAmount = _storedReward;
            }
        }

        private void ClampSelection()
        {
            if (_storedReward <= 0)
            {
                _selectedClaimAmount = 0;
                return;
            }

            _selectedClaimAmount = Math.Max(0, Math.Min(_selectedClaimAmount, _storedReward));
        }
    }
}
