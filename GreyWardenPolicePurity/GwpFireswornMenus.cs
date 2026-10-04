using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
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
        private const string CongregationSound = "gwp/firesworn/congregation";
        private const string DamnationSound = "gwp/firesworn/damnation";

        internal static void Register(CampaignGameStarter starter)
        {
            starter.AddGameMenu(RitualMenuId, "{GWP_FS_RITUAL_TEXT}", RitualInit);
            starter.AddGameMenuOption(RitualMenuId, "gwp_fs_ritual_leave", "{=gwp_fs_ritual_leave}Leave...",
                LeaveCondition, LeaveConsequence, isLeave: true);
            starter.AddGameMenuOption("village", "gwp_fs_village_faith", "{GWP_FS_VILLAGE_OPTION}",
                FaithOptionCondition, FaithOptionConsequence, isLeave: false, index: 5);
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

            bool damnation = kind == GwpFireswornBehavior.RitualKind.Damnation;
            args.MenuContext.SetBackgroundMeshName(damnation ? "wait_raiding_village" : "encounter_peasant");
            args.MenuTitle = damnation
                ? GwpText.Create("{=gwp_fs_ritual_damnation_title}Damnation")
                : GwpText.Create("{=gwp_fs_ritual_congregation_title}Congregation");
            int hours = Math.Max(1, (int)Math.Ceiling(remaining));
            MBTextManager.SetTextVariable("GWP_FS_RITUAL_TEXT", damnation
                ? GwpText.Get("{=gwp_fs_ritual_damnation_text}A pyre is burning in the square of {VILLAGE}. {HOST} is holding a Damnation of the Eternal Fire: those dragged onto it are the ones who would not bow. The women sing around the flames while the men, heads down, feed it wood. The smoke stings your eyes. Nobody here will deal with you for about {HOURS} more hours.",
                    "VILLAGE", village!.Name, "HOST", host, "HOURS", hours)
                : GwpText.Get("{=gwp_fs_ritual_congregation_text}The women of {VILLAGE} crowd the square around {HOST}, swearing themselves to the Eternal Fire one after another. The men have been pushed to the edge of the crowd, where they kneel and listen to the laws of the flame read out over them. A Congregation is under way, and nobody here has time for you. It will last about {HOURS} more hours.",
                    "VILLAGE", village!.Name, "HOST", host, "HOURS", hours));
            PlaySound(damnation ? DamnationSound : CongregationSound);
        }

        private static bool LeaveCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private static void LeaveConsequence(MenuCallbackArgs args)
        {
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
            if (!GwpFireswornBehavior.TryGetVillageFaith(village, out int zealots, out int cap,
                    out Hero? priest))
                return;

            string body = GwpText.Get(
                "{=gwp_fs_village_info}{ZEALOTS} women of the {HEARTH} hearths of {VILLAGE} have sworn themselves to the Eternal Fire ({SHARE}%; at most {CAP} can). The flame passes only from mother to daughter; the men of the village serve it and do not touch it. The village militia are these zealots: they fight raiders, and the dead are not replaced unless the faith wins new believers.",
                "ZEALOTS", zealots, "HEARTH", (int)(village!.Village?.Hearth ?? 0f), "VILLAGE", village.Name,
                "SHARE", HearthShare(village, zealots), "CAP", cap);
            body += "\n\n" + (priest != null
                ? GwpText.Get("{=gwp_fs_village_priest}{PRIEST} tends the Eternal Fire here. Wardens can recruit zealots from the priest.",
                    "PRIEST", priest.Name)
                : GwpText.Get("{=gwp_fs_village_no_priest}No priest of the Eternal Fire lives here."));
            InformationManager.ShowInquiry(new InquiryData(
                GwpText.Get("{=gwp_fs_village_info_title}The Eternal Fire"), body,
                true, false, GwpText.Get("{=gwp_fs_village_info_ok}Close"), string.Empty, null, null));
        }

        private static int HearthShare(Settlement village, int zealots)
        {
            float hearth = village.Village?.Hearth ?? 0f;
            return hearth > 0f ? (int)Math.Round(zealots * 100f / hearth) : 0;
        }

        private static void PlaySound(string eventName)
        {
            int id = SoundEvent.GetEventIdFromString(eventName);
            if (id >= 0)
                SoundEvent.PlaySound2D(id);
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
    /// 原版每天按概率删掉 power 低的要人（C#反编译 v1.4.8
    /// NotablesCampaignBehavior.CheckAndMakeNotableDisappear）；永恒之火祭司不走这条。
    /// </summary>
    [HarmonyPatch(typeof(NotablesCampaignBehavior), "CheckAndMakeNotableDisappear")]
    internal static class GwpFireswornPriestStaysPatch
    {
        private static bool Prefix(Hero notable) => !GwpFireswornBehavior.IsFireswornPriest(notable);
    }

    /// <summary>
    /// 已传教村子的民兵就是狂信者：原版每日增减归零，人数只随传教、烧人、门徒与战斗变化
    /// （用户裁定）。包在已注册的模型外面，其余村镇照原版。
    /// </summary>
    public sealed class GwpFireswornMilitiaModel : SettlementMilitiaModel
    {
        private SettlementMilitiaModel? _fallback;

        private SettlementMilitiaModel Inner =>
            BaseModel ?? (_fallback ??= new DefaultSettlementMilitiaModel());

        public override int MilitiaToSpawnAfterSiege(Town town) => Inner.MilitiaToSpawnAfterSiege(town);

        public override ExplainedNumber CalculateMilitiaChange(Settlement settlement,
            bool includeDescriptions = false) =>
            GwpFireswornBehavior.IsPreachedVillage(settlement)
                ? new ExplainedNumber(0f, includeDescriptions,
                    GwpText.Create("{=gwp_fs_militia_change}Eternal Fire: the militia are the village's zealots"))
                : Inner.CalculateMilitiaChange(settlement, includeDescriptions);

        public override ExplainedNumber CalculateVeteranMilitiaSpawnChance(Settlement settlement) =>
            Inner.CalculateVeteranMilitiaSpawnChance(settlement);

        public override void CalculateMilitiaSpawnRate(Settlement settlement, out float meleeTroopRate,
            out float rangedTroopRate) =>
            Inner.CalculateMilitiaSpawnRate(settlement, out meleeTroopRate, out rangedTroopRate);
    }

    /// <summary>
    /// 永恒之火祭司只产火誓狂热者（用户裁定），也只让灰袍招：灰袍领主与入会的玩家。
    /// 其余要人照原版。
    /// </summary>
    public sealed class GwpFireswornVolunteerModel : VolunteerModel
    {
        private VolunteerModel? _fallback;

        private VolunteerModel Inner => BaseModel ?? (_fallback ??= new DefaultVolunteerModel());

        public override int MaxVolunteerTier => Inner.MaxVolunteerTier;

        public override int MaximumIndexHeroCanRecruitFromHero(Hero buyerHero, Hero sellerHero,
            int useValueAsRelation = -101)
        {
            if (GwpFireswornBehavior.IsFireswornPriest(sellerHero) && !MayRecruitZealots(buyerHero))
                return 0;
            return Inner.MaximumIndexHeroCanRecruitFromHero(buyerHero, sellerHero, useValueAsRelation);
        }

        public override int MaximumIndexGarrisonCanRecruitFromHero(Settlement settlement, Hero sellerHero) =>
            GwpFireswornBehavior.IsFireswornPriest(sellerHero)
                ? 0
                : Inner.MaximumIndexGarrisonCanRecruitFromHero(settlement, sellerHero);

        public override float GetDailyVolunteerProductionProbability(Hero hero, int index,
            Settlement settlement) =>
            Inner.GetDailyVolunteerProductionProbability(hero, index, settlement);

        public override CharacterObject GetBasicVolunteer(Hero hero)
        {
            if (GwpFireswornBehavior.IsFireswornPriest(hero) &&
                CharacterObject.Find(GwpIds.ZealotId) is CharacterObject zealot)
                return zealot;
            return Inner.GetBasicVolunteer(hero);
        }

        public override bool CanHaveRecruits(Hero hero) => Inner.CanHaveRecruits(hero);

        private static bool MayRecruitZealots(Hero? buyer) =>
            GwpCommon.IsGreyWardenClanMember(buyer) ||
            (buyer == Hero.MainHero &&
             Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()?.IsRecruitedByGreyWardens == true);
    }
}
