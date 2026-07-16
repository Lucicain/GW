using System;
using System.Globalization;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    internal static class GwpText
    {
        public static string Get(string template, params object[] variables)
        {
            var text = new TextObject(template);
            for (int i = 0; i + 1 < variables.Length; i += 2)
                Set(text, variables[i]?.ToString() ?? string.Empty, variables[i + 1]);
            return text.ToString();
        }

        public static string Format(object value, string format) =>
            value is IFormattable formattable
                ? formattable.ToString(format, CultureInfo.InvariantCulture)
                : value?.ToString() ?? string.Empty;


        public static string CrimeType(string value)
        {
            switch (value ?? string.Empty)
            {
                case "攻击村民": case "Attack villager": case "Attack villagers":
                    return Get("{=gwp_crime_attack_villagers}Attack upon villagers");
                case "攻击商队": case "Attack caravan":
                    return Get("{=gwp_crime_attack_caravan}Attack upon a caravan");
                case "劫掠村庄": case "Raid village": case "Raid the village":
                    return Get("{=gwp_crime_raid_village}Raid upon a village");
                case "强迫募兵": case "Forced Recruitment": case "Force recruits":
                    return Get("{=gwp_crime_forced_recruitment}Forced recruitment");
                case "强征给养": case "Forced Requisition of Supplies": case "Force supplies":
                    return Get("{=gwp_crime_forced_supplies}Forced requisition of supplies");
                case "劫掠商队": case "Raid the caravan": case "Raid Caravan":
                    return Get("{=gwp_crime_raid_caravan}Raid upon a caravan");
                case "杀害村民": case "Kill the villagers":
                    return Get("{=gwp_crime_kill_villagers}Slaying of villagers");
                case "参与劫掠村庄": case "Participate in raiding village":
                    return Get("{=gwp_crime_join_village_raid}Participation in a village raid");
                case "妨碍执法": case "Obstructed enforcement":
                    return Get("{=gwp_crime_obstruct_law}Obstruction of the law");
                case "帮助罪犯对抗灰袍守卫": case "Helped criminals against the Grey Wardens":
                    return Get("{=gwp_crime_aid_criminals}Aiding malefactors against the Grey Wardens");
                default:
                    return value ?? string.Empty;
            }
        }
        private static void Set(TextObject text, string name, object value)
        {
            switch (value)
            {
                case TextObject textObject: text.SetTextVariable(name, textObject); break;
                case int integer: text.SetTextVariable(name, integer); break;
                case float number: text.SetTextVariable(name, number, 2); break;
                default: text.SetTextVariable(name, value?.ToString() ?? string.Empty); break;
            }
        }
    }
}

