using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Helpers;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 玩家可以从自己队里点出一队灰袍，交给他们两件事：替自己去复命，或者去把求援的话
    /// 带到。派出去的是真部队——归玩家家族，在地图上看得见，路上会遇敌，钱粮会消耗，
    /// 也可能全军覆没。办完事回来，人、钱、粮、物和路上捡到的东西全部交还。
    /// </summary>
    public sealed class GwpWardenDispatchBehavior : CampaignBehaviorBase
    {
        internal const string DispatchPartyPrefix = "gwp_dispatch_";
        // 原版护送会把队伍带到目标身边但不会重叠，判定距离要留出这段站位余量。
        private const float HandoverDistance = 3f;
        private const float DeliveryDistance = 3f;
        private const int StartingFoodDays = 12;
        /// <summary>多久没真的靠近过收件人就算走不动了。</summary>
        private const float StallPatienceHours = 12f;
        /// <summary>小于这个数的靠近算噪声，不算进展。</summary>
        private const float StallProgressEpsilon = 1f;
        /// <summary>回程最后这一段路上，这支队伍不再被任何人撞上。</summary>
        private const float FinalApproachDistance = 15f;
        /// <summary>路上的盘缠：每人这么多第纳尔，与案件款分开，专供买粮。</summary>
        private const int TravelPursePerMan = 120;
        /// <summary>新目标要比手上这个近这么多，才值得改道；否则来回跳。</summary>
        private const float RetargetHysteresis = 25f;
        /// <summary>主动性设定的保持时长；每小时续期一次，覆盖两次续期之间的间隔。</summary>
        private const float CourierInitiativeHours = 6f;

        private readonly List<GwpDispatchRecord> _dispatches = new List<GwpDispatchRecord>();
        private string _dispatchState = string.Empty;

        internal static GwpWardenDispatchBehavior? Instance =>
            Campaign.Current?.GetCampaignBehavior<GwpWardenDispatchBehavior>();

        internal static bool IsDispatchParty(MobileParty? party) =>
            party?.StringId?.StartsWith(DispatchPartyPrefix, StringComparison.Ordinal) == true;

        internal bool HasActiveDispatch(GwpDispatchPurpose purpose) =>
            _dispatches.Any(d => d.Purpose == purpose && FindParty(d.PartyId) != null);

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }

        public override void SyncData(IDataStore dataStore) =>
            GwpLoadFaultWatch.Guard("DISPATCH_SYNC", () => SyncDispatchData(dataStore));

        private void SyncDispatchData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
                _dispatchState = string.Join(";", _dispatches.Select(d => d.Serialize()));
            dataStore.SyncData("gwp_dispatch_state", ref _dispatchState);
            if (!dataStore.IsLoading) return;
            _dispatches.Clear();
            foreach (string line in (_dispatchState ?? string.Empty).Split(';'))
            {
                GwpDispatchRecord? record = GwpDispatchRecord.Deserialize(line);
                if (record != null) _dispatches.Add(record);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter) =>
            GwpLoadFaultWatch.Guard("DISPATCH_SESSION_LAUNCH", () => StartDispatchSession(starter));

        private void StartDispatchSession(CampaignGameStarter starter)
        {
            // 静态状态跨读档留在进程里。会话启动时先撤干净，免得上一局遗留的武装状态
            // 把新一局的第一场对话劫持掉。
            GwpWardenDispatchDialogue.ResetRuntimeState();
            GwpWardenDispatchDialogue.Register(starter);
            foreach (GwpDispatchRecord record in _dispatches.ToList())
            {
                MobileParty? party = FindParty(record.PartyId);
                if (party == null) { _dispatches.Remove(record); continue; }
                TrackOnMap(party);
            }
        }

        private void OnConversationEnded(IEnumerable<CharacterObject> characters)
        {
            _ = characters;
            GwpWardenDispatchDialogue.OnConversationEnded();
        }

        private void OnPartyDestroyed(MobileParty party, PartyBase? destroyer)
        {
            GwpDispatchRecord? record = _dispatches.FirstOrDefault(d =>
                string.Equals(d.PartyId, party?.StringId, StringComparison.OrdinalIgnoreCase));
            if (record == null) return;
            _dispatches.Remove(record);
            GwpAiDiagnostics.WriteAction(party, "DISPATCH_LOST",
                "purpose=" + record.Purpose + "; phase=" + record.Phase +
                "; caseGold=" + record.CaseGoldFloor +
                "; destroyer=" + (destroyer?.MobileParty?.StringId ?? destroyer?.Settlement?.StringId ?? "-"));
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_dispatch_lost}The detachment you sent has been destroyed. Whatever it carried is gone with it."),
                Colors.Red));
        }

        #region 派遣

        /// <summary>
        /// 真正把人分出去。兵员、俘虏、钱和启动粮都从玩家队里实扣，不是凭空造的。
        /// </summary>
        internal MobileParty? Dispatch(
            TroopRoster detachment,
            TroopRoster prisoners,
            GwpDispatchPurpose purpose,
            int carriedCaseGold,
            bool reportLie,
            string prisonerHeroId)
        {
            MobileParty player = MobileParty.MainParty;
            if (player?.IsActive != true || detachment.TotalManCount <= 0) return null;
            Settlement? home = player.CurrentSettlement ??
                GwpCommon.FindNearestTown(player.GetPosition2D);
            MobileParty? receiver = home == null ? null : FindReceiver(player, player);
            if (receiver == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_dispatch_no_receiver}There is no Grey Warden party abroad that your men could reach. Keep them with you for now."),
                    Colors.Yellow));
                return null;
            }

            if (!CanProvision(detachment.TotalManCount, player))
            {
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_dispatch_no_rations}You have neither food nor coin to send with them. Lay in provisions before you send anyone out."),
                    Colors.Yellow));
                GwpAiDiagnostics.WriteFieldArrest("DISPATCH_REFUSED_NO_PROVISIONS",
                    "men=" + detachment.TotalManCount +
                    "; playerFood=" + AvailableFood(player) +
                    "; playerGold=" + Hero.MainHero.Gold);
                return null;
            }

            // 英雄俘虏不能直接塞进新队的名册：他本人的关押归属是另一套状态。
            // 先让他回到主队，建好队伍之后再走原版的转移。
            var heroPrisoners = prisoners.GetTroopRoster()
                .Where(entry => entry.Character?.IsHero == true && entry.Number > 0)
                .Select(entry => entry.Character).ToList();
            foreach (CharacterObject hero in heroPrisoners)
            {
                prisoners.AddToCounts(hero, -1);
                player.PrisonRoster.AddToCounts(hero, 1);
            }

            try
            {
                MobileParty party = CustomPartyComponent.CreateCustomPartyWithTroopRoster(
                    player.Position,
                    0.5f,
                    home,
                    BuildDispatchName(purpose),
                    Clan.PlayerClan,
                    detachment,
                    prisoners,
                    Hero.MainHero,
                    string.Empty,
                    string.Empty,
                    0f,
                    false);

                party.StringId = DispatchPartyPrefix + MBRandom.RandomInt(100000, 999999);
                party.ActualClan = Clan.PlayerClan;
                KeepCourierDisposition(party);
                foreach (CharacterObject hero in heroPrisoners)
                {
                    try { TransferPrisonerAction.Apply(hero, player.Party, party.Party); }
                    catch (Exception transferError)
                    {
                        GwpAiDiagnostics.WriteFieldArrest("DISPATCH_PRISONER_LOAD_FAILED",
                            transferError.ToString());
                    }
                }
                TakeRationsFromPlayer(party, player);
                if (carriedCaseGold > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, carriedCaseGold, true);
                    party.PartyTradeGold += carriedCaseGold;
                }

                var record = new GwpDispatchRecord
                {
                    PartyId = party.StringId,
                    Purpose = purpose,
                    Phase = GwpDispatchPhase.Outbound,
                    ReceiverPartyId = receiver.StringId,
                    CaseGoldFloor = Math.Max(0, carriedCaseGold),
                    ReportLie = reportLie,
                    PrisonerHeroId = prisonerHeroId ?? string.Empty,
                    DispatchedHours = CampaignTime.Now.ToHours
                };
                ResetProgress(record, party, receiver);
                _dispatches.Add(record);
                TrackOnMap(party);
                SendTo(party, receiver);

                GwpAiDiagnostics.WriteAction(party, "DISPATCH_SENT",
                    "purpose=" + purpose + "; men=" + party.MemberRoster.TotalManCount +
                    "; prisoners=" + party.PrisonRoster.TotalManCount +
                    "; caseGold=" + carriedCaseGold + "; lie=" + reportLie +
                    "; receiver=" + receiver.StringId);
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_dispatch_sent}Your detachment has set out to find {VAR_1}.",
                    "VAR_1", receiver.Name.ToString()), Colors.Cyan));
                return party;
            }
            catch (Exception ex)
            {
                GwpAiDiagnostics.WriteFieldArrest("DISPATCH_FAILED", ex.ToString());
                return null;
            }
        }

        private static TextObject BuildDispatchName(GwpDispatchPurpose purpose) =>
            GwpText.Create(purpose == GwpDispatchPurpose.Report
                ? "{=gwp_dispatch_name_report}Grey Warden courier detail"
                : "{=gwp_dispatch_name_support}Grey Warden request rider");

        /// <summary>
        /// 出发的口粮从玩家自己的辎重里分，不凭空生成；玩家没有就带不走。之后靠他们
        /// 自己在路上买——那笔钱只能是他们打劫匪挣来的，绝不动随身的案件款。
        /// </summary>
        private static void TakeRationsFromPlayer(MobileParty party, MobileParty player)
        {
            int men = Math.Max(1, party.MemberRoster.TotalManCount);
            int wanted = Math.Max(1, (int)Math.Ceiling(men / 20f * StartingFoodDays));
            foreach (ItemRosterElement element in player.ItemRoster.ToList())
            {
                if (wanted <= 0) break;
                ItemObject? item = element.EquipmentElement.Item;
                if (item?.IsFood != true || element.Amount <= 0) continue;
                int taken = Math.Min(element.Amount, wanted);
                player.ItemRoster.AddToCounts(element.EquipmentElement, -taken);
                party.ItemRoster.AddToCounts(element.EquipmentElement, taken);
                wanted -= taken;
            }

            // 带不够就给盘缠，让他们自己路上买。
            //
            // 这是"出门就断粮"的真正原因：他们身上唯一的钱是玩家托付的案件款，
            // 而那笔钱一个子儿都不许动（CaseGoldFloor），于是 BuyFoodIfNeeded 里
            // 可用余额永远是 0——原版进城买粮的欲望就算跑赢了，到了城里也买不起。
            // 盘缠与案件款分开记，路上花剩的照样带回来。
            int purse = TravelPurseFor(men, wanted);
            if (purse > 0 && Hero.MainHero.Gold >= purse)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, purse, true);
                party.PartyTradeGold += purse;
            }

            GwpAiDiagnostics.WriteAction(party, "DISPATCH_RATIONS",
                "men=" + men + "; food=" + party.ItemRoster.TotalFood.ToString(
                    "0.0", System.Globalization.CultureInfo.InvariantCulture) +
                "; shortBy=" + wanted + "; purse=" + purse +
                "; playerGold=" + Hero.MainHero.Gold);
        }

        /// <summary>带不满的口粮折成盘缠；至少够买几天的粮。</summary>
        private static int TravelPurseFor(int men, int shortBy) =>
            shortBy <= 0 ? 0 : Math.Max(TravelPursePerMan, shortBy * TravelPursePerMan / Math.Max(1, men));

        /// <summary>一支这么大的队伍出这趟门要带多少口粮。</summary>
        private static int RationsWantedFor(int men) =>
            Math.Max(1, (int)Math.Ceiling(Math.Max(1, men) / 20f * StartingFoodDays));

        /// <summary>玩家手上能匀出来的口粮。</summary>
        private static int AvailableFood(MobileParty player)
        {
            int total = 0;
            foreach (ItemRosterElement element in player.ItemRoster)
                if (element.EquipmentElement.Item?.IsFood == true && element.Amount > 0)
                    total += element.Amount;
            return total;
        }

        /// <summary>
        /// 粮和钱都拿不出来就别让他们出门。饿着肚子上路只有一个结局：半路散了，
        /// 玩家的人、钱、要交的人一起没。宁可当场退回，让玩家先去备点东西。
        /// </summary>
        private static bool CanProvision(int men, MobileParty player)
        {
            if (AvailableFood(player) > 0) return true;
            int purse = TravelPurseFor(men, RationsWantedFor(men));
            return purse > 0 && Hero.MainHero.Gold >= purse;
        }

        /// <summary>
        /// 派出去的人和灰袍领主用同一套下注方式：巡逻类候选由欲望系统统一压到最低，
        /// 其余原版欲望一分不动，我们自己只加**一个**任务欲望。所以缺粮、疗伤、卖货这些
        /// 原版欲望真到了该办的时候，会正常压过这趟差事，办完再回来赶路。
        /// 行为选的是全速直扑目标那一档——护送会跟着对方的步子走，办差的人不该这么慢。
        /// </summary>
        private static void SendTo(MobileParty party, MobileParty? target)
        {
            if (party?.IsActive != true || target?.IsActive != true || party == target) return;
            KeepCourierDisposition(party);
            GreyWardenPartyDesireBehavior.RequestRush(party, target);
        }

        /// <summary>
        /// 送信队的性子：不主动接战、遇险就躲。原版短期主动性每小时都会重新决定要不要
        /// 扑上去或者绕开，一支十来个人的队伍拿默认值就会一路追野怪、走走停停，看上去
        /// 摇摆不定，还常常把自己打残。这里把它按信使该有的样子设定，长期保持。
        /// </summary>
        /// <summary>
        /// 办差的队伍要专心。原版短期主动性每小时重新决定扑上去还是绕开，路上看见劫匪
        /// 就想追，走走停停还常把自己打残。这里按信使该有的样子设定：不主动接战、遇险
        /// 就躲；挨打时原版照样自卫。
        /// </summary>
        private static void KeepCourierDisposition(MobileParty party)
        {
            try { party.Ai.SetInitiative(0f, 1f, CourierInitiativeHours); }
            catch { }
        }

        private static void TrackOnMap(MobileParty party)
        {
            try
            {
                VisualTrackerManager? tracker = Campaign.Current?.VisualTrackerManager;
                if (tracker == null) return;
                if (party.IsCurrentlyUsedByAQuest && tracker.CheckTracked(party)) return;

                // MapTrackerProvider.CanAddMobileParty 的接受条件（反编译 IL 逐分支确认）是
                // 三条同时成立：无英雄领队、IsCurrentlyUsedByAQuest、且已在视觉追踪里。
                // 顺序要紧：先登记视觉追踪，再翻转任务占用——后者会派发
                // OnMobilePartyQuestStatusChanged，提供者就在那一刻重新评估。
                if (!tracker.CheckTracked(party)) tracker.RegisterObject(party);
                if (!party.IsCurrentlyUsedByAQuest) party.SetPartyUsedByQuest(true);
            }
            catch (Exception ex)
            {
                GwpAiDiagnostics.WriteFieldArrest("DISPATCH_TRACK_FAILED", ex.ToString());
            }
        }

        private static void UntrackOnMap(MobileParty party)
        {
            try
            {
                if (party.IsCurrentlyUsedByAQuest) party.SetPartyUsedByQuest(false);
                Campaign.Current?.VisualTrackerManager?.RemoveTrackedObject(party, true);
            }
            catch { }
        }

        /// <summary>
        /// 派出去的人遇到险情要说一声：被人堵上了、伤亡过半、或者已经断粮。
        /// 同一种险情只报一次，不在同一趟路上反复刷屏。
        /// </summary>
        private void WarnIfInTrouble(GwpDispatchRecord record, MobileParty party)
        {
            string trouble = DescribeTrouble(party);
            if (string.Equals(record.LastWarning, trouble, StringComparison.Ordinal)) return;
            record.LastWarning = trouble;
            if (trouble.Length == 0) return;

            GwpAiDiagnostics.WriteAction(party, "DISPATCH_IN_TROUBLE",
                "trouble=" + trouble + "; men=" + party.MemberRoster.TotalManCount +
                "; wounded=" + party.MemberRoster.TotalWoundedRegulars +
                "; food=" + party.ItemRoster.TotalFood.ToString(
                    "0.0", System.Globalization.CultureInfo.InvariantCulture));

            string text = trouble switch
            {
                "battle" => GwpText.Get(
                    "{=gwp_dispatch_trouble_battle}Word reaches you: the men you sent have been brought to battle."),
                "mauled" => GwpText.Get(
                    "{=gwp_dispatch_trouble_mauled}Word reaches you: the men you sent have been badly cut up."),
                _ => GwpText.Get(
                    "{=gwp_dispatch_trouble_starving}Word reaches you: the men you sent have run out of food.")
            };
            InformationManager.DisplayMessage(new InformationMessage(text, Colors.Red));
        }

        private static string DescribeTrouble(MobileParty party)
        {
            if (party.MapEvent != null) return "battle";
            int men = party.MemberRoster.TotalManCount;
            if (men > 0 && party.MemberRoster.TotalWoundedRegulars * 2 >= men) return "mauled";
            if (party.ItemRoster.TotalFood <= 0f) return "starving";
            return string.Empty;
        }

        /// <summary>
        /// 送信的队伍能不能真的走到那个人跟前。
        ///
        /// 这一条不是保险，是必需的。引擎在 <c>MobileParty.GetTargetCampaignPosition</c> 里，
        /// 对"跟着某支队伍走"这种走法有一道硬闸：目标所在的位置若不合本队的通行方式
        /// （<c>NavigationHelper.IsPositionValidForNavigationType</c>），它不报错、不改行为，
        /// 而是把本帧的目的地直接换成**自己脚下**。表现就是队伍停在原地一步不挪，欲望、
        /// 目标、速度在日志里却全是对的。灰袍领主是会上船出海的（见
        /// <c>PoliceResourceManager.GivePoliceShips</c>），一支没有船的送信队盯上一个在海上的
        /// 收件人，就会这样永远钉死在出发点。
        /// </summary>
        private static bool CanReach(MobileParty courier, MobileParty target)
        {
            if (courier?.IsActive != true || target?.IsActive != true) return false;
            try
            {
                return NavigationHelper.IsPositionValidForNavigationType(
                    target.Position, courier.NavigationCapability);
            }
            catch { return !target.IsCurrentlyAtSea; }
        }

        /// <summary>
        /// 最近的、而且**走得到**的灰袍领主队伍。走不到的一概不选：宁可当场告诉玩家
        /// 没人可送，也不要派一支队伍出去钉在原地。
        /// </summary>
        private static MobileParty? FindReceiver(MobileParty from, MobileParty? courier = null)
        {
            Clan? wardens = PoliceStats.GetPoliceClan();
            if (wardens == null) return null;
            MobileParty traveller = courier ?? from;
            return MobileParty.All
                .Where(p => p?.IsActive == true && p.LeaderHero != null &&
                            p.MapFaction != null &&
                            string.Equals(p.ActualClan?.StringId, wardens.StringId,
                                StringComparison.OrdinalIgnoreCase) &&
                            CanReach(traveller, p))
                .OrderBy(p => p.GetPosition2D.Distance(from.GetPosition2D))
                .FirstOrDefault();
        }

        #endregion

        #region 行程

        private void OnHourlyTick() =>
            GwpLoadFaultWatch.Guard("DISPATCH_HOURLY", AdvanceDispatches);

        private void AdvanceDispatches()
        {
            foreach (GwpDispatchRecord record in _dispatches.ToList())
            {
                MobileParty? party = FindParty(record.PartyId);
                if (party == null) { _dispatches.Remove(record); continue; }

                TrackOnMap(party);
                WarnIfInTrouble(record, party);
                if (party.MapEvent != null) continue;
                KeepCourierDisposition(party);

                if (record.Phase == GwpDispatchPhase.Outbound) AdvanceOutbound(record, party);
                else AdvanceReturn(record, party);
            }
        }

        /// <summary>
        /// 原版 <c>AiVisitSettlementBehavior</c> 只服务小势力／王国势力的部队，或者有
        /// IsLord 领主的部队；无领主的玩家家族队一条进城欲望都拿不到。曾试过给派遣队配
        /// 一名临时队长去满足它，结果三头落空：地图标记那条路恰恰要求
        /// <c>LeaderHero == null</c>（见 MapTrackerProvider.CanAddMobileParty），标记直接没了；
        /// 多出来的假英雄可以对话、还占玩家家族的位置；原版进城欲望又把队伍钉在村里。
        /// 因此回到这条细适配：只补"决定进城"这一个欲望，行为用原版 <c>GoToSettlement</c>、
        /// 按 `0.99` 与其他原版欲望同场竞价。进城之后卖俘虏仍由原版
        /// <c>PartiesSellPrisonerCampaignBehavior</c> 自己完成。
        ///
        /// 硬边界：**押着本案目标时一律不进城**，那个人要押去交差，不能被当普通俘虏处置。
        /// </summary>
        private bool TryHandleTownBusiness(GwpDispatchRecord record, MobileParty party)
        {
            if (!string.IsNullOrEmpty(record.PrisonerHeroId)) return false;

            bool hasPrisoners = party.PrisonRoster.TotalManCount > 0;
            bool hungry = party.ItemRoster.TotalFood <
                          party.MemberRoster.TotalManCount * 1.5f;
            // 缺粮就该进城，不该先问"买得起吗"。买不起还有卖俘虏、卖战利品这条路，
            // 原来那个"余额大于零才算缺粮"的条件把断粮的队伍直接钉在野外。
            if (!hasPrisoners && !hungry) return false;

            Settlement? town = FindTradeTown(party);
            if (town == null) return false;

            if (party.CurrentSettlement == town)
            {
                BuyFoodIfNeeded(record, party);
                return true;
            }

            GreyWardenPartyDesireBehavior.RequestVisit(party, town);
            GwpAiDiagnostics.WriteAction(party, "DISPATCH_TOWN_BUSINESS",
                "town=" + town.StringId + "; prisoners=" + party.PrisonRoster.TotalManCount +
                "; food=" + party.ItemRoster.TotalFood.ToString(
                    "0.0", System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }

        /// <summary>能做买卖的最近城镇：不在围城中，且与我方不处于交战。</summary>
        private static Settlement? FindTradeTown(MobileParty party)
        {
            IFaction? ours = party.MapFaction;
            return Settlement.All
                .Where(settlement => settlement.IsTown &&
                                     settlement.SiegeEvent == null &&
                                     settlement.MapFaction != null &&
                                     (ours == null || !settlement.MapFaction.IsAtWarWith(ours)))
                .OrderBy(settlement => settlement.GetPosition2D.Distance(party.GetPosition2D))
                .FirstOrDefault();
        }

        /// <summary>
        /// 进城之后按原版市价真实买粮。原版 PartiesBuyFoodCampaignBehavior 硬性要求领主
        /// 英雄，无领主队走不到那条路，这一段由我们补。只花受保护额之上的钱。
        /// </summary>
        private static void BuyFoodIfNeeded(GwpDispatchRecord record, MobileParty party)
        {
            Settlement? settlement = party.CurrentSettlement;
            if (settlement?.ItemRoster == null) return;
            if (party.ItemRoster.TotalFood > party.MemberRoster.TotalManCount * 2f) return;

            int spendable = Math.Max(0, party.PartyTradeGold - record.CaseGoldFloor);
            if (spendable <= 0) return;

            foreach (ItemRosterElement element in settlement.ItemRoster.ToList())
            {
                ItemObject? item = element.EquipmentElement.Item;
                if (item?.IsFood != true || element.Amount <= 0) continue;
                int price = settlement.Town != null
                    ? settlement.Town.GetItemPrice(item, party, false)
                    : Math.Max(1, item.Value);
                if (price <= 0 || price > spendable) continue;
                int affordable = Math.Min(element.Amount, spendable / price);
                if (affordable <= 0) continue;
                try
                {
                    SellItemsAction.Apply(settlement.Party, party.Party,
                        element, affordable, settlement);
                    spendable = Math.Max(0, party.PartyTradeGold - record.CaseGoldFloor);
                    GwpAiDiagnostics.WriteAction(party, "DISPATCH_BOUGHT_FOOD",
                        "item=" + item.StringId + "; amount=" + affordable + "; price=" + price);
                }
                catch { }
                if (party.ItemRoster.TotalFood > party.MemberRoster.TotalManCount * 2f) break;
            }
        }

        private void AdvanceOutbound(GwpDispatchRecord record, MobileParty party)
        {
            // 送信的人不认死一个收件人。灰袍一直在动，谁这会儿最近就找谁——
            // 出发时选的那个跑远了，半路上遇见另一个更近的，当然是给他。
            MobileParty? receiver = FindReceiver(party, party);
            if (receiver == null)
            {
                // 一个走得到的灰袍都没有：带着东西回来，不把玩家的人和钱丢在路上。
                BeginReturn(record, party, "no_receiver");
                return;
            }
            if (!string.Equals(receiver.StringId, record.ReceiverPartyId,
                    StringComparison.OrdinalIgnoreCase))
            {
                // 几个灰袍挤在差不多远的地方时，"最近的那个"每小时都会换一次，队伍就
                // 在原地来回改道。新目标要明显更近才值得改，否则认准手上这个走完。
                MobileParty? current = FindParty(record.ReceiverPartyId);
                float here = party.GetPosition2D.Distance(receiver.GetPosition2D);
                bool worthIt = current?.IsActive != true || current.LeaderHero == null ||
                               !CanReach(party, current) ||
                               here < party.GetPosition2D.Distance(current.GetPosition2D)
                                   - RetargetHysteresis;
                if (worthIt)
                {
                    GwpAiDiagnostics.WriteAction(party, "DISPATCH_RETARGET",
                        "from=" + (record.ReceiverPartyId ?? "-") + "; to=" + receiver.StringId +
                        "; distance=" + here.ToString(
                            "0.0", System.Globalization.CultureInfo.InvariantCulture));
                    record.ReceiverPartyId = receiver.StringId;
                    ResetProgress(record, party, receiver);
                }
                else receiver = current!;
            }

            float distance = party.GetPosition2D.Distance(receiver.GetPosition2D);
            if (distance > DeliveryDistance)
            {
                if (!HasStalled(record, party, receiver, distance) &&
                    !TryHandleTownBusiness(record, party))
                    SendTo(party, receiver);
                return;
            }

            DeliverTo(record, party, receiver);
        }

        private static void ResetProgress(GwpDispatchRecord record, MobileParty party,
            MobileParty receiver)
        {
            record.LastDistance = party.GetPosition2D.Distance(receiver.GetPosition2D);
            record.LastProgressHours = CampaignTime.Now.ToHours;
        }

        /// <summary>
        /// 走不动了就别硬等。只要这么久都没有真的靠近过收件人，就换一个人送；
        /// 连换都换不出来，就把队伍连人带钱带回玩家身边——办不成事不要紧，东西不能丢。
        /// </summary>
        private bool HasStalled(GwpDispatchRecord record, MobileParty party,
            MobileParty receiver, float distance)
        {
            if (distance < record.LastDistance - StallProgressEpsilon)
            {
                record.LastDistance = distance;
                record.LastProgressHours = CampaignTime.Now.ToHours;
                return false;
            }
            if (record.LastProgressHours <= 0d)
            {
                ResetProgress(record, party, receiver);
                return false;
            }
            if (CampaignTime.Now.ToHours - record.LastProgressHours < StallPatienceHours)
                return false;

            GwpAiDiagnostics.WriteAction(party, "DISPATCH_STALLED",
                "receiver=" + receiver.StringId +
                "; distance=" + distance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                "; hoursWithoutProgress=" +
                (CampaignTime.Now.ToHours - record.LastProgressHours).ToString(
                    "0.0", System.Globalization.CultureInfo.InvariantCulture) +
                "; canReach=" + CanReach(party, receiver) +
                "; moveMode=" + party.PartyMoveMode +
                "; moveTarget=" + (party.MoveTargetParty?.StringId ?? "-") +
                "; navigation=" + party.NavigationCapability +
                "; receiverAtSea=" + receiver.IsCurrentlyAtSea);

            MobileParty? other = MobileParty.All
                .Where(p => p != receiver && p?.IsActive == true && p.LeaderHero != null &&
                            string.Equals(p.ActualClan?.StringId,
                                PoliceStats.GetPoliceClan()?.StringId,
                                StringComparison.OrdinalIgnoreCase) &&
                            CanReach(party, p))
                .OrderBy(p => p.GetPosition2D.Distance(party.GetPosition2D))
                .FirstOrDefault();
            if (other != null)
            {
                record.ReceiverPartyId = other.StringId;
                ResetProgress(record, party, other);
                SendTo(party, other);
                return true;
            }

            BeginReturn(record, party, "stalled");
            return true;
        }

        private void DeliverTo(GwpDispatchRecord record, MobileParty party, MobileParty receiver)
        {
            if (record.Purpose == GwpDispatchPurpose.Support)
            {
                bool granted = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
                    ?.GrantCaseSupport("courier_delivered", receiver) == true;
                GwpAiDiagnostics.WriteAction(party, "DISPATCH_SUPPORT_DELIVERED",
                    "receiver=" + receiver.StringId + "; granted=" + granted);
                if (!granted)
                    InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                        "{=gwp_dispatch_support_moot}Your riders delivered the request, but the matter no longer stands."),
                        Colors.Yellow));
                BeginReturn(record, party, "support_delivered");
                return;
            }

            var bounty = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            if (bounty?.CanDispatchCaseReport != true)
            {
                // 玩家已经自己交过差，或案子已经不在了。钱、人、俘虏原样带回，
                // 绝不在这里"交"给一个不存在的委托。
                GwpAiDiagnostics.WriteAction(party, "DISPATCH_REPORT_MOOT",
                    "receiver=" + receiver.StringId + "; carried=" + party.PartyTradeGold);
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_dispatch_report_moot}Your men arrived to find the commission already closed. They are bringing everything back."),
                    Colors.Yellow));
                BeginReturn(record, party, "report_moot");
                return;
            }

            int deliveredCash = Math.Min(record.CaseGoldFloor, party.PartyTradeGold);
            bool prisonerDelivered = false;
            if (!string.IsNullOrEmpty(record.PrisonerHeroId))
            {
                Hero? prisoner = Hero.FindFirst(h => h.StringId == record.PrisonerHeroId);
                if (prisoner?.IsPrisoner == true && prisoner.PartyBelongedToAsPrisoner == party.Party)
                {
                    try
                    {
                        TransferPrisonerAction.Apply(prisoner.CharacterObject, party.Party, receiver.Party);
                        prisonerDelivered = prisoner.PartyBelongedToAsPrisoner == receiver.Party;
                    }
                    catch (Exception ex)
                    {
                        GwpAiDiagnostics.WriteFieldArrest("DISPATCH_PRISONER_FAILED", ex.ToString());
                    }
                }
            }

            int fee = bounty.CompleteDispatchedCaseReport(
                deliveredCash, record.ReportLie, prisonerDelivered, receiver.Party);
            if (fee < 0)
            {
                // 交接没有成立，账不能当作已缴，钱一分不动。
                GwpAiDiagnostics.WriteAction(party, "DISPATCH_REPORT_REFUSED",
                    "receiver=" + receiver.StringId + "; carried=" + party.PartyTradeGold);
                BeginReturn(record, party, "report_refused");
                return;
            }

            party.PartyTradeGold = Math.Max(0, party.PartyTradeGold - deliveredCash);
            // 办案费交到使者手上带回来，并且同样受保护：他们买粮只能花路上挣的，
            // 不许动玩家托付的钱，也不许动要带回去的酬劳。路上被打光才会一起没。
            if (fee > 0) party.PartyTradeGold += fee;
            record.CaseGoldFloor = Math.Max(0, fee);

            GwpAiDiagnostics.WriteAction(party, "DISPATCH_REPORT_DELIVERED",
                "receiver=" + receiver.StringId + "; cash=" + deliveredCash +
                "; prisoner=" + prisonerDelivered + "; lie=" + record.ReportLie + "; fee=" + fee);
            BeginReturn(record, party, "report_delivered");
        }

        private void BeginReturn(GwpDispatchRecord record, MobileParty party, string reason)
        {
            record.Phase = GwpDispatchPhase.Returning;
            SendTo(party, MobileParty.MainParty);
            GwpAiDiagnostics.WriteAction(party, "DISPATCH_RETURNING", "reason=" + reason);
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_dispatch_returning}Your detachment has done its errand and is on its way back."),
                Colors.Cyan));
        }

        private void AdvanceReturn(GwpDispatchRecord record, MobileParty party)
        {
            MobileParty? player = MobileParty.MainParty;
            if (player?.IsActive != true) return;
            float distance = party.GetPosition2D.Distance(player.GetPosition2D);

            // 自己人回来交割，不该弹出一场遭遇。原版把"能不能被撞上"挂在
            // PartyBase.CanPartyInteract → MobileParty.ShouldBeIgnored 上，所以最后这一段
            // 路把这支队伍设成不可交互，交割由我们自己在会合时完成。
            // 只在最后一段路这么做：外面那一路照样会被劫匪堵、照样有风险。
            if (distance <= FinalApproachDistance) KeepOutOfPlayerWay(party);

            if (distance > HandoverDistance)
            {
                if (!TryHandleTownBusiness(record, party)) SendTo(party, player);
                return;
            }
            HandBackEverything(record, party, player);
        }

        private static void KeepOutOfPlayerWay(MobileParty party)
        {
            try { party.IgnoreByOtherPartiesTill(CampaignTime.HoursFromNow(2f)); }
            catch { }
        }

        /// <summary>
        /// 兜底：万一还是撞上了（两次巡检之间走到了一起），当场把东西交了、把遭遇关掉，
        /// 别把玩家丢进一个只能投降的战斗界面。
        /// </summary>
        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty,
            PartyBase defenderParty)
        {
            _ = attackerParty;
            _ = defenderParty;
            MobileParty? player = MobileParty.MainParty;
            if (player?.IsActive != true || _dispatches.Count == 0) return;
            if (!mapEvent.InvolvedParties.Any(p => p.MobileParty?.IsMainParty == true)) return;

            foreach (GwpDispatchRecord record in _dispatches.ToList())
            {
                MobileParty? party = FindParty(record.PartyId);
                if (party == null ||
                    !mapEvent.InvolvedParties.Any(p => p.MobileParty == party)) continue;

                GwpAiDiagnostics.WriteAction(party, "DISPATCH_MET_PLAYER_IN_ENCOUNTER",
                    "phase=" + record.Phase + "; purpose=" + record.Purpose);
                KeepOutOfPlayerWay(party);
                GwpCommon.TryFinishPlayerEncounter();
                HandBackEverything(record, party, player);
            }
        }

        /// <summary>
        /// 会合清点。活着的人、伤员、俘虏、粮食、路上打回来的东西和剩下的钱，一样不留。
        /// </summary>
        private void HandBackEverything(GwpDispatchRecord record, MobileParty party, MobileParty player)
        {
            int men = party.MemberRoster.TotalManCount;
            int wounded = party.MemberRoster.TotalWoundedRegulars;
            int prisoners = party.PrisonRoster.TotalManCount;
            int gold = Math.Max(0, party.PartyTradeGold);
            int items = 0;

            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster().ToList())
            {
                // 临时队长随差事撤销，不会跟着回到玩家队里。
                if (element.Character == null || element.Number <= 0 ||
                    element.Character.IsHero) continue;
                player.MemberRoster.AddToCounts(element.Character, element.Number,
                    false, element.WoundedNumber, element.Xp);
            }
            foreach (TroopRosterElement element in party.PrisonRoster.GetTroopRoster().ToList())
            {
                if (element.Character == null || element.Number <= 0) continue;
                if (element.Character.IsHero && element.Character.HeroObject != null)
                {
                    try
                    {
                        TransferPrisonerAction.Apply(element.Character, party.Party, player.Party);
                        continue;
                    }
                    catch { }
                }
                player.PrisonRoster.AddToCounts(element.Character, element.Number,
                    false, element.WoundedNumber);
            }
            foreach (ItemRosterElement element in party.ItemRoster.ToList())
            {
                if (element.EquipmentElement.Item == null || element.Amount <= 0) continue;
                player.ItemRoster.AddToCounts(element.EquipmentElement, element.Amount);
                items += element.Amount;
            }
            if (gold > 0) GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, gold, true);

            party.MemberRoster.Clear();
            party.PrisonRoster.Clear();
            party.ItemRoster.Clear();
            party.PartyTradeGold = 0;
            _dispatches.Remove(record);
            GwpAiDiagnostics.WriteAction(party, "DISPATCH_HANDOVER",
                "men=" + men + "; wounded=" + wounded + "; prisoners=" + prisoners +
                "; gold=" + gold + "; items=" + items);
            DestroyDispatchParty(party);

            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_dispatch_handover}Your detachment has rejoined you: {VAR_1} men ({VAR_2} wounded), {VAR_3} prisoners, {VAR_4} items and {VAR_5} denars returned to your party.",
                "VAR_1", men, "VAR_2", wounded, "VAR_3", prisoners, "VAR_4", items, "VAR_5", gold),
                Colors.Green));
        }

        private static void DestroyDispatchParty(MobileParty party)
        {
            try
            {
                UntrackOnMap(party);
                GreyWardenPartyDesireBehavior.ClearIntent(party);
                DestroyPartyAction.Apply(null, party);
            }
            catch { }
        }

        #endregion

        #region 补给与工资

        /// <summary>
        /// 工资由玩家付——这是他自己的人。案件款不参与：那笔钱只能原样送到灰袍手里。
        /// 领队是普通士兵，原版的自动买粮只认英雄领队，所以缺粮时按原版市场价在
        /// 当地真实买入，买不起就饿着，不凭空变粮也不凭空变钱。
        /// </summary>
        private void OnDailyTick() =>
            GwpLoadFaultWatch.Guard("DISPATCH_DAILY", UpkeepDispatches);

        private void UpkeepDispatches()
        {
            foreach (GwpDispatchRecord record in _dispatches.ToList())
            {
                MobileParty? party = FindParty(record.PartyId);
                if (party == null) { _dispatches.Remove(record); continue; }
                PayDispatchWages(party);
            }
        }

        private static void PayDispatchWages(MobileParty party)
        {
            int wage = 0;
            try { wage = Math.Max(0, party.TotalWage); }
            catch { }
            if (wage <= 0) return;
            int paid = Math.Min(wage, Hero.MainHero?.Gold ?? 0);
            if (paid > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, paid, true);
            if (paid >= wage) return;
            party.RecentEventsMorale -= 1f;
            GwpAiDiagnostics.WriteAction(party, "DISPATCH_WAGES_SHORT",
                "wage=" + wage + "; paid=" + paid);
        }

        #endregion

        private static MobileParty? FindParty(string? id) =>
            string.IsNullOrWhiteSpace(id)
                ? null
                : MobileParty.All.FirstOrDefault(p => p?.IsActive == true &&
                    string.Equals(p.StringId, id, StringComparison.OrdinalIgnoreCase));
    }
}
