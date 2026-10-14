using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 玩家与灰袍领主的婚姻（用户裁定 2026-10-14：新档与每位灰袍领主关系 100，能和任意一位灰袍领主结婚；之后给每人做故事线）。
    /// 灰袍领主对话里直接求婚、当场成婚，不走原版求爱说服和嫁妆谈判。
    /// </summary>
    public sealed class GwpWardenMarriageBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, OnCharacterCreationIsOver);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddDialogs);
        }

        // 关系只在新档设一次，之后照原版涨落；求婚是即时动作，没有进行中的状态。
        public override void SyncData(IDataStore dataStore) { }

        /// <summary>
        /// 原版两位英雄的关系落在双方族长之间（C#反编译 v1.4.8 DefaultDiplomacyModel.GetHeroesForEffectiveRelation），
        /// 所以玩家与每位灰袍领主的关系就是玩家与灰袍族长这一对。
        /// </summary>
        private static void OnCharacterCreationIsOver()
        {
            Clan? wardens = PoliceStats.GetPoliceClan();
            if (wardens?.Leader == null)
                return;
            Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(
                Hero.MainHero, wardens.Leader, out Hero player, out Hero theirs);
            player.SetPersonalRelation(theirs, Campaign.Current.Models.DiplomacyModel.MaxRelationLimit);
        }

        private static void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("gwp_warden_marry_ask", "hero_main_options", "gwp_warden_marry_yes",
                GwpText.Get(GwpTextCatalog.gwp_warden_marry_ask),
                CanPropose, null, 120);
            starter.AddDialogLine("gwp_warden_marry_yes", "gwp_warden_marry_yes", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_warden_marry_yes),
                null, Marry, 120);
        }

        /// <summary>
        /// 原版不让两位族长成婚（C#反编译 v1.4.8 DefaultMarriageModel.IsCoupleSuitableForMarriage），玩家永远是族长，
        /// 所以对灰袍族长只看其余条件；成婚前把族长位子交给另一位灰袍领主（<see cref="Marry"/>）。没有人可以接位时不提供求婚。
        /// </summary>
        private static bool CanPropose()
        {
            Hero? warden = Hero.OneToOneConversationHero;
            if (!GwpCommon.IsGreyWardenLord(warden))
                return false;
            MarriageModel model = Campaign.Current.Models.MarriageModel;
            if (warden!.Clan.Leader != warden)
                return model.IsCoupleSuitableForMarriage(Hero.MainHero, warden);
            Hero? courted = Romance.GetCourtedHeroInOtherClan(Hero.MainHero, warden);
            return Successor(warden) != null && Hero.MainHero.IsFemale != warden.IsFemale &&
                   Hero.MainHero.CanMarry() && warden.CanMarry() && (courted == null || courted == warden);
        }

        /// <summary>
        /// 求的是灰袍族长：先照原版换族长（ChangeClanLeaderAction 把族长身上的钱交给新族长，灰袍公库就是族长的钱，
        /// 见 PoliceResourceManager.GetJudicialTreasuryBalance），再成婚。她随原版婚姻进玩家家族。
        /// </summary>
        private static void Marry()
        {
            Hero warden = Hero.OneToOneConversationHero;
            if (warden.Clan.Leader == warden)
                ChangeClanLeaderAction.ApplyWithSelectedNewLeader(warden.Clan, Successor(warden)!);
            MarriageAction.Apply(Hero.MainHero, warden);
        }

        /// <summary>接族长位子的人：本家族另一位活着的成年灰袍领主，创始领主按编号先。</summary>
        private static Hero? Successor(Hero leaving) =>
            leaving.Clan.Heroes
                .Where(hero => hero != leaving && hero.IsAlive && !hero.IsChild && GwpCommon.IsGreyWardenLord(hero))
                .OrderBy(hero => hero.StringId, System.StringComparer.Ordinal)
                .FirstOrDefault();
    }
}
