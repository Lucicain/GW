using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    internal static partial class GwpStrength
    {
        // A preview is synchronous business evaluation, never a diagnostic rerun.
        // Native enemy relations and its temporary danger list are restored on exit.
        [ThreadStatic] private static NativeFrame? _nativeFrame;
        [ThreadStatic] private static NativePreview? _nativePreview;

        internal sealed class NativeContact
        {
            internal MobileParty Target = null!;
            internal float Friendly, Enemy;
        }

        internal sealed class NativeFrame
        {
            internal MobileParty Actor = null!;
            internal NativeFrame? Previous;
            internal readonly List<NativeContact> Contacts = new List<NativeContact>();
        }

        private sealed class NativePreview
        {
            internal MobileParty Actor = null!, Target = null!;
            internal MobileParty? Excluded;
            internal bool IncludePlayer;
            internal NativeFrame? Result;
        }

        internal static bool IsNativePreview => _nativePreview != null;
        internal static bool IsInNativeEvaluation(MobileParty actor) => _nativeFrame?.Actor == actor;

        internal static NativeFrame? BeginNativeEvaluation(MobileParty actor)
        {
            if (_nativePreview?.Actor != actor && !GwpCommon.IsGreyWardenLordParty(actor)) return null;
            var frame = new NativeFrame { Actor = actor, Previous = _nativeFrame };
            _nativeFrame = frame;
            return frame;
        }

        internal static void EndNativeEvaluation(NativeFrame? frame)
        {
            if (frame != null && _nativeFrame == frame) _nativeFrame = frame.Previous;
        }

        internal static bool TryPreviewWar(IFaction first, IFaction second)
        {
            NativePreview? preview = _nativePreview;
            return preview != null &&
                (first == preview.Actor.MapFaction && second == preview.Target.MapFaction ||
                 second == preview.Actor.MapFaction && first == preview.Target.MapFaction);
        }

        private static readonly Func<DefaultMobilePartyAIModel, PartyBase, MobileParty, bool> OriginalIsEnemy =
            AccessTools.MethodDelegate<Func<DefaultMobilePartyAIModel, PartyBase, MobileParty, bool>>(
                AccessTools.Method(typeof(DefaultMobilePartyAIModel), "IsEnemy"));

        internal static bool NativeIsEnemy(DefaultMobilePartyAIModel model, PartyBase party, MobileParty actor) =>
            TryPreviewWar(party.MapFaction, actor.MapFaction) || OriginalIsEnemy(model, party, actor);

        internal static bool NativeWarRelation(IFaction first, IFaction second) =>
            TryPreviewWar(first, second) || first.IsAtWarWith(second);

        internal static MobileParty NativeNext(ref LocatableSearchData<MobileParty> search)
        {
            MobileParty party;
            do { party = MobileParty.FindNextLocatable(ref search); }
            while (party != null && party == _nativePreview?.Excluded);
            return party!;
        }

        // Injected immediately before the native friendly/enemy division. All
        // navigation, membership, distance weights and battle rules remain native.
        internal static void ObserveNativeRegion(MobileParty actor, MobileParty target,
            ref float friendly, ref float enemy)
        {
            NativeFrame? frame = _nativeFrame;
            if (frame?.Actor != actor) return;
            bool includePlayer = _nativePreview?.IncludePlayer == true ||
                PoliceEnforcementBehavior.NativeCaseIncludesPlayer(actor);
            // Existing player-request rule: the physically present player is a
            // participant even though his clan differs. Never count him remotely.
            MobileParty player = MobileParty.MainParty;
            if (includePlayer && actor != player && player?.IsActive == true &&
                player.CurrentSettlement == null && player.Position.IsOnLand == actor.Position.IsOnLand &&
                player.Position.ToVec2().Distance(actor.Position.ToVec2()) <=
                    Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius &&
                (player.Army == null || player.Army != actor.Army))
                friendly += CombatGroup(player);
            frame.Contacts.Add(new NativeContact { Target = target, Friendly = friendly, Enemy = enemy });
        }

        private static NativeContact? UnsafeNativeContact(NativeFrame frame, MobileParty target) =>
            frame.Contacts.FirstOrDefault(item => item.Friendly <= item.Enemy &&
                (item.Target == target || Campaign.Current.Models.MobilePartyAIModel
                    .ShouldConsiderAvoiding(frame.Actor, item.Target)));

        internal static void FinishNativeEvaluation(NativeFrame? frame,
            ref AiBehavior behavior, ref MobileParty target, ref float score)
        {
            if (frame == null) return;
            if (_nativePreview?.Actor == frame.Actor) _nativePreview.Result = frame;
            if (IsNativePreview || behavior != AiBehavior.EngageParty ||
                !PoliceEnforcementBehavior.TryGetNativeCaseTarget(frame.Actor, out MobileParty? offender) ||
                target != offender && target != CombatGroupOf(offender!)) return;
            if (UnsafeNativeContact(frame, CombatGroupOf(offender!)) == null) return;
            // Native has already evaluated every nearby threat. An attack can win
            // on its close-range multiplier despite a regional disadvantage.
            // Withhold this voluntary case attack; native avoidance stays intact.
            behavior = AiBehavior.None;
            target = null!;
            score = 0f;
        }

        internal static bool NativeAttackReady(MobileParty actor, MobileParty target)
        {
            NativeFrame? frame = _nativeFrame;
            return frame?.Actor == actor && frame.Contacts.Any(item => item.Target == target) &&
                UnsafeNativeContact(frame, target) == null;
        }

        private static NativeFrame ReadNativeRegion(MobileParty actor, MobileParty offender,
            bool includePlayer = false, MobileParty? excluded = null)
        {
            var preview = new NativePreview { Actor = actor, Target = CombatGroupOf(offender),
                IncludePlayer = includePlayer, Excluded = excluded };
            NativePreview? previous = _nativePreview;
            var danger = MobilePartyAi.DangerousPartiesAndTheirVecs.ToList();
            _nativePreview = preview;
            try
            {
                Campaign.Current.Models.MobilePartyAIModel.GetBestInitiativeBehavior(actor, out _, out _, out _, out _);
                return preview.Result ?? throw new InvalidOperationException("Native regional evaluation did not run.");
            }
            finally
            {
                _nativePreview = previous;
                MobilePartyAi.DangerousPartiesAndTheirVecs.Clear();
                MobilePartyAi.DangerousPartiesAndTheirVecs.AddRange(danger);
            }
        }
    }

    [HarmonyPatch(typeof(DefaultMobilePartyAIModel), nameof(DefaultMobilePartyAIModel.GetBestInitiativeBehavior))]
    internal static class GwpNativeRegionPatch
    {
        [HarmonyPrefix]
        private static void Prefix(MobileParty mobileParty, out GwpStrength.NativeFrame? __state) =>
            __state = GwpStrength.BeginNativeEvaluation(mobileParty);

        [HarmonyPostfix, HarmonyPriority(Priority.First)]
        private static void Postfix(GwpStrength.NativeFrame? __state, ref AiBehavior bestInitiativeBehavior,
            ref MobileParty bestInitiativeTargetParty, ref float bestInitiativeBehaviorScore) =>
            GwpStrength.FinishNativeEvaluation(__state, ref bestInitiativeBehavior,
                ref bestInitiativeTargetParty, ref bestInitiativeBehaviorScore);

        [HarmonyFinalizer]
        private static void Finalizer(GwpStrength.NativeFrame? __state) => GwpStrength.EndNativeEvaluation(__state);

        private static int LocalIndex(CodeInstruction code)
        {
            if (code.opcode == OpCodes.Ldloc_0 || code.opcode == OpCodes.Stloc_0) return 0;
            if (code.opcode == OpCodes.Ldloc_1 || code.opcode == OpCodes.Stloc_1) return 1;
            if (code.opcode == OpCodes.Ldloc_2 || code.opcode == OpCodes.Stloc_2) return 2;
            if (code.opcode == OpCodes.Ldloc_3 || code.opcode == OpCodes.Stloc_3) return 3;
            return code.operand is LocalBuilder local ? local.LocalIndex : Convert.ToInt32(code.operand);
        }

        private static bool LoadsLocal(CodeInstruction code) => code.opcode == OpCodes.Ldloc ||
            code.opcode == OpCodes.Ldloc_S || code.opcode == OpCodes.Ldloc_0 || code.opcode == OpCodes.Ldloc_1 ||
            code.opcode == OpCodes.Ldloc_2 || code.opcode == OpCodes.Ldloc_3;

        private static bool StoresLocal(CodeInstruction code) => code.opcode == OpCodes.Stloc ||
            code.opcode == OpCodes.Stloc_S || code.opcode == OpCodes.Stloc_0 || code.opcode == OpCodes.Stloc_1 ||
            code.opcode == OpCodes.Stloc_2 || code.opcode == OpCodes.Stloc_3;

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            int call = code.FindIndex(item => item.operand is MethodInfo method &&
                method.Name == "CalculateInitiativeScoresForEnemy");
            if (call < 7 || !LoadsLocal(code[call - 2]) || !LoadsLocal(code[call - 5]))
                throw new InvalidOperationException("Native initiative call shape changed.");
            int ratio = LocalIndex(code[call - 2]);
            int division = code.FindIndex(item => item.opcode == OpCodes.Div && code.IndexOf(item) + 1 < call &&
                StoresLocal(code[code.IndexOf(item) + 1]) && LocalIndex(code[code.IndexOf(item) + 1]) == ratio);
            if (division < 2 || !LoadsLocal(code[division - 2]) || !LoadsLocal(code[division - 1]))
                throw new InvalidOperationException("Native regional strength division changed.");
            CodeInstruction enemy = code[call - 5];
            var friendlyRef = new CodeInstruction(OpCodes.Ldloca, code[division - 2].operand ??
                LocalIndex(code[division - 2]));
            var enemyRef = new CodeInstruction(OpCodes.Ldloca, code[division - 1].operand ??
                LocalIndex(code[division - 1]));
            var inserted = new[] { new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(enemy.opcode, enemy.operand), friendlyRef, enemyRef,
                CodeInstruction.Call(typeof(GwpStrength), nameof(GwpStrength.ObserveNativeRegion)) };
            inserted[0].labels.AddRange(code[division - 2].labels);
            code[division - 2].labels.Clear();
            code.InsertRange(division - 2, inserted);
            foreach (CodeInstruction item in code)
            {
                if (item.operand is MethodInfo method && method.DeclaringType == typeof(MobileParty) &&
                    method.Name == nameof(MobileParty.FindNextLocatable))
                {
                    item.opcode = OpCodes.Call;
                    item.operand = AccessTools.Method(typeof(GwpStrength), nameof(GwpStrength.NativeNext));
                }
                else if (item.operand is MethodInfo enemyMethod &&
                         enemyMethod.DeclaringType == typeof(DefaultMobilePartyAIModel) && enemyMethod.Name == "IsEnemy")
                {
                    item.opcode = OpCodes.Call;
                    item.operand = AccessTools.Method(typeof(GwpStrength), nameof(GwpStrength.NativeIsEnemy));
                }
                else if (item.operand is MethodInfo relation && relation.DeclaringType == typeof(IFaction) &&
                         relation.Name == nameof(IFaction.IsAtWarWith))
                {
                    item.opcode = OpCodes.Call;
                    item.operand = AccessTools.Method(typeof(GwpStrength), nameof(GwpStrength.NativeWarRelation));
                }
                yield return item;
            }
        }
    }

}
