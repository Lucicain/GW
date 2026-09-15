using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 让分兵界面允许把本案的目标交给派出去的队伍。
    ///
    /// 原版 <c>Helpers.PartyScreenHelper.OpenScreenWithDummyRoster</c> 把
    /// <c>PrisonerTransferState</c> **写死成 NotTransferable**（IL 里是一句
    /// `ldc.i4.0; stfld PrisonerTransferState`，与成员那一行的 `ldc.i4.1` 正好相对）。
    /// 而 <c>IsTroopTransferable</c> 的第一件事就是问
    /// <c>IsTroopRosterTransferable(TroopType.Prisoner)</c>——这一关过不去，
    /// **我们自己那个 `IsSendable` 判据根本没有机会被调用**。表现就是俘虏在界面里拖不动，
    /// 日志里永远是 `prisoners=0`。此前两轮在 `IsDeliverableCasePrisoner` 上找原因，
    /// 方向就是错的：问题不在判据，在判据之前。
    ///
    /// 这里只在派遣队伍的分兵界面开着的时候放开"俘虏这一栏可以动"，具体哪一个人能动
    /// 仍然由 <c>GwpWardenDispatchDialogue.IsSendable</c> 说了算——也就是只有本案目标。
    /// 原版其他场合的分兵界面一概不受影响。
    /// </summary>
    [HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.IsTroopRosterTransferable))]
    internal static class GwpDispatchPrisonerTransferPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PartyScreenLogic.TroopType troopType, ref bool __result)
        {
            if (__result || troopType != PartyScreenLogic.TroopType.Prisoner) return;
            if (!GwpWardenDispatchDialogue.IsPrisonerHandoverOpen) return;
            __result = true;
        }
    }
}
