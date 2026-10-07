using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 仪式进行时，大地图上那座村子放闪卡音效（用户裁定 2026-10-07：地图上用闪卡音效，点进去随机打出音效）。
    /// 照原版地图战斗的声音：开打时在战场位置建一个循环的 3D 声音，结束时停（C#反编译 v1.4.8
    /// SandBox.GauntletUI.Map.GauntletMapEventVisual.Initialize / OnMapEventEnd）。声音挂在地图场景上，
    /// 地图界面暂停、恢复时原版统一暂停、恢复地图场景的声音（MapScreen.OnPause / OnResume →
    /// PauseSceneSounds / ResumeSceneSounds），地图界面销毁时这里全部停掉。
    /// 类别 campaign_node 是原版给地图定点声音（河流等）的持续类别（Native module_sounds.xml），镜头离得近才听得到。
    /// 声音只是界面，不存档：读档后按正在进行的仪式重新建。
    /// </summary>
    public sealed class GwpRitualMapSoundView : MapView
    {
        private readonly Dictionary<string, SoundEvent> _sounds = new Dictionary<string, SoundEvent>();
        private bool _synced;

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            if (_synced)
                return;
            _synced = true;
            Sync();
        }

        protected override void OnHourlyTick()
        {
            base.OnHourlyTick();
            Sync();
        }

        protected override void OnFinalize()
        {
            foreach (string villageId in _sounds.Keys.ToList())
                Stop(villageId, "map-screen-finalized");
            base.OnFinalize();
        }

        private void Sync()
        {
            Dictionary<string, GwpFireswornBehavior.RitualKind> active = GwpFireswornBehavior.ActiveRituals()
                .GroupBy(entry => entry.Key)
                .ToDictionary(group => group.Key, group => group.First().Value);
            foreach (string villageId in _sounds.Keys.Where(id => !active.ContainsKey(id)).ToList())
                Stop(villageId, "ritual-ended");
            foreach (KeyValuePair<string, GwpFireswornBehavior.RitualKind> entry in active)
                if (!_sounds.ContainsKey(entry.Key))
                    Start(Settlement.Find(entry.Key), entry.Value);
        }

        private void Start(Settlement village, GwpFireswornBehavior.RitualKind kind)
        {
            string name = kind == GwpFireswornBehavior.RitualKind.Damnation ? "gwp/firesworn/damnation" : "gwp/firesworn/congregation";
            CampaignVec2 position = village.Position;
            float height = 0f;
            Campaign.Current.MapSceneWrapper.GetHeightAtPoint(in position, ref height);
            SoundEvent sound = SoundEvent.CreateEvent(SoundEvent.GetEventIdFromString(name), MapScreen.MapScene);
            sound.PlayInPosition(new Vec3(position.X, position.Y, height + 2f));
            _sounds[village.StringId] = sound;
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFaith("RITUAL_MAP_SOUND_START", "village=" + village.StringId + "; sound=" + name);
#endif
        }

        private void Stop(string villageId, string reason)
        {
            _sounds[villageId].Stop();
            _sounds.Remove(villageId);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFaith("RITUAL_MAP_SOUND_STOP", "village=" + villageId + "; reason=" + reason);
#else
            _ = reason;
#endif
        }
    }

    /// <summary>原版在 MapScreen 构造和初始化时加各个地图视图（C#反编译 v1.4.8 MapScreen），仪式声音视图跟在初始化后面加。</summary>
    [HarmonyPatch(typeof(MapScreen), "OnInitialize")]
    internal static class GwpRitualMapSoundViewPatch
    {
        private static void Postfix(MapScreen __instance) => __instance.AddMapView<GwpRitualMapSoundView>();
    }
}
