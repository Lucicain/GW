using System;
using System.IO;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.TwoDimension;
using EngineTextureObject = TaleWorlds.Engine.Texture;
using Path = System.IO.Path;
using UiTexture = TaleWorlds.TwoDimension.Texture;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 烧人、传教的配图与配音，替换原版村庄菜单的背景图（用户 2026-10-06：用辛迪加的素材，做原版替换）。
    ///
    /// 原版菜单背景是按名字从全局精灵表取的精灵（Native GUI/Prefabs/GameMenu/GameMenu.xml：
    /// GameMenuWidget Sprite="@Background"，名字来自 MenuContext.SetBackgroundMeshName）。
    /// 把卡面原画（「烈火责罚」202352、「集会传教」202374，卡牌站 art/*.webp 转成 PNG，放在 GUI/Images）
    /// 在运行时读成贴图、以自己的名字登记进 <c>UIResourceManager.SpriteData.Sprites</c>，
    /// 菜单背景设成这个名字，就是直接替换。建贴图的做法同地图栏灰袍事务图标（<see cref="GwpAffairsIcon"/>）。
    ///
    /// 配音是卡牌的闪卡音效（module_sounds.xml）。在大地图上用地图场景建声音事件播放，
    /// 做法同战场援军的号角（GwpBattleReinforcementBehavior.StartHorn）。
    /// </summary>
    internal static class GwpFireswornArt
    {
        internal const string DamnationBackground = "gwp_firesworn_damnation";
        internal const string CongregationBackground = "gwp_firesworn_congregation";

        private static SoundEvent? _playing;

        internal static string Background(GwpFireswornBehavior.RitualKind kind)
        {
            string name = kind == GwpFireswornBehavior.RitualKind.Damnation ? DamnationBackground : CongregationBackground;
            if (!UIResourceManager.SpriteData.Sprites.ContainsKey(name))
                UIResourceManager.SpriteData.Sprites[name] = LoadSprite(name);
            return name;
        }

        private static Sprite LoadSprite(string name)
        {
            string module = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(typeof(SubModule).Assembly.Location)!, "../.."));
            byte[] png = File.ReadAllBytes(Path.Combine(module, "GUI", "Images", name + ".png"));
            GwpAffairsIcon.TryReadPngSize(png, out int width, out int height);
            EngineTextureObject texture = EngineTextureObject.CreateFromMemory(png);
            texture.Name = name;
            return new GwpTextureSprite(name, new UiTexture(new EngineTexture(texture)), width, height);
        }

        /// <summary>放这场仪式的配音；上一段还在放就先停掉。返回声音事件编号，给监控看（小于 0 是没找到）。</summary>
        internal static int PlaySound(GwpFireswornBehavior.RitualKind kind)
        {
            int id = SoundEvent.GetEventIdFromString(kind == GwpFireswornBehavior.RitualKind.Damnation
                ? GwpFireswornMenus.DamnationSound
                : GwpFireswornMenus.CongregationSound);
            if (id < 0)
                return id;
            Scene scene = ((MapScene)Campaign.Current.MapSceneWrapper).Scene;
            _playing?.Stop();
            _playing?.Release();
            _playing = SoundEvent.CreateEvent(id, scene);
            _playing.Play();
            return id;
        }
    }
}
