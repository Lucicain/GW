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
            /// 正常构造器：接受悬赏任务时调用。questGiver 须为警察领主。
            /// 任务不带固定报酬：办案费按罚金封顶，交差时才算（见 player-commission.md）。
            /// </summary>
            public BountyHunterQuest(Hero questGiver, string targetName)
                : this(
                    questGiver,
                    targetName,
                    CampaignTime.DaysFromNow(GwpTuning.Bounty.DeadlineDays))
            {
            }

            internal BountyHunterQuest(
                Hero questGiver,
                string targetName,
                CampaignTime dueTime)
                : base(
                    GwpIds.BountyQuestPrefix + MBRandom.RandomInt(1000, 9999),
                    questGiver,
                    dueTime,
                    0)
            {
                _targetName = targetName ?? GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_questandnotification_001);
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
                new TextObject(GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_questandnotification_002, "VAR_1", _targetName ?? GwpText.Get(GwpTextCatalog.gwp_common_unknown_target)));
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
                AddLog(text, false);
            }

            internal void SucceedQuest()
            {
                AddLog(new TextObject(GwpText.Get(GwpTextCatalog.gwp_playerbountybehavior_questandnotification_003)), false);

                // A journal rendering problem must not prevent native completion.
                CompleteQuestWithSuccess();
            }

            internal void MarkReadyForTurnIn()
            {
                ChangeQuestDueTime(CampaignTime.Never);
                if (_readyForTurnInLogWritten) return;

                _readyForTurnInLogWritten = true;
                WriteLog(GwpText.Get(
                    GwpTextCatalog.gwp_bounty_ready_for_turnin));
            }

            internal void TimeOutQuest()
            {
                CompleteQuestWithTimeOut(new TextObject(GwpText.Get(
                    GwpTextCatalog.gwp_bounty_contract_timed_out_log)));
            }

            protected override void OnTimedOut()
            {
                Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
                    ?.OnBountyQuestTimedOut(this);
            }

            internal void FailQuestMembershipEnded()
            {
                CompleteQuestWithCancel(new TextObject(GwpText.Get(GwpTextCatalog.gwp_bounty_membership_ended)));
            }

            internal void CancelUnavailableCommission() =>
                CompleteQuestWithCancel(GwpText.Create(GwpTextCatalog.gwp_case_custody_withdrawn));

            internal void FailCitySearch(string message) => CompleteQuestWithFail(new TextObject(message));

        }

        #endregion

    }
}
