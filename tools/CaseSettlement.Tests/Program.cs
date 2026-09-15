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

        Console.WriteLine("PASS: " + checks + " production settlement/collection assertions (engine actors stubbed).");
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
