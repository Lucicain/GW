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
                "我接受，愿为灰袍守卫效力。",
                null, null, 100);

            starter.AddDialogLine(
                "gwp_recruit_accept_response",
                "gwp_recruit_accept_response",
                "close_window",
                "很好。这套装备能让我们认出你的身份。击败通缉犯后，前往我们首领处领取赏金。"
                + "务必记住——追捕时引发的战争，灰袍会在任务结算后出面调停。",
                null,
                OnRecruitAcceptConsequence,
                100);

            starter.AddPlayerLine(
                "gwp_recruit_refuse",
                "gwp_recruit_options",
                "gwp_recruit_refuse_response",
                "不，我不感兴趣。",
                null, null, 100);

            starter.AddDialogLine(
                "gwp_recruit_refuse_response",
                "gwp_recruit_refuse_response",
                "close_window",
                "随你。若你改变主意，为时已晚——此机会不会再来。",
                null,
                OnRecruitRefuseConsequence,
                100);

            // ── 招募已完成时的兜底对话 ──────────────────────────────────────────────
            // LeaveEncounter = true 正常情况下已足够；这条对话防止极端情况下
            // 遭遇系统在 close_window 后再次触发对话，导致"我不能和你说话"→战斗准备。
            starter.AddDialogLine(
                "gwp_recruit_already_done",
                "start",
                "close_window",
                "我们的事务已了结，请继续前行。",
                () =>
                {
                    MobileParty? conv = MobileParty.ConversationParty;
                    if (conv == null || !IsRecruitmentPatrol(conv)) return false;
                    if (!_recruitmentOffered) return false;
                    // 兜底：再次确保遭遇被和平关闭
                    if (PlayerEncounter.IsActive)
                        PlayerEncounter.LeaveEncounter = true;
                    return true;
                },
                TriggerPatrolReturn,
                100);

            // ── 赏金领取（向护送警察对话，优先）────────────────────────────────────
            // 有护送方时，玩家与护送警察对话领取赏金；无护送方时降级为族长路径。
            starter.AddPlayerLine(
                "gwp_bounty_escort_collect",
                "lord_talk_speak_diplomacy_2",
                "gwp_bounty_escort_reward_response",
                "关于那个悬赏任务，我已击败目标，来结算赏金。",
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
                "关于那个悬赏任务，我已经完成了。",
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
                "旅行者，稍等。灰袍守卫注意到你近来的善行，我们想与你谈一笔生意。"
                + "作为悬赏猎人，你可以协助我们追捕通缉犯，并获得丰厚赏金。"
                + "但需提醒：追捕行动可能被当地国家视为入侵，引发战争。"
                + "不过，任务完成并领取赏金后，灰袍守卫会出面调停，"
                + "确保你不会承担战争后果。你是否愿意加入？");
            return true;
        }

        private void OnRecruitAcceptConsequence()
        {
            _recruitmentAccepted = true;
            _recruitmentOffered = true;
            GiveCommanderEquipment();

            if (PlayerEncounter.IsActive)
                PlayerEncounter.LeaveEncounter = true;

            TriggerPatrolReturn();

            InformationManager.DisplayMessage(new InformationMessage(
                "你已成为灰袍悬赏猎人！黑袍指挥官套装已加入行李，穿戴后即可接受悬赏任务。",
                Colors.Green));
        }

        private void OnRecruitRefuseConsequence()
        {
            _recruitmentOffered = true;

            if (PlayerEncounter.IsActive)
                PlayerEncounter.LeaveEncounter = true;

            TriggerPatrolReturn();

            InformationManager.DisplayMessage(new InformationMessage(
                "你拒绝了灰袍守卫的招募，此机会不会再来。",
                Colors.Yellow));
        }

        /// <summary>
        /// 将黑袍指挥官全套五件装备加入玩家行李。
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

            foreach (PartyBase p in mapEvent.InvolvedParties)
            {
                if (p.MobileParty != null && IsRecruitmentPatrol(p.MobileParty)) recruitInvolved = true;
                if (p.MobileParty != null && p.MobileParty.IsMainParty) playerInvolved = true;
            }

            if (recruitInvolved && playerInvolved &&
                PlayerEncounter.IsActive && PlayerEncounter.EncounteredParty != null)
            {
                try { PlayerEncounter.DoMeeting(); } catch { }
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
                $"出色的工作。任务已完成，这是约定的赏金：{_pendingReward} 第纳尔。");
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
                $"出色的工作。按照约定，这是你应得的赏金：{_pendingReward} 第纳尔。希望我们还有合作的机会。");
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
                    $"已从警察领主处领取悬赏赏金：{reward} 第纳尔",
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
                        $"灰袍调停：已与 {criminalFaction.Name} 达成和平", Colors.Green));
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
            if (crime == null || !crime.IsOffenderValid()) return;
            if (HasBountyTask) return;

            MobileParty? target = crime.Offender;
            if (target == null) return;

            int targetSize = target.Party.NumberOfAllMembers;
            int estimatedReward = targetSize * GwpTuning.Bounty.RewardPerTroop;
            string nearestSettlement = GetNearestSettlementName(target.GetPosition2D);

            string description =
                $"目标势力：{target.Name}\n" +
                $"犯罪类型：{crime.CrimeType}\n" +
                $"最后目击：{nearestSettlement} 附近\n" +
                $"队伍规模：{targetSize} 人\n" +
                $"预计赏金：约 {estimatedReward} 第纳尔\n" +
                $"（按接任务时人数 × {GwpTuning.Bounty.RewardPerTroop} 结算）\n\n" +
                $"完成后前往警察家族领主处领取赏金。";

            InformationManager.ShowInquiry(
                new InquiryData(
                    "灰袍悬赏任务",
                    description,
                    true, true,
                    "接受任务", "拒绝",
                    () => AcceptBounty(crime),
                    () => { },
                    "event:/ui/panels/quest_start"),
                true);
        }

        private void AcceptBounty(CrimeRecord crime)
        {
            if (!crime.IsOffenderValid())
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "目标已失效，悬赏任务取消", Colors.Red));
                return;
            }

            MobileParty? offender = crime.Offender;
            if (offender == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "目标已失效，悬赏任务取消", Colors.Red));
                return;
            }

            _activeBountyTargetId = offender.StringId;
            _activeBountyTargetName = offender.Name.ToString();
            _activeBountyTargetFactionId = offender.MapFaction?.StringId ?? string.Empty;
            _activeBountyTargetSize = offender.Party.NumberOfAllMembers;

            _escortPolicePartyId = CrimeState.GetAssignedPolicePartyId(offender.StringId) ?? string.Empty;
            if (!string.IsNullOrEmpty(_escortPolicePartyId))
            {
                CrimeState.SetBountyEscortFlag(_escortPolicePartyId, true);
                InformationManager.DisplayMessage(new InformationMessage(
                    "灰袍护送方已就位，跟随你追击目标。击败后直接向护送警察领取赏金。",
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
                        $"目标：{offender.Name}（当前 {_activeBountyTargetSize} 人）。\n" +
                        $"最后目击位置：{lastSeenNear} 附近。\n" +
                        $"击败后前往警察领主处领取赏金约 {_activeBountyTargetSize * GwpTuning.Bounty.RewardPerTroop} 第纳尔。");
                }
                catch { _activeQuest = null!; }
            }

            int estimatedGold = _activeBountyTargetSize * GwpTuning.Bounty.RewardPerTroop;
            InformationManager.DisplayMessage(new InformationMessage(
                $"已接受悬赏任务：追击 {offender.Name}" +
                $"（{_activeBountyTargetSize} 人），赏金约 {estimatedGold} 第纳尔",
                Colors.Cyan));
        }

        #endregion
    }
}
