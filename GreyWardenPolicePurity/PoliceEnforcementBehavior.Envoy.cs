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
        internal enum EnvoyStage
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
            /// <summary>案子已结，使者等他打完仗再押人或看他杀兵（<see cref="BeginAftermath"/>）。</summary>
            Aftermath = 5,
            /// <summary>已答应单挑，承办人先履行已有玩家事务或另一场约斗，再选城赴会。</summary>
            WaitingForDuel = 6,
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
            /// <summary>Aftermath 阶段等着办的那件事（投案或杀自己的兵）；−1 表示没有。</summary>
            public int PendingDesire = -1;
        }

        private readonly Dictionary<string, EnvoyCase> _envoyCases =
            new Dictionary<string, EnvoyCase>(StringComparer.OrdinalIgnoreCase);

        /// <summary>案件台账显示的使者进度（用户裁定 2026-10-16）。</summary>
        internal readonly struct EnvoyStatus
        {
            internal EnvoyStatus(EnvoyStage stage, bool collectOnly, double dueHours, string townId)
            {
                Stage = stage;
                CollectOnly = collectOnly;
                DueHours = dueHours;
                TownId = townId;
            }

            internal EnvoyStage Stage { get; }
            internal bool CollectOnly { get; }
            internal double DueHours { get; }
            internal string TownId { get; }
        }

        internal static EnvoyStatus? GetEnvoyStatus(string crimeId) =>
            _instance != null && _instance._envoyCases.TryGetValue(crimeId, out EnvoyCase? envoyCase)
                ? new EnvoyStatus(envoyCase.Stage, envoyCase.CollectOnly, envoyCase.DueHours, envoyCase.TownId)
                : (EnvoyStatus?)null;

        /// <summary>
        /// 一次使者谈判的结果，给案件台账看（用户裁定 2026-10-16：显示使者进度和结果）。案子结了卷宗就没了，
        /// 所以人和结果当场记下来，只留最近 <see cref="GwpTuning.Enforcement.EnvoyReportsKept"/> 条，进存档。
        /// </summary>
        internal sealed class EnvoyReport
        {
            public double Hours;
            public string OffenderHeroId = string.Empty;
            public string WardenHeroId = string.Empty;
            /// <summary>结果代码，台账按它选文本：grace、duel_called、paid、paid_share、surrender、bribed、
            /// duel_won、duel_lost、refused、talks_failed、grace_broke、lost、timeout、duel_cancelled、failed。</summary>
            public string Outcome = string.Empty;
            public int Amount;
            public string TownId = string.Empty;
        }

        private readonly List<EnvoyReport> _envoyReports = new List<EnvoyReport>();

        internal static IReadOnlyList<EnvoyReport> GetEnvoyReports() =>
            _instance?._envoyReports ?? new List<EnvoyReport>();

        private void RecordEnvoyReport(EnvoyCase envoyCase, string outcome, int amount)
        {
            MobileParty? police = FindActiveParty(envoyCase.WardenPartyId);
            var report = new EnvoyReport
            {
                Hours = CampaignTime.Now.ToHours,
                OffenderHeroId = CrimePool.GetRecordByKey(envoyCase.CrimeId)?.OffenderHeroId ?? string.Empty,
                WardenHeroId = police?.LeaderHero?.StringId ?? string.Empty,
                Outcome = outcome,
                Amount = amount,
                TownId = envoyCase.TownId
            };
            _envoyReports.Insert(0, report);
            if (_envoyReports.Count > GwpTuning.Enforcement.EnvoyReportsKept)
                _envoyReports.RemoveRange(GwpTuning.Enforcement.EnvoyReportsKept,
                    _envoyReports.Count - GwpTuning.Enforcement.EnvoyReportsKept);
#if GWP_DIAGNOSTICS
            if (police != null)
                GwpAiDiagnostics.WriteAction(police, "ENVOY_REPORT",
                    "crime=" + envoyCase.CrimeId + "; offender=" + report.OffenderHeroId +
                    "; outcome=" + outcome + "; amount=" + amount + "; collectOnly=" + envoyCase.CollectOnly +
                    "; helper=" + IsAssistanceMember(police.StringId));
#endif
        }

        /// <summary>谈崩的原因归成台账上的几种说法。</summary>
        private static string FailureOutcome(string reason) => reason switch
        {
            "envoy_refused" => "refused",
            "envoy_layer_one_failed" => "talks_failed",
            "envoy_grace_broke" => "grace_broke",
            "envoy_lost" => "lost",
            "envoy_timeout_or_war" => "timeout",
            _ when reason.StartsWith("duel_", StringComparison.Ordinal) => "duel_cancelled",
            _ => "failed"
        };

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
            List<int> pending = null!;
            List<double> reportHours = null!;
            List<string> reportOffenders = null!;
            List<string> reportWardens = null!;
            List<string> reportOutcomes = null!;
            List<int> reportAmounts = null!;
            List<string> reportTowns = null!;
            if (dataStore.IsSaving)
            {
                reportHours = _envoyReports.Select(r => r.Hours).ToList();
                reportOffenders = _envoyReports.Select(r => r.OffenderHeroId).ToList();
                reportWardens = _envoyReports.Select(r => r.WardenHeroId).ToList();
                reportOutcomes = _envoyReports.Select(r => r.Outcome).ToList();
                reportAmounts = _envoyReports.Select(r => r.Amount).ToList();
                reportTowns = _envoyReports.Select(r => r.TownId).ToList();
                List<EnvoyCase> cases = _envoyCases.Values.ToList();
                crimeIds = cases.Select(c => c.CrimeId).ToList();
                wardenIds = cases.Select(c => c.WardenPartyId).ToList();
                envoyIds = cases.Select(c => c.EnvoyPartyId).ToList();
                townIds = cases.Select(c => c.TownId).ToList();
                targetIds = cases.Select(c => c.TargetPartyId).ToList();
                stages = cases.Select(c => (int)c.Stage).ToList();
                flags = cases.Select(c => (c.CollectOnly ? 1 : 0) | (c.WardenWon ? 2 : 0)).ToList();
                dueHours = cases.Select(c => c.DueHours).ToList();
                pending = cases.Select(c => c.PendingDesire).ToList();
            }

            dataStore.SyncData("gwp_enf_envoy_crimes", ref crimeIds);
            dataStore.SyncData("gwp_enf_envoy_wardens", ref wardenIds);
            dataStore.SyncData("gwp_enf_envoy_parties", ref envoyIds);
            dataStore.SyncData("gwp_enf_envoy_towns", ref townIds);
            dataStore.SyncData("gwp_enf_envoy_targets", ref targetIds);
            dataStore.SyncData("gwp_enf_envoy_stages", ref stages);
            dataStore.SyncData("gwp_enf_envoy_flags", ref flags);
            dataStore.SyncData("gwp_enf_envoy_due", ref dueHours);
            dataStore.SyncData("gwp_enf_envoy_pending", ref pending);
            dataStore.SyncData("gwp_enf_envoy_report_hours", ref reportHours);
            dataStore.SyncData("gwp_enf_envoy_report_offenders", ref reportOffenders);
            dataStore.SyncData("gwp_enf_envoy_report_wardens", ref reportWardens);
            dataStore.SyncData("gwp_enf_envoy_report_outcomes", ref reportOutcomes);
            dataStore.SyncData("gwp_enf_envoy_report_amounts", ref reportAmounts);
            dataStore.SyncData("gwp_enf_envoy_report_towns", ref reportTowns);

            if (!dataStore.IsLoading) return;
            _envoyReports.Clear();
            for (int i = 0; reportOutcomes != null && i < reportOutcomes.Count; i++)
                _envoyReports.Add(new EnvoyReport
                {
                    Hours = reportHours[i],
                    OffenderHeroId = reportOffenders[i],
                    WardenHeroId = reportWardens[i],
                    Outcome = reportOutcomes[i],
                    Amount = reportAmounts[i],
                    TownId = reportTowns[i]
                });
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
                    DueHours = dueHours != null && i < dueHours.Count ? dueHours[i] : 0d,
                    PendingDesire = pending != null && i < pending.Count ? pending[i] : -1
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
            // 承办人正在打仗，抽不出人：这一小时先不宣战，仗打完就派（实机诊断 2026-10-15）。
            if (police.MapEvent != null)
                return true;
            return TrySendEnvoy(police, crime, criminal, false);
        }

        /// <summary>他在宽限期里，或收钱的使者正在路上：这宗案子谁都不接（用户裁定 2026-10-15）。</summary>
        private bool IsAwaitingEnvoyCollection(CrimeRecord crime) =>
            _envoyCases.TryGetValue(crime.CrimeId, out EnvoyCase? envoyCase) &&
            (envoyCase.Stage == EnvoyStage.Grace ||
             envoyCase.Stage == EnvoyStage.Travelling && envoyCase.CollectOnly);

        /// <summary>
        /// 此刻为什么派不出使者；派得出返回 null。已经和灰袍开战的势力，使者过去就是送死；出借方在海上
        /// 只剩自己那条船时，临时队出不了门（PoliceResourceManager.IsStrandedAtSea）。他在打仗、在别人军团里
        /// 都照派照谈（用户裁定 2026-10-15）。
        /// </summary>
        private static string? EnvoyBlocker(MobileParty police, MobileParty criminal)
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return "no_police_clan";
            if (criminal.LeaderHero == null) return "target_leaderless";
            if (police.MapEvent != null) return "warden_in_battle";
            if (police.Army != null && police.Army.LeaderParty != police) return "warden_army_member";
            if (GwpPlayerCaseAssignment.IsReservedTarget(criminal)) return "player_target";
            if (FactionManager.IsAtWarAgainstFaction(policeClan, criminal.MapFaction))
                return "already_at_war:" + (criminal.MapFaction?.StringId ?? "-");
            if (PoliceResourceManager.IsStrandedAtSea(police)) return "warden_stranded_at_sea";
            return null;
        }

        private bool TrySendEnvoy(MobileParty police, CrimeRecord crime, MobileParty criminal, bool collectOnly)
        {
            string? blocker = EnvoyBlocker(police, criminal);
#if GWP_DIAGNOSTICS
            TraceEnvoyBlocker(police, crime, blocker);
#endif
            if (blocker != null)
                return false;

            // 用户裁定 2026-10-15：使者优先排骑兵，不够由别的兵补，只从承办领主自己队里抽；船由主队借。
            List<ImmediateInterceptorTroopTransfer> batches =
                DetachmentBatches(police, new[] { police }, false);
            int size = Math.Min(GwpTuning.Enforcement.EnvoySize, HealthyTroopCount(batches));
            if (size <= 0)
                return false;

            var transferred = new List<ImmediateInterceptorTroopTransfer>();
            MobileParty? envoy = CreateDetachment(police, batches, size, 1,
                GwpText.Get(GwpTextCatalog.gwp_envoy_party_name),
                transferred, out int moved, out Settlement? returnSettlement);
            if (envoy == null || returnSettlement == null)
                return false;

            PoliceResourceManager.OutfitTemporaryDutyParty(envoy, police);
            // 使者是去谈的，不主动打任何人：原版的进攻评分乘以队伍的好斗度，置零就不会被路边的劫匪勾走
            // （C#反编译 v1.4.8 DefaultMobilePartyAIModel；实机诊断 2026-10-15：使者全部的仗都是自己冲劫匪打起来的）。
            // 好斗度是原版存档字段，读档后照旧；逃跑判断不动，碰上打不过的照样会躲。
            envoy.Aggressiveness = 0f;
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
                if (envoyCase.Stage == EnvoyStage.Aftermath)
                {
                    TendAftermath(envoyCase);
                    continue;
                }
                CrimeRecord? crime = CrimePool.GetRecordByKey(envoyCase.CrimeId);
                if (crime != null && GwpPlayerCaseAssignment.IsCaseHeldByPlayer(crime.OffenderHeroId))
                {
                    CancelEnvoyForPlayer(envoyCase);
                    continue;
                }
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
                    case EnvoyStage.WaitingForDuel:
                        MobileParty? warden = FindActiveParty(envoyCase.WardenPartyId);
                        if (warden != null && crime.Offender?.IsActive == true)
                            StartDuel(envoyCase, warden, crime.Offender);
                        else
                            FailEnvoy(envoyCase, "duel_participant_unavailable");
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

            // 已出使者固定该罪犯；原承办人改追或撤下任务不撤销交涉。
            MobileParty? target = crime.Offender;
            if (target?.IsActive != true)
            {
                if (CampaignTime.Now.ToHours >= envoyCase.DueHours)
                    FailEnvoy(envoyCase, "offender_unavailable_timeout");
                return;
            }
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (CampaignTime.Now.ToHours >= envoyCase.DueHours || policeClan == null ||
                FactionManager.IsAtWarAgainstFaction(policeClan, target.MapFaction))
            {
                FailEnvoy(envoyCase, "envoy_timeout_or_war");
                return;
            }

            // 他正在打仗（多半是在烧村、抢商队，案子就是这么立的）也照谈（用户裁定 2026-10-15）；
            // 只有使者自己被人拖进仗里时等它打完。
            if (envoy.MapEvent != null)
                return;
            // 被人打过一仗后原版把它停在原地，要等下一轮拍卖才动（实机诊断 2026-10-15：停了约 11 小时）。
            if (envoy.DefaultBehavior == AiBehavior.Hold)
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(envoy);

            if (!EnvoyReached(envoy, target))
            {
                FollowTarget(envoy, target);
                return;
            }

            MobileParty? police = FindActiveParty(envoyCase.WardenPartyId);
            Hero? offender = target.LeaderHero;
            if (police?.LeaderHero == null || offender == null)
            {
                FailEnvoy(envoyCase, "no_one_to_speak");
                return;
            }

            Negotiate(envoyCase, crime, police, police.LeaderHero, envoy, target, offender);
            // 谈出结果就回主队；等他打完仗再押人、看他杀兵的那种留在他身边（Aftermath）。
            if (envoyCase.Stage == EnvoyStage.Aftermath && _envoyCases.ContainsKey(envoyCase.CrimeId))
                FollowTarget(envoy, target);
            else
                MarkDelayPatrolReturning(envoy.StringId);
        }

        /// <summary>
        /// 走到他跟前没有：与追截队回主队同一个距离，即原版军团成员算"走到一起"的距离（海上用海上的）。
        /// 他在城里、村里时，使者进去或走到城下（与火誓者到达判定同一个 <c>HasArrived</c>）就进去谈。
        /// </summary>
        private static bool EnvoyReached(MobileParty envoy, MobileParty target)
        {
            if (target.CurrentSettlement != null)
                return GwpFireswornBehavior.HasArrived(envoy, target.CurrentSettlement);
            float contactDistance = envoy.IsCurrentlyAtSea
                ? Campaign.Current.Models.EncounterModel.MaximumAllowedNavalDistanceForEncounteringMobilePartyInArmy
                : Campaign.Current.Models.EncounterModel.MaximumAllowedLandDistanceForEncounteringMobilePartyInArmy;
            return envoy.GetPosition2D.Distance(target.GetPosition2D) <= contactDistance;
        }

        /// <summary>他在定居点里就去那座定居点，否则跟着他走。</summary>
        private static void FollowTarget(MobileParty envoy, MobileParty target)
        {
            if (target.CurrentSettlement != null)
                GreyWardenPartyDesireBehavior.RequestVisit(envoy, target.CurrentSettlement, 8f);
            else
                GreyWardenPartyDesireBehavior.RequestEscort(envoy, target, 8f);
        }

        /// <summary>
        /// 使者当面谈。第一层与玩家当面谈相同（GwpNegotiationPolicy），只是没有对话，按概率直接出结果。
        /// 第二层不再把他拉回照数全缴：他抽到什么打算就照办，第二层只在交钱的打算上讨价还价（用户裁定 2026-10-15）。
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

            // 用户裁定 2026-10-15：第一层实力至上，看灰袍主队和目标的实力，不看使者自己。两边都按实际一起
            // 行动的那一组算（已挂靠的军团、攻城营），他在别人军团里时，他背后就是那支军团。
            float ours = GwpStrength.CombatGroup(GwpStrength.CombatGroupOf(police));
            float theirs = GwpStrength.CombatGroup(GwpStrength.CombatGroupOf(target));
            int wealth = GwpAssetPayment.Wealth(offender, target.Party);
            int charm = warden.GetSkillValue(DefaultSkills.Charm);
            float renown = warden.Clan?.Renown ?? 0f;
            int layerOne = GwpNegotiationResistance.LayerOneResistance(offender, crime.CrimeCategory, ours, theirs,
                GwpFieldArrestBehavior.WardenStandingTier.Ordinary, renown, wealth, fine);
            double refusal = GwpNegotiationPolicy.RefusalChance(layerOne,
                offender.GetTraitLevel(DefaultTraits.Honor), offender.GetTraitLevel(DefaultTraits.Mercy),
                offender.GetTraitLevel(DefaultTraits.Generosity), wealth, fine);
            GwpOffenderDesire desire = GwpOffenderDesires.Roll(offender, fine, true,
                target.MemberRoster.GetTroopRoster().Any(e => !e.Character.IsHero && e.Number > 0));
            bool listens = refusal < 0.55d;
            bool acknowledged = listens && GwpNegotiationResistance.SimulatePersuasion(
                GwpNegotiationResistance.LayerOneReservations, layerOne, true, charm);

            // 交钱的打算：第二层谈下来，他肯给的比例往全额挪一半；谈不下来按他说的数。别的打算不谈，照办。
            int share = GwpOffenderDesires.OfferedSharePercent(desire);
            bool bargains = acknowledged && share < 100 && desire != GwpOffenderDesire.BribeTheWarden;
            int layerTwo = bargains
                ? GwpNegotiationResistance.LayerTwoResistance(offender, desire, charm, renown, wealth, fine)
                : -1;
            bool bargained = bargains && GwpNegotiationResistance.SimulatePersuasion(
                GwpNegotiationResistance.LayerTwoReservations, layerTwo, false, charm);
            if (bargained)
                share = (share + 100) / 2;
            GwpAiDiagnostics.WriteAction(police, "ENVOY_NEGOTIATED",
                "envoy=" + envoy.StringId + "; offender=" + offender.StringId +
                "; ours=" + ours.ToString("0") + "; theirs=" + theirs.ToString("0") +
                "; fine=" + fine + "; wealth=" + wealth + "; gold=" + offender.Gold +
                "; layerOne=" + layerOne + "; refusal=" + refusal.ToString("0.00") +
                "; acknowledged=" + acknowledged + "; desire=" + desire + "; layerTwo=" + layerTwo +
                "; bargained=" + bargained + "; share=" + share +
                "; inBattle=" + (target.MapEvent != null));

            if (!acknowledged)
            {
                FailEnvoy(envoyCase, listens ? "envoy_layer_one_failed" : "envoy_refused");
                return;
            }

            switch (desire)
            {
                case GwpOffenderDesire.RequestGrace:
                    // 用户裁定 2026-10-15：给了宽限，承办人直接去接下一个差事，到期派使者收钱。
                    envoyCase.Stage = EnvoyStage.Grace;
                    envoyCase.DueHours = CampaignTime.Now.ToHours + GwpTuning.FieldArrest.GraceHours;
                    RecordEnvoyReport(envoyCase, "grace", 0);
                    PoliceTask? graceTask = TaskForCrime(crime);
                    if (graceTask != null)
                        EndCaseTask(graceTask.PolicePartyId, CaseTaskEnd.Released, "envoy_grace");
                    return;
                case GwpOffenderDesire.DemandDuel:
                    StartDuel(envoyCase, police, target);
                    return;
                case GwpOffenderDesire.SurrenderSelf:
                    // 谈妥就结案：使者路上出事是灰袍的风险，不让他担（用户裁定 2026-10-15）。
                    // 交到灰袍手里那一刻原版抓捕事件只认还开着的案子，所以震慑、清账在这里先做。
                    GwpFieldArrestBehavior.RegisterFieldEnforcement(offender, crime);
                    GwpAiDeterrenceState.ClearArrestRecord(offender);
                    RecordEnvoyReport(envoyCase, "surrender", 0);
                    ResolveEnvoyCase(envoyCase, crime, true, "envoy_surrender");
                    if (target.MapEvent == null)
                        TakePrisonerAction.Apply(envoy.Party, offender);
                    else
                        BeginAftermath(envoyCase, desire);
                    return;
                case GwpOffenderDesire.BribeTheWarden:
                    // 用户裁定 2026-10-15：收私钱不算谈崩。钱进承办领主的口袋，案子销掉，案底一点不少。
                    int bribe = Math.Min(offender.Gold,
                        Math.Max(1, fine * GwpTuning.FieldArrest.BribeSharePercent / 100));
                    if (bribe > 0)
                        GiveGoldAction.ApplyBetweenCharacters(offender, warden, bribe, true);
                    GwpFieldArrestBehavior.RegisterFieldEnforcement(offender, crime);
                    RecordEnvoyReport(envoyCase, "bribed", bribe);
                    ResolveEnvoyCase(envoyCase, crime, false, "envoy_bribed");
                    return;
            }

            SettleThroughEnvoy(envoyCase, crime, envoy, offender, fine * share / 100,
                share >= 100 ? "envoy_paid_in_full" : "envoy_paid_share");
            if (desire == GwpOffenderDesire.DeedsOnly)
            {
                if (target.MapEvent == null)
                    GwpOffenderDesires.SacrificeOwnMen(target);
                else
                    BeginAftermath(envoyCase, desire);
            }
        }

        /// <summary>
        /// 案子已经结了，只剩要动他名册的那一步（押他走、看他杀兵）而他正在打仗：使者跟着他，打完再办。
        /// 打仗时改名册会和原版战斗结算对不上。记录留在 <see cref="_envoyCases"/> 里，进存档。
        /// </summary>
        private void BeginAftermath(EnvoyCase envoyCase, GwpOffenderDesire desire)
        {
            envoyCase.Stage = EnvoyStage.Aftermath;
            envoyCase.PendingDesire = (int)desire;
            envoyCase.DueHours = CampaignTime.Now.ToHours + GwpTuning.Enforcement.EnvoyTimeoutHours;
            _envoyCases[envoyCase.CrimeId] = envoyCase;
            // 结案时承办人的临时队都被转成返程（ClearTaskWarTracking），这一支还要留下。
            if (_delayPatrolStates.TryGetValue(envoyCase.EnvoyPartyId, out DelayPatrolState? state))
                state.Returning = false;
        }

        private void TendAftermath(EnvoyCase envoyCase)
        {
            MobileParty? envoy = FindActiveParty(envoyCase.EnvoyPartyId);
            MobileParty? target = FindActiveParty(envoyCase.TargetPartyId);
            Hero? offender = target?.LeaderHero;
            if (envoy == null || target == null || offender == null ||
                CampaignTime.Now.ToHours >= envoyCase.DueHours)
            {
                if (envoy != null) MarkDelayPatrolReturning(envoy.StringId);
                ForgetEnvoyCase(envoyCase, "aftermath_lapsed");
                return;
            }
            if (envoy.MapEvent != null || target.MapEvent != null || !EnvoyReached(envoy, target))
            {
                FollowTarget(envoy, target);
                return;
            }

            if ((GwpOffenderDesire)envoyCase.PendingDesire == GwpOffenderDesire.SurrenderSelf)
                TakePrisonerAction.Apply(envoy.Party, offender);
            else
                GwpOffenderDesires.SacrificeOwnMen(target);
            MarkDelayPatrolReturning(envoy.StringId);
            ForgetEnvoyCase(envoyCase, "aftermath_done");
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
            RecordEnvoyReport(envoyCase, reason == "envoy_paid_share" ? "paid_share" : "paid", collected);
            ResolveEnvoyCase(envoyCase, crime, true, reason + "_" + collected);
        }

        /// <summary>
        /// 宽限到期：离他最近的灰袍领主派一趟只收钱的使者。最近的派不了（正在打仗等）就换下一个，
        /// 都派不了下一小时再试。
        /// </summary>
        private void SendGraceCollector(EnvoyCase envoyCase, CrimeRecord crime)
        {
            MobileParty? criminal = crime.Offender;
            if (criminal?.IsActive != true)
                return;
            // 宽限期里他的势力和灰袍打起来了：使者去不了，案子回台账照常追捕。
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null || FactionManager.IsAtWarAgainstFaction(policeClan, criminal.MapFaction))
            {
                FailEnvoy(envoyCase, "grace_collector_at_war");
                return;
            }
            // 正在打仗、在军团里跟着走的领主这一小时派不了，就换下一个近的；都派不了下一小时再试。
            foreach (MobileParty police in PoliceStats.GetAllPoliceParties()
                         .Where(party => party?.IsActive == true && party.IsLordParty &&
                             CanContinueLeadingPoliceTask(party))
                         .OrderBy(party => party.GetPosition2D.Distance(criminal.GetPosition2D)))
            {
                if (TrySendEnvoy(police, crime, criminal, true))
                    return;
            }
        }

        #region 比武

        /// <summary>
        /// 他要单挑：约到原版比武大会（用户裁定 2026-10-15）。最近三座能进的城里有比武就去那一座，
        /// 都没有就在最近的城新开一场。两人留城等赛。
        /// </summary>
        private void StartDuel(EnvoyCase envoyCase, MobileParty police, MobileParty target)
        {
            if (police.MapEvent is { IsFinalized: false } ||
                GwpWardenDuties.HoldsAny(police, WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder,
                    WardenDuty.PlayerEscort, WardenDuty.PlayerCase, WardenDuty.Duel))
            {
                envoyCase.Stage = EnvoyStage.WaitingForDuel;
                return;
            }
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
            // 他正在协助别人：约了比武就离开那个协力组去赴约（用户裁定 2026-10-16：协办人自己的使者照常办完）。
            LeaveAssistanceAsMember(police, "envoy_duel");
            envoyCase.Stage = EnvoyStage.Duel;
            envoyCase.TownId = venue.Settlement.StringId;
            RecordEnvoyReport(envoyCase, "duel_called", 0);
            envoyCase.DueHours = CampaignTime.Now.ToHours + GwpTuning.Enforcement.TournamentDuelTimeoutDays * 24d;
            HoldForDuel(police, venue.Settlement, true);
            HoldForDuel(target, venue.Settlement, false);
            GwpAiDiagnostics.WriteAction(police, "ENVOY_DUEL_CALLED",
                "offender=" + target.StringId + "; town=" + venue.Settlement.StringId +
                "; existingTournament=" + existingTournament);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_envoy_duel_called,
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
                RecordEnvoyReport(envoyCase, "duel_won", collected);
                ResolveEnvoyCase(envoyCase, crime, true, "envoy_duel_won_" + collected);
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get(GwpTextCatalog.gwp_envoy_duel_warden_won,
                        "VAR_1", wardenName, "VAR_2", offenderName, "VAR_3", townName),
                    Colors.Green));
                return;
            }

            // 他赢了按约免罚：案子销掉，案底照留。
            GwpFieldArrestBehavior.RegisterFieldEnforcement(offender, crime);
            RecordEnvoyReport(envoyCase, "duel_lost", 0);
            ResolveEnvoyCase(envoyCase, crime, false, "envoy_duel_lost");
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_envoy_duel_offender_won,
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
            // 他在别人军团里当成员：先离队去赴约，和原版成婚时离开军团同一个做法（MarriageAction）。
            if (party.Army != null && party.Army.LeaderParty != party)
                party.Army = null;
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
            RecordEnvoyReport(envoyCase, FailureOutcome(reason), 0);
            MobileParty? police = FindActiveParty(envoyCase.WardenPartyId);
            if (police != null)
                GwpAiDiagnostics.WriteAction(police, "ENVOY_FAILED",
                    "crime=" + envoyCase.CrimeId + "; reason=" + reason);
        }

