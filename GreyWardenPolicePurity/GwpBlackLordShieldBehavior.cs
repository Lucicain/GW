using System;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Colors the lord-only shield while the engine is assembling WeaponData.
    /// At this point the View module has already supplied private MetaMesh
    /// copies, but no player/AI weapon entity (including a batched AI entity)
    /// has been created from them yet.
    /// </summary>
    [HarmonyPatch(
        typeof(MissionWeapon),
        nameof(MissionWeapon.GetWeaponData),
        typeof(bool))]
    internal static class GwpBlackLordShieldWeaponDataPatch
    {
        private const string BlackGoldMaterialResource = "gwp_black";

        // Used only if the standalone black/gold texture cannot be loaded.
        // It is deliberately much lighter than the old 0x18 near-black tint.
        private const uint FallbackCharcoalColor = 0xFF48403Au;
        private const uint WhiteFactorColor = 0xFFFFFFFFu;
        private static readonly object MaterialLock = new object();
        private static bool _materialLoadAttempted;
        private static Material? _blackGoldMaterial;

        [HarmonyPostfix]
        private static void AfterGetWeaponData(
            ref MissionWeapon __instance,
            ref WeaponData __result)
        {
            if (__instance.Item == null
                || __instance.Item.StringId
                    != GwpIds.BlackLargeShieldItemId
                || !__result.IsValid())
            {
                return;
            }

            ApplyBlackColor(__result.WeaponMesh);
            ApplyBlackColor(__result.HolsterMesh);
            ApplyBlackColor(__result.HolsterMeshWithWeapon);
            ApplyBlackColor(__result.FlyingMesh);
        }

        private static void ApplyBlackColor(MetaMesh? metaMesh)
        {
            if (metaMesh == null || !metaMesh.IsValid)
                return;

            EnsureBlackGoldMaterial();

            bool hasBlackGoldMaterial = _blackGoldMaterial != null
                && _blackGoldMaterial.IsValid;
            if (hasBlackGoldMaterial)
            {
                metaMesh.SetMaterial(_blackGoldMaterial!);
            }

            uint factorColor = hasBlackGoldMaterial
                ? WhiteFactorColor
                : FallbackCharcoalColor;
            metaMesh.SetFactor1(factorColor);
            metaMesh.SetFactor2(factorColor);

            // Match AgentVisuals' factor-color handling so both regular and
            // batched AI weapon visuals retain the intended neutral color.
            for (int index = 0; index < metaMesh.MeshCount; index++)
            {
                Mesh? mesh = metaMesh.GetMeshAtIndex(index);
                if (mesh == null)
                    continue;

                try
                {
                    if (!mesh.IsValid)
                        continue;
                    mesh.Color = factorColor;
                    mesh.Color2 = factorColor;
                }
                finally
                {
                    mesh.ManualInvalidate();
                }
            }
        }

        private static void EnsureBlackGoldMaterial()
        {
            if (_materialLoadAttempted)
                return;

            lock (MaterialLock)
            {
                if (_materialLoadAttempted)
                    return;

                _materialLoadAttempted = true;
                try
                {
                    // Load the complete editor-authored PBR material. This
                    // preserves its diffuse, normal, packed specular map,
                    // coefficients and shader flags as one resource instead
                    // of rebuilding part of the material at runtime.
                    _blackGoldMaterial = Material.GetFromResource(
                        BlackGoldMaterialResource);
                    if (_blackGoldMaterial == null
                        || !_blackGoldMaterial.IsValid)
                    {
                        throw new InvalidOperationException(
                            "black/gold material resource is invalid");
                    }

                    Texture diffuse = _blackGoldMaterial.GetTexture(
                        Material.MBTextureType.DiffuseMap);
                    Texture normal = _blackGoldMaterial.GetTexture(
                        Material.MBTextureType.BumpMap);
                    Texture specular = _blackGoldMaterial.GetTexture(
                        Material.MBTextureType.SpecularMap);
                    if (diffuse == null || !diffuse.IsValid
                        || normal == null || !normal.IsValid
                        || specular == null || !specular.IsValid)
                    {
                        throw new InvalidOperationException(
                            "black/gold material does not contain all three PBR textures");
                    }

                }
                catch
                {
                    _blackGoldMaterial = null;
                }
            }
        }
    }
}
