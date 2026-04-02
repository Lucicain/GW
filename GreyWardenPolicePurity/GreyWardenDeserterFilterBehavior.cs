using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    public sealed class GreyWardenDeserterFilterBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            SanitizeAllDeserterParties();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            SanitizeAllDeserterParties();
        }

        private static void SanitizeAllDeserterParties()
        {
            foreach (MobileParty party in MobileParty.All.ToList())
            {
                if (!GwpCommon.IsDeserterParty(party))
                    continue;

                SanitizeDeserterParty(party);
            }
        }

        private static void SanitizeDeserterParty(MobileParty? party)
        {
            if (party == null || !party.IsActive)
                return;

            if (!GwpCommon.RemoveGreyWardenTroops(party.MemberRoster))
                return;

            if (party.MemberRoster.TotalRegulars <= 0)
            {
                try { DestroyPartyAction.Apply(null, party); } catch { }
                return;
            }

            PartyBaseHelper.SortRoster(party);
        }
    }
}
