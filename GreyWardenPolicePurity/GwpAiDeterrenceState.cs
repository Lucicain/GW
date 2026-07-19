using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 威慑状态直接存放在每位领主唯一的永久案底记录中。
    /// 本人被捕与家族受震慑分别累计、按总量共同衰退，案底次数永不因威慑归零而删除。
    /// </summary>
    internal static class GwpAiDeterrenceState
    {
        internal readonly struct DeterrenceDetails
        {
            public bool HasEntry { get; init; }
            public float DirectPenalty { get; init; }
            public float SharedPenalty { get; init; }
            public float EffectivePenalty { get; init; }
            public int TotalCrimeCount { get; init; }
            public int TotalArrestCount { get; init; }
            public int EnforcementCount { get; init; }
            public int SharedDeterrenceCount { get; init; }
            public float RaidScoreMultiplier { get; init; }
            public float DaysSinceLastEnforcement { get; init; }
            public string MapStatus { get; init; }
            public string MapLocation { get; init; }
        }

        public static void ClearAll()
        {
            foreach (CrimeRecord record in CrimePool.LedgerRecords)
            {
                record.DirectDeterrencePoints = 0f;
                record.SharedDeterrencePoints = 0f;
                record.SharedDeterrenceCount = 0;
                record.LastDeterrenceUpdatedHours = 0f;
                record.LastEnforcementHours = 0f;
            }
        }

        /// <summary>登记一次由灰袍实际实施的抓捕，并返回本次新增的本人威慑。</summary>
        public static float RegisterPoliceArrest(Hero leader, MobileParty? sourceParty = null)
        {
            if (!CanTrack(leader)) return 0f;

            CrimeRecord record = CrimePool.GetOrCreateRecord(leader);
            if (sourceParty != null)
                record.Offender = sourceParty;

            UpdateDecay(record, leader, updateRecord: true);
            int arrestCount = CrimePool.RecordArrest(leader);
            float desiredGain = MathF.Min((float)arrestCount, GwpTuning.Deterrence.MaxPenaltyGainPerCapture);
            float previousDirect = record.DirectDeterrencePoints;
            record.DirectDeterrencePoints = MathF.Min(
                GwpTuning.Deterrence.RaidPenaltyCap,
                previousDirect + desiredGain);
            float actualGain = record.DirectDeterrencePoints - previousDirect;

            // 本人实际被捕的直接经验优先保留；总量触顶时只压缩家族转述造成的震慑。
            float remainingSharedRoom = MathF.Max(
                0f,
                GwpTuning.Deterrence.RaidPenaltyCap - record.DirectDeterrencePoints);
            record.SharedDeterrencePoints = MathF.Min(record.SharedDeterrencePoints, remainingSharedRoom);
            record.LastDeterrenceUpdatedHours = (float)CampaignTime.Now.ToHours;
            record.LastEnforcementHours = record.LastDeterrenceUpdatedHours;
            return actualGain;
        }

        public static float RegisterSharedFamilyDeterrence(Hero leader, float penaltyGain)
        {
            if (!CanTrack(leader) || penaltyGain <= 0f) return 0f;

            CrimeRecord record = CrimePool.GetOrCreateRecord(leader);
            UpdateDecay(record, leader, updateRecord: true);

            float total = record.DirectDeterrencePoints + record.SharedDeterrencePoints;
            float actualGain = MathF.Min(
                penaltyGain,
                MathF.Max(0f, GwpTuning.Deterrence.RaidPenaltyCap - total));
            if (actualGain <= 0f)
                return total;

            record.SharedDeterrencePoints += actualGain;
            record.SharedDeterrenceCount++;
            record.LastDeterrenceUpdatedHours = (float)CampaignTime.Now.ToHours;
            record.LastEnforcementHours = record.LastDeterrenceUpdatedHours;
            return record.DirectDeterrencePoints + record.SharedDeterrencePoints;
        }

        public static float GetRaidScoreMultiplier(MobileParty party) => GetCrimeDesireMultiplier(party);

        public static float GetCrimeDesireMultiplier(MobileParty? party)
        {
            if (party == null) return 1f;
            if (IsGreyWardenPoliceParty(party.ActualClan)) return 0f;
            return GetCrimeDesireMultiplier(party.LeaderHero);
        }

        public static float GetCrimeDesireMultiplier(Hero? hero)
        {
            if (Campaign.Current == null || hero == null) return 1f;
            if (IsGreyWardenPoliceHero(hero)) return 0f;

            float penalty = GetCurrentPenalty(hero);
            if (penalty <= GwpTuning.Deterrence.ForgetThreshold) return 1f;

            float multiplier = MathF.Pow(GwpTuning.Deterrence.RaidScoreMultiplierPerPoint, penalty);
            return MathF.Max(GwpTuning.Deterrence.RaidScoreMultiplierFloor, multiplier);
        }

        public static float GetCurrentPenalty(Hero? hero)
        {
            CrimeRecord? record = CrimePool.GetRecord(hero);
            if (record == null || hero == null) return 0f;
            GetEffectiveComponents(record, hero, out float direct, out float shared);
            return direct + shared;
        }

        public static DeterrenceDetails GetDeterrenceDetails(Hero? hero)
        {
            if (hero == null)
                return EmptyDetails();

            CrimeRecord? record = CrimePool.GetRecord(hero);
            string status = BuildTrackingStatus(hero);
            string location = BuildTrackingLocation(hero);
            if (record == null)
            {
                DeterrenceDetails empty = EmptyDetails();
                return new DeterrenceDetails
                {
                    MapStatus = status,
                    MapLocation = location,
                    RaidScoreMultiplier = empty.RaidScoreMultiplier
                };
            }

            GetEffectiveComponents(record, hero, out float direct, out float shared);
            float total = direct + shared;
            float now = (float)CampaignTime.Now.ToHours;
            float days = record.LastEnforcementHours > 0f
                ? MathF.Max(0f, (now - record.LastEnforcementHours) / CampaignTime.HoursInDay)
                : 0f;

            return new DeterrenceDetails
            {
                HasEntry = true,
                DirectPenalty = direct,
                SharedPenalty = shared,
                EffectivePenalty = total,
                TotalCrimeCount = record.TotalCrimeCount,
                TotalArrestCount = record.TotalArrestCount,
                EnforcementCount = record.TotalArrestCount,
                SharedDeterrenceCount = record.SharedDeterrenceCount,
                RaidScoreMultiplier = GetCrimeDesireMultiplier(hero),
                DaysSinceLastEnforcement = days,
                MapStatus = status,
                MapLocation = location
            };
        }

        private static DeterrenceDetails EmptyDetails() => new DeterrenceDetails
        {
            HasEntry = false,
            RaidScoreMultiplier = 1f,
            MapStatus = GwpText.Get("{=gwp_gwpaideterrencestate_001}Unknown status"),
            MapLocation = GwpText.Get("{=gwp_gwpaideterrencestate_002}Unknown location")
        };

        /// <summary>
        /// 按主导震慑来源、总震慑等级、领主性格权重与玩家灰袍身份生成回应。
        /// 性格仅改变各说辞被抽中的概率，不再以固定优先级锁死台词。
        /// </summary>
        public static bool TryBuildPainDialogue(Hero hero, out TextObject intro, out TextObject followup)
        {
            intro = new TextObject(string.Empty);
            followup = new TextObject(string.Empty);
            CrimeRecord? record = CrimePool.GetRecord(hero);
            if (record == null) return false;

            GetEffectiveComponents(record, hero, out float direct, out float shared);
            float total = direct + shared;
            if (total < GwpTuning.Deterrence.ActiveDialogueThreshold)
                return false;

            DeterrenceSource source = direct >= shared && direct > GwpTuning.Deterrence.ForgetThreshold
                ? DeterrenceSource.Personal
                : DeterrenceSource.Family;
            DeterrenceTier tier = total <= 3f
                ? DeterrenceTier.Low
                : total <= 6f
                    ? DeterrenceTier.Medium
                    : DeterrenceTier.High;
            DeterrenceVoice voice = SelectWeightedVoice(hero);
            bool playerIsWarden = IsPlayerGreyWarden();

            string name = hero.Name?.ToString() ?? GwpText.Get("{=gwp_gwpaideterrencestate_003}This person");
            intro = GwpAiDeterrenceDialogueCatalog.GetIntro(source, tier);
            intro.SetTextVariable("HERO_NAME", name);
            followup = new TextObject(GwpAiDeterrenceDialogueCatalog.GetResponse(
                source,
                tier,
                voice,
                playerIsWarden));
            return true;
        }

        private static DeterrenceVoice SelectWeightedVoice(Hero hero)
        {
            int honor = hero.GetTraitLevel(DefaultTraits.Honor);
            int valor = hero.GetTraitLevel(DefaultTraits.Valor);
            int mercy = hero.GetTraitLevel(DefaultTraits.Mercy);
            int generosity = hero.GetTraitLevel(DefaultTraits.Generosity);
            int calculating = hero.GetTraitLevel(DefaultTraits.Calculating);

            int totalWeight = Math.Abs(honor) + Math.Abs(valor) + Math.Abs(mercy) +
                              Math.Abs(generosity) + Math.Abs(calculating);
            if (totalWeight <= 0)
                return DeterrenceVoice.Neutral;

            float roll = MBRandom.RandomFloat * totalWeight;
            if (TakeTraitWeight(ref roll, honor))
                return honor > 0 ? DeterrenceVoice.HonorHigh : DeterrenceVoice.HonorLow;
            if (TakeTraitWeight(ref roll, valor))
                return valor > 0 ? DeterrenceVoice.ValorHigh : DeterrenceVoice.ValorLow;
            if (TakeTraitWeight(ref roll, mercy))
                return mercy > 0 ? DeterrenceVoice.MercyHigh : DeterrenceVoice.MercyLow;
            if (TakeTraitWeight(ref roll, generosity))
                return generosity > 0 ? DeterrenceVoice.GenerosityHigh : DeterrenceVoice.GenerosityLow;
            return calculating > 0
                ? DeterrenceVoice.CalculatingHigh
                : DeterrenceVoice.CalculatingLow;
        }

        private static bool TakeTraitWeight(ref float roll, int traitLevel)
        {
            int weight = Math.Abs(traitLevel);
            if (weight <= 0) return false;
            if (roll < weight) return true;
            roll -= weight;
            return false;
        }

        private static bool IsPlayerGreyWarden()
        {
            if (string.Equals(
                    Clan.PlayerClan?.StringId,
                    GwpIds.PoliceClanId,
                    StringComparison.OrdinalIgnoreCase))
                return true;

            PlayerBountyBehavior? bountyBehavior =
                Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            return bountyBehavior?.IsRecruitedByGreyWardens == true;
        }

        public static void DailyCleanup()
        {
            foreach (CrimeRecord record in CrimePool.LedgerRecords.ToList())
            {
                Hero? hero = record.OffenderHero;
                if (hero == null) continue;
                UpdateDecay(record, hero, updateRecord: true);
            }
        }

        /// <summary>威慑现已随案底账本统一序列化，避免重复存档。</summary>
        public static void SyncData(IDataStore dataStore) { }

        private static void GetEffectiveComponents(CrimeRecord record, Hero hero, out float direct, out float shared)
        {
            float total = UpdateDecay(record, hero, updateRecord: false);
            float storedTotal = record.DirectDeterrencePoints + record.SharedDeterrencePoints;
            if (storedTotal <= 0f || total <= 0f)
            {
                direct = 0f;
                shared = 0f;
                return;
            }

            float scale = total / storedTotal;
            direct = record.DirectDeterrencePoints * scale;
            shared = record.SharedDeterrencePoints * scale;
        }

        /// <summary>两类威慑按当前占比共同衰退，避免分别扣减造成来源比重突变。</summary>
        private static float UpdateDecay(CrimeRecord record, Hero hero, bool updateRecord)
        {
            float storedTotal = MathF.Max(0f, record.DirectDeterrencePoints) +
                                MathF.Max(0f, record.SharedDeterrencePoints);
            if (storedTotal <= 0f) return 0f;

            float now = (float)CampaignTime.Now.ToHours;
            float elapsedDays = CanRecoverPenalty(hero)
                ? MathF.Max(0f, (now - record.LastDeterrenceUpdatedHours) / CampaignTime.HoursInDay)
                : 0f;
            float effective = MathF.Max(0f, storedTotal - elapsedDays * GetRecoveryPerDay(hero));

            if (updateRecord)
            {
                float scale = storedTotal > 0f ? effective / storedTotal : 0f;
                record.DirectDeterrencePoints *= scale;
                record.SharedDeterrencePoints *= scale;
                record.LastDeterrenceUpdatedHours = now;
                if (effective <= GwpTuning.Deterrence.ForgetThreshold)
                {
                    record.DirectDeterrencePoints = 0f;
                    record.SharedDeterrencePoints = 0f;
                }
            }

            return effective;
        }

        private static bool CanRecoverPenalty(Hero hero)
        {
            if (hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null) return false;
            if (hero.PartyBelongedTo?.IsActive == true) return true;
            return hero.CurrentSettlement != null || hero.StayingInSettlement != null;
        }

        private static float GetRecoveryPerDay(Hero hero)
        {
            float recovery = GwpTuning.Deterrence.BaseRecoveryPerDay;
            recovery += hero.GetTraitLevel(DefaultTraits.Valor) * 0.025f;
            recovery -= hero.GetTraitLevel(DefaultTraits.Honor) * 0.02f;
            recovery -= hero.GetTraitLevel(DefaultTraits.Mercy) * 0.02f;
            recovery -= hero.GetTraitLevel(DefaultTraits.Calculating) * 0.015f;
            return MBMath.ClampFloat(
                recovery,
                GwpTuning.Deterrence.MinRecoveryPerDay,
                GwpTuning.Deterrence.MaxRecoveryPerDay);
        }

        private static bool CanTrack(Hero? hero) =>
            Campaign.Current != null && hero != null && hero != Hero.MainHero &&
            !string.IsNullOrWhiteSpace(hero.StringId) && !IsGreyWardenPoliceHero(hero);

        private static bool IsGreyWardenPoliceHero(Hero? hero) =>
            IsGreyWardenPoliceParty(hero?.Clan);

        private static bool IsGreyWardenPoliceParty(Clan? clan) =>
            string.Equals(clan?.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase);

        private static string BuildTrackingLocation(Hero hero)
        {
            if (hero.IsPrisoner && hero.PartyBelongedToAsPrisoner != null)
            {
                PartyBase captor = hero.PartyBelongedToAsPrisoner;
                if (captor.IsSettlement && captor.Settlement != null)
                    return captor.Settlement.Name.ToString();
                if (captor.MobileParty != null)
                    return captor.MobileParty.Name.ToString();
            }

            MobileParty? party = hero.PartyBelongedTo;
            if (party?.CurrentSettlement != null) return party.CurrentSettlement.Name.ToString();
            if (party != null) return GwpCommon.FindNearestTown(party)?.Name?.ToString()
                                      ?? GwpText.Get("{=gwp_gwpaideterrencestate_032}Unknown location");
            return (hero.CurrentSettlement ?? hero.StayingInSettlement)?.Name?.ToString()
                   ?? GwpText.Get("{=gwp_gwpaideterrencestate_032}Unknown location");
        }

        private static string BuildTrackingStatus(Hero hero)
        {
            if (hero.IsPrisoner)
                return GwpText.Get("{=gwp_gwpaideterrencestate_036}is in a captive state");
            if (hero.PartyBelongedTo?.IsActive == true)
                return GwpText.Get("{=gwp_gwpaideterrencestate_038}travelling with a party");
            if (hero.CurrentSettlement != null || hero.StayingInSettlement != null)
                return GwpText.Get("{=gwp_gwpaideterrencestate_037}holding position with a party");
            return GwpText.Get("{=gwp_gwpaideterrencestate_033}Unknown status");
        }
    }
}
