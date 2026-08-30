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
        internal static bool IsTrackedPreviewCharacter(string? characterId)
        {
            return string.Equals(characterId, GwpIds.ArcherId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(characterId, GwpIds.CustomBattleCommanderId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(characterId, "commander_2", StringComparison.OrdinalIgnoreCase);
        }

        private static bool _auditedAfterLoad;

        /// <summary>
        /// OnGameStart runs before the item XMLs are deserialized, so the audit
        /// there has only ever been able to report missing=true and has never
        /// captured the two values that decide whether the off-hand blade can
        /// be wielded at all. Run it once more from the first real dual-blade
        /// spawn, where the objects are guaranteed to exist.
        /// </summary>
        internal static void AuditLoadedObjectsOnce(Game? game)
        {
            if (_auditedAfterLoad)
                return;

            _auditedAfterLoad = true;
            AuditLoadedObjects(game, "FirstDualBladeSpawn");
        }

        internal static void AuditLoadedObjects(
            Game? game,
            string phase = "OnGameStart")
        {
            try
            {
                if (game?.ObjectManager == null)
                {
                    Write("OBJECT_AUDIT_NO_GAME");
                    return;
                }

                // gwonehandedsword is the Grey Warden arming sword the paired
                // blades are supposed to match. Auditing it beside them turns
                // "the model looks wrong" into a comparison against a known
                // good item built from the same four crafting pieces.
                foreach (string itemId in new[]
                {
                    GwpIds.DualBladeOffhandItemId,
                    GwpIds.DualBladeMainhandItemId,
                    "gwonehandedsword"
                })
                {
                    ItemObject? item = game.ObjectManager.GetObject<ItemObject>(itemId);
                    Write(
                        "OBJECT_AUDIT_ITEM",
                        details: "phase=" + phase + "; "
                            + (item == null
                            ? "id=" + itemId + "; missing=true"
                            : "id=" + itemId
                              + "; type=" + item.Type
                              + "; body=" + item.BodyName
                              + "; collision=" + item.CollisionBodyName
                              + "; usage=" + item.PrimaryWeapon?.ItemUsage
                              + "; flags=" + item.ItemFlags));
                }

                foreach (string characterId in new[]
                {
                    GwpIds.ArcherId,
                    GwpIds.CustomBattleCommanderId
                })
                {
                    BasicCharacterObject? character = game.ObjectManager
                        .GetObject<BasicCharacterObject>(characterId);
                    Write(
                        "OBJECT_AUDIT_CHARACTER",
                        details: "phase=" + phase + "; "
                            + (character == null
                            ? "id=" + characterId + "; missing=true"
                            : "id=" + characterId
                              + "; equipment="
                              + character.Equipment.CalculateEquipmentCode()));
                }

                AuditActionSets();
            }
            catch (Exception exception)
            {
                Write(
                    "OBJECT_AUDIT_FAILED",
                    details: exception.GetType().Name + ": " + exception.Message);
            }
        }

        /// <summary>
        /// as_human_warrior and as_human_female_warrior are the sets every human
        /// in the game resolves, and CharacterTableau builds every preview from
        /// them, so record that they stay valid alongside the two dedicated
        /// dual-blade sets. CheckActionAnimationClipExists additionally says
        /// whether the animation clips the dual actions name are actually
        /// present in the loaded asset packages — an action that resolves to a
        /// missing clip is exactly what a broken pose looks like.
        /// </summary>
        private static void AuditActionSets()
        {
            string[] setIds =
            {
                "as_human_warrior",
                "as_human_female_warrior",
                "as_human_gwp_dual",
                "as_human_female_gwp_dual"
            };

            string[] probeActions =
            {
                "act_inventory_idle_start",
                "act_gwd_ready_thrust_1h",
                "act_walk_idle_1h_with_gwd_shld"
            };

            foreach (string setId in setIds)
            {
                MBActionSet actionSet = MBActionSet.GetActionSet(setId);
                string details = "id=" + setId + "; valid=" + actionSet.IsValid;

                if (actionSet.IsValid)
                {
                    foreach (string actionName in probeActions)
                    {
                        ActionIndexCache action =
                            ActionIndexCache.Create(actionName);
                        details += "; " + actionName
                            + "=index:" + action.Index
                            + ",clip:"
                            + (action.Index >= 0
                                && MBActionSet.CheckActionAnimationClipExists(
                                    actionSet, in action));
                    }
                }

                Write("ACTION_SET_AUDIT", details: details);
            }
        }

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
    /// The encyclopedia and the Custom Battle character preview both build
    /// their model through CharacterTableau, which takes the equipment code
    /// (confirmed to generate cleanly) and turns it into AgentVisuals. A blank
    /// or badly posed model there is invisible in every log we have: the last
    /// session produced no managed exception, no crash dump, no missing-asset
    /// message and no action-set assert.
    ///
    /// Observe that build without changing it. The finalizer returns the
    /// exception untouched, so behaviour is identical whether or not the
    /// tableau throws — it only makes a throwing preview visible in the trace.
    /// Resolved by name so a moved or renamed type just skips the patch.
    /// </summary>
    [HarmonyPatch]
    internal static class GwpCharacterTableauTracePatch
    {
        private static MethodBase? TargetMethod() =>
            AccessTools.TypeByName(
                "TaleWorlds.MountAndBlade.View.Tableaus.CharacterTableau") is Type tableau
                ? AccessTools.Method(tableau, "RefreshCharacterTableau")
                : null;

        private static bool Prepare() => TargetMethod() != null;

        [HarmonyFinalizer]
        private static Exception? Finalizer(Exception? __exception)
        {
            if (__exception != null)
            {
                GwpDualBladeTrace.Write(
                    "CHARACTER_TABLEAU_REFRESH_FAILED",
                    details: __exception.GetType().FullName
                        + ": " + __exception.Message);
            }

            return __exception;
        }
    }

    [HarmonyPatch(
        typeof(CharacterCode),
        nameof(CharacterCode.CreateFrom),
        new[] { typeof(BasicCharacterObject) })]
    internal static class GwpCharacterCodeTracePatch
    {
        [HarmonyPrefix]
        private static void Prefix(BasicCharacterObject character)
        {
            if (character == null)
                return;

            if (GwpDualBladeTrace.IsTrackedPreviewCharacter(character.StringId))
            {
                GwpDualBladeTrace.Write(
                    "CHARACTER_CODE_CREATE",
                    details: "character=" + character.StringId
                    + "; equipment="
                    + (character.Equipment?.CalculateEquipmentCode() ?? "-"));
            }
        }

        [HarmonyPostfix]
        private static void Postfix(BasicCharacterObject character)
        {
            if (character != null
                && GwpDualBladeTrace.IsTrackedPreviewCharacter(character.StringId))
            {
                GwpDualBladeTrace.Write(
                    "CHARACTER_CODE_CREATE_OK",
                    details: "character=" + character.StringId);
            }
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(
            Exception? __exception,
            BasicCharacterObject character)
        {
            if (character != null
                && GwpDualBladeTrace.IsTrackedPreviewCharacter(character.StringId))
            {
                GwpDualBladeTrace.Write(
                    "CHARACTER_CODE_CREATE_END",
                    details: "character=" + character.StringId
                    + "; exception=" + (__exception?.GetType().FullName ?? "none"));
            }
            return __exception;
        }
    }

    [HarmonyPatch(
        typeof(CharacterCode),
        nameof(CharacterCode.CreateFrom),
        new[] { typeof(BasicCharacterObject), typeof(Equipment) })]
    internal static class GwpCharacterCodeEquipmentTracePatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            BasicCharacterObject character,
            Equipment equipment)
        {
            if (character == null)
                return;

            if (GwpDualBladeTrace.IsTrackedPreviewCharacter(character.StringId))
            {
                GwpDualBladeTrace.Write(
                    "CHARACTER_CODE_CREATE_EQUIPMENT",
                    details: "character=" + character.StringId
                    + "; equipment="
                    + (equipment?.CalculateEquipmentCode() ?? "-"));
            }
        }

        [HarmonyPostfix]
        private static void Postfix(BasicCharacterObject character)
        {
            if (character != null
                && GwpDualBladeTrace.IsTrackedPreviewCharacter(character.StringId))
            {
                GwpDualBladeTrace.Write(
                    "CHARACTER_CODE_CREATE_EQUIPMENT_OK",
                    details: "character=" + character.StringId);
            }
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(
            Exception? __exception,
            BasicCharacterObject character)
        {
            if (character != null
                && GwpDualBladeTrace.IsTrackedPreviewCharacter(character.StringId))
            {
                GwpDualBladeTrace.Write(
                    "CHARACTER_CODE_CREATE_EQUIPMENT_END",
                    details: "character=" + character.StringId
                    + "; exception=" + (__exception?.GetType().FullName ?? "none"));
            }
            return __exception;
        }

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
    /// Installs the dedicated dual-blade action set only on a real mission
    /// Agent carrying the archer's complete pair.  Character tableaux keep
    /// reading the unmodified character and equipment definitions.
    /// </summary>
    [HarmonyPatch(
        typeof(Mission),
        nameof(Mission.SpawnAgent),
        new[]
        {
            typeof(AgentBuildData),
            typeof(bool),
            typeof(Equipment),
            typeof(ItemObject)
        })]
    internal static class GwpDualBladeActionSetPatch
    {
        // Native builds a special action set name from the agent's monster and
        // gender (see MBGlobals.GetActionSetWithSuffix), so a dual-blade set has
        // to exist for both. action_sets.xslt emits as_human_gwp_dual from
        // as_human_warrior and as_human_female_gwp_dual from
        // as_human_female_warrior; the female one keeps its base_set, so it
        // still inherits the shared animations instead of replacing a female
        // agent's skeleton-bound set with the male root set.
        private const string DualBladeActionSetSuffix = "_gwp_dual";

        [HarmonyPostfix]
        private static void Postfix(Agent __result)
        {
            if (__result == null)
                return;

            bool isAiDual = GwpDualBladeLoadout.HasCompleteAiLoadout(__result);
            bool isPlayerDual = GwpDualBladeLoadout.HasCompletePlayerLoadout(__result);

            if (!isAiDual && !isPlayerDual)
                return;

            GwpDualBladeTrace.AuditLoadedObjectsOnce(Game.Current);

            bool applied = TryApplyActionSet(__result);
            GwpDualBladeWieldSync.Attach(__result);
            GwpDualBladeTrace.Write(
                "SPAWN_AGENT_POSTFIX",
                __result,
                "isAi=" + isAiDual
                + "; isPlayer=" + isPlayerDual
                + "; female=" + __result.IsFemale
                + "; actionSet=" + applied
                + "; main=" + __result.GetPrimaryWieldedItemIndex()
                + "; offhand=" + __result.GetOffhandWieldedItemIndex());
#if GWP_DIAGNOSTICS
            Debug.Print(
                "[GreyWarden Dual Blade] archer_spawn character="
                + __result.Character.StringId
                + "; action_set=" + applied
                + "; main=" + __result.GetPrimaryWieldedItemIndex()
                + "; offhand=" + __result.GetOffhandWieldedItemIndex());
#endif
        }

        internal static bool TryApplyActionSet(Agent? agent)
        {
            if (agent == null)
                return false;

            string actionSetId = ActionSetCode.GenerateActionSetNameWithSuffix(
                agent.Monster,
                agent.IsFemale,
                DualBladeActionSetSuffix);

            MBActionSet actionSet = MBActionSet.GetActionSet(actionSetId);
            if (!actionSet.IsValid)
            {
                GwpDualBladeTrace.Write(
                    "ACTION_SET_MISSING",
                    agent,
                    "actionSet=" + actionSetId);
                return false;
            }

            AnimationSystemData animationSystemData = agent.Monster
                .FillAnimationSystemData(
                    actionSet,
                    agent.Character.GetStepSize(),
                    hasClippingPlane: false);
            agent.SetActionSet(ref animationSystemData);
            return true;
        }
    }

    /// <summary>
    /// Uses the actual Agent event rather than detouring the Native callback.
    /// When an archer changes between bow and the Weapon1 sword, pair or sheath
    /// Weapon0 once.  The transitional "off-hand first, main hand still None"
    /// state is deliberately left alone.
    /// </summary>
    internal static class GwpDualBladeWieldSync
    {
        [ThreadStatic]
        private static bool _synchronizing;

        internal static void Attach(Agent agent)
        {
            agent.OnAgentWieldedItemChange += () => Synchronize(agent);
        }

        internal static void Synchronize(Agent agent)
        {
            if (_synchronizing
                || !GwpDualBladeLoadout.HasCompleteLoadout(agent))
            {
                return;
            }

            EquipmentIndex primarySlot = agent.GetPrimaryWieldedItemIndex();
            EquipmentIndex offhandSlot = agent.GetOffhandWieldedItemIndex();

            if (primarySlot == EquipmentIndex.Weapon1)
            {
                if (offhandSlot != EquipmentIndex.WeaponItemBeginSlot)
                {
                    _synchronizing = true;
                    try
                    {
                        GwpDualBladeTrace.Write(
                            "ARCHER_OFFHAND_PAIR_REQUEST",
                            agent,
                            "main=" + primarySlot + "; offhand=" + offhandSlot);

                        // A bare single-slot request for Weapon0 is rejected by
                        // native once the main hand is already drawn: the trace
                        // records main=Weapon1, offhand=None both before and
                        // after the call, 199 times a session. The
                        // player-controlled commander carries the very same two
                        // items and does get the pair, because it goes through
                        // WieldInitialWeapons, which wields the off hand first
                        // and passes isWieldedOnSpawn: true. Re-run that native
                        // routine instead of re-issuing the request native has
                        // already refused. These characters carry nothing but
                        // the pair, so "initial weapons" is exactly the pair.
                        agent.WieldInitialWeapons(
                            Agent.WeaponWieldActionType.InstantAfterPickUp);

                        GwpDualBladeTrace.Write(
                            "ARCHER_OFFHAND_PAIR_RESULT",
                            agent,
                            "main=" + agent.GetPrimaryWieldedItemIndex()
                            + "; offhand=" + agent.GetOffhandWieldedItemIndex());
                    }
                    finally
                    {
                        _synchronizing = false;
                    }
#if GWP_DIAGNOSTICS
                    Debug.Print(
                        "[GreyWarden Dual Blade] archer_melee_pair character="
                        + agent.Character.StringId
                        + "; main=" + primarySlot
                        + "; offhand="
                        + agent.GetOffhandWieldedItemIndex());
#endif
                }
            }
            else if (primarySlot != EquipmentIndex.None)
            {
                if (offhandSlot == EquipmentIndex.WeaponItemBeginSlot)
                {
                    _synchronizing = true;
                    try
                    {
                        agent.TryToSheathWeaponInHand(
                            Agent.HandIndex.OffHand,
                            Agent.WeaponWieldActionType.InstantAfterPickUp);
                    }
                    finally
                    {
                        _synchronizing = false;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Pure loadout checks shared by the dual-blade mission patches. AI
    /// qualification additionally requires membership in the current Mission;
    /// this excludes tableau helpers and other non-battle preview objects.
    /// </summary>
    internal static class GwpDualBladeLoadout
    {
        // AI enhancement is now re-enabled only for a real mission Agent that
        // is controlled by the game and carries the complete archer pair.
        // Preview/tableau agents do not have a Mission and therefore cannot
        // enter the equipment/action-set synchronization scope.
        internal static bool HasCompleteAiLoadout(Agent? agent) =>
            agent != null
            && agent.IsAIControlled
            && agent.Mission != null
            && HasCompleteLoadout(agent);

        internal static bool HasCompletePlayerLoadout(Agent? agent) =>
            agent != null && !agent.IsAIControlled && HasCompleteLoadout(agent);

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

        internal static bool IsEligibleDualBladeUser(Agent? agent)
        {
            if (agent?.Character == null)
                return false;

            // A human-controlled character may use the pair received from
            // GreyWarden. AI qualification stays limited to the two explicit
            // dual-blade character definitions; ordinary soldiers never enter
            // the action-set or wield-synchronisation path.
            return !agent.IsAIControlled
                || string.Equals(
                    agent.Character.StringId,
                    GwpIds.ArcherId,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    agent.Character.StringId,
                    GwpIds.CustomBattleCommanderId,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsItem(ItemObject? item, string itemId) =>
            item != null
            && string.Equals(item.StringId, itemId,
                StringComparison.OrdinalIgnoreCase);

        private static bool IsItem(in MissionWeapon weapon, string itemId) =>
            !weapon.IsEmpty && IsItem(weapon.Item, itemId);
    }
}
