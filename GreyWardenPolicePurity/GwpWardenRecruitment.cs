using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍只招狂信者（用户裁定 2026-10-05，docs/state/firesworn.md「下一版方向」）：
    /// 兵只出自信众村——祭司的志愿兵格子，和村里直接招的最低级狂信者（见 <see cref="GwpFireswornBehavior"/>）。
    /// 原版的另外三条补兵路（普通要人的志愿兵、城里的雇佣兵、招降俘虏）对灰袍领主全部关掉；
    /// 战后收进队里的外来兵仍由纯化换成灰袍新兵（PoliceResourceManager）。
    /// </summary>
    internal static class GwpWardenRecruitment
    {
        internal static bool IsWardenBuyer(Hero? hero) => GwpCommon.IsGreyWardenClanMember(hero);

        internal static bool IsWardenParty(MobileParty? party) =>
            GwpCommon.IsGreyWardenClanParty(party);
    }

    /// <summary>
    /// 祭司的志愿兵格子：起码是灰袍新兵，出兵快（用户裁定）。只有灰袍家族成员能招；
    /// 玩家现在只走练兵官（用户裁定）。灰袍领主不能从普通要人那里招兵。
    /// </summary>
    public sealed class GwpFireswornVolunteerModel : VolunteerModel
    {
        // AddModel<T> 总会把已注册的模型设成 BaseModel（CampaignGameStarter.AddModel<T>）。
        private VolunteerModel Inner => BaseModel;

        public override int MaxVolunteerTier => Inner.MaxVolunteerTier;

        public override int MaximumIndexHeroCanRecruitFromHero(Hero buyerHero, Hero sellerHero,
            int useValueAsRelation = -101)
        {
            bool priest = GwpFireswornBehavior.IsFireswornPriest(sellerHero);
            if (priest != GwpWardenRecruitment.IsWardenBuyer(buyerHero))
                return 0;
            return Inner.MaximumIndexHeroCanRecruitFromHero(buyerHero, sellerHero, useValueAsRelation);
        }

        public override int MaximumIndexGarrisonCanRecruitFromHero(Settlement settlement, Hero sellerHero) =>
            GwpFireswornBehavior.IsFireswornPriest(sellerHero)
                ? 0
                : Inner.MaximumIndexGarrisonCanRecruitFromHero(settlement, sellerHero);

        // 原版每格每天 0.75×p^(格序+1)（p 在 0.7–0.9），一位要人一天补 2–3 个；祭司每天把 6 格补满。
        public override float GetDailyVolunteerProductionProbability(Hero hero, int index,
            Settlement settlement) =>
            GwpFireswornBehavior.IsFireswornPriest(hero)
                ? 1f
                : Inner.GetDailyVolunteerProductionProbability(hero, index, settlement);

        public override CharacterObject GetBasicVolunteer(Hero hero) =>
            GwpFireswornBehavior.IsFireswornPriest(hero)
                ? CharacterObject.Find(GwpIds.NewRecruitId)
                : Inner.GetBasicVolunteer(hero);

        public override bool CanHaveRecruits(Hero hero) => Inner.CanHaveRecruits(hero);
    }

    /// <summary>灰袍兵是信仰势力，不领饷（用户裁定）。其余兵、英雄照原版算。</summary>
    public sealed class GwpWardenWageModel : PartyWageModel
    {
        private PartyWageModel Inner => BaseModel;

        public override int MaxWagePaymentLimit => Inner.MaxWagePaymentLimit;

        public override int GetCharacterWage(CharacterObject character) =>
            GwpCommon.IsGreyWardenTroop(character) ? 0 : Inner.GetCharacterWage(character);

        // 原版直接读 character.TroopWage 逐格累加，所以把灰袍兵从名册里拿掉再交给原版，
        // 英雄工资和各项 perk 照原版。不挂 OwnerParty 的名册加英雄没有副作用（TroopRoster.AddToCountsAtIndex）。
        public override ExplainedNumber GetTotalWage(MobileParty mobileParty, TroopRoster troopRoster,
            bool includeDescriptions = false)
        {
            bool hasWardenTroops = false;
            for (int i = 0; i < troopRoster.Count && !hasWardenTroops; i++)
                hasWardenTroops = GwpCommon.IsGreyWardenTroop(troopRoster.GetCharacterAtIndex(i));
            if (!hasWardenTroops)
                return Inner.GetTotalWage(mobileParty, troopRoster, includeDescriptions);

            TroopRoster paid = TroopRoster.CreateDummyTroopRoster();
            for (int i = 0; i < troopRoster.Count; i++)
            {
                TroopRosterElement element = troopRoster.GetElementCopyAtIndex(i);
                if (!GwpCommon.IsGreyWardenTroop(element.Character))
                    paid.AddToCounts(element.Character, element.Number, false, element.WoundedNumber);
            }
            return Inner.GetTotalWage(mobileParty, paid, includeDescriptions);
        }

        public override ExplainedNumber GetTroopRecruitmentCost(CharacterObject troop, Hero buyerHero,
            bool withoutItemCost = false) =>
            Inner.GetTroopRecruitmentCost(troop, buyerHero, withoutItemCost);
    }

    /// <summary>灰袍领主不招降俘虏：俘虏送去信众村烧（用户裁定）。</summary>
    public sealed class GwpWardenPrisonerRecruitmentModel : PrisonerRecruitmentCalculationModel
    {
        private PrisonerRecruitmentCalculationModel Inner => BaseModel;

        public override int GetConformityNeededToRecruitPrisoner(CharacterObject character) =>
            Inner.GetConformityNeededToRecruitPrisoner(character);

        public override ExplainedNumber GetConformityChangePerHour(PartyBase party, CharacterObject character) =>
            Inner.GetConformityChangePerHour(party, character);

        public override int GetPrisonerRecruitmentMoraleEffect(PartyBase party, CharacterObject character,
            int num) =>
            Inner.GetPrisonerRecruitmentMoraleEffect(party, character, num);

        public override bool IsPrisonerRecruitable(PartyBase party, CharacterObject character,
            out int conformityNeeded) =>
            Inner.IsPrisonerRecruitable(party, character, out conformityNeeded);

        public override bool ShouldPartyRecruitPrisoners(PartyBase party) =>
            !GwpWardenRecruitment.IsWardenParty(party.MobileParty) && Inner.ShouldPartyRecruitPrisoners(party);

        public override int CalculateRecruitableNumber(PartyBase party, CharacterObject character) =>
            Inner.CalculateRecruitableNumber(party, character);
    }

    /// <summary>
    /// 灰袍领主不雇城里的雇佣兵。只挡真正雇人的那一步：原版领主从要人那里招志愿兵
    /// （RecruitVolunteersFromNotable）也是在 CheckRecruiting 里面调的，整段挡掉祭司就招不到兵了
    /// （C#反编译 v1.4.8 RecruitmentCampaignBehavior.CheckRecruiting；实机诊断 2026-10-06：
    /// 祭司格子一周都是满的，灰袍领主进村一个都没招）。
    /// </summary>
    [HarmonyPatch(typeof(RecruitmentCampaignBehavior), "ApplyRecruitMercenary")]
    internal static class GwpWardenNoMercenariesPatch
    {
        private static bool Prefix(MobileParty side1Party) =>
            !(side1Party.IsLordParty && GwpWardenRecruitment.IsWardenParty(side1Party));
    }

    /// <summary>
    /// 原版估算「这里能招几个兵」时不看招不招得到，只数每位要人的前 4 格（C#反编译 v1.4.8
    /// AiVisitSettlementBehavior.GetApproximateVolunteersCanBeRecruitedDataFromSettlement）。
    /// 灰袍只在信众村招兵，所以别处一律算 0；信众村只数祭司的格子，再加上能直接招的狂信者，
    /// 招兵的意愿就回到信众村（用户裁定）。
    /// 第二个值是平均工资，原版拿它去除工资预算，再用 MathF.Floor 转成整数，作为能养得起的人数上限
    /// （AiVisitSettlementBehavior.AiHourlyTick）。灰袍兵不领饷，平均工资是 0 时得到正无穷，转整数成了
    /// -2147483648，招兵意愿整个变成负数（C#反编译、运行时反射 2026-10-07）。所以报 1：不领饷，人数上限就是预算本身，
    /// 招多少由兵源和空位决定。
    /// </summary>
    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "GetApproximateVolunteersCanBeRecruitedDataFromSettlement")]
    internal static class GwpWardenRecruitDesirePatch
    {
        private static void Postfix(Hero hero, Settlement settlement, ref (int, float) __result)
        {
            if (GwpWardenRecruitment.IsWardenBuyer(hero))
                __result = (GwpFireswornBehavior.RecruitsAvailableToWardens(settlement, hero), 1f);
        }
    }

    /// <summary>
    /// 灰袍领主的普通俘虏送去信众村烧，不在城里卖（用户裁定）。原版 AI 部队进入不交战的城堡、城镇就把普通俘虏和
    /// 敌对英雄全部卖掉（C#反编译 v1.4.8 PartiesSellPrisonerCampaignBehavior.OnSettlementEntered）。
    /// 灰袍领主照原版只卖英雄俘虏，普通俘虏留着。
    /// </summary>
    [HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior), "OnSettlementEntered")]
    internal static class GwpWardenKeepPrisonersPatch
    {
        private static bool Prefix(MobileParty mobileParty, Settlement settlement)
        {
            if (mobileParty == null || !mobileParty.IsLordParty || !GwpWardenRecruitment.IsWardenParty(mobileParty))
                return true;
            if (!settlement.IsFortification || mobileParty.IsDisbanding ||
                mobileParty.MapFaction.IsAtWarWith(settlement.MapFaction))
                return false;
            TroopRoster heroes = TroopRoster.CreateDummyTroopRoster();
            foreach (TroopRosterElement element in mobileParty.PrisonRoster.GetTroopRoster())
                if (element.Character.IsHero && !element.Character.IsPlayerCharacter &&
                    element.Character.HeroObject.MapFaction.IsAtWarWith(settlement.MapFaction))
                    heroes.Add(element);
            if (heroes.Count > 0)
                SellPrisonersAction.ApplyForSelectedPrisoners(mobileParty.Party, settlement.Party, heroes);
            return false;
        }
    }

    /// <summary>
    /// 灰袍领主的东西捐给信众村，不在城里卖（用户裁定：领主给村子的一律算捐赠，由村子卖进城）。
    /// 原版领主进城会把背包里非口粮的东西卖掉（C#反编译 v1.4.8 PartiesSellLootCampaignBehavior.OnSettlementEntered）。
    /// </summary>
    [HarmonyPatch(typeof(PartiesSellLootCampaignBehavior), nameof(PartiesSellLootCampaignBehavior.OnSettlementEntered))]
    internal static class GwpWardenKeepLootPatch
    {
        private static bool Prefix(MobileParty mobileParty) =>
            mobileParty == null || !mobileParty.IsLordParty || !GwpWardenRecruitment.IsWardenParty(mobileParty);
    }
}
