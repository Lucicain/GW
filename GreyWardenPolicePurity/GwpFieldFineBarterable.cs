using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    // A dedicated payment entry records exactly what was delivered. Ordinary
    // purchases, gifts and cancelled offers cannot masquerade as a case payment.
    internal sealed class GwpFieldFineBarterable : Barterable
    {
        private readonly Hero _receiver;
        private readonly int _maximum;
        internal bool Applied { get; private set; }
        internal int Paid { get; private set; }

        internal GwpFieldFineBarterable(Hero receiver, int assessed)
            : base(Hero.MainHero, MobileParty.MainParty.Party)
        {
            _receiver = receiver;
            _maximum = Math.Max(0, Math.Min(Hero.MainHero.Gold, assessed));
        }

        public override string StringID => "gwp_case_payment";
        public override TextObject Name => GwpText.Create("{=gwp_case_payment}Case fines delivered to the judicial treasury");
        public override int MaxAmount => _maximum;
        public override int GetUnitValueForFaction(IFaction faction) =>
            faction == _receiver.MapFaction || faction == _receiver.Clan ? 0 : -1;
        public override void Apply()
        {
            if (Applied) return;
            Paid = Math.Max(0, Math.Min(CurrentAmount, Math.Min(_maximum, Hero.MainHero.Gold)));
            if (Paid > 0)
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, _receiver, Paid, disableNotification: true);
            Applied = true;
        }
        public override void CheckBarterLink(Barterable linkedBarterable) { }
        public override ImageIdentifier GetVisualIdentifier() => null!;
    }
}
