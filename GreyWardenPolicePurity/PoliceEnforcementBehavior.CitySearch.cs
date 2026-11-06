using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using GreyWardenPolicePurity.Core.CitySupport;

namespace GreyWardenPolicePurity
{
    public partial class PoliceEnforcementBehavior
    {
        private DelayPatrolState? CityPartyState(CrimeRecord crime) => _delayPatrolStates.Values.FirstOrDefault(state =>
            state.CityCrimeId == crime.CrimeId && !state.Returning);

        /// <summary>只有开放住城案件已有承办者时，才锁定该英雄当前的聚落。</summary>
        internal static Settlement? AcceptedCityCaseSettlement(Hero hero)
        {
            CrimeRecord? crime = CrimePool.GetRecord(hero);
            if (crime?.HasOpenCase != true) return null;
            Settlement? settlement = CrimeRecord.ShelterOf(hero);
            if (settlement == null) return null;

            bool assigned = GwpPlayerCaseAssignment.IsCaseHeldByPlayer(hero.StringId)
                || CrimePool.ActiveTasks.Values.Any(task => task.TargetCrimeId == crime.CrimeId)
                || _instance?.CityPartyState(crime) != null;
            return assigned ? settlement : null;
        }

        internal MobileParty? CitySearchSupport(Hero target, bool requireArrival = true)
        {
            CrimeRecord? crime = CrimePool.GetRecord(target);
            DelayPatrolState? state = crime == null ? null : CityPartyState(crime);
            MobileParty? party = state?.CityForPlayer == true ? FindActiveParty(state.PatrolPartyId) : null;
            if (party == null || party.MapEvent != null || party.MemberRoster.TotalHealthyCount == 0) return null;
            return !requireArrival || CityPartyReached(party, MobileParty.MainParty) ? party : null;
        }

        /// <summary>玩家住城委托是否有办案队负责（含失队待补派），以及当前实际在场的那支。</summary>
        internal bool TryGetPlayerCitySupport(Hero target, out MobileParty? party)
        {
            CrimeRecord? crime = CrimePool.GetRecord(target);
            DelayPatrolState? state = crime == null ? null : CityPartyState(crime);
            party = state?.CityForPlayer == true ? FindActiveParty(state.PatrolPartyId) : null;
            return state?.CityForPlayer == true;
        }

        internal static bool IsCitySearchParty(PartyBase? party) => party?.MobileParty is { } mobile
            && _instance?._delayPatrolStates.TryGetValue(mobile.StringId, out DelayPatrolState? state) == true
            && !string.IsNullOrEmpty(state.CityCrimeId);

        /// <summary>玩家住城委托的求援成立：先由接到求援的领主出人，不行再按离玩家远近找能出人的灰袍。</summary>
        internal MobileParty? SendPlayerCitySearchParty(CrimeRecord crime, MobileParty? preferred) =>
            PoliceStats.GetAllPoliceParties().Where(CanContinueLeadingPoliceTask)
                .OrderBy(party => party == preferred ? 0 : 1)
                .ThenBy(party => party.GetPosition2D.Distance(MobileParty.MainParty.GetPosition2D))
                .FirstOrDefault(source => SendCitySearchParty(source, crime, true));

        /// <summary>玩家接了案，原来独立办理的城内办案队和使者一样撤回；要人手由玩家求援。</summary>
        private void ReleaseCityPartiesToPlayer(string heroId)
        {
            foreach (DelayPatrolState state in _delayPatrolStates.Values.Where(state => !state.Returning && !state.CityForPlayer
                         && string.Equals(CrimePool.GetRecordByKey(state.CityCrimeId)?.OffenderHeroId, heroId,
                             StringComparison.OrdinalIgnoreCase)).ToList())
                MarkDelayPatrolReturning(state.PatrolPartyId);
        }

        private bool SendCitySearchParty(MobileParty source, CrimeRecord crime, bool followPlayer,
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
                CityForPlayer = followPlayer, IsEnvoy = true
            };
            if (replacing != null) MarkDelayPatrolReturning(replacing.PatrolPartyId);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteAction(party, "CITY_CASE_SENT", "crime=" + crime.CrimeId
                + "; source=" + source.StringId + "; count=" + size + "; player=" + followPlayer);
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
                        && !CrimePool.ActiveTasks.Values.Any(task => task.TargetCrimeId == record.CrimeId))
                    .OrderBy(record => preferred != GwpCrimeCategory.Unknown && record.CrimeCategory == preferred ? 0 : 1)
                    .ThenBy(record => source.GetPosition2D.Distance(record.ShelterSettlement!.GetPosition2D))
                    .ToList();
                foreach (CrimeRecord crime in candidates)
                    if (SendCitySearchParty(source, crime, false)) break;
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
                CityTeamView view = CityTeamViewOf(state, party, search,
                    out CrimeRecord? crime, out Hero? target, out Settlement? town);
                CityTeamDecision decision = CityTeamRules.Decide(view);

