﻿using System;
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
        /// ★ SyncData 必须 override：保存 _targetName，否则读档后标题为空。
        /// ★ InitializeQuestOnGameLoad 读档时自动 Fail：任务是当局会话的显示层，
        ///   实际悬赏状态由 PlayerBountyBehavior.SyncData 持久化。
        /// </summary>
        internal sealed class BountyHunterQuest : QuestBase
        {
            // [SaveableField] 让 Bannerlord 存档系统在序列化/反序列化时自动保存此字段。
            // 不加此标注则读档后 _targetName = null，任务标题变为"灰袍悬赏：未知目标"。
            // ID=1 在本类内唯一；基类 QuestBase 使用 100~107，无冲突。
            [SaveableField(1)]
            private string _targetName;

            /// <summary>
            /// 正常构造器：接受悬赏任务时调用。
            /// questGiver 须为警察领主；rewardGold 用于任务日志显示。
            /// </summary>
            public BountyHunterQuest(Hero questGiver, int rewardGold, string targetName)
                : base(
                    "gwp_bounty_quest_" + MBRandom.RandomInt(1000, 9999),
                    questGiver,
                    CampaignTime.DaysFromNow(45),
                    rewardGold)
            {
                _targetName = targetName ?? "未知目标";
            }

            /// <summary>
            /// 无参构造器：供存档系统反序列化时调用（安全兜底）。
            ///
            /// Bannerlord 通常通过 FormatterServices.GetUninitializedObject 创建实例
            /// （完全绕过构造器），但部分版本或自定义序列化器可能会调用无参构造器。
            /// 此构造器使用安全的哑值（id="gwp_bounty_quest_0", questGiver=null 等），
            /// 实际字段在 InitializeQuestOnGameLoad 中会立即被 Fail 处理，无需正确值。
            /// </summary>
            internal BountyHunterQuest()
                : base("gwp_bounty_quest_0", null, CampaignTime.Never, 0)
            {
                _targetName = "";
            }

            public override TextObject Title =>
                new TextObject($"灰袍悬赏：{_targetName ?? "未知目标"}");
            public override bool IsRemainingTimeHidden => false;

            /// <summary>
            /// ★ 关键：必须返回非空字符串，才能让本 Quest 通过 QuestManager.OnGameLoaded() 的检查。
            ///
            /// Bannerlord 在每次读档时，对 QuestManager 中的每个 Quest 执行：
            ///   if (有关联 IssueBase || IsSpecialQuest)
            ///       InitializeQuestOnGameLoad(); // 正常恢复
            ///   else
            ///       CompleteQuestWithCancel();   // 直接取消！进"旧任务"
            ///
            /// 本 Quest 没有关联 IssueBase，因此必须通过 IsSpecialQuest 告知引擎
            /// "这是一个独立的特殊任务，不需要 IssueBase 也应当正常恢复"。
            /// IsSpecialQuest 的实现就是 string.IsNullOrEmpty(SpecialQuestType) == false。
            /// </summary>
            public override string SpecialQuestType => "GwpBountyHunterQuest";

            protected override void SetDialogs() { }

            protected override void InitializeQuestOnGameLoad()
            {
                // ★ 由 QuestManager.OnGameLoaded() 调用（因 SpecialQuestType 非空，引擎不会取消本 Quest）。
                // 通过 behavior 回调重连运行时引用。若 behavior.SyncData() 已先执行，
                // OnQuestLoadedFromSave 中 hasBountyTask=true → 直接重连 _activeQuest。
                // 若 SyncData 尚未执行 → 早返回 → 首次 OnHourlyTick 兜底从 QM 查找重连。
                try
                {
                    var b = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
                    b?.OnQuestLoadedFromSave(this);
                }
                catch { }
            }

            internal void WriteLog(string text)
            {
                try { AddLog(new TextObject(text), false); } catch { }
            }

            internal void SucceedQuest()
            {
                try
                {
                    AddLog(new TextObject("你击败了悬赏目标并成功领取了赏金。"), false);
                    CompleteQuestWithSuccess();
                }
                catch { }
            }

            internal void FailQuestTargetGone()
            {
                try { CompleteQuestWithFail(new TextObject("目标已失踪，悬赏任务取消。")); } catch { }
            }

        }

        #endregion

        #region 右侧通知数据层（InformationData）

        /// <summary>
        /// ★ internal（非 private）：存档系统需通过反射访问。
        /// ★ 不存储 CrimeRecord/PlayerBountyBehavior 引用：这两者不可序列化。
        ///   只存 offender 的 StringId 和显示名，均为可序列化的 string。
        /// ★ 无参构造器：存档系统重建对象时调用。
        /// </summary>
        internal sealed class BountyMapNotification : InformationData
        {
            internal string OffenderStringId { get; private set; }
            private string _offenderName;

            // ★ 存档系统重建时需要无参构造器
            internal BountyMapNotification() : base(new TextObject("")) { }

            internal BountyMapNotification(CrimeRecord crime)
                : base(new TextObject($"追缉目标：{crime?.Offender?.Name}"))
            {
                OffenderStringId = crime?.Offender?.StringId;
                _offenderName    = crime?.Offender?.Name?.ToString() ?? "未知目标";
            }

            public override TextObject TitleText =>
                new TextObject($"灰袍悬赏：{_offenderName ?? "未知目标"}");
            public override string SoundEventPath => "event:/ui/notification/quest_start";

            public override bool IsValid()
            {
                if (OffenderStringId == null) return false;
                return MobileParty.All.Any(p => p.StringId == OffenderStringId && p.IsActive);
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
                string offenderId = data.OffenderStringId;
                _onInspect = () =>
                {
                    ExecuteRemove();
                    // 通过 StringId 从 CrimePool 查找 CrimeRecord
                    var behavior = Campaign.Current
                        ?.GetCampaignBehavior<PlayerBountyBehavior>();
                    if (behavior == null) return;
                    CrimeRecord crime = CrimePool.GetByOffenderId(offenderId);
                    if (crime != null)
                        behavior.ShowBountyInquiry(crime);
                    else
                        InformationManager.DisplayMessage(new InformationMessage(
                            "该悬赏目标已失效", Colors.Yellow));
                };
            }
        }

        #endregion
    }
}
