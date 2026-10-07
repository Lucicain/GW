using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍兵男女五五混编，只有灰袍领主是女性（用户裁定 2026-10-07：男性负责传教相关，女性负责大局统领）。
    /// 兵种定义仍是女性：头像、百科、部队名单、地图图标按兵种显示；只在战场上每个兵按自己的种子分男女。
    /// 原版生成兵时，AgentBuildData 指定了性别就用指定的，否则用兵种的 is_female；没指定长相就按兵种的脸型范围、
    /// 兵种的性别生成（C#反编译 v1.4.8 Mission.SpawnAgent、BasicCharacterObject.GetBodyProperties）。
    /// 所以男兵同时指定性别和长相：照原版 FaceGen.GetRandomBodyProperties，换成原版帝国士兵的男性脸型范围
    /// fighter_empire（兵种自己的 townswoman_empire 是女性范围，没有胡子；sandboxcore_bodyproperties.xml）。
    /// 排在军械库换装（GwpArmorySpawnPatch）之后，按换好的装备算头发遮挡。
    /// </summary>
    [HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent))]
    [HarmonyPriority(Priority.Low)]
    internal static class GwpWardenTroopGenderPatch
    {
        private const string MaleBodyPropertyId = "fighter_empire";

        private static void Prefix(AgentBuildData agentBuildData)
        {
            if (agentBuildData.AgentCharacter is not CharacterObject troop || !GwpCommon.IsGreyWardenTroop(troop) ||
                agentBuildData.GenderOverriden || agentBuildData.BodyPropertiesOverriden ||
                !IsMale(agentBuildData.AgentEquipmentSeed))
                return;
            MBBodyProperty male = MBObjectManager.Instance.GetObject<MBBodyProperty>(MaleBodyPropertyId);
            Equipment equipment = agentBuildData.AgentOverridenSpawnEquipment ?? troop.FirstBattleEquipment;
            agentBuildData.IsFemale(false).BodyProperties(TaleWorlds.Core.FaceGen.GetRandomBodyProperties(troop.Race, false,
                male.BodyPropertyMin, male.BodyPropertyMax, (int)equipment.HairCoverType, agentBuildData.AgentEquipmentSeed,
                male.HairTags, male.BeardTags, male.TattooTags, 0f));
        }

        /// <summary>同一个兵每次一样：原版按兵的种子定装备和长相（PartyAgentOrigin.Seed），这里也按它分男女，一半一半。</summary>
        private static bool IsMale(int seed) => (unchecked((uint)seed * 2654435761u) >> 16 & 1u) == 1u;
    }
}
