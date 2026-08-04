using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    public partial class PlayerBountyBehavior
    {
        private string _bountyCollectionCourierToResumeId = null!;

        private static bool IsBountyCollectionCourier(MobileParty? party) =>
            party?.StringId?.StartsWith(
                GwpIds.BountyCollectionCourierPrefix,
                StringComparison.Ordinal) == true;

        private static List<MobileParty> GetActiveBountyCollectionCouriers() =>
            MobileParty.All
                .Where(party => party?.IsActive == true &&
                                IsBountyCollectionCourier(party))
                .ToList();

        private void UpdateBountyCollectionCouriers()
        {
            List<MobileParty> couriers = GetActiveBountyCollectionCouriers();
            PruneBountyCollectionCourierReturnState(couriers);

            foreach (MobileParty courier in couriers.ToList())
            {
                if (!TryGetBountyCollectionCourierReturnTarget(
                        courier,
                        out Settlement returnTarget))
                    continue;

                UpdateReturningBountyCollectionCourier(courier, returnTarget);
                couriers.Remove(courier);
            }

            if (!IsWaitingForBountyCollection)
            {
                foreach (MobileParty courier in couriers)
                    RecallBountyCollectionCourier(courier);
                return;
            }

            if (_bountyCollectionStartedHours < 0d)
                _bountyCollectionStartedHours = CampaignTime.Now.ToHours;

            double waitingHours = CampaignTime.Now.ToHours -
                                  _bountyCollectionStartedHours;
            if (waitingHours < GwpTuning.Bounty.CollectionCourierDelayDays * 24d)
                return;

            if (couriers.Count == 0)
            {
                MobileParty? spawned = SpawnBountyCollectionCourier();
                if (spawned != null)
                    couriers.Add(spawned);
            }

            MobileParty? player = MobileParty.MainParty;
            if (player?.IsActive != true) return;

            foreach (MobileParty courier in couriers)
            {
                PoliceResourceManager.ProvisionTemporaryDutyParty(courier);
                float distance = courier.GetPosition2D.Distance(player.GetPosition2D);
                if (distance <= GwpTuning.Bounty.RecruitmentContactDistance)
                {
                    GreyWardenPartyDesireBehavior.ClearIntent(courier);
                    courier.Ai.SetDoNotMakeNewDecisions(false);
                    courier.SetMoveEngageParty(player, courier.NavigationCapability);
                }
                else
                {
                    GreyWardenPartyDesireBehavior.RequestApproach(
                        courier,
                        player,
                        8f);
                }
            }
        }

        private MobileParty? SpawnBountyCollectionCourier()
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            MobileParty? player = MobileParty.MainParty;
            if (policeClan == null || player?.IsActive != true) return null;

            Settlement? spawnPoint = GwpCommon.FindNearestTown(player.GetPosition2D);
            if (spawnPoint == null) return null;

            try
            {
                MobileParty courier = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                    spawnPoint.GatePosition,
                    1f,
                    spawnPoint,
                    new TextObject(GwpText.Get(
                        "{=gwp_bounty_collection_courier_name}Grey Warden settlement party")),
                    policeClan,
                    policeClan.DefaultPartyTemplate,
                    null,
                    "",
                    "",
                    5f,
                    false);

                courier.StringId = GwpIds.BountyCollectionCourierPrefix +
                                   MBRandom.RandomInt(10000, 99999);
                courier.ActualClan = policeClan;
                courier.MemberRoster.Clear();

                CharacterObject? knight = CharacterObject.Find(GwpIds.KnightId);
                CharacterObject? infantry = CharacterObject.Find(GwpIds.HeavyInfantryId);
                CharacterObject? troop = knight ?? infantry;
                if (troop != null)
                    courier.MemberRoster.AddToCounts(
                        troop,
                        GwpTuning.Bounty.CollectionCourierPatrolSize);

                PoliceResourceManager.ProvisionTemporaryDutyParty(courier);
                PoliceResourceManager.GivePoliceShips(courier);
                GreyWardenPartyDesireBehavior.RequestApproach(courier, player, 8f);

                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get(
                        "{=gwp_bounty_collection_courier_dispatched}Five days have passed since the quarry fell. A Grey Warden settlement party has been sent from {VAR_1} to find you and close the warrant.",
                        "VAR_1", spawnPoint.Name),
                    Colors.Cyan));
                GwpAiDiagnostics.WriteAction(
                    courier,
                    "BOUNTY_COLLECTION_COURIER_DISPATCHED",
                    "reward=" + _activeBountyReward +
                    "; waitingHours=" + waitingHoursText());
                return courier;
            }
            catch
            {
                return null;
            }

            string waitingHoursText() =>
                (CampaignTime.Now.ToHours - _bountyCollectionStartedHours)
                .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void RecallBountyCollectionCouriers(MobileParty? preserved = null)
        {
            foreach (MobileParty courier in GetActiveBountyCollectionCouriers())
            {
                if (preserved != null && ReferenceEquals(courier, preserved))
                    continue;
                if (IsReturningBountyCollectionCourier(courier))
                    continue;
                RecallBountyCollectionCourier(courier);
            }
        }

        private void RecallBountyCollectionCourier(MobileParty courier)
        {
            Settlement? returnTarget = GwpCommon.FindNearestTown(
                courier.GetPosition2D);
            if (returnTarget == null)
            {
                DestroyBountyCollectionCourier(courier);
                return;
            }

            RememberBountyCollectionCourierReturn(courier, returnTarget);
            ApplyBountyCollectionCourierReturn(courier, returnTarget);
            GwpAiDiagnostics.WriteAction(
                courier,
                "BOUNTY_COLLECTION_COURIER_RECALLED",
                "returnTarget=" + returnTarget.StringId);
        }

        private void DestroyBountyCollectionCourier(MobileParty? courier)
        {
            if (courier == null) return;
            ForgetBountyCollectionCourierReturn(courier.StringId);
            if (courier.IsActive != true) return;
            try
            {
                GreyWardenPartyDesireBehavior.ClearIntent(courier);
                DestroyPartyAction.Apply(null, courier);
            }
            catch { }
        }

        private void CloseBountyCollectionCourierEncounterAndReturn(
            MobileParty courier)
        {
            Settlement? returnTarget = GwpCommon.FindNearestTown(
                courier.GetPosition2D);
            if (returnTarget != null)
            {
                RememberBountyCollectionCourierReturn(courier, returnTarget);
                ApplyBountyCollectionCourierReturn(courier, returnTarget);
            }

            _bountyCollectionCourierToResumeId = courier.StringId;
            GwpAiDiagnostics.WriteAction(
                courier,
                "BOUNTY_COLLECTION_COURIER_PAYMENT_COMPLETE",
                "reward=" + _activeBountyReward +
                "; returnTarget=" + (returnTarget?.StringId ?? "none"));

            if (PlayerEncounter.IsActive)
                PlayerEncounter.LeaveEncounter = true;

            if (Campaign.Current?.ConversationManager == null)
            {
                FinishBountyCollectionCourierEncounterAndReturn();
                return;
            }

            Campaign.Current.ConversationManager.ConversationEndOneShot -=
                FinishBountyCollectionCourierEncounterAndReturn;
            Campaign.Current.ConversationManager.ConversationEndOneShot +=
                FinishBountyCollectionCourierEncounterAndReturn;
        }

        private void ResumeBountyCollectionCourierEncounterAndReturn(
            MobileParty courier)
        {
            _bountyCollectionCourierToResumeId = courier.StringId;
            if (PlayerEncounter.IsActive)
                PlayerEncounter.LeaveEncounter = true;

            if (Campaign.Current?.ConversationManager == null)
            {
                FinishBountyCollectionCourierEncounterAndReturn();
                return;
            }

            Campaign.Current.ConversationManager.ConversationEndOneShot -=
                FinishBountyCollectionCourierEncounterAndReturn;
            Campaign.Current.ConversationManager.ConversationEndOneShot +=
                FinishBountyCollectionCourierEncounterAndReturn;
        }

        private void FinishBountyCollectionCourierEncounterAndReturn()
        {
            try
            {
                MobileParty? encountered = PlayerEncounter.IsActive
                    ? PlayerEncounter.EncounteredMobileParty
                    : null;
                if (IsBountyCollectionCourier(encountered))
                {
                    PlayerEncounter.LeaveEncounter = true;
                    PlayerEncounter.Finish();
                }
            }
            catch { }

            MobileParty? courier = MobileParty.All.FirstOrDefault(party =>
                string.Equals(
                    party.StringId,
                    _bountyCollectionCourierToResumeId,
                    StringComparison.OrdinalIgnoreCase));
            if (courier != null &&
                TryGetBountyCollectionCourierReturnTarget(
                    courier,
                    out Settlement returnTarget))
            {
                ApplyBountyCollectionCourierReturn(courier, returnTarget);
            }

            _bountyCollectionCourierToResumeId = null!;
        }

        private void UpdateReturningBountyCollectionCourier(
            MobileParty courier,
            Settlement returnTarget)
        {
            if (courier.CurrentSettlement == returnTarget ||
                courier.GetPosition2D.Distance(returnTarget.GetPosition2D) < 3f)
            {
                GwpAiDiagnostics.WriteAction(
                    courier,
                    "BOUNTY_COLLECTION_COURIER_RETURN_COMPLETE",
                    "returnTarget=" + returnTarget.StringId);
                DestroyBountyCollectionCourier(courier);
                return;
            }

            PoliceResourceManager.ProvisionTemporaryDutyParty(courier);
            ApplyBountyCollectionCourierReturn(courier, returnTarget);
        }

        private static void ApplyBountyCollectionCourierReturn(
            MobileParty courier,
            Settlement returnTarget)
        {
            GreyWardenPartyDesireBehavior.RequestVisit(courier, returnTarget, 8f);
            try
            {
                courier.Ai.SetDoNotAttackMainParty(2);
                courier.Ai.SetDoNotMakeNewDecisions(false);
                courier.SetMoveGoToSettlement(
                    returnTarget,
                    courier.NavigationCapability,
                    false);
            }
            catch (Exception ex)
            {
                GwpAiDiagnostics.WriteAction(
                    courier,
                    "BOUNTY_COLLECTION_COURIER_NATIVE_RETURN_FAILED",
                    ex.GetType().Name + ":" + ex.Message);
            }
        }

        private bool IsReturningBountyCollectionCourier(MobileParty? courier) =>
            courier != null &&
            TryGetBountyCollectionCourierReturnTarget(
                courier,
                out Settlement _);

        private bool TryGetBountyCollectionCourierReturnTarget(
            MobileParty courier,
            out Settlement returnTarget)
        {
            returnTarget = null!;
            Dictionary<string, string> states =
                ReadBountyCollectionCourierReturnState();
            if (!states.TryGetValue(courier.StringId, out string settlementId))
                return false;

            returnTarget = Settlement.All.FirstOrDefault(settlement =>
                string.Equals(
                    settlement.StringId,
                    settlementId,
                    StringComparison.OrdinalIgnoreCase));
            if (returnTarget != null) return true;

            Settlement? fallbackTarget = GwpCommon.FindNearestTown(
                courier.GetPosition2D);
            if (fallbackTarget == null)
            {
                ForgetBountyCollectionCourierReturn(courier.StringId);
                return false;
            }

            returnTarget = fallbackTarget;
            RememberBountyCollectionCourierReturn(courier, returnTarget);
            return true;
        }

        private void RememberBountyCollectionCourierReturn(
            MobileParty courier,
            Settlement returnTarget)
        {
            Dictionary<string, string> states =
                ReadBountyCollectionCourierReturnState();
            states[courier.StringId] = returnTarget.StringId;
            WriteBountyCollectionCourierReturnState(states);
        }

        private void ForgetBountyCollectionCourierReturn(string? courierId)
        {
            if (string.IsNullOrEmpty(courierId)) return;
            Dictionary<string, string> states =
                ReadBountyCollectionCourierReturnState();
            if (!states.Remove(courierId!)) return;
            WriteBountyCollectionCourierReturnState(states);
        }

        private void PruneBountyCollectionCourierReturnState(
            IEnumerable<MobileParty> activeCouriers)
        {
            HashSet<string> activeIds = new HashSet<string>(
                activeCouriers.Select(courier => courier.StringId),
                StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> states =
                ReadBountyCollectionCourierReturnState();
            List<string> staleIds = states.Keys
                .Where(id => !activeIds.Contains(id))
                .ToList();
            foreach (string staleId in staleIds)
                states.Remove(staleId);
            if (staleIds.Count > 0)
                WriteBountyCollectionCourierReturnState(states);
        }

        private Dictionary<string, string>
            ReadBountyCollectionCourierReturnState()
        {
            Dictionary<string, string> states =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(
                    _bountyCollectionCourierReturnState))
                return states;

            foreach (string entry in
                     _bountyCollectionCourierReturnState.Split(';'))
            {
                string[] parts = entry.Split('|');
                if (parts.Length != 2 ||
                    string.IsNullOrWhiteSpace(parts[0]) ||
                    string.IsNullOrWhiteSpace(parts[1]))
                    continue;
                states[parts[0]] = parts[1];
            }

            return states;
        }

        private void WriteBountyCollectionCourierReturnState(
            Dictionary<string, string> states)
        {
            _bountyCollectionCourierReturnState = string.Join(
                ";",
                states.OrderBy(pair => pair.Key)
                    .Select(pair => pair.Key + "|" + pair.Value));
        }
    }
}
