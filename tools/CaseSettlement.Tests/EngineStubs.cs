// Minimal in-memory actors for production ledger/payment tests. These stubs do
// not exercise native barter UI, campaign event order, combat or prisoner transfer.
using System;
using System.Collections.Generic;
namespace TaleWorlds.CampaignSystem
{
    public interface IFaction { }
    public class Clan : IFaction { }
    public class Hero
    {
        private static readonly List<Hero> all = new();
        public static Hero MainHero = null!;
        public string StringId; public string Name; public int Gold;
        public Clan Clan = new(); public IFaction MapFaction => Clan;
        public Hero(string id, int gold) { StringId = Name = id; Gold = gold; all.Add(this); }
        public static Hero? FindFirst(Func<Hero, bool> test) => all.Find(h => test(h));
    }
    public interface IDataStore { bool IsSaving { get; } bool IsLoading { get; } void SyncData<T>(string key, ref T value); }
    public abstract class CampaignBehaviorBase { public abstract void RegisterEvents(); public abstract void SyncData(IDataStore store); }
    public struct CampaignTime { public double ToHours; public static CampaignTime Now; }
    public sealed class DailyEvent { private Action? action; public void AddNonSerializedListener(object owner, Action listener) { action += listener; } public void Fire() => action?.Invoke(); }
    public static class CampaignEvents { public static DailyEvent DailyTickEvent = new(); }
    public class Campaign { public static Campaign? Current; public T? GetCampaignBehavior<T>() where T : class => null; }
}
namespace TaleWorlds.CampaignSystem.Party
{
    public class PartyBase { }
    public class MobileParty { public static MobileParty MainParty = new(); public PartyBase Party = new(); }
}
namespace TaleWorlds.CampaignSystem.Actions
{
    public static class GiveGoldAction
    {
        public static void ApplyBetweenCharacters(Hero from, Hero to, int amount, bool disableNotification = false)
        { from.Gold -= amount; to.Gold += amount; }
    }
}
namespace TaleWorlds.CampaignSystem.BarterSystem.Barterables
{
    public abstract class Barterable
    {
        protected Barterable(Hero owner, Party.PartyBase party) { }
        public int CurrentAmount;
        public abstract string StringID { get; }
        public abstract TaleWorlds.Localization.TextObject Name { get; }
        public abstract int MaxAmount { get; }
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
    public class InformationMessage { public InformationMessage(string text, int color) { } }
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
        public static TaleWorlds.Localization.TextObject Create(string text) => new();
    }
    internal static class GwpAiDiagnostics { public static void WriteFieldArrest(string kind, string text) { } }
    internal class HeroCrimeStats { public int NegativeStanding; }
    internal static class CrimePool
    {
        private static readonly Dictionary<string, HeroCrimeStats> stats = new();
        public static HeroCrimeStats GetOrCreateHistory(TaleWorlds.CampaignSystem.Hero hero)
        { if (!stats.TryGetValue(hero.StringId, out var value)) stats[hero.StringId] = value = new(); return value; }
    }
}
