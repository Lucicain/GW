using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;

namespace GreyWardenPolicePurity
{
    public partial class PlayerBountyBehavior
    {
        #region 对话注册

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // ★ 每次新会话（新档/读档）必须重置此 static 标志。
            // 原因：_notificationTypeRegistered 是 static 字段，在进程生命周期内持续存在。
            // 会话1注册后置 true → 会话2 TryRegisterNotificationType() 直接 return →
            // 新 MapNotificationView 未注册类型 → 通知图标消失 → 玩家永远看不到悬赏任务。
            _notificationTypeRegistered = false;

            // ── 招募对话（招募使者接触玩家时触发）────────────────────────────────────
            starter.AddDialogLine(
                "gwp_recruit_start",
                "start",
                "gwp_recruit_options",
                "{" + GwpTextKeys.RecruitGreeting + "}",
                RecruitDialogCondition,
                null,
                100);

            starter.AddPlayerLine(
                "gwp_recruit_accept",
                "gwp_recruit_options",
                "gwp_recruit_accept_response",
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_001}I accept. I shall serve the Grey Wardens in this matter."),
                null, OnRecruitAcceptConsequence, 100);

            starter.AddDialogLine(
                "gwp_recruit_accept_response",
                "gwp_recruit_accept_response",
                "close_window",
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_002}Good. This raiment will mark you as our sworn agent. Once the wanted party is defeated, seek a Warden-lord and claim the bounty.")
                + GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_003}Remember this: should the pursuit kindle war, the Grey Wardens will mediate once the contract is settled."),
                null,
                null,
                100);

            starter.AddPlayerLine(
                "gwp_recruit_refuse",
                "gwp_recruit_options",
                "gwp_recruit_refuse_response",
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_004}No. I have no interest."),
                null, OnRecruitRefuseConsequence, 100);

            starter.AddDialogLine(
                "gwp_recruit_refuse_response",
                "gwp_recruit_refuse_response",
                "close_window",
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_005}As you will. This herald will not trouble you again; if you later reconsider, speak to a Warden-lord."),
                null,
                null,
                100);

            // ── 通过灰袍领主重新加入 / 主动退出 ──────────────────────────────────
            starter.AddPlayerLine(
                "gwp_membership_rejoin",
                "lord_talk_speak_diplomacy_2",
                "gwp_membership_rejoin_response",
                GwpText.Get("{=gwp_membership_rejoin_player}I wish to petition for a place among the Grey Warden sworn hunters again."),
                CanRejoinThroughLord,
                null,
                102);

            starter.AddDialogLine(
                "gwp_membership_rejoin_response",
                "gwp_membership_rejoin_response",
                "lord_pretalk",
                GwpText.Get("{=gwp_membership_rejoin_response}Your petition is accepted. You are again recognised as a sworn hunter; take a fresh commander's harness and answer our warrants faithfully."),
                null,
                OnRejoinThroughLord,
                100);

            starter.AddPlayerLine(
                "gwp_membership_leave",
                "lord_talk_speak_diplomacy_2",
                "gwp_membership_leave_prompt",
                GwpText.Get("{=gwp_membership_leave_player}I wish to relinquish my place with the Grey Wardens."),
                CanLeaveThroughLord,
                null,
                102);

            starter.AddDialogLine(
                "gwp_membership_leave_prompt",
                "gwp_membership_leave_prompt",
                "gwp_membership_leave_options",
                "{" + GwpTextKeys.MembershipLeavePrompt + "}",
                PrepareMembershipLeavePrompt,
                null,
                100);

            starter.AddPlayerLine(
                "gwp_membership_leave_confirm",
                "gwp_membership_leave_options",
                "gwp_membership_leave_done",
                GwpText.Get("{=gwp_membership_leave_confirm}Yes. Remove my name from the roll."),
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_membership_leave_done",
                "gwp_membership_leave_done",
                "lord_pretalk",
                GwpText.Get("{=gwp_membership_leave_done}It is done. The equipment already entrusted to you remains yours, but it no longer grants any Grey Warden authority."),
                null,
                OnLeaveThroughLord,
                100);

            starter.AddPlayerLine(
                "gwp_membership_leave_cancel",
                "gwp_membership_leave_options",
                "gwp_membership_leave_cancelled",
                GwpText.Get("{=gwp_membership_leave_cancel}No. I will remain."),
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_membership_leave_cancelled",
                "gwp_membership_leave_cancelled",
                "lord_pretalk",
                GwpText.Get("{=gwp_membership_leave_cancelled}Then your place on the roll remains unchanged."),
                null,
                null,
                100);

            // ── 招募已完成时的兜底对话 ──────────────────────────────────────────────
            // LeaveEncounter = true 正常情况下已足够；这条对话防止极端情况下
            // 遭遇系统在 close_window 后再次触发对话，导致"我不能和你说话"→战斗准备。
            starter.AddDialogLine(
                "gwp_recruit_already_done",
                "start",
                "close_window",
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_006}Our business is concluded. Go on your way."),
                () =>
                {
                    MobileParty? conv = MobileParty.ConversationParty;
                    if (conv == null || !IsRecruitmentPatrol(conv)) return false;
                    if (!_recruitmentOffered) return false;
                    return true;
                },
                CloseRecruitmentEncounterAndReturn,
                100);

            // ── 五日后主动找上玩家的无领主结算队 ──────────────────────────────────
            starter.AddDialogLine(
                "gwp_bounty_courier_returning_start",
                "start",
                "close_window",
                GwpText.Get("{=gwp_bounty_courier_returning}The warrant has been settled. We are returning to quarters and have no further business with you."),
                BountyCollectionCourierReturningDialogCondition,
                BountyCollectionCourierReturningConsequence,
                130);

            starter.AddDialogLine(
                "gwp_bounty_courier_start",
                "start",
                "gwp_bounty_courier_player",
                "{GWP_BOUNTY_COURIER_GREETING}",
                BountyCollectionCourierDialogCondition,
                null,
                120);

            starter.AddPlayerLine(
                "gwp_bounty_courier_turnin",
                "gwp_bounty_courier_player",
                "gwp_bounty_courier_response",
                GwpText.Get("{=gwp_bounty_courier_player}Then close the warrant. I accept the promised bounty."),
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_bounty_courier_response",
                "gwp_bounty_courier_response",
                "close_window",
                GwpText.Get("{=gwp_bounty_courier_response}The warrant is closed. Here is the payment recorded in the contract. We will return to the nearest settlement to report."),
                null,
                BountyRewardConsequence,
                100);

            // ── 赏金领取（目标落败后可向任意普通灰袍领主统一结算）──────────────────
            starter.AddPlayerLine(
                "gwp_bounty_collect_option",
                "lord_talk_speak_diplomacy_2",
                "gwp_bounty_reward_response",
                GwpText.Get("{=gwp_bounty_turnin_player}The quarry has been defeated. I have come to close the warrant and receive the promised bounty."),
                BountyRewardCondition,
                null,
                101);

            starter.AddDialogLine(
                "gwp_bounty_reward_response",
                "gwp_bounty_reward_response",
                "lord_pretalk",
                "{" + GwpTextKeys.BountyRewardResponse + "}",
                null,
                BountyRewardConsequence,
                100);

            // ── 读档后悬赏任务恢复（兜底）─────────────────────────────────────────
            // 此时 SyncData 已完成，所有持久化字段均已正确加载，可以安全访问。
            ReconcileRecruitmentPatrolState();
            TryRestoreBountyQuestOnSessionStart();
            _bountyCollectionCourierToResumeId = null!;
            UpdateBountyCollectionCouriers();
        }

        #endregion

        #region 招募对话逻辑

        private bool RecruitDialogCondition()
        {
            MobileParty? conversationParty = MobileParty.ConversationParty;
            if (conversationParty == null) return false;
            if (!IsRecruitmentPatrol(conversationParty)) return false;
            if (_recruitmentOffered || _recruitmentAccepted) return false;

            MBTextManager.SetTextVariable(GwpTextKeys.RecruitGreeting,
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_009}Traveller, stay a moment. The Grey Wardens have marked your recent good service, and would set a charge before you.")
                + GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_010}As our sworn hunter, you may pursue wanted malefactors and receive a worthy bounty.")
                + GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_011}Be warned: a foreign realm may deem such pursuit an incursion and answer it with war.")
                + GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_012}Yet once the quarry is defeated and the bounty claimed, the Grey Wardens will mediate,")
                + GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_013}so that the war does not remain yours to bear. Will you take the oath?"));
            WriteRecruitmentTrace(conversationParty, "RECRUIT_DIALOG_OPENED",
                "recruitment start line accepted");
            return true;
        }

        private void OnRecruitAcceptConsequence()
        {
            MobileParty? herald = ResolveCurrentRecruitmentPatrol();
            WriteRecruitmentTrace(herald, "RECRUIT_ACCEPT_SELECTED",
                "player selected acceptance");
            _recruitmentAccepted = true;
            _recruitmentOffered = true;
            WriteRecruitmentTrace(herald, "RECRUIT_ACCEPT_COMMITTED",
                "membership state committed on player choice");
            GiveCommanderEquipment();
            CloseRecruitmentEncounterAndReturn();

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_014}You are now a sworn hunter of the Grey Wardens. The black commander’s harness has been placed in your baggage; wear it to receive bounty contracts."),
                Colors.Green));
        }

        private void OnRecruitRefuseConsequence()
        {
            MobileParty? herald = ResolveCurrentRecruitmentPatrol();
            WriteRecruitmentTrace(herald, "RECRUIT_REFUSE_SELECTED",
                "player selected refusal");
            _recruitmentOffered = true;
            WriteRecruitmentTrace(herald, "RECRUIT_REFUSE_COMMITTED",
                "refusal state committed on player choice");
            CloseRecruitmentEncounterAndReturn();

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_015}You refused the Grey Wardens’ summons. The herald will not return, but you may reconsider by speaking to a Warden-lord."),
                Colors.Yellow));
        }

        /// <summary>
        /// Recruitment can begin either because the player clicked the herald or
        /// because the herald engaged the player and OnMapEventStarted forced a
        /// meeting. In the forced path, issuing the return order before the
        /// conversation has fully closed lets the encounter cleanup overwrite it
        /// with another EngageParty order. Mark the encounter for departure now,
        /// then finish it and reissue the return order after conversation cleanup.
        /// </summary>
        private void CloseRecruitmentEncounterAndReturn()
        {
            MobileParty? herald = ResolveCurrentRecruitmentPatrol();
            WriteRecruitmentTrace(herald, "RECRUIT_CLOSE_QUEUED",
                "mark encounter for departure and queue conversation-end cleanup");
            _recruitmentPatrolReturning = true;

            if (PlayerEncounter.IsActive)
                PlayerEncounter.LeaveEncounter = true;

            TriggerPatrolReturn();
            WriteRecruitmentTrace(herald, "RECRUIT_RETURN_REQUESTED",
                "initial return order issued");

            if (Campaign.Current?.ConversationManager == null)
                return;

            Campaign.Current.ConversationManager.ConversationEndOneShot -=
                FinishRecruitmentEncounterAndReturn;
            Campaign.Current.ConversationManager.ConversationEndOneShot +=
                FinishRecruitmentEncounterAndReturn;
        }

        private void FinishRecruitmentEncounterAndReturn()
        {
            MobileParty? herald = ResolveCurrentRecruitmentPatrol();
            WriteRecruitmentTrace(herald, "RECRUIT_CONVERSATION_ENDED",
                "conversation-end callback entered");
            try
            {
                MobileParty? encountered = PlayerEncounter.IsActive
                    ? PlayerEncounter.EncounteredMobileParty
                    : null;
                if (encountered != null && IsRecruitmentPatrol(encountered))
                {
                    PlayerEncounter.LeaveEncounter = true;
                    PlayerEncounter.Finish();
                }
            }
            catch (Exception ex)
            {
                // The native encounter may already have finished itself. The
                // returning flag and the command below remain valid either way.
                WriteRecruitmentTrace(herald, "RECRUIT_ENCOUNTER_FINISH_FAILED",
                    ex.GetType().Name + ":" + ex.Message);
            }

            TriggerPatrolReturn();
            WriteRecruitmentTrace(herald, "RECRUIT_RETURN_REISSUED",
                "native return order reissued after conversation cleanup");
        }

        private MobileParty? ResolveCurrentRecruitmentPatrol()
        {
            MobileParty? party = MobileParty.ConversationParty;
            if (party != null && IsRecruitmentPatrol(party))
                return party;

            party = PlayerEncounter.IsActive
                ? PlayerEncounter.EncounteredMobileParty
                : null;
            if (party != null && IsRecruitmentPatrol(party))
                return party;

            return GetActiveRecruitmentPatrols().FirstOrDefault();
        }

        private void WriteRecruitmentTrace(MobileParty? herald, string action,
            string details)
        {
            if (herald == null) return;

            try
            {
                string encounteredId = PlayerEncounter.IsActive
                    ? PlayerEncounter.EncounteredMobileParty?.StringId ?? "-"
                    : "-";
                string conversationId = MobileParty.ConversationParty?.StringId ?? "-";
                GwpAiDiagnostics.WriteAction(herald, action,
                    details +
                    "; offered=" + _recruitmentOffered +
                    "; accepted=" + _recruitmentAccepted +
                    "; returning=" + _recruitmentPatrolReturning +
                    "; tracked=" + (_recruitmentPatrolId ?? "-") +
                    "; encounterActive=" + PlayerEncounter.IsActive +
                    "; encountered=" + encounteredId +
                    "; conversation=" + conversationId);
            }
            catch
            {
                // Diagnostics must never interfere with the recruitment flow.
            }
        }

        private bool CanRejoinThroughLord()
        {
            return IsOrdinaryGreyWardenLordConversation() &&
                   _recruitmentOffered &&
                   !_recruitmentAccepted &&
                   _voluntaryExitCount < GwpTuning.Bounty.MaximumVoluntaryExits &&
                   PlayerState.Reputation >= GetRequiredReadmissionReputation();
        }

        private bool CanLeaveThroughLord()
        {
            return IsOrdinaryGreyWardenLordConversation() && _recruitmentAccepted;
        }

        private int GetRequiredReadmissionReputation()
        {
            return GwpTuning.Bounty.RecruitmentReputationThreshold +
                   _voluntaryExitCount * GwpTuning.Bounty.ReadmissionReputationStep;
        }

        private bool PrepareMembershipLeavePrompt()
        {
            string prompt;
            int nextExitCount = _voluntaryExitCount + 1;
            if (nextExitCount >= GwpTuning.Bounty.MaximumVoluntaryExits)
            {
                prompt = GwpText.Get(
                    "{=gwp_membership_leave_prompt_final}If you withdraw this time, your name will be struck permanently and no Warden-lord will readmit you. Is this your final decision?");
            }
            else
            {
                int nextThreshold = GwpTuning.Bounty.RecruitmentReputationThreshold +
                                    nextExitCount * GwpTuning.Bounty.ReadmissionReputationStep;
                prompt = GwpText.Get(
                    "{=gwp_membership_leave_prompt_higher}If you withdraw, your warrants, detachments, and battlefield relief end. Readmission will require {VAR_1} standing and a personal appeal to a Warden-lord. Is this your final decision?",
                    "VAR_1", nextThreshold);
            }

            MBTextManager.SetTextVariable(GwpTextKeys.MembershipLeavePrompt, prompt);
            return true;
        }

        private static bool IsOrdinaryGreyWardenLordConversation()
        {
            Hero? conversationHero = Hero.OneToOneConversationHero;
            if (!GwpCommon.IsGreyWardenLord(conversationHero)) return false;

            MobileParty? conversationParty = MobileParty.ConversationParty;
            if (conversationParty == null) return true;
            if (GwpCommon.IsPatrolParty(conversationParty) ||
                GwpCommon.IsEnforcementDelayPatrolParty(conversationParty))
                return false;

            PoliceTask? task = CrimeState.GetTask(conversationParty.StringId);
            return task?.TargetCrime?.Offender?.IsMainParty != true;
        }

        private void OnRejoinThroughLord()
        {
            _recruitmentAccepted = true;
            _recruitmentOffered = true;
            DestroyRecruitmentPatrol();
            GiveCommanderEquipment();
        }

        private void OnLeaveThroughLord()
        {
            _recruitmentAccepted = false;
            _recruitmentOffered = true;
            _voluntaryExitCount = Math.Min(
                GwpTuning.Bounty.MaximumVoluntaryExits,
                _voluntaryExitCount + 1);
            DestroyRecruitmentPatrol();

            if (!HasBountyTask) return;

            try { _activeQuest?.FailQuestMembershipEnded(); } catch { }
            EndBountyTaskState(tryRestorePeace: true);
        }

        /// <summary>
        /// 将黑袍指挥官全套装备、黑曜指挥官盾和双刀加入玩家行李。
        /// 同时输出调试信息，方便确认每件装备是否成功找到。
        /// </summary>
        private static void GiveCommanderEquipment()
        {
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null) return;

            var ids = new List<string>(GwpIds.MembershipGrantItemIds);
            foreach (string itemId in ids)
            {
                ItemObject? item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                if (item == null)
                {
                    foreach (ItemObject candidate in Game.Current.ObjectManager.GetObjectTypeList<ItemObject>())
                    {
                        if (candidate.StringId.Equals(itemId, StringComparison.OrdinalIgnoreCase))
                        {
                            item = candidate;
                            break;
                        }
                    }
                }

                if (item != null)
                {
                    roster.AddToCounts(new EquipmentElement(item), 1);
                }
            }
        }

        #endregion

        #region 强制对话拦截（招募使者遭遇玩家时）

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null) return;

            bool recruitInvolved = false;
            bool playerInvolved = false;
            bool bountyTargetInvolved = false;
            bool bountyCollectionCourierInvolved = false;
            MobileParty? bountyCollectionCourier = null;
            MobileParty? herald = null;

            foreach (PartyBase p in mapEvent.InvolvedParties)
            {
                if (p.MobileParty != null && IsRecruitmentPatrol(p.MobileParty))
                {
                    recruitInvolved = true;
                    herald = p.MobileParty;
                }
                if (p.MobileParty != null && p.MobileParty.IsMainParty) playerInvolved = true;
                if (p.MobileParty != null && IsTrackingBountyTarget &&
                    string.Equals(p.MobileParty.StringId, _activeBountyTargetId,
                        StringComparison.OrdinalIgnoreCase))
                    bountyTargetInvolved = true;
                if (IsBountyCollectionCourier(p.MobileParty))
                {
                    bountyCollectionCourierInvolved = true;
                    bountyCollectionCourier = p.MobileParty;
                }
            }

            if (playerInvolved && bountyTargetInvolved)
            {
                _bountyTargetEncounterStarted = true;
                if (!string.IsNullOrEmpty(_escortPolicePartyId))
                {
                    PoliceEnforcementBehavior.RefreshPlayerBountyCaseContact(
                        _escortPolicePartyId, true);
                }
            }

            if (playerInvolved && bountyCollectionCourierInvolved &&
                (IsWaitingForBountyCollection ||
                 IsReturningBountyCollectionCourier(bountyCollectionCourier)))
            {
                if (PlayerEncounter.IsActive &&
                    PlayerEncounter.EncounteredParty != null)
                {
                    try { PlayerEncounter.DoMeeting(); } catch { }
                }
                return;
            }

            if (!recruitInvolved || !playerInvolved)
                return;

            WriteRecruitmentTrace(herald, "RECRUIT_MAP_EVENT_STARTED",
                "herald and player are involved in the same map event");

            // Once the player has answered, this hook must never reopen the
            // recruitment meeting while the herald is trying to leave.
            if (_recruitmentOffered || _recruitmentAccepted)
            {
                WriteRecruitmentTrace(herald, "RECRUIT_FORCE_MEETING_BLOCKED",
                    "decision state already committed");
                return;
            }

            if (PlayerEncounter.IsActive && PlayerEncounter.EncounteredParty != null)
            {
                WriteRecruitmentTrace(herald, "RECRUIT_FORCE_MEETING_REQUESTED",
                    "calling PlayerEncounter.DoMeeting");
                try
                {
                    PlayerEncounter.DoMeeting();
                }
                catch (Exception ex)
                {
                    WriteRecruitmentTrace(herald, "RECRUIT_FORCE_MEETING_FAILED",
                        ex.GetType().Name + ":" + ex.Message);
                }
            }
        }

        #endregion

        #region 赏金领取对话

        private bool BountyCollectionCourierDialogCondition()
        {
            MobileParty? conversationParty = MobileParty.ConversationParty;
            if (!IsWaitingForBountyCollection ||
                !IsBountyCollectionCourier(conversationParty))
                return false;

            MBTextManager.SetTextVariable(
                "GWP_BOUNTY_COURIER_GREETING",
                GwpText.Get(
                    "{=gwp_bounty_courier_greeting}The Grey Wardens sent us to find you. The quarry's defeat has been confirmed. We can close the warrant here and pay the promised {VAR_1} denars.",
                    "VAR_1", _activeBountyReward));
            return true;
        }

        private bool BountyCollectionCourierReturningDialogCondition()
        {
            MobileParty? conversationParty = MobileParty.ConversationParty;
            return IsBountyCollectionCourier(conversationParty) &&
                   IsReturningBountyCollectionCourier(conversationParty);
        }

        private void BountyCollectionCourierReturningConsequence()
        {
            MobileParty? conversationParty = MobileParty.ConversationParty;
            if (conversationParty != null &&
                IsReturningBountyCollectionCourier(conversationParty))
            {
                ResumeBountyCollectionCourierEncounterAndReturn(
                    conversationParty);
            }
        }

        /// <summary>目标落败后可向任意正常灰袍领主统一结算。</summary>
        private bool BountyRewardCondition()
        {
            if (!IsWaitingForBountyCollection) return false;
            if (!IsOrdinaryGreyWardenLordConversation()) return false;

            MBTextManager.SetTextVariable(GwpTextKeys.BountyRewardResponse,
                GwpText.Get("{=gwp_bounty_turnin_lord}The report is confirmed and the warrant is closed. Take the promised bounty of {VAR_1} denars.", "VAR_1", _activeBountyReward));
            return true;
        }

        private void ShowBountyCompletionNotice()
        {
            string title = GwpText.Get("{=gwp_bounty_complete_notice_title}Bounty target defeated");
            string body = GwpText.Get(
                "{=gwp_bounty_complete_notice_body}The pursuit is over and the assigned escort has returned to its duties. Report to any Grey Warden lord to receive {VAR_1} denars. If the warrant remains unsettled for five days, a Warden settlement party will come to you.",
                "VAR_1", _activeBountyReward);

            InformationManager.ShowInquiry(
                new InquiryData(
                    title,
                    body,
                    true,
                    false,
                    GwpText.Get("{=gwp_common_understood}Understood"),
                    string.Empty,
                    null,
                    null,
                    "event:/ui/notification/quest_finished"),
                true);
        }

        private void BountyRewardConsequence()
        {
            MobileParty? collectionCourier = IsBountyCollectionCourier(
                MobileParty.ConversationParty)
                ? MobileParty.ConversationParty
                : null;
            try
            {
                int reward = _activeBountyReward;
                Hero.MainHero.ChangeHeroGold(reward);
                try { _activeQuest?.SucceedQuest(); } catch { }
                string paymentMessage = collectionCourier == null
                    ? GwpText.Get(
                        "{=gwp_playerbountybehavior_dialogueandnotification_018}Bounty received from a Warden-lord: {VAR_1} denars",
                        "VAR_1", reward)
                    : GwpText.Get(
                        "{=gwp_bounty_courier_payment_received}Bounty received from the Grey Warden settlement party: {VAR_1} denars",
                        "VAR_1", reward);
                InformationManager.DisplayMessage(new InformationMessage(
                    paymentMessage,
                    Colors.Green));
                MakePeaceWithCriminalFaction();
            }
            catch { }
            finally
            {
                if (collectionCourier != null)
                    CloseBountyCollectionCourierEncounterAndReturn(
                        collectionCourier);
                ClearBountyTaskState(collectionCourier);
            }
        }

        private void MakePeaceWithCriminalFaction()
        {
            if (string.IsNullOrEmpty(_activeBountyTargetFactionId)) return;
            if (string.IsNullOrEmpty(_activeBountyPlayerFactionId)) return;
            if (_playerFactionWasAtWarWhenBountyAccepted) return;
            if (!_bountyTargetEncounterStarted) return;

            try
            {
                IFaction? playerFaction = Hero.MainHero?.MapFaction;
                if (playerFaction == null) return;
                if (!string.Equals(playerFaction.StringId, _activeBountyPlayerFactionId,
                        StringComparison.OrdinalIgnoreCase))
                    return;

                IFaction? criminalFaction = null;
                foreach (Kingdom kingdom in Kingdom.All)
                {
                    if (kingdom.StringId == _activeBountyTargetFactionId)
                    {
                        criminalFaction = kingdom;
                        break;
                    }
                }

                if (criminalFaction == null)
                {
                    foreach (Clan clan in Clan.All)
                    {
                        if (clan.StringId == _activeBountyTargetFactionId)
                        {
                            criminalFaction = clan;
                            break;
                        }
                    }
                }

                if (criminalFaction == null || criminalFaction == playerFaction) return;
                if (FactionManager.IsAtWarAgainstFaction(playerFaction, criminalFaction))
                {
                    MakePeaceAction.Apply(playerFaction, criminalFaction);
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_019}Grey Warden mediation: peace concluded with {VAR_1}", "VAR_1", criminalFaction.Name), Colors.Green));
                }
            }
            catch { }
        }

        #endregion

        #region 悬赏派发（原版通知与三选一询问）

        private enum BountyDifficulty
        {
            Easy,
            Standard,
            Hard
        }

        private enum BountyOfferRole
        {
            Nearest,
            Harder,
            Easier
        }

        private sealed class BountyOfferChoice
        {
            internal CrimeRecord Crime { get; }
            internal BountyOfferRole Role { get; }
            internal BountyDifficulty Difficulty { get; }
            internal int Reward { get; }

            internal BountyOfferChoice(
                CrimeRecord crime,
                BountyOfferRole role,
                BountyDifficulty difficulty,
                int reward)
            {
                Crime = crime;
                Role = role;
                Difficulty = difficulty;
                Reward = reward;
            }
        }

        private void OfferBountySelection()
        {
            TryRegisterNotificationType();
            try
            {
                Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(
                    new BountyMapNotification());
            }
            catch { }
        }

        private static void TryRegisterNotificationType()
        {
            if (_notificationTypeRegistered) return;

            _notificationTypeRegistered = true;
            try
            {
                MapScreen? mapScreen = ScreenManager.TopScreen as MapScreen;
                mapScreen?.MapNotificationView?.RegisterMapNotificationType(
                    typeof(BountyMapNotification),
                    typeof(BountyMapNotificationItemVM));
            }
            catch { }
        }

        internal bool CanInspectBountyOffers() =>
            _recruitmentAccepted &&
            PlayerState.Reputation >= GwpTuning.Bounty.RecruitmentReputationThreshold &&
            IsWearingCommanderSet() &&
            !HasBountyTask &&
            CrimeState.GetAvailablePlayerBounties().Count > 0;

        internal void ShowBountySelectionInquiry()
        {
            if (!CanInspectBountyOffers()) return;

            List<BountyOfferChoice> choices = BuildBountyOfferChoices();
            if (choices.Count == 0) return;

            var roles = new[]
            {
                BountyOfferRole.Nearest,
                BountyOfferRole.Harder,
                BountyOfferRole.Easier
            };
            var elements = new List<InquiryElement>(3);

            foreach (BountyOfferRole role in roles)
            {
                BountyOfferChoice? choice = choices.FirstOrDefault(item => item.Role == role);
                string roleText = GetRoleText(role);
                if (choice == null)
                {
                    elements.Add(new InquiryElement(
                        null,
                        GwpText.Get("{=gwp_bounty_choice_unavailable}{VAR_1}: no separate valid contract", "VAR_1", roleText),
                        null,
                        false,
                        GwpText.Get("{=gwp_bounty_choice_unavailable_hint}More open cases are needed for a separate choice.")));
                    continue;
                }

                MobileParty target = choice.Crime.Offender!;
                string label = GwpText.Get(
                    "{=gwp_bounty_choice_label}{VAR_1}: {VAR_2} — {VAR_3}, {VAR_4} denars",
                    "VAR_1", roleText,
                    "VAR_2", target.Name,
                    "VAR_3", GetDifficultyText(choice.Difficulty),
                    "VAR_4", choice.Reward);
                string hint = GwpText.Get(
                    "{=gwp_bounty_choice_hint}{VAR_1}; last sighted near {VAR_2}.",
                    "VAR_1", GwpText.CrimeType(choice.Crime.CrimeType),
                    "VAR_2", GetNearestSettlementName(target.GetPosition2D));
                elements.Add(new InquiryElement(choice, label, null, true, hint));
            }

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    GwpText.Get("{=gwp_bounty_select_title}Select a Grey Warden bounty"),
                    GwpText.Get("{=gwp_bounty_select_description}Choose the nearest quarry, a harder quarry, or an easier quarry. Payment is fixed by difficulty."),
                    elements,
                    true,
                    1,
                    1,
                    GwpText.Get("{=gwp_bounty_review_contract}Review contract"),
                    GwpText.Get("{=gwp_cancel}Cancel"),
                    selected =>
                    {
                        BountyOfferChoice? choice = selected.FirstOrDefault()?.Identifier
                            as BountyOfferChoice;
                        if (choice != null)
                            ShowBountyInquiry(choice);
                    },
                    _ => { }),
                true);
        }

        private List<BountyOfferChoice> BuildBountyOfferChoices()
        {
            List<CrimeRecord> available = CrimeState.GetAvailablePlayerBounties();
            if (available.Count == 0) return new List<BountyOfferChoice>();

            Vec2 playerPosition = MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<BountyOfferChoice>(3);

            AddBountyChoice(
                result,
                used,
                available.OrderBy(crime =>
                    playerPosition.Distance(crime.Offender!.GetPosition2D)).FirstOrDefault(),
                BountyOfferRole.Nearest);
            AddBountyChoice(
                result,
                used,
                available.Where(crime => !used.Contains(crime.CrimeId))
                    .OrderByDescending(GetBountyStrength).FirstOrDefault(),
                BountyOfferRole.Harder);
            AddBountyChoice(
                result,
                used,
                available.Where(crime => !used.Contains(crime.CrimeId))
                    .OrderBy(GetBountyStrength).FirstOrDefault(),
                BountyOfferRole.Easier);

            return result;
        }

        private static void AddBountyChoice(
            ICollection<BountyOfferChoice> result,
            ISet<string> used,
            CrimeRecord? crime,
            BountyOfferRole role)
        {
            if (crime == null || !used.Add(crime.CrimeId)) return;

            BountyDifficulty difficulty = GetBountyDifficulty(crime);
            result.Add(new BountyOfferChoice(
                crime,
                role,
                difficulty,
                GetBountyReward(difficulty)));
        }

        private static float GetBountyStrength(CrimeRecord crime) =>
            Math.Max(1f, crime.Offender?.Party.EstimatedStrength ?? 0f);

        private static BountyDifficulty GetBountyDifficulty(CrimeRecord crime)
        {
            float playerStrength = Math.Max(
                1f,
                MobileParty.MainParty?.Party.EstimatedStrength ?? 0f);
            float ratio = GetBountyStrength(crime) / playerStrength;
            if (ratio <= GwpTuning.Bounty.EasyStrengthRatio)
                return BountyDifficulty.Easy;
            if (ratio >= GwpTuning.Bounty.HardStrengthRatio)
                return BountyDifficulty.Hard;
            return BountyDifficulty.Standard;
        }

        private static int GetBountyReward(BountyDifficulty difficulty) => difficulty switch
        {
            BountyDifficulty.Easy => GwpTuning.Bounty.EasyReward,
            BountyDifficulty.Hard => GwpTuning.Bounty.HardReward,
            _ => GwpTuning.Bounty.StandardReward
        };

        private static string GetDifficultyText(BountyDifficulty difficulty) => difficulty switch
        {
            BountyDifficulty.Easy => GwpText.Get("{=gwp_bounty_difficulty_easy}Easy"),
            BountyDifficulty.Hard => GwpText.Get("{=gwp_bounty_difficulty_hard}Hard"),
            _ => GwpText.Get("{=gwp_bounty_difficulty_standard}Standard")
        };

        private static string GetRoleText(BountyOfferRole role) => role switch
        {
            BountyOfferRole.Harder => GwpText.Get("{=gwp_bounty_choice_harder}Harder"),
            BountyOfferRole.Easier => GwpText.Get("{=gwp_bounty_choice_easier}Easier"),
            _ => GwpText.Get("{=gwp_bounty_choice_nearest}Nearest")
        };

        private void ShowBountyInquiry(BountyOfferChoice choice)
        {
            if (!CanInspectBountyOffers()) return;
            CrimeRecord crime = choice.Crime;
            if (!crime.IsOffenderPursuable()) return;

            MobileParty target = crime.Offender!;
            string description = string.Join(
                Environment.NewLine,
                GwpText.Get("{=gwp_bounty_contract_target}Target: {VAR_1}", "VAR_1", target.Name),
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_021}Crime Type: {VAR_1}", "VAR_1", GwpText.CrimeType(crime.CrimeType)),
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_022}Last sighting: {VAR_1} nearby", "VAR_1", GetNearestSettlementName(target.GetPosition2D)),
                GwpText.Get("{=gwp_bounty_contract_difficulty}Assessed difficulty: {VAR_1}", "VAR_1", GetDifficultyText(choice.Difficulty)),
                GwpText.Get("{=gwp_bounty_contract_reward}Fixed bounty: {VAR_1} denars", "VAR_1", choice.Reward),
                GwpText.Get("{=gwp_bounty_contract_deadline}The warrant remains active for 45 days."),
                GwpText.Get("{=gwp_bounty_contract_turnin}After defeating the quarry, report to any Grey Warden lord."));

            InformationManager.ShowInquiry(
                new InquiryData(
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_027}Grey Warden Bounty"),
                    description,
                    true,
                    true,
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_028}Accept the charge"),
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_029}Refuse"),
                    () => AcceptBounty(choice),
                    () => { },
                    "event:/ui/panels/quest_start"),
                true);
        }

        private void AcceptBounty(BountyOfferChoice choice)
        {
            if (!CanInspectBountyOffers()) return;

            CrimeRecord crime = choice.Crime;
            if (!crime.IsOffenderPursuable())
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_030}The target is no longer available, and the bounty contract has been cancelled."), Colors.Red));
                return;
            }

            MobileParty offender = crime.Offender!;
            _activeBountyTargetId = offender.StringId;
            _activeBountyTargetName = offender.Name.ToString();
            _activeBountyTargetFactionId = offender.MapFaction?.StringId ?? string.Empty;
            _activeBountyTargetHeroId = crime.OffenderHeroId ?? string.Empty;
            _activeBountyCrimeCategory = (int)crime.CrimeCategory;
            _activeBountyReward = choice.Reward;
            _waitingForCollection = false;
            _activeBountyDeadlineHours = CampaignTime.Now.ToHours +
                                         GwpTuning.Bounty.DeadlineDays * 24d;

            IFaction? playerFaction = Hero.MainHero?.MapFaction;
            IFaction? targetFaction = offender.MapFaction;
            _activeBountyPlayerFactionId = playerFaction?.StringId ?? string.Empty;
            _playerFactionWasAtWarWhenBountyAccepted =
                playerFaction != null &&
                targetFaction != null &&
                FactionManager.IsAtWarAgainstFaction(playerFaction, targetFaction);
            _bountyTargetEncounterStarted = false;

            _escortPolicePartyId = CrimeState.GetAssignedPolicePartyId(offender.StringId) ?? string.Empty;
            if (!string.IsNullOrEmpty(_escortPolicePartyId))
            {
                CrimeState.SetBountyEscortFlag(_escortPolicePartyId, true);
                PoliceEnforcementBehavior.RefreshPlayerBountyAssistanceEscort(
                    _escortPolicePartyId);
                PoliceEnforcementBehavior.RefreshPlayerBountyCaseContact(
                    _escortPolicePartyId);
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_032}The Grey Warden escort is ready and will follow your pursuit until the quarry falls."),
                    Colors.Cyan));
            }

            Hero? policeLeader = PoliceStats.GetPoliceClan()?.Leader;
            if (policeLeader != null)
            {
                try
                {
                    _activeQuest = new BountyHunterQuest(
                        policeLeader,
                        _activeBountyReward,
                        offender.Name.ToString());
                    _activeQuest.StartQuest();
                    Settlement? lastSeenSettlement = FindNearestSettlement(offender.GetPosition2D);
                    TextObject lastSeenNear = lastSeenSettlement?.EncyclopediaLinkWithName ??
                                              GwpText.Create("{=gwp_playerbountybehavior_020}unknown location");
                    _activeQuest.WriteLog(
                        GwpText.Create(
                            "{=!}{VAR_1}{VAR_2}{VAR_3}",
                            "VAR_1", GwpText.Create(
                                "{=gwp_bounty_quest_target}Target: {VAR_1}. Assessed difficulty: {VAR_2}.\n",
                                "VAR_1", offender.Name,
                                "VAR_2", GetDifficultyText(choice.Difficulty)),
                            "VAR_2", GwpText.Create(
                                "{=gwp_playerbountybehavior_dialogueandnotification_034}Last sighted location: Near {VAR_1}.\n",
                                "VAR_1", lastSeenNear),
                            "VAR_3", GwpText.Create(
                                "{=gwp_bounty_quest_reward}Defeat the quarry within 45 days, then report to any Grey Warden lord for the fixed bounty of {VAR_1} denars.",
                                "VAR_1", _activeBountyReward)));
                }
                catch { _activeQuest = null!; }
            }

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(
                    "{=gwp_bounty_contract_accepted}Bounty accepted: pursue {VAR_1}. Difficulty: {VAR_2}; fixed reward: {VAR_3} denars.",
                    "VAR_1", offender.Name,
                    "VAR_2", GetDifficultyText(choice.Difficulty),
                    "VAR_3", _activeBountyReward),
                Colors.Cyan));
        }

        #endregion
    }
}
