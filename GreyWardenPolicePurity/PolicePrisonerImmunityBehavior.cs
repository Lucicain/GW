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
            if (!GwpCommon.IsGreyWardenClanMember(prisoner))
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
            List<Hero> defeatedPoliceHeroes = new List<Hero>();
            foreach (MapEventParty? involvedParty in loserSide.Parties)
            {
                PartyBase? partyBase = involvedParty?.Party;
                MobileParty? mobileParty = partyBase?.MobileParty;
                // 只看真正带队的英雄，不退到部队的所有者：火誓者无人队、纠察队等无领主队的所有者是灰袍族长，
                // 以前它们一打输就把族长当成败将强制逃亡，原版 MakeHeroFugitiveAction 第一步就销毁她正带着的部队
                // （C#反编译 v1.4.8 MakeHeroFugitiveAction.ApplyInternal；实机诊断 2026-10-08：每次 FAITH_PARTY_LOST 同一时刻
                // gw_leader_0 HERO_BECAME_FUGITIVE，至少 6 次；她带着协力军团时留下空壳军团，下一个整点闪退）。
                Hero? leader = mobileParty?.LeaderHero ?? partyBase?.LeaderHero;
                if (leader == null || string.IsNullOrWhiteSpace(leader.StringId))
                    continue;

                if (!GwpCommon.IsGreyWardenClanMember(leader))
                    continue;

                if (!processedHeroIds.Add(leader.StringId))
                    continue;

                if (playerWonBattle)
                    continue;

                defeatedPoliceHeroes.Add(leader);
            }

            // Changing a defeated hero to fugitive can remove that hero's party from the
            // map event. Apply those changes only after loserSide.Parties is no longer
            // being enumerated.
            foreach (Hero leader in defeatedPoliceHeroes)
            {
                ForcePoliceHeroFugitive(leader);
            }
        }

        private static void ForcePoliceHeroFugitive(Hero hero)
        {
            if (!GwpCommon.IsGreyWardenClanMember(hero) || !hero.IsAlive || hero.IsFugitive)
                return;

            PartyBase? captorParty = hero.PartyBelongedToAsPrisoner;
            if (captorParty != null && captorParty.PrisonRoster.Contains(hero.CharacterObject))
                captorParty.PrisonRoster.RemoveTroop(hero.CharacterObject);

            MakeHeroFugitiveAction.Apply(hero);
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
