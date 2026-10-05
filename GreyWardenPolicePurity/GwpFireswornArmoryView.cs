using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 看军械库里有什么（用户 2026-10-06：村子的、部队的都要看到）。在信众村的村庄菜单里多一项「军械库」，
    /// 先列出本村和每支灰袍领主部队的军械库，选一个再列出里面的每样东西（带原版物品图标）。只看，不能拿。
    /// </summary>
    internal static class GwpFireswornArmoryView
    {
        internal static void Register(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("village", "gwp_fs_village_armory",
                "{=gwp_fs_armory_option}Eternal Fire: armories",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return GwpFireswornBehavior.IsPreachedVillage(Settlement.CurrentSettlement);
                },
                _ => ShowHolders(), isLeave: false, index: 6);
        }

        private static void ShowHolders()
        {
            List<InquiryElement> holders = GwpFireswornBehavior.ArmoryHolders(Settlement.CurrentSettlement)
                .Select(holder => new InquiryElement(holder.Id, holder.Title, null))
                .ToList();
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                GwpText.Get("{=gwp_fs_armory_holders_title}Armories of the Eternal Fire"),
                GwpText.Get("{=gwp_fs_armory_holders_text}Every faith village and every Warden lord's company keeps its own armory. The zealots and new recruits wear what is in it, one kit to a soldier. Choose one to see what it holds."),
                holders, true, 1, 1,
                GwpText.Get("{=gwp_fs_armory_open}Open"), GwpText.Get("{=gwp_fs_armory_close}Close"),
                chosen => ShowContents((string)chosen[0].Identifier), null));
        }

        private static void ShowContents(string holder)
        {
            ItemRoster ledger = GwpFireswornBehavior.ArmoryLedger(holder);
            List<InquiryElement> items = new List<InquiryElement>();
            foreach (ItemRosterElement element in Enumerable.Range(0, ledger.Count)
                         .Select(ledger.GetElementCopyAtIndex)
                         .OrderBy(element => element.EquipmentElement.Item.ItemType)
                         .ThenByDescending(element => (int)element.EquipmentElement.Item.Tier))
            {
                string name = element.EquipmentElement.GetModifiedItemName().ToString();
                items.Add(new InquiryElement(element.EquipmentElement.Item.StringId,
                    GwpText.Get("{=gwp_fs_armory_item}{NAME} × {COUNT}", "NAME", name, "COUNT", element.Amount),
                    new ItemImageIdentifier(element.EquipmentElement.Item), true, string.Empty));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                GwpFireswornBehavior.ArmoryHolderTitle(holder),
                GwpFireswornBehavior.ArmorySummary(holder),
                items, true, 0, 0,
                GwpText.Get("{=gwp_fs_armory_back}Back"), GwpText.Get("{=gwp_fs_armory_close}Close"),
                _ => ShowHolders(), null));
        }
    }
}
