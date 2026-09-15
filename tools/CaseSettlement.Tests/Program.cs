using System.Linq;
using System;
using System.Collections.Generic;
using GreyWardenPolicePurity;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

internal static class Program
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string scenario)
    {
        if (!Equals(expected, actual)) throw new Exception(scenario + ": expected " + expected + ", got " + actual);
        checks++;
    }

    private static void Main()
    {
        Equal(false, GwpDispatchSupplyRules.NeedsFood(1.8f, 0.1f), "two couriers with eighteen days do not seek a town");
        Equal(true, GwpDispatchSupplyRules.NeedsFood(0.2f, 0.1f), "two days triggers refill");
        Equal(2, GwpDispatchSupplyRules.TargetFood(0.1f), "two couriers receive rounded twelve-day supply");
        Equal(0, GwpDispatchSupplyRules.PurchaseCount(2f, 0.1f, 100, 6000, 10), "full supply does not spend protected or spare cash");
        Equal(2, GwpDispatchSupplyRules.PurchaseCount(0f, 0.1f, 100, 240, 10), "buy only missing food rather than the entire purse");
        Equal(0, GwpDispatchSupplyRules.PurchaseCount(0f, 0.1f, 100, 0, 10), "protected-only purse cannot buy");
        Equal(1, GwpDispatchSupplyRules.PurchaseCount(0f, 0.1f, 1, 240, 10), "limited town stock");
        Equal(1, GwpDispatchSupplyRules.PurchaseCount(0f, 0.1f, 100, 15, 10), "limited travel purse");
        foreach (object savedFlag in new object[] { true, false, 1, 0 })
        {
            var legacy = new MemoryStore(false, new Dictionary<string, object?> { ["flag"] = savedFlag });
            bool expected = savedFlag is bool b ? b : (int)savedFlag != 0;
            Equal(expected, GwpLegacySave.ReadFlag(legacy, "flag", true), "legacy bool-first supports " + savedFlag.GetType());
            Equal(expected, GwpLegacySave.ReadFlag(legacy, "flag", false), "legacy int-first supports " + savedFlag.GetType());
        }
        Equal(false, GwpLegacySave.ReadFlag(new MemoryStore(false), "missing", true), "missing legacy support defaults false");
        var oldDispatch = GwpDispatchRecord.Deserialize("courier|0|1|receiver|6000|0||100");
        Equal(6000, oldDispatch!.CaseGoldFloor, "old courier preserves protected money");
        Equal(0d, oldDispatch.NextTownBusinessHours, "old courier defaults supply retry");
        oldDispatch.NextTownBusinessHours = 124;
        Equal(124d, GwpDispatchRecord.Deserialize(oldDispatch.Serialize())!.NextTownBusinessHours, "supply cooldown survives save/load");
        Hero.MainHero = new Hero("player", 8000);
        Hero offender = new Hero("offender", 600);
        Hero clerk = new Hero("clerk", 10000);
        var party = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.Party;
        var poor = new GwpFieldCollectionBarterable(offender, party, 2400) { CurrentAmount = 600 };
        Equal(600, poor.MaxAmount, "collection shows actual poor purse");
        Equal(600, poor.AgreedLimit, "full demand bounded by available money");
        Equal(0, poor.GetUnitValueForFaction(offender.Clan), "agreed collection accepted by native zero-value threshold");
        poor.Apply(); poor.Apply();
        Equal(600, poor.Paid, "collection receipt applied once");
        Equal(8600, Hero.MainHero.Gold, "collection reaches hunter once");
        Equal(0, offender.Gold, "collection drains only agreed purse");
        offender.Gold = 9000;
        var deceptive = new GwpFieldCollectionBarterable(offender, party, 1200) { CurrentAmount = 2400 };
        Equal(9000, deceptive.MaxAmount, "wealth remains visible despite half-payment terms");
        Equal(-1, deceptive.GetUnitValueForFaction(offender.Clan), "full demand rejected after half-payment agreement");
        deceptive.Apply();
        Equal(false, deceptive.Applied, "bypass cannot apply excessive request");
        Equal(9000, offender.Gold, "invalid collection transfers nothing");
        deceptive.CurrentAmount = 1000; deceptive.Apply();
        Equal(1000, deceptive.Paid, "smaller agreed collection permitted");
        var cancelled = new GwpFieldCollectionBarterable(offender, party, 1200);
        Equal(false, cancelled.Applied, "opening or cancelling has no receipt");

        GwpTuning.FieldArrest.ImmediateAuditTesting = false;
        // ================= 上缴与查账（2026-09-15 新口径） =================
        // 玩家代表灰袍办案：对方交得出多少不算他的过失，只要把收到的钱如实上缴就算办妥。
        // 唯一会出事的是私留——手里收了多少、交上来多少，差额就是昧下的钱，排进查账。
        GwpTuning.FieldArrest.ImmediateAuditTesting = false;
        var ledger = new GwpFieldReportLedger();

        ledger.RecordSettlement(offender, 2400, 600, 1200, true);
        Equal(0, ledger.DeclareAmount(600, truthful: true), "handing over everything collected is never penalised");
        Equal(0, ledger.PendingAuditGap, "an honest full hand-in queues no audit");
        Equal(false, ledger.HasPendingReports, "a hand-in closes the report");
        Equal(0, ledger.RecentLieCount, "an honest hand-in is not remembered as a lie");

        ledger.RecordSettlement(offender, 2400, 300, 1200, false);
        Equal(0, ledger.DeclareAmount(300, truthful: true), "a small collection from a rich offender is still not the player's fault");
        Equal(0, ledger.PendingAuditGap, "nothing was kept, so nothing is audited");

        ledger.RecordSettlement(offender, 2400, 1000, 1200, false);
        ledger.DeclareAmount(400, truthful: false);
        Equal(600, ledger.PendingAuditGap, "only the money kept back is audited");
        Equal(1, ledger.RecentLieCount, "a false report is remembered");

        // 索贿照样放行：只要那笔钱如实进了公库。
        var bribes = new GwpFieldReportLedger();
        bribes.RecordSettlement(offender, 2400, 0, 1200, false, 900);
        Equal(0, bribes.DeclareAmount(900, truthful: true), "a bribe handed over in full is not punished");
        Equal(0, bribes.PendingAuditGap, "a fully surrendered bribe queues no audit");
        bribes.RecordSettlement(offender, 2400, 0, 1200, false, 900);
        bribes.DeclareAmount(300, truthful: false);
        Equal(600, bribes.PendingAuditGap, "a bribe kept back is audited like any other concealment");

        // 被查出来的概率：谎报越多越高，声望越高越低，并被上下限夹住。
        var odds = new GwpFieldReportLedger();
        GwpRuntimeState.Player.Reputation = 0;
        float clean = odds.CurrentAuditChance();
        Equal(true, clean > 0.34f && clean < 0.36f, "a clean record audits at the base chance");
        for (int i = 0; i < 4; i++) { odds.RecordSettlement(offender, 1000, 500, 0, false); odds.DeclareAmount(0, truthful: false); }
        Equal(4, odds.RecentLieCount, "recent lies are counted");
        Equal(true, odds.CurrentAuditChance() > clean, "recent lies raise the chance of being found out");
        GwpRuntimeState.Player.Reputation = 20;
        Equal(true, odds.CurrentAuditChance() < 0.55f, "standing buys some trust back");
        GwpRuntimeState.Player.Reputation = -50;
        Equal(true, odds.CurrentAuditChance() <= 0.95f, "the chance is capped");
        GwpRuntimeState.Player.Reputation = 500;
        Equal(true, odds.CurrentAuditChance() >= 0.05f, "the chance has a floor");
        GwpRuntimeState.Player.Reputation = 0;

        // 谎报记录只保留最近十次。
        var memory = new GwpFieldReportLedger();
        for (int i = 0; i < 12; i++) { memory.RecordSettlement(offender, 1000, 500, 0, false); memory.DeclareAmount(500, truthful: true); }
        Equal(0, memory.RecentLieCount, "an unbroken honest streak leaves no lies on record");
        for (int i = 0; i < 12; i++) { memory.RecordSettlement(offender, 1000, 500, 0, false); memory.DeclareAmount(0, truthful: false); }
        Equal(10, memory.RecentLieCount, "only the last ten reports are remembered");

        // 收了钱又动手：独立查一次，与私留各判各的。
        var excess = new GwpFieldReportLedger();
        Equal(false, excess.HasPendingExcessEnforcement(offender.StringId), "no excess before any settlement");
        excess.RecordExcessEnforcement(offender.StringId, 1200);
        Equal(true, excess.HasPendingExcessEnforcement(offender.StringId), "breaking a man who already paid is recorded");
        excess.ResolveExcessAudit(offender.StringId, 0.99f);
        Equal(false, excess.HasPendingExcessEnforcement(offender.StringId), "an undiscovered review closes without a penalty");
        excess.RecordExcessEnforcement(offender.StringId, 1200);
        var excessSave = new MemoryStore(true); excess.SyncData(excessSave);
        var excessLoaded = new GwpFieldReportLedger(); excessLoaded.SyncData(excessSave.Load());
        Equal(true, excessLoaded.HasPendingExcessEnforcement(offender.StringId), "a pending review survives save and load");
        excessLoaded.ResolveExcessAudit(offender.StringId, 0f);
        Equal(false, excessLoaded.HasPendingExcessEnforcement(offender.StringId), "a discovered review settles once");
        excessLoaded.RecordExcessEnforcement(offender.StringId, 0);
        Equal(false, excessLoaded.HasPendingExcessEnforcement(offender.StringId), "nothing to answer for when nothing was paid");

        // 谎报记录随存档往返。
        var honesty = new GwpFieldReportLedger();
        honesty.RecordSettlement(offender, 1000, 800, 0, false);
        honesty.DeclareAmount(100, truthful: false);
        var honestySave = new MemoryStore(true); honesty.SyncData(honestySave);
        var honestyLoaded = new GwpFieldReportLedger(); honestyLoaded.SyncData(honestySave.Load());
        Equal(1, honestyLoaded.RecentLieCount, "the lie record survives save and load");

        // ---- 案子被别人了结：辛苦费仍按底薪加阵亡算 ----
        Equal(400, GwpCaseSettlementRules.WithdrawnCaseCompensation(0),
            "a withdrawn case still pays the base expense");
        Equal(700, GwpCaseSettlementRules.WithdrawnCaseCompensation(5),
            "casualties are compensated on a withdrawn case");
        Equal(400, GwpCaseSettlementRules.WithdrawnCaseCompensation(-3),
            "negative casualties cannot reduce the expense");
        Equal(true, GwpCaseSettlementRules.WithdrawnCaseCompensation(3)
            < GwpCaseSettlementRules.Reward(int.MaxValue, 3, 4),
            "a withdrawn case pays less than bringing the man in");

        // ================= 财物交付：GwpAssetPayment =================
        // 2026-09-15 重写：上一轮改写账本测试时误删了这一段，按生产代码重建覆盖。
        var payFrom = new TaleWorlds.CampaignSystem.Party.PartyBase();
        var payTo = new TaleWorlds.CampaignSystem.Party.PartyBase();
        var horse = new ItemObject { Value = 400 };
        var grain = new ItemObject { Value = 20 };
        payFrom.ItemRoster.AddToCounts(new EquipmentElement { Item = horse }, 2);
        payFrom.ItemRoster.AddToCounts(new EquipmentElement { Item = grain }, 10);
        Hero.MainHero.Gold = 1000;
        Hero clerkHero = new Hero("clerk2", 5000);

        var pay = new GwpAssetPayment(Hero.MainHero, clerkHero, payFrom, payTo, 2400, 2400);
        Equal(1000 + 2 * 400 + 10 * 20, pay.Available, "available wealth counts purse plus goods at item value");
        Equal(false, pay.Applied, "opening the table transfers nothing");
        Equal(0, pay.Paid, "nothing is receipted before a commit");

        var suggestion = pay.SuggestedOffer();
        Equal(true, suggestion.Count > 0, "an auto offer is produced for a solvent payer");
        int suggested = 0;
        foreach (var entry in suggestion)
        {
            entry.Key.CurrentAmount = entry.Value;
            entry.Key.SetIsOffered(true);
            suggested += entry.Key is TaleWorlds.CampaignSystem.BarterSystem.Barterables.ItemBarterable item
                ? entry.Value * Math.Max(1, item.ItemRosterElement.EquipmentElement.ItemValue)
                : entry.Value;
        }
        Equal(true, suggested <= 2400, "the auto offer never exceeds the amount owed");
        Equal(true, pay.Valid, "the auto offer is a valid selection");

        // 超过上限的选择不能提交，也不能转走任何东西。
        var over = new GwpAssetPayment(Hero.MainHero, clerkHero, payFrom, payTo, 500, 500);
        var overMoney = over.Entries[0];
        overMoney.CurrentAmount = 900; overMoney.SetIsOffered(true);
        Equal(false, over.Valid, "an offer above the ceiling is invalid");
        int goldBefore = Hero.MainHero.Gold;
        over.Entries.Last().Apply();
        Equal(false, over.Applied, "an invalid selection cannot be committed");
        Equal(goldBefore, Hero.MainHero.Gold, "a rejected selection moves no money");

        // 付不出的钱不能选。
        var broke = new GwpAssetPayment(Hero.MainHero, clerkHero, payFrom, payTo, 100000, 100000);
        var brokeMoney = broke.Entries[0];
        brokeMoney.CurrentAmount = Hero.MainHero.Gold + 1; brokeMoney.SetIsOffered(true);
        Equal(false, broke.Valid, "a payer cannot offer more gold than he holds");

        // 正常提交：钱与货各转一次，收据等于实际净额。
        var commit = new GwpAssetPayment(Hero.MainHero, clerkHero, payFrom, payTo, 2400, 2400);
        var commitMoney = commit.Entries[0];
        commitMoney.CurrentAmount = 200; commitMoney.SetIsOffered(true);
        var commitHorse = commit.Entries.OfType<TaleWorlds.CampaignSystem.BarterSystem.Barterables.ItemBarterable>()
            .First(e => e.ItemRosterElement.EquipmentElement.Item == horse);
        commitHorse.CurrentAmount = 1; commitHorse.SetIsOffered(true);
        Equal(true, commit.Valid, "money plus one horse is within the ceiling");
        int purseBefore = Hero.MainHero.Gold;
        int horsesBefore = payFrom.ItemRoster.Where(e => e.EquipmentElement.Item == horse).Sum(e => e.Amount);
        commit.Entries.Last().Apply();
        Equal(true, commit.Applied, "a valid selection commits");
        Equal(600, commit.Paid, "the receipt equals the value actually handed over");
        Equal(purseBefore - 200, Hero.MainHero.Gold, "the offered money leaves the payer once");
        Equal(horsesBefore - 1, payFrom.ItemRoster.Where(e => e.EquipmentElement.Item == horse).Sum(e => e.Amount),
            "the offered horse leaves the payer inventory once");
        Equal(1, payTo.ItemRoster.Where(e => e.EquipmentElement.Item == horse).Sum(e => e.Amount),
            "the offered horse reaches the receiver once");
        commit.Entries.Last().Apply();
        Equal(600, commit.Paid, "committing twice does not double the receipt");
        Equal(purseBefore - 200, Hero.MainHero.Gold, "committing twice does not move money twice");

        // Courier selection uses the actual native-barter payment implementation,
        // but does not transfer to the remote lord or take his money as change.
        var selection = new GwpAssetPayment(Hero.MainHero, clerkHero, payFrom, payTo, int.MaxValue, 850, true);
        Equal(true, selection.Entries.All(e => e.OriginalOwner == Hero.MainHero), "courier only offers player assets");
        int plannedGoldBefore = Hero.MainHero.Gold;
        int plannedGoodsBefore = payFrom.ItemRoster.Sum(e => e.Amount);
        foreach (var offer in selection.SuggestedOffer())
        {
            offer.Key.CurrentAmount = offer.Value;
            offer.Key.SetIsOffered(true);
        }
        selection.ConfirmSelection();
        Equal(true, selection.Applied, "courier accepts a mixed manifest");
        Equal(true, selection.Paid >= 850, "indivisible cargo rounds up without remote change");
        Equal(plannedGoldBefore, Hero.MainHero.Gold, "selection does not debit gold before departure");
        Equal(plannedGoodsBefore, payFrom.ItemRoster.Sum(e => e.Amount), "selection does not teleport goods");
        Equal(plannedGoldBefore, selection.SelectedGold, "manifest carries the chosen gold");
        Equal(true, selection.SelectedGoods.Count > 0, "manifest includes actual goods");
        var zero = new GwpAssetPayment(Hero.MainHero, clerkHero, payFrom, payTo, int.MaxValue, 850, true);
        zero.ConfirmSelection();
        Equal(true, zero.Applied, "zero hand-in remains a valid deliberate choice");
        Equal(0, zero.Paid, "zero hand-in credits nothing");
        var cargoRecord = new GwpDispatchRecord { PartyId = "courier", CargoState = "aXRlbQkxCTEwMA==" };
        Equal(cargoRecord.CargoState, GwpDispatchRecord.Deserialize(cargoRecord.Serialize())!.CargoState, "cargo survives dispatch serialization");
        Equal(string.Empty, GwpDispatchRecord.Deserialize("old|0|0|receiver|500|0||1")!.CargoState, "old dispatch saves have no cargo");

        var foodItem = new ItemObject { StringId = "cargo_grain", Value = 20, IsFood = true };
        var modifier = new ItemModifier { StringId = "fine" };
        TaleWorlds.ObjectSystem.MBObjectManager.Instance.Objects[foodItem.StringId] = foodItem;
        TaleWorlds.ObjectSystem.MBObjectManager.Instance.Objects[modifier.StringId] = modifier;
        var courier = new TaleWorlds.CampaignSystem.Party.MobileParty();
        var equipment = new EquipmentElement(foodItem, modifier);
        courier.ItemRoster.AddToCounts(equipment, 12);
        string manifest = GwpDispatchCargo.Encode(new[] { new GwpDispatchCargo.Entry { Item = equipment, Amount = 10, Price = 20 } });
        Equal(false, manifest.Contains('|') || manifest.Contains(';'), "manifest cannot break outer save delimiters");
        var restored = GwpDispatchCargo.Decode(manifest);
        Equal(1, restored.Count, "manifest restores every stack");
        Equal(modifier, restored[0].Item.ItemModifier, "manifest preserves item modifiers");
        Equal(2, GwpDispatchCargo.Food(courier, manifest), "ten cargo grain leave two usable rations");
        Equal(200, GwpDispatchCargo.Value(courier, manifest), "only the designated cargo is credited");
        foodItem.Value = 99;
        Equal(200, GwpDispatchCargo.Value(courier, manifest), "shipment keeps agreed valuation");
        GwpWardenDispatchBehavior.Instance.CargoState = manifest;
        var prefix = typeof(GwpDispatchCargoFoodPatch).GetMethod("Prefix", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var finalizer = typeof(GwpDispatchCargoFoodPatch).GetMethod("Finalizer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        object?[] call = { courier, null };
        prefix.Invoke(null, call);
        Equal(2, courier.ItemRoster.TotalFood, "native feeding only sees the rations");
        courier.ItemRoster.AddToCounts(equipment, -1);
        var nativeFailure = new InvalidOperationException("test native failure");
        Equal(nativeFailure, finalizer.Invoke(null, new[] { courier, call[1], nativeFailure }), "native exception is not swallowed");
        Equal(11, courier.ItemRoster.TotalFood, "cargo returns even after feeding fails");
        courier.ItemRoster.AddToCounts(equipment, -8);
        Equal(60, GwpDispatchCargo.Value(courier, manifest), "lost cargo is never credited or recreated");
        Equal(0, GwpDispatchCargo.Food(courier, manifest), "remaining cargo is not considered provisions");
        Equal(0, GwpDispatchCargo.Decode("").Count, "old saves need no manifest migration");

        TestItemizedReports();
        TestBarterVmEntry();
        Console.WriteLine("PASS: " + checks + " production settlement/collection assertions (engine actors stubbed).");
    }

    private static void TestBarterVmEntry()
    {
        // Invoke production VM prefixes directly: no manager Harmony patch is
        // installed in this harness, matching the observed bypass condition.
        var offer = typeof(GwpAssetOfferValidationPatch).GetMethod("Prefix",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var cancel = typeof(GwpDispatchVmCancelPatch).GetMethod("Prefix",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        Campaign.Current = new Campaign();
        Hero.MainHero = new Hero("vm_player", 2000);
        var receiver = new Hero("vm_receiver", 0);
        var from = new TaleWorlds.CampaignSystem.Party.PartyBase();
        var to = new TaleWorlds.CampaignSystem.Party.PartyBase();
        foreach (bool selectionOnly in new[] { true, false })
        {
            var payment = new GwpAssetPayment(Hero.MainHero, receiver, from, to, int.MaxValue,
                1300, selectionOnly, true);
            var data = new TaleWorlds.CampaignSystem.BarterSystem.BarterData();
            payment.PrepareCatalogue(data);
            var money = payment.Entries[0]; money.SetIsOffered(true);
            var vm = new TaleWorlds.CampaignSystem.ViewModelCollection.Barter.BarterVM();
            vm.RightOfferList.Add(new() { Barterable = money, CurrentOfferedAmount = 1300 });
            int before = Hero.MainHero.Gold;
            Campaign.Current.ConversationManager.IsConversationInProgress = !selectionOnly;
            Equal(false, (bool)offer.Invoke(null, new object[] { vm, data })!, "VM owns report commit without manager detour");
            Equal(true, payment.Applied, "VM confirms the recognized payment");
            Equal(1300, payment.Paid, "VM synchronizes displayed amount before confirming");
            Equal(before - (selectionOnly ? 0 : 1300), Hero.MainHero.Gold, "preview preserves cash; personal report transfers cash");
        }
        Equal(1, Campaign.Current.ConversationManager.Continuations, "only personal report continues conversation");
        var pending = new GwpAssetPayment(Hero.MainHero, receiver, from, to, int.MaxValue, 0, true, true);
        var pendingData = new TaleWorlds.CampaignSystem.BarterSystem.BarterData(); pending.PrepareCatalogue(pendingData);
        Equal(false, (bool)cancel.Invoke(null, new object[] { pendingData })!, "VM owns dispatch cancel without native cancel");
        Equal(false, pending.Applied, "cancel does not confirm assets");
        var invalidVm = new TaleWorlds.CampaignSystem.ViewModelCollection.Barter.BarterVM();
        pending.Entries[0].SetIsOffered(true);
        invalidVm.RightOfferList.Add(new() { Barterable = pending.Entries[0], CurrentOfferedAmount = 9999 });
        int closes = Campaign.Current.BarterManager.Closes;
        Equal(false, (bool)offer.Invoke(null, new object[] { invalidVm, pendingData })!, "invalid preview cannot enter native commit");
        Equal(closes, Campaign.Current.BarterManager.Closes, "invalid preview stays open");
        Equal(false, pending.Applied, "invalid preview is unapplied");
        var unrelated = new TaleWorlds.CampaignSystem.BarterSystem.BarterData();
        Equal(true, (bool)offer.Invoke(null, new object[] { invalidVm, unrelated })!, "unrelated offer passes through");
        Equal(true, (bool)cancel.Invoke(null, new object[] { unrelated })!, "unrelated cancel passes through");
    }

    private static void TestItemizedReports()
    {
        Hero.MainHero = new Hero("receipt_player", 9000);
        var offender = new Hero("receipt_offender", 300);
        var other = new Hero("receipt_other", 0);
        var receiver = new Hero("receipt_clerk", 2000);
        var from = new TaleWorlds.CampaignSystem.Party.PartyBase();
        var to = new TaleWorlds.CampaignSystem.Party.PartyBase();
        var horse = new ItemObject { StringId = "receipt_horse", Value = 100 };
        var modifier = new ItemModifier { StringId = "receipt_modifier" };
        var equipment = new EquipmentElement(horse, modifier);
        from.ItemRoster.AddToCounts(equipment, 4);
        var collect = new GwpAssetPayment(offender, Hero.MainHero, from, to, 1000);
        collect.Entries[0].SetIsOffered(true); collect.Entries[0].CurrentAmount = 200;
        collect.Entries[1].SetIsOffered(true); collect.Entries[1].CurrentAmount = 20;
        var goods = collect.Entries.OfType<TaleWorlds.CampaignSystem.BarterSystem.Barterables.ItemBarterable>().First();
        goods.SetIsOffered(true); goods.CurrentAmount = 2;
        goods.Apply();
        Equal(180, collect.Receipt!.Gold, "receipt records net cash after change");
        Equal(2, collect.Receipt.Items.Single().Amount, "receipt records actual item count");
        Equal(modifier.StringId, collect.Receipt.Items.Single().Modifier, "receipt keeps modifier identity");
        var ledger = new GwpFieldReportLedger();
        ledger.RecordSettlement(offender, 1000, 380, 1000, true, receipt: collect.Receipt);
        ledger.RecordSettlement(offender, 1000, 20, 1000, true, receipt: new GwpCaseReceipt { Gold = 20 });
        ledger.RecordSettlement(other, 1000, 900, 1000, false, receipt: new GwpCaseReceipt { Gold = 900 });
        var saved = new MemoryStore(true); ledger.SyncData(saved);
        var loaded = new GwpFieldReportLedger(); loaded.SyncData(saved.Load());
        var receipt = loaded.ReceiptFor(offender.StringId)!;
        Equal(200, receipt.Gold, "multiple collection cash survives save load");
        Equal(2, receipt.Items.Single().Amount, "items survive save load without duplication");
        Equal(400, loaded.PendingReceivedFor(offender.StringId), "case totals exclude other case");
        to.ItemRoster.AddToCounts(equipment, -1);
        to.ItemRoster.AddToCounts(new EquipmentElement(horse), 10);
        horse.Value = 300;
        var prisoner = new Hero("receipt_prisoner", 0) { IsPrisoner = true, PartyBelongedToAsPrisoner = to };
        var report = new GwpAssetPayment(Hero.MainHero, receiver, to, from, int.MaxValue, 400, true, true, receipt, prisoner);
        foreach (var pair in report.SuggestedOffer()) { pair.Key.SetIsOffered(true); pair.Key.CurrentAmount = pair.Value; }
        Equal(200, report.SelectedGold, "auto selects receipt cash not player's entire purse");
        Equal(1, report.SelectedGoods.Sum(e => e.Amount), "missing goods are not replaced by cash or different modifiers");
        Equal(prisoner, report.SelectedPrisoner, "auto selects case prisoner alongside money and goods");
        Equal(300, report.OfferedValue, "receipt valuation stays fixed; prisoner has no cash credit");
        int before = Hero.MainHero.Gold;
        report.ConfirmSelection();
        Equal(before, Hero.MainHero.Gold, "courier preview still does not take money");
        Equal(to, prisoner.PartyBelongedToAsPrisoner, "courier preview does not move prisoner");
        var personal = new GwpAssetPayment(Hero.MainHero, receiver, to, from, int.MaxValue, 400, false, true, receipt, prisoner);
        foreach (var pair in personal.SuggestedOffer()) { pair.Key.SetIsOffered(true); pair.Key.CurrentAmount = pair.Value; }
        personal.ConfirmSelection(); personal.ConfirmSelection();
        Equal(before - 200, Hero.MainHero.Gold, "personal hand-in transfers exact cash once");
        Equal(from, prisoner.PartyBelongedToAsPrisoner, "personal hand-in transfers actual custody");
        Equal(300, personal.Paid, "personal and courier credit the same assets");
        loaded.DeclareAmount(300, true, offender.StringId);
        Equal(0, loaded.RecentLieCount, "admitting withheld assets is not recorded as a lie");
        Equal(100, loaded.PendingAuditGap, "admitted withholding is still audited");
        Equal(900, loaded.PendingReceivedFor(other.StringId), "hand-in never clears another case receipt");
        Equal(true, loaded.ReceiptFor(other.StringId) != null, "other case retains itemization");
        var legacy = new GwpFieldReportLedger();
        legacy.RecordSettlement(offender, 1000, 400, 1000, true);
        Equal<GwpCaseReceipt?>(null, legacy.ReceiptFor(offender.StringId), "legacy totals do not invent original cash");
        var manual = new GwpAssetPayment(Hero.MainHero, receiver, to, from, int.MaxValue, 400, true, true, null);
        Equal(0, manual.SuggestedOffer().Count, "unknown receipt auto selects no arbitrary assets");
        manual.ConfirmSelection();
        Equal(true, manual.Applied, "zero hand-in remains confirmable");
        var honestPrisoner = new GwpFieldReportLedger();
        honestPrisoner.RecordSettlement(offender, 1000, 400, 1000, true);
        Equal(true, honestPrisoner.NeedsExplanation(offender.StringId, 400, false), "poverty can be explained without theft");
        honestPrisoner.ResolveByPrisoner(offender.StringId, 100, true);
        Equal(300, honestPrisoner.PendingAuditGap, "prisoner does not erase previously collected assets");
        Equal(0, honestPrisoner.RecentLieCount, "truthful prisoner shortfall is not a lie");
        Equal<GwpCaseReceipt?>(null, GwpCaseReceipt.Decode(""), "missing receipt remains distinct from empty receipt");
        Equal(0, GwpCaseReceipt.Decode(new GwpCaseReceipt().Encode())!.Gold, "known empty receipt roundtrips");
    }
}

internal sealed class MemoryStore : IDataStore
{
    private readonly Dictionary<string, object?> values;
    public bool IsSaving { get; }
    public bool IsLoading => !IsSaving;
    internal MemoryStore(bool saving, Dictionary<string, object?>? data = null) { IsSaving = saving; values = data ?? new(); }
    internal MemoryStore Load() => new(false, values);
    public void SyncData<T>(string key, ref T value)
    {
        if (IsSaving) values[key] = value;
        else if (values.TryGetValue(key, out object? saved)) value = (T)saved!;
    }
}
