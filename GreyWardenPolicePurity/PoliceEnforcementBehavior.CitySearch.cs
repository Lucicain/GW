using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

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
            if (crime?.HasOpenCase != true || CrimeRecord.ShelterOf(target) == null || GwpCitySearchCasePolicy.SupportSpent(crime))
                return false;
            DelayPatrolState? existing = CityPartyState(crime);
            if (existing != null)
            {
                if (FindActiveParty(existing.PatrolPartyId)?.MemberRoster.TotalHealthyCount is not > 0) return false;
                existing.CityForPlayer = followPlayer;
                existing.CityDelegated = !followPlayer;
                return true;
            }
            if (!GwpCitySearchCasePolicy.CanDispatch(crime)) return false;
            foreach (MobileParty source in PoliceStats.GetAllPoliceParties().Where(CanContinueLeadingPoliceTask)
                         .OrderBy(party => party.GetPosition2D.Distance(MobileParty.MainParty.GetPosition2D)))
                if (SendCitySearchParty(source, crime, followPlayer, !followPlayer)) return true;
            return false;
        }

        private bool SendCitySearchParty(MobileParty source, CrimeRecord crime, bool followPlayer, bool delegated)
        {
            Settlement? town = crime.ShelterSettlement;
            Clan? clan = PoliceStats.GetPoliceClan();
            if (town == null || clan == null || source.MapEvent != null
                || source.Army != null && source.Army.LeaderParty != source || PoliceResourceManager.IsStrandedAtSea(source)
                || !GwpCitySearchCasePolicy.CanDispatch(crime) || CityPartyState(crime) != null
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
            GwpCitySearchCasePolicy.MarkDispatched(crime);
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
                        && GwpCitySearchCasePolicy.CanDispatch(record) && !HasEnvoyAgreement(record)
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
                if (party == null) continue;
                var search = Campaign.Current.GetCampaignBehavior<GwpCitySearchBehavior>();
                // 地图暂停/存读后的现场仍占用该队；不能在另一处同时归还或自动结算。
                if (search?.SearchActive == true && search.TroopSource == party.Party) continue;
                CrimeRecord? crime = CrimePool.GetRecordByKey(state.CityCrimeId);
                Hero? target = crime?.OffenderHero;
                if (crime?.HasOpenCase != true || target == null || !target.IsAlive || target.IsPrisoner
                    || party.MemberRoster.TotalHealthyCount == 0)
                {
                    if (crime != null && party.MemberRoster.TotalHealthyCount == 0)
                        GwpCitySearchCasePolicy.TryBegin(crime, true);
                    MarkDelayPatrolReturning(party.StringId);
                    continue;
                }
                if (party.MapEvent != null) continue;
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
                if (!GwpVoluntarySurrender.CanCapture(target)) continue;
                ResolveCitySearchParty(party, crime, target);
                MarkDelayPatrolReturning(party.StringId);
            }
        }

        private static void ResolveCitySearchParty(MobileParty party, CrimeRecord crime, Hero target)
        {
            if (!GwpCitySearchCasePolicy.TryBegin(crime, true)) return;
            CharacterObject champion = party.MemberRoster.GetTroopRoster()
                .Where(element => element.Number > element.WoundedNumber && !element.Character.IsHero)
                .OrderByDescending(element => Campaign.Current.Models.TournamentModel
                    .GetTournamentSimulationScore(element.Character)).First().Character;
            bool duel = target.GetTraitLevel(DefaultTraits.Valor) > 0;
            bool won = !duel || CityChampionWins(champion, target.CharacterObject);
            if (duel && !won) party.MemberRoster.AddToCounts(champion, 0, woundedCount: 1);
            if (duel && won) target.MakeWounded();
            bool captured = won && GwpVoluntarySurrender.TryCapture(party.Party, target);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteAction(party, "CITY_CASE_RESULT", "crime=" + crime.CrimeId
                + "; target=" + target.StringId + "; duel=" + duel + "; won=" + won + "; captured=" + captured);
#endif
            if (captured)
            {
                CloseCase(crime, "city_detachment_captured");
                PoliceResourceManager.CreditSuccessfulCaseCompletion();
            }
            if (GwpPlayerCaseAssignment.IsCaseHeldByPlayer(target.StringId))
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Create(captured
                    ? GwpTextCatalog.gwp_cs_support_success : GwpTextCatalog.gwp_cs_support_failed, "TARGET", target.Name).ToString()));
        }

        private static bool CityChampionWins(CharacterObject champion, CharacterObject target)
        {
            var model = Campaign.Current.Models.TournamentModel;
            float ours = model.GetTournamentSimulationScore(champion) * (0.75f + 0.25f * MBRandom.RandomFloat);
            float theirs = model.GetTournamentSimulationScore(target) * (0.75f + 0.25f * MBRandom.RandomFloat);
            return ours >= theirs;
        }
    }
}
