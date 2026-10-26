using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Localization;

internal static class Program
{
    // Only managed methods run. No game, scene, native agent or campaign tick is started.
    private static int Main(string[] args)
    {
        string game = args[0], module = args[1];
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string dir in new[] { Path.GetDirectoryName(module), Path.Combine(game, "bin/Win64_Shipping_Client"),
                Path.Combine(game, "Modules/SandBox/bin/Win64_Shipping_Client"),
                Path.Combine(game, "Modules/Native/bin/Win64_Shipping_Client") })
            {
                string path = Path.Combine(dir, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        return Checks.Run(module);
    }
}

internal static class Checks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static int _count, _skill, _playerTrait, _opponentTrait;
    private static float _difficultyBonus, _roll;
    private static bool _meaningfulFavors;
    private static bool _finiteArmourDenominator;
    private static readonly HashSet<ItemObject> TestItems = new HashSet<ItemObject>();
    private static Hero _hero;
    private static CharacterObject _player;
    private static PerkObject _perk;
    private static Type _behaviorType;
    private static CampaignBehaviorBase _behavior;
    private static readonly DefaultPersuasionModel Model = new DefaultPersuasionModel();
    private static readonly SkillObject Skill = Empty<SkillObject>();
    private static readonly TraitObject Trait = Empty<TraitObject>();

