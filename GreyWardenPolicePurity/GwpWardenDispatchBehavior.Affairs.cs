using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    public partial class GwpWardenDispatchBehavior
    {
        internal IEnumerable<GwpAffairsEntry> GetAffairsDispatches()
        {
            foreach (GwpDispatchRecord record in _dispatches.OrderByDescending(d => d.DispatchedHours))
            {
                MobileParty? party = FindParty(record.PartyId);
                if (party == null) continue;
                string titleKey = record.Purpose switch
                {
                    GwpDispatchPurpose.Report => GwpTextCatalog.gwp_affairs_report,
                    GwpDispatchPurpose.Support => GwpTextCatalog.gwp_affairs_support_courier,
                    GwpDispatchPurpose.TroopOrder => GwpTextCatalog.gwp_affairs_order_courier,
                    GwpDispatchPurpose.FiefAppeal => GwpTextCatalog.gwp_affairs_fief_courier,
                    _ => GwpTextCatalog.gwp_affairs_peace_courier
                };
                MobileParty? receiver = FindParty(record.ReceiverPartyId);
                var entry = new GwpAffairsEntry("dispatch:" + record.PartyId,
                    GwpText.Get(titleKey), GwpText.Get(titleKey))
                {
                    IsPersonal = true, DispatchPurpose = record.Purpose, Hero = receiver?.LeaderHero,
                    NotesTitle = GwpText.Get(GwpTextCatalog.gwp_affairs_cargo),
                    Summary = GwpText.Get(record.Phase == GwpDispatchPhase.Outbound
                        ? GwpTextCatalog.gwp_affairs_outbound : GwpTextCatalog.gwp_affairs_returning)
                };
                if (record.HandoverPending) entry.Summary = GwpText.Get(GwpTextCatalog.gwp_affairs_pending);
                else if (record.Phase == GwpDispatchPhase.Returning && MobileParty.MainParty?.IsActive != true)
                    entry.Summary = GwpText.Get(GwpTextCatalog.gwp_affairs_waiting);
                else if (!string.IsNullOrWhiteSpace(record.SupplyTownId))
                    entry.Summary = GwpText.Get(GwpTextCatalog.gwp_affairs_selling);
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_status, entry.Summary));
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_receiver,
                    receiver?.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_affairs_unknown)));
                if (record.Purpose == GwpDispatchPurpose.FiefAppeal)
                    entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_fief_target,
                        TaleWorlds.CampaignSystem.Settlements.Settlement.Find(record.FiefSettlementId)?.Name?.ToString() ?? record.FiefSettlementId));
                entry.Fields.Add(GwpAffairsText.TargetLocation(party, GwpTextCatalog.gwp_affairs_current_location));
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_men, GwpText.Get(GwpTextCatalog.gwp_affairs_numbers,
                    "TOTAL", party.MemberRoster.TotalManCount, "HEALTHY", party.Party.NumberOfHealthyMembers)));
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_gold, GwpAffairsText.Money(party.PartyTradeGold)));
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_prisoners, party.PrisonRoster.TotalManCount));
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_departed,
                    GwpCaseArchiveItemVM.FormatCampaignDate(CampaignTime.Hours((float)record.DispatchedHours))));
                foreach (var item in party.ItemRoster)
                    entry.Notes.Add(GwpAffairsText.Pair(item.EquipmentElement.GetModifiedItemName().ToString(), item.Amount.ToString()));
                yield return entry;
            }
        }
    }
}
