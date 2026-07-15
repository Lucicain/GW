using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Replaces the six founding Grey Warden lords' old all-300 skill sheet
    /// with the same strong, specialized profiles used by native Empire lords.
    /// It also rewrites existing saves once at load/session start, so the
    /// balanced party-size skills are not limited to newly created campaigns.
    /// </summary>
    public sealed class GreyWardenLeaderBalanceBehavior : CampaignBehaviorBase
    {
        private static readonly IReadOnlyDictionary<string, SkillProfile>
            Profiles = new Dictionary<string, SkillProfile>(
                StringComparer.OrdinalIgnoreCase)
            {
                // Native strong-lord profiles from SandBox's 1.4.7
                // sandbox_skill_sets.xml. Each founder has a different focus.
                ["gw_leader_0"] = SkillProfile.Knight,
                ["gw_leader_1"] = SkillProfile.Phalanx,
                ["gw_leader_2"] = SkillProfile.MountedArcher,
                ["gw_leader_3"] = SkillProfile.Quartermaster,
                ["gw_leader_4"] = SkillProfile.Politician,
                ["gw_leader_5"] = SkillProfile.Diplomat
            };

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedPartialFollowUpEvent
                .AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.OnGameLoadedEvent
                .AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent
                .AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnNewGameCreated(CampaignGameStarter starter, int index)
        {
            if (index == 0)
                ApplyProfiles();
        }

        private void OnGameLoaded(CampaignGameStarter starter) =>
            ApplyProfiles();

        private void OnSessionLaunched(CampaignGameStarter starter) =>
            ApplyProfiles();

        private static void ApplyProfiles()
        {
            if (Campaign.Current == null)
                return;

            foreach (KeyValuePair<string, SkillProfile> entry in Profiles)
            {
                Hero? hero = Hero.Find(entry.Key);
                if (hero == null)
                    continue;

                entry.Value.Apply(hero);
            }
        }

        private readonly struct SkillProfile
        {
            internal static readonly SkillProfile Knight = new(
                oneHanded: 180, twoHanded: 150, polearm: 210,
                bow: 20, crossbow: 20, throwing: 60,
                riding: 220, athletics: 150, crafting: 70,
                scouting: 140, tactics: 170, roguery: 30,
                charm: 160, leadership: 190, trade: 50,
                steward: 120, medicine: 100, engineering: 40);

            internal static readonly SkillProfile Phalanx = new(
                oneHanded: 150, twoHanded: 80, polearm: 200,
                bow: 80, crossbow: 20, throwing: 100,
                riding: 100, athletics: 180, crafting: 50,
                scouting: 60, tactics: 170, roguery: 60,
                charm: 80, leadership: 140, trade: 30,
                steward: 110, medicine: 70, engineering: 90);

            internal static readonly SkillProfile MountedArcher = new(
                oneHanded: 160, twoHanded: 60, polearm: 160,
                bow: 210, crossbow: 20, throwing: 90,
                riding: 210, athletics: 130, crafting: 40,
                scouting: 150, tactics: 170, roguery: 70,
                charm: 120, leadership: 160, trade: 30,
                steward: 100, medicine: 80, engineering: 50);

            internal static readonly SkillProfile Quartermaster = new(
                oneHanded: 140, twoHanded: 80, polearm: 100,
                bow: 70, crossbow: 60, throwing: 50,
                riding: 110, athletics: 130, crafting: 170,
                scouting: 180, tactics: 150, roguery: 80,
                charm: 140, leadership: 160, trade: 210,
                steward: 220, medicine: 190, engineering: 200);

            internal static readonly SkillProfile Politician = new(
                oneHanded: 160, twoHanded: 90, polearm: 110,
                bow: 25, crossbow: 25, throwing: 60,
                riding: 120, athletics: 70, crafting: 50,
                scouting: 110, tactics: 140, roguery: 180,
                charm: 220, leadership: 200, trade: 170,
                steward: 160, medicine: 120, engineering: 80);

            internal static readonly SkillProfile Diplomat = new(
                oneHanded: 110, twoHanded: 70, polearm: 60,
                bow: 70, crossbow: 50, throwing: 30,
                riding: 150, athletics: 90, crafting: 40,
                scouting: 160, tactics: 170, roguery: 60,
                charm: 230, leadership: 210, trade: 220,
                steward: 190, medicine: 120, engineering: 70);

            private readonly int _oneHanded;
            private readonly int _twoHanded;
            private readonly int _polearm;
            private readonly int _bow;
            private readonly int _crossbow;
            private readonly int _throwing;
            private readonly int _riding;
            private readonly int _athletics;
            private readonly int _crafting;
            private readonly int _scouting;
            private readonly int _tactics;
            private readonly int _roguery;
            private readonly int _charm;
            private readonly int _leadership;
            private readonly int _trade;
            private readonly int _steward;
            private readonly int _medicine;
            private readonly int _engineering;

            private SkillProfile(
                int oneHanded, int twoHanded, int polearm,
                int bow, int crossbow, int throwing,
                int riding, int athletics, int crafting,
                int scouting, int tactics, int roguery,
                int charm, int leadership, int trade,
                int steward, int medicine, int engineering)
            {
                _oneHanded = oneHanded;
                _twoHanded = twoHanded;
                _polearm = polearm;
                _bow = bow;
                _crossbow = crossbow;
                _throwing = throwing;
                _riding = riding;
                _athletics = athletics;
                _crafting = crafting;
                _scouting = scouting;
                _tactics = tactics;
                _roguery = roguery;
                _charm = charm;
                _leadership = leadership;
                _trade = trade;
                _steward = steward;
                _medicine = medicine;
                _engineering = engineering;
            }

            internal void Apply(Hero hero)
            {
                hero.SetSkillValue(DefaultSkills.OneHanded, _oneHanded);
                hero.SetSkillValue(DefaultSkills.TwoHanded, _twoHanded);
                hero.SetSkillValue(DefaultSkills.Polearm, _polearm);
                hero.SetSkillValue(DefaultSkills.Bow, _bow);
                hero.SetSkillValue(DefaultSkills.Crossbow, _crossbow);
                hero.SetSkillValue(DefaultSkills.Throwing, _throwing);
                hero.SetSkillValue(DefaultSkills.Riding, _riding);
                hero.SetSkillValue(DefaultSkills.Athletics, _athletics);
                hero.SetSkillValue(DefaultSkills.Crafting, _crafting);
                hero.SetSkillValue(DefaultSkills.Scouting, _scouting);
                hero.SetSkillValue(DefaultSkills.Tactics, _tactics);
                hero.SetSkillValue(DefaultSkills.Roguery, _roguery);
                hero.SetSkillValue(DefaultSkills.Charm, _charm);
                hero.SetSkillValue(DefaultSkills.Leadership, _leadership);
                hero.SetSkillValue(DefaultSkills.Trade, _trade);
                hero.SetSkillValue(DefaultSkills.Steward, _steward);
                hero.SetSkillValue(DefaultSkills.Medicine, _medicine);
                hero.SetSkillValue(DefaultSkills.Engineering, _engineering);
                hero.ClearPerks();
            }
        }
    }
}
