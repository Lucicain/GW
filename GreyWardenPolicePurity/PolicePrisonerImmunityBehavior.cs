using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    public sealed class PolicePrisonerImmunityBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            _ = dataStore;
        }

        private static void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (!IsPoliceHero(prisoner))
                return;

            if (IsPlayerCapturer(capturer))
                return;

            ForcePoliceHeroFugitive(prisoner);
        }

        private static void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || !mapEvent.HasWinner || mapEvent.Winner == null)
                return;

            MapEventSide? loserSide = mapEvent.Winner == mapEvent.AttackerSide
                ? mapEvent.DefenderSide
                : mapEvent.AttackerSide;
            if (loserSide == null)
                return;

            bool playerWonBattle = DoesSideContainPlayer(loserSide == mapEvent.DefenderSide
                ? mapEvent.AttackerSide
                : mapEvent.DefenderSide);
            HashSet<string> processedHeroIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MapEventParty? involvedParty in loserSide.Parties)
            {
                PartyBase? partyBase = involvedParty?.Party;
                MobileParty? mobileParty = partyBase?.MobileParty;
                Hero? leader = mobileParty?.LeaderHero
                               ?? partyBase?.LeaderHero
                               ?? mobileParty?.Owner
                               ?? partyBase?.Owner;
                if (leader == null || string.IsNullOrWhiteSpace(leader.StringId))
                    continue;

                if (!IsPoliceHero(leader))
                    continue;

                if (!processedHeroIds.Add(leader.StringId))
                    continue;

                if (playerWonBattle)
                    continue;

                ForcePoliceHeroFugitive(leader);
            }
        }

        private static void ForcePoliceHeroFugitive(Hero hero)
        {
            if (!IsPoliceHero(hero) || !hero.IsAlive || hero.IsFugitive)
                return;

            try
            {
                PartyBase? captorParty = hero.PartyBelongedToAsPrisoner;
                if (captorParty != null && captorParty.PrisonRoster.Contains(hero.CharacterObject))
                    captorParty.PrisonRoster.RemoveTroop(hero.CharacterObject);
            }
            catch
            {
            }

            try
            {
                MakeHeroFugitiveAction.Apply(hero);
            }
            catch
            {
            }
        }

        private static bool IsPoliceHero(Hero? hero)
        {
            return hero?.Clan != null &&
                   string.Equals(hero.Clan.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlayerCapturer(PartyBase? capturer)
        {
            return capturer?.MobileParty?.IsMainParty == true ||
                   capturer == MobileParty.MainParty?.Party ||
                   capturer?.LeaderHero?.IsHumanPlayerCharacter == true;
        }

        private static bool DoesSideContainPlayer(MapEventSide? side)
        {
            if (side == null)
                return false;

            foreach (MapEventParty? involvedParty in side.Parties)
            {
                PartyBase? partyBase = involvedParty?.Party;
                if (partyBase?.MobileParty?.IsMainParty == true ||
                    partyBase == MobileParty.MainParty?.Party ||
                    partyBase?.LeaderHero?.IsHumanPlayerCharacter == true)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
