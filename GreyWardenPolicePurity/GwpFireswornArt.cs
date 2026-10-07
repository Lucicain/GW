using System;
using System.IO;
using TaleWorlds.Core;
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
    /// 配音由 GwpFireswornSoundView 管理，生命周期随原版菜单视图。
    /// </summary>
    internal static class GwpFireswornArt
    {
        internal const string DamnationBackground = "gwp_firesworn_damnation";
        internal const string CongregationBackground = "gwp_firesworn_congregation";

        internal static string Background(GwpFireswornBehavior.RitualKind kind)
        {
            string name = kind == GwpFireswornBehavior.RitualKind.Damnation ? DamnationBackground : CongregationBackground;
            if (!UIResourceManager.SpriteData.Sprites.ContainsKey(name))
                UIResourceManager.SpriteData.Sprites[name] = LoadSprite(name);
            return name;
        }

        private static Sprite LoadSprite(string name)
        {
            byte[] png = File.ReadAllBytes(Path.Combine(ModuleDirectory(), "GUI", "Images", name + ".png"));
            GwpAffairsIcon.TryReadPngSize(png, out int width, out int height);
            EngineTextureObject texture = EngineTextureObject.CreateFromMemory(png);
            texture.Name = name;
            return new GwpTextureSprite(name, new UiTexture(new EngineTexture(texture)), width, height);
        }

        /// <summary>
        /// 点进仪式画面播随机一段「打出」音效（<c>{背景名}_play_N.wav</c>），地图上播闪卡音效（<see cref="GwpRitualMapSoundView"/>）
        /// （用户裁定 2026-10-07）。
        /// </summary>
        internal static string SoundPath(string background)
        {
            string[] plays = Directory.GetFiles(Path.Combine(ModuleDirectory(), "ModuleSounds"), background + "_play_*.wav");
            return plays[MBRandom.RandomInt(plays.Length)];
        }

        /// <summary>闪卡音效（大地图上放的那段）。</summary>
        internal static string FlashSoundPath(string background) =>
            Path.Combine(ModuleDirectory(), "ModuleSounds", background + ".wav");

        private static string ModuleDirectory() => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(SubModule).Assembly.Location)!, "../.."));
    }
}
