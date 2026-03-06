using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GreyWardenPolicePurity
{
    public partial class PoliceEnforcementBehavior
    {
        // 对话临时变量（警察执法拦截：让玩家选择缴纳或战斗）
        private int _dialogFine = 0;
        private MobileParty _dialogPolice = null!;
        private PoliceTask _dialogTask = null!;
        private bool _enforcementBarterInProgress = false;
        private bool _enforcementAtonementAssigned = false;

        #region 对话系统（执法拦截：玩家可选择缴纳罚金或战斗）

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddDialogLine(
                "gwp_enforcement_start",
                "start",
                "gwp_enforcement_options",
                "{" + GwpTextKeys.EnforcementGreeting + "}",
                EnforcementDialogCondition,
                null,
                100);

            starter.AddPlayerLine(
                "gwp_enforcement_pay",
                "gwp_enforcement_options",
                "gwp_enforcement_pay_barter_pre",
                "{" + GwpTextKeys.EnforcementPayText + "}",
                EnforcementPayCondition,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_pre",
                "gwp_enforcement_pay_barter_pre",
                "gwp_enforcement_pay_barter_screen",
                "按灰袍法令，先把正式罚金缴清。",
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_screen",
                "gwp_enforcement_pay_barter_screen",
                "gwp_enforcement_pay_barter_post",
                "{=!}Barter screen goes here",
                null,
                OnEnforcementPayBarterConsequence,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_post_success",
                "gwp_enforcement_pay_barter_post",
                "close_window",
                "罚金确认。本轮案件结案，你可以离开了。",
                EnforcementBarterSuccessfulCondition,
                OnEnforcementPayAcceptedConsequence,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_post_failed",
                "gwp_enforcement_pay_barter_post",
                "gwp_enforcement_options",
                "你的出价低于正式罚金。你可以继续出价，或拒绝执法。",
                () => !EnforcementBarterSuccessfulCondition(),
                OnEnforcementPayRejectedConsequence,
                100);

            starter.AddPlayerLine(
                "gwp_enforcement_atonement",
                "gwp_enforcement_options",
                "gwp_enforcement_atonement_result",
                "我认罪认罚，请给我赎罪任务。",
                EnforcementAtonementCondition,
                OnEnforcementAtonementConsequence,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_atonement_success",
                "gwp_enforcement_atonement_result",
                "close_window",
                "{" + GwpTextKeys.EnforcementAtonementText + "}",
                () => _enforcementAtonementAssigned,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_atonement_failed",
                "gwp_enforcement_atonement_result",
                "gwp_enforcement_options",
                "当前无法分配赎罪任务。你可以继续缴纳罚金，或拒绝执法。",
                () => !_enforcementAtonementAssigned,
                null,
                100);

            starter.AddPlayerLine(
                "gwp_enforcement_fight",
                "gwp_enforcement_options",
                "gwp_enforcement_fight_response",
                "拒绝执法，开战。",
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_fight_response",
                "gwp_enforcement_fight_response",
                "close_window",
                "拒捕记录在案。灰袍守卫，执行抓捕！",
                null,
                OnEnforcementFightConsequence,
                100);

            starter.AddPlayerLine(
                "gwp_enforcement_atonement_turnin",
                "lord_talk_speak_diplomacy_2",
                "gwp_enforcement_atonement_turnin_response",
                "{GWP_ENFORCEMENT_ATONEMENT_TURNIN_OPTION}",
                EnforcementAtonementTurnInCondition,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_atonement_turnin_response",
                "gwp_enforcement_atonement_turnin_response",
                "lord_pretalk",
                "{GWP_ENFORCEMENT_ATONEMENT_TURNIN_TEXT}",
                null,
                OnEnforcementAtonementTurnInConsequence,
                100);

            PlayerState.SetAtonementTaskActive(HasAtonementTask);
            TryRestoreAtonementQuestOnSessionStart();
        }

        private bool EnforcementDialogCondition()
        {
            MobileParty? conversationParty = MobileParty.ConversationParty;
            if (conversationParty == null) return false;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return false;

            if (conversationParty.ActualClan != policeClan) return false;
            if (GwpCommon.IsPatrolParty(conversationParty)) return false;

            PoliceTask? task = CrimeState.GetTask(conversationParty.StringId);
            if (task == null) return false;
            if (task.TargetCrime?.Offender?.IsMainParty != true) return false;
            if (task.FlowState != PoliceTaskFlowState.Pursuit) return false;

            int rep = PlayerState.Reputation;
            _dialogFine = Math.Abs(rep) * 300;
            _dialogPolice = conversationParty;
            _dialogTask = task;

            int playerGold = Hero.MainHero.Gold;
            string payInfo = playerGold >= _dialogFine
                ? $"你当前携带 {playerGold} 金，可直接缴清。"
                : $"你当前携带 {playerGold} 金，可在谈判界面继续出价，或改选认罪认罚。";

            MBTextManager.SetTextVariable(GwpTextKeys.EnforcementGreeting,
                $"站住！灰袍守卫正在执行逮捕。你当前负声望 {Math.Abs(rep)}，" +
                $"本案正式罚金 {_dialogFine} 金。{payInfo}");

            return true;
        }

        private bool EnforcementPayCondition()
        {
            MBTextManager.SetTextVariable(
                GwpTextKeys.EnforcementPayText,
                $"缴纳正式罚金（{_dialogFine} 金，清除通缉）");
            return true;
        }

        private bool EnforcementAtonementCondition()
        {
            if (HasAtonementTask) return false;
            if (_dialogFine <= 0) return false;
            return Hero.MainHero.Gold < _dialogFine;
        }

        private void OnEnforcementAtonementConsequence()
        {
            _enforcementAtonementAssigned = TryAssignAtonementTask();
        }

        private bool TryAssignAtonementTask()
        {
            if (HasAtonementTask) return false;

            CrimeRecord? targetCrime = CrimeState.GetNearestNonPlayerFromAll(
                MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero);
            if (targetCrime == null || targetCrime.Offender == null || !targetCrime.Offender.IsActive)
                return false;

            MobileParty offender = targetCrime.Offender;
            int targetSizeSnapshot = Math.Max(1, offender.Party?.NumberOfAllMembers ?? 1);
            int rewardRep = Math.Max(1, (int)Math.Ceiling(targetSizeSnapshot / 10f));

            SetAtonementFlowState(AtonementFlowState.Active);
            _atonementTargetPartyId = offender.StringId ?? string.Empty;
            _atonementTargetName = offender.Name?.ToString() ?? "未知目标";
            _atonementTargetFactionId = offender.MapFaction?.StringId ?? string.Empty;
            _atonementTargetSizeSnapshot = targetSizeSnapshot;
            _atonementReputationReward = rewardRep;
            _atonementDeadlineHours = (float)(CampaignTime.Now.ToHours + GwpTuning.Enforcement.AtonementDeadlineDays * 24f);
            _lastAtonementIntelReportTime = CampaignTime.Now;
            StartAtonementQuest();

            CrimeState.EndPlayerHunt();
            if (_dialogPolice != null && _dialogPolice.IsActive)
            {
                RestoreAi(_dialogPolice);
                PoliceResourceManager.StartResupply(_dialogPolice);
                PoliceResourceManager.ForceImmediateMoveToResupply(_dialogPolice);
            }
            MakePeaceWithPoliceAndVictims();

            MBTextManager.SetTextVariable(GwpTextKeys.EnforcementAtonementText,
                $"赎罪任务已下达：追捕 {_atonementTargetName}（接案规模 {targetSizeSnapshot} 人）。" +
                $"完成可恢复最多 {_atonementReputationReward} 点声望（最高恢复到 0）；" +
                $"失败将追加 5 点负声望。");

            InformationManager.DisplayMessage(new InformationMessage(
                $"赎罪任务已记录到任务面板：击败 {_atonementTargetName} 后，向族长或任意灰袍警察交付（{GwpTuning.Enforcement.AtonementDeadlineDays:0} 天内，失败声望 -5）。",
                Colors.Yellow));

            try { GwpCommon.TryFinishPlayerEncounter(); } catch { }
            return true;
        }

        private bool EnforcementBarterSuccessfulCondition()
        {
            return _enforcementBarterInProgress
                && Campaign.Current?.BarterManager != null
                && Campaign.Current.BarterManager.LastBarterIsAccepted;
        }

        private void OnEnforcementPayBarterConsequence()
        {
            _enforcementBarterInProgress =
                StartEnforcementPaymentBarter(_dialogPolice, _dialogFine, "缴纳正式罚金");
        }

        private void OnEnforcementPayRejectedConsequence()
        {
            _enforcementBarterInProgress = false;
        }

        private void OnEnforcementPayAcceptedConsequence()
        {
            try
            {
                int paid = PoliceResourceManager.CollectFine(_dialogFine);

                PlayerState.ResetReputation(0);
                CrimeState.EndPlayerHunt();

                if (_dialogPolice != null && _dialogPolice.IsActive)
                {
                    RestoreAi(_dialogPolice);
                    PoliceResourceManager.StartResupply(_dialogPolice);
                    PoliceResourceManager.ForceImmediateMoveToResupply(_dialogPolice);
                }

                MakePeaceWithPoliceAndVictims();

                InformationManager.DisplayMessage(new InformationMessage(
                    $"正式罚金已收 {paid} 金，通缉已解除。",
                    Colors.Yellow));

                try { GwpCommon.TryFinishPlayerEncounter(); } catch { }
            }
            catch { }
            finally
            {
                ResetDialogueState();
            }
        }

        private void OnEnforcementFightConsequence()
        {
            try
            {
                if (_dialogTask != null && MobileParty.MainParty != null)
                    DeclareWar(_dialogTask, MobileParty.MainParty);

                if (_dialogPolice != null && _dialogPolice.IsActive)
                    _dialogPolice.SetMoveEngageParty(MobileParty.MainParty, NavigationType.Default);

                InformationManager.DisplayMessage(new InformationMessage(
                    "你拒绝执法，灰袍守卫将强制抓捕。",
                    Colors.Red));
            }
            catch { }
            finally
            {
                ResetDialogueState();
            }
        }

        private bool StartEnforcementPaymentBarter(MobileParty policePartyMobile, int amount, string barterDisplayName)
        {
            if (policePartyMobile == null || !policePartyMobile.IsActive || MobileParty.MainParty == null)
                return false;

            Hero? barterHero = Hero.OneToOneConversationHero
                               ?? policePartyMobile.LeaderHero
                               ?? GetEnforcementBarterHero();
            if (barterHero == null)
                return false;

            PartyBase policeParty = policePartyMobile.Party;
            PartyBase playerParty = MobileParty.MainParty.Party;
            if (policeParty == null || playerParty == null)
                return false;

            int paymentAmount = Math.Max(1, amount);
            var fineBarter = new GwpBribeBarterable(
                barterHero,
                Hero.MainHero,
                policeParty,
                playerParty,
                paymentAmount,
                barterDisplayName);

            try
            {
                Campaign.Current.BarterManager.StartBarterOffer(
                    Hero.MainHero,
                    barterHero,
                    playerParty,
                    policeParty,
                    null,
                    InitializeEnforcementBarterContext,
                    0,
                    false,
                    new[] { fineBarter });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool InitializeEnforcementBarterContext(Barterable barterable, BarterData args, object obj)
        {
            return barterable is GwpBribeBarterable;
        }

        private Hero? GetEnforcementBarterHero()
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return null;

            Hero leader = policeClan.Leader;
            if (leader != null && leader.IsActive && !leader.IsDead && !leader.IsChild)
                return leader;

            return policeClan.Heroes.FirstOrDefault(h =>
                h != null &&
                h.IsActive &&
                !h.IsDead &&
                !h.IsChild &&
                !h.IsPrisoner);
        }

        private void ResetDialogueState()
        {
            _enforcementBarterInProgress = false;
            _enforcementAtonementAssigned = false;
            _dialogFine = 0;
            _dialogPolice = null!;
            _dialogTask = null!;
        }

        #endregion

        #region 遭遇拦截（强制对话，在宣战前让玩家选择）

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null) return;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            bool policeHasPlayerTask = false;
            bool playerInvolved = false;

            foreach (var p in mapEvent.InvolvedParties)
            {
                if (p?.MobileParty == null) continue;

                if (p.MobileParty.IsMainParty)
                {
                    playerInvolved = true;
                    continue;
                }

                var task = CrimeState.GetTask(p.MobileParty.StringId);
                if (task != null &&
                    task.TargetCrime?.Offender?.IsMainParty == true &&
                    !task.WarDeclared &&
                    !task.IsEscortingPlayer &&
                    p.MobileParty.ActualClan == policeClan)
                {
                    policeHasPlayerTask = true;
                }
            }

            if (!policeHasPlayerTask || !playerInvolved) return;

            IFaction? pf = Clan.PlayerClan?.MapFaction;
            bool atWar = pf != null && FactionManager.IsAtWarAgainstFaction(policeClan, pf);

            if (!atWar && PlayerEncounter.IsActive && PlayerEncounter.EncounteredParty != null)
            {
                try { PlayerEncounter.DoMeeting(); } catch { }
            }
        }

        #endregion
    }
}
