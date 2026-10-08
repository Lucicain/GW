#if GWP_DIAGNOSTICS
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 取证：地图仪式声音进菜单时停不停得掉（用户 2026-10-08：停掉以后仍和菜单打出音效叠在一起）。
    /// 反汇编（TaleWorlds.Native v1.4.8）看停止是通的：StopEvent 按编号查表 → 事件对象停止函数 0x48c870 → FMOD EventInstance::stop。
    /// 这里记每次放、每次进菜单停，停后 1 秒、3 秒按编号再查一次是否有效、是否还在放，分清是停不掉、被别处重新放起来，还是另有声源。
    /// 用户确认声音不再重叠后整份删掉。
    /// </summary>
    public sealed partial class GwpRitualMapSoundView
    {
        private static readonly ConstructorInfo SoundById = AccessTools.Constructor(typeof(SoundEvent), new[] { typeof(int) });

        private sealed class Probe
        {
            public string VillageId = string.Empty;
            public int SoundId;
            public float Elapsed;
            public int Step;
        }

        private readonly List<Probe> _probes = new List<Probe>();

        partial void TracePlay(string villageId, SoundEvent sound) =>
            GwpAiDiagnostics.WriteFaith("RITUAL_MAP_SOUND_PLAY",
                "village=" + villageId + "; soundId=" + sound.GetSoundId() + "; atMenu=" + MapState.AtMenu +
                "; voices=" + _voices.Count + "; totalEvents=" + SoundEvent.GetTotalEventCount());

        partial void TraceMenuStop(string villageId, SoundEvent sound)
        {
            GwpAiDiagnostics.WriteFaith("RITUAL_MAP_SOUND_MENU_STOP",
                "village=" + villageId + "; soundId=" + sound.GetSoundId() + "; valid=" + sound.IsValid +
                "; playing=" + sound.IsPlaying() + "; paused=" + sound.IsPaused());
            _probes.Add(new Probe { VillageId = villageId, SoundId = sound.GetSoundId() });
        }

        partial void TraceTick(float dt)
        {
            for (int i = _probes.Count - 1; i >= 0; i--)
            {
                Probe probe = _probes[i];
                probe.Elapsed += dt;
                float due = probe.Step == 0 ? 1f : 3f;
                if (probe.Elapsed < due)
                    continue;
                var sound = (SoundEvent)SoundById.Invoke(new object[] { probe.SoundId });
                GwpAiDiagnostics.WriteFaith("RITUAL_MAP_SOUND_PROBE",
                    "village=" + probe.VillageId + "; soundId=" + probe.SoundId + "; afterSeconds=" + due +
                    "; valid=" + sound.IsValid + "; playing=" + sound.IsPlaying() + "; paused=" + sound.IsPaused() +
                    "; atMenu=" + MapState.AtMenu);
                probe.Step++;
                if (probe.Step >= 2)
                    _probes.RemoveAt(i);
            }
        }
    }
}
#endif
