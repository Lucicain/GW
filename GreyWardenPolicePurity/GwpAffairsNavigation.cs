using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using SandBox.View.Map.Navigation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.TwoDimension;
using EngineTextureObject = TaleWorlds.Engine.Texture;
using UiTexture = TaleWorlds.TwoDimension.Texture;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 地图左下角导航栏的「灰袍事务」按钮，排在家族之后、王国之前。
    /// 原版按钮由 <see cref="MapNavigationHandler"/> 的 OnCreateElements 列出，
    /// NavalDLC 的舰队按钮也是这样插进去的。未入会时按钮灰显并给出原因，
    /// 与原版王国按钮在没有王国时的做法一致；百科家族页的入口不设门槛。
    /// </summary>
    internal sealed class GwpAffairsNavigationElement : MapNavigationElementBase
    {
        internal const string Id = "gwp_affairs";

        public GwpAffairsNavigationElement(MapNavigationHandler handler)
            : base(handler)
        {
        }

        public override string StringId => Id;

        public override bool IsActive => GwpCaseArchiveScreen.IsOpen;

        public override bool IsLockingNavigation => false;

        public override bool HasAlert => false;

        protected override NavigationPermissionItem GetPermission()
        {
            if (!MapNavigationHelper.IsNavigationBarEnabled(_handler) || IsActive)
                return new NavigationPermissionItem(false, null);
            if (Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
                    ?.IsRecruitedByGreyWardens != true)
                return new NavigationPermissionItem(false, GwpText.Create(
                    "{=gwp_affairs_nav_need_membership}Only sworn hunters of the Grey Wardens can open their affairs."));
            return new NavigationPermissionItem(true, null);
        }

        protected override TextObject GetTooltip() =>
            GwpText.Create("{=gwp_gwpencyclopediaclanpagevm_004}Grey Warden affairs");

        protected override TextObject GetAlertTooltip() => TextObject.GetEmpty();

        public override void OpenView()
        {
            if (Permission.IsAuthorized)
                MapNavigationHelper.SwitchToANewScreen(GwpCaseArchiveScreen.Show);
        }

        public override void OpenView(params object[] parameters) => OpenView();

        public override void GoToLink()
        {
            Clan? police = PoliceStats.GetPoliceClan();
            if (police != null)
                Campaign.Current?.EncyclopediaManager.GoToLink(police.EncyclopediaLink);
        }
    }

    [HarmonyPatch(typeof(MapNavigationHandler), "OnCreateElements")]
    internal static class GwpAffairsNavigationPatch
    {
        private const string ClanElementId = "clan";

        [HarmonyPostfix]
        private static void InsertAfterClan(MapNavigationHandler __instance,
            ref INavigationElement[] __result)
        {
            try
            {
                if (__result == null ||
                    __result.Any(element => element?.StringId == GwpAffairsNavigationElement.Id))
                    return;

                GwpAffairsIcon.EnsureBrushLayers();
                var elements = __result.ToList();
                int clan = elements.FindIndex(element => element?.StringId == ClanElementId);
                elements.Insert(clan >= 0 ? clan + 1 : elements.Count,
                    new GwpAffairsNavigationElement(__instance));
                __result = elements.ToArray();
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }
    }

    /// <summary>
    /// 按钮由两层组成：底板取自 <c>MapBar.Left.Button.Backgrounds</c>，图标取自
    /// <c>MapBar.Left.Icons</c>，都按按钮 id 找同名图层。这里照原版家族那两层复制出
    /// 灰袍的图层：底板沿用家族的石板，图标换成 <c>GUI/Icons/gwp_affairs.png</c>。
    /// 图标在运行时读入（原版头像缩略图同样用 <see cref="EngineTextureObject.CreateFromMemory"/>
    /// 建图），不经 Modding Kit 打精灵表。
    /// </summary>
    internal static class GwpAffairsIcon
    {
        private const string IconsBrushName = "MapBar.Left.Icons";
        private const string BackgroundsBrushName = "MapBar.Left.Button.Backgrounds";
        private const string TemplateLayerName = "clan";
        private const string IconFileName = "gwp_affairs.png";

        // 引擎贴图由这里持有，整局只读一次。
        private static Sprite? _iconSprite;

        internal static void EnsureBrushLayers()
        {
            BrushFactory? factory = UIResourceManager.BrushFactory;
            AddLayerFromTemplate(factory?.GetBrush(BackgroundsBrushName), null);

            Sprite? icon = _iconSprite ??= LoadIconSprite();
            if (icon != null)
                AddLayerFromTemplate(factory?.GetBrush(IconsBrushName), icon);
        }

        private static void AddLayerFromTemplate(Brush? brush, Sprite? sprite)
        {
            if (brush == null || brush.GetLayer(GwpAffairsNavigationElement.Id) != null)
                return;
            BrushLayer? template = brush.GetLayer(TemplateLayerName);
            if (template == null)
                return;

            var layer = new BrushLayer();
            layer.FillFrom(template);
            layer.Name = GwpAffairsNavigationElement.Id;
            if (sprite != null)
                layer.Sprite = sprite;
            brush.AddLayer(layer);
        }

        private static Sprite? LoadIconSprite()
        {
            string module = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(typeof(SubModule).Assembly.Location)!, "../.."));
            string path = Path.Combine(module, "GUI", "Icons", IconFileName);
            if (!File.Exists(path))
            {
                GwpFaultTrace.Write("AFFAIRS_ICON_MISSING", details: path);
                return null;
            }

            byte[] png = File.ReadAllBytes(path);
            if (!TryReadPngSize(png, out int width, out int height))
            {
                GwpFaultTrace.Write("AFFAIRS_ICON_NOT_PNG", details: path);
                return null;
            }
            EngineTextureObject texture = EngineTextureObject.CreateFromMemory(png);
            if (texture == null)
            {
                GwpFaultTrace.Write("AFFAIRS_ICON_DECODE_FAILED", details: path);
                return null;
            }
            texture.Name = "gwp_affairs_icon";
            // 尺寸取自文件头：引擎贴图可能还没载完，Contain 缩放要靠精灵的宽高定比例。
            return new GwpTextureSprite("gwp_affairs_icon",
                new UiTexture(new EngineTexture(texture)), width, height);
        }

        private static bool TryReadPngSize(byte[] png, out int width, out int height)
        {
            width = height = 0;
            // 8 字节签名之后第一个块是 IHDR，宽高为大端 32 位整数。
            if (png.Length < 24 || png[0] != 0x89 || png[1] != (byte)'P' ||
                png[12] != (byte)'I' || png[13] != (byte)'H' || png[14] != (byte)'D' || png[15] != (byte)'R')
                return false;
            width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
            return width > 0 && height > 0;
        }
    }

    /// <summary>整张贴图作为一个精灵。原版只提供图集分片（SpriteGeneric），界面绘制只依赖抽象的 Sprite。</summary>
    internal sealed class GwpTextureSprite : Sprite
    {
        private readonly UiTexture _texture;

        internal GwpTextureSprite(string name, UiTexture texture, int width, int height)
            : base(name, width, height, SpriteNinePatchParameters.Empty)
        {
            _texture = texture;
        }

        public override UiTexture Texture => _texture;

        public override Vec2 GetMinUvs() => Vec2.Zero;

        public override Vec2 GetMaxUvs() => new Vec2(1f, 1f);
    }
}
