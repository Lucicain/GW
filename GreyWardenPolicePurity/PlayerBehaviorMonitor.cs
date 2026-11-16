using System;
using System.Collections.Generic;
using System.Linq;
using GreyWardenPolicePurity.Core.Troops;
using HarmonyLib;
using SandBox.CampaignBehaviors;
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
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 玩家行为监控 - 监听玩家的犯罪和行善行为，记录到 PlayerBehaviorPool
    /// </summary>
    public class PlayerBehaviorMonitor : CampaignBehaviorBase
    {

        // 用真实参战侧绑定选边与战前人数。主队撤离后原战斗仍可能继续。
        private MapEventSide? _pendingBattleSide;
        private int _pendingEnemyCount = 0;
        private int _pendingPoliceCrimeSupport = 0; // 1=帮助灰袍守卫，-1=帮助犯人，0=普通战斗
        // 同侧多轮保留个人和主队战果。换边后从新参战侧开始记录。
        private MapEventSide? _goodDeedBattleSide;
        private int _goodDeedPersonalWeight;
        private int _goodDeedMainPartyWeight;
        private MapEventSide? _villageDefenseSide;
        private Settlement? _defendedVillage;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

            // ── 犯罪 / 行善监听 ─────────────────────────────────────────────────────
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            // VillageBeingRaided only names the village, so the raider used to be
            // guessed from whichever party had it as its TargetSettlement - which
            // is true of anyone standing in the village, tax collection included.
            // VillageStateChanged carries the raider that ChangeVillageStateAction
            // was given, so no guessing is needed.
            CampaignEvents.VillageStateChanged.AddNonSerializedListener(this, OnVillageStateChanged);
            CampaignEvents.ForceVolunteersCompletedEvent.AddNonSerializedListener(this, OnForceVolunteers);
            CampaignEvents.ForceSuppliesCompletedEvent.AddNonSerializedListener(this, OnForceSupplies);
            CampaignEvents.OnPlayerPartyKnockedOrKilledTroopEvent.AddNonSerializedListener(this, OnSimulatedMainPartyDefeat);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            PlayerBehaviorPool.SyncData(dataStore);
            dataStore.SyncData("gwp_good_deed_battle_side", ref _goodDeedBattleSide);
            dataStore.SyncData("gwp_good_deed_battle_weight", ref _goodDeedPersonalWeight);
            dataStore.SyncData("gwp_good_deed_main_party_battle_weight", ref _goodDeedMainPartyWeight);
            dataStore.SyncData("gwp_player_reputation_battle_side", ref _pendingBattleSide);
            // 沿用旧键接续护村旧档。所有玩家地图战斗均记录选边和战前人数。
            dataStore.SyncData("gwp_village_defense_enemy_count", ref _pendingEnemyCount);
            dataStore.SyncData("gwp_village_defense_police_support", ref _pendingPoliceCrimeSupport);
            dataStore.SyncData("gwp_village_defense_side", ref _villageDefenseSide);
            if (_villageDefenseSide == null) return;
            dataStore.SyncData("gwp_village_defense_village", ref _defendedVillage);
            if (dataStore.IsLoading)
            {
                _pendingBattleSide ??= _villageDefenseSide;
                if (_goodDeedBattleSide == null)
                    RestoreLegacyVillageDefenseWeight(dataStore, _villageDefenseSide);
            }
        }

        private void RestoreLegacyVillageDefenseWeight(IDataStore dataStore, MapEventSide side)
        {
            int baseline = -1;
            if (!dataStore.SyncData("gwp_village_defense_kill_baseline", ref baseline) || baseline < 0) return;

            // Sandbox 的战役统计先于本行为读档。旧记录只剩人数，按旧进度等值接续。
            int current = Campaign.Current.GetCampaignBehavior<IStatisticsCampaignBehavior>()
                ?.GetNumberOfTroopsKnockedOrKilledByPlayer() ?? 0;
            if (current <= baseline) return;

            _goodDeedBattleSide = side;
            _goodDeedPersonalWeight = (current - baseline) *
                (WardenReputationRules.TroopWeightPerPoint / PlayerBehaviorPool.LegacyGoodDeedKillsPerPoint);
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
            MapEventSide? playerSide = playerIsAttacker ? mapEvent.AttackerSide
                : playerIsDefender ? mapEvent.DefenderSide : null;
            if (playerSide != null && _pendingBattleSide != playerSide)
            {
                _pendingBattleSide = playerSide;
                _pendingPoliceCrimeSupport = 0;
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

            // 只记受害势力，不扣声望：声望按击败人数在 OnMapEventEnded 中缩放扣除
            if (defender.PartyComponent is VillagerPartyComponent ||
                defender.PartyComponent is CaravanPartyComponent)
                PlayerBehaviorPool.TrackVictimFaction(defender.ActualClan?.MapFaction);
        }

        /// <summary>
        /// The raid offence, charged only to the party that actually raided.
        ///
        /// This used to listen to VillageBeingRaided, which passes nothing but
        /// the village, and recovered the raider by looking for a party whose
        /// TargetSettlement was that village.  The player's own party matches
        /// that test whenever it is simply at the village - collecting taxes,
        /// running a duty, waiting - so an AI lord raiding a village the player
        /// happened to be standing in charged the raid to the player: the -2,
        /// the crime record, and the warrant that follows from it.  That is the
        /// reported "losing reputation near a raided village".
        ///
        /// ChangeVillageStateAction is handed the raider and passes it through
        /// VillageStateChanged, so the offender is now read rather than guessed.
        /// </summary>
        private void OnVillageStateChanged(
            Village village,
            Village.VillageStates oldState,
            Village.VillageStates newState,
            MobileParty raiderParty)
        {
            if (village?.Settlement == null) return;
            if (newState != Village.VillageStates.BeingRaided) return;

            GwpAiDiagnostics.WritePlayerJusticeState(
                "VILLAGE_BEING_RAIDED_EVALUATED",
                "village=" + village.Settlement.StringId +
                "; oldState=" + oldState +
                "; raider=" + (raiderParty?.StringId ?? "-") +
                "; raiderIsPlayer=" + (raiderParty?.IsMainParty == true));
            if (raiderParty == null || !raiderParty.IsMainParty) return;

            Vec2 location = village.Settlement.Position.ToVec2();
            IFaction victimFaction = village.Settlement.MapFaction;

            // ★ 功能 4A：劫掠村庄固定扣 -2 声望（无论村庄有无防守者）
            PlayerBehaviorPool.ChangeReputation(-2);
            GwpAiDiagnostics.WritePlayerJusticeState(
                "VILLAGE_RAID_REPUTATION_APPLIED",
                "village=" + village.Settlement.StringId + "; delta=-2");

            // 若已进入通缉状态，立即触发警察追捕
            if (PlayerBehaviorPool.IsWanted)
                CrimePool.TryAddPlayerCrime(GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_005), location, village.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_006));

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_007, "VAR_1", village.Name),
                Colors.Red));

            PlayerBehaviorPool.TrackVictimFaction(victimFaction);
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

            PlayerBehaviorPool.AddCrime(GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_010), settlement.Position.ToVec2(),
                settlement.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_011), settlement.MapFaction);
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

            PlayerBehaviorPool.AddCrime(GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_012), settlement.Position.ToVec2(),
                settlement.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_013), settlement.MapFaction);
        }

        #endregion

        #region 行善监听

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null) return;
            MapEventSide? winner = mapEvent.Winner;
            var weights = TakeGoodDeedBattleWeights(mapEvent, winner);
            (int enemyCount, int policeSupport) = TakeBattleContext(mapEvent, winner);
            Settlement? defendedVillage = winner != null ? DefendedVillage(winner) : null;
            bool recordedDefense = _villageDefenseSide?.MapEvent == mapEvent;
            if (recordedDefense)
            {
                _villageDefenseSide = null;
                _defendedVillage = null;
            }
            if (winner == null || !winner.Parties.Any(p => p.Party?.MobileParty?.IsMainParty == true))
                return;

            if (TryResolvePendingPoliceCriminalReputation(mapEvent, weights, policeSupport))
                return;

            if (TryApplyPoliceBattlePenalty(mapEvent))
                return;

            // 在案罪犯、剿匪和救援共用战果权重，协助灰袍的战果已在上面结算。
            if (defendedVillage == null && TryResolveCaseCriminalReputation(mapEvent, weights))
                return;

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

            bool anyGoodDeed = enemyIsBandit || defenderIsVillager || defenderIsCaravan || defendedVillage != null;
            if (anyGoodDeed && PlayerBehaviorPool.TryAwardGoodDeedReputation(weights))
            {
                string deedType = defendedVillage != null ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_014)
                                : defenderIsCaravan     ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_015)
                                : defenderIsVillager    ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_016)
                                : GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_017);
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_018, "VAR_1", deedType),
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
            bool playerForcedVolunteers = mapEvent.IsForcingVolunteers && mapEvent.Winner == mapEvent.AttackerSide;
            bool playerForcedSupplies = mapEvent.IsForcingSupplies && mapEvent.Winner == mapEvent.AttackerSide;
            bool playerForcedVillageAction = playerForcedVolunteers || playerForcedSupplies;

            bool anyBadDeed = loserIsVillager || loserIsCaravan || playerRaidedVillage || playerForcedVillageAction;
            if (anyBadDeed)
            {
                // 不设下限：无人死伤（如无人防守的村庄劫掠）时不扣声望
                int killCount = enemyCount;

                // 村庄事件的防守方可能是聚落。如果没有记下人数，按败方的民兵、驻军等移动部队计。
                if ((playerRaidedVillage || playerForcedVillageAction) && killCount == 0)
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
                        string crimeType = playerRaidedVillage    ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_019)
                                         : playerForcedVolunteers ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_020)
                                         : playerForcedSupplies   ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_021)
                                         : loserIsCaravan         ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_022)
                                         : GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_023);
                        CrimePool.TryAddPlayerCrime(crimeType, mapEvent.Position.ToVec2(), crimeType);
                    }

                    string badDeedType = playerRaidedVillage    ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_024)
                                       : playerForcedVolunteers ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_025)
                                       : playerForcedSupplies   ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_026)
                                       : loserIsCaravan         ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_027)
                                       : GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_028);
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_029, "VAR_1", badDeedType, "VAR_2", killCount),
                        Colors.Red));
                }
            }
        }

        #endregion

        #region 强制介入 GameMenu 选项

        private void OnSessionLaunched(CampaignGameStarter starter) => GwpPlayerIntervention.Register(starter);

        internal static Settlement? RaidedVillage(MapEvent battle) =>
            battle.IsRaid && battle.MapEventSettlement is { IsVillage: true } village ? village : null;

        internal Settlement? DefendedVillage(MapEventSide side)
        {
            MapEvent battle = side.MapEvent;
            if (_villageDefenseSide?.MapEvent == battle)
                return _villageDefenseSide == side ? _defendedVillage : null;

            return side == battle.DefenderSide ? RaidedVillage(battle) : null;
        }

        internal void RecordVillageDefense(MapEventSide side, Settlement village)
        {
            if (_villageDefenseSide == side) return;
            _villageDefenseSide = side;
            _defendedVillage = village;
        }

        internal static (int EnemyCount, int PoliceSupport) DescribeIntervention(MapEvent battle, BattleSideEnum side)
        {
            BattleSideEnum enemySide = side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;
            int enemyCount = battle.GetMapEventSide(enemySide).Parties
                .Where(p => p.Party?.IsMobile == true).Sum(p => p.Party.NumberOfAllMembers);
            int policeSupport = TryGetPoliceCriminalEncounter(battle, out BattleSideEnum policeSide, out _)
                ? (side == policeSide ? 1 : -1)
                : 0;
            return (enemyCount, policeSupport);
        }

        internal void RecordInterventionBattle(MapEventSide side, int enemyCount, int policeSupport)
        {
            // 转场新建战斗时，OnMapEventStarted 已记下实际编入的敌方人员。
            if (_pendingBattleSide != side) _pendingEnemyCount = enemyCount;
            _pendingBattleSide = side;
            _pendingPoliceCrimeSupport = policeSupport;
        }

        internal static bool TryBuildForceJoinTexts(MapEvent battle, out string defendText, out string attackText)
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
            bool isVillageRaid = RaidedVillage(battle) != null;

            if (!defenderIsCaravan && !defenderIsVillager && !isVillageRaid)
                return false;

            string defendTarget = isVillageRaid ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_030)
                : defenderIsCaravan ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_031)
                : GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_032);
            string attackAction = isVillageRaid ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_033)
                : defenderIsCaravan ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_034)
                : GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_035);

            defendText = GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_036, "VAR_1", defendTarget);
            attackText = GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_037, "VAR_1", attackAction);
            return true;
        }

        private static bool TryBuildPoliceCriminalEncounterTexts(MapEvent battle, out string defendText, out string attackText)
        {
            defendText = string.Empty;
            attackText = string.Empty;

            if (!TryGetPoliceCriminalEncounter(battle, out BattleSideEnum policeSide, out MobileParty? criminal))
                return false;

            string criminalName = criminal?.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_038);
            bool policeDefends = policeSide == BattleSideEnum.Defender;

            defendText = policeDefends
                ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_039)
                : GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_040, "VAR_1", criminalName);
            attackText = policeDefends
                ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_041, "VAR_1", criminalName)
                : GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_042);
            return true;
        }

        private bool TryResolvePendingPoliceCriminalReputation(MapEvent mapEvent,
            (int Personal, int MainParty) weights, int policeSupport)
        {
            if (policeSupport == 0)
                return false;

            if (policeSupport > 0)
            {
                if (!PlayerBehaviorPool.TryAwardGoodDeedReputation(weights))
                    return true;

                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_043),
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

                PlayerBehaviorPool.ChangeReputation(-repLoss);

                if (PlayerBehaviorPool.IsWanted)
                {
                    CrimePool.TryAddPlayerCrime(
                        GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_044),
                        mapEvent.Position.ToVec2(),
                        GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_045));
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_046, "VAR_1", policeCasualties),
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

            PlayerBehaviorPool.ChangeReputation(-repLoss);

            string targetName = loserHadPatrol ? GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_047) : GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_048);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_049, "VAR_1", targetName, "VAR_2", policeCasualties),
                Colors.Red));
            return true;
        }

        /// <summary>
        /// 玩家获胜且失败方包含仍有开放案卷的罪犯部队时，按个人和主队战果累计灰袍声望。
        /// 命中强制介入协助分支时，由 TryResolvePendingPoliceCriminalReputation
        /// 先结算并返回。
        /// </summary>
        private bool TryResolveCaseCriminalReputation(MapEvent mapEvent, (int Personal, int MainParty) weights)
        {
            if (mapEvent == null || !mapEvent.HasWinner)
                return false;

            MapEventSide loser = mapEvent.Winner == mapEvent.AttackerSide
                ? mapEvent.DefenderSide
                : mapEvent.AttackerSide;
            if (loser == null || !SideContainsOpenCaseCriminal(loser))
                return false;

            if (!PlayerBehaviorPool.TryAwardGoodDeedReputation(weights))
                return true;

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_playerbehaviormonitor_054),
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
            return CrimePool.GetByOffenderId(party.StringId) != null;
        }

        private static bool TryGetPoliceCriminalEncounter(MapEvent battle, out BattleSideEnum policeSide, out MobileParty? criminal)
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

        private static bool TryMatchPoliceCriminalEncounterOnSide(
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
                if (!GwpCommon.IsGreyWardenEnforcementParty(policeParty))
                    continue;

                PoliceTask? task = policeParty?.StringId != null
                    ? CrimePool.GetTask(policeParty.StringId)
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

        private void OnSimulatedMainPartyDefeat(CharacterObject defeated)
        {
            if (PartyBase.MainParty?.MapEventSide is { MapEvent: { IsFinalized: false } } side)
                RecordGoodDeedDefeat(side, defeated.Tier, personal: false);
        }

        internal void RecordGoodDeedDefeat(MapEventSide side, int tier, bool personal)
        {
            if (_goodDeedBattleSide != side)
            {
                _goodDeedBattleSide = side;
                _goodDeedPersonalWeight = 0;
                _goodDeedMainPartyWeight = 0;
            }

            int weight = WardenReputationRules.TroopRewardWeight(tier);
            if (personal) _goodDeedPersonalWeight += weight;
            else _goodDeedMainPartyWeight += weight;
        }

        private (int Personal, int MainParty) TakeGoodDeedBattleWeights(MapEvent battle, MapEventSide? winner)
        {
            if (_goodDeedBattleSide?.MapEvent != battle) return default;

            (int Personal, int MainParty) weights = _goodDeedBattleSide == winner
                ? (_goodDeedPersonalWeight, _goodDeedMainPartyWeight)
                : default;
            _goodDeedBattleSide = null;
            _goodDeedPersonalWeight = 0;
            _goodDeedMainPartyWeight = 0;
            return weights;
        }

        private (int EnemyCount, int PoliceSupport) TakeBattleContext(MapEvent battle, MapEventSide? winner)
        {
            if (_pendingBattleSide?.MapEvent != battle) return default;

            var context = _pendingBattleSide == winner ? (_pendingEnemyCount, _pendingPoliceCrimeSupport) : default;
            _pendingBattleSide = null;
            _pendingEnemyCount = 0;
            _pendingPoliceCrimeSupport = 0;
            return context;
        }

        private static int CountPoliceCasualties(MapEventSide side)
        {
            int total = 0;

            foreach (MapEventParty party in side.Parties)
            {
                MobileParty? mobileParty = party?.Party?.MobileParty;
                if (party == null || !GwpCommon.IsGreyWardenEnforcementParty(mobileParty))
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
    }

    [HarmonyPatch(typeof(StatisticsCampaignBehavior), "OnAgentRemoved", typeof(Agent), typeof(Agent))]
    internal static class GwpPlayerGoodDeedCasualtyPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Agent affectedAgent, Agent? affectorAgent)
        {
            PartyBase? mainParty = PartyBase.MainParty;
            if (affectorAgent == null || !affectorAgent.IsHuman ||
                affectedAgent.State is not (AgentState.Killed or AgentState.Unconscious) ||
                !affectedAgent.IsHuman || !affectedAgent.IsEnemyOf(affectorAgent) ||
                affectedAgent.Character is not CharacterObject troop ||
                mainParty?.MapEventSide is not { MapEvent: { IsFinalized: false } } side)
                return;

            // 同队形的士兵仍归各自原部队。场景临时援军借用接应者的部队标识。
            bool personal = affectorAgent == Agent.Main;
            if (!personal && (affectorAgent.Origin is GwpBattleSupportOrigin ||
                !ReferenceEquals(affectorAgent.Origin?.BattleCombatant, mainParty)))
                return;

            Campaign.Current?.GetCampaignBehavior<PlayerBehaviorMonitor>()?.RecordGoodDeedDefeat(side, troop.Tier, personal);
        }
    }
}
