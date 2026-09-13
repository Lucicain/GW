using System;
using System.Collections.Generic;
using GreyWardenPolicePurity;
using TaleWorlds.CampaignSystem;

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

        var ledger = new GwpFieldReportLedger();
        CrimePool.GetOrCreateHistory(offender).NegativeStanding = 4;
        ledger.RecordSettlement(offender, 2400, 600, 1200, true);
        int before = GwpRuntimeState.Player.Reputation;
        Equal(0, ledger.DeclareAmount(600, truthful: true), "truthful genuine poverty is exempt");
        Equal(before, GwpRuntimeState.Player.Reputation, "poor offender does not penalize hunter");
        Equal(4, CrimePool.GetOrCreateHistory(offender).NegativeStanding, "unpaid offender standing remains");
        Equal(0, ledger.PendingAuditGap, "truthful short payment is not a false report");
        Equal(false, ledger.HasPendingReports, "truthful poverty hand-in closes report");
        ledger.RecordSettlement(offender, 2400, 300, 1200, true);
        Equal(0, ledger.DeclareAmount(300, true), "discretionary collection from genuinely poor offender stays truthful");
        ledger.RecordSettlement(offender, 2400, 600, 1200, true);
        ledger.DeclareFalseAmount(300);
        Equal(300, ledger.PendingAuditGap, "only concealed money audited for genuinely poor offender");
        ledger.ResolveAudit(offender.StringId, 0f);
        Equal(before - 1, GwpRuntimeState.Player.Reputation, "caught concealment costs exact difference standing");
        ledger.ResolveAudit(offender.StringId, 0f);
        Equal(before - 1, GwpRuntimeState.Player.Reputation, "same audit cannot charge twice");
        ledger.RecordSettlement(offender, 2400, 1200, 1200, false);
        Equal(4, ledger.DeclareAmount(1200, true), "rich offender's reduced deal is not poverty exemption");
        ledger.RecordSettlement(offender, 2400, 600, 1200, true);
        Equal(0, ledger.DeclareAmount(2400, true), "player may cover full fine from own money");
        Equal(0, CrimePool.GetOrCreateHistory(offender).NegativeStanding, "top-up clears covered standing");

        ledger.RecordSettlement(offender, 2400, 1200, 1200, false);
        ledger.DeclareFalseAmount(600);
        ledger.RecordSettlement(offender, 2400, 600, 1200, true);
        ledger.DeclareAmount(600, true);
        Equal(1800, ledger.PendingAuditGap, "later truthful report cannot erase earlier false one");
        var save = new MemoryStore(true); ledger.SyncData(save);
        var loaded = new GwpFieldReportLedger(); loaded.SyncData(save.Load());
        Equal(1800, loaded.PendingAuditGap, "audit gap survives save");
        before = GwpRuntimeState.Player.Reputation;
        loaded.RegisterEvents();
        CampaignTime.Now = new CampaignTime { ToHours = 24 * 6 };
        CampaignEvents.DailyTickEvent.Fire();
        Equal(1800, loaded.PendingAuditGap, "audit does not run before scheduled day");
        CampaignTime.Now = new CampaignTime { ToHours = 24 * 7 };
        TaleWorlds.Core.MBRandom.RandomFloat = 0f;
        CampaignEvents.DailyTickEvent.Fire();
        Equal(before - 6, GwpRuntimeState.Player.Reputation, "scheduled discovery applies one difference penalty");
        Equal(0, loaded.PendingAuditGap, "completed audit removed");
        loaded.RecordSettlement(offender, 2400, 1200, 1200, false);
        loaded.DeclareFalseAmount(0);
        before = GwpRuntimeState.Player.Reputation;
        loaded.ResolveAudit(offender.StringId, 0.99f);
        Equal(before, GwpRuntimeState.Player.Reputation, "failed audit draw does not penalize");
        Equal(0, loaded.PendingAuditGap, "single audit chance is not eventual guaranteed discovery");

        loaded.RecordSettlement(offender, 2400, 2400, 1200, false);
        loaded.ResolveByPrisoner(offender.StringId, 0);
        Equal(2400, loaded.PendingAuditGap, "prisoner cannot erase prior cash collection");
        loaded.ResolveAudit(offender.StringId, 0.99f);
        loaded.RecordSettlement(offender, 2400, 2400, 1200, false);
        loaded.ResolveByPrisoner(offender.StringId, 2400);
        Equal(0, loaded.PendingAuditGap, "prisoner and full received cash leave no concealed gap");
        loaded.RecordSettlement(offender, 2400, 0, 1200, false, 360);
        var receiptSave = new MemoryStore(true); loaded.SyncData(receiptSave);
        var restored = new GwpFieldReportLedger(); restored.SyncData(receiptSave.Load());
        Equal(360, restored.TotalReceived, "private bribe receipt survives save");
        restored.ResolveByPrisoner(offender.StringId, 60);
        Equal(300, restored.PendingAuditGap, "private money also accountable after prisoner delivery");
        restored.ResolveByPrisoner(offender.StringId, 0);
        Equal(300, restored.PendingAuditGap, "repeated prisoner receipt does not duplicate debt");

        var deposit = new GwpFieldFineBarterable(clerk, 2400) { CurrentAmount = 1200 };
        before = Hero.MainHero.Gold;
        Equal(0, deposit.GetUnitValueForFaction(clerk.Clan), "fine does not produce native gift-overpay credit");
        deposit.Apply(); deposit.Apply();
        Equal(before - 1200, Hero.MainHero.Gold, "hand-in transfers once");
        Equal(1200, deposit.Paid, "hand-in amount recorded exactly");
        Hero.MainHero.Gold = 75;
        var capped = new GwpFieldFineBarterable(clerk, 2400) { CurrentAmount = 2400 }; capped.Apply();
        Equal(75, capped.Paid, "hand-in bounded by current wallet");
        Equal(0, Hero.MainHero.Gold, "hand-in cannot overdraw wallet");
        Equal(880, GwpCaseSettlementRules.Reward(2400, 0, 4), "existing expense scale retained");
        Equal(600, GwpCaseSettlementRules.Reward(600, 30, 4), "expenses capped by delivered cash");
        GwpTuning.FieldArrest.ImmediateAuditTesting = true;
        var immediate = new GwpFieldReportLedger();
        immediate.RecordSettlement(offender, 2400, 2400, 1200, false);
        before = GwpRuntimeState.Player.Reputation;
        immediate.DeclareFalseAmount(1071);
        Equal(before - 4, GwpRuntimeState.Player.Reputation, "test audit immediately discovers 1329 concealed denars");
        Equal(0, immediate.PendingAuditGap, "immediate audit cannot recur later");
        immediate.RecordSettlement(offender, 2400, 600, 1200, true);
        before = GwpRuntimeState.Player.Reputation;
        immediate.DeclareAmount(600, true);
        Equal(before, GwpRuntimeState.Player.Reputation, "immediate audit preserves genuine poverty exemption");
        immediate.RecordSettlement(offender, 2400, 2400, 1200, false);
        immediate.ResolveByPrisoner(offender.StringId, 0);
        Equal(before - 8, GwpRuntimeState.Player.Reputation, "prisoner concealment also discovered at hand-in");
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
