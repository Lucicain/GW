﻿using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 玩家行为监控 - 监听玩家的犯罪和行善行为，记录到 PlayerBehaviorPool
    /// </summary>
    public class PlayerBehaviorMonitor : CampaignBehaviorBase
    {
        // 运行时（无需持久化）：记录战斗开始时敌方部队总人数，用于声望缩放计算
        private int _pendingEnemyCount = 0;

        public override void RegisterEvents()
        {
            // ── 新档初始化 ──────────────────────────────────────────────────────────
            // ★ 必须监听此事件以重置静态数据。
            //
            // 问题根因：PlayerBehaviorPool 和 CrimePool 均为 static 类，
            // 其字段（包括 Reputation）在进程生命周期内持续存在，不随游戏会话重置。
            // SyncData() 只在【加载/保存】已有存档时触发，【新建游戏】时不触发。
            //
            // 复现路径：
            //   1. 加载旧档（Reputation = 3）→ SyncData 恢复 Reputation = 3
            //   2. 退出到主菜单 → 新建游戏
            //   3. 新档的 SyncData 不触发 → Reputation 仍为 3
            //   4. OnHourlyTick 见 Reputation ≥ 1 → 立刻生成招募使者
            //
            // 修复：在此事件中手动调用 ClearAll()，保证新档从干净状态启动。
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

            // ── 犯罪 / 行善监听 ─────────────────────────────────────────────────────
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, OnVillageBeingRaided);
            CampaignEvents.ForceVolunteersCompletedEvent.AddNonSerializedListener(this, OnForceVolunteers);
            CampaignEvents.ForceSuppliesCompletedEvent.AddNonSerializedListener(this, OnForceSupplies);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        /// <summary>
        /// 新建游戏时调用：重置所有静态状态数据。
        ///
        /// 与 SyncData(IsLoading) 的区别：
        ///   SyncData 从存档文件读取并恢复数据（加载已有存档）。
        ///   本方法直接归零所有数据（开新档，无存档文件可读）。
        ///
        /// PlayerBehaviorPool.ClearAll() → Reputation = 0, Records 清空, VictimFactions 清空
        /// CrimePool.ClearAll()          → 待处理犯罪池清空, 活跃任务清空, IsAccepting = true
        /// </summary>
        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            PlayerBehaviorPool.ClearAll();
            CrimePool.ClearAll();
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                int reputation = PlayerBehaviorPool.Reputation;
                dataStore.SyncData("gwp_reputation", ref reputation);

                List<string> victimFactionIds = PlayerBehaviorPool.VictimFactions
                    .Where(f => f != null)
                    .Select(f => f.StringId)
                    .ToList();
                int victimCount = victimFactionIds.Count;
                dataStore.SyncData("gwp_victim_count", ref victimCount);
                for (int i = 0; i < victimFactionIds.Count; i++)
                {
                    string factionId = victimFactionIds[i];
                    dataStore.SyncData($"gwp_victim_{i}", ref factionId);
                }
            }
            else if (dataStore.IsLoading)
            {
                // 先清空（包括上次会话残留），再读档，防止跨存档联动 bug
                // 注意：CrimePool 已由 PoliceEnforcementBehavior.SyncData()（先执行）恢复，
                // 此处不再调用 CrimePool.ClearAll()，否则会销毁刚恢复的数据。
                PlayerBehaviorPool.ClearAll();

                int reputation = 0;
                dataStore.SyncData("gwp_reputation", ref reputation);
                PlayerBehaviorPool.ResetReputation(reputation);

                int victimCount = 0;
                dataStore.SyncData("gwp_victim_count", ref victimCount);
                for (int i = 0; i < victimCount; i++)
                {
                    string factionId = "";
                    dataStore.SyncData($"gwp_victim_{i}", ref factionId);
                    if (!string.IsNullOrEmpty(factionId))
                    {
                        IFaction faction = Kingdom.All.FirstOrDefault(k => k.StringId == factionId);
                        if (faction != null)
                            PlayerBehaviorPool.AddVictimFactionOnLoad(faction);
                    }
                }
            }
        }

        #region 犯罪监听

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null || attackerParty == null || defenderParty == null) return;

            MobileParty attacker = attackerParty.MobileParty;
            MobileParty defender = defenderParty.MobileParty;

            // ── 记录敌方战前人数（必须在 null 检查之前执行！）────────────────────────
            // 原因：劫掠事件的 defenderParty 是 Settlement（MobileParty=null），
            // 若先做 null 检查会提前 return，导致 _pendingEnemyCount 永远为 0。
            // 但 MapEvent.DefenderSide.Parties 在战斗开始时已含民兵移动部队，此处可正确计数。
            bool playerIsAttacker = attacker?.IsMainParty == true;
            bool playerIsDefender = defender?.IsMainParty == true;
            if (playerIsAttacker || playerIsDefender)
            {
                MapEventSide enemySideForCount = playerIsAttacker ? mapEvent.DefenderSide : mapEvent.AttackerSide;
                _pendingEnemyCount = 0;
                foreach (var p in enemySideForCount.Parties)
                    if (p.Party?.IsMobile == true)
                        _pendingEnemyCount += p.Party.NumberOfAllMembers;
            }

            // 以下犯罪检测要求双方均为移动部队（Settlement 类型直接跳过，由其他事件处理）
            if (attacker == null || defender == null) return;

            // ── 犯罪检测（仅在玩家为攻击者时执行）──────────────────────────────────
            if (!attacker.IsMainParty) return;

            Vec2 location = mapEvent.Position.ToVec2();

            if (defender.PartyComponent is VillagerPartyComponent)
            {
                string victimName = defender.Name?.ToString() ?? "村民";
                IFaction victimFaction = defender.ActualClan?.MapFaction;
                // 仅记录犯罪事件，不扣声望：声望按击败人数在 OnMapEventEnded 中缩放扣除
                PlayerBehaviorPool.AddCrimeRecord("攻击村民", location, victimName, victimFaction);
                return;
            }

            if (defender.PartyComponent is CaravanPartyComponent)
            {
                string victimName = defender.Name?.ToString() ?? "商队";
                IFaction victimFaction = defender.ActualClan?.MapFaction;
                // 仅记录犯罪事件，不扣声望：声望按击败人数在 OnMapEventEnded 中缩放扣除
                PlayerBehaviorPool.AddCrimeRecord("攻击商队", location, victimName, victimFaction);
            }
        }

        private void OnVillageBeingRaided(Village village)
        {
            if (village == null) return;

            MobileParty raider = FindPlayerRaidingParty(village);
            if (raider == null || !raider.IsMainParty) return;

            Vec2 location = village.Settlement.Position.ToVec2();
            IFaction victimFaction = village.Settlement.MapFaction;

            // ★ 功能 4A：劫掠村庄固定扣 -2 声望（无论村庄有无防守者）
            PlayerBehaviorPool.ChangeReputation(-2);

            // 若已进入通缉状态，立即触发警察追捕
            if (PlayerBehaviorPool.IsWanted)
                CrimePool.TryAddPlayerCrime("劫掠村庄", location, village.Name?.ToString() ?? "未知村庄");

            InformationManager.DisplayMessage(new InformationMessage(
                $"灰袍守卫记录了你的恶行：劫掠村庄 {village.Name} | " +
                $"{PlayerBehaviorPool.GetReputationDisplay()} (-2)",
                Colors.Red));

            // 记录犯罪历史（供受害势力追踪等后续逻辑使用）
            PlayerBehaviorPool.AddCrimeRecord("劫掠村庄", location, village.Name?.ToString() ?? "未知村庄", victimFaction);
        }

        private void OnForceVolunteers(BattleSideEnum side, ForceVolunteersEventComponent component)
        {
            if (side != BattleSideEnum.Attacker) return;

            var mapEvent = component?.MapEvent;
            var settlement = mapEvent?.MapEventSettlement;
            if (mapEvent == null || settlement == null) return;

            bool playerInvolved = false;
            foreach (var p in mapEvent.AttackerSide.Parties)
            {
                if (p.Party?.IsMobile == true && p.Party.MobileParty.IsMainParty)
                { playerInvolved = true; break; }
            }
            if (!playerInvolved) return;

            PlayerBehaviorPool.AddCrime("强迫募兵", settlement.Position.ToVec2(),
                settlement.Name?.ToString() ?? "未知村庄", settlement.MapFaction);
        }

        private void OnForceSupplies(BattleSideEnum side, ForceSuppliesEventComponent component)
        {
            if (side != BattleSideEnum.Attacker) return;

            var mapEvent = component?.MapEvent;
            var settlement = mapEvent?.MapEventSettlement;
            if (mapEvent == null || settlement == null) return;

            bool playerInvolved = false;
            foreach (var p in mapEvent.AttackerSide.Parties)
            {
                if (p.Party?.IsMobile == true && p.Party.MobileParty.IsMainParty)
                { playerInvolved = true; break; }
            }
            if (!playerInvolved) return;

            PlayerBehaviorPool.AddCrime("强征给养", settlement.Position.ToVec2(),
                settlement.Name?.ToString() ?? "未知村庄", settlement.MapFaction);
        }

        #endregion

        #region 行善监听

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || !mapEvent.HasWinner) return;

            bool playerWon = false;
            foreach (var p in mapEvent.Winner.Parties)
            {
                if (p.Party?.IsMobile == true && p.Party.MobileParty.IsMainParty)
                { playerWon = true; break; }
            }
            if (!playerWon) return;

            bool enemyIsBandit = false;
            bool defenderIsVillager = false;
            bool defenderIsCaravan = false;

            MapEventSide loser = mapEvent.Winner == mapEvent.AttackerSide
                ? mapEvent.DefenderSide : mapEvent.AttackerSide;

            foreach (var p in loser.Parties)
            {
                if (p.Party?.IsMobile != true) continue;
                if (p.Party.MobileParty.ActualClan?.IsBanditFaction == true)
                    enemyIsBandit = true;
            }

            foreach (var p in mapEvent.Winner.Parties)
            {
                if (p.Party?.IsMobile != true) continue;
                var mp = p.Party.MobileParty;
                if (mp.PartyComponent is VillagerPartyComponent) defenderIsVillager = true;
                if (mp.PartyComponent is CaravanPartyComponent) defenderIsCaravan = true;
            }

            // ── 声望缩放：按敌方战前人数向上取整（1~10人=+1，11~20人=+2……）────────
            // 额外场景：玩家以防守方身份击退非法劫掠村庄的部队（非盗贼势力的lord也算）
            // 此时防守方=定居点（非移动部队），enemyIsBandit/defenderIsVillager/defenderIsCaravan 均为 false，
            // 需单独检查：IsRaid && 胜者为防守方
            bool defenderIsVillageRaid = mapEvent.IsRaid && mapEvent.Winner == mapEvent.DefenderSide;

            bool anyGoodDeed = enemyIsBandit || defenderIsVillager || defenderIsCaravan || defenderIsVillageRaid;
            if (anyGoodDeed)
            {
                int enemyCount = Math.Max(1, _pendingEnemyCount); // 无法记录时保底+1
                int repGain    = (enemyCount + 9) / 10;           // 向上取整

                PlayerBehaviorPool.ChangeReputation(repGain);

                string deedType = defenderIsVillageRaid ? "保护村庄免遭劫掠"
                                : defenderIsCaravan     ? "解救商队"
                                : defenderIsVillager    ? "解救村民"
                                : "击败劫匪";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"灰袍守卫注意到你的善行：{deedType}（击败 {enemyCount} 人）| " +
                    $"{PlayerBehaviorPool.GetReputationDisplay()} (+{repGain})",
                    Colors.Green));
            }

            // ── 声望扣除：按失败方战前人数向上取整（1~10人=-1，无人死伤=0）────────
            // 适用场景：玩家作为攻击方击败村民/商队，或成功劫掠村庄
            // 注：loserIsVillager/loserIsCaravan 与上方 defenderIsVillager/defenderIsCaravan 互斥
            //     （村民/商队在 loser 侧 = 玩家攻打他们；在 winner 侧 = 玩家保护他们）
            bool loserIsVillager = false;
            bool loserIsCaravan  = false;
            foreach (var p in loser.Parties)
            {
                if (p.Party?.IsMobile != true) continue;
                var mp2 = p.Party.MobileParty;
                if (mp2.PartyComponent is VillagerPartyComponent) loserIsVillager = true;
                if (mp2.PartyComponent is CaravanPartyComponent)  loserIsCaravan  = true;
            }
            // 玩家作为攻击方赢得村庄劫掠（包括强制介入后帮助劫掠方获胜的情况）
            bool playerRaidedVillage = mapEvent.IsRaid && mapEvent.Winner == mapEvent.AttackerSide;

            bool anyBadDeed = loserIsVillager || loserIsCaravan || playerRaidedVillage;
            if (anyBadDeed)
            {
                // 不设下限：无人死伤（如无人防守的村庄劫掠）时不扣声望
                int killCount = _pendingEnemyCount;

                // ★ 功能 4B：村庄劫掠事件中 _pendingEnemyCount 始终为 0
                // （MapEventStarted 的 defenderParty 是 Settlement 非 MobileParty → null → 提前 return，未记录人数）
                // 兜底：直接从失败方的移动部队中计数（民兵/驻军等），确保"村民抵抗"战斗按每10人扣1分
                if (playerRaidedVillage && killCount == 0)
                {
                    foreach (var p in loser.Parties)
                        if (p.Party?.IsMobile == true)
                            killCount += p.Party.NumberOfAllMembers;
                }

                int repLoss   = (killCount + 9) / 10; // 向上取整；killCount=0 → repLoss=0
                if (repLoss > 0)
                {
                    PlayerBehaviorPool.ChangeReputation(-repLoss);

                    // 若已进入通缉状态，触发警察追捕
                    if (PlayerBehaviorPool.IsWanted)
                    {
                        string crimeType = playerRaidedVillage ? "劫掠村庄"
                                         : loserIsCaravan      ? "劫掠商队"
                                         : "杀害村民";
                        CrimePool.TryAddPlayerCrime(crimeType, mapEvent.Position.ToVec2(), crimeType);
                    }

                    string badDeedType = playerRaidedVillage ? "劫掠村庄"
                                       : loserIsCaravan      ? "劫掠商队"
                                       : "杀害村民";
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"灰袍守卫记录了你的恶行：{badDeedType}（击败 {killCount} 人）| " +
                        $"{PlayerBehaviorPool.GetReputationDisplay()} (-{repLoss})",
                        Colors.Red));
                }
            }

            _pendingEnemyCount = 0;
        }

        #endregion

        #region 强制介入 GameMenu 选项

        /// <summary>
        /// 注册"强制介入"选项到 join_encounter 菜单。
        ///
        /// join_encounter 是玩家点击地图上正在进行中的战斗/劫掠事件时弹出的菜单，
        /// 涵盖三种场景：NPC攻击商队、NPC攻击村民、NPC劫掠村庄。
        ///
        /// 原版菜单已有"帮助防守方"选项，但条件因未宣战而隐藏该按钮。
        /// 本选项绕过此限制：先对攻击方宣战，再以防守方身份加入战斗。
        /// </summary>
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // 强制介入（保护无辜方）：对攻击方宣战，以防守方身份加入战斗
            starter.AddGameMenuOption(
                "join_encounter",
                "gwp_force_join_battle",
                "{GWP_FORCE_JOIN_TEXT}",
                ForceJoinBattleCondition,
                ForceJoinBattleConsequence,
                isLeave: false,
                index: -1);

            // 落井下石（帮助攻击方）：对防守方宣战，以攻击方身份加入战斗，可获收益但扣声望
            starter.AddGameMenuOption(
                "join_encounter",
                "gwp_force_join_attackers",
                "{GWP_FORCE_JOIN_ATTACKERS_TEXT}",
                ForceJoinAttackersCondition,
                ForceJoinAttackersConsequence,
                isLeave: false,
                index: -1);
        }

        /// <summary>
        /// 条件：防守方为商队/村民/村庄，且玩家与攻击方未宣战时，显示"强制介入"选项。
        /// </summary>
        private bool ForceJoinBattleCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;

                MapEvent battle = PlayerEncounter.EncounteredBattle;
                if (battle == null) return false;

                // 获取攻击方领袖及其派系
                PartyBase attackerLeader = battle.GetLeaderParty(BattleSideEnum.Attacker);
                if (attackerLeader == null) return false;

                IFaction playerFaction   = Hero.MainHero?.MapFaction;
                IFaction attackerFaction = attackerLeader.MapFaction;
                if (playerFaction == null || attackerFaction == null) return false;

                // 已宣战 → 不显示（原版"帮助防守方"选项此时已可用）
                if (FactionManager.IsAtWarAgainstFaction(playerFaction, attackerFaction)) return false;

                // 判断场景：防守方是商队、村民，或本场是村庄劫掠
                bool defenderIsCaravan  = battle.DefenderSide.Parties
                    .Any(p => p.Party?.IsMobile == true && p.Party.MobileParty.IsCaravan);
                bool defenderIsVillager = battle.DefenderSide.Parties
                    .Any(p => p.Party?.IsMobile == true && p.Party.MobileParty.IsVillager);
                bool isVillageRaid      = battle.IsRaid
                    && battle.GetLeaderParty(BattleSideEnum.Defender)?.IsSettlement == true;

                if (!defenderIsCaravan && !defenderIsVillager && !isVillageRaid) return false;

                // 设置按钮文本
                string targetDesc = isVillageRaid      ? "村庄"
                                  : defenderIsCaravan  ? "商队"
                                  : "村民";
                MBTextManager.SetTextVariable("GWP_FORCE_JOIN_TEXT",
                    new TextObject($"强制介入！对 {attackerFaction.Name} 宣战，保护{targetDesc}。"));
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 对攻击方势力宣战，记录敌方人数供声望缩放，以防守方身份加入战斗并切换到战斗菜单。
        /// </summary>
        private void ForceJoinBattleConsequence(MenuCallbackArgs args)
        {
            try
            {
                MapEvent battle = PlayerEncounter.EncounteredBattle;
                if (battle == null) return;

                PartyBase attackerLeader = battle.GetLeaderParty(BattleSideEnum.Attacker);
                IFaction playerFaction   = Hero.MainHero?.MapFaction;
                IFaction attackerFaction = attackerLeader?.MapFaction;

                // 1. 宣战
                if (playerFaction != null && attackerFaction != null
                    && !FactionManager.IsAtWarAgainstFaction(playerFaction, attackerFaction))
                {
                    FactionManager.DeclareWar(playerFaction, attackerFaction);
                }

                // 2. 记录攻击方战前人数（JoinBattle 后不再触发 OnMapEventStarted，需在此预先记录）
                _pendingEnemyCount = 0;
                foreach (var p in battle.AttackerSide.Parties)
                    if (p.Party?.IsMobile == true)
                        _pendingEnemyCount += p.Party.NumberOfAllMembers;

                // 3. 以防守方身份加入战斗 → 切换到遭遇战菜单（进入战斗准备界面）
                PlayerEncounter.JoinBattle(BattleSideEnum.Defender);
                GameMenu.ActivateGameMenu("encounter");
            }
            catch { }
        }

        /// <summary>
        /// 条件：防守方为商队/村民/村庄，且玩家与防守方未宣战时，显示"落井下石"选项。
        /// 点击后将对防守方势力宣战，并以攻击方身份加入战斗。
        /// </summary>
        private bool ForceJoinAttackersCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;

                MapEvent battle = PlayerEncounter.EncounteredBattle;
                if (battle == null) return false;

                // 获取防守方领袖及其派系（无辜方）
                PartyBase defenderLeader = battle.GetLeaderParty(BattleSideEnum.Defender);
                if (defenderLeader == null) return false;

                IFaction playerFaction   = Hero.MainHero?.MapFaction;
                IFaction defenderFaction = defenderLeader.MapFaction;
                if (playerFaction == null || defenderFaction == null) return false;

                // 已对防守方宣战 → 不显示（原版"帮助攻击方"选项此时已可用）
                if (FactionManager.IsAtWarAgainstFaction(playerFaction, defenderFaction)) return false;

                // 判断场景：防守方是商队、村民，或本场是村庄劫掠
                bool defenderIsCaravan  = battle.DefenderSide.Parties
                    .Any(p => p.Party?.IsMobile == true && p.Party.MobileParty.IsCaravan);
                bool defenderIsVillager = battle.DefenderSide.Parties
                    .Any(p => p.Party?.IsMobile == true && p.Party.MobileParty.IsVillager);
                bool isVillageRaid      = battle.IsRaid
                    && battle.GetLeaderParty(BattleSideEnum.Defender)?.IsSettlement == true;

                if (!defenderIsCaravan && !defenderIsVillager && !isVillageRaid) return false;

                // 设置按钮文本
                string targetDesc = isVillageRaid      ? "参与劫掠村庄"
                                  : defenderIsCaravan  ? "劫掠商队"
                                  : "攻击村民";
                MBTextManager.SetTextVariable("GWP_FORCE_JOIN_ATTACKERS_TEXT",
                    new TextObject($"落井下石！对 {defenderFaction.Name} 宣战，{targetDesc}。"));
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 对防守方势力宣战，记录防守方人数供战后声望扣除缩放，以攻击方身份加入战斗。
        /// </summary>
        private void ForceJoinAttackersConsequence(MenuCallbackArgs args)
        {
            try
            {
                MapEvent battle = PlayerEncounter.EncounteredBattle;
                if (battle == null) return;

                PartyBase defenderLeader = battle.GetLeaderParty(BattleSideEnum.Defender);
                IFaction playerFaction   = Hero.MainHero?.MapFaction;
                IFaction defenderFaction = defenderLeader?.MapFaction;

                // 1. 宣战（对防守方所属势力）
                if (playerFaction != null && defenderFaction != null
                    && !FactionManager.IsAtWarAgainstFaction(playerFaction, defenderFaction))
                {
                    FactionManager.DeclareWar(playerFaction, defenderFaction);
                }

                // 2. 记录防守方战前人数（供战后按人数缩放扣声望；JoinBattle 不触发 OnMapEventStarted）
                _pendingEnemyCount = 0;
                foreach (var p in battle.DefenderSide.Parties)
                    if (p.Party?.IsMobile == true)
                        _pendingEnemyCount += p.Party.NumberOfAllMembers;

                // 3. 以攻击方身份加入战斗 → 切换到遭遇战菜单（进入战斗准备界面）
                PlayerEncounter.JoinBattle(BattleSideEnum.Attacker);
                GameMenu.ActivateGameMenu("encounter");
            }
            catch { }
        }

        #endregion

        private MobileParty FindPlayerRaidingParty(Village village)
        {
            Settlement target = village?.Settlement;
            if (target == null) return null;

            foreach (MobileParty party in MobileParty.All)
            {
                if (party.IsMainParty && party.TargetSettlement == target)
                    return party;
            }
            return null;
        }
    }
}
