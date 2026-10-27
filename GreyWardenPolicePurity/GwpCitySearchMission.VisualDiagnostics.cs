#if GWP_DIAGNOSTICS
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchMissionController
    {
        private readonly Dictionary<Agent, (string Identity, ulong Visual, ulong Entity)> _searchVisuals = new();
        private readonly Dictionary<Agent, string> _visualRemovalStates = new();
        private float _nextVisualSample;

        /// <summary>2026-10-27原生转储：渲染队列里的Agent_visuals仍在使用，但+0x8B8实体已空；保存地址以便指认人物。</summary>
        private void CaptureSearchVisual(Agent agent, string sample)
        {
            if (!agent.IsActive()) return;
            var visuals = agent.AgentVisuals;
            ulong visual = visuals?.Pointer.ToUInt64() ?? 0;
            // 原版API283只返回弱实体指针，不创建GameEntity包装、不延长实体寿命；失效外观返回0。
            ulong entity = visual == 0 ? 0 : visuals!.GetWeakEntity().Pointer.ToUInt64();
            bool known = _searchVisuals.TryGetValue(agent, out var previous);
            if (known && previous.Visual == visual && previous.Entity == entity) return;
            string identity = known ? previous.Identity : "index=" + agent.Index + "; id=" + agent.Character?.StringId
                + "; name=" + agent.Character?.Name + "; human=" + agent.IsHuman;
            _searchVisuals[agent] = (identity, visual, entity);
            GwpAiDiagnostics.WriteCitySearch("VISUAL_STATE", "sample=" + sample + "; place=" + _place + "; " + identity
                + "; visual=0x" + visual.ToString("X") + "; entity=0x" + entity.ToString("X")
                + "; previousVisual=0x" + previous.Visual.ToString("X") + "; previousEntity=0x" + previous.Entity.ToString("X"));
        }

        private void TickSearchVisuals()
        {
            if (Mission.CurrentTime < _nextVisualSample) return;
            _nextVisualSample = Mission.CurrentTime + 0.5f;
            GwpCitySearchNativeVisualFix.WriteDiagnostics();
            // 包含玩家、我方、坐骑和新生增援；仅记录首次出现或地址变化，不逐帧刷日志。
            foreach (Agent agent in Mission.Agents) CaptureSearchVisual(agent, "tick");
        }

        private void WriteSearchVisualRemoval(Agent agent, string state)
        {
            // 移除/删除回调可能已清掉原生对象，严格只读此前保存的值。
            _visualRemovalStates[agent] = state;
            if (_searchVisuals.TryGetValue(agent, out var saved))
            {
                GwpAiDiagnostics.WriteCitySearch("VISUAL_REMOVED", "place=" + _place + "; " + saved.Identity
                    + "; state=" + state + "; visual=0x" + saved.Visual.ToString("X") + "; entity=0x" + saved.Entity.ToString("X"));
            }
        }

        private bool WasSearchVisualRemoved(Agent agent) => _visualRemovalStates.ContainsKey(agent);

        public override void OnAgentDeleted(Agent affectedAgent) => WriteSearchVisualRemoval(affectedAgent, "Deleted");

        internal (string Context, ulong Visual, long Before)? CaptureFadeTrace(Agent agent)
        {
            // 连重复FadeOut也取证；IsActive也读取原生状态指针，此处完全用移除回调与身份缓存。
            if (!_searchVisuals.TryGetValue(agent, out var saved)) return null;
            long before = GwpCitySearchNativeVisualFix.EmptyUpdateCount;
            string context = "place=" + _place + "; " + saved.Identity + "; removalSeen="
                + (_visualRemovalStates.TryGetValue(agent, out string state) ? state : "none")
                + "; visual=0x" + saved.Visual.ToString("X");
            GwpAiDiagnostics.WriteCitySearch("VISUAL_FADE_BEGIN", context + "; emptyUpdates=" + before);
            return (context, saved.Visual, before);
        }
    }

    [HarmonyPatch(typeof(Agent), nameof(Agent.FadeOut))]
    internal static class GwpCitySearchFadeDiagnosticsPatch
    {
        private static void Prefix(Agent __instance, out (string Context, ulong Visual, long Before)? __state) =>
            __state = GwpCitySearchMissionController.Current?.CaptureFadeTrace(__instance);

        private static void Postfix((string Context, ulong Visual, long Before)? __state)
        {
            if (!__state.HasValue) return;
            var trace = __state.Value;
            bool stable = GwpCitySearchNativeVisualFix.TryReadLastUpdate(out ulong visual, out long after);
            bool match = stable && after > trace.Before && visual == trace.Visual;
            GwpAiDiagnostics.WriteCitySearch("VISUAL_FADE_END", trace.Context + "; updatesBefore=" + trace.Before
                + "; updatesAfter=" + after + "; latestVisual=0x" + visual.ToString("X") + "; sampleStable=" + stable
                + "; matchingEmptyUpdateDuringCall=" + match
                + (match ? "; caller=" + System.Environment.StackTrace : ""));
        }
    }
}
#endif
