using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>灰袍领主身上可能挂着的差事。登记在 <see cref="GwpWardenDuties"/>。</summary>
    internal enum WardenDuty
    {
        /// <summary>玩家贵族事务（封地申诉）预留的领主。</summary>
        PlayerRequest,
        /// <summary>玩家练兵订单预留的练兵官。</summary>
        PlayerTroopOrder,
        /// <summary>被焚村庄善后（强制差事）。</summary>
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
        /// <summary>正在练兵（练兵官或受训方）。</summary>
        Training,
        /// <summary>正在练兵，或练兵本职已练满在排队：不接新差事。</summary>
        TrainingQueue,
        /// <summary>信众村差事。</summary>
        FaithErrand,
        /// <summary>承办普通案件。</summary>
        OrdinaryCase,
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
                [WardenDuty.Training] = GreyWardenTrainingBehavior.IsTrainingOccupied,
                [WardenDuty.TrainingQueue] = GreyWardenTrainingBehavior.ShouldReserveFromNewDuties,
                [WardenDuty.FaithErrand] = GwpFireswornBehavior.IsOnFaithErrand,
                [WardenDuty.OrdinaryCase] = party =>
                    GwpRuntimeState.Crime.GetTask(party.StringId) is { } task && task.TargetCrimeId != CrimePool.PlayerCrimeId &&
                    !task.IsEscortingPlayer && !task.IsPlayerBountyEscort,
            };

        /// <summary>
        /// 想接左边这种差事时，身上挂着右边任何一种就不行。没列出来的，是左边这种差事压得过的——
        /// 由那一边的系统自己让路（取消或退回，例如信众村差事被占用就取消，普通案件被协力抽调就退回台账）。
        /// 玩家贵族事务和练兵订单是指定的人，谁都压不过，所以不在左边。
        /// </summary>
        private static readonly Dictionary<WardenDuty, WardenDuty[]> BlockedBy =
            new Dictionary<WardenDuty, WardenDuty[]>
            {
                [WardenDuty.VillageRelief] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.TrainingQueue, WardenDuty.Assistance,
                },
                [WardenDuty.PlayerCase] = new[]
                {
                    WardenDuty.VillageRelief, WardenDuty.PlayerEscort, WardenDuty.Assistance,
                },
                [WardenDuty.Assistance] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.PlayerEscort, WardenDuty.PlayerCase, WardenDuty.Assistance,
                    WardenDuty.ReconstructionReserve, WardenDuty.IssueReserve, WardenDuty.TrainingQueue,
                },
                [WardenDuty.Reconstruction] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.Assistance, WardenDuty.Reconstruction, WardenDuty.Issue, WardenDuty.TrainingQueue,
                },
                [WardenDuty.Issue] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.Assistance, WardenDuty.Reconstruction, WardenDuty.Issue, WardenDuty.TrainingQueue,
                },
                [WardenDuty.Training] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.PlayerEscort, WardenDuty.PlayerCase, WardenDuty.Assistance,
                    WardenDuty.Reconstruction, WardenDuty.Issue, WardenDuty.Training, WardenDuty.OrdinaryCase,
                },
                [WardenDuty.FaithErrand] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.Assistance, WardenDuty.Reconstruction, WardenDuty.Issue, WardenDuty.TrainingQueue,
                },
                [WardenDuty.OrdinaryCase] = new[]
                {
                    WardenDuty.PlayerRequest, WardenDuty.PlayerTroopOrder, WardenDuty.VillageRelief,
                    WardenDuty.PlayerEscort, WardenDuty.PlayerCase, WardenDuty.Assistance,
                    WardenDuty.ReconstructionReserve, WardenDuty.IssueReserve, WardenDuty.TrainingQueue,
                    WardenDuty.FaithErrand, WardenDuty.OrdinaryCase,
                },
            };

        internal static bool HoldsAny(MobileParty? party, params WardenDuty[] duties) =>
            party != null && duties.Any(duty => Holders[duty](party));

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
