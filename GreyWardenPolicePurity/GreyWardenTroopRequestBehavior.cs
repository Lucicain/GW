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
            new TroopOffer("recruits_small",  GwpIds.PoliceRecruitId,  5,  GwpTuning.TroopRequest.MinimumReputation, GwpTuning.TroopRequest.RecruitBasePrice,      GwpText.Get("{=gwp_greywardentrooprequestbehavior_001}A few Warden initiates")),
            new TroopOffer("recruits_large",  GwpIds.PoliceRecruitId, 10,  GwpTuning.TroopRequest.MinimumReputation, GwpTuning.TroopRequest.RecruitBasePrice,      GwpText.Get("{=gwp_greywardentrooprequestbehavior_002}A company of Warden initiates")),
            new TroopOffer("infantry_small",  GwpIds.HeavyInfantryId,  5,  GwpTuning.TroopRequest.VeteranReputation, GwpTuning.TroopRequest.HeavyInfantryBasePrice, GwpText.Get("{=gwp_greywardentrooprequestbehavior_003}A detachment of heavy foot")),
            new TroopOffer("archers_small",   GwpIds.ArcherId,         5,  GwpTuning.TroopRequest.VeteranReputation, GwpTuning.TroopRequest.ArcherBasePrice,        GwpText.Get("{=gwp_greywardentrooprequestbehavior_004}A company of duty archers")),
            new TroopOffer("knights_small",   GwpIds.KnightId,         3,  GwpTuning.TroopRequest.KnightReputation,  GwpTuning.TroopRequest.KnightBasePrice,        GwpText.Get("{=gwp_greywardentrooprequestbehavior_005}An armoured mounted patrol"))
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
                GwpText.Get("{=gwp_greywardentrooprequestbehavior_006}I would ask for Grey Warden personnel."),
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
                GwpText.Get("{=gwp_greywardentrooprequestbehavior_007}Not now."),
                null,
                ClearSelectedOffer,
                100);

            starter.AddDialogLine(
                "gwp_troop_request_cancel_response",
                "gwp_troop_request_cancel_response",
                "lord_talk_speak_diplomacy_2",
                GwpText.Get("{=gwp_greywardentrooprequestbehavior_008}Then speak again when you are ready."),
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
                GwpText.Get("{=gwp_greywardentrooprequestbehavior_009}The sum is accounted for. The Grey Wardens shall place them under your charge; do not employ them as common sellswords."),
                TroopRequestBarterSucceeded,
                OnTroopRequestBarterAccepted,
                100);

            starter.AddDialogLine(
                "gwp_troop_request_barter_failed",
                "gwp_troop_request_barter_post",
                "gwp_troop_request_menu",
                GwpText.Get("{=gwp_greywardentrooprequestbehavior_010}Your offer falls short. If you truly seek a detachment, place a worthier sum upon the table."),
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
                GwpText.Get("{=gwp_greywardentrooprequestbehavior_011}These are not mercenaries bought in a market. By your present standing, the detachment requires {VAR_1} denars. If you consent, place the sum upon the bargaining table.", "VAR_1", price));
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
                GwpText.Get("{=gwp_greywardentrooprequestbehavior_012}The Grey Wardens have assigned {VAR_1} {VAR_2} to your command.", "VAR_1", offer.Count, "VAR_2", troop.Name),
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
                GwpText.Get("{=gwp_greywardentrooprequestbehavior_013}Assign {VAR_1}", "VAR_1", offer.DisplayName));

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
            string discountText = discountPercent > 0 ? GwpText.Get("{=gwp_greywardentrooprequestbehavior_014}, with a {VAR_1}% reduction for your standing", "VAR_1", discountPercent) : "";
            return GwpText.Get("{=gwp_greywardentrooprequestbehavior_015}I request {VAR_1} {VAR_2} ({VAR_3} denars{VAR_4})", "VAR_1", offer.Count, "VAR_2", troopName, "VAR_3", price, "VAR_4", discountText);
        }

        private static TextObject BuildTroopRequestResponse(int reputation)
        {
            if (reputation >= GwpTuning.TroopRequest.KnightReputation)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_troop_req_high}Your standing with the Grey Wardens is high. If you need to restore a duty company, archers and armoured mounted patrols may both be assigned to you, within reason."));
            }

            if (reputation >= GwpTuning.TroopRequest.VeteranReputation)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_troop_req_mid}You have proved worthy of trust. Beyond initiates, I may assign heavy foot or duty archers to you. Choose."));
            }

            return new TextObject(
                GwpText.Get("{=gwp_troop_req_low}At present, your standing warrants only a few initiates under your charge. Earn greater trust before asking for seasoned Wardens."));
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
