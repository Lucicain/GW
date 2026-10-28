using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    /// <summary>真实支援队名册承担伤亡；在本次搜捕里由玩家指挥。</summary>
    internal sealed class GwpCitySearchSupportOrigin : PartyAgentOrigin, IAgentOriginBase
    {
        internal GwpCitySearchSupportOrigin(PartyBase source, CharacterObject troop) : base(source, troop) { }
        bool IAgentOriginBase.IsUnderPlayersCommand => true;
        bool IAgentOriginBase.IsInSameArmyAsPlayer => true;
    }
}
