using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍使者（用户裁定 2026-10-15）。承办领主赶去宣战的路上，先从自己队里抽几名骑兵去和目标谈；
    /// 还没宣战，碰上了也打不起来。谈判照玩家当面谈的两层规则掷骰，第一层比的是灰袍主队和目标队伍。
    /// 谈成就结案，使者回主队交钱交人；谈崩不再派，照原来的宣战流程走；收了私钱不算谈崩。
    /// 要单挑的约到原版比武大会，名次在前者胜——只有这一种结局给玩家提示，玩家可以去看、也可以下场。
    /// </summary>
    public partial class PoliceEnforcementBehavior
    {
        private enum EnvoyStage
        {
            /// <summary>使者在路上。期间不宣战。</summary>
            Travelling = 0,
            /// <summary>谈崩、使者没了或超时。这宗案子不再派使者，照常宣战。</summary>
            Failed = 1,
            /// <summary>他求了宽限。承办人当场放下案子去忙别的，到期由最近的灰袍领主派一趟只收钱的使者。</summary>
            Grace = 2,
            /// <summary>双方约在比武大会，留城等赛。</summary>
            Duel = 3,
            /// <summary>比武结果已出（在任务场景里得出），下一个小时结算。</summary>
            DuelDecided = 4,
        }

        /// <summary>一宗案子的使者进度，按案卷记。谈崩记录留到销案为止，所以同一宗案子不会再派。</summary>
        private sealed class EnvoyCase
        {
            public string CrimeId = string.Empty;
            public string WardenPartyId = string.Empty;
            public string EnvoyPartyId = string.Empty;
            public EnvoyStage Stage;
            /// <summary>使者超时、宽限到期或比武截止的时刻（小时）。</summary>
            public double DueHours;
            public string TownId = string.Empty;
            /// <summary>比武作废或案子被别人了结时，要放开的那支原版部队。</summary>
            public string TargetPartyId = string.Empty;
            /// <summary>宽限到期后的这一趟只收钱，不再谈。</summary>
            public bool CollectOnly;
            public bool WardenWon;
        }

        private readonly Dictionary<string, EnvoyCase> _envoyCases =
            new Dictionary<string, EnvoyCase>(StringComparer.OrdinalIgnoreCase);

        private void SyncEnvoyData(IDataStore dataStore)
        {
            List<string> crimeIds = null!;
            List<string> wardenIds = null!;
            List<string> envoyIds = null!;
            List<string> townIds = null!;
            List<string> targetIds = null!;
            List<int> stages = null!;
            List<int> flags = null!;
            List<double> dueHours = null!;
            if (dataStore.IsSaving)
            {
                List<EnvoyCase> cases = _envoyCases.Values.ToList();
                crimeIds = cases.Select(c => c.CrimeId).ToList();
                wardenIds = cases.Select(c => c.WardenPartyId).ToList();
                envoyIds = cases.Select(c => c.EnvoyPartyId).ToList();
                townIds = cases.Select(c => c.TownId).ToList();
                targetIds = cases.Select(c => c.TargetPartyId).ToList();
                stages = cases.Select(c => (int)c.Stage).ToList();
                flags = cases.Select(c => (c.CollectOnly ? 1 : 0) | (c.WardenWon ? 2 : 0)).ToList();
                dueHours = cases.Select(c => c.DueHours).ToList();
            }

            dataStore.SyncData("gwp_enf_envoy_crimes", ref crimeIds);
            dataStore.SyncData("gwp_enf_envoy_wardens", ref wardenIds);
            dataStore.SyncData("gwp_enf_envoy_parties", ref envoyIds);
            dataStore.SyncData("gwp_enf_envoy_towns", ref townIds);
            dataStore.SyncData("gwp_enf_envoy_targets", ref targetIds);
            dataStore.SyncData("gwp_enf_envoy_stages", ref stages);
            dataStore.SyncData("gwp_enf_envoy_flags", ref flags);
            dataStore.SyncData("gwp_enf_envoy_due", ref dueHours);

            if (!dataStore.IsLoading) return;
            _envoyCases.Clear();
            if (crimeIds == null) return;
            for (int i = 0; i < crimeIds.Count; i++)
            {
                if (string.IsNullOrEmpty(crimeIds[i])) continue;
                int flag = flags != null && i < flags.Count ? flags[i] : 0;
                _envoyCases[crimeIds[i]] = new EnvoyCase
                {
                    CrimeId = crimeIds[i],
                    WardenPartyId = wardenIds != null && i < wardenIds.Count ? wardenIds[i] : string.Empty,
                    EnvoyPartyId = envoyIds != null && i < envoyIds.Count ? envoyIds[i] : string.Empty,
                    TownId = townIds != null && i < townIds.Count ? townIds[i] : string.Empty,
                    TargetPartyId = targetIds != null && i < targetIds.Count ? targetIds[i] : string.Empty,
                    Stage = stages != null && i < stages.Count ? (EnvoyStage)stages[i] : EnvoyStage.Failed,
                    CollectOnly = (flag & 1) != 0,
                    WardenWon = (flag & 2) != 0,
                    DueHours = dueHours != null && i < dueHours.Count ? dueHours[i] : 0d
                };
            }
        }

        /// <summary>
        /// UpdateTasks 在宣战判定前调用。返回 true 表示这宗案子此刻不宣战：使者在路上、他在宽限期里，
        /// 或者双方约了比武。没派过使者的案子在这里派。谈崩记在案子上，换了承办人也不再派。
        /// </summary>
        private bool HoldWarForEnvoy(MobileParty police, PoliceTask task, MobileParty criminal)
        {
            CrimeRecord? crime = task.TargetCrime;
            if (task.WarDeclared || criminal.IsMainParty || crime == null)
                return false;
            if (_envoyCases.TryGetValue(crime.CrimeId, out EnvoyCase? envoyCase))
                return envoyCase.Stage != EnvoyStage.Failed;
            return TrySendEnvoy(police, crime, criminal, false);
        }

        /// <summary>他在宽限期里，或收钱的使者正在路上：这宗案子谁都不接（用户裁定 2026-10-15）。</summary>
        private bool IsAwaitingEnvoyCollection(CrimeRecord crime) =>
            _envoyCases.TryGetValue(crime.CrimeId, out EnvoyCase? envoyCase) &&
            (envoyCase.Stage == EnvoyStage.Grace ||
             envoyCase.Stage == EnvoyStage.Travelling && envoyCase.CollectOnly);

        private bool TrySendEnvoy(MobileParty police, CrimeRecord crime, MobileParty criminal, bool collectOnly)
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            // 他替别人的军团跑腿时作不了主；已经和灰袍开战的势力，使者过去就是送死。
            if (policeClan == null || criminal.LeaderHero == null ||
                police.MapEvent != null || criminal.MapEvent != null ||
                police.Army != null && police.Army.LeaderParty != police ||
                criminal.Army != null && criminal.Army.LeaderParty != criminal ||
                PlayerBountyBehavior.IsReservedTarget(criminal) ||
                FactionManager.IsAtWarAgainstFaction(policeClan, criminal.MapFaction) ||
                criminal.IsCurrentlyAtSea || PoliceResourceManager.IsStrandedAtSea(police))
                return false;

            // 用户裁定 2026-10-15：使者按追截队的骑兵来排，只从承办领主自己队里抽。
            List<ImmediateInterceptorTroopTransfer> batches =
                MountedWardenBatches(police, new[] { police });
            int size = Math.Min(GwpTuning.Enforcement.EnvoySize, HealthyTroopCount(batches));
            if (size < GwpTuning.Enforcement.EnvoyMinimumSize)
                return false;

            var transferred = new List<ImmediateInterceptorTroopTransfer>();
            MobileParty? envoy = CreateDetachment(police, batches, size,
                GwpTuning.Enforcement.EnvoyMinimumSize,
                GwpText.Get("{=gwp_envoy_party_name}Grey Warden envoy"),
                transferred, out int moved, out Settlement? returnSettlement);
            if (envoy == null || returnSettlement == null)
                return false;

            PoliceResourceManager.OutfitTemporaryDutyParty(envoy, police);
            _delayPatrolStates[envoy.StringId] = new DelayPatrolState
            {
                PatrolPartyId = envoy.StringId,
                SourceTaskPolicePartyId = police.StringId,
                TargetPartyId = criminal.StringId,
                ReturnSettlementId = returnSettlement.StringId,
                IsEnvoy = true
            };
            _envoyCases[crime.CrimeId] = new EnvoyCase
            {
                CrimeId = crime.CrimeId,
                WardenPartyId = police.StringId,
                EnvoyPartyId = envoy.StringId,
                TargetPartyId = criminal.StringId,
                Stage = EnvoyStage.Travelling,
                DueHours = CampaignTime.Now.ToHours + GwpTuning.Enforcement.EnvoyTimeoutHours,
                CollectOnly = collectOnly
            };
            // 跟随中立目标不会触发遭遇（只有 EngageParty 才会），所以用护送去追。
            GreyWardenPartyDesireBehavior.RequestEscort(envoy, criminal, 8f);
            GwpAiDiagnostics.WriteAction(police, "ENVOY_SENT",
                "envoy=" + envoy.StringId + "; target=" + criminal.StringId +
                "; crime=" + crime.CrimeId + "; troops=" + moved + "; collectOnly=" + collectOnly);
            return true;
        }

        /// <summary>每小时照看所有使者案：赶路、接触、宽限到期、比武等待与结算。</summary>
        private void TendEnvoyCases()
        {
            foreach (EnvoyCase envoyCase in _envoyCases.Values.ToList())
            {
                CrimeRecord? crime = CrimePool.GetRecordByKey(envoyCase.CrimeId);
                if (crime?.HasOpenCase != true)
                {
                    if (envoyCase.Stage == EnvoyStage.Duel || envoyCase.Stage == EnvoyStage.DuelDecided)
                        ReleaseDuelists(envoyCase);
                    ForgetEnvoyCase(envoyCase, "case_closed");
                    continue;
                }

                switch (envoyCase.Stage)
                {
                    case EnvoyStage.Travelling:
                        TendTravellingEnvoy(envoyCase, crime);
                        break;
                    case EnvoyStage.Grace:
                        if (CampaignTime.Now.ToHours >= envoyCase.DueHours)
                            SendGraceCollector(envoyCase, crime);
                        break;
                    case EnvoyStage.Duel:
                        TendDuel(envoyCase, crime);
                        break;
                    case EnvoyStage.DuelDecided:
                        FinishDuel(envoyCase, crime);
                        break;
                }
            }
        }

        private void TendTravellingEnvoy(EnvoyCase envoyCase, CrimeRecord crime)
        {
            MobileParty? envoy = FindActiveParty(envoyCase.EnvoyPartyId);
            if (envoy == null)
            {
                FailEnvoy(envoyCase, "envoy_lost");
                return;
            }

            // 承办人放下了这宗案子（改追别人、被调走）：使者回去，案子留给下一个人，不算谈崩。
            // 宽限后去收钱的那趟本来就没有承办人。
            PoliceTask? task = TaskForCrime(crime);
            MobileParty? target = crime.Offender;
            bool ownerLeft = !envoyCase.CollectOnly && (task == null ||
                !string.Equals(task.PolicePartyId, envoyCase.WardenPartyId, StringComparison.OrdinalIgnoreCase));
            if (ownerLeft || target?.IsActive != true)
            {
                MarkDelayPatrolReturning(envoy.StringId);
                ForgetEnvoyCase(envoyCase, "case_left_owner");
                return;
            }

            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (CampaignTime.Now.ToHours >= envoyCase.DueHours || policeClan == null ||
                FactionManager.IsAtWarAgainstFaction(policeClan, target.MapFaction))
            {
                FailEnvoy(envoyCase, "envoy_timeout_or_war");
                return;
            }

            if (envoy.MapEvent != null || target.MapEvent != null)
                return;

            // 与追截队回主队同一个距离：原版军团成员算"走到一起"的距离。目标在城里时进城去谈。
            bool reached = target.CurrentSettlement != null && envoy.CurrentSettlement == target.CurrentSettlement ||
                envoy.GetPosition2D.Distance(target.GetPosition2D) <=
                Campaign.Current.Models.EncounterModel.MaximumAllowedLandDistanceForEncounteringMobilePartyInArmy;
            if (!reached)
            {
                GreyWardenPartyDesireBehavior.RequestEscort(envoy, target, 8f);
                return;
            }

            MobileParty? police = FindActiveParty(envoyCase.WardenPartyId);
            Hero? offender = target.LeaderHero;
            if (police?.LeaderHero == null || offender == null)
            {
                FailEnvoy(envoyCase, "no_one_to_speak");
                return;
            }

            MarkDelayPatrolReturning(envoy.StringId);
            Negotiate(envoyCase, crime, police, police.LeaderHero, envoy, target, offender);
        }

        /// <summary>
        /// 使者当面谈。规则与玩家当面谈相同（GwpNegotiationPolicy），只是没有对话，按概率直接出结果。
        /// </summary>
        private void Negotiate(EnvoyCase envoyCase, CrimeRecord crime, MobileParty police, Hero warden,
            MobileParty envoy, MobileParty target, Hero offender)
        {
            int fine = GwpFieldArrestPricing.AssessFine(crime);
            if (envoyCase.CollectOnly)
            {
                // 宽限到期：钱够就照数收，不够就算他食言。
                if (offender.Gold >= fine)
                    SettleThroughEnvoy(envoyCase, crime, envoy, offender, fine, "envoy_grace_paid");
                else
                    FailEnvoy(envoyCase, "envoy_grace_broke");
                return;
            }

            // 用户裁定 2026-10-15：第一层看灰袍主队和目标队伍的实力，不看使者自己。
            float ours = GwpStrength.Party(police);
            float theirs = GwpStrength.Party(target);
            int wealth = GwpAssetPayment.Wealth(offender, target.Party);
            int charm = warden.GetSkillValue(DefaultSkills.Charm);
            float renown = warden.Clan?.Renown ?? 0f;
            int layerOne = GwpNegotiationPolicy.LayerOneResistance(offender, crime.CrimeCategory, ours, theirs,
                GwpFieldArrestBehavior.WardenStandingTier.Ordinary, renown, wealth, fine);
            double refusal = GwpNegotiationPolicy.RefusalChance(layerOne,
                offender.GetTraitLevel(DefaultTraits.Honor), offender.GetTraitLevel(DefaultTraits.Mercy),
                offender.GetTraitLevel(DefaultTraits.Generosity), wealth, fine);
            GwpOffenderDesire desire = GwpOffenderDesires.Roll(offender, fine, true,
                target.MemberRoster.GetTroopRoster().Any(e => !e.Character.IsHero && e.Number > 0));
            int layerTwo = GwpNegotiationPolicy.LayerTwoResistance(offender, desire, ours, theirs,
                charm, renown, wealth, fine);
            bool listens = refusal < 0.55d;
            bool acknowledged = listens && GwpNegotiationPolicy.SimulatePersuasion(
                GwpNegotiationPolicy.LayerOneReservations, layerOne, true, charm);
            bool paysInFull = acknowledged && (GwpOffenderDesires.IsFullPayment(desire) ||
                GwpNegotiationPolicy.SimulatePersuasion(GwpNegotiationPolicy.LayerTwoReservations, layerTwo, false, charm));
            GwpAiDiagnostics.WriteAction(police, "ENVOY_NEGOTIATED",
                "envoy=" + envoy.StringId + "; offender=" + offender.StringId +
                "; ours=" + ours.ToString("0") + "; theirs=" + theirs.ToString("0") +
                "; fine=" + fine + "; wealth=" + wealth + "; gold=" + offender.Gold +
                "; layerOne=" + layerOne + "; refusal=" + refusal.ToString("0.00") +
                "; acknowledged=" + acknowledged + "; desire=" + desire + "; layerTwo=" + layerTwo +
                "; paysInFull=" + paysInFull);

            if (!acknowledged)
            {
                FailEnvoy(envoyCase, listens ? "envoy_layer_one_failed" : "envoy_refused");
                return;
            }
            if (paysInFull)
            {
                SettleThroughEnvoy(envoyCase, crime, envoy, offender, fine, "envoy_paid_in_full");
                return;
            }

            // 第二层没谈拢：照他自己的打算办。
            switch (desire)
            {
                case GwpOffenderDesire.RequestGrace:
                    // 用户裁定 2026-10-15：给了宽限，承办人直接去接下一个差事，到期派使者收钱。
                    envoyCase.Stage = EnvoyStage.Grace;
                    envoyCase.DueHours = CampaignTime.Now.ToHours + GwpTuning.FieldArrest.GraceHours;
                    PoliceTask? graceTask = TaskForCrime(crime);
                    if (graceTask != null)
                        EndCaseTask(graceTask.PolicePartyId, CaseTaskEnd.Released, "envoy_grace");
                    return;
                case GwpOffenderDesire.DemandDuel:
                    StartDuel(envoyCase, police, target);
                    return;
                case GwpOffenderDesire.SurrenderSelf:
                    // 交到灰袍手里的那一刻，原版抓捕事件会登记震慑、清案底、销案（GwpAiDeterrenceState）。
                    TakePrisonerAction.Apply(envoy.Party, offender);
                    ForgetEnvoyCase(envoyCase, "envoy_surrender");
                    return;
                case GwpOffenderDesire.BribeTheWarden:
                    // 用户裁定 2026-10-15：收私钱不算谈崩。钱进承办领主的口袋，案子销掉，案底一点不少。
                    int bribe = Math.Min(offender.Gold,
                        Math.Max(1, fine * GwpTuning.FieldArrest.BribeSharePercent / 100));
                    if (bribe > 0)
                        GiveGoldAction.ApplyBetweenCharacters(offender, warden, bribe, true);
                    GwpFieldArrestBehavior.RegisterFieldEnforcement(offender, crime);
                    ResolveEnvoyCase(envoyCase, crime, false, "envoy_bribed");
                    return;
                case GwpOffenderDesire.DeedsOnly:
                    GwpOffenderDesires.SacrificeOwnMen(target);
                    break;
            }
            SettleThroughEnvoy(envoyCase, crime, envoy, offender,
                fine * GwpOffenderDesires.OfferedSharePercent(desire) / 100, "envoy_paid_share");
        }

        /// <summary>他交钱了结。钱交给使者带回公库；他名下的账按实缴清，和玩家当面收钱一样。</summary>
        private void SettleThroughEnvoy(EnvoyCase envoyCase, CrimeRecord crime, MobileParty envoy,
            Hero offender, int demanded, string reason)
        {
            int collected = Math.Min(Math.Max(0, offender.Gold), demanded);
            if (collected > 0)
                GiveGoldAction.ApplyForCharacterToParty(offender, envoy.Party, collected, true);
            GwpFieldReportLedger.SettleWithWardens(offender, GwpFieldArrestPricing.AssessFine(crime),
                collected, GwpFieldArrestPricing.BaseChargeFor(crime));
            GwpFieldArrestBehavior.RegisterFieldEnforcement(offender, crime);
            ResolveEnvoyCase(envoyCase, crime, true, reason + "_" + collected);
        }

        /// <summary>
        /// 宽限到期：离他最近、派得出骑兵的灰袍领主派一趟只收钱的使者。这一小时谁都派不出（都没骑兵、他在海上等）
        /// 就下一小时再试；过了到期后一个使者超时还派不出，算他食言，案子回台账照常追捕。
        /// </summary>
        private void SendGraceCollector(EnvoyCase envoyCase, CrimeRecord crime)
        {
            MobileParty? criminal = crime.Offender;
            if (criminal?.IsActive == true)
            {
                foreach (MobileParty police in PoliceStats.GetAllPoliceParties()
                             .Where(party => party?.IsActive == true && party.IsLordParty &&
                                 CanContinueLeadingPoliceTask(party))
                             .OrderBy(party => party.GetPosition2D.Distance(criminal.GetPosition2D)))
                {
                    if (TrySendEnvoy(police, crime, criminal, true))
                        return;
                }
            }
            if (CampaignTime.Now.ToHours >= envoyCase.DueHours + GwpTuning.Enforcement.EnvoyTimeoutHours)
                FailEnvoy(envoyCase, "grace_collector_unavailable");
        }

        #region 比武

        /// <summary>
        /// 他要单挑：约到原版比武大会（用户裁定 2026-10-15）。最近三座能进的城里有比武就去那一座，
        /// 都没有就在最近的城新开一场。两人留城等赛。
        /// </summary>
        private void StartDuel(EnvoyCase envoyCase, MobileParty police, MobileParty target)
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            Vec2 middle = (police.GetPosition2D + target.GetPosition2D) * 0.5f;
            List<Town> towns = Town.AllTowns
                .Where(town => town.IsTown && !town.IsUnderSiege && !town.InRebelliousState &&
                    policeClan != null &&
                    !FactionManager.IsAtWarAgainstFaction(town.MapFaction, policeClan) &&
                    !FactionManager.IsAtWarAgainstFaction(town.MapFaction, target.MapFaction))
                .OrderBy(town => town.Settlement.GetPosition2D.DistanceSquared(middle))
                .Take(GwpTuning.Enforcement.TournamentDuelNearbyTowns)
                .ToList();
            Town? venue = towns.FirstOrDefault(town =>
                Campaign.Current.TournamentManager.GetTournamentGame(town) != null) ?? towns.FirstOrDefault();
            if (venue == null)
            {
                FailEnvoy(envoyCase, "duel_no_town");
                return;
            }
            bool existingTournament = Campaign.Current.TournamentManager.GetTournamentGame(venue) != null;
            if (!existingTournament)
                Campaign.Current.TournamentManager.AddTournament(
                    Campaign.Current.Models.TournamentModel.CreateTournament(venue));

            ReleaseAssistanceGroup(police.StringId, "envoy_duel");
            envoyCase.Stage = EnvoyStage.Duel;
            envoyCase.TownId = venue.Settlement.StringId;
            envoyCase.DueHours = CampaignTime.Now.ToHours + GwpTuning.Enforcement.TournamentDuelTimeoutDays * 24d;
            HoldForDuel(police, venue.Settlement, true);
            HoldForDuel(target, venue.Settlement, false);
            GwpAiDiagnostics.WriteAction(police, "ENVOY_DUEL_CALLED",
                "offender=" + target.StringId + "; town=" + venue.Settlement.StringId +
                "; existingTournament=" + existingTournament);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_envoy_duel_called}{VAR_1} has challenged {VAR_2} to settle the Wardens' charge at the tournament in {VAR_3}.",
                    "VAR_1", target.LeaderHero?.Name?.ToString() ?? string.Empty,
                    "VAR_2", police.LeaderHero?.Name?.ToString() ?? string.Empty,
                    "VAR_3", venue.Name?.ToString() ?? string.Empty),
                Colors.Yellow));
        }

        private void TendDuel(EnvoyCase envoyCase, CrimeRecord crime)
        {
            MobileParty? police = FindActiveParty(envoyCase.WardenPartyId);
            MobileParty? target = crime.Offender;
            Settlement? town = Settlement.Find(envoyCase.TownId);
            if (police?.LeaderHero == null || target?.IsActive != true || target.LeaderHero == null ||
                town?.Town == null || town.Town.IsUnderSiege)
            {
                // 有一方来不了（战败、被俘、城被围）：约定作废，照常追捕。
                ReleaseDuelists(envoyCase);
                FailEnvoy(envoyCase, "duel_cancelled");
                return;
            }

            HoldForDuel(police, town, true);
            HoldForDuel(target, town, false);
            TournamentGame? game = Campaign.Current.TournamentManager.GetTournamentGame(town.Town);
            bool bothPresent = police.CurrentSettlement == town && target.CurrentSettlement == town;
            if (CampaignTime.Now.ToHours < envoyCase.DueHours)
            {
                // 别人赶在他们到之前把那场打完了，就再开一场。
                if (game == null)
                    Campaign.Current.TournamentManager.AddTournament(
                        Campaign.Current.Models.TournamentModel.CreateTournament(town.Town));
                return;
            }

            // 到了截止时间：两人都在城里，就按原版的结算把这场打完；有人没到，按原版比武评分再比一次。
            if (bothPresent && game != null)
                Campaign.Current.TournamentManager.ResolveTournament(game, town.Town);
            if (envoyCase.Stage == EnvoyStage.Duel)
            {
                envoyCase.WardenWon = WardenOutscores(police.LeaderHero, target.LeaderHero);
                envoyCase.Stage = EnvoyStage.DuelDecided;
            }
            FinishDuel(envoyCase, crime);
        }

        /// <summary>
        /// 比武结束（AI 自行结算或玩家在场）。名次靠前者胜；都没夺冠又分不出先后，按原版比武评分再比一次
        /// （用户裁定 2026-10-15）。这里可能在任务场景里，结算推到下一个小时做。
        /// </summary>
        private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants,
            Town town, ItemObject prize)
        {
            foreach (EnvoyCase envoyCase in _envoyCases.Values.Where(c => c.Stage == EnvoyStage.Duel &&
                         string.Equals(c.TownId, town.Settlement.StringId, StringComparison.OrdinalIgnoreCase)))
            {
                Hero? warden = FindActiveParty(envoyCase.WardenPartyId)?.LeaderHero;
                Hero? offender = CrimePool.GetRecordByKey(envoyCase.CrimeId)?.OffenderHero;
                if (warden == null || offender == null ||
                    warden.CurrentSettlement != town.Settlement || offender.CurrentSettlement != town.Settlement)
                    continue;
                int wardenPlace = TournamentPlace(warden, winner);
                int offenderPlace = TournamentPlace(offender, winner);
                envoyCase.WardenWon = wardenPlace != offenderPlace
                    ? wardenPlace > offenderPlace
                    : WardenOutscores(warden, offender);
                envoyCase.Stage = EnvoyStage.DuelDecided;
            }
        }

        /// <summary>
        /// 打到了第几轮（0–3），夺冠记 4，没上场记 −1。AI 自行结算的比武原版只算出冠军，
        /// 其余人都是 −1；玩家在场的那场读 SandBox <c>TournamentBehavior.Rounds</c>。
        /// </summary>
        private static int TournamentPlace(Hero hero, CharacterObject winner)
        {
            if (winner == hero.CharacterObject) return TournamentBehavior.RoundCount;
            TournamentBehavior? tournament = Mission.Current?.GetMissionBehavior<TournamentBehavior>();
            if (tournament?.Rounds == null) return -1;
            for (int round = tournament.Rounds.Length - 1; round >= 0; round--)
            {
                if (tournament.Rounds[round]?.Matches?.Any(match =>
                        match?.Participants.Any(p => p.Character == hero.CharacterObject) == true) == true)
                    return round;
            }
            return -1;
        }

        /// <summary>原版比武模拟同一个式子：评分 × (0.75 + 0.25 × 随机)（C#反编译 v1.4.8 TournamentManager.SimulateTournament）。</summary>
        private static bool WardenOutscores(Hero warden, Hero offender)
        {
            var model = Campaign.Current.Models.TournamentModel;
            float wardenScore = model.GetTournamentSimulationScore(warden.CharacterObject) * (0.75f + 0.25f * MBRandom.RandomFloat);
            float offenderScore = model.GetTournamentSimulationScore(offender.CharacterObject) * (0.75f + 0.25f * MBRandom.RandomFloat);
            return wardenScore >= offenderScore;
        }

        private void FinishDuel(EnvoyCase envoyCase, CrimeRecord crime)
        {
            ReleaseDuelists(envoyCase);
            MobileParty? police = FindActiveParty(envoyCase.WardenPartyId);
            Hero? offender = crime.OffenderHero;
            Hero? warden = police?.LeaderHero;
            Settlement? town = Settlement.Find(envoyCase.TownId);
            if (offender == null || warden == null)
            {
                FailEnvoy(envoyCase, "duel_party_missing");
                return;
            }

            GwpAiDiagnostics.WriteAction(police!, "ENVOY_DUEL_FINISHED",
                "offender=" + offender.StringId + "; wardenWon=" + envoyCase.WardenWon +
                "; town=" + envoyCase.TownId);
            string offenderName = offender.Name?.ToString() ?? string.Empty;
            string wardenName = warden.Name?.ToString() ?? string.Empty;
            string townName = town?.Name?.ToString() ?? string.Empty;
            if (envoyCase.WardenWon)
            {
                // 他输了照数全缴。人就在承办领主跟前，钱直接入公库。
                int fine = GwpFieldArrestPricing.AssessFine(crime);
                int collected = Math.Min(Math.Max(0, offender.Gold), fine);
                if (collected > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(offender, null, collected, true);
                    PoliceResourceManager.CreditJudicialTreasuryFromCourier(collected);
                }
                GwpFieldReportLedger.SettleWithWardens(offender, fine, collected, GwpFieldArrestPricing.BaseChargeFor(crime));
                GwpFieldArrestBehavior.RegisterFieldEnforcement(offender, crime);
                ResolveEnvoyCase(envoyCase, crime, true, "envoy_duel_won_" + collected);
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_envoy_duel_warden_won}{VAR_1} placed above {VAR_2} at the tournament in {VAR_3}. {VAR_2} pays the Wardens in full.",
                        "VAR_1", wardenName, "VAR_2", offenderName, "VAR_3", townName),
                    Colors.Green));
                return;
            }

            // 他赢了按约免罚：案子销掉，案底照留。
            GwpFieldArrestBehavior.RegisterFieldEnforcement(offender, crime);
            ResolveEnvoyCase(envoyCase, crime, false, "envoy_duel_lost");
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_envoy_duel_offender_won}{VAR_1} placed above {VAR_2} at the tournament in {VAR_3}, and the Wardens let him go as agreed.",
                    "VAR_1", offenderName, "VAR_2", wardenName, "VAR_3", townName),
                Colors.Yellow));
        }

        /// <summary>
        /// 留城等赛。灰袍领主靠欲望层的去城意图（外部意图压过案件追捕）；目标是原版领主，
        /// 直接下令进城并停掉他自己的决策，比武了结或作废时由 <see cref="ReleaseDuelists"/> 放开。
        /// 原版把停决策这一位存进存档（MobilePartyAi.DoNotMakeNewDecisions），读档后接得上。
        /// </summary>
        private static void HoldForDuel(MobileParty party, Settlement town, bool warden)
        {
            if (warden)
            {
                GreyWardenPartyDesireBehavior.RequestVisit(party, town, 8f);
                return;
            }
            if (party.MapEvent != null) return;
            if (party.CurrentSettlement != town && party.TargetSettlement != town)
            {
                party.Ai.SetDoNotMakeNewDecisions(false);
                party.SetMoveGoToSettlement(town, party.NavigationCapability, false);
            }
            party.Ai.SetDoNotMakeNewDecisions(true);
        }

        private static void ReleaseDuelists(EnvoyCase envoyCase)
        {
            MobileParty? police = FindActiveParty(envoyCase.WardenPartyId);
            if (police != null)
                GreyWardenPartyDesireBehavior.ClearIntent(police);
            GwpCommon.TryResetAi(FindActiveParty(envoyCase.TargetPartyId));
        }

        #endregion

        private static PoliceTask? TaskForCrime(CrimeRecord crime) =>
            CrimeState.ActiveTasks.Values.FirstOrDefault(task =>
                string.Equals(task.TargetCrimeId, crime.CrimeId, StringComparison.OrdinalIgnoreCase));

        /// <summary>谈出了结果：有承办人就按结局收尾（办成记功），没有就直接销卷。</summary>
        private void ResolveEnvoyCase(EnvoyCase envoyCase, CrimeRecord crime, bool solved, string reason)
        {
            _envoyCases.Remove(envoyCase.CrimeId);
            PoliceTask? task = TaskForCrime(crime);
            if (task != null)
            {
                EndCaseTask(task.PolicePartyId, solved ? CaseTaskEnd.Solved : CaseTaskEnd.Closed, reason);
                return;
            }
            // 宽限后收钱那趟没有承办人，办成照样记一笔办案经费。
            CloseCase(crime, reason);
            if (solved)
                PoliceResourceManager.CreditSuccessfulCaseCompletion();
        }

        /// <summary>谈崩：使者回去，这宗案子不再派（用户裁定 2026-10-15）。</summary>
        private void FailEnvoy(EnvoyCase envoyCase, string reason)
        {
            if (!string.IsNullOrEmpty(envoyCase.EnvoyPartyId) &&
                _delayPatrolStates.ContainsKey(envoyCase.EnvoyPartyId))
                MarkDelayPatrolReturning(envoyCase.EnvoyPartyId);
            envoyCase.Stage = EnvoyStage.Failed;
            MobileParty? police = FindActiveParty(envoyCase.WardenPartyId);
            if (police != null)
                GwpAiDiagnostics.WriteAction(police, "ENVOY_FAILED",
                    "crime=" + envoyCase.CrimeId + "; reason=" + reason);
        }

        private void ForgetEnvoyCase(EnvoyCase envoyCase, string reason)
        {
            _envoyCases.Remove(envoyCase.CrimeId);
            MobileParty? police = FindActiveParty(envoyCase.WardenPartyId);
            if (police != null)
                GwpAiDiagnostics.WriteAction(police, "ENVOY_CASE_DROPPED",
                    "crime=" + envoyCase.CrimeId + "; stage=" + envoyCase.Stage + "; reason=" + reason);
        }
    }
}
