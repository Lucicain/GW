// Minimal in-memory actors for production ledger/payment tests. These stubs do
// not exercise native barter UI, campaign event order, combat or prisoner transfer.
using System;
using System.Collections.Generic;
using System.Linq;
namespace TaleWorlds.CampaignSystem
{
    public interface IFaction { }
    public class CharacterObject { public Hero HeroObject = null!; }
    public class Clan : IFaction { }
    public class Hero
    {
        private static readonly List<Hero> all = new();
        public static Hero MainHero = null!;
        public string StringId; public string Name; public int Gold;
        public bool IsPrisoner;
        public Party.PartyBase? PartyBelongedToAsPrisoner;
        public CharacterObject CharacterObject => new CharacterObject { HeroObject = this };
        public Clan Clan = new(); public IFaction MapFaction => Clan;
        public Hero(string id, int gold) { StringId = Name = id; Gold = gold; all.Add(this); }
        public static Hero? FindFirst(Func<Hero, bool> test) => all.Find(h => test(h));
    }
    public interface IDataStore { bool IsSaving { get; } bool IsLoading { get; } void SyncData<T>(string key, ref T value); }
    public abstract class CampaignBehaviorBase { public abstract void RegisterEvents(); public abstract void SyncData(IDataStore store); }
    public struct CampaignTime { public double ToHours; public static CampaignTime Now; }
    public sealed class DailyEvent { private Action? action; public void AddNonSerializedListener(object owner, Action listener) { action += listener; } public void Fire() => action?.Invoke(); }
    public static class CampaignEvents { public static DailyEvent DailyTickEvent = new(); }
    public class Campaign
    {
        public static Campaign? Current;
        public BarterSystem.BarterManager BarterManager = new();
        public ConversationManager ConversationManager = new();
        public T? GetCampaignBehavior<T>() where T : class => null;
    }
    public class ConversationManager
    {
        public bool IsConversationInProgress;
        public int Continuations;
        public void ContinueConversation() => Continuations++;
    }
}
namespace TaleWorlds.CampaignSystem.Party
{
    public class PartyBase { public TaleWorlds.Core.ItemRoster ItemRoster = new(); }
    public class MobileParty { public static MobileParty MainParty = new(); public PartyBase Party = new(); public TaleWorlds.Core.ItemRoster ItemRoster => Party.ItemRoster; }
}
namespace TaleWorlds.CampaignSystem.Actions
{
    public static class TransferPrisonerAction
    {
        public static void Apply(CharacterObject character, Party.PartyBase from, Party.PartyBase to)
        { character.HeroObject.PartyBelongedToAsPrisoner = to; }
    }
    public static class GiveGoldAction
    {
        public static void ApplyBetweenCharacters(Hero from, Hero to, int amount, bool disableNotification = false)
        { from.Gold -= amount; to.Gold += amount; }
    }
}
namespace TaleWorlds.CampaignSystem.BarterSystem.Barterables
{
    public class TransferPrisonerBarterable : Barterable
    {
        public TransferPrisonerBarterable(Hero prisoner, Hero owner, Party.PartyBase from, Hero other, Party.PartyBase to) : base(owner, from) { }
        public override string StringID => "prisoner";
        public override TaleWorlds.Localization.TextObject Name => new();
        public override int GetUnitValueForFaction(IFaction faction) => 0;
        public override TaleWorlds.Core.ImageIdentifiers.ImageIdentifier GetVisualIdentifier() => new();
        public override void Apply() { }
    }
    public abstract class Barterable
    {
        protected Barterable(Hero owner, Party.PartyBase party) { OriginalOwner = owner; OriginalParty = party; }
        public Hero OriginalOwner; public Party.PartyBase OriginalParty;
        public object? Group; public bool IsContextDependent;
        public bool IsOffered; public void SetIsOffered(bool value) => IsOffered = value;
        public int CurrentAmount;
        public abstract string StringID { get; }
        public abstract TaleWorlds.Localization.TextObject Name { get; }
        public virtual int MaxAmount => 1;
        public abstract int GetUnitValueForFaction(IFaction faction);
        public abstract void Apply();
        public virtual void CheckBarterLink(Barterable linked) { }
        public abstract TaleWorlds.Core.ImageIdentifiers.ImageIdentifier GetVisualIdentifier();
    }
}
namespace TaleWorlds.Core.ImageIdentifiers { public class ImageIdentifier { } }
namespace TaleWorlds.Localization { public class TextObject { } }
namespace TaleWorlds.Core
{
    public static class MBRandom { public static float RandomFloat; }
}
namespace TaleWorlds.Library
{
    public class InformationMessage { public InformationMessage(string text, int color = 0) { } }
    public static class InformationManager { public static void DisplayMessage(InformationMessage msg) { } }
}
namespace TaleWorlds.Library { public static class Colors { public const int Red = 0; } }
namespace GreyWardenPolicePurity
{
    internal static class GwpRuntimeState
    {
        internal static PlayerState Player = new();
        internal class PlayerState { public int Reputation; public void ChangeReputation(int delta) { Reputation += delta; } }
    }
    internal static class GwpText
    {
        public static string Get(string text, params object[] args) => text;
        public static TaleWorlds.Localization.TextObject Create(string text, params object[] args) => new();
    }
    internal static class GwpAiDiagnostics { public static void WriteFieldArrest(string kind, string text) { } }
    internal class HeroCrimeStats { public int NegativeStanding; }
    internal static class CrimePool
    {
        public const string PlayerCrimeId = "player";
        public static HeroCrimeStats? GetHistory(TaleWorlds.CampaignSystem.Hero hero) => GetOrCreateHistory(hero);
        private static readonly Dictionary<string, HeroCrimeStats> stats = new();
        public static HeroCrimeStats GetOrCreateHistory(TaleWorlds.CampaignSystem.Hero hero)
        { if (!stats.TryGetValue(hero.StringId, out var value)) stats[hero.StringId] = value = new(); return value; }
    }
    public class CrimeRecord
    {
        public int IncidentCount; public int AccruedBaseFine;
        public GwpCrimeCategory CrimeCategory;
        public TaleWorlds.CampaignSystem.Hero? OffenderHero;
    }
}

