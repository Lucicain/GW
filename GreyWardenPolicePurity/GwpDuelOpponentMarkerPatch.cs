using System.Linq;
using HarmonyLib;
using SandBox.ViewModelCollection.Missions.NameMarker;
using SandBox.ViewModelCollection.Missions.NameMarker.Targets;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    // Native town markers classify lords by campaign diplomacy, not duel teams.
    // Add just this mission's opponent without declaring a campaign war.
    [HarmonyPatch(typeof(MissionNameMarkerVM), nameof(MissionNameMarkerVM.Tick))]
    internal static class GwpDuelOpponentMarkerPatch
    {
        private static void Postfix(MissionNameMarkerVM __instance)
        {
            Agent? target = Mission.Current?.GetMissionBehavior<GreyWardenFieldSparringMissionController>()?.MarkerOpponent;
            if (target == null) return;
            var marker = __instance.Targets.OfType<MissionAgentMarkerTargetVM>().FirstOrDefault(m => m.Target == target);
            if (!target.IsActive())
            {
                if (marker != null) __instance.Targets.Remove(marker);
                return;
            }
            if (marker == null)
            {
                marker = new MissionAgentMarkerTargetVM(target);
                __instance.Targets.Add(marker);
            }
            marker.IsEnemy = true;
            marker.IsFriendly = false;
            marker.NameType = "Enemy";
            marker.SetEnabledState(__instance.IsEnabled);
        }
    }
}
