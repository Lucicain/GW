using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private readonly int _autoTarget;
        internal static readonly ConditionalWeakTable<BarterData, GwpAssetPayment> Sessions = new ConditionalWeakTable<BarterData, GwpAssetPayment>();
        internal readonly List<Barterable> Entries = new List<Barterable>();
        internal bool Applied { get; private set; }
        internal int Paid { get; private set; }
        internal int Available { get; }
        internal bool SelectionOnly { get; }
        internal bool ReportMode { get; }
        private readonly GwpCaseReceipt? _autoReceipt;
        internal GwpCaseReceipt? Receipt { get; private set; }
        internal Hero? SelectedPrisoner => Entries.OfType<Prisoner>().FirstOrDefault(p => p.IsOffered && p.CurrentAmount == 1)?.Hero;
        internal int OfferedValue => (int)Math.Max(0, Math.Min(int.MaxValue, Net));
        internal int SuggestedTarget => _autoTarget;
        internal int SelectedGold => Entries.OfType<Money>().Where(m => m.Incoming && m.IsOffered).Sum(m => m.CurrentAmount);
        internal List<ItemRosterElement> SelectedGoods => Entries.OfType<Goods>()
            .Where(g => g.Incoming && g.IsOffered && g.CurrentAmount > 0)
            .Select(g => new ItemRosterElement(g.ItemRosterElement.EquipmentElement, g.CurrentAmount)).ToList();
        internal void ConfirmSelection() { if (SelectionOnly || ReportMode) Apply(); }
        /// <summary>
        /// 自动交易不许动用的口粮。送信队出发前要从玩家辎重里分走这么多粮，
        /// 一键换货要是把粮食也换走，队伍就只能在出发关口被拦下来。留出这条底线，
        /// 玩家仍可手动把粮食加进去——那属于他自己的选择，出发判断照旧会拦。
        /// </summary>
        private readonly int _rationsFloor;

        internal GwpAssetPayment(Hero payer, Hero receiver, PartyBase from, PartyBase to, int limit, int autoTarget = -1, bool selectionOnly = false,
            bool reportMode = false, GwpCaseReceipt? autoReceipt = null, Hero? prisoner = null, int rationsFloor = 0)
        {
            _rationsFloor = Math.Max(0, rationsFloor);
            SelectionOnly = selectionOnly;
            ReportMode = reportMode; _autoReceipt = autoReceipt;
            _payer = payer; _receiver = receiver; _from = from; _to = to; _limit = Math.Max(0, limit); _autoTarget = autoTarget < 0 ? _limit : Math.Max(0, autoTarget);
            Entries.Add(new Money(this, payer, from, true));
            if (!selectionOnly && !reportMode) Entries.Add(new Money(this, receiver, to, false));
            AddGoods(payer, receiver, from, to, true);
            if (!selectionOnly && !reportMode) AddGoods(receiver, payer, to, from, false);
            if (reportMode && prisoner != null) Entries.Add(new Prisoner(this, prisoner));
            Available = Wealth(payer, from);
            if (!selectionOnly && limit != int.MaxValue) Entries.Add(new Agreement(this));
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
        internal bool Valid => Net >= 0 && Net <= _limit && Entries.All(e =>
            e.CurrentAmount >= 0 && (!e.IsOffered || e is Agreement || (e is Money m ? m.CurrentAmount <= m.OriginalOwner.Gold
            : e is Prisoner p ? e.CurrentAmount <= 1 && p.Hero.IsPrisoner && p.Hero.PartyBelongedToAsPrisoner == _from
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
            Sessions.Remove(data); Sessions.Add(data, this);
            data.GetBarterables().Clear();
            foreach (var entry in Entries)
            {
                // Native VM keeps reusable catalogue entries only for initially unoffered assets.
                int initial = entry is Agreement ? 1 : 0;
                entry.SetIsOffered(initial > 0);
                entry.CurrentAmount = initial;
                if (entry is Money) data.AddBarterable<GoldBarterGroup>(entry);
                else if (entry is Goods) data.AddBarterable<ItemBarterGroup>(entry);
                else if (entry is Prisoner) data.AddBarterable<PrisonerBarterGroup>(entry);
                else data.AddBarterable<OtherBarterGroup>(entry, true);
            }
        }
        internal Dictionary<Barterable, int> SuggestedOffer()
        {
            var result = new Dictionary<Barterable, int>();
            if (ReportMode)
            {
                // 一键换货可以动的粮食上限：辎重里的粮总量减去出发要带的口粮。
                int spareFood = Math.Max(0, _from.ItemRoster
                    .Where(x => x.EquipmentElement.Item?.IsFood == true)
                    .Sum(x => Math.Max(0, x.Amount)) - _rationsFloor);
                foreach (var entry in Entries)
                {
                    int wanted = entry is Money ? Math.Max(0, _autoReceipt?.Gold ?? 0)
                        : entry is Goods g ? Math.Max(0, _autoReceipt?.Items.Where(i => i.Matches(g.ItemRosterElement.EquipmentElement)).Sum(i => i.Amount) ?? 0)
                        : entry is Prisoner ? 1 : 0;
                    int available = entry is Money ? Math.Max(0, _payer.Gold)
                        : entry is Goods goods ? _from.ItemRoster.Where(x => x.EquipmentElement.Equals(goods.ItemRosterElement.EquipmentElement)).Sum(x => x.Amount)
                        : entry is Prisoner p && p.Hero.IsPrisoner && p.Hero.PartyBelongedToAsPrisoner == _from ? 1 : 0;
                    int amount = Math.Min(wanted, Math.Min(entry.MaxAmount, available));
                    if (entry is Goods food && food.ItemRosterElement.EquipmentElement.Item?.IsFood == true)
                    {
                        amount = Math.Min(amount, spareFood);
                        spareFood = Math.Max(0, spareFood - amount);
                    }
                    if (amount > 0) result[entry] = amount;
                }
                return result;
            }
            long remaining = Math.Min(_autoTarget, _limit);
            foreach (var entry in Entries)
            {
                int value = entry is Money m && m.Incoming ? 1 : entry is Goods g && g.Incoming ? g.Value : 0;
                if (value <= 0) continue;
                int available = entry is Money ? Math.Max(0, _payer.Gold)
                    : _from.ItemRoster.Where(x => x.EquipmentElement.Equals(((Goods)entry).ItemRosterElement.EquipmentElement)).Sum(x => x.Amount);
                int amount = (int)Math.Min(Math.Min(entry.MaxAmount, available), remaining / value);
                if (amount > 0) { result[entry] = amount; remaining -= (long)amount * value; }
            }
            if (remaining > 0)
            {
                var changeItem = Entries.OfType<Goods>().Where(g => g.Incoming && g.Value > remaining
                    && (SelectionOnly ? (long)_autoTarget - remaining + g.Value <= _limit
                        : g.Value - remaining <= Math.Max(0, _receiver.Gold))
                    && (result.TryGetValue(g, out int count) ? count : 0) < Math.Min(g.MaxAmount,
                        _from.ItemRoster.Where(x => x.EquipmentElement.Equals(g.ItemRosterElement.EquipmentElement)).Sum(x => x.Amount)))
                    .OrderBy(g => g.Value).FirstOrDefault();
                if (changeItem != null)
                {
                    result[changeItem] = (result.TryGetValue(changeItem, out int count) ? count : 0) + 1;
                    if (!SelectionOnly) result[Entries[1]] = (int)(changeItem.Value - remaining);
                }
            }
            return result;
        }
        private sealed class Agreement : Barterable
        {
            private readonly GwpAssetPayment _payment;
            internal Agreement(GwpAssetPayment payment) : base(payment._receiver, payment._to) { _payment = payment; }
            public override string StringID => "gwp_collection_agreement";
            public override TextObject Name => GwpText.Create("{=gwp_collection_agreement}Honor our agreement");
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
#if GWP_DIAGNOSTICS
            if (!SelectionOnly) GwpAiDiagnostics.WriteFieldArrest("ASSET_PAYMENT_COMMIT", "net=" + Net + "; limit=" + _limit
                + "; selected=" + string.Join(" | ", Entries.Where(e => e.IsOffered).Select(e => e.StringID + ":" + e.CurrentAmount + ":" + e.OriginalOwner?.StringId
                    + (e is Goods g ? ":item=" + g.ItemRosterElement.EquipmentElement.Item.StringId
                        + ":modifier=" + g.ItemRosterElement.EquipmentElement.ItemModifier?.StringId + ":unitValue=" + g.Value : ""))));
#endif
            Paid = (int)Net;
            Receipt = new GwpCaseReceipt { Gold = Entries.OfType<Money>().Where(m => m.IsOffered).Sum(m => m.CurrentAmount * (m.Incoming ? 1 : -1)) };
            foreach (var g in Entries.OfType<Goods>().Where(g => g.IsOffered && g.CurrentAmount > 0))
                Receipt.Items.Add(new GwpCaseReceipt.Item { Id = g.ItemRosterElement.EquipmentElement.Item.StringId,
                    Modifier = g.ItemRosterElement.EquipmentElement.ItemModifier?.StringId ?? "",
                    Amount = g.CurrentAmount * (g.Incoming ? 1 : -1), Price = g.Value });
            // Verify custody before accepting a prisoner resolution or moving its money.
            if (!SelectionOnly)
                foreach (var prisoner in Entries.OfType<Prisoner>().Where(p => p.IsOffered && p.CurrentAmount == 1))
                    prisoner.Transfer();
            // Validation covers the entire selection before any transfer takes place.
            Applied = true;
            // A courier manifest is an instruction, not a remote transfer.
            if (SelectionOnly) return;
            foreach (var entry in Entries.Where(e => e.IsOffered && e.CurrentAmount > 0))
            {
                if (entry is Money m)
                    GiveGoldAction.ApplyBetweenCharacters(m.OriginalOwner, m.Incoming ? _receiver : _payer,
                        m.CurrentAmount, disableNotification: true);
                else if (entry is Goods goods) goods.Transfer();
            }
        }
        private sealed class Prisoner : TransferPrisonerBarterable
        {
            private readonly GwpAssetPayment _payment;
            internal Hero Hero { get; }
            internal Prisoner(GwpAssetPayment payment, Hero hero) : base(hero, payment._payer, payment._from, payment._receiver, payment._to)
            { _payment = payment; Hero = hero; }
            public override int GetUnitValueForFaction(IFaction faction) => 0;
            public override void Apply() => _payment.Apply();
            internal void Transfer()
            {
                TransferPrisonerAction.Apply(Hero.CharacterObject, _payment._from, _payment._to);
                if (Hero.PartyBelongedToAsPrisoner != _payment._to) throw new InvalidOperationException("Case prisoner custody transfer failed: " + Hero.StringId);
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
                ItemRosterElement item, bool incoming) : base(owner, other, from, to, item, payment._autoReceipt?.PriceFor(item.EquipmentElement) ?? Price(item))
            { _payment = payment; Incoming = incoming; Value = payment._autoReceipt?.PriceFor(item.EquipmentElement) ?? Price(item); }
            public override TextObject Name => GwpText.Create("{=gwp_asset_value}{VAR_1} ({VAR_2} denars each)",
                "VAR_1", base.Name, "VAR_2", Value);
            public override int GetUnitValueForFaction(IFaction faction) => _payment.ValueFor(faction, Incoming, Value);
            public override void Apply() => _payment.Apply();
            internal void Transfer() => base.Apply();
        }
    }
}