                // 两项前置动作的顺序保持原样；Rethink 只设置下次小时决策标志，不立即执行 AI。
                if (decision.Rethink) GreyWardenPartyDesireBehavior.RequestImmediateRethink(party);
                if (decision.DropPlayerLink) state.CityForPlayer = false;

                switch (decision.Action)
                {
                    case CityTeamAction.Wait:
                        break;
                    case CityTeamAction.FinishSearchScene:
                        search!.FinishSearch(false);
                        break;
                    case CityTeamAction.Return:
#if GWP_DIAGNOSTICS
                        if (party != null)
                            GwpAiDiagnostics.WriteAction(party, "CITY_CASE_RETURN", "crime=" + state.CityCrimeId
                                + "; open=" + view.CaseOpen + "; available=" + view.TargetAvailable
                                + "; shelter=" + (town?.StringId ?? "-") + "; hostile=" + view.AtWarWithShelter);
#endif
                        MarkDelayPatrolReturning(state.PatrolPartyId);
                        break;
                    case CityTeamAction.Replace:
                        // 决定补派不等于已经成功；实际抽兵、登记与旧队归还仍由派队入口办理。
                        PoliceStats.GetAllPoliceParties().Where(CanContinueLeadingPoliceTask)
                            .OrderBy(source => source.GetPosition2D.Distance(
                                state.CityForPlayer ? MobileParty.MainParty.GetPosition2D
                                    : crime!.ShelterSettlement?.GetPosition2D ?? MobileParty.MainParty.GetPosition2D))
                            .Any(source => SendCitySearchParty(source, crime!, state.CityForPlayer, state));
                        break;
                    case CityTeamAction.EscortPlayer:
                        GreyWardenPartyDesireBehavior.RequestEscort(party!, MobileParty.MainParty, 8f);
                        break;
                    case CityTeamAction.GoToShelter:
                        GreyWardenPartyDesireBehavior.RequestVisit(party!, town!, 8f);
                        break;
                    case CityTeamAction.Settle:
                        ResolveCitySearchParty(party!, crime!, target!);
                        MarkDelayPatrolReturning(party!.StringId);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(decision.Action), decision.Action, null);
                }
            }
        }

        /// <summary>
        /// 采集生产规则所需的数据，并保留本次执行使用的真实对象。
        /// 按现场、案卷、战斗、兵员、跟随、目的地依次读取；不可用阶段不访问后续对象。
        /// </summary>
        private static CityTeamView CityTeamViewOf(DelayPatrolState state, MobileParty? party,
            GwpCitySearchBehavior? search, out CrimeRecord? crime, out Hero? target, out Settlement? town)
        {
            crime = null;
            target = null;
            town = null;
            var view = new CityTeamView
            {
                PartyExists = party != null,
                UsedByActiveSearchScene = search?.SearchActive == true && search.UsesSupportParty(state.PatrolPartyId),
            };
            if (view.UsedByActiveSearchScene) return view;

            crime = CrimePool.GetRecordByKey(state.CityCrimeId);
            target = crime?.OffenderHero;
            view.CaseOpen = crime?.HasOpenCase == true;
            view.TargetAvailable = target != null && target.IsAlive && !target.IsPrisoner;
            if (!view.CaseOpen || !view.TargetAvailable) return view;

            view.InBattle = party?.MapEvent != null;
            if (view.InBattle) return view;
            view.ForPlayer = state.CityForPlayer;
            view.PlayerHoldsCase = state.CityForPlayer && GwpPlayerCaseAssignment.IsCaseHeldByPlayer(target!.StringId);
            town = CrimeRecord.ShelterOf(target);
            view.ShelterKnown = town != null;
            if (!view.PlayerHoldsCase)
            {
                if (town == null) return view;
                if ((party?.MapFaction ?? PoliceStats.GetPoliceClan()) is { } faction)
                    view.AtWarWithShelter = FactionManager.IsAtWarAgainstFaction(faction, town.MapFaction);
                if (view.AtWarWithShelter) return view;
            }
            view.HealthyMembers = party?.MemberRoster.TotalHealthyCount ?? 0;
            if (party == null || view.HealthyMembers == 0) return view;

            view.Holding = party.DefaultBehavior == AiBehavior.Hold;
            if (view.PlayerHoldsCase) return view;
            view.ArrivedAtShelter = GwpFireswornBehavior.HasArrived(party, town!);
            return view;
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
