using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
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
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_002}Then take this harness. Wear it when you ride under our warrant, and bring the money or prisoner to a Warden-lord.")
                + GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_003} If the pursuit brings war upon you, we will speak for you when the matter is settled."),
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

            RegisterCaseTurnInDialogues(starter);
            RegisterSupportDialogue(starter);

            // ── 读档后悬赏任务恢复（兜底）─────────────────────────────────────────
            // 此时 SyncData 已完成，所有持久化字段均已正确加载，可以安全访问。
            ReconcileRecruitmentPatrolState();
            MigrateCaseSettlement();
            TryRestoreBountyQuestOnSessionStart();
            RetireLegacyCollectionCouriers();
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
                GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_009}The Grey Wardens have heard of your deeds. Will you hunt outlaws for us? There is pay for the work."));
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
            // Leaving employment ends authority, not the obligation to hand over
            // fines already collected or a prisoner already accepted for delivery.
            if (HasFieldBusinessToSettle) return;

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
        /// 结算队已经退役：玩家现在可以找任意灰袍领主复命，也可以派自己的人去，不再需要
        /// 灰袍主动找上门这道兜底。旧存档里可能还有一支停在路上，读档时一并解散。
        /// </summary>
        private static void RetireLegacyCollectionCouriers()
        {
            foreach (MobileParty party in MobileParty.All
                         .Where(candidate => candidate?.IsActive == true &&
                             candidate.StringId?.StartsWith("gwp_bounty_collect_",
                                 StringComparison.Ordinal) == true)
                         .ToList())
            {
                try
                {
                    GreyWardenPartyDesireBehavior.ClearIntent(party);
                    DestroyPartyAction.Apply(null, party);
                }
                catch { }
            }
        }

        /// <summary>Only mediate the saved commission faction pair, never a pre-existing war.</summary>
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
                // 结案不再替玩家自动讲和。这一战本来就是替灰袍办案打的，记进申请通道，
                // 由玩家自己去找灰袍或派使者一并了结。
                PoliceAntiWarDeclaration.RecordMediationRequest(
                    criminalFaction, "bounty_case_closed");
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
            MeetsBountyEquipmentAndStanding() &&
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
                    "{=gwp_bounty_choice_label}{VAR_1}: {VAR_2} — {VAR_3}; assessed fine {VAR_4} denars",
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
                    GwpText.Get("{=gwp_bounty_select_description}Choose a nearby, stronger or weaker offender. The amount shown is the assessed fine, not your personal payment."),
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
                GwpFieldArrestPricing.AssessFine(crime)));
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
                GwpText.Get("{=gwp_bounty_contract_reward}Assessed fine: {VAR_1} denars. Once you deliver, we will review the case and your losses and settle your expenses.", "VAR_1", choice.Reward),
                GwpText.Get("{=gwp_bounty_contract_deadline}The warrant remains active for 45 days."),
                GwpText.Get("{=gwp_bounty_contract_turnin}Negotiate payment or take the offender prisoner, then report to any Grey Warden lord. Winning a battle alone does not complete delivery."));

            InformationManager.ShowInquiry(
                new InquiryData(
                    GwpText.Get("{=gwp_playerbountybehavior_dialogueandnotification_027}Grey Warden commission"),
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
            _activeBountyReward = 0;
            _fieldCaseContract = true;
            _assignedCaseFine = choice.Reward;
            _assignedCaseStanding = Math.Max(0, CrimePool.GetHistory(crime.OffenderHero)?.NegativeStanding ?? 0);
            _bountyPlayerCasualties = 0;
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

            // 接了案子，这宗案子就是玩家的。原来的承办灰袍就此撤出去办别的事，不再
            // 跟着、也不再自行追捕；要人手，玩家派人去求援。
            _escortPolicePartyId = string.Empty;
            _supportRequested = false;
            PoliceEnforcementBehavior.ReleaseWardensFromCase(_activeBountyTargetHeroId);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_bounty_case_is_yours}The case is yours now. If you need men, send someone to ask the Wardens for help."),
                Colors.Cyan));

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
                                "{=gwp_bounty_quest_reward}Within 45 days, collect the assessed fine of {VAR_1} denars or capture the offender. Deliver the money or prisoner to a Grey Warden lord. Expenses depend on the actual delivery.",
                                "VAR_1", _assignedCaseFine)));
                }
                catch { _activeQuest = null!; }
            }

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(
                    "{=gwp_bounty_contract_accepted}Commission accepted: {VAR_1}. Difficulty: {VAR_2}; assessed fine: {VAR_3} denars.",
                    "VAR_1", offender.Name,
                    "VAR_2", GetDifficultyText(choice.Difficulty),
                    "VAR_3", _assignedCaseFine),
                Colors.Cyan));
        }

        #endregion
    }
}
