#if GWP_DIAGNOSTICS
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace GreyWardenPolicePurity
{
    /// <summary>现场已确认动作缓存失效；只观察原版名字查找，捕捉动作表加载前的失败调用栈。</summary>
    internal static class GwpActionCacheDiagnostics
    {
        private static readonly System.Collections.Generic.HashSet<string> Seen = new(System.StringComparer.Ordinal);
        private static bool _installed;
        private static volatile bool _actionsLoaded;

        internal static void InstallBeforeModulePatches()
        {
            if (_installed) return;
            // MBAnimation包装的空名分支读取ActionIndexCache，不能早装；生成桥接类只做UTF8封送和原生委托调用。
            new Harmony(SubModule.HarmonyId).Patch(
                AccessTools.Method(AccessTools.TypeByName("ManagedCallbacks.ScriptingInterfaceOfIMBAnimation"),
                    nameof(MBAnimation.GetActionCodeWithName)),
                postfix: new HarmonyMethod(typeof(GwpActionCacheDiagnostics), nameof(ObserveResolution)));
            _installed = true;
        }

        private static void ObserveResolution(string __0, int __result)
        {
            if (_actionsLoaded || __result >= 0 || (__0 != "act_inventory_idle_start"
                && __0 != "act_taunt_cheer_1" && __0 != "act_arena_spectator")) return;
            lock (Seen)
                if (!Seen.Add(__0)) return;
            // 三个已知原版动作各最多一条；写故障日志，避免战役日志开局轮转丢掉启动证据。
            GwpFaultTrace.Write("ACTION_CACHE_EARLY_MISS", details: "name=" + __0 + "; index=" + __result
                + "; caller=" + System.Environment.StackTrace);
        }

        internal static void MarkActionsLoaded()
        {
            if (_actionsLoaded) return;
            _actionsLoaded = true;
            GwpFaultTrace.Write("ACTION_CACHE_READY", details: "cachedIdle=" + ActionIndexCache.act_inventory_idle_start.Index
                + "; resolvedIdle=" + MBAnimation.GetActionCodeWithName("act_inventory_idle_start")
                + "; earlyMissNames=" + Seen.Count);
        }
    }

    /// <summary>预览横躺取证。动作表加载后才安装，只观察，不改动作、矩阵或资源。</summary>
    internal static class GwpCharacterPreviewDiagnostics
    {
        private sealed class Sample
        {
            internal ulong Visual;
            internal int Count;
            internal double NextAt;
        }

        private const int SamplesPerVisual = 3;
        private const int SessionLimit = 60;
        private static readonly ConditionalWeakTable<CharacterTableau, Sample> Samples = new();
        private static bool _installed;
        private static int _written;

        // 没有HarmonyPatch/TargetMethod标记，不进入模组启动扫补丁；原版此时还没加载动作表。
        internal static void Install()
        {
            if (_installed) return;
            new Harmony(SubModule.HarmonyId).Patch(
                AccessTools.Method(typeof(CharacterTableau), nameof(CharacterTableau.OnTick)),
                postfix: new HarmonyMethod(typeof(GwpCharacterPreviewDiagnostics), nameof(Observe)));
            _installed = true;
        }

        private static void Observe(CharacterTableau __instance, AgentVisuals? ____agentVisuals,
            AgentVisuals? ____mountVisuals, string ____charStringId, bool ____isVisualsDirty,
            bool ____isFinalized, bool ____isEnabled, int ____agentVisualLoadingCounter,
            int ____mountVisualLoadingCounter, MBActionSet ____characterActionSet, MatrixFrame ____initialSpawnFrame)
        {
            // 同原版显示门槛：隐藏、已销毁或资源未就绪时不读取骨骼。
            if (_written >= SessionLimit || ____isFinalized || !____isEnabled || ____isVisualsDirty
                || ____agentVisualLoadingCounter > 0 || ____mountVisualLoadingCounter > 0 || ____agentVisuals == null) return;
            Sample sample = Samples.GetOrCreateValue(__instance);
            ulong pointer = ____agentVisuals.GetVisuals().Pointer.ToUInt64();
            if (sample.Visual != pointer)
            {
                sample.Visual = pointer;
                sample.Count = 0;
                sample.NextAt = 0;
            }
            double now = (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
            if (sample.Count >= SamplesPerVisual || now < sample.NextAt) return;
            sample.Count++;
            sample.NextAt = now + 0.5;
            _written++;
            var entity = ____agentVisuals.GetEntity();
            MatrixFrame local = entity.GetFrame();
            MatrixFrame global = entity.GetGlobalFrame();
            MatrixFrame root = entity.Skeleton.GetBoneEntitialFrame(0);
            ActionIndexCache action = entity.Skeleton.GetActionAtChannel(0);
            GwpAiDiagnostics.WriteCharacterPreview("id=" + ____charStringId + "; sample=" + sample.Count
                + "; visual=0x" + pointer.ToString("X") + "; actionSet=" + ____characterActionSet.GetName()
                + "; cachedIdle=" + ActionIndexCache.act_inventory_idle_start.Index
                + "; resolvedIdle=" + MBAnimation.GetActionCodeWithName("act_inventory_idle_start")
                + "; action=" + action.Index + "; spawnUp=" + ____initialSpawnFrame.rotation.u
                + "; localUp=" + local.rotation.u + "; globalUp=" + global.rotation.u
                + "; rootUp=" + root.rotation.u + "; rootForward=" + root.rotation.f
                + "; mountUp=" + (____mountVisuals == null ? "none" : ____mountVisuals.GetEntity().GetGlobalFrame().rotation.u.ToString()));
        }
    }
}
#endif
