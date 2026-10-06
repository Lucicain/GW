using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 火誓者在村庄菜单里给玩家看到的东西：已传教村子的狂信者数（像户数一样显示，用户裁定），
    /// 以及传教、烧人期间进村时只看到的仪式画面与音效（先只放图和音效，用户裁定）。
    /// </summary>
    internal static class GwpFireswornMenus
    {
        internal const string RitualMenuId = "gwp_firesworn_ritual";

        internal static void Register(CampaignGameStarter starter)
        {
            starter.AddGameMenu(RitualMenuId, "{GWP_FS_RITUAL_TEXT}", RitualInit);
            starter.AddGameMenuOption(RitualMenuId, "gwp_fs_ritual_leave", "{=gwp_fs_ritual_leave}Leave...",
                LeaveCondition, LeaveConsequence, isLeave: true);
            starter.AddGameMenuOption("village", "gwp_fs_village_faith", "{GWP_FS_VILLAGE_OPTION}",
                FaithOptionCondition, FaithOptionConsequence, isLeave: false, index: 5);
            GwpFireswornArmoryView.Register(starter);
        }

        private static void RitualInit(MenuCallbackArgs args)
        {
            Settlement? village = Settlement.CurrentSettlement;
            if (!GwpFireswornBehavior.TryGetActiveRitual(village, out GwpFireswornBehavior.RitualKind kind,
                    out double remaining, out string host))
            {
                GameMenu.SwitchToMenu("village");
                return;
            }

            args.MenuContext.SetBackgroundMeshName(GwpFireswornArt.Background(kind));
            args.MenuTitle = kind == GwpFireswornBehavior.RitualKind.Damnation
                ? GwpText.Create("{=gwp_fs_ritual_damnation_title}Damnation")
                : GwpText.Create("{=gwp_fs_ritual_congregation_title}Congregation");
            MBTextManager.SetTextVariable("GWP_FS_RITUAL_TEXT", RitualText(kind, village!, host, remaining));
            GwpAiDiagnostics.WriteFaith("RITUAL_MENU_SHOWN", "village=" + village!.StringId + "; kind=" + kind +
                "; background=" + GwpFireswornArt.Background(kind));
        }

        private static string RitualText(GwpFireswornBehavior.RitualKind kind, Settlement village, string host,
            double remainingHours)
        {
            int hours = Math.Max(1, (int)Math.Ceiling(remainingHours));
            return kind == GwpFireswornBehavior.RitualKind.Damnation
                ? GwpText.Get("{=gwp_fs_ritual_damnation_text}A pyre is burning in the square of {VILLAGE}. {HOST} is holding a Damnation of the Eternal Fire: those bound to it are the ones the flame has judged. The sworn women sing around the pyre while the villagers feed it wood. The smoke stings your eyes. Nobody here will deal with you for about {HOURS} more hours.",
                    "VILLAGE", village.Name, "HOST", host, "HOURS", hours)
                : GwpText.Get("{=gwp_fs_ritual_congregation_text}The square of {VILLAGE} is packed. {HOST} stands before the Eternal Fire reading out the laws of the flame, and the women who would swear step forward one by one and hold their hands over the fire to take the oath; the villagers around them speak the words with her. A Congregation is under way, and nobody here has time for you. It will last about {HOURS} more hours.",
                    "VILLAGE", village.Name, "HOST", host, "HOURS", hours);
        }

        private static bool LeaveCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private static void LeaveConsequence(MenuCallbackArgs args)
        {
            (args.MenuContext.Handler as SandBox.View.Menu.MenuViewContext)?
                .GetMenuView<GwpFireswornSoundView>()?.EndVisit();
            PlayerEncounter.LeaveSettlement();
            PlayerEncounter.Finish();
        }

        private static bool FaithOptionCondition(MenuCallbackArgs args)
        {
            Settlement? village = Settlement.CurrentSettlement;
            if (!GwpFireswornBehavior.TryGetVillageFaith(village, out int zealots, out _, out _))
                return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            MBTextManager.SetTextVariable("GWP_FS_VILLAGE_OPTION", GwpText.Get(
                "{=gwp_fs_village_option}Eternal Fire: {ZEALOTS} zealots ({SHARE}% of hearths)",
                "ZEALOTS", zealots, "SHARE", HearthShare(village!, zealots)));
            return true;
        }

        private static void FaithOptionConsequence(MenuCallbackArgs args)
        {
            Settlement? village = Settlement.CurrentSettlement;
            if (!GwpFireswornBehavior.TryGetVillageFaith(village, out int zealots, out _, out Hero? priest))
                return;

            string body = GwpText.Get(
                "{=gwp_fs_village_info}{ZEALOTS} women of the {HEARTH} hearths of {VILLAGE} have sworn themselves to the Eternal Fire. Women tend the flame, and the priestess and the sworn sisters keep its laws; the rest of the village works its fields as it always has. When raiders come, it is these sworn women who take up the spear.",
                "ZEALOTS", zealots, "HEARTH", (int)(village!.Village?.Hearth ?? 0f), "VILLAGE", village.Name);
            body += "\n\n" + (priest != null
                ? GwpText.Get("{=gwp_fs_village_priest}{PRIEST} tends the Eternal Fire here. When the Wardens need fighters, the sworn daughters of this village go with them from her hearth.",
                    "PRIEST", priest.Name)
                : GwpText.Get("{=gwp_fs_village_no_priest}No priest of the Eternal Fire lives here."));
            GwpFireswornBehavior.GetVillageStores(village, out int prisoners, out int armoryItems, out int goodsValue);
            body += "\n\n" + GwpText.Get(
                "{=gwp_fs_village_stores}{PRISONERS} wretches wait in the cellar for the pyre. The storeroom holds {ARMORY} pieces of arms and armour for the sworn. Goods worth {GOODS} denars wait to be carted to market.",
                "PRISONERS", prisoners, "ARMORY", armoryItems, "GOODS", goodsValue);
            InformationManager.ShowInquiry(new InquiryData(
                GwpText.Get("{=gwp_fs_village_info_title}The Eternal Fire"), body,
                true, false, GwpText.Get("{=gwp_fs_village_info_ok}Close"), string.Empty, null, null));
        }

        private static int HearthShare(Settlement village, int zealots)
        {
            float hearth = village.Village?.Hearth ?? 0f;
            return hearth > 0f ? (int)Math.Round(zealots * 100f / hearth) : 0;
        }
    }

    /// <summary>
    /// 传教、烧人期间，村子像比武大会进行中或被洗劫过一样不能交互（用户裁定）。照原版
    /// 洗劫后的做法：打开村庄菜单时跳到另一张菜单，不改村子状态（改 VillageState 会影响生产、
    /// 民兵与户数；依据：C#反编译 v1.4.8 PlayerTownVisitCampaignBehavior.game_menu_village_on_init）。
    /// </summary>
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), "game_menu_village_on_init")]
    internal static class GwpFireswornVillageLockPatch
    {
        private static void Postfix()
        {
            if (GwpFireswornBehavior.TryGetActiveRitual(Settlement.CurrentSettlement, out _, out _, out _))
                GameMenu.SwitchToMenu(GwpFireswornMenus.RitualMenuId);
        }
    }

    /// <summary>
    /// 原版民兵减员按兵种比例从整支民兵队里扣（C#反编译 v1.4.8 Settlement.RemoveMilitiasFromParty），
    /// 有守村队的村子只扣原版民兵：灰袍兵只靠战斗、烧人、招兵、出门减少。
    /// </summary>
    [HarmonyPatch(typeof(Settlement), "RemoveMilitiasFromParty")]
    internal static class GwpFireswornGuardStaysPatch
    {
        private static bool Prefix(MobileParty militiaParty, int numberToRemove)
        {
            if (GwpFireswornBehavior.GuardHeadcount(militiaParty.HomeSettlement) == 0)
                return true;
            GwpFireswornBehavior.RemoveVanillaMilitia(militiaParty, numberToRemove);
            return false;
        }
    }

    /// <summary>
    /// 原版每天按概率删掉 power 低的要人（C#反编译 v1.4.8
    /// NotablesCampaignBehavior.CheckAndMakeNotableDisappear）；永恒之火祭司不走这条。
    /// </summary>
    [HarmonyPatch(typeof(NotablesCampaignBehavior), "CheckAndMakeNotableDisappear")]
    internal static class GwpFireswornPriestStaysPatch
    {
        private static bool Prefix(Hero notable) => !GwpFireswornBehavior.IsFireswornPriest(notable);
    }

    /// <summary>
    /// 守村队的灰袍兵和原版民兵站在同一支民兵队里，原版民兵照原版增长（用户裁定 2026-10-07：民兵不直接转化，
    /// 照原版增长、参战，是烧人的原料）。原版每日变化里的退役项按全队人数算（现有 × 2.5%，C#反编译 v1.4.8
    /// DefaultSettlementMilitiaModel.CalculateMilitiaChangeInternal），把灰袍兵那一份加回来，原版民兵的增减就和没有灰袍兵时一样。
    /// 包在已注册的模型外面，没有守村队的村镇照原版。
    /// </summary>
    public sealed class GwpFireswornMilitiaModel : SettlementMilitiaModel
    {
        // AddModel<T> 总会把已注册的模型设成 BaseModel（CampaignGameStarter.AddModel<T>）。
        private SettlementMilitiaModel Inner => BaseModel;

        public override int MilitiaToSpawnAfterSiege(Town town) => Inner.MilitiaToSpawnAfterSiege(town);

        public override ExplainedNumber CalculateMilitiaChange(Settlement settlement,
            bool includeDescriptions = false)
        {
            ExplainedNumber change = Inner.CalculateMilitiaChange(settlement, includeDescriptions);
            int guard = GwpFireswornBehavior.GuardHeadcount(settlement);
            // 原版村子不在正常状态时整项是 0，没有退役项可加回。
            if (guard > 0 && settlement.Village?.VillageState == Village.VillageStates.Normal)
                change.Add(guard * GwpTuning.Firesworn.VanillaMilitiaRetireShare,
                    GwpText.Create("{=gwp_fs_militia_change}The Eternal Fire's sworn do not leave the militia"));
            return change;
        }

        public override ExplainedNumber CalculateVeteranMilitiaSpawnChance(Settlement settlement) =>
            Inner.CalculateVeteranMilitiaSpawnChance(settlement);

        public override void CalculateMilitiaSpawnRate(Settlement settlement, out float meleeTroopRate,
            out float rangedTroopRate) =>
            Inner.CalculateMilitiaSpawnRate(settlement, out meleeTroopRate, out rangedTroopRate);
    }
}
