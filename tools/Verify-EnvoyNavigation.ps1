<# Calls installed native transition/ship-transfer methods and production reunion policy offline.
   No game, campaign save, scene or UI is opened. Scene pathfinding and unrelated lord eligibility are controlled boundaries. #>
[CmdletBinding()]
param(
    [string]$ModuleDll = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden\bin\Win64_Shipping_Client\GreyWardenPolicePurity.dll',
    [string]$GameFolder = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord',
    [switch]$ReproduceOnly,
    [switch]$ReproduceDispatchOnly
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Run in Windows PowerShell 5.1 (net472).' }
$gameBin = Join-Path $GameFolder 'bin\Win64_Shipping_Client'
$references = @(Get-ChildItem $gameBin -Filter 'TaleWorlds.*.dll' |
    Where-Object { $_.Name -ne 'TaleWorlds.Native.dll' } | ForEach-Object { $_.FullName })
foreach ($path in $references) { [void][Reflection.Assembly]::LoadFrom($path) }
$navalDll = Join-Path $GameFolder 'Modules\NavalDLC\bin\Win64_Shipping_Client\NavalDLC.dll'
$harmonyDll = Join-Path (Split-Path $ModuleDll) '0Harmony.dll'
[void][Reflection.Assembly]::LoadFrom($navalDll)
[void][Reflection.Assembly]::LoadFrom($harmonyDll)
[void][Reflection.Assembly]::LoadFrom($ModuleDll)
$framework = [Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$references += @('mscorlib.dll', 'System.dll', 'System.Core.dll') | ForEach-Object { Join-Path $framework $_ }
$references += Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll'
$references += @($navalDll, $harmonyDll)
Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

public static class EnvoyNavigationRegression
{
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static object tracker;
    static readonly Assembly Mod = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "GreyWardenPolicePurity");
    static readonly Assembly Naval = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "NavalDLC");
    static readonly Type Resource = Mod.GetType("GreyWardenPolicePurity.PoliceResourceManager", true);
    static readonly Type Enforcement = Mod.GetType("GreyWardenPolicePurity.PoliceEnforcementBehavior", true);
    static readonly Type Dispatch = Mod.GetType("GreyWardenPolicePurity.GwpWardenDispatchBehavior", true);
    static bool probingHandover;
    static int prematureHandovers;
    static object Empty(Type t) { return FormatterServices.GetUninitializedObject(t); }
    static FieldInfo Field(Type t, string name)
    {
        for (; t != null; t = t.BaseType) { var f = t.GetField(name, Any); if (f != null) return f; }
        throw new Exception("Missing field " + name);
    }
    static void Set(object owner, string name, object value) { Field(owner.GetType(), name).SetValue(owner, value); }
    static void Property(object owner, string name, object value) { owner.GetType().GetProperty(name, Any).SetValue(owner, value, null); }
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static void Clock(float hours)
    {
        long ticks = (long)typeof(CampaignTime).GetField("TimeTicksPerHour", Any).GetValue(null);
        Set(tracker, "_numTicks", (long)(hours * ticks));
    }
    static void World()
    {
        var campaign = (Campaign)Empty(typeof(Campaign));
        typeof(Campaign).GetProperty("Current", Any).SetValue(null, campaign, null);
        var models = (GameModels)Empty(typeof(GameModels));
        Set(campaign, "_gameModels", models);
        Property(models, "PartyNavigationModel", Activator.CreateInstance(Naval.GetType("NavalDLC.GameComponents.NavalPartyNavigationModel", true), new object[] { new DefaultPartyNavigationModel() }));
        Property(models, "PartyTransitionModel", Activator.CreateInstance(Naval.GetType("NavalDLC.GameComponents.NavalDLCPartyTransitionModel", true)));
        Property(models, "EncounterModel", new DefaultEncounterModel());
        // Only elapsed-hour comparisons are exercised; no campaign calendar/database is loaded.
        var calendar = new DefaultCampaignTimeModel();
        long ticksPerHour = calendar.TimeTicksPerMillisecond * calendar.MillisecondInSecond * calendar.SecondsInMinute * calendar.MinutesInHour;
        typeof(CampaignTime).GetField("TimeTicksPerHour", Any).SetValue(null, ticksPerHour);
        tracker = Empty(typeof(Campaign).Assembly.GetType("TaleWorlds.CampaignSystem.MapTimeTracker", true));
        Property(campaign, "MapTimeTracker", tracker);
        var events = (CampaignEventDispatcher)Empty(typeof(CampaignEventDispatcher));
        Set(events, "_eventReceivers", new CampaignEventReceiver[0]);
        Property(campaign, "CampaignEventDispatcher", events);
        var objects = (CampaignObjectManager)Empty(typeof(CampaignObjectManager));
        Set(objects, "_mobileParties", new MBList<MobileParty>());
        Property(campaign, "CampaignObjectManager", objects);
        Clock(150f);
    }
    static MobileParty Party(string id, bool sea)
    {
        var p = (MobileParty)Empty(typeof(MobileParty));
        p.StringId = id;
        p.IsActive = true;
        Set(p, "_isCurrentlyAtSea", sea);
        Set(p, "_attachedParties", new MBList<MobileParty>());
        Property(p, "HasLandNavigationCapability", true);
        var rosterOwner = (PartyBase)Empty(typeof(PartyBase));
        Property(rosterOwner, "MobileParty", p);
        Set(rosterOwner, "_ships", new MBList<Ship>());
        Property(p, "Party", rosterOwner);
        Property(p, "Anchor", new AnchorPoint(p));
        Set(p, "_position", new CampaignVec2(new Vec2(398.2438f, 344.4264f), !sea));
        var pathType = typeof(MobileParty).GetField("Path", Any).FieldType;
        typeof(MobileParty).GetField("Path", Any).SetValue(p, Empty(pathType));
        return p;
    }
    static Ship Ship(MobileParty p)
    {
        var s = (Ship)Empty(typeof(Ship));
        Set(s, "_owner", p.Party);
        Set(s, "_shipPieces", new Dictionary<string, ShipUpgradePiece>());
        typeof(PartyBase).GetMethod("AddShipInternal", Any).Invoke(p.Party, new object[] { s });
        return s;
    }
    static void Returning(MobileParty p, MobileParty lender)
    {
        Property(Campaign.Current, "MainParty", lender);
        var resources = Empty(Resource);
        Set(resources, "_shipLenders", new Dictionary<string, string> { { p.StringId, lender.StringId } });
        Resource.GetField("_instance", Any).SetValue(null, resources);
    }
    static void ReturnShip(MobileParty p) { Resource.GetMethod("ReturnLentShips", Any).Invoke(null, new object[] { p }); }
    static void Transition(MobileParty p, float start, float duration)
    {
        Set(p, "_navigationTransitionStartTime", CampaignTime.Hours(start));
        Property(p, "NavigationTransitionDuration", CampaignTime.Hours(duration));
        Property(p, "EndPositionForNavigationTransition", new CampaignVec2(new Vec2(398.6f, 344.8f), p.IsCurrentlyAtSea));
    }
    static void Boarding(MobileParty p)
    {
        // The actual private setter asks the installed NavalDLC model for the duration.
        // An invalid anchor produces its native 48-hour embark duration without a scene query.
        Property(p, "NavigationTransitionStartTime", CampaignTime.Hours(100f));
        Property(p, "EndPositionForNavigationTransition", new CampaignVec2(new Vec2(398.6f, 344.8f), false));
        Check(p.NavigationTransitionDuration.ToHours == 48d, "actual NavalDLC computes boarding duration");
    }
    static int NativeTransitionTick(MobileParty p)
    {
        var cache = Empty(typeof(MobileParty).GetNestedType("CachedPartyVariables", Any));
        Set(cache, "NextPosition", p.Position);
        object[] args = { cache, -1, new MobileParty[2], 0.1f };
        typeof(MobileParty).GetMethod("CommonTransitioningPartyTick", Any).Invoke(p, args);
        return (int)args[1];
    }
    static bool NoScenePath(ref bool __result) { __result = false; return false; }
    static bool EligibleLord(ref bool __result) { __result = true; return false; }
    static void NativeFinish(MobileParty p) { typeof(MobileParty).GetMethod("FinishNavigationTransitionInternal", Any).Invoke(p, null); }
    static bool CanRejoin(MobileParty p, MobileParty lender)
    {
        return (bool)Mod.GetType("GreyWardenPolicePurity.GwpCommon", true)
            .GetMethod("CanCompletePartyHandover", Any).Invoke(null, new object[] { p, lender });
    }
    static bool StopAtHandoverBoundary()
    {
        if (!probingHandover) return true;
        prematureHandovers++;
        throw new InvalidOperationException("gwp-handover-boundary");
    }
    static void ProbeDispatch(MobileParty party, MobileParty player, bool outbound = false)
    {
        Type recordType = Mod.GetType("GreyWardenPolicePurity.GwpDispatchRecord", true);
        object record = Activator.CreateInstance(recordType, true);
        var phase = recordType.GetField("Phase", Any);
        phase.SetValue(record, Enum.Parse(phase.FieldType, "Returning"));
        var purpose = recordType.GetField("Purpose", Any);
        purpose.SetValue(record, Enum.Parse(purpose.FieldType, "PeaceRequest"));
        var records = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(recordType));
        records.Add(record);
        object behavior = Empty(Dispatch);
        Set(behavior, "_dispatches", records);
        probingHandover = true;
        try { Dispatch.GetMethod(outbound ? "DeliverTo" : "HandBackEverything", Any)
                .Invoke(behavior, new[] { record, party, player }); }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException == null || ex.InnerException.Message != "gwp-handover-boundary") throw;
        }
        finally { probingHandover = false; }
    }
    public static string Run(bool reproduceOnly, bool reproduceDispatchOnly)
    {
        World();
        var h = new Harmony("GreyWarden.Offline.EnvoyNavigation");
        var scenePath = typeof(MobileParty).GetMethods(Any).Single(m => m.Name == "ComputePath" && m.GetParameters().Length == 3);
        h.Patch(scenePath, new HarmonyMethod(typeof(EnvoyNavigationRegression).GetMethod("NoScenePath", Any)));
        h.Patch(Enforcement.GetMethod("CanContinueLeadingPoliceTask", Any), new HarmonyMethod(typeof(EnvoyNavigationRegression).GetMethod("EligibleLord", Any)));
        h.Patch(Dispatch.GetMethod("UntrackOnMap", Any), new HarmonyMethod(typeof(EnvoyNavigationRegression).GetMethod("StopAtHandoverBoundary", Any)));
        h.Patch(Mod.GetType("GreyWardenPolicePurity.PoliceAntiWarDeclaration", true)
            .GetMethod("ApplyWardenMediation", Any),
            new HarmonyMethod(typeof(EnvoyNavigationRegression).GetMethod("StopAtHandoverBoundary", Any)));
        try
        {
            if (reproduceDispatchOnly)
            {
                ProbeDispatch(Party("courier", false), Party("player", true));
                Check(prematureHandovers == 1, "old player courier enters handover across shoreline");
                return "PLAYER COURIER BUG REPRODUCED: actual production handover admitted opposite sea/land sides; stopped before assets moved";
            }
            var lender = Party("lender", false);
            var p = Party("gwp_enf_delay_offline", false);
            var ship = Ship(p);
            Returning(p, lender);
            Boarding(p); // native duration has elapsed, not yet consumed by native RealTick
            Check(!p.IsCurrentlyAtSea && p.IsTransitionInProgress && p.Ships.Count == 1, "native boarding remains land until finish");
            // Existing production land-only return condition admits this exact state.
            if (!p.IsCurrentlyAtSea) ReturnShip(p);
            Check(p.Ships.Count == 0 && lender.Ships.Single() == ship, "real production return transfers the only real ship");
            Check(NativeTransitionTick(p) == 0, "native completed transition queues finish even with no ships");
            NativeFinish(p);
            Check(p.IsCurrentlyAtSea && p.Ships.Count == 0, "BUG REPRODUCED: native finish boards the now-shipless party");
            Transition(p, 149f, 2f); // the next disembark attempt has not yet finished
            Check(NativeTransitionTick(p) == -1 && !p.IsTransitionInProgress, "native cancels shipless disembarking before two hours");
            if (reproduceOnly) return "ENVOY NATIVE BUG REPRODUCED: " + checks + " checks (actual game + production DLLs)";

            // Verify actual production policy across both sides and both transition directions.
            foreach (bool partySea in new[] { false, true })
            foreach (bool lenderSea in new[] { false, true })
            foreach (bool partyTransition in new[] { false, true })
            foreach (bool lenderTransition in new[] { false, true })
            foreach (bool partyPortExit in new[] { false, true })
            foreach (bool lenderPortExit in new[] { false, true })
            {
                var a = Party("a", partySea); var b = Party("b", lenderSea);
                if (partyTransition) Transition(a, 109f, 2f);
                if (lenderTransition) Transition(b, 109f, 2f);
                a.StartTransitionNextFrameToExitFromPort = partyPortExit;
                b.StartTransitionNextFrameToExitFromPort = lenderPortExit;
                bool expected = partySea == lenderSea && !partyTransition && !lenderTransition && !partyPortExit && !lenderPortExit;
                Check(CanRejoin(a, b) == expected, "production reunion must be on same side after both transitions finish");
                if (!expected)
                {
                    var realShip = Ship(a);
                    Returning(a, b);
                    var all = (MBList<MobileParty>)Field(typeof(CampaignObjectManager), "_mobileParties").GetValue(Campaign.Current.CampaignObjectManager);
                    all.Clear(); all.Add(a); all.Add(b);
                    Set(a, "_partyTradeGold", 25);
                    var state = Activator.CreateInstance(Enforcement.GetNestedType("DelayPatrolState", Any), true);
                    Property(state, "IsEnvoy", true); Property(state, "Returning", true);
                    Property(state, "SourceTaskPolicePartyId", b.StringId);
                    var enforcement = Empty(Enforcement);
                    bool handled = (bool)Enforcement.GetMethod("TryReturnImmediateInterceptorToSource", Any).Invoke(enforcement, new object[] { a, state });
                    Check(handled && a.Ships.Single() == realShip && b.Ships.Count == 0 && a.PartyTradeGold == 25,
                        "actual production reunion stops before ship, troops or money transfer while transition/shoreline mismatches");
                    int before = prematureHandovers;
                    ProbeDispatch(a, b);
                    Check(prematureHandovers == before,
                        "actual player courier return must stop before handover side effects during transitions or across shoreline");
                    ProbeDispatch(a, b, true);
                    Check(prematureHandovers == before,
                        "actual player courier delivery must stop before applying an errand during transitions or across shoreline");
                }
                else
                {
                    int before = prematureHandovers;
                    ProbeDispatch(a, b);
                    Check(prematureHandovers == before + 1, "stable same-side courier return reaches the handover boundary");
                    ProbeDispatch(a, b, true);
                    Check(prematureHandovers == before + 2, "stable same-side delivery reaches its errand boundary");
                }
            }
            // Complete boarding with the real ship retained; disembark, then hand the same ship back.
            p = Party("safe", false); lender = Party("lender2", false); ship = Ship(p); Returning(p, lender);
            Boarding(p);
            Check(!CanRejoin(p, lender) && p.Ships.Count == 1, "boarding party cannot hand its ship away");
            Check(NativeTransitionTick(p) == 0, "boarding reaches native completion"); NativeFinish(p);
            Check(p.IsCurrentlyAtSea && p.Ships.Single() == ship, "native boarding retains its actual ship");
            Check(!CanRejoin(p, lender), "across shoreline cannot complete handover");
            Transition(p, 149f, 2f);
            Check(!CanRejoin(p, lender) && p.Ships.Single() == ship, "disembarking cannot hand its real ship away");
            Clock(152f);
            Check(NativeTransitionTick(p) == 0, "native disembark can finish"); NativeFinish(p);
            Check(!p.IsCurrentlyAtSea && !p.IsTransitionInProgress && CanRejoin(p, lender), "landing completes before land handover");
            ReturnShip(p);
            Check(p.Ships.Count == 0 && lender.Ships.Single() == ship, "same real ship returns once after landing");
            Check(!p.IsTransitionInProgress && !p.HasNavalNavigationCapability, "shipless land party has no pending boarding");
            return "ENVOY NAVIGATION: PASS (" + checks + " checks; actual native transitions, real ship identity/ownership and production policy; scene pathfinding excluded)";
        }
        finally { h.UnpatchAll(h.Id); }
    }
}
'@
try { [EnvoyNavigationRegression]::Run($ReproduceOnly.IsPresent, $ReproduceDispatchOnly.IsPresent) }
catch { throw $_.Exception.ToString() }
