using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
        private const float AtonementIntelReportIntervalDays = 2f;
        private const float AtonementDeadlineDays = 45f;

        private int _atonementTargetSizeSnapshot = 0;
        private string _atonementTargetFactionId = string.Empty;
        private bool _atonementWaitingForTurnIn = false;
        private bool _awaitingAtonementQuestReconnect = false;
        private CampaignTime _lastAtonementIntelReportTime = CampaignTime.Zero;
        private AtonementQuest _atonementQuest = null!;

        internal sealed class AtonementQuest : QuestBase
        {
            [SaveableField(1)]
            private string _targetName;

            public AtonementQuest(Hero questGiver, string targetName, int repReward)
                : base(
                    "gwp_atonement_quest_" + MBRandom.RandomInt(1000, 9999),
                    questGiver,
                    CampaignTime.DaysFromNow(45f),
                    Math.Max(1, repReward))
            {
                _targetName = string.IsNullOrWhiteSpace(targetName) ? "未知目标" : targetName;
            }

            internal AtonementQuest()
                : base("gwp_atonement_quest_0", null, CampaignTime.Never, 0)
            {
                _targetName = "未知目标";
            }

            public override TextObject Title =>
                new TextObject($"灰袍赎罪令：追捕 {_targetName ?? "未知目标"}");

            public override bool IsRemainingTimeHidden => false;

            public override string SpecialQuestType => "GwpPlayerAtonementQuest";

            protected override void SetDialogs() { }

            protected override void InitializeQuestOnGameLoad()
            {
                try
                {
                    var behavior = Campaign.Current?.GetCampaignBehavior<PoliceEnforcementBehavior>();
                    behavior?.OnAtonementQuestLoadedFromSave(this);
                }
                catch { }
            }

            internal void WriteLog(string text)
            {
                try { AddLog(new TextObject(text), false); } catch { }
            }

            internal void MarkReadyForTurnIn()
            {
                WriteLog("目标已击败。前往族长或任意灰袍警察处交付赎罪任务。");
            }

            internal void SucceedQuestWithReputation(int gain, int currentReputation)
            {
                try
                {
                    WriteLog($"赎罪完成：信誉 +{gain}，当前信誉 {currentReputation}。");
                    CompleteQuestWithSuccess();
                }
                catch { }
            }

            internal void FailQuestWithReason(string reason)
            {
                try { CompleteQuestWithFail(new TextObject(reason)); } catch { }
            }
        }

        private Hero GetAtonementQuestGiver()
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return null;

            Hero leader = policeClan.Leader;
            if (leader != null && leader.IsActive && !leader.IsDead && !leader.IsChild && !leader.IsPrisoner)
                return leader;

            return policeClan.Heroes.FirstOrDefault(h =>
                h != null &&
                h.IsActive &&
                !h.IsDead &&
                !h.IsChild &&
                !h.IsPrisoner);
        }

        private void StartAtonementQuest()
        {
            if (_atonementQuest != null && _atonementQuest.IsOngoing) return;

            Hero questGiver = GetAtonementQuestGiver();
            if (questGiver == null) return;

            try
            {
                _atonementQuest = new AtonementQuest(questGiver, _atonementTargetName, _atonementReputationReward);
                _atonementQuest.StartQuest();
                if (_atonementWaitingForTurnIn)
                {
                    _atonementQuest.MarkReadyForTurnIn();
                }
                else
                {
                    string targetSettlement = "未知位置";
                    MobileParty target = MobileParty.All.FirstOrDefault(p =>
                        p.StringId == _atonementTargetPartyId && p.IsActive);
                    if (target != null)
                        targetSettlement = GetNearestSettlementName(target.GetPosition2D);

                    _atonementQuest.WriteLog(
                        $"任务下达：在 {AtonementDeadlineDays:0} 天内击败 {_atonementTargetName}（接案规模 {_atonementTargetSizeSnapshot} 人）。");
                    _atonementQuest.WriteLog(
                        $"探子初报：目标最后在 {targetSettlement} 附近活动。完成后向族长或任意灰袍警察交任务。");
                }
            }
            catch
            {
                _atonementQuest = null!;
            }
        }

        private static string GetNearestSettlementName(Vec2 position)
        {
            Settlement nearest = null;
            float nearestDist = float.MaxValue;
            foreach (Settlement s in Settlement.All)
            {
                if (s == null) continue;
                float dist = s.GetPosition2D.Distance(position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = s;
                }
            }
            return nearest?.Name?.ToString() ?? "未知位置";
        }

        private void AppendAtonementIntelLog(MobileParty target)
        {
            if (target == null || !target.IsActive) return;

            int currentSize = Math.Max(1, target.Party?.NumberOfAllMembers ?? 1);
            string nearestSettlement = GetNearestSettlementName(target.GetPosition2D);
            string intel = $"探子回报：{_atonementTargetName} 最近出现在 {nearestSettlement} 附近（约 {currentSize} 人）。";

            try { _atonementQuest?.WriteLog(intel); } catch { }
            InformationManager.DisplayMessage(new InformationMessage(intel, Colors.Cyan));
        }

        private void TryRestoreAtonementQuestOnSessionStart()
        {
            if (!_atonementActive && !_atonementWaitingForTurnIn) return;

            PlayerBehaviorPool.SetAtonementTaskActive(true);
            _awaitingAtonementQuestReconnect = true;
        }

        private void TryReconnectAtonementQuestOnHourlyTick()
        {
            if (!_awaitingAtonementQuestReconnect) return;
            _awaitingAtonementQuestReconnect = false;

            if (!_atonementActive && !_atonementWaitingForTurnIn)
                return;

            try
            {
                AtonementQuest existing = Campaign.Current?.QuestManager?.Quests
                    ?.OfType<AtonementQuest>()
                    ?.FirstOrDefault(q => q.IsOngoing);
                if (existing != null)
                {
                    _atonementQuest = existing;
                    if (_atonementWaitingForTurnIn)
                        existing.MarkReadyForTurnIn();
                    else
                        existing.WriteLog("读档恢复：继续追踪赎罪目标。");
                    return;
                }
            }
            catch { }

            StartAtonementQuest();
            if (_atonementQuest != null && _atonementQuest.IsOngoing)
            {
                if (_atonementWaitingForTurnIn)
                    _atonementQuest.MarkReadyForTurnIn();
                else
                    _atonementQuest.WriteLog("读档恢复：继续追踪赎罪目标。");
            }
        }

        internal void OnAtonementQuestLoadedFromSave(AtonementQuest quest)
        {
            if (quest == null || !quest.IsOngoing) return;
            if (!_atonementActive && !_atonementWaitingForTurnIn) return;

            _atonementQuest = quest;
            _awaitingAtonementQuestReconnect = false;
            if (_atonementWaitingForTurnIn)
                quest.MarkReadyForTurnIn();
            else
                quest.WriteLog("读档恢复：继续追踪赎罪目标。");
        }

        private bool EnforcementAtonementTurnInCondition()
        {
            if (!_atonementWaitingForTurnIn) return false;

            Hero conversationHero = Hero.OneToOneConversationHero;
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (conversationHero == null || policeClan == null) return false;
            if (conversationHero.Clan != policeClan) return false;
            if (!conversationHero.IsActive || conversationHero.IsDead || conversationHero.IsChild) return false;

            MBTextManager.SetTextVariable(
                "GWP_ENFORCEMENT_ATONEMENT_TURNIN_OPTION",
                $"关于赎罪任务（提交后恢复最多 {_atonementReputationReward} 点信誉）");
            MBTextManager.SetTextVariable(
                "GWP_ENFORCEMENT_ATONEMENT_TURNIN_TEXT",
                $"核验无误。你已完成赎罪任务，按案卷可恢复最多 {_atonementReputationReward} 点信誉。");
            return true;
        }

        private void OnEnforcementAtonementTurnInConsequence()
        {
            int before = PlayerBehaviorPool.Reputation;
            int after = Math.Min(0, before + Math.Max(1, _atonementReputationReward));
            int gain = after - before;
            PlayerBehaviorPool.ResetReputation(after);
            MakePeaceWithAtonementTargetFaction();

            try { _atonementQuest?.SucceedQuestWithReputation(gain, after); } catch { }

            InformationManager.DisplayMessage(new InformationMessage(
                $"赎罪任务已交付：信誉 +{gain}（当前 {after}）",
                Colors.Green));

            ClearAtonementTaskState();
        }

        private void MakePeaceWithAtonementTargetFaction()
        {
            if (string.IsNullOrEmpty(_atonementTargetFactionId)) return;

            IFaction playerFaction = Hero.MainHero?.MapFaction;
            if (playerFaction == null) return;

            IFaction targetFaction = Kingdom.All.FirstOrDefault(k => k.StringId == _atonementTargetFactionId)
                ?? (IFaction)Clan.All.FirstOrDefault(c => c.StringId == _atonementTargetFactionId);
            if (targetFaction == null || targetFaction == playerFaction) return;
            if (!FactionManager.IsAtWarAgainstFaction(playerFaction, targetFaction)) return;

            try
            {
                MakePeaceAction.Apply(playerFaction, targetFaction);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"赎罪交付后，灰袍调停：你与 {targetFaction.Name} 已恢复和平。",
                    Colors.Green));
            }
            catch
            {
                GwpCommon.TrySetNeutral(playerFaction, targetFaction);
            }
        }
    }
}