namespace TaleWorlds.Core
{
 public class ItemObject { public string StringId = ""; public bool IsFood; public int Value; public TaleWorlds.Localization.TextObject Name = new(); }
 public class ItemModifier { public string StringId = ""; }
 public struct EquipmentElement { public ItemObject Item; public ItemModifier? ItemModifier; public int ItemValue => Item.Value; public EquipmentElement(ItemObject item, ItemModifier? modifier = null) { Item = item; ItemModifier = modifier; } }
 public struct ItemRosterElement { public EquipmentElement EquipmentElement; public int Amount; public ItemRosterElement(EquipmentElement element, int amount) { EquipmentElement=element; Amount=amount; } }
 public class ItemRoster : List<ItemRosterElement>
 {
  public int TotalFood => this.Where(e => e.EquipmentElement.Item.IsFood).Sum(e => e.Amount);
  public void AddToCounts(EquipmentElement e,int amount)
  { int i=FindIndex(x=>x.EquipmentElement.Equals(e)); if(i<0) Add(new ItemRosterElement{EquipmentElement=e,Amount=amount}); else { var x=this[i];x.Amount+=amount;this[i]=x; } }
 }
}

namespace HarmonyLib { public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } } }
namespace TaleWorlds.CampaignSystem.CampaignBehaviors { public class FoodConsumptionBehavior { public void DailyTickParty(Party.MobileParty party) { } } }
namespace TaleWorlds.ObjectSystem
{
 public class MBObjectManager
 {
  public static MBObjectManager Instance = new();
  public Dictionary<string, object> Objects = new();
  public T? GetObject<T>(string id) where T : class => Objects.TryGetValue(id, out var value) ? value as T : null;
 }
}
namespace GreyWardenPolicePurity
{
 internal class GwpWardenDispatchBehavior
 {
  internal static GwpWardenDispatchBehavior Instance = new();
  internal string CargoState = "";
  internal string CargoFor(TaleWorlds.CampaignSystem.Party.MobileParty party) => CargoState;
 }
}
namespace TaleWorlds.CampaignSystem.BarterSystem.Barterables
{
 public class ItemBarterable : Barterable
 {
  public TaleWorlds.Core.ItemRosterElement ItemRosterElement; private Party.PartyBase other;
  public ItemBarterable(Hero owner,Hero receiver,Party.PartyBase from,Party.PartyBase to,TaleWorlds.Core.ItemRosterElement item,int value):base(owner,from) {ItemRosterElement=item;other=to;}
  public override string StringID=>"item";
  public override TaleWorlds.Localization.TextObject Name=>new();
  public override int MaxAmount=>ItemRosterElement.Amount;
  public override int GetUnitValueForFaction(IFaction faction)=>0;
  public override TaleWorlds.Core.ImageIdentifiers.ImageIdentifier GetVisualIdentifier()=>new();
  public override void Apply(){OriginalParty.ItemRoster.AddToCounts(ItemRosterElement.EquipmentElement,-CurrentAmount);other.ItemRoster.AddToCounts(ItemRosterElement.EquipmentElement,CurrentAmount);}
 }
}

namespace TaleWorlds.CampaignSystem.BarterSystem
{
 public class BarterManager
 {
  public int Closes;
  public void Close() => Closes++;
  public void IsOfferAcceptable() { }
  public void ApplyAndFinalizePlayerBarter() { }
  public void CancelAndFinalizePlayerBarter() { }
 }
 public class GoldBarterGroup {} public class ItemBarterGroup {} public class OtherBarterGroup {} public class PrisonerBarterGroup {}
 public class BarterData
 {
  private readonly List<Barterables.Barterable> entries = new();
  public List<Barterables.Barterable> GetBarterables() => entries;
  public void AddBarterable<T>(Barterables.Barterable item, bool contextual = false) where T : new()
  { item.Group = new T(); item.IsContextDependent = contextual; entries.Add(item); }
 }
}
namespace TaleWorlds.CampaignSystem.ViewModelCollection.Barter
{
 public class BarterItemVM
 {
  public BarterSystem.Barterables.Barterable Barterable = null!;
  public int CurrentOfferedAmount;
 }
 public class BarterVM
 {
  public List<BarterItemVM> LeftOfferList = new(), RightOfferList = new();
  public bool IsOfferDisabled;
  public string OfferLbl = "";
  public void ExecuteOffer() { }
  public void ExecuteCancel() { }
 }
}
namespace TaleWorlds.CampaignSystem.BarterSystem.Barterables
{
 public class GoldBarterable : Barterable
 {
  public GoldBarterable(Hero owner,Hero other,Party.PartyBase from,Party.PartyBase to,int val):base(owner,from) {}
  public override string StringID => "gold";
  public override TaleWorlds.Localization.TextObject Name => new();
  public override int GetUnitValueForFaction(IFaction faction) => 0;
  public override void Apply() {}
  public override TaleWorlds.Core.ImageIdentifiers.ImageIdentifier GetVisualIdentifier() => new();
 }
}
