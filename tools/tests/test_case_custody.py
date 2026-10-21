"""Commission custody, native barter entry/valuation and prisoner transfer bodies.

Campaign objects, event delivery, UI and navigation remain controlled boundaries.
No game is launched and no save is read or changed.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

from test_duty_banner import method

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'
GAME = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')


class CaseCustodyTests(unittest.TestCase):
    def test_captor_trade_cancellation_and_field_identity(self):
        dll=GAME/'bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll'
        def native(name):
            return subprocess.check_output(['ilspycmd','-t',name,str(dll)],text=True,encoding='utf-8')
        barter=native('TaleWorlds.CampaignSystem.BarterSystem.BarterManager')
        transfer=native('TaleWorlds.CampaignSystem.Actions.TransferPrisonerAction')
        prisoner=native('TaleWorlds.CampaignSystem.BarterSystem.Barterables.TransferPrisonerBarterable')
        gold=native('TaleWorlds.CampaignSystem.BarterSystem.Barterables.GoldBarterable')
        custody=(MODULE/'PlayerBountyBehavior.Custody.cs').read_text(encoding='utf-8-sig')
        quest=(MODULE/'PlayerBountyBehavior.QuestAndNotification.cs').read_text(encoding='utf-8-sig')
        field=(MODULE/'GwpFieldArrestBehavior.cs').read_text(encoding='utf-8-sig')
        keys=sorted(set(re.findall(r'GwpTextCatalog\.(\w+)',custody+method(quest,'CancelUnavailableCommission'))))
        code=r'''
using System;using System.Linq;using System.Collections.Generic;
using TaleWorlds.CampaignSystem;using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Actions;using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.Library;using TaleWorlds.Localization;
namespace TaleWorlds.Localization {public class TextObject {public string Value;public TextObject(string s){Value=s;}public override string ToString()=>Value;}public static class MBTextManager{public static void SetTextVariable(string k,object v){}}}
namespace TaleWorlds.Library {public static class InformationManager{public static void DisplayMessage(InformationMessage m){}}public class InformationMessage{public InformationMessage(string m){}}public static class Debug{public static int Rejections;public static void FailedAssert(string m,string f,string method,int l){Rejections++;}}public static class MBInformationManager{public static void AddQuickInformation(TextObject t){}}public static class GameTexts{public static TextObject FindText(string key)=>new(key);}}
namespace TaleWorlds.Core {public class ItemRoster{}}
namespace TaleWorlds.CampaignSystem {
 public class Faction:IFaction{public bool IsAtWarWith(Faction other)=>false;}
 public interface IFaction{}public class Clan:Faction,IFaction{public Hero Leader;}
 public class Hero{public static Hero MainHero,OneToOneConversationHero;public bool IsPrisoner,IsDead,IsFugitive,IsDisabled;public string StringId;public string Name;public PartyBase PartyBelongedToAsPrisoner;public MobileParty PartyBelongedTo;public Settlement CurrentSettlement;public int Gold=5000;public Clan Clan=new();public Faction MapFaction=>Clan;public CharacterObject CharacterObject;public Hero(string id){StringId=Name=id;CharacterObject=new(){HeroObject=this};}}
 public class CharacterObject{public Hero HeroObject;}
 public class Settlement{public Town Town=new();public Clan OwnerClan;public string Name="castle";}
 public class Town{public Hero Governor;}
 public static class PlayerCaptivity{public static PartyBase CaptorParty;}
 public class Campaign {public static Campaign Current=new();public BarterManager BarterManager=new();public Models Models=new();public Conversation ConversationManager=new();public T GetCampaignBehavior<T>() where T:class=>null;}
 public class Models {public Ransom RansomValueCalculationModel=new();}public class Ransom{public int PrisonerRansomValue(CharacterObject c)=>3500;}
 public class Conversation{public bool CurrentConversationIsFirst=true,IsConversationInProgress=true;public int Continued;public void ContinueConversation(){Continued++;}}
 public class CampaignGameStarter{public void AddPlayerLine(string a,string b,string c,string text,Func<bool> f,Action x,int priority=100){}public void AddDialogLine(string a,string b,string c,string text,Func<bool> f,Action x,int priority=100){}}
 public class CampaignMission{public static CampaignMission Current=>null;public void SetMissionMode(MissionMode m,bool atStart){}}
 public enum MissionMode{Barter}
 public class CampaignEventDispatcher{public static CampaignEventDispatcher Instance=new();public void OnBarterablesRequested(BarterData d){d.GetBarterables().Add(new GoldBarterable(Hero.MainHero,Hero.OneToOneConversationHero,MobileParty.MainParty.Party));d.GetBarterables().Add(new ItemBarterable());d.GetBarterables().Add(new Junk());}public void OnBarterAccepted(Hero a,Hero b,List<Barterable> x){}}
}
namespace TaleWorlds.CampaignSystem.Party {
 public class Roster{readonly PartyBase owner;public Roster(PartyBase p){owner=p;}public int Count;public void AddToCounts(CharacterObject c,int n){Count+=n;}}
 public class PartyBase{public static PartyBase MainParty=>MobileParty.MainParty.Party;public string Name="holder";public MobileParty MobileParty;public Settlement Settlement;public Faction MapFaction=new();public Roster PrisonRoster;public PartyBase(){PrisonRoster=new(this);}public void AddPrisoner(CharacterObject c,int n){PrisonRoster.Count+=n;c.HeroObject.IsPrisoner=true;c.HeroObject.PartyBelongedToAsPrisoner=this;}}
 public class MobileParty{public static List<MobileParty> All=new();public string StringId="party";public static MobileParty MainParty,ConversationParty;public PartyBase Party;public bool IsActive=true;public Hero LeaderHero;public Settlement CurrentSettlement;public MobileParty(){StringId="party"+All.Count;All.Add(this);Party=new(){MobileParty=this};}}
}
namespace TaleWorlds.CampaignSystem.Actions {public static class TransferPrisonerAction { TRANSFER_INTERNAL TRANSFER_PUBLIC }public static class GiveGoldAction{public static void ApplyBetweenCharacters(Hero a,Hero b,int n){a.Gold-=n;b.Gold+=n;}public static void ApplyForPartyToParty(PartyBase a,PartyBase b,int n)=>throw new Exception("unexpected nonhero payment");public static void ApplyForPartyToCharacter(PartyBase a,Hero b,int n)=>throw new Exception("unexpected nonhero payment");public static void ApplyForCharacterToParty(Hero a,PartyBase b,int n)=>throw new Exception("unexpected nonhero payment");}}
namespace TaleWorlds.CampaignSystem.BarterSystem {
 public class GoldBarterGroup{}public class ItemBarterGroup{}public class PrisonerBarterGroup{}public class OtherBarterGroup{}public class DefaultsBarterGroup{}
 public class BarterData{public PartyBase OffererParty;public List<Barterable> Entries=new();public int ContextPrisoners;public BarterData(Hero a,Hero b,PartyBase c,PartyBase d,BarterManager.BarterContextInitializer init,int reduction){OffererParty=c;}public List<Barterable> GetBarterables()=>Entries;public List<Barterable> GetOfferedBarterables()=>Entries.Where(e=>e.IsOffered&&e.CurrentAmount>0).ToList();public void AddBarterGroup(object g){}public void AddBarterable<T>(Barterable b,bool isContextDependent=false){Entries.Add(b);if(b is TransferPrisonerBarterable&&isContextDependent)ContextPrisoners++;}}
 public class BarterManager{public delegate void BarterBeginEventDelegate(BarterData d);public delegate bool BarterContextInitializer(Barterable b,BarterData d,object obj=null);public BarterBeginEventDelegate BarterBegin;public bool LastBarterIsAccepted;float _overpayAmount;public bool CanPlayerBarterWithHero(Hero h)=>true;void ClearHeroCooldowns(){}void ApplyOverpayBonus(Hero h){}void Close(){}public void Accept(Hero a,Hero b,BarterData d){ApplyBarterOffer(a,b,d.GetOfferedBarterables());} NATIVE_ADD_BASE NATIVE_START NATIVE_BEGIN NATIVE_ACCEPTABLE NATIVE_OFFER_VALUE NATIVE_APPLY }
}
namespace TaleWorlds.CampaignSystem.BarterSystem.Barterables {
 public abstract class Barterable{public Hero OriginalOwner;public PartyBase OriginalParty;public bool IsOffered;public int CurrentAmount;public void SetIsOffered(bool value){IsOffered=value;}public int GetValueForFaction(IFaction f)=>CurrentAmount*GetUnitValueForFaction(f);public virtual void Apply(){}public virtual int GetUnitValueForFaction(IFaction f)=>0;}
 public class GoldBarterable:Barterable{Hero _ownerHero,_otherHero;PartyBase _otherParty;public GoldBarterable(Hero a,Hero b,PartyBase p){_ownerHero=OriginalOwner=a;_otherHero=b;OriginalParty=p;_otherParty=b.PartyBelongedTo.Party;}NATIVE_GOLD_VALUE NATIVE_GOLD_APPLY}public class ItemBarterable:Barterable{}public class Junk:Barterable{}
 public class TransferPrisonerBarterable:Barterable{protected Hero _prisonerCharacter,_opponentHero;protected PartyBase _otherParty;public TransferPrisonerBarterable(Hero h,Hero owner,PartyBase from,Hero other,PartyBase to){_prisonerCharacter=h;_opponentHero=other;_otherParty=to;OriginalOwner=owner;OriginalParty=from;}NATIVE_VALUE NATIVE_PRISONER_APPLY}
}
namespace GreyWardenPolicePurity {
 static class GwpTextCatalog{KEYS}
 static class GwpText{public static string Get(string k,params object[] a)=>k;public static TextObject Create(string k,params object[] a)=>new(k);}
 static class GwpPlayerCaseAssignment{public static bool Authority=true;public static bool HasAuthority(Hero h)=>Authority;}
 static class CrimePool{public static CrimeRecord Record=new();public static CrimeRecord GetRecord(Hero h)=>Record;}class CrimeRecord{public bool HasOpenCase=true;public static Settlement? ShelterOf(Hero? h)=>null;}
 public partial class PlayerBountyBehavior {
  string _activeBountyTargetId;public bool IsTrackingBountyTarget=true,HasCompletedEnforcement;public Hero CaseHero;public BountyHunterQuest _activeQuest=new();public int Ended,Registered;public bool CaseStillOpen=true;public void EndBountyTaskState(bool peace){Ended++;IsTrackingBountyTarget=false;}public void RegisterCaptureInCustody(){Registered++;}public void LeaveCaseClerk(){} TARGET_RESOLVER
  public class BountyHunterQuest {public int Canceled;public void CompleteQuestWithCancel(TextObject t){Canceled++;}CANCEL_QUEST}
  static int checks;static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
  public static void Main(){
   Hero.MainHero=new("player");MobileParty.MainParty=new();var seller=new Hero("captor");var target=new Hero("offender"){IsPrisoner=true};var holdingParty=new MobileParty{LeaderHero=seller};seller.PartyBelongedTo=holdingParty;target.PartyBelongedToAsPrisoner=holdingParty.Party;holdingParty.Party.PrisonRoster.Count=1;
   Hero.OneToOneConversationHero=seller;MobileParty.ConversationParty=holdingParty;
   var b=new PlayerBountyBehavior{CaseHero=target};Check(b.CanWithdrawUnavailableCommission&&b.CanBuyCasePrisoner(),"third party captive can be purchased or withdrawn");
   Hero.OneToOneConversationHero=target;Check(!b.CanBuyCasePrisoner(),"the captive cannot sell his captor's prisoner");
   var unrelated=new Hero("other");Hero.OneToOneConversationHero=unrelated;Check(!b.CanBuyCasePrisoner(),"unrelated lord cannot sell another party's prisoner");Hero.OneToOneConversationHero=seller;
   var native=new TransferPrisonerBarterable(target,seller,holdingParty.Party,Hero.MainHero,MobileParty.MainParty.Party);native.Apply();Check(Debug.Rejections==1&&target.PartyBelongedToAsPrisoner==holdingParty.Party,"native transfer alone rejects neutral custody");
   BarterData table=null;BarterManager.BarterBeginEventDelegate original=d=>table=d;Campaign.Current.BarterManager.BarterBegin=original;
   b.OpenCaseCustodyBarter();Check(table!=null&&table.Entries.Count==3&&table.ContextPrisoners==1,"native barter opens with currency goods and exactly one fixed case prisoner");
   Check(table.Entries.OfType<Junk>().Count()==0,"unrelated native release/prisoner deals removed");Check(Campaign.Current.BarterManager.BarterBegin==original,"native UI callback restored");
   var purchase=b._casePrisonerTransfer;Check(purchase.GetUnitValueForFaction(seller.Clan)==-3500&&purchase.GetUnitValueForFaction(Hero.MainHero.Clan)==3500,"native prisoner quote and counterparty valuation retained");
   Check(target.PartyBelongedToAsPrisoner==holdingParty.Party&&b.CanWithdrawUnavailableCommission,"opening or declining a barter does not transfer or complete the case");
   var manager=Campaign.Current.BarterManager;Check(!manager.IsOfferAcceptable(table,seller,holdingParty.Party),"native seller rejects an unpaid prisoner offer");
   var money=table.Entries.OfType<GoldBarterable>().Single();money.SetIsOffered(true);money.CurrentAmount=3499;Check(!manager.IsOfferAcceptable(table,seller,holdingParty.Party),"native seller rejects insufficient ransom");money.CurrentAmount=3500;Check(manager.IsOfferAcceptable(table,seller,holdingParty.Party),"native seller accepts its actual quoted ransom");
   int buyerGold=Hero.MainHero.Gold,sellerGold=seller.Gold;manager.Accept(Hero.MainHero,seller,table);Check(Hero.MainHero.Gold==buyerGold-3500&&seller.Gold==sellerGold+3500,"native barter applies actual money exactly once");
   Check(purchase.Transferred&&target.PartyBelongedToAsPrisoner==MobileParty.MainParty.Party,"approved purchase uses actual native prisoner transfer body without war");Check(holdingParty.Party.PrisonRoster.Count==0&&MobileParty.MainParty.Party.PrisonRoster.Count==1,"one real prisoner moved and none duplicated");purchase.Apply();Check(MobileParty.MainParty.Party.PrisonRoster.Count==1,"duplicate application harmless");Check(!b.CanWithdrawUnavailableCommission,"owned captive proceeds to existing report instead of cancellation");
   target.PartyBelongedToAsPrisoner=holdingParty.Party;holdingParty.Party.PrisonRoster.Count=1;
   var stale=new CasePrisonerTransfer(target,seller,holdingParty.Party,Hero.MainHero,MobileParty.MainParty.Party);target.PartyBelongedToAsPrisoner=new PartyBase();bool rejected=false;try{stale.Apply();}catch(InvalidOperationException){rejected=true;}Check(rejected,"a different holder is never robbed by stale barter data");target.PartyBelongedToAsPrisoner=holdingParty.Party;
   b.HasCompletedEnforcement=true;Check(!b.CanWithdrawUnavailableCommission,"already completed commission still requires report");b.HasCompletedEnforcement=false;
   CrimePool.Record.HasOpenCase=false;Check(!b.CanWithdrawUnavailableCommission&&!b.CanBuyCasePrisoner(),"actually settled case cannot be purchased again");CrimePool.Record.HasOpenCase=true;
   Check(b.WithdrawUnavailableCommission()&&b.Ended==1&&b._activeQuest.Canceled==1&&b.CaseStillOpen,"withdraw cancels only commission and retains the unpaid case");Check(!b.WithdrawUnavailableCommission()&&b.Ended==1,"double withdrawal does nothing");
   b.IsTrackingBountyTarget=true;target.IsPrisoner=false;target.PartyBelongedTo=holdingParty;Check(!b.WithdrawUnavailableCommission(),"free active offender does not grant penalty-free withdrawal");target.IsPrisoner=true;
   var castle=new Settlement{OwnerClan=new(){Leader=seller}};target.PartyBelongedToAsPrisoner=new(){Settlement=castle};seller.CurrentSettlement=castle;Check(b.CanBuyCasePrisoner(),"present settlement owner can sell dungeon prisoner");castle.OwnerClan.Leader=unrelated;Check(!b.CanBuyCasePrisoner(),"visitor cannot sell dungeon prisoner");castle.Town.Governor=seller;Check(b.CanBuyCasePrisoner(),"present governor can negotiate dungeon custody");
   var arrest=new GwpFieldArrestBehavior();Hero.OneToOneConversationHero=target;MobileParty.ConversationParty=holdingParty;
   Check(!arrest.FieldArrestAvailableCondition(),"prisoner conversation never opens free-field enforcement");arrest.SetTarget(target,holdingParty);arrest.AttackConsequence();Check(arrest.Pending==null&&arrest.Left==1,"existing prisoner dialogue cannot queue attack on captor");
   target.IsPrisoner=false;target.PartyBelongedTo=holdingParty;Check(arrest.FieldArrestAvailableCondition(),"free assigned offender keeps original field entrance");arrest.SetTarget(target,holdingParty);arrest.AttackConsequence();Check(arrest.Pending==holdingParty,"free assigned offender can still be attacked");
   target.IsPrisoner=false;target.IsFugitive=true;b._activeBountyTargetId=holdingParty.StringId;Check(b.ResolveBountyTargetParty()==null,"fugitive does not keep reporting his still-active former army");Check(b.CanWithdrawUnavailableCommission&&!b.CanBuyCasePrisoner(),"fugitive with stale former party can withdraw but cannot be bought from a captor");
   Check(b.WithdrawUnavailableCommission()&&b.Ended==2&&CrimePool.Record.HasOpenCase,"fugitive withdrawal releases commission while retaining original unresolved case");
   b.IsTrackingBountyTarget=true;target.IsFugitive=false;target.PartyBelongedTo=null;Check(b.ResolveBountyTargetParty()==null,"respawning target cannot fall back to cached former army");Check(b.CanWithdrawUnavailableCommission,"waiting for respawn with no party may withdraw");
   target.PartyBelongedTo=holdingParty;holdingParty.IsActive=false;Check(b.CanWithdrawUnavailableCommission,"destroyed former party does not block withdrawal");holdingParty.IsActive=true;
   target.IsDisabled=true;Check(b.CanWithdrawUnavailableCommission,"temporarily disabled target permits no-penalty withdrawal");target.IsDisabled=false;
   Check(!b.CanWithdrawUnavailableCommission,"once target returns to active party the exceptional withdrawal closes");
   target.IsFugitive=true;b.HasCompletedEnforcement=true;Check(!b.CanWithdrawUnavailableCommission,"already defeated or settled fugitive must use report rather than withdraw");b.HasCompletedEnforcement=false;
   target.IsDead=true;Check(!b.CanWithdrawUnavailableCommission,"dead target keeps external-closure report flow");target.IsDead=false;
   CrimePool.Record.HasOpenCase=false;Check(!b.CanWithdrawUnavailableCommission,"no open unpaid case cannot be returned twice");CrimePool.Record.HasOpenCase=true;
   target.IsFugitive=false;var rebuilt=new MobileParty();target.PartyBelongedTo=rebuilt;Check(b.ResolveBountyTargetParty()==rebuilt&&b._activeBountyTargetId==rebuilt.StringId,"returned target follows new real party");
   b.CaseHero=null;Check(!b.CanWithdrawUnavailableCommission,"unresolved hero lookup does not invent a returned case");
   Console.WriteLine("CASE_CUSTODY_ASSERTIONS="+checks+"; PASS; production custody and field entry with installed barter/quote/transfer bodies; UI and world controlled");
  }
 }
 class GwpFieldArrestBehavior {
  Hero _offender;MobileParty _offenderParty,_pendingBattleParty;int _fine;string _desire="";public MobileParty Pending=>_pendingBattleParty;public int Left;public void SetTarget(Hero h,MobileParty p){_offender=h;_offenderParty=p;}void EndPersuasionConsequence(){}void ClearState(){_offender=null;_offenderParty=null;}void LeaveEncounterPeacefully(){Left++;}FIELD_ENTRY FIELD_ATTACK
 }
 static class GwpAiDiagnostics{public static void WriteFieldArrest(string a,string b){}}
}
'''
        code=code.replace('TRANSFER_INTERNAL',method(transfer,'ApplyInternal')).replace('TRANSFER_PUBLIC',method(transfer,'Apply'))
        code=code.replace('NATIVE_ADD_BASE',method(barter,'AddBaseBarterables')).replace('NATIVE_START',method(barter,'StartBarterOffer')).replace('NATIVE_BEGIN',method(barter,'BeginPlayerBarter'))
        code=code.replace('NATIVE_VALUE',method(prisoner,'GetUnitValueForFaction')).replace('NATIVE_PRISONER_APPLY',method(prisoner,'Apply'))
        code=code.replace('NATIVE_ACCEPTABLE',method(barter,'IsOfferAcceptable')).replace('NATIVE_OFFER_VALUE',method(barter,'GetOfferValue')).replace('NATIVE_APPLY',method(barter,'ApplyBarterOffer'))
        code=code.replace('NATIVE_GOLD_VALUE',method(gold,'GetUnitValueForFaction')).replace('NATIVE_GOLD_APPLY',method(gold,'Apply'))
        code=code.replace('KEYS',''.join(f'public const string {k}="{k}";' for k in keys)).replace('CANCEL_QUEST',method(quest,'CancelUnavailableCommission'))
        code=code.replace('FIELD_ENTRY',method(field,'FieldArrestAvailableCondition').replace('private bool','public bool',1)).replace('FIELD_ATTACK',method(field,'AttackConsequence').replace('private void','public void',1))
        code=code.replace('public void LeaveCaseClerk(){}','public void LeaveCaseClerk(){}public void NotifyCaseCombatStarting(Hero h){}')
        code=code.replace('TARGET_RESOLVER',method((MODULE/'PlayerBountyBehavior.cs').read_text(encoding='utf-8-sig'),'ResolveBountyTargetParty'))
        code+='\n'+custody
        major=subprocess.check_output(['dotnet','--version'],text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-case-custody-') as directory:
            p=Path(directory)
            (p/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>annotations</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>',encoding='utf-8')
            # Keep production using directives at file scope in their own file.
            code=code.removesuffix('\n'+custody)
            (p/'Program.cs').write_text(code,encoding='utf-8')
            (p/'Custody.cs').write_text(custody,encoding='utf-8')
            result=subprocess.run(['dotnet','run','--project',str(p/'Test.csproj'),'-c','Release','-v','quiet'],capture_output=True,text=True,timeout=90)
            self.assertEqual(0,result.returncode,result.stdout+result.stderr)
            print(next(l for l in result.stdout.splitlines() if l.startswith('CASE_CUSTODY_ASSERTIONS=')))


if __name__=='__main__':
    unittest.main()
