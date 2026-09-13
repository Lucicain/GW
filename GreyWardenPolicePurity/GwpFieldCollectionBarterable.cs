using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    // The whole purse is visible. The native offer button refuses amounts above
    // the terms actually agreed, even when the offender can afford more.
    internal sealed class GwpFieldCollectionBarterable : Barterable
    {
        private readonly Hero _payer;
        private readonly int _available;
        internal int AgreedLimit { get; }
        internal bool Applied { get; private set; }
        internal int Paid { get; private set; }
        internal GwpFieldCollectionBarterable(Hero payer, PartyBase party, int agreedLimit) : base(payer, party)
        {
            _payer = payer;
            _available = Math.Max(0, payer.Gold);
            AgreedLimit = Math.Max(0, Math.Min(_available, agreedLimit));
        }
        public override string StringID => "gwp_field_collection";
        public override TextObject Name => GwpText.Create("{=gwp_field_collection_money}Money in the offender's purse");
        public override int MaxAmount => _available;
        public override int GetUnitValueForFaction(IFaction faction) =>
            faction == _payer.Clan || faction == _payer.MapFaction
                ? (CurrentAmount <= AgreedLimit ? 0 : -1) : 1;
        public override void Apply()
        {
            if (Applied) return;
            if (CurrentAmount < 0 || CurrentAmount > AgreedLimit || CurrentAmount > _payer.Gold)
            {
                GwpAiDiagnostics.WriteFieldArrest("COLLECTION_REJECTED", "asked=" + CurrentAmount + "; limit=" + AgreedLimit);
                return;
            }
            Paid = CurrentAmount;
            if (Paid > 0) GiveGoldAction.ApplyBetweenCharacters(_payer, Hero.MainHero, Paid, disableNotification: true);
            Applied = true;
        }
        public override ImageIdentifier GetVisualIdentifier() => null!;
    }
}
