using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 把玩家的灰袍声望挂进原版右下角信息条，紧挨原版影响力。
    ///
    /// 这里不改任何 GUI 预制件。原版 <c>MapBar.xml</c> 的两排信息完全是数据驱动的：
    /// <c>PrimaryInfoItems</c> / <c>SecondaryInfoItems</c> 各自按 ItemTemplate 渲染，
    /// 图标取 <c>IconBrush="MapBar.Right.Icons"</c> 里名为 <c>@VisualId</c> 的那一层。
    /// 所以只要往列表里加一个 <see cref="MapInfoItemVM"/>、往那个笔刷里加一层图标，
    /// 原版自己就会把它画出来，位置、字体、悬浮提示全部沿用原版。
    ///
    /// 图标是原版影响力图标的副本，只换颜色：<c>IconBrushWidget.UpdateIcon</c> 在
    /// <c>UseStylesFromSourceIcon</c> 为真时执行 <c>layer.FillFrom(sourceLayer)</c>，
    /// 而 <c>BrushLayer.FillFrom</c> 连 <c>Color</c> 一起复制，因此改这一层的颜色就够了。
    /// </summary>
    internal static class GwpMapBarReputation
    {
        private const string IconBrushName = "MapBar.Right.Icons";
        private const string SourceLayerName = "influence";
        internal const string ReputationVisualId = "gwp_warden_reputation";

        // 灰袍的冷银蓝。金币图标本身偏暖黄，换成冷色才不会在一排里混成同一个东西。
        private static readonly Color ReputationIconColor = new Color(0.50f, 0.68f, 0.82f);

        private static readonly Dictionary<MapInfoVM, MapInfoItemVM> Items =
            new Dictionary<MapInfoVM, MapInfoItemVM>();

        /// <summary>
        /// 往原版图标笔刷里补一层染过色的影响力图标。幂等：已经有了就不再加。
        /// 只新增一层，不改动原版那 12 层里的任何一层，也不整体覆盖笔刷文件，
        /// 所以游戏更新改了别的图标也不受影响。
        /// </summary>
        private static bool EnsureIconLayer()
        {
            try
            {
                Brush? brush = UIResourceManager.BrushFactory?.GetBrush(IconBrushName);
                if (brush == null) return false;
                if (brush.GetLayer(ReputationVisualId) != null) return true;

                BrushLayer? source = brush.GetLayer(SourceLayerName);
                if (source == null) return false;

                var layer = new BrushLayer();
                layer.FillFrom(source);
                // FillFrom 连 Name 一起复制，改名必须放在它后面。
                layer.Name = ReputationVisualId;
                layer.Color = ReputationIconColor;
                brush.AddLayer(layer);
                return true;
            }
            catch (Exception exception)
            {
                GwpFaultTrace.Write("MAPBAR_REPUTATION_ICON_FAILED",
                    details: exception.GetType().FullName + ":" + exception.Message);
                return false;
            }
        }

        private static List<TooltipProperty> BuildTooltip()
        {
            int reputation = PlayerBehaviorPool.Reputation;
            var properties = new List<TooltipProperty>
            {
                new TooltipProperty(
                    GwpText.Get("{=gwp_mapbar_reputation_title}Grey Warden Standing"),
                    reputation.ToString(), 0, false, TooltipProperty.TooltipPropertyFlags.Title),
                new TooltipProperty(string.Empty,
                    GwpText.Get("{=gwp_mapbar_reputation_hint}How the Grey Wardens judge you. Crimes lower it, good deeds raise it."),
                    0, false, TooltipProperty.TooltipPropertyFlags.MultiLine)
            };

            if (PlayerBehaviorPool.IsWanted)
            {
                properties.Add(new TooltipProperty(string.Empty,
                    GwpText.Get("{=gwp_mapbar_reputation_wanted}You are wanted. Grey Warden lords will open a case against you."),
                    0, false, TooltipProperty.TooltipPropertyFlags.MultiLine));
            }

            return properties;
        }

        internal static void AttachItem(MapInfoVM vm)
        {
            if (vm?.SecondaryInfoItems == null) return;
            if (!EnsureIconLayer()) return;

            // CreateItems 每次都会清空并重建两排，所以这里始终新建一个跟着走。
            var item = new MapInfoItemVM(ReputationVisualId, BuildTooltip);
            Items[vm] = item;
            // 紧跟原版影响力。代价要记清楚：影响力在 SecondaryInfoItems，那一排
            // 只在信息条展开（IsInfoBarExtended）时才显示；第纳尔所在的
            // PrimaryInfoItems 才是常驻的。按用户要求放在影响力旁边。
            vm.SecondaryInfoItems.Insert(
                Math.Min(1, vm.SecondaryInfoItems.Count), item);
            UpdateItem(vm, true);
        }

        internal static void UpdateItem(MapInfoVM vm, bool forced)
        {
            if (vm == null || !Items.TryGetValue(vm, out MapInfoItemVM? item) || item == null)
                return;

            int reputation = PlayerBehaviorPool.Reputation;
            // 被通缉才标红，与既有的"通缉已解除"口径一致；只是负分还不至于报警。
            item.HasWarning = PlayerBehaviorPool.IsWanted;
            if (item.IntValue == reputation && !forced) return;

            item.IntValue = reputation;
            item.Value = reputation.ToString();
        }
    }

    [HarmonyPatch(typeof(MapInfoVM), "CreateItems")]
    internal static class GwpMapInfoCreateItemsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MapInfoVM __instance) =>
            GwpMapBarReputation.AttachItem(__instance);
    }

    [HarmonyPatch(typeof(MapInfoVM), "UpdatePlayerInfo")]
    internal static class GwpMapInfoUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(MapInfoVM __instance, bool updateForced) =>
            GwpMapBarReputation.UpdateItem(__instance, updateForced);
    }
}
