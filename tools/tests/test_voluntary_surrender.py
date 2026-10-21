"""Production surrender/return with installed capture, army teardown and garrison merge.

Entities, navigation, event fan-out, ransom and ship ownership are controlled
boundaries. This is managed integration, not a full campaign or original-save replay.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

from test_army_influence_fault import HARNESS, GAME_DLL, ROOT
from test_battle_peace import extract


EXTRA = r'''
abstract class CampaignBehaviorBase{public abstract void RegisterEvents();public abstract void SyncData(IDataStore s);}
interface IDataStore{void SyncData<T>(string key,ref T value);}
class Store:IDataStore{public bool Saving;public Dictionary<string,object> Data=new();public void SyncData<T>(string key,ref T value){if(Saving)Data[key]=value!;else if(Data.TryGetValue(key,out var v))value=(T)v;else value=default!;}}
class Evt{public void AddNonSerializedListener(object owner,Action a){}public void AddNonSerializedListener(object owner,Action<MobileParty,PartyBase> a){}}
static class CampaignEvents{public static Evt HourlyTickEvent=new(),MobilePartyDestroyed=new();}
class TextObject{public TextObject(string s){}public void SetTextVariable(string key,object value){}}
static class CampaignData{public static string NeutralFactionName="neutral";}
interface IDisbandPartyCampaignBehavior{bool IsPartyWaitingForDisband(MobileParty p);}
class NativeRetirement:IDisbandPartyCampaignBehavior{
 public Dictionary<MobileParty,CampaignTime> _partiesThatWaitingToDisband=new();
 public bool IsPartyWaitingForDisband(MobileParty p)=>_partiesThatWaitingToDisband.ContainsKey(p);
 NATIVE_RETIREMENT
 public void Started(MobileParty p)=>OnPartyDisbandStarted(p);
 public void AdvanceWaiting(){foreach(var t in _partiesThatWaitingToDisband.Values)t.IsPast=true;HourlyTick();}
 public void Merge(MobileParty p,Settlement s)=>MergeDisbandPartyToFortification(p,s);
}
static class DisbandPartyAction{NATIVE_DISBAND}
static class TeleportHeroAction{public static Hero? Chosen;public static void ApplyDelayedTeleportToPartyAsPartyLeader(Hero h,MobileParty p){Chosen=h;h.Traveling=true;}}
static class MobilePartyHelper{public static Settlement? GetCurrentSettlementOfMobilePartyForAICalculation(MobileParty p)=>p.CurrentSettlement;}
static class MakeHeroFugitiveAction{public static void Apply(Hero h){}}
class Clock{public float ElapsedDaysUntilNow=1;}
class Item{public string EquipmentElement="sword:lordly";public int Amount=2;}
class Items:List<Item>{public void AddToCounts(string e,int amount){var r=this.FirstOrDefault(x=>x.EquipmentElement==e);if(r==null)Add(new(){EquipmentElement=e,Amount=amount});else r.Amount+=amount;}}
class Ship{public PartyBase Owner=null!;public int Hull=87;}
static class ChangeShipOwnerAction{public static void ApplyByTransferring(PartyBase dest,Ship ship){ship.Owner.MobileParty!.Ships.Remove(ship);ship.Owner=dest;dest.Ships.Add(ship);}}
class Town{public MobileParty? GarrisonParty;public int Gold;public void ChangeGold(int amount){Gold+=amount;}}
static class AiHelper{public static void GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(MobileParty p,Settlement s,bool port,out MobileParty.NavigationType nav,out float distance,out bool fromPort){nav=s.Reachable? (port?MobileParty.NavigationType.Naval:MobileParty.NavigationType.Default):MobileParty.NavigationType.None;distance=s.Distance+(port?-1:0);fromPort=p.IsCurrentlyAtSea;}}
class NativeAi{public void SetInitiative(float attack,float avoid,float hours){}public void SetDoNotMakeNewDecisions(bool doNotMakeNewDecisions){}}
class Timer{public bool Deleted;public void DeletePeriodicEvent(){Deleted=true;}}
static class NavigationHelper{public static int FindReachablePointAroundPosition(int position,MobileParty.NavigationType nav,float radius)=>position;}
class GameStateManager{public object? ActiveState;}
class Game{public static Game Current=new();public GameStateManager GameStateManager=new();}
class MapState{public void OnLeaveArmy(){}public void OnDispersePlayerLeadedArmy(){}}
class PrisonerDonationModel{public float CalculateInfluenceGainAfterTroopDonation(PartyBase p,CharacterObject c,Settlement s)=>0;}
static class TransferPrisonerAction{public static void Apply(CharacterObject c,PartyBase from,PartyBase dest){from.MobileParty!.PrisonRoster.Rows.RemoveAll(x=>x.Character==c);dest.PrisonRoster.Rows.Add(new(){Character=c,Number=1});c.HeroObject.PrisonerParty=dest;}}
static class EndCaptivityAction{public static void ApplyByEscape(Hero h){h.State=Hero.CharacterStates.Active;}}
static class SellPrisonersAction{public static int Sold;public static void ApplyForAllPrisoners(PartyBase from,PartyBase dest){Sold+=from.PrisonRoster.TotalManCount;from.PrisonRoster.Rows.Clear();}}
static class GainKingdomInfluenceAction{public static void ApplyForDonatePrisoners(MobileParty p,float amount){}}
static class DestroyPartyAction{
 public static void Apply(PartyBase? by,MobileParty p){p.IsActive=false;p.ItemRoster.Clear();p.MemberRoster.Rows.Clear();p.PrisonRoster.Rows.Clear();p.Ships.Clear();}
 public static void ApplyForDisbanding(MobileParty p,Settlement s){Campaign.Current.Retirement.Merge(p,s);Apply(null,p);}
}
VOLUNTARY_SOURCE
class Program{
 static int checks;static void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}
 static MobileParty Party(string id,Clan clan){var p=new MobileParty{StringId=id,ActualClan=clan,MapFaction=clan.Kingdom!};var hero=new Hero{StringId=id+"_hero",Clan=clan};hero.PartyBelongedTo=p;p.PartyComponent=new LordPartyComponent{MobileParty=p,_leader=hero};return p;}
 static Army Formation(MobileParty chief,MobileParty member){var a=new Army{LeaderParty=chief,Kingdom=chief.MapFaction,Cohesion=49};chief.Army=a;member.Army=a;return a;}
 static void Main(){
  Settlement.All.Clear();var faction=new Kingdom();var clan=new Clan{Kingdom=faction};var chief=Party("chief",clan);clan.Leader=chief.LeaderHero;var target=Party("offender",clan);var offender=target.LeaderHero!;var army=Formation(chief,target);
  var envoy=Party("envoy",clan);var warden=Party("warden",clan);var enforcement=new PoliceEnforcementBehavior();
  target.ItemRoster.Add(new());var originalShip=new Ship{Owner=target.Party};target.Ships.Add(originalShip);
  enforcement.Surrender(warden,envoy,target,false);
  Check(offender.IsPrisoner&&offender.PartyBelongedToAsPrisoner==envoy.Party,"actual production negotiation reaches native capture");
  Check(target.Army==null&&!army.Parties.Contains(target)&&chief.Army==army,"native setter removes ordinary member, chief army preserved");
  Check(target.MemberRoster.TotalManCount==76&&target.ItemRoster.Single().Amount==2&&target.Ships.Single()==originalShip,"handoff itself does not seize or discard source troops cargo or ship");
  Check(Campaign.Current.Retirement.IsPartyWaitingForDisband(target),"complete native StartDisband registers original party for vanilla retirement");
  for(int h=0;h<1000;h++)army.RunHour();Check(chief.LeaderHero!=null,"native hourly chain no longer sees surrendered member over1000 controlled hours");
  Campaign.Current.Retirement.AdvanceWaiting();Check(target.IsDisbanding,"installed native waiting timer activates retirement");
  var home=new Settlement{MapFaction=faction};target.CurrentSettlement=home;target.MemberRoster.Rows[0].WoundedNumber=7;target.MemberRoster.Rows[0].Xp=123;target.PrisonRoster.Rows.Add(new(){Number=4});int before=SellPrisonersAction.Sold;
  DestroyPartyAction.ApplyForDisbanding(target,home);
  var row=home.Town.GarrisonParty!.MemberRoster.Rows.Single();Check(row.Number==76&&row.WoundedNumber==7&&row.Xp==123,"installed native friendly-garrison merge preserves troops wounded and xp");
  Check(!target.IsActive&&SellPrisonersAction.Sold-before==4,"native retirement destroys old party after garrison merge and hands prisoners to original ransom boundary");
  var c2=Party("army_chief",clan);var helper=Party("other_lord",clan);var a2=Formation(c2,helper);helper.AttachedTo=c2;var h2=helper.LeaderHero!;var c2hero=c2.LeaderHero!;
  enforcement=new();enforcement.Surrender(warden,envoy,c2,false);
  Check(c2hero.IsPrisoner&&c2.Army==null&&helper.Army==null&&helper.AttachedTo==null,"commander surrender runs installed army disband and detaches all parties before capture");
  Check(a2.HourlyDeleted&&a2.TickDeleted&&a2.Parties.Count==0,"commander army removes both periodic callbacks and member list");
  Check(helper.LeaderHero==h2&&helper.MemberRoster.TotalManCount==76,"other lord and original troops remain independent");
  Check(Campaign.Current.Retirement.IsPartyWaitingForDisband(c2),"only commander's old party enters native retirement");
  var c3=Party("busy_chief",clan);var helper3=Party("busy_helper",clan);var a3=Formation(c3,helper3);helper3.MapEvent=new();
  Check(!GwpVoluntarySurrender.TryCapture(envoy.Party,c3.LeaderHero!)&&c3.Army==a3&&c3.LeaderHero!=null,"other member battle defers commander surrender without partial dismantling");helper3.MapEvent=null;
  Check(GwpVoluntarySurrender.TryCapture(envoy.Party,c3.LeaderHero!),"commander can surrender once related battles finish");
  var c4=Party("later",clan);var target4=Party("after_battle",clan);var off4=target4.LeaderHero!;var a4=Formation(c4,target4);target4.MapEvent=new();enforcement=new();enforcement.Surrender(warden,envoy,target4,true);
  Check(enforcement.CapturesBeforeBattleEnd==0&&off4.IsPrisoner&&target4.Army==null,"production aftermath waits for battle then uses repaired shared capture");a4.RunHour();
  var remote=new Hero{Clan=clan,CurrentSettlement=home};clan.Heroes.Add(remote);var t6=Party("remote_successor",clan);for(int i=0;i<9;i++)t6.MemberRoster.Rows.Add(new(){Number=1});
  Check(GwpVoluntarySurrender.TryCapture(envoy.Party,t6.LeaderHero!)&&TeleportHeroAction.Chosen==remote,"installed native retirement selects eligible replacement under its own conditions");
  Check(t6.IsActive&&!t6.IsDisbanding&&t6.MemberRoster.TotalManCount==85,"vanilla replacement path preserves original force while replacement travels");clan.Heroes.Clear();
  var direct=Party("player_surrender",clan);var directHero=direct.LeaderHero!;var directArmy=Formation(Party("player_target_chief",clan),direct);
  Check(GwpVoluntarySurrender.TryCapture(MobileParty.MainParty.Party,directHero)&&directHero.PartyBelongedToAsPrisoner==MobileParty.MainParty.Party&&direct.Army==null,"same shared capture supports player receiver without requiring a custom campaign behavior");directArmy.RunHour();
  var atSea=Party("sea_party",clan);atSea.IsCurrentlyAtSea=true;atSea.Ships.Add(new(){Owner=atSea.Party});var seaArmy=Formation(Party("sea_chief",clan),atSea);
  Check(GwpVoluntarySurrender.TryCapture(envoy.Party,atSea.LeaderHero!)&&atSea.Army==null&&atSea.Ships.Count==1,"sea surrender leaves original ships with original party and retires via native flow");
  var zero=Party("zero_regulars",clan);zero.MemberRoster.Rows.Clear();Check(GwpVoluntarySurrender.TryCapture(envoy.Party,zero.LeaderHero!)&&!zero.IsActive,"zero remaining troops use native immediate empty-party retirement");
  var already=Party("independent",clan);Check(GwpVoluntarySurrender.TryCapture(envoy.Party,already.LeaderHero!)&&Campaign.Current.Retirement.IsPartyWaitingForDisband(already),"independent party also enters same native retirement once");
  var activeBattle=Party("active_battle",clan);activeBattle.MapEvent=new();var hero=activeBattle.LeaderHero;Check(!GwpVoluntarySurrender.TryCapture(envoy.Party,hero!)&&activeBattle.LeaderHero==hero,"source battle cannot be partially captured");
  Console.WriteLine("VOLUNTARY_SURRENDER_ASSERTIONS="+checks+"; PASS; shared production capture + installed native retirement/army/garrison bodies, controlled navigation/event/ransom/ship boundaries");
 }
}

'''


def build_code(native):
    code = HARNESS[:HARNESS.index('class Program {')]
    code = code.replace('public bool IsPlayerCompanion,IsHumanPlayerCharacter;', 'public bool IsPlayerCompanion,IsHumanPlayerCharacter,IsNoncombatant,Traveling;public bool IsPrisoner=>State==CharacterStates.Prisoner;public bool IsActive=>State==CharacterStates.Active;public bool IsDead=>false;public Kingdom MapFaction=>Clan!.Kingdom!;public PartyBase? PartyBelongedToAsPrisoner=>PrisonerParty;public Settlement? CurrentSettlement;public object? GovernorOf;public KillCharacterAction.KillCharacterActionDetail DeathMark;')
    code = code.replace('public Kingdom? Kingdom;}', 'public Kingdom? Kingdom;public List<Hero> Heroes=new();public string Name="clan";public static Clan PlayerClan=new();}')
    code = code.replace('class CharacterObject{public Hero HeroObject=null!;public bool IsHero;}', 'class CharacterObject{public Hero HeroObject=null!;public bool IsHero;public bool IsPlayerCharacter;public bool IsDead;}' )
    code = code.replace('public int Number=76;', 'public int Number=76,WoundedNumber,Xp;')
    start=code.index('class CaptureRoster{');end=code.index('\nstatic class Debug',start)
    code=code[:start]+'''class CaptureRoster{public List<TroopRosterElement> Rows=new(){new()};public int Count=>Rows.Count;public int TotalManCount=>Rows.Sum(x=>x.Number);public int TotalHeroes=>Rows.Count(x=>x.Character.IsHero);public IEnumerable<TroopRosterElement> GetTroopRoster()=>Rows.ToList();public bool Contains(CharacterObject c)=>true;public void RemoveTroop(CharacterObject c){c.HeroObject.PartyBelongedTo=null;}public void Add(CaptureRoster r){Rows.AddRange(r.Rows);}public void Add(TroopRosterElement r){Rows.Add(r);}public void Clear(){Rows.Clear();}public static CaptureRoster CreateDummyTroopRoster()=>new(){Rows=new()};public CaptureRoster ToFlattenedRoster()=>this;}'''+code[end:]
    code=code.replace('public void OnHeroPrisonerTaken(PartyBase capturer,Hero prisoner){}','public void OnHeroPrisonerTaken(PartyBase capturer,Hero prisoner){}public void OnPartyRemovedFromArmy(MobileParty p){}public void OnArmyOverlaySetDirty(){}public void OnPartyLeftArmy(MobileParty p,Army a){}public void OnArmyDispersed(Army a,Army.ArmyDispersionReason r,bool player){}public void OnPartyDisbandStarted(MobileParty p){Campaign.Current.Retirement.Started(p);}public void OnPrisonerDonatedToSettlement(MobileParty p,CaptureRoster r,Settlement s){}')
    code=code.replace('public void AddPrisoner(CharacterObject c,int count)', 'public MobileParty? MobileParty;public List<Ship> Ships=new();public CaptureRoster PrisonRoster=>MobileParty?.PrisonRoster??_prison;CaptureRoster _prison=new(){Rows=new()};public CaptureRoster MemberRoster=>MobileParty!.MemberRoster;public void SetCustomName(TextObject t){}public void SetVisualAsDirty(){}public void UpdateVisibilityAndInspected(int pos){}public void AddPrisoner(CharacterObject c,int count)')
    code=code.replace('public List<object> Ships=new();','public List<Ship> Ships=>Party.Ships;')
    code=code.replace('public Army? Army;', 'private Army? _army;public Army? Army { ARMY_PROPERTY }')
    code=code.replace('public PartyBase Party=new();', 'public PartyBase Party;public MobileParty(){Party=new(){MobileParty=this};}public enum NavigationType{None,Default,Naval}public Clan? ActualClan;public bool IsDisbanding,IsTransitionInProgress,StartTransitionNextFrameToExitFromPort,HasNavalNavigationCapability=true,IsCaravan;public MobileParty? AttachedTo;public int Position;public void UpdateVersionNo(){}public NativeAi Ai=new();public Clock StationaryStartTime=new();public Items ItemRoster=new();public CaptureRoster PrisonRoster=new(){Rows=new()};public int PartyTradeGold;public void ChangePartyLeader(Hero h){((LordPartyComponent)PartyComponent!)._leader=h;}public void SetMoveGoToSettlement(Settlement s,NavigationType n,bool port){TargetSettlement=s;}')
    code=code.replace('class Settlement {public object? SiegeEvent;}', 'class Settlement {public object? SiegeEvent;public static List<Settlement> All=new();public bool IsFortification=true,IsUnderSiege,IsUnderRaid,HasPort,Reachable=true;public Kingdom MapFaction=new();public Clan? OwnerClan;public float Distance=1;public Town Town=new();public Items ItemRoster=new();public PartyBase Party=new();public void AddGarrisonParty(){Town.GarrisonParty=new();Town.GarrisonParty.MemberRoster.Rows.Clear();}}')
    code=code.replace('public const int HoursInDay=24;', 'public bool IsPast;public static CampaignTime Never=new();public static CampaignTime DaysFromNow(float d)=>new();public const int HoursInDay=24;')
    code=code.replace('public Models Models=new();', 'public NativeRetirement Retirement=new();public PartyBase? CameraFollowParty;public T? GetCampaignBehavior<T>() where T:class=>Retirement as T;public Models Models=new();')
    code=code.replace('class Models{', 'class Models{public PrisonerDonationModel PrisonerDonationModel=new();')
    code=code.replace('public float Cohesion=49;', 'public float Cohesion=49;')
    code=code.replace('public int Boosts;', 'List<MobileParty> _parties=>Parties;bool _armyIsDispersing;Timer _hourlyTickEvent=new(),_tickEvent=new();public bool HourlyDeleted=>_hourlyTickEvent.Deleted;public bool TickDeleted=>_tickEvent.Deleted;public enum ArmyDispersionReason{Unknown,LeaderPartyRemoved,ArmyLeaderIsDead,ObjectiveFinished,NotEnoughParty,PlayerTakenPrisoner}public void StopTrackingTargetSettlement(){}public void OnAddPartyInternal(MobileParty p){Parties.Add(p);}NATIVE_ARMY_TEARDOWN public int Boosts;')
    code=code.replace('static class DisbandArmyAction{public static void ApplyByCohesionDepleted(Army a){}}', 'static class DisbandArmyAction{public static void ApplyByCohesionDepleted(Army a){}public static void ApplyByLeaderPartyRemoved(Army a)=>a.DisperseInternal(Army.ArmyDispersionReason.LeaderPartyRemoved);public static void ApplyByArmyLeaderIsDead(Army a)=>a.DisperseInternal();public static void ApplyByObjectiveFinished(Army a)=>a.DisperseInternal();public static void ApplyByPlayerTakenPrisoner(Army a)=>a.DisperseInternal();public static void ApplyByNotEnoughParty(Army a)=>a.DisperseInternal();}')
    code=code.replace('static class DestroyShipAction{public static void Apply(object ship){}}','static class DestroyShipAction{public static void Apply(Ship ship){}}static class KillCharacterAction{public enum KillCharacterActionDetail{None}}')
    code=code.replace('public static void ApplyByCohesionDepleted(Army a){}','public static void ApplyByCohesionDepleted(Army a){}public static void ApplyByUnknownReason(Army a)=>a.DisperseInternal();')
    code=code.replace('public bool IsKingdomFaction;', 'public bool IsKingdomFaction;public bool IsAtWarWith(Kingdom other)=>this!=other;')
    cost=native['cost'];methods='\n'.join(extract(cost,n).replace('public override ','public ') for n in ['CalculatePartyInfluenceCost','CalculateTotalInfluenceCost','CalculateTotalInfluenceCostInternal','CalculateDailyCohesionChange','CalculateCohesionChangeInternal'])
    methods=methods.replace('public int CalculatePartyInfluenceCost(', 'private int NativePartyInfluenceCost(').replace('public ExplainedNumber CalculateDailyCohesionChange(', 'private ExplainedNumber NativeCohesionChange(')
    module=ROOT/'GreyWardenPolicePurity';patch=(module/'GwpAssistanceArmyLifecyclePatch.cs').read_text(encoding='utf-8-sig')
    parts={'TOTAL_CAPTURE_METHOD':extract(patch[patch.index('internal static class GwpArmyInfluenceFailureTracePatch'):],'Finalizer'), 'CAPTURE_METHOD':extract(patch[patch.index('internal static class GwpLeaderlessSupportArmyInfluencePatch'):],'Finalizer'),'COST_PATCH':extract(patch[patch.index('internal static class GwpLeaderlessSupportArmyInfluencePatch'):],'Prefix'),'COHESION_PATCH':extract(patch[patch.index('internal static class GwpAssistanceArmyCohesionPatch'):],'Postfix'),'COST_METHODS':methods,'NATIVE_METHOD':'\n'.join(extract(native['diplomacy'],n).replace('public override ','public ') for n in ['GetHeroesForEffectiveRelation','GetEffectiveRelation']),'ARMY_METHODS':'\n'.join(extract(native['army'],n) for n in ['HourlyTick','ThinkAboutCohesionBoost']),'PRISONER_ACTION_BODY':extract(native['capture'],'ApplyInternal'),'REMOVE_PARTY_LEADER':extract(native['party'],'RemovePartyLeader'),'PARTY_LEADER_CHANGE':extract(native['component'],'ChangePartyLeader'),'LORD_LEADER_CHANGE':extract(native['lord'].replace('protected override void OnChangePartyLeader','internal override void OnChangePartyLeader'),'OnChangePartyLeader').replace('internal override void','protected override void'),'ENVOY_PRODUCTION_METHODS':'\n'.join(extract((module/'PoliceEnforcementBehavior.Envoy.cs').read_text(encoding='utf-8-sig'),n) for n in ['Negotiate','BeginAftermath','TendAftermath']),'NATIVE_ARMY_TEARDOWN':'\n'.join(extract(native['army'],n) for n in ['OnRemovePartyInternal','DisperseInternal'])}
    prop=native['party'];start=prop.index('\n\tpublic Army Army');end=prop.index('\n\tpublic BesiegerCamp',start);parts['ARMY_PROPERTY']=prop[start:end][prop[start:end].index('{')+1:prop[start:end].rfind('}')]
    for key in sorted(parts,key=len,reverse=True):code=code.replace(key,parts[key])
    owner=(module/'GwpVoluntarySurrender.cs').read_text(encoding='utf-8-sig');owner=owner[owner.index('    internal static class'):owner.rfind('}')]
    extra=EXTRA.replace('VOLUNTARY_SOURCE',owner).replace('NATIVE_DISBAND',extract(native['disband'],'StartDisband')).replace('NATIVE_RETIREMENT','\n'.join(extract(native['retire'],n) for n in ['OnPartyDisbandStarted','MergeDisbandPartyToFortification','CheckDisbandedPartyDaily','HourlyTick']))
    extra=extra.replace('NATIVE_RETIREMENT','')
    # Unexercised player-caravan branch of the complete native retirement method.
    extra=extra.replace(' NATIVE_RETIREMENT','')
    extra=extra.replace('public void Started(MobileParty p)', 'void GetTargetSettlementForDisbandingParty(MobileParty p,out Settlement? s,out MobileParty.NavigationType nav,out bool port){s=p.TargetSettlement;nav=MobileParty.NavigationType.Default;port=false;}public void Started(MobileParty p)')
    return 'using TroopRoster=CaptureRoster;\n'+code+extra


class VoluntarySurrenderTests(unittest.TestCase):
    def test_production_capture_return_and_native_army(self):
        types={'diplomacy':'GameComponents.DefaultDiplomacyModel','cost':'GameComponents.DefaultArmyManagementCalculationModel','army':'Army','capture':'Actions.TakePrisonerAction','party':'Party.MobileParty','component':'Party.PartyComponents.PartyComponent','lord':'Party.PartyComponents.LordPartyComponent','disband':'Actions.DisbandPartyAction','retire':'CampaignBehaviors.DisbandPartyCampaignBehavior'}
        native={k:subprocess.check_output(['ilspycmd','-t','TaleWorlds.CampaignSystem.'+v,str(GAME_DLL)],text=True,encoding='utf-8-sig') for k,v in types.items()}
        code=build_code(native)
        major=subprocess.check_output(['dotnet','--version'],text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-voluntary-') as temp:
            p=Path(temp);(p/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>')
            (p/'Program.cs').write_text(code,encoding='utf-8')
            r=subprocess.run(['dotnet','run','--project',temp,'-c','Release','-v','quiet'],text=True,capture_output=True)
            self.assertEqual(0,r.returncode,'\n'.join(l for l in r.stdout.splitlines() if ': warning ' not in l)+r.stderr)
            print('\n'.join(l for l in r.stdout.splitlines() if l.startswith('VOLUNTARY_')))


if __name__=='__main__': unittest.main()
