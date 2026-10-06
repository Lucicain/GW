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
    /// 看军械库里有什么（用户 2026-10-06：村子的、部队的都要看到）。每个军械库只在它自己那里看：
    /// 信众村的在这座村的村庄菜单里看；灰袍领主部队的，找这位领主说话时问她（入了会才问得到）。
    /// 一个入口看遍所有军械库是不对的（用户裁定 2026-10-07）。只看，不能拿。
    /// </summary>
    internal static class GwpFireswornArmoryView
    {
        internal static void Register(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("village", "gwp_fs_village_armory",
                "{=gwp_fs_armory_option}Eternal Fire: storeroom",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return GwpFireswornBehavior.IsPreachedVillage(Settlement.CurrentSettlement);
                },
                _ => ShowContents(Settlement.CurrentSettlement.StringId), isLeave: false, index: 6);

            starter.AddPlayerLine("gwp_fs_lord_armory_ask", "lord_talk_speak_diplomacy_2", "gwp_fs_lord_armory_reply",
                GwpText.Get("{=gwp_fs_lord_armory_ask}Show me the arms your company carries for the sworn."),
                () => GwpCommon.IsOrdinaryGreyWardenLordConversation() &&
                      Hero.OneToOneConversationHero.PartyBelongedTo?.LeaderHero == Hero.OneToOneConversationHero &&
                      Campaign.Current.GetCampaignBehavior<PlayerBountyBehavior>().IsRecruitedByGreyWardens,
                null, 215);
            starter.AddDialogLine("gwp_fs_lord_armory_reply", "gwp_fs_lord_armory_reply", "lord_pretalk",
                GwpText.Get("{=gwp_fs_lord_armory_reply}Here. Everything we carry, and who wears it."),
                null, () => ShowContents(Hero.OneToOneConversationHero.PartyBelongedTo.StringId), 215);
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
                GwpText.Get("{=gwp_fs_armory_close}Close"), string.Empty, _ => { }, null));
        }
    }
}
