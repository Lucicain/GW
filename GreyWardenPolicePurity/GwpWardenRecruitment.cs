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
    /// 原版补兵路（志愿兵、雇佣兵、招降俘虏）照常；非灰袍兵的纯化由 GwpWardenPurification 负责。
    /// 信众村的祭司格子放精锐，直接招守村队则低阶优先，都给村庄留底（用户裁定 2026-10-15）。
    /// 只有灰袍领主能从信众村这两条路招兵；灰袍兵不领饷。
    /// </summary>
    internal static class GwpWardenRecruitment
    {
        internal static bool IsWardenBuyer(Hero? hero) => GwpCommon.IsGreyWardenClanMember(hero);

        internal static bool IsWardenParty(MobileParty? party) =>
            GwpCommon.IsGreyWardenClanParty(party);
    }

    /// <summary>
    /// 祭司的志愿兵格子：格子里是守村队里最精锐、配装最好的灰袍兵；格子数、什么时候补、怎么排都照原版
    /// （用户裁定 2026-10-07）。原版给空格子补的「基础志愿兵」换成守村队里最精锐的兵种，守村队空了就不补；
    /// 具体放谁由 GwpFireswornBehavior.ReconcilePriestSlots 每小时照守村队重排。
    /// 祭司的格子只有灰袍家族成员能招；玩家现在只走练兵官（用户裁定）。别的要人照原版，灰袍也能招（用户裁定 2026-10-07）。
    /// </summary>
    public sealed class GwpFireswornVolunteerModel : VolunteerModel
    {
        // AddModel<T> 总会把已注册的模型设成 BaseModel（CampaignGameStarter.AddModel<T>）。
        private VolunteerModel Inner => BaseModel;

        public override int MaxVolunteerTier => Inner.MaxVolunteerTier;

        public override int MaximumIndexHeroCanRecruitFromHero(Hero buyerHero, Hero sellerHero,
            int useValueAsRelation = -101)
        {
            if (GwpFireswornBehavior.IsFireswornPriest(sellerHero) && !GwpWardenRecruitment.IsWardenBuyer(buyerHero))
                return 0;
            return Inner.MaximumIndexHeroCanRecruitFromHero(buyerHero, sellerHero, useValueAsRelation);
        }

        public override int MaximumIndexGarrisonCanRecruitFromHero(Settlement settlement, Hero sellerHero) =>
            GwpFireswornBehavior.IsFireswornPriest(sellerHero)
                ? 0
                : Inner.MaximumIndexGarrisonCanRecruitFromHero(settlement, sellerHero);

        public override float GetDailyVolunteerProductionProbability(Hero hero, int index,
            Settlement settlement) =>
            Inner.GetDailyVolunteerProductionProbability(hero, index, settlement);

        // 返回 null 时原版那一格留空（RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement 直接赋值）。
        public override CharacterObject GetBasicVolunteer(Hero hero) =>
            GwpFireswornBehavior.IsFireswornPriest(hero)
                ? GwpFireswornBehavior.BestGuardTroop(hero)!
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
}
