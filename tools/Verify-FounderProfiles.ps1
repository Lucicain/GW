<# Run native conversation tags, trait clamping and the production patch without starting the game. #>
[CmdletBinding()]
param(
    [string]$ModuleDll = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden\bin\Win64_Shipping_Client\GreyWardenPolicePurity.dll',
    [string]$GameFolder = 'D:\steam\steamapps\common\Mount & Blade II Bannerlord'
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Run in Windows PowerShell 5.1 (net472).' }
$references = @(Get-ChildItem (Join-Path $GameFolder 'bin\Win64_Shipping_Client') -Filter 'TaleWorlds.*.dll' |
    Where-Object { $_.Name -ne 'TaleWorlds.Native.dll' } | ForEach-Object { $_.FullName })
foreach ($path in $references) { [void][Reflection.Assembly]::LoadFrom($path) }
$harmony = Join-Path (Split-Path $ModuleDll) '0Harmony.dll'
[void][Reflection.Assembly]::LoadFrom($harmony)
[void][Reflection.Assembly]::LoadFrom($ModuleDll)
$framework = [Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$references += @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll') | ForEach-Object { Join-Path $framework $_ }
$references += $harmony
$references += Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll'
Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation.Tags;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

public static class FounderProfileRegression
{
    static int checks;
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static T Empty<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    static void Set(object owner, string field, object value) { owner.GetType().GetField(field, Hidden).SetValue(owner, value); }
    static void Check(bool result, string message) { checks++; if (!result) throw new Exception(message); }

    public static void Run(string moduleXml, string nativeVoices)
    {
        var campaign = Empty<Campaign>();
        typeof(Campaign).GetField("<Current>k__BackingField", Hidden).SetValue(null, campaign);
        var defaults = Empty<DefaultTraits>();
        typeof(Campaign).GetField("<DefaultTraits>k__BackingField", Hidden).SetValue(campaign, defaults);
        var personas = new Dictionary<string, TraitObject>();
        foreach (string name in new[] { "curt", "earnest", "ironic", "softspoken" })
        {
            var trait = Empty<TraitObject>(); trait.StringId = name; personas[name] = trait;
            Set(defaults, "_traitPersona" + char.ToUpper(name[0]) + name.Substring(1), trait);
        }
        var bounded = Empty<TraitObject>(); bounded.StringId = "Mercy";
        Set(bounded, "<MinValue>k__BackingField", -2); Set(bounded, "<MaxValue>k__BackingField", 2);
        var subject = Empty<Hero>(); Set(subject, "_heroTraits", new PropertyOwner<TraitObject>());
        subject.SetTraitLevel(bounded, 12); Check(subject.GetTraitLevel(bounded) == 2, "old 12 clamps to 2");
        subject.SetTraitLevel(bounded, 9); Check(subject.GetTraitLevel(bounded) == 2, "old 9 clamps to 2");
        subject.SetTraitLevel(bounded, 1); Check(subject.GetTraitLevel(bounded) == 1, "new distinctions survive clamping");
        var culture = Empty<CultureObject>(); culture.StringId = "empire";
        var clan = Empty<Clan>(); clan.IsNoble = true;
        var doc = new XmlDocument(); doc.Load(moduleXml);
        var voices = new XmlDocument(); voices.Load(nativeVoices);
        var characters = new List<CharacterObject>();
        var personality = new HashSet<string>(); var combat = new HashSet<string>();
        foreach (XmlElement node in doc.SelectNodes("/NPCCharacters/NPCCharacter"))
        {
            if (!node.GetAttribute("id").StartsWith("gw_leader_")) continue;
            var character = Empty<CharacterObject>(); character.StringId = node.GetAttribute("id");
            var hero = Empty<Hero>(); hero.Culture = culture;
            Set(hero, "_clan", clan); Set(hero, "_characterObject", character);
            Set(character, "_heroObject", hero); Set(character, "_persona", personas[node.GetAttribute("voice")]);
            characters.Add(character);
            Check(ConversationTagHelper.UsesHighRegister(character), "native noble uses high register");
            string vector = "";
            foreach (string name in new[] { "Mercy", "Valor", "Honor", "Generosity", "Calculating" })
            {
                int value = int.Parse(((XmlElement)node.SelectSingleNode("Traits/Trait[@id='" + name + "']")).GetAttribute("value"));
                Check(value >= 0 && value <= 2, "positive personality within native bounds");
                vector += value + ",";
            }
            Check(personality.Add(vector), "distinct personality vectors");
            string key = ((XmlElement)node.SelectSingleNode("face/BodyProperties")).GetAttribute("key");
            ulong last = Convert.ToUInt64(key.Substring(112), 16);
            Check((last & 63) < 5, "native adult female voice exists");
            Check(combat.Add((last & 63) + ":" + ((last >> 25) & 63)), "distinct combat voice and pitch");
        }
        Check(characters.Count == 6, "six founders");
        var patch = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GreyWardenPolicePurity.GwpFounderVoiceRegisterPatch")).First(t => t != null);
        new Harmony("gwp.offline.founder-profiles").CreateClassProcessor(patch).Patch();
        var groups = new HashSet<string>();
        foreach (var character in characters)
        {
            bool high = ConversationTagHelper.UsesHighRegister(character);
            Check(high != ConversationTagHelper.UsesLowRegister(character), "exactly one conversation register");
            string group = new DefaultVoiceOverModel().GetAccentClass(culture, high) + "_female_" + character.GetPersona().StringId;
            Check(groups.Add(group), "six distinct native dialogue speakers");
            Check(voices.SelectNodes("//Voice[contains(@path,'/" + group + "_')]").Count > 0, "recorded native actor paths exist");
            Check(character.HeroObject.Clan.IsNoble && character.Culture == culture, "nobility and culture preserved");
        }
        var anotherClan = Empty<Clan>(); anotherClan.IsNoble = true;
        Set(characters[4].HeroObject, "_clan", anotherClan);
        Check(ConversationTagHelper.UsesLowRegister(characters[4]), "voice follows founder after marriage");
        characters[4].StringId = "gw_leader_6";
        Check(ConversationTagHelper.UsesHighRegister(characters[4]), "successors are not overridden");
        characters[5].StringId = "native_lord";
        Check(ConversationTagHelper.UsesHighRegister(characters[5]), "native noble unaffected");
        Console.WriteLine("FOUNDER_PROFILE_ASSERTIONS=" + checks + "; PASS");
    }
}
'@
$moduleXml = Join-Path $PSScriptRoot '..\GreyWardenPolicePurity\_Module\ModuleData\spspecialcharacters.xml'
$nativeVoices = Join-Path $GameFolder 'Modules\SandBox\ModuleData\Languages\VoicedLines\sandbox_voiced_lines_en.xml'
[FounderProfileRegression]::Run($moduleXml, $nativeVoices)
