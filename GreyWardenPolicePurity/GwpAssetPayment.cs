using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    // One receipt and one shared ceiling for gold and goods on both sides.
    // Item values are disclosed in the table; equipment worn by heroes is excluded.
    internal sealed class GwpAssetPayment
    {
        private readonly Hero _payer, _receiver;
        private readonly PartyBase _from, _to;
        private readonly int _limit;
        internal readonly List<Barterable> Entries = new List<Barterable>();
        internal bool Applied { get; private set; }
        internal int Paid { get; private set; }
        internal int Available { get; }
        internal GwpAssetPayment(Hero payer, Hero receiver, PartyBase from, PartyBase to, int limit)
        {
            _payer = payer; _receiver = receiver; _from = from; _to = to; _limit = Math.Max(0, limit);
            Entries.Add(new Money(this, payer, from, true));
            Entries.Add(new Money(this, receiver, to, false));
            AddGoods(payer, receiver, from, to, true);
            AddGoods(receiver, payer, to, from, false);
            Available = Wealth(payer, from);
            if (limit != int.MaxValue) Entries.Add(new Agreement(this));
        }
        internal static int Wealth(Hero hero, PartyBase party) => (int)Math.Min(int.MaxValue,
            Math.Max(0, (long)hero.Gold) + party.ItemRoster.Sum(e => (long)Math.Max(0, e.Amount) * Price(e)));
        private static int Price(ItemRosterElement e) => Math.Max(1, e.EquipmentElement.ItemValue);
        private void AddGoods(Hero owner, Hero other, PartyBase from, PartyBase to, bool incoming)
        {
            foreach (var item in from.ItemRoster)
                if (item.Amount > 0 && item.EquipmentElement.Item != null)
                    Entries.Add(new Goods(this, owner, other, from, to, item, incoming));
        }
        private long Net => Entries.Where(e => e.IsOffered).Sum(e =>
            e is Money m ? (long)m.CurrentAmount * (m.Incoming ? 1 : -1)
            : e is Goods g ? (long)e.CurrentAmount * g.Value * (g.Incoming ? 1 : -1) : 0);
        private bool Valid => Net >= 0 && Net <= _limit && Entries.All(e =>
            e.CurrentAmount >= 0 && (!e.IsOffered || e is Agreement || (e is Money m ? m.CurrentAmount <= m.OriginalOwner.Gold
            : e.CurrentAmount <= e.OriginalParty.ItemRoster.Where(x => x.EquipmentElement.Equals(((Goods)e).ItemRosterElement.EquipmentElement)).Sum(x => x.Amount))));
        private int ValueFor(IFaction faction, bool incoming, int value)
        {
            // Prefer the actual counterparty when both heroes share a kingdom.
            Hero other = _payer == Hero.MainHero ? _receiver : _payer;
            bool forOther = faction == other.Clan || faction == other.MapFaction;
            bool forPayer = forOther ? other == _payer : other != _payer;
            return value * (incoming == forPayer ? -1 : 1);
        }
        internal bool IsAgreement(Barterable entry) => entry is Agreement;
        internal void PrepareCatalogue(BarterData data)
        {
            data.GetBarterables().Clear();
            foreach (var entry in Entries)
            {
                int initial = entry is Agreement ? 1
                    : entry == Entries[0] && _limit != int.MaxValue ? Math.Min(_limit, Math.Max(0, _payer.Gold)) : 0;
                entry.SetIsOffered(initial > 0);
                entry.CurrentAmount = initial;
                if (entry is Money) data.AddBarterable<GoldBarterGroup>(entry);
                else if (entry is Goods) data.AddBarterable<ItemBarterGroup>(entry);
                else data.AddBarterable<OtherBarterGroup>(entry, true);
            }
        }
        private sealed class Agreement : Barterable
        {
            private readonly GwpAssetPayment _payment;
            internal Agreement(GwpAssetPayment payment) : base(payment._receiver, payment._to) { _payment = payment; }
            public override string StringID => "gwp_collection_agreement";
            public override TextObject Name => GwpText.Create("{=gwp_collection_agreement}Agreed payment allowance ({VAR_1} denars)", "VAR_1", _payment._limit);
            public override int GetUnitValueForFaction(IFaction faction)
            {
                Hero other = _payment._payer == Hero.MainHero ? _payment._receiver : _payment._payer;
                // No resale value to the player: native auto-offer must not remove this fixed condition.
                return faction == other.Clan || faction == other.MapFaction ? _payment._limit : 0;
            }
            public override ImageIdentifier GetVisualIdentifier() => null!;
            public override void Apply() => _payment.Apply();
        }
        private void Apply()
        {
            if (Applied) return;
            if (!Valid)
            {
#if GWP_DIAGNOSTICS
                GwpAiDiagnostics.WriteFieldArrest("ASSET_PAYMENT_REJECTED", "net=" + Net + "; limit=" + _limit + "; entries=" + Entries.Count);
#endif
                return;
            }
            Paid = (int)Net;
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFieldArrest("ASSET_PAYMENT_APPLIED", "net=" + Paid + "; limit=" + _limit + "; offered=" + Entries.Count(e => e.IsOffered));
#endif
            // Validation covers the entire selection before any transfer takes place.
            Applied = true;
            foreach (var entry in Entries.Where(e => e.IsOffered && e.CurrentAmount > 0))
            {
                if (entry is Money m)
                    GiveGoldAction.ApplyBetweenCharacters(m.OriginalOwner, m.Incoming ? _receiver : _payer,
                        m.CurrentAmount, disableNotification: true);
                else if (entry is Goods goods) goods.Transfer();
            }
        }
        private sealed class Money : GoldBarterable
        {
            private readonly GwpAssetPayment _payment;
            internal bool Incoming { get; }
            private readonly int _max;
            internal Money(GwpAssetPayment payment, Hero hero, PartyBase party, bool incoming) : base(hero, incoming ? payment._receiver : payment._payer, party, incoming ? payment._to : payment._from, Math.Max(0, hero.Gold))
            { _payment = payment; Incoming = incoming; _max = Math.Max(0, hero.Gold); }
            public override string StringID => Incoming ? "gwp_asset_gold_in" : "gwp_asset_gold_out";
            public override int MaxAmount => _max;
            public override TextObject Name => GwpText.Create("{=gwp_asset_gold}Gold");
            public override int GetUnitValueForFaction(IFaction faction) => _payment.ValueFor(faction, Incoming, 1);
            public override ImageIdentifier GetVisualIdentifier() => null!;
            public override void Apply() => _payment.Apply();
        }
        private sealed class Goods : ItemBarterable
        {
            private readonly GwpAssetPayment _payment;
            internal bool Incoming { get; }
            internal int Value { get; }
            internal Goods(GwpAssetPayment payment, Hero owner, Hero other, PartyBase from, PartyBase to,
                ItemRosterElement item, bool incoming) : base(owner, other, from, to, item, Price(item))
            { _payment = payment; Incoming = incoming; Value = Price(item); }
            public override TextObject Name => GwpText.Create("{=gwp_asset_value}{VAR_1} ({VAR_2} denars each)",
                "VAR_1", base.Name, "VAR_2", Value);
            public override int GetUnitValueForFaction(IFaction faction) => _payment.ValueFor(faction, Incoming, Value);
            public override void Apply() => _payment.Apply();
            internal void Transfer() => base.Apply();
        }
    }
}
