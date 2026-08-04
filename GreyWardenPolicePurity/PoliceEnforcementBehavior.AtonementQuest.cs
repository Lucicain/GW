using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace GreyWardenPolicePurity
{
    public partial class PoliceEnforcementBehavior
    {
        private int _atonementTargetSizeSnapshot = 0;
        private string _atonementTargetFactionId = string.Empty;
        private bool _atonementWaitingForTurnIn = false;
        private bool _awaitingAtonementQuestReconnect = false;
        private CampaignTime _lastAtonementIntelReportTime = CampaignTime.Zero;
        private AtonementQuest _atonementQuest = null!;

        internal sealed class AtonementQuest : QuestBase
        {
            [SaveableField(1)]
            private string _targetName;

            public AtonementQuest(Hero questGiver, string targetName, int repReward)
                : base(
                    GwpIds.AtonementQuestPrefix + MBRandom.RandomInt(1000, 9999),
                    questGiver,
                    CampaignTime.DaysFromNow(GwpTuning.Enforcement.AtonementDeadlineDays),
                    Math.Max(1, repReward))
            {
                _targetName = string.IsNullOrWhiteSpace(targetName) ? GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_001}Unknown Target") : targetName;
            }

            internal AtonementQuest()
                : base(GwpIds.AtonementQuestFallbackId, null, CampaignTime.Never, 0)
            {
                _targetName = GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_002}Unknown Target");
            }

            public override TextObject Title =>
                new TextObject(GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_003}the Grey Wardens Atonement: The Hunt {VAR_1}", "VAR_1", _targetName ?? GwpText.Get("{=gwp_common_unknown_target}Unknown target")));

            public override bool IsRemainingTimeHidden => false;

            public override string SpecialQuestType => GwpIds.AtonementSpecialQuestType;

            protected override void SetDialogs() { }

            protected override void InitializeQuestOnGameLoad()
            {
                try
                {
                    var behavior = Campaign.Current?.GetCampaignBehavior<PoliceEnforcementBehavior>();
                    behavior?.OnAtonementQuestLoadedFromSave(this);
                }
                catch { }
            }

            internal void WriteLog(string text)
            {
                WriteLog(new TextObject(text));
            }

            internal void WriteLog(TextObject text)
            {
                try { AddLog(text, false); } catch { }
            }

            internal void MarkReadyForTurnIn()
            {
                WriteLog(GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_004}The quarry is defeated. Report to the Warden-General or any Grey Warden to discharge the atonement."));
            }

            internal void SucceedQuestWithReputation(int gain, int currentReputation)
            {
                try
                {
                    WriteLog(GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_005}Atonement completed: reputation +{VAR_1}, current reputation {VAR_2}.", "VAR_1", gain, "VAR_2", currentReputation));
                    CompleteQuestWithSuccess();
                }
                catch { }
            }

            internal void FailQuestWithReason(string reason)
            {
                try { CompleteQuestWithFail(new TextObject(reason)); } catch { }
            }
        }

        private Hero? GetAtonementQuestGiver()
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return null;

            Hero leader = policeClan.Leader;
            if (leader != null && leader.IsActive && !leader.IsDead && !leader.IsChild && !leader.IsPrisoner)
                return leader;

            return policeClan.Heroes.FirstOrDefault(h =>
                h != null &&
                h.IsActive &&
                !h.IsDead &&
                !h.IsChild &&
                !h.IsPrisoner);
        }

        private void StartAtonementQuest()
        {
            if (_atonementQuest != null && _atonementQuest.IsOngoing) return;

            Hero? questGiver = GetAtonementQuestGiver();
            if (questGiver == null) return;

            try
            {
                _atonementQuest = new AtonementQuest(questGiver, _atonementTargetName, _atonementReputationReward);
                _atonementQuest.StartQuest();
                if (_atonementWaitingForTurnIn)
                {
                    _atonementQuest.MarkReadyForTurnIn();
                }
                else
                {
                    TextObject targetSettlement = GwpText.Create(
                        "{=gwp_policeenforcementbehavior_atonementquest_006}Unknown location");
                    MobileParty target = MobileParty.All.FirstOrDefault(p =>
                        p.StringId == _atonementTargetPartyId && p.IsActive);
                    if (target != null)
                    {
                        Settlement? nearest = FindNearestSettlement(target.GetPosition2D);
                        if (nearest != null)
                            targetSettlement = nearest.EncyclopediaLinkWithName;
                    }

                    _atonementQuest.WriteLog(
                        GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_007}Contract assigned: Defeat {VAR_2} within {VAR_1} days (case size {VAR_3} people).", "VAR_1", GwpText.Format(GwpTuning.Enforcement.AtonementDeadlineDays, "0"), "VAR_2", _atonementTargetName, "VAR_3", _atonementTargetSizeSnapshot));
                    _atonementQuest.WriteLog(
                        GwpText.Create("{=gwp_policeenforcementbehavior_atonementquest_008}First report: the quarry was last seen near {VAR_1}. When the deed is done, report to the Warden-General or any Grey Warden.", "VAR_1", targetSettlement));
                }
            }
            catch
            {
                _atonementQuest = null!;
            }
        }

        private static Settlement? FindNearestSettlement(Vec2 position)
            => GwpCommon.FindNearestSettlement(position);

        private void AppendAtonementIntelLog(MobileParty target)
        {
            if (target == null || !target.IsActive) return;

            int currentSize = Math.Max(1, target.Party?.NumberOfAllMembers ?? 1);
            Settlement? nearestSettlement = FindNearestSettlement(target.GetPosition2D);
            string nearestSettlementName = nearestSettlement?.Name?.ToString() ??
                                           GwpText.Get(
                                               "{=gwp_policeenforcementbehavior_atonementquest_009}Unknown location");
            TextObject nearestSettlementLink = nearestSettlement?.EncyclopediaLinkWithName ??
                                               GwpText.Create(
                                                   "{=gwp_policeenforcementbehavior_atonementquest_009}Unknown location");
            TextObject intelLog = GwpText.Create(
                "{=gwp_policeenforcementbehavior_atonementquest_010}Spy report: {VAR_1} recently appeared near {VAR_2} (about {VAR_3} people).",
                "VAR_1", _atonementTargetName,
                "VAR_2", nearestSettlementLink,
                "VAR_3", currentSize);
            string intelMessage = GwpText.Get(
                "{=gwp_policeenforcementbehavior_atonementquest_010}Spy report: {VAR_1} recently appeared near {VAR_2} (about {VAR_3} people).",
                "VAR_1", _atonementTargetName,
                "VAR_2", nearestSettlementName,
                "VAR_3", currentSize);

            try { _atonementQuest?.WriteLog(intelLog); } catch { }
            InformationManager.DisplayMessage(new InformationMessage(intelMessage, Colors.Cyan));
        }

        private void TryRestoreAtonementQuestOnSessionStart()
        {
            if (!HasAtonementTask) return;

            PlayerState.SetAtonementTaskActive(true);
            _awaitingAtonementQuestReconnect = true;
        }

        private void TryReconnectAtonementQuestOnHourlyTick()
        {
            if (!_awaitingAtonementQuestReconnect) return;
            _awaitingAtonementQuestReconnect = false;

            if (!HasAtonementTask)
                return;

            try
            {
                AtonementQuest? existing = Campaign.Current?.QuestManager?.Quests
                    ?.OfType<AtonementQuest>()
                    ?.FirstOrDefault(q => q.IsOngoing);
                if (existing != null)
                {
                    _atonementQuest = existing;
                    if (IsAtonementWaitingForTurnInState)
                        existing.MarkReadyForTurnIn();
                    else
                        existing.WriteLog(GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_011}Load recovery: Continue to track the atonement target."));
                    return;
                }
            }
            catch { }

            StartAtonementQuest();
            if (_atonementQuest != null && _atonementQuest.IsOngoing)
            {
                if (IsAtonementWaitingForTurnInState)
                    _atonementQuest.MarkReadyForTurnIn();
                else
                    _atonementQuest.WriteLog(GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_012}Load recovery: Continue to track the atonement target."));
            }
        }

        internal void OnAtonementQuestLoadedFromSave(AtonementQuest quest)
        {
            if (quest == null || !quest.IsOngoing) return;
            if (!HasAtonementTask) return;

            _atonementQuest = quest;
            _awaitingAtonementQuestReconnect = false;
            if (IsAtonementWaitingForTurnInState)
                quest.MarkReadyForTurnIn();
            else
                quest.WriteLog(GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_013}Load recovery: Continue to track the atonement target."));
        }

        private bool EnforcementAtonementTurnInCondition()
        {
            if (!IsAtonementWaitingForTurnInState) return false;

            Hero conversationHero = Hero.OneToOneConversationHero;
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (conversationHero == null || policeClan == null) return false;
            if (conversationHero.Clan != policeClan) return false;
            if (!conversationHero.IsActive || conversationHero.IsDead || conversationHero.IsChild) return false;

            MBTextManager.SetTextVariable(
                "GWP_ENFORCEMENT_ATONEMENT_TURNIN_OPTION",
                GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_014}Concerning the atonement (up to {VAR_1} standing may be restored upon discharge)", "VAR_1", _atonementReputationReward));
            MBTextManager.SetTextVariable(
                "GWP_ENFORCEMENT_ATONEMENT_TURNIN_TEXT",
                GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_015}The account is verified. Your atonement is complete, and the roll allows the restoration of up to {VAR_1} standing.", "VAR_1", _atonementReputationReward));
            return true;
        }

        private void OnEnforcementAtonementTurnInConsequence()
        {
            int before = PlayerState.Reputation;
            int after = Math.Min(0, before + Math.Max(1, _atonementReputationReward));
            int gain = after - before;
            PlayerState.ResetReputation(after);
            MakePeaceWithAtonementTargetFaction();

            try { _atonementQuest?.SucceedQuestWithReputation(gain, after); } catch { }

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_016}Atonement contract delivered: Reputation +{VAR_1} (currently {VAR_2})", "VAR_1", gain, "VAR_2", after),
                Colors.Green));

            ClearAtonementTaskState();
        }

        private void MakePeaceWithAtonementTargetFaction()
        {
            if (string.IsNullOrEmpty(_atonementTargetFactionId)) return;

            IFaction? playerFaction = Hero.MainHero?.MapFaction;
            if (playerFaction == null) return;

            IFaction targetFaction = Kingdom.All.FirstOrDefault(k => k.StringId == _atonementTargetFactionId)
                ?? (IFaction)Clan.All.FirstOrDefault(c => c.StringId == _atonementTargetFactionId);
            if (targetFaction == null || targetFaction == playerFaction) return;
            if (!FactionManager.IsAtWarAgainstFaction(playerFaction, targetFaction)) return;

            try
            {
                MakePeaceAction.Apply(playerFaction, targetFaction);
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_policeenforcementbehavior_atonementquest_017}Atonement delivered, the Grey Wardens Mediation: Peace has been restored between you and {VAR_1}.", "VAR_1", targetFaction.Name),
                    Colors.Green));
            }
            catch
            {
                GwpCommon.TrySetNeutral(playerFaction, targetFaction);
            }
        }
    }
}
