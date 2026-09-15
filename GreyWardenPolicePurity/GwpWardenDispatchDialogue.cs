using System;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 和队里的灰袍士兵说话，把两件差事交给他们：替自己复命，或者去求援。
    /// 人数由玩家在原版分兵界面自己定；上缴多少、怎么说，也由玩家在出发前定死，
    /// 士兵不替他临场决定。
    /// </summary>
    internal static class GwpWardenDispatchDialogue
    {
        private static CharacterObject? _requestedTroop;
        private static int _requestedAtTicks;
        private static CharacterObject? _activeTroop;
        private static int _openedAtTicks;
        /// <summary>点了按钮却一直开不了口（例如人还在城里），过一会儿就当作没点过。</summary>
        private const int RequestExpiryMilliseconds = 20000;
        /// <summary>
        /// 开了对话却迟迟没有真正开始时的兜底。地图对话是延迟启动的，中间有若干帧
        /// <c>IsConversationInProgress</c> 还是 false——这段窗口里绝不能把武装状态清掉，
        /// 否则开场白的条件不成立，玩家会进到一场一句话都没有的对话里，整个交互就卡死。
        /// </summary>
        private const int OpenGraceMilliseconds = 15000;
        private static GwpDispatchPurpose _pendingPurpose;
        private static bool _selectionQueued;

        /// <summary>
        /// 队伍界面的交谈按钮只提出请求。界面关闭要等原版自己走完，所以真正开对话
        /// 推到下一次心跳，不在关屏的同一帧里抢状态。
        /// </summary>
        internal static void OpenWithTroop(CharacterObject? troop)
        {
            if (troop == null || troop.IsHero || MobileParty.MainParty?.IsActive != true) return;
            _requestedTroop = troop;
            _requestedAtTicks = Environment.TickCount;
        }

        /// <summary>
        /// 由派遣行为的心跳驱动：先把对话开起来，对话收尾之后再开分兵界面。
        /// 两件事都只在地图界面就绪、且没有别的对话在进行时才做。
        /// </summary>
        internal static void Pump()
        {
            // 没有待办就彻底不做事。读档和过场期间这条路必须是完全惰性的。
            if (_requestedTroop == null && !_selectionQueued && _activeTroop == null) return;

            ConversationManager? manager = Campaign.Current?.ConversationManager;
            if (manager == null) return;
            if (manager.IsConversationInProgress) return;

            // 对话真正结束由 ConversationEnded 负责撤下武装；这里只兜底处理"开了却始终
            // 没开起来"的情况，绝不在启动窗口内提前清掉。
            if (_activeTroop != null)
            {
                if (unchecked(Environment.TickCount - _openedAtTicks) <= OpenGraceMilliseconds)
                    return;
                Disarm();
            }
            if (_requestedTroop == null && !_selectionQueued) return;

            // 只用战役侧的状态判断是否可以开对话或开界面。这条路每 0.25 秒走一次，
            // 不依赖任何视图层类型，免得在读档这种最糟的时刻去解析视图程序集。
            if (Campaign.Current?.CurrentMenuContext != null) return;
            if (PlayerEncounter.Current != null) return;
            MobileParty? main = MobileParty.MainParty;
            if (main?.IsActive != true || main.MapEvent != null ||
                main.CurrentSettlement != null) return;

            if (_selectionQueued)
            {
                _selectionQueued = false;
                OpenTroopSelection();
                return;
            }

            CharacterObject? troop = _requestedTroop;
            if (troop == null) return;
            if (unchecked(Environment.TickCount - _requestedAtTicks) > RequestExpiryMilliseconds)
            {
                _requestedTroop = null;
                return;
            }
            _requestedTroop = null;
            if (!CanTalkToTroop(troop)) return;

            try
            {
                _activeTroop = troop;
                _openedAtTicks = Environment.TickCount;
                ConversationCharacterData playerData = new ConversationCharacterData(
                    CharacterObject.PlayerCharacter, PartyBase.MainParty,
                    false, false, false, false, false, false);
                ConversationCharacterData troopData = new ConversationCharacterData(
                    troop, PartyBase.MainParty,
                    false, false, false, false, false, false);
                CampaignMapConversation.OpenConversation(playerData, troopData);
            }
            catch (Exception ex)
            {
                _activeTroop = null;
                GwpAiDiagnostics.WriteFieldArrest("DISPATCH_CONVERSATION_FAILED", ex.ToString());
            }
        }

        /// <summary>
        /// 这场对话是不是我方刚刚开起来的那一场。
        ///
        /// 判据只看"我方是否正武装着"。**不能**再去比对
        /// <c>OneToOneConversationCharacter</c>：地图对话是延迟启动的，条件求值时这个属性
        /// 可能还没填、或者对普通士兵根本不填。一旦因此判成 false，"start" 上就没有任何一条
        /// 台词成立，玩家会被扔进一场一句话都没有的对话里出不来，表现就是点谁都没反应。
        /// 武装状态只在我方开对话的那一刻置位，对话结束或超时即撤下。
        /// </summary>
        private static bool IsOurConversation() => _activeTroop != null;

        /// <summary>对话结束就撤下武装，绝不把状态留到下一场别人的对话里。</summary>
        internal static void OnConversationEnded()
        {
            _activeTroop = null;
        }

        /// <summary>会话启动时清空全部静态状态。</summary>
        internal static void ResetRuntimeState()
        {
            _prisonerHandoverOpen = false;
            _requestedTroop = null;
            _activeTroop = null;
            _selectionQueued = false;
        }

        internal static bool CanTalkToTroop(CharacterObject? troop) =>
            troop != null && !troop.IsHero && GwpCommon.IsGreyWardenTroop(troop) &&
            Campaign.Current?.GetCampaignBehavior<GwpWardenDispatchBehavior>() != null &&
            (ReportAvailable() || SupportAvailable());

        internal static void Register(CampaignGameStarter starter)
        {
            starter.AddDialogLine("gwp_dispatch_greeting", "start", "gwp_dispatch_options",
                GwpText.Get("{=gwp_dispatch_greeting}Commander. Say the word and we ride."),
                IsOurConversation, null, 400);

            starter.AddPlayerLine("gwp_dispatch_report", "gwp_dispatch_options", "gwp_dispatch_confirm",
                GwpText.Get("{=gwp_dispatch_report}Take our account of the commission to the Wardens for me."),
                CanSendReport, () => _pendingPurpose = GwpDispatchPurpose.Report);

            starter.AddPlayerLine("gwp_dispatch_support", "gwp_dispatch_options", "gwp_dispatch_confirm",
                GwpText.Get("{=gwp_dispatch_support}Ride to the Wardens and ask them to take the field with me."),
                CanSendSupport, () => _pendingPurpose = GwpDispatchPurpose.Support);

            starter.AddPlayerLine("gwp_dispatch_never_mind", "gwp_dispatch_options", "close_window",
                GwpText.Get("{=gwp_dispatch_never_mind}Nothing for now. Back to your post."),
                IsOurConversation, null);

            starter.AddDialogLine("gwp_dispatch_confirm", "gwp_dispatch_confirm", "close_window",
                GwpText.Get("{=gwp_dispatch_confirm}Then pick the men who ride with me, Commander."),
                IsOurConversation, QueueSelection, 400);
        }

        private static bool CanSendReport() => IsOurConversation() && ReportAvailable();

        private static bool ReportAvailable()
        {
            var bounty = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            var dispatch = Campaign.Current?.GetCampaignBehavior<GwpWardenDispatchBehavior>();
            return bounty?.CanDispatchCaseReport == true &&
                   dispatch?.HasActiveDispatch(GwpDispatchPurpose.Report) != true;
        }

        private static bool CanSendSupport() => IsOurConversation() && SupportAvailable();

        private static bool SupportAvailable()
        {
            var bounty = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            var dispatch = Campaign.Current?.GetCampaignBehavior<GwpWardenDispatchBehavior>();
            return bounty?.CanRequestCaseSupport == true &&
                   dispatch?.HasActiveDispatch(GwpDispatchPurpose.Support) != true;
        }

        private static void Disarm()
        {
            _activeTroop = null;
            _requestedTroop = null;
        }

        private static void QueueSelection()
        {
            _selectionQueued = true;
        }

        /// <summary>
        /// 送信差事的分兵界面正开着。只在这段时间里放开"俘虏那一栏可以动"，
        /// 见 <see cref="GwpDispatchPrisonerTransferPatch"/>。
        /// </summary>
        private static bool _prisonerHandoverOpen;

        internal static bool IsPrisonerHandoverOpen => _prisonerHandoverOpen;

        private static void OpenTroopSelection()
        {
            GwpDispatchPurpose purpose = _pendingPurpose;
            var detachment = TroopRoster.CreateDummyTroopRoster();
            var prisoners = TroopRoster.CreateDummyTroopRoster();

            // 打开分兵界面时把"那个人能不能交"的每一项条件都记下来。俘虏在界面里选不动
            // 的时候，这一行直接说明卡在哪一条，不必再靠猜。
            if (purpose == GwpDispatchPurpose.Report)
            {
                _prisonerHandoverOpen = true;
                GwpAiDiagnostics.WriteFieldArrest("DISPATCH_PRISONER_GATE",
                    Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
                        ?.DescribeCasePrisonerGate() ?? "no bounty behaviour");
            }

            try
            {
                PartyScreenHelper.OpenScreenWithDummyRosterWithMainParty(
                    detachment,
                    prisoners,
                    BuildScreenName(purpose),
                    Math.Max(1, MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 1),
                    (left, leftPrison, right, rightPrison, leftLimit, rightLimit) =>
                        new Tuple<bool, TextObject>(
                            left.TotalManCount > 0,
                            left.TotalManCount > 0
                                ? new TextObject(string.Empty)
                                : GwpText.Create("{=gwp_dispatch_pick_someone}Choose at least one man to send.")),
                    (leftOwner, leftMembers, leftPrison, rightOwner, rightMembers, rightPrison, fromCancel) =>
                        OnSelectionClosed(purpose, leftMembers, leftPrison, fromCancel),
                    (character, type, side, leftOwnerParty) => IsSendable(purpose, character, type),
                    () => true);
            }
            catch (Exception ex)
            {
                GwpAiDiagnostics.WriteFieldArrest("DISPATCH_SELECTION_FAILED", ex.ToString());
            }
        }

        private static TextObject BuildScreenName(GwpDispatchPurpose purpose) =>
            GwpText.Create(purpose == GwpDispatchPurpose.Report
                ? "{=gwp_dispatch_screen_report}Courier detail"
                : "{=gwp_dispatch_screen_support}Request rider");

        /// <summary>
        /// 只许派灰袍的兵；俘虏只许带本案目标本人，别的人犯不在这趟差事里。
        /// </summary>
        private static bool IsSendable(GwpDispatchPurpose purpose,
            CharacterObject character, PartyScreenLogic.TroopType type)
        {
            if (character == null) return false;
            if (character.IsHero)
                return type == PartyScreenLogic.TroopType.Prisoner &&
                       purpose == GwpDispatchPurpose.Report && IsCasePrisoner(character);
            return type == PartyScreenLogic.TroopType.Member && GwpCommon.IsGreyWardenTroop(character);
        }

        private static bool IsCasePrisoner(CharacterObject character)
        {
            Hero? hero = character?.HeroObject;
            var bounty = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            return hero != null && bounty?.IsDeliverableCasePrisoner(hero) == true;
        }

        private static void OnSelectionClosed(GwpDispatchPurpose purpose,
            TroopRoster members, TroopRoster prisoners, bool fromCancel)
        {
            _prisonerHandoverOpen = false;
            if (fromCancel || members == null || members.TotalManCount <= 0)
            {
                ReturnSelection(members, prisoners);
                return;
            }

            string prisonerHeroId = prisoners?.GetTroopRoster()
                .Select(entry => entry.Character?.HeroObject)
                .FirstOrDefault(hero => hero != null)?.StringId ?? string.Empty;

            if (purpose == GwpDispatchPurpose.Support)
            {
                Send(purpose, members, prisoners, 0, false, prisonerHeroId);
                return;
            }

            AskHandInAmount(members, prisoners, prisonerHeroId);
        }

        /// <summary>
        /// 交多少、怎么说，出发前由玩家自己定死。士兵只是把话和钱带到。
        /// </summary>
        private static void AskHandInAmount(TroopRoster members, TroopRoster prisoners,
            string prisonerHeroId)
        {
            var bounty = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            int due = Math.Max(0, bounty?.CaseReportAmountDue ?? 0);
            int wallet = Math.Max(0, Hero.MainHero?.Gold ?? 0);
            int cap = Math.Min(due, wallet);

            InformationManager.ShowTextInquiry(new TextInquiryData(
                GwpText.Get("{=gwp_dispatch_amount_title}Money for the Wardens"),
                GwpText.Get(
                    "{=gwp_dispatch_amount_body}The account stands at {VAR_1} denars. How much do your men carry? You hold {VAR_2}.",
                    "VAR_1", due, "VAR_2", wallet),
                true, true,
                GwpText.Get("{=gwp_common_confirm}Confirm"),
                GwpText.Get("{=gwp_common_cancel}Cancel"),
                input => AskTruthfulness(members, prisoners, prisonerHeroId, ClampAmount(input, cap)),
                () => ReturnSelection(members, prisoners),
                false,
                input => new Tuple<bool, string>(
                    int.TryParse(input, out int value) && value >= 0 && value <= cap,
                    GwpText.Get("{=gwp_dispatch_amount_invalid}Enter a number you actually have.")),
                string.Empty,
                cap.ToString()),
                true);
        }

        private static int ClampAmount(string? input, int cap) =>
            int.TryParse(input, out int value) ? Math.Max(0, Math.Min(cap, value)) : 0;

        private static void AskTruthfulness(TroopRoster members, TroopRoster prisoners,
            string prisonerHeroId, int amount)
        {
            var bounty = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            int due = Math.Max(0, bounty?.CaseReportAmountDue ?? 0);
            if (amount >= due)
            {
                Send(GwpDispatchPurpose.Report, members, prisoners, amount, false, prisonerHeroId);
                return;
            }

            InformationManager.ShowInquiry(new InquiryData(
                GwpText.Get("{=gwp_dispatch_word_title}What your men are to say"),
                GwpText.Get(
                    "{=gwp_dispatch_word_body}They will hand over {VAR_1} of the {VAR_2} on the account. What account do they give of the rest?",
                    "VAR_1", amount, "VAR_2", due),
                true, true,
                GwpText.Get("{=gwp_dispatch_word_truth}The plain truth"),
                GwpText.Get("{=gwp_dispatch_word_lie}[Lie] That was all he could pay"),
                () => Send(GwpDispatchPurpose.Report, members, prisoners, amount, false, prisonerHeroId),
                () => Send(GwpDispatchPurpose.Report, members, prisoners, amount, true, prisonerHeroId)),
                true);
        }

        private static void Send(GwpDispatchPurpose purpose, TroopRoster members,
            TroopRoster prisoners, int amount, bool lie, string prisonerHeroId)
        {
            var dispatch = Campaign.Current?.GetCampaignBehavior<GwpWardenDispatchBehavior>();
            if (dispatch?.Dispatch(members, prisoners, purpose, amount, lie, prisonerHeroId) == null)
                ReturnSelection(members, prisoners);
        }

        /// <summary>取消或派不出去时，选中的人和俘虏原样还给主队，不能凭空消失。</summary>
        private static void ReturnSelection(TroopRoster? members, TroopRoster? prisoners)
        {
            MobileParty? player = MobileParty.MainParty;
            if (player?.IsActive != true) return;
            if (members != null)
                foreach (TroopRosterElement element in members.GetTroopRoster().ToList())
                    if (element.Character != null && element.Number > 0)
                        player.MemberRoster.AddToCounts(element.Character, element.Number,
                            false, element.WoundedNumber, element.Xp);
            if (prisoners != null)
                foreach (TroopRosterElement element in prisoners.GetTroopRoster().ToList())
                    if (element.Character != null && element.Number > 0)
                        player.PrisonRoster.AddToCounts(element.Character, element.Number,
                            false, element.WoundedNumber);
            members?.Clear();
            prisoners?.Clear();
        }
    }
}
