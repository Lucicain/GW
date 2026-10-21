"""Run production preparation code; native world, menus and battle execution are controlled boundaries."""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'


class PlayerInterventionTests(unittest.TestCase):
    def test_preparation_cancel_commit_and_native_boundary(self):
        sources = [MODULE / 'GwpPlayerIntervention.cs']
        keys = sorted(set(re.findall(r'GwpTextCatalog\.(\w+)', ''.join(p.read_text(encoding='utf-8-sig') for p in sources))))
        stubs = r'''
using System;using System.Collections.Generic;using System.Linq;using System.Reflection;
using TaleWorlds.Core;using TaleWorlds.CampaignSystem;using TaleWorlds.CampaignSystem.Party;using TaleWorlds.CampaignSystem.MapEvents;using TaleWorlds.CampaignSystem.GameMenus;using TaleWorlds.CampaignSystem.Encounters;
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class|AttributeTargets.Method)]public class HarmonyPatch:Attribute{}public static class AccessTools{public static MethodInfo Method(Type t,string n)=>t.GetMethod(n,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!;}}
namespace TaleWorlds.Localization{public class TextObject{}}
namespace TaleWorlds.Core{
 public enum BattleSideEnum{None,Attacker,Defender}public static class MBTextManager{public static void SetTextVariable(string k,string value){}}
 public class Game{public static Game Current=new();public States GameStateManager=new();}public class States{public object ActiveState=new TaleWorlds.CampaignSystem.GameState.MapState();public T? LastOrDefault<T>() where T:class=>ActiveState as T;}
}
namespace TaleWorlds.CampaignSystem.GameState{public class MapState{public Handler Handler=new();}public class Handler{public int Moves;public void StartCameraAnimation(Pos p,float d){Moves++;}}}
namespace TaleWorlds.CampaignSystem{
 public struct Pos{public float X;public Pos(float x){X=x;}public Pos ToVec2()=>this;public float Distance(Pos p)=>Math.Abs(X-p.X);}
 public class Hero{public static Hero MainHero=new();public bool IsWounded;public MobileParty? PartyBelongedTo;}
 public class PartyBase{public string Name="party";public Hero? LeaderHero;public int NumberOfHealthyMembers=10;}
 public class Conversation{public bool IsConversationInProgress;}
 public class Campaign{public static Campaign Current=new();public MenuContext? CurrentMenuContext;public Conversation ConversationManager=new();public Manager MapEventManager=new();public Dictionary<Type,object> Behaviors=new();public T GetCampaignBehavior<T>()=>(T)Behaviors[typeof(T)];}
 public class CampaignGameStarter{public void AddGameMenu(string id,string text,Action<MenuCallbackArgs> init){GameMenu.Inits[id]=init;}public void AddGameMenuOption(string menu,string id,string text,Func<MenuCallbackArgs,bool> condition,Action<MenuCallbackArgs> execute,bool isLeave=false){GameMenu.Conditions[id]=condition;GameMenu.Commands[id]=execute;}}
 public class CharacterObject{public bool IsHero,Grey=true;}public class Troop{public int Number=3;public CharacterObject Character=new();}public class Roster{public List<Troop> Troops=new(){new()};public IEnumerable<Troop> GetTroopRoster()=>Troops;}
}
namespace TaleWorlds.CampaignSystem.Party{public class Army{public MobileParty? LeaderParty;}public class MobileParty{public static MobileParty MainParty=new();public bool IsActive=true,IsVisible=true;public MapEvent? MapEvent;public TaleWorlds.CampaignSystem.Settlements.Settlement? CurrentSettlement;public PartyBase Party=new();public Army? Army;public Roster MemberRoster=new();public Pos Position;public Pos GetPosition2D=>Position;public void SetMoveModeHold(){}}}
namespace TaleWorlds.CampaignSystem.Settlements{public class Settlement{public static Settlement? CurrentSettlement;public bool IsVillage=true;public string Name="village";public Pos Position;}}
namespace TaleWorlds.CampaignSystem.MapEvents{
 public class Side{public int TroopCount=10;public PartyBase LeaderParty=new();}
 public class MapEvent{public bool IsFinalized,HasWinner,IsVisible=true,Eligible=true,Raid;public Pos Position;public Side AttackerSide=new(),DefenderSide=new();public TaleWorlds.CampaignSystem.Settlements.Settlement? MapEventSettlement;public Side GetMapEventSide(BattleSideEnum side)=>side==BattleSideEnum.Attacker?AttackerSide:DefenderSide;public PartyBase GetLeaderParty(BattleSideEnum side)=>GetMapEventSide(side).LeaderParty;}
 public class Manager{public List<MapEvent> MapEvents=new();}
}
namespace TaleWorlds.CampaignSystem.GameMenus{
 public class GameMenuOption{public enum LeaveType{Submenu,Leave,DefendAction,OrderTroopsToAttack,Mission}}
 public class MenuCallbackArgs{public bool IsEnabled=true;public object? Tooltip;public GameMenuOption.LeaveType optionLeaveType;}
 public class MenuContext{public GameMenu GameMenu=new();}
 public class GameMenu{public string StringId="join_encounter";public static Dictionary<string,Action<MenuCallbackArgs>> Inits=new(),Commands=new();public static Dictionary<string,Func<MenuCallbackArgs,bool>> Conditions=new();public static void SwitchToMenu(string id){Campaign.Current.CurrentMenuContext=new(){GameMenu=new(){StringId=id}};}}
}
namespace TaleWorlds.CampaignSystem.Encounters{public class PlayerEncounter{public static PlayerEncounter? Current=new();public static MapEvent? EncounteredBattle;public static int Finishes;public static void Finish(){Finishes++;if(MobileParty.MainParty.MapEvent!=null)MobileParty.MainParty.MapEvent.IsFinalized=true;Current=null;}}}
namespace TaleWorlds.CampaignSystem.CampaignBehaviors{
 public class EncounterGameMenuBehavior{
  public bool NativeEnabled=true,AllowJoin;public int AttackJoins,DefendJoins;public BattleSideEnum JoinedSide;
  private bool Condition(MenuCallbackArgs args,BattleSideEnum side){args.IsEnabled&=NativeEnabled;bool visible=AllowJoin;GreyWardenPolicePurity.GwpPlayerIntervention.ExtendChoice(args,side,ref visible);return visible;}
  private bool game_menu_join_encounter_help_attackers_on_condition(MenuCallbackArgs args)=>Condition(args,BattleSideEnum.Attacker);
  private bool game_menu_join_encounter_help_defenders_on_condition(MenuCallbackArgs args)=>Condition(args,BattleSideEnum.Defender);
  private void game_menu_join_encounter_help_attackers_on_consequence(MenuCallbackArgs args){if(!GreyWardenPolicePurity.GwpPlayerIntervention.Select(BattleSideEnum.Attacker))throw new Exception("commit recursively opened preparation");AttackJoins++;Join(BattleSideEnum.Attacker);}
  private void game_menu_join_encounter_help_defenders_on_consequence(MenuCallbackArgs args){if(!GreyWardenPolicePurity.GwpPlayerIntervention.Select(BattleSideEnum.Defender))throw new Exception("commit recursively opened preparation");DefendJoins++;Join(BattleSideEnum.Defender);}
  private void Join(BattleSideEnum side){JoinedSide=side;var old=PlayerEncounter.EncounteredBattle!;if(old.Raid){old.IsFinalized=true;MobileParty.MainParty.MapEvent=new();}else MobileParty.MainParty.MapEvent=old;GameMenu.SwitchToMenu("encounter");}
 }
}
namespace Helpers{public static class MobilePartyHelper{public static bool Morale=true;public static bool CanPartyAttackWithCurrentMorale(MobileParty p)=>Morale;}public static class MenuHelper{public static int Attacks,Orders;public static void EncounterAttackConsequence(MenuCallbackArgs a){if(MobileParty.MainParty.MapEvent==null)throw new Exception("attack before native join");Attacks++;}public static void EncounterOrderAttackConsequence(MenuCallbackArgs a){if(MobileParty.MainParty.MapEvent==null)throw new Exception("simulation before native join");Orders++;}}}
namespace GreyWardenPolicePurity{
 static class GwpTextCatalog{KEYS}static class GwpText{public static string Get(string k,params object[] values)=>k;public static TaleWorlds.Localization.TextObject Create(string k)=>new();}
 class PlayerBehaviorMonitor{public int Prepared;public static bool TryBuildForceJoinTexts(MapEvent b,out string a,out string d){a=d="";return b.Eligible;}public void PrepareIntervention(MapEvent b,BattleSideEnum s){Prepared++;}}

}
'''.replace('KEYS', ''.join(f'public const string {key}="{key}";' for key in keys))
        program = r'''
using System;using System.Linq;using GreyWardenPolicePurity;using TaleWorlds.Core;using TaleWorlds.CampaignSystem;using TaleWorlds.CampaignSystem.GameMenus;using TaleWorlds.CampaignSystem.MapEvents;using TaleWorlds.CampaignSystem.Party;using TaleWorlds.CampaignSystem.Encounters;using TaleWorlds.CampaignSystem.CampaignBehaviors;using TaleWorlds.CampaignSystem.GameState;using Helpers;
class Program{
 static int checks;static void Check(bool x,string message){checks++;if(!x)throw new Exception(message);}
 static EncounterGameMenuBehavior Native=>Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();
 static MapEvent Reset(){Campaign.Current=new();GameMenu.SwitchToMenu("join_encounter");Campaign.Current.Behaviors[typeof(EncounterGameMenuBehavior)]=new EncounterGameMenuBehavior();Campaign.Current.Behaviors[typeof(PlayerBehaviorMonitor)]=new PlayerBehaviorMonitor();MobileParty.MainParty=new();PlayerEncounter.Current=new();PlayerEncounter.EncounteredBattle=new();Game.Current=new();MenuHelper.Attacks=MenuHelper.Orders=0;GwpPlayerIntervention.Register(new());return PlayerEncounter.EncounteredBattle;}
 static void Click(string action)=>GameMenu.Commands[action](new());
 static void Main(){
 foreach(bool raid in new[]{false,true})foreach(var side in new[]{BattleSideEnum.Attacker,BattleSideEnum.Defender}){
  var b=Reset();b.Raid=raid;Check(!GwpPlayerIntervention.Select(side),"eligible choice deferred");Check(MobileParty.MainParty.MapEvent==null&&!b.IsFinalized,"selecting side does not join or convert raid");
  Click("back");Check(MobileParty.MainParty.MapEvent==null&&!b.IsFinalized&&Campaign.Current.CurrentMenuContext!.GameMenu.StringId=="join_encounter","back preserves ongoing NPC battle");
  GwpPlayerIntervention.Select(side);Click("leave");Check(PlayerEncounter.Current==null&&!b.IsFinalized&&MobileParty.MainParty.MapEvent==null,"not getting involved finishes only the uncommitted encounter");
  b=Reset();b.Raid=raid;GwpPlayerIntervention.Select(side);Click("attack");Check((side==BattleSideEnum.Attacker?Native.AttackJoins:Native.DefendJoins)==1&&MenuHelper.Attacks==1,"attack invokes correct native join exactly once then native attack");Check(Campaign.Current.GetCampaignBehavior<PlayerBehaviorMonitor>().Prepared==1,"crime participation captured only on commit");Check(b.IsFinalized==raid,"only native raid adapter owns conversion");Click("attack");Check(MenuHelper.Attacks==1&&Campaign.Current.CurrentMenuContext!.GameMenu.StringId=="encounter","duplicate callback cannot commit or change active menu");
  b=Reset();b.Raid=raid;GwpPlayerIntervention.Select(side);Click("send_troops");Check(MenuHelper.Orders==1&&MenuHelper.Attacks==0,"simulation uses native send-troops path");
 }
 var e=Reset();GwpPlayerIntervention.Select(BattleSideEnum.Defender);e.IsFinalized=true;Click("attack");Check(Native.DefendJoins==0&&MenuHelper.Attacks==0,"expired event is not committed");
 e=Reset();GwpPlayerIntervention.Select(BattleSideEnum.Defender);PlayerEncounter.EncounteredBattle=new();Click("attack");Check(Native.DefendJoins==0,"changed encounter identity rejected");
 e=Reset();GwpPlayerIntervention.Select(BattleSideEnum.Defender);Native.NativeEnabled=false;Click("attack");Check(Native.DefendJoins==0&&MobileParty.MainParty.MapEvent==null,"native honour/naval restriction preserved before join");
 e=Reset();GwpPlayerIntervention.Select(BattleSideEnum.Defender);Hero.MainHero.IsWounded=true;Click("attack");Check(MenuHelper.Attacks==0&&MobileParty.MainParty.MapEvent==null,"wounded player does not commit manual battle");Hero.MainHero.IsWounded=false;
 e=Reset();e.Eligible=false;Check(GwpPlayerIntervention.Select(BattleSideEnum.Defender),"unrelated native encounters untouched");bool visible=false;GwpPlayerIntervention.ExtendChoice(new(),BattleSideEnum.Defender,ref visible);Check(!visible,"unrelated join visibility untouched");e.Eligible=true;var disabled=new MenuCallbackArgs{IsEnabled=false};GwpPlayerIntervention.ExtendChoice(disabled,BattleSideEnum.Defender,ref visible);Check(visible&&!disabled.IsEnabled,"extension preserves native disabled reason");
 e=Reset();MobileParty.MainParty.MapEvent=e;Check(GwpPlayerIntervention.Select(BattleSideEnum.Defender),"already joined battles stay native");bool joinedVisible=false;GwpPlayerIntervention.ExtendChoice(new(),BattleSideEnum.Defender,ref joinedVisible);Check(!joinedVisible,"already joined visibility stays native");
 e=Reset();GameMenu.SwitchToMenu("encounter_interrupted");Check(GwpPlayerIntervention.Select(BattleSideEnum.Defender),"interrupted settlement wait stays native");bool interruptedVisible=false;GwpPlayerIntervention.ExtendChoice(new(),BattleSideEnum.Defender,ref interruptedVisible);Check(!interruptedVisible,"interrupted wait visibility stays native");
 Console.WriteLine("PLAYER_INTERVENTION_ASSERTIONS="+checks);
 }
}
'''
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-intervention-') as tmp:
            p = Path(tmp)
            (p / 'Tests.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>')
            for source in sources:
                (p / source.name).write_text(source.read_text(encoding='utf-8-sig'), encoding='utf-8')
            (p / 'Stubs.cs').write_text(stubs, encoding='utf-8')
            (p / 'Program.cs').write_text(program, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', tmp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(s for s in result.stdout.splitlines() if s.startswith('PLAYER_INTERVENTION_ASSERTIONS=')))


if __name__ == '__main__':
    unittest.main()
