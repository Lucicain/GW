using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 火誓者巡逻队、紧急出动的队见敌时照原版巡逻队估形势：原版只给 PatrolPartyComponent 的队把本方优势乘上系数——对匪 1.2、对领主 0.9、
    /// 对巡逻队 0.8（C#反编译 v1.4.8 DefaultMobilePartyAIModel.GetBestInitiativeBehavior，乘完传给 CalculateInitiativeScoresForEnemy）。
    /// 我们的巡逻队是自定义队，原版认不出来，这里补上同样的乘数（用户 2026-10-08：巡逻队进攻欲望太低）。
    /// </summary>
    [HarmonyPatch(typeof(DefaultMobilePartyAIModel), "CalculateInitiativeScoresForEnemy")]
    internal static class GwpFaithPatrolInitiativePatch
    {
        private static void Prefix(MobileParty mobileParty, MobileParty enemyParty, ref float localAdvantage)
        {
            if (!GwpFireswornBehavior.IsFaithPatrol(mobileParty))
                return;
            // 玩家和灰袍可能不同势力。原版只把同势力附近部队算作友军，漏掉正在共同作战的玩家。
            // 只在确实能加入玩家这一侧时使用共享战力归属方，把玩家实际参战兵力算入。
            if (enemyParty.MapEvent != null && enemyParty.MapEvent == MobileParty.MainParty.MapEvent &&
                MobileParty.MainParty.Party.Side != TaleWorlds.Core.BattleSideEnum.None &&
                enemyParty.Party.Side != MobileParty.MainParty.Party.Side &&
                GwpFireswornBehavior.IsPatrolEnemy(mobileParty, enemyParty))
            {
                var balance = GwpStrength.Compare(mobileParty, enemyParty, includePlayer: true);
                if (balance.EnemyLocalStrength > 0f)
                    localAdvantage = balance.FriendlyLocalStrength / balance.EnemyLocalStrength;
            }
            localAdvantage *= enemyParty.IsBandit ? 1.2f : enemyParty.IsLordParty ? 0.9f : enemyParty.IsPatrolParty ? 0.8f : 1f;
        }
    }
}
