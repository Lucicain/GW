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
                new TextObject(GwpTextCatalog.gwp_ai_det_intro_personal_low),
            (DeterrenceSource.Personal, DeterrenceTier.Medium) =>
                new TextObject(GwpTextCatalog.gwp_ai_det_intro_personal_medium),
            (DeterrenceSource.Personal, DeterrenceTier.High) =>
                new TextObject(GwpTextCatalog.gwp_ai_det_intro_personal_high),
            (DeterrenceSource.Family, DeterrenceTier.Low) =>
                new TextObject(GwpTextCatalog.gwp_ai_det_intro_family_low),
            (DeterrenceSource.Family, DeterrenceTier.Medium) =>
                new TextObject(GwpTextCatalog.gwp_ai_det_intro_family_medium),
            _ =>
                new TextObject(GwpTextCatalog.gwp_ai_det_intro_family_high)
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
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_low_honorable),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_low_dishonorable),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_low_valorous),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_low_cautious),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_low_merciful),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_low_cruel),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_low_generous),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_low_closefisted),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_low_calculating),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_low_impulsive),
                (DeterrenceSource.Personal, DeterrenceTier.Low, DeterrenceVoice.Neutral) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_low_neutral),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_medium_honorable),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_medium_dishonorable),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_medium_valorous),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_medium_cautious),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_medium_merciful),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_medium_cruel),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_medium_generous),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_medium_closefisted),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_medium_calculating),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_medium_impulsive),
                (DeterrenceSource.Personal, DeterrenceTier.Medium, DeterrenceVoice.Neutral) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_medium_neutral),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_high_honorable),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_high_dishonorable),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_high_valorous),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_high_cautious),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_high_merciful),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_high_cruel),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_high_generous),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_high_closefisted),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_high_calculating),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_high_impulsive),
                (DeterrenceSource.Personal, DeterrenceTier.High, DeterrenceVoice.Neutral) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_personal_high_neutral),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_low_honorable),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_low_dishonorable),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_low_valorous),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_low_cautious),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_low_merciful),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_low_cruel),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_low_generous),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_low_closefisted),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_low_calculating),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_low_impulsive),
                (DeterrenceSource.Family, DeterrenceTier.Low, DeterrenceVoice.Neutral) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_low_neutral),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_medium_honorable),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_medium_dishonorable),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_medium_valorous),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_medium_cautious),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_medium_merciful),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_medium_cruel),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_medium_generous),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_medium_closefisted),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_medium_calculating),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_medium_impulsive),
                (DeterrenceSource.Family, DeterrenceTier.Medium, DeterrenceVoice.Neutral) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_medium_neutral),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_high_honorable),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_high_dishonorable),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_high_valorous),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_high_cautious),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_high_merciful),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_high_cruel),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_high_generous),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_high_closefisted),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_high_calculating),
                (DeterrenceSource.Family, DeterrenceTier.High, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_high_impulsive),
                _ =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_core_family_high_neutral)
            };

        private static string GetAudience(bool playerIsWarden, DeterrenceTier tier, DeterrenceVoice voice) =>
            (playerIsWarden, tier, voice) switch
            {
                (false, DeterrenceTier.Low, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_low_honorable),
                (false, DeterrenceTier.Low, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_low_dishonorable),
                (false, DeterrenceTier.Low, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_low_valorous),
                (false, DeterrenceTier.Low, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_low_cautious),
                (false, DeterrenceTier.Low, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_low_merciful),
                (false, DeterrenceTier.Low, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_low_cruel),
                (false, DeterrenceTier.Low, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_low_generous),
                (false, DeterrenceTier.Low, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_low_closefisted),
                (false, DeterrenceTier.Low, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_low_calculating),
                (false, DeterrenceTier.Low, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_low_impulsive),
                (false, DeterrenceTier.Low, DeterrenceVoice.Neutral) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_low_neutral),
                (false, DeterrenceTier.Medium, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_medium_honorable),
                (false, DeterrenceTier.Medium, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_medium_dishonorable),
                (false, DeterrenceTier.Medium, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_medium_valorous),
                (false, DeterrenceTier.Medium, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_medium_cautious),
                (false, DeterrenceTier.Medium, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_medium_merciful),
                (false, DeterrenceTier.Medium, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_medium_cruel),
                (false, DeterrenceTier.Medium, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_medium_generous),
                (false, DeterrenceTier.Medium, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_medium_closefisted),
                (false, DeterrenceTier.Medium, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_medium_calculating),
                (false, DeterrenceTier.Medium, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_medium_impulsive),
                (false, DeterrenceTier.Medium, DeterrenceVoice.Neutral) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_medium_neutral),
                (false, DeterrenceTier.High, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_high_honorable),
                (false, DeterrenceTier.High, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_high_dishonorable),
                (false, DeterrenceTier.High, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_high_valorous),
                (false, DeterrenceTier.High, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_high_cautious),
                (false, DeterrenceTier.High, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_high_merciful),
                (false, DeterrenceTier.High, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_high_cruel),
                (false, DeterrenceTier.High, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_high_generous),
                (false, DeterrenceTier.High, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_high_closefisted),
                (false, DeterrenceTier.High, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_high_calculating),
                (false, DeterrenceTier.High, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_high_impulsive),
                (false, DeterrenceTier.High, DeterrenceVoice.Neutral) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_outsider_high_neutral),
                (true, DeterrenceTier.Low, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_low_honorable),
                (true, DeterrenceTier.Low, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_low_dishonorable),
                (true, DeterrenceTier.Low, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_low_valorous),
                (true, DeterrenceTier.Low, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_low_cautious),
                (true, DeterrenceTier.Low, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_low_merciful),
                (true, DeterrenceTier.Low, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_low_cruel),
                (true, DeterrenceTier.Low, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_low_generous),
                (true, DeterrenceTier.Low, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_low_closefisted),
                (true, DeterrenceTier.Low, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_low_calculating),
                (true, DeterrenceTier.Low, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_low_impulsive),
                (true, DeterrenceTier.Low, DeterrenceVoice.Neutral) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_low_neutral),
                (true, DeterrenceTier.Medium, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_medium_honorable),
                (true, DeterrenceTier.Medium, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_medium_dishonorable),
                (true, DeterrenceTier.Medium, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_medium_valorous),
                (true, DeterrenceTier.Medium, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_medium_cautious),
                (true, DeterrenceTier.Medium, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_medium_merciful),
                (true, DeterrenceTier.Medium, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_medium_cruel),
                (true, DeterrenceTier.Medium, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_medium_generous),
                (true, DeterrenceTier.Medium, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_medium_closefisted),
                (true, DeterrenceTier.Medium, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_medium_calculating),
                (true, DeterrenceTier.Medium, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_medium_impulsive),
                (true, DeterrenceTier.Medium, DeterrenceVoice.Neutral) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_medium_neutral),
                (true, DeterrenceTier.High, DeterrenceVoice.HonorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_high_honorable),
                (true, DeterrenceTier.High, DeterrenceVoice.HonorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_high_dishonorable),
                (true, DeterrenceTier.High, DeterrenceVoice.ValorHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_high_valorous),
                (true, DeterrenceTier.High, DeterrenceVoice.ValorLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_high_cautious),
                (true, DeterrenceTier.High, DeterrenceVoice.MercyHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_high_merciful),
                (true, DeterrenceTier.High, DeterrenceVoice.MercyLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_high_cruel),
                (true, DeterrenceTier.High, DeterrenceVoice.GenerosityHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_high_generous),
                (true, DeterrenceTier.High, DeterrenceVoice.GenerosityLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_high_closefisted),
                (true, DeterrenceTier.High, DeterrenceVoice.CalculatingHigh) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_high_calculating),
                (true, DeterrenceTier.High, DeterrenceVoice.CalculatingLow) =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_high_impulsive),
                _ =>
                    GwpText.Get(GwpTextCatalog.gwp_ai_det_audience_warden_high_neutral)
            };
    }
}
