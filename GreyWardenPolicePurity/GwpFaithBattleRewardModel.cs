using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 信众村自己守赢也要有收益：俘虏和财货（用户裁定 2026-10-05）。
    /// 原版把民兵、驻军排除在分赃之外，在村里的民兵也不俘虏敌兵（C#反编译 v1.4.8 DefaultBattleRewardModel：
    /// GetCaptureMemberChancesForWinnerParties、GetLootPrisonerChances、GetLootItemChancesForWinnerParties），
    /// 所以村子单独守赢时什么都拿不到。这里只补这一种情况：原版一个分赃的人都没有、而胜方里有信众村民兵，
    /// 就让民兵按原版给领主的比例拿。有领主帮守时照原版，全归帮守的领主。
    /// 民兵拿到的东西由 <see cref="GwpFireswornBehavior"/> 每小时收进地牢、货仓、公库。
    /// </summary>
    public sealed class GwpFaithBattleRewardModel : BattleRewardModel
    {
        private BattleRewardModel Inner => BaseModel;

        // MapEventSide.IsSurrendered 是 internal 字段（C#反编译 v1.4.8）。
        private static readonly AccessTools.FieldRef<MapEventSide, bool> IsSurrendered =
            AccessTools.FieldRefAccess<MapEventSide, bool>("IsSurrendered");

        private static MapEventParty? FaithMilitia(IEnumerable<MapEventParty> winners) =>
            winners.FirstOrDefault(party => party.ContributionToBattle > 0 &&
                party.Party.MobileParty?.IsMilitia == true &&
                GwpFireswornBehavior.IsPreachedVillage(party.Party.MobileParty.HomeSettlement));

        public override void GetCaptureMemberChancesForWinnerParties(MapEvent endedMapEvent,
            MBReadOnlyList<MapEventParty> winnerParties,
            out MBList<KeyValuePair<MapEventParty, float>> woundedMemberChances,
            out MBList<KeyValuePair<MapEventParty, float>> healthyMemberChances)
        {
            Inner.GetCaptureMemberChancesForWinnerParties(endedMapEvent, winnerParties,
                out woundedMemberChances, out healthyMemberChances);
            MapEventParty? militia = FaithMilitia(winnerParties);
            if (woundedMemberChances.Count > 0 || militia == null)
                return;
            // 原版：伤兵全俘，没受伤的俘四分之一，投降时全俘。
            float healthy = IsSurrendered(endedMapEvent.GetMapEventSide(endedMapEvent.DefeatedSide)) ? 1f : 0.25f;
            woundedMemberChances.Add(new KeyValuePair<MapEventParty, float>(militia, 1f));
            healthyMemberChances.Add(new KeyValuePair<MapEventParty, float>(militia, healthy));
        }

        public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootPrisonerChances(
            MBReadOnlyList<MapEventParty> winnerParties, TroopRosterElement prisonerElement)
        {
            MBReadOnlyList<KeyValuePair<MapEventParty, float>> chances =
                Inner.GetLootPrisonerChances(winnerParties, prisonerElement);
            MapEventParty? militia = FaithMilitia(winnerParties);
            if (chances.Count > 0 || militia == null || prisonerElement.Character.IsHero)
                return chances;
            return new MBList<KeyValuePair<MapEventParty, float>> { new KeyValuePair<MapEventParty, float>(militia, 1f) };
        }

        public override MBList<KeyValuePair<MapEventParty, float>> GetLootItemChancesForWinnerParties(
            MBReadOnlyList<MapEventParty> winnerParties, PartyBase defeatedParty)
        {
            MBList<KeyValuePair<MapEventParty, float>> chances =
                Inner.GetLootItemChancesForWinnerParties(winnerParties, defeatedParty);
            MapEventParty? militia = FaithMilitia(winnerParties);
            if (chances.Count == 0 && militia != null && !defeatedParty.IsSettlement)
                chances.Add(new KeyValuePair<MapEventParty, float>(militia, 0.5f)); // 原版给领主的基础份额是 0.5
            return chances;
        }

        public override float GetBannerLootChanceFromDefeatedHero(Hero defeatedHero) =>
            Inner.GetBannerLootChanceFromDefeatedHero(defeatedHero);

        public override ItemObject GetBannerRewardForWinningMapEvent(MapEvent mapEvent) =>
            Inner.GetBannerRewardForWinningMapEvent(mapEvent);

        public override int GetPlayerGainedRelationAmount(MapEvent mapEvent, Hero hero) =>
            Inner.GetPlayerGainedRelationAmount(mapEvent, hero);

        public override ExplainedNumber CalculateRenownGain(PartyBase winnerParty, float renownValueOfBattleForWinnerSide,
            float contributionShareOfWinnerParty, float renownMultiplierForWinnerSide, bool includeDescriptions) =>
            Inner.CalculateRenownGain(winnerParty, renownValueOfBattleForWinnerSide, contributionShareOfWinnerParty,
                renownMultiplierForWinnerSide, includeDescriptions);

        public override ExplainedNumber CalculateInfluenceGain(PartyBase winnerParty, float influenceValueOfBattleForWinnerSide,
            float contributionShareOfWinnerParty, float influenceMultiplierForWinnerSide, bool includeDescriptions) =>
            Inner.CalculateInfluenceGain(winnerParty, influenceValueOfBattleForWinnerSide, contributionShareOfWinnerParty,
                influenceMultiplierForWinnerSide, includeDescriptions);

        public override ExplainedNumber CalculateMoraleGainVictory(PartyBase winnerParty, float renownValueOfBattleForWinnerSide,
            float contributionShareOfWinnerParty, bool includeDescriptions) =>
            Inner.CalculateMoraleGainVictory(winnerParty, renownValueOfBattleForWinnerSide, contributionShareOfWinnerParty,
                includeDescriptions);

        public override float CalculateMoraleChangeOnRoundVictory(PartyBase party, MapEventSide partySide,
            BattleSideEnum roundWinner) =>
            Inner.CalculateMoraleChangeOnRoundVictory(party, partySide, roundWinner);

        public override int CalculateGoldLossAfterDefeat(Hero partyLeaderHero) =>
            Inner.CalculateGoldLossAfterDefeat(partyLeaderHero);

        public override EquipmentElement GetLootedItemFromTroop(CharacterObject character, float targetValue) =>
            Inner.GetLootedItemFromTroop(character, targetValue);

        public override float GetExpectedLootedItemValueFromCasualty(Hero winnerPartyLeaderHero, CharacterObject casualtyCharacter) =>
            Inner.GetExpectedLootedItemValueFromCasualty(winnerPartyLeaderHero, casualtyCharacter);

        public override int CalculatePlunderedGoldAmountFromDefeatedParty(PartyBase defeatedParty) =>
            Inner.CalculatePlunderedGoldAmountFromDefeatedParty(defeatedParty);

        public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootGoldChances(
            MBReadOnlyList<MapEventParty> winnerParties) =>
            Inner.GetLootGoldChances(winnerParties);

        public override float GetMainPartyMemberScatterChance() => Inner.GetMainPartyMemberScatterChance();

        public override float GetAITradePenalty() => Inner.GetAITradePenalty();

        public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootCasualtyChances(
            MBReadOnlyList<MapEventParty> winnerParties, PartyBase defeatedParty) =>
            Inner.GetLootCasualtyChances(winnerParties, defeatedParty);

        public override float CalculateShipDamageAfterDefeat(Ship ship) => Inner.CalculateShipDamageAfterDefeat(ship);

        public override MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> DistributeDefeatedPartyShipsAmongWinners(
            MapEvent mapEvent, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties) =>
            Inner.DistributeDefeatedPartyShipsAmongWinners(mapEvent, shipsToLoot, winnerParties);

        public override float GetSunkenShipMoraleEffect(PartyBase shipOwner, Ship ship) =>
            Inner.GetSunkenShipMoraleEffect(shipOwner, ship);

        public override float GetShipSiegeEngineHitMoraleEffect(Ship ship, SiegeEngineType siegeEngineType) =>
            Inner.GetShipSiegeEngineHitMoraleEffect(ship, siegeEngineType);

        public override Figurehead GetFigureheadLoot(MBReadOnlyList<MapEventParty> defeatedParties,
            PartyBase defeatedSideLeaderParty) =>
            Inner.GetFigureheadLoot(defeatedParties, defeatedSideLeaderParty);

        public override MBReadOnlyList<MapEventParty> GetWinnerPartiesThatCanPlunderGoldFromShips(
            MBReadOnlyList<MapEventParty> winnerParties) =>
            Inner.GetWinnerPartiesThatCanPlunderGoldFromShips(winnerParties);

        public override bool CanTroopBeTakenPrisoner(CharacterObject troop) => Inner.CanTroopBeTakenPrisoner(troop);
    }
}
