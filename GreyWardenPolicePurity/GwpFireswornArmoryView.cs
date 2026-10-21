#if GWP_DIAGNOSTICS
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 看库房里有什么，只在开发版里有，玩家版没有（用户 2026-10-08：这是调试功能）。
    /// 信众村在本村菜单里看；灰袍领主的找这位领主说话时看，不要求入会。
    /// 各看各的（用户裁定 2026-10-07）；村庄列军械与货仓，领主只列军械，不列背包（用户裁定 2026-10-19）。只看，不能拿。
    /// </summary>
    internal static class GwpFireswornArmoryView
    {
        internal static void Register(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("village", "gwp_fs_village_armory",
                GwpTextCatalog.gwp_fs_armory_option,
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return GwpFireswornBehavior.HasFireswornGuard(Settlement.CurrentSettlement);
                },
                _ => ShowContents(Settlement.CurrentSettlement.StringId), isLeave: false, index: 6);

            // 调试用，任何灰袍领主都能直接问，不用入会、不用是带队的那位（用户 2026-10-08）。
            starter.AddPlayerLine("gwp_fs_lord_armory_ask", "lord_talk_speak_diplomacy_2", "gwp_fs_lord_armory_reply",
                GwpText.Get(GwpTextCatalog.gwp_fs_lord_armory_ask),
                () => GwpCommon.IsOrdinaryGreyWardenLordConversation() &&
                      Hero.OneToOneConversationHero.PartyBelongedTo != null,
                null, 215);
            starter.AddDialogLine("gwp_fs_lord_armory_reply", "gwp_fs_lord_armory_reply", "lord_pretalk",
                GwpText.Get(GwpTextCatalog.gwp_fs_lord_armory_reply),
                null, () => ShowContents(GwpFireswornBehavior.LordArmoryId(Hero.OneToOneConversationHero)), 215);
        }

        private static void ShowContents(string holder)
        {
            List<InquiryElement> items = new List<InquiryElement>();
            AddSection(items, GwpText.Get(GwpTextCatalog.gwp_fs_armory_section_arms),
                GwpFireswornBehavior.ArmoryLedger(holder));
            if (Settlement.Find(holder) != null)
                AddSection(items, GwpText.Get(GwpTextCatalog.gwp_fs_armory_section_goods),
                    GwpFireswornBehavior.VillageGoodsLedger(holder));
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                GwpFireswornBehavior.ArmoryHolderTitle(holder),
                GwpFireswornBehavior.ArmorySummary(holder),
                items, true, 0, 0,
                GwpText.Get(GwpTextCatalog.gwp_fs_armory_close), string.Empty, _ => { }, null));
        }

        /// <summary>一段：灰的标题行，下面按类别、阶数列东西。</summary>
        private static void AddSection(List<InquiryElement> items, string title, ItemRoster roster)
        {
            List<ItemRosterElement> elements = Enumerable.Range(0, roster.Count)
                .Select(roster.GetElementCopyAtIndex)
                .Where(element => element.Amount > 0)
                .OrderBy(element => element.EquipmentElement.Item.ItemType)
                .ThenByDescending(element => (int)element.EquipmentElement.Item.Tier)
                .ToList();
            items.Add(new InquiryElement(title, GwpText.Get(GwpTextCatalog.gwp_fs_armory_section,
                    "TITLE", title, "COUNT", elements.Sum(element => element.Amount),
                    "VALUE", elements.Sum(element => element.Amount * element.EquipmentElement.ItemValue)),
                null, false, string.Empty));
            foreach (ItemRosterElement element in elements)
            {
                string name = element.EquipmentElement.GetModifiedItemName().ToString();
                items.Add(new InquiryElement(element.EquipmentElement.Item.StringId,
                    GwpText.Get(GwpTextCatalog.gwp_fs_armory_item, "NAME", name, "COUNT", element.Amount),
                    new ItemImageIdentifier(element.EquipmentElement.Item), true, string.Empty));
            }
        }
    }
}
#endif
