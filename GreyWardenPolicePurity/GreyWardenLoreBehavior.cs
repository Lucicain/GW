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
                ["gw_leader_0"] = GwpTextCatalog.gwp_enc_vandi,
                ["gw_leader_1"] = GwpTextCatalog.gwp_enc_yoer,
                ["gw_leader_2"] = GwpTextCatalog.gwp_enc_mise,
                ["gw_leader_3"] = GwpTextCatalog.gwp_enc_shengduo,
                ["gw_leader_4"] = GwpTextCatalog.gwp_enc_chenxi,
                ["gw_leader_5"] = GwpTextCatalog.gwp_enc_muguang
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
                GwpText.Get(GwpTextCatalog.gwp_grey_lord_followup),
                GreyLordMetGreetingCondition,
                null,
                200);
        }

        private static bool GreyLordMetGreetingCondition()
        {
            if (IsPostBattleCaptureConversation())
                return false;

            Hero? conversationHero = Hero.OneToOneConversationHero;
            if (!GwpCommon.IsGreyWardenLord(conversationHero))
                return false;

            if (GreyWardenTroopRequestBehavior
                    .IsPendingAutomaticConversation(conversationHero) ||
                GreyWardenPlayerRequestBehavior
                    .IsPendingAutomaticConversation(conversationHero))
                return false;

            if (!conversationHero.HasMet)
                return false;

            if (GwpCommon.IsEnforcementConversation())
                return false;

            if (!RollMetGreetingChance(conversationHero))
                return false;

            MBTextManager.SetTextVariable(
                GwpTextKeys.GreyLordGreeting,
                BuildMetGreeting(PlayerState.Reputation));
            return true;
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
                    GwpText.Get(GwpTextCatalog.gwp_greet_very_high));
            }

            if (reputation >= 20)
            {
                return new TextObject(
                    GwpText.Get(GwpTextCatalog.gwp_greet_high));
            }

            if (reputation >= 5)
            {
                return new TextObject(
                    GwpText.Get(GwpTextCatalog.gwp_greet_good));
            }

            if (reputation <= -40)
            {
                return new TextObject(
                    GwpText.Get(GwpTextCatalog.gwp_greet_very_low));
            }

            if (reputation <= -11)
            {
                return new TextObject(
                    GwpText.Get(GwpTextCatalog.gwp_greet_wanted));
            }

            if (reputation < 0)
            {
                return new TextObject(
                    GwpText.Get(GwpTextCatalog.gwp_greet_bad));
            }

            return new TextObject(
                GwpText.Get(GwpTextCatalog.gwp_greet_neutral));
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

    }
}
