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
using TaleWorlds.CampaignSystem.Roster;
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
                GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_dialogueandnotification_001),
                null, OnRecruitAcceptConsequence, 100);

            starter.AddDialogLine(
                "gwp_recruit_accept_response",
                "gwp_recruit_accept_response",
                "close_window",
                GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_dialogueandnotification_002)
                + GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_dialogueandnotification_003),
                null,
                null,
                100);

            starter.AddPlayerLine(
                "gwp_recruit_refuse",
                "gwp_recruit_options",
                "gwp_recruit_refuse_response",
                GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_dialogueandnotification_004),
                null, OnRecruitRefuseConsequence, 100);

            starter.AddDialogLine(
                "gwp_recruit_refuse_response",
                "gwp_recruit_refuse_response",
                "close_window",
                GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_dialogueandnotification_005),
                null,
                null,
                100);

            starter.AddPlayerLine("gwp_duty_banner_request", "lord_talk_speak_diplomacy_2",
                "gwp_duty_banner_issued", GwpText.Get(GwpTextCatalog.gwp_duty_banner_request),
                CanRequestDutyBanner, null, 102);
            starter.AddDialogLine("gwp_duty_banner_issued", "gwp_duty_banner_issued",
                "lord_pretalk", GwpText.Get(GwpTextCatalog.gwp_duty_banner_issued),
                null, IssueDutyBanner, 100);

            // ── 通过灰袍领主重新加入 / 主动退出 ──────────────────────────────────
            starter.AddPlayerLine(
                "gwp_membership_rejoin",
                "lord_talk_speak_diplomacy_2",
                "gwp_membership_rejoin_response",
                GwpText.Get(GwpTextCatalog.gwp_membership_rejoin_player),
                CanRejoinThroughLord,
                null,
                102);

            starter.AddDialogLine(
                "gwp_membership_rejoin_response",
                "gwp_membership_rejoin_response",
                "lord_pretalk",
                GwpText.Get(GwpTextCatalog.gwp_membership_rejoin_response),
                null,
                OnRejoinThroughLord,
                100);

            starter.AddPlayerLine(
                "gwp_membership_leave",
                "lord_talk_speak_diplomacy_2",
                "gwp_membership_leave_prompt",
                GwpText.Get(GwpTextCatalog.gwp_membership_leave_player),
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
                GwpText.Get(GwpTextCatalog.gwp_membership_leave_confirm),
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_membership_leave_done",
                "gwp_membership_leave_done",
                "lord_pretalk",
                GwpText.Get(GwpTextCatalog.gwp_membership_leave_done),
                null,
                OnLeaveThroughLord,
                100);

            starter.AddPlayerLine(
                "gwp_membership_leave_cancel",
                "gwp_membership_leave_options",
                "gwp_membership_leave_cancelled",
                GwpText.Get(GwpTextCatalog.gwp_membership_leave_cancel),
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_membership_leave_cancelled",
                "gwp_membership_leave_cancelled",
                "lord_pretalk",
                GwpText.Get(GwpTextCatalog.gwp_membership_leave_cancelled),
                null,
                null,
                100);

            // 碰到正在返程的使者时，结束会面并让使者继续返城。
            starter.AddDialogLine(
                "gwp_recruit_already_done",
                "start",
                "close_window",
                GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_dialogueandnotification_006),
                () =>
                {
                    MobileParty? conv = MobileParty.ConversationParty;
                    return IsRecruitmentPatrol(conv) && !CanOfferRecruitment(conv);
                },
                () => CloseRecruitmentEncounterAndReturn(MobileParty.ConversationParty),
                100);

            RegisterCaseTurnInDialogues(starter);
            RegisterSupportDialogue(starter);
            RegisterCaseCustodyDialogues(starter);

            // ── 读档后悬赏任务恢复（兜底）─────────────────────────────────────────
            // 此时 SyncData 已完成，所有持久化字段均已正确加载，可以安全访问。
            RestoreCaseSettlementAfterLoad();
            ReconcileRecruitmentPatrolState();
            TryRestoreBountyQuestOnSessionStart();
        }

        #endregion

        #region 招募对话逻辑

        private bool RecruitDialogCondition()
        {
            MobileParty? conversationParty = MobileParty.ConversationParty;
            if (conversationParty == null) return false;
            if (!IsRecruitmentPatrol(conversationParty) || !CanOfferRecruitment(conversationParty))
                return false;

            MBTextManager.SetTextVariable(GwpTextKeys.RecruitGreeting,
                GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_dialogueandnotification_009));
            return true;
        }

        private bool CanOfferRecruitment(MobileParty? patrol) =>
            patrol != null && patrol.StringId == _recruitmentPatrolId &&
            !_recruitmentOffered && !_recruitmentAccepted && !_recruitmentPatrolReturning;

        private void OnRecruitAcceptConsequence()
        {
            MobileParty? herald = MobileParty.ConversationParty;
            if (!CanOfferRecruitment(herald)) return;
            _recruitmentAccepted = true;
            _recruitmentOffered = true;
            GiveCommanderEquipment();
            GiveDutyBanner();
            GiveStarterRecruits();
            CloseRecruitmentEncounterAndReturn(herald);

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_dialogueandnotification_014),
                Colors.Green));
        }

        private void OnRecruitRefuseConsequence()
        {
            MobileParty? herald = MobileParty.ConversationParty;
            if (!CanOfferRecruitment(herald)) return;
            _recruitmentOffered = true;
            CloseRecruitmentEncounterAndReturn(herald);

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_dialogueandnotification_015),
                Colors.Yellow));
        }

        /// <summary>
        /// 对话关闭前下达返程，原版清理结束后为同一支使者重发返程命令。
        /// </summary>
        private void CloseRecruitmentEncounterAndReturn(MobileParty? herald)
        {
            if (herald == null) return;

            if (PlayerEncounter.EncounteredMobileParty == herald)
                PlayerEncounter.LeaveEncounter = true;

            ReturnRecruitmentPatrol(herald);

            Campaign.Current.ConversationManager.ConversationEndOneShot +=
                () => FinishRecruitmentEncounterAndReturn(herald);
        }

        private void FinishRecruitmentEncounterAndReturn(MobileParty herald)
        {
            if (PlayerEncounter.EncounteredMobileParty == herald)
            {
                PlayerEncounter.LeaveEncounter = true;
                PlayerEncounter.Finish();
            }

            ReturnRecruitmentPatrol(herald);
        }

        private bool CanRejoinThroughLord()
        {
            return GwpCommon.IsOrdinaryGreyWardenLordConversation() &&
                   _recruitmentOffered &&
                   !_recruitmentAccepted &&
                   _voluntaryExitCount < GwpTuning.Bounty.MaximumVoluntaryExits &&
                   PlayerBehaviorPool.Reputation >= GetRequiredReadmissionReputation();
        }

        private bool CanLeaveThroughLord()
        {
            return GwpCommon.IsOrdinaryGreyWardenLordConversation() && _recruitmentAccepted;
        }

        private bool CanRequestDutyBanner() => CanLeaveThroughLord()
            && !GwpCommon.IsPlayerGreyWardenOnDuty()
            && MobileParty.MainParty?.ItemRoster.Any(element =>
                element.EquipmentElement.Item?.StringId == GwpIds.DutyBannerItemId) == false
            && MBObjectManager.Instance.GetObject<ItemObject>(GwpIds.DutyBannerItemId) != null;

        private void IssueDutyBanner()
        {
            if (!CanRequestDutyBanner()) return;
            GiveDutyBanner();
        }

        private static void GiveDutyBanner()
        {
            if (Hero.MainHero.BannerItem.Item?.StringId == GwpIds.DutyBannerItemId ||
                MobileParty.MainParty.ItemRoster.Any(element => element.EquipmentElement.Item?.StringId == GwpIds.DutyBannerItemId))
                return;
            ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(GwpIds.DutyBannerItemId);
            MobileParty.MainParty.ItemRoster.AddToCounts(new EquipmentElement(item), 1);
        }

        private static void RecoverDutyBanners()
        {
            foreach (ItemRosterElement element in MobileParty.MainParty.ItemRoster
                         .Where(element => element.EquipmentElement.Item?.StringId == GwpIds.DutyBannerItemId).ToList())
                MobileParty.MainParty.ItemRoster.AddToCounts(element.EquipmentElement, -element.Amount);
            foreach (Equipment equipment in new[] { Hero.MainHero.BattleEquipment, Hero.MainHero.CivilianEquipment })
                if (equipment[EquipmentIndex.ExtraWeaponSlot].Item?.StringId == GwpIds.DutyBannerItemId)
                    equipment[EquipmentIndex.ExtraWeaponSlot] = default;
            GwpDutyBanner.RefreshPlayerClanVisuals();
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
                    GwpTextCatalog.gwp_membership_leave_prompt_final);
            }
            else
            {
                int nextThreshold = GwpTuning.Bounty.RecruitmentReputationThreshold +
                                    nextExitCount * GwpTuning.Bounty.ReadmissionReputationStep;
                prompt = GwpText.Get(
                    GwpTextCatalog.gwp_membership_leave_prompt_higher,
                    "VAR_1", nextThreshold);
            }

            MBTextManager.SetTextVariable(GwpTextKeys.MembershipLeavePrompt, prompt);
            return true;
        }

        private void OnRejoinThroughLord()
        {
            _recruitmentAccepted = true;
            _recruitmentOffered = true;
            DestroyRecruitmentPatrol();
            // 第一次拒绝使者、之后从领主处加入仍算首次；主动退出过就不再发整套。
            if (_voluntaryExitCount == 0) GiveCommanderEquipment();
            GiveDutyBanner();
            GiveStarterRecruits();
        }

        private void OnLeaveThroughLord()
        {
            _recruitmentAccepted = false;
            RecoverDutyBanners();
            _recruitmentOffered = true;
            _voluntaryExitCount = Math.Min(
                GwpTuning.Bounty.MaximumVoluntaryExits,
                _voluntaryExitCount + 1);
            DestroyRecruitmentPatrol();

            if (!HasBountyTask) return;
            // Leaving employment ends authority, not the obligation to hand over
            // fines already collected or a prisoner already accepted for delivery.
            if (HasFieldBusinessToSettle) return;

            _activeQuest?.FailQuestMembershipEnded();
            EndBountyTaskState(tryRestorePeace: true);
        }

        /// <summary>
        /// 将黑袍指挥官全套装备、黑曜指挥官盾和双刀加入玩家行李。
        /// 同时输出调试信息，方便确认每件装备是否成功找到。
        /// </summary>
        /// <summary>
        /// 入会时发下一小批灰袍新兵。
        ///
        /// 灰袍兵种之前唯一的获取途径是找练兵官下订单，刚入会的玩家
        /// 手上一个都没有。这里只给最低级的 `gwnewrecruit`，往上升仍然走
        /// 原有的练兵与升级管线，不绕过任何现有门槛。
        ///
        /// 重新入会同样发放：自愿退会次数有 `MaximumVoluntaryExits` 封顶，
        /// 来回刷的上限就是那么几批最低级新兵，不值得为此再加一套计数。
        /// </summary>
        private static void GiveStarterRecruits()
        {
            var roster = MobileParty.MainParty?.MemberRoster;
            if (roster == null) return;

            CharacterObject? recruit = CharacterObject.Find(GwpIds.NewRecruitId);
            if (recruit == null) return;

            int count = Math.Min(GwpTuning.Bounty.JoinRecruitCount,
                GwpPlayerWardenTroops.RemainingSlots(roster));
            if (count == 0) return;
            roster.AddToCounts(recruit, count);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_join_recruits_granted,
                    "VAR_1", count),
                Colors.Green));
        }

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
                    // 每件取所在品质组里最好的原版词条：品质档最高（传奇），同档取价格倍率最高。
                    // 灰袍装备不进店铺、不进掉落，这是它唯一带上词条的地方。（用户裁定 2026-10-02）
                    ItemModifier? modifier = item.ItemComponent?.ItemModifierGroup?.ItemModifiers
                        .OrderByDescending(candidate => candidate.ItemQuality)
                        .ThenByDescending(candidate => candidate.PriceMultiplier)
                        .FirstOrDefault();
                    roster.AddToCounts(new EquipmentElement(item, modifier), 1);
                }
            }
        }

        #endregion

        #region 玩家与悬赏目标卷入同一场战斗

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null) return;

            bool playerInvolved = false;
            bool bountyTargetInvolved = false;

            foreach (PartyBase p in mapEvent.InvolvedParties)
            {
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
        }

        #endregion

        #region 赏金领取对话

        /// <summary>Only mediate the saved commission faction pair, never a pre-existing war.</summary>
        private void MakePeaceWithCriminalFaction()
        {
            if (string.IsNullOrEmpty(_activeBountyTargetFactionId)) return;
            if (string.IsNullOrEmpty(_activeBountyPlayerFactionId)) return;
            if (_playerFactionWasAtWarWhenBountyAccepted) return;
            if (!_bountyTargetEncounterStarted) return;

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

        #endregion

        #region 事务页接案

        private enum BountyDifficulty
        {
            Easy,
            Standard,
            Hard
        }

        private string? GetBountyOfferBlocker()
        {
            if (!_recruitmentAccepted)
                return GwpText.Get(GwpTextCatalog.gwp_affairs_nav_need_membership);
            if (HasBountyTask)
                return GwpText.Get(GwpTextCatalog.gwp_bounty_duty_busy);
            if (!MeetsBountyDutyAndStanding())
                return GwpText.Get(GwpTextCatalog.gwp_bounty_duty_requirements,
                    "STANDING", GwpTuning.Bounty.RecruitmentReputationThreshold);
            return GetAffairsOffers().Count == 0
                ? GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_012) : null;
        }

        internal bool CanInspectBountyOffers() => GetBountyOfferBlocker() == null;

        internal string GetBountyOfferStatusText() => GetBountyOfferBlocker()
            ?? GwpText.Get(GwpTextCatalog.gwp_bounty_duty_ready);

        // The affairs screen lists every currently eligible case; no three-choice picker.
        internal IReadOnlyList<CrimeRecord> GetAffairsOffers() =>
            CrimePool.GetAvailablePlayerBounties()
                .Where(crime => GreyWardenDutyScheduler.IsRoadsAndVillagesCase(crime.CrimeCategory))
                .OrderByDescending(EstimateAffairsReward)
                .ThenBy(crime => crime.CrimeId, StringComparer.OrdinalIgnoreCase).ToList();

        internal static int EstimateAffairsReward(CrimeRecord crime) =>
            GwpCaseSettlementRules.Reward(GwpFieldArrestPricing.AssessFine(crime), 0,
                Math.Max(0, CrimePool.GetHistory(crime.OffenderHero)?.NegativeStanding ?? 0));

        internal static string AffairsDifficulty(CrimeRecord crime) => GetDifficultyText(GetBountyDifficulty(crime));

        internal bool TryAcceptAffairsCase(string crimeId, int displayedFine, int displayedReward)
        {
            if (!CanInspectBountyOffers()) return false;
            CrimeRecord? crime = GetAffairsOffers().FirstOrDefault(row =>
                string.Equals(row.CrimeId, crimeId, StringComparison.OrdinalIgnoreCase));
            // Re-read the authority's pool: stale rows, agreements and withdrawn cases are not contracts.
            if (crime == null || GwpFieldArrestPricing.AssessFine(crime) != displayedFine ||
                EstimateAffairsReward(crime) != displayedReward) return false;
            AcceptBounty(crime, displayedFine);
            return true;
        }

        // 与接案门槛同一个口径：目标所在战斗组，不算路过的人。
        private static float GetBountyStrength(CrimeRecord crime) =>
            Math.Max(1f, GwpStrength.CaseTarget(crime));

        private static BountyDifficulty GetBountyDifficulty(CrimeRecord crime)
        {
            float playerStrength = Math.Max(1f,
                MobileParty.MainParty == null ? 0f : GwpStrength.CombatGroup(MobileParty.MainParty));
            float ratio = GetBountyStrength(crime) / playerStrength;
            if (ratio <= GwpTuning.Bounty.EasyStrengthRatio)
                return BountyDifficulty.Easy;
            if (ratio >= GwpTuning.Bounty.HardStrengthRatio)
                return BountyDifficulty.Hard;
            return BountyDifficulty.Standard;
        }

        private static string GetDifficultyText(BountyDifficulty difficulty) => difficulty switch
        {
            BountyDifficulty.Easy => GwpText.Get(GwpTextCatalog.gwp_bounty_difficulty_easy),
            BountyDifficulty.Hard => GwpText.Get(GwpTextCatalog.gwp_bounty_difficulty_hard),
            _ => GwpText.Get(GwpTextCatalog.gwp_bounty_difficulty_standard)
        };

        private void AcceptBounty(CrimeRecord crime, int assessedFine)
        {
            if (!CanInspectBountyOffers()) return;

            // 野外有部队的照旧追部队；没带部队、住在城里的，记下人和他所在的城，进城搜捕。
            MobileParty? offender = crime.IsOffenderPursuable() ? crime.Offender : null;
            Settlement? shelter = offender == null && crime.IsOffenderSearchableInSettlement() ? crime.ShelterSettlement : null;
            Hero? offenderHero = crime.OffenderHero;
            if (offender == null && (shelter == null || offenderHero == null))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_dialogueandnotification_030), Colors.Red));
                return;
            }

            TextObject targetName = offender?.Name ?? offenderHero!.Name;
            IFaction? targetFaction = offender?.MapFaction ?? offenderHero!.MapFaction;
            _activeBountyTargetId = offender?.StringId!;
            _activeBountyTargetName = targetName.ToString();
            _activeBountyTargetFactionId = targetFaction?.StringId ?? string.Empty;
            _activeBountyTargetHeroId = crime.OffenderHeroId ?? string.Empty;
            _assignedCaseFine = assessedFine;
            _assignedCaseStanding = Math.Max(0, CrimePool.GetHistory(crime.OffenderHero)?.NegativeStanding ?? 0);
            _bountyPlayerCasualties = 0;
            _waitingForCollection = false;
            _activeBountyDeadlineHours = CampaignTime.Now.ToHours +
                                         GwpTuning.Bounty.DeadlineDays * 24d;

            IFaction? playerFaction = Hero.MainHero?.MapFaction;
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
                GwpText.Get(GwpTextCatalog.gwp_bounty_case_is_yours),
                Colors.Cyan));

            Hero? policeLeader = PoliceStats.GetPoliceClan()?.Leader;
            if (policeLeader != null)
            {
                _activeQuest = new BountyHunterQuest(
                    policeLeader,
                    targetName.ToString());
                _activeQuest.StartQuest();
                Settlement? lastSeenSettlement = shelter ?? FindNearestSettlement(offender!.GetPosition2D);
                TextObject lastSeenNear = lastSeenSettlement?.EncyclopediaLinkWithName ??
                                          GwpText.Create(GwpTextCatalog.gwp_playerbountybehavior_020);
                _activeQuest.WriteLog(
                    GwpText.Create(
                        "{=!}{VAR_1}{VAR_2}{VAR_3}",
                        "VAR_1", GwpText.Create(
                            GwpTextCatalog.gwp_bounty_quest_target,
                            "VAR_1", targetName,
                            "VAR_2", GetDifficultyText(GetBountyDifficulty(crime))),
                        "VAR_2", GwpText.Create(
                            shelter != null ? GwpTextCatalog.gwp_bounty_quest_sheltered
                                : GwpTextCatalog.gwp_playerbountybehavior_dialogueandnotification_034,
                            "VAR_1", lastSeenNear),
                        "VAR_3", GwpText.Create(
                            GwpTextCatalog.gwp_bounty_quest_reward,
                            "VAR_1", _assignedCaseFine)));
            }

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(
                    GwpTextCatalog.gwp_bounty_contract_accepted,
                    "VAR_1", targetName,
                    "VAR_2", GetDifficultyText(GetBountyDifficulty(crime)),
                    "VAR_3", _assignedCaseFine),
                Colors.Cyan));
        }

        #endregion
    }
}
