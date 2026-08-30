using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GreyWardenPolicePurity
{
    public partial class PoliceEnforcementBehavior
    {
        // 对话临时变量（警察执法拦截：让玩家选择缴纳或战斗）
        private int _dialogFine = 0;
        private MobileParty _dialogPolice = null!;
        private PoliceTask _dialogTask = null!;
        private bool _enforcementBarterInProgress = false;
        private bool _enforcementAtonementAssigned = false;
        private bool _enforcementEncounterFinishQueued = false;

        #region 对话系统（执法拦截：玩家可选择缴纳罚金或战斗）

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Do not run this from SyncData: Hero.FindFirst's source collection is not
            // guaranteed to exist until campaign initialization has completed.
            CrimeState.Clean();

            starter.AddDialogLine(
                "gwp_enforcement_start",
                "start",
                "gwp_enforcement_options",
                "{" + GwpTextKeys.EnforcementGreeting + "}",
                EnforcementDialogCondition,
                null,
                100);

            starter.AddPlayerLine(
                "gwp_enforcement_pay",
                "gwp_enforcement_options",
                "gwp_enforcement_pay_barter_pre",
                "{" + GwpTextKeys.EnforcementPayText + "}",
                EnforcementPayCondition,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_pre",
                "gwp_enforcement_pay_barter_pre",
                "gwp_enforcement_pay_barter_screen",
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_001}By Grey Warden ordinance, the lawful fine must first be paid."),
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_screen",
                "gwp_enforcement_pay_barter_screen",
                "gwp_enforcement_pay_barter_post",
                "{=!}Barter screen goes here",
                null,
                OnEnforcementPayBarterConsequence,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_post_success",
                "gwp_enforcement_pay_barter_post",
                "close_window",
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_002}The fine is acknowledged. This case is closed; you may depart."),
                EnforcementBarterSuccessfulCondition,
                OnEnforcementPayAcceptedConsequence,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_post_failed",
                "gwp_enforcement_pay_barter_post",
                "gwp_enforcement_options",
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_003}Your offer falls below the lawful fine. You may raise it or refuse the order."),
                () => !EnforcementBarterSuccessfulCondition(),
                OnEnforcementPayRejectedConsequence,
                100);

            starter.AddPlayerLine(
                "gwp_enforcement_atonement",
                "gwp_enforcement_options",
                "gwp_enforcement_atonement_result",
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_004}I confess the offence and accept judgment. Grant me a charge of atonement."),
                EnforcementAtonementCondition,
                OnEnforcementAtonementConsequence,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_atonement_success",
                "gwp_enforcement_atonement_result",
                "close_window",
                "{" + GwpTextKeys.EnforcementAtonementText + "}",
                () => _enforcementAtonementAssigned,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_atonement_failed",
                "gwp_enforcement_atonement_result",
                "gwp_enforcement_options",
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_005}No charge of atonement can presently be assigned. You may pay the fine or refuse the order."),
                () => !_enforcementAtonementAssigned,
                null,
                100);

            starter.AddPlayerLine(
                "gwp_enforcement_fight",
                "gwp_enforcement_options",
                "gwp_enforcement_fight_response",
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_006}I refuse the order. Let arms decide."),
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_fight_response",
                "gwp_enforcement_fight_response",
                "close_window",
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_007}Resistance is entered upon the roll. Grey Wardens—make the arrest!"),
                null,
                OnEnforcementFightConsequence,
                100);

            starter.AddPlayerLine(
                "gwp_enforcement_atonement_turnin",
                "lord_talk_speak_diplomacy_2",
                "gwp_enforcement_atonement_turnin_response",
                "{GWP_ENFORCEMENT_ATONEMENT_TURNIN_OPTION}",
                EnforcementAtonementTurnInCondition,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_atonement_turnin_response",
                "gwp_enforcement_atonement_turnin_response",
                "lord_pretalk",
                "{GWP_ENFORCEMENT_ATONEMENT_TURNIN_TEXT}",
                null,
                OnEnforcementAtonementTurnInConsequence,
                100);

            PlayerState.SetAtonementTaskActive(HasAtonementTask);
            TryRestoreAtonementQuestOnSessionStart();
        }

        private bool EnforcementDialogCondition()
        {
            MobileParty? conversationParty = MobileParty.ConversationParty;
            if (conversationParty == null) return false;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return false;

            if (conversationParty.ActualClan != policeClan) return false;
            if (GwpCommon.IsPatrolParty(conversationParty)) return false;

            PoliceTask? task = CrimeState.GetTask(conversationParty.StringId);
            if (task == null) return false;
            if (task.TargetCrime?.Offender?.IsMainParty != true) return false;
            if (task.FlowState != PoliceTaskFlowState.Pursuit) return false;

            int rep = PlayerState.Reputation;
            _dialogFine = Math.Abs(rep) * 300;
            _dialogPolice = conversationParty;
            _dialogTask = task;
            // Arm the same retry guard for a player-initiated encounter as for
            // the automatic EngageParty bridge.  Closing the conversation
            // without choosing an outcome must not immediately reopen it.
            _nextPlayerEnforcementContactHour = CampaignTime.Now.ToHours +
                GwpTuning.PlayerRequests.DeferredContactHours;

            int playerGold = Hero.MainHero.Gold;
            GwpAiDiagnostics.WritePlayerJusticeState(
                "ENFORCEMENT_FINE_DIALOG_OPENED",
                "police=" + conversationParty.StringId +
                "; taskState=" + task.FlowState +
                "; fine=" + _dialogFine +
                "; playerGold=" + playerGold);
            string payInfo = playerGold >= _dialogFine
                ? GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_008}You carry {VAR_1} denars, enough to pay in full.", "VAR_1", playerGold)
                : GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_009}You carry {VAR_1} denars. You may make another offer at the table, or confess and accept judgment.", "VAR_1", playerGold);

            MBTextManager.SetTextVariable(GwpTextKeys.EnforcementGreeting,
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_010}Stand! The Grey Wardens come under warrant. Your present standing is {VAR_1},", "VAR_1", Math.Abs(rep)) +
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_011}and the lawful fine in this case is {VAR_1} denars. {VAR_2}", "VAR_1", _dialogFine, "VAR_2", payInfo));

            return true;
        }

        private bool EnforcementPayCondition()
        {
            MBTextManager.SetTextVariable(
                GwpTextKeys.EnforcementPayText,
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_012}Pay the lawful fine ({VAR_1} denars; clear the warrant)", "VAR_1", _dialogFine));
            return true;
        }

        private bool EnforcementAtonementCondition()
        {
            if (HasAtonementTask) return false;
            if (_dialogFine <= 0) return false;
            return Hero.MainHero.Gold < _dialogFine;
        }

        private void OnEnforcementAtonementConsequence()
        {
            _enforcementAtonementAssigned = TryAssignAtonementTask();
            if (_enforcementAtonementAssigned)
                QueueFinishEnforcementEncounter();
        }

        private bool TryAssignAtonementTask()
        {
            if (HasAtonementTask) return false;

            CrimeRecord? targetCrime = CrimeState.GetNearestNonPlayerFromAll(
                MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero);
            if (targetCrime == null || targetCrime.Offender == null || !targetCrime.Offender.IsActive)
                return false;

            MobileParty offender = targetCrime.Offender;
            int targetSizeSnapshot = Math.Max(1, offender.Party?.NumberOfAllMembers ?? 1);
            int rewardRep = Math.Max(1, (int)Math.Ceiling(targetSizeSnapshot / 10f));

            SetAtonementFlowState(AtonementFlowState.Active);
            _atonementTargetPartyId = offender.StringId ?? string.Empty;
            _atonementTargetName = offender.Name?.ToString() ?? GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_013}unknown quarry");
            _atonementTargetHeroId = targetCrime.OffenderHeroId ?? string.Empty;
            _atonementTargetCrimeCategory = (int)targetCrime.CrimeCategory;
            _atonementTargetFactionId = offender.MapFaction?.StringId ?? string.Empty;
            _atonementTargetSizeSnapshot = targetSizeSnapshot;
            _atonementReputationReward = rewardRep;
            _atonementDeadlineHours = (float)(CampaignTime.Now.ToHours + GwpTuning.Enforcement.AtonementDeadlineDays * 24f);
            _lastAtonementIntelReportTime = CampaignTime.Now;
            StartAtonementQuest();

            CrimeState.EndPlayerHunt();
            if (_dialogPolice != null && _dialogPolice.IsActive)
            {
                GreyWardenPartyDesireBehavior.ClearIntent(_dialogPolice);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(_dialogPolice);
            }
            MakePeaceWithPoliceAndVictims();

            MBTextManager.SetTextVariable(GwpTextKeys.EnforcementAtonementText,
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_014}A charge of atonement has been issued: pursue {VAR_1} (strength when assigned: {VAR_2}).", "VAR_1", _atonementTargetName, "VAR_2", targetSizeSnapshot) +
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_015}Fulfilment may restore up to {VAR_1} standing, but never above 0;", "VAR_1", _atonementReputationReward) +
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_016}failure will impose a further loss of 5 standing."));

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_017}The charge of atonement is entered in your quest roll: defeat {VAR_1}, then report to the Warden-General or any Grey Warden within {VAR_2} days. Failure: -5 standing.", "VAR_1", _atonementTargetName, "VAR_2", GwpText.Format(GwpTuning.Enforcement.AtonementDeadlineDays, "0")),
                Colors.Yellow));

            return true;
        }

        private bool EnforcementBarterSuccessfulCondition()
        {
            return _enforcementBarterInProgress
                && Campaign.Current?.BarterManager != null
                && Campaign.Current.BarterManager.LastBarterIsAccepted;
        }

        private void OnEnforcementPayBarterConsequence()
        {
            _enforcementBarterInProgress =
                StartEnforcementPaymentBarter(_dialogPolice, _dialogFine, GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_018}Pay the lawful fine"));
        }

        private void OnEnforcementPayRejectedConsequence()
        {
            _enforcementBarterInProgress = false;
        }

        private void OnEnforcementPayAcceptedConsequence()
        {
            try
            {
                GwpAiDiagnostics.WritePlayerJusticeState(
                    "ENFORCEMENT_FINE_ACCEPTED_BEFORE_CLEAR",
                    "fine=" + _dialogFine +
                    "; police=" + (_dialogPolice?.StringId ?? "-"));
                int paid = PoliceResourceManager.CollectFine(_dialogFine);

                PlayerState.ResetReputation(0);
                CrimeState.EndPlayerHunt();

                if (_dialogPolice != null && _dialogPolice.IsActive)
                {
                    GreyWardenPartyDesireBehavior.ClearIntent(_dialogPolice);
                    GreyWardenPartyDesireBehavior.RequestImmediateRethink(_dialogPolice);
                }

                MakePeaceWithPoliceAndVictims();

                GwpAiDiagnostics.WritePlayerJusticeState(
                    "ENFORCEMENT_FINE_ACCEPTED_AFTER_CLEAR",
                    "fine=" + _dialogFine +
                    "; paid=" + paid +
                    "; police=" + (_dialogPolice?.StringId ?? "-"));

                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_019}The lawful fine of {VAR_1} denars has been received. The warrant is lifted.", "VAR_1", paid),
                    Colors.Yellow));

                // The line's normal target is close_window.  Finish the native
                // PlayerEncounter only after ConversationManager has actually
                // closed the conversation; doing it from this consequence leaves
                // EngageParty/TargetParty alive and can reopen the same warrant.
                QueueFinishEnforcementEncounter();
            }
            catch
            {
                // Even if a native economy/faction callback fails, the accepted
                // outcome must still close the encounter instead of leaving the
                // old EngageParty target available for another conversation.
                QueueFinishEnforcementEncounter();
            }
        }

        private void QueueFinishEnforcementEncounter()
        {
            if (_enforcementEncounterFinishQueued)
                return;

            _enforcementEncounterFinishQueued = true;
            if (PlayerEncounter.IsActive)
                PlayerEncounter.LeaveEncounter = true;

            var conversationManager = Campaign.Current?.ConversationManager;
            if (conversationManager == null)
            {
                FinishEnforcementEncounter();
                return;
            }

            conversationManager.ConversationEndOneShot -=
                FinishEnforcementEncounter;
            conversationManager.ConversationEndOneShot +=
                FinishEnforcementEncounter;

            GwpAiDiagnostics.WritePlayerJusticeState(
                "ENFORCEMENT_CONTACT_FINISH_QUEUED",
                "police=" + (_dialogPolice?.StringId ?? "-") +
                "; encounterActive=" + PlayerEncounter.IsActive);
        }

        private void FinishEnforcementEncounter()
        {
            MobileParty? police = _dialogPolice;
            try
            {
                GwpCommon.TryFinishPlayerEncounter();

                // EndPlayerHunt removes the case, but native EngageParty can
                // survive the conversation for one AI pass.  Reset the native
                // movement target immediately so the finished case cannot make
                // another contact while both parties are still overlapping.
                if (police?.IsActive == true)
                {
                    GreyWardenPartyDesireBehavior.ClearIntent(police);
                    police.Ai.SetDoNotAttackMainParty(2);
                    police.Ai.SetDoNotMakeNewDecisions(false);
                    police.SetMoveModeHold();
                    police.Ai.RethinkAtNextHourlyTick = true;
                }

                GwpAiDiagnostics.WritePlayerJusticeState(
                    "ENFORCEMENT_CONTACT_FINISHED",
                    "police=" + (police?.StringId ?? "-") +
                    "; encounterActive=" + PlayerEncounter.IsActive);
            }
            catch (Exception exception)
            {
                GwpAiDiagnostics.WritePlayerJusticeState(
                    "ENFORCEMENT_CONTACT_FINISH_FAILED",
                    "police=" + (police?.StringId ?? "-") +
                    "; error=" + exception.GetType().Name);
            }
            finally
            {
                _enforcementEncounterFinishQueued = false;
                ResetDialogueState();
            }
        }

        private void OnEnforcementFightConsequence()
        {
            try
            {
                if (_dialogTask != null && MobileParty.MainParty != null)
                    DeclareWar(_dialogTask, MobileParty.MainParty);

                if (_dialogPolice != null && _dialogPolice.IsActive)
                    GreyWardenPartyDesireBehavior.RequestImmediateRethink(_dialogPolice);

                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_policeenforcementbehavior_dialogue_020}You have refused the order. The Grey Wardens will take you by force."),
                    Colors.Red));
            }
            catch { }
            finally
            {
                ResetDialogueState();
            }
        }

        private bool StartEnforcementPaymentBarter(MobileParty policePartyMobile, int amount, string barterDisplayName)
        {
            if (policePartyMobile == null || !policePartyMobile.IsActive || MobileParty.MainParty == null)
                return false;

            Hero? barterHero = Hero.OneToOneConversationHero
                               ?? policePartyMobile.LeaderHero
                               ?? GetEnforcementBarterHero();
            if (barterHero == null)
                return false;

            PartyBase policeParty = policePartyMobile.Party;
            PartyBase playerParty = MobileParty.MainParty.Party;
            if (policeParty == null || playerParty == null)
                return false;

            int paymentAmount = Math.Max(1, amount);
            var fineBarter = new GwpBribeBarterable(
                barterHero,
                Hero.MainHero,
                policeParty,
                playerParty,
                paymentAmount,
                barterDisplayName);

            try
            {
                Campaign.Current.BarterManager.StartBarterOffer(
                    Hero.MainHero,
                    barterHero,
                    playerParty,
                    policeParty,
                    null,
                    InitializeEnforcementBarterContext,
                    0,
                    false,
                    new[] { fineBarter });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool InitializeEnforcementBarterContext(Barterable barterable, BarterData args, object obj)
        {
            return barterable is GwpBribeBarterable;
        }

        private Hero? GetEnforcementBarterHero()
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return null;

            Hero leader = policeClan.Leader;
            if (leader != null && leader.IsActive && !leader.IsDead && !leader.IsChild)
                return leader;

            return policeClan.Heroes.FirstOrDefault(h =>
                h != null &&
                h.IsActive &&
                !h.IsDead &&
                !h.IsChild &&
                !h.IsPrisoner);
        }

        private void ResetDialogueState()
        {
            _enforcementBarterInProgress = false;
            _enforcementAtonementAssigned = false;
            _dialogFine = 0;
            _dialogPolice = null!;
            _dialogTask = null!;
        }

        #endregion

        #region 遭遇拦截（强制对话，在宣战前让玩家选择）

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null) return;
            GwpAiDiagnostics.WriteMapEvent(mapEvent, "STARTED");

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            bool policeHasPlayerTask = false;
            bool playerInvolved = false;

            foreach (var p in mapEvent.InvolvedParties)
            {
                if (p?.MobileParty == null) continue;

                if (p.MobileParty.IsMainParty)
                {
                    playerInvolved = true;
                    continue;
                }

                var task = CrimeState.GetTask(p.MobileParty.StringId);
                if (task != null &&
                    task.TargetCrime?.Offender?.IsMainParty == true &&
                    !task.WarDeclared &&
                    !task.IsEscortingPlayer &&
                    p.MobileParty.ActualClan == policeClan)
                {
                    policeHasPlayerTask = true;
                }
            }

            if (!policeHasPlayerTask || !playerInvolved) return;

            IFaction? pf = Clan.PlayerClan?.MapFaction;
            bool atWar = pf != null && FactionManager.IsAtWarAgainstFaction(policeClan, pf);

            if (!atWar && PlayerEncounter.IsActive && PlayerEncounter.EncounteredParty != null)
            {
                try { PlayerEncounter.DoMeeting(); } catch { }
            }
        }

        #endregion
    }
}
