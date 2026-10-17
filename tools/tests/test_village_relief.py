"""Regress relief progress, explicit visit target and same-build save/load.

Movement and adoption side effects are controlled engine boundaries.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from test_battle_peace import extract
from test_assistance import member

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'

HARNESS = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace GreyWardenPolicePurity {
class Vec{public float X;public float Distance(Vec b)=>Math.Abs(X-b.X);}
class Settlement{public bool IsVillage=true;public Vec GetPosition2D=new();}
class MapEvent{public bool IsFinalized;}
class Hero{public bool IsActive=true,IsPrisoner;}
class MobileParty{public string StringId="joel";public bool IsActive=true;public Vec GetPosition2D=new();public Hero? LeaderHero=new();public Settlement? CurrentSettlement;public MapEvent? MapEvent;}
class CampaignTime{public static CampaignTime Now=new();public double ToHours;}
class CampaignGameStarter{}
interface IDataStore{bool IsSaving{get;}bool IsLoading{get;}void SyncData<T>(string key,ref T value);}
class Store:IDataStore{public bool IsSaving{get;set;}public bool IsLoading=>!IsSaving;public Dictionary<string,object> Data=new();public void SyncData<T>(string k,ref T v){if(IsSaving)Data[k]=v!;else v=(T)Data[k];}}
static class GreyWardenFamilyBehavior{public static void RefreshPoliceClanFamilyPresentation(){}}
static class GwpCommon{public static bool IsPatrolParty(MobileParty p)=>false;public static bool IsEnforcementDelayPatrolParty(MobileParty p)=>false;}
static class GwpWardenDuties{public static bool Ready=true;public static bool CanContinueRelief(MobileParty p)=>Ready;}
static class GwpTuning{public static class Family{public const float VillageReliefStayHours=STAY;public const float VillageReliefArrivalDistance=RADIUS;}}
class GreyWardenVillageAdoptionBehavior{
 CONSTANTS
 class ActiveVillageRelief{public string PolicePartyId="joel",VillageSettlementId="village",VillageName="Village";public bool ReliefStarted;public double RemainingHours=72,LastWorkHours=-1,QueuedTimeHours=90;}
 class PendingVillageRelief{public string VillageSettlementId="village",VillageName="Village";public double QueuedTimeHours=90;}
 readonly List<ActiveVillageRelief> _activeReliefs=new();readonly List<PendingVillageRelief> _pendingReliefs=new();readonly Dictionary<string,string> _adoptionOrigins=new();double _lastAdoptionTimeHours=-1000000;
 static HashSet<string> _activeReliefPartyIds=new();static GreyWardenVillageAdoptionBehavior? _instance;
 static MobileParty? Party;static Settlement? Village;int Completed;bool LastShouldAdopt;
 static MobileParty? FindPoliceParty(string id)=>Party;static Settlement? FindSettlement(string id)=>Village;
 void FinishRelief(ActiveVillageRelief r,MobileParty? p,bool shouldAdopt){Completed++;LastShouldAdopt=shouldAdopt;_activeReliefs.Remove(r);}
 REAL_METHODS
 static int Checks;static void Check(bool b,string message){if(!b)throw new Exception(message);Checks++;}
 static GreyWardenVillageAdoptionBehavior Setup(float distance,bool inside){
  CampaignTime.Now.ToHours=100;GwpWardenDuties.Ready=true;Village=new Settlement();Party=new MobileParty{GetPosition2D=new Vec{X=distance},CurrentSettlement=inside?Village:null};
  var b=new GreyWardenVillageAdoptionBehavior();_instance=b;b._activeReliefs.Add(new());b.RebuildActiveReliefPartyIndex();return b;
 }
 static GreyWardenVillageAdoptionBehavior Reload(GreyWardenVillageAdoptionBehavior b){var store=new Store{IsSaving=true};b.SyncData(store);store.IsSaving=false;var copy=new GreyWardenVillageAdoptionBehavior();_instance=copy;copy.SyncData(store);copy.OnGameLoaded(new CampaignGameStarter());return copy;}
 static void Main(){
  var b=Setup(4,false);b.UpdateActiveReliefs();Check(!b._activeReliefs[0].ReliefStarted,"outside radius does not start work");
  Check(TryGetReliefVisit(Party!,out var target,out var score)&&target==Village&&score==8,"target is reconstructed from saved assignment even without hourly intent");
  b=Setup(2.9f,false);b.UpdateActiveReliefs();Check(b._activeReliefs[0].ReliefStarted&&b._activeReliefs[0].RemainingHours==72,"existing arrival radius retained with full72 hours");
  Check(TryGetReliefVisit(Party!,out target,out score)&&target==Village&&score==10,"at gate uses explicit target village for hold");
  CampaignTime.Now.ToHours=110;b.UpdateActiveReliefs();Check(b._activeReliefs[0].RemainingHours==62,"actual stay accumulates elapsed time");
  b=Reload(b);Check(b._activeReliefs[0].RemainingHours==62&&b._activeReliefs[0].LastWorkHours==110&&_activeReliefPartyIds.Contains("joel"),"same-build save/load preserves progress and occupation");
  GwpWardenDuties.Ready=false;CampaignTime.Now.ToHours=111;b.UpdateActiveReliefs();
  CampaignTime.Now.ToHours=211;b.UpdateActiveReliefs();Check(b._activeReliefs[0].RemainingHours==62&&b.Completed==0,"duel or player commitment suspends relief clock");
  b=Reload(b);Check(b._activeReliefs[0].LastWorkHours<0&&b._activeReliefs[0].RemainingHours==62,"paused relief roundtrip preserves suspended clock");
  GwpWardenDuties.Ready=true;Party!.GetPosition2D.X=50;CampaignTime.Now.ToHours=212;b.UpdateActiveReliefs();Check(b.Completed==0&&b._activeReliefs[0].RemainingHours==62,"return journey does not count as work");
  Check(TryGetReliefVisit(Party!,out target,out score)&&target==Village&&score==8,"return travel has real village target");
  Party.GetPosition2D.X=0;CampaignTime.Now.ToHours=213;b.UpdateActiveReliefs();Check(b._activeReliefs[0].RemainingHours==62,"return begins a new continuous work interval");
  CampaignTime.Now.ToHours=274;b.UpdateActiveReliefs();Check(b.Completed==0&&b._activeReliefs[0].RemainingHours==1,"only actual remaining work counts");
  CampaignTime.Now.ToHours=275;b.UpdateActiveReliefs();Check(b.Completed==1&&b.LastShouldAdopt&&!_activeReliefPartyIds.Contains("joel"),"full72 hours completes and releases exactly once");
  b.UpdateActiveReliefs();Check(b.Completed==1,"repeat hourly update cannot pay again");
  b=Setup(0,true);b.UpdateActiveReliefs();Party!.MapEvent=new();CampaignTime.Now.ToHours=150;b.UpdateActiveReliefs();Check(b.Completed==0&&b._activeReliefs[0].RemainingHours==72,"battle suspends timer");
  Party.MapEvent=null;Party.CurrentSettlement=null;Party.GetPosition2D.X=50;CampaignTime.Now.ToHours=250;b.UpdateActiveReliefs();Check(b.Completed==0,"long absence never completes adoption remotely");
  Party.LeaderHero!.IsPrisoner=true;b.UpdateActiveReliefs();Check(b.Completed==1&&!b.LastShouldAdopt,"captured warden aborts without adoption");
  b=Setup(0,true);b._activeReliefs.Clear();b._pendingReliefs.Add(new());b=Reload(b);Check(b._pendingReliefs.Count==1&&b._activeReliefs.Count==0,"load keeps pending task instead of normalization deletion");
  Console.WriteLine("VILLAGE_RELIEF_ASSERTIONS="+Checks+"; production update, visit and save/load; movement/completion effects controlled");
 }
}
}
'''


class VillageReliefCharacterization(unittest.TestCase):
    def test_stay_pause_return_and_save_load(self):
        source=(MODULE/'GreyWardenVillageAdoptionBehavior.cs').read_text(encoding='utf-8-sig')
        desire=(MODULE/'GreyWardenPartyDesireBehavior.cs').read_text(encoding='utf-8-sig')
        tuning=(MODULE/'GwpTuning.cs').read_text(encoding='utf-8-sig')
        code=HARNESS.replace('REAL_METHODS','\n'.join(member(source.replace('public override void SyncData','internal void SyncData'),n).replace('public override void SyncData','public void SyncData') for n in ['UpdateActiveReliefs','HasArrivedAtVillage','ShouldAbortReliefForOperationalReason','RebuildActiveReliefPartyIndex','TryGetReliefVisit','SyncData','MinCount','OnGameLoaded']))
        code=code.replace('CONSTANTS','\n'.join(re.findall(r'private const [^;]+;',source)))
        code=code.replace('STAY',re.search(r'VillageReliefStayHours\s*=\s*([^;]+)',tuning)[1]).replace('RADIUS',re.search(r'VillageReliefArrivalDistance\s*=\s*([^;]+)',tuning)[1])
        major=subprocess.check_output(['dotnet','--version'],text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-relief-characterization-') as temp:
            p=Path(temp)
            (p/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>',encoding='utf-8')
            (p/'Program.cs').write_text(code,encoding='utf-8')
            r=subprocess.run(['dotnet','run','--project',temp,'-c','Release','-v','quiet'],capture_output=True,text=True)
            self.assertEqual(0,r.returncode,r.stdout+r.stderr)
            print(next(line for line in r.stdout.splitlines() if line.startswith('VILLAGE_RELIEF_ASSERTIONS=')))


if __name__ == '__main__':
    unittest.main()
