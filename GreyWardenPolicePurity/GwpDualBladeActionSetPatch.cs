using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using IOPath = System.IO.Path;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle;
using TaleWorlds.ObjectSystem;

namespace GreyWardenPolicePurity
{
    internal static class GwpDualBladeTrace
    {
#if GWP_DIAGNOSTICS
        private static readonly object Sync = new object();

        private static string LogPath => IOPath.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "GreyWarden-DualBlade-Trace.log");

        internal static void Write(
            string stage,
            Agent? agent = null,
            string? details = null)
        {
            try
            {
                string character = "-";
                string agentId = "-";
                if (agent != null)
                {
                    agentId = agent.Index.ToString();
                    character = agent.Character?.StringId ?? "-";
                }

                string line = DateTime.Now.ToString("O")
                    + " | " + stage
                    + " | agent=" + agentId
                    + " | character=" + character
                    + (string.IsNullOrEmpty(details)
                        ? string.Empty
                        : " | " + details)
                    + Environment.NewLine;
                lock (Sync)
                {
                    Directory.CreateDirectory(IOPath.GetDirectoryName(LogPath)!);
                    File.AppendAllText(LogPath, line);
                }
            }
            catch
            {
                // Diagnostics must never affect the game path.
            }
        }
#else
        internal static void Write(
            string stage,
            Agent? agent = null,
            string? details = null)
        {
        }
#endif
    }


    /// <summary>
    /// Adds the Grey Warden commander as the first Custom Battle choice while
    /// leaving the native commander_1..commander_24 objects untouched.  The
    /// old XML override of commander_2 caused the stock tableau to resolve a
    /// mixed character/object graph, which presented as a missing lord model.
    /// </summary>
    [HarmonyPatch(typeof(CustomBattleData), "get_Characters")]
    internal static class GwpCustomBattleCommanderListPatch
    {
        [HarmonyPrefix]
        private static void Prefix() =>
            GwpDualBladeTrace.Write("CUSTOM_BATTLE_CHARACTERS_GET_BEGIN");

        [HarmonyPostfix]
        private static void Postfix(ref IEnumerable<BasicCharacterObject> __result)
        {
            GwpCustomBattleCommanderListSupport.Insert(ref __result, "custom");
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(Exception? __exception)
        {
            if (__exception != null)
            {
                GwpDualBladeTrace.Write(
                    "CUSTOM_BATTLE_CHARACTERS_GET_FAILED",
                    details: __exception.GetType().FullName + ": " + __exception.Message);
            }
            return __exception;
        }
    }

    /// <summary>
    /// Naval DLC owns the custom-battle screen when the Naval DLC provider is
    /// active.  Its character catalogue is a separate iterator, so the
    /// CustomBattleData patch above never runs on that path.  Patch the Naval
    /// getter by reflection (the optional DLC assembly is not a compile-time
    /// dependency) and use the exact same safe materialisation/filtering.
    /// </summary>
    [HarmonyPatch]
    internal static class GwpNavalCustomBattleCommanderListPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            const string typeName =
                "NavalDLC.CustomBattle.CustomBattle.NavalCustomBattleData";
            Type? type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                try
                {
                    type = Assembly.Load(
                            new AssemblyName("NavalDLC.CustomBattle"))
                        .GetType(typeName, throwOnError: false);
                }
                catch
                {
                    // The optional DLC may not be installed or loaded yet.
                }
            }
            if (type == null)
            {
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                    .FirstOrDefault(candidate => candidate != null);
            }
            MethodInfo? getter = type == null
                ? null
                : AccessTools.DeclaredMethod(type, "get_Characters");
            GwpDualBladeTrace.Write(
                "NAVAL_CUSTOM_BATTLE_PATCH_TARGET",
                details: "type=" + (type?.AssemblyQualifiedName ?? "missing")
                    + "; getter=" + (getter?.ToString() ?? "missing"));
            if (getter != null)
                yield return getter;
        }

        [HarmonyPrefix]
        private static void Prefix() =>
            GwpDualBladeTrace.Write("NAVAL_CUSTOM_BATTLE_CHARACTERS_GET_BEGIN");

        [HarmonyPostfix]
        private static void Postfix(ref IEnumerable<BasicCharacterObject> __result)
        {
            GwpCustomBattleCommanderListSupport.Insert(ref __result, "naval");
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(Exception? __exception)
        {
            if (__exception != null)
            {
                GwpDualBladeTrace.Write(
                    "NAVAL_CUSTOM_BATTLE_CHARACTERS_GET_FAILED",
                    details: __exception.GetType().FullName + ": " + __exception.Message);
            }
            return __exception;
        }
    }

    internal static class GwpCustomBattleCommanderListSupport
    {
        internal static void Insert(
            ref IEnumerable<BasicCharacterObject> result,
            string source)
        {
            try
            {
                GwpDualBladeTrace.Write(
                    "CUSTOM_BATTLE_COMMANDER_INSERT_BEGIN",
                    details: "source=" + source);
                BasicCharacterObject? custom = Game.Current?.ObjectManager?
                    .GetObject<BasicCharacterObject>(GwpIds.CustomBattleCommanderId);
                if (custom == null)
                {
                    GwpDualBladeTrace.Write(
                        "CUSTOM_BATTLE_COMMANDER_INSERT_SKIPPED",
                        details: "source=" + source + "; reason=object_missing");
                    return;
                }

                List<BasicCharacterObject> characters = (result ?? Enumerable.Empty<BasicCharacterObject>())
                    .Where(character => character != null
                        && !string.Equals(
                            character.StringId,
                            custom.StringId,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();
                characters.Insert(0, custom);
                result = characters;
                GwpDualBladeTrace.Write(
                    "CUSTOM_BATTLE_COMMANDER_INSERT",
                    details: "source=" + source
                        + "; id=" + custom.StringId
                        + "; count=" + characters.Count);
            }
            catch (Exception exception)
            {
                GwpDualBladeTrace.Write(
                    "CUSTOM_BATTLE_COMMANDER_INSERT_FAILED",
                    details: "source=" + source + "; "
                        + exception.GetType().FullName + ": " + exception.Message);
            }
        }
    }

    /// <summary>
    /// Keeps the fixed blade templates registered so CharacterCode and
    /// tableaux can resolve the real crafted weapons. They are filtered only
    /// from the native crafting-template catalogue, which also keeps them out
    /// of the smithy and town-order selection.
    /// </summary>
    [HarmonyPatch(
        typeof(CraftingTemplate),
        nameof(CraftingTemplate.All),
        MethodType.Getter)]
    internal static class GwpDualBladeCraftingTemplateVisibilityPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            ref MBReadOnlyList<CraftingTemplate> __result)
        {
            if (__result == null)
                return;

            CraftingTemplate[] visible = __result
                .Where(template => template != null
                    && !GwpIds.DualBladeCraftingTemplateIds.Any(
                        id => string.Equals(
                            id,
                            template.StringId,
                            StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            if (visible.Length != __result.Count)
            {
                __result = new MBReadOnlyList<CraftingTemplate>(visible);
            }
        }
    }



    /// <summary>
    /// Pure loadout checks shared by the dual-blade patches. Dual wielding is
    /// deliberately limited to the human-controlled character, which is the
    /// only form ROT ever implemented and the only one this mod has ever had
    /// working. AI never qualifies, so no soldier enters a dual-blade path.
    /// </summary>
    internal static class GwpDualBladeLoadout
    {
        internal static bool HasCompleteLoadout(Agent? agent)
        {
            if (agent == null || !IsEligibleDualBladeUser(agent))
                return false;

            try
            {
                Equipment? spawnEquipment = agent.SpawnEquipment;
                if (spawnEquipment != null
                    && IsItem(spawnEquipment[EquipmentIndex.WeaponItemBeginSlot].Item,
                        GwpIds.DualBladeOffhandItemId)
                    && IsItem(spawnEquipment[EquipmentIndex.Weapon1].Item,
                        GwpIds.DualBladeMainhandItemId))
                {
                    return true;
                }

                MissionEquipment? equipment = agent.Equipment;
                return equipment != null
                    && IsItem(equipment[EquipmentIndex.WeaponItemBeginSlot],
                        GwpIds.DualBladeOffhandItemId)
                    && IsItem(equipment[EquipmentIndex.Weapon1],
                        GwpIds.DualBladeMainhandItemId);
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsEligibleDualBladeUser(Agent? agent) =>
            agent?.Character != null && !agent.IsAIControlled;

        private static bool IsItem(ItemObject? item, string itemId) =>
            item != null
            && string.Equals(item.StringId, itemId,
                StringComparison.OrdinalIgnoreCase);

        private static bool IsItem(in MissionWeapon weapon, string itemId) =>
            !weapon.IsEmpty && IsItem(weapon.Item, itemId);
    }
}
