using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.MapEvents;
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
        private const string ActiveStartedFlagsKey = "GWPP_AdoptActiveStartedFlags";
        private const string ActiveEndHoursKey = "GWPP_AdoptActiveEndHours";
        private const string ActiveAwaitingResupplyFlagsKey = "GWPP_AdoptActiveAwaitingResupplyFlags";
        private const string OriginHeroIdsKey = "GWPP_AdoptOriginHeroIds";
        private const string OriginVillageNamesKey = "GWPP_AdoptOriginVillageNames";
        private const string LastAdoptionHoursKey = "GWPP_LastAdoptionHours";
        private const double NoRecordedAdoptionHours = -1000000d;

        private static GreyWardenVillageAdoptionBehavior? _instance;
        private static readonly HashSet<string> _activeReliefPartyIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly FieldInfo? CampaignMapNoticesField =
            typeof(CampaignInformationManager).GetField("_mapNotices", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<PendingVillageRelief> _pendingReliefs = new List<PendingVillageRelief>();
        private readonly List<ActiveVillageRelief> _activeReliefs = new List<ActiveVillageRelief>();
        private readonly Dictionary<string, string> _adoptionOrigins =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private double _lastAdoptionTimeHours = NoRecordedAdoptionHours;

        public GreyWardenVillageAdoptionBehavior()
        {
            _instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? pendingVillageIds = null;
            List<string>? pendingVillageNames = null;
            List<double>? pendingQueuedHours = null;
            List<string>? activePartyIds = null;
            List<string>? activeVillageIds = null;
            List<string>? activeVillageNames = null;
            List<int>? activeStartedFlags = null;
            List<double>? activeEndHours = null;
            List<int>? activeAwaitingResupplyFlags = null;
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
                activeStartedFlags = _activeReliefs.Select(static x => x.ReliefStarted ? 1 : 0).ToList();
                activeEndHours = _activeReliefs.Select(static x => x.ReliefEndHours).ToList();
                activeAwaitingResupplyFlags = _activeReliefs.Select(static x => x.AwaitingResupply ? 1 : 0).ToList();

                originHeroIds = _adoptionOrigins.Keys.ToList();
                originVillageNames = _adoptionOrigins.Values.ToList();
            }

            dataStore.SyncData(PendingVillageIdsKey, ref pendingVillageIds);
            dataStore.SyncData(PendingVillageNamesKey, ref pendingVillageNames);
            dataStore.SyncData(PendingQueuedHoursKey, ref pendingQueuedHours);
            dataStore.SyncData(ActivePartyIdsKey, ref activePartyIds);
            dataStore.SyncData(ActiveVillageIdsKey, ref activeVillageIds);
            dataStore.SyncData(ActiveVillageNamesKey, ref activeVillageNames);
            dataStore.SyncData(ActiveStartedFlagsKey, ref activeStartedFlags);
            dataStore.SyncData(ActiveEndHoursKey, ref activeEndHours);
            dataStore.SyncData(ActiveAwaitingResupplyFlagsKey, ref activeAwaitingResupplyFlags);
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
                activeStartedFlags,
                activeEndHours,
                activeAwaitingResupplyFlags);
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
                    ReliefStarted = activeStartedFlags![i] != 0,
                    ReliefEndHours = activeEndHours![i],
                    AwaitingResupply = activeAwaitingResupplyFlags![i] != 0
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

            SanitizeLegacyAdoptionMapNotices();
            RebuildActiveReliefPartyIndex();
        }

        internal static bool IsVillageReliefParty(MobileParty? party)
        {
            return party != null && _activeReliefPartyIds.Contains(party.StringId);
        }

        internal static bool TryGetAdoptionOrigin(string? heroId, out string villageName)
        {
            villageName = string.Empty;
            if (_instance == null || string.IsNullOrWhiteSpace(heroId))
            {
                return false;
            }

            return _instance._adoptionOrigins.TryGetValue(heroId, out villageName);
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
            NormalizeReliefStateAfterLoad();
            RebuildActiveReliefPartyIndex();
            GreyWardenFamilyBehavior.RefreshPoliceClanFamilyPresentation();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            NormalizeReliefStateAfterLoad();
            RebuildActiveReliefPartyIndex();
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

                if (relief.AwaitingResupply)
                {
                    currentReliefStage = ReliefStage.AwaitingResupply;
                }
                else if (relief.ReliefStarted)
                {
                    currentReliefStage = ReliefStage.StayingInVillage;
                    currentReliefRemainingHours = Math.Max(0d, relief.ReliefEndHours - CampaignTime.Now.ToHours);
                }
                else
                {
                    currentReliefStage = ReliefStage.TravelingToVillage;
                }
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

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (!WasVillageSuccessfullyRaided(mapEvent, out Settlement? villageSettlement) || villageSettlement == null)
            {
                return;
            }

            if (!IsAdoptionSystemEligible() || HasAnyQueuedOrActiveRelief())
            {
                return;
            }

            _pendingReliefs.Clear();
            _pendingReliefs.Add(new PendingVillageRelief
            {
                VillageSettlementId = villageSettlement.StringId,
                VillageName = villageSettlement.Name?.ToString() ?? "无名村庄",
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
                AwaitingResupply = true,
                ReliefStarted = false,
                ReliefEndHours = 0d
            });

            _pendingReliefs.Clear();
            RebuildActiveReliefPartyIndex();
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

                if (police == null || !police.IsActive || village == null || !village.IsVillage)
                {
                    FinishRelief(relief, police, shouldAdopt: false);
                    continue;
                }

                if (ShouldAbortReliefForOperationalReason(police))
                {
                    FinishRelief(relief, police, shouldAdopt: false);
                    continue;
                }

                if (relief.AwaitingResupply)
                {
                    if (PoliceResourceManager.IsResupplying(police))
                    {
                        continue;
                    }

                    relief.AwaitingResupply = false;
                    police.Ai.SetDoNotMakeNewDecisions(true);
                    police.Ai.SetInitiative(1f, 0f, 999f);
                    police.SetMoveGoToSettlement(village, MobileParty.NavigationType.Default, false);
                    continue;
                }

                if (police.MapEvent != null && !police.MapEvent.IsFinalized)
                {
                    continue;
                }

                if (!relief.ReliefStarted)
                {
                    if (HasArrivedAtVillage(police, village))
                    {
                        relief.ReliefStarted = true;
                        relief.ReliefEndHours = CampaignTime.Now.ToHours + GwpTuning.Family.VillageReliefStayHours;
                        HoldPartyInVillage(police);
                    }
                    else
                    {
                        police.Ai.SetDoNotMakeNewDecisions(true);
                        police.Ai.SetInitiative(1f, 0f, 999f);
                        police.SetMoveGoToSettlement(village, MobileParty.NavigationType.Default, false);
                    }

                    continue;
                }

                HoldPartyInVillage(police);

                if (CampaignTime.Now.ToHours >= relief.ReliefEndHours)
                {
                    FinishRelief(relief, police, shouldAdopt: true);
                }
            }

            RebuildActiveReliefPartyIndex();
        }

        private void FinishRelief(ActiveVillageRelief relief, MobileParty? police, bool shouldAdopt)
        {
            Settlement? village = FindSettlement(relief.VillageSettlementId);
            Hero? adoptedHero = null;
            if (shouldAdopt && village != null && IsAdoptionSystemEligible())
            {
                adoptedHero = TryCreateAdoptedGirl(village, relief.VillageName);
            }

            if (police != null && police.IsActive)
            {
                GwpCommon.TryResetAi(police);
                if (!CrimePool.HasTask(police.StringId))
                {
                    PoliceResourceManager.StartResupply(police);
                }
            }

            _activeReliefs.Remove(relief);

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

            string villageName = village.Name?.ToString();
            if (string.IsNullOrWhiteSpace(villageName))
            {
                villageName = fallbackVillageName;
            }

            if (!string.IsNullOrWhiteSpace(hero.StringId) && !string.IsNullOrWhiteSpace(villageName))
            {
                _adoptionOrigins[hero.StringId] = villageName;
            }

            _lastAdoptionTimeHours = CampaignTime.Now.ToHours;
            return hero;
        }

        private static void PublishAdoptionLogEntry(Hero adoptedHero, string villageName)
        {
            try
            {
                if (adoptedHero == null)
                {
                    return;
                }
                LogEntry.AddLogEntry(new GreyWardenAdoptionLogEntry(adoptedHero, villageName));
            }
            catch
            {
            }
        }

        private static void EquipInitialChildGear(Hero hero)
        {
            try
            {
                MBEquipmentRoster? roster =
                    Campaign.Current.Models.EquipmentSelectionModel
                        .GetEquipmentRostersForInitialChildrenGeneration(hero)
                        .GetRandomElementInefficiently();

                if (roster == null)
                {
                    return;
                }

                EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, roster.DefaultEquipment);
                hero.CheckInvalidEquipmentsAndReplaceIfNeeded();
            }
            catch
            {
            }
        }

        private static bool WasVillageSuccessfullyRaided(MapEvent? mapEvent, out Settlement? villageSettlement)
        {
            villageSettlement = null;
            if (mapEvent == null || !mapEvent.IsRaid || !mapEvent.HasWinner || mapEvent.Winner != mapEvent.AttackerSide)
            {
                return false;
            }

            villageSettlement = mapEvent.MapEventSettlement;
            if (villageSettlement?.IsVillage == true)
            {
                return true;
            }

            villageSettlement = mapEvent.DefenderSide?.LeaderParty?.Settlement;
            return villageSettlement?.IsVillage == true;
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

        private static void HoldPartyInVillage(MobileParty police)
        {
            police.Ai.SetDoNotMakeNewDecisions(true);
            police.Ai.SetInitiative(0f, 0f, 0f);
            police.SetMoveModeHold();
        }

        private static bool ShouldAbortReliefForOperationalReason(MobileParty police)
        {
            return GwpCommon.IsPatrolParty(police) || GwpCommon.IsEnforcementDelayPatrolParty(police);
        }

        private static IEnumerable<MobileParty> GetReliefCandidatesByDistance(Settlement village)
        {
            return PoliceStats.GetAllPoliceParties()
                .Where(IsBasicVillageReliefCandidate)
                .OrderBy(police => police.GetPosition2D.Distance(village.GetPosition2D));
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
                || IsVillageReliefParty(police))
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

        private void NormalizeReliefStateAfterLoad()
        {
            bool hadBacklog = _pendingReliefs.Count > 0 || _activeReliefs.Count > 1;

            if (_pendingReliefs.Count > 0)
            {
                _pendingReliefs.Clear();
            }

            if (_activeReliefs.Count == 0)
            {
                RebuildActiveReliefPartyIndex();
                return;
            }

            if (hadBacklog)
            {
                foreach (ActiveVillageRelief relief in _activeReliefs.ToList())
                {
                    FinishRelief(relief, FindPoliceParty(relief.PolicePartyId), shouldAdopt: false);
                }

                RebuildActiveReliefPartyIndex();
                return;
            }

            ActiveVillageRelief keep = ChooseReliefToKeep(_activeReliefs);
            foreach (ActiveVillageRelief relief in _activeReliefs.ToList())
            {
                if (ReferenceEquals(relief, keep))
                {
                    continue;
                }

                FinishRelief(relief, FindPoliceParty(relief.PolicePartyId), shouldAdopt: false);
            }

            MobileParty? keepPolice = FindPoliceParty(keep.PolicePartyId);
            Settlement? keepVillage = FindSettlement(keep.VillageSettlementId);
            if (keepPolice == null || !keepPolice.IsActive || keepVillage == null || !keepVillage.IsVillage || !IsAdoptionSystemEligible())
            {
                FinishRelief(keep, keepPolice, shouldAdopt: false);
                RebuildActiveReliefPartyIndex();
                return;
            }

            if (keep.AwaitingResupply)
            {
                PoliceResourceManager.StartResupply(keepPolice);
            }

            RebuildActiveReliefPartyIndex();
        }

        // Older versions emitted ChildBornMapNotification for adopted girls.
        // Those notices crash vanilla's newborn notification VM after a reload
        // because adopted girls have no biological mother reference.
        private static void SanitizeLegacyAdoptionMapNotices()
        {
            try
            {
                CampaignInformationManager? informationManager = Campaign.Current?.CampaignInformationManager;
                if (informationManager == null || CampaignMapNoticesField == null)
                {
                    return;
                }

                if (CampaignMapNoticesField.GetValue(informationManager) is not List<InformationData> mapNotices ||
                    mapNotices.Count == 0)
                {
                    return;
                }

                for (int i = mapNotices.Count - 1; i >= 0; i--)
                {
                    if (mapNotices[i] is ChildBornMapNotification childNotice &&
                        (childNotice.NewbornHero == null || childNotice.NewbornHero.Mother == null))
                    {
                        mapNotices.RemoveAt(i);
                    }
                }
            }
            catch
            {
            }
        }

        private static ActiveVillageRelief ChooseReliefToKeep(IEnumerable<ActiveVillageRelief> reliefs)
        {
            return reliefs
                .OrderByDescending(static relief => relief.ReliefStarted)
                .ThenBy(static relief => relief.AwaitingResupply ? 1 : 0)
                .ThenBy(static relief => relief.ReliefEndHours <= 0d ? double.MaxValue : relief.ReliefEndHours)
                .First();
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
            public bool AwaitingResupply { get; set; }
            public bool ReliefStarted { get; set; }
            public double ReliefEndHours { get; set; }
        }

        internal enum ReliefStage
        {
            None,
            WaitingForAssignment,
            AwaitingResupply,
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
