namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 战役启动时清空进程级旧状态；读档随后由各归属方恢复保存数据。
    /// 不进存档的静态集合也在这里清：新档没有 SyncData(IsLoading) 可依靠（见原版战役生命周期手册）。
    /// </summary>
    internal static class GwpRuntimeState
    {
        internal static void ResetForCampaign()
        {
            PlayerBehaviorPool.ClearAll();
            CrimePool.ClearAll();
            GreyWardenPartyDesireBehavior.ResetForCampaign();
            GwpPeace.ResetForCampaign();
        }
    }
}
