// Only the engine boundary is stubbed; mission and probability code are linked production sources.
using System;
using System.Collections.Generic;
using System.Linq;
namespace TaleWorlds.Library
{
 public struct Vec2
 {
  public float X,Y; public Vec2(float x,float y){X=x;Y=y;}
  public static Vec2 Forward=>new(0,1); public float LengthSquared=>X*X+Y*Y;
  public Vec2 Normalized(){float n=(float)Math.Sqrt(LengthSquared);return new(X/n,Y/n);}
  public Vec3 ToVec3(float z=0)=>new(X,Y,z);
  public static Vec2 operator +(Vec2 a,Vec2 b)=>new(a.X+b.X,a.Y+b.Y);
  public static Vec2 operator -(Vec2 a,Vec2 b)=>new(a.X-b.X,a.Y-b.Y);
  public static Vec2 operator *(Vec2 a,float f)=>new(a.X*f,a.Y*f);
 }
 public struct Vec3
 {
  public float x,y,z; public Vec3(float a,float b,float c){x=a;y=b;z=c;}
  public float Z=>z; public Vec2 AsVec2=>new(x,y); public static Vec3 Up=>new(0,0,1);
  public Vec3 NormalizedCopy()=>this; public static Vec3 CrossProduct(Vec3 a,Vec3 b)=>a;
  public static Vec3 operator +(Vec3 a,Vec3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
 }
 public struct Mat3 {public Vec3 f,u,s; public static Mat3 Identity=>new();}
 public struct MatrixFrame {public Mat3 rotation;public Vec3 origin;public MatrixFrame(Mat3 r,Vec3 o){rotation=r;origin=o;}}
}
namespace TaleWorlds.Core
{
 public enum BattleSideEnum {None=-1,Defender=0,Attacker=1}
 public enum FormationClass {Infantry,Ranged,Cavalry}
 public enum AgentState {Killed,Unconscious,Routed,Deleted}
 public class Banner {} public class KillingBlow {} public class WeaponComponentData {} public enum TroopTraitsMask {None}
 public interface IBattleCombatant {}
 public struct UniqueTroopDescriptor {public int UniqueSeed;public UniqueTroopDescriptor(int s){UniqueSeed=s;}}
 public class Game {public static Game Current=new();int _seed;public int NextUniqueTroopSeed=>++_seed;}
 public class BasicCharacterObject
 {
  public string StringId="plain";public bool IsHero;public float Power=1;
  public float GetPower()=>Power;public int GetDefaultFaceSeed(int seed)=>seed;
 }
 public interface IAgentOriginBase
 {
  bool IsUnderPlayersCommand{get;} bool IsInSameArmyAsPlayer{get;}
  uint FactionColor{get;}uint FactionColor2{get;}IBattleCombatant BattleCombatant{get;}
  int UniqueSeed{get;}int Seed{get;}Banner Banner{get;}BasicCharacterObject Troop{get;}
  bool HasThrownWeapon{get;}bool HasHeavyArmor{get;}bool HasShield{get;}bool HasSpear{get;}
  void SetWounded();void SetKilled();void SetRouted(bool isOrderRetreat);void OnAgentRemoved(float agentHealth);
  void OnScoreHit(BasicCharacterObject victim,BasicCharacterObject formationCaptain,int damage,bool isFatal,bool isTeamKill,WeaponComponentData attackerWeapon);
  void SetBanner(Banner banner);TroopTraitsMask GetTraitsMask();
 }
 public static class MBRandom
 {
  public static Queue<float> Rolls=new();public static int Calls;
  public static float RandomFloat {get{Calls++;return Rolls.Count>0?Rolls.Dequeue():.99f;}}
  public static float RandomFloatRanged(float a,float b)=>(a+b)/2;
 }
 public static class MBInformationManager
 {public static List<string> Messages=new();public static void AddQuickInformation(TaleWorlds.Localization.TextObject text,int v)=>Messages.Add(text.Text);}
}
namespace TaleWorlds.Localization {public class TextObject {public string Text;public TextObject(string s){Text=s;}}}
namespace TaleWorlds.Engine
{
 public class Scene {public float GetGroundHeightAtPosition(TaleWorlds.Library.Vec3 position)=>0;}
 public class SoundEvent
 {
  public static int Active;
  public static int GetEventIdFromString(string s)=>1;public static SoundEvent CreateEvent(int id,Scene scene)=>new();
  public void Play(){Active++;}public void Stop(){Active--;}public void Release(){}
 }
}
namespace TaleWorlds.CampaignSystem
{
 public class Campaign
 {
  public static Campaign? Current;public GreyWardenPolicePurity.PlayerBountyBehavior Bounty=new();
  public T? GetCampaignBehavior<T>() where T:class=>Bounty as T;
 }
}
namespace TaleWorlds.MountAndBlade
{
 using TaleWorlds.Core;using TaleWorlds.Engine;using TaleWorlds.Library;
 public static class GameNetwork {public static bool IsMultiplayer;}
 public enum MissionMode {Battle,Deployment,Conversation}
 public struct MovementOrder
 {
  public enum MovementOrderEnum {Charge,Retreat,Move,FallBack,Stop}
  public MovementOrderEnum OrderEnum;
 }
 public class CommonAIComponent {public float Morale{get;set;} public void Panic(){}public void Retreat(bool useCachingSystem=false){}}
 public class HumanAIComponent
 {
  public enum BehaviorValueSet {Charge,DefaultMove}
  public BehaviorValueSet Value;
  public void SetBehaviorValueSet(BehaviorValueSet value)=>Value=value;
  public void RefreshBehaviorValues(MovementOrder.MovementOrderEnum movementOrder){}
 }
 public enum MissionBehaviorType {Other}
 public class MissionBehavior
 {
  public Mission Mission=null!;public virtual MissionBehaviorType BehaviorType=>MissionBehaviorType.Other;
  public virtual void OnMissionTick(float dt){}public virtual void OnAgentBuild(Agent agent,Banner banner){}
  public virtual void OnAgentRemoved(Agent agent,Agent affector,AgentState state,KillingBlow blow){}
  protected virtual void OnEndMission(){}public virtual void OnRemoveBehavior(){}
 }
 public class Team {public BattleSideEnum Side;public Formation GetFormation(FormationClass c)=>new();}
 public class Formation {public MovementOrder Order;public ref readonly MovementOrder GetReadonlyMovementOrderReference()=>ref Order;}
 public class Agent
 {
  public bool IsHuman=true,Active=true,IsRunningAway;public Team Team=null!;public BasicCharacterObject Character=new();
  public IAgentOriginBase Origin=null!;public Vec3 Position;public float CharacterPowerCached=>Character.GetPower();
  public Mission Mission=null!;public bool IsAIControlled=true;public Formation? Formation;
  public HumanAIComponent? HumanAIComponent=new();public float GetMorale()=>0;
  public bool IsActive()=>Active;public enum WatchState{Alarmed}public void SetWatchState(WatchState state){}
 }
 public class AgentBuildData
 {
  public IAgentOriginBase Origin;public Team Target=null!;public Vec3 Position;
  public AgentBuildData(IAgentOriginBase origin){Origin=origin;}
  public AgentBuildData Team(Team t){Target=t;return this;}public AgentBuildData Formation(Formation f)=>this;
  public AgentBuildData InitialPosition(in Vec3 p){Position=p;return this;}public AgentBuildData InitialDirection(in Vec2 d)=>this;
 }
 public class Mission
 {
  public bool MissionEnded;public object? MissionResult;public Agent? MainAgent;public Team PlayerTeam=null!;
  public MissionMode Mode=MissionMode.Battle;
  public Scene Scene=new();public List<Agent> Agents=new();public List<MissionBehavior> Behaviors=new();
  public List<Agent> Spawned=new();public bool FailSpawn;
  public T? GetMissionBehavior<T>() where T:MissionBehavior=>Behaviors.OfType<T>().FirstOrDefault();
  public void Add(MissionBehavior b){b.Mission=this;Behaviors.Add(b);}
  public Vec2 GetClosestBoundaryPosition(Vec2 p)=>new(-100,0);public bool IsPositionInsideBoundaries(Vec2 p)=>true;
  public Agent SpawnAgent(AgentBuildData d)
  {
   if(FailSpawn) throw new InvalidOperationException("injected spawn failure");
   var a=new Agent{Mission=this,Origin=d.Origin,Character=d.Origin.Troop,Team=d.Target,Position=d.Position};
   Agents.Add(a);Spawned.Add(a);foreach(var b in Behaviors)b.OnAgentBuild(a,new Banner());return a;
  }
 }
 public class DefaultBattleMissionAgentSpawnLogic:MissionBehavior
 {
  public bool IsDeploymentOver=true;public int NumberOfRemainingDefenderTroops,NumberOfRemainingAttackerTroops;
  public static int MaxNumberOfAgentsForMission=2000;
  public int NumberOfAgents=>Mission.Agents.Count;
  public bool IsSideDepleted(BattleSideEnum side)=>false;
  public List<IAgentOriginBase>[] Rosters={new(),new()};
  public int GetTotalNumberOfTroopsForSide(BattleSideEnum s)=>Rosters[(int)s].Count;
  public IEnumerable<IAgentOriginBase> GetAllTroopsForSide(BattleSideEnum s)=>Rosters[(int)s];
 }
 public class BasicBattleAgentOrigin:IAgentOriginBase
 {
  public BasicBattleAgentOrigin(BasicCharacterObject t){Troop=t;}
  public BasicCharacterObject Troop{get;}public bool IsUnderPlayersCommand{get;set;}public bool IsInSameArmyAsPlayer=>IsUnderPlayersCommand;
  public uint FactionColor=>1;public uint FactionColor2=>2;public IBattleCombatant BattleCombatant{get;set;}=null!;
  public int UniqueSeed=>1;public int Seed=>1;public Banner Banner=>new();
  public bool HasThrownWeapon=>false;public bool HasHeavyArmor=>true;public bool HasShield=>true;public bool HasSpear=>false;
  public int Mutations;public void SetWounded(){Mutations++;}public void SetKilled(){Mutations++;}public void SetRouted(bool b){Mutations++;}
  public void OnAgentRemoved(float h){Mutations++;}public void OnScoreHit(BasicCharacterObject a,BasicCharacterObject b,int d,bool f,bool k,WeaponComponentData w){Mutations++;}
  public void SetBanner(Banner b){}public TroopTraitsMask GetTraitsMask()=>TroopTraitsMask.None;
 }
}
namespace GreyWardenPolicePurity
{
 using TaleWorlds.Core;using TaleWorlds.MountAndBlade;
 internal static class GwpCommon {public static bool IsGreyWardenAffiliatedCharacter(BasicCharacterObject c)=>c.StringId.StartsWith("gw");}
 internal static class GwpText {public static string Get(string s)=>s;}
 internal static class GwpFaultTrace {public static List<string> Logs=new();public static void Write(string s,string details)=>Logs.Add(s+" "+details);public static void Write(string s,Agent agent,string details)=>Logs.Add(s+" "+details);public static void WriteQuiet(Exception e)=>Logs.Add(e.Message);}
 internal static class PlayerBehaviorPool {public static int Reputation;}
 public class PlayerBountyBehavior {public bool IsRecruitedByGreyWardens;}
 internal class GreyWardenFieldSparringMissionController:MissionBehavior{}
 internal class GwpSyndicateMusicBehavior:MissionBehavior {public List<BattleSideEnum> Arrivals=new();public void NotifyReinforcementArrival(BattleSideEnum s,int c)=>Arrivals.Add(s);}
}
namespace HarmonyLib
{
 public enum MethodType {Setter}
 [System.AttributeUsage(System.AttributeTargets.Class)]public class HarmonyPatch:System.Attribute {public HarmonyPatch(System.Type type,string name){}public HarmonyPatch(System.Type type,string name,MethodType method){}}
 [System.AttributeUsage(System.AttributeTargets.Method)]public class HarmonyPrefix:System.Attribute {}
 [System.AttributeUsage(System.AttributeTargets.Method)]public class HarmonyPostfix:System.Attribute {}
}
