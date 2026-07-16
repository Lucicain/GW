using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍设定文本：
    /// 1. 为已相识的灰袍领主提供基于玩家声望的动态会面台词。
    /// 2. 为灰袍核心成员写入百科人物介绍。
    /// </summary>
    public sealed class GreyWardenLoreBehavior : CampaignBehaviorBase
    {
        private const float MetGreetingChance = 0.5f;
        private static GwpRuntimeState.CrimeState CrimeState => GwpRuntimeState.Crime;
        private static GwpRuntimeState.PlayerState PlayerState => GwpRuntimeState.Player;
        private static string _lastGreetingConversationKey = string.Empty;
        private static bool _lastGreetingConversationResult;

        private static readonly Dictionary<string, TextObject> HeroEncyclopediaTexts =
            new Dictionary<string, TextObject>
            {
                ["gw_leader_0"] = new TextObject(
                    GwpText.Get("{=gwp_enc_vandi}Aethelflaed is the present Warden-General and one of the clearest living heirs to the constabulary of the old, undivided Empire. She holds the Grey Wardens to be a fellowship of law and oath, not an ordinary noble house. Beyond their walls she forbids them to seek crowns or widen estates: they are to keep the roads, villages, towns, and markets. Within, she bids every sworn daughter remember that the law is not a stage for pride, but a covenant by which common folk may trust that justice will endure another day.")),
                ["gw_leader_1"] = new TextObject(
                    GwpText.Get("{=gwp_enc_yoer}Cyneburh has long kept watch over the post roads and countryside of the old Empire. She is most often found upon distant tracks, ferries, and the crossings of merchants, settling brigandage, blood-feuds, and unlawful tolls. Many traders and villagers remember the grey mantle before they learn her name. To them she embodies a plain and steadfast truth: the Empire has broken, yet someone still keeps the road.")),
                ["gw_leader_2"] = new TextObject(
                    GwpText.Get("{=gwp_enc_mise}Mildthryth keeps the Grey Wardens’ case rolls and oversees atonement and amendment within the order. She believes that order reduced to punishment will in time become mere terror; therefore the guilty must be left a door by which to return. She has gathered and renewed many old rules of fines, escort, atonement, and settlement. Through her, the Wardens seem less a hard company of soldiers than a secular order bound by discipline.")),
                ["gw_leader_3"] = new TextObject(
                    GwpText.Get("{=gwp_enc_shengduo}Wynflaed preserves the statutes, judgments, and patrol records inherited from the old, undivided Empire. She seldom speaks in lofty terms, yet can swiftly trace any custom to its root in the ancient rolls. To the Grey Wardens, an archive is more than parchment: it is proof that they are no private warband born of disorder. The Empire may have lost its throne; the law has not thereby lost its voice.")),
                ["gw_leader_4"] = new TextObject(
                    GwpText.Get("{=gwp_enc_chenxi}Eadgifu is often sent where villages have suffered disaster, famine grips the marches, or war has left its deepest scars. She is skilled in relief, allotment, mediation, and the restoration of the simplest public order, and the common folk speak gently of her. Many first understand the Grey Wardens not when they see a criminal seized, but when they see ordinary lives set upright again amid the worst confusion.")),
                ["gw_leader_5"] = new TextObject(
                    GwpText.Get("{=gwp_enc_muguang}Wulfhild commands the sternest of the Grey Wardens’ field companies. She pursues those who resist arrest, escorts grave offenders, and chastens the incorrigible. Her name travels first among malefactors, and only afterward through the taverns. Yet the order counts her a strict keeper of rule, for she holds that steel may clear a path for the law, but must never take the law’s place."))
            };

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            ApplyHeroEncyclopediaTexts();
            RegisterMetLordGreeting(starter);
        }

        private void OnConversationEnded(IEnumerable<CharacterObject> characters)
        {
            _ = characters;
            _lastGreetingConversationKey = string.Empty;
            _lastGreetingConversationResult = false;
        }

        private static void RegisterMetLordGreeting(CampaignGameStarter starter)
        {
            starter.AddDialogLine(
                "gwp_grey_lord_met_greeting",
                "start",
                "gwp_grey_lord_met_followup",
                "{" + GwpTextKeys.GreyLordGreeting + "}",
                GreyLordMetGreetingCondition,
                null,
                200);

            starter.AddDialogLine(
                "gwp_grey_lord_met_followup",
                "gwp_grey_lord_met_followup",
                "lord_talk_speak_diplomacy_2",
                GwpText.Get("{=gwp_grey_lord_followup}Well, then—what is it?"),
                GreyLordMetGreetingCondition,
                null,
                200);
        }

        private static bool GreyLordMetGreetingCondition()
        {
            if (IsPostBattleCaptureConversation())
                return false;

            Hero? conversationHero = Hero.OneToOneConversationHero;
            if (!IsGreyWardenLord(conversationHero))
                return false;

            if (!conversationHero.HasMet)
                return false;

            if (IsPoliceInteractionConversation())
                return false;

            if (!RollMetGreetingChance(conversationHero))
                return false;

            MBTextManager.SetTextVariable(
                GwpTextKeys.GreyLordGreeting,
                BuildMetGreeting(PlayerState.Reputation));
            return true;
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

            PoliceTask? task = CrimeState.GetTask(conversationParty.StringId);
            return task?.TargetCrime?.Offender?.IsMainParty == true;
        }

        private static bool IsPostBattleCaptureConversation()
        {
            Campaign? campaign = Campaign.Current;
            if (campaign == null)
                return false;

            return campaign.CurrentConversationContext == ConversationContext.CapturedLord ||
                   campaign.CurrentConversationContext == ConversationContext.FreeOrCapturePrisonerHero;
        }

        private static bool RollMetGreetingChance(Hero conversationHero)
        {
            Campaign? campaign = Campaign.Current;
            string heroId = conversationHero.StringId ?? string.Empty;
            string partyId = MobileParty.ConversationParty?.StringId ?? string.Empty;
            string key = $"{campaign?.CurrentConversationContext}|{heroId}|{partyId}";

            if (!string.Equals(_lastGreetingConversationKey, key, System.StringComparison.Ordinal))
            {
                _lastGreetingConversationKey = key;
                _lastGreetingConversationResult = MBRandom.RandomFloat <= MetGreetingChance;
            }

            return _lastGreetingConversationResult;
        }

        private static TextObject BuildMetGreeting(int reputation)
        {
            if (reputation >= 40)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greet_very_high}Your name is spoken with honour from village to town. The Grey Wardens give praise sparingly, yet the common folk have spoken well of you. Keep your measure, and we shall count you among those to whom a charge may safely be entrusted."));
            }

            if (reputation >= 20)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greet_high}I have read your record. The rolls are clean and your conduct upright; the Grey Wardens remember such folk. The old Imperial law rewards no empty boast, only those who truly stand between the people and harm."));
            }

            if (reputation >= 5)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greet_good}Your conduct has remained proper. The Grey Wardens keep both rule and public trust; while you hold to that path, we shall receive you as one who keeps the law."));
            }

            if (reputation <= -40)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greet_very_low}Your name is no longer merely written in the case rolls. Cross the line once more, and when the Grey Wardens come before you, they shall bring more than words."));
            }

            if (reputation <= -11)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greet_wanted}Your case remains open. I speak with you today because the law of the Grey Wardens goes before the sword. Do not mistake that order for indulgence."));
            }

            if (reputation < 0)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greet_bad}Your record is not clean. The Grey Wardens leave a road back for those who will take it, but none for those who feign ignorance."));
            }

            return new TextObject(
                GwpText.Get("{=gwp_greet_neutral}The Grey Wardens remember both service and offence. Since you stand before me, I shall speak with you according to rule."));
        }

        private static void ApplyHeroEncyclopediaTexts()
        {
            foreach (var entry in HeroEncyclopediaTexts)
            {
                Hero hero = Hero.Find(entry.Key);
                if (hero == null)
                    continue;

                hero.EncyclopediaText = entry.Value;
            }
        }

        private static bool IsGreyWardenLord(Hero? hero)
        {
            if (hero == null || hero.Clan == null)
                return false;

            if (!string.Equals(hero.Clan.StringId, GwpIds.PoliceClanId, System.StringComparison.OrdinalIgnoreCase))
                return false;

            return hero.Occupation == Occupation.Lord;
        }
    }
}
