using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 我们建的无领主办差队不吃饭（用户裁定 2026-09-25）。
    ///
    /// 只关 <c>DoesPartyConsumeFood</c>：原版每日进食由它把门，关掉后
    /// <c>RemainingFoodPercentage</c> 不再变，队伍永远不会进入饥饿（饥饿每天把 25%
    /// 普通兵打成伤兵）。伙食量 <c>FoodChange</c> 照原版算、不改成 0——
    /// <c>GetNumDaysForFoodToLast</c> 要除以它，军团算粮会把各队天数相加。
    ///
    /// 包在已注册的食物模型外面（<c>AddModel&lt;T&gt;</c> 把它设成 BaseModel），
    /// NavalDLC 的海上减耗照样生效。
    /// </summary>
    public sealed class GwpLeaderlessFoodModel : MobilePartyFoodConsumptionModel
    {
        private MobilePartyFoodConsumptionModel Inner =>
            BaseModel ?? (_fallback ??= new DefaultMobilePartyFoodConsumptionModel());

        private MobilePartyFoodConsumptionModel? _fallback;

        public override int NumberOfMenOnMapToEatOneFood => Inner.NumberOfMenOnMapToEatOneFood;

        public override ExplainedNumber CalculateDailyBaseFoodConsumptionf(MobileParty party,
            bool includeDescription = false) =>
            Inner.CalculateDailyBaseFoodConsumptionf(party, includeDescription);

        public override ExplainedNumber CalculateDailyFoodConsumptionf(MobileParty party,
            ExplainedNumber baseConsumption) =>
            Inner.CalculateDailyFoodConsumptionf(party, baseConsumption);

        public override bool DoesPartyConsumeFood(MobileParty mobileParty) =>
            !GwpCommon.IsLeaderlessDutyParty(mobileParty) &&
            Inner.DoesPartyConsumeFood(mobileParty);
    }
}