    private static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static void Field(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    private static void Property(object o, string name, object value) => o.GetType().GetProperty(name, Flags).SetValue(o, value);
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    private static void Check(bool value, string message) { _count++; if (!value) throw new Exception(message); }
    private static void Near(float a, float b, string why) => Check(Math.Abs(a - b) < 0.00001f, why + ": " + a + " vs " + b);
    private static void Patch(Harmony h, MethodBase method, string prefix) =>
        h.Patch(method, new HarmonyMethod(typeof(Checks).GetMethod(prefix, Flags)));

    private static bool MainHero(ref Hero __result) { __result = _hero; return false; }
    private static bool PlayerCharacter(ref CharacterObject __result) { __result = _player; return false; }
    private static bool SkillValue(ref int __result) { __result = _skill; return false; }
    private static bool HeroTrait(ref int __result) { __result = _playerTrait; return false; }
    private static bool CharacterTrait(CharacterObject __instance, ref int __result)
    { __result = __instance == _player ? _playerTrait : _opponentTrait; return false; }
    private static bool PerkValue(PerkObject perk, ref bool __result)
    { __result = _meaningfulFavors && perk == _perk; return false; }
    private static bool DifficultyBonus(ref float __result) { __result = _difficultyBonus; return false; }
    private static bool Text(ref TextObject __result) { __result = new TextObject("offline difficulty"); return false; }
    private static bool Roll(ref float __result) { __result = _roll; return false; }
    private static bool RelevantSkill(ItemObject __instance, ref SkillObject __result)
    { if (!TestItems.Contains(__instance)) return true; __result = Skill; return false; }
    private static bool ArmourWeight(ref float __result)
    { if (!_finiteArmourDenominator) return true; __result = 1f; return false; }
    private static bool NoScene() => false;
    private static bool Inactive(ref bool __result) { __result = false; return false; }

    private static float Difficulty(int standing, float ours = 100f, float theirs = 100f) =>
        (float)_behaviorType.GetMethod("SearchDifficulty", Flags).Invoke(null, new object[] { standing, ours, theirs });

    private static PersuasionOptionArgs Option(bool critical = false, bool reverse = false)
    {
        var strength = Model.GetArgumentStrengthBasedOnTargetTraits(Empty<CharacterObject>(),
            new[] { Tuple.Create(Trait, reverse ? -1 : 1) });
        return new PersuasionOptionArgs(Skill, Trait, reverse ? TraitEffect.Negative : TraitEffect.Positive,
            strength, critical, new TextObject("offline"), null, false, true);
    }

    private static object Start(string field, PersuasionOptionArgs option, float difficulty)
    {
        object round = _behaviorType.GetField(field, Flags).GetValue(_behavior);
        var task = new PersuasionTask(0);
        task.AddOptionToTask(option);
        Call(round, "Begin", new[] { task }, difficulty);
        return round;
    }

    private static float[] Chances(PersuasionOptionArgs option, float difficulty = 0.6f)
    {
        Model.GetChances(option, out float s, out float cs, out float cf, out float f, difficulty);
        return new[] { s, cs, cf, f };
    }

    private static float Win(float[] p) => p[0] + p[1];

    private static void CheckUiAndRoll(PersuasionOptionArgs option)
    {
        var persuasion = Empty<Persuasion>();
        Field(persuasion, "_difficultyMultiplier", 0.6f);
        typeof(ConversationManager).GetField("_persuasion", Flags).SetValue(null, persuasion);
        var conversation = Empty<ConversationManager>();
        var sentence = Empty<ConversationSentence>();
        Property(sentence, "PersuationOptionArgs", option);
        Field(conversation, "_sentences", new List<ConversationSentence> { sentence });
        conversation.GetPersuasionChances(new ConversationSentenceOption { SentenceNo = 0, HasPersuasion = true },
            out float s, out float cs, out float cf, out float f);
        float[] p = Chances(option), ui = { s, cs, cf, f };
        for (int i = 0; i < 4; i++) Near(ui[i], p[i], "installed UI queries the same probabilities");
        float edge = 0f;
        foreach (var bucket in new[] { (cs, PersuasionOptionResult.CriticalSuccess), (s, PersuasionOptionResult.Success),
            (f, PersuasionOptionResult.Failure), (cf, PersuasionOptionResult.CriticalFailure) })
        {
            if (bucket.Item1 > 0f)
            {
                _roll = edge + bucket.Item1 / 2f;
                Check((PersuasionOptionResult)Call(persuasion, "GetResult", option) == bucket.Item2,
                    "installed dice result matches the displayed probability interval");
            }
            edge += bucket.Item1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int Run(string module)
    {
        Assembly assembly = Assembly.LoadFrom(module);
        _behaviorType = assembly.GetType("GreyWardenPolicePurity.GwpCitySearchBehavior", true);
        _behavior = (CampaignBehaviorBase)Activator.CreateInstance(_behaviorType, true);
        var campaign = Empty<Campaign>();
        typeof(Campaign).GetProperty("Current", Flags).SetValue(null, campaign);
        var manager = Empty<CampaignBehaviorManager>();
        Field(manager, "_campaignBehaviors", new List<CampaignBehaviorBase> { _behavior });
        campaign.AddCampaignBehaviorManager(manager);
        var models = Empty<GameModels>();
        Property(models, "PersuasionModel", Model);
        Property(models, "CharacterDevelopmentModel", Empty<DefaultCharacterDevelopmentModel>());
        Property(models, "DifficultyModel", new DefaultDifficultyModel());
        Field(campaign, "_gameModels", models);
        var perks = Empty<DefaultPerks>();
        _perk = Empty<PerkObject>();
        Property(_perk, "PrimaryBonus", 0.1f);
        Field(perks, "_charmMeaningfulFavors", _perk);
        Property(campaign, "DefaultPerks", perks);
        _hero = Empty<Hero>();
        _player = Empty<CharacterObject>();
        var environment = new Harmony("gwp.offline.persuasion.environment");
        Patch(environment, AccessTools.PropertyGetter(typeof(Hero), "MainHero"), nameof(MainHero));
        Patch(environment, AccessTools.PropertyGetter(typeof(CharacterObject), "PlayerCharacter"), nameof(PlayerCharacter));
        Patch(environment, AccessTools.Method(typeof(Hero), "GetSkillValue"), nameof(SkillValue));
        Patch(environment, AccessTools.Method(typeof(CharacterObject), "GetSkillValue"), nameof(SkillValue));
        Patch(environment, AccessTools.Method(typeof(Hero), "GetTraitLevel"), nameof(HeroTrait));
        Patch(environment, AccessTools.Method(typeof(CharacterObject), "GetTraitLevel"), nameof(CharacterTrait));
        Patch(environment, AccessTools.Method(typeof(Hero), "GetPerkValue"), nameof(PerkValue));
        Patch(environment, AccessTools.Method(typeof(DefaultDifficultyModel), "GetPersuasionBonusChance"), nameof(DifficultyBonus));
        Patch(environment, AccessTools.Method(typeof(GameTexts), "FindText"), nameof(Text));
        Patch(environment, AccessTools.PropertyGetter(typeof(MBRandom), "RandomFloat"), nameof(Roll));
        Patch(environment, AccessTools.PropertyGetter(typeof(ItemObject), "RelevantSkill"), nameof(RelevantSkill));
        Patch(environment, AccessTools.Method(typeof(Equipment), "GetTotalWeightOfArmor"), nameof(ArmourWeight));
        Patch(environment, AccessTools.Method(typeof(ConversationManager), "StartPersuasion"), nameof(NoScene));
        Patch(environment, AccessTools.Method(typeof(ConversationManager), "GetPersuasionIsActive"), nameof(Inactive));

        // Installed v1.4.8 divides zero armour by zero weight on a bare character.
        // Run that method unpatched as the negative control before testing the production adapter.
        var bare = new Equipment(Equipment.EquipmentType.Battle);
        var bareCharacter = Empty<CharacterObject>();
        bareCharacter.GetSimulationAttackPower(out float bareAttack, out float bareDefence, bare);
        Check(bareAttack == 0f && float.IsNaN(bareDefence), "installed native zero-armour division reproduces NaN");
        object[] bareArgs = { bareCharacter, bare, 0f, 0f };
        assembly.GetType("GreyWardenPolicePurity.GwpCitySearchMissionController", true)
            .GetMethod("SimulationPower", Flags).Invoke(null, bareArgs);
        Near((float)bareArgs[3], 4000f, "bare character retains native base defence without the zero-over-zero term");
        Check(!float.IsNaN(Difficulty(20, 100f, (float)Math.Sqrt(100f * (float)bareArgs[3]))),
            "zero-armour input cannot poison search difficulty");

        // Compare the actual native loops with only its known 0/0 denominator removed,
        // so shields/riding remain an independent reference for the production correction.
        foreach (bool shield in new[] { false, true })
        foreach (bool mounted in new[] { false, true })
        foreach (int skill in new[] { 0, 100, 300 })
        {
            var equipment = new Equipment(Equipment.EquipmentType.Battle);
            var sword = Empty<ItemObject>(); Property(sword, "Effectiveness", 90f); TestItems.Add(sword);
            sword.AddWeapon(new WeaponComponentData(sword, WeaponClass.OneHandedSword), null);
            equipment[EquipmentIndex.Weapon0] = new EquipmentElement(sword);
            if (shield)
            {
                var item = Empty<ItemObject>(); Property(item, "Effectiveness", 200f); TestItems.Add(item);
                item.AddWeapon(new WeaponComponentData(item, WeaponClass.LargeShield), null);
                equipment[EquipmentIndex.Weapon1] = new EquipmentElement(item);
            }
            if (mounted)
            {
                var horse = Empty<ItemObject>(); Property(horse, "Effectiveness", 75f); TestItems.Add(horse);
                var harness = Empty<ItemObject>(); Property(harness, "Effectiveness", 25f);
                equipment[EquipmentIndex.Horse] = new EquipmentElement(horse);
                equipment[EquipmentIndex.HorseHarness] = new EquipmentElement(harness);
            }
            _skill = skill;
            _finiteArmourDenominator = false;
            bareCharacter.GetSimulationAttackPower(out float nativeAttack, out float nativeDefence, equipment);
            Check(float.IsNaN(nativeDefence), "armed zero-armour character reproduces native NaN");
            _finiteArmourDenominator = true;
            bareCharacter.GetSimulationAttackPower(out float referenceAttack, out float referenceDefence, equipment);
            _finiteArmourDenominator = false;
            object[] corrected = { bareCharacter, equipment, 3f, 5f };
            assembly.GetType("GreyWardenPolicePurity.GwpCitySearchMissionController", true)
                .GetMethod("SimulationPower", Flags).Invoke(null, corrected);
            Near((float)corrected[2], referenceAttack + 3f, "native attack and accumulator are preserved");
            Near((float)corrected[3], referenceDefence + 5f, "base, shield, mount and skill defence match native without 0/0");
        }

        // No replacement of GetChances/GetDefaultSuccessChance/GetBonusSuccessChance:
        // installed game arithmetic, traits, perk scaling and the real production patch all execute.
        _skill = 100;
        var external = Option();
        float[] original = Chances(external);
        var integration = new Harmony("gwp.offline.persuasion.integration");
        integration.CreateClassProcessor(assembly.GetType("GreyWardenPolicePurity.GwpNegotiationChancePatch", true)).Patch();
        var untouched = Chances(external);
        for (int i = 0; i < 4; i++) Near(original[i], untouched[i], "unowned native dialogue is unchanged");
        Near(Difficulty(20), 75f, "standing 20 is the neutral reference");
        Near(Difficulty(19) - Difficulty(20), 0.5f, "no standing tier jump at the baseline");
        Check(Difficulty(60) < Difficulty(20) && Difficulty(0) > Difficulty(20), "reputation affects native difficulty");
        Check(Difficulty(20, 200, 100) < Difficulty(20) && Difficulty(20, 100, 200) > Difficulty(20), "both force directions affect difficulty");
        foreach (float ours in new[] { 0f, 1f, 100f, 1000000f })
        foreach (float theirs in new[] { 0f, 1f, 100f, 1000000f })
        foreach (int standing in new[] { int.MinValue, -50, 0, 20, 60, int.MaxValue })
        { float d = Difficulty(standing, ours, theirs); Check(d >= 30f && d <= 120f, "finite bounded difficulty including empty teams"); }

        var owned = Option();
        object gate = Start("_guardRound", owned, Difficulty(20));
        float[] baseline = Chances(owned);
        CheckUiAndRoll(owned);
        Check(Win(baseline) < Win(original) - 0.3f, "negative control: old nearly-one default input reproduces easy persuasion");
        // Old working-tree code additionally multiplied by 0.6 + skill/250 (1 at skill100).
        Near(Win(original), Win(original) * (0.6f + 100f / 250f), "old skill multiplier leaves the high baseline intact");
        _skill = 0; float low = Win(Chances(owned));
        _skill = 100; float middle = Win(Chances(owned));
        _skill = 200; float high = Win(Chances(owned));
        Check(low < middle && middle < high && high - low > 0.25f, "installed native skill calculation changes continuously");
        _skill = 101; Check(Win(Chances(owned)) > middle, "one skill point matters below the epic-perk threshold");
        Call(gate, "End");
        _skill = 100;
        for (int i = 0; i < 4; i++) Near(original[i], Chances(owned)[i], "ending clears ownership and native behavior resumes");
        foreach (string field in new[] { "_guardRound", "_hallRound", "_choiceRound" })
        {
            var option = Option(); var round = Start(field, option, Difficulty(20));
            Near(Win(Chances(option)), middle, "all three rounds share the same native calculation");
            Near(Win(Chances(option, 0.1f)), middle, "UI or roll input uses this round's captured difficulty");
            Call(round, "End");
        }
        _opponentTrait = -2;
        var resistant = Option(); var resistantRound = Start("_guardRound", resistant, Difficulty(20));
        Check(Win(Chances(resistant)) < middle, "opponent trait conflict makes the argument harder");
        Call(resistantRound, "End"); _opponentTrait = 0;
        var a = Option(); var b = Option();
        var ra = Start("_guardRound", a, Difficulty(60, 2, 1));
        var rb = Start("_hallRound", b, Difficulty(0, 1, 2));
        Check(Win(Chances(a)) > Win(Chances(b)), "each round keeps its own snapshot without leaking to another round");
        Call(ra, "End"); Call(rb, "End");
        foreach (int standing in new[] { 0, 20, 60 })
        foreach (float ratio in new[] { 0.5f, 1f, 2f })
        foreach (int skill in new[] { 0, 100, 200, 300 })
        foreach (int playerTrait in new[] { -2, 0, 2 })
        foreach (int targetTrait in new[] { -2, 0, 2 })
        foreach (bool critical in new[] { false, true })
        foreach (bool reverse in new[] { false, true })
        {
            _skill = skill; _playerTrait = playerTrait; _opponentTrait = targetTrait;
            var option = Option(critical, reverse); var round = Start("_guardRound", option, Difficulty(standing, ratio, 1f));
            float[] p = Chances(option);
            Check(p.All(x => x >= 0f && x <= 1f) && Math.Abs(p.Sum() - 1f) < 0.00001f, "probability simplex");
            Check(p[2] >= (critical ? 0.1f : 0.05f) - 0.00001f, "critical failure floor");
            Check(critical ? p[0] == 0f : p[1] == 0f, "native critical-success selection preserved");
            Call(round, "End");
        }
        _skill = 100; _playerTrait = 0; _opponentTrait = 0;
        var criticalOption = Option(true); var criticalRound = Start("_hallRound", criticalOption, Difficulty(20));
        CheckUiAndRoll(criticalOption);
        float withoutPerk = Win(Chances(criticalOption)); _meaningfulFavors = true;
        Check(Win(Chances(criticalOption)) > withoutPerk, "native Meaningful Favors is retained");
        _meaningfulFavors = false; _difficultyBonus = 0.1f;
        Check(Win(Chances(criticalOption)) > withoutPerk, "native game difficulty bonus is retained");
        _difficultyBonus = 0f; Call(criticalRound, "End");
        foreach (int standing in new[] { 0, 20, 60 })
        foreach (float ratio in new[] { 0.5f, 1f, 2f })
        {
            var rates = new List<string>();
            foreach (int skill in new[] { 0, 100, 200 })
            {
                _skill = skill; var option = Option(); var round = Start("_guardRound", option, Difficulty(standing, ratio, 1f));
                rates.Add(Win(Chances(option)).ToString("P1")); Call(round, "End");
            }
            Console.WriteLine("ODDS standing=" + standing + " power=" + ratio + " skill0/100/200=" + string.Join("/", rates));
        }
        Console.WriteLine("CITY_SEARCH_NATIVE_PERSUASION_ASSERTIONS=" + _count);
        return 0;
    }
}
