using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 仁慈高的罪犯交完罚金后，带自己的部队去修本国一座废村（用户裁定 2026-11-06、2026-11-07）。
    /// 工期与到达距离沿用灰袍重建，不花灰袍公库；修好时按村庄类别再记他一次被抓，叠一层震慑。
    /// 去村与在村口干活都走欲望层的访问意图（同灰袍重建），不停他的决策：本国废村多在前线，原版避敌照常起作用。
    /// 罚金在谈判当场已照数收清，这里只管修村；人没了、部队没了、村子被别人修好或成了敌境，就作罢。
    /// </summary>
    public sealed class GwpOffenderAmendsBehavior : CampaignBehaviorBase
    {
        private const string HeroIdsKey = "GWPP_AmendsHeroIds";
        private const string PartyIdsKey = "GWPP_AmendsPartyIds";
        private const string VillageIdsKey = "GWPP_AmendsVillageIds";
        private const string WorkEndHoursKey = "GWPP_AmendsWorkEndHours";
        private const string DueHoursKey = "GWPP_AmendsDueHours";

        private static GwpOffenderAmendsBehavior? _instance;
        private readonly List<Amends> _amends = new List<Amends>();

        private sealed class Amends
        {
            public string HeroId = string.Empty;
            /// <summary>领了修村意图的那支部队。领袖死后原版可能让别人接任，撤意图时按部队找，不按人找。</summary>
            public string PartyId = string.Empty;
            public string VillageId = string.Empty;
            /// <summary>0 表示还没到村开工。</summary>
            public double WorkEndHours;
            public double DueHours;
        }

        public GwpOffenderAmendsBehavior()
        {
            _instance = this;
        }

        public override void RegisterEvents() =>
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, Tend);

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? heroIds = null;
            List<string>? partyIds = null;
            List<string>? villageIds = null;
            List<double>? workEnds = null;
            List<double>? dues = null;
            if (dataStore.IsSaving)
            {
                heroIds = _amends.Select(entry => entry.HeroId).ToList();
                partyIds = _amends.Select(entry => entry.PartyId).ToList();
                villageIds = _amends.Select(entry => entry.VillageId).ToList();
                workEnds = _amends.Select(entry => entry.WorkEndHours).ToList();
                dues = _amends.Select(entry => entry.DueHours).ToList();
            }

            dataStore.SyncData(HeroIdsKey, ref heroIds);
            dataStore.SyncData(PartyIdsKey, ref partyIds);
            dataStore.SyncData(VillageIdsKey, ref villageIds);
            dataStore.SyncData(WorkEndHoursKey, ref workEnds);
            dataStore.SyncData(DueHoursKey, ref dues);
            if (!dataStore.IsLoading) return;

            _amends.Clear();
            int count = new[] { heroIds?.Count ?? 0, partyIds?.Count ?? 0, villageIds?.Count ?? 0,
                workEnds?.Count ?? 0, dues?.Count ?? 0 }.Min();
            for (int i = 0; i < count; i++)
                _amends.Add(new Amends
                {
                    HeroId = heroIds![i] ?? string.Empty,
                    PartyId = partyIds![i] ?? string.Empty,
                    VillageId = villageIds![i] ?? string.Empty,
                    WorkEndHours = workEnds![i],
                    DueHours = dues![i]
                });
        }

        /// <summary>
        /// 离他最近、属于本国的废村。原版只能劫敌国的村，他烧过的村几乎都还在敌境，所以改修本国的废村
        /// （用户裁定 2026-11-07）。犯的是村庄类案子才修村，修好记的也是村庄类被抓；本国没有废村时仁慈仍落在照数全缴上。
        /// </summary>
        internal static Settlement? RepairableVillage(CrimeRecord? crime, Hero offender)
        {
            if (crime == null || crime.CrimeCategory != GwpCrimeCategory.VillageViolence || offender.MapFaction == null)
                return null;
            Vec2 from = offender.PartyBelongedTo?.GetPosition2D ?? crime.Location;
            return Settlement.All
                .Where(settlement => settlement.MapFaction == offender.MapFaction
                    && GreyWardenVillageReconstructionBehavior.IsLootedVillage(settlement))
                .OrderBy(settlement => settlement.GetPosition2D.Distance(from))
                .FirstOrDefault();
        }

        /// <summary>谈妥修村、罚金已经收齐：把他交给每小时推进。</summary>
        internal static void Begin(Hero offender, Settlement village)
        {
            MobileParty? party = LedBy(offender, offender.PartyBelongedTo);
            if (_instance == null || party == null) return;
            _instance._amends.RemoveAll(entry => entry.HeroId == offender.StringId);
            _instance._amends.Add(new Amends
            {
                HeroId = offender.StringId,
                PartyId = party.StringId,
                VillageId = village.StringId,
                DueHours = CampaignTime.Now.ToHours + GwpTuning.FieldArrest.AmendsTravelHours
            });
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFieldArrest("OFFENDER_AMENDS_BEGUN", "offender=" + offender.StringId
                + "; party=" + party.StringId + "; village=" + village.StringId);
#endif
        }

        private void Tend()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (Amends entry in _amends.ToList())
            {
                Hero? hero = Hero.Find(entry.HeroId);
                MobileParty? party = LedBy(hero, FindParty(entry.PartyId));
                Settlement? village = Settlement.Find(entry.VillageId);
                string? lapse = hero?.IsAlive != true || hero.IsPrisoner ? "offender_gone"
                    : party == null ? "party_gone"
                    : !GreyWardenVillageReconstructionBehavior.IsLootedVillage(village) ? "village_not_ruined"
                    : FactionManager.IsAtWarAgainstFaction(party.MapFaction, village!.MapFaction) ? "village_hostile"
                    : entry.WorkEndHours <= 0d && now >= entry.DueHours ? "never_arrived"
                    : null;
                if (lapse != null)
                {
                    Finish(entry, lapse);
                    continue;
                }

                bool atVillage = party!.GetPosition2D.Distance(village!.GetPosition2D) <= GwpTuning.Reconstruction.ArrivalDistance;
                // 打仗、在军团、攻城或被困在被围的城里时不拉他走：撤掉意图交还原版，等他脱身再去；已开工的工期顺延。
                if (party.MapEvent != null || party.Army != null || party.BesiegedSettlement != null
                    || party.CurrentSettlement?.SiegeEvent != null)
                {
                    GreyWardenPartyDesireBehavior.ClearIntent(party);
                    if (entry.WorkEndHours > 0d) entry.WorkEndHours += 1d;
                    continue;
                }

                GreyWardenPartyDesireBehavior.RequestVisit(party, village, 10f);
                if (entry.WorkEndHours <= 0d)
                {
                    if (atVillage) entry.WorkEndHours = now + GwpTuning.Reconstruction.WorkHours;
                    continue;
                }
                if (!atVillage)
                {
                    entry.WorkEndHours += 1d;
                    continue;
                }
                if (now < entry.WorkEndHours) continue;

                IncreaseSettlementHealthAction.Apply(village, MathF.Max(0f, 1f - village.SettlementHitPoints));
                // 按村庄类别再记一次被抓：震慑与恢复下限照被抓一次处理（用户裁定 2026-11-07）。
                GwpFieldArrestBehavior.RegisterFieldEnforcement(hero, null);
                GwpSettlementNotices.Post(village, hero, GwpText.Get(GwpTextCatalog.gwp_notice_rebuilt));
                Finish(entry, "rebuilt");
            }
        }

        /// <summary>作罢或修好：按存下的部队撤掉修村意图，即使他已不是那支部队的领袖。</summary>
        private void Finish(Amends entry, string reason)
        {
            _amends.Remove(entry);
            GreyWardenPartyDesireBehavior.ClearIntent(FindParty(entry.PartyId));
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFieldArrest("OFFENDER_AMENDS_FINISHED", "offender=" + entry.HeroId
                + "; village=" + entry.VillageId + "; reason=" + reason);
#endif
        }

        private static MobileParty? FindParty(string partyId) =>
            string.IsNullOrEmpty(partyId) ? null : MobileParty.All.FirstOrDefault(party => party.StringId == partyId);

        private static MobileParty? LedBy(Hero? hero, MobileParty? party) =>
            party is { IsActive: true } && party.LeaderHero == hero ? party : null;
    }
}
