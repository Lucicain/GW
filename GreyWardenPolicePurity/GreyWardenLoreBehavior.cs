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

        private static readonly Dictionary<string, string> HeroEncyclopediaTemplates =
            new Dictionary<string, string>
            {
                ["gw_leader_0"] = "{=gwp_enc_vandi}{HERO_NAME} is the present Warden-General and one of the clearest living heirs to the constabulary of the old, undivided Empire. She is often found in the drill yard before dawn, correcting a shield line or making veterans teach recruits why discipline matters more than display. Beyond their walls she forbids the Grey Wardens to seek crowns or widen estates; within, she insists that every sworn daughter be made ready to hold the road when law must be defended by force.",
                ["gw_leader_1"] = "{=gwp_enc_yoer}{HERO_NAME} has long kept watch over the post roads and countryside of the old Empire. She is most often found upon distant tracks, ferries, and the crossings of merchants, settling brigandage, blood-feuds, and unlawful tolls. Many traders and villagers remember the grey mantle before they learn her name. To them she embodies a plain and steadfast truth: the Empire has broken, yet someone still keeps the road.",
                ["gw_leader_2"] = "{=gwp_enc_mise}{HERO_NAME} rides most often where villagers have been struck on the road or where smoke rises above the fields. She remembers the names of burned hamlets and measures the law by whether ordinary families can work and travel without becoming spoils of war. Those who prey upon peasants have learned that her quiet manner ends the moment a village is threatened.",
                ["gw_leader_3"] = "{=gwp_enc_shengduo}{HERO_NAME} preserves the statutes and judgments inherited from the old, undivided Empire, but she seldom remains long beside the archive. Village headmen, town merchants, artisans, and even the harder figures of the alleys have seen her arrive, hear an unresolved petition, and stay until its cause is withdrawn or settled. She holds that an old law proves its worth only when it can still quiet a living grievance.",
                ["gw_leader_4"] = "{=gwp_enc_chenxi}{HERO_NAME} is often sent where villages have suffered disaster, famine grips the marches, or war has left its deepest scars. She is skilled in relief, allotment, mediation, and the restoration of the simplest public order, and the common folk speak gently of her. Many first understand the Grey Wardens not when they see a criminal seized, but when they see ordinary lives set upright again amid the worst confusion.",
                ["gw_leader_5"] = "{=gwp_enc_muguang}{HERO_NAME} keeps an uncommon watch for petitions that do not fit the old rolls, especially those carried directly to the Grey Wardens by travellers whose deeds have altered the realm. She is slow to promise what precedent has not yet defined, yet rarely dismisses a request merely because no former clerk imagined it. Among the six, she is the one most often asked what the law should become next."
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
                GwpText.Get("{=gwp_grey_lord_followup}All right. Tell me what you need."),
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

            if (GreyWardenTroopRequestBehavior
                    .IsPendingAutomaticConversation(conversationHero) ||
                GreyWardenPlayerRequestBehavior
                    .IsPendingAutomaticConversation(conversationHero))
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
                    GwpText.Get("{=gwp_greet_very_high}I have heard a lot of good things about you. Villagers and merchants say you help when you can and respect the rules. Keep that up, and the Grey Wardens will trust you with more important work."));
            }

            if (reputation >= 20)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greet_high}I have read your record. You have consistently helped victims without causing trouble for us. The Grey Wardens remember that kind of work."));
            }

            if (reputation >= 5)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greet_good}Your record is good. Keep following the rules and helping people who need it, and we will treat you as someone we can trust."));
            }

            if (reputation <= -40)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greet_very_low}Your situation is already serious. If you keep breaking the law, the next Grey Warden you meet will take action instead of offering another warning."));
            }

            if (reputation <= -11)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greet_wanted}The case involving you is still open. I can hear your explanation, but that does not mean the matter is over."));
            }

            if (reputation < 0)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greet_bad}There are problems in your record. If you are willing to correct them, the Grey Wardens will still give you that chance."));
            }

            return new TextObject(
                GwpText.Get("{=gwp_greet_neutral}We keep track of what people do, both the good and the bad. At the moment, your record gives me no reason to turn you away."));
        }

        private static void ApplyHeroEncyclopediaTexts()
        {
            foreach (var entry in HeroEncyclopediaTemplates)
            {
                Hero hero = Hero.Find(entry.Key);
                if (hero == null)
                    continue;

                hero.EncyclopediaText = new TextObject(
                    GwpText.Get(entry.Value, "HERO_NAME", hero.Name));
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
