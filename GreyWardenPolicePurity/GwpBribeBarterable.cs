﻿using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    public class GwpBribeBarterable : Barterable
    {
        private readonly int _bribeAmount;
        private readonly Hero _originalOwner;
        private readonly Hero _otherHero;
        private readonly PartyBase _ownerParty;
        private readonly PartyBase _otherParty;
        private readonly TextObject _name;

        public GwpBribeBarterable(Hero originalOwner, Hero otherHero, PartyBase ownerParty, PartyBase otherParty, int bribeAmount)
            : this(originalOwner, otherHero, ownerParty, otherParty, bribeAmount, "")
        {
        }

        public GwpBribeBarterable(Hero originalOwner, Hero otherHero, PartyBase ownerParty, PartyBase otherParty, int bribeAmount, string displayName)
            : base(originalOwner, ownerParty)
        {
            _bribeAmount = bribeAmount;
            _originalOwner = originalOwner;
            _otherHero = otherHero;
            _ownerParty = ownerParty;
            _otherParty = otherParty;
            _name = new TextObject(string.IsNullOrWhiteSpace(displayName)
                ? "{=gwp_patrol_barter_name_cn}Penalty payment"
                : displayName);
        }

        public override string StringID => "gwp_patrol_bribe";
        public override TextObject Name => _name;
        public override int MaxAmount => 1;

        public override int GetUnitValueForFaction(IFaction faction)
        {
            if (faction == _otherHero?.MapFaction || faction == _otherParty?.MapFaction)
                return _bribeAmount;

            if (faction == _originalOwner?.MapFaction || faction == _ownerParty?.MapFaction)
                return -_bribeAmount;

            return 0;
        }

        public override void Apply()
        {
            // Side effects are handled in the post-barter conversation consequence.
        }

        public override void CheckBarterLink(Barterable linkedBarterable) { }
        public override ImageIdentifier GetVisualIdentifier() => null;
    }
}

