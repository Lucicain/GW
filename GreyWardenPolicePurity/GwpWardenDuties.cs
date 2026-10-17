using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    /// <summary>灰袍领主身上可能挂着的差事。登记在 <see cref="GwpWardenDuties"/>。</summary>
    internal enum WardenDuty
    {
        /// <summary>玩家贵族事务（封地申诉）预留的领主。</summary>
        PlayerRequest,
        /// <summary>玩家练兵订单预留的练兵官。</summary>
        PlayerTroopOrder,
        /// <summary>已经约定的比武，赴会或等赛。</summary>
        Duel,
        /// <summary>烧村后的收养驻留。</summary>
        VillageRelief,
        /// <summary>押送被俘的玩家，或作为支援跟随玩家办委托。</summary>
        PlayerEscort,
        /// <summary>承办玩家通缉。</summary>
        PlayerCase,
        /// <summary>协力组的组长或成员。</summary>
        Assistance,
        /// <summary>手上有一项村庄重建。</summary>
        Reconstruction,
        /// <summary>重建本职有待重建的村子，或已在重建：不接普通案件。</summary>
        ReconstructionReserve,
        /// <summary>手上有一项请愿。</summary>
        Issue,
        /// <summary>请愿本职有待办请愿，或已在办：不接普通案件。</summary>
        IssueReserve,
        /// <summary>承办普通案件。</summary>
        OrdinaryCase,
        /// <summary>玩家练兵订单挑选临时换兵来源；不是自动运兵占用。</summary>
        StockExchange,
    }

    /// <summary>
    /// 灰袍差事登记处：谁正在做什么、想接某种差事时谁会挡住你，都只写在这里。
    ///
    /// 以前每个差事系统自己挑人，再各抄一遍「别人是不是占着他」的清单（8 份，彼此有出入：
    /// 练兵与协力不看信众村差事、请愿与重建不看协力……），每加一种差事都要回头改所有清单。
    /// 现在：新差事在 <see cref="Holders"/> 登记一个判断，在 <see cref="BlockedBy"/> 写一行，
    /// 各系统挑人时只问 <see cref="IsFreeFor"/>。各差事自己的条件（本职、资源、优先次序、
    /// 部队状态）仍留在各系统里。
    ///
    /// 判断全部取自各系统已存档的状态，这里不另存任何东西。
    /// </summary>
    internal static class GwpWardenDuties
    {
        private static readonly Dictionary<WardenDuty, Func<MobileParty, bool>> Holders =
            new Dictionary<WardenDuty, Func<MobileParty, bool>>
            {
                [WardenDuty.PlayerRequest] = GreyWardenPlayerRequestBehavior.IsPartyReservedForPlayerRequest,
                [WardenDuty.PlayerTroopOrder] = GreyWardenTroopRequestBehavior.IsTrainerReservedForPlayerOrder,
                [WardenDuty.Duel] = PoliceEnforcementBehavior.IsPartyHeldForDuel,
                [WardenDuty.VillageRelief] = GreyWardenVillageAdoptionBehavior.IsVillageReliefParty,
                [WardenDuty.PlayerEscort] = party =>
                    GwpRuntimeState.Crime.GetTask(party.StringId) is { } task && (task.IsEscortingPlayer || task.IsPlayerBountyEscort),
                [WardenDuty.PlayerCase] = party =>
                    GwpRuntimeState.Crime.GetTask(party.StringId) is { } task && task.TargetCrimeId == CrimePool.PlayerCrimeId &&
                    !task.IsEscortingPlayer && !task.IsPlayerBountyEscort,
                [WardenDuty.Assistance] = PoliceEnforcementBehavior.IsPartyOccupiedByAssistance,
                [WardenDuty.Reconstruction] = GreyWardenVillageReconstructionBehavior.IsReconstructionParty,
                [WardenDuty.ReconstructionReserve] = GreyWardenVillageReconstructionBehavior.ShouldReserveFromOrdinaryCases,
                [WardenDuty.Issue] = GreyWardenIssueResolutionBehavior.IsIssueDutyParty,
                [WardenDuty.IssueReserve] = GreyWardenIssueResolutionBehavior.ShouldReserveFromOrdinaryCases,
                [WardenDuty.OrdinaryCase] = party =>
                    GwpRuntimeState.Crime.GetTask(party.StringId) is { } task && task.TargetCrimeId != CrimePool.PlayerCrimeId &&
                    !task.IsEscortingPlayer && !task.IsPlayerBountyEscort,
            };

        /// <summary>
        /// 想接左边这种差事时，右边的占用不允许被打断。未列的差事由接入入口先释放或暂停。
        /// 协力暂停普通案；本职尚未认领的待办不挡协力，已经认领的重建和请愿保留。
        /// 玩家事务和练兵订单共用 PlayerRequest 准备入口，不中断既有护送、通缉或约斗。
        /// </summary>
        private static readonly Dictionary<WardenDuty, WardenDuty[]> BlockedBy =
            new Dictionary<WardenDuty, WardenDuty[]>
            {
                [WardenDuty.PlayerRequest] = new[]
                {
                    WardenDuty.PlayerEscort, WardenDuty.PlayerCase, WardenDuty.Duel,
                },
                [WardenDuty.PlayerEscort] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.PlayerEscort,
                    WardenDuty.PlayerCase, WardenDuty.Duel,
                },
                [WardenDuty.VillageRelief] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.Assistance, WardenDuty.Duel, WardenDuty.PlayerEscort, WardenDuty.PlayerCase,
                    WardenDuty.Reconstruction, WardenDuty.Issue,
                },
                [WardenDuty.PlayerCase] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.Duel,
                    WardenDuty.VillageRelief, WardenDuty.PlayerEscort, WardenDuty.Assistance,
                    WardenDuty.Reconstruction, WardenDuty.Issue,
                },
                [WardenDuty.Assistance] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.PlayerEscort, WardenDuty.PlayerCase, WardenDuty.Assistance,
                    WardenDuty.Duel, WardenDuty.Reconstruction, WardenDuty.Issue,
                },
                [WardenDuty.Reconstruction] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.Assistance, WardenDuty.Reconstruction, WardenDuty.Issue,
                    WardenDuty.Duel, WardenDuty.PlayerEscort, WardenDuty.PlayerCase,
                },
                [WardenDuty.Issue] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.Assistance, WardenDuty.Reconstruction, WardenDuty.Issue,
                    WardenDuty.Duel, WardenDuty.PlayerEscort, WardenDuty.PlayerCase,
                },
                [WardenDuty.StockExchange] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.PlayerEscort, WardenDuty.PlayerCase, WardenDuty.Assistance,
                    WardenDuty.Reconstruction, WardenDuty.Issue, WardenDuty.OrdinaryCase,
                    WardenDuty.Duel,
                },
                [WardenDuty.OrdinaryCase] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.PlayerEscort, WardenDuty.PlayerCase, WardenDuty.Assistance,
                    WardenDuty.ReconstructionReserve, WardenDuty.IssueReserve,
                    WardenDuty.OrdinaryCase,
                    WardenDuty.Duel,
                },
            };

        internal static bool HoldsAny(MobileParty? party, params WardenDuty[] duties) =>
            party != null && duties.Any(duty => Holders[duty](party));

        /// <summary>已有案件可以保留，但不能同时支配另一件正在执行的差事。</summary>
        internal static bool CanContinueCase(MobileParty party) =>
            !HoldsAny(party, WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder,
                WardenDuty.Duel, WardenDuty.VillageRelief, WardenDuty.Reconstruction, WardenDuty.Issue);

        internal static bool CanContinueRelief(MobileParty party) =>
            !HoldsAny(party, WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder,
                WardenDuty.Duel, WardenDuty.PlayerEscort, WardenDuty.PlayerCase);

        /// <summary>从保存的任务直接恢复驻留目标，不依赖外部指令的提交次序或8小时有效期。</summary>
        internal static bool TryGetVisitDuty(MobileParty party, out Settlement? target, out float score)
        {
            target = PoliceEnforcementBehavior.GetDuelVenue(party);
            score = 8f;
            if (target != null) return true;
            if (!CanContinueRelief(party))
                return false;
            return GreyWardenVillageAdoptionBehavior.TryGetReliefVisit(party, out target, out score);
        }

        /// <summary>赴约、离开工地或战斗时暂停；暂停起点由所属任务存档。</summary>
        internal static bool PauseWork(MobileParty party, bool atWorksite,
            ref double endHours, ref double pausedAtHours)
        {
            double now = CampaignTime.Now.ToHours;
            if (HoldsAny(party, WardenDuty.Duel) ||
                endHours > 0d && (!atWorksite || party.MapEvent is { IsFinalized: false }))
            {
                if (pausedAtHours < 0d) pausedAtHours = now;
                return true;
            }
            if (pausedAtHours >= 0d)
            {
                if (endHours > 0d) endHours += now - pausedAtHours;
                pausedAtHours = -1d;
            }
            return false;
        }

        internal static double RemainingWorkHours(double endHours, double pausedAtHours) =>
            Math.Max(0d, endHours - (pausedAtHours >= 0d ? pausedAtHours : CampaignTime.Now.ToHours));

        /// <summary>这支部队身上有没有挡住 <paramref name="wanted"/> 的差事。<paramref name="except"/> 里的不算。</summary>
        internal static bool IsFreeFor(MobileParty? party, WardenDuty wanted, params WardenDuty[] except) =>
            party != null && BlockingDuty(party, wanted, except) == null;

        /// <summary>挡住 <paramref name="wanted"/> 的第一件差事（按表中顺序），没有则为 null。用于诊断。</summary>
        internal static WardenDuty? BlockingDuty(MobileParty party, WardenDuty wanted, params WardenDuty[] except)
        {
            foreach (WardenDuty duty in BlockedBy[wanted])
                if (!except.Contains(duty) && Holders[duty](party))
                    return duty;
            return null;
        }
    }
}
