using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 警察资源管理
    /// 1. 常驻领主资源完全交给原版经济和城镇访问欲望：不发钱、不造粮、不免费补兵
    ///    无英雄的临时纠察/支援队是一次性任务单位，只在生成时携带固定行军口粮，
    ///    不参与灰袍家族收支，也不会进城购买补给。
    /// 2. 保留旧补给 API 仅用于存档/调用兼容；它只请求重新思考，不下移动命令
    /// 兵员纯化已删（用户裁定 2026-10-07：灰袍照原版招原版兵，原版兵留在队里）。
    /// </summary>
    public class PoliceResourceManager : CampaignBehaviorBase
    {
        private const int EquipmentSlotCount = 12;
        private const int TroopsPerShip = 50;
        private const int TemporaryDutyFoodDays = 20;
        internal const int SuccessfulCaseReward = 3000;
        // NavalDLC 可选依赖：运行时一次性检测（所有模块 DLL 加载后）
        // 若 NavalDLC 未安装，借船与还船直接跳过，不影响游玩
        private static readonly bool _navalDlcLoaded =
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "NavalDLC");

        public static bool IsResupplying(MobileParty party) => false;

        public static bool IsReady(MobileParty party)
        {
            if (party == null || !party.IsActive) return false;
            return true;
        }

        /// <summary>
        /// 临时办差队没有英雄领队，也不是 WarPartyComponent，不进氏族军费结算。
        /// 生成时清零独立钱袋，按计划人数配二十日口粮。它们不吃饭
        /// （<see cref="GwpLeaderlessFoodModel"/>），这份粮不会减少，只为让原版看到
        /// 一支"粮够吃"的队伍：<c>AiPatrollingBehavior</c> 在粮食天数 ≤ 6 时不给巡逻候选。
        /// 只在出生时配一次；剩余口粮随部队销毁，不进灰袍家族库存或金库。
        /// </summary>
        private static void ProvisionTemporaryDutyParty(MobileParty? party, int plannedMen)
        {
            if (party?.IsActive != true) return;

            party.PartyTradeGold = 0;
            if (party.ItemRoster.TotalFood > 0) return;

            ItemObject? grain = MBObjectManager.Instance.GetObject<ItemObject>(GwpIds.GrainItemId);
            if (grain == null) return;

            int men = Math.Max(1, Math.Max(plannedMen, party.MemberRoster.TotalManCount));
            int grainCount = Math.Max(1,
                (int)Math.Ceiling(men / 20f * TemporaryDutyFoodDays));
            party.ItemRoster.AddToCounts(grain, grainCount);
        }

        /// <summary>
        /// 临时队出生时的一次性配给：口粮 + 向出借方借船。只在各创建点调用。
        /// 不指定出借方时，向最近的、在陆上、有空闲船的灰袍领主借。
        /// </summary>
        internal static void OutfitTemporaryDutyParty(MobileParty? party,
            MobileParty? lender = null, int plannedMen = 0)
        {
            ProvisionTemporaryDutyParty(party, plannedMen);
            if (party?.IsActive == true)
                LendShips(party, lender ?? FindNearestShipLender(party), plannedMen);
        }

        public override void RegisterEvents()
        {
            _instance = this;
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, RepairCampaignState);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, RepairCampaignState);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, OnHeroComesOfAge);
            CampaignEvents.OnShipOwnerChangedEvent.AddNonSerializedListener(this, OnShipOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
            List<string> borrowers = dataStore.IsSaving ? _shipLenders.Keys.ToList() : null!;
            List<string> lenders = dataStore.IsSaving ? _shipLenders.Values.ToList() : null!;
            dataStore.SyncData("GWPP_ShipLoanBorrowers", ref borrowers);
            dataStore.SyncData("GWPP_ShipLoanLenders", ref lenders);
            if (dataStore.IsLoading)
            {
                _shipLenders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (borrowers != null && lenders != null)
                    for (int i = 0; i < Math.Min(borrowers.Count, lenders.Count); i++)
                        if (!string.IsNullOrEmpty(borrowers[i]) && !string.IsNullOrEmpty(lenders[i]))
                            _shipLenders[borrowers[i]] = lenders[i];
            }
        }

        #region 每日资源与成年战斗员保障

        // 读档和开局都跑一遍。
        private void RepairCampaignState(CampaignGameStarter starter)
        {
            CleanupLeaderlessPoliceLordParties();
            EnsureAllAdultGreyWardensAreCombatants();
            RepairGeneratedAdultCommanderLoadouts();
        }

        private static void CleanupLeaderlessPoliceLordParties()
        {
            foreach (MobileParty party in MobileParty.All.Where(static party =>
                         party?.IsActive == true && party.IsLordParty &&
                         party.LeaderHero == null &&
                         GwpCommon.IsGreyWardenClanParty(party)).ToList())
            {
                GwpAiDiagnostics.WritePartyLifecycle(party,
                    "LEADERLESS_POLICE_LORD_PARTY_CLEANUP", string.Empty);
                GwpCommon.TryDestroyParty(party);
            }
        }

        private void OnDailyTick()
        {
            // 防止警察家族被引擎标记为已消灭
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan?.IsEliminated == true)
            {
                typeof(Clan)
                    .GetField("_isEliminated", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(policeClan, false);
            }

            CollectDailyVillageProtectionContributions();
            EnsureAllAdultGreyWardensAreCombatants();
        }

        /// <summary>
        /// 只有信众村按当前户数每日向司法公库缴纳保护费（用户裁定 2026-10-05：不再全图收钱）。
        /// 户数只作为计费基数，不因缴费发生变化。
        /// </summary>
        private static void CollectDailyVillageProtectionContributions()
        {
            double exactContribution = Village.All
                .Where(village => GwpFireswornBehavior.IsPreachedVillage(village.Settlement))
                .Sum(village => (double)village.Hearth * GwpTuning.Firesworn.FaithProtectionPerHearth);
            int totalContribution = (int)Math.Floor(exactContribution);
            CreditJudicialTreasury(totalContribution);
            GwpAiDiagnostics.WriteFaith("PROTECTION_FEE", "amount=" + totalContribution +
                "; treasuryAfter=" + GetJudicialTreasuryBalance());
        }

        private static void EnsureAllAdultGreyWardensAreCombatants()
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null)
                return;

            foreach (Hero? hero in policeClan.Heroes.ToList())
            {
                EnsureAdultGreyWardenIsCombatant(hero);
            }
        }


        private void OnHeroComesOfAge(Hero hero)
        {
            if (hero == null || !GwpCommon.IsGreyWardenClanMember(hero)) return;
            EnsureAdultGreyWardenIsCombatant(hero);
        }

        /// <summary>
        /// 在原版成年换装完成后补上灰袍领主套装。原版
        /// AgingCampaignBehavior.OnHeroComesOfAge 会在自己的事件回调里重新生成
        /// 战斗/平民装备，因此不能只依赖另一个同级事件监听器。
        /// </summary>
        internal static bool EnsureCommanderLoadout(Hero? hero, string reason)
        {
            if (hero == null || !GwpCommon.IsGreyWardenClanMember(hero) || hero.IsDead ||
                hero.IsChild || hero.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge)
                return false;

            CharacterObject? template = CharacterObject.Find(GwpIds.CommanderTemplateCharacterId)
                ?? hero.Clan?.Leader?.CharacterObject;
            if (template == null) return false;

            Equipment battleTemplate = template.FirstBattleEquipment;
            Equipment civilianTemplate = template.FirstCivilianEquipment;
            if (battleTemplate == null || battleTemplate.IsEmpty()) return false;
            if (civilianTemplate == null || civilianTemplate.IsEmpty()) return false;
            if (EquipmentMatches(battleTemplate, hero.BattleEquipment) &&
                EquipmentMatches(civilianTemplate, hero.CivilianEquipment))
                return false;

            // 先确保英雄拥有独立装备实例，避免写入共享的默认死者装备。
            hero.ResetEquipments();
            CopyEquipment(battleTemplate, hero.BattleEquipment);
            CopyEquipment(civilianTemplate, hero.CivilianEquipment);
            hero.CheckInvalidEquipmentsAndReplaceIfNeeded();
            GwpAiDiagnostics.WriteHeroLifecycle(hero, "ADULT_COMMANDER_LOADOUT_APPLIED",
                "reason=" + reason + "; template=" + template.StringId);
            return true;
        }

        private static void RepairGeneratedAdultCommanderLoadouts()
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null)
                return;

            foreach (Hero hero in policeClan.Heroes
                         .Where(GreyWardenFamilyBehavior.IsGeneratedPoliceHero)
                         .Where(hero => hero.IsAlive && !hero.IsChild &&
                                        hero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge)
                         .ToList())
            {
                EnsureCommanderLoadout(hero, "existing_save_repair");
            }
        }

        private static bool EquipmentMatches(Equipment expected, Equipment actual)
        {
            if (expected == null || actual == null)
                return false;

            for (int i = 0; i < EquipmentSlotCount; i++)
            {
                EquipmentElement expectedElement = expected[i];
                EquipmentElement actualElement = actual[i];
                if (expectedElement.Item != actualElement.Item ||
                    expectedElement.ItemModifier != actualElement.ItemModifier)
                    return false;
            }

            return true;
        }

        private static void EnsureAdultGreyWardenIsCombatant(Hero? hero)
        {
            if (hero == null || !GwpCommon.IsGreyWardenClanMember(hero) || hero.IsDead || hero.IsDisabled ||
                hero.IsChild || hero.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge ||
                !hero.IsNoncombatant)
                return;

            // 正常情况下 PoliceHeroCreationModel 已直接返回战斗员。这里保留一次
            // 运行时兜底，防止其他模组在更晚阶段替换 HeroCreationModel。
            int oldValue = hero.GetSkillValue(DefaultSkills.OneHanded);
            hero.SetSkillValue(DefaultSkills.OneHanded, Math.Max(100, oldValue));
            GwpAiDiagnostics.WriteHeroLifecycle(hero, "ADULT_COMBATANT_INVARIANT_REPAIRED",
                "skill=OneHanded; old=" + oldValue + "; new=" +
                hero.GetSkillValue(DefaultSkills.OneHanded));
        }

        private static void CopyEquipment(Equipment source, Equipment destination)
        {
            if (source == null || destination == null) return;
            for (int i = 0; i < EquipmentSlotCount; i++)
                destination[i] = source[i];
        }


        #endregion

        #region 补给流程


        #endregion

        #region 补兵 / 补粮 / 释放俘虏


        /// <summary>
        /// 按当前兵力为警察部队补足缺少的船只。
        /// 规则：每 50 人 1 艘，向上取整，最少 1 艘。
        /// 只追加缺失的船，不删除现有船，也不重建整个舰队。
        /// 不安装任何升级件，也不挂船首像。
        /// 无 NavalDLC 时静默跳过，不报错。
        /// 船都是真船：常驻领主靠原版 NavalDLC 自行购船与海战缴获；模组的无领主临时队
        /// 只能向出借方借（领主借给灰袍临时队、玩家借给送信队），队伍销毁前原样退回。
        /// 不凭空生成任何船——凭空的船一旦随队伍解散，原版会把它分给同家族领主或折成
        /// 金币发给家族族长，等于造钱。
        /// </summary>
        private Dictionary<string, string> _shipLenders =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static PoliceResourceManager? _instance;

        /// <summary>出借方至少给自己留一条船。</summary>
        private static int SpareShipCount(MobileParty? lender) =>
            lender?.IsActive == true ? Math.Max(0, lender.Ships.Count - 1) : 0;

        /// <summary>
        /// 出借方在海上又借不出船：从他身边出发的临时队没有船就出不了门，不派。
        /// 在陆上借不到船照常派，只走陆路。
        /// </summary>
        internal static bool IsStrandedAtSea(MobileParty? lender) =>
            _navalDlcLoaded && lender?.IsCurrentlyAtSea == true && SpareShipCount(lender) <= 0;

        /// <summary>没有对应领主的临时队（纠察队、招募使者队）向谁借船。</summary>
        internal static MobileParty? FindNearestShipLender(MobileParty borrower)
        {
            if (!_navalDlcLoaded || borrower == null) return null;
            return MobileParty.All
                .Where(p => GwpCommon.IsGreyWardenLordParty(p) && p.IsActive && p.LeaderHero != null &&
                            !p.IsCurrentlyAtSea && p.MapEvent == null && SpareShipCount(p) > 0)
                .OrderBy(p => p.GetPosition2D.DistanceSquared(borrower.GetPosition2D))
                .FirstOrDefault();
        }

        /// <summary>
        /// 借船并登记出借方。只借空闲船里最便宜的几条，按计划人数每 50 人一条。
        /// 借不到也照样登记，队伍路上缴获的船解散时同样交回出借方。
        /// </summary>
        internal static void LendShips(MobileParty? borrower, MobileParty? lender, int plannedMen = 0)
        {
            if (!_navalDlcLoaded || _instance == null) return;
            if (borrower?.IsActive != true || lender?.IsActive != true ||
                lender == borrower) return;
            _instance._shipLenders[borrower.StringId] = lender.StringId;
            if (lender.MapEvent != null || borrower.Ships.Count > 0) return;

            int wanted = Math.Min(SpareShipCount(lender),
                GetRequiredShipCount(borrower, plannedMen));
            if (wanted <= 0) return;
            foreach (Ship ship in lender.Ships
                         .OrderBy(s => Campaign.Current.Models.ShipCostModel
                             .GetShipTradeValue(s, lender.Party, null))
                         .Take(wanted).ToList())
            {
                ChangeShipOwnerAction.ApplyByTransferring(borrower.Party, ship);
            }
            borrower.SetNavalVisualAsDirty();
            lender.SetNavalVisualAsDirty();
        }

        /// <summary>
        /// 临时队销毁前把身上所有船交回出借方（<see cref="GwpShipLoanReturnPatch"/>），
        /// 抢在原版“解散分船/折金币”之前。出借的灰袍领主已不在，就交给最近的灰袍领主。
        /// </summary>
        internal static void ReturnLentShips(MobileParty? borrower)
        {
            if (_instance == null || borrower == null) return;
            string? lenderId = null;
            bool registered = _instance._shipLenders.TryGetValue(borrower.StringId, out lenderId);
            if (!registered && !GwpCommon.IsLeaderlessDutyParty(borrower)) return;
            _instance._shipLenders.Remove(borrower.StringId);
            if (borrower.Ships.Count == 0) return;
            MobileParty? receiver = null;
            if (registered)
            {
                // 玩家被俘时主队不活跃，但船仍是玩家的（原版被俘不动主队的船）。
                MobileParty? main = MobileParty.MainParty;
                receiver = main != null && main != borrower &&
                           string.Equals(main.StringId, lenderId, StringComparison.OrdinalIgnoreCase)
                    ? main
                    : MobileParty.All.FirstOrDefault(p => p != borrower &&
                        string.Equals(p.StringId, lenderId, StringComparison.OrdinalIgnoreCase) &&
                        CanReceiveReturnedShips(p));
                receiver ??= MobileParty.All
                    .Where(p => p != borrower && GwpCommon.IsGreyWardenLordParty(p) && CanReceiveReturnedShips(p))
                    .OrderBy(p => p.GetPosition2D.DistanceSquared(borrower.GetPosition2D))
                    .FirstOrDefault();
            }

            if (receiver != null)
            {
                foreach (Ship ship in borrower.Ships.ToList())
                    ChangeShipOwnerAction.ApplyByTransferring(receiver.Party, ship);
                receiver.SetNavalVisualAsDirty();
                return;
            }

            // 还不回去的船不得留给 NavalDLC 分船/折金币。在海上原版不折钱，
            // RemoveParty 会自己销毁剩下的船；在陆上这里先销毁。
            if (borrower.IsCurrentlyAtSea) return;
            foreach (Ship ship in borrower.Ships.ToList())
                DestroyShipAction.Apply(ship);
            GwpAiDiagnostics.WriteAction(borrower, "SHIP_LOAN_UNRETURNABLE_DESTROYED",
                "lender=" + (lenderId ?? "-") + "; registered=" + registered);
        }

        /// <summary>
        /// 与 NavalDLC <c>CanSendShipToParty</c> 同一口径（除主队）：活着、不在海上交战、没在解散。
        /// </summary>
        private static bool CanReceiveReturnedShips(MobileParty party) =>
            party.IsActive && (!party.IsCurrentlyAtSea || party.MapEvent == null) && !party.IsDisbanding;

        private static void OnShipOwnerChanged(
            Ship ship,
            PartyBase oldOwner,
            ChangeShipOwnerAction.ShipOwnerChangeDetail detail)
        {
            MobileParty? oldParty = oldOwner?.MobileParty;
            MobileParty? newParty = ship?.Owner?.MobileParty;
            bool oldPolice = GwpCommon.IsGreyWardenLordParty(oldParty);
            bool newPolice = GwpCommon.IsGreyWardenLordParty(newParty);
            if (oldPolice == newPolice) return;

            MobileParty? party = newPolice ? newParty : oldParty;
            if (party == null) return;

            GwpAiDiagnostics.WriteAction(
                party,
                newPolice ? "SHIP_ACQUIRED" : "SHIP_DISPOSED",
                "hull=" + (ship?.ShipHull?.StringId ?? string.Empty) +
                "; detail=" + detail +
                "; oldOwner=" + DescribeShipOwner(oldOwner) +
                "; newOwner=" + DescribeShipOwner(ship?.Owner));
        }

        private static string DescribeShipOwner(PartyBase? owner)
        {
            if (owner == null) return "-";
            if (owner.IsSettlement)
                return owner.Settlement?.StringId ?? "settlement";
            return owner.MobileParty?.StringId ?? "party";
        }


        private static int GetRequiredShipCount(MobileParty party, int plannedMen)
        {
            int troopCount = Math.Max(1, Math.Max(plannedMen,
                party?.MemberRoster?.TotalManCount ?? 0));
            return Math.Max(1, (troopCount + TroopsPerShip - 1) / TroopsPerShip);
        }

        #endregion

        #region 罚款收取

        public static int CollectFine(int fine)
        {
            if (fine <= 0) return 0;

            int goldTaken = TransferPlayerGoldToJudicialTreasury(fine);

            int remaining = fine - goldTaken;
            int itemsValue = 0;

            if (remaining > 0)
            {
                itemsValue = ConfiscateItems(remaining);
                if (itemsValue > 0)
                {
                    CreditJudicialTreasury(itemsValue);
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get("{=gwp_policeresourcemanager_001}Coin is insufficient; goods worth a further {VAR_1} denars have been confiscated.", "VAR_1", itemsValue), Colors.Yellow));
                }
            }

            return goldTaken + itemsValue;
        }

        /// <summary>
        /// 只收金币，不没收背包物品。用于战败押送后的严肃罚款流程。
        /// </summary>
        public static int CollectFineGoldOnly(int fine)
        {
            if (fine <= 0) return 0;
            return TransferPlayerGoldToJudicialTreasury(fine);
        }

        /// <summary>
        /// Player-request payments are all-or-nothing and always enter the clan
        /// leader's wallet, which is the Grey Warden public treasury.
        /// </summary>
        internal static bool CanCollectPlayerRequestPayment(int amount)
        {
            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            return amount > 0 && Hero.MainHero != null &&
                   Hero.MainHero.Gold >= amount &&
                   treasurer != null && !treasurer.IsDead &&
                   treasurer != Hero.MainHero;
        }

        internal static bool TryCollectPlayerRequestPayment(int amount)
        {
            if (!CanCollectPlayerRequestPayment(amount)) return false;
            return TransferPlayerGoldToJudicialTreasury(amount) == amount;
        }

        internal static void RefundPlayerRequestPayment(int amount)
        {
            if (amount <= 0 || Hero.MainHero == null) return;

            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            int treasuryRefund = treasurer?.IsDead == false &&
                                 treasurer != Hero.MainHero
                ? Math.Min(Math.Max(0, treasurer.Gold), amount)
                : 0;
            if (treasuryRefund > 0)
                GiveGoldAction.ApplyBetweenCharacters(treasurer, Hero.MainHero,
                    treasuryRefund, disableNotification: true);
            int remainder = amount - treasuryRefund;
            if (remainder > 0)
                Hero.MainHero.ChangeHeroGold(remainder);
        }

        /// <summary>
        /// 族长的钱包就是原版家族金库，也作为灰袍司法公库。无论罚款由常驻
        /// 领主还是临时纠察队代收，金币都从玩家真实转入族长，不留在经手队伍。
        /// </summary>
        private static int TransferPlayerGoldToJudicialTreasury(int requested)
        {
            if (requested <= 0 || Hero.MainHero == null) return 0;

            int amount = Math.Min(Hero.MainHero.Gold, requested);
            if (amount <= 0) return 0;

            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            if (treasurer != null && treasurer != Hero.MainHero && !treasurer.IsDead)
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, treasurer, amount,
                    disableNotification: true);
            else
                Hero.MainHero.ChangeHeroGold(-amount);

            return amount;
        }

        /// <summary>
        /// A fine settled in the field leaves the offender's own purse. The arresting
        /// Warden keeps a stated share as expenses and the rest reaches the judicial
        /// treasury, which is the police clan leader's wallet. Returns what was actually
        /// collected: a payer who is short pays what he has, and the caller decides what
        /// happens to a man who cannot pay at all.
        /// </summary>
        internal static int CollectFieldFine(Hero? payer, int requested)
        {
            if (payer == null || requested <= 0 || Hero.MainHero == null) return 0;

            int amount = Math.Min(payer.Gold, requested);
            if (amount <= 0) return 0;

            GiveGoldAction.ApplyBetweenCharacters(payer, Hero.MainHero, amount,
                disableNotification: true);
            return amount;
        }

        /// <summary>
        /// A case closed by taking the man brings in no fine, so the expenses come out of
        /// the judicial treasury. It pays what it can and no more - an empty treasury pays
        /// nothing, which is itself information for the player.
        /// </summary>
        /// <summary>
        /// 从司法公库支出，但不交到玩家本人手上——由派出去的使者带回来。路上出了事，
        /// 这笔钱就跟着没了，这正是派人代办应该承担的风险。
        /// </summary>
        internal static int WithdrawFromJudicialTreasury(int amount)
        {
            if (amount <= 0) return 0;

            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            if (treasurer == null || treasurer.IsDead || treasurer == Hero.MainHero)
                return 0;

            int paid = Math.Min(Math.Max(0, treasurer.Gold), amount);
            if (paid <= 0) return 0;

            GiveGoldAction.ApplyBetweenCharacters(treasurer, null, paid,
                disableNotification: true);
            return paid;
        }

        internal static int PayFromJudicialTreasury(int amount)
        {
            if (amount <= 0 || Hero.MainHero == null) return 0;

            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            if (treasurer == null || treasurer.IsDead || treasurer == Hero.MainHero)
                return 0;

            int paid = Math.Min(Math.Max(0, treasurer.Gold), amount);
            if (paid <= 0) return 0;

            GiveGoldAction.ApplyBetweenCharacters(treasurer, Hero.MainHero, paid,
                disableNotification: true);
            return paid;
        }

        /// <summary>
        /// 罚没物离开玩家背包后视为由灰袍统一拍卖，估值直接归司法公库。
        /// </summary>
        internal static void CreditSuccessfulCaseCompletion() =>
            CreditJudicialTreasury(SuccessfulCaseReward);

        /// <summary>
        /// The clan leader's wallet is the judicial treasury. This read-only view is
        /// used by the case ledger and never transfers money between parties.
        /// </summary>
        internal static int GetJudicialTreasuryBalance()
        {
            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            return treasurer?.IsDead == false ? Math.Max(0, treasurer.Gold) : 0;
        }

        internal static bool CanFundVillageReconstruction(out int cost, out int reserve)
        {
            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            int treasury = GetJudicialTreasuryBalance();
            cost = CalculateVillageReconstructionCost(treasury);
            int dailyWages = PoliceStats.GetAllPoliceParties()
                .Where(party => party?.IsActive == true && party.LeaderHero != null)
                .Sum(party => Math.Max(0, party.TotalWage));
            reserve = Math.Max(
                GwpTuning.Reconstruction.MinimumTreasuryReserve,
                dailyWages * GwpTuning.Reconstruction.WageReserveDays);
            return treasurer != null && !treasurer.IsDead && treasury - cost >= reserve;
        }

        internal static bool TrySpendVillageReconstructionFunds(out int cost, out int reserve)
        {
            if (!CanFundVillageReconstruction(out cost, out reserve))
                return false;

            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            if (treasurer == null || treasurer.IsDead)
                return false;
            treasurer.ChangeHeroGold(-cost);
            return true;
        }

        /// <summary>烈火责罚每烧一人从公库付钱。调用方已按余额算好人数。</summary>
        internal static void SpendJudicialTreasury(int amount)
        {
            if (amount > 0)
                PoliceStats.GetPoliceClan().Leader.ChangeHeroGold(-amount);
        }

        internal static void RefundJudicialTreasury(int amount) =>
            CreditJudicialTreasury(amount);

        private static int CalculateVillageReconstructionCost(int treasury)
        {
            int proportional = (int)Math.Round(
                treasury * GwpTuning.Reconstruction.TreasuryShare / 100d,
                MidpointRounding.AwayFromZero) * 100;
            return Math.Max(GwpTuning.Reconstruction.MinimumCost,
                Math.Min(GwpTuning.Reconstruction.MaximumCost, proportional));
        }

        /// <summary>使者把玩家预付的订金送到之后，这笔钱直接入库——玩家的金币在
        /// 出发时就已经扣走，这里不能再向玩家收一次。</summary>
        internal static void CreditJudicialTreasuryFromCourier(int amount) =>
            CreditJudicialTreasury(amount);

        private static void CreditJudicialTreasury(int amount)
        {
            if (amount <= 0) return;
            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            if (treasurer != null && !treasurer.IsDead)
                treasurer.ChangeHeroGold(amount);
        }

        private static int ConfiscateItems(int debt)
        {
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null || roster.Count == 0) return 0;

            var elements = new List<(EquipmentElement eq, int amount, int value)>();
            foreach (ItemRosterElement e in roster)
            {
                var item = e.EquipmentElement.Item;
                if (item == null || e.Amount <= 0 || item.Value <= 0) continue;
                elements.Add((e.EquipmentElement, e.Amount, item.Value));
            }
            elements.Sort((a, b) => b.value.CompareTo(a.value));

            int confiscated = 0;
            foreach (var (eq, amount, value) in elements)
            {
                if (debt <= 0) break;
                int take = Math.Min(amount, (int)Math.Ceiling((double)debt / value));
                roster.AddToCounts(eq, -take);
                int gained = value * take;
                confiscated += gained;
                debt -= gained;
            }
            return confiscated;
        }

        #endregion

    }

    /// <summary>
    /// 原版销毁队伍（战败、模组自己拆队）都经过这里，而 NavalDLC “分船/折金币”挂在它随后
    /// 发出的 <c>MobilePartyDestroyed</c> 上。先把借来的船交回出借方。
    /// </summary>
    [HarmonyPatch(typeof(DestroyPartyAction), "ApplyInternal")]
    internal static class GwpShipLoanReturnPatch
    {
        [HarmonyPrefix]
        private static void Before(MobileParty destroyedParty)
        {
            PoliceResourceManager.ReturnLentShips(destroyedParty);
        }
    }

    /// <summary>
    /// 解散这条路先发 <c>OnPartyDisbanded</c>（NavalDLC 在这里就分船/折金币）再进
    /// <c>ApplyInternal</c>，上面的前缀来不及，必须在入口先还船。
    /// </summary>
    [HarmonyPatch(typeof(DestroyPartyAction), nameof(DestroyPartyAction.ApplyForDisbanding))]
    internal static class GwpShipLoanReturnOnDisbandPatch
    {
        [HarmonyPrefix]
        private static void Before(MobileParty disbandedParty)
        {
            PoliceResourceManager.ReturnLentShips(disbandedParty);
        }
    }
}
