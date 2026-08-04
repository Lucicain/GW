using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using TaleWorlds.ScreenSystem;

namespace GreyWardenPolicePurity
{
    public partial class PlayerBountyBehavior
    {
        #region 任务日志（QuestBase）

        /// <summary>
        /// ★ internal（非 private）：存档系统需通过反射访问此类型。
        /// 任务本体由原版 QuestManager 保存，行为层只保存追捕状态。
        /// </summary>
        internal sealed class BountyHunterQuest : QuestBase
        {
            // [SaveableField] 让 Bannerlord 存档系统在序列化/反序列化时自动保存此字段。
            // 不加此标注则读档后 _targetName = null，任务标题变为"灰袍悬赏：未知目标"。
            // ID=1 在本类内唯一；基类 QuestBase 使用 100~107，无冲突。
            [SaveableField(1)]
            private string _targetName;

            [SaveableField(2)]
            private bool _readyForTurnInLogWritten;

            /// <summary>
            /// 正常构造器：接受悬赏任务时调用。
            /// questGiver 须为警察领主；rewardGold 用于任务日志显示。
            /// </summary>
            public BountyHunterQuest(Hero questGiver, int rewardGold, string targetName)
                : this(
                    questGiver,
                    rewardGold,
                    targetName,
                    CampaignTime.DaysFromNow(GwpTuning.Bounty.DeadlineDays))
            {
            }

            internal BountyHunterQuest(
                Hero questGiver,
                int rewardGold,
                string targetName,
                CampaignTime dueTime)
                : base(
                    GwpIds.BountyQuestPrefix + MBRandom.RandomInt(1000, 9999),
                    questGiver,
                    dueTime,
                    rewardGold)
            {
                _targetName = targetName ?? GwpText.Get("{=gwp_playerbountybehavior_questandnotification_001}Unknown target");
                _readyForTurnInLogWritten = false;
            }

            /// <summary>
            /// 无参构造器供存档系统反序列化时调用。
            /// </summary>
            internal BountyHunterQuest()
                : base(GwpIds.BountyQuestFallbackId, null, CampaignTime.Never, 0)
            {
                _targetName = "";
                _readyForTurnInLogWritten = false;
            }

            public override TextObject Title =>
                new TextObject(GwpText.Get("{=gwp_playerbountybehavior_questandnotification_002}Grey Warden bounty: {VAR_1}", "VAR_1", _targetName ?? GwpText.Get("{=gwp_common_unknown_target}Unknown target")));
            public override bool IsRemainingTimeHidden => _readyForTurnInLogWritten;

            /// <summary>
            /// 非空 SpecialQuestType 让没有 IssueBase 的独立任务按原版特殊任务恢复。
            /// </summary>
            public override string SpecialQuestType => GwpIds.BountySpecialQuestType;

            protected override void SetDialogs() { }

            protected override void InitializeQuestOnGameLoad() { }

            internal void WriteLog(string text)
            {
                WriteLog(new TextObject(text));
            }

            internal void WriteLog(TextObject text)
            {
                try { AddLog(text, false); } catch { }
            }

            internal void SucceedQuest()
            {
                try
                {
                    AddLog(new TextObject(GwpText.Get("{=gwp_playerbountybehavior_questandnotification_003}You defeated the bounty target and successfully claimed the bounty.")), false);
                    CompleteQuestWithSuccess();
                }
                catch { }
            }

            internal void MarkReadyForTurnIn()
            {
                try { ChangeQuestDueTime(CampaignTime.Never); } catch { }
                if (_readyForTurnInLogWritten) return;

                _readyForTurnInLogWritten = true;
                WriteLog(GwpText.Get(
                    "{=gwp_bounty_ready_for_turnin}The quarry has been defeated. Report to any Grey Warden lord to receive the bounty. If the warrant remains unsettled for five days, a Warden settlement party will come to you."));
            }

            internal void TimeOutQuest()
            {
                try
                {
                    CompleteQuestWithTimeOut(new TextObject(GwpText.Get(
                        "{=gwp_bounty_contract_timed_out_log}The bounty contract expired before the quarry was defeated.")));
                }
                catch { }
            }

            protected override void OnTimedOut()
            {
                try
                {
                    Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
                        ?.OnBountyQuestTimedOut(this);
                }
                catch { }
            }

            internal void FailQuestMembershipEnded()
            {
                try { CompleteQuestWithCancel(new TextObject(GwpText.Get("{=gwp_bounty_membership_ended}You left the Grey Wardens, and the active bounty contract was withdrawn."))); } catch { }
            }

        }

        #endregion

        #region 右侧通知数据层（InformationData）

        /// <summary>
        /// ★ internal（非 private）：存档系统需通过反射访问。
        /// ★ 无参构造器：存档系统重建对象时调用。
        /// </summary>
        internal sealed class BountyMapNotification : InformationData
        {
            internal BountyMapNotification()
                : base(new TextObject(GwpText.Get(
                    "{=gwp_bounty_notification_description}New bounty contracts are available."))) { }

            public override TextObject TitleText =>
                new TextObject(GwpText.Get(
                    "{=gwp_bounty_notification_title}Grey Warden bounty contracts"));
            public override string SoundEventPath => "event:/ui/notification/quest_start";

            public override bool IsValid()
            {
                PlayerBountyBehavior? behavior = Campaign.Current
                    ?.GetCampaignBehavior<PlayerBountyBehavior>();
                return behavior?.CanInspectBountyOffers() == true;
            }
        }

        #endregion

        #region 右侧通知ViewModel层（MapNotificationItemBaseVM）

        /// <summary>★ internal（非 private）：与 BountyMapNotification 同理。</summary>
        internal sealed class BountyMapNotificationItemVM : MapNotificationItemBaseVM
        {
            public BountyMapNotificationItemVM(BountyMapNotification data) : base(data)
            {
                NotificationIdentifier = "armycreation";
                _onInspect = () =>
                {
                    ExecuteRemove();
                    var behavior = Campaign.Current
                        ?.GetCampaignBehavior<PlayerBountyBehavior>();
                    behavior?.ShowBountySelectionInquiry();
                };
            }
        }

        #endregion
    }
}
