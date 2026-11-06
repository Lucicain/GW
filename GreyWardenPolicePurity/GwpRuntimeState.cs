namespace GreyWardenPolicePurity
{
    /// <summary>战役启动时清空进程级旧状态；读档随后由各归属方恢复保存数据。</summary>
    internal static class GwpRuntimeState
    {
        internal static void ResetForCampaign()
        {
            PlayerBehaviorPool.ClearAll();
            CrimePool.ClearAll();
        }
    }
}
