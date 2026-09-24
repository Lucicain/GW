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
        /// <summary>
        /// 送信队的攻击倾向。原版 <c>CalculateInitiativeScoresForEnemy</c> 里
        /// <c>num11</c> 对**非领主**敌人取的就是这个值，并直接乘进 attackScore；
        /// 对领主敌人则恒为 1，不受此值影响。
        /// 置 0 等于完全不打；这里给一个低值：贴身的弱敌照打，但不会为了追一个
        /// 远处目标把差事丢下——超出约 2.5 格之后原版的 num5 不再给 100 倍加成，
        /// 0.2 的乘数足以把追击分压到门槛之下。
        /// </summary>
        private const float CourierAttackInitiative = 0.2f;

        private readonly List<GwpDispatchRecord> _dispatches = new List<GwpDispatchRecord>();
        private string _dispatchState = string.Empty;

        internal static GwpWardenDispatchBehavior? Instance =>
            Campaign.Current?.GetCampaignBehavior<GwpWardenDispatchBehavior>();

        internal static bool IsDispatchParty(MobileParty? party) =>
            party?.StringId?.StartsWith(DispatchPartyPrefix, StringComparison.Ordinal) == true;

        internal bool HasActiveDispatch(GwpDispatchPurpose purpose) =>
            _dispatches.Any(d => d.Purpose == purpose && FindParty(d.PartyId) != null);

        internal string CargoFor(MobileParty party) => _dispatches.FirstOrDefault(d => d.PartyId == party.StringId)?.CargoState ?? string.Empty;

        private int UsableFood(MobileParty party) => GwpDispatchCargo.Food(party, CargoFor(party));

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, CompletePendingHandovers);
        }

        private void CompletePendingHandovers(float dt)
        {
            // 同样挂在每帧的 TickEvent 上。没有派遣在外时，下面那句
            // Where(...).ToList() 每帧都要分配一个 LINQ 迭代器加一个 List，
            // 只为了立刻发现列表是空的。
            if (_dispatches.Count == 0) return;

            foreach (GwpDispatchRecord record in _dispatches.Where(d => d.HandoverPending).ToList())
            {
                MobileParty? party = FindParty(record.PartyId);
                if (party == null) { _dispatches.Remove(record); continue; }
                if (party.MapEvent != null)
                {
                    GwpCommon.TryFinishPlayerEncounter();
                    continue;
                }
                if (MobileParty.MainParty?.MapEvent != null) continue;
                record.HandoverPending = false;
                if (MobileParty.MainParty?.IsActive == true)
                    GwpRuntimeFaultWatch.Guard("DISPATCH_DEFERRED_HANDOVER", () =>
                        HandBackEverything(record, party, MobileParty.MainParty));
            }
        }

        public override void SyncData(IDataStore dataStore) =>
            GwpRuntimeFaultWatch.Guard("DISPATCH_SYNC", () => SyncDispatchData(dataStore));

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
            GwpRuntimeFaultWatch.Guard("DISPATCH_SESSION_LAUNCH", () => StartDispatchSession(starter));

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
                if (record.Phase != GwpDispatchPhase.Rejoined) TrackOnMap(party);
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
            if (record.Phase == GwpDispatchPhase.Rejoined) return;
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
            string prisonerHeroId, List<ItemRosterElement>? cargo = null,
            string orderTroopId = "", int orderCount = 0, int orderPrice = 0)
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

            if (PoliceResourceManager.IsStrandedAtSea(player))
            {
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_dispatch_no_spare_ship}You are at sea with no ship to spare. Your men cannot leave until you make landfall."),
                    Colors.Yellow));
                return null;
            }

            carriedCaseGold = Math.Max(0, carriedCaseGold);
            cargo = cargo ?? new List<ItemRosterElement>();
            if (cargo.Any(e => e.Amount <= 0 || player.ItemRoster.Where(x => x.EquipmentElement.Equals(e.EquipmentElement)).Sum(x => x.Amount) < e.Amount)) return null;
            int reservedFood = cargo.Where(e => e.EquipmentElement.Item.IsFood).Sum(e => e.Amount);
            if (carriedCaseGold > Hero.MainHero.Gold ||
                !CanProvision(detachment.TotalManCount, player, carriedCaseGold, reservedFood))
            {
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_dispatch_no_rations}You have neither food nor coin to send with them. Lay in provisions before you send anyone out."),
                    Colors.Yellow));
                return null;
            }

            // 要押走的那个人不经过分兵界面——原版那个界面把"俘虏"整栏写死成不可转移，
            // 玩家既看不到也拖不动。改为玩家在出发前单独答一句"押人还是交钱"，
            // 这里建好队伍之后走原版的转移直接把他交过去。
            Hero? casePrisoner = string.IsNullOrEmpty(prisonerHeroId)
                ? null
                : Hero.FindFirst(h => h.StringId == prisonerHeroId);

            MobileParty? createdParty = null;
            int caseGoldTransferred = 0;
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

                createdParty = party;
                party.StringId = DispatchPartyPrefix + MBRandom.RandomInt(100000, 999999);
                party.ActualClan = Clan.PlayerClan;
                KeepCourierDisposition(party);
                // 送信队自带货物口粮，不走灰袍临时队的口粮配给；船向玩家借。
                PoliceResourceManager.LendShips(party, player);
                bool prisonerLoaded = false;
                if (casePrisoner != null)
                {
                    try
                    {
                        TransferPrisonerAction.Apply(
                            casePrisoner.CharacterObject, player.Party, party.Party);
                        prisonerLoaded = casePrisoner.PartyBelongedToAsPrisoner == party.Party;
                    }
                    catch (Exception transferError)
                    {
                        GwpAiDiagnostics.WriteFieldArrest("DISPATCH_PRISONER_LOAD_FAILED",
                            transferError.ToString());
                    }
                    if (!prisonerLoaded)
                        InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                            "{=gwp_dispatch_prisoner_failed}Your men could not take custody of him. Keep him with you and deliver him yourself."),
                            Colors.Red));
                }
                if (carriedCaseGold > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, carriedCaseGold, true);
                    party.PartyTradeGold += carriedCaseGold;
                    caseGoldTransferred = carriedCaseGold;
                }
                var record = new GwpDispatchRecord
                {
                    PartyId = party.StringId,
                    Purpose = purpose,
                    Phase = GwpDispatchPhase.Outbound,
                    ReceiverPartyId = receiver.StringId,
                    CaseGoldFloor = Math.Max(0, carriedCaseGold),
                    OrderTroopId = orderTroopId ?? string.Empty,
                    OrderCount = Math.Max(0, orderCount),
                    OrderPrice = Math.Max(0, orderPrice),
                    CaseHeroId = purpose == GwpDispatchPurpose.Report
                        ? Campaign.Current.GetCampaignBehavior<PlayerBountyBehavior>()?.CaseReportIdentity ?? string.Empty : string.Empty,
                    ReportLie = reportLie,
                    PrisonerHeroId = casePrisoner?.PartyBelongedToAsPrisoner == party.Party
                        ? prisonerHeroId ?? string.Empty
                        : string.Empty,
                    DispatchedHours = CampaignTime.Now.ToHours
                };
                ResetProgress(record, party, receiver);
                _dispatches.Add(record);
                var loaded = new List<GwpDispatchCargo.Entry>();
                foreach (var item in cargo)
                {
                    player.ItemRoster.AddToCounts(item.EquipmentElement, -item.Amount);
                    party.ItemRoster.AddToCounts(item.EquipmentElement, item.Amount);
                    loaded.Add(new GwpDispatchCargo.Entry { Item = item.EquipmentElement,
                        Amount = item.Amount, Price = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()?.CaseReportReceipt?.PriceFor(item.EquipmentElement)
                            ?? Math.Max(1, item.EquipmentElement.ItemValue) });
                    record.CargoState = GwpDispatchCargo.Encode(loaded);
                }
                TakeRationsFromPlayer(party, player);
                TrackOnMap(party);
                SendTo(party, receiver);
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_dispatch_sent}Your detachment has set out to find {VAR_1}.",
                    "VAR_1", receiver.Name.ToString()), Colors.Cyan));
                return party;
            }
            catch (Exception ex)
            {
                GwpAiDiagnostics.WriteFieldArrest("DISPATCH_FAILED", ex.ToString());
                GwpFaultTrace.Write("DISPATCH_CREATE_FAILED", details: ex.ToString());
                // Once the real party exists, the caller must not return the
                // dummy selection as well. Bring back its actual remaining cargo.
                if (createdParty?.IsActive == true)
                {
                    GwpDispatchRecord? recovery = _dispatches.FirstOrDefault(d => d.PartyId == createdParty.StringId);
                    if (recovery == null)
                    {
                        recovery = new GwpDispatchRecord
                        {
                            PartyId = createdParty.StringId, Purpose = purpose,
                            CaseGoldFloor = caseGoldTransferred,
                            OrderTroopId = orderTroopId ?? string.Empty,
                            OrderCount = Math.Max(0, orderCount),
                            OrderPrice = Math.Max(0, orderPrice),
                            PrisonerHeroId = casePrisoner?.PartyBelongedToAsPrisoner == createdParty.Party
                                ? prisonerHeroId : string.Empty,
                            DispatchedHours = CampaignTime.Now.ToHours
                        };
                        _dispatches.Add(recovery);
                    }
                    recovery.Phase = GwpDispatchPhase.Returning;
                    return createdParty;
                }
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
            int wanted = GwpDispatchSupplyRules.TargetFood(DailyFood(party));
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

        }

        /// <summary>带不满的口粮折成盘缠；至少够买几天的粮。</summary>
        private static int TravelPurseFor(int men, int shortBy) =>
            shortBy <= 0 ? 0 : Math.Max(TravelPursePerMan, shortBy * TravelPursePerMan / Math.Max(1, men));

        /// <summary>一支这么大的队伍出这趟门要带多少口粮。</summary>
        internal static int RationsWantedFor(int men) =>
            GwpDispatchSupplyRules.TargetFood(Math.Max(1, men) /
                (float)Campaign.Current.Models.MobilePartyFoodConsumptionModel.NumberOfMenOnMapToEatOneFood);

        private static float DailyFood(MobileParty party)
        {
            var model = Campaign.Current.Models.MobilePartyFoodConsumptionModel;
            return Math.Max(0.01f, -model.CalculateDailyFoodConsumptionf(party,
                model.CalculateDailyBaseFoodConsumptionf(party)).ResultNumber);
        }

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
        private static bool CanProvision(int men, MobileParty player, int reservedGold, int reservedFood = 0)
        {
            if (AvailableFood(player) > reservedFood) return true;
            int purse = TravelPurseFor(men, RationsWantedFor(men));
            return purse > 0 && Hero.MainHero.Gold - reservedGold >= purse;
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
        /// 回程不再用"直扑"。`RequestRush` 落到原版的 <see cref="AiBehavior.EngageParty"/>，
        /// 那是进攻性移动指令——目标是玩家时，原版 EncounterManager 会据此拉出一场遭遇，
        /// 于是自己人回来交割却弹出"战斗还是投降"。改用跟随：只把队伍带到玩家身边，
        /// 不带交战语义；真正的归队仍由 AdvanceReturn 的距离判断（HandoverDistance）完成。
        /// 顺带把出程遗留的直攻锁解掉——RequestEscort 内部会 ReleaseDirectAttackLock。
        /// </summary>
        private static void FollowTo(MobileParty party, MobileParty? target)
        {
            if (party?.IsActive != true || target?.IsActive != true || party == target) return;
            KeepCourierDisposition(party);
            GreyWardenPartyDesireBehavior.RequestEscort(party, target);
        }

        /// <summary>
        /// 办差的队伍要专心。原版短期主动性每小时都会重新决定扑上去还是绕开，一支十来个
        /// 人的队伍拿默认值就会一路追野怪、走走停停，还常把自己打残。这里按信使该有的
        /// 样子设定并长期保持：**侵略性很低但不是不打**——贴身的弱敌照样收拾，远处的
        /// 目标不值得为它把差事丢下；遇险就躲，挨打时原版照样自卫。
        /// </summary>
        private static void KeepCourierDisposition(MobileParty party)
        {
            try { party.Ai.SetInitiative(CourierAttackInitiative, 1f, CourierInitiativeHours); }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
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
                Campaign.Current?.VisualTrackerManager?.RemoveTrackedObject(party, true);
                if (party.IsCurrentlyUsedByAQuest) party.SetPartyUsedByQuest(false);
            }
            catch (Exception exception)
            {
                GwpFaultTrace.Write("DISPATCH_UNTRACK_FAILED", details: party.StringId + " | " + exception);
            }
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
            if (GwpDispatchCargo.Food(party, Instance?.CargoFor(party) ?? string.Empty) <= 0) return "starving";
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
        /// <c>PoliceResourceManager.LendShips</c>），一支没有船的送信队盯上一个在海上的
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
        internal static MobileParty? FindReceiver(MobileParty from, MobileParty? courier = null)
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
            GwpRuntimeFaultWatch.Guard("DISPATCH_HOURLY", AdvanceDispatches);

        private void AdvanceDispatches()
        {
            foreach (GwpDispatchRecord record in _dispatches.ToList())
            {
                MobileParty? party = FindParty(record.PartyId);
                if (party == null) { _dispatches.Remove(record); continue; }

                if (record.Phase == GwpDispatchPhase.Rejoined)
                {
                    if (DestroyDispatchParty(party)) _dispatches.Remove(record);
                    continue;
                }

                if (record.Phase != GwpDispatchPhase.Rejoined) TrackOnMap(party);
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
            if (!string.IsNullOrEmpty(record.PrisonerHeroId) &&
                party.PrisonRoster.GetTroopRoster().Any(e =>
                    e.Character?.HeroObject?.StringId == record.PrisonerHeroId)) return false;
            double now = CampaignTime.Now.ToHours;
            if (now < record.NextTownBusinessHours) return false;
            bool hasPrisoners = party.PrisonRoster.TotalManCount > 0;
            bool hungry = GwpDispatchSupplyRules.NeedsFood(UsableFood(party), DailyFood(party));
            // 缺粮就该进城，不该先问"买得起吗"。买不起还有卖俘虏、卖战利品这条路，
            // 原来那个"余额大于零才算缺粮"的条件把断粮的队伍直接钉在野外。
            if (!hasPrisoners && !hungry && record.SupplyTownId.Length == 0) return false;

            Settlement? town = party.CurrentSettlement?.IsTown == true
                ? party.CurrentSettlement : FindTradeTown(party);
            if (town == null) return false;

            if (party.CurrentSettlement == town)
            {
                BuyFoodIfNeeded(record, party);
                record.NextTownBusinessHours = now + GwpDispatchSupplyRules.RetryHours;
                record.SupplyTownId = string.Empty;
                // Give the ordinary mission intent back immediately, including
                // when the market is empty or the travel purse cannot buy food.
                GreyWardenPartyDesireBehavior.ClearIntent(party);
                LeaveSettlementAction.ApplyForParty(party);
                record.LastProgressHours = now;
                record.LastDistance = float.MaxValue;
                return false;
            }
            if (record.SupplyTownId.Length == 0)
            {
                record.SupplyTownId = town.StringId;
                record.SupplyStartedHours = now;
            }
            if (now - record.SupplyStartedHours >= 24d)
            {
                record.SupplyTownId = string.Empty;
                record.NextTownBusinessHours = now + GwpDispatchSupplyRules.RetryHours;
                return false;
            }
            record.LastProgressHours = now;
            record.LastDistance = float.MaxValue;
            GreyWardenPartyDesireBehavior.RequestVisit(party, town);
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
                                     NavigationHelper.IsPositionValidForNavigationType(settlement.Position, party.NavigationCapability) &&
                                     (ours == null || !settlement.MapFaction.IsAtWarWith(ours)))
                .OrderBy(settlement => settlement.GetPosition2D.Distance(party.GetPosition2D))
                .FirstOrDefault();
        }

        /// <summary>
        /// 进城之后按原版市价真实买粮。原版 PartiesBuyFoodCampaignBehavior 硬性要求领主
        /// 英雄，无领主队走不到那条路，这一段由我们补。只花受保护额之上的钱。
        /// </summary>
        private void BuyFoodIfNeeded(GwpDispatchRecord record, MobileParty party)
        {
            Settlement? settlement = party.CurrentSettlement;
            if (settlement?.ItemRoster == null || settlement.Town == null) return;
            float daily = DailyFood(party);
            if (UsableFood(party) >= GwpDispatchSupplyRules.TargetFood(daily)) return;

            int spendable = Math.Max(0, party.PartyTradeGold - record.CaseGoldFloor);
            if (spendable <= 0) return;

            foreach (ItemRosterElement element in settlement.ItemRoster.ToList())
            {
                ItemObject? item = element.EquipmentElement.Item;
                if (item?.IsFood != true || element.Amount <= 0) continue;
                int price = settlement.Town.GetItemPrice(element.EquipmentElement, party, false);
                if (price <= 0 || price > spendable) continue;
                int affordable = GwpDispatchSupplyRules.PurchaseCount(
                    UsableFood(party), daily, element.Amount, spendable, price);
                if (affordable <= 0) continue;
                try
                {
                    // SellItemsAction bills LeaderHero for every non-caravan.
                    // This detail has none. Settle each unit against its own
                    // purse and the actual town inventory at the native price.
                    for (int unit = 0; unit < affordable; unit++)
                    {
                        int currentPrice = settlement.Town.GetItemPrice(element.EquipmentElement, party, false);
                        int available = Math.Max(0, party.PartyTradeGold - record.CaseGoldFloor);
                        if (currentPrice <= 0 || currentPrice > available) break;
                        settlement.ItemRoster.AddToCounts(element.EquipmentElement, -1);
                        party.ItemRoster.AddToCounts(element.EquipmentElement, 1);
                        party.PartyTradeGold -= currentPrice;
                        int tax = MBRandom.RoundRandomized(currentPrice *
                            Campaign.Current.Models.SettlementTaxModel.GetTownTaxRatio(settlement.Town));
                        settlement.SettlementComponent.ChangeGold(currentPrice - tax);
                        settlement.Town.TradeTaxAccumulated += (int)Campaign.Current.Models.SettlementTaxModel
                            .GetTownCommissionChangeBasedOnSecurity(settlement.Town, tax);
                    }
                    spendable = Math.Max(0, party.PartyTradeGold - record.CaseGoldFloor);
                }
                catch (Exception exception)
                {
                    GwpFaultTrace.Write("DISPATCH_BUY_FOOD_FAILED", details: party.StringId + " | " + exception);
                }
                if (UsableFood(party) >= GwpDispatchSupplyRules.TargetFood(daily)) break;
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
                BeginReturn(record, party);
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
                    record.ReceiverPartyId = receiver.StringId;
                    ResetProgress(record, party, receiver);
                }
                else receiver = current!;
            }

            float distance = party.GetPosition2D.Distance(receiver.GetPosition2D);
            if (distance > DeliveryDistance)
            {
                if (!TryHandleTownBusiness(record, party) &&
                    !HasStalled(record, party, receiver, distance))
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

            BeginReturn(record, party);
            return true;
        }

        private void DeliverTo(GwpDispatchRecord record, MobileParty party, MobileParty receiver)
        {
            if (record.Purpose == GwpDispatchPurpose.Support)
            {
                bool granted = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
                    ?.GrantCaseSupport("courier_delivered", receiver) == true;
                if (!granted)
                    InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                        "{=gwp_dispatch_support_moot}Your riders delivered the request, but the matter no longer stands."),
                        Colors.Yellow));
                BeginReturn(record, party);
                return;
            }

            if (record.Purpose == GwpDispatchPurpose.PeaceRequest)
            {
                int settled = PoliceAntiWarDeclaration.ApplyWardenMediation();
                InformationManager.DisplayMessage(new InformationMessage(settled > 0
                    ? GwpText.Get("{=gwp_dispatch_peace_done}Your riders carried the word. The quarrels you took up for the Wardens are closed.")
                    : GwpText.Get("{=gwp_dispatch_peace_moot}Your riders arrived to find nothing left to settle."),
                    settled > 0 ? Colors.Green : Colors.Yellow));
                BeginReturn(record, party);
                return;
            }

            if (record.Purpose == GwpDispatchPurpose.TroopOrder)
            {
                // 订金在出发时已从玩家手里扣走并随队带着，这里直接入库，不再向玩家收。
                // 买兵的钱可多不可少：路上折损到不够订价，这一单就不立，钱原样带回。
                int paid = Math.Min(record.OrderPrice, party.PartyTradeGold);
                bool filed = paid >= record.OrderPrice &&
                    GreyWardenTroopRequestBehavior.FileCourierOrder(
                        record.OrderTroopId, record.OrderCount, record.OrderPrice);
                if (filed)
                {
                    party.PartyTradeGold -= paid;
                    record.CaseGoldFloor = Math.Max(0, record.CaseGoldFloor - paid);
                    PoliceResourceManager.CreditJudicialTreasuryFromCourier(paid);
                    GwpAiDiagnostics.WriteAction(receiver, "DISPATCH_TROOP_ORDER_FILED",
                        "courier=" + party.StringId + "; troop=" + record.OrderTroopId +
                        "; count=" + record.OrderCount + "; paid=" + paid);
                }
                else
                {
                    // 练兵长没了，或者玩家在这中间又下了一单。钱原样带回来。
                    InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                        "{=gwp_dispatch_order_moot}Your riders delivered the order, but the Wardens cannot take it up. They are bringing the coin back."),
                        Colors.Yellow));
                }
                BeginReturn(record, party);
                return;
            }

            var bounty = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            if (bounty?.CanDispatchCaseReport != true ||
                (record.CaseHeroId.Length > 0 && record.CaseHeroId != bounty.CaseReportIdentity))
            {
                // 玩家已经自己交过差，或案子已经不在了。钱、人、俘虏原样带回，
                // 绝不在这里"交"给一个不存在的委托。
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_dispatch_report_moot}Your men arrived to find the commission already closed. They are bringing everything back."),
                    Colors.Yellow));
                BeginReturn(record, party);
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
                        if (prisonerDelivered) record.PrisonerHeroId = string.Empty;
                    }
                    catch (Exception ex)
                    {
                        GwpAiDiagnostics.WriteFieldArrest("DISPATCH_PRISONER_FAILED", ex.ToString());
                    }
                }
            }

            int deliveredGoods = GwpDispatchCargo.Value(party, record.CargoState);
            int deliveredValue = (int)Math.Min(int.MaxValue, (long)deliveredCash + deliveredGoods);
            int fee = bounty.CompleteDispatchedCaseReport(
                deliveredValue, record.ReportLie, prisonerDelivered, receiver.Party);
            if (fee < 0)
            {
                // 交接没有成立，账不能当作已缴，钱一分不动。
                BeginReturn(record, party);
                return;
            }

            party.PartyTradeGold = Math.Max(0, party.PartyTradeGold - deliveredCash);
            if (deliveredCash > 0) receiver.PartyTradeGold += deliveredCash;
            foreach (var item in GwpDispatchCargo.Decode(record.CargoState))
            {
                int count = GwpDispatchCargo.Available(party, item);
                if (count <= 0) continue;
                party.ItemRoster.AddToCounts(item.Item, -count);
                receiver.ItemRoster.AddToCounts(item.Item, count);
            }
            record.CargoState = string.Empty;
            // 办案费交到使者手上带回来，并且同样受保护：他们买粮只能花路上挣的，
            // 不许动玩家托付的钱，也不许动要带回去的酬劳。路上被打光才会一起没。
            if (fee > 0) party.PartyTradeGold += fee;
            record.CaseGoldFloor = Math.Max(0, fee);
            BeginReturn(record, party);
        }

        private void BeginReturn(GwpDispatchRecord record, MobileParty party)
        {
            record.Phase = GwpDispatchPhase.Returning;
            record.SupplyTownId = string.Empty;
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_dispatch_returning}Your detachment has done its errand and is on its way back."),
                Colors.Cyan));
            // The player may already be beside the recipient. Apply the return
            // proximity guard / handover now, before issuing a Rush to the player.
            // Waiting for the next hourly update lets native EngageParty start
            // a battle with our own detachment in the meantime.
            AdvanceReturn(record, party);
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
                if (!TryHandleTownBusiness(record, party)) FollowTo(party, player);
                return;
            }
            HandBackEverything(record, party, player);
        }

        private static void KeepOutOfPlayerWay(MobileParty party)
        {
            try { party.IgnoreByOtherPartiesTill(CampaignTime.HoursFromNow(2f)); }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
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
                if (party == null || record.Phase != GwpDispatchPhase.Returning ||
                    !mapEvent.InvolvedParties.Any(p => p.MobileParty == party)) continue;

                // A shared battle against bandits is not a courier handover.
                if (!((attackerParty == party.Party && defenderParty == player.Party) ||
                      (defenderParty == party.Party && attackerParty == player.Party))) continue;

                GwpFaultTrace.Write("DISPATCH_MET_PLAYER_IN_ENCOUNTER", details:
                    "phase=" + record.Phase + "; purpose=" + record.Purpose);
                KeepOutOfPlayerWay(party);
                record.HandoverPending = true;
            }
        }

        /// <summary>
        /// 会合清点。活着的人、伤员、俘虏、粮食、路上打回来的东西和剩下的钱，一样不留。
        /// </summary>
        private void HandBackEverything(GwpDispatchRecord record, MobileParty party, MobileParty player)
        {
            if (!_dispatches.Contains(record) || !party.IsActive || party.MapEvent != null ||
                player.MapEvent != null || record.Phase == GwpDispatchPhase.Rejoined) return;
            UntrackOnMap(party);
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
                party.MemberRoster.AddToCounts(element.Character, -element.Number,
                    false, -element.WoundedNumber, -element.Xp);
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
                    catch (Exception exception)
                    {
                        GwpFaultTrace.Write("DISPATCH_RETURN_PRISONER_FAILED",
                            details: party.StringId + " | " + exception);
                        return;
                    }
                }
                player.PrisonRoster.AddToCounts(element.Character, element.Number,
                    false, element.WoundedNumber);
                party.PrisonRoster.AddToCounts(element.Character, -element.Number,
                    false, -element.WoundedNumber);
            }
            foreach (ItemRosterElement element in party.ItemRoster.ToList())
            {
                if (element.EquipmentElement.Item == null || element.Amount <= 0) continue;
                player.ItemRoster.AddToCounts(element.EquipmentElement, element.Amount);
                party.ItemRoster.AddToCounts(element.EquipmentElement, -element.Amount);
                items += element.Amount;
            }
            if (gold > 0) GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, gold, true);

            party.MemberRoster.Clear();
            party.PrisonRoster.Clear();
            party.ItemRoster.Clear();
            party.PartyTradeGold = 0;
            record.Phase = GwpDispatchPhase.Rejoined;
            if (DestroyDispatchParty(party)) _dispatches.Remove(record);

            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_dispatch_handover}Your detachment has rejoined you: {VAR_1} men ({VAR_2} wounded), {VAR_3} prisoners, {VAR_4} items and {VAR_5} denars returned to your party.",
                "VAR_1", men, "VAR_2", wounded, "VAR_3", prisoners, "VAR_4", items, "VAR_5", gold),
                Colors.Green));
        }

        private static bool DestroyDispatchParty(MobileParty party)
        {
            try
            {
                UntrackOnMap(party);
                GreyWardenPartyDesireBehavior.ClearIntent(party);
                if (party.IsActive) DestroyPartyAction.Apply(null, party);
            }
            catch (Exception exception)
            {
                GwpFaultTrace.Write("DISPATCH_DESTROY_FAILED", details: party.StringId + " | " + exception);
            }
            return !party.IsActive;
        }

        #endregion

        #region 补给与工资

        /// <summary>
        /// 工资由玩家付——这是他自己的人。案件款不参与：那笔钱只能原样送到灰袍手里。
        /// 领队是普通士兵，原版的自动买粮只认英雄领队，所以缺粮时按原版市场价在
        /// 当地真实买入，买不起就饿着，不凭空变粮也不凭空变钱。
        /// </summary>
        private void OnDailyTick() =>
            GwpRuntimeFaultWatch.Guard("DISPATCH_DAILY", UpkeepDispatches);

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
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
            if (wage <= 0) return;
            int paid = Math.Min(wage, Hero.MainHero?.Gold ?? 0);
            if (paid > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, paid, true);
            if (paid >= wage) return;
            party.RecentEventsMorale -= 1f;
        }

        #endregion

        private static MobileParty? FindParty(string? id) =>
            string.IsNullOrWhiteSpace(id)
                ? null
                : MobileParty.All.FirstOrDefault(p => p?.IsActive == true &&
                    string.Equals(p.StringId, id, StringComparison.OrdinalIgnoreCase));
    }
}
