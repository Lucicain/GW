using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
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

        public GwpBribeBarterable(Hero originalOwner, Hero otherHero, PartyBase ownerParty, PartyBase otherParty, int bribeAmount)
            : base(originalOwner, ownerParty)
        {
            _bribeAmount = bribeAmount;
            _originalOwner = originalOwner;
            _otherHero = otherHero;
            _ownerParty = ownerParty;
            _otherParty = otherParty;
        }

        public override string StringID => "gwp_bribe";
        public override TextObject Name => new TextObject("打发纠察队的贿款");
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
            // 我们在成功后的对话中(OnBribeConsequence)处理后续逻辑
        }

        public override void CheckBarterLink(Barterable linkedBarterable) { }
        public override TaleWorlds.Core.ImageIdentifiers.ImageIdentifier GetVisualIdentifier() => null;
    }
}
