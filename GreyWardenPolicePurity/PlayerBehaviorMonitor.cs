using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
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
        private static GwpRuntimeState.CrimeState CrimeState => GwpRuntimeState.Crime;
        private static GwpRuntimeState.PlayerState PlayerState => GwpRuntimeState.Player;

        // 运行时（无需持久化）：记录战斗开始时敌方部队总人数，用于声望缩放计算
        private int _pendingEnemyCount = 0;
        private int _pendingPoliceCrimeSupport = 0; // 1=帮助灰袍守卫，-1=帮助犯人，0=普通战斗
        private int _pendingPlayerKillBaseline = -1;
        private bool _pendingVillageDefenseEncounterText;
        private string _pendingVillageDefenseName = string.Empty;

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
            CampaignEvents.AfterGameMenuInitializedEvent.AddNonSerializedListener(this, OnAfterGameMenuInitialized);

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
            GwpRuntimeState.ResetForNewGame();
        }

        public override void SyncData(IDataStore dataStore)
        {
            GwpRuntimeState.SyncPlayerBehaviorData(dataStore);
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

                CapturePlayerKillBaseline();
            }

            // 以下犯罪检测要求双方均为移动部队（Settlement 类型直接跳过，由其他事件处理）
            if (attacker == null || defender == null) return;

            // ── 犯罪检测（仅在玩家为攻击者时执行）──────────────────────────────────
            if (!attacker.IsMainParty) return;

            Vec2 location = mapEvent.Position.ToVec2();

            if (defender.PartyComponent is VillagerPartyComponent)
            {
                string victimName = defender.Name?.ToString() ?? GwpText.Get("{=gwp_playerbehaviormonitor_001}Villager");
                IFaction victimFaction = defender.ActualClan?.MapFaction;
                // 仅记录犯罪事件，不扣声望：声望按击败人数在 OnMapEventEnded 中缩放扣除
                PlayerState.AddCrimeRecord(GwpText.Get("{=gwp_playerbehaviormonitor_002}Attack villager"), location, victimName, victimFaction);
                return;
            }

            if (defender.PartyComponent is CaravanPartyComponent)
            {
                string victimName = defender.Name?.ToString() ?? GwpText.Get("{=gwp_playerbehaviormonitor_003}Caravan");
                IFaction victimFaction = defender.ActualClan?.MapFaction;
                // 仅记录犯罪事件，不扣声望：声望按击败人数在 OnMapEventEnded 中缩放扣除
                PlayerState.AddCrimeRecord(GwpText.Get("{=gwp_playerbehaviormonitor_004}Attack caravan"), location, victimName, victimFaction);
            }
        }

        private void OnVillageBeingRaided(Village village)
        {
            if (village == null) return;

            MobileParty raider = FindPlayerRaidingParty(village);
            MobileParty mainParty = MobileParty.MainParty;
            float distance = mainParty == null
                ? -1f
                : mainParty.GetPosition2D.Distance(village.Settlement.Position.ToVec2());
            GwpAiDiagnostics.WritePlayerJusticeState(
                "VILLAGE_BEING_RAIDED_EVALUATED",
                "village=" + village.Settlement.StringId +
                "; distance=" + distance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                "; detectedRaider=" + (raider?.StringId ?? "-") +
                "; playerTargetMatches=" + (mainParty?.TargetSettlement == village.Settlement) +
                "; playerMapEventIsRaid=" + (mainParty?.MapEvent?.IsRaid == true) +
                "; playerMapEventSettlement=" + (mainParty?.MapEvent?.MapEventSettlement?.StringId ?? "-"));
            if (raider == null || !raider.IsMainParty) return;

            Vec2 location = village.Settlement.Position.ToVec2();
            IFaction victimFaction = village.Settlement.MapFaction;

            // ★ 功能 4A：劫掠村庄固定扣 -2 声望（无论村庄有无防守者）
            PlayerState.ChangeReputation(-2);
            GwpAiDiagnostics.WritePlayerJusticeState(
                "VILLAGE_RAID_REPUTATION_APPLIED",
                "village=" + village.Settlement.StringId + "; delta=-2");

            // 若已进入通缉状态，立即触发警察追捕
            if (PlayerState.IsWanted)
                CrimeState.TryAddPlayerCrime(GwpText.Get("{=gwp_playerbehaviormonitor_005}Raid village"), location, village.Name?.ToString() ?? GwpText.Get("{=gwp_playerbehaviormonitor_006}Unknown village"));

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_playerbehaviormonitor_007}The Grey Wardens enter your offence upon the roll: raid upon {VAR_1} |", "VAR_1", village.Name) +
                $"{PlayerState.GetReputationDisplay()} (-2)",
                Colors.Red));

            // 记录犯罪历史（供受害势力追踪等后续逻辑使用）
            PlayerState.AddCrimeRecord(GwpText.Get("{=gwp_playerbehaviormonitor_008}Raid village"), location, village.Name?.ToString() ?? GwpText.Get("{=gwp_playerbehaviormonitor_009}Unknown Village"), victimFaction);
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

            PlayerState.AddCrime(GwpText.Get("{=gwp_playerbehaviormonitor_010}Forced Recruitment"), settlement.Position.ToVec2(),
                settlement.Name?.ToString() ?? GwpText.Get("{=gwp_playerbehaviormonitor_011}Unknown Village"), settlement.MapFaction);
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

            PlayerState.AddCrime(GwpText.Get("{=gwp_playerbehaviormonitor_012}Forced Requisition of Supplies"), settlement.Position.ToVec2(),
                settlement.Name?.ToString() ?? GwpText.Get("{=gwp_playerbehaviormonitor_013}Unknown Village"), settlement.MapFaction);
        }

        #endregion

        #region 行善监听

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || !mapEvent.HasWinner) return;

            bool playerWon = false;
            bool playerInvolved = false;
            foreach (var p in mapEvent.Winner.Parties)
            {
                if (p.Party?.IsMobile == true && p.Party.MobileParty.IsMainParty)
                { playerWon = true; break; }
            }
            if (_pendingPoliceCrimeSupport != 0)
            {
                foreach (var p in mapEvent.InvolvedParties)
                {
                    if (p?.MobileParty?.IsMainParty == true)
                    {
                        playerInvolved = true;
                        break;
                    }
                }
            }

            if (!playerWon)
            {
                if (playerInvolved)
                {
                    _pendingEnemyCount = 0;
                    _pendingPoliceCrimeSupport = 0;
                    _pendingPlayerKillBaseline = -1;
                }
                return;
            }

            int playerKillCount = GetPlayerKillCountSinceBattleStart();

            if (TryResolvePendingPoliceCriminalReputation(mapEvent, playerKillCount))
            {
                _pendingEnemyCount = 0;
                _pendingPoliceCrimeSupport = 0;
                _pendingPlayerKillBaseline = -1;
                return;
            }

            if (TryApplyPoliceBattlePenalty(mapEvent))
            {
                _pendingEnemyCount = 0;
                _pendingPoliceCrimeSupport = 0;
                _pendingPlayerKillBaseline = -1;
                return;
            }

            // ★ R10：玩家亲手击败仍在案的罪犯部队也会获得声望。
            // 与剿匪/救援共用同一把"亲手击倒十人得一点"的累计器，并与上面的
            // 协助灰袍抓捕路径互斥，避免同一场战斗被重复结算。
            if (TryResolveCaseCriminalReputation(mapEvent, playerKillCount))
            {
                _pendingEnemyCount = 0;
                _pendingPoliceCrimeSupport = 0;
                _pendingPlayerKillBaseline = -1;
                return;
            }

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
                // 正义战斗中的个人击杀跨战斗累计；每满十人结算一点声望，余数随存档保留。
                int repGain = PlayerState.AccumulateGoodDeedKills(playerKillCount);

                if (repGain > 0)
                {
                    PlayerState.ChangeReputation(repGain);

                    string deedType = defenderIsVillageRaid ? GwpText.Get("{=gwp_playerbehaviormonitor_014}Protect the Village from Plunder")
                                    : defenderIsCaravan     ? GwpText.Get("{=gwp_playerbehaviormonitor_015}Rescue the Caravan")
                                    : defenderIsVillager    ? GwpText.Get("{=gwp_playerbehaviormonitor_016}Rescue the Villagers")
                                    : GwpText.Get("{=gwp_playerbehaviormonitor_017}Defeat the robbers");
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get("{=gwp_playerbehaviormonitor_018}The Grey Wardens mark your good service: {VAR_1} ({VAR_2} personally defeated) |", "VAR_1", deedType, "VAR_2", playerKillCount) +
                        $"{PlayerState.GetReputationDisplay()} (+{repGain})",
                        Colors.Green));
                }
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
            bool playerForcedVolunteers = mapEvent.IsForcingVolunteers && mapEvent.Winner == mapEvent.AttackerSide;
            bool playerForcedSupplies = mapEvent.IsForcingSupplies && mapEvent.Winner == mapEvent.AttackerSide;
            bool playerForcedVillageAction = playerForcedVolunteers || playerForcedSupplies;

            bool anyBadDeed = loserIsVillager || loserIsCaravan || playerRaidedVillage || playerForcedVillageAction;
            if (anyBadDeed)
            {
                // 不设下限：无人死伤（如无人防守的村庄劫掠）时不扣声望
                int killCount = _pendingEnemyCount;

                // ★ 功能 4B：村庄劫掠事件中 _pendingEnemyCount 始终为 0
                // （MapEventStarted 的 defenderParty 是 Settlement 非 MobileParty → null → 提前 return，未记录人数）
                // 兜底：直接从失败方的移动部队中计数（民兵/驻军等），确保"村民抵抗"战斗按每10人扣1分
                if ((playerRaidedVillage || playerForcedVillageAction) && killCount == 0)
                {
                    foreach (var p in loser.Parties)
                        if (p.Party?.IsMobile == true)
                            killCount += p.Party.NumberOfAllMembers;
                }

                int repLoss   = (killCount + 9) / 10; // 向上取整；killCount=0 → repLoss=0
                if (repLoss > 0)
                {
                    PlayerState.ChangeReputation(-repLoss);

                    // 若已进入通缉状态，触发警察追捕
                    if (PlayerState.IsWanted)
                    {
                        string crimeType = playerRaidedVillage    ? GwpText.Get("{=gwp_playerbehaviormonitor_019}Raid the village")
                                         : playerForcedVolunteers ? GwpText.Get("{=gwp_playerbehaviormonitor_020}Force recruits")
                                         : playerForcedSupplies   ? GwpText.Get("{=gwp_playerbehaviormonitor_021}Force supplies")
                                         : loserIsCaravan         ? GwpText.Get("{=gwp_playerbehaviormonitor_022}Raid the caravan")
                                         : GwpText.Get("{=gwp_playerbehaviormonitor_023}Kill the villagers");
                        CrimeState.TryAddPlayerCrime(crimeType, mapEvent.Position.ToVec2(), crimeType);
                    }

                    string badDeedType = playerRaidedVillage    ? GwpText.Get("{=gwp_playerbehaviormonitor_024}Raid a village")
                                       : playerForcedVolunteers ? GwpText.Get("{=gwp_playerbehaviormonitor_025}Force recruitment")
                                       : playerForcedSupplies   ? GwpText.Get("{=gwp_playerbehaviormonitor_026}Forced supplies")
                                       : loserIsCaravan         ? GwpText.Get("{=gwp_playerbehaviormonitor_027}Raid a caravan")
                                       : GwpText.Get("{=gwp_playerbehaviormonitor_028}Kill villagers");
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get("{=gwp_playerbehaviormonitor_029}The Grey Wardens enter your offence upon the roll: {VAR_1} ({VAR_2} defeated) |", "VAR_1", badDeedType, "VAR_2", killCount) +
                        $"{PlayerState.GetReputationDisplay()} (-{repLoss})",
                        Colors.Red));
                }
            }

            _pendingEnemyCount = 0;
            _pendingPoliceCrimeSupport = 0;
            _pendingPlayerKillBaseline = -1;
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
            // 强制介入（保护防守方）：无视外交状态，以防守方身份加入战斗
            starter.AddGameMenuOption(
                "join_encounter",
                "gwp_force_join_battle",
                "{GWP_FORCE_JOIN_TEXT}",
                ForceJoinBattleCondition,
                ForceJoinBattleConsequence,
                isLeave: false,
                index: -1);

            // 落井下石（帮助攻击方）：无视外交状态，以攻击方身份加入战斗
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
        /// 条件：当前遭遇属于可强制介入场景时，显示"强制介入"选项。
        /// </summary>
        private bool ForceJoinBattleCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;

                MapEvent battle = PlayerEncounter.EncounteredBattle;
                if (battle == null) return false;

                if (!TryBuildForceJoinTexts(battle, out string defendText, out _))
                    return false;

                MBTextManager.SetTextVariable("GWP_FORCE_JOIN_TEXT", new TextObject(defendText));
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 记录敌方人数供声望缩放，以防守方身份加入战斗并切换到战斗菜单。
        /// </summary>
        private void ForceJoinBattleConsequence(MenuCallbackArgs args)
        {
            try
            {
                MapEvent battle = PlayerEncounter.EncounteredBattle;
                if (battle == null) return;

                CaptureForcedJoinEncounterState(battle, BattleSideEnum.Defender);

                // 记录攻击方战前人数（JoinBattle 后不再触发 OnMapEventStarted，需在此预先记录）
                _pendingEnemyCount = 0;
                foreach (var p in battle.AttackerSide.Parties)
                    if (p.Party?.IsMobile == true)
                        _pendingEnemyCount += p.Party.NumberOfAllMembers;

                CapturePlayerKillBaseline();

                // 直接加入防守方的战斗阵营是正确的；问题仅在于原版仍沿用
                // 村庄劫掠事件的 encounter 说明，把玩家写成了劫掠者。
                // 在菜单完成原版初始化后覆盖说明文字，不改动任何战斗方。
                if (battle.IsRaid)
                {
                    _pendingVillageDefenseEncounterText = true;
                    _pendingVillageDefenseName = battle.MapEventSettlement?.Name?.ToString()
                        ?? GwpText.Get("{=gwp_playerbehaviormonitor_050}the village");
                }

                PlayerEncounter.JoinBattle(BattleSideEnum.Defender);
                GameMenu.ActivateGameMenu("encounter");
            }
            catch { }
        }

        private void OnAfterGameMenuInitialized(MenuCallbackArgs args)
        {
            if (!_pendingVillageDefenseEncounterText ||
                !string.Equals(args.MenuContext?.GameMenu?.StringId, "encounter", StringComparison.Ordinal))
            {
                return;
            }

            MBTextManager.SetTextVariable(
                "ENCOUNTER_TEXT",
                new TextObject(GwpText.Get(
                    "{=gwp_playerbehaviormonitor_051}You have come to {VAR_1} and joined its militia against the raiders. When you are ready, stand with the villagers and meet the enemy.",
                    "VAR_1",
                    _pendingVillageDefenseName)));
            MBTextManager.SetTextVariable(
                "ATTACK_TEXT",
                new TextObject(GwpText.Get("{=gwp_playerbehaviormonitor_052}Stand with the militia and attack")));

            _pendingVillageDefenseEncounterText = false;
            _pendingVillageDefenseName = string.Empty;
        }

        /// <summary>
        /// 条件：当前遭遇属于可强制介入场景时，显示"落井下石"选项。
        /// </summary>
        private bool ForceJoinAttackersCondition(MenuCallbackArgs args)
        {
            try
            {
                args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;

                MapEvent battle = PlayerEncounter.EncounteredBattle;
                if (battle == null) return false;

                if (!TryBuildForceJoinTexts(battle, out _, out string attackText))
                    return false;

                MBTextManager.SetTextVariable("GWP_FORCE_JOIN_ATTACKERS_TEXT", new TextObject(attackText));
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 记录防守方人数供战后声望扣除缩放，以攻击方身份加入战斗。
        /// </summary>
        private void ForceJoinAttackersConsequence(MenuCallbackArgs args)
        {
            try
            {
                MapEvent battle = PlayerEncounter.EncounteredBattle;
                if (battle == null) return;

                CaptureForcedJoinEncounterState(battle, BattleSideEnum.Attacker);

                // 记录防守方战前人数（供战后按人数缩放扣声望；JoinBattle 不触发 OnMapEventStarted）
                _pendingEnemyCount = 0;
                foreach (var p in battle.DefenderSide.Parties)
                    if (p.Party?.IsMobile == true)
                        _pendingEnemyCount += p.Party.NumberOfAllMembers;

                CapturePlayerKillBaseline();

                // 以攻击方身份加入战斗 → 切换到遭遇战菜单（进入战斗准备界面）
                PlayerEncounter.JoinBattle(BattleSideEnum.Attacker);
                GameMenu.ActivateGameMenu("encounter");
            }
            catch { }
        }

        private bool TryBuildForceJoinTexts(MapEvent battle, out string defendText, out string attackText)
        {
            defendText = string.Empty;
            attackText = string.Empty;

            if (TryBuildCivilianEncounterTexts(battle, out defendText, out attackText))
                return true;

            if (TryBuildPoliceCriminalEncounterTexts(battle, out defendText, out attackText))
                return true;

            return false;
        }

        private static bool TryBuildCivilianEncounterTexts(MapEvent battle, out string defendText, out string attackText)
        {
            defendText = string.Empty;
            attackText = string.Empty;

            bool defenderIsCaravan = battle.DefenderSide.Parties
                .Any(p => p.Party?.IsMobile == true && p.Party.MobileParty.IsCaravan);
            bool defenderIsVillager = battle.DefenderSide.Parties
                .Any(p => p.Party?.IsMobile == true && p.Party.MobileParty.IsVillager);
            bool isVillageRaid = battle.IsRaid
                && battle.GetLeaderParty(BattleSideEnum.Defender)?.IsSettlement == true;

            if (!defenderIsCaravan && !defenderIsVillager && !isVillageRaid)
                return false;

            string defendTarget = isVillageRaid ? GwpText.Get("{=gwp_playerbehaviormonitor_030}Village")
                : defenderIsCaravan ? GwpText.Get("{=gwp_playerbehaviormonitor_031}Caravan")
                : GwpText.Get("{=gwp_playerbehaviormonitor_032}Villager");
            string attackAction = isVillageRaid ? GwpText.Get("{=gwp_playerbehaviormonitor_033}Participate in raiding village")
                : defenderIsCaravan ? GwpText.Get("{=gwp_playerbehaviormonitor_034}Raid Caravan")
                : GwpText.Get("{=gwp_playerbehaviormonitor_035}Attack villagers");

            defendText = GwpText.Get("{=gwp_playerbehaviormonitor_036}Join the defenders and protect {VAR_1}.", "VAR_1", defendTarget);
            attackText = GwpText.Get("{=gwp_playerbehaviormonitor_037}joins the attacker, {VAR_1}.", "VAR_1", attackAction);
            return true;
        }

        private bool TryBuildPoliceCriminalEncounterTexts(MapEvent battle, out string defendText, out string attackText)
        {
            defendText = string.Empty;
            attackText = string.Empty;

            if (!TryGetPoliceCriminalEncounter(battle, out BattleSideEnum policeSide, out MobileParty? criminal))
                return false;

            string criminalName = criminal?.Name?.ToString() ?? GwpText.Get("{=gwp_playerbehaviormonitor_038}Prisoner");
            bool policeDefends = policeSide == BattleSideEnum.Defender;

            defendText = policeDefends
                ? GwpText.Get("{=gwp_playerbehaviormonitor_039}Join the Grey Wardens and help hunt down criminals.")
                : GwpText.Get("{=gwp_playerbehaviormonitor_040}Join {VAR_1} against the Grey Wardens.", "VAR_1", criminalName);
            attackText = policeDefends
                ? GwpText.Get("{=gwp_playerbehaviormonitor_041}Join {VAR_1} against the Grey Wardens.", "VAR_1", criminalName)
                : GwpText.Get("{=gwp_playerbehaviormonitor_042}Join the Grey Wardens and help catch criminals.");
            return true;
        }

        private void CaptureForcedJoinEncounterState(MapEvent battle, BattleSideEnum playerJoinSide)
        {
            _pendingPoliceCrimeSupport = 0;

            if (!TryGetPoliceCriminalEncounter(battle, out BattleSideEnum policeSide, out _))
                return;

            _pendingPoliceCrimeSupport = playerJoinSide == policeSide ? 1 : -1;
        }

        private bool TryResolvePendingPoliceCriminalReputation(MapEvent mapEvent, int playerKillCount)
        {
            if (_pendingPoliceCrimeSupport == 0)
                return false;

            if (_pendingPoliceCrimeSupport > 0)
            {
                // 正义战斗中的个人击杀跨战斗累计；每满十人结算一点声望，余数随存档保留。
                int repGain = PlayerState.AccumulateGoodDeedKills(playerKillCount);
                if (repGain <= 0)
                    return true;

                PlayerState.ChangeReputation(repGain);
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_playerbehaviormonitor_043}The Grey Wardens noticed your good deeds: Assisted in apprehending criminals (defeated {VAR_1} people personally) |", "VAR_1", playerKillCount) +
                    $"{PlayerState.GetReputationDisplay()} (+{repGain})",
                    Colors.Green));
            }
            else
            {
                if (!TryGetPoliceCriminalEncounter(mapEvent, out BattleSideEnum policeSide, out _))
                    return false;

                MapEventSide policeEventSide = policeSide == BattleSideEnum.Attacker
                    ? mapEvent.AttackerSide
                    : mapEvent.DefenderSide;
                int policeCasualties = CountPoliceCasualties(policeEventSide);
                int repLoss = (policeCasualties + 9) / 10;
                if (repLoss <= 0)
                    return true;

                PlayerState.ChangeReputation(-repLoss);

                if (PlayerState.IsWanted)
                {
                    CrimeState.TryAddPlayerCrime(
                        GwpText.Get("{=gwp_playerbehaviormonitor_044}Obstructed enforcement"),
                        mapEvent.Position.ToVec2(),
                        GwpText.Get("{=gwp_playerbehaviormonitor_045}Helped criminals against the Grey Wardens"));
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_playerbehaviormonitor_046}The Grey Wardens enter your offence upon the roll: obstruction of the law ({VAR_1} Wardens defeated) |", "VAR_1", policeCasualties) +
                    $"{PlayerState.GetReputationDisplay()} (-{repLoss})",
                    Colors.Red));
            }

            return true;
        }

        private bool TryApplyPoliceBattlePenalty(MapEvent mapEvent)
        {
            if (mapEvent == null || !mapEvent.HasWinner)
                return false;

            MapEventSide loser = mapEvent.Winner == mapEvent.AttackerSide
                ? mapEvent.DefenderSide
                : mapEvent.AttackerSide;
            if (loser == null)
                return false;

            int policeCasualties = CountPoliceCasualties(loser);
            if (policeCasualties <= 0)
                return false;

            bool loserHadPatrol = SideContainsPartyType(loser, static p => GwpCommon.IsPatrolParty(p));
            int repLoss = (policeCasualties + 9) / 10;
            if (repLoss <= 0)
                return false;

            PlayerState.ChangeReputation(-repLoss);

            string targetName = loserHadPatrol ? GwpText.Get("{=gwp_playerbehaviormonitor_047}Picket") : GwpText.Get("{=gwp_playerbehaviormonitor_048}Grey Wardens");
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_playerbehaviormonitor_049}The Grey Wardens enter your offence upon the roll: defeated {VAR_1} ({VAR_2} troops) |", "VAR_1", targetName, "VAR_2", policeCasualties) +
                $"{PlayerState.GetReputationDisplay()} (-{repLoss})",
                Colors.Red));
            return true;
        }

        /// <summary>
        /// 玩家获胜且失败方包含仍有开放案卷的罪犯部队时，按其亲手击倒数
        /// 累计灰袍声望（每十人一点，跨战斗保留余数）。仅当该罪犯同时是
        /// 灰袍承办目标且玩家走了强制介入协助路径时，由
        /// TryResolvePendingPoliceCriminalReputation 先结算并短路，不会重复计。
        /// </summary>
        private bool TryResolveCaseCriminalReputation(
            MapEvent mapEvent,
            int playerKillCount)
        {
            if (mapEvent == null || !mapEvent.HasWinner)
                return false;

            MapEventSide loser = mapEvent.Winner == mapEvent.AttackerSide
                ? mapEvent.DefenderSide
                : mapEvent.AttackerSide;
            if (loser == null || !SideContainsOpenCaseCriminal(loser))
                return false;

            int repGain = PlayerState.AccumulateGoodDeedKills(playerKillCount);
            if (repGain <= 0)
                return true;

            PlayerState.ChangeReputation(repGain);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_playerbehaviormonitor_054}The Grey Wardens mark your good service: defeated the forces of a wanted criminal ({VAR_1} personally defeated) |", "VAR_1", playerKillCount) +
                $"{PlayerState.GetReputationDisplay()} (+{repGain})",
                Colors.Green));
            return true;
        }

        private static bool SideContainsOpenCaseCriminal(MapEventSide side)
        {
            foreach (var p in side.Parties)
            {
                MobileParty? party = p?.Party?.MobileParty;
                if (party == null) continue;

                if (IsOpenCaseCriminal(party) ||
                    IsOpenCaseCriminal(party.AttachedTo) ||
                    IsOpenCaseCriminal(party.Army?.LeaderParty))
                    return true;
            }
            return false;
        }

        private static bool IsOpenCaseCriminal(MobileParty? party)
        {
            if (party == null || party.IsMainParty)
                return false;
            return CrimeState.GetByOffenderId(party.StringId) != null;
        }

        private bool TryGetPoliceCriminalEncounter(MapEvent battle, out BattleSideEnum policeSide, out MobileParty? criminal)
        {
            criminal = null!;
            policeSide = BattleSideEnum.Attacker;

            if (TryMatchPoliceCriminalEncounterOnSide(battle, BattleSideEnum.Attacker, out criminal))
            {
                policeSide = BattleSideEnum.Attacker;
                return true;
            }

            if (TryMatchPoliceCriminalEncounterOnSide(battle, BattleSideEnum.Defender, out criminal))
            {
                policeSide = BattleSideEnum.Defender;
                return true;
            }

            return false;
        }

        private bool TryMatchPoliceCriminalEncounterOnSide(
            MapEvent battle,
            BattleSideEnum policeSide,
            out MobileParty? criminal)
        {
            criminal = null!;
            MapEventSide policeEventSide = policeSide == BattleSideEnum.Attacker
                ? battle.AttackerSide
                : battle.DefenderSide;
            BattleSideEnum criminalSide = policeSide == BattleSideEnum.Attacker
                ? BattleSideEnum.Defender
                : BattleSideEnum.Attacker;

            foreach (var party in policeEventSide.Parties)
            {
                MobileParty? policeParty = party?.Party?.MobileParty;
                if (!IsPoliceEncounterParty(policeParty))
                    continue;

                PoliceTask? task = policeParty?.StringId != null
                    ? CrimeState.GetTask(policeParty.StringId)
                    : null;
                MobileParty? offender = task?.TargetCrime?.Offender;
                if (offender == null || !offender.IsActive || offender.IsMainParty)
                    continue;

                if (!IsPartyOnBattleSide(battle, criminalSide, offender))
                    continue;

                criminal = offender;
                return true;
            }

            return false;
        }

        private static bool IsPoliceEncounterParty(MobileParty? party)
        {
            if (party == null)
                return false;

            if (GwpCommon.IsPatrolParty(party) || GwpCommon.IsEnforcementDelayPatrolParty(party))
                return true;

            return string.Equals(
                party.ActualClan?.StringId,
                GwpIds.PoliceClanId,
                StringComparison.OrdinalIgnoreCase);
        }

        private void CapturePlayerKillBaseline()
        {
            _pendingPlayerKillBaseline = GetCurrentPlayerKillTotal();
        }

        private int GetPlayerKillCountSinceBattleStart()
        {
            int current = GetCurrentPlayerKillTotal();
            if (_pendingPlayerKillBaseline < 0 || current < _pendingPlayerKillBaseline)
                return 0;

            return current - _pendingPlayerKillBaseline;
        }

        private static int GetCurrentPlayerKillTotal()
        {
            IStatisticsCampaignBehavior? stats =
                Campaign.Current?.GetCampaignBehavior<IStatisticsCampaignBehavior>();
            return stats?.GetNumberOfTroopsKnockedOrKilledByPlayer() ?? 0;
        }

        private static int CountPoliceCasualties(MapEventSide side)
        {
            int total = 0;

            foreach (MapEventParty party in side.Parties)
            {
                MobileParty? mobileParty = party?.Party?.MobileParty;
                if (!IsPoliceEncounterParty(mobileParty))
                    continue;

                total += CountRosterMembers(party.DiedInBattle);
                total += CountRosterMembers(party.WoundedInBattle);
                total += CountRosterMembers(party.RoutedInBattle);
            }

            return total;
        }

        private static int CountRosterMembers(TroopRoster? roster)
        {
            if (roster == null)
                return 0;

            int total = 0;
            foreach (TroopRosterElement element in roster.GetTroopRoster())
                total += element.Number;
            return total;
        }

        private static bool SideContainsPartyType(MapEventSide side, Func<MobileParty, bool> predicate)
        {
            foreach (MapEventParty party in side.Parties)
            {
                MobileParty? mobileParty = party?.Party?.MobileParty;
                if (mobileParty != null && predicate(mobileParty))
                    return true;
            }

            return false;
        }

        private static bool IsPartyOnBattleSide(MapEvent battle, BattleSideEnum side, MobileParty? party)
        {
            if (party == null)
                return false;

            MapEventSide eventSide = side == BattleSideEnum.Attacker
                ? battle.AttackerSide
                : battle.DefenderSide;

            return eventSide.Parties.Any(p => p.Party?.MobileParty == party);
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
