"""Run the installed encounter gate with the production interceptor command.

Campaign objects, proximity and engine movement are controlled boundaries. The
entire encounter decision body is freshly decompiled; no game is launched.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

from test_battle_peace import extract

ROOT = Path(__file__).resolve().parents[2]
GAME_BIN = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client')

HARNESS = r'''
using System;
namespace Audit {
enum AiBehavior {None,EngageParty,GoToPoint,AssaultSettlement}
enum BattleSideEnum {Attacker,Defender}
interface IInteractablePoint {bool CanPartyInteract(MobileParty p,float dt);void OnPartyInteraction(MobileParty p);}
class PartyBase:IInteractablePoint {
 public static PartyBase MainParty=new();public bool IsSettlement,IsMobile=true;public object MapFaction=new();
 public MapEvent? MapEvent;public bool InReach=true;public int Interactions;
 public bool CanPartyInteract(MobileParty p,float dt)=>InReach;
 public void OnPartyInteraction(MobileParty p){Interactions++;}
}
class MapEvent {public PartyBase Attacker=new(),Defender=new();public PartyBase GetLeaderParty(BattleSideEnum s)=>s==BattleSideEnum.Attacker?Attacker:Defender;}
class Settlement {}
class PartyAi {
 public IInteractablePoint? AiBehaviorInteractable;public bool Locked;public float Attack,Avoid,Hours;
 public void SetDoNotMakeNewDecisions(bool b){Locked=b;}
 public void SetInitiative(float attack,float avoid,float hours){Attack=attack;Avoid=avoid;Hours=hours;}
}
class MobileParty {
 public enum NavigationType {Default}
 public bool IsActive=true,IsGarrison;public MobileParty? AttachedTo,ShortTermTargetParty;
 public object? MapEventSide;public MapEvent? MapEvent;public Settlement? CurrentSettlement,BesiegedSettlement;
 public AiBehavior ShortTermBehavior;public PartyBase Party=new();public PartyAi Ai=new();
 public object MapFaction=>Party.MapFaction;
 public bool IsCurrentlyEngagingParty=>ShortTermBehavior==AiBehavior.EngageParty;
 public bool IsCurrentlyEngagingSettlement=>ShortTermBehavior==AiBehavior.AssaultSettlement;
 public Settlement? ShortTermTargetSettlement;
 public void SetMoveEngageParty(MobileParty target,NavigationType n){
  ShortTermBehavior=AiBehavior.EngageParty;ShortTermTargetParty=target;Ai.AiBehaviorInteractable=target.Party;
 }
}
class PlayerEncounter {public static MobileParty? EncounteredMobileParty;public static PlayerEncounter? Current;public BattleSideEnum PlayerSide;}
static class NativeEncounter {
 ENCOUNTER_METHOD
}
static class ProductionCommand {
 private static void ResolveNavigation(MobileParty p,MobileParty t,out MobileParty.NavigationType n,out bool from){n=MobileParty.NavigationType.Default;from=false;}
 DIRECT_ATTACK_METHOD
 public static void Run(MobileParty p,MobileParty t)=>StartDirectAttack(p,t);
}
static class Replay {
 static int Checks;static void Check(bool b,string label){if(!b)throw new Exception(label);Checks++;}
 static void Main(){
  var patrol=new MobileParty();var target=new MobileParty{CurrentSettlement=new()};var town=target.CurrentSettlement;
  ProductionCommand.Run(patrol,target);
  Check(patrol.Ai.Locked&&patrol.Ai.Attack==1&&patrol.Ai.Avoid==0,"production interceptor direct attack lock");
  Check(patrol.ShortTermTargetParty==target,"direct command targets offender");
  Check(target.CurrentSettlement==town,"direct command does not expel resident target");
  NativeEncounter.HandleEncounterForMobileParty(patrol,1);
  Check(target.Party.Interactions==0&&target.CurrentSettlement==town,"native contact rejects ordinary town resident even with engage order and reach");
  target.CurrentSettlement=null;NativeEncounter.HandleEncounterForMobileParty(patrol,1);
  Check(target.Party.Interactions==1,"contact works after offender leaves settlement");
  target.Party.InReach=false;NativeEncounter.HandleEncounterForMobileParty(patrol,1);
  Check(target.Party.Interactions==1,"engage order does not bypass actual contact distance");
  target.Party.InReach=true;target.CurrentSettlement=town;target.MapEvent=new();
  target.MapEvent.Attacker.MapFaction=patrol.MapFaction;NativeEncounter.HandleEncounterForMobileParty(patrol,1);
  Check(target.Party.Interactions==2,"settlement exception requires existing battle with same-faction side");
  target.MapEvent=null;patrol.CurrentSettlement=new();target.CurrentSettlement=null;
  NativeEncounter.HandleEncounterForMobileParty(patrol,1);
  Check(target.Party.Interactions==2,"non-garrison actor itself in settlement cannot initiate contact");
  Console.WriteLine("NATIVE_INTERCEPTOR_CONTACT_ASSERTIONS="+Checks+"; PASS; production command and installed encounter gate, engine boundaries controlled");
 }
}
}
'''


class NativeInterceptorContactTests(unittest.TestCase):
    def test_direct_attack_cannot_pull_resident_out_of_settlement(self):
        native = subprocess.check_output(['ilspycmd', '-r', str(GAME_BIN), '-t',
            'TaleWorlds.CampaignSystem.EncounterManager',
            str(GAME_BIN / 'TaleWorlds.CampaignSystem.dll')], text=True, encoding='utf-8')
        production = (ROOT / 'GreyWardenPolicePurity/GreyWardenPartyDesireBehavior.cs').read_text(encoding='utf-8-sig')
        code = HARNESS.replace('ENCOUNTER_METHOD', extract(native, 'HandleEncounterForMobileParty'))
        code = code.replace('DIRECT_ATTACK_METHOD', extract(production, 'StartDirectAttack'))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-native-contact-') as directory:
            path = Path(directory)
            (path / 'Replay.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', directory, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(line for line in result.stdout.splitlines() if line.startswith('NATIVE_INTERCEPTOR_CONTACT_ASSERTIONS=')))


if __name__ == '__main__':
    unittest.main()
