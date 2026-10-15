using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
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
        // Native barter blocks campaign saving; this selection belongs only to the open dialogue.
        private GwpAssetPayment? _atonementPayment;
        private bool _atonementTurnInOpened;

        private Hero? AtonementTargetHero => string.IsNullOrWhiteSpace(_atonementTargetHeroId)
            ? null : Hero.Find(_atonementTargetHeroId);
        private MobileParty? AtonementTargetParty => AtonementTargetHero?.PartyBelongedTo
            ?? FindActiveParty(_atonementTargetPartyId);

        internal bool HoldsAtonementCase(string? heroId) => HasAtonementTask &&
            !string.IsNullOrWhiteSpace(heroId) &&
            string.Equals(heroId, _atonementTargetHeroId, StringComparison.OrdinalIgnoreCase);
        internal bool IsActiveAtonementTarget(Hero? hero) => IsAtonementActiveState && hero != null &&
            (HoldsAtonementCase(hero.StringId) ||
             (string.IsNullOrEmpty(_atonementTargetHeroId) && hero.PartyBelongedTo?.StringId == _atonementTargetPartyId));
        internal bool ReservesAtonementTarget(MobileParty? party)
        {
            if (!HasAtonementTask || party == null) return false;
            MobileParty? target = AtonementTargetParty;
            return party.StringId == _atonementTargetPartyId || party == target ||
                (target?.Army?.LeaderParty != null && party == target.Army.LeaderParty);
        }
        internal bool CanOfferAtonementGrace => IsAtonementActiveState &&
            _atonementDeadlineHours - CampaignTime.Now.ToHours >= GwpTuning.FieldArrest.GraceHours;
        internal void RecordAtonementGrace(string message) => _atonementQuest?.WriteLog(message);

        internal void NotifyAtonementResolved(Hero? hero, string source)
        {
            if (IsActiveAtonementTarget(hero)) MarkAtonementReady(source);
        }

        private void MarkAtonementReady(string source)
        {
            if (!IsAtonementActiveState) return;
            SetAtonementFlowState(AtonementFlowState.WaitingForTurnIn);
            _atonementDeadlineHours = 0f;
            _atonementQuest?.MarkReadyForTurnIn();
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFieldArrest("ATONEMENT_READY",
                "hero=" + _atonementTargetHeroId + "; party=" + _atonementTargetPartyId + "; source=" + source);
#endif
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_003, "VAR_1", _atonementTargetName), Colors.Green));
        }

        private void WithdrawAtonementTask()
        {
            string message = GwpText.Get(GwpTextCatalog.gwp_atonement_closed_elsewhere);
            if (_atonementQuest?.IsOngoing == true)
                _atonementQuest.CompleteQuestWithCancel(new TextObject(message));
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFieldArrest("ATONEMENT_WITHDRAWN", "hero=" + _atonementTargetHeroId);
#endif
            ClearAtonementTaskState();
            InformationManager.DisplayMessage(new InformationMessage(message, Colors.Yellow));
        }

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
                _targetName = string.IsNullOrWhiteSpace(targetName) ? GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_001) : targetName;
            }

            internal AtonementQuest()
                : base(GwpIds.AtonementQuestFallbackId, null, CampaignTime.Never, 0)
            {
                _targetName = GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_002);
            }

            public override TextObject Title =>
                new TextObject(GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_003, "VAR_1", _targetName ?? GwpText.Get(GwpTextCatalog.gwp_common_unknown_target)));

            public override bool IsRemainingTimeHidden => false;

            public override string SpecialQuestType => GwpIds.AtonementSpecialQuestType;

            protected override void SetDialogs() { }

            protected override void InitializeQuestOnGameLoad()
            {
                var behavior = Campaign.Current?.GetCampaignBehavior<PoliceEnforcementBehavior>();
                behavior?.OnAtonementQuestLoadedFromSave(this);
            }

            internal void WriteLog(string text)
            {
                WriteLog(new TextObject(text));
            }

            internal void WriteLog(TextObject text)
            {
                AddLog(text, false);
            }

            internal void MarkReadyForTurnIn()
            {
                ChangeQuestDueTime(CampaignTime.Never);
                WriteLog(GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_004));
            }

            internal void SucceedQuestWithReputation(int gain, int currentReputation)
            {
                WriteLog(GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_005, "VAR_1", gain, "VAR_2", currentReputation));
                CompleteQuestWithSuccess();
            }

            internal void FailQuestWithReason(string reason)
            {
                CompleteQuestWithFail(new TextObject(reason));
            }
        }

        private void StartAtonementQuest()
        {
            if (_atonementQuest != null && _atonementQuest.IsOngoing) return;

            Hero? questGiver = GwpCommon.FindGreyWardenSpokesperson();
            if (questGiver == null) return;

            _atonementQuest = new AtonementQuest(questGiver, _atonementTargetName, _atonementReputationReward);
            _atonementQuest.StartQuest();
            if (_atonementWaitingForTurnIn)
            {
                _atonementQuest.MarkReadyForTurnIn();
            }
            else
            {
                TextObject targetSettlement = GwpText.Create(
                    GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_006);
                MobileParty target = MobileParty.All.FirstOrDefault(p =>
                    p.StringId == _atonementTargetPartyId && p.IsActive);
                if (target != null)
                {
                    Settlement? nearest = FindNearestSettlement(target.GetPosition2D);
                    if (nearest != null)
                        targetSettlement = nearest.EncyclopediaLinkWithName;
                }

                _atonementQuest.WriteLog(
                    GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_007, "VAR_1", GwpText.Format(GwpTuning.Enforcement.AtonementDeadlineDays, "0"), "VAR_2", _atonementTargetName, "VAR_3", _atonementTargetSizeSnapshot));
                _atonementQuest.WriteLog(
                    GwpText.Create(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_008, "VAR_1", targetSettlement));
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
                                               GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_009);
            TextObject nearestSettlementLink = nearestSettlement?.EncyclopediaLinkWithName ??
                                               GwpText.Create(
                                                   GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_009);
            TextObject intelLog = GwpText.Create(
                GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_010,
                "VAR_1", _atonementTargetName,
                "VAR_2", nearestSettlementLink,
                "VAR_3", currentSize);
            string intelMessage = GwpText.Get(
                GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_010,
                "VAR_1", _atonementTargetName,
                "VAR_2", nearestSettlementName,
                "VAR_3", currentSize);

            _atonementQuest?.WriteLog(intelLog);
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

            AtonementQuest? existing = Campaign.Current?.QuestManager?.Quests
                ?.OfType<AtonementQuest>()
                ?.FirstOrDefault(q => q.IsOngoing);
            if (existing != null)
            {
                _atonementQuest = existing;
                if (IsAtonementWaitingForTurnInState)
                    existing.MarkReadyForTurnIn();
                else
                    existing.WriteLog(GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_011));
                return;
            }

            StartAtonementQuest();
            if (_atonementQuest != null && _atonementQuest.IsOngoing)
            {
                if (IsAtonementWaitingForTurnInState)
                    _atonementQuest.MarkReadyForTurnIn();
                else
                    _atonementQuest.WriteLog(GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_012));
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
                quest.WriteLog(GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_013));
        }

        private bool EnforcementAtonementTurnInCondition()
        {
            if (!IsAtonementWaitingForTurnInState) return false;

            Hero conversationHero = Hero.OneToOneConversationHero;
            if (!GwpCommon.IsGreyWardenClanMember(conversationHero)) return false;
            if (!conversationHero.IsActive || conversationHero.IsDead || conversationHero.IsChild) return false;

            MBTextManager.SetTextVariable(
                "GWP_ENFORCEMENT_ATONEMENT_TURNIN_OPTION",
                GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_014, "VAR_1", _atonementReputationReward));
            MBTextManager.SetTextVariable(
                "GWP_ENFORCEMENT_ATONEMENT_TURNIN_TEXT",
                GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_015, "VAR_1", _atonementReputationReward));
            return true;
        }

        private void OnEnforcementAtonementTurnInConsequence()
        {
            if (!IsAtonementWaitingForTurnInState || !_atonementTurnInOpened ||
                (_atonementPayment != null && !_atonementPayment.Applied)) return;
            int delivered = _atonementPayment?.Paid ?? 0;
            if (GwpFieldReportLedger.Instance?.PendingAssessedFor(_atonementTargetHeroId) > 0)
                GwpFieldReportLedger.Instance.DeclareAmount(delivered, offenderId: _atonementTargetHeroId);
            _atonementPayment = null;
            _atonementTurnInOpened = false;
            int before = PlayerState.Reputation;
            int after = before < 0 ? Math.Min(0, before + Math.Max(1, _atonementReputationReward)) : before;
            int gain = after - before;
            PlayerState.ResetReputation(after);
            MakePeaceWithAtonementTargetFaction();

            _atonementQuest?.SucceedQuestWithReputation(gain, after);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFieldArrest("ATONEMENT_TURNED_IN",
                "hero=" + _atonementTargetHeroId + "; delivered=" + delivered +
                "; gain=" + gain + "; reputation=" + after);
#endif

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_016, "VAR_1", gain, "VAR_2", after),
                Colors.Green));

            ClearAtonementTaskState();
        }

        private void BeginAtonementTurnIn()
        {
            _atonementTurnInOpened = false;
            _atonementPayment = null;
            if (!IsAtonementWaitingForTurnInState) return;
            GwpFieldReportLedger? ledger = GwpFieldReportLedger.Instance;
            int received = ledger?.PendingReceivedFor(_atonementTargetHeroId) ?? 0;
            Hero? captive = AtonementTargetHero;
            if (captive?.IsPrisoner != true || captive.PartyBelongedToAsPrisoner != MobileParty.MainParty.Party)
                captive = null;
            if (received == 0 && captive == null) { _atonementTurnInOpened = true; return; }
            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            PartyBase? clerk = MobileParty.ConversationParty?.Party
                ?? Hero.OneToOneConversationHero?.PartyBelongedTo?.Party
                ?? Hero.OneToOneConversationHero?.CurrentSettlement?.Party;
            if (treasurer == null || clerk == null) return;
            _atonementPayment = new GwpAssetPayment(Hero.MainHero, treasurer,
                MobileParty.MainParty.Party, clerk, int.MaxValue, received,
                reportMode: true, autoReceipt: ledger?.ReceiptFor(_atonementTargetHeroId), prisoner: captive);
            _atonementTurnInOpened = true;
            BarterManager manager = Campaign.Current.BarterManager;
            BarterManager.BarterBeginEventDelegate original = manager.BarterBegin;
            try
            {
                manager.BarterBegin = data => { _atonementPayment.PrepareCatalogue(data); original?.Invoke(data); };
                manager.StartBarterOffer(Hero.MainHero, treasurer, MobileParty.MainParty.Party, clerk,
                    null, AtonementPaymentContext, 0, false, _atonementPayment.Entries);
            }
            finally { manager.BarterBegin = original; }
        }

        private bool AtonementPaymentContext(Barterable barterable, BarterData data, object obj) => false;

        private bool AtonementPaymentAccepted() => _atonementTurnInOpened &&
            (_atonementPayment == null || _atonementPayment.Applied);

        private void MakePeaceWithAtonementTargetFaction()
        {
            if (string.IsNullOrEmpty(_atonementTargetFactionId)) return;

            IFaction? playerFaction = Hero.MainHero?.MapFaction;
            if (playerFaction == null) return;

            IFaction targetFaction = Kingdom.All.FirstOrDefault(k => k.StringId == _atonementTargetFactionId)
                ?? (IFaction)Clan.All.FirstOrDefault(c => c.StringId == _atonementTargetFactionId);
            if (targetFaction == null || targetFaction == playerFaction) return;
            if (!FactionManager.IsAtWarAgainstFaction(playerFaction, targetFaction)) return;

            GwpPeace.Make(playerFaction, targetFaction);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_policeenforcementbehavior_atonementquest_017, "VAR_1", targetFaction.Name),
                Colors.Green));
        }
    }
}
