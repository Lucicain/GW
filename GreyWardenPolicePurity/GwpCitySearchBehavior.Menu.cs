using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchBehavior
    {
        private const string SearchMenu = "gwp_city_search";

        private void RegisterSearchMenu(CampaignGameStarter starter)
        {
            starter.AddGameMenu(SearchMenu, "{GWP_CS_MENU_TEXT}", InitializeSearchMenu);
            starter.AddGameMenuOption(SearchMenu, "begin", GwpTextCatalog.gwp_cs_menu_begin,
                args => MenuRoute(args, !SearchActive), SelectSearchTroops);
            starter.AddGameMenuOption(SearchMenu, "resume", GwpTextCatalog.gwp_cs_menu_resume,
                args => MenuRoute(args, SearchHere && SearchPlace != GwpCitySearchPlace.Arena),
                _ => QueuePlace(SearchPlace));
            starter.AddGameMenuOption(SearchMenu, "hall", GwpTextCatalog.gwp_cs_menu_hall,
                args => MenuRoute(args, SearchHere && HallAccessible && !HallResolved && SearchPlace != GwpCitySearchPlace.Hall),
                _ => QueuePlace(GwpCitySearchPlace.Hall));
            starter.AddGameMenuOption(SearchMenu, "street", GwpTextCatalog.gwp_cs_menu_street,
                args => MenuRoute(args, SearchHere && SearchPlace == GwpCitySearchPlace.Hall),
                _ => QueuePlace(Returning ? GwpCitySearchPlace.Escape : GwpCitySearchPlace.Street));
            starter.AddGameMenuOption(SearchMenu, "arena", GwpTextCatalog.gwp_cs_menu_arena,
                args => MenuRoute(args, SearchHere && DuelAgreed && Settlement.CurrentSettlement.IsTown),
                _ => QueuePlace(GwpCitySearchPlace.Arena));
            starter.AddGameMenuOption(SearchMenu, "leave", GwpTextCatalog.gwp_cs_menu_leave,
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return SearchHere && !SearchWar; },
                _ => FinishSearch(true), isLeave: true);
            starter.AddGameMenuOption(SearchMenu, "back", GwpTextCatalog.gwp_cs_menu_back,
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Submenu; return !SearchHere || !SearchWar; },
                _ => GameMenu.SwitchToMenu(Settlement.CurrentSettlement.IsCastle ? "castle" : "town"));
        }

        private bool MenuRoute(MenuCallbackArgs args, bool visible)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            args.IsEnabled = SearchHere || !Hero.MainHero.IsWounded;
            if (!args.IsEnabled) args.Tooltip = GwpText.Create(GwpTextCatalog.gwp_city_search_wounded);
            return visible;
        }

        private void InitializeSearchMenu(MenuCallbackArgs args)
        {
            // 这里不触发下一场景。原版 Tab 回到原菜单，玩家自己决定继续步行还是选地点。
            Hero? target = SearchHere ? SearchTarget : Bounty?.CitySearchTargetIn(Settlement.CurrentSettlement);
            if (target == null)
            {
                GameMenu.SwitchToMenu(Settlement.CurrentSettlement.IsCastle ? "castle" : "town");
                return;
            }
            string state = !SearchHere ? GwpTextCatalog.gwp_cs_menu_new
                : Returning ? SearchWar ? GwpTextCatalog.gwp_cs_menu_fight_out : GwpTextCatalog.gwp_cs_menu_return
                : DuelAgreed ? Settlement.CurrentSettlement.IsCastle ? GwpTextCatalog.gwp_cs_menu_street : GwpTextCatalog.gwp_cs_menu_duel
                : HallAccessible ? GwpTextCatalog.gwp_cs_menu_allowed : GwpTextCatalog.gwp_cs_menu_gate;
            MBTextManager.SetTextVariable("GWP_CS_MENU_TEXT", GwpText.Create(state, "TARGET", target.Name));
        }
    }
}
