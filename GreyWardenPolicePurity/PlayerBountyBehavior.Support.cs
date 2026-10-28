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

        internal static bool IsBountyReservedTarget(MobileParty? target)
        {
            var current = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            if (target == null || current == null || current._supportRequested || !current.IsTrackingBountyTarget) return false;
            var offender = current.CaseHero?.PartyBelongedTo;
            return target.StringId == current._activeBountyTargetId || target == offender
                || (offender?.Army?.LeaderParty != null && target == offender.Army.LeaderParty);
        }

        internal bool CanRequestCaseSupport =>
            IsTrackingBountyTarget && !_supportRequested && !HasCompletedEnforcement;

        /// <summary>
        /// 求援成立。无论是玩家当面开口，还是派去的士兵把话带到，都是同一件事：
        /// 从这一刻起灰袍才有权为这宗案子开战。到场的部队仍然跟着玩家走。
        /// </summary>
        internal bool GrantCaseSupport(string source, MobileParty? responder)
        {
            if (!CanRequestCaseSupport) return false;

            CrimeRecord? crime = CrimePool.LedgerRecords.FirstOrDefault(record =>
                record.HasOpenCase && string.Equals(record.OffenderHeroId,
                    _activeBountyTargetHeroId, StringComparison.OrdinalIgnoreCase));
            // 住城目标只能进城搜捕：领主本人不跟，由他分出同催缴编制的黑甲队跟着玩家（用户裁定 2026-10-28）。
            if (crime != null && CrimeRecord.ShelterOf(CaseHero) != null)
                return GrantCitySearchSupport(source, crime, responder);

            MobileParty? lord = ResolveSupportResponder(responder);
            if (lord == null || crime == null ||
                !PoliceEnforcementBehavior.TryReservePartyForPlayerRequest(lord))
            {
                GwpAiDiagnostics.WriteFieldArrest("CASE_SUPPORT_NO_RESPONDER",
                    "source=" + source + "; lord=" + (lord?.StringId ?? "-")
                    + "; crimeOpen=" + (crime != null));
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    GwpTextCatalog.gwp_support_no_responder),
                    Colors.Yellow));
                return false;
            }

            // 同一玩家准备入口已经处理了协力退出、旧案退回和意图交接。
            CrimeState.BeginTask(lord.StringId, crime);
            if (CrimeState.GetTask(lord.StringId)?.TargetCrimeId != crime.CrimeId)
            {
                GwpAiDiagnostics.WriteFieldArrest("CASE_SUPPORT_ASSIGN_FAILED",
                    "source=" + source + "; lord=" + lord.StringId + "; crime=" + crime.CrimeId);
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    GwpTextCatalog.gwp_support_no_responder),
                    Colors.Yellow));
                return false;
            }

            CrimeState.SetBountyEscortFlag(lord.StringId, true);
            _escortPolicePartyId = lord.StringId;
            _supportRequested = true;
            _activeQuest?.WriteLog(GwpText.Create(
                GwpTextCatalog.gwp_support_requested_log));
            GwpAiDiagnostics.WriteFieldArrest("CASE_SUPPORT_GRANTED",
                "source=" + source + "; responder=" + lord.StringId + "; crime=" + crime.CrimeId);
            PoliceEnforcementBehavior.RefreshPlayerBountyAssistanceEscort(_escortPolicePartyId);
            PoliceEnforcementBehavior.RefreshPlayerBountyCaseContact(_escortPolicePartyId);
            UpdateEscortPatrol();
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                GwpTextCatalog.gwp_support_granted_notice,
                "VAR_1", lord.Name.ToString()), Colors.Cyan));
            return true;
        }

        private bool GrantCitySearchSupport(string source, CrimeRecord crime, MobileParty? responder)
        {
            MobileParty? sender = Campaign.Current.GetCampaignBehavior<PoliceEnforcementBehavior>()
                ?.SendPlayerCitySearchParty(crime, responder);
            GwpAiDiagnostics.WriteFieldArrest(sender == null ? "CASE_SUPPORT_NO_RESPONDER" : "CASE_SUPPORT_GRANTED",
                "source=" + source + "; city=true; responder=" + (sender?.StringId ?? "-") + "; crime=" + crime.CrimeId);
            if (sender == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    GwpTextCatalog.gwp_support_no_responder), Colors.Yellow));
                return false;
            }
            _supportRequested = true;
            _activeQuest?.WriteLog(GwpText.Create(GwpTextCatalog.gwp_support_requested_log));
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                GwpTextCatalog.gwp_cs_support_sent), Colors.Cyan));
            return true;
        }

        /// <summary>
        /// 接下求援的人：优先是使者真正见到的那名领主；他已经不合适时，改由离玩家最近的
        /// 灰袍领主接手。不凭空生成部队。
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
                   party.MapEvent is not { IsFinalized: false } &&
                   GwpWardenDuties.IsFreeFor(party, WardenDuty.PlayerEscort) &&
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
            Clan? criminalClan = criminal.ActualClan;
            if (criminalClan != null && criminalClan.IsOutlaw && criminalClan.IsBanditFaction) return;
            DeclarePlayerCaseWar(criminal.MapFaction, "case_support_declaration",
                GwpTextCatalog.gwp_support_player_war);
        }

        /// <summary>
        /// 玩家为本案开战的唯一入口（求援成立、进城搜捕动手）：玩家阵营对对方势力宣战，
        /// 记进调停申请通道，结案时不再替玩家自动讲和。接案前就在打的战争不算本案造成。
        /// </summary>
        internal void DeclarePlayerCaseWar(IFaction? target, string reason, string noticeKey)
        {
            if (!HasBountyTask) return;
            IFaction? playerFaction = Hero.MainHero?.MapFaction;
            if (playerFaction == null || target == null || playerFaction == target) return;

            _bountyTargetEncounterStarted = true;
            if (FactionManager.IsAtWarAgainstFaction(playerFaction, target))
            {
                // 接案之前就已经在打的战争不是这宗案子造成的，结案时也不该由它来收。
                _playerFactionWasAtWarWhenBountyAccepted = true;
                return;
            }

            DeclareWarAction.ApplyByDefault(playerFaction, target);
            PoliceAntiWarDeclaration.RecordMediationRequest(target, reason);
            GwpAiDiagnostics.WriteFieldArrest("CASE_PLAYER_WAR", "reason=" + reason +
                "; playerFaction=" + playerFaction.StringId + "; targetFaction=" + target.StringId);
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                noticeKey, "VAR_1", target.Name), Colors.Cyan));
        }

        private void RegisterSupportDialogue(CampaignGameStarter starter)
        {
            // 当面开口和派人送信是同一件事：话到了灰袍领主耳朵里，支援才成立。
            starter.AddPlayerLine("gwp_request_case_support", "lord_talk_speak_diplomacy_2", "gwp_request_case_support_reply",
                GwpText.Get(GwpTextCatalog.gwp_request_case_support),
                () => CanRequestCaseSupport && GwpCommon.IsOrdinaryGreyWardenLordConversation(), null, 110);
            starter.AddDialogLine("gwp_request_case_support_reply", "gwp_request_case_support_reply", "lord_talk_speak_diplomacy_2",
                GwpText.Get(GwpTextCatalog.gwp_request_case_support_reply), null,
                () => GrantCaseSupport("player_in_person", MobileParty.ConversationParty));
        }
    }
}
