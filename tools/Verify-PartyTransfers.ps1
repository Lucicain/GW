<# Runs production troop transfers against actual net472 game rosters; no game or save is opened. #>
[CmdletBinding()]
param(
    [string]$ModuleDll = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden\bin\Win64_Shipping_Client\GreyWardenPolicePurity.dll',
    [string]$GameFolder = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord'
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Run in Windows PowerShell 5.1 (net472).' }
$gameBin = Join-Path $GameFolder 'bin\Win64_Shipping_Client'
$references = @(Get-ChildItem $gameBin -Filter 'TaleWorlds.*.dll' |
    Where-Object { $_.Name -ne 'TaleWorlds.Native.dll' } | ForEach-Object { $_.FullName })
foreach ($path in $references) { [void][Reflection.Assembly]::LoadFrom($path) }
[void][Reflection.Assembly]::LoadFrom($ModuleDll)
$framework = [Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$references += @('mscorlib.dll', 'System.dll', 'System.Core.dll') | ForEach-Object { Join-Path $framework $_ }
$references += Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll'
Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;

public static class PartyTransferRegression
{
    static int checks;
    static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Type Owner = AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "GreyWardenPolicePurity").GetType("GreyWardenPolicePurity.GwpTroopTransfer", true);
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static CharacterObject Troop() { return (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject)); }
    static TroopRoster Roster(CharacterObject troop, int number, int wounded, int xp)
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        roster.AddToCounts(troop, number, false, wounded, xp);
        return roster;
    }
    static TroopRosterElement State(TroopRoster roster, CharacterObject troop)
    {
        int index = roster.FindIndexOfTroop(troop);
        return index < 0 ? new TroopRosterElement(troop) : roster.GetElementCopyAtIndex(index);
    }
    static TroopRosterElement Move(TroopRoster from, TroopRoster to, CharacterObject troop, int count, bool healthy, int? xp)
    {
        return (TroopRosterElement)Owner.GetMethod("Move", Hidden).Invoke(null, new object[] { from, to, troop, count, healthy, xp });
    }
    public static string Run()
    {
        var troop = Troop();
        // Full moves preserve the division remainder, and partial moves never heal a wound.
        foreach (int count in new[] { 1, 6, 7, 10, 20 })
        foreach (bool healthy in new[] { false, true })
        {
            var from = Roster(troop, 10, 4, 907);
            var to = Roster(troop, 3, 2, 111);
            var moved = Move(from, to, troop, count, healthy, null);
            var a = State(from, troop); var b = State(to, troop);
            int expected = Math.Min(count, healthy ? 6 : 10);
            Check(moved.Number == expected, "available-count cap");
            Check(a.Number + b.Number == 13, "soldiers conserved");
            Check(a.WoundedNumber + b.WoundedNumber == 6, "wounds conserved");
            Check(a.Xp + b.Xp == 1018, "experience conserved");
            Check(moved.WoundedNumber == Math.Max(0, expected - 6), "healthy first");
            Check(moved.Xp == (expected == 10 ? 907 : expected * 90), "native per-soldier division; whole remainder follows full move");
        }
        // Joining two different sources must allow the creation rollback to restore each one's exact XP.
        var first = Roster(troop, 6, 0, 109);
        var second = Roster(troop, 4, 0, 301);
        var escort = TroopRoster.CreateDummyTroopRoster();
        var one = Move(first, escort, troop, 3, true, null);
        var two = Move(second, escort, troop, 4, true, null);
        Move(escort, second, troop, two.Number, true, two.Xp);
        Move(escort, first, troop, one.Number, true, one.Xp);
        Check(State(first, troop).Number == 6 && State(first, troop).Xp == 109, "first source rollback");
        Check(State(second, troop).Number == 4 && State(second, troop).Xp == 301, "second source rollback");
        Check(escort.TotalManCount == 0, "rollback empties new party");
        var wounded = Roster(troop, 4, 4, 123);
        Check(Move(wounded, escort, troop, 10, true, null).Number == 0, "healthy-only excludes wounded");
        Check(Move(wounded, wounded, troop, 10, false, null).Number == 0, "self move is a no-op");
        Check(Move(wounded, escort, Troop(), 10, false, null).Number == 0, "absent troop is a no-op");
        Check(Move(wounded, escort, troop, 0, false, null).Number == 0, "zero request is a no-op");
        var other = Troop();
        wounded.AddToCounts(other, 3, false, 1, 205);
        int taken = (int)Owner.GetMethod("MoveBatches", Hidden).Invoke(null,
            new object[] { wounded, escort, wounded.GetTroopRoster(), 10, false });
        Check(taken == 7 && wounded.TotalManCount == 0 && escort.TotalManCount == 7, "full multi-batch count");
        Check(State(escort, troop).WoundedNumber + State(escort, other).WoundedNumber == 5, "multi-batch wounds");
        Check(State(escort, troop).Xp + State(escort, other).Xp == 328, "multi-batch XP");

        // The original registry supplies unique IDs across prefixes and existing suffixes.
        var manager = new CampaignObjectManager();
        var parties = (MBList<MobileParty>)typeof(CampaignObjectManager).GetField("_mobileParties", Hidden).GetValue(manager);
        foreach (string id in new[] { "watch1", "watch123456", "courier1" })
        {
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            party.StringId = id; parties.Add(party);
        }
        typeof(CampaignObjectManager).GetMethod("InitializeManagerObjectLists", Hidden).Invoke(manager, null);
        Check(manager.FindNextUniqueStringId<MobileParty>("watch1") == "watch123457", "native registered suffix bound");
        Check(manager.FindNextUniqueStringId<MobileParty>("courier1") == "courier123457", "native cross-prefix bound");
        Check(manager.FindNextUniqueStringId<MobileParty>("new1") == "new1", "unused prefix");
        var identity = Owner.Assembly.GetType("GreyWardenPolicePurity.GwpPartyIdentity", true)
            .GetMethod("Next", Hidden, null, new[] { typeof(CampaignObjectManager), typeof(string) }, null);
        for (int i = 0; i < 100; i++)
        {
            string id = (string)identity.Invoke(null, new object[] { manager, "watch" });
            Check(parties.All(p => p.StringId != id), "renamed party ID must not collide");
            var created = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            created.StringId = id; parties.Add(created);
            // Deliberately do not update the registry's suffix bound: post-creation rename does not.
        }
        return "PARTY_TRANSFER_ASSERTIONS=" + checks + "; PASS";
    }
}
'@
[PartyTransferRegression]::Run()
