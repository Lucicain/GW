using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 高声望玩家可向灰袍领主征调兵员。
    /// 规则：
    /// 1. 声望 20+ 开放征调；
    /// 2. 声望越高，可选兵种越精锐，价格越低；
    /// 3. 付款走 barter，成功后直接补兵到主角部队。
    /// </summary>
    public sealed class GreyWardenTroopRequestBehavior : CampaignBehaviorBase
    {
        private static readonly TroopOffer[] TroopOffers =
        {
            new TroopOffer("recruits_small",  GwpIds.PoliceRecruitId,  5,  GwpTuning.TroopRequest.MinimumReputation, GwpTuning.TroopRequest.RecruitBasePrice,      "少量见习守卫"),
            new TroopOffer("recruits_large",  GwpIds.PoliceRecruitId, 10,  GwpTuning.TroopRequest.MinimumReputation, GwpTuning.TroopRequest.RecruitBasePrice,      "一队见习守卫"),
            new TroopOffer("infantry_small",  GwpIds.HeavyInfantryId,  5,  GwpTuning.TroopRequest.VeteranReputation, GwpTuning.TroopRequest.HeavyInfantryBasePrice, "重装步兵小队"),
            new TroopOffer("archers_small",   GwpIds.ArcherId,         5,  GwpTuning.TroopRequest.VeteranReputation, GwpTuning.TroopRequest.ArcherBasePrice,        "持弓执勤队"),
            new TroopOffer("knights_small",   GwpIds.KnightId,         3,  GwpTuning.TroopRequest.KnightReputation,  GwpTuning.TroopRequest.KnightBasePrice,        "披甲骑巡队")
        };

        private TroopOffer? _selectedOffer;
        private bool _troopBarterStarted;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsLoading)
            {
                _selectedOffer = null;
                _troopBarterStarted = false;
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "gwp_troop_request_open",
                "lord_talk_speak_diplomacy_2",
                "gwp_troop_request_response",
                "我想调一些灰袍的人手。",
                CanOpenTroopRequestDialogue,
                null,
                100);

            starter.AddDialogLine(
                "gwp_troop_request_response",
                "gwp_troop_request_response",
                "gwp_troop_request_menu",
                "{" + GwpTextKeys.TroopRequestResponse + "}",
                PrepareTroopRequestResponse,
                null,
                100);

            AddOfferLine(starter, "recruits_small");
            AddOfferLine(starter, "recruits_large");
            AddOfferLine(starter, "infantry_small");
            AddOfferLine(starter, "archers_small");
            AddOfferLine(starter, "knights_small");

            starter.AddPlayerLine(
                "gwp_troop_request_cancel",
                "gwp_troop_request_menu",
                "gwp_troop_request_cancel_response",
                "先算了。",
                null,
                ClearSelectedOffer,
                100);

            starter.AddDialogLine(
                "gwp_troop_request_cancel_response",
                "gwp_troop_request_cancel_response",
                "lord_talk_speak_diplomacy_2",
                "那就等你准备好再来开口。",
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_troop_request_barter_pre",
                "gwp_troop_request_barter_pre",
                "gwp_troop_request_barter_screen",
                "{" + GwpTextKeys.TroopSelectedOfferPrice + "}",
                PrepareSelectedOfferPriceText,
                null,
                100);

            starter.AddDialogLine(
                "gwp_troop_request_barter_screen",
                "gwp_troop_request_barter_screen",
                "gwp_troop_request_barter_post",
                "{=!}Barter screen goes here",
                null,
                OnTroopRequestBarterConsequence,
                100);

            starter.AddDialogLine(
                "gwp_troop_request_barter_success",
                "gwp_troop_request_barter_post",
                "lord_pretalk",
                "款项核清。灰袍会把人交给你，但别把她们当成寻常雇兵使唤。",
                TroopRequestBarterSucceeded,
                OnTroopRequestBarterAccepted,
                100);

            starter.AddDialogLine(
                "gwp_troop_request_barter_failed",
                "gwp_troop_request_barter_post",
                "gwp_troop_request_menu",
                "你的报价还不够。若真要调人，就拿出更像样的数目。",
                () => !TroopRequestBarterSucceeded(),
                null,
                100);
        }

        private void AddOfferLine(CampaignGameStarter starter, string offerId)
        {
            TroopOffer? offer = FindOffer(offerId);
            if (offer == null)
                return;

            starter.AddPlayerLine(
                "gwp_troop_offer_" + offerId,
                "gwp_troop_request_menu",
                "gwp_troop_request_barter_pre",
                "{" + offer.TextVariableKey + "}",
                () => IsOfferAvailable(offerId),
                () => SelectOffer(offerId),
                100);
        }

        private bool CanOpenTroopRequestDialogue()
        {
            Hero? conversationHero = Hero.OneToOneConversationHero;
            if (!GwpCommon.IsGreyWardenLord(conversationHero))
                return false;

            if (GetPlayerReputation() < GwpTuning.TroopRequest.MinimumReputation)
                return false;

            return !IsPoliceInteractionConversation();
        }

        private bool PrepareTroopRequestResponse()
        {
            int reputation = GetPlayerReputation();
            MBTextManager.SetTextVariable(
                GwpTextKeys.TroopRequestResponse,
                BuildTroopRequestResponse(reputation));

            foreach (TroopOffer offer in TroopOffers)
            {
                MBTextManager.SetTextVariable(
                    offer.TextVariableKey,
                    BuildOfferLabel(offer, reputation));
            }

            return true;
        }

        private bool IsOfferAvailable(string offerId)
        {
            TroopOffer? offer = FindOffer(offerId);
            if (offer == null)
                return false;

            int reputation = GetPlayerReputation();
            return reputation >= offer.MinimumReputation;
        }

        private void SelectOffer(string offerId)
        {
            _selectedOffer = FindOffer(offerId);
            _troopBarterStarted = false;
        }

        private bool PrepareSelectedOfferPriceText()
        {
            TroopOffer? offer = _selectedOffer;
            if (offer == null)
                return false;

            int reputation = GetPlayerReputation();
            int price = GetOfferPrice(offer, reputation);
            MBTextManager.SetTextVariable(
                GwpTextKeys.TroopSelectedOfferPrice,
                $"这批人不是市井佣兵。按你现在的名声，这笔调拨要 {price} 金。若你愿意，现在就把款项放上谈判桌。");
            return true;
        }

        private void OnTroopRequestBarterConsequence()
        {
            TroopOffer? offer = _selectedOffer;
            if (offer == null)
                return;

            _troopBarterStarted = StartTroopRequestBarter(offer, GetOfferPrice(offer, GetPlayerReputation()));
        }

        private bool TroopRequestBarterSucceeded()
        {
            return _troopBarterStarted &&
                   Campaign.Current?.BarterManager != null &&
                   Campaign.Current.BarterManager.LastBarterIsAccepted;
        }

        private void OnTroopRequestBarterAccepted()
        {
            TroopOffer? offer = _selectedOffer;
            _selectedOffer = null;
            _troopBarterStarted = false;
            if (offer == null)
                return;

            CharacterObject troop = CharacterObject.Find(offer.TroopId);
            if (troop == null || MobileParty.MainParty == null)
                return;

            MobileParty.MainParty.MemberRoster.AddToCounts(troop, offer.Count);
            InformationManager.DisplayMessage(new InformationMessage(
                $"灰袍已向你调拨 {offer.Count} 名{troop.Name}。",
                Colors.Green));
        }

        private void ClearSelectedOffer()
        {
            _selectedOffer = null;
            _troopBarterStarted = false;
        }

        private bool StartTroopRequestBarter(TroopOffer offer, int amount)
        {
            Hero? barterHero = Hero.OneToOneConversationHero;
            if (barterHero == null || MobileParty.MainParty == null || Campaign.Current?.BarterManager == null)
                return false;

            PartyBase playerParty = MobileParty.MainParty.Party;
            PartyBase barterParty = barterHero.PartyBelongedTo?.Party ?? playerParty;
            if (playerParty == null || barterParty == null)
                return false;

            var troopPurchase = new GwpBribeBarterable(
                barterHero,
                Hero.MainHero,
                barterParty,
                playerParty,
                Math.Max(1, amount),
                $"调拨{offer.DisplayName}");

            try
            {
                Campaign.Current.BarterManager.StartBarterOffer(
                    Hero.MainHero,
                    barterHero,
                    playerParty,
                    barterParty,
                    null,
                    InitializeTroopRequestBarterContext,
                    0,
                    false,
                    new[] { troopPurchase });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool InitializeTroopRequestBarterContext(Barterable barterable, BarterData args, object obj)
        {
            return barterable is GwpBribeBarterable;
        }

        private static TroopOffer? FindOffer(string offerId)
        {
            foreach (TroopOffer offer in TroopOffers)
            {
                if (string.Equals(offer.Id, offerId, StringComparison.Ordinal))
                    return offer;
            }

            return null;
        }

        private static int GetPlayerReputation()
        {
            return GwpRuntimeState.Player.Reputation;
        }

        private static int GetOfferPrice(TroopOffer offer, int reputation)
        {
            int rawPrice = offer.Count * offer.BasePricePerTroop;
            int discountPercent = GetDiscountPercent(reputation);
            return Math.Max(1, rawPrice * (100 - discountPercent) / 100);
        }

        private static int GetDiscountPercent(int reputation)
        {
            if (reputation >= GwpTuning.TroopRequest.EliteDiscountReputation)
                return 30;

            if (reputation >= GwpTuning.TroopRequest.KnightReputation)
                return 20;

            if (reputation >= GwpTuning.TroopRequest.VeteranReputation)
                return 10;

            return 0;
        }

        private static string BuildOfferLabel(TroopOffer offer, int reputation)
        {
            CharacterObject troop = CharacterObject.Find(offer.TroopId);
            string troopName = troop?.Name?.ToString() ?? offer.DisplayName;
            int price = GetOfferPrice(offer, reputation);
            int discountPercent = GetDiscountPercent(reputation);
            string discountText = discountPercent > 0 ? $"，按当前名声减免 {discountPercent}%" : "";
            return $"我要 {offer.Count} 名{troopName}（{price} 金{discountText}）";
        }

        private static TextObject BuildTroopRequestResponse(int reputation)
        {
            if (reputation >= GwpTuning.TroopRequest.KnightReputation)
            {
                return new TextObject(
                    "{=gwp_troop_req_high}你在灰袍这里的名声已经够高。若只是补一支执勤队，步弓与披甲骑巡都可以调给你，但数目不会太离谱。");
            }

            if (reputation >= GwpTuning.TroopRequest.VeteranReputation)
            {
                return new TextObject(
                    "{=gwp_troop_req_mid}你已证明自己值得托付。见习守卫之外，我也可以拨一些重装步兵或执勤弓手给你。你自己挑。");
            }

            return new TextObject(
                "{=gwp_troop_req_low}你现在的名声，还只够让灰袍放一些见习守卫跟着你办事。若想调更老练的人，就再多积些信誉。");
        }

        private static bool IsPoliceInteractionConversation()
        {
            MobileParty? conversationParty = MobileParty.ConversationParty;
            if (conversationParty == null)
                return false;

            if (GwpCommon.IsPatrolParty(conversationParty) ||
                GwpCommon.IsEnforcementDelayPatrolParty(conversationParty))
            {
                return true;
            }

            PoliceTask? task = GwpRuntimeState.Crime.GetTask(conversationParty.StringId);
            return task?.TargetCrime?.Offender?.IsMainParty == true;
        }

        private sealed class TroopOffer
        {
            public TroopOffer(string id, string troopId, int count, int minimumReputation, int basePricePerTroop, string displayName)
            {
                Id = id;
                TroopId = troopId;
                Count = count;
                MinimumReputation = minimumReputation;
                BasePricePerTroop = basePricePerTroop;
                DisplayName = displayName;
                TextVariableKey = "GWP_TROOP_OFFER_" + id.ToUpperInvariant();
            }

            public string Id { get; }
            public string TroopId { get; }
            public int Count { get; }
            public int MinimumReputation { get; }
            public int BasePricePerTroop { get; }
            public string DisplayName { get; }
            public string TextVariableKey { get; }
        }
    }
}
