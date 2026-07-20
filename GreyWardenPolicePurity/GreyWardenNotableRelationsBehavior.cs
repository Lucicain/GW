using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍是面向全大陆平民和地方社群的公共治安组织。所有灰袍家族成员与
    /// 聚落要人的基础关系恒定为满值，使原版招募、访问和要人交互不会因为
    /// 灰袍没有封地、与本地要人缺少私人关系而长期失效。
    /// </summary>
    public sealed class GreyWardenNotableRelationsBehavior : CampaignBehaviorBase
    {
        internal const int MaximumNotableRelation = 100;

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent
                .AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent
                .AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent
                .AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HeroCreated
                .AddNonSerializedListener(this, OnHeroCreated);
            CampaignEvents.HeroComesOfAgeEvent
                .AddNonSerializedListener(this, OnHeroComesOfAge);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void OnGameLoaded(CampaignGameStarter starter) =>
            RefreshAllRelations();

        private static void OnSessionLaunched(CampaignGameStarter starter) =>
            RefreshAllRelations();

        private static void OnDailyTick() => RefreshAllRelations();

        private static void OnHeroCreated(Hero hero, bool isBornNaturally)
        {
            _ = isBornNaturally;

            if (GwpCommon.IsGreyWardenClanMember(hero))
            {
                RefreshRelationsForWarden(hero);
            }
            else if (IsSettlementNotable(hero))
            {
                RefreshRelationsForNotable(hero);
            }
        }

        private static void OnHeroComesOfAge(Hero hero)
        {
            if (GwpCommon.IsGreyWardenClanMember(hero))
            {
                RefreshRelationsForWarden(hero);
            }
        }

        internal static void RefreshAllRelations()
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null)
            {
                return;
            }

            List<Hero> wardens = policeClan.Heroes
                .Where(static hero => hero?.IsAlive == true)
                .ToList();
            if (wardens.Count == 0)
            {
                return;
            }

            List<Hero> notables = Settlement.All
                .SelectMany(static settlement => settlement.Notables)
                .Where(static hero => hero?.IsAlive == true && hero.IsNotable)
                .Distinct()
                .ToList();

            foreach (Hero warden in wardens)
            {
                foreach (Hero notable in notables)
                {
                    SetMaximumRelation(warden, notable);
                }
            }
        }

        private static void RefreshRelationsForWarden(Hero? warden)
        {
            if (warden?.IsAlive != true ||
                !GwpCommon.IsGreyWardenClanMember(warden))
            {
                return;
            }

            foreach (Hero notable in Settlement.All
                         .SelectMany(static settlement => settlement.Notables)
                         .Where(static hero =>
                             hero?.IsAlive == true && hero.IsNotable)
                         .Distinct())
            {
                SetMaximumRelation(warden, notable);
            }
        }

        private static void RefreshRelationsForNotable(Hero? notable)
        {
            if (!IsSettlementNotable(notable))
            {
                return;
            }

            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null)
            {
                return;
            }

            foreach (Hero warden in policeClan.Heroes.Where(static hero =>
                         hero?.IsAlive == true))
            {
                SetMaximumRelation(warden, notable!);
            }
        }

        internal static bool IsProtectedPair(Hero? first, Hero? second)
        {
            if (first == null || second == null || first == second)
            {
                return false;
            }

            return (GwpCommon.IsGreyWardenClanMember(first) &&
                    IsSettlementNotable(second)) ||
                   (GwpCommon.IsGreyWardenClanMember(second) &&
                    IsSettlementNotable(first));
        }

        internal static void SetMaximumRelation(Hero? first, Hero? second)
        {
            if (!IsProtectedPair(first, second) || first == null || second == null)
            {
                return;
            }

            if (CharacterRelationManager.GetHeroRelation(first, second) ==
                MaximumNotableRelation)
            {
                return;
            }

            CharacterRelationManager.SetHeroRelation(
                first,
                second,
                MaximumNotableRelation);
        }

        private static bool IsSettlementNotable(Hero? hero) =>
            hero?.IsNotable == true;
    }

    /// <summary>
    /// 覆盖所有经公开关系管理器写入的关系值。日结算负责补齐旧档和新生成
    /// 要人；此前缀负责保证写入发生的同一刻也不能把受保护关系降到满值以下。
    /// </summary>
    [HarmonyPatch(
        typeof(CharacterRelationManager),
        nameof(CharacterRelationManager.SetHeroRelation))]
    internal static class GreyWardenNotableRelationWritePatch
    {
        private static void Prefix(
            Hero hero1,
            Hero hero2,
            ref int value)
        {
            if (GreyWardenNotableRelationsBehavior.IsProtectedPair(hero1, hero2))
            {
                value = GreyWardenNotableRelationsBehavior.MaximumNotableRelation;
            }
        }
    }

    /// <summary>
    /// 关系动作在写值后还会广播“关系下降”事件。受保护配对直接截断该动作，
    /// 避免实际关系仍为满值却出现负面通知或让其他系统误以为关系已经下降。
    /// </summary>
    [HarmonyPatch]
    internal static class GreyWardenNotableRelationActionPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(
                typeof(ChangeRelationAction),
                "ApplyInternal",
                new[]
                {
                    typeof(Hero),
                    typeof(Hero),
                    typeof(int),
                    typeof(bool),
                    typeof(ChangeRelationAction.ChangeRelationDetail)
                });

        private static bool Prefix(
            Hero originalHero,
            Hero originalGainedRelationWith)
        {
            if (!GreyWardenNotableRelationsBehavior.IsProtectedPair(
                    originalHero,
                    originalGainedRelationWith))
            {
                return true;
            }

            GreyWardenNotableRelationsBehavior.SetMaximumRelation(
                originalHero,
                originalGainedRelationWith);
            return false;
        }
    }
}
