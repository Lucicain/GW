using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 增强版犯罪监控 - 在原有基础上添加事件通知
    /// </summary>
    public class PoliceCrimeMonitorEnhanced : CampaignBehaviorBase
    {
        // 犯罪通知事件 - 供惩戒系统订阅
        public static event Action<string, MobileParty, Vec2, string>? OnCrimeDetected;

        public override void RegisterEvents()
        {
            // 攻击村民/商队
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);

            // 村庄劫掠
            CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, OnVillageBeingRaided);
        }

        public override void SyncData(IDataStore dataStore) { }

        /// <summary>
        /// 攻击村民/商队
        /// </summary>
        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null || attackerParty == null || defenderParty == null)
                return;

            MobileParty attacker = attackerParty.MobileParty;
            MobileParty defender = defenderParty.MobileParty;

            if (attacker == null || defender == null)
                return;

            // 警察全忙时静默

            // 巡逻队只算临时执勤单位，不进犯罪池，也不挂犯罪标记。
            if (GwpCommon.ShouldIgnoreCrimeTracking(attacker))
                return;

            // 过滤野怪
            if (attacker.ActualClan != null)
            {
                Clan attackerClan = attacker.ActualClan;
                if (attackerClan.IsOutlaw && attackerClan.IsBanditFaction)
                    return;
            }

            // 获取位置（Vec2用于距离计算）
            Vec2 location = mapEvent.Position.ToVec2();

            // 攻击村民
            if (defender.PartyComponent is VillagerPartyComponent)
            {
                string victimName = defender.Name?.ToString() ?? GwpText.Get("{=gwp_policecrimemonitorenhanced_001}Villager");

                // 显示消息
                Report(GwpText.Get("{=gwp_policecrimemonitorenhanced_002}Attack villager"), attacker, victimName, location);

                // 触发事件通知惩戒系统
                OnCrimeDetected?.Invoke(GwpText.Get("{=gwp_policecrimemonitorenhanced_003}Attack villager"), attacker, location, victimName);
                return;
            }

            // 攻击商队
            if (defender.PartyComponent is CaravanPartyComponent)
            {
                string victimName = defender.Name?.ToString() ?? GwpText.Get("{=gwp_policecrimemonitorenhanced_004}Caravan");

                Report(GwpText.Get("{=gwp_policecrimemonitorenhanced_005}Attack caravan"), attacker, victimName, location);

                OnCrimeDetected?.Invoke(GwpText.Get("{=gwp_policecrimemonitorenhanced_006}Attack caravan"), attacker, location, victimName);
                return;
            }
        }

        /// <summary>
        /// 劫掠村庄 - 开始
        /// </summary>
        private void OnVillageBeingRaided(Village village)
        {
            if (village == null) return;

            Vec2 location = village.Settlement.Position.ToVec2();
            MobileParty offender = FindRaidingParty(village);

            if (offender == null) return;
            if (GwpCommon.ShouldIgnoreCrimeTracking(offender)) return;

            // 过滤野怪
            if (offender.ActualClan != null)
            {
                Clan offenderClan = offender.ActualClan;
                if (offenderClan.IsOutlaw && offenderClan.IsBanditFaction)
                    return;
            }

            string victimName = GwpText.Get("{=gwp_policecrimemonitorenhanced_007}{VAR_1} Villager", "VAR_1", village.Name);

            Report(GwpText.Get("{=gwp_policecrimemonitorenhanced_008}Raid village (start)"), offender, victimName, location, GwpText.Get("{=gwp_policecrimemonitorenhanced_009}Village={VAR_1}", "VAR_1", village.Name));

            OnCrimeDetected?.Invoke(GwpText.Get("{=gwp_policecrimemonitorenhanced_010}Raid Village"), offender, location, victimName);
        }

        /// <summary>
        /// 显示犯罪消息
        /// </summary>
        private void Report(string type, MobileParty offender, string victimName, Vec2 location, string? extra = null)
        {
            string offenderName = offender?.Name?.ToString() ?? "Unknown";
            string extraInfo = string.IsNullOrEmpty(extra) ? "" : $" | {extra}";

            // 犯罪检测内部日志（开发调试，正式版不显示）
            // InformationManager.DisplayMessage(new InformationMessage(
            //     $"[GWP Police] {type} | 犯罪者={offenderName} | 受害者={victimName} | 地点={location}{extraInfo}",
            //     Colors.Red
            // ));
        }

        /// <summary>
        /// 找到正在劫掠村庄的队伍
        /// </summary>
        private MobileParty? FindRaidingParty(Village village)
        {
            Settlement? target = village?.Settlement;
            if (target == null) return null;

            foreach (MobileParty p in MobileParty.All)
            {
                if (p == null || !p.IsActive || p.IsMainParty) continue;

                if (p.TargetSettlement != target) continue;

                if (p.DefaultBehavior == AiBehavior.RaidSettlement ||
                    p.ShortTermBehavior == AiBehavior.RaidSettlement)
                {
                    return p;
                }
            }

            return null;
        }
    }
}
