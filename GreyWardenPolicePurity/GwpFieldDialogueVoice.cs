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
                case "gwp_fa_task_authority": alternatives = new[] { "{=gwp_fa_task_authority_variant_1}The Wardens may have a say, but that does not put you in command of me.", "{=gwp_fa_task_authority_variant_2}You are running an errand for the Wardens. Why should I answer to you?" }; break;
                case "gwp_fa_task_under_charge": alternatives = new[] { "{=gwp_fa_task_under_charge_variant_1}Your own reputation is hardly spotless. Now you lecture me?", "{=gwp_fa_task_under_charge_variant_2}I might listen to someone else. You should put your own affairs right first." }; break;
                case "gwp_fa_task_price": alternatives = new[] { "{=gwp_fa_task_price_variant_1}That much? What gives the Wardens cause to demand that sum?", "{=gwp_fa_task_price_variant_2}You ask for a great deal of money. You owe me an explanation." }; break;
                case "gwp_fa_task_tally": alternatives = new[] { "{=gwp_fa_task_tally_variant_1}You mean to pursue the older offences too? How far back will you go?", "{=gwp_fa_task_tally_variant_2}Those incidents were long apart. Now you bring them all to me at once?" }; break;
                case "gwp_fa_task_denial": alternatives = new[] { "{=gwp_fa_task_denial_variant_1}Ask my soldiers what they did. I gave no such order.", "{=gwp_fa_task_denial_variant_2}Why should I bear all the blame for what my men did?" }; break;
                case "gwp_fa_task_war": alternatives = new[] { "{=gwp_fa_task_war_variant_1}We cannot take enemy goods in war now? The Wardens reach too far.", "{=gwp_fa_task_war_variant_2}That was an enemy caravan. I am fighting a war, not robbing travellers." }; break;
                case "gwp_fa_task_consequence": alternatives = new[] { "{=gwp_fa_task_consequence_variant_1}Suppose I refuse to pay today. What will you do?", "{=gwp_fa_task_consequence_variant_2}I will take my men and ride on. Will the Wardens follow me forever?" }; break;
                case "gwp_fa_task_record": alternatives = new[] { "{=gwp_fa_task_record_variant_1}The Wardens already took me once. Was that not enough?", "{=gwp_fa_task_record_variant_2}The Wardens again. Must your people follow wherever I go?" }; break;
                case "gwp_terms_final_objection": alternatives = new[] { "{=gwp_terms_final_objection_variant_1}What do I gain by giving up all my terms?", "{=gwp_terms_final_objection_variant_2}Why should I accept the loss and pay everything you ask?" }; break;
                case "gwp_fa_open_under_charge": alternatives = new[] { "{=gwp_fa_open_under_charge_variant_1}You work for the Wardens now? That is new. What do you want?", "{=gwp_fa_open_under_charge_variant_2}I have heard of you. Tell me what the Wardens sent you for." }; break;
                case "gwp_fa_open_respected_known": alternatives = new[] { "{=gwp_fa_open_respected_known_variant_1}You. My last meeting with the Wardens was no pleasure. What is it now?", "{=gwp_fa_open_respected_known_variant_2}The Wardens want me again? Speak. I am listening." }; break;
                case "gwp_fa_open_respected": alternatives = new[] { "{=gwp_fa_open_respected_variant_1}I have heard of you. State your business.", "{=gwp_fa_open_respected_variant_2}If the Wardens sent you, there must be business to discuss. Go on." }; break;
                case "gwp_fa_open_known": alternatives = new[] { "{=gwp_fa_open_known_variant_1}I have not forgotten being taken by your people. What do you want this time?", "{=gwp_fa_open_known_variant_2}Stopping me again? What do the Wardens want now?" }; break;
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
                case "demand_RequestGrace": return GwpText.Create("{=gwp_voice_grace_alt}I need three days to gather the money. I am asking for time, not forgiveness.");
                case "demand_PayInFull": return GwpText.Create("{=gwp_voice_demand_payinfull_alt}I will pay the assessed sum. Let us settle this.");
                case "demand_PayToAvoidBattle": return GwpText.Create("{=gwp_voice_demand_paytoavoidbattle_alt}Tell your men to hold. I will pay in full; nobody needs to die here.");
                case "demand_PayForPeace": return GwpText.Create("{=gwp_voice_demand_payforpeace_alt}Enough people have suffered. I will pay in full; leave the others out of it.");
                case "demand_PayGenerously": return GwpText.Create("{=gwp_voice_demand_paygenerously_alt}I can afford it. Take the whole sum and settle the account.");
                case "demand_SurrenderSelf": return GwpText.Create("{=gwp_voice_demand_surrenderself_alt}The money must feed these men. If you need a prisoner, take me alone.");
                case "demand_BribeTheWarden": return GwpText.Create("{=gwp_voice_demand_bribethewarden_alt}This is worth {GWP_BRIBE_AMOUNT} denars to you. Take it and let our discussion end here.");
                case "demand_CunningHalf": return GwpText.Create("{=gwp_voice_demand_cunninghalf_alt}I cannot raise that sum. Half is all there is. Take it if you want it.");
                case "demand_RashMost": return GwpText.Create("{=gwp_voice_demand_rashmost_alt}Four fifths. Take it. Spare me the argument over the rest.");
                case "demand_DemandDuel": return GwpText.Create("{=gwp_voice_demand_demandduel_alt}Just you and me. You win, I pay the fine. I win, you leave empty-handed.");
                case "demand_DeedsOnly": return GwpText.Create("{=gwp_voice_demand_deedsonly_alt}Two fifths is all I will pay. I will kill the men I blame myself.");
                case "demand_HaggleDown": return GwpText.Create("{=gwp_voice_demand_haggledown_alt}Seven tenths is plenty. Think carefully about that offer.");
                case "hear": return GwpText.Create("{=gwp_voice_hear_alt}I heard the charge. Why should I accept your authority?");
                case "refuse": return GwpText.Create("{=gwp_voice_refuse_alt}You want me to accept your authority? No. Stand aside.");
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

                // 认罚。原来每种性子只有一句写死的台词，同一个人说第二次就重样了。
                case "submit_Upright": choices = new[] { "{=gwp_voice_submit_upright_0}The charge is fair. Take it, and let it be written that I paid it standing.", "{=gwp_voice_submit_upright_1}I will not argue a debt I owe. Count it.", "{=gwp_voice_submit_upright_2}Then it is owed, and I pay what is owed. Here." }; break;
                case "submit_Cold": choices = new[] { "{=gwp_voice_submit_cold_0}Cheaper than the alternative. That is the only reason, and we both know it.", "{=gwp_voice_submit_cold_1}The sum is smaller than the trouble. Take it.", "{=gwp_voice_submit_cold_2}I have weighed it. You win this column. Count your money." }; break;
                case "submit_Soft": choices = new[] { "{=gwp_voice_submit_soft_0}Take it. It buys nothing back, but take it.", "{=gwp_voice_submit_soft_1}If this closes the matter for them, then take it.", "{=gwp_voice_submit_soft_2}Here. I would rather it went to them than stayed with me." }; break;
                case "submit_Tight": choices = new[] { "{=gwp_voice_submit_tight_0}Every coin of it. I hope your order chokes on the counting.", "{=gwp_voice_submit_tight_1}There. Count it twice, you will not find a coin short.", "{=gwp_voice_submit_tight_2}Take it and be quick. Every moment of this costs me more." }; break;
                case "submit_Fierce": choices = new[] { "{=gwp_voice_submit_fierce_0}Fine. Money, then. Do not mistake it for fear.", "{=gwp_voice_submit_fierce_1}Take your coin. It settles nothing between us.", "{=gwp_voice_submit_fierce_2}Here, and let that be the end of your talking." }; break;
                case "submit_Plain": choices = new[] { "{=gwp_voice_submit_plain_0}Here. Count it, and be gone.", "{=gwp_voice_submit_plain_1}All right. The sum you named, no more.", "{=gwp_voice_submit_plain_2}Take it. I want no further trouble over this." }; break;

                // 交不出钱。
                case "plead_Cold": choices = new[] { "{=gwp_voice_plead_cold_0}You have named a sum I cannot meet. Wages, feed, remounts - look at the column and tell me where it hides.", "{=gwp_voice_plead_cold_1}The figure is beyond me. I can show you the accounts if you doubt it.", "{=gwp_voice_plead_cold_2}What you ask does not exist in my purse. That is simply the arithmetic." }; break;
                case "plead_Tight": choices = new[] { "{=gwp_voice_plead_tight_0}I am not a rich man, whatever your ledger says. You will not find it on me.", "{=gwp_voice_plead_tight_1}Search me if you like. There is nothing like that sum here.", "{=gwp_voice_plead_tight_2}You have been told wrong about my means. I cannot pay it." }; break;
                case "plead_Upright": choices = new[] { "{=gwp_voice_plead_upright_0}I will not promise what I cannot pay. That sum is beyond me.", "{=gwp_voice_plead_upright_1}I would pay it if I had it. I do not." }; break;
                case "plead_Soft": choices = new[] { "{=gwp_voice_plead_soft_0}I have men to feed before I have coin for you. There is nothing left.", "{=gwp_voice_plead_soft_1}Do not take what these people still need. I cannot meet it." }; break;
                case "plead_Fierce": choices = new[] { "{=gwp_voice_plead_fierce_0}I have no such money, and I will not pretend otherwise.", "{=gwp_voice_plead_fierce_1}Ask for what I have, not for what I do not." }; break;
                case "plead_Plain": choices = new[] { "{=gwp_voice_plead_plain_0}That sum? I do not carry it. Search the baggage if you like.", "{=gwp_voice_plead_plain_1}I cannot raise that. Not today, not from what I have here." }; break;

                // 抗法。
                case "resist_Upright": choices = new[] { "{=gwp_voice_resist_upright_0}I will not pay for a charge I do not accept. Do what you came to do.", "{=gwp_voice_resist_upright_1}Bring me before someone with the standing to judge it. Until then, no." }; break;
                case "resist_Fierce": choices = new[] { "{=gwp_voice_resist_fierce_0}I have never bought my way out of anything. Draw.", "{=gwp_voice_resist_fierce_1}You will take nothing from me while I stand. Come on, then.", "{=gwp_voice_resist_fierce_2}Enough talk. Let the swords settle it." }; break;
                case "resist_Cold": choices = new[] { "{=gwp_voice_resist_cold_0}I have counted your column and I have counted mine. No.", "{=gwp_voice_resist_cold_1}The odds do not favour you enough. My answer is no.", "{=gwp_voice_resist_cold_2}No. You may test that answer if you wish." }; break;
                case "resist_Tight": choices = new[] { "{=gwp_voice_resist_tight_0}You will not have a denar of it. Not one.", "{=gwp_voice_resist_tight_1}I would sooner bleed than pay that. Try me.", "{=gwp_voice_resist_tight_2}Not a coin. Take it off my body if you can." }; break;
                case "resist_Soft": choices = new[] { "{=gwp_voice_resist_soft_0}I cannot give you what my people still need. I will not.", "{=gwp_voice_resist_soft_1}I am sorry it comes to this, but my answer is no." }; break;
                case "resist_Plain": choices = new[] { "{=gwp_voice_resist_plain_0}No. Come and take it, if you can.", "{=gwp_voice_resist_plain_1}My answer is no. Do as you must." }; break;
                case "resist_undercharge": choices = new[] { "{=gwp_voice_resist_scorn_0}You are under your own charge and you come to collect mine? Draw.", "{=gwp_voice_resist_scorn_1}A wanted man collecting fines. I will not answer to you.", "{=gwp_voice_resist_scorn_2}Put your own name right before you read mine off a list." }; break;
                default: throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown dialogue group");
            }
            int index = _random.Next(choices.Length);
            if (_last.TryGetValue(key, out int previous) && index == previous) index = (index + 1) % choices.Length;
            _last[key] = index;
            return _visible[key] = GwpText.Create(choices[index]);
        }
    }
}
