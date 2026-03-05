using TaleWorlds.SaveSystem;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 注册本 mod 中需要进入 Campaign 持久化系统的自定义类型。
    ///
    /// ── 为什么需要这个类 ──────────────────────────────────────────────────────────
    /// Bannerlord 的存档系统在序列化每个对象时，会查找该类型在"类型注册表"中的 ID，
    /// 将 ID（而非类型全名）写入存档文件。反序列化时再用 ID 还原类型。
    ///
    /// 如果类型未注册 → 序列化时查不到 ID → 抛异常 → 玩家看到"无法存档"。
    ///
    /// 受影响的类型：
    ///   • 继承 QuestBase 且通过 StartQuest() 注册进 Campaign.QuestManager 的类。
    ///   • 继承 InformationData 且通过 CampaignInformationManager.NewMapNoticeAdded()
    ///     进入持久化通知列表的类。
    ///
    /// ── 如何生效 ──────────────────────────────────────────────────────────────────
    /// Bannerlord 在游戏启动时通过反射扫描所有已加载程序集，
    /// 自动发现并实例化所有 SaveableTypeDefiner 子类（包括 mod 程序集中的）。
    /// 无需在 SubModule.cs 或任何地方手动注册本类。
    ///
    /// ── 类型 ID 规则 ──────────────────────────────────────────────────────────────
    /// 全局类型 ID = base + localId。
    /// TaleWorlds 原版占用 1 ~ 1_000_000；本 mod 使用 2_894_632 作为 base，
    /// 远离原版范围，与其他 mod 冲突概率极低。
    /// 一旦发布后不可修改已有 localId，否则现有存档无法加载对应类型。
    /// </summary>
    public class GwpSaveableTypeDefiner : SaveableTypeDefiner
    {
        // ★ base ID 在整个游戏（含所有 mod）中必须唯一。
        //   如果与其他 mod 冲突，两个 mod 同时加载时存档系统会出现类型混淆。
        public GwpSaveableTypeDefiner() : base(2_894_632) { }

        protected override void DefineClassTypes()
        {
            // localId = 1 → 全局 ID 2_894_633
            // BountyHunterQuest 在 AcceptBounty() → StartQuest() 后进入
            // Campaign.QuestManager 的持久化 Quest 列表。
            // 读档时通过 SpecialQuestType 告知引擎本任务为独立特殊任务，
            // 可正常调用 InitializeQuestOnGameLoad() 而不被自动取消。
            AddClassDefinition(typeof(PlayerBountyBehavior.BountyHunterQuest), 1);

            // localId = 2 → 全局 ID 2_894_634
            // BountyMapNotification 在 OfferBounty() → NewMapNoticeAdded() 后进入
            // Campaign 持久化通知列表。
            // 读档时 IsValid() 因 CrimePool 已清空而返回 false，通知自动清除。
            AddClassDefinition(typeof(PlayerBountyBehavior.BountyMapNotification), 2);
        }
    }
}
