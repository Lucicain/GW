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
                        && !CrimeState.ActiveTasks.Values.Any(task => task.TargetCrimeId == record.CrimeId))
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
                CityTeamView view = CityTeamViewOf(state, party, search);
                // 地图暂停/存读后的现场仍占用该队；不能在另一处同时归还或自动结算。
                if (search?.SearchActive == true && search.UsesSupportParty(state.PatrolPartyId))
                {
                    if (party == null) search.FinishSearch(false);
                    ShadowCityTeam(view, party == null ? CityTeamAction.FinishSearchScene : CityTeamAction.Wait);
                    continue;
                }
                CrimeRecord? crime = CrimePool.GetRecordByKey(state.CityCrimeId);
                Hero? target = crime?.OffenderHero;
                if (crime?.HasOpenCase != true || target == null || !target.IsAlive || target.IsPrisoner)
                {
                    MarkDelayPatrolReturning(state.PatrolPartyId);
                    ShadowCityTeam(view, CityTeamAction.Return);
                    continue;
                }
                if (party?.MapEvent != null)
                {
                    ShadowCityTeam(view, CityTeamAction.Wait);
                    continue;
                }
                if (party == null || party.MemberRoster.TotalHealthyCount == 0)
                {
                    // 派遣本身不是玩家的办理次数。失队后保留原指派，直到真实兵员能补派。
                    PoliceStats.GetAllPoliceParties().Where(CanContinueLeadingPoliceTask)
                        .OrderBy(source => source.GetPosition2D.Distance(
                            state.CityForPlayer ? MobileParty.MainParty.GetPosition2D
                                : crime.ShelterSettlement?.GetPosition2D ?? MobileParty.MainParty.GetPosition2D))
                        .Any(source => SendCitySearchParty(source, crime, state.CityForPlayer, state));
                    ShadowCityTeam(view, CityTeamAction.Replace);
                    continue;
                }
                bool rethink = party.DefaultBehavior == AiBehavior.Hold;
                if (rethink)
                    GreyWardenPartyDesireBehavior.RequestImmediateRethink(party);
                bool drop = false;
                if (state.CityForPlayer)
                {
                    if (GwpPlayerCaseAssignment.IsCaseHeldByPlayer(target.StringId))
                    {
                        GreyWardenPartyDesireBehavior.RequestEscort(party, MobileParty.MainParty, 8f);
                        ShadowCityTeam(view, CityTeamAction.EscortPlayer, rethink);
                        continue;
                    }
                    state.CityForPlayer = false;
                    drop = true;
                }
                Settlement? town = CrimeRecord.ShelterOf(target);
                if (town == null) // 同催缴：目标暂时不可接触时保留案卷和使者，不当作成功或重派。
                {
                    ShadowCityTeam(view, CityTeamAction.Wait, rethink, drop);
                    continue;
                }
                if (FactionManager.IsAtWarAgainstFaction(party.MapFaction, town.MapFaction))
                {
                    MarkDelayPatrolReturning(party.StringId);
                    ShadowCityTeam(view, CityTeamAction.Return, rethink, drop);
                    continue;
                }
                if (!GwpFireswornBehavior.HasArrived(party, town))
                {
                    GreyWardenPartyDesireBehavior.RequestVisit(party, town, 8f);
                    ShadowCityTeam(view, CityTeamAction.GoToShelter, rethink, drop);
                    continue;
                }
                ResolveCitySearchParty(party, crime, target);
                MarkDelayPatrolReturning(party.StringId);
                ShadowCityTeam(view, CityTeamAction.Settle, rethink, drop);
            }
        }

        /// <summary>
        /// 把一支城内办案队此刻的实际状态整理成逻辑层的局面；必须在本小时的任何改动之前调用。
        /// 只读，不改任何游戏对象。
        /// </summary>
        private static CityTeamView CityTeamViewOf(DelayPatrolState state, MobileParty? party, GwpCitySearchBehavior? search)
        {
            CrimeRecord? crime = CrimePool.GetRecordByKey(state.CityCrimeId);
            Hero? target = crime?.OffenderHero;
            Settlement? town = target == null ? null : CrimeRecord.ShelterOf(target);
            return new CityTeamView
            {
                TeamId = state.PatrolPartyId,
                UsedByActiveSearchScene = search?.SearchActive == true && search.UsesSupportParty(state.PatrolPartyId),
                PartyExists = party != null,
                CaseOpen = crime?.HasOpenCase == true,
                TargetAvailable = target != null && target.IsAlive && !target.IsPrisoner,
                InBattle = party?.MapEvent != null,
                HealthyMembers = party?.MemberRoster.TotalHealthyCount ?? 0,
                Holding = party?.DefaultBehavior == AiBehavior.Hold,
                ForPlayer = state.CityForPlayer,
                PlayerHoldsCase = target != null && GwpPlayerCaseAssignment.IsCaseHeldByPlayer(target.StringId),
                ShelterKnown = town != null,
                AtWarWithShelter = party != null && town != null
                    && FactionManager.IsAtWarAgainstFaction(party.MapFaction, town.MapFaction),
                ArrivedAtShelter = party != null && town != null && GwpFireswornBehavior.HasArrived(party, town),
            };
        }

#if GWP_DIAGNOSTICS
        private readonly Dictionary<string, string> _cityTeamShadowLast = new Dictionary<string, string>();
#endif

        /// <summary>
        /// 影子运行（分层试点，2026-11-06）：旧代码照常执行，这里把同一局面交给逻辑层算一遍；
        /// 两边不一致记 MISMATCH。每支队只在局面或决定变化时记一行，供录成回放用例。
        /// 迁移完成、旧分支删除后与本方法一起退休。
        /// </summary>
        [System.Diagnostics.Conditional("GWP_DIAGNOSTICS")]
        private void ShadowCityTeam(CityTeamView view, CityTeamAction oldAction, bool rethink = false, bool drop = false)
        {
#if GWP_DIAGNOSTICS
            var old = new CityTeamDecision(oldAction, rethink, drop);
            CityTeamDecision core = CityTeamRules.Decide(view);
            string line = view.ToRecord() + " >> " + old.ToRecord();
            bool mismatch = !core.Equals(old);
            if (!mismatch && _cityTeamShadowLast.TryGetValue(view.TeamId, out string? last) && last == line) return;
            _cityTeamShadowLast[view.TeamId] = line;
            GwpAiDiagnostics.WriteCitySearch(mismatch ? "CITY_TEAM_SHADOW_MISMATCH" : "CITY_TEAM_SHADOW",
                line + (mismatch ? " >> core " + core.ToRecord() : string.Empty));
#endif
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
