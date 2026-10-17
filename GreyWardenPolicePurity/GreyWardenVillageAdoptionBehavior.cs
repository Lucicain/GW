using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 村庄被焚毁后，灰袍守卫会派出一支警察队伍进行善后。
    /// 若善后完成时满足家族人数与收养冷却条件，则新增一名被收留的女童。
    /// </summary>
    public sealed class GreyWardenVillageAdoptionBehavior : CampaignBehaviorBase
    {
        private const string PendingVillageIdsKey = "GWPP_AdoptPendingVillageIds";
        private const string PendingVillageNamesKey = "GWPP_AdoptPendingVillageNames";
        private const string PendingQueuedHoursKey = "GWPP_AdoptPendingQueuedHours";
        private const string ActivePartyIdsKey = "GWPP_AdoptActivePartyIds";
        private const string ActiveVillageIdsKey = "GWPP_AdoptActiveVillageIds";
        private const string ActiveVillageNamesKey = "GWPP_AdoptActiveVillageNames";
        private const string ActiveQueuedHoursKey = "GWPP_AdoptActiveQueuedHours";
        private const string ActiveStartedFlagsKey = "GWPP_AdoptActiveStartedFlags";
        private const string ActiveRemainingHoursKey = "GWPP_AdoptActiveRemainingHours";
        private const string ActiveLastWorkHoursKey = "GWPP_AdoptActiveLastWorkHours";
        private const string OriginHeroIdsKey = "GWPP_AdoptOriginHeroIds";
        private const string OriginVillageNamesKey = "GWPP_AdoptOriginVillageNames";
        private const string LastAdoptionHoursKey = "GWPP_LastAdoptionHours";
        private const double NoRecordedAdoptionHours = -1000000d;

        private static GreyWardenVillageAdoptionBehavior? _instance;
        private static readonly HashSet<string> _activeReliefPartyIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly List<PendingVillageRelief> _pendingReliefs = new List<PendingVillageRelief>();
        private readonly List<ActiveVillageRelief> _activeReliefs = new List<ActiveVillageRelief>();
        private readonly Dictionary<string, string> _adoptionOrigins =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private double _lastAdoptionTimeHours = NoRecordedAdoptionHours;

        internal sealed class VillageReliefTaskSnapshot
        {
            public string PolicePartyId { get; set; } = string.Empty;
            public string VillageSettlementId { get; set; } = string.Empty;
            public string VillageName { get; set; } = string.Empty;
            public CampaignTime QueuedTime { get; set; }
            public ReliefStage Stage { get; set; }
            public double RemainingHours { get; set; }
            public bool IsAssigned => !string.IsNullOrWhiteSpace(PolicePartyId);
        }

        public GreyWardenVillageAdoptionBehavior()
        {
            _instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.VillageLooted.AddNonSerializedListener(this, OnVillageLooted);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? pendingVillageIds = null;
            List<string>? pendingVillageNames = null;
            List<double>? pendingQueuedHours = null;
            List<string>? activePartyIds = null;
            List<string>? activeVillageIds = null;
            List<string>? activeVillageNames = null;
            List<double>? activeQueuedHours = null;
            List<int>? activeStartedFlags = null;
            List<double>? activeRemainingHours = null;
            List<double>? activeLastWorkHours = null;
            List<string>? originHeroIds = null;
            List<string>? originVillageNames = null;

            if (dataStore.IsSaving)
            {
                pendingVillageIds = _pendingReliefs.Select(static x => x.VillageSettlementId).ToList();
                pendingVillageNames = _pendingReliefs.Select(static x => x.VillageName).ToList();
                pendingQueuedHours = _pendingReliefs.Select(static x => x.QueuedTimeHours).ToList();

                activePartyIds = _activeReliefs.Select(static x => x.PolicePartyId).ToList();
                activeVillageIds = _activeReliefs.Select(static x => x.VillageSettlementId).ToList();
                activeVillageNames = _activeReliefs.Select(static x => x.VillageName).ToList();
                activeQueuedHours = _activeReliefs.Select(static x => x.QueuedTimeHours).ToList();
                activeStartedFlags = _activeReliefs.Select(static x => x.ReliefStarted ? 1 : 0).ToList();
                activeRemainingHours = _activeReliefs.Select(static x => x.RemainingHours).ToList();
                activeLastWorkHours = _activeReliefs.Select(static x => x.LastWorkHours).ToList();

                originHeroIds = _adoptionOrigins.Keys.ToList();
                originVillageNames = _adoptionOrigins.Values.ToList();
            }

            dataStore.SyncData(PendingVillageIdsKey, ref pendingVillageIds);
            dataStore.SyncData(PendingVillageNamesKey, ref pendingVillageNames);
            dataStore.SyncData(PendingQueuedHoursKey, ref pendingQueuedHours);
            dataStore.SyncData(ActivePartyIdsKey, ref activePartyIds);
            dataStore.SyncData(ActiveVillageIdsKey, ref activeVillageIds);
            dataStore.SyncData(ActiveVillageNamesKey, ref activeVillageNames);
            dataStore.SyncData(ActiveQueuedHoursKey, ref activeQueuedHours);
            dataStore.SyncData(ActiveStartedFlagsKey, ref activeStartedFlags);
            dataStore.SyncData(ActiveRemainingHoursKey, ref activeRemainingHours);
            dataStore.SyncData(ActiveLastWorkHoursKey, ref activeLastWorkHours);
            dataStore.SyncData(OriginHeroIdsKey, ref originHeroIds);
            dataStore.SyncData(OriginVillageNamesKey, ref originVillageNames);
            dataStore.SyncData(LastAdoptionHoursKey, ref _lastAdoptionTimeHours);

            if (!dataStore.IsLoading)
            {
                return;
            }

            _pendingReliefs.Clear();
            _activeReliefs.Clear();
            _adoptionOrigins.Clear();

            int pendingCount = MinCount(pendingVillageIds, pendingVillageNames, pendingQueuedHours);
            for (int i = 0; i < pendingCount; i++)
            {
                if (string.IsNullOrWhiteSpace(pendingVillageIds![i]))
                {
                    continue;
                }

                _pendingReliefs.Add(new PendingVillageRelief
                {
                    VillageSettlementId = pendingVillageIds[i],
                    VillageName = pendingVillageNames![i] ?? string.Empty,
                    QueuedTimeHours = pendingQueuedHours![i]
                });
            }

            int activeCount = MinCount(
                activePartyIds,
                activeVillageIds,
                activeVillageNames,
                activeQueuedHours,
                activeStartedFlags,
                activeRemainingHours,
                activeLastWorkHours);
            for (int i = 0; i < activeCount; i++)
            {
                if (string.IsNullOrWhiteSpace(activePartyIds![i]) ||
                    string.IsNullOrWhiteSpace(activeVillageIds![i]))
                {
                    continue;
                }

                _activeReliefs.Add(new ActiveVillageRelief
                {
                    PolicePartyId = activePartyIds[i],
                    VillageSettlementId = activeVillageIds[i],
                    VillageName = activeVillageNames![i] ?? string.Empty,
                    QueuedTimeHours = activeQueuedHours![i],
                    ReliefStarted = activeStartedFlags![i] != 0,
                    RemainingHours = activeRemainingHours![i],
                    LastWorkHours = activeLastWorkHours![i]
                });
            }

            int originCount = Math.Min(originHeroIds?.Count ?? 0, originVillageNames?.Count ?? 0);
            for (int i = 0; i < originCount; i++)
            {
                string heroId = originHeroIds![i];
                string villageName = originVillageNames![i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(heroId) || string.IsNullOrWhiteSpace(villageName))
                {
                    continue;
                }

                _adoptionOrigins[heroId] = villageName;
            }

            RebuildActiveReliefPartyIndex();
        }

        internal static bool IsVillageReliefParty(MobileParty? party)
        {
            return party != null && _activeReliefPartyIds.Contains(party.StringId);
        }

        internal static bool TryGetReliefVisit(MobileParty party, out Settlement? target, out float score)
        {
            ActiveVillageRelief? relief = _instance?._activeReliefs.FirstOrDefault(
                entry => string.Equals(entry.PolicePartyId, party.StringId, StringComparison.OrdinalIgnoreCase));
            target = relief == null ? null : FindSettlement(relief.VillageSettlementId);
            score = target != null && HasArrivedAtVillage(party, target) ? 10f : 8f;
            return target?.IsVillage == true;
        }

        internal static bool TryGetAdoptionOrigin(string? heroId, out string villageName)
        {
            villageName = string.Empty;
            if (_instance == null || string.IsNullOrWhiteSpace(heroId))
            {
                return false;
            }

            return _instance._adoptionOrigins.TryGetValue(heroId!, out villageName);
        }

        internal static bool TryGetAdoptionStatus(out AdoptionStatusInfo info)
        {
            if (_instance == null)
            {
                info = default;
                return false;
            }

            info = _instance.BuildAdoptionStatusInfo();
            return true;
        }

        internal static IReadOnlyList<VillageReliefTaskSnapshot> GetTaskSnapshots()
        {
            var result = new List<VillageReliefTaskSnapshot>();
            if (_instance == null)
                return result;

            foreach (ActiveVillageRelief relief in _instance._activeReliefs)
            {
                MobileParty? party = FindPoliceParty(relief.PolicePartyId);
                ReliefStage stage = party != null && !GwpWardenDuties.CanContinueRelief(party)
                    ? ReliefStage.Paused
                    : relief.ReliefStarted
                        ? ReliefStage.StayingInVillage
                        : ReliefStage.TravelingToVillage;
                result.Add(new VillageReliefTaskSnapshot
                {
                    PolicePartyId = relief.PolicePartyId,
                    VillageSettlementId = relief.VillageSettlementId,
                    VillageName = _instance.ResolveVillageName(
                        relief.VillageSettlementId, relief.VillageName),
                    QueuedTime = CampaignTime.Hours((float)relief.QueuedTimeHours),
                    Stage = stage,
                    RemainingHours = relief.ReliefStarted
                        ? relief.RemainingHours
                        : 0d
                });
            }

            foreach (PendingVillageRelief relief in _instance._pendingReliefs)
                result.Add(new VillageReliefTaskSnapshot
                {
                    VillageSettlementId = relief.VillageSettlementId,
                    VillageName = _instance.ResolveVillageName(
                        relief.VillageSettlementId, relief.VillageName),
                    QueuedTime = CampaignTime.Hours((float)relief.QueuedTimeHours),
                    Stage = ReliefStage.WaitingForAssignment
                });

            return result;
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            _pendingReliefs.Clear();
            _activeReliefs.Clear();
            _adoptionOrigins.Clear();
            _lastAdoptionTimeHours = NoRecordedAdoptionHours;
            RebuildActiveReliefPartyIndex();
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            RebuildActiveReliefPartyIndex();
            GreyWardenFamilyBehavior.RefreshPoliceClanFamilyPresentation();
        }

        private AdoptionStatusInfo BuildAdoptionStatusInfo()
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            int livingMembers = policeClan?.Heroes.Count(static h => h != null && h.IsAlive) ?? 0;

            bool hasRecordedAdoption = _lastAdoptionTimeHours > NoRecordedAdoptionHours + 1d;
            double remainingCooldownHours = hasRecordedAdoption
                ? Math.Max(0d, GetAdoptionCooldownHours() - (CampaignTime.Now.ToHours - _lastAdoptionTimeHours))
                : 0d;

            ReliefStage currentReliefStage = ReliefStage.None;
            string currentReliefVillageName = string.Empty;
            double currentReliefRemainingHours = 0d;

            if (_activeReliefs.Count > 0)
            {
                ActiveVillageRelief relief = _activeReliefs[0];
                currentReliefVillageName = ResolveVillageName(relief.VillageSettlementId, relief.VillageName);

                MobileParty? party = FindPoliceParty(relief.PolicePartyId);
                if (party != null && !GwpWardenDuties.CanContinueRelief(party))
                {
                    currentReliefStage = ReliefStage.Paused;
                }
                else if (relief.ReliefStarted)
                {
                    currentReliefStage = ReliefStage.StayingInVillage;
                }
                else
                {
                    currentReliefStage = ReliefStage.TravelingToVillage;
                }
                currentReliefRemainingHours = relief.ReliefStarted ? relief.RemainingHours : 0d;
            }
            else if (_pendingReliefs.Count > 0)
            {
                PendingVillageRelief relief = _pendingReliefs[0];
                currentReliefStage = ReliefStage.WaitingForAssignment;
                currentReliefVillageName = ResolveVillageName(relief.VillageSettlementId, relief.VillageName);
            }

            return new AdoptionStatusInfo(
                livingMembers,
                GwpTuning.Family.MaxClanMembers,
                hasRecordedAdoption,
                _lastAdoptionTimeHours,
                remainingCooldownHours,
                remainingCooldownHours <= 0d,
                currentReliefStage,
                currentReliefVillageName,
                currentReliefRemainingHours);
        }

        private void OnVillageLooted(Village village)
        {
            Settlement? villageSettlement = village?.Settlement;
            if (!IsLootedVillageSettlement(villageSettlement) || villageSettlement == null)
            {
                return;
            }

            QueuePendingRelief(villageSettlement);
        }

        private void QueuePendingRelief(Settlement villageSettlement)
        {
            if (!IsAdoptionSystemEligible() || HasAnyQueuedOrActiveRelief())
            {
                return;
            }

            _pendingReliefs.Clear();
            _pendingReliefs.Add(new PendingVillageRelief
            {
                VillageSettlementId = villageSettlement.StringId,
                VillageName = villageSettlement.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_greywardenvillageadoptionbehavior_001),
                QueuedTimeHours = CampaignTime.Now.ToHours
            });

        }

        private void OnHourlyTick()
        {
            UpdateActiveReliefs();
            AssignPendingReliefs();
        }

        private void AssignPendingReliefs()
        {
            if (_pendingReliefs.Count == 0 || _activeReliefs.Count > 0)
            {
                return;
            }

            if (!IsAdoptionSystemEligible())
            {
                _pendingReliefs.Clear();
                return;
            }

            PendingVillageRelief pending = _pendingReliefs[0];
            Settlement? village = FindSettlement(pending.VillageSettlementId);
            if (village == null || !village.IsVillage)
            {
                _pendingReliefs.Clear();
                return;
            }

            MobileParty? reservedPolice = null;
            foreach (MobileParty police in GetReliefCandidatesByDistance(village))
            {
                if (PoliceEnforcementBehavior.TryReservePolicePartyForVillageRelief(police))
                {
                    reservedPolice = police;
                    break;
                }
            }

            if (reservedPolice == null)
            {
                return;
            }

            _activeReliefs.Add(new ActiveVillageRelief
            {
                PolicePartyId = reservedPolice.StringId,
                VillageSettlementId = pending.VillageSettlementId,
                VillageName = pending.VillageName,
                QueuedTimeHours = pending.QueuedTimeHours,
                ReliefStarted = false,
                RemainingHours = GwpTuning.Family.VillageReliefStayHours,
                LastWorkHours = -1d
            });

            _pendingReliefs.Clear();
            RebuildActiveReliefPartyIndex();
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(reservedPolice);

        }

        private void UpdateActiveReliefs()
        {
            if (_activeReliefs.Count == 0)
            {
                RebuildActiveReliefPartyIndex();
                return;
            }

            foreach (ActiveVillageRelief relief in _activeReliefs.ToList())
            {
                MobileParty? police = FindPoliceParty(relief.PolicePartyId);
                Settlement? village = FindSettlement(relief.VillageSettlementId);

                if (police?.IsActive != true || police.LeaderHero?.IsActive != true ||
                    police.LeaderHero.IsPrisoner || village == null || !village.IsVillage)
                {
                    FinishRelief(relief, police, shouldAdopt: false);
                    continue;
                }

                if (ShouldAbortReliefForOperationalReason(police))
                {
                    FinishRelief(relief, police, shouldAdopt: false);
                    continue;
                }

                if (!GwpWardenDuties.CanContinueRelief(police) ||
                    police.MapEvent is { IsFinalized: false } || !HasArrivedAtVillage(police, village))
                {
#if GWP_DIAGNOSTICS
                    if (relief.LastWorkHours >= 0d)
                        GwpAiDiagnostics.WriteAction(police, "ADOPTION_STAY_PAUSED",
                            "village=" + village.StringId + "; remainingHours=" + relief.RemainingHours);
#endif
                    relief.LastWorkHours = -1d;
                    continue;
                }

                // 只累计连续两个小时检查都在执行驻留的时间。暂停和在途不计时；进度随任务保存。
                double now = CampaignTime.Now.ToHours;
#if GWP_DIAGNOSTICS
                if (relief.LastWorkHours < 0d)
                    GwpAiDiagnostics.WriteAction(police, "ADOPTION_STAY_STARTED_OR_RESUMED",
                        "village=" + village.StringId + "; remainingHours=" + relief.RemainingHours);
#endif
                relief.ReliefStarted = true;
                if (relief.LastWorkHours >= 0d)
                    relief.RemainingHours = Math.Max(0d, relief.RemainingHours - (now - relief.LastWorkHours));
                relief.LastWorkHours = now;
                if (relief.RemainingHours <= 0d)
                {
                    FinishRelief(relief, police, shouldAdopt: true);
                }
            }

            RebuildActiveReliefPartyIndex();
        }

        private void FinishRelief(ActiveVillageRelief relief, MobileParty? police, bool shouldAdopt)
        {
#if GWP_DIAGNOSTICS
            if (police != null)
                GwpAiDiagnostics.WriteAction(police, "ADOPTION_STAY_FINISHED",
                    "village=" + relief.VillageSettlementId + "; completed=" + shouldAdopt +
                    "; remainingHours=" + relief.RemainingHours);
#endif
            Settlement? village = FindSettlement(relief.VillageSettlementId);
            Hero? adoptedHero = null;
            if (shouldAdopt && village != null && IsAdoptionSystemEligible())
            {
                adoptedHero = TryCreateAdoptedGirl(village, relief.VillageName);
            }

            if (police != null && police.IsActive)
            {
                GreyWardenPartyDesireBehavior.ClearIntent(police);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);
            }

            _activeReliefs.Remove(relief);

            // Only the normal end of the full village stay is a completed ledger case.
            // Abort, invalid-target, and load-normalization cleanup paths pass false.
            if (shouldAdopt && police?.IsActive == true && village?.IsVillage == true)
            {
                PoliceResourceManager.CreditSuccessfulCaseCompletion();
                GwpPlayerRequestDeferral.NotifyDutyCompleted(police,
                    "village_relief");
            }

            if (adoptedHero != null)
            {
                PublishAdoptionLogEntry(adoptedHero, ResolveVillageName(relief.VillageSettlementId, relief.VillageName));
                GreyWardenFamilyBehavior.RefreshPoliceClanFamilyPresentation();
            }
        }

        private Hero? TryCreateAdoptedGirl(Settlement village, string fallbackVillageName)
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            CharacterObject? template = CharacterObject.Find(GwpIds.CommanderTemplateCharacterId);
            if (policeClan == null || template == null)
            {
                return null;
            }

            int age = MBRandom.RandomInt(
                GwpTuning.Family.AdoptedGirlMinAge,
                GwpTuning.Family.AdoptedGirlMaxAge + 1);

            Hero hero = HeroCreator.CreateSpecialHero(template, village, policeClan, null, age);
            hero.IsFemale = true;
            hero.BornSettlement = village;
            hero.UpdateHomeSettlement();
            hero.HeroDeveloper?.InitializeHeroDeveloper();
            EquipInitialChildGear(hero);

            string? villageName = village.Name?.ToString();
            if (string.IsNullOrWhiteSpace(villageName))
            {
                villageName = fallbackVillageName;
            }

            // net472 的 string.IsNullOrWhiteSpace 没有 NotNullWhen 标注，可空流分析
            // 传不过这道判断，因此这里显式取一个非空局部变量。
            string? heroId = hero.StringId;
            string? origin = villageName;
            if (!string.IsNullOrWhiteSpace(heroId) && !string.IsNullOrWhiteSpace(origin))
            {
                _adoptionOrigins[heroId!] = origin!;
            }

            _lastAdoptionTimeHours = CampaignTime.Now.ToHours;
            return hero;
        }

        private static void PublishAdoptionLogEntry(Hero adoptedHero, string villageName)
        {
            if (adoptedHero == null)
            {
                return;
            }
            LogEntry.AddLogEntry(new GreyWardenAdoptionLogEntry(adoptedHero, villageName));
        }

        private static void EquipInitialChildGear(Hero hero)
        {
            Equipment? equipment = Campaign.Current.Models
                .EquipmentSelectionModel
                .GetEquipmentForInitialChildrenGeneration(hero);

            if (equipment == null)
            {
                return;
            }

            EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, equipment);
            hero.CheckInvalidEquipmentsAndReplaceIfNeeded();
        }

        private static bool IsLootedVillageSettlement(Settlement? settlement)
        {
            if (settlement?.IsVillage != true)
            {
                return false;
            }

            Village? village = settlement.SettlementComponent as Village;
            return village?.VillageState == Village.VillageStates.Looted;
        }

        private bool IsAdoptionSystemEligible()
        {
            return IsBelowClanMemberCap() && IsAdoptionCooldownReady();
        }

        private bool IsBelowClanMemberCap()
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null)
            {
                return false;
            }

            int livingMembers = policeClan.Heroes.Count(static h => h != null && h.IsAlive);
            return livingMembers < GwpTuning.Family.MaxClanMembers;
        }

        private bool IsAdoptionCooldownReady()
        {
            if (_lastAdoptionTimeHours <= NoRecordedAdoptionHours + 1d)
            {
                return true;
            }

            double elapsedHours = CampaignTime.Now.ToHours - _lastAdoptionTimeHours;
            return elapsedHours >= GetAdoptionCooldownHours();
        }

        private static double GetAdoptionCooldownHours()
        {
            return CampaignTime.Years(GwpTuning.Family.AdoptionCooldownYears).ToHours;
        }

        private bool HasAnyQueuedOrActiveRelief()
        {
            return _pendingReliefs.Count > 0 || _activeReliefs.Count > 0;
        }

        private static Settlement? FindSettlement(string settlementId)
        {
            if (string.IsNullOrWhiteSpace(settlementId))
            {
                return null;
            }

            return Settlement.Find(settlementId);
        }

        private static MobileParty? FindPoliceParty(string partyId)
        {
            if (string.IsNullOrWhiteSpace(partyId))
            {
                return null;
            }

            return MobileParty.All.FirstOrDefault(p =>
                string.Equals(p.StringId, partyId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasArrivedAtVillage(MobileParty police, Settlement village)
        {
            if (police.CurrentSettlement == village)
            {
                return true;
            }

            return police.GetPosition2D.Distance(village.GetPosition2D) <= GwpTuning.Family.VillageReliefArrivalDistance;
        }

        private static bool ShouldAbortReliefForOperationalReason(MobileParty police)
        {
            return GwpCommon.IsPatrolParty(police) || GwpCommon.IsEnforcementDelayPatrolParty(police);
        }

        private static IEnumerable<MobileParty> GetReliefCandidatesByDistance(Settlement village)
        {
            return PoliceStats.GetAllPoliceParties()
                .Where(IsBasicVillageReliefCandidate)
                .OrderBy(police => CrimePool.HasTask(police.StringId) ? 1 : 0)
                .ThenBy(police => police.GetPosition2D.Distance(village.GetPosition2D));
        }

        private static bool IsBasicVillageReliefCandidate(MobileParty? police)
        {
            if (police == null || !police.IsActive)
            {
                return false;
            }

            if (police.LeaderHero == null || !police.LeaderHero.IsActive)
            {
                return false;
            }

            if (GwpCommon.IsPatrolParty(police)
                || GwpCommon.IsEnforcementDelayPatrolParty(police)
                || !GwpWardenDuties.IsFreeFor(police, WardenDuty.VillageRelief))
            {
                return false;
            }

            return police.MapEvent == null || police.MapEvent.IsFinalized;
        }

        private string ResolveVillageName(string villageSettlementId, string fallbackVillageName)
        {
            Settlement? village = FindSettlement(villageSettlementId);
            return village?.Name?.ToString() ?? fallbackVillageName;
        }

        private void RebuildActiveReliefPartyIndex()
        {
            _activeReliefPartyIds.Clear();
            foreach (ActiveVillageRelief relief in _activeReliefs)
            {
                if (!string.IsNullOrWhiteSpace(relief.PolicePartyId))
                {
                    _activeReliefPartyIds.Add(relief.PolicePartyId);
                }
            }
        }

        private static int MinCount(params System.Collections.ICollection?[] lists)
        {
            int result = int.MaxValue;
            foreach (System.Collections.ICollection? list in lists)
            {
                result = Math.Min(result, list?.Count ?? 0);
            }

            return result == int.MaxValue ? 0 : result;
        }

        private sealed class PendingVillageRelief
        {
            public string VillageSettlementId { get; set; } = string.Empty;
            public string VillageName { get; set; } = string.Empty;
            public double QueuedTimeHours { get; set; }
        }

        private sealed class ActiveVillageRelief
        {
            public string PolicePartyId { get; set; } = string.Empty;
            public string VillageSettlementId { get; set; } = string.Empty;
            public string VillageName { get; set; } = string.Empty;
            public double QueuedTimeHours { get; set; }
            public bool ReliefStarted { get; set; }
            public double RemainingHours { get; set; }
            public double LastWorkHours { get; set; }
        }

        internal enum ReliefStage
        {
            None,
            WaitingForAssignment,
            Paused,
            TravelingToVillage,
            StayingInVillage
        }

        internal readonly struct AdoptionStatusInfo
        {
            public AdoptionStatusInfo(
                int livingMembers,
                int maxMembers,
                bool hasRecordedAdoption,
                double lastAdoptionTimeHours,
                double remainingCooldownHours,
                bool isCooldownReady,
                ReliefStage currentReliefStage,
                string currentReliefVillageName,
                double currentReliefRemainingHours)
            {
                LivingMembers = livingMembers;
                MaxMembers = maxMembers;
                HasRecordedAdoption = hasRecordedAdoption;
                LastAdoptionTimeHours = lastAdoptionTimeHours;
                RemainingCooldownHours = remainingCooldownHours;
                IsCooldownReady = isCooldownReady;
                CurrentReliefStage = currentReliefStage;
                CurrentReliefVillageName = currentReliefVillageName ?? string.Empty;
                CurrentReliefRemainingHours = currentReliefRemainingHours;
            }

            public int LivingMembers { get; }
            public int MaxMembers { get; }
            public bool HasRecordedAdoption { get; }
            public double LastAdoptionTimeHours { get; }
            public double RemainingCooldownHours { get; }
            public bool IsCooldownReady { get; }
            public ReliefStage CurrentReliefStage { get; }
            public string CurrentReliefVillageName { get; }
            public double CurrentReliefRemainingHours { get; }
        }
    }
}
