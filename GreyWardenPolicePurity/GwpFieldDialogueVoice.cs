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
                case "demand_PayInFull": return GwpText.Create("{=gwp_voice_demand_payinfull_alt}I will pay the assessed sum. Let us settle this.");
                case "demand_PayToAvoidBattle": return GwpText.Create("{=gwp_voice_demand_paytoavoidbattle_alt}Tell your men to hold. I will pay in full; nobody needs to die here.");
                case "demand_PayForPeace": return GwpText.Create("{=gwp_voice_demand_payforpeace_alt}Enough people have suffered. I will pay in full; leave the others out of it.");
                case "demand_PayGenerously": return GwpText.Create("{=gwp_voice_demand_paygenerously_alt}I can afford it. Take the whole sum and settle the account.");
                case "demand_SurrenderSelf": return GwpText.Create("{=gwp_voice_demand_surrenderself_alt}The money must feed these men. If you need a prisoner, take me alone.");
                case "demand_BribeTheWarden": return GwpText.Create("{=gwp_voice_demand_bribethewarden_alt}Fifteen percent of the fine, yours to keep. What you tell them is up to you.");
                case "demand_CunningHalf": return GwpText.Create("{=gwp_voice_demand_cunninghalf_alt}I cannot raise that sum. Half is all there is. Take it if you want it.");
                case "demand_RashMost": return GwpText.Create("{=gwp_voice_demand_rashmost_alt}Four fifths. Take it. Spare me the argument over the rest.");
                case "demand_DemandDuel": return GwpText.Create("{=gwp_voice_demand_demandduel_alt}Just you and me. You win, I pay the fine. I win, you leave empty-handed.");
                case "demand_DeedsOnly": return GwpText.Create("{=gwp_voice_demand_deedsonly_alt}Two fifths is all I will pay. I will kill the men I blame myself.");
                case "demand_HaggleDown": return GwpText.Create("{=gwp_voice_demand_haggledown_alt}Seven tenths is plenty. Think carefully about that offer.");
                case "hear": return GwpText.Create("{=gwp_voice_hear_alt}I heard the charge. Why should I accept your authority?");
                case "refuse": return GwpText.Create("{=gwp_voice_refuse_alt}You expect me to bow to that handful of men? Stand aside. We have nothing to discuss.");
                case "done1": return GwpText.Create("{=gwp_voice_done1_alt}All right, I accept the charge. We still have to agree on how to settle it.");
                case "lost1": return GwpText.Create("{=gwp_voice_lost1_alt}You still have not given me a reason to submit. Stop circling the matter.");
                case "done2": return GwpText.Create("{=gwp_voice_done2_alt}All right, the sum you named. No further conditions.");
                case "lost2": return GwpText.Create("{=gwp_voice_lost2_alt}I will not yield on that. My offer still stands.");
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }
        internal TextObject Line(string key)
        {
            if (_visible.TryGetValue(key, out var text)) return text;
            string[] choices;
            switch (key)
            {
                case "open_Upright": choices = new[] { "{=gwp_voice_open_upright_0}Show me the warrant. I will answer a proper charge.", "{=gwp_voice_open_upright_1}If you speak for the Wardens, state your business clearly." }; break;
                case "open_Cold": choices = new[] { "{=gwp_voice_open_cold_0}You did not chase me down to ask directions. Speak.", "{=gwp_voice_open_cold_1}The Wardens want me? Tell me the business first." }; break;
                case "open_Fierce": choices = new[] { "{=gwp_voice_open_fierce_0}You had better have a reason for blocking my road.", "{=gwp_voice_open_fierce_1}I can see your banner. What do you want?" }; break;
                case "open_Soft": choices = new[] { "{=gwp_voice_open_soft_0}Speak here. There is no need to alarm the others.", "{=gwp_voice_open_soft_1}The Wardens... Who has suffered this time?" }; break;
                case "open_Tight": choices = new[] { "{=gwp_voice_open_tight_0}Say your piece. I have promised you nothing.", "{=gwp_voice_open_tight_1}A visit from your people is seldom cheap." }; break;
                case "open_Plain": choices = new[] { "{=gwp_voice_open_plain_0}You work for the Wardens? What is this about?", "{=gwp_voice_open_plain_1}Wait. Did you say the Wardens sent you?" }; break;
                case "paid": choices = new[] { "{=gwp_voice_paid_0}We counted the money together. Why are you back?", "{=gwp_voice_paid_1}I have paid you. What remains to discuss?" }; break;
                case "fight_Upright": choices = new[] { "{=gwp_voice_fight_upright_0}Let everyone here see who draws first.", "{=gwp_voice_fight_upright_1}I will not submit. Come and take me." }; break;
                case "fight_Cold": choices = new[] { "{=gwp_voice_fight_cold_0}There is nothing left to reckon. Form up.", "{=gwp_voice_fight_cold_1}You chose this course. I hope you counted the cost." }; break;
                case "fight_Fierce": choices = new[] { "{=gwp_voice_fight_fierce_0}Good! We could have spared ourselves the speeches.", "{=gwp_voice_fight_fierce_1}Come, then. Let us see if you can!" }; break;
                case "fight_Soft": choices = new[] { "{=gwp_voice_fight_soft_0}Must this end in blood? Then I have to defend my people.", "{=gwp_voice_fight_soft_1}Stand back, away from us. It seems there is no avoiding this." }; break;
                case "fight_Tight": choices = new[] { "{=gwp_voice_fight_tight_0}Cannot take the money, so you take me? Try it.", "{=gwp_voice_fight_tight_1}You want to hand me over? It will not be easy." }; break;
                case "fight_Plain": choices = new[] { "{=gwp_voice_fight_plain_0}Then there is nothing left to say. Draw.", "{=gwp_voice_fight_plain_1}I will not hold out my hands for your ropes." }; break;
                case "betray_Upright": choices = new[] { "{=gwp_voice_betray_upright_0}You took the money and now break our agreement? Everyone heard it!", "{=gwp_voice_betray_upright_1}I paid as agreed. Is this how you serve the Wardens?" }; break;
                case "betray_Cold": choices = new[] { "{=gwp_voice_betray_cold_0}So payment was not the end. I gave your word too much credit.", "{=gwp_voice_betray_cold_1}My money in your purse and me in your prison cart? Quite a bargain." }; break;
                case "betray_Fierce": choices = new[] { "{=gwp_voice_betray_fierce_0}You take my money and turn on me? Draw your sword!", "{=gwp_voice_betray_fierce_1}I did not pay out of fear! Now you will learn that." }; break;
                case "betray_Soft": choices = new[] { "{=gwp_voice_betray_soft_0}I paid so everyone could leave safely. Will you not even allow that?", "{=gwp_voice_betray_soft_1}You have the money. Why force these people to fight as well?" }; break;
                case "betray_Tight": choices = new[] { "{=gwp_voice_betray_tight_0}Give that money back! Paid and arrested, is that your trade?", "{=gwp_voice_betray_tight_1}I counted every coin into your hands, and now it means nothing?" }; break;
                case "betray_Plain": choices = new[] { "{=gwp_voice_betray_plain_0}Wait, I already paid you! How can you turn on me now?", "{=gwp_voice_betray_plain_1}That was not our agreement! You have the money. What more do you want?" }; break;
                case "success_Upright": choices = new[] { "{=gwp_voice_success_upright_0}That is a fair point. I accept it.", "{=gwp_voice_success_upright_1}I cannot dispute that point." }; break;
                case "success_Cold": choices = new[] { "{=gwp_voice_success_cold_0}That is something I must consider.", "{=gwp_voice_success_cold_1}That does change the reckoning." }; break;
                case "success_Fierce": choices = new[] { "{=gwp_voice_success_fierce_0}All right. I hear you.", "{=gwp_voice_success_fierce_1}That argument carries some weight." }; break;
                case "success_Soft": choices = new[] { "{=gwp_voice_success_soft_0}I cannot ignore the people you speak of.", "{=gwp_voice_success_soft_1}Yes... I should give that more thought." }; break;
                case "success_Tight": choices = new[] { "{=gwp_voice_success_tight_0}As you put it, there is something in it for me.", "{=gwp_voice_success_tight_1}Those figures do make some sense." }; break;
                case "success_Plain": choices = new[] { "{=gwp_voice_success_plain_0}That is true.", "{=gwp_voice_success_plain_1}You have persuaded me on that point." }; break;
                default: throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown dialogue group");
            }
            int index = _random.Next(choices.Length);
            if (_last.TryGetValue(key, out int previous) && index == previous) index = (index + 1) % choices.Length;
            _last[key] = index;
            return _visible[key] = GwpText.Create(choices[index]);
        }
    }
}
