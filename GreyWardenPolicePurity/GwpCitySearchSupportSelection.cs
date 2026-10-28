using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TroopSelection;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 进城搜捕选兵：已到身边的灰袍办案队单独成行、锁定必带，不与玩家本队的同兵种合并（用户裁定 2026-10-28）。
    /// 原版选兵界面按兵种一行（GameMenuTroopSelectionVM.InitList，C#反编译 v1.4.8），所以这一次打开传入
    /// 玩家名册的副本作钥匙，界面初始化与重置时按它找回办案队名册，追加锁定行。
    /// </summary>
    internal static class GwpCitySearchSupportSelection
    {
        private static readonly ConditionalWeakTable<TroopRoster, TroopRoster> Pending =
            new ConditionalWeakTable<TroopRoster, TroopRoster>();

        internal static TroopRoster Register(TroopRoster own, TroopRoster? support)
        {
            if (support == null) return own;
            TroopRoster copy = TroopRoster.CreateDummyTroopRoster();
            foreach (TroopRosterElement element in own.GetTroopRoster())
                copy.AddToCounts(element.Character, element.Number, woundedCount: element.WoundedNumber);
            Pending.Add(copy, support);
            return copy;
        }

        [HarmonyPatch(typeof(GameMenuTroopSelectionVM), "InitList")]
        internal static class InitListPatch
        {
            private static void Postfix(GameMenuTroopSelectionVM __instance, TroopRoster ____fullRoster,
                ref int ____currentTotalSelectedTroopCount)
            {
                if (!Pending.TryGetValue(____fullRoster, out TroopRoster support)) return;
                foreach (TroopRosterElement element in support.GetTroopRoster())
                {
                    int healthy = element.Number - element.WoundedNumber;
                    if (healthy <= 0 || element.Character.IsHero) continue;
                    // 不接加减回调：清空与增减都碰不到这一行，人数始终是办案队全部健康兵。
                    __instance.Troops.Add(new TroopSelectionItemVM(element, null, null) { IsLocked = true, CurrentAmount = healthy });
                    ____currentTotalSelectedTroopCount += healthy;
                }
                __instance.Troops.Sort(new TroopItemComparer());
            }
        }
    }
}
