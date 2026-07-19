using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    internal enum DeterrenceSource
    {
        Personal,
        Family
    }

    internal enum DeterrenceTier
    {
        Low,
        Medium,
        High
    }

    internal enum DeterrenceVoice
    {
        HonorHigh,
        HonorLow,
        ValorHigh,
        ValorLow,
        MercyHigh,
        MercyLow,
        GenerosityHigh,
        GenerosityLow,
        CalculatingHigh,
        CalculatingLow,
        Neutral
    }

    /// <summary>
    /// 台词按震慑来源、强度、原版五项性格的正负方向与玩家灰袍身份组合。
    /// 英文是源文本；简体中文由语言 XML 以相同 key 覆盖。
    /// </summary>
    internal static class GwpAiDeterrenceDialogueCatalog
    {
        public static TextObject GetIntro(DeterrenceSource source, DeterrenceTier tier) => (source, tier) switch
        {
            (DeterrenceSource.Personal, DeterrenceTier.Low) =>
                new TextObject("{=gwp_ai_det_intro_personal_low}{HERO_NAME} regards you steadily. Their encounter with the Grey Wardens is remembered, but it does not rule their manner."),
            (DeterrenceSource.Personal, DeterrenceTier.Medium) =>
                new TextObject("{=gwp_ai_det_intro_personal_medium}{HERO_NAME}'s voice grows more measured. Their own encounter with the Grey Wardens still weighs on every word."),
            (DeterrenceSource.Personal, DeterrenceTier.High) =>
                new TextObject("{=gwp_ai_det_intro_personal_high}{HERO_NAME} holds your gaze with deliberate composure. What the Grey Wardens did to them left a mark too deep to conceal."),
            (DeterrenceSource.Family, DeterrenceTier.Low) =>
                new TextObject("{=gwp_ai_det_intro_family_low}{HERO_NAME} pauses briefly before speaking. The Grey Wardens' dealings with their kin have not quite been forgotten."),
            (DeterrenceSource.Family, DeterrenceTier.Medium) =>
                new TextObject("{=gwp_ai_det_intro_family_medium}{HERO_NAME} chooses each word carefully. Their clan's encounter with the Grey Wardens remains a serious concern."),
            _ =>
                new TextObject("{=gwp_ai_det_intro_family_high}{HERO_NAME}'s expression hardens for a moment. The Grey Wardens' actions still hang heavily over their clan.")
        };

        public static string GetResponse(
            DeterrenceSource source,
            DeterrenceTier tier,
            DeterrenceVoice voice,
            bool playerIsWarden)
        {
            return GetCore(source, tier, voice) + " " + GetAudience(playerIsWarden, tier, voice);
        }

        private static string GetCore(DeterrenceSource source, DeterrenceTier tier, DeterrenceVoice voice) =>
            (source, tier, voice) switch
            {
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_low_honorable}I crossed a boundary, and being stopped was the proper consequence."),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_low_dishonorable}Rules are useful until they obstruct necessity. Being caught only proved that I chose the wrong moment."),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_low_valorous}One defeat does not make me afraid, but it taught me to respect the Grey Wardens' reach."),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_low_cautious}I have no wish to face the Grey Wardens again over something as small as a village raid."),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_low_merciful}Whatever my reasons, villagers should not have borne the price."),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_low_cruel}They made their point. I have simply chosen not to test it again yet."),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_low_generous}My followers paid for my choice as surely as I did. I owe them better judgment next time."),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_low_closefisted}I lost more than I gained. I will not waste my own men and coin that way again."),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_low_calculating}That affair taught me where the cost begins to outweigh the gain."),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_low_impulsive}I acted in anger and paid for it before the anger had even cooled."),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.Neutral) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_low_neutral}The matter is over, but I have not discarded its lesson."),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_medium_honorable}I broke the peace and answered for it. Denying that would only add cowardice to the fault."),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_medium_dishonorable}I will not pretend their law was sacred. I learned only that breaking it openly gives enemies a weapon."),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_medium_valorous}I do not fear a hard fight, but only a fool mistakes courage for blindness to consequence."),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_medium_cautious}Call it caution if you like. I know how quickly they can close every road around a person."),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_medium_merciful}I remember the frightened villagers more clearly than the chains. That is reason enough to stop."),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_medium_cruel}They hurt me enough to make patience useful. Do not confuse that with remorse."),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_medium_generous}The people who followed me bore the losses of my decision. Loyalty requires that I not spend them so carelessly again."),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_medium_closefisted}A burned village is not worth my soldiers, my coin, and my freedom all at once."),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_medium_calculating}I have measured the losses twice. No village is worth drawing that pursuit without a far greater purpose."),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_medium_impulsive}I let anger choose the road, and the Grey Wardens decided where that road ended."),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.Neutral) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_medium_neutral}Since that capture, every raid carries a second calculation: whether the Grey Wardens will answer."),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_high_honorable}I was guilty, and the punishment was deserved. The depth of the lesson does not make the judgment less just."),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_high_dishonorable}I regret being caught, not crossing their line. What changed is that I now hide my steps more carefully."),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_high_valorous}I could face them again. That does not mean I am foolish enough to call them harmless."),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_high_cautious}I survived by learning which fights to avoid. Giving them another reason to pursue me would be needless folly."),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_high_merciful}What weighs on me most is not what they did to me, but what my choices had already done to people who could not resist."),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_high_cruel}I remember exactly what they did. My restraint is calculation, not surrender and certainly not forgiveness."),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_high_generous}I cannot ask my household to endure that cost again merely to satisfy one more raid."),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_high_closefisted}I will not impoverish myself for plunder that the Grey Wardens can take back with interest."),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_high_calculating}Now every road toward a village includes the same figure in my reckoning: what the Grey Wardens will take in return."),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_high_impulsive}Whenever my temper rises, the memory of that punishment drags it down before I act."),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.Neutral) =>
                    GwpText.Get("{=gwp_ai_det_core_personal_high_neutral}Their intervention changed how I judge every raid, every captive, and every road back home."),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_low_honorable}My kin crossed a boundary. I cannot call the Grey Wardens wrong for answering it."),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_low_dishonorable}Blood comes before strangers' rules. I judge the Grey Wardens by what they did to my kin, not by the charge they named."),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_low_valorous}My clan took a blow; it did not lose its courage. Still, the warning was clear."),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_low_cautious}What happened to my kin showed us a danger the clan would be foolish to provoke without need."),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_low_merciful}I am more troubled by the villagers who suffered than by my kin's wounded pride."),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_low_cruel}They laid hands on my blood. I remember it, even while I let the matter rest."),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_low_generous}Whatever my kinsman's fault, they are still ours. We must correct them without abandoning them."),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_low_closefisted}My kinsman's folly became a bill for the whole clan. They should have borne it alone."),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_low_calculating}What happened to my kin gave the clan a useful measure of the risks involved."),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_low_impulsive}My kin acted before thinking, and the rest of us were left to remember the result."),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.Neutral) =>
                    GwpText.Get("{=gwp_ai_det_core_family_low_neutral}The affair touched my family, so naturally the clan remembers it."),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_medium_honorable}My kinsman broke the peace and the clan paid for it. Responsibility does not vanish merely because blood is shared."),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_medium_dishonorable}Whatever my kinsman did, outsiders had no right to humble our house and call the matter justice."),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_medium_valorous}We are not cowed, but neither will we charge blindly into the same punishment twice."),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_medium_cautious}The clan has no desire to walk into the same trap merely to prove that it is unafraid."),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_medium_merciful}I would rather my family learn restraint than leave more peasants grieving for what nobles chose to do."),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_medium_cruel}The Grey Wardens made my clan bleed for another's mistake. Such accounts are not forgotten."),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_medium_generous}One kinsman's mistake cost everyone who stood beside them. Our duty now is to repair what it damaged."),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_medium_closefisted}I have no intention of paying again for a relative's appetite for plunder."),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_medium_calculating}The clan has counted the men, coin, and freedom that one reckless choice cost us."),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_medium_impulsive}One hotheaded decision brought the Grey Wardens down on all of us. That is not easily forgotten."),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.Neutral) =>
                    GwpText.Get("{=gwp_ai_det_core_family_medium_neutral}What happened to one of us changed how the whole clan speaks of raids and reprisals."),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_high_honorable}The lesson reached the whole clan: rank and blood do not excuse a crime against those under our protection."),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_high_dishonorable}My clan remembers the punishment as an insult, not a judgment. Restraint does not mean that we accept their right."),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_high_valorous}My house still has courage, but courage now walks beside a very clear memory of the Grey Wardens."),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_high_cautious}We know how completely they can close around a clan. Avoiding that danger again is survival, not submission."),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_high_merciful}If my kin finally understand the suffering they caused, then perhaps something decent can still come from the punishment."),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_high_cruel}Their hand still lies heavy on my clan. We have not submitted; we have merely learned when vengeance is too costly."),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_high_generous}The whole clan carried the burden. I owe protection to those who kept faith when another brought ruin home."),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_high_closefisted}The Grey Wardens taught us that one relative's folly can become everyone's expense. I will not permit that twice."),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_core_family_high_calculating}No one in my clan now discusses a village raid without first accounting for the Grey Wardens' answer."),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_core_family_high_impulsive}The whole clan felt the consequence of one unchecked impulse. Now even anger must answer to that memory."),
                _ =>
                    GwpText.Get("{=gwp_ai_det_core_family_high_neutral}The consequences settled over the entire clan. None of us now treats the Grey Wardens as a distant concern.")
            };

        private static string GetAudience(bool playerIsWarden, DeterrenceTier tier, DeterrenceVoice voice) =>
            (playerIsWarden, tier, voice) switch
            {
                (false, DeterrenceTier.Low, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_low_honorable}Take it as honest advice: do not bring the same judgment on yourself by preying on villages."),
                (false, DeterrenceTier.Low, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_low_dishonorable}Keep your promises if it suits you, but at least do not make yourself such easy prey."),
                (false, DeterrenceTier.Low, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_low_valorous}Test your courage on armed warriors, not on villagers who cannot answer you."),
                (false, DeterrenceTier.Low, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_low_cautious}You need not prove anything by provoking people who will pursue you without tiring."),
                (false, DeterrenceTier.Low, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_low_merciful}You can choose a better road before frightened families are made to pay for it."),
                (false, DeterrenceTier.Low, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_low_cruel}Do as you please, but do not say no one warned you what follows."),
                (false, DeterrenceTier.Low, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_low_generous}Do not make your followers and family pay for a choice made only to satisfy you."),
                (false, DeterrenceTier.Low, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_low_closefisted}If conscience does not move you, then protect your own purse and soldiers."),
                (false, DeterrenceTier.Low, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_low_calculating}If profit is what guides you, remember to include the Grey Wardens in your reckoning."),
                (false, DeterrenceTier.Low, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_low_impulsive}Do not let one angry moment choose a road that takes years to escape."),
                (false, DeterrenceTier.Low, DeterrenceVoice.Neutral) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_low_neutral}You would be wise to remember that before choosing your own targets."),
                (false, DeterrenceTier.Medium, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_medium_honorable}Keep faith with the innocent, and you will never need to learn that lesson as I did."),
                (false, DeterrenceTier.Medium, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_medium_dishonorable}If you cross that line, do not imagine that rank or a convenient excuse will shield you."),
                (false, DeterrenceTier.Medium, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_medium_valorous}Do not mistake cruelty toward the helpless for bravery, nor the Grey Wardens' patience for weakness."),
                (false, DeterrenceTier.Medium, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_medium_cautious}Turn away from the villages while the choice is still yours; retreat is cheaper before pursuit begins."),
                (false, DeterrenceTier.Medium, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_medium_merciful}Leave the villages in peace; their people have already endured enough from lords like us."),
                (false, DeterrenceTier.Medium, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_medium_cruel}Choose that road if you wish. Just be ready to pay more than you take."),
                (false, DeterrenceTier.Medium, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_medium_generous}A lord owes protection to those who trust them, not a trail of losses created for pride."),
                (false, DeterrenceTier.Medium, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_medium_closefisted}The loot from a village will not repay the men, coin, and freedom that pursuit can cost."),
                (false, DeterrenceTier.Medium, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_medium_calculating}If you intend to raid, understand that the cost may follow you long after the loot is spent."),
                (false, DeterrenceTier.Medium, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_medium_impulsive}Pause before you order the torches lit. Anger leaves faster than consequences."),
                (false, DeterrenceTier.Medium, DeterrenceVoice.Neutral) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_medium_neutral}Consider the consequence before you make the same choice."),
                (false, DeterrenceTier.High, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_high_honorable}I tell you plainly: protect the villages under your power, or justice will eventually find you as well."),
                (false, DeterrenceTier.High, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_high_dishonorable}Choose carefully which laws you break. The Grey Wardens care more about the deed than the excuse."),
                (false, DeterrenceTier.High, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_high_valorous}If you want a worthy enemy, seek one who can fight back; the Grey Wardens will answer if you choose the helpless."),
                (false, DeterrenceTier.High, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_high_cautious}If self-preservation matters to you, do not give them a reason to learn your name."),
                (false, DeterrenceTier.High, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_high_merciful}Spare yourself this lesson by sparing the people who would otherwise suffer first."),
                (false, DeterrenceTier.High, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_high_cruel}Burn what you like, but understand that the Grey Wardens know how to make memory outlive pride."),
                (false, DeterrenceTier.High, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_high_generous}Think of your clan and followers before you make them inherit the price of your decision."),
                (false, DeterrenceTier.High, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_high_closefisted}Even the most selfish arithmetic reaches the same answer: a raid is not worth what they collect afterward."),
                (false, DeterrenceTier.High, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_high_calculating}Whatever gain you imagine in a burning village, set it against years of pursuit before you decide."),
                (false, DeterrenceTier.High, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_high_impulsive}If your temper commands you, the Grey Wardens may be the ones who decide when it stops."),
                (false, DeterrenceTier.High, DeterrenceVoice.Neutral) =>
                    GwpText.Get("{=gwp_ai_det_audience_outsider_high_neutral}Stay away from that path unless you are prepared for the Grey Wardens to follow it back to you."),
                (true, DeterrenceTier.Low, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_low_honorable}Since you serve beside them, hold yourself to the same law they claimed to uphold when they intervened."),
                (true, DeterrenceTier.Low, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_low_dishonorable}You carry their warrants, but ink alone does not turn power into virtue."),
                (true, DeterrenceTier.Low, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_low_valorous}You carry their authority now; prove that it rests on courage rather than intimidation."),
                (true, DeterrenceTier.Low, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_low_cautious}Authority can make a fool reckless. Do not let their name push you into a fight you have not measured."),
                (true, DeterrenceTier.Low, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_low_merciful}If you work with them, use that place to protect villagers before punishment becomes necessary."),
                (true, DeterrenceTier.Low, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_low_cruel}So you chose to work with them. Do not expect that alone to make every lord bow."),
                (true, DeterrenceTier.Low, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_low_generous}If you serve them, remember the people who follow you and will pay for every order."),
                (true, DeterrenceTier.Low, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_low_closefisted}If a bounty pays you, do not pretend that coin and justice are the same thing."),
                (true, DeterrenceTier.Low, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_low_calculating}You chose to work with the Grey Wardens; that makes candour more useful than pretence."),
                (true, DeterrenceTier.Low, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_low_impulsive}A warrant does not excuse acting in anger. You are still answerable for what follows."),
                (true, DeterrenceTier.Low, DeterrenceVoice.Neutral) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_low_neutral}You serve with the Grey Wardens, so you should understand why the matter is remembered."),
                (true, DeterrenceTier.Medium, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_medium_honorable}If you act in their name, keep the law above vengeance, even when the accused deserves no sympathy."),
                (true, DeterrenceTier.Medium, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_medium_dishonorable}Use their authority if you must, but do not preach purity while bending the law for yourself."),
                (true, DeterrenceTier.Medium, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_medium_valorous}If you bear their authority, meet armed offenders openly and leave terror to lesser people."),
                (true, DeterrenceTier.Medium, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_medium_cautious}A warrant is no substitute for knowing when to advance and when to withdraw."),
                (true, DeterrenceTier.Medium, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_medium_merciful}Use your place among them to prevent the next village from suffering, not merely to punish afterward."),
                (true, DeterrenceTier.Medium, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_medium_cruel}You may hunt for them, but do not mistake a warrant for invincibility."),
                (true, DeterrenceTier.Medium, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_medium_generous}Protect your companions and the villagers alike; authority is a debt before it is a privilege."),
                (true, DeterrenceTier.Medium, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_medium_closefisted}Do not spend other people's lives merely to enlarge your reward."),
                (true, DeterrenceTier.Medium, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_medium_calculating}A Grey Warden ally should know that measured enforcement wins more obedience than needless humiliation."),
                (true, DeterrenceTier.Medium, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_medium_impulsive}Do not let an insult turn enforcement into vengeance simply because their seal is behind you."),
                (true, DeterrenceTier.Medium, DeterrenceVoice.Neutral) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_medium_neutral}You know their methods from the other side of the warrant; remember what those methods leave behind."),
                (true, DeterrenceTier.High, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_high_honorable}You represent them now. If their justice is to mean anything, your conduct must be cleaner than the crimes you pursue."),
                (true, DeterrenceTier.High, DeterrenceVoice.HonorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_high_dishonorable}If you call what you do justice, prove it through consistent conduct rather than the power to punish."),
                (true, DeterrenceTier.High, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_high_valorous}Stand with them boldly if you must, but let your courage be measured by whom you protect, not whom you can frighten."),
                (true, DeterrenceTier.High, DeterrenceVoice.ValorLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_high_cautious}If the Grey Wardens send you after dangerous people, remember that caution keeps an enforcer alive."),
                (true, DeterrenceTier.High, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_high_merciful}Stand between the Grey Wardens and needless suffering when you can; justice has no need to become cruelty."),
                (true, DeterrenceTier.High, DeterrenceVoice.MercyLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_high_cruel}Their mark is on my house, and you have chosen their side. Do not expect me to confuse either with friendship."),
                (true, DeterrenceTier.High, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_high_generous}Their authority makes you responsible for everyone placed in danger by your decisions."),
                (true, DeterrenceTier.High, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_high_closefisted}If you serve only for profit, understand that uncontrolled enforcement eventually costs more than it pays."),
                (true, DeterrenceTier.High, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_high_calculating}Because you work with them, you should understand this: fear spends quickly, but consistent law keeps paying."),
                (true, DeterrenceTier.High, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_high_impulsive}If temper governs your sword, their authority will not stop you from becoming another danger to the innocent."),
                _ =>
                    GwpText.Get("{=gwp_ai_det_audience_warden_high_neutral}You chose to work with the Grey Wardens. Then you should know that what they do is remembered long after the warrant closes.")
            };
    }
}
