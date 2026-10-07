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
    /// 照原版地图战斗的声音：在战场位置建 3D 声音，看不见就暂停，结束时停（C#反编译 v1.4.8
    /// SandBox.GauntletUI.Map.GauntletMapEventVisual.Initialize / SetVisibility / OnMapEventEnd）。
    /// <list type="bullet">
    /// <item>不无缝循环：卡牌站里闪卡音效每次触发只放一遍（syndicate-cards card-audio.js，规则里只有 Play、没有循环）。
    /// 登记的类别 campaign_node 会自己循环（Native module_sounds.xml「persistent」；用户实测 2026-10-07 听到无缝重复），
    /// 所以放满一遍的时长就停，隔 GwpTuning.Firesworn.RitualMapSoundGapSeconds 秒再放，仪式结束就不再放。（用户裁定 2026-10-07）</item>
    /// <item>打开任何菜单（进村、遭遇）都暂停，关了接着放：进了仪式画面里面放的是打出音效，两边一起响会叠成回声（用户实测 2026-10-07）。</item>
    /// <item>地图界面暂停、恢复时原版统一暂停、恢复地图场景的声音（MapScreen.OnPause / OnResume → PauseSceneSounds / ResumeSceneSounds），
    /// 地图界面销毁时这里全部停掉。</item>
    /// </list>
    /// 声音只是界面，不存档：读档后按正在进行的仪式重新建。
    /// </summary>
    public sealed class GwpRitualMapSoundView : MapView
    {
        private sealed class Voice
        {
            public SoundEvent? Sound;
            public float Remaining;
        }

        private readonly Dictionary<string, Voice> _voices = new Dictionary<string, Voice>();
        private readonly Dictionary<GwpFireswornBehavior.RitualKind, float> _durations = new Dictionary<GwpFireswornBehavior.RitualKind, float>();
        private bool _synced;
        private bool _paused;

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            if (!_synced)
            {
                _synced = true;
                Sync();
            }
            bool menu = MapState.AtMenu;
            if (menu != _paused)
            {
                _paused = menu;
                foreach (Voice voice in _voices.Values)
                    if (menu)
                        voice.Sound?.Pause();
                    else
                        voice.Sound?.Resume();
            }
            if (_paused)
                return;
            foreach (KeyValuePair<string, Voice> entry in _voices.ToList())
            {
                Voice voice = entry.Value;
                voice.Remaining -= dt;
                if (voice.Remaining > 0f)
                    continue;
                if (voice.Sound != null)
                {
                    voice.Sound.Stop();
                    voice.Sound = null;
                    voice.Remaining = GwpTuning.Firesworn.RitualMapSoundGapSeconds;
                }
                else
                    Play(entry.Key, voice);
            }
        }

        protected override void OnHourlyTick()
        {
            base.OnHourlyTick();
            Sync();
        }

        protected override void OnFinalize()
        {
            foreach (string villageId in _voices.Keys.ToList())
                Stop(villageId, "map-screen-finalized");
            base.OnFinalize();
        }

        private void Sync()
        {
            HashSet<string> active = new HashSet<string>(GwpFireswornBehavior.ActiveRituals().Select(entry => entry.Key));
            foreach (string villageId in _voices.Keys.Where(id => !active.Contains(id)).ToList())
                Stop(villageId, "ritual-ended");
            foreach (string villageId in active.Where(id => !_voices.ContainsKey(id)))
                _voices[villageId] = new Voice();
        }

        private void Play(string villageId, Voice voice)
        {
            GwpFireswornBehavior.RitualKind kind = GwpFireswornBehavior.ActiveRituals().First(entry => entry.Key == villageId).Value;
            string name = kind == GwpFireswornBehavior.RitualKind.Damnation ? "gwp/firesworn/damnation" : "gwp/firesworn/congregation";
            if (!_durations.TryGetValue(kind, out float duration))
                _durations[kind] = duration = GwpRitualSoundOutput.DurationSeconds(
                    GwpFireswornArt.FlashSoundPath(kind == GwpFireswornBehavior.RitualKind.Damnation
                        ? GwpFireswornArt.DamnationBackground
                        : GwpFireswornArt.CongregationBackground));
            Settlement village = Settlement.Find(villageId);
            CampaignVec2 position = village.Position;
            float height = 0f;
            Campaign.Current.MapSceneWrapper.GetHeightAtPoint(in position, ref height);
            voice.Sound = SoundEvent.CreateEvent(SoundEvent.GetEventIdFromString(name), MapScreen.MapScene);
            voice.Sound.PlayInPosition(new Vec3(position.X, position.Y, height + 2f));
            voice.Remaining = duration;
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFaith("RITUAL_MAP_SOUND_PLAY", "village=" + villageId + "; sound=" + name +
                "; seconds=" + duration.ToString("0.00"));
#endif
        }

        private void Stop(string villageId, string reason)
        {
            _voices[villageId].Sound?.Stop();
            _voices.Remove(villageId);
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
