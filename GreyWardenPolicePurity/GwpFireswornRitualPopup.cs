using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;
using EngineTextureObject = TaleWorlds.Engine.Texture;
using UiTexture = TaleWorlds.TwoDimension.Texture;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 烧人、传教的配图与配音（用户 2026-10-06：用辛迪加的素材）。弹窗里放卡面原画
    /// （「烈火责罚」202352、「集会传教」202374，从卡牌站 art/*.webp 转成 PNG，放在 GUI/Images），
    /// 同时放卡牌的闪卡音效。玩家进正在办仪式的村子、或者仪式在玩家附近开始时弹出。
    /// 原画在运行时读入，建贴图的做法同地图栏灰袍事务图标（<see cref="GwpAffairsIcon"/>）。
    /// </summary>
    internal sealed class GwpFireswornRitualPopup
    {
        private const string ArtWidgetId = "GwpRitualArt";

        private static GwpFireswornRitualPopup? _active;
        // 引擎贴图整局只读一次。
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        private readonly ScreenBase _host;
        private readonly GwpFireswornRitualPopupVM _dataSource;
        private GauntletLayer? _layer;

        private GwpFireswornRitualPopup(ScreenBase host, string title, string text)
        {
            _host = host;
            _dataSource = new GwpFireswornRitualPopupVM(title, text, Close);
        }

        internal static void Show(GwpFireswornBehavior.RitualKind kind, string title, string text)
        {
            ScreenBase? host = ScreenManager.TopScreen;
            if (host == null)
                return;
            _active?.Close();
            _active = new GwpFireswornRitualPopup(host, title, text);
            _active.Open(kind);
        }

        private void Open(GwpFireswornBehavior.RitualKind kind)
        {
            bool damnation = kind == GwpFireswornBehavior.RitualKind.Damnation;
            _layer = new GauntletLayer("GwpFireswornRitual", 230, false)
            {
                IsFocusLayer = true,
                ActiveCursor = CursorType.Default
            };
            _layer.InputRestrictions.SetInputRestrictions(true,
                InputUsageMask.All | InputUsageMask.BlockEverythingWithoutHitTest);
            _host.AddLayer(_layer);
            IGauntletMovie movie = _layer.LoadMovie("GwpFireswornRitual", _dataSource).Movie;
            GwpGauntletWidgetUtility.FindById(movie.RootWidget, ArtWidgetId)!.Sprite =
                ArtSprite(damnation ? "gwp_firesworn_damnation.png" : "gwp_firesworn_congregation.png");
            ScreenManager.TrySetFocus(_layer);
            if (Campaign.Current != null)
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            SoundEvent.PlaySound2D(SoundEvent.GetEventIdFromString(
                damnation ? GwpFireswornMenus.DamnationSound : GwpFireswornMenus.CongregationSound));
        }

        private static Sprite ArtSprite(string fileName)
        {
            if (Sprites.TryGetValue(fileName, out Sprite? sprite))
                return sprite;
            string module = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(typeof(SubModule).Assembly.Location)!, "../.."));
            byte[] png = File.ReadAllBytes(Path.Combine(module, "GUI", "Images", fileName));
            GwpAffairsIcon.TryReadPngSize(png, out int width, out int height);
            EngineTextureObject texture = EngineTextureObject.CreateFromMemory(png);
            texture.Name = Path.GetFileNameWithoutExtension(fileName);
            sprite = new GwpTextureSprite(texture.Name, new UiTexture(new EngineTexture(texture)), width, height);
            Sprites[fileName] = sprite;
            return sprite;
        }

        private void Close()
        {
            if (_layer != null)
            {
                ScreenManager.TryLoseFocus(_layer);
                _layer.InputRestrictions.ResetInputRestrictions();
                if (_host.HasLayer(_layer))
                    _host.RemoveLayer(_layer);
                _layer = null;
            }
            if (ReferenceEquals(_active, this))
                _active = null;
        }
    }

    public sealed class GwpFireswornRitualPopupVM : ViewModel
    {
        private readonly Action _onClose;

        public GwpFireswornRitualPopupVM(string title, string text, Action onClose)
        {
            Title = title;
            Text = text;
            CloseText = GwpText.Get("{=gwp_fs_ritual_popup_close}Close");
            _onClose = onClose;
        }

        [DataSourceProperty]
        public string Title { get; }

        [DataSourceProperty]
        public string Text { get; }

        [DataSourceProperty]
        public string CloseText { get; }

        public void ExecuteClose() => _onClose();
    }
}
