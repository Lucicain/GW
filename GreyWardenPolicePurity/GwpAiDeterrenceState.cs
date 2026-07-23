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
    /// 威慑状态与累计次数存放在每位领主唯一的长期数字档案中。
    /// 本人被捕与家族受震慑分别累计、按总量共同衰退，长期数字不会因当前案件结案而删除。
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
            public float VillageDirectPenalty { get; init; }
            public float VillageSharedPenalty { get; init; }
            public float VillageEffectivePenalty { get; init; }
            public float CaravanDirectPenalty { get; init; }
            public float CaravanSharedPenalty { get; init; }
            public float CaravanEffectivePenalty { get; init; }
            public float CaravanScoreMultiplier { get; init; }
            public float VillageRecoveryDaysRemaining { get; init; }
            public float CaravanRecoveryDaysRemaining { get; init; }
            public bool RecoveryPaused { get; init; }
            public float DaysSinceLastEnforcement { get; init; }
            public string MapStatus { get; init; }
            public string MapLocation { get; init; }
        }

        public static void ClearAll()
        {
            foreach (HeroCrimeStats record in CrimePool.HistoryRecords)
            {
                record.DirectDeterrencePoints = 0f;
                record.SharedDeterrencePoints = 0f;
                record.SharedDeterrenceCount = 0;
                record.LastDeterrenceUpdatedHours = 0f;
                record.LastEnforcementHours = 0f;
                record.CaravanArrestCount = 0;
                record.CaravanDirectDeterrencePoints = 0f;
                record.CaravanSharedDeterrencePoints = 0f;
                record.CaravanSharedDeterrenceCount = 0;
                record.CaravanLastDeterrenceUpdatedHours = 0f;
                record.CaravanLastEnforcementHours = 0f;
            }
        }

        /// <summary>登记一次由灰袍实际实施的抓捕，并返回本次新增的本人威慑。</summary>
        public static float RegisterPoliceArrest(Hero leader, GwpCrimeCategory category)
        {
            if (!CanTrack(leader)) return 0f;

            HeroCrimeStats record = CrimePool.GetOrCreateHistory(leader);
            return category == GwpCrimeCategory.CaravanAttack
                ? RegisterCaravanArrest(record, leader)
                : RegisterVillageViolenceArrest(record, leader);
        }

        private static float RegisterVillageViolenceArrest(HeroCrimeStats record, Hero leader)
        {

            UpdateDecay(record, leader, updateRecord: true);
            int totalArrests = CrimePool.RecordArrest(leader);
            int arrestCount = Math.Max(1, totalArrests - record.CaravanArrestCount);
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

        private static float RegisterCaravanArrest(HeroCrimeStats record, Hero leader)
        {
            UpdateCaravanDecay(record, leader, updateRecord: true);
            CrimePool.RecordArrest(leader);
            int arrestCount = ++record.CaravanArrestCount;
            float desiredGain = MathF.Min((float)arrestCount,
                GwpTuning.Deterrence.MaxPenaltyGainPerCapture);
            float previousDirect = record.CaravanDirectDeterrencePoints;
            record.CaravanDirectDeterrencePoints = MathF.Min(
                GwpTuning.Deterrence.RaidPenaltyCap, previousDirect + desiredGain);
            float actualGain = record.CaravanDirectDeterrencePoints - previousDirect;
            float remainingSharedRoom = MathF.Max(0f,
                GwpTuning.Deterrence.RaidPenaltyCap - record.CaravanDirectDeterrencePoints);
            record.CaravanSharedDeterrencePoints = MathF.Min(
                record.CaravanSharedDeterrencePoints, remainingSharedRoom);
            record.CaravanLastDeterrenceUpdatedHours = (float)CampaignTime.Now.ToHours;
            record.CaravanLastEnforcementHours = record.CaravanLastDeterrenceUpdatedHours;
            record.LastEnforcementHours = MathF.Max(record.LastEnforcementHours,
                record.CaravanLastEnforcementHours);
            return actualGain;
        }

        public static float RegisterSharedFamilyDeterrence(Hero leader, float penaltyGain,
            GwpCrimeCategory category)
        {
            if (!CanTrack(leader) || penaltyGain <= 0f) return 0f;

            return category == GwpCrimeCategory.CaravanAttack
                ? RegisterSharedCaravanDeterrence(leader, penaltyGain)
                : RegisterSharedVillageDeterrence(leader, penaltyGain);
        }

        private static float RegisterSharedVillageDeterrence(Hero leader, float penaltyGain)
        {

            HeroCrimeStats record = CrimePool.GetOrCreateHistory(leader);
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

        private static float RegisterSharedCaravanDeterrence(Hero leader, float penaltyGain)
        {
            HeroCrimeStats record = CrimePool.GetOrCreateHistory(leader);
            UpdateCaravanDecay(record, leader, updateRecord: true);
            float total = record.CaravanDirectDeterrencePoints +
                          record.CaravanSharedDeterrencePoints;
            float actualGain = MathF.Min(penaltyGain,
                MathF.Max(0f, GwpTuning.Deterrence.RaidPenaltyCap - total));
            if (actualGain <= 0f) return total;
            record.CaravanSharedDeterrencePoints += actualGain;
            record.CaravanSharedDeterrenceCount++;
            record.CaravanLastDeterrenceUpdatedHours = (float)CampaignTime.Now.ToHours;
            record.CaravanLastEnforcementHours = record.CaravanLastDeterrenceUpdatedHours;
            record.LastEnforcementHours = MathF.Max(record.LastEnforcementHours,
                record.CaravanLastEnforcementHours);
            return record.CaravanDirectDeterrencePoints +
                   record.CaravanSharedDeterrencePoints;
        }

        public static float GetRaidScoreMultiplier(MobileParty party) =>
            GetCrimeDesireMultiplier(party, GwpCrimeCategory.VillageViolence);

        public static float GetCaravanAttackScoreMultiplier(MobileParty? party) =>
            GetCrimeDesireMultiplier(party, GwpCrimeCategory.CaravanAttack);

        public static float GetVillagerAttackScoreMultiplier(MobileParty? party) =>
            GetCrimeDesireMultiplier(party, GwpCrimeCategory.VillageViolence);

        public static float GetCrimeDesireMultiplier(MobileParty? party)
            => GetCrimeDesireMultiplier(party, GwpCrimeCategory.VillageViolence);

        public static float GetCrimeDesireMultiplier(MobileParty? party,
            GwpCrimeCategory category)
        {
            if (party == null) return 1f;
            if (IsGreyWardenPoliceParty(party.ActualClan)) return 0f;
            return GetCrimeDesireMultiplier(party.LeaderHero, category);
        }

        public static float GetCrimeDesireMultiplier(Hero? hero)
            => GetCrimeDesireMultiplier(hero, GwpCrimeCategory.VillageViolence);

        public static float GetCrimeDesireMultiplier(Hero? hero, GwpCrimeCategory category)
        {
            if (Campaign.Current == null || hero == null) return 1f;
            if (IsGreyWardenPoliceHero(hero)) return 0f;

            float penalty = GetCurrentPenalty(hero, category);
            if (penalty <= GwpTuning.Deterrence.ForgetThreshold) return 1f;

            float multiplier = MathF.Pow(GwpTuning.Deterrence.RaidScoreMultiplierPerPoint, penalty);
            return MathF.Max(GwpTuning.Deterrence.RaidScoreMultiplierFloor, multiplier);
        }

        public static float GetCurrentPenalty(Hero? hero)
        {
            HeroCrimeStats? record = CrimePool.GetHistory(hero);
            if (record == null || hero == null) return 0f;
            GetEffectiveComponents(record, hero, out float direct, out float shared);
            return direct + shared;
        }

        public static float GetCurrentPenalty(Hero? hero, GwpCrimeCategory category)
        {
            HeroCrimeStats? record = CrimePool.GetHistory(hero);
            if (record == null || hero == null) return 0f;
            if (category == GwpCrimeCategory.CaravanAttack)
            {
                GetEffectiveCaravanComponents(record, hero, out float direct, out float shared);
                return direct + shared;
            }
            GetEffectiveComponents(record, hero, out float villageDirect, out float villageShared);
            return villageDirect + villageShared;
        }

        public static DeterrenceDetails GetDeterrenceDetails(Hero? hero)
        {
            if (hero == null)
                return EmptyDetails();

            HeroCrimeStats? record = CrimePool.GetHistory(hero);
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
            GetEffectiveCaravanComponents(record, hero, out float caravanDirect,
                out float caravanShared);
            float villageTotal = direct + shared;
            float caravanTotal = caravanDirect + caravanShared;
            float total = villageTotal + caravanTotal;
            float now = (float)CampaignTime.Now.ToHours;
            float days = record.LastEnforcementHours > 0f
                ? MathF.Max(0f, (now - record.LastEnforcementHours) / CampaignTime.HoursInDay)
                : 0f;
            float recoveryPerDay = GetRecoveryPerDay(hero);
            bool recoveryPaused = !CanRecoverPenalty(hero) &&
                                  (villageTotal > GwpTuning.Deterrence.ForgetThreshold ||
                                   caravanTotal > GwpTuning.Deterrence.ForgetThreshold);

            return new DeterrenceDetails
            {
                HasEntry = true,
                DirectPenalty = direct + caravanDirect,
                SharedPenalty = shared + caravanShared,
                EffectivePenalty = total,
                TotalCrimeCount = record.TotalCrimeCount,
                TotalArrestCount = record.TotalArrestCount,
                EnforcementCount = record.TotalArrestCount,
                SharedDeterrenceCount = record.SharedDeterrenceCount +
                                         record.CaravanSharedDeterrenceCount,
                RaidScoreMultiplier = GetCrimeDesireMultiplier(hero,
                    GwpCrimeCategory.VillageViolence),
                VillageDirectPenalty = direct,
                VillageSharedPenalty = shared,
                VillageEffectivePenalty = villageTotal,
                CaravanDirectPenalty = caravanDirect,
                CaravanSharedPenalty = caravanShared,
                CaravanEffectivePenalty = caravanTotal,
                CaravanScoreMultiplier = GetCrimeDesireMultiplier(hero,
                    GwpCrimeCategory.CaravanAttack),
                VillageRecoveryDaysRemaining = GetRecoveryDaysRemaining(
                    villageTotal, recoveryPerDay),
                CaravanRecoveryDaysRemaining = GetRecoveryDaysRemaining(
                    caravanTotal, recoveryPerDay),
                RecoveryPaused = recoveryPaused,
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
            HeroCrimeStats? record = CrimePool.GetHistory(hero);
            if (record == null) return false;

            GetEffectiveComponents(record, hero, out float direct, out float shared);
            GetEffectiveCaravanComponents(record, hero, out float caravanDirect,
                out float caravanShared);
            direct += caravanDirect;
            shared += caravanShared;
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
            foreach (HeroCrimeStats record in CrimePool.HistoryRecords.ToList())
            {
                Hero? hero = record.Hero;
                if (hero == null) continue;
                UpdateDecay(record, hero, updateRecord: true);
                UpdateCaravanDecay(record, hero, updateRecord: true);
            }
        }

        /// <summary>威慑与累计次数已随长期数字档案统一序列化。</summary>
        public static void SyncData(IDataStore dataStore) { }

        private static void GetEffectiveComponents(HeroCrimeStats record, Hero hero, out float direct, out float shared)
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

        private static void GetEffectiveCaravanComponents(HeroCrimeStats record, Hero hero,
            out float direct, out float shared)
        {
            float total = UpdateCaravanDecay(record, hero, updateRecord: false);
            float storedTotal = record.CaravanDirectDeterrencePoints +
                                record.CaravanSharedDeterrencePoints;
            if (storedTotal <= 0f || total <= 0f)
            {
                direct = 0f;
                shared = 0f;
                return;
            }
            float scale = total / storedTotal;
            direct = record.CaravanDirectDeterrencePoints * scale;
            shared = record.CaravanSharedDeterrencePoints * scale;
        }

        /// <summary>两类威慑按当前占比共同衰退，避免分别扣减造成来源比重突变。</summary>
        private static float UpdateDecay(HeroCrimeStats record, Hero hero, bool updateRecord)
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

        private static float UpdateCaravanDecay(HeroCrimeStats record, Hero hero,
            bool updateRecord)
        {
            float storedTotal = MathF.Max(0f, record.CaravanDirectDeterrencePoints) +
                                MathF.Max(0f, record.CaravanSharedDeterrencePoints);
            if (storedTotal <= 0f) return 0f;
            float now = (float)CampaignTime.Now.ToHours;
            float updated = record.CaravanLastDeterrenceUpdatedHours > 0f
                ? record.CaravanLastDeterrenceUpdatedHours
                : record.LastDeterrenceUpdatedHours;
            float elapsedDays = CanRecoverPenalty(hero)
                ? MathF.Max(0f, (now - updated) / CampaignTime.HoursInDay)
                : 0f;
            float effective = MathF.Max(0f,
                storedTotal - elapsedDays * GetRecoveryPerDay(hero));
            if (updateRecord)
            {
                float scale = storedTotal > 0f ? effective / storedTotal : 0f;
                record.CaravanDirectDeterrencePoints *= scale;
                record.CaravanSharedDeterrencePoints *= scale;
                record.CaravanLastDeterrenceUpdatedHours = now;
                if (effective <= GwpTuning.Deterrence.ForgetThreshold)
                {
                    record.CaravanDirectDeterrencePoints = 0f;
                    record.CaravanSharedDeterrencePoints = 0f;
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
            recovery += hero.GetTraitLevel(DefaultTraits.Valor) * 0.0025f;
            recovery -= hero.GetTraitLevel(DefaultTraits.Honor) * 0.002f;
            recovery -= hero.GetTraitLevel(DefaultTraits.Mercy) * 0.002f;
            recovery -= hero.GetTraitLevel(DefaultTraits.Calculating) * 0.0015f;
            return MBMath.ClampFloat(
                recovery,
                GwpTuning.Deterrence.MinRecoveryPerDay,
                GwpTuning.Deterrence.MaxRecoveryPerDay) *
                   GwpTuning.Deterrence.RecoverySpeedMultiplier;
        }

        private static float GetRecoveryDaysRemaining(
            float currentPenalty,
            float recoveryPerDay)
        {
            float remainingPenalty = MathF.Max(
                0f, currentPenalty - GwpTuning.Deterrence.ForgetThreshold);
            return recoveryPerDay > 0f
                ? remainingPenalty / recoveryPerDay
                : 0f;
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
