using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 在大地图上遇到信众队时的对话，照原版村民队（C#反编译 v1.4.8 VillagerCampaignBehavior
    /// .AddVillageFarmerTradeAndLootDialogs）：打招呼说明来处和去向，卖货队可以买它车上的货，可以威逼，可以放行。
    /// 原版对话只认村民队，这种队以前落到原版兜底那一句「我不能和你说话」
    /// （default_conversation_for_wrongly_created_heroes；用户反馈 2026-10-07）。
    /// 狂信者不向威逼低头，没有原版村民的交钱、投降两条路；威逼之后回到遭遇菜单，玩家自己决定打不打。
    /// </summary>
    internal static class GwpFireswornPartyDialogs
    {
        internal static void Register(CampaignGameStarter starter)
        {
            starter.AddDialogLine("gwp_fs_party_greeting", "start", "gwp_fs_party_talk",
                "{=!}{GWP_FS_PARTY_GREETING}", GreetingCondition, null, 200);
            starter.AddDialogLine("gwp_fs_party_pretalk", "gwp_fs_party_pretalk", "gwp_fs_party_talk",
                GwpText.Get(GwpTextCatalog.gwp_fs_party_pretalk), null, null);

            starter.AddPlayerLine("gwp_fs_party_buy", "gwp_fs_party_talk", "gwp_fs_party_offer",
                GwpText.Get(GwpTextCatalog.gwp_fs_party_buy), BuyCondition, null);
            starter.AddDialogLine("gwp_fs_party_offer", "gwp_fs_party_offer", "gwp_fs_party_buy_decision",
                GwpText.Get(GwpTextCatalog.gwp_fs_party_offer),
                null, null);
            starter.AddPlayerLine("gwp_fs_party_buy_yes", "gwp_fs_party_buy_decision", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_fs_party_buy_yes), null, Buy);
            starter.AddPlayerLine("gwp_fs_party_buy_no", "gwp_fs_party_buy_decision", "gwp_fs_party_pretalk",
                GwpText.Get(GwpTextCatalog.gwp_fs_party_buy_no), null, null);

            starter.AddPlayerLine("gwp_fs_party_threaten", "gwp_fs_party_talk", "gwp_fs_party_refuse",
                GwpText.Get(GwpTextCatalog.gwp_fs_party_threaten), null, null);
            starter.AddDialogLine("gwp_fs_party_refuse", "gwp_fs_party_refuse", "gwp_fs_party_fight_decision",
                GwpText.Get(GwpTextCatalog.gwp_fs_party_refuse),
                null, null);
            starter.AddPlayerLine("gwp_fs_party_fight", "gwp_fs_party_fight_decision", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_fs_party_fight), null, null);
            starter.AddPlayerLine("gwp_fs_party_back_off", "gwp_fs_party_fight_decision", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_fs_party_back_off), null, Leave);

            starter.AddPlayerLine("gwp_fs_party_leave", "gwp_fs_party_talk", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_fs_party_leave), null, Leave);
        }

        private static MobileParty? Encountered() =>
            PlayerEncounter.Current != null &&
            Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter
                ? PlayerEncounter.EncounteredParty?.MobileParty
                : null;

        private static bool GreetingCondition()
        {
            MobileParty? party = Encountered();
            if (!GwpFireswornBehavior.TryDescribeFaithParty(party, out GwpFireswornBehavior.FaithPartyKind kind, out bool outbound,
                    out Settlement? home, out Settlement? target))
                return false;
            TextObject greeting = kind == GwpFireswornBehavior.FaithPartyKind.Training
                ? GwpText.Create(GwpTextCatalog.gwp_training_transport_greeting)
                : kind == GwpFireswornBehavior.FaithPartyKind.Mission
                ? GwpText.Create(GwpTextCatalog.gwp_fs_party_greet_mission,
                    "HOME", home!.Name, "TARGET", target!.Name)
                : kind == GwpFireswornBehavior.FaithPartyKind.Balance
                ? GwpText.Create(GwpTextCatalog.gwp_fs_party_greet_balance,
                    "HOME", home!.Name, "TARGET", target!.Name)
                : outbound
                    ? GwpText.Create(GwpTextCatalog.gwp_fs_party_greet_cart_out,
                        "HOME", home!.Name, "TARGET", target!.Name)
                    : GwpText.Create(GwpTextCatalog.gwp_fs_party_greet_cart_back,
                        "HOME", home!.Name);
            MBTextManager.SetTextVariable("GWP_FS_PARTY_GREETING", greeting);
            return true;
        }

        private static int Price(MobileParty party, EquipmentElement element)
        {
            Settlement market = party.HomeSettlement.Village.TradeBound;
            return market.Town.GetItemPrice(element, MobileParty.MainParty, isSelling: false);
        }

        private static IEnumerable<ItemRosterElement> Goods(MobileParty party) =>
            Enumerable.Range(0, party.ItemRoster.Count)
                .Select(party.ItemRoster.GetElementCopyAtIndex)
                .Where(element => !element.EquipmentElement.Item.IsFood && element.Amount > 0);

        private static bool BuyCondition()
        {
            MobileParty? party = Encountered();
            if (!GwpFireswornBehavior.TryDescribeFaithParty(party, out GwpFireswornBehavior.FaithPartyKind kind, out _, out _, out _) ||
                kind != GwpFireswornBehavior.FaithPartyKind.Market)
                return false;
            List<ItemRosterElement> goods = Goods(party!).ToList();
            int total = goods.Sum(element => Price(party!, element.EquipmentElement) * element.Amount);
            if (goods.Count == 0 || total <= 0 || Hero.MainHero.Gold < total)
                return false;
            string products = string.Join(GwpText.Get(GwpTextCatalog.gwp_fs_party_products_separator), goods
                .OrderByDescending(element => element.Amount * element.EquipmentElement.ItemValue)
                .Take(4)
                .Select(element => GwpText.Get(GwpTextCatalog.gwp_fs_party_product,
                    "COUNT", element.Amount, "ITEM", element.EquipmentElement.GetModifiedItemName())));
            if (goods.Count > 4)
                products += GwpText.Get(GwpTextCatalog.gwp_fs_party_products_more);
            MBTextManager.SetTextVariable("PRODUCTS", products);
            MBTextManager.SetTextVariable("TOTAL_PRICE", total);
            return true;
        }

        /// <summary>钱进队伍身上，回村后进灰袍公库，和在城里卖货一样（GwpFireswornParties.ArriveHome）。</summary>
        private static void Buy()
        {
            MobileParty party = Encountered()!;
            foreach (ItemRosterElement element in Goods(party).ToList())
            {
                int cost = Price(party, element.EquipmentElement) * element.Amount;
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost);
                party.PartyTradeGold += cost;
                PartyBase.MainParty.ItemRoster.AddToCounts(element.EquipmentElement, element.Amount);
                party.ItemRoster.AddToCounts(element.EquipmentElement, -element.Amount);
            }
            Leave();
        }

        private static void Leave() => PlayerEncounter.LeaveEncounter = true;
    }
}
