using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 玩家与灰袍领主的婚姻（用户裁定 2026-10-14：新档与每位灰袍领主关系 100，能和任意一位灰袍领主结婚；之后给每人做故事线）。
    /// 灰袍领主对话里直接求婚、当场成婚，不走原版求爱说服和嫁妆谈判。
    /// 嫁出去的灰袍领主带走灰袍领主的战斗加成（用户裁定 2026-10-16：跟随，不要消灭），名单在这里记、进存档。
    /// </summary>
    public sealed class GwpWardenMarriageBehavior : CampaignBehaviorBase
    {
        private static GwpWardenMarriageBehavior? _instance;
        // 成婚那一刻还是灰袍领主的英雄 ID。婚后她进玩家家族，不再是灰袍家族成员，只能从这里认。
        private List<string> _marriedWardenIds = new List<string>();

        public GwpWardenMarriageBehavior() => _instance = this;

        public override void RegisterEvents()
        {
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, OnCharacterCreationIsOver);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddDialogs);
            CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnBeforeHeroesMarried);
        }

        // 关系只在新档设一次，之后照原版涨落；求婚是即时动作，没有进行中的状态。嫁出去的名单要存。
        public override void SyncData(IDataStore dataStore) =>
            dataStore.SyncData("gwp_married_warden_ids", ref _marriedWardenIds);

        /// <summary>嫁出灰袍家族、仍保留灰袍领主战斗身份的英雄。</summary>
        internal static bool IsMarriedWarden(Hero? hero) =>
            hero != null && _instance != null && _instance._marriedWardenIds.Contains(hero.StringId);

        /// <summary>
        /// 原版在换家族之前发这个事件（C#反编译 v1.4.8 MarriageAction.ApplyInternal：OnBeforeHeroesMarried 早于
        /// HandleClanChangeAfterMarriageForHero），此刻还能按家族认出灰袍领主。不管走对话求婚还是原版求爱都经过这里。
        /// </summary>
        private void OnBeforeHeroesMarried(Hero first, Hero second, bool showNotification)
        {
            foreach (Hero hero in new[] { first, second })
                if (GwpCommon.IsGreyWardenLord(hero) && !_marriedWardenIds.Contains(hero.StringId))
                    _marriedWardenIds.Add(hero.StringId);
        }

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
