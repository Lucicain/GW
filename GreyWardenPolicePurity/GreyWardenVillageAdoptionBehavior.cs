using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍村庄善后与收养逻辑。
    /// 村庄被成功劫掠后，派出一支空闲警察部队前往废墟驻留数日，
    /// 善后完成后若冷却与人数上限允许，则为灰袍家族新增一名被收留的女童。
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
        private const string OriginHeroIdsKey = "GWPP_AdoptOriginHeroIds";
        private const string OriginVillageNamesKey = "GWPP_AdoptOriginVillageNames";
        private const string LastAdoptionHoursKey = "GWPP_LastAdoptionHours";

        private static GreyWardenVillageAdoptionBehavior? _instance;
        private static readonly HashSet<string> _activeReliefPartyIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly List<PendingVillageRelief> _pendingReliefs = new List<PendingVillageRelief>();
        private readonly List<ActiveVillageRelief> _activeReliefs = new List<ActiveVillageRelief>();
        private readonly Dictionary<string, string> _adoptionOrigins =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private double _lastAdoptionTimeHours = -1000000d;

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

            int activeCount = MinCount(activePartyIds, activeVillageIds, activeVillageNames, activeStartedFlags, activeEndHours);
            for (int i = 0; i < activeCount; i++)
            {
                if (string.IsNullOrWhiteSpace(activePartyIds![i]) || string.IsNullOrWhiteSpace(activeVillageIds![i]))
                {
                    continue;
                }

                _activeReliefs.Add(new ActiveVillageRelief
                {
                    PolicePartyId = activePartyIds[i],
                    VillageSettlementId = activeVillageIds[i],
                    VillageName = activeVillageNames![i] ?? string.Empty,
                    ReliefStarted = activeStartedFlags![i] != 0,
                    ReliefEndHours = activeEndHours![i]
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

        internal static bool TryGetAdoptionOrigin(string? heroId, out string villageName)
        {
            villageName = string.Empty;
            if (_instance == null || string.IsNullOrWhiteSpace(heroId))
            {
                return false;
            }

            return _instance._adoptionOrigins.TryGetValue(heroId!, out villageName);
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            _pendingReliefs.Clear();
            _activeReliefs.Clear();
            _adoptionOrigins.Clear();
            _lastAdoptionTimeHours = -1000000d;
            RebuildActiveReliefPartyIndex();
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            RebuildActiveReliefPartyIndex();
            GreyWardenFamilyBehavior.RefreshPoliceClanFamilyPresentation();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            RebuildActiveReliefPartyIndex();
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (!WasVillageSuccessfullyRaided(mapEvent, out Settlement? villageSettlement))
            {
                return;
            }

            if (villageSettlement == null || !IsAdoptionSystemEligible())
            {
                return;
            }

            if (HasQueuedOrActiveRelief(villageSettlement.StringId))
            {
                return;
            }

            _pendingReliefs.Add(new PendingVillageRelief
            {
                VillageSettlementId = villageSettlement.StringId,
                VillageName = villageSettlement.Name?.ToString() ?? "无名村庄",
                QueuedTimeHours = CampaignTime.Now.ToHours
            });
        }

        private void OnHourlyTick()
        {
            AssignPendingReliefs();
            UpdateActiveReliefs();
        }

        private void AssignPendingReliefs()
        {
            if (_pendingReliefs.Count == 0)
            {
                return;
            }

            foreach (PendingVillageRelief pending in _pendingReliefs.ToList())
            {
                Settlement? village = FindSettlement(pending.VillageSettlementId);
                if (village == null || !village.IsVillage)
                {
                    _pendingReliefs.Remove(pending);
                    continue;
                }

                if (!IsAdoptionSystemEligible())
                {
                    break;
                }

                MobileParty? police = FindNearestAvailablePoliceParty(village);
                if (police == null)
                {
                    continue;
                }

                police.Ai.SetDoNotMakeNewDecisions(true);
                police.Ai.SetInitiative(1f, 0f, 999f);
                police.SetMoveGoToSettlement(village, MobileParty.NavigationType.Default, false);

                _activeReliefs.Add(new ActiveVillageRelief
                {
                    PolicePartyId = police.StringId,
                    VillageSettlementId = pending.VillageSettlementId,
                    VillageName = pending.VillageName,
                    ReliefStarted = false,
                    ReliefEndHours = 0d
                });

                _pendingReliefs.Remove(pending);
                RebuildActiveReliefPartyIndex();
            }
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
                MobileParty? police = MobileParty.All.FirstOrDefault(p => string.Equals(p.StringId, relief.PolicePartyId, StringComparison.OrdinalIgnoreCase));
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
            bool adopted = false;
            if (shouldAdopt && village != null && IsAdoptionSystemEligible())
            {
                adopted = TryCreateAdoptedGirl(village, relief.VillageName);
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

            if (adopted)
            {
                GreyWardenFamilyBehavior.RefreshPoliceClanFamilyPresentation();
            }
        }

        private bool TryCreateAdoptedGirl(Settlement village, string fallbackVillageName)
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            CharacterObject? template = CharacterObject.Find(GwpIds.CommanderTemplateCharacterId);
            if (policeClan == null || template == null)
            {
                return false;
            }

            int age = MBRandom.RandomInt(
                GwpTuning.Family.AdoptedGirlMinAge,
                GwpTuning.Family.AdoptedGirlMaxAge + 1);

            Hero hero = HeroCreator.CreateSpecialHero(template, village, policeClan, null, age);
            if (hero == null)
            {
                return false;
            }

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
            return true;
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
            double elapsedHours = CampaignTime.Now.ToHours - _lastAdoptionTimeHours;
            return elapsedHours >= GwpTuning.Family.AdoptionCooldownDays * 24f;
        }

        private bool HasQueuedOrActiveRelief(string villageSettlementId)
        {
            return _pendingReliefs.Any(x => string.Equals(x.VillageSettlementId, villageSettlementId, StringComparison.OrdinalIgnoreCase))
                   || _activeReliefs.Any(x => string.Equals(x.VillageSettlementId, villageSettlementId, StringComparison.OrdinalIgnoreCase));
        }

        private static Settlement? FindSettlement(string settlementId)
        {
            if (string.IsNullOrWhiteSpace(settlementId))
            {
                return null;
            }

            return Settlement.Find(settlementId);
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
            return GwpCommon.IsEnforcementDelayPatrolParty(police)
                   || PoliceResourceManager.IsResupplying(police)
                   || CrimePool.HasTask(police.StringId);
        }

        private static MobileParty? FindNearestAvailablePoliceParty(Settlement village)
        {
            MobileParty? nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (MobileParty police in PoliceStats.GetAllPoliceParties())
            {
                if (!IsEligiblePoliceParty(police))
                {
                    continue;
                }

                float distance = police.GetPosition2D.Distance(village.GetPosition2D);
                if (distance < nearestDistance)
                {
                    nearest = police;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private static bool IsEligiblePoliceParty(MobileParty? police)
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
                || IsVillageReliefParty(police)
                || PoliceResourceManager.IsResupplying(police)
                || CrimePool.HasTask(police.StringId))
            {
                return false;
            }

            return true;
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
            public bool ReliefStarted { get; set; }
            public double ReliefEndHours { get; set; }
        }
    }
}
