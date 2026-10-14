using System;
using System.Collections.Generic;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    // Cosmetic randomness must never consume the gameplay RNG. Cache visible lines
    // for the encounter; advancing a spoken argument invalidates only its reply.
    internal sealed class GwpFieldDialogueVoice
    {
        private readonly Random _random = new Random();
        private readonly Dictionary<string, int> _last = new Dictionary<string, int>();
        private readonly Dictionary<string, TextObject> _visible = new Dictionary<string, TextObject>();
        internal void BeginEncounter() => _visible.Clear();
        internal void NextArgument() { foreach (var key in new List<string>(_visible.Keys)) if (key.StartsWith("success_")) _visible.Remove(key); }
        internal TextObject Question(TextObject original)
        {
            string key = original.GetID();
            if (_visible.TryGetValue(key, out var cached)) return cached;
            string[] alternatives;
            switch (key)
            {
                case "gwp_fa_task_authority": alternatives = new[] { GwpTextCatalog.gwp_fa_task_authority_variant_1, GwpTextCatalog.gwp_fa_task_authority_variant_2 }; break;
                case "gwp_fa_task_under_charge": alternatives = new[] { GwpTextCatalog.gwp_fa_task_under_charge_variant_1, GwpTextCatalog.gwp_fa_task_under_charge_variant_2 }; break;
                case "gwp_fa_task_price": alternatives = new[] { GwpTextCatalog.gwp_fa_task_price_variant_1, GwpTextCatalog.gwp_fa_task_price_variant_2 }; break;
                case "gwp_fa_task_tally": alternatives = new[] { GwpTextCatalog.gwp_fa_task_tally_variant_1, GwpTextCatalog.gwp_fa_task_tally_variant_2 }; break;
                case "gwp_fa_task_denial": alternatives = new[] { GwpTextCatalog.gwp_fa_task_denial_variant_1, GwpTextCatalog.gwp_fa_task_denial_variant_2 }; break;
                case "gwp_fa_task_war": alternatives = new[] { GwpTextCatalog.gwp_fa_task_war_variant_1, GwpTextCatalog.gwp_fa_task_war_variant_2 }; break;
                case "gwp_fa_task_consequence": alternatives = new[] { GwpTextCatalog.gwp_fa_task_consequence_variant_1, GwpTextCatalog.gwp_fa_task_consequence_variant_2 }; break;
                case "gwp_fa_task_record": alternatives = new[] { GwpTextCatalog.gwp_fa_task_record_variant_1, GwpTextCatalog.gwp_fa_task_record_variant_2 }; break;
                case "gwp_terms_final_objection": alternatives = new[] { GwpTextCatalog.gwp_terms_final_objection_variant_1, GwpTextCatalog.gwp_terms_final_objection_variant_2 }; break;
                case "gwp_fa_open_under_charge": alternatives = new[] { GwpTextCatalog.gwp_fa_open_under_charge_variant_1, GwpTextCatalog.gwp_fa_open_under_charge_variant_2 }; break;
                case "gwp_fa_open_respected_known": alternatives = new[] { GwpTextCatalog.gwp_fa_open_respected_known_variant_1, GwpTextCatalog.gwp_fa_open_respected_known_variant_2 }; break;
                case "gwp_fa_open_respected": alternatives = new[] { GwpTextCatalog.gwp_fa_open_respected_variant_1, GwpTextCatalog.gwp_fa_open_respected_variant_2 }; break;
                case "gwp_fa_open_known": alternatives = new[] { GwpTextCatalog.gwp_fa_open_known_variant_1, GwpTextCatalog.gwp_fa_open_known_variant_2 }; break;
                default: return original;
            }
            int count = alternatives.Length + 1;
            int index;
            if (_last.TryGetValue(key, out int previous))
            {
                index = _random.Next(count - 1);
                if (index >= previous) index++;
            }
            else index = _random.Next(count);
            _last[key] = index;
            return _visible[key] = index == 0 ? original : GwpText.Create(alternatives[index - 1]);
        }

        internal TextObject Alternate(string key, TextObject original)
        {
            if (_visible.TryGetValue(key, out var cached)) return cached;
            int choice = _last.TryGetValue(key, out int previous) ? 1 - previous : _random.Next(2);
            _last[key] = choice;
            return _visible[key] = choice == 0 ? original : Alternative(key);
        }
        private static TextObject Alternative(string key)
        {
            switch (key)
            {
                case "demand_RequestGrace": return GwpText.Create(GwpTextCatalog.gwp_voice_grace_alt);
                case "demand_PayInFull": return GwpText.Create(GwpTextCatalog.gwp_voice_demand_payinfull_alt);
                case "demand_PayToAvoidBattle": return GwpText.Create(GwpTextCatalog.gwp_voice_demand_paytoavoidbattle_alt);
                case "demand_PayForPeace": return GwpText.Create(GwpTextCatalog.gwp_voice_demand_payforpeace_alt);
                case "demand_PayGenerously": return GwpText.Create(GwpTextCatalog.gwp_voice_demand_paygenerously_alt);
                case "demand_SurrenderSelf": return GwpText.Create(GwpTextCatalog.gwp_voice_demand_surrenderself_alt);
                case "demand_BribeTheWarden": return GwpText.Create(GwpTextCatalog.gwp_voice_demand_bribethewarden_alt);
                case "demand_CunningHalf": return GwpText.Create(GwpTextCatalog.gwp_voice_demand_cunninghalf_alt);
                case "demand_RashMost": return GwpText.Create(GwpTextCatalog.gwp_voice_demand_rashmost_alt);
                case "demand_DemandDuel": return GwpText.Create(GwpTextCatalog.gwp_voice_demand_demandduel_alt);
                case "demand_DeedsOnly": return GwpText.Create(GwpTextCatalog.gwp_voice_demand_deedsonly_alt);
                case "demand_HaggleDown": return GwpText.Create(GwpTextCatalog.gwp_voice_demand_haggledown_alt);
                case "hear": return GwpText.Create(GwpTextCatalog.gwp_voice_hear_alt);
                case "refuse": return GwpText.Create(GwpTextCatalog.gwp_voice_refuse_alt);
                case "done1": return GwpText.Create(GwpTextCatalog.gwp_voice_done1_alt);
                case "lost1": return GwpText.Create(GwpTextCatalog.gwp_voice_lost1_alt);
                case "done2": return GwpText.Create(GwpTextCatalog.gwp_voice_done2_alt);
                case "lost2": return GwpText.Create(GwpTextCatalog.gwp_voice_lost2_alt);
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }
        internal TextObject Line(string key)
        {
            if (_visible.TryGetValue(key, out var text)) return text;
            string[] choices;
            switch (key)
            {
                case "open_Upright": choices = new[] { GwpTextCatalog.gwp_voice_open_upright_0, GwpTextCatalog.gwp_voice_open_upright_1 }; break;
                case "open_Cold": choices = new[] { GwpTextCatalog.gwp_voice_open_cold_0, GwpTextCatalog.gwp_voice_open_cold_1 }; break;
                case "open_Fierce": choices = new[] { GwpTextCatalog.gwp_voice_open_fierce_0, GwpTextCatalog.gwp_voice_open_fierce_1 }; break;
                case "open_Soft": choices = new[] { GwpTextCatalog.gwp_voice_open_soft_0, GwpTextCatalog.gwp_voice_open_soft_1 }; break;
                case "open_Tight": choices = new[] { GwpTextCatalog.gwp_voice_open_tight_0, GwpTextCatalog.gwp_voice_open_tight_1 }; break;
                case "open_Plain": choices = new[] { GwpTextCatalog.gwp_voice_open_plain_0, GwpTextCatalog.gwp_voice_open_plain_1 }; break;
                case "paid": choices = new[] { GwpTextCatalog.gwp_voice_paid_0, GwpTextCatalog.gwp_voice_paid_1 }; break;
                case "fight_Upright": choices = new[] { GwpTextCatalog.gwp_voice_fight_upright_0, GwpTextCatalog.gwp_voice_fight_upright_1 }; break;
                case "fight_Cold": choices = new[] { GwpTextCatalog.gwp_voice_fight_cold_0, GwpTextCatalog.gwp_voice_fight_cold_1 }; break;
                case "fight_Fierce": choices = new[] { GwpTextCatalog.gwp_voice_fight_fierce_0, GwpTextCatalog.gwp_voice_fight_fierce_1 }; break;
                case "fight_Soft": choices = new[] { GwpTextCatalog.gwp_voice_fight_soft_0, GwpTextCatalog.gwp_voice_fight_soft_1 }; break;
                case "fight_Tight": choices = new[] { GwpTextCatalog.gwp_voice_fight_tight_0, GwpTextCatalog.gwp_voice_fight_tight_1 }; break;
                case "fight_Plain": choices = new[] { GwpTextCatalog.gwp_voice_fight_plain_0, GwpTextCatalog.gwp_voice_fight_plain_1 }; break;
                case "betray_Upright": choices = new[] { GwpTextCatalog.gwp_voice_betray_upright_0, GwpTextCatalog.gwp_voice_betray_upright_1 }; break;
                case "betray_Cold": choices = new[] { GwpTextCatalog.gwp_voice_betray_cold_0, GwpTextCatalog.gwp_voice_betray_cold_1 }; break;
                case "betray_Fierce": choices = new[] { GwpTextCatalog.gwp_voice_betray_fierce_0, GwpTextCatalog.gwp_voice_betray_fierce_1 }; break;
                case "betray_Soft": choices = new[] { GwpTextCatalog.gwp_voice_betray_soft_0, GwpTextCatalog.gwp_voice_betray_soft_1 }; break;
                case "betray_Tight": choices = new[] { GwpTextCatalog.gwp_voice_betray_tight_0, GwpTextCatalog.gwp_voice_betray_tight_1 }; break;
                case "betray_Plain": choices = new[] { GwpTextCatalog.gwp_voice_betray_plain_0, GwpTextCatalog.gwp_voice_betray_plain_1 }; break;
                case "success_Upright": choices = new[] { GwpTextCatalog.gwp_voice_success_upright_0, GwpTextCatalog.gwp_voice_success_upright_1 }; break;
                case "success_Cold": choices = new[] { GwpTextCatalog.gwp_voice_success_cold_0, GwpTextCatalog.gwp_voice_success_cold_1 }; break;
                case "success_Fierce": choices = new[] { GwpTextCatalog.gwp_voice_success_fierce_0, GwpTextCatalog.gwp_voice_success_fierce_1 }; break;
                case "success_Soft": choices = new[] { GwpTextCatalog.gwp_voice_success_soft_0, GwpTextCatalog.gwp_voice_success_soft_1 }; break;
                case "success_Tight": choices = new[] { GwpTextCatalog.gwp_voice_success_tight_0, GwpTextCatalog.gwp_voice_success_tight_1 }; break;
                case "success_Plain": choices = new[] { GwpTextCatalog.gwp_voice_success_plain_0, GwpTextCatalog.gwp_voice_success_plain_1 }; break;

                // 认罚。原来每种性子只有一句写死的台词，同一个人说第二次就重样了。
                case "submit_Upright": choices = new[] { GwpTextCatalog.gwp_voice_submit_upright_0, GwpTextCatalog.gwp_voice_submit_upright_1, GwpTextCatalog.gwp_voice_submit_upright_2 }; break;
                case "submit_Cold": choices = new[] { GwpTextCatalog.gwp_voice_submit_cold_0, GwpTextCatalog.gwp_voice_submit_cold_1, GwpTextCatalog.gwp_voice_submit_cold_2 }; break;
                case "submit_Soft": choices = new[] { GwpTextCatalog.gwp_voice_submit_soft_0, GwpTextCatalog.gwp_voice_submit_soft_1, GwpTextCatalog.gwp_voice_submit_soft_2 }; break;
                case "submit_Tight": choices = new[] { GwpTextCatalog.gwp_voice_submit_tight_0, GwpTextCatalog.gwp_voice_submit_tight_1, GwpTextCatalog.gwp_voice_submit_tight_2 }; break;
                case "submit_Fierce": choices = new[] { GwpTextCatalog.gwp_voice_submit_fierce_0, GwpTextCatalog.gwp_voice_submit_fierce_1, GwpTextCatalog.gwp_voice_submit_fierce_2 }; break;
                case "submit_Plain": choices = new[] { GwpTextCatalog.gwp_voice_submit_plain_0, GwpTextCatalog.gwp_voice_submit_plain_1, GwpTextCatalog.gwp_voice_submit_plain_2 }; break;

                // 交不出钱。
                case "plead_Cold": choices = new[] { GwpTextCatalog.gwp_voice_plead_cold_0, GwpTextCatalog.gwp_voice_plead_cold_1, GwpTextCatalog.gwp_voice_plead_cold_2 }; break;
                case "plead_Tight": choices = new[] { GwpTextCatalog.gwp_voice_plead_tight_0, GwpTextCatalog.gwp_voice_plead_tight_1, GwpTextCatalog.gwp_voice_plead_tight_2 }; break;
                case "plead_Upright": choices = new[] { GwpTextCatalog.gwp_voice_plead_upright_0, GwpTextCatalog.gwp_voice_plead_upright_1 }; break;
                case "plead_Soft": choices = new[] { GwpTextCatalog.gwp_voice_plead_soft_0, GwpTextCatalog.gwp_voice_plead_soft_1 }; break;
                case "plead_Fierce": choices = new[] { GwpTextCatalog.gwp_voice_plead_fierce_0, GwpTextCatalog.gwp_voice_plead_fierce_1 }; break;
                case "plead_Plain": choices = new[] { GwpTextCatalog.gwp_voice_plead_plain_0, GwpTextCatalog.gwp_voice_plead_plain_1 }; break;

                // 抗法。
                case "resist_Upright": choices = new[] { GwpTextCatalog.gwp_voice_resist_upright_0, GwpTextCatalog.gwp_voice_resist_upright_1 }; break;
                case "resist_Fierce": choices = new[] { GwpTextCatalog.gwp_voice_resist_fierce_0, GwpTextCatalog.gwp_voice_resist_fierce_1, GwpTextCatalog.gwp_voice_resist_fierce_2 }; break;
                case "resist_Cold": choices = new[] { GwpTextCatalog.gwp_voice_resist_cold_0, GwpTextCatalog.gwp_voice_resist_cold_1, GwpTextCatalog.gwp_voice_resist_cold_2 }; break;
                case "resist_Tight": choices = new[] { GwpTextCatalog.gwp_voice_resist_tight_0, GwpTextCatalog.gwp_voice_resist_tight_1, GwpTextCatalog.gwp_voice_resist_tight_2 }; break;
                case "resist_Soft": choices = new[] { GwpTextCatalog.gwp_voice_resist_soft_0, GwpTextCatalog.gwp_voice_resist_soft_1 }; break;
                case "resist_Plain": choices = new[] { GwpTextCatalog.gwp_voice_resist_plain_0, GwpTextCatalog.gwp_voice_resist_plain_1 }; break;
                case "resist_undercharge": choices = new[] { GwpTextCatalog.gwp_voice_resist_scorn_0, GwpTextCatalog.gwp_voice_resist_scorn_1, GwpTextCatalog.gwp_voice_resist_scorn_2 }; break;
                default: throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown dialogue group");
            }
            int index = _random.Next(choices.Length);
            if (_last.TryGetValue(key, out int previous) && index == previous) index = (index + 1) % choices.Length;
            _last[key] = index;
            return _visible[key] = GwpText.Create(choices[index]);
        }
    }
}
