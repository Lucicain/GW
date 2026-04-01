using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    internal static class PoliceShipModelSupport
    {
        internal const float PoliceDesiredCampaignSpeed = 3.5f;

        internal static bool IsPoliceParty(MobileParty? party) =>
            party?.ActualClan != null
            && string.Equals(party.ActualClan.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase);

        internal static bool IsPoliceShip(Ship? ship) => IsPoliceParty(ship?.Owner?.MobileParty);

        internal static TModel CreateFallbackModel<TModel>(string navalTypeName, Func<TModel> defaultFactory)
            where TModel : class
        {
            Type? navalType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(navalTypeName, false))
                .FirstOrDefault(t => t != null && typeof(TModel).IsAssignableFrom(t));

            if (navalType != null && Activator.CreateInstance(navalType) is TModel navalModel)
            {
                return navalModel;
            }

            return defaultFactory();
        }
    }

    public sealed class PoliceShipDamageModel : CampaignShipDamageModel
    {
        private readonly CampaignShipDamageModel _fallback =
            PoliceShipModelSupport.CreateFallbackModel(
                "NavalDLC.GameComponents.NavalDLCCampaignShipDamageModel",
                static () => new DefaultCampaignShipDamageModel());

        public override float GetEstimatedSafeSailDuration(MobileParty mobileParty) =>
            PoliceShipModelSupport.IsPoliceParty(mobileParty)
                ? float.MaxValue
                : _fallback.GetEstimatedSafeSailDuration(mobileParty);

        public override int GetHourlyShipDamage(MobileParty owner, Ship ship) =>
            PoliceShipModelSupport.IsPoliceParty(owner) || PoliceShipModelSupport.IsPoliceShip(ship)
                ? 0
                : _fallback.GetHourlyShipDamage(owner, ship);

        public override float GetShipDamage(Ship ship, Ship rammingShip, float rawDamage) =>
            _fallback.GetShipDamage(ship, rammingShip, rawDamage);
    }

    public sealed class PoliceShipParametersModel : CampaignShipParametersModel
    {
        private readonly CampaignShipParametersModel _fallback =
            PoliceShipModelSupport.CreateFallbackModel(
                "NavalDLC.GameComponents.NavalDLCCampaignShipParametersModel",
                static () => new DefaultCampaignShipParametersModel());

        public override int GetAdditionalAmmoBonus(Ship ship) => _fallback.GetAdditionalAmmoBonus(ship);

        public override int GetAdditionalArcherQuivers(Ship ship) => _fallback.GetAdditionalArcherQuivers(ship);

        public override int GetAdditionalThrowingWeaponStack(Ship ship) => _fallback.GetAdditionalThrowingWeaponStack(ship);

        public override float GetCampaignSpeedBonusFactor(Ship ship) =>
            PoliceShipModelSupport.IsPoliceShip(ship)
                ? (ship?.ShipHull?.BaseSpeed > 0f
                    ? PoliceShipModelSupport.PoliceDesiredCampaignSpeed / ship.ShipHull.BaseSpeed - 1f
                    : 0f)
                : _fallback.GetCampaignSpeedBonusFactor(ship);

        public override float GetCrewCapacityBonusFactor(Ship ship) => _fallback.GetCrewCapacityBonusFactor(ship);

        public override float GetCrewMeleeDamageFactor(Ship ship) => _fallback.GetCrewMeleeDamageFactor(ship);

        public override float GetCrewShieldHitPointsFactor(Ship ship) => _fallback.GetCrewShieldHitPointsFactor(ship);

        public override float GetDefaultCombatFactor(TaleWorlds.Core.ShipHull shipHull) => _fallback.GetDefaultCombatFactor(shipHull);

        public override float GetForwardDragFactor(Ship ship) => _fallback.GetForwardDragFactor(ship);

        public override float GetFurlUnfurlSpeedFactor(Ship ship) => _fallback.GetFurlUnfurlSpeedFactor(ship);

        public override float GetMaxOarForceFactor(Ship ship) => _fallback.GetMaxOarForceFactor(ship);

        public override float GetMaxOarPowerFactor(Ship ship) => _fallback.GetMaxOarPowerFactor(ship);

        public override float GetSailForceFactor(Ship ship) => _fallback.GetSailForceFactor(ship);

        public override float GetSailRotationSpeedFactor(Ship ship) => _fallback.GetSailRotationSpeedFactor(ship);

        public override float GetShipSizeWeatherFactor(TaleWorlds.Core.ShipHull shipHull) => _fallback.GetShipSizeWeatherFactor(shipHull);

        public override float GetShipWeightFactor(Ship ship) => _fallback.GetShipWeightFactor(ship);
    }
}
