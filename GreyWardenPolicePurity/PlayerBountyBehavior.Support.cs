using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    public partial class PlayerBountyBehavior
    {
        private bool _supportRequested;

        /// <summary>
        /// 支援尚未成立。求援送到之前，灰袍不替玩家开战，也不去追罪犯；
        /// 送到之后走的就是普通案件那一套战力判定。
        /// </summary>
        internal static bool AwaitingSupportRequest(PoliceTask? task)
        {
            if (task?.IsPlayerBountyEscort != true) return false;
            var current = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            return current == null || !current._supportRequested;
        }

        internal static bool IsReservedTarget(MobileParty? target)
        {
            var current = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            if (target == null || current == null || current._supportRequested || !current.IsTrackingBountyTarget) return false;
            var offender = current.CaseHero?.PartyBelongedTo;
            return target.StringId == current._activeBountyTargetId || target == offender
                || (offender?.Army?.LeaderParty != null && target == offender.Army.LeaderParty);
        }

        internal bool CanRequestCaseSupport =>
            IsTrackingBountyTarget && !_supportRequested && !HasCompletedEnforcement;
        internal bool HasRequestedCaseSupport => _supportRequested;

        /// <summary>
        /// 求援成立。无论是玩家当面开口，还是派去的士兵把话带到，都是同一件事：
        /// 从这一刻起灰袍才有权为这宗案子开战。到场的部队仍然跟着玩家走。
        /// </summary>
        internal bool GrantCaseSupport(string source, MobileParty? responder)
        {
            if (!CanRequestCaseSupport) return false;

            MobileParty? lord = ResolveSupportResponder(responder);
            CrimeRecord? crime = CrimePool.LedgerRecords.FirstOrDefault(record =>
                record.HasOpenCase && string.Equals(record.OffenderHeroId,
                    _activeBountyTargetHeroId, StringComparison.OrdinalIgnoreCase));
            if (lord == null || crime == null)
            {
                GwpAiDiagnostics.WriteFieldArrest("CASE_SUPPORT_NO_RESPONDER",
                    "source=" + source + "; lord=" + (lord?.StringId ?? "-")
                    + "; crimeOpen=" + (crime != null));
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_support_no_responder}No Grey Warden lord is free to answer the request just now."),
                    Colors.Yellow));
                return false;
            }

            // 求援送到之后，灰袍才正式派这名领主接手这宗案子的支援职责。先把他手上的事
            // 全部放下：退出别人的协力组、脱离军团、清掉旧意图，否则他的欲望仍旧指向
            // 原来那个目标，看上去就是"接了却不跟玩家"。他自己原本承办的案子由
            // BeginTask 重新放回案件池，交给别的灰袍去接。
            PoliceEnforcementBehavior.ReleasePartyFromAssistance(
                lord.StringId, "reassigned_to_player_support");
            CrimeState.BeginTask(lord.StringId, crime);
            if (CrimeState.GetTask(lord.StringId)?.TargetCrimeId != crime.CrimeId)
            {
                GwpAiDiagnostics.WriteFieldArrest("CASE_SUPPORT_ASSIGN_FAILED",
                    "source=" + source + "; lord=" + lord.StringId + "; crime=" + crime.CrimeId);
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_support_no_responder}No Grey Warden lord is free to answer the request just now."),
                    Colors.Yellow));
                return false;
            }

            CrimeState.SetBountyEscortFlag(lord.StringId, true);
            _escortPolicePartyId = lord.StringId;
            _supportRequested = true;
            _activeQuest?.WriteLog(GwpText.Create(
                "{=gwp_support_requested_log}You asked the Wardens to take the field beside you against the offender."));
            GwpAiDiagnostics.WriteFieldArrest("CASE_SUPPORT_GRANTED",
                "source=" + source + "; responder=" + lord.StringId + "; crime=" + crime.CrimeId);
            PoliceEnforcementBehavior.RefreshPlayerBountyAssistanceEscort(_escortPolicePartyId);
            PoliceEnforcementBehavior.RefreshPlayerBountyCaseContact(_escortPolicePartyId);
            UpdateEscortPatrol();
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_support_granted_notice}{VAR_1} has taken up your request and is riding to join you.",
                "VAR_1", lord.Name.ToString()), Colors.Cyan));
            return true;
        }

        /// <summary>
        /// 接下求援的人：优先是使者真正见到的那名领主；他已经不合适时，改由离玩家最近的
        /// 灰袍领主接手。不凭空生成部队。
        /// </summary>
        /// <summary>
        /// 接下求援的就是使者真正见到的那名领主——他当时在忙什么都得放下。只有他已经不在
        /// （阵亡、被俘、部队没了）时，才退而求其次找离玩家最近的合格灰袍领主。
        /// </summary>
        private static MobileParty? ResolveSupportResponder(MobileParty? responder)
        {
            if (IsEligibleSupportResponder(responder)) return responder;

            MobileParty? player = MobileParty.MainParty;
            if (player?.IsActive != true) return null;
            return MobileParty.All
                .Where(IsEligibleSupportResponder)
                .OrderBy(party => party!.GetPosition2D.Distance(player.GetPosition2D))
                .FirstOrDefault();
        }

        private static bool IsEligibleSupportResponder(MobileParty? party)
        {
            Clan? wardens = PoliceStats.GetPoliceClan();
            return party?.IsActive == true && party.LeaderHero != null &&
                   wardens != null && party.ActualClan != null &&
                   string.Equals(party.ActualClan.StringId, wardens.StringId,
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 灰袍为这宗案子开战时，玩家阵营一并对犯人宣战——请来的援军和玩家打的是
        /// 同一场。结案时沿用既有的调停恢复和平，不会留下一场永久的战争。
        /// </summary>
        internal void NotifySupportWarDeclared(MobileParty? criminal)
        {
            if (!HasBountyTask || criminal == null) return;
            try
            {
                IFaction? playerFaction = Hero.MainHero?.MapFaction;
                IFaction? criminalFaction = criminal.MapFaction;
                if (playerFaction == null || criminalFaction == null ||
                    playerFaction == criminalFaction) return;

                Clan? criminalClan = criminal.ActualClan;
                if (criminalClan != null && criminalClan.IsOutlaw && criminalClan.IsBanditFaction) return;

                _bountyTargetEncounterStarted = true;
                if (FactionManager.IsAtWarAgainstFaction(playerFaction, criminalFaction))
                {
                    // 接案之前就已经在打的战争不是这宗案子造成的，结案时也不该由它来收。
                    _playerFactionWasAtWarWhenBountyAccepted = true;
                    return;
                }

                DeclareWarAction.ApplyByDefault(playerFaction, criminalFaction);
                GwpAiDiagnostics.WriteFieldArrest("CASE_SUPPORT_PLAYER_WAR",
                    "playerFaction=" + playerFaction.StringId +
                    "; criminalFaction=" + criminalFaction.StringId);
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_support_player_war}The Wardens have declared against {VAR_1}, and your own banner now stands with them.",
                    "VAR_1", criminalFaction.Name), Colors.Cyan));
            }
            catch (Exception ex)
            {
                GwpAiDiagnostics.WriteFieldArrest("CASE_SUPPORT_PLAYER_WAR_FAILED", ex.ToString());
            }
        }

        private void RegisterSupportDialogue(CampaignGameStarter starter)
        {
            // 当面开口和派人送信是同一件事：话到了灰袍领主耳朵里，支援才成立。
            starter.AddPlayerLine("gwp_request_case_support", "lord_talk_speak_diplomacy_2", "gwp_request_case_support_reply",
                GwpText.Get("{=gwp_request_case_support}I need your help taking the offender. Take the field with me."),
                () => CanRequestCaseSupport && IsOrdinaryGreyWardenLordConversation(), null, 110);
            starter.AddDialogLine("gwp_request_case_support_reply", "gwp_request_case_support_reply", "lord_talk_speak_diplomacy_2",
                GwpText.Get("{=gwp_request_case_support_reply}Then we march with you. We will strike when the odds on the field are ours."), null,
                () => GrantCaseSupport("player_in_person", MobileParty.ConversationParty));
        }
    }
}
