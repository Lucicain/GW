using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    public partial class PoliceEnforcementBehavior
    {
        private DelayPatrolState? CityPartyState(CrimeRecord crime) => _delayPatrolStates.Values.FirstOrDefault(state =>
            state.CityCrimeId == crime.CrimeId && !state.Returning);

        internal MobileParty? CitySearchSupport(Hero target, bool requireArrival = true)
        {
            CrimeRecord? crime = CrimePool.GetRecord(target);
            DelayPatrolState? state = crime == null ? null : CityPartyState(crime);
            MobileParty? party = state?.CityForPlayer == true ? FindActiveParty(state.PatrolPartyId) : null;
            if (party == null || party.MapEvent != null || party.MemberRoster.TotalHealthyCount == 0) return null;
            return !requireArrival || CityPartyReached(party, MobileParty.MainParty) ? party : null;
        }

        internal bool CitySearchDelegated(Hero target) => CrimePool.GetRecord(target) is { } crime
            && CityPartyState(crime)?.CityDelegated == true;

        internal static bool IsCitySearchParty(PartyBase? party) => party?.MobileParty is { } mobile
            && _instance?._delayPatrolStates.TryGetValue(mobile.StringId, out DelayPatrolState? state) == true
            && !string.IsNullOrEmpty(state.CityCrimeId);

        internal bool RequestCitySearchParty(Hero target, bool followPlayer)
        {
            CrimeRecord? crime = CrimePool.GetRecord(target);
            if (crime?.HasOpenCase != true || CrimeRecord.ShelterOf(target) == null
                || followPlayer && GwpCitySearchCasePolicy.SupportSpent(crime))
                return false;
            DelayPatrolState? existing = CityPartyState(crime);
            if (existing != null)
            {
                existing.CityForPlayer = followPlayer;
                existing.CityDelegated = !followPlayer;
                return true;
            }
            foreach (MobileParty source in PoliceStats.GetAllPoliceParties().Where(CanContinueLeadingPoliceTask)
                         .OrderBy(party => party.GetPosition2D.Distance(MobileParty.MainParty.GetPosition2D)))
                if (SendCitySearchParty(source, crime, followPlayer, !followPlayer)) return true;
            return false;
        }

        private bool SendCitySearchParty(MobileParty source, CrimeRecord crime, bool followPlayer, bool delegated,
            DelayPatrolState? replacing = null)
        {
            Settlement? town = crime.ShelterSettlement;
            Clan? clan = PoliceStats.GetPoliceClan();
            if (town == null || clan == null || source.MapEvent != null
                || source.Army != null && source.Army.LeaderParty != source || PoliceResourceManager.IsStrandedAtSea(source)
                || !crime.HasOpenCase || CityPartyState(crime) != replacing
                || FactionManager.IsAtWarAgainstFaction(clan, town.MapFaction)) return false;
            var batches = DetachmentBatches(source, new[] { source })
                .Where(batch => GwpCommon.IsGreyWardenTroop(batch.Character)).ToList();
            int size = Math.Min(GwpTuning.Enforcement.EnvoySize, HealthyTroopCount(batches));
            if (size == 0) return false;
            var transferred = new List<ImmediateInterceptorTroopTransfer>();
            MobileParty? party = CreateDetachment(source, batches, size, 1,
                GwpText.Get(GwpTextCatalog.gwp_cs_support_party_name), transferred, out _, out Settlement? home);
            if (party == null) return false;
            PoliceResourceManager.OutfitTemporaryDutyParty(party, source);
            party.Aggressiveness = 0f;
            _delayPatrolStates[party.StringId] = new DelayPatrolState
            {
                PatrolPartyId = party.StringId, SourceTaskPolicePartyId = source.StringId,
                ReturnSettlementId = home!.StringId, CityCrimeId = crime.CrimeId,
                CityForPlayer = followPlayer, CityDelegated = delegated, IsEnvoy = true
            };
            if (replacing != null) MarkDelayPatrolReturning(replacing.PatrolPartyId);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteAction(party, "CITY_CASE_SENT", "crime=" + crime.CrimeId
                + "; source=" + source.StringId + "; count=" + size + "; player=" + followPlayer + "; delegated=" + delegated);
#endif
            if (followPlayer) GreyWardenPartyDesireBehavior.RequestEscort(party, MobileParty.MainParty, 8f);
            else GreyWardenPartyDesireBehavior.RequestVisit(party, town, 8f);
            return true;
        }

        private void DispatchCitySearchCases()
        {
            foreach (MobileParty source in PoliceStats.GetAllPoliceParties().Where(CanAssignOrdinaryCaseNow).ToList())
            {
                GwpCrimeCategory preferred = GetPreferredCrimeCategory(source);
                var candidates = CrimePool.LedgerRecords.Where(record => record.IsOffenderSearchableInSettlement()
                        && CityPartyState(record) == null && !HasEnvoyAgreement(record)
                        && !CrimeState.ActiveTasks.Values.Any(task => task.TargetCrimeId == record.CrimeId))
                    .OrderBy(record => preferred != GwpCrimeCategory.Unknown && record.CrimeCategory == preferred ? 0 : 1)
                    .ThenBy(record => source.GetPosition2D.Distance(record.ShelterSettlement!.GetPosition2D))
                    .ToList();
                foreach (CrimeRecord crime in candidates)
                    if (SendCitySearchParty(source, crime, false, false)) break;
            }
        }

        private static bool CityPartyReached(MobileParty party, MobileParty other) =>
            GwpCommon.CanCompletePartyHandover(party, other) && party.MapEvent == null && other.MapEvent == null
            && party.GetPosition2D.Distance(other.GetPosition2D) <= Campaign.Current.Models.EncounterModel
                .MaximumAllowedLandDistanceForEncounteringMobilePartyInArmy;

        internal void ReleaseCitySearchParty(string partyId)
        {
            if (_delayPatrolStates.TryGetValue(partyId, out DelayPatrolState? state)
                && !string.IsNullOrEmpty(state.CityCrimeId)) MarkDelayPatrolReturning(partyId);
        }

        private void TendCitySearchParties()
        {
            foreach (DelayPatrolState state in _delayPatrolStates.Values
                         .Where(state => !state.Returning && !string.IsNullOrEmpty(state.CityCrimeId)).ToList())
            {
                MobileParty? party = FindActiveParty(state.PatrolPartyId);
                var search = Campaign.Current.GetCampaignBehavior<GwpCitySearchBehavior>();
                // 地图暂停/存读后的现场仍占用该队；不能在另一处同时归还或自动结算。
                if (search?.SearchActive == true && search.UsesSupportParty(state.PatrolPartyId))
                {
                    if (party == null) search.FinishSearch(false);
                    continue;
                }
                CrimeRecord? crime = CrimePool.GetRecordByKey(state.CityCrimeId);
                Hero? target = crime?.OffenderHero;
                if (crime?.HasOpenCase != true || target == null || !target.IsAlive || target.IsPrisoner)
                {
                    MarkDelayPatrolReturning(state.PatrolPartyId);
                    continue;
                }
                if (party?.MapEvent != null) continue;
                if (party == null || party.MemberRoster.TotalHealthyCount == 0)
                {
                    // 派遣本身不是玩家的办理次数。失队后保留原指派，直到真实兵员能补派。
                    foreach (MobileParty source in PoliceStats.GetAllPoliceParties().Where(CanContinueLeadingPoliceTask)
                                 .OrderBy(source => source.GetPosition2D.Distance(
                                     state.CityForPlayer ? MobileParty.MainParty.GetPosition2D
                                         : crime.ShelterSettlement?.GetPosition2D ?? MobileParty.MainParty.GetPosition2D)))
                        if (SendCitySearchParty(source, crime, state.CityForPlayer, state.CityDelegated, state)) break;
                    continue;
                }
                if (party.DefaultBehavior == AiBehavior.Hold)
                    GreyWardenPartyDesireBehavior.RequestImmediateRethink(party);
                if (!state.CityDelegated && GwpPlayerCaseAssignment.IsCaseHeldByPlayer(target.StringId))
                    state.CityForPlayer = true;
                if (state.CityForPlayer)
                {
                    if (GwpPlayerCaseAssignment.IsCaseHeldByPlayer(target.StringId))
                    {
                        GreyWardenPartyDesireBehavior.RequestEscort(party, MobileParty.MainParty, 8f);
                        continue;
                    }
                    state.CityForPlayer = false;
                }
                Settlement? town = CrimeRecord.ShelterOf(target);
                if (town == null) continue; // 同催缴：目标暂时不可接触时保留案卷和使者，不当作成功或重派。
                if (FactionManager.IsAtWarAgainstFaction(party.MapFaction, town.MapFaction))
                {
                    MarkDelayPatrolReturning(party.StringId);
                    continue;
                }
                if (!GwpFireswornBehavior.HasArrived(party, town))
                {
                    GreyWardenPartyDesireBehavior.RequestVisit(party, town, 8f);
                    continue;
                }
                ResolveCitySearchParty(party, crime, target);
                MarkDelayPatrolReturning(party.StringId);
            }
        }

        private static void ResolveCitySearchParty(MobileParty party, CrimeRecord crime, Hero target)
        {
            int assessed = GwpFieldArrestPricing.AssessFine(crime);
            int collected = CollectEnvoyFine(crime, party, target, assessed);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteAction(party, "CITY_CASE_RESULT", "crime=" + crime.CrimeId
                + "; target=" + target.StringId + "; assessed=" + assessed + "; collected=" + collected);
#endif
            CloseCase(crime, "city_detachment_settled");
            PoliceResourceManager.CreditSuccessfulCaseCompletion();
        }
    }
}
