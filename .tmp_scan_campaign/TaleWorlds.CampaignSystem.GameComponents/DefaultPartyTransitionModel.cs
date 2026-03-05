using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.GameComponents;

public class DefaultPartyTransitionModel : PartyTransitionModel
{
	private const float MinHoursToMoveAnchor = 1f;

	private const float MaxHoursToMoveAnchor = 6f;

	private const float InstantEmbarkDistanceThreshold = 20f;

	private const float DisembarkHours = 2f;

	public override CampaignTime GetFleetTravelTimeToPoint(MobileParty mobileParty, CampaignVec2 target)
	{
		CampaignVec2 campaignVec = (mobileParty.Anchor.IsMovingToPoint ? mobileParty.TargetPosition : mobileParty.Anchor.GetInteractionPosition(mobileParty));
		if (campaignVec.IsValid())
		{
			float num = campaignVec.Distance(target);
			if (num < 20f)
			{
				return CampaignTime.Zero;
			}
			float averageDistanceBetweenClosestTwoTownsWithNavigationType = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Naval);
			float num2 = (mobileParty.Anchor.IsMovingToPoint ? ((float)(mobileParty.Anchor.ArrivalTime - CampaignTime.Now).ToHours) : 0f);
			return CampaignTime.Hours(MBMath.ClampFloat(num / averageDistanceBetweenClosestTwoTownsWithNavigationType + num2, 1f, 6f));
		}
		return CampaignTime.Hours(6f);
	}

	public override CampaignTime GetTransitionTimeDisembarking(MobileParty mobileParty)
	{
		if (mobileParty.IsInRaftState)
		{
			return CampaignTime.Zero;
		}
		return CampaignTime.Hours(2f);
	}

	public override CampaignTime GetTransitionTimeForEmbarking(MobileParty mobileParty)
	{
		if (!mobileParty.Anchor.IsValid)
		{
			return CampaignTime.Hours(6f);
		}
		float landRatio;
		float num = ((mobileParty.CurrentSettlement != null) ? Campaign.Current.Models.MapDistanceModel.GetDistance(mobileParty.CurrentSettlement, mobileParty.Anchor.Position, isFromPort: true, MobileParty.NavigationType.Naval) : Campaign.Current.Models.MapDistanceModel.GetDistance(mobileParty, mobileParty.Anchor.GetInteractionPosition(mobileParty), MobileParty.NavigationType.Default, out landRatio));
		if (num < 20f)
		{
			return CampaignTime.Zero;
		}
		float averageDistanceBetweenClosestTwoTownsWithNavigationType = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Naval);
		return CampaignTime.Hours(MBMath.ClampFloat(num / averageDistanceBetweenClosestTwoTownsWithNavigationType, 1f, 6f));
	}
}
