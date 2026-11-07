using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 勇武低的罪犯带着自己的部队躲进本国的城，一直不出来，部队不解散（用户裁定 2026-11-06）。
    /// 路上走欲望层的访问意图，原版避敌照常；进城后停住他的决策，让部队留在城里。
    /// 躲在城里的人按住城案办（<see cref="CrimeRecord.ShelterOf"/>）：灰袍派城内办案队，或者玩家进城搜捕；
    /// 案子结了、人没了、部队没了，或灰袍与那座城开战（城内办案队到不了）就放开。
    /// </summary>
    public sealed class GwpOffenderHidingBehavior : CampaignBehaviorBase
    {
        private const string HeroIdsKey = "GWPP_HideHeroIds";
        private const string PartyIdsKey = "GWPP_HidePartyIds";
        private const string TownIdsKey = "GWPP_HideTownIds";
        private const string DueHoursKey = "GWPP_HideDueHours";

        private static GwpOffenderHidingBehavior? _instance;
        private readonly List<Hideout> _hideouts = new List<Hideout>();

        private sealed class Hideout
        {
            public string HeroId = string.Empty;
            /// <summary>躲城的那支部队；领袖换人时按部队放开。</summary>
            public string PartyId = string.Empty;
            public string TownId = string.Empty;
            /// <summary>还没进城时的到达期限；在城里时每小时顺延。</summary>
            public double DueHours;
        }

        public GwpOffenderHidingBehavior()
        {
            _instance = this;
        }

        public override void RegisterEvents() =>
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, Tend);

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? heroIds = null;
            List<string>? partyIds = null;
            List<string>? townIds = null;
            List<double>? dues = null;
            if (dataStore.IsSaving)
            {
                heroIds = _hideouts.Select(entry => entry.HeroId).ToList();
                partyIds = _hideouts.Select(entry => entry.PartyId).ToList();
                townIds = _hideouts.Select(entry => entry.TownId).ToList();
                dues = _hideouts.Select(entry => entry.DueHours).ToList();
            }

            dataStore.SyncData(HeroIdsKey, ref heroIds);
            dataStore.SyncData(PartyIdsKey, ref partyIds);
            dataStore.SyncData(TownIdsKey, ref townIds);
            dataStore.SyncData(DueHoursKey, ref dues);
            if (!dataStore.IsLoading) return;

            _hideouts.Clear();
            int count = new[] { heroIds?.Count ?? 0, partyIds?.Count ?? 0, townIds?.Count ?? 0, dues?.Count ?? 0 }.Min();
            for (int i = 0; i < count; i++)
                _hideouts.Add(new Hideout
                {
                    HeroId = heroIds![i] ?? string.Empty,
                    PartyId = partyIds![i] ?? string.Empty,
                    TownId = townIds![i] ?? string.Empty,
                    DueHours = dues![i]
                });
        }

        /// <summary>他正带着部队躲在城里：那座城就是他的住处，按住城案办。</summary>
        internal static Settlement? HideoutOf(Hero? hero)
        {
            if (_instance == null || hero == null || !hero.IsAlive || hero.IsPrisoner) return null;
            Hideout? entry = _instance._hideouts.FirstOrDefault(row => row.HeroId == hero.StringId);
            MobileParty? party = hero.PartyBelongedTo;
            return entry != null && party?.IsActive == true && party.StringId == entry.PartyId
                && party.LeaderHero == hero && party.CurrentSettlement is { IsFortification: true } town
                && town.StringId == entry.TownId ? town : null;
        }

        /// <summary>他答应了躲城（还在路上或已进城）：野外这一套不再追他。</summary>
        internal static bool IsHiding(string heroId) =>
            _instance?._hideouts.Any(entry => entry.HeroId == heroId) == true;

        /// <returns>他要去躲的城；躲不成时为空。</returns>
        internal static Settlement? Begin(Hero offender)
        {
            MobileParty? party = offender.PartyBelongedTo is { IsActive: true } led && led.LeaderHero == offender ? led : null;
            Settlement? town = GwpCommon.NearestOwnFortification(party);
            if (_instance == null || party == null || town == null) return null;
            // 他若还欠着修村，躲城优先，修村作罢。
            GwpOffenderAmendsBehavior.Cancel(offender.StringId);
            _instance._hideouts.RemoveAll(entry => entry.HeroId == offender.StringId);
            _instance._hideouts.Add(new Hideout
            {
                HeroId = offender.StringId,
                PartyId = party.StringId,
                TownId = town.StringId,
                DueHours = CampaignTime.Now.ToHours + GwpTuning.FieldArrest.AmendsTravelHours
            });
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFieldArrest("OFFENDER_HIDE_BEGUN", "offender=" + offender.StringId
                + "; party=" + party.StringId + "; town=" + town.StringId);
#endif
            return town;
        }

        private void Tend()
        {
            double now = CampaignTime.Now.ToHours;
            Clan? wardens = PoliceStats.GetPoliceClan();
            foreach (Hideout entry in _hideouts.ToList())
            {
                Hero? hero = Hero.Find(entry.HeroId);
                MobileParty? party = GwpCommon.FindParty(entry.PartyId);
                Settlement? town = Settlement.Find(entry.TownId);
                bool inside = party != null && party.CurrentSettlement == town;
                string? lapse = hero?.IsAlive != true || hero.IsPrisoner ? "offender_gone"
                    : party?.IsActive != true || party.LeaderHero != hero ? "party_gone"
                    : CrimePool.GetRecord(hero)?.HasOpenCase != true ? "case_closed"
                    : town == null || FactionManager.IsAtWarAgainstFaction(party.MapFaction, town.MapFaction) ? "town_lost"
                    : wardens != null && FactionManager.IsAtWarAgainstFaction(wardens, town.MapFaction) ? "wardens_at_war"
                    : !inside && now >= entry.DueHours ? "never_arrived"
                    : null;
                if (lapse != null)
                {
                    Finish(entry, lapse);
                    continue;
                }

                if (inside)
                {
                    // 进了城就停住，部队留在城里不出来；在城里时到达期限一直顺延，被赶出城后还能回来。
                    GreyWardenPartyDesireBehavior.ClearIntent(party);
                    GwpCommon.HoldPartyAt(party!, town!);
                    entry.DueHours = now + GwpTuning.FieldArrest.AmendsTravelHours;
                    continue;
                }
                // 还没进城时那座城被围了，就改去另一座。
                if (town!.SiegeEvent != null)
                {
                    Settlement? other = GwpCommon.NearestOwnFortification(party);
                    if (other == null)
                    {
                        Finish(entry, "no_town");
                        continue;
                    }
                    entry.TownId = other.StringId;
                    town = other;
                }
                if (party!.MapEvent != null) continue;
                // 被赶出城后，原版存着的留城冻结也要解除，才能再次参加访问意图的决策。
                if (party.Ai.DoNotMakeNewDecisions) GwpCommon.TryResetAi(party);
                // 成员离队去躲；统帅走原版访问流程，附属部队由原版跟随进城。
                if (party.Army != null && party.Army.LeaderParty != party)
                    party.Army = null;
                GreyWardenPartyDesireBehavior.RequestVisit(party, town!, 10f);
            }
        }

        private void Finish(Hideout entry, string reason)
        {
            _hideouts.Remove(entry);
            MobileParty? party = GwpCommon.FindParty(entry.PartyId);
            GreyWardenPartyDesireBehavior.ClearIntent(party);
            GwpCommon.TryResetAi(party);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFieldArrest("OFFENDER_HIDE_FINISHED", "offender=" + entry.HeroId
                + "; town=" + entry.TownId + "; reason=" + reason);
#endif
        }
    }
}