#if GWP_DIAGNOSTICS
        // 案子 → 上一次记下的派不出原因。只在原因变化时写一行，免得每小时刷屏；不进存档。
        private readonly Dictionary<string, string> _envoyBlockerTrace =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private void TraceEnvoyBlocker(MobileParty police, CrimeRecord crime, string? blocker)
        {
            string key = crime.CrimeId;
            string current = blocker ?? string.Empty;
            if (_envoyBlockerTrace.TryGetValue(key, out string? last) && last == current) return;
            _envoyBlockerTrace[key] = current;
            if (blocker != null)
                GwpAiDiagnostics.WriteAction(police, "ENVOY_NOT_SENT",
                    "crime=" + crime.CrimeId + "; reason=" + blocker);
        }
#endif

        private void ForgetEnvoyCase(EnvoyCase envoyCase, string reason)
        {
            _envoyCases.Remove(envoyCase.CrimeId);
            MobileParty? police = FindActiveParty(envoyCase.WardenPartyId);
            if (police != null)
                GwpAiDiagnostics.WriteAction(police, "ENVOY_CASE_DROPPED",
                    "crime=" + envoyCase.CrimeId + "; stage=" + envoyCase.Stage + "; reason=" + reason);
        }

        private void ReleaseEnvoysToPlayer(string heroId)
        {
            foreach (EnvoyCase entry in _envoyCases.Values.Where(e => e.Stage != EnvoyStage.Aftermath &&
                         string.Equals(CrimePool.GetRecordByKey(e.CrimeId)?.OffenderHeroId,
                             heroId, StringComparison.OrdinalIgnoreCase)).ToList())
                CancelEnvoyForPlayer(entry);
        }

        private void CancelEnvoyForPlayer(EnvoyCase entry)
        {
            // A failed negotiation has already sent its envoy home. Keep the
            // recorded failure if the player's warrant later returns to the pool.
            if (entry.Stage == EnvoyStage.Failed) return;
            if (entry.Stage == EnvoyStage.Duel || entry.Stage == EnvoyStage.DuelDecided)
                ReleaseDuelists(entry);
            if (_delayPatrolStates.ContainsKey(entry.EnvoyPartyId))
                MarkDelayPatrolReturning(entry.EnvoyPartyId);
            ForgetEnvoyCase(entry, "assigned_to_player");
        }

        internal static bool HasEnvoyAgreement(CrimeRecord crime) => _instance != null &&
            (_instance.IsAwaitingEnvoyCollection(crime) ||
             _instance._envoyCases.TryGetValue(crime.CrimeId, out EnvoyCase? entry) &&
             (entry.Stage == EnvoyStage.Duel || entry.Stage == EnvoyStage.DuelDecided ||
              entry.Stage == EnvoyStage.WaitingForDuel));
    }
}
