using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
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

            // ── 赏金领取（向护送警察对话，优先）────────────────────────────────────
            // 有护送方时，玩家与护送警察对话领取赏金；无护送方时降级为族长路径。
            starter.AddPlayerLine(
                "gwp_bounty_escort_collect",
                "lord_talk_speak_diplomacy_2",
                "gwp_bounty_escort_reward_response",
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_007}I defeated the quarry named in the bounty. I have come for settlement."),
                EscortBountyRewardCondition,
                null,
                101);

            starter.AddDialogLine(
                "gwp_bounty_escort_reward_response",
                "gwp_bounty_escort_reward_response",
                "lord_pretalk",
                "{" + GwpTextKeys.BountyRewardResponse + "}",
                null,
                BountyRewardConsequence,
                100);

            // ── 赏金领取（向警察领主对话，无护送时的兜底路径）────────────────────────
            starter.AddPlayerLine(
                "gwp_bounty_collect_option",
                "lord_talk_speak_diplomacy_2",
                "gwp_bounty_reward_response",
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_008}The bounty contract is fulfilled."),
                BountyRewardCondition,
                null,
                100);

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
            MakePeaceWithCriminalFaction();
            ClearBountyTaskState();
        }

        /// <summary>
        /// 将黑袍指挥官全套装备和黑曜指挥官盾加入玩家行李。
        /// 同时输出调试信息，方便确认每件装备是否成功找到。
        /// </summary>
        private static void GiveCommanderEquipment()
        {
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null) return;

            var ids = new List<string>(GwpIds.CommanderSetItemIds);
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
            bool recruitInvolved = false;
            bool playerInvolved = false;
            MobileParty? herald = null;

            foreach (PartyBase p in mapEvent.InvolvedParties)
            {
                if (p.MobileParty != null && IsRecruitmentPatrol(p.MobileParty))
                {
                    recruitInvolved = true;
                    herald = p.MobileParty;
                }
                if (p.MobileParty != null && p.MobileParty.IsMainParty) playerInvolved = true;
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

        /// <summary>
        /// 护送方对话领赏条件：有护送方 + 等待领赏 + 正在和护送方对话。
        /// 优先于族长路径（优先级 101 vs 100）。
        /// </summary>
        private bool EscortBountyRewardCondition()
        {
            if (!IsWaitingForBountyCollection) return false;
            if (!HasEscortPoliceParty) return false;

            MobileParty? convParty = MobileParty.ConversationParty;
            if (convParty?.StringId != _escortPolicePartyId) return false;

            MBTextManager.SetTextVariable(GwpTextKeys.BountyRewardResponse,
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_016}Well done. The contract is fulfilled; here is the promised bounty: {VAR_1} denars.", "VAR_1", _pendingReward));
            return true;
        }

        /// <summary>
        /// 族长对话领赏条件：无护送方（或护送方已失联）+ 等待领赏 + 正在和族长对话。
        /// 作为护送路径不可用时的兜底。
        /// </summary>
        private bool BountyRewardCondition()
        {
            if (!IsWaitingForBountyCollection) return false;
            if (HasEscortPoliceParty) return false;

            Hero? conversationHero = Hero.OneToOneConversationHero;
            if (conversationHero == null) return false;

            Hero? policeLeader = PoliceStats.GetPoliceClan()?.Leader;
            if (policeLeader == null || conversationHero != policeLeader) return false;

            MBTextManager.SetTextVariable(GwpTextKeys.BountyRewardResponse,
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_017}Well done. As agreed, here is your due: {VAR_1} denars. May we have cause to employ you again.", "VAR_1", _pendingReward));
            return true;
        }

        private void BountyRewardConsequence()
        {
            try
            {
                int reward = _pendingReward;
                Hero.MainHero.ChangeHeroGold(reward);
                try { _activeQuest?.SucceedQuest(); } catch { }
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_018}Bounty received from a Warden-lord: {VAR_1} denars", "VAR_1", reward),
                    Colors.Green));
                MakePeaceWithCriminalFaction();
            }
            catch { }
            finally
            {
                ClearBountyTaskState();
            }
        }

        private void MakePeaceWithCriminalFaction()
        {
            if (string.IsNullOrEmpty(_activeBountyTargetFactionId)) return;

            try
            {
                IFaction? playerFaction = Hero.MainHero?.MapFaction;
                if (playerFaction == null) return;

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

        #region 悬赏派发（右侧通知面板）

        private void OfferBounty(CrimeRecord crime)
        {
            if (!crime.IsOffenderValid()) return;

            TryRegisterNotificationType();
            var notification = new BountyMapNotification(crime);
            try { Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(notification); } catch { }
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

        internal void ShowBountyInquiry(CrimeRecord crime)
        {
            if (!_recruitmentAccepted ||
                PlayerState.Reputation < GwpTuning.Bounty.RecruitmentReputationThreshold ||
                !IsWearingCommanderSet())
                return;
            if (crime == null || !crime.IsOffenderValid()) return;
            if (HasBountyTask) return;

            MobileParty? target = crime.Offender;
            if (target == null) return;

            int targetSize = target.Party.NumberOfAllMembers;
            int estimatedReward = targetSize * GwpTuning.Bounty.RewardPerTroop;
            string nearestSettlement = GetNearestSettlementName(target.GetPosition2D);

            string description =
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_020}Target Faction: {VAR_1}", "VAR_1", target.Name) +
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_021}Crime Type: {VAR_1}", "VAR_1", GwpText.CrimeType(crime.CrimeType)) +
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_022}Last sighting: {VAR_1} nearby", "VAR_1", nearestSettlement) +
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_023}Party strength: {VAR_1} people", "VAR_1", targetSize) +
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_024}Estimated bounty: About {VAR_1} dinars", "VAR_1", estimatedReward) +
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_025}(settled at the strength accepted × {VAR_1})", "VAR_1", GwpTuning.Bounty.RewardPerTroop) +
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_026}When the quarry is defeated, seek a Warden-lord to claim the bounty.");

            InformationManager.ShowInquiry(
                new InquiryData(
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_027}Grey Warden Bounty"),
                    description,
                    true, true,
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_028}Accept the charge"), GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_029}Refuse"),
                    () => AcceptBounty(crime),
                    () => { },
                    "event:/ui/panels/quest_start"),
                true);
        }

        private void AcceptBounty(CrimeRecord crime)
        {
            if (!_recruitmentAccepted ||
                PlayerState.Reputation < GwpTuning.Bounty.RecruitmentReputationThreshold ||
                !IsWearingCommanderSet())
                return;
            if (!crime.IsOffenderValid())
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_030}The target has expired and the bounty contract has been cancelled."), Colors.Red));
                return;
            }

            MobileParty? offender = crime.Offender;
            if (offender == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_031}The goal has expired and the bounty contract has been cancelled."), Colors.Red));
                return;
            }

            _activeBountyTargetId = offender.StringId;
            _activeBountyTargetName = offender.Name.ToString();
            _activeBountyTargetFactionId = offender.MapFaction?.StringId ?? string.Empty;
            _activeBountyTargetHeroId = crime.OffenderHeroId ?? string.Empty;
            _activeBountyCrimeCategory = (int)crime.CrimeCategory;
            _activeBountyTargetSize = offender.Party.NumberOfAllMembers;

            _escortPolicePartyId = CrimeState.GetAssignedPolicePartyId(offender.StringId) ?? string.Empty;
            if (!string.IsNullOrEmpty(_escortPolicePartyId))
            {
                CrimeState.SetBountyEscortFlag(_escortPolicePartyId, true);
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_032}The Grey Warden escort is ready and will follow your pursuit. Once the quarry falls, claim the bounty from the escorting Warden."),
                    Colors.Cyan));
            }

            Hero? policeLeader = PoliceStats.GetPoliceClan()?.Leader;
            if (policeLeader != null)
            {
                try
                {
                    _activeQuest = new BountyHunterQuest(
                        policeLeader,
                        _activeBountyTargetSize * GwpTuning.Bounty.RewardPerTroop,
                        offender.Name.ToString());
                    _activeQuest.StartQuest();
                    string lastSeenNear = GetNearestSettlementName(offender.GetPosition2D);
                    _activeQuest.WriteLog(
                        GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_033}Target: {VAR_1} (currently {VAR_2} people).", "VAR_1", offender.Name, "VAR_2", _activeBountyTargetSize) +
                        GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_034}Last sighted location: Near {VAR_1}.", "VAR_1", lastSeenNear) +
                        GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_035}After defeating, go to the warden-lord to collect the bounty of approximately {VAR_1} dinars.", "VAR_1", _activeBountyTargetSize * GwpTuning.Bounty.RewardPerTroop));
                }
                catch { _activeQuest = null!; }
            }

            int estimatedGold = _activeBountyTargetSize * GwpTuning.Bounty.RewardPerTroop;
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_036}has accepted the bounty contract: chasing {VAR_1}", "VAR_1", offender.Name) +
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_037}({VAR_1} person), the reward is about {VAR_2} dinars", "VAR_1", _activeBountyTargetSize, "VAR_2", estimatedGold),
                Colors.Cyan));
        }

        #endregion
    }
}
