using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
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
        private static readonly Dictionary<string, TextObject> HeroEncyclopediaTexts =
            new Dictionary<string, TextObject>
            {
                ["gw_leader_0"] = new TextObject(
                    "{=gwp_enc_vandi}梵蒂是现任灰袍总长，也是统一帝国旧警制在当世最直接的继承者之一。她将灰袍视作法统与誓约的共同体，而非寻常贵族家门。对外，她坚持灰袍不争王位、不扩领地，只守道路、村镇与市集的秩序；对内，她要求每一名灰袍之女都记住，执法的目的不是逞威，而是让百姓敢于相信明日仍有公道。"),
                ["gw_leader_1"] = new TextObject(
                    "{=gwp_enc_yoer}约珥长期负责旧帝国驿道与乡野巡察。她最常出现在边远道路、渡口与商旅往来的要冲，处理盗匪、私斗与沿途勒索。许多行商与村民先记住她的灰袍，再记住她的名字。对平民而言，她代表的是一种朴素却可靠的事实：帝国虽然已经崩塌，但仍有人在守路。"),
                ["gw_leader_2"] = new TextObject(
                    "{=gwp_enc_mise}弥瑟主管灰袍内部的案卷、赎罪与归正事务。她坚信秩序若只剩惩罚，迟早会沦为恐惧；因此灰袍必须留下让罪人回头的门。许多关于罚金、押送、赎罪和调停的旧规，都是由她整理并续行的。她使灰袍看起来不像冷硬的军警，更像一支持戒而行的世俗修会。"),
                ["gw_leader_3"] = new TextObject(
                    "{=gwp_enc_shengduo}圣铎负责保存统一帝国时代遗留下来的法条、判例与巡察记录。她极少高谈理想，却能在最短时间内从旧卷宗里找出某项惯例的根源。对灰袍而言，档案不仅是文书，更是证明她们并非乱世私兵的依据：帝国失国，不等于法律失声。"),
                ["gw_leader_4"] = new TextObject(
                    "{=gwp_enc_chenxi}晨曦常被派往受灾村镇、饥荒边地和战后余波最重的地区。她擅长安抚、分配、调停和维持最基础的公共秩序，因此在百姓中的名声尤为温和。很多人第一次理解灰袍，并不是因为看见她们缉捕罪犯，而是因为看见她们在最混乱的时候，仍先替普通人把日子重新扶正。"),
                ["gw_leader_5"] = new TextObject(
                    "{=gwp_enc_muguang}暮光统率灰袍中最强硬的外勤执法力量，常负责追缉拒捕者、押送重犯和震慑屡教不改之徒。她的名声往往先在案犯之间流传，再传到酒馆里。即便如此，她仍被灰袍内部视作守规矩的人，因为她相信刀剑只能替法律开路，不能代替法律本身。")
            };

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            ApplyHeroEncyclopediaTexts();
            RegisterMetLordGreeting(starter);
        }

        private static void RegisterMetLordGreeting(CampaignGameStarter starter)
        {
            starter.AddDialogLine(
                "gwp_grey_lord_met_greeting",
                "start",
                "lord_talk_speak_diplomacy_2",
                "{" + GwpTextKeys.GreyLordGreeting + "}",
                GreyLordMetGreetingCondition,
                null,
                200);
        }

        private static bool GreyLordMetGreetingCondition()
        {
            Hero? conversationHero = Hero.OneToOneConversationHero;
            if (!IsGreyWardenLord(conversationHero))
                return false;

            if (!conversationHero.HasMet)
                return false;

            if (IsPoliceInteractionConversation())
                return false;

            MBTextManager.SetTextVariable(
                GwpTextKeys.GreyLordGreeting,
                BuildMetGreeting(PlayerBehaviorPool.Reputation));
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

            PoliceTask? task = CrimePool.GetTask(conversationParty.StringId);
            return task?.TargetCrime?.Offender?.IsMainParty == true;
        }

        private static TextObject BuildMetGreeting(int reputation)
        {
            if (reputation >= 40)
            {
                return new TextObject(
                    "{=gwp_greet_very_high}你的名字在村镇间传得很正。灰袍不会轻许赞誉，但百姓替你说了不少好话。只要你仍守得住分寸，我们便把你当可以托付的人。");
            }

            if (reputation >= 20)
            {
                return new TextObject(
                    "{=gwp_greet_high}我看过你的记录。案卷干净，行迹也正；这样的人，灰袍记得。帝国旧法并不奖赏空话，只承认真正替百姓挡事的人。");
            }

            if (reputation >= 5)
            {
                return new TextObject(
                    "{=gwp_greet_good}你的行止还算端正。灰袍守的是规矩，也是人心；既然你还在这条线上，我们便按守法之人待你。");
            }

            if (reputation <= -40)
            {
                return new TextObject(
                    "{=gwp_greet_very_low}你的名字已经不只是写在案卷里了。若你再越线，灰袍来见你时，带来的就不会只是言语。");
            }

            if (reputation <= -11)
            {
                return new TextObject(
                    "{=gwp_greet_wanted}你仍在案。今日我肯与你说话，是因为灰袍的法先于刀兵；别把这当成宽纵。");
            }

            if (reputation < 0)
            {
                return new TextObject(
                    "{=gwp_greet_bad}你的记录并不干净。灰袍会给人回头的机会，但不会给人装糊涂的余地。");
            }

            return new TextObject(
                "{=gwp_greet_neutral}灰袍记人，不只记功，也记过。你既来到我面前，我便按规矩与你说话。");
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
