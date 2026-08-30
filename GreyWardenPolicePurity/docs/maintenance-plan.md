# GreyWarden Maintenance Plan

## 2026-08-30 用户确认：NPC 双刀实现成功，建立稳定检查点

- **用户实机确认：双刀功能正常，人物模型/预览显示正常。** 双刃卫士在战斗中真正双持，左右手动作与玩家一致；英雄预览、百科与自定义战斗预览均无异常。
- 本检查点提交 `b98d01e`，本地标签 `checkpoint/npc-dual-blade`。这是"NPC 可用双刀"的首个已验证基线，后续任何回归直接回到该提交。

### 最终生效的实现（三部分，全部落在预览安全区）

1. **建立 —— 无需任何代码。** 原生自身的出生拔刀（`Agent.WieldInitialWeapons` → `Equipment.GetInitialWeaponIndicesToEquip`）就能把 `Weapon0` 副手刀与 `Weapon1` 主手刀正确配好。部署阶段的实测截图证实了这一点。此前两周所有"设法把副手塞进手里"的尝试都是在解决一个不存在的问题。
2. **保持之一 · 阵型 —— `GwpDualBladeShieldDirectionPatch`。** 后置 `ArrangementOrder.GetShieldDirectionOfUnit`，把双刃卫士的返回值由 `UsageDirection.None`（`-1`，语义为"收起副手"）改为 `AttackEnd`（`4`，"携带但不举起"）。两处收刀调用点（`ArrangementOrder.OnApply` 与 `Agent.UpdateFormationOrders`）共用该静态方法，一个补丁覆盖两者。
3. **保持之二 · AI 武器重选 —— `GwpDualBladeGuardBehavior` + `GwpDualBladeGuardInputComponent`。** 通过 `AgentComponent.OnAIInputSet`（**组件，非 Harmony 补丁**）丢弃双刃卫士的全部换武器输入（`Wield0..Wield3`、`Sheath0/1`、`ToggleAlternativeWeapon`）。该兵种只带两把刀、没有第二种武器，因此不损失任何战术行为；移动、攻击、格挡等决策完全交给原生。
- 配套数据：`gwtwinblade` 兵种（`Item0` 副手刀 / `Item1` 主手刀，无弓，level 26，由 `gwrecruit` 升级）；`gwdualbladeoffhandai`（与玩家副手刀复用同一锻造模板与同样四个部件，外观一致，仅 id 不同）；`GwpDualBladeNpcItemSetup` 在 `OnGameInitializationFinished` 对该 NPC 物品一次性写入 `CollisionBodyName = BodyName`、`WeaponFlags |= HasHitPoints | CanBlockRanged`（遍历全部 usage）、`MaxDataValue = 500`。玩家的 `gwdualbladeoffhand` 完全未改动。
- 伤害与击倒：`IsOffHandBladeId` 使玩家刀与 NPC 刀在装备判定、副手伤害类型与 bone-20 击倒三处一视同仁。

### 补丁目标安全边界（本模组 1.5.2 的硬性结论）

| 目标 | 预览 | 依据 |
|---|---|---|
| `Mission.*` | 安全 | `2367e60` 用户确认"什么都好" |
| `ArrangementOrder.*` | 安全 | 本轮英雄预览正常 |
| `MissionBehavior` / `AgentComponent`（非补丁） | 安全 | 本轮确认 |
| **`Agent.*` per-call** | **有毒** | 两次复现，第二次判定已窄到只读不可变 id，排除判定条件变量 |
| **`MissionWeapon.*` per-call** | **有毒** | 多轮复现；`GwpBlackLordShieldBehavior` 早年也因此被清空 |

**两周内反复出现的"人物模型消失/姿态异常"，成因就是在后两类类型上安装 per-call 补丁，与补丁内容无关。** 后续任何开发不得违反此边界。

### 基线校验值

- 离线预检 `PATCH_OK=38; PATCH_FAIL=0`（检查点 37 + `ArrangementOrder` 1），程序集类型数 411；`HarmonyPatch(typeof(Agent)` 与 `HarmonyPatch(typeof(MissionWeapon)` 命中均为 0。
- XSLT 变换后 action_set 总数 102（与原版一致）、重复 id 0、`as_human_female_warrior` 保持原版 298 条。
- 30 个 XML/XSLT/mbproj 解析失败 0；仓库 `_Module` 与 live 36 个可部署文件缺失 0、差异 0。
- 客户端与编辑器 DLL 均为 797696 字节、SHA-256 `D473A3E99BBFAE9B5ED586005420CC8A929BE405318459DA7241B54CC6280A26`。Bannerlord 进程数 0。
- 已知取舍（用户已同意）：双刃卫士的副手刀带 `CanBlockRanged`，因此能挡下飞箭；这是让原生 AI 承认该副手所必需的，玩家双刀不受影响。
- 失败候选全部保留可查：`failed/npc-native-qualification`、`failed/npc-ai-input-boundary`、`failed/shield-enforcement-bypass`、`failed/shield-bypass-narrow`，以及两个 stash 与 reflog。

## 2026-08-30 部署阶段实证：双刀本来就能拿住，开战时被原生 AI 武器重选收走

- 用户实测并附截图：**部署阶段双刃卫士双刀在手且稳定**（截图中卫士背后为交叉双刀）；**点击开始战斗的瞬间只剩主剑**，并且主剑被重复拔出三次；英雄预览模型正常。
- 这组观测把"建立"与"保持"彻底分开，结论明确：
  1. **原生自己的出生拔刀就能把双刀正确配好** —— 建立从来不是问题，也不需要任何代码。此前所有"想办法把副手塞进手里"的努力都是在解决一个不存在的问题。
  2. 部署阶段没有 AI 武器决策在运行 → 双刀一直保持。**开战瞬间 AI 激活 → 立即收掉副手。**
  3. **收刀者不是阵型。** `ArrangementOrder` 后置已经生效（副刀不再被反复收放，士兵稳定持主刀），且部署阶段本就没有阵型收刀问题。真正的收刀者是**原生 AI 的武器重选**。
  4. "主剑重复拔出三次"是上一版 `GwpDualBladeGuardBehavior` 的 `MaxSequences=3` 序列在徒劳挣扎：收主手 → 拔副手（失败）→ 拔回主手，重复三轮。该行为不仅无效，还制造了可见抽搐，已完整删除。
- 原生 AI 武器重选的**唯一托管接口**是 `AgentComponent.OnAIInputSet`（`agent.cs.txt:1626`，原生按引用交出 `EventControlFlag`，由 `SetHasOnAiInputSetCallback(true)` 启用）。关键是：**这是组件，不是 Harmony 补丁**，因此不触碰 `Agent`/`MissionWeapon` 这两个已确证会破坏预览的类型。
- 本轮实现：`GwpDualBladeGuardBehavior` 改为只做一件事 —— 给双刃卫士挂 `GwpDualBladeGuardInputComponent`，在 `OnAIInputSet` 中**丢弃全部换武器输入**（`Wield0..Wield3`、`Sheath0/1`、`ToggleAlternativeWeapon`）。移动、攻击、格挡等其余一切决策完全交给原生。
- 这样做的依据是兵种设计本身：双刃卫士**身上只有两把刀，没有第二种武器**，因此不存在任何有意义的武器选择可做；把这半个输入丢掉不损失任何战术行为，而原生在出生时已经配好的双刀就会保持不变。此前对该钩子的尝试之所以失败，是因为当时试图**用它去建立**配对（同帧请求双手，596 次全败）；现在只用它**阻止破坏**，与已证实可行的"原生自己建立"配合。
- 补丁面：`PATCH_OK=38; PATCH_FAIL=0`（检查点 37 + `ArrangementOrder` 1）；`HarmonyPatch(typeof(Agent)` 与 `HarmonyPatch(typeof(MissionWeapon)` 命中均为 **0**；XSLT 复验 102 个 action_set、`as_human_female_warrior` 原版 298 条。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 797696 字节、SHA-256 `F91398BD72DA652F4EA39C1FFF11F3F89371E67D1373FDC6478A5D79ECC7DDC0`。回滚点 `checkpoint/player-only-dual-blade` (`003fea5`)。
- 验收：① 开战后双刃卫士是否**保持**双刀（部署阶段已知可行，本轮验证能否延续到战斗中）；② 是否还有主剑重复拔出的抽搐（该逻辑已删除，预期消失）；③ 英雄预览是否仍正常；④ 追踪中 `GUARD_WEAPON_SELECTION_SUPPRESSED` 是否出现及其记录的主副手槽位。若开战后仍被收走，则原生 AI 的收刀不经过 `OnAIInputSet`，该方向的托管接口即告穷尽。

## 2026-08-30 补丁目标的安全/有毒分界确立；拆成"保持 + 建立"两半，各用安全落点

- 用户实测上一轮：**英雄预览模型正常**，士兵**稳定**拿出右手剑、左手剑留在鞘里，无闪烁。两条结论：
  1. **`ArrangementOrder` 补丁对预览是安全的。** 因此"任何 Harmony 补丁都会破坏预览"不成立，有毒的是特定类型。
  2. 副手"稳定在鞘里"说明阵型已不再收刀（保持问题已解决），**剩下的是从来没被拔出来过**（建立问题）。
- **补丁目标分界（本模组 1.5.2 的经验结论，后续一律遵守）：**
  | 目标 | 预览 | 依据 |
  |---|---|---|
  | `Mission.*` | 安全 | `2367e60` 使用 `Mission.SpawnAgent`，用户当时确认"什么都好" |
  | `ArrangementOrder.*` | 安全 | 本轮英雄预览正常 |
  | `MissionBehavior`（非补丁） | 安全 | 同上 |
  | **`Agent.*` per-call** | **有毒** | 两次复现，第二次判定已窄到只读不可变 id，排除判定条件变量 |
  | **`MissionWeapon.*` per-call** | **有毒** | 多轮复现 |
- 据此把功能拆成两半，各用安全落点：
  - **保持**：`GwpDualBladeShieldDirectionPatch`（`ArrangementOrder.GetShieldDirectionOfUnit` 后置，双刃卫士的 `None` → `AttackEnd`）。已由本轮实测验证既安全又生效（副刀不再被反复收放）。
  - **建立**：新增 `GwpDualBladeGuardBehavior`（`MissionBehavior`，**完全不是补丁**），在 `OnMissionTick` 中以**跨帧序列**为双刃卫士拔出副手：收主手 → 拔副手 → 拔主手 → 校验，每帧只推进一步，且仅在动作码为 `Other`/`Idle`/`Guard` 时动作。
- 选择跨帧序列的依据是既有实测：同一帧内同时请求主副手从不成功（原生 `WieldInitialWeapons` 即如此，另有一版把两个输入标志同帧注入 596 次而配对为 0）；同样三次调用摊到不同帧则达到 **553/597**。当年该方案失败的唯一原因是拔出后又被阵型收走 —— 而这正是本轮"保持"那一半已经堵上的。
- 序列上限 3 次/每兵，失败即停，绝不会重现早期的"反复拔刀收刀"。作用域只按 `Character.StringId == gwtwinblade` 判定（不可变读取），英雄与预览角色不可能匹配。
- 补丁面：`PATCH_OK=38; PATCH_FAIL=0` —— 检查点 37 + `ArrangementOrder` 1；`HarmonyPatch(typeof(Agent)` 与 `HarmonyPatch(typeof(MissionWeapon)` 命中均为 **0**；XSLT 复验 102 个 action_set、`as_human_female_warrior` 原版 298 条。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 798208 字节、SHA-256 `A4BFF87A7BA618BCDECB22DF7FA0D604CB72ECDFBA5BA7F8AA4633022374E561`。回滚点 `checkpoint/player-only-dual-blade` (`003fea5`)。
- 验收：① 英雄预览是否仍正常（本轮未新增任何有毒目标，预期不变）；② 双刃卫士左手刀是否拔出并**保持**；③ 追踪中 `GUARD_PAIR_RESULT` 的 `offhand=` 是否为 `WeaponItemBeginSlot`。若 ③ 显示配对成功而实机仍看不到，则问题在视觉挂点而非拔刀逻辑；若 ③ 始终为 `None`，则跨帧序列在无弓兵种上不成立，需回到 `Mission.SpawnAgent` 时机重新定位建立点。

## 2026-08-30 结论确立：任何针对 `Agent` 的 per-call 补丁都会破坏预览；改攻静态阵型方法（本方向最后一个候选）

- 用户实测：把 `EnforceShieldUsage` 旁路的判定收窄到**只读一个不可变角色 id**（不访问 Equipment/SpawnEquipment、不查 Mission、不写日志）后，**英雄预览仍然损坏**，双刀也未实现。已回退（标签 `failed/shield-bypass-narrow`，`852326f`），补丁面回到 `PATCH_OK=37`，`Agent`/`MissionWeapon` per-call 补丁 0 命中。
- **由此确立结论（排除了判定条件这一变量）：任何针对 `Agent` 的 per-call Harmony 补丁都会波及人物预览。** 连同此前对 `MissionWeapon.GetWeaponData`/`GetWeaponStatsData` 的多次复现，本模组的预览损坏与"补丁做了什么"无关，只与"在这两个类型上安装了 per-call 补丁"有关。
- 已确认良好的状态（模型正常、预览正常、双刀装备并显示，仅副手不出鞘）对应：检查点代码 + 双刃卫士兵种 + NPC 专用刀 + 一次性数据写入，`PATCH_OK=37`。这是当前的可用基线。
- 本方向的最后一个候选：**改攻静态方法 `ArrangementOrder.GetShieldDirectionOfUnit`，完全不碰 `Agent`。** 依据是两处收刀调用点走的是同一个静态帮助方法：
  - `ArrangementOrder.OnApply` → `agent.EnforceShieldUsage(GetShieldDirectionOfUnit(...))`
  - `Agent.UpdateFormationOrders` → `EnforceShieldUsage(ArrangementOrder.GetShieldDirectionOfUnit(...))`
  该方法对 `ShieldWall`/`Circle`/`Square` 之外的一切阵型返回 `UsageDirection.None`（`-1`），而 `None` 正是"收起副手"。盾墙中间排返回的是 `AttackEnd`（`4`），语义为"携带但不朝某方向举起" —— 正是需要的状态。
- 因此后置把双刃卫士的返回值由 `None` 改为 `AttackEnd`。一个补丁同时覆盖两个调用点；目标类型是阵型/命令类，与人物渲染无关，预览不存在 Formation。判定仍只读 `Character.StringId`，英雄与预览角色不可能匹配。
- 补丁面：`PATCH_OK=38; PATCH_FAIL=0`，相对基线只多 `ArrangementOrder.GetShieldDirectionOfUnit` 一个；`HarmonyPatch(typeof(Agent)` 与 `HarmonyPatch(typeof(MissionWeapon)` 命中均为 0；XSLT 复验 102 个 action_set、`as_human_female_warrior` 原版 298 条。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 796672 字节。回滚点 `checkpoint/player-only-dual-blade` (`003fea5`)。
- **明确的停止条件**：若本轮预览再次损坏，则说明连阵型类的 per-call 补丁也会波及预览，本模组在 1.5.2 上无法通过任何 Harmony 手段阻止阵型收刀，AI 双持方向应就此终止，保留"仅玩家双持 + 双刃卫士带刀但只用主手"的可用形态。若预览正常而副手仍不出鞘，则 `EnforceShieldUsage` 并非唯一收刀者，但此时至少证明阵型类补丁是安全的落点，可继续在该类内定位。

## 2026-08-30 底层定位：阵型 `EnforceShieldUsage(None)` 就是收刀者；旁路收窄到只匹配双刃卫士

- 用户实测上一轮：`EnforceShieldUsage` 旁路**反而弄丢了预览界面的英雄模型**，副手仍未出鞘。已回退该补丁（标签 `failed/shield-enforcement-bypass`，`9c95a97`），代码回到已确认良好的 `2381a22` 状态，仅保留其中无害的"限定所有 weapon usage"数据写入改进；离线预检回到 `PATCH_OK=37`，与检查点一致。
- **底层机制已在原生托管代码中定位到确切位置**。`ArrangementOrder.OnApply` 对阵型内每个 AI 单位执行：
  ```csharp
  formation.ApplyActionOnEachUnit(delegate(Agent agent) {
      if (agent.IsAIControlled) {
          var dir = GetShieldDirectionOfUnit(formation, agent, orderEnum);
          agent.EnforceShieldUsage(dir);
      }
      ...
  ```
  而 `GetShieldDirectionOfUnit` 只在 `ShieldWall`/`Circle`/`Square` 三种阵型下返回具体方向，**其余一律返回 `UsageDirection.None`**（`default: return Agent.UsageDirection.None;`）。`EnforceShieldUsage(None)` 即"收起副手物品"。
- 该机制一次性解释了此前所有观测：
  - 弓箭手当年"出生是双刀在手、随后就没了"—— 阵型指令在出生之后才应用；
  - 双刃卫士"从来看不到拔出"—— 它是 Infantry，编入阵型并立即整理；
  - 副手存活中位约 1.3 秒且与 AI decide timer 吻合 —— 阵型/命令刷新周期；
  - 物品层资格齐全（`qualified=True`）仍不出鞘 —— 资格决定"能不能拿"，阵型整理决定"允不允许留着"，是两件事。
  同时也确认 `ApplyActionOnEachUnit` 是普通 `foreach`，不涉及多线程，先前的线程安全猜测排除。
- **上一版旁路为何弄丢英雄模型，找到了具体嫌疑**：其判定条件是"AI + 有 Mission + 携带完整双刀"，而**自定义战斗的灰袍武将 `gwp_custom_commander` 恰好也携带完整双刀**。若英雄预览会创建真实 Agent，该补丁就会在英雄预览身上触发 —— 而丢的正是英雄模型。此外该判定还会访问 `agent.Equipment` / `agent.SpawnEquipment`，在预览构建阶段读取装备容器本身也有风险。
- 本轮据此把旁路收窄到**只匹配 `gwtwinblade` 这一个兵种 id**：`__instance?.Character?.StringId != GwpIds.TwinbladeTroopId`。这是一次不可变引用读取，**不访问 Equipment/SpawnEquipment、不查 Mission、不写日志**，且任何英雄或预览角色都不可能匹配（武将 id 为 `gwp_custom_commander`）。这些卫士本来就没有盾，跳过盾牌整理不损失任何原版行为。
- 补丁面：`PATCH_OK=38; PATCH_FAIL=0`，相对检查点只多 `Agent.EnforceShieldUsage` 一个；`HarmonyPatch(typeof(MissionWeapon)` 命中 0；XSLT 复验 102 个 action_set、`as_human_female_warrior` 原版 298 条。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 796672 字节、SHA-256 `6DBC847D16C945985AC5E45D52FBE831C31C16505617FC489F451D393651E492`。回滚点 `checkpoint/player-only-dual-blade` (`003fea5`)。
- 判读：① 英雄预览模型是否正常（若正常，则证实上一轮的损坏来自"判定条件误匹配武将"，而非"patch Agent 方法"本身）；② 双刃卫士左手刀是否出鞘并持续握住。若 ① 正常而 ② 仍失败，则 `EnforceShieldUsage` 并非唯一收刀者，需继续在 `Agent.UpdateFormationOrders` 等其余调用点定位；若 ① 再次损坏，则"任何针对 `Agent` 的 per-call 补丁都会波及预览"成立，该方向终结。

## 2026-08-30 重新梳理：v1.4.8 是四个脚本协同，当前缺的是 `EnforceShieldUsage` 旁路

- 用户实测：**模型正常、副剑显示正常（已装备、可见），但仍不出鞘**。追踪确认资格这次真的写进去了：
  ```
  NPC_ITEM_SETUP | gwdualbladeoffhandai; collision=bo_sword_one_handed;
                   flags=MeleeWeapon, HasHitPoints, CanBlockRanged; maxDataValue=500; qualified=True
  ```
  因此得到一条干净的否定结论：**仅在物品层补齐资格（标志 + 耐久 + 碰撞体），在 1.5.2 上不足以让原生 AI 拔出副手。**
- 用户提供关键历史：v1.4.8 的可用实现是**四个脚本协同**，后来整合进一个脚本，随后因未提交而丢失。与记录逐一对号后确认这四个是：
  | 当年补丁类 | 目标方法 | 当前状态 |
  |---|---|---|
  | `GwpDualBladeAiWeaponDataPatch` | `MissionWeapon.GetWeaponData` | 已由数据层一次性写入替代（碰撞体） |
  | `GwpDualBladeAiWeaponStatsPatch` | `MissionWeapon.GetWeaponStatsData` | 已由数据层一次性写入替代（标志 + 耐久） |
  | `GwpDualBladeAiEquipmentSyncScopePatch` | `Agent.EquipItemsFromSpawnEquipment` | 仅为上两者的作用域，数据层方案下不再需要 |
  | **`GwpDualBladeAiShieldEnforcementPatch`** | **`Agent.EnforceShieldUsage`** | **从未与"真正生效的资格"同时存在过** |
- 也就是说，此前每一轮都只复现了四分之二或四分之三，而第四块从来没和有效资格同时上过场。记录对它的原话是"**避免原版盾墙整理再次把非标准副手直接剔除**"，即原生阵型盾墙逻辑会主动剔除非标准副手物品 —— 这正与"资格齐全但副手始终不出鞘"的现象吻合。
- 本轮补上第四块 `GwpDualBladeShieldEnforcementPatch`：对通过 `IsDualBladeNpc`（AI + 有 Mission + 携带完整双刀）的 agent 跳过 `Agent.EnforceShieldUsage`。选择它而非四者全复原的理由：它是 **Agent 实例方法**，tableau 走 `AgentVisuals` 且没有 Agent，因此不可能触及预览链；而已被隔离实验证明有毒的是 `MissionWeapon` 那两个 per-call 补丁，本轮仍保持 0 命中。这些角色本来就没有盾，跳过它不损失任何原版行为。
- 同时修正一处可能的遗漏：资格写入原先只作用于 `blade.PrimaryWeapon`，现改为遍历 `blade.Weapons` 的**每一个 usage**。锻造物可能生成多个 `WeaponComponentData`，原生读取的是当前 usage，只改第一个可能改不到实际生效的那个。诊断输出新增 `usages=已限定/总数`。
- 需求放宽已生效：用户明确副手刀可以挡飞箭，因此不再需要 `Mission.MissileHitCallback` 还原补丁，战斗热路径上没有任何新增全局入口。碰撞体按用户要求限制为与剑 mesh 一致（`bo_sword_one_handed`），未借用大盾体积。
- 补丁面：`PATCH_OK=38; PATCH_FAIL=0`，相对检查点（37）**只新增 `Agent.EnforceShieldUsage` 一个**；`HarmonyPatch(typeof(MissionWeapon)` 命中 0；XSLT 复验 102 个 action_set、`as_human_female_warrior` 原版 298 条。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 797184 字节、SHA-256 `11AEFADAA2C8965BCAA30AB752E4EE6CC0546FD2109D119C51AFB1297674DBF4`。回滚点 `checkpoint/player-only-dual-blade` (`003fea5`)。
- 验收：① `NPC_ITEM_SETUP` 的 `usages=` 是否全部限定；② 是否出现 `NPC_SHIELD_ENFORCEMENT_BYPASSED`；③ **双刃卫士左手刀是否出鞘并持续握住**；④ 模型/预览是否仍正常（预期不变）。若 ①② 都成立而 ③ 仍失败，则四块拼齐后仍不成立，说明 v1.4.8 结论不适用于 1.5.2，该方向可判定终结。

## 2026-08-30 上一轮资格标志根本没写进去（反射目标类型判断错误），已直接赋值修正

- 用户实测上一轮：**模型正常**（零补丁方案确实保住了预览），但左手刀仍未出鞘。
- 追踪给出了确切原因，而且不是机制问题：
  ```
  NPC_ITEM_SETUP | gwdualbladeoffhandai; collision=bo_sword_one_handed; flags=MeleeWeapon; maxDataValue=500
  ```
  `collision` 与 `maxDataValue` 都写入成功，**`flags` 仍然只有 `MeleeWeapon`** —— `HasHitPoints | CanBlockRanged` 一个都没生效。
- 根因：`WeaponComponentData.WeaponFlags` 在 1.5.2 中是 **`public WeaponFlags WeaponFlags;`，即公开字段而非属性**（`core-latest/TaleWorlds.Core/WeaponComponentData.cs:19`）。上一轮用 `AccessTools.Property(typeof(WeaponComponentData), "WeaponFlags")` 取，返回 `null`，又被 `?.SetValue(...)` 的空条件调用静默吞掉，因此既没写入也没抛异常。**该字段是公开的，根本不需要反射**，已改为 `weapon.WeaponFlags |= Qualification;` 直接赋值。
- **结论修正**：因此"给 NPC 副手补 `HasHitPoints | CanBlockRanged` 能否让原生 AI 持刀"这一机制，在上一轮**从未被真正验证过** —— 那轮测的是一个空操作。记录中 v1.4.8 的 A/B 结论仍是当前最可靠依据，本轮才是对它的第一次有效复现。
- 诊断加固：`NPC_ITEM_SETUP` 现在从物品上**回读**写入结果，并输出 `qualified=`（`(WeaponFlags & Qualification) == Qualification`）。下一轮只需看这一个布尔值即可判定写入是否真正生效，不会再出现"以为写了其实没写"。
- 需求放宽（用户明确）：**副手刀可以挡飞箭**，用户只要求双刀在观感与体验上与玩家一致。因此 `Mission.MissileHitCallback` 还原补丁**不再需要**，战斗热路径上不新增任何全局入口，符合上一轮确立的红线。用户同时指出碰撞体应限制为与剑 mesh 一致 —— 当前实现正是如此（`CollisionBodyName = BodyName = bo_sword_one_handed`，未借用大盾体积）。
- 补丁面保持不变：离线预检 `PATCH_OK=37; PATCH_FAIL=0`，与用户确认预览正常的检查点逐一相同；全仓库 `HarmonyPatch(typeof(MissionWeapon)` 与 `HarmonyPatch(typeof(Agent)` 命中均为 0；XSLT 复验 102 个 action_set、`as_human_female_warrior` 原版 298 条。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 795648 字节、SHA-256 `1FD794C2279ABE6B6DDFC7BA29E1FDC924156ABBB6C877ABC4955497AB5EA335`。回滚点 `checkpoint/player-only-dual-blade` (`003fea5`)。
- 验收：① `NPC_ITEM_SETUP` 的 `qualified=True` 且 `flags` 含 `HasHitPoints, CanBlockRanged`；② 双刃卫士左手刀是否出鞘并**持续**握住；③ 模型/预览是否仍正常（补丁面未变，预期不变）；④ 格挡是否崩溃。若 ① 为 True 而 ② 仍失败，则 v1.4.8 的 A/B 结论在 1.5.2 上不成立，届时该方向可判定终结。

## 2026-08-30 隔离结论：预览损坏来自代码而非数据；改用零补丁的 NPC 专用副手刀（待用户实机验收）

- **单变量隔离得到明确结论。** 用户实测上一轮（检查点代码 + 仅新增带双刀的双刃卫士）：**模型正常、预览正常、双刀已装备上，只有副手拔不出来**。因此：
  - "兵种携带双刀"这一数据事实**不会**破坏人物预览；
  - 两周来反复出现的模型异常**全部来自代码侧的 per-call Harmony 补丁**；
  - 上一轮"副剑没装备上"确认是当时那两个补丁引入的，与兵种数据无关。
  这是两周来第一次把数据与代码两个变量真正分开，此前所有归因（动作集注入、槽位布局、`GetWeaponData`）均已被逐一证伪。
- 由此确立设计红线：**任何 NPC 双刀方案都不得在 `MissionWeapon`、`Agent` 装备/输入链上新增 per-call 补丁。**
- 本轮据此改为**零补丁**实现，只用数据 + 一次性对象写入：
  - 新增 `gwdualbladeoffhandai`：与玩家副手刀**复用同一个锻造模板与同样四个部件**，因此外观完全一致，只有 item id 不同。双刃卫士的 `Item0` 改用它，玩家的 `gwdualbladeoffhand` 完全不动。
  - 新增 `GwpDualBladeNpcItemSetup`，在 `OnGameInitializationFinished`（物品 XML 已加载）对该 NPC 物品**一次性**写入：`CollisionBodyName = BodyName`、`WeaponFlags |= HasHitPoints | CanBlockRanged`（按位 OR，`MeleeWeapon` 与剑用途保留）、`MaxDataValue = 500`。
  - 取证依据：`Crafting.SetWeaponData` 中 `maxDataValue` 只对投掷类赋值，`OneHandedSword` 恒为 `0`；`BladeData` 无碰撞体字段。因此这两项是锻造物在 XML 层**拿不到**的，只能写到已加载的对象上 —— 这也是历史上必须 patch `MissionWeapon` 的真正原因，现在用一次性写入替代。
  - 识别侧新增 `IsOffHandBladeId`，玩家刀与 NPC 刀在装备判定、副手伤害类型、bone-20 击倒三处一视同仁，因此双刃卫士的左手刀伤害与击倒与玩家一致。
- **关键验证：补丁面与检查点完全相同。** 离线预检 `PATCH_OK=37; PATCH_FAIL=0`，与用户确认预览正常的检查点**逐一相同**；全仓库 `HarmonyPatch(typeof(MissionWeapon)` 与 `HarmonyPatch(typeof(Agent)` 命中均为 **0**；XSLT 复验 102 个 action_set、`as_human_female_warrior` 原版 298 条。类型数仅由 407 增至 408（新增的一次性设置类）。
- 如实说明的已知偏差：`CanBlockRanged` 会让**双刃卫士的**副手刀挡住弓箭，与"挡不住弓箭"的要求不符。玩家的刀不受影响。其还原方案（`Mission.MissileHitCallback`）是战斗热路径上的全局入口，按上述红线**暂不加入**，待本轮确认副手能持稳后再单独评估。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；30 个 XML/XSLT/mbproj 解析失败 0；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 795648 字节、SHA-256 `1DA7B1419AB70BE8D8E2C04F9933D2DAB0A996BEE5ADCB3F73C5AA8BC94D7FB4`。回滚点 `checkpoint/player-only-dual-blade` (`003fea5`)。
- 验收顺序：① 追踪中 `NPC_ITEM_SETUP` 是否写入成功且 `flags` 含 `HasHitPoints, CanBlockRanged`、`maxDataValue=500`；② 人物预览/百科是否仍正常（补丁面未变，预期不变）；③ 双刃卫士左手刀是否持续出鞘；④ 格挡时是否崩溃。

## 2026-08-30 单变量隔离：检查点代码 + 仅新增带双刀的双刃卫士兵种（待用户实机验收）

- 用户实测上一候选：副剑仍未装备上，模型依旧异常。用户据此要求回退到稳定点、**只保留双刃卫士并给它双刀**，判断依据是"模型正常 + 进场带双剑"这两点应当能同时成立，只是副剑可能拔不出来。该判断与本方证据一致，本轮照此执行。
- 本轮是两周以来**第一次真正的单变量隔离**。此前每一轮都同时改动代码与数据，因此"预览异常"始终无法归因。当前状态：
  - **代码与检查点 `003fea5` 逐字节一致**（`git diff 003fea5 -- "*.cs"` 为空）。已删除 `GwpDualBladeAiNativeSyncPatch.cs` 与 `GwpDualBladeNpcBehavior.cs`，`GwpDualBladeActionSetPatch`/`GwpAgentApplyDamageModel`/`GwpDualWieldingPatch`/`SubModule` 全部还原。离线预检 `PATCH_OK=37; PATCH_FAIL=0`、类型数 407，与检查点完全相同。
  - **数据层唯一差异**：新增兵种 `gwtwinblade`（`Item0=gwdualbladeoffhand`、`Item1=gwdualblademainhand`，无弓箭，level 26，由 `gwrecruit` 升级）与其中文名字符串；`gwarcher` 保持纯远程（双刀命中 0）。动作集未动（XSLT 复验 102 个 action_set、`as_human_female_warrior` 原版 298 条）。
- 该状态的判定力：检查点已由用户确认"预览正常、仅玩家可用双刀"，两者之间只差"一个兵种携带双刀"这一条数据事实。因此本轮实测可以一次性回答两周未决的问题：
  - **预览仍异常** → 成因就是"兵种携带双刀"这一数据事实本身（与任何 Harmony 补丁无关）。下一步应转向物品/装备数据侧：对比 `gwonehandedsword` 与两把双刀在 `CraftedItem` 解析结果上的差异，重点查 `HeldInOffHand` 锻造物在 `CharacterTableau` 中的处理，而不再改任何运行时代码。
  - **预览恢复正常** → 成因是代码侧的全局补丁；此前所有候选中被反复怀疑的三处（动作集注入、`GetWeaponData`、`GetWeaponStatsData`）都已被单独证伪或撤销，需要重新逐一排查，但至少可以确定与兵种数据无关。
  - 同时确认第二点：**双刃卫士是否带着两把刀进场**（装备栏/背后是否有第二把刀）。上一轮"副剑没装备上"是在有补丁的状态下出现的；若本轮纯数据状态下能正常装备，则可确认该现象由那两个补丁引入，而非兵种数据问题。
- 预期的已知不足：本轮没有任何 AI 双持代码，双刃卫士只会用主手刀作战，左手刀不会拔出，也不具备左手伤害与击倒。双语 README 已按此如实描述，不写成已实现。
- 失败候选全部保留：`failed/npc-native-qualification`（`3459f7c`，原生资格代理 + 碰撞体）、`failed/npc-ai-input-boundary`（`ca3df79`，AI 输入边界）、两个 stash、reflog 中的 Mission Tick 方案。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；30 个 XML/XSLT/mbproj 解析失败 0；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 794112 字节、SHA-256 `6119130BC2239284A9B1549DD255F6320516C2E95D869120FE312D049ABD5113`。回滚点 `checkpoint/player-only-dual-blade` (`003fea5`)。

## 2026-08-30 撤销上一条错误结论；补上碰撞体解决"副剑未装备"；预览成因仍未定位

- **撤销上一条记录的"永久结论"**：上一轮断言"`MissionWeapon.GetWeaponData` 不可被 patch，它就是人物模型异常的成因"。本轮已把该补丁完整移除，**预览依旧异常** —— 该结论被证伪，予以撤销。这是本方在预览成因上连续第二次误判（前一次归因于共用动作集注入），记录在此以免后续再次沿此方向浪费轮次。
- 上一轮修正确实生效的部分：`MissionWeapon` 是 struct，Harmony 对值类型实例方法需 `ref __instance`；改正后资格代理才真正具备生效条件。
- **新现象"副剑没有装备上"（此前从未出现）已定位**：上一轮在移除 `GetWeaponData` 补丁时，连带删掉了碰撞体设置，于是副手只有 `HasHitPoints | CanBlockRanged` 而**没有任何碰撞对象**。带耐久的物品在原生注册时需要碰撞体，缺失导致该武器根本没能挂上，表现为"没装备"。这也与记录中"缺盾碰撞对象时首次真实格挡崩在 `Native.dll+0x73ddf8`（`rdx=0` 空源指针）"同源 —— 都是缺少碰撞对象。
- 修复方式改走**数据层一次性写入**，不再碰 `GetWeaponData`：反编译确认 `MissionWeapon.GetWeaponData` 中 `weaponData.CollisionShape` 直接取自 `Item.CollisionBodyName`，因此在物品加载后对 `ItemObject.CollisionBodyName` 写一次即可（首个双刀 AI 装备作用域开启时触发，带幂等保护与异常兜底，写入结果记 `AI_NATIVE_COLLISION_BODY`）。
- **不使用大盾碰撞体**：历史方案填的是 `bo_wlarge_shield`，但崩溃本质是空指针，任何**有效**碰撞体即可满足；借用大盾体积会连玩家的剑一起把碰撞盒撑大。本轮填入刀自身的 `BodyName`（`bo_sword_one_handed`）。
- 当前 NPC 双刀实现面收敛为两处：`Agent.EquipItemsFromSpawnEquipment` 的线程局部作用域，以及作用域内 `MissionWeapon.GetWeaponStatsData` 的 `WeaponFlags |= HasHitPoints | CanBlockRanged`、`MaxDataValue = 500`。加上一次性的 `ItemObject.CollisionBodyName` 写入。托管 `MissionWeapon` 不改、玩家不进作用域、无预览侧补丁、无动作集改动（XSLT 复验仍为 102 个 action_set、`as_human_female_warrior` 原版 298 条）。
- **预览成因仍未定位，需要用户一次观察来切分**：检查点状态（无任何兵种携带双刀、无这两个补丁）用户确认预览正常；当前状态同时引入了"兵种携带双刀"与"MissionWeapon 补丁"两个变量。下一轮实测时只需额外确认一点 —— **百科里的原版兵种（如古拉姆骑兵）是否也异常**：
  - 原版兵种也异常 → 成因是代码（全局补丁），与本模组兵种数据无关；
  - 只有灰袍兵种异常 → 成因是"兵种携带双刀"这一数据事实本身。
  该观察不额外增加测试轮次，但能一次性把两周来一直没分开的两个变量切开。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；离线预检 `PATCH_OK=39; PATCH_FAIL=0`，类型数 410；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 797184 字节、SHA-256 `64C7A0D1BB22A4FBBA6FD464FEE9A0E4833E07EA64912E9CDE60AA1034AA3073`。回滚点 `checkpoint/player-only-dual-blade` (`003fea5`)。
- 验收顺序：① `AI_NATIVE_COLLISION_BODY` 是否写入成功；② 双刃卫士**是否装备上副剑**（本轮首要修复目标）；③ 左手剑是否持续出鞘；④ 格挡是否崩溃；⑤ 百科中原版兵种与灰袍兵种的预览分别是否异常。

## 2026-08-30 定位到预览损坏的真正机制：`MissionWeapon.GetWeaponData` 不可被 patch（永久结论）

- 用户实测上一候选：预览界面模型异常**同样复现**；双刃卫士游戏里**没有第二把刀**。追踪显示 `AI_NATIVE_SYNC_BEGIN` 出现 199 次（角色为 `gwtwinblade`），作用域正常开合，但没有任何实际效果。
- **两个现象同源，且都定位到具体代码缺陷：**
  1. **没有第二把刀**：`MissionWeapon` 是 **struct**。Harmony 对值类型实例方法要求 `ref MissionWeapon __instance`，上一版写成了 `in MissionWeapon __instance` —— 绑定失败，`IsOffHandBlade` 从未命中，**资格标志一次都没施加**。
  2. **预览损坏**：这才是关键。既然 (1) 说明那两个 postfix **实际什么都没做**，预览却照样坏 —— 说明**光是把补丁挂在 `MissionWeapon.GetWeaponData` 上就会破坏预览**，与补丁内容无关。
- 机制解释：`GetWeaponData` **按值返回 `WeaponData`**，该结构体内含 `MetaMesh WeaponMesh/HolsterMesh/FlyingMesh`、`Material TableauMaterial`、`PhysicsShape Shape/CollisionShape` 等原生句柄。Harmony 包住一个大型按值返回的结构体方法，会破坏**所有调用方**收到的数据；`AgentVisuals` 正是通过它构建武器网格，因此人物 tableau 首当其冲。
- **第三次独立佐证**：仓库中 `GwpBlackLordShieldBehavior.cs` 至今是空实现，其注释写明"Intentionally empty during native-shutdown diagnosis. The previous `MissionWeapon.GetWeaponData` postfix retrieved and recolored private..." —— 该文件当年也是因为在这个方法上挂 postfix 引发原生关闭问题而被清空。加上本轮，以及维护记录中多轮"安装这两个全局补丁会污染 CharacterTableau/AgentVisuals"的结论，证据链已经闭合。
- **永久结论（写死，任何方案不得违反）：`MissionWeapon.GetWeaponData` 绝对不可被 Harmony patch。** 此前一周反复出现、每次都被归因到别处（动作集注入、槽位布局、CraftedItem 标志、预览隔离补丁）的"人物模型消失/姿态异常"，真正的成因就是它。历史上那次"用 `WeaponData.CollisionShape = bo_wlarge_shield` 修复格挡崩溃"的做法正是走这个方法，**因此不可复用**。
- 本轮修改：完整删除 `GetWeaponData` 补丁及其碰撞体逻辑；`GetWeaponStatsData` 的 `__instance` 改为 `ref MissionWeapon`。`GetWeaponStatsData` 返回的是托管数组引用（`WeaponStatsData[]`），不是按值大结构体，postfix 安全，且资格标志与耐久本来就在它里面。新增一次性 `AI_NATIVE_SYNC_APPLIED`，记录实际写入的 `flags` 与 `maxDataValue`，用于确认这次真的生效。
- **已知未决风险（如实说明）**：`CanBlockRanged` 生效后若没有盾碰撞对象，历史上首次真实格挡会崩在 `Native.dll+0x73ddf8`。原来的解法走 `GetWeaponData`，已被本轮结论封死。若本轮出现格挡崩溃，下一步方向是**在物品加载后一次性设置 `ItemObject.CollisionBodyName`**（一次性托管对象写入，不是热路径补丁），而不是重新 patch `GetWeaponData`。
- 同样如实说明：`CanBlockRanged` 目前会让副手刀挡住弓箭，与需求不符。其还原方案（`Mission.MissileHitCallback`）仍刻意未与本轮捆绑，待持刀与格挡两项确认后再单独加入。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；离线预检 `PATCH_OK=39; PATCH_FAIL=0`（检查点 37 + 装备作用域 + 武器统计），类型数 410；全仓库 Harmony 目标中 `GetWeaponData` 命中 **0**；XSLT 复验 102 个 action_set、`as_human_female_warrior` 保持原版 298 条；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 796672 字节、SHA-256 `CD9BCB24856C605532F8E38688D18F5556C59260C96361D791CB8F9C9FDA1944`。回滚点 `checkpoint/player-only-dual-blade` (`003fea5`)。
- 验收顺序：① **人物预览/百科是否恢复正常**（这是本轮最主要的验证目标，若恢复即证明上述永久结论成立）；② `AI_NATIVE_SYNC_APPLIED` 是否出现且 `flags` 含 `HasHitPoints, CanBlockRanged`；③ 双刃卫士左手剑是否持续出鞘超过 3 秒；④ 格挡时是否崩溃。

## 2026-08-30 按 v1.4.8 已成功配方重建 NPC 双刀：原生资格代理 + 独立双刃卫士兵种（待用户实机验收）

- 用户指出关键事实：**v1.4.8 时期本项目配合 ROT 确实做出过 AI 双持**，当时不是逼弓箭手用，而是单独造了兵种「双刃卫士」，之后才移植的双刀击倒机制。据此回查开发记录，找到了完整配方。
- **该实现从未进入过 Git**：`eda353f` 是首个含双刀文件的提交，那时同步补丁与双刃卫士兵种都已被删除；`git log -S` 在全历史中查不到 `GwpDualBladeAiNativeSyncPatch` 与 `gwtwinblade`。两者都只存在于当时未提交的工作树，属于此前记录的"项目管理问题导致功能丢失"。本轮按维护记录重建。
- 记录中的 A/B 阶梯（2026-08-27/28）明确给出原生 AI 保留副手所需的条件：
  1. 仅 `MeleeWeapon + HasHitPoints`：200 名双刃卫士在 `WieldInitialWeapons()` 后达到 `actualOff=WeaponItemBeginSlot`，**约 2.3 秒后统一变回 `None`** —— 耐久不足以让原生长期占用副手。
  2. 追加 `CanBlockRanged`：记录原文"**NPC 已成功拔出左手剑并保持双持动作**"。这正是原生副手资格实际检验的标志，与"原版 53 件 `HeldInOffHand` 物品全是盾"完全自洽。
  3. 带 `CanBlockRanged` 但没有盾碰撞对象时，首次真实格挡崩在 `TaleWorlds.Native.dll+0x73ddf8`（`mov rbp,[rdx+8]`，`rdx=0`，空源对象复制）。补上真实碰撞体是当时的应对。
  4. 清除 `WeaponMask`（去掉 `MeleeWeapon`）会引发另一处原生崩溃，**不得重现**；ROT 的左右手剑始终保留 `OneHandedSword + MeleeWeapon`。
- 本轮实现 `GwpDualBladeAiNativeSyncPatch`：在**一次** `Agent.EquipItemsFromSpawnEquipment(bool,bool,bool,int)` 的线程局部作用域内（`[ThreadStatic]`，仅当 `IsDualBladeNpc` 成立即 AI + 有 Mission + 携带完整双刀），对送往 Native 的**副手副本**执行：`WeaponFlags |= HasHitPoints | CanBlockRanged`（按位 OR，`MeleeWeapon` 与剑用途原样保留）、`MaxDataValue = 500`、`WeaponData.CollisionShape = bo_wlarge_shield`（一次性解析并缓存，取不到就不改）。托管 `MissionWeapon` 不改、玩家不进作用域、不生成实体、不重挂、不用 Mission Tick。
- **不添加任何预览侧补丁**。历史上正是"为隔离预览而增加的展示装备码替换/tableau 补丁"反复搞坏人物模型。tableau 走 `AgentVisuals`，不会调用 `Agent.EquipItemsFromSpawnEquipment`，因此作用域关闭时这两个 postfix 原样返回原生数据。本轮 XSLT 复验仍为 102 个 action_set、`as_human_female_warrior` 保持原版 298 条，动作集结构完全没动。
- 兵种侧按 v1.4.8 形态重建 `gwtwinblade`（灰袍守护者双刃卫士）：`level=26`、`default_group="Infantry"`、由 `gwrecruit` 升级、**只带 Weapon0 副手刀 + Weapon1 主手刀，没有弓箭**。这一点是关键 —— 身上没有第二种武器，原生 AI 就没有远程/近战重选可走，直接避开了此前把双刀塞给弓箭手所引出的一整类问题。`gwarcher` 保持纯远程，不再携带双刀。新增中文名字符串 `gwp_troop_twinblade`。
- 伤害侧新增 `IsDualBladeCombatant`（任何携带完整双刀者，玩家或 NPC），`GwpAgentApplyDamageModel` 与 `GwpDualWieldingPatch` 改用它，使双刃卫士的左手伤害类型与击倒判定与玩家一致；地面拾取仍保留玩家专属的 `IsEligibleDualBladeUser`。
- **本轮刻意未做的一项**：`CanBlockRanged` 会让副手刀同时挡住弓箭，与用户"挡不住弓箭"的要求不符。记录显示当时的解法是用 `Mission.MissileHitCallback` 把该副手的远程盾挡结果还原为普通命中。该补丁是战斗热路径上的全局入口，本轮**不与资格代理捆绑**，以便万一出问题能明确归因；待 NPC 确认能持刀且近战格挡不崩后再单独加入。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；离线预检 `PATCH_OK=40; PATCH_FAIL=0`（检查点 37 + 新增 3 个入口全部解析成功），类型数 411；30 个 XML/XSLT/mbproj 解析失败 0；仓库 `_Module` 与 live 36 个可部署文件差异 0；`gwarcher` 装备中 `gwdualblade` 命中 0。客户端/编辑器 DLL 均为 797184 字节、SHA-256 `DDD7D43D990A4028683BC62DDCCEEBD3997D323DA4CA29D78F062D4836330890`。回滚点 `checkpoint/player-only-dual-blade` (`003fea5`)。
- 验收重点（按记录当年的顺序）：① 人物预览/百科是否仍正常（本轮未动预览链，预期不变）；② 双刃卫士出生后左手剑是否**持续**出鞘、超过 3 秒不被清空；③ 玩家攻击双刃卫士、双刃卫士格挡时是否崩溃；④ 双刃卫士能否用双持攻击与四向近战格挡。若 ② 通过而 ③ 崩溃，说明碰撞体仍不足，应调整碰撞体而非撤回 `CanBlockRanged`。

## 2026-08-30 NPC 双刀全部候选失败，按用户要求回退到检查点重新开发

- 用户结论：两个都是老问题（人物模型异常、NPC 只拿单刀），要求回退到稳定点重新开发。
- 已回退：代码与检查点 `003fea5`（标签 `checkpoint/player-only-dual-blade`）**逐字节一致**（`git diff 003fea5 -- . ':!docs'` 为空）。复验：离线预检 `PATCH_OK=37; PATCH_FAIL=0`、类型数 407、`spnpccharacters.xml` 中 `gwdualblade` 命中 0、XSLT 输出 102 个 action_set 且 `as_human_female_warrior` 保持原版 298 条、30 个 XML 解析失败 0、仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 794112 字节、SHA-256 `ACB648082282D25FF95E043D58AFD791CE496E0FDDEB19C6E8E91F5D52953AF0`。
- 失败资产全部保留，可随时取回：标签 `failed/npc-ai-input-boundary`（`ca3df79`，AI 输入边界方案）、stash `failed-npc-dual-input-2026-08-30`、stash `failed-direction2-shield-offhand-2026-08-30`、reflog 中的 Mission Tick 方案（至 `3693545`）。

### NPC 副手：已穷尽的三条路径（重新开发时不要重走）

1. **拔刀 API（事后补拔）** —— `TryToWieldWeaponInSlot` 单槽位、`WieldInitialWeapons` 整套。主手被占用时 620/620 全部被拒。
2. **Mission Tick 分帧序列**（收主手 → 拔副手 → 拔主手）—— 配对成功率 553/597 ≈ 92.6%，但 `NPC_PAIR_LOST` 2169 次，副手存活中位 `1.29s` 后被清空，与原生 AI decide timer 吻合；以重试对抗必然产生"反复拔刀收刀"。
3. **AI 输入边界 `Agent.OnAIInputSet`** —— 钩子确实触发（`NPC_DUAL_INPUT_MELEE_PAIR` 596 条），但同帧同时请求双手无效；改为分帧后，因错把"副手优先"用在持弓状态而失败（`GAVE_UP` 398），修正为"先 `Sheath0` 收弓 → 拔副手 → 拔主手"后仍未通过实机。

### 引擎侧已确证的硬约束（永久结论）

- 原版 `mpitems.xml` 中 **53 件 `HeldInOffHand` 物品全部是 `Type="Shield"`、`weapon_class="LargeShield"`，`MeleeWeapon` 命中 0**。引擎的"副手"概念等同于盾。
- `WeaponComponentData.IsShield` = 不含 `MeleeWeapon`/`RangedWeapon` 且同时具备 `HasHitPoints | CanBlockRanged`。因此"引擎认可的副手"与用户要求（有伤害、挡不住弓箭）**定义上互斥**，副手改盾类方向作废。
- 同一输入/调用帧内同时处理主副手，只有一只手会留下。
- 主手被占用时，单独请求副手一定失败；必须先让主手为空。
- 原生 AI 按 decide timer（约 1.3 秒）周期性重拔主手，副手随之被清；盾能留住是因为原生有独立的盾重装逻辑。
- 玩家之所以可用，是因为 `Equipment.GetInitialWeaponIndicesToEquip` 在出生时认 `HeldInOffHand`，且玩家不触发 AI 武器重选 —— ROT 钻的就是这个空子，且 ROT **从未**把双刀给过任何 AI（`dual_blades` 仅出现在其 `items.xml`，兵种/领主/英雄/装备表 0 命中）。

### 人物模型异常：尚未定位，且与本模组数据层不相关

- 用户两次提供的模型异常截图（读档界面人物横躺悬空、坐骑正常）**游戏版本均为 `v1.4.8.119303`**，模组列表含 `War Sails v1.2.8.119303`；而本模组当前开发目标为 `v1.5.2.120933`。该版本差异尚未与用户确认，是后续定位该问题时必须先排除的变量。
- 取证显示该现象与本模组数据层无关：`git diff 2367e60 HEAD -- _Module/ModuleData/` 为空，即出现异常时的数据层与用户曾确认"什么都好"的状态逐字节相同；且关闭 GreyWarden 后一切正常这一结论来自更早的 1.5.2 会话，与本次 1.4.8 截图不是同一条证据线。
- 重新开发时应**先单独定位模型异常**（确认复现所用的游戏版本、是否为预览链、是否波及原版兵种），再动 NPC 双刀 —— 两个问题混在一起是此前多轮反复的主要原因。

## 2026-08-30 NPC 双刀第四轮：修正拔刀顺序（持弓时必须先收主手），并杜绝失败时的动画循环

- 用户实机：弓箭手停射拔左手剑异常，**持续触发拔剑/收剑动画**；模型异常未解决。
- 追踪 `20:56` 会话给出直接原因，且是本方上一版自身的实现错误：
  ```
  NPC_DUAL_INPUT_OFFHAND | before=Wield1, Walk, Stand; after=Wield0, Walk, Stand; main=Weapon2; offhand=None
  ```
  `main=Weapon2` 表示弓箭手**手里还握着弓**，AI 发出的 `Wield1` 是要从弓切到主手刀；而上一版把这个请求**替换成了 `Wield0`**（副手）。主手被弓占着时副手不可能拔出，于是重试 6 次 → `NPC_DUAL_INPUT_GAVE_UP` → 60 帧冷却 → 恢复入口再次触发，形成约 1 秒一轮的拔/收循环。会话计数印证：`NPC_DUAL_INPUT_OFFHAND` 398、`GAVE_UP` 398、`MAINHAND` 与 `KEEP_PAIR` 均为 0，一步都没走通。
- 错误根源是把"副手优先"从**空手**场景（`WieldInitialWeapons` 从双手皆空开始）照搬到了**持弓**场景。本项目此前测得的可用序列本来就是"**主手先空出来** → 拔副手 → 拔主手"（分帧执行，553/597）。
- 本轮修正：状态机改为四步 `Sheathing → OffHandRequested → MainHandRequested → 落定`。截获 AI 的近战意图后**先发 `Sheath0` 收掉主手（弓）**，确认主手为空后再请求 `Wield0`，确认副手到手后再请求 `Wield1`。每一步都先校验上一步是否真的落地才推进。
- 同时杜绝动画循环：任一步骤重试超过 6 帧即 `Abandon` —— 把主手刀放回并进入 180 帧冷却；连续 3 次序列失败后该 agent 永久停用（`Step.Disabled`，回调直接返回）。**失败的最坏结果是 NPC 单刀作战，不会再出现反复拔刀收刀。** 另修掉 `Abandon` 的 `eventFlag` 按值传参缺陷（改动传不回调用方，主手刀放不回去）。
- 模型异常方面本轮未做改动，因为取证显示与之无关：`git diff 2367e60 HEAD -- _Module/ModuleData/` **为空**，即当前数据层与用户曾确认"什么都好"的 `2367e60` **逐字节相同**；与检查点 `003fea5` 的唯一差异是 `spnpccharacters.xml`（弓箭手重新携带双刀），而该状态在 `2367e60` 时预览正常。本轮亦无新崩溃转储。因此若"模型异常"指的是**百科/自定义战斗预览**，则其成因不在当前数据层，需要用户区分它与"战场上弓箭手因拔刀循环而看起来不正常"是否为同一现象后再定位；若指的就是战场上的拔刀循环，则本轮修正已直接针对它。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；离线预检 `PATCH_OK=38; PATCH_FAIL=0`，类型数 412；XSLT 复验 102 个 action_set、`as_human_female_warrior` 保持原版 298 条；仓库 `_Module` 与 live 36 个可部署文件差异 0；客户端/编辑器 DLL 均为 798208 字节、SHA-256 `2782CCE32ABC5368A7A193DF37B52864B15D546D12A26D0995C7716BEE89F994`。
- 判读：按序应出现 `NPC_DUAL_INPUT_SHEATHE` → `NPC_DUAL_INPUT_OFFHAND` → `NPC_DUAL_INPUT_MAINHAND` → `NPC_DUAL_INPUT_KEEP_PAIR`。若停在 `SHEATHE` 说明 `Sheath0` 未能清空主手；若停在 `OFFHAND` 说明主手已空但副手仍被拒——那将是"经由输入边界也无法把非盾牌放入 AI 副手"的最终证据，届时三条路径（拔刀 API、Mission Tick、AI 输入边界）全部否定，应停止该方向。

## 2026-08-30 NPC 双刀第三轮：合并两条线索——沿用其 AI 输入边界，但把主副手拆到不同输入帧（待用户实机验收）

- 本轮接手另一模型的两轮开发。**其最有价值的产出是找到了 `Agent.OnAIInputSet` 这个托管钩子**，此前本记录曾错误断言"原生 AI 的武器决策不经过任何托管入口"——该结论作废。取证：`agent.cs.txt:1626` 的 `internal void OnAIInputSet(ref EventControlFlag, ref MovementControlFlag, ref Vec2)` 会把**可变的**输入标志逐个交给 `AgentComponent`，配合 `Agent.SetHasOnAiInputSetCallback(true)` 启用；`Agent.EventControlFlag` 含 `Wield0..Wield3` 与 `Sheath0/Sheath1`。AI 的换武器是以**输入标志**表达的，不是 `Agent.UpdateWeapons`——所以此前只查后者才得出了错误结论。
- 其两轮实测结果（`19:31` 会话）：`NPC_DUAL_INITIAL_RANGED` 199 条（出生强制先拿弓，成功）、**`NPC_DUAL_INPUT_MELEE_PAIR` 596 条（钩子确实命中并改写了标志）**、`NPC_DUAL_INPUT_KEEP_PAIR` **0 条**（双刀始终没握住）。首轮该钩子为 0 条，第二轮才真正生效。
- **失败原因已定位**：其实现在**同一个输入帧内同时请求 `Wield0 | Wield2`**。这正好撞上本项目此前已两次测得的同一条规律——同帧内同时处理主副手，只有一只手会留下（原生 `WieldInitialWeapons` 同帧先副后主 → `offhand=None`；同样三次调用拆到不同帧 → `paired=True` 553/597）。两条线索合起来就是解法：**用它的钩子，按我们的分帧规律驱动。**
- **模型稳定性丢失的来源已定位并修复**：其第二轮把 `gwarcher` 槽位改成 Weapon0=副刀 / Weapon1=弓 / Weapon2=主刀 / Weapon3=箭。核对 `Equipment.GetInitialWeaponIndicesToEquip` 后确认**该改动毫无必要**：在原 ROT 布局（W0=副刀、W1=主刀、W2=弓、W3=箭）下配合 `RangedForMainHand`，扫描顺序为 W0(HeldInOffHand→副手) → W1(主手，flag2=false) → W2(`RangedForMainHand && !flag2` 成立 → 主手=弓)，同样得到"主手弓 + 副手刀"。而改后的布局让弓箭手变成"远程主手 + `HeldInOffHand` 刀"的组合，人物预览没有对应姿势映射——这是模型异常的直接来源。已恢复 ROT 标准布局，玩家与 NPC 重新共用同一套槽位。
- 本轮实现：
  - 保留其 `Agent.WieldInitialWeapons` 前置（`Any` → `RangedForMainHand`），但资格判定改为纯装备判定，不再硬编码 `gwarcher`。
  - 保留 `GwpDualBladeNpcBehavior` + `GwpDualBladeNpcInputComponent`，但把 `OnAIInputSet` 改为**三帧状态机**：第 1 帧只请求 `Wield0`（副手）并清除竞争性 `Sheath0/1`；第 2 帧只请求 `Wield1`（主手）；第 3 帧让原生自行落定。双刀已在手时清掉重复的近战重选与收刀标志，防止原生每个决策周期重拔主手时把副手带走。
  - 弓箭相关标志（`Wield2`/`Wield3`）全程原样放行，AI 何时射击、何时切近战完全由原生决定。
  - `TryGetCombatPair` 由双布局收敛回单一 ROT 布局（W0 副手 / W1 主手），伤害类型与击倒判定对玩家和 NPC 一致；地面拾取仍严格保持玩家专属（沿用其判断，避免扩散到所有 AI）。
  - 每 agent 最多 4 条输入日志，避免刷屏。
- 未新增任何动作集、武器数据或预览路径改动。Harmony 目标：`Mission.SpawnAgent`、`Agent.WieldInitialWeapons`、两个 `MissionCombatMechanicsHelper`、`SpawnedItemEntity.OnUseStopped`、`CraftingTemplate.All`、自定义战斗角色列表；无 `CharacterCode`/`CharacterTableau`/`MissionWeapon`/`AgentVisuals` 命中。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；离线预检 `PATCH_OK=38; PATCH_FAIL=0`，类型数 412；XSLT 复验仍为 102 个 action_set、`as_human_female_warrior` 保持原版 298 条；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 均为 797184 字节、SHA-256 `A5E97FFCEDC2979B8A48640ACE0BAF91E893BBC0F7B616F7D6B23B262908CE9A`。
- 资产完整性：另一模型的两轮工作保存在 stash `54aa30d`（`failed-npc-dual-input-2026-08-30`），本方此前的分帧候选保存在 reflog（至 `3693545`），检查点标签 `checkpoint/player-only-dual-blade` (`003fea5`) 完好，均可恢复。
- 第三轮补完（本次）：
  - 组件挂载点由 `OnAgentCreated` 改为 `OnAgentBuild`。取证 `mission.txt:4359`：原生在 `BuildAgent(agent, agentBuildData)` **之后**才广播 `OnAgentBuild`，此时装备已就位，资格判定读到的 `SpawnEquipment`/`Equipment` 才是完整的；并加了重复挂载保护。
  - **修掉一个会让 AI 卡在近战的缺陷**：原实现在双刀已在手时无条件清除 `Sheath0/Sheath1`。但 AI 切回弓箭往往先发单独的收刀标志，被清掉后弓箭手将再也无法转回远程。现改为**只有在同一帧还伴随近战 `Wield` 请求时才清除收刀标志**（那才是会带走副手的重复重选）；单独的收刀一律放行。
  - 分帧序列增加落地校验：第 2 帧先确认副手确实已进手才请求主手；未进手则重发副手请求，最多 6 次后放弃并进入 60 帧冷却，避免把"拒绝"变成每帧循环。
  - 新增恢复入口：即使 AI 没有发出近战请求，只要处于"主手为主刀、副手为空"的状态也会启动序列，用于覆盖原生周期性重选清空副手的情况。
- 需求逐条对照（均为数据/既有实现，本轮只做核对）：左右手动作 = `as_human_warrior` 内 84 条 gwd 动作，全体人类 agent 共用；左手伤害 = 副手件 `Swing Cut 4.1` / `Thrust Pierce 3.2`，伤害类型与 bone-20 碰撞判定已改用与角色无关的 `TryGetCombatPair`，NPC 与玩家一致；格挡 = `WoodenParry`（与 ROT 逐字相同）；**挡不住弓箭 = 副手 WeaponFlags 只有 `MeleeWeapon`，无 `CanBlockRanged`，故 `IsShield` 为假**，无需再加 `MissileHitCallback` 还原补丁；击倒 = `GwpAgentApplyDamageModel` 同样走 `TryGetCombatPair`。地面拾取仍严格保持玩家专属。
- 最终验证：Release 重建 0 errors、44 条既有 nullable warnings；离线预检 `PATCH_OK=38; PATCH_FAIL=0`，类型数 412；XSLT 复验 102 个 action_set、重复 id 0、`as_human_female_warrior` 保持原版 298 条；30 个 XML/XSLT/mbproj 解析失败 0；仓库 `_Module` 与 live 36 个可部署文件差异 0；客户端/编辑器 DLL 均为 797696 字节、SHA-256 `E298CB961E3D70B1EC5707AF5D22100353359FFA213BE003D4041086A4813374`（README 同步后的最终增量构建）；Bannerlord 进程数 0。
- 判读：看 `NPC_DUAL_INPUT_OFFHAND` → `NPC_DUAL_INPUT_MAINHAND` → `NPC_DUAL_INPUT_KEEP_PAIR` 是否按序出现。出现 `KEEP_PAIR` 即表示双刀已稳定握住且原生重选被成功抑制；若出现 `NPC_DUAL_INPUT_GAVE_UP`，说明经由输入边界的副手请求同样被拒，届时该结论适用于全部三条已知路径（拔刀 API、Mission Tick、AI 输入边界）。

## 2026-08-30 NPC 双刀重做第二轮：改用原生 AI 输入边界与远程初始偏好（待用户实机验收）

- 按用户要求废弃上一代 Mission Tick/分帧补刀候选：当前仓库已回退到 `003fea5`，上一代完整改动保存在 `failed-npc-dual-input-2026-08-30`。本轮不创建新的 Git 检查点，等待实机确认。
- 采用更接近原版/玩家路径的底层入口：Harmony 只对 `Agent.WieldInitialWeapons` 的 `gwarcher` AI 将 `InitialWeaponEquipPreference.Any` 改为原生 `RangedForMainHand`；不改全局 Agent、武器数据或动作集，不在 Mission Tick 中调用拔刀 API。
- `gwarcher` 装备槽改为 Weapon0=`gwdualbladeoffhand`、Weapon1=`noble_long_bow`、Weapon2=`gwdualblademainhand`、Weapon3=`piercing_arrows`。这样原版 `Equipment.GetInitialWeaponIndicesToEquip` 会优先选 Weapon1 弓，Weapon0 仍保留为真实副手剑，Weapon2 作为近战主手剑。
- 新增 `GwpDualBladeNpcBehavior`/`GwpDualBladeNpcInputComponent`，只挂在真实战场中的 `gwarcher` AI。`OnAIInputSet` 放行 Wield1/Wield3 的弓箭请求；检测到近战 Wield0/Wield2 时，在同一原生输入帧请求 Weapon0+Weapon2 并清除竞争性的 Sheath0/Sheath1；双刀已在手时清掉重复近战重选，避免副手被原生二次选择清空。
- 玩家双刀布局 Weapon0+Weapon1 保持不变。伤害类型/击倒判定新增 NPC 的 Weapon0+Weapon2 槽位识别，地面拾取仍严格保持玩家专属；bone-20 碰撞例外继续使用既有 ROT 对等实现。
- Release 构建成功：0 个编译错误、44 条既有可空性警告。客户端 DLL 798208 字节，SHA-256 `30A7CF44918147E2DE1A9D2DBFC0D5CB205915638B3F9D918AAD0596D5A59C95`；仓库 `_Module` 与 live `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden` 的 36 个可部署源文件哈希差异 0，README 已在构建后单独同步并复核。
- 取证依据：当前 Bannerlord 客户端反编译的 `Equipment.GetInitialWeaponIndicesToEquip` 明确按 ExtraWeaponSlot→Weapon0→Weapon1→Weapon2→Weapon3 扫描，并以 `RangedForMainHand` 保留首个远程主手；`Agent.WieldInitialWeapons` 先请求 off-hand 再请求 main-hand；`Agent.OnAIInputSet` 将可变 `EventControlFlag` 传给 AgentComponent。临时反编译目录已从仓库删除。
- 实机验收重点：出生是否只拿弓箭；停止射击后是否一次性出现左右双刀；左手模型/攻击是否持续；再次切弓和再次切回近战是否无闪烁、无单独收回副手。若失败，保留日志后回退到 `003fea5`，不在本候选上继续堆叠补丁。

## 2026-08-30 NPC 双刀重做首轮实测：初始配对时序错误，输入拦截未命中（已回退，待重做）

- 用户实机复测上一代候选：弓箭手出生时短暂拿出双刀，随后把两把刀逐把收回并正常切到长弓/箭；停止射击回到近战时双刀短暂出现，但左手剑又立即被收回。弓与箭本身的远程切换正常。
- 对应 `GreyWarden-DualBlade-Trace.log` 的 `18:58` 会话：共记录约 199 个 `gwarcher` AI 的 `NPC_DUAL_PAIR_START` / `NPC_DUAL_PAIR_RESULT`。几乎所有配对开始都记录为 `main=Weapon1; offhand=None`，时间集中在出生后的初始装备阶段；配对结果绝大多数为 `paired=True`，少数为 `false`。
- 该会话没有任何 `NPC_DUAL_INPUT_FILTER` 记录。因此不能证明左手剑被收回是上一代正在过滤的 `Wield0/Wield1/Sheath0` 输入组合；实际清除可能发生在原生远程选择路径、另一组输入标志，或回调未覆盖的执行边界。
- 已确认的错误归因：`Agent.WieldInitialWeapons()` 使用默认 `InitialWeaponEquipPreference.Any`，不会因为兵种 `default_group="Ranged"` 就强制先拿弓；当 Weapon0/1 放双刀、Weapon2/3 放弓箭时，原生初始选择先给出 `Weapon1` 主手剑。上一代候选把这个“主手剑已出现、副手为空”的出生状态误判为近战入口，主动开始配对，正好造成“出生双刀→逐把收回→弓箭”。
- 上一代候选已保存为 Git stash `failed-npc-dual-input-2026-08-30`，并已回退到稳定检查点 `003fea5`（标签 `checkpoint/player-only-dual-blade`）。本轮不继续在错误状态机上修补；下一轮从该点重新探索原生 AI 的初始装备与切换边界。

## 2026-08-30 用户确认稳定基线与弓箭手遗留问题

- 用户实机确认：人物预览恢复正常；双刀与灰袍单手剑外观一致；自定义战斗灰袍武将能正常拔出双刀且双刀动作正常；双刀与普通士兵交战正常，没有此前的防御报错/卡死。
- 用户同时确认唯一遗留问题：灰袍弓箭手切入近战时只拔出右手刀，副手刀没有拔出。该问题与已确认稳定的预览、模型和接战路径分开处理。
- 上述稳定功能已建立本地 Git 检查点 `eda353f`（`checkpoint: stable dual-blade models and combat`）。下一轮只允许围绕弓箭手副手拔刀同步做增量实验；若需要回滚，恢复到该提交即可。

## 2026-08-30 模组级 A/B 定案：移除士兵双刀的全部开发，回到 ROT 式"仅玩家双持"

- 用户执行模组级隔离并给出结论：**关闭 GreyWarden 后一切恢复正常**，因此两个问题都由本模组的双刀开发引起，与 1.5.2 beta、NavalDLC/War Sails 无关。用户同时指明稳定回退点是"只有玩家能用双刀（ROT 已实现的形态）"，并要求不再让其做诊断性实验，直接移除士兵双刀的全部开发后重做。
- 与 ROT 逐项对照后确认本模组相对 ROT 的三处结构性偏差，全部集中在为"让士兵用双刀"而新增的东西上：
  1. ROT 的 `action_sets.xslt` **只**注入 `as_human_warrior` 一个模板（218 条动作），从不创建额外 action_set；本模组除注入外还**额外生成两个完整 action_set**（`as_human_gwp_dual` 4784 条 / `as_human_female_gwp_dual` 382 条），纯粹为了给 AI 调 `SetActionSet`。
  2. ROT 从不碰 `as_human_female_warrior`；该集带 `base_set="as_human_warrior"`，本来就继承注入结果。本模组却在这个派生集里**重复定义同样 84 条动作类型**，这是 ROT 从未产生过的状态。
  3. ROT 的双持只作用于玩家：`dual_blades` 仅出现在 `items.xml`，`ROT-Troops.xml`/`ROT_lords.xml`/`ROT_heroes.xml`/装备表 0 命中；战斗侧只有 `IsCollisionBoneDifferentThanWeaponAttachBonePatch`（bone 20）一个补丁，`DualWieldingPatches` 只做库存界面提示。
- 本轮移除的内容（全部属于士兵/AI 双刀开发）：
  - 代码：`GwpDualBladeActionSetPatch`（`Mission.SpawnAgent` 后置强制套用自定义动作集）、`GwpDualBladeWieldSync`（Attach/Synchronize 副手同步）、`GwpCharacterTableauTracePatch`、`GwpCharacterCodeTracePatch`、`GwpCharacterCodeEquipmentTracePatch`（三者都是追这个 bug 时加在预览链上的诊断）、`AuditLoadedObjects`/`AuditActionSets` 全套审计、`HasCompleteAiLoadout`/`HasCompletePlayerLoadout`。地面拾取不再强制套动作集与双手拔刀，只保留固定槽位路由。
  - 资格：`IsEligibleDualBladeUser` 收敛为 `agent?.Character != null && !agent.IsAIControlled`，**AI 永远不合格**，因此伤害类型、击倒、地面拾取三条共用该判断的路径同时只对玩家生效。
  - 动作资源：`action_sets.xslt` 由 362 行减到 96 行，只保留对 `as_human_warrior` 的注入，删除两个额外 action_set 与整个 `as_human_female_warrior` 模板 —— 与 ROT 形状完全一致。
  - 兵种数据：`gwarcher` 按 v1.4-r9 (`dcdc042`) 的定义还原为 `default_group="Ranged"` + `noble_long_bow` / `piercing_arrows` ×2 / `gwonehandedsword`，不再携带双刀。全模组只剩 `gwp_custom_commander` 带双刀，而追踪已证实自定义战斗中该角色就是玩家本人（agent 400，`isAi=False; isPlayer=True`）。
- 保留（ROT 对等的玩家双持）：两把刀的物品与锻造数据、`item_usage_sets`/`movement_sets`/`full_movement_sets`/`item_holsters`、`GwpDualWieldCollisionPatch`（bone 20，与 ROT 同款）、`GwpDualWieldDamageTypePatch`（副手挥砍伤害类型，仅玩家）、`GwpDualBladeCraftingTemplateVisibilityPatch`（双刀模板不进锻造目录）、自定义战斗灰袍武将列表插入（含 NavalDLC 反射目标）。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings。离线预检 `PATCH_OK=37; PATCH_FAIL=0`（补丁类由 41 降到 37），程序集类型数 413→407，`GwpDualBladeActionSetPatch`/`GwpDualBladeWieldSync`/三个预览诊断类型均已不存在。全仓库与 `_Module` 已无 `as_human_gwp_dual`、`as_gwp_dual_warrior`、`HasCompleteAiLoadout`、`TryApplyActionSet` 的任何引用。
- 用游戏同款 `XslCompiledTransform` 对 live 的 `action_sets.xslt` + Native `action_sets.xml` 实跑：**总 action_set 数 102，与原版完全相同（不再多出任何集）**，重复 id 0；`as_human_warrior` 4700→4784（注入 84 条，与 ROT 注入 218 条同一形式），`as_human_female_warrior` 保持原版 298 条、gwd 动作 0 条，由 `base_set` 继承。
- 部署：30 个 XML/XSLT/mbproj 解析失败 0；仓库 `_Module` 与 live 36 个可部署文件缺失 0、差异 0。客户端与编辑器 DLL 均为 794112 字节、SHA-256 `0AB0B1CCFFA4883C2E7F10CCCAE5342EA9C3DF50C00E9208606FB605E1150C04`（README 同步后的最终增量构建）；`action_sets.xslt` SHA-256 `99B2A23FDA62D59708E8B387DC932C4219BE271BEABE21DE6723FDB9A7AFCC8F`。Bannerlord 进程数 0，未代表用户启动游戏。
- 双语 README 已同步：移除全部与士兵双刀相关的条目和"已知问题"行，改为说明双刀回到仅玩家可用、弓箭手恢复远程兵种。
- 后续重做 AI 双持时的硬性前提（写死在此，避免重复本轮循环）：ROT 没有 AI 双持参考实现；已实测证明单槽位 `TryToWieldWeaponInSlot` 与整套 `WieldInitialWeapons` 对 AI Agent 均被原生拒绝，而同样两件物品在玩家 Agent 上成功，差异在 `IsAIControlled` 本身；任何新方案都不得再向 `as_human_warrior` 之外增建全局 action_set，也不得在派生集里重复定义同名动作类型。

## 2026-08-30 16:10 会话：共用动作集假设被 A/B 证伪并回滚；预览需要模组级隔离

- 用户复测结论：百科士兵预览没有恢复；在存档里移动时游戏直接崩溃退出；重启后复检，其余问题也全部依旧。
- **共用动作集假设被本轮自己的监控证伪。** 新增的 `ACTION_SET_AUDIT` 显示四个动作集 `valid=True`，且：
  - `act_inventory_idle_start`（预览 idle）在四个集里都是 `index:4009, clip:True` —— 预览用的 idle 动画本来就完好。
  - `act_gwd_ready_thrust_1h` / `act_walk_idle_1h_with_gwd_shld` 在 `as_human_warrior`、`as_human_female_warrior` 中 `clip:False`（本轮按预期移除），在 `as_human_gwp_dual`、`as_human_female_gwp_dual` 中 `clip:True`。
  即：双刀动画片段确实存在于 `gwp_dual_wield_animations.tpac`，XSLT 改动也确实生效，但预览依旧损坏。这构成一次完整 A/B：**注入共用动作集与不注入，预览都坏，因此"共用动作集污染"不是预览根因。**
- **`CHARACTER_TABLEAU_REFRESH_FAILED` 计数为 0。** 预览链不抛异常，属于"构建成功但姿势/挂点错误"，不是构建失败。上一轮设计的二分到此有了答案。
- **新增回归：本轮候选疑似引入战役地图崩溃。** 15:40 会话（改动前）无崩溃；16:08 会话读档进入 `NavalDLC.View.Map.NavalMapScreen::HandleActivate` 后原生崩溃，WER 记录 `APPCRASH / c0000005 / 模块 unknown`，转储为 CrashDumps 目录下的 `TaleWorlds.MountAndBlade.Launcher.exe.22272.dmp`（100124045 字节，16:08），托管错误日志为空。合理机制：移除后 `act_*_gwd_shld` 在全局动作类型表里仍能解析出索引（如 5244）却在共用集里 `clip:False`，这种"有类型无片段"的中间状态比原状态更危险。
- 据此**已把 `action_sets.xslt` 回滚到 `784cc3b` 的版本**（共用集重新带 84 条动作，`as_human_warrior` 4784 / `as_human_female_warrior` 382，两个专用集不变），恢复到 15:40 那个不崩溃的动作集状态。用游戏同款 `XslCompiledTransform` 对 live 文件复验：104 个 action_set、重复 id 0。
- **弓箭手副手：`WieldInitialWeapons()` 候选同样失败。** `ARCHER_OFFHAND_PAIR_RESULT` 全部为 `main=Weapon1; offhand=None`，即调用原生自身的配对 routine 之后副手仍为空。结合上一轮结论，**"换一种拔刀调用形式"这条路已经走到头**：单槽位 `TryToWieldWeaponInSlot` 与整套 `WieldInitialWeapons` 都被拒绝，而同样两件物品在玩家 Agent 上成功。差异不在调用方式，而在 Agent 本身（`IsAIControlled`）。诊断保留，本轮未再改动。
- 用户补充的关键信息：**玩家双持是移植 ROT 的实现，本来就没问题**。与 ROT 反编译取证一致（`dual_blades` 只出现在 `items.xml`，兵种/领主/英雄/装备表 0 命中，ROT 从未做过 AI 双持）。
- 预览方向目前已被实测排除的根因：双刀物品数据（usage/flags 已审计正确）、装备码、`CharacterCode` 生成、holster 定义、item usage set、共用动作集注入、预览链抛异常；且故障波及原版兵种（古拉姆骑兵）。**剩余唯一未做的关键切分是模组级隔离**：在启动器中只关闭 GreyWarden，查看同一个百科兵种页，以判定该预览损坏是否由本模组引起。已就此向用户提出该 A/B，在拿到结果前不再改动预览相关代码。
- 两个 README 已按实际状态回退不实条目，并新增"已知问题"行（弓箭手仍单刀；预览仍异常且波及原版兵种）。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；仓库 `_Module` 与 live 36 个可部署文件差异 0。回滚点仍为 `eda353f`。

## 2026-08-30 用户截图定性：预览损坏波及原版兵种（共用动作集假设已被 A/B 证伪并回滚）

- 用户提供四张截图，彻底改变了"人物显示异常"的定性：
  1. 海上自定义战斗选人界面：灰袍武将只剩两把刀悬空，**人物身体完全不显示**。
  2. 战场：灰袍士兵确实只有单刀在手，副刀挂在背后（`gwp_dual_back` holster，`show_holster_when_drawn="true"`）。
  3. 读档界面（旧 v1.4.8 会话）：玩家人物**横躺悬空**，坐骑正常。
  4. 百科 → 士兵 → **古拉姆骑兵**：人物瘫成一堆，武器竖立悬空，**坐骑渲染完全正常**。
- **第 4 张是决定性证据**：古拉姆骑兵是原版阿塞莱兵种，身上没有任何 GreyWarden 物品，却同样损坏。因此预览问题**根本不是双刀特有**，而是全局波及所有人物 tableau。此前所有"双刀物品/装备码/材质"方向的排查都对错了目标。
- 四张图的共同特征：**武器和坐骑渲染正常，唯独人类身体缺失或塌陷**，即人类骨架拿不到有效姿势。
- 结合已确认的 `CharacterTableau` 实现（`_characterActionSet = MBGlobals.GetActionSetWithSuffix(MonsterData, _isFemale, "_warrior")`，idle 为 `act_inventory_idle_start`），根因锁定：本模组的 `action_sets.xslt` 一直在把 84 条自定义双刀动作**注入全游戏人物共用的 `as_human_warrior` 与 `as_human_female_warrior`** —— 而这两个正是每个人物 tableau 解析的动作集。原版兵种同样解析它们，所以一起损坏。
- 这也是本模组对"所有人类角色"唯一的全局改动。核对确认其余数据层没有全局副作用：`combat_parameters.xml` 只用 `gwp_*` 前缀 id；`movement_sets.xml`/`full_movement_sets.xml`/`item_usage_sets.xml` 只新增 `dual_*`/`1h_with_dual_shield` 等新 id，与原版无冲突（原版确有 `hand_shield` 根集，`dual_shield` 的 `base_set` 可解析）；`gwp_dual_back` 与 ROT `dual_back` 逐字段相同。
- 本轮修改：`action_sets.xslt` 不再向两个共用集追加任何动作，只保留派生出的专用集。用游戏同款 `XslCompiledTransform` 对 live 文件实测确认：`as_human_warrior` 动作数 **4700**、`as_human_female_warrior` **298**，与原版逐数一致且 gwd 动作数为 **0**；`as_human_gwp_dual` 4784 条、`as_human_female_gwp_dual` 382 条，各含 84 条 gwd 动作；全局 104 个 action_set、重复 id 0；含 gwd 动作的集合**只有那两个专用集**。
- 新增 `ACTION_SET_AUDIT` 监控：在首个双刀出生时记录四个动作集的 `IsValid`，并对 `act_inventory_idle_start`、`act_gwd_ready_thrust_1h`、`act_walk_idle_1h_with_gwd_shld` 分别记录 `ActionIndexCache` 索引与 `MBActionSet.CheckActionAnimationClipExists` 结果。若专用集里的双刀动作 clip 不存在（`gwp_dual_wield_animations.tpac` 缺片段），下一轮可直接看到，而且损坏范围已被限制在灰袍双刀单位内，不再波及原版人物。
- 弓箭手副手仍按上一条记录使用原生 `WieldInitialWeapons()`，本轮未改动；`ARCHER_OFFHAND_PAIR_RESULT` 与 `CHARACTER_TABLEAU_REFRESH_FAILED` 两条监控同时保留。
- 用户同时说明：**玩家双持是移植 ROT 的实现，本来就没有问题**，与 ROT 只做玩家双持的取证结论一致；AI 双持仍是本项目独有、无参考实现的部分。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；离线预检 `PATCH_OK=41; PATCH_FAIL=0`；仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 802304 字节、SHA-256 `CA28ADD83CC07BCF444B338E9CFF37C921BE3F622E9D6FA85A372D24BB22E241`（README 同步后的最终增量构建）。未代表用户启动游戏。回滚点仍为 `eda353f`。

## 2026-08-30 15:40 会话：物品数据彻底证清白，副手拒绝点锁定在单槽位拔刀调用

- 用户复测 `15:40:16`–`15:42`，`rgl_log_errors_31704.txt` 无托管异常、CrashDumps 目录为空、watchdog 记录为正常 `EXIT_PROCESS_DEBUG_EVENT`。用户反馈两项均未解决：士兵依旧单刀，人物显示依旧有问题。
- **上一轮的 `Agent.UpdateWeapons` 前置被实测证明完全无效**：`AI_WEAPON_SELECTION_SKIPPED` 计数为 `0`，即托管 `Agent.UpdateWeapons()` 在该场景下根本没有被调用过。由此也修正了上一轮的一个错误归因：12:10 会话里的 199 条配对请求并非来自 `UpdateWeapons` 后置，而是一直来自 `OnAgentWieldedItemChange` 事件。该补丁已从代码中完整删除，不留死代码（`PATCH_OK` 由 41 降回 40，类型数 413→412）。
- **一次性审计首次拿到关键数据**（`phase=FirstDualBladeSpawn`，此前 8 个会话 18 条全是 `missing=true`）：
  - `gwdualbladeoffhand`：`type=OneHandedWeapon; body=bo_sword_one_handed; collision=; usage=dual_shield; flags=ForceAttachOffHandPrimaryItemBone, WoodenParry, HeldInOffHand`
  - `gwdualblademainhand`：`type=OneHandedWeapon; body=bo_sword_one_handed; collision=; usage=dual_shield_thrust; flags=0`
  - 两个角色的装备码完整，`CHARACTER_CODE_CREATE` 7 次全部 `OK`/`exception=none`。
  这与 ROT 的 `dual_blades`/`dual_blades2` 在 usage 与副手标志上逐项一致。**至此"锻造物丢失副手标志"、"item usage 合成错误"、"装备码缺失"、"对象未注册"四条假设全部由实测排除，物品数据层不需要再改，双刀外观不必让步。**
- **拒绝点已精确定位到调用形式**：新增的 `SPAWN_AGENT_POSTFIX` 字段显示所有 Agent（含玩家）在 `SpawnAgent` 后置时都是 `main=None; offhand=None`，符合 `SpawnTroop` 在 `SpawnAgent` 返回后才调用 `WieldInitialWeapons` 的顺序。AI 弓箭手在 `15:41:45.6` 生成、`15:41:49.1` 才拔刀（+3.5 秒），拔出的只有主手 `Weapon1`。`rgl_log` 的 `archer_melee_pair` 是在 `TryToWieldWeaponInSlot` **返回之后**写的，199 条全部是 `main=Weapon1; offhand=None` —— 即原生**直接拒绝**了 `TryToWieldWeaponInSlot(Weapon0, InstantAfterPickUp, isWieldedOnSpawn: false)`，不是拔出后又被清空。
- **玩家路径的对照是本轮最强证据**：agent `400`（`gwp_custom_commander`，`isAi=False; isPlayer=True`）全程没有发出任何配对请求，说明它双刀在手。它与弓箭手用的是**完全相同的两件物品和相同的固定槽位**，唯一差别是它经由原生 `WieldInitialWeapons()` 拿到配对 —— 该routine 先拔副手、后拔主手，且两次都传 `isWieldedOnSpawn: true`。
- 本轮据此把 `Synchronize` 里那次被拒绝的单槽位请求，替换为直接调用原生 `agent.WieldInitialWeapons(WeaponWieldActionType.InstantAfterPickUp)`。这些角色身上只有两把刀，所谓"初始武器"就是这一对，因此重跑该 routine 不会改变武器组合。新增 `ARCHER_OFFHAND_PAIR_RESULT` 记录调用**之后**的主副手槽位，下一轮可直接判定原生是否接受。
- 女性动作集修复保留并已验证生效：`gwarcher` 与 `gwp_custom_commander` 均为 `female=True`，`actionSet=True` 200 次、`ACTION_SET_MISSING` 0 次，说明 `as_human_female_gwp_dual` 正常解析。
- 为下一轮定位"人物显示"问题，审计新增对照项 `gwonehandedsword`（双刀本应与之同型、且由同样四个锻造部件构成）。注意当前双刀的 `collision=` 为空而 ROT 显式使用 `body_name="bo_mace_a" recalculate_body="false"`；在拿到对照数据前不改动该字段，避免重蹈 2026-08-30 早期"运行时改写碰撞体导致预览中断"的覆辙。
- 本轮日志中没有任何网格/资源缺失、动作集断言或托管异常，因此“人物显示异常”无法仅从监控判定具体形态。已向用户定位现象，用户答复：**出现在自定义战斗选人/预览界面与百科页面**（战场模型不在其列），表现为**模型完全不显示（空白）**与**模型显示但姿势异常**两种。
- 据此确认故障面是 `CharacterTableau` 这条预览链，而不是战场 Agent 链。反编译 `TaleWorlds.MountAndBlade.View.Tableaus.CharacterTableau` 确认：预览用的动作集是 `MBGlobals.GetActionSetWithSuffix(MonsterData, _isFemale, "_warrior")`，即 `as_human_warrior` / `as_human_female_warrior`（两者都已由本模组 XSLT 追加 84 条 gwd 动作），idle 为 `act_inventory_idle_start`。
- 已排除的预览侧数据项：`gwp_dual_back` holster 与 ROT 的 `dual_back` 除 id 外逐字段完全相同；`item_usage_sets.xml` 的四个 dual 集与 ROT 逐节点一致；两把刀的 usage/flags 已由本轮审计证实正确。另外核实全部 104 个 action_set 中，自定义 `act_*_gwd_shld` 动作只在 `as_human_warrior`、`as_human_female_warrior` 及两个 `*_gwp_dual` 中有映射——而预览恰好用前两者，因此“预览动作集缺少自定义动作映射”这一条也不成立。
- 新增只读诊断 `GwpCharacterTableauTracePatch`：对 `CharacterTableau.RefreshCharacterTableau` 挂 Finalizer，原样返回 `__exception`，因此不改变任何行为，只在预览构建抛异常时写 `CHARACTER_TABLEAU_REFRESH_FAILED`。目标按名称反射解析，类型改名时 `Prepare()` 直接跳过而不影响 `PatchAll`。下一轮据此可一刀切分：“预览构建抛异常”与“构建成功但姿势/挂点错误”两类，避免继续猜测式改动。
- 已请用户提供预览界面截图，以进一步区分空白与姿势异常分别出现在哪些角色上。
- 验证：Release 重建 0 errors、44 条既有 nullable warnings；离线预检 `PATCH_OK=40; PATCH_FAIL=0`，`GwpDualBladeAiWeaponSelectionPatch` 与 `GwpDualBladeUpdateWeaponsPatch` 均已不存在。仓库 `_Module` 与 live 36 个可部署文件差异 0。客户端/编辑器 DLL 801280 字节、SHA-256 `322577EFED685E5D3EE2881C011121EFD6EB7B9391D72D46B51288A65DCD4ED7`（含预览诊断，离线预检 `PATCH_OK=41; PATCH_FAIL=0`）。未代表用户启动游戏。回滚点仍为 `eda353f`。

## 2026-08-30 弓箭手副手：改为阻止原生 AI 换武器判断，并补齐女性双刀动作集（阻断部分已实测无效，已删除；女性动作集保留）

- 用户复测已在 `12:06`–`12:10` 产生新会话，上一条候选的"待验收"状态由本轮追踪直接结案：`SUBMODULE_PATCH_OK` 1 次、`CHARACTER_CODE_CREATE_OK` 7 次、`CUSTOM_BATTLE_COMMANDER_INSERT` 8 次、CrashDumps 目录为空，因此预览崩溃、模型消失与接战卡死三项确实已经稳定，不再复现。
- 同一会话记录 `SPAWN_AGENT_POSTFIX` 200 次、`ARCHER_OFFHAND_PAIR_REQUEST` 199 次，全部为 `main=Weapon1; offhand=None`，与 2026-08-29 的 199 次 `archer_melee_pair` 逐字相同。`Agent.UpdateWeapons` 后置候选因此判定失败：它既没有改变结果，也没有产出新信息，已从代码中完整删除。
- 关键新证据一（agent 编号）：agent `201`–`399` 是 `isAi=True` 的 `gwarcher`，agent `400` 是 `isAi=False; isPlayer=True` 的 `gwp_custom_commander`。**只有 400 号没有发出配对请求**，即玩家控制的武将双刀始终在手，而 199 个 AI 弓箭手在出生后约 1.9 秒（`12:10:00.6` 生成完毕 → `12:10:02.5` 请求）丢掉 `Weapon0`。这把故障从"物品数据/装备码"彻底移到"原生 AI 换武器选择"。
- 关键新证据二（物品标志已可反推）：对照 1.5.2 的 `Equipment.GetInitialWeaponIndicesToEquip`，主手是按数组顺序取第一个**不带** `HeldInOffHand` 的槽位。日志中主手稳定解析为 `Weapon1` 而不是 `Weapon0`，只有在 `gwdualbladeoffhand` 确实带 `HeldInOffHand` 时才可能出现。因此"锻造物 `ItemFlags` 丢失副手标志"这一条已由实测排除，不需要再把双刀改回普通 `<Item>`，外观也不必让步。
- 关键新证据三（ROT 参考的边界）：全量检索本机 `rot_decompile` 后确认，ROT 的双持战斗实现只有 `IsCollisionBoneDifferentThanWeaponAttachBonePatch`（bone 20）一个补丁，`DualWieldingPatches` 只做库存界面提示；并且 `dual_blades` 仅出现在 ROT 的 `items.xml`，`ROT-Troops.xml`、`ROT_lords.xml`、`ROT_heroes.xml`、装备表全部为 0 命中。**ROT 从未让 AI 兵种双持**，所以此前反复用 ROT 当作 AI 分支的参照本身不成立，这解释了 B 类候选连续五次失败。
- 本轮改为阻断而非补救：新增 `GwpDualBladeAiWeaponSelectionPatch`，在 `Agent.UpdateWeapons` 前置对**符合 `HasCompleteAiLoadout` 的灰袍双刀 AI** 返回 `false`，跳过原生武器重选；这些角色身上只有两把刀，原生本来也无从可选。前置内仍调用一次现有 `Synchronize` 作兜底。该补丁不写任何 `WeaponData`/`WeaponStatsData`、不伪装盾牌、不获取或传入原生句柄，因此不触碰 2026-08-29 已确认会污染 tableau 的那类全局入口；其他所有 Agent（含同场普通士兵）保持原生选择。
- 同时修复一个独立的真实缺陷：`gwarcher` 与 `gwp_custom_commander` 均为 `is_female="true"`，而旧的 `as_gwp_dual_warrior` 是从 `as_human_warrior` 复制出来的**男性根动作集**（带 `skeleton="human_skeleton"`、4784 条动作），且没有任何女性变体。按 1.5.2 原生 `MBGlobals.GetActionSetWithSuffix` → `ActionSetCode.GenerateActionSetNameWithSuffix` 的约定改为后缀式命名：`action_sets.xslt` 现在从 `as_human_warrior` 生成 `as_human_gwp_dual`，从 `as_human_female_warrior` 生成 `as_human_female_gwp_dual`；代码改用 `ActionSetCode.GenerateActionSetNameWithSuffix(agent.Monster, agent.IsFemale, "_gwp_dual")`，取不到时写 `ACTION_SET_MISSING` 而不抛异常。
- 监控修正：`AuditLoadedObjects` 此前只在 `OnGameStart` 调用，8 个会话共 18 条 `OBJECT_AUDIT_ITEM` 全是 `missing=true`，从未捕获过 `usage=` 与 `flags=`。新增一次性的 `AuditLoadedObjectsOnce`，由首个双刀 `SpawnAgent` 后置触发（此时对象必然已加载）；`SPAWN_AGENT_POSTFIX` 也补记 `female=`、`main=`、`offhand=`，新增 `AI_WEAPON_SELECTION_SKIPPED` 记录跳过前的主副手状态。
- 验证：`dotnet build ... --configuration Release --no-restore -t:Rebuild -p:DeployToLiveModule=true` 成功，0 errors、44 条既有 nullable warnings。Windows PowerShell 5.1 离线预检逐类 `CreateClassProcessor().Patch()` 全部通过：`PATCH_OK=41; PATCH_FAIL=0`，程序集 413 个类型；`GwpDualBladeUpdateWeaponsPatch` 已不存在、`GwpDualBladeAiWeaponSelectionPatch` 存在；`GenerateActionSetNameWithSuffix` 实测返回 `as_human_gwp_dual` / `as_human_female_gwp_dual`，与 XSLT 产出的 id 精确一致。
- XSLT 用游戏同款 `System.Xml.Xsl.XslCompiledTransform` 对 live 模组的 `action_sets.xslt` + Native `action_sets.xml` 实跑：共 104 个 action_set、重复 id 0；`as_human_gwp_dual` = 4784 条动作 / `skeleton=human_skeleton` / 无 base_set（对齐 `as_human_warrior`），`as_human_female_gwp_dual` = 382 条动作 / `base_set=as_human_warrior`（对齐 `as_human_female_warrior`），两者各含 84 条 gwd 动作。
- 部署：仓库 `_Module` 与 live 模组 36 个可部署文件缺失 0、哈希差异 0；30 个 XML/XSLT/mbproj 解析失败 0。客户端与编辑器 diagnostics-enabled DLL 均为 801280 字节、SHA-256 `28F473F47FF0D6920C903564526A886B6435FE366118417C87F792E6F83DBBEB`（该哈希取自加入本地化字符串后的最终增量构建，并已用同一份 live DLL 重跑离线预检：`PATCH_OK=41; PATCH_FAIL=0`）；`action_sets.xslt` SHA-256 `80D652A4DBF28A05475F2528FDA5705C088C15D2B1F8505F090449F693503D47`，中文 README SHA-256 `214DE41964270E426171130F8C4DB6643538201EF4B4978A57279D976BB3D817`、英文 README SHA-256 `70F86072E3D8DB9EAF5C7D8BE5AB7DC984499B1CFE40A8C804664F7F64CECF52`，仓库与 live 全部一致。Bannerlord 相关进程数 0，未代表用户启动游戏。
- 回滚点：稳定基线仍是 `eda353f`。本轮的仓库卫生提交为 `2210f01`（把 15 个无关治安/声望改动单独固化，使双刀实验可以整体丢弃而不损失其他工作）。
- 待用户实机验收：进入自定义战斗，确认（1）人物预览与模型正常；（2）灰袍弓箭手在战场上保持双刀出鞘；（3）双刀动作与接战无报错。若仍只剩单刀，直接读取新会话的 `OBJECT_AUDIT_ITEM`（现在会带 `usage=`/`flags=`）与 `AI_WEAPON_SELECTION_SKIPPED` 的 `main=`/`offhand=`：若跳过前 offhand 已是 `None`，说明清空发生在 `UpdateWeapons` 之外，下一步应转向 `Agent.UpdateFormationOrders` → `EnforceShieldUsage` 这一条 Agent 级（非 tableau）边界，而不是再换一个出生期入口。

## 2026-08-30 弓箭手原生武器选择后的副手同步候选（已实测失败，已撤回）

- 用户确认当前基线：预览人物正常、双刀与灰袍单手剑同型、灰袍武将双刀动作正常，双刀与普通士兵交战正常；唯一遗留是灰袍弓箭手切入近战时只拔出右手刀。
- 对照 1.5.2 反编译，`Mission.SpawnTroopWithAgentBuildData` 在 `SpawnAgent` 返回后才调用 `WieldInitialWeapons()`；现有 `OnAgentWieldedItemChange` 监听已在 SpawnAgent 后挂载，但 Native AI 的 `UpdateWeapons()` 可能清空 Weapon0 而不触发该托管回调。此前追踪只看到 `SPAWN_AGENT_POSTFIX`，无法证明近战选择边界被覆盖。
- 新增精确到 `Agent.UpdateWeapons()` 的 Harmony postfix：调用现有标准 `TryToWieldWeaponInSlot(WeaponItemBeginSlot, InstantAfterPickUp)`，仅当 Agent 携带完整 GreyWarden 双刀且角色为 `gwarcher`/自定义武将或玩家时才进入；普通 AI、原生武将、盾牌、远程防御、伤害和模型路径不变。同步请求会写入 `ARCHER_OFFHAND_PAIR_REQUEST`，便于从监控确认 Native 清空后的补拔刀是否命中。
- 本候选不使用 Mission Tick、不创建或复制武器实体、不修改 `MissionWeapon` 统计，不恢复已撤销的盾牌字段或合成伤害补丁。若实机仍只显示单刀，需读取 `ARCHER_OFFHAND_PAIR_REQUEST` 的主副手索引和后续状态，再决定是否继续沿同一 Native 边界收敛。
- `dotnet build GreyWardenPolicePurity.slnx --configuration Release --no-restore -t:Rebuild -p:DeployToLiveModule=true` 成功，0 errors、44 条既有 nullable warnings；离线 Harmony 预检确认 `Agent.UpdateWeapons_Patch1` 可生成。仓库 `_Module` 与 live 模组 36 个可部署文件缺失/哈希差异均为 0，ModuleData XML 解析失败 0；live diagnostics DLL SHA-256 为 `5C7D575C2E8F04594D0C88197B99998287FD1370C3237E89BB197059CACA0B36`，中文 README `B7FF0E57C6D5B6927F5EBAA587C7B8AB4964516DCF1A1D715D1748F16131F538`、英文 README `A3BADFBEA0CCC693C941D9ACC5CB4CDF84894A7FFF77955A61EDE54FADC14356`；Bannerlord 进程数 0，未启动游戏。

## 2026-08-30 1.5.2 进入成功后的三项回归：恢复弓箭手 AI 资格并确认模型差异来源

- 用户复测确认自定义战斗已经可以正常进入，但反馈预览模型异常、弓箭手双刀没有生效，以及武将/士兵双刀外观不同于灰袍单手剑。最新追踪在 `11:08:24` 显示 `gwp_custom_commander` 两个 `CharacterCode.CreateFrom` 重载均成功；大量 `gwarcher` 的 `EQUIP_SCOPE_PREFIX active=False` 与当时刻的稳定性开关一致，说明弓箭手没有进入双刀 AI 装配链，而不是装备码缺失。
- 按本次实测结果，将 `HasCompleteAiLoadout` 从恒 false 改为严格限定：`IsAIControlled && Mission != null && gwarcher && Weapon0/Weapon1` 完整双刀。这样只恢复真实战场弓箭手的 AI 装配、专用动作集和双刀同步；百科、人物预览、自定义战斗 tableau 没有 Mission，不会进入该链。玩家路径保持不变。
- 双刀外观不同的原因已确认：上一候选为避开预览链，把双刀临时改成固定 `mesh="vlandia_twohanded_sword_a"` 的普通 Item，而灰袍 `gwonehandedsword` 是由 `vlandian_blade_3 + vlandian_guard_8 + sturgian_grip_36 + empire_pommel_6` 组合的 CraftedItem，因此不可能同型。用户明确要求只用 GreyWarden 既有外观后，已撤回该普通 Item 候选，恢复项目中原有的 `GwpOneHandedSwordDualOffhand/Mainhand` 模板、`gwp_vlandian_blade_3_dual` 副手片和四件同型部件。
- 对照 ROT 的 `DualWieldingPatches`，其双刀 Item 只依靠 `dual_shield`/`dual_shield_thrust` usage、`HeldInOffHand` 和 bone-20 碰撞例外，并不把副手剑改成盾牌。旧 `MissionWeapon.GetWeaponStatsData` 后置曾给副手注入 `HasHitPoints | CanBlockRanged`，与用户反馈的防御状态异常相符；本轮先撤销这些写入，取得后续接战转储后又把相关全局入口完整删除。
- 11:16:53 的上一候选实测追踪显示所有记录到的 `gwarcher` 均以 `active=True` 完成装配作用域、`exception=False`，并成功应用 `as_gwp_dual_warrior`；这证明弓箭手生成阶段不是 11:18 Native 崩溃点，不能再把问题归回装备码缺失。
- 已核对程序集、`SubModule.xml` 和全部双刀 XML：没有 ROT DLL、程序集、物品 ID 或模型路径。`CraftingPieces`、`CraftingTemplates`、`WeaponDescriptions` 现重新对 Campaign、CustomGame 与 EditorGame 注册，避免人物预览解析 CraftedItem 时缺少模板；副手片只增加 `ForceAttachOffHandPrimaryItemBone + HeldInOffHand + WoodenParry`，武器描述仍只有 `MeleeWeapon`，没有 `HasHitPoints`、`CanBlockRanged`、盾牌类别或盾牌碰撞体。
- 11:18:34 的最新 WER/应用事件确认 `TaleWorlds.Native.dll` 在 0x720e18 处发生 `0xc0000005`，时间线位于 11:17:54 冲锋命令之后；加载、`CharacterCode` 和弓箭手生成均已成功。该边界与防御接触时 `MeleeHitCallback` 重新调用 `victim.RegisterBlow` 的合成控制伤害一致。按 ROT 的最小实现，已完整删除 `GwpDualBladeDefenceBypassPatch`，不再 patch `MeleeHitCallback` 或把伪造 `Blow/AttackCollisionData` 送回 Native；原生近战格挡、双刀 bone-20 碰撞和普通伤害仍保留。
- 同时完整删除只剩监控作用的 `Agent.EquipItemsFromSpawnEquipment`、`MissionWeapon.GetWeaponData/GetWeaponStatsData` 以及 `Mission.MissileHitCallback` 补丁。它们不再改变任何武器数据，也不再占用曾污染 tableau 和接战路径的全局入口。当前战斗运行面只剩真实 Agent 出生后的专用动作集、事件式拔刀同步、bone-20 碰撞例外与左手挥砍伤害类型修正。
- 双刀资格重新明确为：AI 只允许 `gwarcher` 与 `gwp_custom_commander`，其他 AI 士兵即使意外取得同名物品也不进入动作集/同步；玩家控制的角色只要携带完整配对即可继续使用双刀。伤害类型、击倒资格和地面拾取使用同一判断，避免配置与运行范围分叉。
- 最终 Release 重建与 live 部署成功，0 errors、44 条既有 nullable warnings；客户端/编辑器 DLL 为 800256 字节、SHA-256 `47B2639352F4077E2EB6E8E1BFC69AAB15F17E06CA710601CC90A31D780DB527`。离线 Harmony 预检只剩 CharacterCode/自定义武将目录、模板目录、bone-20 碰撞、伤害类型、真实 Agent 动作集和地面拾取入口，已删除的五个全局/合成入口不再出现。仓库与 live 36 个可部署文件缺失/差异均为 0；中文 README SHA-256 `C8FE2B1915984E108CC8DDEEA903A1257CBDCDC99009AC89D9C99EF570AA6717`、英文 README SHA-256 `69D21CF1C91E3340A897E340954DD3F931FC1551B50AA4F4A0EA443BF17804C0` 均与 live 一致。Bannerlord 进程数 0，未启动游戏。下一轮由用户验证预览人物、双刀同型外观、AI 双刀拔出以及首次真实近战防御/接战。

## 2026-08-30 1.5.2 自定义战斗弹窗：接入 NavalDLC 角色列表并补齐枚举监控

- 用户最新复测仍是在点击自定义战斗后直接弹错；本轮没有启动游戏，只读取最新追踪和转储。`GreyWarden-DualBlade-Trace.log` 的新会话为 `NavalDLC.CustomBattle.NavalCustomGame`，早期 `OBJECT_AUDIT_* missing=true` 发生在 `OnGameStart`，不能作为对象未注册证据；随后 `gwarcher` 的两个 `CharacterCode.CreateFrom` 重载均出现 `OK` 且 `exception=none`。最新转储为 `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.27740.dmp`（101494121 字节，10:57:32），故障边界已从 CharacterCode 前移到其后的自定义战斗角色目录/视图初始化。
- 反编译 `NavalDLC.CustomBattle.dll` 确认该 DLC 使用独立的 `NavalDLC.CustomBattle.CustomBattle.NavalCustomBattleData.Characters` 迭代器，原有 `CustomBattleData.get_Characters` 补丁不会覆盖海战自定义模式。新增反射目标补丁，在可选 NavalDLC 已加载时把灰袍自定义武将插入第一项，同时保留 `commander_1..commander_24`；若 DLC 未安装，目标枚举为空，不会阻断启动。
- 自定义武将 XML 的双刀槽位统一使用普通 Item ID（去掉 `Item.` 前缀），并复用与弓箭手相同的静态普通物品解析链。新增角色列表入口 Prefix/Postfix/Finalizer；Postfix 先物化原生迭代器并过滤空项，若原生枚举器抛异常会留下 `CUSTOM_BATTLE_COMMANDER_INSERT_FAILED`，不再让诊断逻辑改变返回值。
- 离线预检已实际加载本机 NavalDLC 程序集，确认 `NavalCustomBattleData.get_Characters` 与原生 `CustomBattleData.get_Characters` 均能生成 Harmony replacement；Release 重编译/部署成功，0 errors、44 条既有 nullable warnings。客户端/编辑器 diagnostics-enabled DLL 均为 800768 字节、SHA-256 `54B989D12E935598FE63124B11DF0464A9956186C021C0EEC3AE64A1AEB8418A`；仓库 `_Module` 与 live 的 36 个可部署文件缺失 0、哈希差异 0，中英文 README 也已同步。未启动游戏，等待用户重新点击自定义战斗后再读取 `NAVAL_CUSTOM_BATTLE_*` 与 `CUSTOM_BATTLE_*` 事件。

## 2026-08-30 1.5.2 双刀稳定性基线：改用 ROT 普通物品并停用 NPC 注入

- 用户连续两次复测均在点击自定义模式时直接报错。最新会话生成 CrashDump `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.28100.dmp`（102020306 字节，02:08:19）；`GreyWarden-DualBlade-Trace.log` 最后仍是 02:08:05 的 `CHARACTER_CODE_CREATE` 与 `CHARACTER_CODE_CREATE_EQUIPMENT`，没有成功后置、没有自定义武将列表事件。恢复 `bo_sword_one_handed` 后症状完全不变，因此排除“仅由静态 `bo_mace_a` 导致”；稳定崩溃边界是 `CharacterCode` 将 `gwarcher` 的隐藏双刀 CraftedItem 交给原生 tableau 解析之后。
- 直接核对本机 ROT 运行数据：`D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ROT-Content\ModuleData\items.xml:843-860` 的 `dual_blades/dual_blades2` 均是普通 `<Item>`，不是 CraftedItem；两者固定 `body_name="bo_mace_a"`、`recalculate_body="false"`。主手使用 `dual_shield_thrust`；副手使用 `dual_shield` 并设置 `WoodenParry + ForceAttachOffHandPrimaryItemBone + HeldInOffHand`。这条静态物品链不依赖锻造模板，也不需要 `MissionWeapon` 运行时改写。
- 已把 `gwdualbladeoffhand/gwdualblademainhand` 从 CraftedItem 重建为同结构普通 Item，保留本模组剑刃 mesh 与既有双刀 usage/holster 资源；从 `SubModule.xml` 停止注册已不再需要的 `CraftingPieces`、`CraftingTemplates` 和 `WeaponDescriptions`。没有预览装备替换或隔离副本。
- `gwarcher` 固定槽位仍为 `Item0=gwdualbladeoffhand`、`Item1=gwdualblademainhand`，已删除贵族长弓和箭袋并改为 Infantry；`gwp_custom_commander` 同样只装备这两个固定槽位。所有原生自定义武将仍保留，灰袍武将继续由列表补丁插入第一项。
- 按用户要求暂停 NPC 双刀机制：`GwpDualBladeLoadout.HasCompleteAiLoadout` 当前恒为 false，因而 AI 装备作用域、WeaponData/WeaponStats、专用动作集、远程格挡修正和 AI 接战分支均不对任何 NPC 生效。当前候选只验证“普通物品能否进入自定义模式、显示人物并静态装备双刀”；后续机制必须在该基线由用户确认后另行恢复。
- 监控加强：`OnGameStart` 新增游戏类型、双刀 Item 的类型/body/collision/usage/flags、弓箭手与自定义武将完整装备码审计；两个 `CharacterCode.CreateFrom` 重载新增成功后置和异常 finalizer。若再次原生退出，可由最后一条事件精确区分对象加载、装备码生成、CharacterCode 返回和自定义武将列表枚举。
- 项目根 `AGENTS.md` 新增“Stable features require local Git checkpoints”：用户实机确认功能稳定后，继续风险开发前必须创建包含实现、双语 README 和维护记录的本地 checkpoint commit，并在本文记录 commit hash 与复测结论；仍崩溃或未测试候选不得伪装成稳定检查点。规则本身已单独保存为本地 commit `d90dac1`；当前双刀版本尚未实机通过，因此不创建错误的功能基线提交。
- Release 重编译/部署成功，0 errors、44 条既有 nullable warnings；XML 解析检查 `items.xml/spnpccharacters.xml/sphpcustombattle.xml/SubModule.xml` 全部通过。离线 Harmony 预检确认 CharacterCode 双重载监控、自定义武将列表、装备作用域及现有战斗入口均能生成；NPC 资格因稳定性开关恒为 false，不会进入注入分支。仓库 `_Module` 与 live 的 36 个可部署文件缺失 0、哈希差异 0；live 客户端/编辑器 DLL 均为 800768 字节、SHA-256 `D4902E51E8041EC331C4D5C8D024E7F64DF9D6D565542A9EEBB70BB1FD04DE6D`。未启动游戏、未制作正式 ZIP。

## 2026-08-30 1.5.2 双刀原生装配崩溃定位：移除运行时碰撞体改写

- 用户最新复测反馈为“普通士兵可以进入，但双刀弓箭手弹错；外部武将预览没有模型”。本次没有启动游戏，只读取监控文件。最新双刀追踪日志 `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-DualBlade-Trace.log` 在 `gwarcher` 的 `WEAPON_STATS_PATCH` 后立即结束；没有对应的 `EQUIP_SCOPE_FINALIZER` 或 `SPAWN_AGENT_POSTFIX`。普通士兵的作用域进入/退出均完整，说明崩溃点已收窄到副手 `WeaponData` 写入之后的原生 `WeaponEquipped` 参数消费。
- 追踪中最后一条双刀数据曾显示代码在托管侧把 `WeaponData.Shape` 与 `CollisionShape` 运行时替换为 `PhysicsShape.GetFromResource("bo_mace_a")`。这会在原生装备注册时传入新取得的非原生时序句柄，是当前最直接的高风险点；不能继续依赖运行时句柄改写。
- 已删除 `GwpDualBladeAiWeaponDataPatch` 中的运行时 `PhysicsShape` 获取和 `Shape/CollisionShape` 赋值；本次进一步撤回静态 `body_name="bo_mace_a"`，恢复双刀 BladeData 原有 `bo_sword_one_handed`，因为新追踪证明前者会在 `CharacterCode.CreateFrom(gwarcher)` 预览阶段直接中断。远程命中回调仍会把副手的远程盾挡结果还原为普通命中，因此不增加远程防御。
- 外部预览仍通过只读 `CharacterCode.CreateFrom` 监控确认：此前的 `gwarcher` 和原 `commander_2` 均能生成完整装备码。本轮将自定义武将改为新增 `gwp_custom_commander`，并通过 `CustomBattleData.get_Characters` 插入第一项，保留原生 `commander_1..commander_24`；本轮没有对预览对象做装备替换或隔离。若模型仍缺失，将以新追踪中的自定义角色装备码与对象加载日志继续区分“资源解析失败”和“自定义战斗选择器覆盖”。
- Release 重编译/部署已通过，0 errors、44 条既有 nullable warnings；离线 Harmony 预检确认新增 `CustomBattleData.get_Characters` 插入补丁、双刀作用域和武器数据入口均成功生成。仓库 `_Module` 与 live 的 36 个可部署文件缺失 0、哈希差异 0；live 客户端/编辑器 DLL 均为 798208 字节、SHA-256 `B2466D3709811203C678BC9C814B1A12EBEA24B2D3B658FC8F778BBECDED6DFA`。未制作正式 ZIP、未启动游戏。用户负责实机复测，开发侧只读取监控。

## 2026-08-30 1.5.2 追加双刀/预览边界监控

- 用户复测后生成最新 `rgl_log_21560.txt` 与 CrashDump `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.21560.dmp`；日志仍在 `MissionScreen::OnActivate` 后停止，未出现 `archer_spawn`，`rgl_log_errors_21560.txt` 没有托管异常。由此确认上次候选仍未能定位原生退出点。
- 在唯一的 `GwpDualBladeActionSetPatch.cs` 中加入开发期 `GreyWarden-DualBlade-Trace.log`：记录 SubModule Harmony 安装结果、`CharacterCode.CreateFrom` 的弓箭手/指挥官预览输入、Agent 装备注入作用域进入/退出、双刀 `WeaponData`/`WeaponStatsData` 修改以及 `SpawnAgent` 后置结果。每条记录立即追加到文档目录，诊断异常自动吞掉，不改变游戏流程。
- 保持本轮安全边界：监控补丁只读/记录，不替换预览装备，不调用原生 `WeaponEquipped`，不启动游戏。用户下一次复现后，先读取 `GreyWarden-DualBlade-Trace.log` 最后一条事件，再与同一时间的 `rgl_log` 对齐，即可判断崩溃发生在预览 CharacterCode、装备作用域、武器数据生成还是 SpawnAgent 之后。
- Release 重编译成功，0 errors；离线 Harmony 预检新增两个 CharacterCode 追踪目标并全部成功。仓库 `_Module` 与 live 运行文件共 36 个文件缺失 0、哈希差异 0；live 客户端/编辑器 DLL 均为 798208 字节、SHA-256 `4D8DEB1A66D1EDCC5480E145ED11309616BD8F09C8E0E5BE6053EA5693E3E491`。未制作正式 ZIP、未启动游戏。

## 2026-08-30 1.5.2 装备注入回归：撤销 WeaponEquipped 原生入口补丁

- 用户复测仍在进入自定义战斗后报错且没有人物；最新 `rgl_log_6088.txt` 在
  `MissionScreen::HandleActivate` 后停止，没有 `archer_spawn`，错误日志无托管异常，CrashDump 为
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.6088.dmp`。
- 这排除了弓箭手接战、双刀碰撞和动作切换；崩溃发生在第一批 Agent 装备注入时。上一候选把
  `Agent.WeaponEquipped` 私有原生入口作为 Harmony 前置并修改 `WeaponData`，即使不主动调用该方法，仍会把
  原生注册时序暴露给托管补丁，不能继续保留。
- 已从 `GwpDualBladeActionSetPatch.cs` 删除 `GwpDualBladeAiNativeRegistrationPatch`。双刀数据改为同一文件中的三个协同入口：
  精确四参数 `Agent.EquipItemsFromSpawnEquipment` 作用域、作用域内的 `MissionWeapon.GetWeaponData` 和
  `MissionWeapon.GetWeaponStatsData`。只有真实战场中携带完整 `gwarcher` 双刀的 AI 才进入作用域；百科、士兵百科和
  自定义战斗人物预览不会调用 Agent 装配链。
- 副手仍使用 `bo_mace_a` 的形状、有效耐久和近战标志；远程命中回调继续把副手的远程盾挡结果还原为普通命中，因而不提供
  远程防护。未恢复旧的四个全局补丁，也未启动游戏。
- Release 重编译成功，0 errors；离线 Harmony 预检确认四参数装配作用域、两个 MissionWeapon 数据补丁、模板目录过滤、
  SpawnAgent、远程命中、碰撞、伤害、击倒和地面拾取目标均成功。仓库 `_Module` 与 live 运行文件共 36 个文件缺失 0、哈希差异 0；
  live 客户端/编辑器 DLL 均为 795136 字节、SHA-256 `935C673DD91421469D21BC94A8B591FCF727580F925F8A8F029C82D6BE35BEDB`。

## 2026-08-30 1.5.2 双刀与预览回归：恢复单入口原生注册并保留真实模板

- 用户最新复测仍是“进入游戏直接报错、人物模型不显示”。按用户要求没有启动游戏，只读取 `rgl_log_22108.txt`、WER、CrashDump、ROT 数据、源码和维护记录；当前没有晚于 01:05 的新测试日志。
- 该会话在 `MissionScreen::HandleActivate` 后立即停止，没有任何 `archer_spawn`；Windows WER `CLR20r3` 记录故障程序集 `TaleWorlds.MountAndBlade.AutoGenerated`、`System.AccessViolationException`，转储为 `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.22108.dmp`。根因是上一候选在 `Mission.BuildAgent` 结束后反射调用私有 `Agent.WeaponEquipped`，并传入从错误时序取得的武器实体句柄；该直接 Native 重挂已完整删除。
- 开发记录的既有 A/B 已证明四个 `Agent.EnforceShieldUsage` / `Agent.EquipItemsFromSpawnEquipment` / `MissionWeapon.GetWeaponData/GetWeaponStatsData` 全局脚本会令人物预览回归。短暂恢复的四脚本候选因此同样撤销；当前双刀资格、动作分配和切手逻辑重新合并在唯一的 `GwpDualBladeActionSetPatch.cs` 中。
- 新的 `GwpDualBladeAiNativeRegistrationPatch` 只在原生自己调用私有 `Agent.WeaponEquipped` 的入口前修改当前 `gwarcher` 的 `Weapon0` 参数；不主动调用该方法、不缓存或传入实体句柄、不重复注册。原生调用继续拥有正确时序、实体和释放流程。副手参数保留剑用途，补 `HasHitPoints | CanBlockRanged`、有效耐久，并同时使用 ROT 固定双刀验证过的 `bo_mace_a` Shape/CollisionShape；不再使用曾导致接战崩溃的 `bo_wlarge_shield` 碰撞体。`Mission.MissileHitCallback` 仍把该副手的远程盾挡结果改回普通命中，所以只保留近战攻击/招架，不提供远程防护。
- 人物模型消失的另一条直接根因是 `SubModule.AfterRegisterSubModuleObjects` 曾注销 `GwpOneHandedSwordDualOffhand/Mainhand`。1.5.2 的 `CharacterCode`、百科和自定义战斗 tableau 会重新解析真实 CraftedItem 的模板，注销后模型链失效。现在模板始终保留在对象管理器供真实人物和武器解析；同一脚本中的 `GwpDualBladeCraftingTemplateVisibilityPatch` 只从 `CraftingTemplate.All` 公共目录过滤这两个模板，因此城镇订单和锻造界面仍不会选中它们。没有装备码替换、展示用剑盾、人物数据副本或预览隔离。
- ROT 当前 `dual_blades/dual_blades2` 静态定义再次核对：两把刀使用 `bo_mace_a`，副手为 `OneHandedSword + MeleeWeapon + HeldInOffHand`，固定槽位规则与本模组一致。`gwarcher` 仍是唯一带 `Weapon0=gwdualbladeoffhand`、`Weapon1=gwdualblademainhand` 的兵种；主手以外的领主、玩家和普通士兵不进入 AI 注册补丁。
- 最终 `Release -t:Rebuild --no-restore -p:DeployToLiveModule=true` 已通过，0 errors、44 条既有 nullable warnings。离线 Harmony 预检成功生成 `Agent.WeaponEquipped`、`CraftingTemplate.get_All`、`Mission.SpawnAgent`、远程命中、ROT 碰撞/伤害、击倒和地面拾取 replacement；ILSpy 类型表确认旧四个资格类型与直接 `WeaponEquipped.Invoke` 均不再存在。客户端和编辑器 diagnostics-enabled DLL 均为 `794624` 字节、SHA-256 `86F6DAEDBDF32C7487D54B0B5ECE1FB32B6CCA18CE4424A41697CDD08B4F7573`；仓库 `_Module` 与 live 的 36 个运行文件缺失 0、哈希差异 0，26 个 XML/XSLT/mbproj 解析错误 0。中英文 README 仓库/live SHA-256 分别为 `E0A4ADC9F6830523DAFAFBCC9BFE11D2774E3F0E05D010CB336623A2F87D623B`、`FEC3C9587CD68038C03C5200878A22E9D43E3E1552B29236549DAFEA00B15E0B`；Bannerlord 相关进程数 0。未启动游戏、未制作正式 ZIP，实机结果仍由用户测试。

## 2026-08-29 回归修正：恢复已验证动作资源并修复双刀预览入口

- 用户在上一版测试后报告两个明确回归：弓箭手切入近战又只拔出一把剑，人物预览/士兵百科模型再次消失；按要求没有启动游戏，开发侧只读取监控和历史记录。
- 最新 `rgl_log_38508.txt`（19:29 会话）记录 `199` 次 `archer_spawn` 与 `199` 次 `archer_melee_pair`；配对行连续显示 `main=Weapon1; offhand=None`，且错误日志没有托管异常或新的 CrashDump。由此排除“本轮接战碰撞再次崩溃”，确认单纯调用 `TryToWieldWeaponInSlot` 不能把副手注册到 1.5.2 的 Native Agent。
- 按 2026-08-28 已确认的 ROT/双刀基线恢复 `ModuleData/action_sets.xml` 为空入口，并恢复同时覆盖 `as_human_warrior` 与 `as_human_female_warrior` 的 `action_sets.xslt`；此前物化的单独 action-set 文件已经由用户实测排除，保留在 `.codex_tmp/action-set-materialization-audit-20260828/` 仅作回滚材料。这样女性 `gwarcher` 的生成校验和专用 `as_gwp_dual_warrior` 都有闭合动作映射。
- 在 `CharacterViewModel.FillFrom(BasicCharacterObject,int,string)` 增加完整 `gwarcher` 双刀的预览安全副本：身体属性、护甲、坐骑和角色数据保持原值，只把展示装备码中的双刀替换为普通灰袍剑盾。自定义战斗的 `UpdateCharacterVisual` 仍有线程范围装备码兜底；真实战场和装备图标不改，因此百科与自定义战斗不再把隐藏双刀锻造物直接交给共享 tableau。
- 恢复 1.5.2 精确 `Agent.EquipItemsFromSpawnEquipment(bool,bool,bool,int)` 作用域及其 `MissionWeapon` 数据代理，仅对当前 Mission 中完整 `gwarcher` 的 Native 装备副本补齐原生双持资格；其他兵种、玩家装备和预览对象不进入该作用域。ROT 的固定槽位约定继续保持：副手 `Weapon0`、主手 `Weapon1`；骨骼 20 的碰撞例外也已按 ROT 规则收窄。
- Release 构建成功（0 errors；44 条既有 nullable warnings），客户端与编辑器 diagnostics-enabled DLL 均为 `798208` 字节、SHA-256 `8CB30AEAF06933F09243884B1E2E21794101ECEF3D6A74AAB85F1A40A393C958`。仓库 `_Module` 与 live 的 36 个运行文件缺失 0、哈希差异 0；构建后另将双语 README 镜像到 live，中文 SHA-256 `4529A027EB8F5C5AD4B796CA9C40705425837AC1E3DE43AB88646BADA0F50ED5`、英文 SHA-256 `BB98AFA89489B206BAF402EF588705E13B29D783B4E1922BA8ED9FBA0DB3740E` 均一致。`action_sets.xml` SHA-256 为 `06C5509045556C00081B93395A2E84CD863419F6ACA9FAA7320ACED8B99A1E60`，含男女动作映射的 `action_sets.xslt` SHA-256 为 `64B4766189AF48D28ADBE0F6047054EFDCB8345E8E5BBFA8AE37475E77F6B947`。离线 Harmony 预检确认三个预览入口、四个 AI 资格入口及 ROT 碰撞入口全部生成；Bannerlord 相关进程数为 `0`。本轮仍由用户实测，随后只读取新的 `rgl_log`。

## 2026-08-29 双刀接战弹错/卡死与人物预览回归：撤销四个全局盾牌伪装补丁

- 用户指出两个问题均是此前已经定位并修复过的回归：百科/人物预览模型消失或姿态异常，以及双刀士兵与普通士兵接战时弹错或卡死；副手刀只能用于近战格挡，不能防御远程攻击。按要求只读取开发记录、源码、`rgl_log` 与系统事件，没有启动游戏。
- 维护记录中的既有 A/B 已证明：安装 `Agent.EnforceShieldUsage`、`Agent.EquipItemsFromSpawnEquipment`、`MissionWeapon.GetWeaponData` 或 `MissionWeapon.GetWeaponStatsData` 这些全局双刀补丁会污染 `CharacterTableau` / `AgentVisuals`，曾导致百科与自定义战斗人物模型消失。旧版把副手剑加入 `CanBlockRanged` 并赋予 `bo_wlarge_shield` 碰撞体时，也曾在真实接战阶段触发 `TaleWorlds.Native.dll` 空指针；不应把近战剑伪装成盾牌。
- 当前回归代码重新包含了上述四个全局 Harmony 类型，并给 `gwdualbladeoffhand` 的 Native 副本加入 `HasHitPoints | CanBlockRanged`、`bo_mace_a` 主体及 `bo_wlarge_shield` 碰撞体。最新 `rgl_log_34236.txt` 没有托管异常，但在下达冲锋命令后停止；同一会话记录 `597` 次弓箭手生成和 `1759` 次 `archer_melee_pair`，说明旧的全局资格链与高频武器回调都已重新进入运行时。当前没有对应的新 CrashDump，因此本轮根因判断来自“相同代码重新出现 + 历史转储/A-B 已确认”的直接证据，不把缺失的新转储伪装成新增证据。
- 已删除 `GwpDualBladeAiNativeSyncPatch.cs`，把无副作用的 `GwpDualBladeLoadout` 资格判断并回唯一的 `GwpDualBladeActionSetPatch.cs`；最终 DLL 不再包含 `GwpDualBladeAiShieldEnforcementPatch`、`GwpDualBladeAiEquipmentSyncScopePatch`、`GwpDualBladeAiWeaponDataPatch` 或 `GwpDualBladeAiWeaponStatsPatch`，也不再 patch 上述四个全局方法。副手保持原始 `OneHandedSword + MeleeWeapon`、`HeldInOffHand` 与剑体碰撞数据，不注入 `CanBlockRanged`、盾牌碰撞体或盾牌主体。
- `GwpDualBladeWieldSyncPatch` 只在灰袍弓箭手实际切到 `Weapon1` 主手刀且副手尚未在手时补一次配对；加线程重入保护，收刀同理。开发日志只在状态确实改变时写 `archer_melee_pair`，不再为已经正确持刀的每次回调重复写入。左手 bone 20 碰撞例外同时要求攻击来源槽为 `Weapon0`，普通士兵或普通主手武器不会误入双刀碰撞放行。
- Release 重建成功，`0` errors、`44` 条既有 nullable warnings；更新文档后的最终增量构建为 `0` errors、`0` warnings。Windows PowerShell 5.1 / .NET Framework 对六个保留的双刀 Harmony 类型逐类安装，全部返回 `PATCH_OK`；ILSpy 类型表确认四个旧全局类型均不存在，只保留同一动作脚本中的 `GwpDualBladeActionSetPatch`、`GwpDualBladeWieldSyncPatch` 与纯帮助类 `GwpDualBladeLoadout`。客户端与编辑器 diagnostics-enabled DLL 均为程序集 `1.4.11.0`、`793600` 字节、SHA-256 `BF57B583371C9A8E5CFDD011B51EF7115E204A3D2D3DA18EDF17609DD09EABCB`。仓库 `_Module` 的 `36` 个可部署文件与 live 缺失 `0`、哈希差异 `0`，XML/XSLT/mbproj 解析失败 `0`；中英文 README SHA-256 分别为 `77FA9D61DB9C2DA0317E93F2F5B695AF5D6AF55C1E5468266E56B21842F7E099`、`046EA05BBDD4D6E7B0DDCFE80651F30E7E99C890DDB5D555C0CEB84F8B9C5A1A`，均已同步 live。`git diff --check` 无空白错误，Bannerlord 相关进程数为 `0`。未制作正式 ZIP；游戏内结果仍由用户测试。

## 2026-08-29 Bannerlord 1.5.2 弓箭手双刀补丁未加载：修复 SpawnAgent 四参数签名

- 用户说明其他模型已完成多轮自定义战斗修复，当前问题已收敛为：`gwarcher` 能进入战斗，但无法正常拔出双刀；功能边界明确为仅灰袍弓箭手使用双刀，领主、玩家和其他兵种均不用。
- 最新四次实机日志 `rgl_log_13684.txt`、`rgl_log_37492.txt`、`rgl_log_16140.txt`、`rgl_log_41700.txt` 均在启动时明确报告
  `Undefined target method for ... GwpDualBladeActionSetPatch::Postfix`。日志同时能读取双刀物品并正常进入/退出战斗，证明资源和弓箭手装备已经存在，真正断点是 Harmony 没有安装运行逻辑。
- 直接反编译本机 Bannerlord `v1.5.2.120933` 的当前 `TaleWorlds.MountAndBlade.dll`，确认
  `Mission.SpawnAgent` 已由旧的 `(AgentBuildData, bool)` 改为
  `(AgentBuildData, bool, Equipment, ItemObject)`。旧属性目标找不到后，`SubModule` 的一次性 `PatchAll` 在该类中止，动作集切换、主副手同步和后续双刀补丁因此都没有生效。
- `GwpDualBladeActionSetPatch` 已精确改挂四参数重载。出生后只安装专用动作集，不再强制覆盖原生初始武器选择：弓箭手可先使用 `Item2` 贵族长弓和 `Item3` 穿甲箭；原生 AI 切换 `Item1` 主手刀进入近战时，`OnWieldedItemIndexChange` postfix 同步拔出 `Item0` 副手刀。
- 双刀资格现在同时要求角色 ID 为 `gwarcher` 且 `Weapon0/Weapon1` 是完整双刀。该限制覆盖动作集、AI 副手资格、盾牌阵型绕过、地面拾取、双刀伤害类型与击倒判定。`gw_leader_0`、`gw_leader_5` 已恢复普通领主装备表，入会发放表也移除双刀；未再使用的 `spc_gw_leader_dual` 装备表已删除，因此 XML 层也只有弓箭手装备双刀。
- diagnostics-enabled DLL 为弓箭手出生和弓转近战配对各写一条
  `[GreyWarden Dual Blade] archer_spawn` / `archer_melee_pair` 记录，不使用 Mission Tick 或持续轮询。用户测试后只需读取新 `rgl_log`，确认启动阶段不再出现 `Undefined target method`，并观察近战切换记录。
- Release 构建成功（`0` errors、`44` 条既有 nullable warnings）。使用 Windows PowerShell 5.1 / .NET Framework 对 live DLL 执行完整 `Harmony.PatchAll` 离线预检，结果为 `FULL_HARMONY_PREFLIGHT=OK`；另逐类检查十个双刀 Harmony 类型，目标解析失败 `0`。本轮未启动 Bannerlord，由用户实测。
- 最终增量 Release 同步为 `0` errors、`0` warnings。客户端与编辑器 diagnostics-enabled DLL 均为程序集 `1.4.11.0`、`795648` 字节、SHA-256 `E59B06AF9F86D465F63239A64C4B3444BD47DEE75C8EF2A1FC372350864973AF`。仓库 `_Module` 与 live 可部署文件缺失 `0`、哈希差异 `0`；`spnpccharacters.xml`、`spspecialcharacters.xml`、`gw_equipment_sets.xml` 分别为 `B9AFC0064FB6C4DB7C3902CBCEB40D8F7CDAEBCD5FFD3B3F49E7AD7BCD61E014`、`E67B25D5A2891B304207714FAFB1F5017D1F764F589AC36B9E8A573EDDB57970`、`78D81B290DDCF09F7A02C0066310AE0928FB41FFC53C8F0A3900120FE94021EE`。中英文 README 与 live 分别为 `6660EED494C31F38AB956413314F682E720FCFE401576A74C461BB21A2DAE58C`、`2C056D47C0302FA5DA5F2A138DB8568BEDF9D38AF0F467DAF8E86A0BFF8FF64B`；Bannerlord/Launcher/Native 进程数为 `0`。

## 2026-08-29 修复 EquipItemsFromSpawnEquipment 4 参数重载挂载与 SpawnEquipment 判定

- 根因排查突破：
  - 反编译分析 `TaleWorlds.MountAndBlade.Agent` 内部装备装配机制，发现关键盲点：
    1. `Agent.EquipItemsFromSpawnEquipment` 存在两个重载：2 参数的 `(Equipment, Banner)` 和真正执行 Native 数据注入的 4 参数 `(bool neededBatchedItems, bool prepareImmediately, bool useFaceCache, int faceCacheID)`。
    2. 此前未显式指定参数类型导致 Harmony 挂接到 2 参数入口，在 2 参数入口前置执行时，`Agent.Equipment` 尚未被 `FillFrom` 填充（槽位全部为 Empty）。
    3. `HasCompleteDualBladeLoadout` 原先仅检查 `agent.Equipment`，导致 `_activeAgent` 在真正执行武器数据注入时判定为 `false`，未能成功注入 Native 盾牌耐久和碰撞体。
- 本轮修复实施：
  1. `GwpDualBladeAiEquipmentSyncScopePatch` 精确挂接到 4 参数重载 `EquipItemsFromSpawnEquipment(bool, bool, bool, int)`。
  2. `HasCompleteDualBladeLoadout` 与 `IsPlayerDualBlade` 扩展为优先检查 `agent.SpawnEquipment`，并兼顾 `agent.Equipment`，确保在装备注入前即能 100% 识别双刀兵种并开启 Native 注入作用域。
  3. `GwpDualBladeActionSetPatch` 在 `SpawnAgent` 后置中，确保对未出鞘的副手与主手双刀执行即时出鞘。
  4. `spnpccharacters.xml` 中 `gwarcher` 继续保持纯双刀配置（`Item0` 副手双刀、`Item1` 主手双刀，无长弓箭矢）。
- 构建与部署：
  - Release 构建通过（`0` errors、`44` 条既有 nullable warnings）；客户端与编辑器 DLL 均为程序集 `1.4.11.0`、`792064` 字节、SHA-256 `7FD790022B9174FC56111A1195F80E6ECEDB61063CCD9808EAF118BDBCA89169`。
  - 仓库 `_Module` 与实机 live 目录逐文件哈希核对一致，`spnpccharacters.xml` SHA-256 为 `61DDBB2467C7932D6F435CDB1B9EEC2DBCDBD325EEAE703A53873FF5912E9B4A`。





## 2026-08-29 灰袍弓箭手双刀拔刀隔离测试（纯双刀进场）

- 用户实测反馈：弓箭手携带“双刀 + 弓箭”进场时，初始拔出长弓；当下达“停止射击”指令或切换近战时，AI 仅拔出主手单手剑，未能进入双持双刀拔刀状态。
- 为隔离该问题究竟是“AI 双持拔刀机制在纯近战下即存在问题”还是“弓箭手从远程武器切换至近战时的切换逻辑未拔出副手”，按用户指令配置隔离测试：
  - `spnpccharacters.xml` 中 `gwarcher` 仅保留 `Item0`（副手双刀 `gwdualbladeoffhand`）与 `Item1`（主手双刀 `gwdualblademainhand`），临时移除 `Item2`（长弓）与 `Item3`（箭矢）。
- Release 构建通过（`0` errors、`0` warnings）；DLL SHA-256 为 `C0CFD70517FFB41248B2503C6A5AED31672E70078AB55891F475401620DE1FFA`。
- 仓库 `_Module` 与实机 live 目录逐文件哈希核对一致，`spnpccharacters.xml` SHA-256 为 `61DDBB2467C7932D6F435CDB1B9EEC2DBCDBD325EEAE703A53873FF5912E9B4A`。



## 2026-08-29 修复战场生成时女性/男性动作集缺失双刀动作引发的崩溃（as_human_female_warrior）

- 崩溃日志排查：`rgl_log_11840.txt` 记录在战场初始化（`CustomBattleScreen` 点击开始战斗并加载 `MissionScreen` 时，时间戳 `17:37:54.970`）抛出大量 `as_human_female_warrior does not contain act_gwd_quick_release_thrust_1h`、`act_run_idle_1h_with_gwd_shld` 等动作缺失报错并导致引擎底层退出。
- 根因定位：
  - `gwarcher` 等兵种设定了 `is_female="true"`，在战场 Agent 创建阶段，原生引擎调用 `CreateAgent` 默认根据性别为 Agent 分配 `as_human_female_warrior`（女性）或 `as_human_warrior`（男性）。
  - 在随后的 `BuildAgent` 中，原生引擎读取武器的 `item_usage_set`（`dual_shield_swing_thrust`），并在当前的动作集中校验动作条目。
  - 原有 `action_sets.xslt` 仅在 `as_human_warrior` 分支克隆并新建了 `as_gwp_dual_warrior`，而未给原生 `as_human_female_warrior` 和 `as_human_warrior` 注入双刀动作映射，导致在 Agent 刚创建还未进入 Postfix 的短暂窗口内触发了原生动作绑定断言崩溃。
- 修复措施：
  - 更新 `action_sets.xslt`：对 `as_human_warrior` 与 `as_human_female_warrior` 均注入 84 个双刀动作映射，并同时保留 `as_gwp_dual_warrior`。
  - 这样无论男性、女性兵种还是英雄，在生成并装配双刀武器时均能原生通过动作校验，配合 `GwpDualBladeActionSetPatch` 完美运行双刀攻防与击倒。
- Release 构建通过（`0` errors、`0` warnings）；客户端与编辑器 DLL 均为程序集 `1.4.11.0`、`792064` 字节、SHA-256 `C0CFD70517FFB41248B2503C6A5AED31672E70078AB55891F475401620DE1FFA`。
- 仓库 `_Module` 与实机 live 目录逐文件哈希核对一致，`action_sets.xslt` SHA-256 为 `9A9B48F6C912FA2E21DC682F0B9C694F4DFBF197C77EAF6C1FFC5E098FEE6B68`。



## 2026-08-29 灰袍弓箭手移除单手剑并配置双刀机制

- 用户实机验证确认自定义战斗正常打开、第二位指挥官 Yao 恢复、灰袍全兵种显示正常。按用户指令进一步调整灰袍弓箭手装备：移除单手剑 `gwonehandedsword`，配备副手双刀 `gwdualbladeoffhand` 与主手双刀 `gwdualblademainhand`，并保留贵族长弓与穿甲箭。
- `spnpccharacters.xml` 中 `gwarcher` 的装备槽位配置为：`Item0`=副手双刀、`Item1`=主手双刀、`Item2`=贵族长弓、`Item3`=穿甲箭。在战场生成时由 `GwpDualBladeActionSetPatch` 识别并挂载专用双刀动作集 `as_gwp_dual_warrior` 与 AI 防御同步，近战攻击与格挡应用灰袍双刀规则与击倒判定。
- Release 构建通过（`0` errors、`0` warnings）；客户端与编辑器 DLL 均为程序集 `1.4.11.0`、`792064` 字节、SHA-256 `C0CFD70517FFB41248B2503C6A5AED31672E70078AB55891F475401620DE1FFA`。
- 仓库 `_Module` 与实机 live 目录逐文件哈希核对一致，`spnpccharacters.xml` SHA-256 为 `B9AFC0064FB6C4DB7C3902CBCEB40D8F7CDAEBCD5FFD3B3F49E7AD7BCD61E014`，README 中英文已同步更新。



## 2026-08-29 恢复 commander_2 原版覆盖与清除 gwarcher 静态双刀装备：稳定进入自定义战斗基线

- 用户实测反馈点击自定义战斗依然在同一处闪退（停在 `NavalDLC.CustomBattle.CustomBattle.NavalCustomBattleScreen::HandleInitialize`），确认用户明确要求：不退回 Bannerlord 版本（保持 1.5.2 beta 兼容），先回退到稳定无报错状态。
- 根因定位确认两项直接诱因：
  1. `sphpcustombattle.xml` 此前改为 `gwp_custom_battle_commander` 并尝试动态插入破坏了原版稳定机制；原生 `NavalCustomBattleData.Characters` / `CustomBattleData.Characters` 固定遍历 `commander_1..commander_24`。旧版直接将灰袍领主 ID 设为 `commander_2` 借助 XML 原生覆盖替换第二名指挥官，无需任何 C# 动态注入，此前一直 100% 稳定运行。
  2. `gwarcher` 在 `spnpccharacters.xml` 中被直接挂载了 `gwdualbladeoffhand` 与 `gwdualblademainhand`，且 `spnpccharacters` 注册了 `CustomGame`。自定义战斗打开时 `NavalCustomBattleArmyCompositionGroupVM` 遍历所有士兵兵种并调用 `CharacterCode.CreateFrom` 生成头像，在非战斗环境下解析副手双刀及已被注销的锻造模板引发原生断言/空引用崩溃。
- 本轮修复实施：
  1. `sphpcustombattle.xml` 灰袍领主 ID 恢复为 `commander_2`，`GwpIds.CustomBattleCommanderId` 同步恢复为 `"commander_2"`。
  2. `spnpccharacters.xml` 中 `gwarcher` 装备恢复为标准配置（`noble_long_bow`, `piercing_arrows`, `gwonehandedsword`），从静态 XML 中移除副手双刀。双刀机制仍完整保留在代码（动作集补丁、伤害模型、击倒判定、切磋与入队获赠），不污染兵种静态预览。
  3. `SubModule.xml` 维持 `spnpccharacters`、`gw_equipment_sets`、`sphpcustombattle`、`items`、`yao_skill` 在 `CustomGame` 生效；`spspecialcharacters` 保持仅在 `Campaign` / `CampaignStoryMode` / `EditorGame` 生效（避免 1.5.2 缺失 `spc_mounted_archery_skills`）。
- Release 构建成功（`0` errors、`44` 条既有 nullable warnings）；客户端与编辑器 diagnostics-enabled DLL 均为程序集 `1.4.11.0`、`792064` 字节、SHA-256 `C0CFD70517FFB41248B2503C6A5AED31672E70078AB55891F475401620DE1FFA`。
- 部署后仓库与 live 可部署文件缺失 `0`、哈希差异 `0`；`SubModule.xml` 当前 SHA-256 为 `26F2790DDF19D9CFC4DB1BC3FBDD4B547BCCC87729A60162DFE7ABF1EE54DABB`，中英文 README 与 live 分别为 `FE9A360D5AA483C4851683C864FCC9A1139B8B71F5E9B46C5C0400E8DDACCAD9`、`5ADFEFA23E0435AEF94F0C277B1DF9FE11642A5107D9E1A3461D169B0392D3C6`。Bannerlord 进程数为 `0`。


## 2026-08-29 第四次自定义战斗崩溃：界面初始化期间注入指挥官是直接触发器

- 最新 `rgl_log_44724.txt` 已确认本轮完全没有打开 `spspecialcharacters.xml`，但仍精确停在
  `NavalDLC.CustomBattle.CustomBattle.NavalCustomBattleScreen::HandleInitialize`；因此战役 Hero、技能模板和装备表均不
  是这次直接触发器。
- 维护历史中的两次 A/B 已给出可复现结论：`CustomBattleData.Characters` getter 包装和
  `CustomBattleSideVM.RefreshValues()` postfix 都在同一个界面初始化阶段导致相同原生崩溃。当前重新启用的
  `GwpCustomBattleCommanderPatch` 属于第一种路径，必须删除，不能继续在该初始化链插入首位指挥官。
- 已删除 `GwpCustomBattleCommanderPatch.cs` 并移除 `OnSubModuleLoad` 的独立安装；保留唯一 ID 的
  `sphpcustombattle.xml` 数据但不注入原生角色枚举。`spnpccharacters`、`gw_equipment_sets`、`items.xml` 和双刀仍注册
  `CustomGame`，灰袍兵种与装备不隔离；`spspecialcharacters` 仍只用于战役/编辑器。
- 这轮先恢复“能进入自定义战斗”的稳定基线，再另行设计不触碰界面初始化链的首位指挥官方案；在找到兼容入口前，不能声称
  “首位且不替换原生”已经实现。本轮不启动游戏，由用户实测后再读新日志。
- 本轮 Release 构建成功（`0` errors、`44` 条既有 nullable warnings）；客户端与编辑器 diagnostics-enabled DLL 均为程序集
  `1.4.11.0`、`792576` 字节、SHA-256
  `4B772E23DBAAFC277A9D937496A310BEDD5316A6166DA15C93EB677A20B58551`。部署后仓库与 live 可部署文件缺失
  `0`、哈希差异 `0`；`SubModule.xml` 当前 SHA-256 为
  `26F2790DDF19D9CFC4DB1BC3FBDD4B547BCCC87729A60162DFE7ABF1EE54DABB`，中英文 README 与 live 现分别为
  `C6BEE3B039E4154A78A055A0CAE2FFBF26D5CCA1C7E32A6B1A6A077A250F5911`、
  `8833E11DDDDEF2B02CBCBB6F604B7EBF28300FB080CB7D8CB8C60387CFAF7A50`。构建不会删除旧 ModuleData，故已核对并从
  live 目录移除本轮撤销的 `ModuleData/gwp_customgame_skill_sets.xml`（旧 SHA-256
  `78A9B3F4DCCC95CFED2D0C5FFB4E20333455DD79467A062E303157015172820D`）。Bannerlord 进程数为 `0`。

## 2026-08-29 自定义战斗恢复完整灰袍数据与首位专属指挥官

- 用户明确纠正功能边界：自定义战斗用于验证并游玩完整灰袍兵种、领主和装备，不应隔离这些内容；灰袍专属
  指挥官应位于人物列表第一位，但不能覆盖任何原生指挥官。上一候选把双刀并入弓箭手后的崩溃与
  CustomGame 数据加载相关联，在没有本次崩溃异常文本的情况下过度隔离了兵种、领主和装备。该方向会直接造成
  灰袍士兵消失，现已判定为错误并撤销，不再作为设计目标。
- `_Module/SubModule.xml` 现在让 `spnpccharacters`、`sphpcustombattle`、`spspecialcharacters` 和
  `gw_equipment_sets` 四个灰袍人物/装备节点全部注册 `Campaign`、`CampaignStoryMode`、`CustomGame` 与
  `EditorGame`。因此自定义战斗会读取完整兵种树、六名领主的装备模板和全部灰袍装备表，而不只是独立指挥官。
- 反编译当前 Bannerlord 1.5.2 的 `CustomBattleData.Characters` 再次确认原生列表固定返回
  `commander_1..commander_24`；`CustomBattleSideVM.RefreshValues()` 按该枚举顺序建立人物选择，玩家侧默认选择索引
  `0`。原先 `sphpcustombattle.xml` 使用 `commander_2` 会按对象 ID 覆盖原生第二名指挥官，不能实现“新增”。
- 专属指挥官 XML 与 `GwpIds.CustomBattleCommanderId` 已改用唯一 ID
  `gwp_custom_battle_commander`。新增 `GwpCustomBattleCommanderPatch`，只把该对象插入原生枚举首位；先完整物化原生
  迭代器并保持其顺序，不删除或改名任何原生对象。静态对象检查结果：官方 XML 仍有 `24` 名指挥官，
  `commander_2` 恰好 `1` 个，官方 XML 与新唯一 ID 冲突 `0`。
- 此补丁在 `OnSubModuleLoad` 中独立安装，然后才执行其它 `PatchAll`。这样 1.5.2 中任何不相关的可选战斗补丁目标
  变化都不会阻止灰袍指挥官进入列表。diagnostics-enabled 开发 DLL 会在列表实际构建时向 `rgl_log` 写入
  `[GreyWarden Custom Battle] commander first=...; total=...; native_commander_2=...`，只记录列表结果，不轮询
  Agent、不启动任务监控；正式 diagnostics-disabled 构建不会包含该输出。
- 两次 Release 构建均成功，最终为 `0` errors、`44` 条既有 nullable warnings。客户端与编辑器开发 DLL 均为
  `794624` 字节、程序集 `1.4.11.0`、SHA-256
  `C79BD7DE4E7A1D7B091ABDFEE9C03B3A3DDAFD6CFCDD8CDDADED915D2FDA25B7`。ILSpy 复核成品确认唯一 ID、首位插入、
  原生顺序保留和开发诊断字符串均已进入 DLL。
- 仓库 `_Module` 与 live 可部署文件缺失 `0`、哈希差异 `0`；`SubModule.xml`、中文 README、英文 README 的
  仓库/live SHA-256 分别一致为 `FAE9E3176086940EFA8B8CB3726158E4C010F19CE201AC61CBEB03373CE674B0`、
  `AFB3F11058C44634AB076F2B520F4C09F8EE7CFA174A87F1B645C442D032D6A3`、
  `72D7EB25FE363F6E44038E7D0A8CC253BE61163B72C195B856C8A09E5F8AF319`。本轮没有启动 Bannerlord，相关进程数
  始终为 `0`，也没有制作正式 ZIP。
- 下一次由用户实测：进入自定义战斗后确认灰袍指挥官位于第一位、原生第二名指挥官仍在、灰袍五级兵种与
  装备都可选并能出生。测试完成后只读取最新 `rgl_log` 中的上述诊断行、四份 XML 打开记录和任何异常。

## 2026-08-29 自定义战斗完整领主数据的技能集注册补全

- 用户按上一候选实测后立即弹错。最新 `rgl_log_7824.txt` 已读取到完整灰袍文件：`gw_equipment_sets.xml`、
  `spnpccharacters.xml`、`sphpcustombattle.xml`、`spspecialcharacters.xml` 均打开成功；随后在
  `NavalCustomBattleScreen::HandleInitialize` 前记录 `Null object reference found with ID:
  spc_mounted_archery_skills`，watchdog `7824` 确认发生原生崩溃。没有新的托管异常文本。
- 只读检查原版 `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\sandbox_skill_sets.xml`
  确认六个 `spc_knight_skills`、`spc_phalanx_skills`、`spc_mounted_archery_skills`、
  `spc_quartermaster_skills`、`spc_politician_skills`、`spc_diplomat_skills` 都存在；原版
  `SandBox/SubModule.xml` 却只将 `sandbox_skill_sets` 注册到 `Campaign` 与 `CampaignStoryMode`，所以
  放开 `spspecialcharacters` 到 CustomGame 后出现空引用是注册闭环缺口，不是灰袍领主装备本身的问题。
- 已在 GreyWarden `SubModule.xml` 添加同一原版 `SkillSets path="sandbox_skill_sets"` 的 `CustomGame` 注册。
  这不会复制或修改原版技能数值，只让原版模板在 CustomGame 可用；灰袍兵种、六名领主、灰袍装备表和首位专属
  指挥官仍全部保持 CustomGame 生效。
- 第二次日志证明引擎不会跨模块解析这个路径：`sandbox_skill_sets.xml` 没有在 GreyWarden 的 CustomGame
  加载记录中出现。因此改为新增 `ModuleData/gwp_customgame_skill_sets.xml`，逐项镜像原版六个模板的技能值，并将
  GreyWarden 节点改为 `SkillSets path="gwp_customgame_skill_sets"`。该文件只在 CustomGame 注册，避免 Campaign
  对象重复；文件 SHA-256 为 `78A9B3F4DCCC95CFED2D0C5FFB4E20333455DD79467A062E303157015172820D`。
- 修复后 Release 构建为 `0` errors、`0` warnings；客户端和编辑器 DLL 均为程序集 `1.4.11.0`、
  `794624` 字节、SHA-256 `C79BD7DE4E7A1D7B091ABDFEE9C03B3A3DDAFD6CFCDD8CDDADED915D2FDA25B7`。
  仓库/live 可部署文件缺失 `0`、哈希差异 `0`，Bannerlord 进程数为 `0`。
- 本轮仍未启动游戏；下一次只由用户实测。应先确认能进入自定义战斗，再确认灰袍指挥官第一位、原生指挥官未被替换、
  灰袍兵种和装备可选。若进入成功，读取 `rgl_log` 确认 `spc_mounted_archery_skills` 空引用消失。

## 2026-08-29 自定义战斗灰袍士兵消失：恢复兵种 XML 的 CustomGame 注册

- 本节是随即被用户纠正的中间候选，仅保留为失败方案记录；“继续隔离战役领主和装备”的结论已由上一节撤销。
- 用户反馈自定义战斗中灰袍士兵消失，并指出是 `SubModule` 未注册。只读复核最近一次现有日志
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_45340.txt`：CustomGame 初始化阶段记录了原版
  `SandBoxCore/ModuleData/spnpccharacters.xml`，随后读取 GreyWarden 的 `sphpcustombattle.xml`，但没有读取
  GreyWarden 的 `spnpccharacters.xml`。仓库配置也确认该 `NPCCharacters` 节点缺少 `CustomGame`。
- 已在 `_Module/SubModule.xml` 的 `spnpccharacters` 节点补回 `<GameType value="CustomGame"/>`。因此
  `gwnewrecruit`、`gwrecruit`、`gwheavyinfantry`、`gwarcher` 和 `gwknight` 会在自定义战斗对象注册阶段存在；
  `spspecialcharacters` 与 `gw_equipment_sets` 仍不注册 CustomGame，战役专用领主和装备不会重新污染自定义战斗。
- 这是 XML 注册修复，没有启动游戏、没有运行 build，也没有替换当前已部署的 1.4.11 diagnostics DLL。四个相关 XML
  均解析通过；确认 `Bannerlord.Native`、`Bannerlord` 和 `Bannerlord.Launcher` 进程数为 `0`。
- 已将 `SubModule.xml`、中英文 README 镜像到
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`。三份文件逐一 SHA-256 一致：
  `SubModule.xml=205B073FBFB651E8C3FDF514E667DD43DBA58A650A4668B0B9D18B229EAF064E`、
  `README.md=4BAB0489512A320D52E493E3E481E2428CA34B11000649A851930BF969F9E9CB`、
  `README_EN.md=23795BEC2518195A18E6ED259435493ED9DEF791B53B95A11A9D899291B49DF5`。
- 后续由用户实机验证：进入自定义战斗选择灰袍指挥官，确认灰袍士兵列表和战场出生；再在战役验证弓箭手的弓箭/双刀切换。
  维护流程只读取用户测试后产生的监控与日志，不代替用户启动游戏。

## 2026-08-29 自定义战斗崩溃：隔离双刀兵种数据

- 本节记录的 CustomGame 隔离是导致灰袍士兵消失的失败方案，已由“恢复完整灰袍数据与首位专属指挥官”撤销。
- 用户将双刀并入 `gwarcher` 后再次进入自定义战斗立即报错退出。当前没有生成新的托管异常或有效
  崩溃文本；旧双刀诊断日志只对应 2026-08-28 的旧 `gwdualbladeguard` 测试，不能拿来冒充本次
  崩溃证据。
- 静态结构确认 `spnpccharacters.xml`、`spspecialcharacters.xml` 和 `gw_equipment_sets.xml` 原先
  都注册到 `CustomGame`。这会让自定义战斗初始化阶段读取战役兵种/领主装备，其中包含双刀装备和
  专用数据；自定义战斗实际需要的指挥官仍在独立的 `sphpcustombattle.xml` 中。
- 已将上述三个战役数据节点的 `CustomGame` 注册移除，保留 `Campaign`、`CampaignStoryMode` 和
  `EditorGame`；`sphpcustombattle`、双刀物品定义和战役运行路径不变。该改动只隔离 CustomGame
  加载，不关闭 AI 双刀，也不修改击倒概率或双刀动作逻辑。
- 四个相关 XML 解析通过，仓库 `_Module` 与 live 的 `36` 个部署文件缺失 `0`、哈希差异 `0`。
  实机客户端与编辑器 DLL 均恢复为当前诊断版 `792064` 字节，SHA-256
  `5BC924339689B57B9930EF71BCD7D24B9FFF5EAE13074EE599BBD3F6D17FD76C`；README 已同步，中文
  `5BF2CE2E9713D20C43E8CD7FEEDEC33D2DD4D461D4B0DA521A6D58DB5DB667FF`、英文
  `C1A5815417416556DBFB1FF4EC4D83E15C586DED2CF51735BA1B66572429258A`。未制作正式 ZIP。
- 下一步只需完全退出并重启游戏，先验证自定义战斗能否进入，再验证战役中弓箭手能否正常切换弓箭和
  双刀。若自定义战斗仍崩溃，下一隔离点应是 `Items`/动作资源的 `CustomGame` 注册，而不是继续
  修改 AI 或击倒代码。

## 2026-08-29 Bannerlord 1.5.2 beta compatibility repair

- 用户将 Bannerlord 升级到本机测试版 `v1.5.2` 后，GreyWarden 在启动器中被标记为不兼容。直接以当前游戏目录程序集编译复现了唯一阻断错误：`AgentApplyDamageModel.CalculatePassiveAttackDamage` 已从
  `BasicCharacterObject, in AttackCollisionData, float` 改为
  `in AttackInformation, in AttackCollisionData, float`；旧覆盖方法因此同时触发 `CS0115` 和抽象成员未实现的 `CS0534`。
- `GwpAgentApplyDamageModel.CalculatePassiveAttackDamage` 已改用 1.5.2 的 `in AttackInformation` 签名，并将完整结构按 `in` 原样转发给活动原生伤害模型，未改变 GreyWarden 的伤害或击倒规则。该接口中的 `AttackerAgentCharacter` 保留在结构内，但本转发路径不额外读取它。
- 编译项目与 `D:\steam\steamapps\common\Mount & Blade II Bannerlord` 当前 `Win64_Shipping_Client` 程序集对齐；Release `-t:Rebuild --no-restore` 成功，`0` errors、`44` 条既有 nullable warnings。产物 DLL 为 `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden\bin\Win64_Shipping_Client\GreyWardenPolicePurity.dll`。
- 模组内部版本推进为 `1.4.11`（玩家版本 `v1.4-r11` 开发中），仅用于模组修订标识，不声明启动器侧 Bannerlord 版本依赖。中英文 README 当前开发版说明已更新为 Bannerlord 1.5.2 测试版，并保留最新两条玩家发布记录（r11、r10）。
- 本轮构建会自动同步正常客户端运行文件和编辑器 DLL 到 live 模块；完成后必须再次执行逐文件 SHA-256 核对，确认仓库 `_Module`（排除编辑器专用 `Assets`、`AssetSources`、`RuntimeDataCache`）与 `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden` 完全一致。正式玩家包尚未制作，不能用玩家 DLL 覆盖 diagnostics-enabled live DLL。
- 用 `Bannerlord.Native.exe /continuegame` 对当前 `Build Version: 120933`（对应 1.5.2 beta）做启动验收，日志为
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_9148.txt`。日志到达 `Module Initialize end`、
  `GauntletInitialScreen::HandleInitialize/HandleActivate`；`GreyWarden could not be loaded correctly`、
  `dependency conflict` 和 `Loader Exceptions` 均为 `0`。退出前仅见原版 `TaleWorlds.PSAI.XmlSerializers.dll: Invalid Image`
  与联网服务解析错误，未指向 GreyWarden，故不作为本修复回归。
- 最终部署核对：仓库 `_Module` 的 `36` 个可部署文件与 live 缺失 `0`、哈希差异 `0`；客户端诊断 DLL 大小
  `792576` 字节，SHA-256 为 `5A998E8E7CDB0E9AC545C17D66F51ECB32B8E8A825CC944BA4EEB015C219D52E`。
- 用户随后反馈仍在加载界面弹出旧错误。复核 `rgl_log_11448.txt` 确认启动器加载的是旧的 `1.4.9.0` DLL，
  Loader Exception 仍为旧签名缺失；当时 live DLL 哈希为上一轮 `5BC924...`，而不是本轮 Release 的
  `5A998...`。已明确将 `obj\Release\GreyWardenPolicePurity.dll`（程序集 `1.4.11.0`）复制到客户端和编辑器
  两个 live 目录，二者现在均为 `792576` 字节、`5A998...` 哈希。用户明确要求由本人进行后续实机测试，之后不得由维护流程自动启动 Bannerlord。

## 2026-08-28 负声望未派出纠察队：日志复核与补充诊断

- 用户实测犯罪后认为灰袍声望约为 `-5` 或 `-8`，但没有灰袍纠察队来抓。复核当前
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log` 后，
  该文件本次会话没有任何 `WANTED_STATE_EVALUATED`、`DAILY_PUNISHMENT_CHECK`、纠察队生成或罚款事件；
  最后一条玩家快照仍为 `customReputation=8`、`customWanted=False`、`isPlayerHunted=False`。因此现有证据只
  能证明这份日志没有观察到负声望惩罚流程，不能把“没有派队”归因于 AI 追捕失败。
- 代码中的 `PolicePatrolBehavior.OnDailyTick` 不是即时触发：它先跳过俘虏状态，每次每日 tick 递增计数，
  每两天才读取一次灰袍自定义声望。`-1~-10` 才派纠察队；若处于谈判保护期或地图上已有活跃/返程纠察队，
  也会跳过生成。`<=-11` 则不派纠察队，改为正式玩家案卷流程。这些条件解释了短时间内犯罪后看不到队伍的
  合法可能性，但仍需新的状态快照确认。
- 补充开发诊断：每次自定义声望 `ChangeReputation`/`ResetReputation` 都记录前值、增量/请求值和后值；每次
  每日 tick 与实际惩罚检查记录囚禁状态、计数器、保护期、纠察队数量、自定义通缉状态和玩家案卷状态；
  `-1~-10` 分支明确记录因保护期或已有纠察队而跳过的原因。它们仍只存在于 `#if GWP_DIAGNOSTICS`，不改
  玩法、不安装 Harmony、不写正式玩家包。
- 默认 Debug 重建成功，`0` errors、`43` 条既有 nullable warnings。仓库 `obj/Debug`、live 客户端和 live
  编辑器 DLL 均为 `832000` 字节，SHA-256
  `3C0E1300512AC38922AACBB0203D81714B96362ABD1794838C3A0768ED9EC9CF`；源码 `_Module` 的 36 个运行文件与
  live 目录缺失 `0`、哈希差异 `0`。尚未修改正式玩法、README、版本号或发布包。

## 2026-08-28 玩家靠近劫掠村庄误扣声望 / 罚款后零罚金通缉：复现诊断阶段

- 玩家转述了两个尚未由开发者本人复现的现场问题：靠近一个正在被劫掠的村庄时出现“掉声望”；随后缴清
  罚款，自定义显示中的罚金已经为 `0`，但遇到灰袍领主或队伍仍被当作通缉对象，重复缴纳也无法解除。
  当前不根据转述直接改变玩法，先保留原逻辑供本机复现；本节记录的是代码审计结论与诊断准备，不是已经
  验收的正式修复。
- 第一条反馈存在高度吻合的代码路径：`PlayerBehaviorMonitor.OnVillageBeingRaided` 通过
  `FindPlayerRaidingParty` 判断玩家是否为劫掠者，但旧判断只要求主角队伍的 `TargetSettlement` 等于事件村庄，
  没有要求 `DefaultBehavior` / `ShortTermBehavior` 为 `RaidSettlement`，也没有要求主角队伍正处于该村庄的
  Raid `MapEvent`。玩家点击或靠近一个恰好正在被别人劫掠的村庄时，`TargetSettlement` 可能已经指向该村庄，
  因而有可能被误判并扣除灰袍自定义 `PlayerBehaviorPool.Reputation`。同项目的 NPC 犯罪监听
  `PoliceCrimeMonitorEnhanced.FindRaidingParty` 已经同时检查目标村庄和 `RaidSettlement` 行为，形成明确对照。
- 第二条反馈存在一条能精确产生“罚金 0 但仍有通缉对话”的不一致：纠察队的
  `OnPatrolFineBarterAcceptedConsequence` 会把自定义声望重置为 `0`、恢复和平并遣返纠察队，但不会调用
  `CrimeState.EndPlayerHunt()`；正式灰袍执法对话 `EnforcementDialogCondition` 则以仍存在且处于 Pursuit 的
  玩家案卷任务为入口，并未要求当前声望仍小于 `0`。若旧的 `PLAYER_WANTED` 案卷/任务仍在，下一次遇到承办
  灰袍时就会继续进入通缉对话，而 `Math.Abs(0) * 300` 正好显示为 `0` 罚金。此链路目前仍需实机日志确认，
  不能只凭静态审计认定玩家现场一定经过了纠察队缴款入口。
- 为复现新增的记录只扩展既有 `GwpAiDiagnostics`，没有新建后台脚本、Harmony patch、Campaign Tick、Agent
  或动作监控，也不修改任何状态。仅在原有事件入口主动写入 `PLAYER_JUSTICE` 快照：村庄劫掠事件判定与
  扣分、纠察队罚款对话和结算前后、正式执法罚款对话和结算前后、每日通缉分级检查、玩家案卷删除前后。
  每条同时保存灰袍自定义声望/通缉阈值、原版氏族 renown、`PLAYER_WANTED` 案卷是否存在/开放、承办任务与
  FlowState、灰袍和玩家势力是否交战、受害势力列表，以及主角队伍的目标村庄和当前 AI 行为。村庄事件还
  单独保存距离、目标匹配、主角是否实际处于 Raid MapEvent 及事件村庄 ID。
- 所有新增写入仍严格位于 `#if GWP_DIAGNOSTICS` 的开发实现内；`GwpDiagnosticsEnabled=false` 分支只增加同名
  空方法，正式玩家 DLL 不会创建或写入该日志。本轮没有更新玩家 README，因为没有改变玩家可见机制，
  也没有制作 ZIP、改版本号、提交或发布。
- 默认 Debug `dotnet build ... -t:Rebuild --no-restore` 构建为 `0` errors、`43` 条既有 nullable warnings。
  仓库 `obj/Debug`、live 客户端和 live 编辑器 DLL 均为 `830464` 字节，SHA-256
  `B563E619075F4A7766108025B2B315F0FF4B79794CE7225BC1CC0874E5963851`。复现后读取
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`，筛选
  `PLAYER_JUSTICE` 即可判断扣除的是灰袍声望还是原版 renown，并确认缴款后残留的是声望、案卷、任务、战争
  状态还是地图纠察队。
- 另在独立临时目录
  `C:\Users\lucif\AppData\Local\Temp\gwp-diagnostics-off-check` 使用
  `GwpDiagnosticsEnabled=false`、`DeployToLiveModule=false` 完成 Release 重建，产物为 `768000` 字节，SHA-256
  `A42336C0D87796CED4619A383590C361983540D6E39538E7E6342FC90DA0A7BA`。ILSpy 反编译确认
  `WritePlayerJusticeState(string,string)` 方法体为空，且类型中不存在 `File.AppendAllText`、`File.WriteAllText`、
  日志路径或 `PLAYER_JUSTICE` 字符串。独立构建后再次核对 live 客户端、live 编辑器和 `obj/Debug`，三者仍为
  上述 `B563...3851` 的开发诊断 DLL，正式隔离校验没有覆盖本机测试安装。

## 2026-08-28 双刀拾取回归：移除 Agent / MissionEquipment 全局 detour

- 用户实测上一轮地面拾取已经实现：先副手后主手、先主手后副手均能正常完成并进入双刀状态；但百科士兵
  主模型和自定义战斗人物预览同时复现此前“不显示或姿态异常”的故障。故拾取事务本身正确，回归只来自为
  实现它新增的 Harmony 安装范围，不能通过放弃拾取功能回退。
- 最新 `rgl_log_44464.txt` 能正常进入 `CustomBattleScreen::HandleActivate`、加载
  `inventory_character_scene`、进入两次真实战斗并分别渲染两把双刀；没有新的双刀动作缺失或托管异常。
  与用户确认正常的上一 DLL 相比，唯一结构变化是新增
  `Agent.OnItemPickup` prefix 与 `MissionEquipment.SelectWeaponPickUpSlot` postfix。此前 A/B 已证明全局
  `Agent`/`MissionWeapon` detour 即使不在 tableau 内命中，也会污染人物展示；因此本轮不再浪费一次实测逐个
  开关，而是同时移除两个不必要的公共方法补丁。
- 反编译 Bannerlord 1.4.8 原版确认真实地面拾取的唯一常规上游是
  `SpawnedItemEntity.OnUseStopped(Agent,bool,int)`：交互成功后它调用一次 `Agent.OnItemPickup`，随后根据
  `removeWeapon` 设置 `_readyToBeDeleted`、停止物理并禁用地面实体。新实现只对
  `SpawnedItemEntity.OnUseStopped` 安装一个 transpiler，把这一处调用替换为
  `GwpDualBladeGroundPickup.RoutePickup`；非双刀立即回调完全未修改的原版 `Agent.OnItemPickup`，双刀继续执行
  已验收的固定 `Weapon0/Weapon1`、第一件不拔出、凑齐后先切动作集再按副手/主手顺序拔刀。原方法其余 IL、
  地面实体删除、UseObject 清理、AgentComponent/AI/Mission 通知均保留。
- 最终 DLL 不再包含 `GwpDualBladePickupSlotPatch`，`GwpDualBladeGroundPickupPatch` 的 Harmony 目标只剩
  `SpawnedItemEntity.OnUseStopped`；没有 `Agent.OnItemPickup`、`MissionEquipment.SelectWeaponPickUpSlot`、
  `TryToWieldWeaponInSlot`、Mission Tick、监控或 tableau/ViewModel 补丁。默认 Debug
  `-t:Rebuild --no-restore` 为 `0` errors、`43` 条既有 nullable warnings；仓库 `obj/Debug`、live 客户端与
  live 编辑器 DLL 均为 `826368` 字节，SHA-256
  `DED004F03D4FEA95DBA2F346682516472F061B7DFD4595A820C3989C965A1926`。ILSpy 确认目标、transpiler 与
  RoutePickup 已进入产物，仓库 `_Module` 的 `36` 个运行文件与 live 相比缺失 `0`、哈希差异 `0`，`30` 个
  XML/XSLT/mbproj 解析失败 `0`。中英文 README 已单独镜像到 live，仓库/live SHA-256 分别为
  `FAA741E2BA5C2258EC6310D339A1CA0E8020FA6747FE3935ED4949FA72AD5677` 与
  `B9781A1AAFDA6822CA22992D0435D4CD5D0D40B0C5E51E75E317FE38E5DFA790`。下一次实测优先检查百科士兵和
  自定义战斗双方英雄预览，再抽查任意一种双刀拾取顺序仍正常；未制作正式 ZIP。

## 2026-08-28 双刀地面拾取：固定槽位与完整套装后切换动作

- 用户确认百科、自定义战斗预览、玩家双刀与 AI 双刀均已恢复正常，当前唯一已知故障是从地面拾取
  `gwdualbladeoffhand` / `gwdualblademainhand` 会触发原生报错退出；无论先拾主手或先拾副手都可能复现，
  但体感上副手更容易成为直接触发点。此次不改已经验收的 AI 生成同步、击倒或展示隔离方案。
- 最新复现日志 `rgl_log_22464.txt` 在退出前依次记录
  `Render Requested: gwdualblademainhand`、`Render Requested: gwdualbladeoffhand`，随后明确报告基础动作集
  `as_human_female_warrior` 缺少 `act_gwd_quick_release_thrust_1h`、`act_gwd_release_thrust_1h`、
  `act_gwd_quick_release_slashleft_1h` 与双刀持盾移动动作；没有托管异常。反编译原版
  `Agent.OnItemPickup` 证明拾取会在 `EquipWeaponFromSpawnedItemEntity` 后立即调用
  `TryToWieldWeaponInSlot(...InstantAfterPickUp...)`。因此根因不是拾取实体本身，而是为修复展示污染而把双刀动作隔离到
  `as_gwp_dual_warrior` 后，原版动态拾取仍在动作集切换前尝试拔出专用武器。
- ROT 1.3.15.3 的 `DualWieldingPatches` 只在 `SPInventoryVM.RefreshInformationValues` 校验第一格必须是
  `dual_shield`、第二格必须是 `dual_shield_thrust`，并提示半套或错槽；ROT DLL 中没有
  `Agent.OnItemPickup`、`CanQuickPickUp`、`SelectWeaponPickUpSlot` 或
  `EquipWeaponFromSpawnedItemEntity` 补丁。其 XML 同样只通过 `HeldInOffHand` 与两种 usage 表达双刀。故 ROT
  不能直接提供安全地面拾取实现，但它证明固定 `Weapon0/Weapon1` 是必须保持的不变量。
- 新增 `GwpDualBladePickupSlotPatch`，只对这两个物品覆写原版
  `MissionEquipment.SelectWeaponPickUpSlot` 的结果：副手固定 `WeaponItemBeginSlot`，主手固定 `Weapon1`。
  `GwpDualBladeGroundPickupPatch` 只接管这两个物品的真实 `Agent.OnItemPickup`：沿用原版丢弃目标格旧物、
  `EquipWeaponFromSpawnedItemEntity`、AgentComponent 通知、AI 拾取完成通知与 Mission 拾取事件；第一件只装备不拔出，
  第二件补齐后先为该真实 Mission Agent 分配 `as_gwp_dual_warrior`，AI 再复用既有局部副手资格同步，最后按原版
  `WieldInitialWeapons` 的顺序先拔副手、再拔主手。其他任何地面物品继续执行原版方法。
- `GwpDualBladeActionSetPatch` 仅抽出可复用的 `TryApplyActionSet(Agent)`；SpawnAgent 行为和完整双刀检查不变。
  没有恢复全局 `Agent`/`MissionWeapon` 数据补丁，没有 Mission Tick、监控、持续重挂、额外物品、人物数据或
  tableau/ViewModel 修改。默认 Debug `-t:Rebuild --no-restore` 构建为 `0` errors、`43` 条既有 nullable
  warnings；仓库 `obj/Debug`、live 客户端与 live 编辑器 DLL 均为 `823808` 字节，SHA-256
  `A0F7A7B4271BD9EF7E4AA330CCF444ED41445D662E40E979951171186C9B924E`。ILSpy 确认新 DLL 包含
  `GwpDualBladePickupSlotPatch` 与 `GwpDualBladeGroundPickupPatch`，四个已删除的全局 AI 类型仍全部不存在。
  仓库 `_Module` 的 `36` 个正常客户端文件与 live 相比缺失 `0`、哈希差异 `0`；`30` 个
  XML/XSLT/mbproj 解析失败 `0`。中英文 README 的仓库/live SHA-256 分别为
  `5A6881F0494580EA3BAE0540503F34454BC518B7ECA7FA0651B2B5B65A0EE625` 与
  `E4D49C27097518F524843CE1F1147973305252E64FE70CD8A54CA27B85CF653B`。下一次实测只需在真实战场分别验证
  “主手后副手”和“副手后主手”两种拾取顺序；未制作正式 ZIP。

## 2026-08-28 AI 双刀直接重构：移除四个全局补丁

- 用户要求停止继续逐层开关测试，直接修复已经缩小到四个脚本内的结构问题。现有正反对照已经证明：玩家
  专用 `Mission.SpawnAgent` 动作分配不会破坏展示，而对 `Agent.EnforceShieldUsage` 的全局 prefix 单独存在
  即可复现百科和自定义战斗人物异常；关闭它但恢复其余全局 AI 链后故障仍存在，说明继续给
  `Agent`/`MissionWeapon` 公共方法安装 detour 不是可接受的最终架构。
- 已删除 `GwpDualBladeAiShieldEnforcementPatch`、`GwpDualBladeAiEquipmentSyncScopePatch`、
  `GwpDualBladeAiWeaponDataPatch`、`GwpDualBladeAiWeaponStatsPatch` 四个 Harmony 类型。最终 DLL 的类型表只保留
  `GwpDualBladeAiSpawnSync` 普通帮助类与既有 `GwpDualBladeActionSetPatch`；ILSpy 逐个查询四个旧类型均返回
  “type definition not found”。因此运行时不再 patch `Agent.EnforceShieldUsage`、
  `Agent.EquipItemsFromSpawnEquipment`、`MissionWeapon.GetWeaponData` 或
  `MissionWeapon.GetWeaponStatsData`，百科和自定义战斗的 `AgentVisuals`/tableau 不再可能经过 AI 双刀代码。
- 新实现继续复用已经证明不影响玩家专用阶段展示的 `Mission.SpawnAgent` postfix。完整双刀 Agent 完成真实
  Mission 构建和初始装备后，玩家只切换到 `as_gwp_dual_warrior`；AI 先由
  `GwpDualBladeAiSpawnSync.TryApply` 读取该 Agent 自己的副手 `MissionWeapon`，在局部
  `WeaponData/WeaponStatsData` 副本中加入耐久、`bo_mace_a` shape、`bo_wlarge_shield` collision shape 与
  `HasHitPoints | CanBlockRanged`，再调用 Agent 私有原生 `WeaponEquipped` 写回同一个副手槽。
- 写回时使用 `agent.GetWeaponEntityFromEquipmentSlot(WeaponItemBeginSlot)` 获得现有副手实体，传入
  `removeOldWeaponFromScene=false` 和 `isWieldedOnSpawn=true`；不创建第二个物品、不修改全局 ItemObject、不替换
  装备栏、不发强制拔刀/收刀命令、不做 Tick、持续重挂或监控。局部 WeaponData 的托管资源在调用后立即
  `DeinitializeManagedPointers()`。反射预检已在实机依赖环境中确认目标为
  `TaleWorlds.MountAndBlade.Agent.WeaponEquipped`，八个参数精确匹配
  `EquipmentIndex, WeaponData&, WeaponStatsData[], WeaponData&, WeaponStatsData[], WeakGameEntity, bool, bool`；
  静态句柄不为 null。
- 默认 Debug 开发重建成功，`0` errors、`43` 条既有 nullable warnings。仓库 `obj/Debug`、live 客户端与
  live 编辑器 DLL 均为 `822784` 字节，SHA-256
  `5C4EB9A6ED96C0325F9912603DB62AEB50FB1B476E6472F4ECE05ADA5B527CCE`。ILSpy 确认唯一 Harmony 入口为
  `Mission.SpawnAgent(AgentBuildData,bool)` postfix，且 AI 局部同步发生在完整双刀检查之后。中文 README
  SHA-256 为 `6A2F0985A51FBE451853B3C1E944CA59C80912217DDC22CD1B77D21FD427C5FA`，英文为
  `4EEC9414EE3142D71DC34FAF88EF33EAAC09030BFCB6EC93C8DFAD6A8E3A46E2`，均已同步 live。
- 下一次只需一次联合验收：完全退出并重启后检查百科士兵主模型、自定义战斗英雄预览、AI 左手剑持续出鞘、
  AI 攻击和四向格挡、玩家双刀与击倒。展示路径现在与 AI 代码结构性隔离；若仅 AI 仍收起副手，后续只调整
  `Mission.SpawnAgent` 内这一名 Agent 的局部原生写入，不得重新引入四个全局补丁。未制作正式 ZIP。

## 2026-08-28 AI 双刀第二污染源 A/B：仅启用 EquipmentSyncScope

- 用户实测关闭已确认有问题的 `EnforceShieldUsage` prefix、但恢复装备同步、两个 `MissionWeapon` 数据层和
  AI 动作分配的修复候选后，百科士兵主模型与自定义战斗英雄预览仍然故障。这证明
  `EnforceShieldUsage` 是一个确定污染源，但不是唯一污染源；其余四层中至少还有第二个会破坏展示的补丁。
  上一记录中“永久关闭该层后即可修复展示”的候选结论已被实测否定，不能继续宣称问题已经解决。
- 当前按调用依赖顺序重新隔离：`ShieldEnforcementEnabled=false`、
  `EquipmentSyncScopeEnabled=true`、`WeaponDataEnabled=false`、`WeaponStatsEnabled=false`、
  `AiActionSetEnabled=false`。因此本轮只安装 `Agent.EquipItemsFromSpawnEquipment` 的 Prefix/Finalizer：进入方法
  时为完整双刀 AI 设置线程局部 `_activeAgent`，离开或异常时恢复旧值；没有任何消费者读取该值，也不修改
  `WeaponData`、`WeaponStatsData` 或动作集。玩家双刀保留，AI 双刀按设计仍暂停。
- 这一单层测试专门回答 Prefix/Finalizer 的全局 detour 是否本身影响展示管线。若两个界面再次故障，第二污染
  源即锁定为 `EquipItemsFromSpawnEquipment` scope；若恢复正常，则该 scope 被排除，下一轮在其基础上只加入
  `GetWeaponData`，因为后两个数据 postfix 没有 ActiveAgent scope 时不会进入有效分支。
- 默认 Debug 开发重建成功，`0` errors、`43` 条既有 nullable warnings。仓库 `obj/Debug`、live 客户端与
  live 编辑器 DLL 均为 `822784` 字节，SHA-256
  `9F677DD85E8836628B1AF74AA86B83CF1C318A1BAAAB66A05301BCBE25237EAA`。ILSpy 对 live DLL 确认只有
  `EquipItemsFromSpawnEquipment.Prepare()` 返回 `true`；`EnforceShieldUsage`、`GetWeaponData`、
  `GetWeaponStatsData` 三个 `Prepare()` 均返回 `false`，动作集 postfix 只接受 `!IsAIControlled`。中文 README
  SHA-256 为 `12E64819A5E89C3626AB10194E3B7C243FEBEDBDB9DAC883D0C2F9FDBBAADFA6`，英文为
  `C2FFF56D7D4D20E274E32FC60969FF0833C3487D1737ABD44068664829D90708`，均已同步 live。
- 下一轮只需完全退出并重启，验证百科士兵主模型与自定义战斗双方英雄预览；不需要测试 AI 双刀。未制作
  正式 ZIP。

## 2026-08-28 AI 双刀展示修复候选：永久移除 EnforceShieldUsage detour

- 用户实测上一轮“只恢复 `Agent.EnforceShieldUsage` prefix、其余 AI 双刀层仍关闭”的单层版本后，百科士兵
  主模型和自定义战斗英雄预览同时再次出现人物消失/姿态异常。该结果与完整关闭补丁组时两个界面全部恢复
  构成直接正反对照，证明安装 `GwpDualBladeAiShieldEnforcementPatch` 本身足以触发展示故障。这个全局
  Agent detour 是已证实根因，不能继续保留在最终 AI 双刀实现中。
- 当前修复候选设置为：`ShieldEnforcementEnabled=false`，永久不安装
  `Agent.EnforceShieldUsage` prefix；恢复 `EquipmentSyncScopeEnabled=true`、`WeaponDataEnabled=true`、
  `WeaponStatsEnabled=true` 和 `AiActionSetEnabled=true`。因此完整双刀 AI 会在一次
  `Agent.EquipItemsFromSpawnEquipment()` 作用域内获得副手耐久、`bo_mace_a` shape、
  `bo_wlarge_shield` collision shape、`HasHitPoints | CanBlockRanged`，并在 Mission 生成后切换到
  `as_gwp_dual_warrior`；普通物品、玩家和 Mission 外的调用仍因线程局部 ActiveAgent 为空而不改结果。
- 这一步不是重新启用全局盾牌规则旁路，也没有加入监控、Tick、强制拔刀、实体生成、装备重挂、人物数据或
  tableau 补丁。若 AI 仅凭一次性 Native 数据资格即可保持副手，则同时满足 AI 双持与正常展示；若 AI 仍被
  原版阵型逻辑收起副手，下一步必须寻找不 patch `Agent.EnforceShieldUsage` 的局部替代入口，而不能恢复已证实
  破坏展示的旧 prefix。
- 默认 Debug 开发重建成功，`0` errors、`43` 条既有 nullable warnings。仓库 `obj/Debug`、live 客户端和
  live 编辑器 DLL 均为 `822784` 字节，SHA-256
  `C53AA23994A3E684FEB85C841B291D8FC971DA94B08F699751D21D730B412BA0`。ILSpy 对 live DLL 确认
  `EnforceShieldUsage.Prepare()` 返回 `false`，装备同步、`GetWeaponData`、`GetWeaponStatsData` 三个
  `Prepare()` 均返回 `true`，Mission SpawnAgent postfix 对完整双刀玩家与 AI 都会分配专用动作。中文 README
  SHA-256 为 `BE8C6571B49B4DB3DA588217B7E7BB5E7B0E721285310E0DEB89CF1654CEFBDB`，英文为
  `0864585355C147E261B51D492DAC443400BA4DB85DA85B83817AF3AC50F15522`，已同步 live。
- 下一轮必须完全退出并重启后同时验证：百科士兵主模型、自定义战斗双方英雄预览、双刃卫士/双刀领主是否
  持续拔出左手剑、AI 攻击和四向格挡，以及玩家双刀。若展示正常且 AI 正常，修复完成；若展示正常但 AI
  收起副手，则 `EnforceShieldUsage` 确实同时承担了必要资格和展示污染，需要重新设计局部旁路；若展示再次
  失败，则说明已证实的 `EnforceShieldUsage` 之外，剩余三个全局 Harmony 安装层中还有第二个污染源，应再
  对 `EquipItemsFromSpawnEquipment` 与两个 `MissionWeapon` postfix 分组 A/B。未制作正式 ZIP。

## 2026-08-28 AI 双刀逐层 A/B：仅恢复 EnforceShieldUsage

- 用户实测完整关闭 AI 双刀补丁组和 AI 动作分配的版本后，百科士兵主模型与自定义战斗双方英雄预览
  全部恢复正常。用户未测试该版 AI 双刀，按设计它也不应正常工作。这个阳性 A/B 已证明展示故障位于
  “AI 双刀 Harmony 安装组或 AI 动作分配”之内，排除副手 `WoodenParry`、动作集物化、战役英雄
  `CustomGame` 注册以及此前所有预览 ViewModel 候选作为唯一根因。
- 为保持一次只恢复一层，把原统一开关拆成五个常量：
  `ShieldEnforcementEnabled=true`，`EquipmentSyncScopeEnabled=false`，
  `WeaponDataEnabled=false`，`WeaponStatsEnabled=false`，`AiActionSetEnabled=false`。本轮只有
  `GwpDualBladeAiShieldEnforcementPatch` 的 `[HarmonyPrepare]` 返回 `true`，因此只重新安装
  `Agent.EnforceShieldUsage` prefix；该 prefix 仍只会对完整双刀且 `IsAIControlled` 的 Agent 跳过盾牌阵型
  约束。装备同步作用域、两个全局 `MissionWeapon` postfix 和 AI 专用动作分配全部继续关闭。
- 玩家双刀继续由 `GwpDualBladeActionSetPatch` 获取专用动作。该 postfix 现在按
  `agent.IsAIControlled && !AiActionSetEnabled` 排除 AI，便于后续只修改一个常量恢复动作层；没有删除兵种、
  领主、装备或资源，也没有新增监控、Tick、强制拔刀、重挂、人物数据或 tableau 补丁。本轮 AI 仍不应
  被视为双刀功能恢复，只需验证两个展示界面。
- 默认 Debug 开发重建成功，`0` errors、`43` 条既有 nullable warnings。仓库 `obj/Debug`、live 客户端与
  live 编辑器 DLL 均为 `822784` 字节，SHA-256
  `329A8596D86D3E20FF5C6C5B2BA4C2BD168200B2A48093CD912526177817EAC7`。ILSpy 对 live DLL 确认
  `EnforceShieldUsage.Prepare()` 返回 `true`，其余三个 AI `Prepare()` 返回 `false`，动作集 postfix 仍只接受
  `!IsAIControlled`。中文 README SHA-256 为
  `30E738A4C8872E53B3992A3BF36F9D8B330C628C251F084AFB867397459ED3B0`，英文为
  `B54CE0C2DB662E1C213DE116CD495FBF9A6CCD0405BF7A27A4AEC8D44CB0B8BB`，均已同步 live。
- 下一轮只需完全退出并重启游戏，查看百科任意士兵主模型和自定义战斗双方英雄预览。若仍正常，
  `EnforceShieldUsage` 补丁被排除，下一层启用 `EquipItemsFromSpawnEquipment` scope；若故障立即复现，则根因
  已锁定为 `EnforceShieldUsage` detour 本身，后续应把它永久替换为不全局 patch Agent 的实现，而不是继续
  启用后面三层。未制作正式 ZIP。

## 2026-08-28 玩家双刀保留 / AI 双刀补丁关闭 A/B

- 用户实测上一轮移除副手锻造片 `WoodenParry` 后，百科士兵主展示和自定义战斗英雄预览仍然无法正确
  加载人体/护甲，或出现异常姿态；因此 `WoodenParry` 候选已被排除。本轮先精确恢复该标志，仓库与 live
  的 `gwp_crafting_pieces.xml` SHA-256 均回到
  `8BE1EAD5BDFC8C08DF8682E7BEB8880D364604DFB8AE57535CAE48A29C5B2B65`，动作集和角色注册继续保持
  上一轮已经恢复的基线，不再叠加 XML 实验。
- 按用户要求进入具有直接判定力的代码 A/B：保留双刀物品、双刃卫士、双刀领主、装备表、动画资源、玩家
  双刀碰撞和击倒，只暂停“让 AI 能长期使用非盾牌副手”的运行时补丁。新增统一
  `GwpDualBladeAiAbSwitch.Enabled = false`；四个 AI 专用 Harmony 类
  `GwpDualBladeAiShieldEnforcementPatch`、`GwpDualBladeAiEquipmentSyncScopePatch`、
  `GwpDualBladeAiWeaponDataPatch`、`GwpDualBladeAiWeaponStatsPatch` 均通过 `[HarmonyPrepare]` 返回该开关。
  Harmony 在准备阶段得到 `false` 后不会为
  `Agent.EnforceShieldUsage`、`Agent.EquipItemsFromSpawnEquipment`、`MissionWeapon.GetWeaponData` 或
  `MissionWeapon.GetWeaponStatsData` 安装这些 detour，而不是在补丁命中后再做空操作。
- `GwpDualBladeActionSetPatch` 同时收紧为只给 `IsAIControlled == false` 且完整装备双刀的 Mission Agent
  分配 `as_gwp_dual_warrior`。这恢复“玩家可双刀、AI 不使用双刀”的历史功能边界；AI 兵种和领主的数据
  没有删除，因此 A/B 结束后只需把统一开关改回 `true` 并撤销该玩家条件即可恢复原功能。没有加入监控、
  Mission Tick、强制拔刀、实体生成、装备重挂、人物数据或预览 ViewModel 补丁。
- 使用默认 Debug 开发配置执行
  `dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj -t:Rebuild --no-restore`，结果为
  `0` errors、`43` 条既有 nullable warnings，并由项目目标同步到 live 客户端与编辑器目录。三份 DLL 均为
  `822784` 字节，SHA-256
  `76B115896DB94297F5057CE5288478BF2D7320BC9B10CB301BA8AFCB6E2F8363`。ILSpy 对 live DLL 逐类
  反编译确认四个 `Prepare()` 均直接 `return false`，动作集 postfix 明确要求
  `__result != null && !__result.IsAIControlled`。原稳定 DLL 仍保存在
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\stable-custombattle-baseline-20260828-1117\GreyWardenPolicePurity.dll`，
  SHA-256 为 `0B435AF0A76A678100F639599AFE46308DE655C27EBAB842A5387E8FD5CBF3DE`。
- 仓库 `_Module` 的 `36` 个正常客户端文件与 live 缺失 `0`、哈希差异 `0`；`30` 个
  XML/XSLT/mbproj 解析失败 `0`，`git diff --check` 通过。中文 README SHA-256 为
  `45CC1731CED64723082FA61CC05C2B28E6EBD46052889241BF7C710771721D32`，英文为
  `9E949363B822C815CECDDA8162A1596EBCE47A5C9A38AA1B842695D7D07618EA`，仓库与 live 一致；未制作
  正式 ZIP。
- 本轮实测顺序：完全退出后重新启动，先看百科任意士兵主模型，再看自定义战斗双方英雄预览，最后只验证
  玩家双刀。预期 AI 双刃卫士和领主本轮不会维持左手剑或获得专用动作，这是有意的 A/B 状态。若两个
  tableau 恢复，根因已锁定在这四个 AI Harmony 安装组或 AI 动作分配；下一轮保持界面正常基线，按
  `EnforceShieldUsage -> EquipItemsFromSpawnEquipment scope -> GetWeaponData -> GetWeaponStatsData -> AI action set`
  顺序一次启用一层。若仍不恢复，则可证明这些 AI detour 本身不是根因，必须转向 AI 开发同时新增的全局
  action type、usage set、movement set 或 AssetPackage 资源做玩家阶段/AI 阶段差分。

## 2026-08-28 百科/自定义人物展示：恢复基线并隔离副手 WoodenParry

- 用户复测物化后的自包含 `as_gwp_dual_warrior` 后，百科士兵主展示区仍不显示人体与护甲；因此“空
  `action_sets.xml` 与 XSLT 混合加载是唯一根因”的候选已被实测排除。仓库与 live 已恢复此前精确动作
  基线：空 `action_sets.xml` SHA-256
  `06C5509045556C00081B93395A2E84CD863419F6ACA9FAA7320ACED8B99A1E60`，以及
  `action_sets.xslt` SHA-256
  `1AA305379F827C5562B2FC60A16F55437DA866C4A2AE7DE84577C863F483BFD0`。被排除的物化文件仍可由
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\action-set-materialization-audit-20260828\`
  中的审计产物重建；旧动作基线继续保存在
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\action-set-loading-baseline-20260828\`。
- 从 `spspecialcharacters` 的游戏类型中移除 `CustomGame` 后，用户此前也已确认百科仍出现同类故障；该项
  不是跨界面人物消失的根因。本轮在 `SubModule.xml` 恢复 `CustomGame`，避免把一个无效实验继续混入后续
  A/B。六个技能模板空引用仍是独立数据缺陷，但当前证据不能把它当作人物 tableau 消失原因。
- 按用户提供的时间线重新比较玩家专用阶段与 AI 资格开发记录：隐藏副手锻造片
  `gwp_vlandian_blade_3_dual` 的 `WoodenParry` 是 2026-08-27 为 AI 拔刀资格后来补入的持久物品标志；
  `ForceAttachOffHandPrimaryItemBone` 与 `HeldInOffHand` 在玩家专用实现中已经存在。当前
  `BladeData.body_name` 已是原版剑主体 `bo_sword_one_handed`，该锻造片没有发现其他尚未隔离的 AI 实验字段。
- 本轮唯一的新变量是从该副手锻造片移除 `WoodenParry`，保留两个原有挂接标志。最终成功的 AI 双刀战斗
  资格仍由 `GwpDualBladeAiNativeSyncPatch` 在完整双刀 AI 的一次
  `Agent.EquipItemsFromSpawnEquipment()` 作用域内提供耐久、碰撞体和阻挡统计，因此没有修改 AI 补丁、动作
  分配、人物定义、装备槽、剑模型、剑鞘、击倒或玩家路径。当前推断是：tableau 会在 Mission 外直接读取
  CraftedItem 的持久标志，而 `WoodenParry` 已不再是最终 AI 战斗链的必要条件；该推断尚未经过用户实测，
  不能写成已修复。
- 精确回退本候选只需在 `gwp_crafting_pieces.xml` 的该锻造片 `<Flags>` 中重新加入
  `<Flag name="WoodenParry" type="ItemFlags" />`；修改前文件 SHA-256 为
  `8BE1EAD5BDFC8C08DF8682E7BEB8880D364604DFB8AE57535CAE48A29C5B2B65`，修改后为
  `D63E477BD6DB8D593EC3BA258E831687F161E2AE7B051675F72A7B008B0C5AF7`。
- 本轮没有运行 build；live 客户端与编辑器 DLL 均继续保持用户确认可进入界面的稳定 SHA-256
  `0B435AF0A76A678100F639599AFE46308DE655C27EBAB842A5387E8FD5CBF3DE`。六个改动文件已逐一同步并确认
  source/live 哈希一致；XML/XSLT 解析失败 0，`git diff --check` 通过。中英文 README 已删除未被实测支持的
  “自定义战斗不再加载战役领主”表述，并明确展示故障仍在排查；未制作正式 ZIP。
- 下一次实测必须使用同一轮启动依次检查：百科士兵主模型、自定义战斗双方英雄预览、玩家双刀、双刃卫士或
  双刀领主的拔刀/攻击/四向格挡。若两个 tableau 恢复且 AI 双刀仍正常，`WoodenParry` 即为根因；若展示
  仍不恢复，则立即恢复该单行并进入严格 DLL A/B：保留玩家双刀，临时只停用四个 AI 原生资格 Harmony 补丁
  且动作集仅分配给玩家 Agent，再逐层启用定位。A/B 只用于找出具体补丁，不代表放弃 AI 双刀。

## 2026-08-28 Tableau 全局动作集资源改为自包含专用文件

- 用户在百科士兵页面发现与自定义战斗相同的故障：主展示区的人体与护甲不显示，只剩武器、坐骑或局部装备实体；兵种树缩略图仍正常。百科与自定义战斗使用不同 ViewModel，但都通过 `CharacterTableau` / `AgentVisuals` 构建展示人物，因此该证据排除“仅 CustomBattle 角色列表或角色 XML 导致”的方向，根因必须位于两类 tableau 共同读取的全局人物动作/资源层。
- 审计 `project.mbproj` 及本机已安装动作模组后发现，GreyWarden 把 `ModuleData/action_sets.xml` 注册为 `type="action_set"`，但该文件只有 55 字节，内容是空的 `<action_sets />`；实际 `as_gwp_dual_warrior` 只存在于 `action_sets.xslt`，运行时通过匹配并复制原版 `as_human_warrior` 动态生成。ROT-Content、ROT-Dragon 与 ArtemsCinematicCharges 等同样注册 action-set 资源的本机模组，其 `action_sets.xml` 均包含实际动作集；GreyWarden 是当前唯一注册空动作集文件的模组。
- 这一结构来自 2026-08-28 把双刀动作从全局 `as_human_warrior` 隔离到专用动作集时未完成的迁移：XSLT 已改为生成 `as_gwp_dual_warrior`，但项目资源入口仍指向空文件。战场合并链可以从 XSLT 得到专用动作集，而百科、自定义战斗和其他独立 tableau 场景会直接加载 GreyWarden 项目 action-set 资源；空资源与动态复制原版动作集的组合会让人物动作集/骨架初始化结果依赖加载路径，符合“人体消失但物品实体仍在”的跨界面症状。
- 已用 .NET `XslCompiledTransform` 对当前 Bannerlord 1.4.8 原版 `Native/ModuleData/action_sets.xml` 应用现有 GreyWarden XSLT，再只提取 `as_gwp_dual_warrior`，物化为新的自包含 `ModuleData/action_sets.xml`。成品只含 1 个动作集，骨架为 `human_skeleton`、移动系统为 `bipedal`，完整动作映射 4783 项；相对原版 `as_human_warrior` 新增 84 项，缺失 action type 0、重复 action type 0，不包含或覆盖 `as_human_warrior`。新文件 SHA-256 为 `382D601B2657E8B26CE0CA6BD0131FD21C5B2BD48C18CB57EADAF85B8F9F5FF3`。
- 已从仓库和 live 删除 `ModuleData/action_sets.xslt`，停止运行时动态复制/接触原版人类动作集；`project.mbproj` 继续注册现在非空且只包含专用动作集的 `action_sets.xml`。战场 `GwpDualBladeActionSetPatch` 仍按原逻辑只给完整双刀 Agent 切换到 `as_gwp_dual_warrior`，普通 Agent 与所有 tableau 继续使用 Native 自己的 `as_human_warrior`。
- 旧空文件和 XSLT 已可恢复地备份到绝对目录 `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\action-set-loading-baseline-20260828\`：`action_sets.empty.xml` SHA-256 为 `06C5509045556C00081B93395A2E84CD863419F6ACA9FAA7320ACED8B99A1E60`，`action_sets.xslt` SHA-256 为 `1AA305379F827C5562B2FC60A16F55437DA866C4A2AE7DE84577C863F483BFD0`。若需精确回退，将空 XML 复制回仓库/live 的 `ModuleData/action_sets.xml`，并把备份 XSLT 复制回仓库/live 的 `ModuleData/action_sets.xslt`。
- 本轮没有运行 build，稳定客户端和编辑器 DLL 继续保持用户已确认可进入界面的 `0B435AF0A76A678100F639599AFE46308DE655C27EBAB842A5387E8FD5CBF3DE`。仓库 `_Module` 与 live 当前 35 个正常客户端源文件缺失 0、哈希差异 0，XML/XSLT/mbproj 解析失败 0；中英文 README 与 live 一致。下一次实测同时检查百科士兵页和自定义战斗预览的人体/护甲是否恢复，并在进入战场后确认玩家与 AI 双刀动作仍正常；若两处 tableau 均恢复，根因即为该空 action-set/XSLT 混合加载结构。
- 本轮未制作正式 ZIP，也未改变或清理用户其他工作区修改。

## 2026-08-28 自定义战斗隔离战役专用灰袍领主数据

- 用户已确认精确恢复 SHA-256 `0B435AF0A76A678100F639599AFE46308DE655C27EBAB842A5387E8FD5CBF3DE` 的历史 live DLL 后，自定义战斗重新能够进入，唯一剩余问题恢复为双方英雄人体/护甲预览不显示。由此确认本轮 Release 构建和新增指挥官/拾取补丁造成的直接退出已经消失，后续必须保持该 DLL不变并一次只隔离一项 ModuleData。
- 重读 2026-08-26 至 08-28 双刀开发记录后确认：最早的 `Agent.WieldInitialWeapons` + Mission Tick 监控确实曾访问未完成 Agent，造成“只剩武器、预览缺人和进入场景崩溃”；但当前源码与稳定 DLL均不存在 `GwpDualBladeDiagnostics`、`GwpDualBladeDiagnosticBehavior` 或任何双刀 Tick 监控。当前预览症状不是旧监控仍在执行，而是监控阶段之后留下的双刀数据仍会随 CustomGame 加载。
- AI 双刀开发期间新增 `spc_gw_leader_dual`，并让 `gw_leader_0` 与 `gw_leader_5` 两名战役领主使用完整双刀。`spspecialcharacters.xml` 原先同时注册到 `Campaign`、`CampaignStoryMode`、`CustomGame` 与 `EditorGame`，因此六名只供战役使用的灰袍英雄也会在自定义战斗对象注册阶段被加载；原版 `CustomBattleData.Characters` 固定只读取 `commander_1..24`，这些 `gw_leader_*` 不属于角色选择列表。
- 六名战役英雄分别引用 `spc_knight_skills`、`spc_phalanx_skills`、`spc_mounted_archery_skills`、`spc_quartermaster_skills`、`spc_politician_skills` 与 `spc_diplomat_skills`，而 GreyWarden 当前唯一注册的自定义 SkillSet 文件 `yao_skill.xml` 只定义 `yao_skills`。实机自定义战斗日志持续出现 `Null object reference found with ID: spc_mounted_archery_skills`。该警告在旧版本仍可进入界面，不能单独解释崩溃，但证明战役英雄数据确实不完整地污染了 CustomGame 对象加载。
- 本轮只从 `SubModule.xml` 的 `spspecialcharacters` 节点移除 `<GameType value="CustomGame"/>`；保留 `Campaign`、`CampaignStoryMode` 和 `EditorGame`。没有修改英雄定义、双刀装备、双刃卫士、`commander_2`、动作资源、Harmony 代码、BodyProperties、人物比例或预览 ViewModel。预期结果是自定义战斗不再注册六名战役英雄，同时战役和编辑器继续正常读取它们。
- 为避免再次破坏已验证基线，本轮没有运行任何 build，只把 `SubModule.xml`、`README.md` 与 `README_EN.md` 直接镜像到 live。仓库 `_Module` 与 live 的 36 个正常客户端文件缺失 0、哈希差异 0，XML/XSLT/mbproj 解析失败 0；live 客户端和编辑器 DLL 仍精确保持 `0B435AF0A76A678100F639599AFE46308DE655C27EBAB842A5387E8FD5CBF3DE`。下一次实测只验证英雄预览是否恢复及日志中的 `spc_mounted_archery_skills` 空引用是否消失；若模型仍不显示，则此候选被排除，下一步才隔离双刀全局资源加载闭包。
- 本轮未制作正式 ZIP，也未改变或清理用户其他工作区修改。

## 2026-08-28 回退到 11:17 已知稳定二进制与本轮流程审计

- 用户连续实测本轮三份候选后，进入自定义战斗都会直接报错退出。`rgl_log_14640.txt`、`rgl_log_39656.txt` 与 `rgl_log_42768.txt` 都停在 `NavalDLC.CustomBattle.CustomBattle.NavalCustomBattleScreen::HandleInitialize`，没有像此前 `rgl_log_3892.txt` 那样继续进入 `HandleActivate` 并加载 `inventory_character_scene`。第三份候选已经没有任何自定义战斗 UI/列表补丁仍然崩溃，证明上一轮回退不完整，不能继续把问题单独归咎于指挥官枚举补丁。
- 本轮流程错误已明确：第一，没有先回到用户已确认“能进入界面、只是人物不显示”的稳定二进制便继续叠加候选；第二，把地面拾取禁用、`commander_2` ID 重构和预览排查放在同一轮，扩大变量；第三，在 live 开发模块上擅自使用 `-c Release`，而该项目此前稳定的开发构建命令是默认 Debug 的 `dotnet build --no-restore -p:DeployToLiveModule=true`。正式玩家 Release/诊断关闭构建应进入单独 staging，不能替代 live 的 Debug 测试 DLL；这一点没有按现有维护流程执行。
- 已完整撤销本轮所有运行改动：`GwpIds.CustomBattleCommanderId` 与 `sphpcustombattle.xml` 恢复为 `commander_2`，删除 `IsDualBladeItemId()` 和整个 `GwpDualBladeGroundPickupPatch.cs`，删除两种 `GwpCustomBattleCommanderPatch` 实现，并从双语玩家 README 移除尚未验收的地面拾取与指挥官列表说明。当前源码重新回到提出“禁止拾取双刀”之前的功能边界；地面双刀拾取崩溃尚未修复，不得宣称已经修复。
- 本地 `GreyWardenPolicePurity\obj\Debug\GreyWardenPolicePurity.dll` 在回退前仍保存着 2026-08-28 11:17 的已知稳定 DLL，SHA-256 为 `0B435AF0A76A678100F639599AFE46308DE655C27EBAB842A5387E8FD5CBF3DE`，与此前 live 维护记录完全一致。为防后续构建覆盖，已复制到绝对路径 `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\stable-custombattle-baseline-20260828-1117\GreyWardenPolicePurity.dll`；该文件当前同样为 `0B435AF0A76A678100F639599AFE46308DE655C27EBAB842A5387E8FD5CBF3DE`。
- 精确恢复方法：把上述备份 DLL 分别复制到 `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden\bin\Win64_Shipping_Client\GreyWardenPolicePurity.dll` 和 `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden\bin\Win64_Shipping_wEditor\GreyWardenPolicePurity.dll`。本轮已经执行该恢复，两个 live DLL 的 SHA-256 均重新为 `0B435AF0A76A678100F639599AFE46308DE655C27EBAB842A5387E8FD5CBF3DE`；仓库 `_Module` 与 live 的 36 个正常客户端运行文件缺失 0、哈希差异 0。
- 源码回退后的全量 Debug 重建得到 `BB7944758BAEEAB48C8D6A0BF735CFEBB160F8202FC0082AA76C46041A9F87AB`，没有复现旧 DLL 哈希，原因包括重新生成的调试/源校验元数据；因此没有把“源码语义相同”当作“二进制已恢复”，而是恢复了用户实际测试过的原 DLL。后续在该稳定基线验收前不得再运行会覆盖 live DLL 的 build；若误覆盖，按上一条复制备份恢复。
- 对恢复后的稳定 DLL 做类型清单核对：存在当前已验收的 `GwpDualBladeActionSetPatch`、四个 AI 原生同步类、双刀碰撞/伤害和防御击倒类；不存在 `GwpDualBladeDiagnostics`、`GwpDualBladeDiagnosticBehavior`、`GwpCustomBattleDualBladePreviewPatch`、`GwpCustomBattleCommanderPatch` 或地面拾取补丁。项目仍保留军团、任务和经济等既有开发诊断，这是 AGENTS.md 明确允许留在 live 测试 DLL 中的项目级诊断，与已删除的双刀 Mission Tick 监控不是同一功能。
- 只读结构审计确认下一阶段仍有两个真实但未验证的预览候选：`spspecialcharacters.xml` 被注册到 `CustomGame`，但其六个英雄引用的 `spc_knight_skills`、`spc_phalanx_skills`、`spc_mounted_archery_skills`、`spc_quartermaster_skills`、`spc_politician_skills`、`spc_diplomat_skills` 均未由 GreyWarden 的唯一 `yao_skill.xml` 定义；实机日志会报告 `spc_mounted_archery_skills` 空引用。该警告在旧版本仍能进入界面，不能解释本轮直接崩溃，但这些战役英雄又不属于原版固定自定义战斗指挥官列表，因此其 `CustomGame` 注册是后续可隔离的数据污染候选。另一个候选是双刀全局资源文件/AssetPackage 的加载闭包，而不是已经排除的 Agent 方法或预览 getter。必须先由用户确认 `0B435...` 稳定 DLL重新能够进入界面，再一次只改一个数据变量。
- 本轮没有制作正式 ZIP，也没有清理用户现有工作区改动。

## 2026-08-28 地面双刀拾取禁用与自定义战斗指挥官 ID 隔离

- 用户实测专用 `as_gwp_dual_warrior` 动作集隔离后，玩家与 AI 双刀战斗功能正常，但自定义战斗准备界面的人物仍全部不显示；因此“全局 `as_human_warrior` 注入导致 tableau 失效”的假设已被正式排除。预览问题本轮仍只作为候选修复等待实测，不在玩家日志中宣称已经修复。
- 用户同时确认拾取地面的 `gwdualbladeoffhand` 或 `gwdualblademainhand` 会直接报错退出。最新 `rgl_log_3892.txt` 与 `watchdog_log_3892.txt` 没有托管异常，原生崩溃前最后记录分别为 `Render Requested: gwdualbladeoffhand` 与 `Render Requested: gwdualblademainhand`，说明故障位于原生地面物品视觉/换装路径，而不是双刀攻击逻辑。
- 新增 `GwpDualBladeGroundPickupPatch.cs`，同时拦截 `SpawnedItemEntity.IsDisabledForAgent()`、`Agent.CanInteractableWeaponBePickedUp()`、`Agent.CanQuickPickUp()` 和 `Agent.OnItemPickup()`。两把双刀的地面实体不再显示为可交互物，也不能通过快速拾取或直接拾取入口进入原生换装路径；最后一层会保留地面实体并跳过原方法。其他物品完全沿用原版拾取。
- 没有给物品添加 `ItemFlags.CannotBePickedUp`：Bannerlord 的 `Mission.SpawnAgent()` 会按该标志从出生装备中移除物品，使用它会破坏玩家与 AI 已验收的双刀出生装备。当前方案只识别 `SpawnedItemEntity.WeaponCopy.Item.StringId`，不影响库存装备、入会获赠、战利品持有、玩家双刀或 AI 双刀。
- 反编译当前 1.4.8 `CustomBattleData.Characters` 后确认，自定义战斗不是枚举全部英雄，而是固定读取 `commander_1` 到 `commander_24`。GreyWarden 的 `sphpcustombattle.xml` 此前把专属指挥官也注册为 `commander_2`，实际效果是全局覆盖原版 Elthild，而不是新增灰袍指挥官；这也是启用/停用模组切换后对象状态与共享预览异常的新高概率来源。
- `sphpcustombattle.xml` 与 `GwpIds.CustomBattleCommanderId` 已改为唯一 ID `gwp_custom_battle_commander`，恢复原版 `commander_2`。第一版 `GwpCustomBattleCommanderPatch` 包装 `CustomBattleData.Characters` 的迭代器以追加唯一对象；用户实测进入自定义战斗时直接报错退出。最新 `rgl_log_14640.txt` 停在 `NavalCustomBattleScreen::HandleInitialize`，而上一版同一位置之后会继续执行 `HandleActivate` 并加载 `inventory_character_scene`，证明新枚举包装就是这次回归来源。
- 日志中的 `Null object reference found with ID: spc_mounted_archery_skills` 不是这次直接退出的新增原因：`rgl_log_3892.txt` 和 `rgl_log_15752.txt` 在同一警告后都能正常进入自定义战斗。该缺失模板仍是既有 CustomGame 数据污染线索，后续预览排查时单独处理，不能拿它解释本次回归。
- 第二版改为 `CustomBattleSideVM.RefreshValues()` 完成后的 postfix，用户复测仍在进入自定义战斗时直接报错。`rgl_log_39656.txt` 再次精确停在 `NavalCustomBattleScreen::HandleInitialize`，没有进入 `HandleActivate`；因此不仅枚举包装不可用，任何在该界面初始化期间追加灰袍指挥官的 Harmony 路径都必须从稳定基线中移除。
- 已完整删除 `GwpCustomBattleCommanderPatch.cs`。当前成品 DLL 不再补丁 `CustomBattleData.Characters`、`CustomBattleSideVM`、`UpdateCharacterVisual()`、`CharacterTableau` 或人物装备 getter；自定义战斗完全使用原版指挥官列表。唯一 XML 对象 `gwp_custom_battle_commander` 暂时不进入角色选择，后续只能在不修改界面初始化链的前提下重新设计；双刃卫士、AI 双刀和地面拾取禁用全部保留。
- 数据检查确认 GreyWarden 已不再定义 `commander_2`，原版 `custombattlecharacters.xml` 仍定义原生 `commander_2`。Harmony 独立预检现在只生成原有 7 个双刀战斗补丁与新增 4 个地面拾取补丁，共 11 个 replacement method；不存在自定义战斗界面或角色列表 replacement。
- `Release -t:Rebuild --no-restore -p:DeployToLiveModule=true` 成功，`0` errors、`43` 条既有 nullable warnings。仓库 `_Module` 与 live 的 36 个正常客户端运行文件缺失 0、哈希差异 0；仓库 `obj/Release`、live 客户端和 live 编辑器 DLL SHA-256 均为 `5931C5E4CD1EB1BAF15015ACC9AD37C39DD9A8D3F0D9FE34BC5FE2F1AE018F55`，中英文 README 与 live 一致。本轮未制作正式 ZIP。

## 2026-08-28 自定义战斗预览第二阶段：双刀动作集从全局人物动作中隔离

- 用户实测上一轮恢复坐骑护具引用并统一 XML 编码后，自定义战斗人物仍不显示；因此 `Item.wharnesscom` 和编码声明不是该显示故障的根因，玩家 README 中对应“已修复”表述已撤回。用户同时确认切换 GreyWarden 启用状态后启动会出现报错，而双刀功能加入前没有该现象。
- 对照当前 Bannerlord 1.4.8 反编译结果确认，自定义战斗人物 tableau 与普通战场人类默认都使用 `as_human_warrior`。旧 `action_sets.xslt` 直接把 84 个 GreyWarden 双刀动作注入这一全局动作集，因此任何加载双刀资源的界面都会接触这批动作；此前针对 `Equipment.CalculateEquipmentCode`、`CharacterViewModel.FillFrom`、`BasicCharacterObject.get_Equipment`、`CharacterObject.get_Equipment` 的预览补丁均已被实测排除。
- 已删除整个 `GwpCustomBattleDualBladePreviewPatch.cs`，不再 Harmony 修改 `CustomBattleSideVM.UpdateCharacterVisual()` 或两个角色装备 getter。普通自定义战斗预览现在完全回到原版 ViewModel 与装备读取路径。
- `action_sets.xslt` 现在保持原版 `as_human_warrior` 原样，并另建 `as_gwp_dual_warrior`：专用动作集复制当前原版人类动作后仅在副本追加 84 个双刀动作。离线 `XslCompiledTransform` 验证合并结果为 103 个动作集；原版动作集包含 4699 项、GreyWarden 动作 0 项，专用动作集包含 4783 项、GreyWarden 动作 84 项，缺失 action type 0。
- 新增 `GwpDualBladeActionSetPatch`。它只在 `Mission.SpawnAgent(AgentBuildData,bool)` 已完成真实 Agent 构建后检查第一格副手剑和第二格主手剑；完整配对的玩家或 AI 才调用原版 `Agent.SetActionSet()` 切换到 `as_gwp_dual_warrior`。普通英雄、非双刀兵种、人物预览和库存 tableau 不读取专用动作集。该补丁不生成 Agent、不重挂武器、不轮询、不使用 Mission Tick，也不修改 BodyProperties。
- Harmony 独立预检成功生成 AI 盾牌资格、装备同步、WeaponData、WeaponStatsData、双刀碰撞、伤害类型和新 `Mission.SpawnAgent` 动作集补丁共 7 个 replacement method；旧的三个自定义战斗预览补丁类型已从成品 DLL 消失。
- 读取 10:37 与 10:39 两次实机 `rgl_log` 发现：停用和启用 GreyWarden 的两次进程都正常进入 NavalDLC 自定义战斗界面并执行正常退出清理，日志均没有托管异常；两次末尾都存在 `Non-Zero Device Reference Count`（停用时 ERC2222、启用时 ERC2211）。因此该末尾错误并非只由 GreyWarden 产生，不能把它单独当作本模组启动异常的已证实根因；本轮仍通过专用动作集缩小 GreyWarden 对全局人物资源的影响，等待用户复测切换启动与预览。
- 最终 `dotnet build --no-restore -p:DeployToLiveModule=true` 成功，0 errors、0 warnings（增量构建）。仓库 `_Module` 与 live 模组的 36 个正常客户端文件缺失 0、哈希差异 0，全部 XML/XSLT/mbproj 解析失败 0；live 客户端 DLL SHA-256 为 `0B435AF0A76A678100F639599AFE46308DE655C27EBAB842A5387E8FD5CBF3DE`，专用动作集 XSLT SHA-256 为 `1AA305379F827C5562B2FC60A16F55437DA866C4A2AE7DE84577C863F483BFD0`。本轮未制作正式 ZIP。

## 2026-08-28 已排除：坐骑护具引用与 XML 编码

- 用户复测确认：等级、重步兵小锤、指挥官剑盾和双刀战斗均已正常，唯一残留是启用 AI 双刀后自定义战斗预览的人物模型不显示。
- 对照工作区相对 `HEAD` 的装备表变更发现，`gw_equipment_sets.xml` 原有合法的 `HorseHarness id="wharnesscom"` 被误改为 `Item.wharnesscom`；`items.xml` 中实际对象 ID 是 `wharnesscom`。灰袍英雄的自定义战斗 tableau 会读取完整英雄装备并生成坐骑/人体视觉，无效护具引用会使该视觉创建链失败，同时武器、旗帜和坐骑占位仍可能保留，符合用户症状。已将四处引用恢复为 `wharnesscom`，不改英雄脸型、体型、双刀装备或 AI 逻辑。
- 同轮核对发现 `action_types.xml`、`combat_parameters.xml`、`full_movement_sets.xml`、`item_usage_sets.xml`、`movement_sets.xml` 的 XML 声明写成 `utf-16`，实际文件字节为 UTF-8；已统一声明为 `utf-8`，避免动作资源解析器在预览路径产生部分加载。该修正不改变节点内容或动作名。
- 未重新加入任何双刀监控、Mission Tick、强制生成/重挂武器或人物缩放补丁；AI 双刀补丁、击倒判定和双刀资源保持不变。
- `dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj --no-restore -p:DeployToLiveModule=true` 成功，0 errors、0 warnings（增量构建）；仓库 `_Module` 与 `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden` 的 36 个正常客户端文件缺失 0、哈希差异 0。live 客户端 DLL SHA-256 为 `2911F533B08F7356F09F1419E1E576E9C3551AA3D83A94C8F33B7F8585AEEA68`。
- 本轮尚未代替用户启动游戏；下一验证只需打开自定义战斗预览确认英雄人体/护甲恢复，同时快速确认战场双刀 AI 未回退。若仍缺失，下一隔离点才是 `action_sets.xslt` 的全局 `as_human_warrior` 扩展，不应再叠加 Equipment getter 补丁。

## 2026-08-28 双刃卫士等级、重步兵装备与自定义指挥官预览隔离

- 用户确认双刃卫士显示等级高于普通重步兵。核对 `spnpccharacters.xml` 后发现双刃卫士为 `level=31`、重步兵为 `level=26`；已把双刃卫士改为 `level=26`，两者现在同级，升级关系仍由轻步兵单独指向双刃卫士。
- 普通重步兵原本额外携带 `empire_mace_3_t4` 小锤；已从其战斗装备栏移除，只保留灰袍剑和大盾。没有改双刃卫士的双刀装备。
- 为隔离自定义战斗共享预览问题，`sphpcustombattle.xml` 中的专属指挥官 `commander_2` 已改回普通灰袍剑 `gwonehandedsword` + 黑曜大盾 `wlarge_shield_black`，其余护甲、人物属性和英雄身份不变。双刀领主和双刃卫士的战场配置保留，预览 getter 隔离补丁也保留用于其它可能被选入的双刀角色。
- 这是一次针对预览故障来源的 XML 级 A/B 隔离，不是放弃 AI 双刀；如果自定义战斗人物恢复显示，说明冲突点确实在专属指挥官双刀装备进入共享 tableau 的路径。没有新增监控、Mission Tick、强制拔刀或人物缩放。
- `Release -t:Rebuild --no-restore -p:DeployToLiveModule=true` 成功，`0` errors、`43` 条既有 nullable warnings；仓库 `_Module` 与 live 的 36 个运行文件缺失 0、哈希差异 0。等待用户验证：双刃卫士等级、重步兵不再带锤、自定义战斗人物模型是否恢复，以及战场双刀 AI 是否仍正常。

## 2026-08-28 双刀全方向击倒判定扩展

- 按用户要求，双刀击倒不再只检查左手挥砍。完整双刀装备现在覆盖左挥、右挥、上挥、下劈以及突刺等普通近战攻击；主手附带的长枪等其他武器不会继承双刀击倒。
- 防御碰撞的绕过入口同步扩展到双刀的两只手：被盾挡、武器格挡、招架或 Chamber 时，双刀任一方向的攻击都可以按原有灰袍概率触发同一击倒反应。踢击和盾击仍走原有逻辑，不会重复套用双刀判定。
- 判定依据改为“完整双刀装备 + 当前攻击骨骼对应的实际持用武器”：骨骼 `20` 识别左手副剑，其余近战攻击骨骼识别主手剑；这样不会把领主装备栏中的长枪误判为双刀攻击。没有新增监控、轮询、强制拔刀或人物预览代码。
- `Release -t:Rebuild --no-restore -p:DeployToLiveModule=true` 成功，`0` errors、`43` 条既有 nullable warnings；Harmony 预检成功，仓库、live 客户端和 live 编辑器 DLL SHA-256 均为 `2B88BD9D15D3D2EA8721EF78C46AEA58ECDDAFA50F68015F7A43D17E351AD9D4`。本轮未启动游戏，等待用户实测四向攻击及防御中的击倒。

## 2026-08-28 英雄装备 getter 覆盖补齐（上一版仍不显示）

- 用户实测上一候选后，自定义战斗人物仍不显示，说明只拦 `BasicCharacterObject.get_Equipment` 没有覆盖实际入口。
- 反编译当前 Bannerlord 1.4.8 后确认：`TaleWorlds.CampaignSystem.CharacterObject` 覆盖了 `Equipment` 属性；英雄会直接返回 `HeroObject.BattleEquipment`，不会经过 `BasicCharacterObject.get_Equipment`。灰袍自定义战斗指挥官带 `is_hero="true"`，因此上一版对英雄完全没有生效，这解释了为什么全体共享预览仍被双刀英雄拖坏。
- 新增 `GwpCustomBattleHeroEquipmentGetterPatch`，与基础 getter 共用同一个预览安全装备清理器。它只在 `CustomBattleSideVM.UpdateCharacterVisual()` 的线程局部作用域内生效，仍只返回普通灰袍剑盾克隆；真实英雄装备、AI 战场双刀、BodyProperties、体型、动作、击倒均不改。非英雄和非双刀角色路径不变。
- `Release -t:Rebuild --no-restore -p:DeployToLiveModule=true` 成功，`0` errors、`43` 条既有 nullable warnings。Harmony 预检同时成功生成 `BasicCharacterObject.get_Equipment` 和 `CharacterObject.get_Equipment` 两个预览入口，以及六个双刀战斗补丁。需要用户再次实测自定义战斗预览与双刀战场；本条暂不宣称已修复。

## 2026-08-28 监控时间线复核与自定义战斗装备读取隔离

- 用户补充了最早出现预览缺人的准确时间线：为判断 AI 双刀是否启用以及接战闪退发生在哪一步，第一版曾在 `Agent.WieldInitialWeapons()` 前后记录装备索引，并由 Mission Tick 持续轮询 Agent；从加入这套监控后，自定义战斗开始出现人体/护甲不加载、只剩武器，并伴随过闪退。该记忆与 2026-08-26 的现场记录一致：轮询读取了尚未完成构建的预览 Agent，确实能破坏预览和进入场景流程，不能把这段历史当成用户误判。
- 当前源码和 live DLL 已再次核对：`GwpDualBladeDiagnostics`、`GwpDualBladeDiagnosticBehavior`、初始拔刀/武器更新/收刀请求/拔刀请求/武器选择失效等六类双刀诊断补丁均不存在；双刀也没有 Mission Tick 轮询。项目原有的 `GwpAiDiagnostics` 是大地图办案、队伍和经济诊断，不访问自定义战斗 Agent，不属于当时的双刀监控。故当前症状不是旧监控仍在运行，而是 AI 双刀适配曾把不适用于 tableau 的装备数据带进预览路径。
- 前两轮 `Equipment.CalculateEquipmentCode` 与 `CharacterViewModel.FillFrom` 隔离均已由用户实测证明无效，现已完整撤回，不继续叠加。进一步核对 `CustomBattleSideVM.UpdateCharacterVisual()` 的读取路径后，确认它还会直接访问 `BasicCharacterObject.Equipment`；只替换装备码或 ViewModel 内部副本无法覆盖这条入口。
- 当前候选只在 `CustomBattleSideVM.UpdateCharacterVisual()` 执行期间建立线程局部作用域，并在该作用域内拦截 `BasicCharacterObject.Equipment` 的读取。若角色是完整 GreyWarden 双刀配置，仅向这次预览返回普通灰袍剑与黑曜大盾的装备克隆；角色的真实 `Equipment`、角色定义、BodyProperties、战场生成和 AI 双刀原生同步都不改，非双刀角色完全走原版。作用域用 Harmony finalizer 回收，即使原预览方法抛出异常也不会把隔离状态泄漏到后续界面或战场。
- 本候选没有增加日志、监控、Mission Tick、强制拔刀、重新生成或重挂武器，也没有修改左手击倒。验证目标只有两个：自定义战斗双方人物是否恢复；专属双刀指挥官进入实际战场后是否仍正常双持。未得到用户实测前，不把本候选记作已经修复。
- `Release -t:Rebuild --no-restore -p:DeployToLiveModule=true` 已成功，`0` errors、`43` 条既有 nullable warnings。Harmony 独立预检成功生成 `CustomBattleSideVM.UpdateCharacterVisual` 与 `BasicCharacterObject.get_Equipment` 两个 replacement，并同时确认六个已验收的双刀战斗补丁仍可生成。仓库 `obj/Release`、live 客户端和 live 编辑器 DLL SHA-256 均为 `B8BAFB290D1FD61E6D46E09251F1844F5E7A40D86A23F2117642E3EE32DC95EE`；仓库 `_Module` 的 `36` 个部署文件与 live 缺失 `0`、哈希差异 `0`。README 已撤下尚未实测的“预览已修复”表述；未制作正式 ZIP。

## 2026-08-28 自定义战斗预览第二层隔离（上一版仍不显示）

- 用户截图确认：上一版只隔离 `EquipmentCode` 后，AI 双刀战斗仍正常，但自定义战斗预览依旧只显示武器和旗帜，人体/护甲没有恢复。因此不能把“只替换装备码”当成已解决。
- 反编译 `CharacterViewModel.FillFrom(BasicCharacterObject, int, string)` 后确认它会先读取角色装备生成 `BodyProperties`，再把真实装备克隆到视图模型；仅在 `Equipment.CalculateEquipmentCode()` 返回安全码仍可能让原始双刀装备留在视图模型内部。新增同一自定义战斗线程范围内的 `FillFrom` 前置替代：完整双刀角色使用普通灰袍剑盾副本同时生成身体属性、坐骑键和装备码，然后跳过原版对双刀装备的克隆。`SelectedCharacter`、武器图标列表和战场 AI 装备不改。
- AI 双刀原生同步、双刃卫士、双刀领主、自定义战斗指挥官和左手挥砍击倒均未改；没有监控、轮询、强制拔刀、重新生成、体型/脸型修改。
- Harmony 预检新增并成功生成 `CharacterViewModel.FillFrom` replacement；`Release -t:Rebuild --no-restore -p:DeployToLiveModule=true` 成功，`0` errors、`44` 条既有 nullable warnings。仓库 `obj/Release`、live 客户端和 live 编辑器 DLL SHA-256 均为 `70252E2F11BF2CFCBE289A604359CE1333C4FA8A7FFA48E04BC441E2CF16D485`；仓库 `_Module` 的 `36` 个部署文件与 live 缺失 `0`、哈希差异 `0`，中英文 README 与 live 哈希一致。该版本等待用户再次打开自定义战斗界面验收。

## 2026-08-28 AI 双刀与自定义战斗预览的独立修复

- 用户多次完成启用/停用对照：AI 双刀内容存在时，自定义战斗共享预览中的双方人物会消失或比例异常；把双刀指挥官、双刀领主、双刃卫士及 AI 原生同步临时隔离后，人物显示恢复。该回退只用于建立诊断基线，不代表放弃 AI 双刀。
- 上一候选把 AI 资格收紧为 `agent.Mission == Mission.Current`，但用户复测显示预览问题仍在。反编译确认 `CustomBattleSideVM.UpdateCharacterVisual()` 根本不创建 `Agent`：它直接克隆 `SelectedCharacter.Equipment`、计算 `EquipmentCode`，再交给 `CharacterTableau`。因此该 Agent 条件无法触达故障路径，旧记录中“Mission 归属隔离已修复”的结论错误，现已更正。
- 临时隔离基线之后，已恢复实测能够让 NPC 持续拔出副手、正常攻击和格挡的 `GwpDualBladeAiNativeSyncPatch.cs`，恢复轻步兵到 `gwdualbladeguard` 的独立升级路线、`spc_gw_leader_dual`（仅 `gw_leader_0` 与 `gw_leader_5`）以及双刀自定义战斗指挥官。没有新增物品，也没有恢复监控、Mission Tick、强制拔刀、重新生成或人物数据修改。
- 新增 `GwpCustomBattleDualBladePreviewPatch.cs`。只在 `CustomBattleSideVM.UpdateCharacterVisual()` 的当前线程范围内拦截完整 GreyWarden 双刀装备的 `Equipment.CalculateEquipmentCode()`：克隆一份预览装备，把该副本的武器格换成普通灰袍剑与黑曜大盾后计算展示码；`SelectedCharacter.Equipment`、装备图标列表和进入战场后的真实双刀装备均不改。这样预览场景不再接收 AI 兼容双刀码，而战场 AI 仍走原先已验收的双刀链。
- 左手挥砍与防御中的击倒实现完全未改。Harmony 独立预检已成功生成 `UpdateCharacterVisual`、`CalculateEquipmentCode` 以及四个 AI 双刀目标的 replacement method；仍需用户实机确认自定义战斗双方人物恢复，同时确认专属指挥官和双刃卫士进入战场后继续正常双持。
- 最终 `Release -t:Rebuild --no-restore -p:DeployToLiveModule=true` 成功，`0` errors、`43` 条既有 nullable warnings。成品反编译确认预览补丁只替换当前线程的展示装备码，AI 原生副本仍带 `bo_mace_a`、`bo_wlarge_shield`、近战位、耐久与格挡资格。仓库 `obj/Release`、live 客户端和 live 编辑器 DLL SHA-256 均为 `B53A9CCD6C9C7CCB47A2C19C413808E4B2E42F66F8DC9BB0AC6AC3CC05D7E407`；仓库 `_Module` 的 `36` 个部署文件与 live 缺失 `0`、哈希差异 `0`，`24` 个 XML/XSLT/mbproj 文本解析失败 `0`，中英文 README 与 live 哈希一致。结构核对为：自定义指挥官双刀、双刀领主精确为 `gw_leader_0/gw_leader_5`、双刃卫士一名且由轻步兵升级。未制作正式 ZIP。

## 2026-08-28 左手挥砍无视防御击倒

- 用户确认双刀战斗功能正常，但要求左手挥砍像灰袍踢击/盾击一样，不因目标处于盾挡、武器格挡、招架或
  Chamber 状态而失去击倒反应。反编译 Bannerlord 1.4.8 的 `Mission.MeleeHitCallback()` 后确认：这些防御
  碰撞会让原版跳过 `Mission.RegisterBlow()`；因此仅覆盖
  `AgentApplyDamageModel.DecideAgentKnockedDownByBlow()` 不足以让防御者真正倒地。
- 新增 `GwpDualBladeDefenceBypassPatch`，只匹配完整 Grey Warden 双刀、攻击骨骼 `20`、普通挥砍，并且只在
  原碰撞结果为盾挡/武器格挡/招架/Chamber 时按现有灰袍领主/兵种概率进行一次判定。成功时向同一受击者发送
  一个静音、1 点控制接触、`BlowFlags.KnockDown` 的原生 `Agent.RegisterBlow()`；真实左手剑的伤害、盾牌耐久、
  攻击动作和未防御碰撞路径保持原样。没有新增监控、轮询、强制拔刀或人物数据改写。
- 该控制接触沿用现有踢击/盾击控制反应的原生入口，目的只是绕过防御碰撞本身跳过 `RegisterBlow()` 的边界；
  实机仍需确认防御中的目标确实播放击倒反应且未产生额外伤害异常。
- 本轮 `dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj -c Release -t:Rebuild
  --no-restore -p:DeployToLiveModule=true` 成功，`0` errors、`43` 条既有 nullable warnings。仓库、live
  客户端和 live 编辑器 DLL SHA-256 均为
  `2D557904E1144CBDD69024BD1B5E3D7FE35C8A8686D7E83DDF414414D8E58281`；仓库 `_Module` 的 `36` 个
  正常客户端部署文件与 live 缺失 `0`、哈希差异 `0`。本轮未启动游戏，等待用户实机验证防御中的击倒表现。

## 2026-08-28 自定义战斗人物预览显示问题（上一候选未解决）

- 用户实测上一候选后，自定义战斗仍出现双方人体/护甲消失、只剩武器、旗帜和坐骑；因此“把真实副手锻造片
  `body_name` 恢复为 `bo_sword_one_handed` 即可修复预览”的假设已被排除。本轮不再继续修改
  `BodyProperties`、体型、比例或真实人物数据，也不把该问题写入玩家 README 的“已修复”列表。
- 已确认玩家双刀和 NPC 双刀战斗动作仍正常；显示问题与战场 AI 拔刀/击倒逻辑分离。下一隔离目标是自定义战斗
  `CharacterTableau` 所使用的动作集/XSLT、AssetPackages 资源闭包及其共享预览场景加载，不恢复双刀监控或在
  Mission Tick 中添加补偿逻辑。

## 2026-08-28 自定义战斗人物预览消失修复

- 用户用同一自定义战斗界面完成启用/停用对照：停用 GreyWarden 时双方人物正常，启用后双方人体与
  护甲消失，只剩武器、旗帜和坐骑；用户进一步确认玩家专用双刀阶段没有该问题，症状从 NPC 双刀适配
  后开始。该证据排除单个 `NPCCharacter` 的脸、年龄、体重和体型定义，也不允许继续改
  `BodyProperties`。
- 反编译 Bannerlord 1.4.8 的 `CustomBattleSideVM.UpdateCharacterVisual()` 与
  `CharacterTableau.InitializeAgentVisuals()` 确认，预览不生成战场 `Agent`，而是直接把所选人物的真实
  `Equipment` 交给共享预览场景中的 `AgentVisuals`。因此 `Agent.EnforceShieldUsage()`、
  `EquipItemsFromSpawnEquipment()` 和其线程局部 Native 数据代理不会直接参与预览；继续修改 AI Agent
  条件无法修复这个界面。
- 时间线差分发现 AI 资格实验曾把真实隐藏锻造片 `gwp_vlandian_blade_3_dual` 的 `BladeData.body_name`
  从原版剑主体 `bo_sword_one_handed` 改为 ROT 固定物品使用的 `bo_mace_a`。这一真实物品字段会被
  CharacterTableau 读取，并可使同一共享场景中的双方人物视觉一起失效。现只把锻造片恢复为
  `bo_sword_one_handed`；当前原版
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\crafting_pieces.xml`
  的 `vlandian_blade_3` 也在其 `BladeData` 明确定义同一主体。AI 战场所需的 `bo_mace_a` 与
  `bo_wlarge_shield` 仍仅由
  `GwpDualBladeAiWeaponDataPatch` 在完整双刀 AI 的一次 `EquipItemsFromSpawnEquipment()` 数据副本中
  提供。没有改动作、装备槽、模型、剑鞘、AI 拔刀、格挡、左手劈砍或击倒，也没有恢复任何双刀监控。
- 此项属于基于对照现象和反编译路径的最小修复。`Release -t:Rebuild --no-restore
  -p:DeployToLiveModule=true` 构建通过，`0` errors、`43` 条既有 nullable warnings；仓库、live 客户端和
  live 编辑器 DLL 的 SHA-256 均为
  `17A535B2EAF7CD76137D058DFBD2F06F18505C28C52ADD37BBBF926DA10BF1E8`，证明本轮没有改变已实测成功的
  AI 双持托管代码。`gwp_crafting_pieces.xml` 在仓库与 live 的 SHA-256 均为
  `8BE1EAD5BDFC8C08DF8682E7BEB8880D364604DFB8AE57535CAE48A29C5B2B65`；仓库 `_Module` 的 `36` 个
  正常客户端部署文件与 live 缺失 `0`、哈希差异 `0`。ModuleData 的 `24` 个 XML/XSLT/mbproj 按文本
  实际编码解析失败 `0`；其中 5 个既有动作数据文件声明 UTF-16 但实际为 UTF-8，直接按声明载入会报
  BOM 不匹配，游戏实际读取与按文本规范化解析均不受影响。`git diff --check` 通过，没有创建正式 ZIP。
  尚未代替用户启动游戏；下一次实测只需先进入自定义战斗界面确认双方人体恢复，再进入战场确认 NPC
  仍持续双持、攻击和格挡。

## 2026-08-28 双刀监控移除与左手劈砍判定修复

- 用户明确要求停止双刀相关监控。删除 `GwpDualBladeDiagnostics.cs`，移除
  `SubModule.OnMissionBehaviorInitialize()` 中的 `GwpDualBladeDiagnosticBehavior` 注册，并从
  `GwpDualBladeAiNativeSyncPatch.cs` 删除所有双刀诊断调用。双刀功能补丁、AI 原生同步和项目其他
  AI 诊断不受影响；已有的
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-DualBlade-Diagnostics.log`
  未做破坏性删除，代码已不再写入它。
- 左手剑伤害被原版判成钝击的直接路径是 `MissionCombatMechanicsHelper.GetAttackCollisionResults()`：
  当 `HitWithAnotherBone()` 为真时，原版把伤害类型改成 `Blunt`。ROT v1.3.15.3 的补丁只要
  `AttackCollisionData.AttackBoneIndex == 20` 就把骨骼不匹配判定改为假。GreyWarden 的
  `GwpDualWieldingPatch` 已对齐这一规则，不再额外要求副手槽索引为 `0`；放行后保留武器 XML 的
  `Swing damage_type="Cut"`，由原版正常计算劈砍伤害和护甲倍率。
- 左手挥砍击倒不再依赖 `IsAlternativeAttack == false`、固定 `Weapon0` 碰撞槽或
  `Blow.AttackType == Standard`。`GwpAgentApplyDamageModel` 现在确认攻击者完整装备
  `gwdualbladeoffhand` + `gwdualblademainhand`，命中类型为挥砍，并且传入伤害模型的
  `attackerWeapon` 就是当前挥出的 `WieldedOffhandWeapon.CurrentUsageItem`；这样可覆盖玩家和 AI
  的真实副手挥砍，同时不会把右手剑、踢击或盾击误判为左手剑。击倒概率仍复用原有灰袍领主/兵种档位，
  成功时抑制额外击退，失败时回到原版普通近战击退。
- 本轮没有新增监控、日志、伤害倍率、合成攻击、实体生成、持续重挂或强制拔刀。中英文玩家 README
  的 v1.4-r10 条目已同步说明左手挥砍按劈砍伤害处理并可触发灰袍击倒判定。
- `dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj -c Release -t:Rebuild --no-restore
  -p:DeployToLiveModule=true` 已通过，0 errors、43 条既有 nullable warnings。仓库
  `obj/Release` 客户端 DLL 与 live 客户端 DLL 均为 `788480` 字节，SHA-256
  `0943B5DBF8FDC35475BB1CAE6E95A9D1605C040ABFF07E74329B934FD0285AA6`；仓库 `_Module` 与 live
  模组 36 个可部署文件缺失 0、哈希差异 0。代码源文件与成品 DLL 均未发现
  `GwpDualBladeDiagnostics` 或 `GwpDualBladeDiagnosticBehavior` 引用，`git diff --check` 通过。
  尚未代表用户启动游戏实测。

## 2026-08-28 双刀左手挥砍击倒机制

- 用户确认当前 NPC 双刀已经能够拔出左手剑并正常攻击、格挡，但左手剑伤害体感低于右手剑；用户明确要求不再继续增加伤害监控或倍率修正，而是把现有灰袍踢击/盾击使用的击倒判定接到左手挥砍命中。
- 修改 `GwpAgentApplyDamageModel`：仅当命中来自 `Weapon0`、该槽是 `gwdualbladeoffhand`、`Weapon1` 是 `gwdualblademainhand`、碰撞类型为普通挥砍且 `Blow.AttackType` 为 `Standard` 时，复用原有领主/兵种等级击倒概率；双刃卫士补入原有精英兵种概率档。成功时沿用现有“击倒抑制额外击退”的流程；失败时调用当前游戏原生普通近战击退判定，不给左手挥砍额外强制击退。右手剑、左手刺击、踢击/盾击和非双刀装备不受此分支影响。
- 本轮没有新增命中监控、伤害倍率、合成攻击、实体生成、持续重挂或玩家专用分支。README 的当前 v1.4-r10 中英文条目已同步说明左手挥砍命中可触发灰袍击倒判定。
- `dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj -c Release -t:Rebuild --no-restore -p:DeployToLiveModule=true` 已通过，0 errors、44 条既有 nullable warnings。`obj/Release`、客户端和编辑器 DLL 均为 788480 字节，SHA-256 `E17B5A6CE2E493F343207F3B7D24C85B5C2B1D3622C1DA335395D3DC3D3219F1`；仓库 `_Module` 与 live 模组 36 个可部署文件缺失 0、哈希差异 0，`git diff --check` 通过。本轮仅完成代码和部署验证，尚未代表用户启动实机测试。

## 2026-08-28 双刀 AI 原生副手碰撞体候选（待实机验收）

- 当前实测基线是：玩家双刀正常，NPC 初始拔刀后又被 Native AI 清除左手剑；上一候选只保留
  `MeleeWeapon + HasHitPoints`，因此没有崩溃但也没有持续副手。此前加入 `CanBlockRanged`
  能让 NPC 持刀，却在接战格挡时触发 `TaleWorlds.Native.dll+0x73ddf8` 空指针。
- 本轮只在完整 GreyWarden 双刀 AI 的一次 `Agent.EquipItemsFromSpawnEquipment()` Native 同步作用域
  内恢复 `CanBlockRanged`，保留 `MeleeWeapon`、`HasHitPoints`、`DataValue/MaxDataValue=500`，并把
  `WeaponData.CollisionShape` 补为游戏中已存在的 `bo_wlarge_shield`。`WeaponData.Shape` 仍为
  ROT 使用的 `bo_mace_a`。这解决的是 Native 盾资格路径读取空碰撞对象的问题；不改托管
  `MissionWeapon`、玩家路径、模型、剑鞘、动作资源，不生成实体、不持续重挂、不用 Mission Tick 补拔刀。
- 预期验证顺序：新开自定义战斗后 NPC 左手剑持续出鞘；玩家攻击双刀 NPC 不崩溃；NPC 能用双持
  攻击及四向近战格挡。若仍崩溃，下一步应撤回 `CanBlockRanged`，不要继续扩大盾牌伪装字段。
- 构建部署：`dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj -c Release -t:Rebuild --no-restore -p:DeployToLiveModule=true`，
  0 errors、44 条既有 nullable warnings。客户端与编辑器 DLL 均为 787968 字节，SHA-256
  `6B74A7D05DBDC3AD3695E208E80F0FED1126D378A638656724012AE5E9AA60CB`；仓库 `_Module` 与 live
  模组 36 个可部署文件缺失 0、哈希差异 0。当前诊断日志仍是上一会话
  `2026-08-28T02:07:47–02:07:51`，尚未包含本候选的实机结果。

## 2026-08-28 双刀 AI 碰撞体同步（待实机验收）

- 用户反馈本轮 NPC 没有拔出副手剑，但现有诊断日志最后一次会话仍是
  `2026-08-28T01:19:46–01:22:05`；实时客户端 DLL 在 `01:40:06` 已换成另一份构建，
  因此不能把旧日志归因到当前候选。为隔离这个时间线问题，先做单字段回退：保留
  `HasHitPoints`、`DataValue/MaxDataValue=500`、`MeleeWeapon`、`bo_mace_a` 和原始剑攻击数据，
  移除 `CanBlockRanged`。该标志会把近战剑带入盾牌专用的远程格挡碰撞路径，既可能让初始副手资格
  被 Native 拒绝，也可能重现此前接战格挡空指针；本轮不改玩家路径、不生成实体、不强制重挂。

- 用户最新实测确认上一版已退步：玩家双刀不受影响，但双刃卫士 NPC 不再拔出左手剑。诊断日志显示 `WieldInitialWeapons()` 之后短暂得到
  `actualOff=WeaponItemBeginSlot`，随后仍被 Native AI 清为空；当前副手统计只有 `MeleeWeapon + HasHitPoints`，没有触发原版可长期占用副手的完整判定。
- 对照已安装的 ROT v1.3.15 数据，非锻造左手剑明确使用 `body_name="bo_mace_a"`，对应 `WeaponData.Shape`；ROT 没有声明 `collision_body`。此前恢复
  `CanBlockRanged` 时，副手仍使用锻造剑的 `bo_sword_one_handed` 主体，且没有复制 ROT 的主体形状；该差异与“能出鞘但真实格挡进入 Native 后崩溃”相符。
- 调整 `GwpDualBladeAiNativeSyncPatch`：仅在完整双刀 AI 的一次 `EquipItemsFromSpawnEquipment()` 同步作用域内，对传给 Native 的副本加入
  `HasHitPoints`、`DataValue/MaxDataValue=500`，并将 `WeaponData.Shape` 对齐为原版资源 `bo_mace_a`；不再加入
  `CanBlockRanged`，避免近战剑进入盾牌专用碰撞路径。`CollisionShape` 保持 ROT 的空值。副手锻造片的
  `body_name` 也同步为 `bo_mace_a`。保留 `MeleeWeapon`、剑类用途、伤害、动作、模型、剑鞘和托管
  `MissionWeapon`；玩家和普通装备不进入作用域，不生成实体、不强制重挂、不轮询拔刀。
- 诊断现在记录 `WeaponData.Shape` 与 `CollisionShape` 名称，下一次测试应看到 AI 副手行 `shape=bo_mace_a` 且 `collisionShape=-`，并依次验证：NPC 左手剑持续出鞘；玩家攻击双刀 NPC 不崩溃；NPC 能用双持攻击和四向近战格挡。
- `dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj -c Release -t:Rebuild --no-restore -p:DeployToLiveModule=true`：0 errors、44 条既有 nullable warnings。`obj/Release`、客户端和编辑器 DLL SHA-256 均为
  `763007E718B01226BAEA966A3EBEF0A1BF40AF4BB088B2ED7BBB60C37E49E2FA`；仓库 `_Module` 与 live 模组 36 个可部署文件缺失 0、哈希差异 0，ModuleData XML/XSLT 解析失败 0。仅部署诊断开发版，本轮不制作正式 ZIP，等待实机验收后再决定是否保留此候选。

## 2026-08-28 双刀 AI 最小原生资格同步（待实机验收）

- 用户最新实测确认：NPC 已成功拔出左手剑并保持双持动作，但玩家双刀攻击、NPC 双刀进入防御时，客户端在接战阶段发生
  `TaleWorlds.Native.dll+0x73ddf8` 的 `0xc0000005`。这次不是生成或收刀问题，崩溃只在真实格挡碰撞发生时出现。
- 新转储 `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.47008.dmp` 由 WinDbg 确认：
  故障指令为 `mov rbp,[rdx+8]`，`rdx=0`；调用者位于原生战斗更新路径，故障函数是一个空源对象的 `std::vector` 复制函数。
  这与把剑的 `CanBlockRanged` 发送给 Native 后、第一次真实格挡访问不存在的盾牌碰撞对象相符，但不能据此声称已定位引擎私有函数名。
- 对照 ROT v1.3.15.3 再次确认：ROT 没有 AI、`UpdateWeapons`、`WieldInitialWeapons` 或 AI 格挡补丁；玩家双刀始终保留
  `OneHandedSword + MeleeWeapon`，只依靠 `dual_shield` 用法集和 bone 20 碰撞放行。因此“玩家正常”不能通过复制一段 ROT AI 代码解决，
  需要避免把 AI 的剑统计伪装成盾牌统计。
- 已做最小字段隔离：保留 `MeleeWeapon`、`OneHandedSword`、原始伤害/用途/动作与 `DataValue/MaxDataValue=500`，只从 AI 初次装备同步的
  Native 副本移除 `CanBlockRanged`，不改玩家、不改托管 `MissionWeapon`、不生成或重挂实体。若副手仍能出鞘且格挡不崩，说明耐久资格足够而盾牌碰撞标志是崩溃触发点；若副手再次被清除，则证明原生 AI 资格判断要求更完整的盾牌记录。
- 本轮构建 `dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj -c Release -t:Rebuild --no-restore -p:DeployToLiveModule=true` 成功，
  `0` errors、`44` 条既有 nullable warnings。仓库 `obj/Release`、客户端和编辑器 DLL 均为 `787456` 字节，SHA-256
  `D060F186BEBC6662919130B67DE59F6570FEADB93B3A4A89D704367F4D273E09`；实机目录仍与 `_Module` 全量哈希一致。本轮尚未启动游戏，不制作正式 ZIP。

- 由于转储对应的上一份客户端仍加载了带 `CanBlockRanged` 的旧 DLL，本轮再次执行完整 Rebuild 并覆盖客户端与编辑器 DLL。当前 live 与仓库 DLL
  均为 `787456` 字节、SHA-256 `D060F186BEBC6662919130B67DE59F6570FEADB93B3A4A89D704367F4D273E09`；
  运行文件缺失 `0`、哈希差异 `0`，`ModuleData` 的 `21` 个 XML 全部解析成功。下一次实机必须从新开自定义战斗开始，确认日志中的
  `AI_NATIVE_WEAPON_STATS_PROXY` 只有 `MeleeWeapon, HasHitPoints`，再观察左手剑持续出鞘及真实格挡是否仍崩溃。

- 最新 `2026-08-28T00:23:15` 自定义战斗日志覆盖 200 名双刃卫士：全部在
  `WieldInitialWeapons()` 后达到 `actualMain=Weapon1`、`actualOff=WeaponItemBeginSlot`，约
  2.3 秒后又统一变为 `actualOff=None`。期间已命中 `AI_SHIELD_ENFORCEMENT_BYPASSED` 与
  `AI_FORMATION_INFO_BYPASSED`，却没有 `SHEATH_REQUEST`、`AI_UPDATE_WEAPONS_*` 或
  `AI_INVALIDATE_WEAPON_SELECTIONS_*`。因此装备格、初始拔刀、用途名称和两条托管阵型入口均已排除；
  清空发生在 Native AI 内部武器资格选择。
- 撤回 `GwpDualBladeFormationInfoPatch`。它已由日志证明不能阻止清空，却会让双刀 AI 长期不向
  Native 更新正常阵型文件/排位/间距元数据，副作用大于诊断价值。保留范围精确到完整双刀装备的
  `Agent.EnforceShieldUsage()` 旁路，避免原版盾墙整理再次把非标准副手直接剔除。
- 重新核对 `2026-08-27 12:25:09` 的 WER 事件：旧实验崩溃为
  `TaleWorlds.Native.dll+0x73ddf8`、`0xc0000005`、读取地址 `0x8`。当前 Native DLL 反汇编显示该
  地址位于内部对象复制函数 `0x73ddb0`，故障指令读取传入源对象的 `+8`。转储已经不在现有 WER
  目录中，无法从当前文件继续恢复完整调用栈；但该崩溃只随“清除 `MeleeWeapon`”代理出现并在撤回
  后消失，因而最可靠的字段差分是不能再让副手缺少完整近战数据。ROT 的原始左右手剑均明确保留
  `OneHandedSword + MeleeWeapon`，所以不得恢复清除 `WeaponMask` 的实现，也不把当前反汇编结果
  夸大为已定位具体 Native AI 函数名。
- 恢复此前生成但从未实机验证的最小候选：只在
  `Agent.EquipItemsFromSpawnEquipment()` 同步完整 GreyWarden 双刀 AI 的线程局部作用域内，给送往
  Native 的副手数据副本补 `DataValue=500`、`MaxDataValue=500`、`HasHitPoints` 与
  `CanBlockRanged`。原有 `MeleeWeapon` 以按位 OR 方式强制保留；`WeaponClass`、用途索引、伤害、
  动作、模型、剑鞘及托管 `MissionWeapon` 均不改。玩家、普通 AI、拾取/掉落和后续运行时装备均
  不进入此作用域；没有生成实体、补拔刀、Mission tick 重挂或持续强制。
- 玩家排除不是依赖时序猜测：v1.4.8 反编译的 `Agent.Build()` 在
  `Mission.BuildAgent()` 调用装备同步之前，已经把 `Controller` 设置为 `agentBuildData.AgentController`；
  最新自定义战斗日志也记录玩家指挥官在 `AGENT_BUILD/WIELD_INITIAL` 阶段为 `isAI=False`。因此
  `HasCompleteDualBladeLoadout()` 的 `IsAIControlled` 守卫不会把代理施加给玩家。
- 诊断恢复 `AI_NATIVE_SYNC_BEGIN/END`、`AI_NATIVE_WEAPON_DATA_PROXY` 与
  `AI_NATIVE_WEAPON_STATS_PROXY`，其中统计行明确记录 `meleePreserved=True`。下一次测试必须依次确认：
  游戏与自定义战斗正常启动；接战前不再崩溃；出生约三秒后仍为
  `actualOff=WeaponItemBeginSlot`；士兵和 NPC 领主能用同一动作移动、攻击及格挡。未通过实机前，
  README 只写“待确认”，不得宣称问题已修复。
- `Release -t:Rebuild --no-restore` 已通过，`0` error、`44` 条既有 nullable warning。独立 Harmony
  预检进程分别为 `Agent.EnforceShieldUsage`、`Agent.EquipItemsFromSpawnEquipment`、
  `MissionWeapon.GetWeaponData` 与 `MissionWeapon.GetWeaponStatsData` 成功生成 replacement method。
  ILSpy 复核成品只执行 `originalFlags | 0x10000200`，不存在清除 `MeleeWeapon` 的按位操作，也不再包含
  `GwpDualBladeFormationInfoPatch` 类型。实机客户端、编辑器与仓库 `obj/Release` DLL 均为
  `787456` 字节，SHA-256 均为
  `23B99D450DC02BB08E602194C96F203BCA7F6E112E3E23F6B7E0710A25FC87AA`；仓库 `_Module` 的
  `36` 个普通客户端部署文件与实机缺失 `0`、哈希差异 `0`，ModuleData 的 `23` 个 XML/XSLT
  解析失败 `0`，中英文 README 与实机哈希一致，`git diff --check` 通过。本轮只部署诊断开发版，
  不制作正式 ZIP。

## 2026-08-27 双刀 AI 原生武器选择观测补丁（待实机验收）

- 在上一轮用途标识对齐之后，补充了只读 Harmony 观测：记录双刀 AI 进入和离开
  `Agent.UpdateWeapons()`、`Agent.InvalidateAIWeaponSelections()` 时的主副手索引。
  事件按 Agent 和手部状态去重，避免原生高频更新再次把诊断文件无限放大。
- 观测补丁不改变返回值、装备槽、武器实体、动作、攻击输入或 AI 决策；玩家和普通
  AI 不进入双刀筛选条件。它只用于确认副手清空发生在原生更新前后哪一个边界，之后
  才决定是否需要针对该入口做最小旁路。
- 本次 `dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj -c Release
  -t:Rebuild --no-restore` 成功，0 errors、44 条既有 nullable warnings。开发版 DLL
  已自动部署到
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`；
  仓库 `_Module` 的 36 个可部署文件与实机缺失 0、哈希差异 0。实机 DLL SHA-256 为
  `971F24969BF9FA7D6110DE7ABA9C3C3511CF466113ACBFE00FECFB3537F603DB`。
- 当前仍未代表用户启动游戏，下一次自定义战斗需重点保留
  `AI_UPDATE_WEAPONS_*`、`AI_INVALIDATE_WEAPON_SELECTIONS_*`、
  `WIELD_INITIAL_*` 和 `HAND_STATE_CHANGED` 行，以判断 AI 副手是被原生更新清空、
  选择失效后未重挂，还是仅发生了视觉挂接问题。本轮不制作正式 ZIP。

## 2026-08-27 双刀 AI 阵型元数据重算旁路（待实机验收）

- 上一版实机日志把清空时序固定为：`WieldInitialWeapons()` 后约 `0.6s` 仍为
  `Weapon1/WeaponItemBeginSlot`，约 `2s` 后才变成 `Weapon1/None`；期间没有托管
  `TryToSheathWeaponInHand()`，而 `Agent.EnforceShieldUsage()` 已被前一轮补丁旁路。
  因此“只跳过盾牌整理”不足以排除原生 AI 的第二个状态入口。
- 反编译 Bannerlord 1.4.8 的 `Agent.ApplyFormationValuesPostUpdate(bool,bool)` 后确认，
  AI 的完整阵型更新会依次调用 `UpdateFormationOrders()` 和原生
  `IMBAgent.SetFormationInfo(...)`。前者已被旁路，但后者仍会把阵型元数据送入 native
  agent 状态机，符合“没有收刀请求却清空副手”的剩余路径。新增
  `GwpDualBladeFormationInfoPatch`，只对装备槽 `Weapon0/Weapon1` 精确匹配双刀的 AI
  跳过该私有 post-update 方法；普通 AI、玩家、移动帧更新和完整武器实体均不改动。
- 该补丁复现原方法的 detachment 更新和 `UpdateFormationOrders()`，只省略最后的
  `SetFormationInfo(...)` 原生调用；不调用 `TryToWieldWeaponInSlot`、
  `TryToSheathWeaponInHand`，不生成或复制实体，不改写武器统计。诊断新增一次性
  `AI_FORMATION_INFO_BYPASSED`，用于确认补丁实际命中及清空时序是否消失。
- `dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj --no-restore
  -p:DeployToLiveModule=true`：0 errors、44 条既有 nullable warnings。已自动部署到
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`；本轮尚未启动游戏，等待
  自定义战斗实测左手剑是否保持出鞘、攻击和格挡。

## 2026-08-27 双刀 AI 原生用途标识对齐（待实机验收）

- 上一轮实机诊断已确定：双刀 AI 在 `WieldInitialWeapons()` 结束时同时持有
  `Weapon1` 主手和 `WeaponItemBeginSlot` 副手；随后约两秒内 `actualOff` 变为 `None`，没有
  `TryToSheathWeaponInHand()` 或托管 `OnWieldedItemIndexChange` 作为触发入口。跳过
  `Agent.EnforceShieldUsage()` 只能证明阵型盾牌约束不是唯一清空路径，不能证明原生 AI 武器选择
  已识别双刀用途。
- 对照 ROT v1.3.15 的 XML，发现当前 GreyWarden 使用了自定义的
  `gwp_dual_shield*`、`gwp_1h_with_dual_shield` 和 `gwp:dual:shield*` 标识，而 ROT 使用
  `dual_shield*`、`1h_with_dual_shield` 和 `dual:shield*`。这些字符串会进入原生动作/武器用途
  数据，可能是 native AI 非盾牌副手资格判断的硬编码识别点；ROT DLL 本身没有 NPC 双持补丁，
  因此这次先恢复其原生用途命名，保留 GreyWarden 自己的动作内容和模型。
- 已将 `weapon_descriptions.xml` 的 `item_usage_features`、`item_usage_sets.xml` 的四个用途
  集及其 `base_set`/`require_left_hand_usage_root_set`、`full_movement_sets.xml` 的根集和
  `movement_sets.xml` 的八个移动集 ID 对齐 ROT。`items.xml`、装备槽位、剑鞘、ItemFlags、
  自定义攻击动作和碰撞补丁未改变；没有新物品、复制实体、Mission tick 重挂、强制拔刀或
  持续维持副手。
- 诊断日志现在在每次 Mission 开始时截断为单一会话，并按 Agent 手部状态去重记录
  `AI_SHIELD_ENFORCEMENT_BYPASSED`，避免旧版高频阵型调用把日志增长到数 MB；状态改变时仍会
  保留完整装备快照。下一次测试重点看 `WIELD_INITIAL_POSTFIX` 后副手是否仍被清空，以及
  `actualOff=WeaponItemBeginSlot` 时左剑是否出鞘、AI 是否使用双持攻击/格挡。
- `dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj --no-restore
  -p:DeployToLiveModule=true`：0 errors、44 条既有 nullable warnings。仓库 `_Module` 与实机
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden` 的四个双刀
  XML、两个 README、`SubModule.xml` 均哈希一致；三个 XML 解析成功。诊断 DLL 的 Debug 输出
  与实机客户端 DLL 均为 `825344` 字节，SHA-256 为
  `F059D89B251DF713E5DDF2A3D9C5CC53DB2C121CA27AD1517A745855987DADEE`。
- 本轮没有启动游戏，也没有制作正式 ZIP。若本次实机仍出现 `actualOff=None`，下一步应围绕
  原生 AI 的 `UpdateWeapons`/武器选择路径继续做单入口观测，不恢复已失败的统计伪造、混合盾牌
  数据或 Mission tick 强制重挂。

## 2026-08-27 双刀 AI 阵型盾牌约束旁路（待实机验收）

- 对照 Bannerlord 1.4.8 托管代码重新检查 AI 生成后的第一条阵型路径，定位到此前遗漏的明确入口：
  `ArrangementOrder.Rearrange()` 和 `Agent.UpdateFormationOrders()` 会对每个 AI 调用
  `Agent.EnforceShieldUsage()`。该方法直接进入原生层执行盾牌副手整理，不经过
  `TryToSheathWeaponInHand()`；这与诊断中“`WieldInitialWeapons()` 已成功持有双刀，约 `0.37s` 后没有收刀
  请求却把 `actualOff` 清成 `None`”的时序一致。玩家控制器不受 AI 阵型盾牌约束，因此玩家链始终正常。
- 撤回并完整删除装备同步统计代理：不再 Harmony 修改
  `Agent.EquipItemsFromSpawnEquipment`、`MissionWeapon.GetWeaponData()` 或
  `MissionWeapon.GetWeaponStatsData()`，不再向原生层发送混合的剑/盾 flags 与虚构耐久。由此也移除了上一版接战前
  `TaleWorlds.Native.dll` 空指针崩溃的触发数据。
- `GwpDualBladeAiNativeSyncPatch.cs` 现只 patch `Agent.EnforceShieldUsage()`：当 Agent 为 AI，并且
  `Weapon0/Weapon1` 精确为 `gwdualbladeoffhand/gwdualblademainhand` 时跳过这一次盾牌专用阵型约束；所有其他
  Agent 和所有原版盾牌仍执行原版方法。补丁不修改物品、统计、模型、剑鞘、动作或控制器，不调用拔刀/收刀，不生成
  实体，也没有 Mission tick 重挂。双刀仍由 `WieldInitialWeapons()` 一次正常装备，攻击、防御、移动和目标选择仍交给
  原版 AI 与现有双持动作资源。
- 开发诊断新增 `AI_SHIELD_ENFORCEMENT_BYPASSED`，记录 Agent、阵型要求方向和调用当时主副手槽。下一次实机需确认：
  接战前不再崩溃；日志出现旁路事件后 `actualOff` 持续为 `WeaponItemBeginSlot`；副手剑与剑鞘正确；普通士兵和 NPC
  领主会使用双持移动、攻击与四向格挡；左手 bone 20 攻击能实际命中。若旁路后仍出现 `actualOff=None`，新的状态变化
  时间点将证明还存在另一条原生武器选择入口，不能重新使用统计伪装或强制重挂。
- `Release -t:Rebuild --no-restore` 已通过，`0` error、`44` 条既有 nullable warning。独立 Harmony 预检进程对
  `GwpDualBladeAiShieldEnforcementPatch` 执行 `CreateClassProcessor(...).Patch()`，成功生成
  `Agent.EnforceShieldUsage_Patch1`，排除补丁签名或目标解析导致启动报错。实机客户端与编辑器 DLL 均为
  `782848` 字节，SHA-256 均为
  `E1B379F8E500FA6031D7E38C1D13DC4F349540C3EE42523CDF2B9D2BEE427DB7`。仓库 `_Module` 的 `36` 个普通客户端
  部署文件与实机缺失 `0`、哈希差异 `0`，ModuleData 的 `25` 个 XML/XSLT 解析失败 `0`，中英文 README 与实机
  哈希一致，`git diff --check` 通过。本轮仅部署诊断开发版，不制作正式 ZIP。

## 2026-08-27 AI 原生装备同步耐久资格实验（失败，已撤回）

- 重新检查上一轮原生统计代理后发现决定性遗漏：`gwdualbladeoffhand` 是锻造剑，托管
  `MissionWeapon` 的 `_dataValue/_modifiedMaxDataValue` 均为 `0`。上一轮只把传入原生层的 flags 从
  `MeleeWeapon` 改为 `HasHitPoints | CanBlockRanged`，但 `WeaponData.DataValue` 与
  `WeaponStatsData.MaxDataValue` 仍为 `0/0`；原生层因此可能将它视为已经损坏的盾牌并立即移除。这与当时
  “副手剑和剑鞘一起消失”的实机现象吻合，不能再用该现象证明统计代理路线本身无效。
- 新增 `GwpDualBladeAiNativeSyncPatch.cs`。补丁只在
  `Agent.EquipItemsFromSpawnEquipment` 正在同步一个 AI Agent，并且该 Agent 的 `Weapon0/Weapon1` 精确为
  `gwdualbladeoffhand/gwdualblademainhand` 时进入线程局部作用域；玩家与任何其他装备不进入该作用域。
- 作用域内只改写交给原生 `WeaponEquipped` 的数据副本：副手的 `WeaponData.DataValue` 和
  `WeaponStatsData.MaxDataValue` 同时设为 `500`，统计 flags 清除 `WeaponMask` 后加入
  `HasHitPoints | CanBlockRanged`。物品 ID、`WeaponClass=OneHandedSword`、`gwp_dual_shield` usage、伤害、
  模型、剑鞘和实际托管 `MissionWeapon` 均不变；没有新物品、武器实体生成、拔刀/收刀调用、Mission tick 重挂或
  强制维持副手。
- 开发诊断新增 `AI_NATIVE_SYNC_BEGIN/END`、`AI_NATIVE_WEAPON_DATA_PROXY` 和
  `AI_NATIVE_WEAPON_STATS_PROXY`，会记录原始与传给原生层的 flags、当前耐久和最大耐久。下一次实机必须确认：
  游戏和 Custom Battle 正常启动；`actualOff` 在原生 AI 接管后不再变为 `None`；副手剑与剑鞘显示正确；NPC 能用
  双持移动、攻击与四向格挡；左手 bone 20 攻击能正常命中。未完成这些检查前不得在 README 宣称修复已验收。
- `Release -t:Rebuild --no-restore` 已通过，`0` error、`44` 条既有 nullable warning。独立 Harmony 预检进程对
  `Agent.EquipItemsFromSpawnEquipment`、`MissionWeapon.GetWeaponData` 与
  `MissionWeapon.GetWeaponStatsData` 三个补丁逐一执行 `CreateClassProcessor(...).Patch()`，三个目标均成功生成
  replacement method，排除了补丁参数签名导致启动时直接报错的风险。实机客户端与编辑器 DLL 均为
  `785408` 字节，SHA-256 均为
  `DCB330BB6859B4FC0F552EA6FF6196A8A15FAB9B85A2160B2C7C9AAD9432D295`；仓库 `_Module` 的 `36` 个
  普通客户端部署文件与实机缺失 `0`、哈希差异 `0`，ModuleData 的 `25` 个 XML/XSLT 解析失败 `0`，
  `git diff --check` 通过。本轮只部署诊断开发版，不制作正式 ZIP。
- 后续版本虽然改成保留 `MeleeWeapon`，但整条统计代理路线已由上方“阵型盾牌约束旁路”取代并删除；这些字段实验仅作为
  失败记录保留，不代表当前实现。

## 2026-08-27 原生统计代理导致接战前 Native 崩溃（失败，已撤回）

- 用户实测本轮版本“进入游戏正常，接战前弹出”。双刀诊断日志显示所有 `gwdualbladeguard` 均完成
  `AI_NATIVE_SYNC_BEGIN → AI_NATIVE_WEAPON_DATA_PROXY → AI_NATIVE_WEAPON_STATS_PROXY → AI_NATIVE_SYNC_END`，
  并在 `WIELD_INITIAL_POSTFIX` 看到主、副手均已拔出；补丁没有托管异常或作用域泄漏。
- Windows WER 事件 `Application Error 1000`（`2026-08-27 12:25:09`）确认故障模块为
  `TaleWorlds.Native.dll`，异常 `0xc0000005`。CDB 对
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.46268.dmp` 的
  `!analyze -v` 显示 `READ_ADDRESS=0x8`；崩溃指令位于 `TaleWorlds.Native+0x73ddf8`，其调用栈是一个
  原生 `std::vector` 复制路径，源对象指针为 null。该崩溃发生在最初装备同步约一分钟后、接战准备阶段。
- 结合崩溃路径与前一版的统计代理，已确认不能在传给原生层的副手统计中清除 `MeleeWeapon`：虽然这会满足
  托管 `WeaponComponentData.IsShield` 的“无 WeaponMask”条件，但原生后续双持动作/碰撞仍需要近战数据，
  缺失会留下不完整对象并触发 Native 空指针。该行为不是可接受的“玩家与 AI 同动作”。
- 崩溃后曾生成“保留 `MeleeWeapon`，仅叠加 `HasHitPoints | CanBlockRanged` 并补 `500` 耐久”的候选 DLL，
  但在它产生新的实机日志前即定位到更直接的 `Agent.EnforceShieldUsage()` 阵型入口。为避免继续向原生层发送未经定义的
  混合统计，该候选与整个装备统计代理已经撤回；当前 DLL 不再包含这些补丁。
- 重新构建后实机客户端与编辑器 DLL 均为 `785408` 字节，SHA-256 均为
  `4432401069E7356C87406084D54FC8A5B7B13A45ED260111EDEE46627E5B2582`；仓库 `_Module` 的 `36` 个普通
  客户端部署文件与实机缺失 `0`、哈希差异 `0`，ModuleData 的 `25` 个 XML/XSLT 解析失败 `0`。

## 2026-08-27 单次 Mission 级副手重试（失败，已撤回）

- 实机日志确认上一版 `Agent.OnWieldedItemIndexChange` 监听从未触发（`NPC_OFFHAND_REISSUE_QUEUED=0`），因为
  Bannerlord 的非托管 AI 清理副手时直接改写索引，不进入该托管回调。
- 新增 `GwpDualBladeNpcWieldBehavior` 并注册到每场 Mission。Agent 生成时按装备格登记 AI 双刀对象（不能依赖
  此时尚未完成的主手索引）；Mission 时间超过 `0.5s` 后，每 `0.1s` 仅观察一次状态，首次发现主手仍为双刀主手而
  副手被清空，就通过 `Mission.AddTickAction(TryToWieldWeaponInSlot)` 对原有副手槽提交一次标准原版拔刀请求。
  该 Agent 随后不再重试，未创建武器、未复制实体、未持续重挂。
- 该行为在进入 Custom Battle 时导致游戏直接弹出，已删除 `GwpDualBladeNpcWieldBehavior.cs` 及其注册；不能作为
  NPC 双持实现，也不应继续部署。当前 AI 仍在约出生后 0.4 秒由原生层清空副手。

## 2026-08-27 AI 双持实现边界复核

- 用户要求让 NPC 与玩家共用现有双剑、使用同一套双持动作。重新反编译 Bannerlord 1.4.8 的 `Agent`、`Equipment`
  和 `HumanAIComponent` 后确认：`Agent.WieldInitialWeapons()` 会按 `HeldInOffHand` 正确选择并交给副手，
  但后续原生 AI 的武器选择在 C++ 层执行，没有托管的 `HumanAIComponent` 虚方法或公开回调可改写“非盾副手”资格。
- 实机诊断已复现同一顺序：`actualOff=WeaponItemBeginSlot` 成功维持约 0.37 秒，随后变为 `None`；没有
  `TryToSheathWeaponInHand`、没有模组收刀调用，说明不是动画、物品格、模型或剑鞘资源问题。
- `MissionEquipment.ContainsShield()`、`MissionWeapon.IsShield()` 和 `GetWeaponStatsData()` 的 Harmony 改写均未改变
  原生 AI 决策；其中清除 `MeleeWeapon` 以伪造盾牌资格会破坏副手剑/剑鞘挂接，已撤回。给同一物品追加第二用途也
  无效，因为 `MissionWeapon.IsShield()` 对多用途物品不成立。
- 因此在当前纯 C#/Harmony/XML 范围内，不能证实存在“玩家双持链直接扩展到普通 AI”的安全实现。继续尝试 Mission
  tick、强制重挂或生成替代实体都会偏离用户要求，并已在本轮导致崩溃或物品消失。若要继续，下一阶段必须研究
  原生 AI 选择函数的版本固定 Native DLL hook；这不是 ROT 已提供的功能，也不是普通 Modding Kit XML 导入能完成的。
- 证据来源：本地 `rot_decompile/ROT.HarmonyPatches.Core/DualWieldingPatches.cs`（仅玩家装备校验及碰撞放行）、
  `.codex_tmp/mountandblade148-decomp/TaleWorlds.MountAndBlade/Agent.cs`（`WieldInitialWeapons` 与原生调用边界）、
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-DualBlade-Diagnostics.log`（实际索引变化）。

## 2026-08-27 原生 AI 清理后的单次副手重试

- 新实测日志确认 `WoodenParry` 已进入副手物品（`spawnFlags=ForceAttachOffHandPrimaryItemBone, WoodenParry, HeldInOffHand`），
  但 NPC 仍在约 `0.82s` 后由非托管 AI 清掉 `actualOff`；全程没有 `TryToSheathWeaponInHand`，说明引擎直接重置了副手索引。
- ROT DLL 与 XML 没有任何 NPC/AI 拔刀补丁；其双刀实现只覆盖玩家控制器，因此不能通过继续添加 ROT XML 找到 NPC 兼容开关。
- 新增 `GwpDualBladeNpcWieldPatch`：仅针对同时装备 GreyWarden 主、副手双刀且为 AI Agent 的对象，监听原生
  `Agent.OnWieldedItemIndexChange`。当引擎首次清空副手后，向同一 Mission 队列加入一次原版
  `TryToWieldWeaponInSlot(WeaponItemBeginSlot, Instant)`，每个 Agent 每场战斗最多一次。没有创建武器、复制实体、
  每帧轮询或持续重挂；调用仍是 Bannerlord 自己的拔刀接口。现有诊断的 `WIELD_REQUEST` 会记录该重试。
- 这是针对 1.4.8/1.4.9 非托管 AI 分支的兼容性补偿，未声称 ROT 原生支持 NPC 双持。下一次测试需确认单次重试后
  `actualOff` 是否保持、左手模型是否显示，以及不会造成战斗启动崩溃或副手剑鞘消失。

## 2026-08-27 ROT 原生副手标志对齐与统计层回滚

- 用户实测上一版后确认副手武器与剑鞘直接消失，玩家双持仍可用，NPC 仍不能使用副手。
- 失败方案 `GwpDualBladeNativeWeaponStatsPatch` 已删除。它对 `MissionWeapon.GetWeaponStatsData()` 清除
  `MeleeWeapon` 并加入 `HasHitPoints | CanBlockRanged`，虽然试图伪造盾牌资格，但会破坏原生武器/剑鞘
  挂接链，不能继续使用。此前的 `MissionWeapon.IsShield` 与 `MissionEquipment.ContainsShield` postfix 也一并
  删除，因为日志证明它们返回 `true` 后 NPC 仍在 `WieldInitialWeapons` 后清空副手，且会污染正常盾牌判断。
- 对照 ROT v1.3.15.3 的 `dual_blades2`，当前 GreyWarden 副手锻造刃已有
  `ForceAttachOffHandPrimaryItemBone` 与 `HeldInOffHand`，但缺少 ROT 明确声明的 `WoodenParry`。在
  `gwp_crafting_pieces.xml` 为同一副手刃补上 `WoodenParry`，不新增物品、不改 WeaponStatsData、不生成或
  重挂实体；该标志通过原版 CraftedItem 生成链传递到现有 `gwdualbladeoffhand`。
- 这次只完成 XML 原生标志对齐，尚未有新的实机验收。下一次测试必须同时确认：副手剑鞘仍存在、玩家双持不受影响、
  `gwdualbladeguard` 和自定义战斗指挥官的 `actualOff` 不再被原生 AI 清空。未确认前不得宣称 NPC 双持已修复。
- `Release -t:Rebuild --no-restore` 成功，`0` error、`44` 条既有 nullable warning。诊断 DLL SHA-256 为
  `3D4975BE9449DFF5BDCB8AF28022F0E0642C9681114F4ECB274DB89733CBD053`，仓库构建输出与实机
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden\bin\Win64_Shipping_Client\GreyWardenPolicePurity.dll`
  哈希一致；副手锻造片、两份 README 与实机哈希一致，ModuleData XML/XSLT 解析失败 `0`。本轮未制作正式 ZIP。

## 2026-08-27 原生武器统计层副手资格放行

- 用户再次实测确认仅修改托管层 `MissionWeapon.IsShield()` 与
  `MissionEquipment.ContainsShield()` 仍无效：最新日志中两项均为 `true`、`hasShieldCached=true`，但
  `gwdualbladeguard` 约一秒后仍把 `actualOff` 清为 `None`。这证明原生 AI 使用的是装备同步时的
  `WeaponStatsData.WeaponFlags`，没有读取 Harmony 改写后的托管布尔结果。
- 在同一 `gwdualbladeoffhand` 物品上新增 `MissionWeapon.GetWeaponStatsData()` postfix：只对该物品的唯一
  用途清除原生 `WeaponMask`（`MeleeWeapon`），加入 `HasHitPoints | CanBlockRanged`，让引擎统计层把它视为
  可长期占用副手的盾牌资格；`ItemUsageIndex`、`gwp_dual_shield`、剑模型、动作、伤害和物品 ID 均保持不变。
  这不是新物品，也不生成/重挂实体，不向 Agent 发出拔刀或收刀命令。
- 该统计层修改对玩家和 NPC 共用同一物品，因此两条链使用完全一致；自定义战斗 `commander_2` 仍使用
  `Item0=gwdualbladeoffhand`、`Item1=gwdualblademainhand`。专用诊断继续保留，用于确认原生层最终不再
  清除副手以及双刀动作是否仍正常。

## 2026-08-27 同物品 AI 副手判断放行与自定义战斗双刀指挥官

- 用户否决 NPC 专用第二物品，要求玩家与所有 NPC 继续共用现有 `gwdualbladeoffhand`，并指出应绕过
  原版 AI 的盾牌副手限制，而不是把剑改造成盾。按此约束，没有新增 CraftedItem、WeaponDescription、
  CraftingTemplate 或装备实体，也没有修改现有副手剑的 `OneHandedSword + MeleeWeapon + HeldInOffHand` 数据。
- 新增 `GwpDualBladeAiQualificationPatch.cs`，只在原版两个判断入口
  `MissionWeapon.IsShield()` 与 `MissionEquipment.ContainsShield()` 对精确物品 ID
  `gwdualbladeoffhand` 返回副手资格。物品模型、单用途剑属性、`gwp_dual_shield` usage、伤害、动作与玩家
  输入链保持原样；不调用 `TryToWieldWeaponInSlot`，不在 Agent 生成后补拔刀，不重挂实体，也不持续控制
  AI。现有专用监控暂时保留，用下一次实测确认原生 AI 接管后是否还会把 `actualOff` 清为 `None`。
- 自定义战斗 `commander_2` 按用户最新要求改为
  `Item0=gwdualbladeoffhand`、`Item1=gwdualblademainhand`，移除其灰袍单手剑、骑枪与黑曜大盾，使玩家链与
  普通双刃卫士 AI 链可在同一自定义战斗入口分别复测。
- `Release -t:Rebuild --no-restore` 构建成功，`0` error、`44` 条既有/nullable warning。客户端与编辑器
  DLL 均为 `782848` 字节，SHA-256 均为
  `BC33C124A400550334D2AFC9F838D1E2137806C03F033D86903DF53E6A7CB3E4`。ILSpy 已确认两个补丁只改写
  原版布尔判断的 postfix 结果，资格 helper 只扫描现有 MissionEquipment，不含 Agent 拔刀、收刀、生成、
  重挂或物品修改调用。自定义战斗武器格结构检查为精确的双刀 `Item0/Item1`；ModuleData 的 `25` 个
  XML/XSLT 解析失败 `0`，仓库 `_Module` 的 `36` 个客户端部署文件与实机缺失 `0`、哈希差异 `0`，
  `git diff --check` 通过。本轮未制作正式 ZIP，结果仍需由保留的专用监控进行实机验收。

## 2026-08-27 双刃卫士副手第二用途失败与无干预监控

- 用户实测确认 `GwpDualBladeUsageInitializer` 的“剑用途后追加盾牌用途”方案没有改变 AI 行为：
  双刃卫士仍只拔主手，副手剑继续留在鞘内；自定义战斗现有指挥官已经按此前要求恢复剑、骑枪和盾，
  本身没有双刀，因此不能用来复测玩家链。该结果推翻了 README 中“AI 副手资格已补全”的描述。
- 反编译补充确认 `MissionWeapon.IsShield()` 只有在用途数恰好为 `1` 时才会返回唯一用途的
  `WeaponComponentData.IsShield`。失败候选把副手剑扩成两个用途，所以即使
  `MissionEquipment.ContainsShield()` 能看到其中的盾牌用途，运行时物品本身仍不是真盾牌；该候选不能
  继续作为修复基础。
- 已完整删除 `GwpDualBladeUsageInitializer.cs` 及 `AfterRegisterSubModuleObjects()` 中的调用，恢复原本
  单用途副手剑数据；两份玩家 README 同步撤掉未经实测成立的 AI 修复声明。保留玩家双持物品、动作、
  左手碰撞骨骼兼容和兵种装备，不加入替 AI 补拔刀、收起重挂或武器实体生成。
- 按用户要求进入重新取证阶段。新增仅在开发诊断构建存在的
  `GwpDualBladeDiagnostics`/`GwpDualBladeDiagnosticBehavior`：单独写入
  `%USERPROFILE%\\Documents\\Mount and Blade II Bannerlord\\GreyWarden-DualBlade-Diagnostics.log`，记录双刃
  卫士生成后的四个武器格、物品 flags、全部用途、原版盾牌判定、原版初始主副手选择、实际持握槽，
  并旁路记录 `WieldInitialWeapons`、`TryToWieldWeaponInSlot` 与 `TryToSheathWeaponInHand` 的调用。
  监控不修改返回值、不调用拔刀/收刀、不改装备；状态轮询只接触已经完成 `OnAgentBuild` 的目标 Agent，
  并只在主副手槽发生变化时写日志，避免恢复此前会访问未完成 Agent 并导致自定义战斗崩溃的诊断方式。
- `Release -t:Rebuild --no-restore` 构建成功，`0` error、`44` 条既有/nullable warning。实机客户端与
  编辑器 DLL 均为 `781824` 字节，SHA-256 均为
  `3D4975BE9449DFF5BDCB8AF28022F0E0642C9681114F4ECB274DB89733CBD053`；ILSpy 类型清单包含五个新的双刀
  诊断类型且不再包含 `GwpDualBladeUsageInitializer`。仓库 `_Module` 的 `36` 个普通客户端部署文件与
  实机缺失 `0`、哈希差异 `0`，ModuleData XML/XSLT 共 `25` 个且解析失败 `0`，中英文 README 已同步到
  实机。本轮是开发诊断部署，没有创建正式 ZIP；玩家包构建关闭 `GWP_DIAGNOSTICS` 时只保留空 Behavior，
  不包含文件写入或 Harmony 诊断补丁。
- 用户于 `2026-08-27 00:51` 完成一名 `gwdualbladeguard` 的自定义战斗复测，专用日志为
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-DualBlade-Diagnostics.log`（本次
  `5994` 字节）。`AGENT_BUILD` 时四格装备已完整落入 Agent：`slot0=gwdualbladeoffhand`、
  `slot1=gwdualblademainhand`，副手 flags 为 `ForceAttachOffHandPrimaryItemBone | HeldInOffHand`；原版初始选择
  为 `selectedMain=Weapon1`、`selectedOff=WeaponItemBeginSlot`。
- `WieldInitialWeapons` postfix 在 `00:51:29.699` 明确记录
  `actualMain=Weapon1`、`actualOff=WeaponItemBeginSlot`，证明副手并非“出生时没有生成”或“初始拔刀请求
  失败”。约 `0.37` 秒后的 `00:51:30.073`，同一 Agent 在装备和物品数据完全未变的情况下变成
  `actualMain=Weapon1`、`actualOff=None`；全程
  `spawnContainsShield=False`、`missionContainsShield=False`、`hasShieldCached=False`。因此可复现根因是
  原生 AI 接管后主动清除不具盾牌资格的副手近战剑，而不是模型、剑鞘、装备格或初始选择错误。
- 诊断 Harmony 没有记录到模组侧 `SHEATH_REQUEST`，现有 GreyWarden 代码也没有对该 Agent 发出收刀；
  最新 `rgl_log_11668.txt` 与 `rgl_log_errors_11668.txt` 没有 GreyWarden、动作、XML 或托管异常。监控本身
  没有造成预览缺人或战斗崩溃。下一修复必须让 NPC 使用的副手在原版 AI 看来是单用途真盾牌，或找到
  等价的原版资格数据；再次追加第二用途不能解决 `MissionWeapon.IsShield()` 的单用途限制。

## 2026-08-26 双刃卫士原版 AI 副手资格补全

- 用户再次实测确认玩家出生时两把剑均自然在手、背部副手剑鞘为空；同一装备的普通 AI 只持主手，
  左手为空且副手剑模型仍留在背部剑鞘。结合此前 `WieldInitialWeapons()` 前后均为
  `selectedOff/actualOff=WeaponItemBeginSlot` 的证据，问题已收敛为 AI 接管后的副手资格差异，而非装备格、
  `HeldInOffHand`、模型加载或出生时漏调拔刀。
- v1.4.8 反编译确认 `Agent.HasShieldCached` 调用 `MissionEquipment.ContainsShield()`；后者遍历物品的全部
  `WeaponComponentData`，只要任一用途的 `IsShield=true` 即可。`IsShield` 的精确条件是没有
  `MeleeWeapon/RangedWeapon` 掩码并同时具有 `HasHitPoints | CanBlockRanged`。现有副手剑只有
  `OneHandedSword + MeleeWeapon`，所以玩家输入可以使用，原版 AI 却没有盾牌副手资格。
- 重新完整反编译 ROT v1.3.15.3（`ROT.dll` SHA-256
  `4C416526ECB272164FFE4A85E7C451EA458AABF49F33E002E8AA9A3DA0423FCF`）确认，ROT 没有 Agent、AI、
  `WieldInitialWeapons` 或武器选择补丁。其 `DualWieldingPatches` 只在玩家物品栏检查第一格
  `dual_shield` 与第二格 `dual_shield_thrust` 是否配对；运行时唯一相关补丁是对
  `AttackBoneIndex == 20` 放行。测试副手 `dual_blades2` 本身明确仍为
  `OneHandedSword + MeleeWeapon + HeldInOffHand`，并非真正盾牌。玩家成立依靠的是原版玩家控制器接受
  `HeldInOffHand` 以及 `dual_shield base_set=hand_shield`；GreyWarden 已逐项复制这条数据链，因此不存在
  一段遗漏的 ROT 玩家拔刀代码可直接扩大到 NPC。
- 没有直接采用“在 `weapon_descriptions.xml` 加第二用途”的表面方案。原版
  `Crafting.CraftedItemGenerationHelper.SetWeaponData()` 对非投掷锻造用途把 `MaxDataValue` 固定为 `0`；若仅用
  XML 添加 `HasHitPoints`，生成的盾牌用途耐久仍为 `0`，会成为出生即损坏的无效用途。
- 新增 `GwpDualBladeUsageInitializer.RegisterAiShieldUsage()`，在
  `AfterRegisterSubModuleObjects()` 中、注销双刀锻造模板之前执行一次。它保留现有
  `gwdualbladeoffhand` 的第一用途 `OneHandedSword + MeleeWeapon + gwp_dual_shield`，再通过原版公开
  `WeaponComponentData`/`ItemObject.AddWeapon()` API 追加 `SmallShield + HasHitPoints + CanBlockRanged` 用途，
  复制原剑的 usage、伤害、速度、长度和 frame，并赋予有效耐久。`MissionWeapon` 默认仍从第一用途开始，
  玩家现有双持链和模型不变；AI 可由原版多用途选择与 `HasShield` 资格自行决定副手。
- 本方案不包含 `Agent.WieldInitialWeapons()` Harmony 补丁、Mission tick 轮询、生成后收剑再拔、实体补建、
  每帧重挂或任何 AI 强制控制。仍需用户实机确认 AI 是否会切换到新增盾牌用途并真正显示左手剑，以及
  左手攻击、四向格挡和玩家双持是否保持正常；确认前不得把实机结果记录为已验证。
- `Release -t:Rebuild --no-restore` 构建成功，`0` error、`43` 条既有 nullable warning。实机客户端与
  编辑器 DLL 均为 `776704` 字节，SHA-256 均为
  `57E535CA8F04BE87C0EE0D266B5CB6F0ABFCCCBBA2027C9F59304C6777FAF631`。ILSpy 已确认新增类型只调用
  `WeaponComponentData.Init`、`SetFrame`、`SetAmmoOffset` 与 `ItemObject.AddWeapon`，不含
  `TryToWieldWeaponInSlot`、`WieldInitialWeapons`、`AddTickActionMT`、Mission tick 或补装备实体调用。
  仓库 `_Module` 的 `36` 个普通客户端部署文件与实机缺失 `0`、哈希差异 `0`；两份 README 哈希一致，
  ModuleData XML/XSLT 解析失败 `0`，实机仍无 `Assets`、`AssetSources`、`RuntimeDataCache`。本轮未制作正式
  ZIP，当前玩法结果等待用户实机验收。

## 2026-08-26 双刃卫士副手不可见与诊断回滚

- 用户已稳定复现 `gwdualbladeguard` 普通 AI 只拔主手、始终不拔副手。静态配置复核确认兵种装备仍为
  `Item0=gwdualbladeoffhand`、`Item1=gwdualblademainhand`；副手锻造剑刃也仍带
  `HeldInOffHand` 与 `ForceAttachOffHandPrimaryItemBone`。v1.4.8 托管代码中
  `Equipment.GetInitialWeaponIndicesToEquip()` 会按 `HeldInOffHand` 选出副手，
  `Agent.WieldInitialWeapons()` 也确实先请求副手、再请求主手，因此问题不能归因于装备格顺序或漏掉
  副手标志。
- ROT v1.3.15.3 的双刀只存在于物品 XML 与玩家物品栏检查中；其兵种/领主装备表没有双刀测试对象，
  DLL 也没有让普通 AI 拔副手的兼容层。ROT 证明的是玩家双持动作链，不证明 Bannerlord 普通战斗 AI
  会把 `OneHandedSword + MeleeWeapon` 当成可持续使用的副手。v1.4.8 的盾牌判断仍读取
  `WeaponComponentData.IsShield`；GreyWarden 副手剑和 ROT 原物一样不是原版盾牌，仅继承
  `hand_shield` usage。因此当前高概率原因是原生 AI 选武器时排除了“不是盾牌的副手近战剑”，但仍需
  实测手部索引确认它究竟在初始请求时失败，还是先拔出后被 AI 收回。
- 首版本地诊断在 `Agent.WieldInitialWeapons()` 前后记录手部索引，并于 Mission tick 轮询后续变化。
  用户启动自定义战斗后出现人物预览只剩武器、进入场景时直接报错退出。现场日志为
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_22880.txt` 与
  `watchdog_log_22880.txt`；崩溃发生在大批 Agent 尚未全部构建完成时，轮询已访问这些未完成对象。
  该诊断行为和 Harmony 诊断补丁已完整删除，不能把访问未完成 Agent 的 Mission tick 轮询留在实机。
- 崩溃前已经取得决定性证据：`gwdualbladeguard` 的每个样本在
  `WieldInitialWeapons()` prefix 都是 `selectedMain=Weapon1`、`selectedOff=WeaponItemBeginSlot`；postfix
  都是 `actualMain=Weapon1`、`actualOff=WeaponItemBeginSlot`。副手最终物品标志同时显示
  `ForceAttachOffHandPrimaryItemBone,HeldInOffHand`。因此原版初始选武器和逻辑拔出均成功，用户看到的
  “生成时左手为空”不是 AI 没选 Item0，而是逻辑副手索引已经成立后，左手武器实体/骨骼挂接没有显示。
  后续调查应对比玩家手动拔刀与 AI `isWieldedOnSpawn=true` 的实体挂接路径；不得再按“副手索引为空”
  设计修复，也暂不把剑改成真正盾牌。
- 回滚后 `Release -t:Rebuild --no-restore` 为 `0` error、`43` 条既有 nullable warning；实机客户端与
  编辑器 DLL 均恢复为 `774656` 字节，SHA-256 均为
  `B41F158125B5F2BBBB384995A2B25EDA7B257CC88CCF4C4726738F7347C1AC1F`。ILSpy 类型清单不再包含
  `GwpDualWieldDiagnostic*`；仓库 `_Module` 的 `36` 个可部署文件与实机缺失 `0`、哈希差异 `0`，
  `git diff --check` 通过。此次只撤回开发诊断并记录结论，没有改变玩家玩法，不更新玩家 README，
  也没有制作正式 ZIP。
- 用户随后明确拒绝任何“生成后收起再重新挂接”或其他强制生成方案，要求 NPC 与玩家一样依靠同一套
  原版机制自然成立。上述 `Agent.WieldInitialWeapons()` postfix、下一帧 tick action 和对应中英文玩家
  日志已全部撤回，不能把它们当成完成方案。后续调查必须比较玩家与 NPC 的原版装备/控制器数据差异，
  找到造成原版路径分叉的配置或标志；除现有左手碰撞骨骼兼容外，不得靠补拔刀、补实体或持续控制来
  掩盖差异。
- 被拒绝方案的构建记录仍保留作失败证据：首次编译因 v1.4.8 枚举应写作 `Agent.HandIndex` 出现
  `CS0103`，没有部署；修正后曾产出 SHA-256
  `677E7919FF1F3BC4C8DB3C4BD60FF91C0E250A34F2B5EE37C2941F2B23C72213` 的 DLL，随后按用户要求撤回，
  不得恢复。

## 2026-08-26 Bannerlord v1.4.8 沙盒锻造订单崩溃与双持范围收口

- 用户在启动兼容修复后已能进入游戏，但新开沙盒仍于战役创建阶段直接报错退出。最新日志为
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_13040.txt`，完整转储为
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.13040.dmp`。
  WinDbg/CDB 确认托管异常为 `InvalidOperationException: Sequence contains no matching element`，调用链为
  `CraftingCampaignBehavior.GetWeaponPieces -> CreateTownOrder -> OnNewGameCreatedPartialFollowUpEnd`。
  根因不是 ROT 或双持动画加载：原版创建城镇订单时从 `CraftingTemplate.All` 随机抽中了
  `GwpOneHandedSwordDualOffhand`，但其 Blade 只有带 `is_hidden="true"` 的专用副手剑刃，原版随后执行
  `First(p => !p.CraftingPiece.IsHiddenOnDesigner)` 时找不到候选并崩溃。
- 保留两个专用 `CraftingTemplate` 的 XML，因为 `items.xml` 必须先用它们反序列化固定的主手和副手成品；
  在 `SubModule.AfterRegisterSubModuleObjects()`（所有静态物品注册完成之后）再从 `MBObjectManager` 注销
  `GwpOneHandedSwordDualOffhand` 与 `GwpOneHandedSwordDualMainhand`。反编译 v1.4.8 已确认
  `CraftingTemplate.All` 每次直接读取 `MBObjectManager.GetObjectTypeList<CraftingTemplate>()`，而双刀
  `ItemObject.WeaponDesign` 已持有模板对象引用。因此注销后固定双刀仍可装备、保存和成为战利品，但模板
  不再进入玩家锻造界面、城镇订单或 `CraftingCampaignBehavior` 的零件字典。
- 双持装备范围按用户要求收口：`gwheavyinfantry` 恢复开发前的锤、灰袍剑和大盾并保持独立终阶；
  `gwdualbladeguard`（灰袍双刃卫士）改为直接从 `gwrecruit`（轻步兵）分出的独立高阶路线，不再是
  重步兵的后继；通用领主模板 `spc_gw_leader_0` 与自定义战斗
  `commander_2` 恢复原来的灰袍剑、骑枪和黑曜大盾。新增 `spc_gw_leader_dual`，仅初始领主
  `gw_leader_0`（凡蒂/Aethelflaed）与 `gw_leader_5`（暮光/Wulfhild）引用。其余四名初始领主和后续
  成年灰袍继续使用普通领主模板。
- 用户首次观察双刃卫士 AI 时怀疑其没有拔出副手剑，但同时说明可能看错并准备再次测试；按用户明确要求，
  本轮只改兵种树，没有修改物品格、AI、动作、usage、武器标志或双持补丁。待稳定复现后再根据具体场景
  （开战前/接敌后、第一格是否出鞘、主手是否攻击）继续取证，不能据单次观察先行改动第二项。
- 加入和重新加入灰袍仍统一调用 `GiveCommanderEquipment()`；新增 `MembershipGrantItemIds`，在原指挥官
  套装之外各发放一把 `gwdualbladeoffhand` 与 `gwdualblademainhand`。悬赏着装资格继续只检查
  `CommanderSetItemIds`，不会强迫玩家装备双刀。
- 市场规则最终按用户要求改回纯原版实现：`items.xml` 中全部 `26` 件灰袍武器、护甲、盾牌与马铠
  均使用 `is_merchandise="false"`，由该原版字段阻止它们进入城镇商品自动生成池；不新增交易方向拦截、市场库存
  扫描或战利品过滤，也不从玩家行李删除物品。已经写出的 `GwpExclusiveItemTradePatch` 与
  `GreyWardenExclusiveItemBehavior` 已完整撤销，SubModule 不再注册市场清理行为。玩家持有和主动卖给
  城镇均交回 Bannerlord 原版库存流程处理。进一步反编译 v1.4.8
  `DefaultBattleRewardModel.GetLootedItemFromTroop()` 后确认，原版 `GetRandomItem()` 也明确以
  `!equipment[i].Item.NotMerchandise` 过滤装备掉落；战败队物品库存的分配同样排除
  `NotMerchandise`。因此“纯原版非商品字段”与“仍能作为常规战利品掉落”在当前版本不能同时满足。
  按用户最后明确的纯原版要求，本轮不写战利品例外补丁；当前实际结果是不会自然刷新到市场，也不会
  作为常规装备战利品掉落，但玩家持有和卖出不受本模组额外限制。
- 静态与部署验证：`Release -t:Rebuild --no-restore` 为 `0` 错误、`43` 条既有 nullable 警告；
  实机客户端与编辑器 DLL 均为 `774656` 字节，SHA-256 均为
  `29DA7362B659B92FA2AA45C82504D85AC2DECC4494AFC3B88E218E32DF533DBF`。ILSpy 完整反编译退出码
  `0`，产物包含 `AfterRegisterSubModuleObjects` 的模板注销和 `MembershipGrantItemIds` 发放路径，且不含
  自定义交易补丁或市场扫描行为。ModuleData 的 XML/XSLT 共 `25` 个，.NET XML 解析失败 `0`；
  `items.xml` 共 `26` 件物品，缺少 `is_merchandise="false"` 的数量为 `0`；双持领主精确为
  `gw_leader_0`、`gw_leader_5`，普通重步兵、双刃卫士和自定义战斗装备均通过结构检查。仓库 `_Module`
  的 `36` 个可部署文件与实机缺失 `0`、哈希差异 `0`，中英文 README 哈希一致，实机不存在 `Assets`、
  `AssetSources` 或 `RuntimeDataCache`；`git diff --check` 通过。普通开发构建未创建正式 ZIP。尚未代替
  用户实机新开沙盒，因此当前只确认崩溃根因与静态修复链闭合，仍需游戏内验证新战役创建、双刃卫士
  升级、入会发放以及市场不自然刷新灰袍装备。

## 2026-08-23 GreyWarden 独立双持剑原型实施与部署

- **2026-08-26 Bannerlord v1.4.8 启动兼容修复：**游戏升级后最新
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_30176.txt` 的实际启动模块只有
  `Native`、`SandBoxCore`、`BirthAndDeath`、`CustomBattle`、`FastMode`、`Sandbox`、
  `StoryMode`、`NavalDLC` 与 `GreyWarden`，没有加载 ROT；ROT-Core/ROT-Content 保留
  `v1.3.15.3` 只作为双持机制取证来源，不属于本轮兼容范围。游戏 Build Version 为 `119303`，
  Native 为 `v1.4.8`。崩溃的直接异常是 `ReflectionTypeLoadException`：旧实机 DLL 中
  `GreyWardenSafePartyAgentOrigin` 未能按新版元数据实现
  `IAgentOriginBase.get_IsInSameArmyAsPlayer()`，继而被启动器报告为 GreyWarden 依赖冲突。
- 源码本来已有 `IsInSameArmyAsPlayer` getter，因此不能靠重复添加属性修复；必须针对 v1.4.8
  程序集完整重编译。重编译同时暴露并迁移了本次官方 API 变化：战场刷兵实现改为
  `DefaultBattleMissionAgentSpawnLogic`；收养儿童的初始装备改用直接返回 `Equipment` 的
  `GetEquipmentForInitialChildrenGeneration()`；军团接触距离按实际场景分别使用新版陆地和海上
  属性。最终 `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条既有 nullable 警告并自动
  部署到实机客户端与编辑器目录。
- 新实机客户端与编辑器 DLL 均为 `775680` 字节，SHA-256 均为
  `CF08AB31A922B3DD4A173B836BF23402EC294A05BBBB5FFA222A6FAA33D5F1A2`。反射
  `GetInterfaceMap(IAgentOriginBase)` 已确认新版 DLL 的
  `Boolean get_IsInSameArmyAsPlayer()` 精确映射到同名目标 getter，不再复现日志中的缺失实现。
  对实机 DLL 执行 `Assembly.GetTypes()` 已完整载入 `391` 个类型，无 loader exception；仓库
  `_Module` 的 `36` 个正常客户端文件与实机相比缺失 `0`、哈希差异 `0`，`29` 个 XML/XSLT
  解析失败 `0`，实机也没有 `Assets`、`AssetSources` 或 `RuntimeDataCache`。`git diff --check`
  通过。尚未代表用户启动游戏；下一步由用户实机确认能否越过模组加载并进入主菜单。普通开发
  构建未创建正式 ZIP。

- **2026-08-26 首次实机动作测试与副手物品修复：**用户确认缩短 action type ID 后游戏已能进入，
  自定义战斗装备格也正确显示双剑；但实际拔出时播放盾牌动作/声音，左手剑不可见，随后主手与
  副手均无法攻击或防御，只剩移动有效。四套 GreyWarden item usage set 去除独立前缀后，与
  ROT 的 `dual_shield_swing`、`dual_shield_swing_thrust`、`dual_shield_thrust`、
  `dual_shield` 逐节点完全一致；八套 movement set 同样逐项一致，因此问题不在动作输入表的
  漏抄。
- 反编译当前 `TaleWorlds.Core.Crafting` 确认，锻造物品的 `ItemFlags` 只取剑刃部件的
  `CraftingPiece.AdditionalItemFlags`。ROT 的副手锻造剑使用专门 `_duel` 剑刃，其 XML 明确带
  `ForceAttachOffHandPrimaryItemBone` 与 `HeldInOffHand`；GreyWarden 首版却让副手模板直接复用
  普通 `vlandian_blade_3`，最终生成物因此仍是普通主手剑。它虽然得到继承 `hand_shield` 的
  `gwp_dual_shield` usage，却没有真正成为副手装备，准确解释了左手空手、盾牌拔出反馈及主副手
  输入停住。
- 新增 `ModuleData\gwp_crafting_pieces.xml` 与 `CraftingPieces` SubModule 注册，定义隐藏的
  `gwp_vlandian_blade_3_dual`：网格、剑鞘、长度、重量及伤害参数仍全部复用 Native
  `vlandian_blade_3`，只增加上述两个 ROT 同款副手标志，不调用 ROT 模型。副手物品与其模板、
  weapon description 改用该专用剑刃；主手继续使用普通 `vlandian_blade_3`。副手根 usage 覆盖
  装备/收起音效为剑声。按用户要求，自定义战斗 `commander_2` 已移除 `gwlance`，武器格只剩
  `gwdualbladeoffhand` 与 `gwdualblademainhand`；战役装备未按此句扩大修改。
- Release 构建为 `0` error、`0` warning。静态核验确认副手最终剑刃带
  `ForceAttachOffHandPrimaryItemBone,HeldInOffHand`，主手仍为普通剑刃，自定义战斗长矛为 `0`；
  全部 ModuleData XML/XSLT/项目文件解析失败 `0`。仓库 `_Module` 的 `36` 个可部署文件与实机
  缺失 `0`、哈希差异 `0`，新增 crafting pieces SHA-256 为
  `D63E477BD6DB8D593EC3BA258E831687F161E2AE7B051675F72A7B008B0C5AF7`。实机没有 `Assets`、
  `AssetSources` 或 `RuntimeDataCache`。仍需用户下一轮实机确认双剑可见、攻击/防御输入与副手
  碰撞；尚未把行为手感标记为完成。

- **2026-08-26 启动崩溃修复：**首版部署后用户确认游戏在启动阶段直接报错。最新
  `rgl_log_26308.txt` 显示 GreyWarden 的 `project.mbproj` 被读取后，进程停在
  `reading action files!` 并由 watchdog 捕获崩溃；没有进入 GreyWarden DLL 的战役初始化。
  首轮修复确认首版只新增了 `action_sets.xslt` 和各数据 XML，却漏了在既有
  `ModuleData\project.mbproj` 中注册 `action_set`、`animation_combat_parameters`、
  `action_type`、`item_usage_set`、`item_holster`、`movement_set` 与
  `full_movement_set`。用户随后指出以往动画导入均由官方 Modding Kit 完成；进一步逐项对照
  Native、ROT-Content、ROT-Dragon 与 ArtemsCinematicCharges 后发现，所有声明
  `type="action_set"` 且可用的模块均同时存在实体 `ModuleData\action_sets.xml`，而首轮修复后的
  GreyWarden 是唯一声明该资源却仍缺少目标文件的模块。因此不能把“补七项注册”单独宣称为
  完整修复。现新增有效的空根文件 `action_sets.xml` 作为本模块 action-set 资源入口；具体对
  Native `as_human_warrior` 的增量继续由 `action_sets.xslt` 注入，避免在实体文件与 XSLT 中
  重复定义同一批 action。必须在 Native 动作表离线变换、action type/animation 引用闭合检查、
  构建部署完成后，再由用户实机确认是否越过 `reading action files!`；在此之前不宣称已修复。
  离线使用 .NET `XslCompiledTransform` 将本模块 XSLT 作用于当前 Native `action_sets.xml` 成功，
  合并结果包含 `84` 个 GreyWarden action，且 `action_types.xml` 中相应定义缺失 `0`。
  TpacTool 重新读取 `gwp_dual_wield_animations.tpac` 成功，仍可枚举 `68` 个资产。最终 Release
  构建为 `0` error、`0` warning；仓库 `_Module` 的 `35` 个可部署文件与实机相比缺失 `0`、
  哈希差异 `0`，`23` 个动作/模块 XML、XSLT 与项目文件解析失败 `0`。新
  `action_sets.xml` 的仓库/实机 SHA-256 均为
  `06C5509045556C00081B93395A2E84CD863419F6ACA9FAA7320ACED8B99A1E60`，TPAC 仍为
  `652634753C25CEFBD2547B8AC49D26A493C28C896225EF074665D9864E727F2B`，实机诊断 DLL 仍为
  `31D5529F5DE1FD1BB6AE856B2199F1CC892CF3027770FAB4B14FBE8C0B2664D5`。实机未保留
  `Assets`、`AssetSources` 或 `RuntimeDataCache`。尚未代表用户启动游戏，仍需实机启动验证。
  用户第二次启动仍然崩溃；新日志 `rgl_log_30924.txt` 已越过旧日志的
  `reading action files!`，明确成功打开 GreyWarden `action_types.xml`，随后才在原生模块/人体
  资源加载阶段终止。进一步检查发现首版为隔离 ROT ID 而统一加入的 `gwp_` 前缀使最长 action
  type 达到 `66` 字符；当前 Native 最长 action type 为 `63` 字符，ROT 最长为 `62`。这正好
  越过引擎动作 ID 的 64 字节存储边界，并与“读完 GreyWarden action types 后立即原生崩溃、
  没有托管异常”的时序一致。现将动作 ID 缩短为仍与 ROT 隔离的 `act_gwd*` / `gwd_shld`
  前缀，最长降为 `61`；XSLT 的 `84` 个新增 action、action type 声明及 item usage 的 `105` 个
  唯一动作引用重新核验后均缺失 `0`。Release 构建为 `0` error、`0` warning，仓库与实机
  `35` 个可部署文件缺失 `0`、哈希差异 `0`；客户端与编辑器 DLL 均为
  `31D5529F5DE1FD1BB6AE856B2199F1CC892CF3027770FAB4B14FBE8C0B2664D5`。此项修复仍需下一次
  启动日志确认。

- 根据用户确认直接复刻 ROT 双持机制，但不使用 ROT 武器模型。新增
  `gwdualbladeoffhand` 与 `gwdualblademainhand` 两件锻造物品，均复用
  `gwonehandedsword` 的 `vlandian_blade_3`、`vlandian_guard_8`、
  `sturgian_grip_36`、`empire_pommel_6` 四个部件；物品仍为灰袍专属且不进入市场。
  第一武器格固定副手、第二武器格固定主手。重步兵、战役领主模板与自定义战斗指挥官已换成
  该配对，第三格保留原有备用武器或骑枪。
- 新增独立前缀的数据链：`gwp_dual_shield*` item usage、`act_gwp_dual_*` action type、
  `gwp_1h_with_dual_shield` movement set、`gwp_*dual*` combat parameter、
  `GwpOneHandedSwordDualOffhand/Mainhand` 锻造模板与武器描述，以及 `gwp_dual_back`
  收纳位。武器描述用 `gwp:dual:shield` 和 `gwp:dual:shield:thrust`，按原版
  `Crafting.GetItemUsage()` 的冒号拆分/下划线拼接规则精确生成两件物品所需 usage ID。
- 从 ROT `pack0` 至 `pack7` 只抽取名称或混合源引用包含 `dual` 的 64 个
  `AnimationClip`，再按 GUID 闭包加入 4 个 ROT 自定义 `SkeletalAnimation`；另有 3 个底层
  animation GUID 属于游戏原版依赖，未复制。产物为
  `_Module\AssetPackages\gwp_dual_wield_animations.tpac`，共 68 项、`1,076,983` 字节，
  SHA-256 `652634753C25CEFBD2547B8AC49D26A493C28C896225EF074665D9864E727F2B`。
  数字名平衡混合 clip 与其源 clip 保留原内部名字/GUID，避免破坏引擎计算出的混合动作索引；
  对外 item/action/usage/movement/combat/template ID 均已隔离。四个自定义 combat parameter
  引用在抽取时改为 `gwp_` 前缀。
- `.codex_tmp\tpac-diagnose\Program.cs` 新增 `--extract-dual`。初次尝试把所有外部段反序列化时，
  TpacTool 因 ROT 当前优化动画变体报 `Frames not equal`；最终采用只解析 TPAC 头、对所需外部段
  做原压缩字节复制的方式，避免重编码关键帧。输出重新读取成功，包 GUID 为
  `94ca4c3d-685b-4cae-9f10-67edc31bacdf`，68 个资产均可枚举。
- 新增 `GwpDualWieldCollisionPatch` 后置修补
  `MissionCombatMechanicsHelper.IsCollisionBoneDifferentThanWeaponAttachBone`。只在原版已判定骨骼
  不匹配、`AttackBoneIndex == 20` 且命中来源为固定副手第一武器格时放行；没有照搬 ROT 对所有
  bone 20 攻击全局放行的宽补丁。
- 当前 2026-08-23 游戏程序集已移除旧的 `DefaultBattleMissionAgentSpawnLogic`、海陆分离军团接触
  距离属性与直接生成幼儿装备方法。为恢复本轮构建，只做等价 API 迁移：决斗使用
  `MissionAgentSpawnLogic`；军团接触使用统一的
  `MaximumAllowedDistanceForEncounteringMobilePartyInArmy`；收养装备从
  `GetEquipmentRostersForInitialChildrenGeneration()` 的首个 roster 取得默认装备。双持修改前的
  R10 工作树曾因此无法针对当前实机程序集编译。
- 所有新增 XML 与 XSLT 已通过 .NET XML 解析；项目 Release 构建 0 error、44 个既有 nullable
  warning。普通开发构建已把诊断版 DLL 与 `_Module` 同步到
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`；未制作正式 ZIP。
  最终增量构建为 0 error/0 warning，实机诊断 DLL SHA-256 为
  `31D5529F5DE1FD1BB6AE856B2199F1CC892CF3027770FAB4B14FBE8C0B2664D5`。仓库 `_Module`
  中排除编辑器目录后的 34 个可部署文件逐一与实机比较，缺失 0、哈希差异 0；实机只有
  `AssetPackages`、`bin`、`GUI`、`ModuleData`、`ModuleSounds`、`Shaders`，没有会让普通客户端
  绕过 TPAC 的 `Assets`/`AssetSources`。ILSpy 反编译实机 DLL 已确认窄碰撞补丁条件实际进入产物。
  仍需实机进入战斗确认资源注册、第一/第二格配对、左挥/下刺、副手真实命中、四向格挡和 AI
  使用效果，不能仅凭静态解析宣称最终手感已经验收。

## 2026-08-23 ROT 双持武器机制取证（只读调查，尚未实施）

- 调查对象为实机 ROT `v1.3.15.3`：
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ROT-Content` 与
  `ROT-Core`。本轮没有修改 ROT、GreyWarden 玩家内容或实机模块，也没有构建、部署或制作 ZIP。
- ROT 的“双手剑”实为两件一手剑配对的真正双持。主手测试物品为 `dual_blades`，仍是
  `OneHandedSword`，但使用 `item_usage="dual_shield_thrust"`；副手为 `dual_blades2`，同样是
  `OneHandedSword`，使用 `item_usage="dual_shield"`，并带
  `HeldInOffHand`、`ForceAttachOffHandPrimaryItemBone` 和 `WoodenParry`。副手不是
  `SmallShield`/`LargeShield` 伪装，也没有盾牌耐久或 `CanBlockRanged`，所以其核心是近战武器
  格挡与攻击，不应误当成能像盾牌一样挡箭的剑形盾。
- `ModuleData\item_usage_sets.xml` 只新增四个双持相关用法集：`dual_shield_swing`、
  `dual_shield_swing_thrust`、`dual_shield_thrust` 与 `dual_shield`。副手根集
  `dual_shield` 继承原版 `hand_shield`，让引擎接受副手装备和格挡状态；主手用法在检测到左手
  根集为 `dual_shield` 时，为左向挥砍改用 `act_dual_*slashleft*`，为下向刺击改用
  `act_dual_*thrust*`。其余挥砍继续使用主手动作。格挡仍定义上、下、左、右四向，因此玩家看到
  的是两把剑共同构成一套方向攻击/方向格挡，而不是新增一个独立的“副手攻击键”。
- 流畅动作来自完整数据链，不是物品 XML：`action_types.xml` 定义准备、快速准备、释放、受阻和
  卡住等双持动作类型；`action_sets.xslt` 把对应动画注入原版 `as_human_warrior`；
  `movement_sets.xml` 与 `full_movement_sets.xml` 定义步行、奔跑、蹲走、蹲跑及左右架势；
  `item_usage_sets.xml` 再把攻击方向、手臂/手掌起止位置和动作绑定起来。正常客户端只提供
  `AssetPackages\pack0.tpac` 至 `pack7.tpac`，动画片段在 TPAC 中以大量散列名存储，未提供可直接
  维护的 FBX/Blender 动画源文件。复制架构可行，但 GreyWarden 若要独立发布，应制作并发布自己
  的双持动画资源，不能指望只复制 XML 后复用原版动作就获得同样的副手命中与流畅度。
- 反编译
  `ROT-Core\bin\Gaming.Desktop.x64_Shipping_Client\ROT.dll` 证明还需要两个 Harmony 行为。
  `DualWieldingPatches` 本身不生成攻击，只在物品栏检查配对：第一武器格必须是
  `dual_shield` 副手，第二格必须是 `dual_shield_thrust` 主手，否则禁用完成按钮并显示错误。
  真正让副手命中成立的是
  `IsCollisionBoneDifferentThanWeaponAttachBonePatch`：它后置修补
  `MissionCombatMechanicsHelper.IsCollisionBoneDifferentThanWeaponAttachBone`，当
  `AttackCollisionData.AttackBoneIndex == 20` 时强制接受该碰撞。原版会把这个副手攻击骨骼与
  默认武器挂接骨骼不一致判为无效；因此漏掉此补丁时可能有左手挥剑动画，却不会形成可靠的真实
  武器碰撞。实现 GreyWarden 版本时应把补丁限制在本模组双持用法/物品，不能像 ROT 当前实现那样
  对所有骨骼索引 `20` 的攻击全局放行，以降低与其他动画模组的冲突面。
- ROT 还定义了锻造模板 `OneHandedSwordDual` 和武器描述特征
  `item_usage_features="dual:shield"` / `dual:shield:thrust`，说明正式玩法不是只能使用两把测试
  剑，而是分别锻造副手与主手并按固定武器格配对。当前 ROT 内用于快速实测的明确 ID 为
  `dual_blades2`（第一格/副手）与 `dual_blades`（第二格/主手）。
- 非枪械的其他特殊武器机制中，代码证据最明确的是 `giant_club` 与 `ice_spear`。
  `ROTWeaponComponetData` 给巨人棍动态加入 `CanCrushThrough`、`AffectsArea`、
  `CanKnockDown`、`CanPenetrateShield` 和 `MultiplePenetration`，并修补碰撞反应与剩余动量，使其
  能击穿、击倒并继续传递伤害；冰矛被加入 `MultiplePenetration`。这两项比普通高数值武器更
  接近可移植的特殊机制。Winterfell/Castle Black/The Wall 场景中隐藏生成的
  `stark_sword_1`、`ranseur`、`woodland_longbow`、`weirwood_bow` 只是彩蛋拾取/入库逻辑，未发现
  独立战斗机制。龙焰、龙骑和战车也有成套任务逻辑与专用动画，但属于载具/怪物战斗系统，不是
  适合与双持并列复制的普通武器用法。
- 可行性结论：GreyWarden 可以实现无 ROT 运行依赖的双持，最小完整范围是两类配对物品、四个
  item usage set、双持 action types、注入人类 action set 的 XSLT、四种移动状态及左右架势、
  原创动画 TPAC、受限的副手碰撞补丁，以及装备格配对验证。C# 工作量较小，动画制作、调参和
  实机碰撞验证是主要成本。若先做技术原型，可暂用 GreyWarden 自有剑网格和最少一组左挥/刺击
  动画验证副手命中，再扩展到完整移动与受阻动作；正式实现前需先确定给哪类灰袍角色、是否允许
  玩家使用、是否允许骑乘，以及副手剑是否只挡近战。

## 2026-08-17 工作区体积审计：垃圾来源、可再生性与删除边界

- 本轮先完成只读盘点，再经用户确认执行下述第一阶段清理。清理前工作区约 `12.6 GiB`，
  但当前 Git 跟踪文件只有
  `124` 个、工作树内容约 `5 MiB`；体积增长不是源码造成，而是正式发行过程中反复保留
  staging、ZIP 解包验收目录、反编译输出、外部工具副本和未压缩 Git 对象造成。
- `build-check` 为 `7,813.08 MiB`。其中每个 `package-*`/`release-stage-*` 是正式玩家包
  staging，每个 `extract-*`/`verify-*` 是 ZIP 解包后的逐文件验收副本，`release-player-*`
  是 `GwpDiagnosticsEnabled=false`、`DeployToLiveModule=false` 的独立玩家 DLL 构建，
  `matrix147`/根目录 DLL、PDB 是兼容性或早期构建输出。这里共有二十九份相同的
  `gwp_inherited_legacy_assets.tpac` 和二十八份相同的 `gwp_black_gold_shield.tpac`；所有
  staging/验收目录中的两个 TPAC 均分别匹配权威哈希
  `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40` 和
  `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B`。
- 最新 `build-check/release-stage-v1.4-r9/GreyWarden` 与
  `build-check/verify-package-v1.4-r9/GreyWarden` 均为 `27` 个文件，并与本地正式
  `GreyWarden-v1.4-r9.zip` 比较得到缺失 `0`、额外 `0`、哈希差异 `0`。正式 ZIP 的
  SHA-256 仍为 `FD06FFB28B90F218A7522155BB0BAFC44B3B271D093F063CF2C69D284DDE7146`，
  与旁置校验文件一致；包内玩家 DLL 哈希
  `226270E8A6E8151DB378B8AF398EB9FD1B4A412F68447D777DF35A8957AC15CF` 也与
  `release-player-v1.4-r9` 一致。GitHub 已确认 r3、r4、r5、r6、r7、r8、r9 的正式
  Release ZIP 和校验文件仍在线且带 digest。因此 `build-check` 全目录是已验证可删除的
  构建/发行中间产物；删除不会改变仓库源码、实时测试模块或正式发行资产。执行删除时须在
  本节记录完成时间，不能删除游戏 `Modules` 父目录中唯一保留的 r9 ZIP/校验文件。
- `.codex_tmp` 实际约 `4,031 MiB`，不能整目录删除。约 `2,832 MiB` 的
  `release-v1.4.7*`、`package-20260717-000742`、`package-extract-check-*`、
  `verify-v1.4.7-*`、`verify-readme-r2` 和 `verify-doc-rule-r2` 是已被后续正式版本取代的
  staging、ZIP 和解包验收副本，可删除。`TaleWorlds-Documentations` 为干净的上游 Git
  clone，HEAD `08f74df3f3ce6f80cc18bb30c0418bea8688710d`，可重新 clone，故其
  `791.92 MiB` 可删除或移到仓库外共享缓存。`ilspy`、`py`、`harmony242`、
  `harmony-smoke` 和仅剩 `bin/obj` 的 `PatchProbe` 是可重新安装/构建的工具缓存，也可删除。
  其余以 `decompile*`、`deployed*`、`*-live*`、`*-il*`、`*-build*` 命名的目录主要是
  已安装游戏程序集或本模组 DLL 的反编译/探针输出；结论已写入本维护文件，但删除会失去
  原始取证便利性，统一归为“先归档文本证据，再删除生成目录”，不与大包副本一起盲删。
- `.codex_tmp/TpacTool-src` 不是纯缓存：上游 clone HEAD
  `b56b77ad273ba67192b1594dbb2eeca8c542b3b7` 上有两处未提交修改，分别给
  `ExternalLoader.cs` 增加 `DebugGetRawData()`，以及在 `AssetPackage.cs` 中增加
  `TPACDBG` 元数据诊断和损坏偏移恢复；`.codex_tmp/tpac-diagnose` 依赖该改版。
  在把两处 diff 和诊断项目提取到持久工具目录前，这两个目录不得删除。
- `.codex_tmp/published-assets` 和 `.codex_tmp/pre-six-lod-package` 是三个互不相同、也不等于
  当前正式盾牌 TPAC 的历史发布/回滚资产，SHA-256 分别为
  `031DC4430DA668D1FAB30C57518F99C19A762A99AD938A8D4C8558D417AD1E6A`、
  `D14AE4B3F8576F963C9BF3B0A829402206F2EDDCE464202A24340612CFA1C287` 和
  `1606CAD209D02A33AC75F5FA1F4E3726882F6985B2979046C34017D09A28955B`。
  它们是盾牌资源故障的回滚点，不属于垃圾，不得删除。三张 UI 截图、monitor 原始日志和
  crash 分析文件同样不是可再生内容；它们只可在另有带哈希归档并更新绝对路径后移出仓库。
- 仓库 `_Module/Assets`/`AssetSources` 不是外置编辑器工作区的重复备份。与
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\_GreyWardenEditorWorkspace`
  比较时，文件名集合相同，但 `Assets` 有三个文件哈希不同，`AssetSources` 也有三个文件
  哈希不同；外置工作区另有可再生的 `RuntimeDataCache`。两套代表不同的可恢复编辑状态，
  当前均保留。仓库和实时模块中的两个正式 TPAC 哈希完全一致，也必须保留。
- `.vs` (`8.41 MiB`)、`.codegraph` (`8.26 MiB`) 和项目 `obj` (`4.26 MiB`) 分别由
  Visual Studio 索引、Codex codegraph 索引和 MSBuild 中间编译产生；确认相关进程关闭后可删，
  下次打开/构建会自动重建。它们不包含源码。
- `.git/objects` 为 `608.64 MiB`。其中十五个 `tmp_obj_*` 共 `199.93 MiB`，全部生成于
  2026-07-14 至 2026-07-15，名称不是合法对象 ID，是当时向对象库写入大文件时中断留下的
  临时文件，可在没有 Git 进程运行时直接删除。另有 `4,343` 个有效但不可达的 blob/tree/tag；
  它们不影响当前分支，却可能用于恢复过去未提交或重写前的内容。在当前 R10 修改提交或制作
  补丁备份前不执行 `git gc --prune=now`，避免把“当前功能不引用”误当成“没有恢复价值”。
- 第一阶段零功能影响清理边界因此为：整个 `build-check`；上述明确列出的旧发行/验收副本和
  可重装工具缓存；关闭进程后的 `.vs`、`.codegraph`、`obj`；以及十五个无效
  `tmp_obj_*`。按本次逐项统计，第一阶段可释放 `11,754.13 MiB`（`11.48 GiB`）。明确暂缓：
  TPAC 调试工具改版、兼容性审计的自定义 csproj/探针输入、历史盾牌
  回滚资产、原始截图/日志/crash 证据、两套不同编辑工作区，以及全部有效不可达 Git 对象。
- 第一阶段执行完成：十八个目标路径和十五个 `tmp_obj_*` 已全部删除，残留目标 `0`。
  `.codegraph/codegraph.db` 初次因本仓库专属 Codegraph worker 占用而未删除；确认 PID `12752`
  的命令行准确指向本仓库、不是 Codex 主进程后，仅停止该索引子进程并删除可再生索引。
  工作区清理后为 `1,209,162,239` 字节（`1,153.15 MiB`，`1.13 GiB`），实际释放
  `11,754.08 MiB`（四舍五入为 `11.48 GiB`）。`git count-objects` 的 `garbage=0`、
  `size-garbage=0 bytes`；未对 `4,343` 个有效不可达对象执行 GC。
- 删除后保护项复验通过：仓库两个正式 TPAC、r9 正式 ZIP/校验文件和三个历史盾牌回滚 TPAC
  的七个 SHA-256 均与删除前一致；`TpacTool-src` 的两处未提交修改及 `tpac-diagnose`、
  `compat-audit` 均仍存在；仓库两套编辑素材目录和外置 `_GreyWardenEditorWorkspace` 均仍存在。
  游戏 `Modules` 父目录仍恰好只有 r9 的一个 ZIP/校验文件对，仓库与实时模块的中英文 README
  哈希一致。本次没有构建、部署、制作 ZIP 或改变任何玩家行为。

## 2026-08-06 R10 结案后立即恢复和平（修复“帮灰袍打本国罪犯后灰袍转身打玩家”）

- 根因：灰袍追捕罪犯时由 `DeclareWar` 对罪犯所属整个国家宣战；战斗结束后
  `PoliceAntiWarDeclaration.OnBattleEnded` 先于 `PoliceEnforcementBehavior.OnMapEventEnded`
  结案执行，检查 `HasLegitimateWarReason` 时案件仍在案卷中，于是保留战争。案件随后关闭，
  但战斗胜利结案路径没有像协力失败/玩家请求让位那样在结案后复查和平，导致灰袍仍与玩家
  所在国家保持战争；刚并肩作战的承办领主就在玩家旁边，下一轮 AI 直接发起第二场战斗对话。
- 修复：新增 `PoliceEnforcementBehavior.RestorePeaceAfterCaseEnd(PoliceTask?)`，统一在
  `CrimeState.EndTask` 之后调用——若 `WarTarget` 已无任何合法执法理由（其他案件/悬赏/纠察
  仍针对该势力时保留战争），立即 `GwpCommon.TrySetNeutral` 恢复中立。已接到 `UpdateTasks`
  的三处无效/失活结案、`OnMapEventEnded` 的承办人消失/胜利/战败三条分支，以及
  `FailTaskBecauseOwnerCannotLead`（先捕获任务再 EndTask 再查和平）。玩家押送分支不调用，
  押送期间战争必须保留。玩家 README v1.4-r10 已补对应条目。
- 构建与部署验证：`dotnet build -c Release` 成功，`0` 错误、`44` 条既有警告；仓库 `_Module`
  与实机 `24` 个普通客户端运行文件 `缺失=0、哈希差异=0`；实机客户端 DLL SHA-256
  `8FC050E5040719FCE097510D948C564EEE79C063DE6338E17ED67EA95D3FFB6F`，二进制包含
  `RestorePeaceAfterCaseEnd`。用户将实测“帮灰袍打本国罪犯后不再被立刻攻击”。

## 2026-08-06 R10 实机验证：结案和平修复通过；关闭报错继续观察

- 用户实机验证“帮灰袍打本国罪犯”场景：结案后灰袍不再立刻转身攻击玩家，核心修复通过。
  验证场景同时包含黑金盾与每两天派出的纠察队（可能带 NavalDLC 船）；用户在该纠察队回到
  出生点消除之前保存并退出游戏，未出现关闭报错。关闭报错本轮未复现。
- 按用户决定本轮不做任何隔离改动（临时队停船/移除 TPAC 的诊断变体 A/B 挂起），继续正常
  游玩观察。若关闭报错再次出现：弹窗时选择生成报告/转储、不要取消；记录退出前最后一分钟
  正在做什么（大地图/对话/战斗/切磋/是否有纠察队在场），保留新转储并与既有
  `0x74b1f0/0x74b34a/0x74b3f1` 同族偏移比对。
- 本轮无源码、构建、部署或玩家 README 改动；仓库与实机仍保持上一轮验证状态
  （DLL SHA-256 `1BC286E0D4DCD34DE00B815172C6C90CB0024172E9C1DA432220DEFFAFFE6238`，
  24 个运行文件缺失 0、哈希差异 0）。

## 2026-08-06 R10 关闭游戏报错：旧线索汇总与本次复发取证

- 用户确认该报错仅在本模组启用后偶尔出现、原版不出现。维护计划历史已有完整探索：
  （1）2026-07-16 启动方式隔离：中文站 Mod Manager 的 `ModMasterStarter.bat` 直接以
  `/anticheat` 且按自定义顺序启动 `Bannerlord.exe` 时，两次灰袍单模组运行都在原生 teardown
  （`Managed Interface deleted` 之后）以 `TaleWorlds.Native.dll 0xc0000005` 崩溃；同一构建
  用官方启动器顺序、不带 `/anticheat` 的两次运行均无 WER 崩溃，只打印无危害的
  `Non-Zero Device Reference Count (ERC1513/ERC1567)`。（2）2026-07-18/07-20 同族偏移
  `0x74b1f0/0x74b34a/0x74b3f1` 在原生 teardown 复现，无托管栈；已分别修掉黑金盾运行时
  网格/材质变更、协力军团无首领原生状态、野战未完成时按住 Tab 刷消息三个具体入口。
- 本次复发取证：今天 `10:29:56` 用户关闭游戏后，Windows 事件日志在 `10:32:55` 记录
  `TaleWorlds.MountAndBlade.Launcher.exe` 以 `TaleWorlds.Native.dll`、`0xc0000005`、
  偏移 `0x000000000074b3f1` 终止——正是维护计划记录的同族关闭崩溃偏移；新转储已保留于
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.23780.dmp`
  （112822847 字节，`10:33:09`）。本机未安装 cdb/WinDbg 命令行，未做符号化分析。
- 下一步判定需用户回答两点：本次是用官方启动器/Steam 启动，还是中文站
  `ModMasterStarter.bat`（`/anticheat`）启动；弹窗文字是“Non-Zero Device Reference Count
  (ERC…)”（无危害，维护计划明确记录不构成失败）还是 Windows 崩溃窗口。若走官方启动器且
  只是 ERC 提示，按计划无需改代码；若仍有 WER 崩溃，按 07-16 结论优先改用官方启动器
  复测，并保留新转储比对偏移。

## 2026-08-06 R10 关闭游戏报错：官方启动器复现取证与隔离计划

- 用户确认一直用官方启动器/Steam 启动：正常流程是退回主菜单再点“退出游戏”，偶尔弹出
  “程序遇到问题”的 Windows 崩溃窗口，偶现、无实际损失。因此 07-16 的“仅中文站
  ModMasterStarter 才崩”结论只适用于当时两轮对照，官方启动器同样会间歇复现。
- 本次复现（PID `23780`）深挖结果：`watchdog_log_23780.txt` 确认模块列表为标准官方顺序
  （Native/SandBoxCore/BirthAndDeath/CustomBattle/FastMode/Sandbox/StoryMode/NavalDLC/
  GreyWarden v1.4.9.0），`Has Used Cheats=False`、`Game Integrity is Achieved=True`；
  `rgl_log_23780.txt` 正常走到 `Deleting resources... OK`、`Pre Finalizing Managed
  Interface... OK`、`There are no living managed objects`、`Managed Interface deleted`，
  崩溃发生在托管接口已删除之后的原生工作线程（07-20 分析同款：`TaleWorlds_Native!
  create_game_application+0x1d6300` 读取已释放对象 `[rcx+0B18h]`，栈纯原生、无托管帧）。
- 时间线反驳了“回主菜单后 1.7 秒内立刻退出”的旧假说：本次最后一次玩法活动约
  `10:29:56`，退出流程到 `10:32:48-52` 才执行，用户在菜单/与灰袍领主弥瑟的对话中停留约
  三分钟后关闭，仍然崩溃。`rgl_log` 最后活动是与灰袍领主的对话（10:28/10:29/10:31），
  与 07-18 “关闭时仍处于模组相关对话/菜单附近”的现象有相似性，但无法据此单独归因。
- 模组侧原生资源差异只剩两个可测候选：临时纠察/结算队持有的 NavalDLC 船只
  （原版不会给无英雄自定义队配船，销毁自定义队时船只归属可能悬空）；以及黑金盾
  `AssetPackages` 的 GPU 设备资源（历史 ERC1513/ERC1567 证据）。模组托管代码已无运行时
  网格/材质变更（`GwpBlackLordShieldBehavior` 已是占位类型），也未发现托管线程/异步加载。
- 隔离计划（待用户选择）：A) 出诊断变体停掉临时队配船，正常游玩并反复正常退出
  5～10 次观察是否还崩；B) 临时从实机移除两个 TPAC 包（黑金盾退回普通外观）同样反复退出
  观察。下次弹窗时务必选择生成报告/转储而不是取消，保留新转储用于比对偏移。另请记录
  退出前最后一分钟在做什么（大地图/对话/战斗/切磋）。
- 附带修复：实机日志显示市场清理打印出现负数（`removed -1/-2`），系 `AddToCounts` 返回
  负增量被累加所致，清理本身成功；已改为按移除清单累计正数。`dotnet build -c Release`
  `0` 错误、`44` 条既有警告，24 个运行文件与实机哈希一致，DLL SHA-256
  `1BC286E0D4DCD34DE00B815172C6C90CB0024172E9C1DA432220DEFFAFFE6238`。

## 2026-08-06 R10 读档/启动异常排查与市场清理加固

- 用户反馈改动后存档进不去、有报错弹窗。Windows 事件日志显示当天 `09:54` 与 `09:55`
  各有一次 `TaleWorlds.MountAndBlade.Launcher.exe` 崩溃（`0xe0434352`，.NET 未处理异常，
  KERNELBASE），且与 `08/05 02:12` 的崩溃是同一 WER 桶（`AppCrash_TaleWorlds.Mount_4c92cee…`），
  即该启动器崩溃在我们 R10 改动之前就已存在，属启动器侧间歇性问题，不是 GreyWarden 的
  模块文件导致：`SubModule.xml` 未改动，启动器不解析 `items.xml`/README/CN 文本，也不加载
  模块 DLL。同日 `GreyWarden-AI-Diagnostics.log` 在 `08:09` 后无新会话，说明游戏本体从未
  启动到战役；命令行直接启动 Launcher.exe 时正常退出（code=0），未能复现。
- 为排除“市场清理在存档加载阶段改动城镇库存”这一潜在读档风险，对
  `GreyWardenExclusiveItemBehavior` 做了加固：移除 `OnGameLoadedEvent` 钩子，清理只保留在
  `OnSessionLaunchedEvent`（新档与读档完成后都会触发）和 `DailyTickEvent`；`SweepMarkets`
  整体包 `try/catch`，任何异常只打 `Debug.Print`，绝不向战役流程抛出。这样即使清理逻辑
  在某个环境下失败，读档与日常游玩也不会被拖垮。
- 加固后 `dotnet build -c Release` 成功，`0` 错误、`44` 条既有警告；仓库 `_Module` 与实机
  `24` 个普通客户端运行文件 `缺失=0、哈希差异=0`；实机客户端 DLL SHA-256
  `0FEF55E37FDE3AF03BCADB5BABAA213098933AB25D09CB094757B83FCA6F33B6`。启动器崩溃的复现
  与进一步处理（重试启动、Steam 校验文件、清理启动器缓存）留给用户确认，本次未做任何
  删除操作。

## 2026-08-06 R10 经济方案一实施：不再免费给常驻领主发船

- 按用户选择只实施经济方案一：`PoliceResourceManager.GivePoliceShips` 增加
  `if (party.IsLordParty) return;`，常驻灰袍领主队不再由模组免费生成船只；无英雄的临时
  纠察队与悬赏结算队（`CustomPartyComponent` 创建，非 lord party）仍保留免费配船，
  它们不进入家族公库、也不参与 `SellSurplusPoliceShips` 余船出售。
- 购船能力已核实，不依赖模组：仓库代码中 `ApplyByTrade` 只出现在
  `SellSurplusPoliceShips`（卖船给船坞）；监控日志的三条购船记录
  （campaignHour=628540.03 约珥买自 town_K3、628876.21 暮光买自 town_K5、
  628924.21 圣铎买自 town_EW4，均为 `eastern_trade_ship`、`detail=ApplyByTrade`）只能是
  原版 NavalDLC 的购船决策产生。`NavalDLC.dll` 中亦存在 `BuyShip`/`BuyShipFrom`/
  `BuyShipFromTown` 方法，确认原生 AI 会在有资金和可用船坞时自行购船。
- 行为预期：新档灰袍领主落地时不带模组赠送的船，之后由原版经济在需要时购船（灰袍公库
  充裕，购船无困难）；旧档已经免费生成的船仍保留，可在人数回落时一次性出售，之后不再
  补充，造钱循环即断。玩家 README 的 v1.4-r10 条目已补“常驻队伍不再凭空获得船只”。
- 临时队伍类型全清单（核对 `IsLordParty` 守卫无遗漏）：模组全部临时队均为
  `CustomPartyComponent` 创建、无英雄领队、`IsLordParty=false`，不受新守卫影响。
  （1）纠察队 `gwp_patrol_`（PolicePatrolBehavior）创建时调用 `GivePoliceShips`，仍免费配船；
  （2）悬赏结算队 `gwp_bounty_collect_`（PlayerBountyBehavior.CollectionCourier）创建时调用
  `GivePoliceShips`，仍免费配船；（3）招募使者队 `gwp_recruit_`（PlayerBountyBehavior）只配
  二十日口粮，从不配船，陆路信使不需要船，保持不变；（4）追截支援队 `gwp_enf_delay_`
  （PoliceEnforcementBehavior.DelayPatrols）有即时骑兵截击队与普通延滞支援队两种，均只配
  口粮、从不配船，截击队还需要轻装高速，保持不变。其余“临时”职责（协力军团、村庄救济、
  重建、练兵、调兵、玩家请求）都由灰袍领主队本身执行，属于 lord party，正是本轮停止免费
  发船的对象。
- 构建与部署验证：`dotnet build -c Release` 成功，`0` 错误、`44` 条既有警告；仓库 `_Module`
  与实机 `D:\steam\...\Modules\GreyWarden` 的 `24` 个普通客户端运行文件 `缺失=0、哈希差异=0`；
  实机客户端 DLL SHA-256 `2C5674DE6919F2204059F3A5BCC7CC45B0A16AA1BBA6F18DE2A03C622B52E03F`，
  二进制包含 `IsLordParty` 守卫；`items.xml` SHA-256 不变
  `AAB699DCF6CCCCB1E8C1669F2EF85ED51DC2674AAFD743A3BDE72BF0C09FC070`。未制作 ZIP、
  未替换诊断 DLL。

## 2026-08-06 R10 实施：在案罪犯声望通道 + 专属装备市场隔离

- 玩家声望新增通道：玩家获胜且失败方包含仍有开放案卷的罪犯部队时，按玩家亲手击倒数
  累计灰袍声望，与剿匪/救援共用 `PlayerBehaviorPool.AccumulateGoodDeedKills` 的同一把
  “亲手击倒十人得一点、余数跨战斗存档保留”累计器。实现位于
  `PlayerBehaviorMonitor.TryResolveCaseCriminalReputation` / `SideContainsOpenCaseCriminal`，
  覆盖罪犯本人部队、附着目标与军团首领三种战场身份；玩家通缉身份（`IsMainParty`）被排除。
  与既有“协助灰袍抓捕”路径互斥：`_pendingPoliceCrimeSupport > 0` 时先由
  `TryResolvePendingPoliceCriminalReputation` 结算并短路；玩家帮助罪犯击败灰袍时则先命中
  `TryApplyPoliceBattlePenalty`，不会误发奖励。新增中文文本
  `gwp_playerbehaviormonitor_053/054`。中英文玩家 README 已加入 v1.4-r10 条目，并按要求
  保留 r10、r9 两条、移除 r8 条目。
- 专属装备市场隔离：`_Module/ModuleData/items.xml` 全部 `24` 件灰袍专属物品（三件锻造武器、
  十九件盔甲/盾牌、两件马铠）均已补 `is_merchandise="false"`（原版 CraftedItem 同款用法，
  见 SandBoxCore tournament_weapons.xml），阻止市场后续生成。新增
  `GreyWardenExclusiveItemBehavior`，在会话启动（含读档完成）与每日结算时扫描全部城镇
  `Settlement.ItemRoster`，把 `GwpIds.ExclusiveItemIds` 内物品清零；已在 SubModule 注册。
  清理整体捕获异常，不参与存档加载流程（见上方“读档/启动异常排查与市场清理加固”一节）。
  当前游戏版本不存在 `NotSellable` 物品旗标（已在 TaleWorlds.Core 枚举与全部原版 XML 中确认），
  因此玩家或 AI 战后缴获后主动转卖，仍可能短暂进入某城镇库存，直到当日/次日扫描清除；
  “市场不再生成”由 XML 标志保证，旧档存量清理由每日扫描保证。
- 构建与部署验证：`dotnet build -c Release` 成功，`0` 错误、`44` 条既有可空性警告（与 R9 基线一致）。
  仓库 `_Module` 与实机 `D:\steam\...\Modules\GreyWarden` 的 `24` 个普通客户端运行文件
  `缺失=0、哈希差异=0`。实机客户端 DLL 为 `775168` 字节，SHA-256
  `82BCFAC2B6FF8C310F8881F4E4FD768F7528719B3DD36289295B905C4B462BA6`，二进制中包含
  `GreyWardenExclusiveItemBehavior` 与 `SideContainsOpenCaseCriminal` 两个新类型/方法。
  `items.xml` SHA-256 `AAB699DCF6CCCCB1E8C1669F2EF85ED51DC2674AAFD743A3BDE72BF0C09FC070`，
  XML 解析确认 `24` 件物品全部带 `is_merchandise="false"`；中文语言文件解析正常。未制作 ZIP、
  未替换诊断 DLL、未发布 R10。

## 2026-08-06 R10 经济系统调整方案（待用户选定后实施）

- 依据上一节实测日志：约 28 个游戏日内公库净增 `165160`；地方请求 `16×3000=48000`，村庄重建
  净支出 `270000`，村庄保护费日结算约 `6700～6900`，余船出售 `18` 艘共 `276824`（其中免费生成
  重船 `13` 艘 `263303`）。
- 方案一（已实施，见上方“经济方案一实施”一节）：切断“免费补船→卖船”的造钱循环。
  `GivePoliceShips` 此前每小时按 `ceil(人数/50)` 用 `ApplyByMobilePartyCreation` 免费生成
  首选重船，`SellSurplusPoliceShips` 每日又按同一需求线出售多余船；日志已证实 `18` 次免费
  生成重船、其中 `13` 艘随后以约 `20250` 出售。已改为不再为常驻领主免费生成船只（无领主
  临时队仍保留补给船），预期把约 `26.33 万/28 日` 的最大异常收入直接移除。
- 方案二：协力拨款去重。`PoliceEnforcementBehavior.Assistance.CompleteAssistanceTasks` 现在
  按协力军团每个成员案卷各发一次 `3000`，一个案件会按成员数重复拨款；应改为每个案件只结算
  一次。
- 方案三：案件/请求拨款降额。`SuccessfulCaseReward` 由 `3000` 降至 `1000`（或地方请求直接
  不发公库拨款），当前约每 `1.75` 日完成一件，28 日可少约 `3.2 万`。
- 方案四：村庄保护费再降档。上次 2026-07-21 已把日结算收入下调过一次；若方案一、二落地后
  公库斜率仍偏高，再把当前约 `6700～6900/日` 的净增量按系数减半，预期 28 日少约 `9 万`。
- 建议执行顺序：先实施方案一加方案二，实测一到两周游戏时间复查 `leaderGold/clanGold` 斜率，
  仍偏高再启用方案三、方案四。公库上限、重建费用与工资储备保持不变，避免在来源未修好时
  用支出掩盖问题。上述方案均未在本轮实施，等待用户确认具体组合。

## 2026-08-06 R10 需求调查：案件目标声望、专属装备市场与司法公库暴涨

- 本轮按用户要求只调查、不改玩法代码，也未制作、部署或打包 R10。性能问题暂不进入修改范围；唯一可量化的
  开发环境现象是当前诊断日志在约五十分钟现实时间内写到 `27763733` 字节、`12611` 行，且
  `GwpAiDiagnostics.Append` 每条都同步调用 `File.AppendAllText`。正式玩家构建的诊断实现为空，因此这条证据
  只能说明本地诊断构建可能有额外 I/O 开销，不能据此认定用户观察到的性能问题来自 GreyWarden。
- 当前玩家声望的善行战斗统一按玩家亲自击倒数累计，每满十人结算一点，余数跨战斗和存档保留。
  `PlayerBehaviorMonitor.OnMapEventEnded` 已覆盖强盗、保护村民/商队、阻止烧村，以及玩家通过灰袍正在进行的
  交战界面选边后协助承办队抓捕罪犯；后者依赖 `_pendingPoliceCrimeSupport`，并要求战场中同时存在持有该案
  的灰袍队和其案件目标。玩家独自在别处攻击某个仍有开放案卷的罪犯部队时，当前没有检查
  `CrimePool.ActiveTasks` 或开放 `CrimeRecord`，所以不会因“击倒在案罪犯部队成员”获得声望。这正是 R10
  新入口的现有缺口。实现时应在玩家获胜且失败方确实包含开放案件目标时复用同一累计器，并避免与现有
  “协助灰袍抓捕”路径重复计数；案件目标并入军团或附着队伍时还需按真实参战方而非只看战役地图当前队伍引用。
- `_Module/ModuleData/items.xml` 当前定义 `24` 件灰袍专属物品（三件锻造武器、十九件盔甲/盾牌和两件马铠），
  全部带帝国文化或可交易物品的一般定义，但 `24` 件都没有 `is_merchandise="false"`。原版普通商品同样可省略
  该字段，而专用/测试装备会显式写 `false`；这解释了灰袍装备能被原版市场商品生成器选入商店。仅补 XML
  标志可以阻止新市场库存自然生成，但不能保证从战斗缴获、玩家或 AI 转卖的灰袍装备永不进入城镇库存。
  若 R10 的边界是“任何来源都不得在市场出现”，除给全部专属物品加非商品标志外，还需要在载档/会话启动
  和城镇库存更新后清除已有存档市场中的灰袍物品，不能只依赖 XML。
- 现场经济日志为
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`，本次会话覆盖战役小时
  `628310.48～628983.55`，约 `28.0` 个游戏日。司法公库首个可见余额 `2586989`，末次为 `2752149`，
  净增 `165160`；因此二百七十多万的大部分在本次载档前已经形成，但本次仍明确持续净增长。
- 用户怀疑的地方请求确实有收入：`SuccessfulCaseReward` 当前为 `3000`，圣铎在这二十八日完成
  `16` 个城镇/村庄原版请求，合计只入账 `48000`。同期晨曦完成 `10` 次村庄重建，每次先支出 `30000`、
  再回拨 `3000`，合计净支出 `270000`；所以“每个地方请求给三千”会推高公库，但不是这段现场记录中的
  最大异常来源。成功刑事案件、协力成员、村庄救济等其他案卷也各自调用同一个三千拨款入口，协力军团按
  每个成员案卷分别拨款，后续平衡时应一起审计，不能只改地方请求常量。
- 更严重的已证实来源是舰船循环造钱。`GivePoliceShips` 每小时按
  `ceil(当前部队人数 / 50)` 免费创建首选重船；`SellSurplusPoliceShips` 每日又按同一随人数变化的需求线
  出售多余船。日志记录 `21` 次获得舰船，其中 `18` 次明确为 `ApplyByMobilePartyCreation` 免费生成重船；
  同期出售 `18` 艘船，收入 `276824`，其中 `13` 艘 `sturgia_heavy_ship` 收入 `263303`。兵力跨过五十人
  阈值时免费补船、人数随后回落时卖船，构成可重复的系统性造钱路径，推翻了 2026-07-25 记录中“只出售
  缴获余船、没有额外造钱”的旧结论。R10 经济修复应优先切断免费补船资产与可出售资产之间的转换，例如
  记录模组配发船并禁止其出售或取消按短期兵力波动反复免费补船；之后再决定是否下调三千拨款和全大陆
  村庄保护费。

## 2026-08-05 v1.4-r9 正式发行

- 用户以关闭作弊、只启用 GreyWarden 的新 StoryMode 战役完成最终实机验收。人物与家族百科按钮、
  人物地点链接和悬赏协力流程均已通过；随后购买一百份黄油，Steam 成功弹出原版 Butterlord 成就。
  这证明成就行为不是只在内部完整性检查中显示可用，而是原版统计、Steam 提交和解锁通知的完整链路
  均已实际执行。验收档此前检查的 AchievementsDisabled 为 0。
- 正式发行前从同一最终源码重新完成四分支矩阵：Bannerlord.ReferenceAssemblies
  1.4.5.114824-beta、1.4.5.115026、1.4.6.115628 和本机 1.4.7 均为 0 编译错误；
  只保留 43 条既有可空性警告。中英文玩家 README 已改为正式 v1.4-r9，并压缩为六条主要内容、
  简短安装说明及恰好 v1.4-r9、v1.4-r8 两条正式日志；所有加粗和行内代码装饰均已删除。
- 玩家 DLL 使用 GwpDiagnosticsEnabled=false、DeployToLiveModule=false 独立重建于
  C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\release-player-v1.4-r9\。
  GreyWardenPolicePurity.dll 为 753664 字节，SHA-256 为
  226270E8A6E8151DB378B8AF398EB9FD1B4A412F68447D777DF35A8957AC15CF。ILSpy 反编译确认
  GwpAiDiagnostics 的 LogPath 返回空字符串，全部写入方法为空，两个追踪判断恒为 false；类型中不存在
  System.IO、File.WriteAllText、File.AppendAllText 或 GreyWarden-AI-Diagnostics 字样。
- 独立玩家构建没有覆盖实机测试安装。构建前后实机客户端 DLL 的 SHA-256 均为
  87A84041B59081B098125F80DF88443C26AD77F0C25F172267030FFE0C9600FD，仍是诊断开启的开发 DLL；
  仓库与实机中英文 README 及 SubModule.xml 逐文件哈希一致。
- 正式 staging 位于
  C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\release-stage-v1.4-r9\，
  解包复验位于
  C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\verify-package-v1.4-r9\。
  最终包只有 GreyWarden 一个顶层目录和 27 个正常客户端文件；禁入文件为 0，17 个 XML 解析错误为 0。
  包内玩家 DLL 与独立构建哈希一致，再次反编译仍是空诊断实现；0Harmony.dll SHA-256 为
  7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C。
- 本地正式包为
  D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4-r9.zip，
  大小 349503814 字节，SHA-256 为
  FD06FFB28B90F218A7522155BB0BAFC44B3B271D093F063CF2C69D284DDE7146；匹配的 .zip.sha256
  内容已重新读取并核对一致。
- 发行代码提交为 dcdc042450b4abf5d0e3e9b096df6a7feed5cb92，main 与带注释标签 v1.4-r9 已推送；
  标签解引用后准确指向该提交。正式 Release 位于
  https://github.com/Lucicain/GW/releases/tag/v1.4-r9，为非草稿、非预发行并已标记 latest。
  远端 ZIP 状态为 uploaded，大小仍为 349503814 字节，GitHub digest 为
  sha256:fd06ffb28b90f218a7522155bb0bafc44b3b271d093f063cf2c69d284dde7146；远端校验文件为
  89 字节，digest 为 sha256:535aa0ed60a93dbe14f27d569eac40225fe34470eca254a3e2fa666cbba3fbd7，
  两者均与本地文件一致。本机 Modules 父目录已删除旧 R8 ZIP 与校验文件，只保留 R9 一对。

## 2026-08-05 R9 人物百科按钮挤压正文

- 用户实机截图确认人物“案底与震慑”按钮已经恢复，但整块人物正文被挤窄，右侧历史信息缩成一列；
  同一版本的灰袍家族页两枚按钮正常悬在右上角，正文宽度正常。该现象与是否启用四前置无关，直接原因是
  两份原版页面的控件层级并不相同：家族页的 `RightSideScrollablePanel` 直接位于横向主分栏中，人物页
  则先位于一个包含滚动条的普通 `Widget`，外面才是横向主分栏。上一版统一使用
  `scrollable.ParentWidget.ParentWidget`，家族页恰好抵达页面主体，人物页却只抵达横向 `ListPanel`；
  固定宽度按钮因此成为主分栏的新列，真实占用 `150` 像素并把人物正文推窄。
- 不再按固定父级数量猜测挂载位置。`GwpGauntletWidgetUtility.FindAncestorChildOf<BrushWidget>` 从原版滚动区
  向上寻找直接位于百科背景 Brush 下的页面主体；人物页和家族页均把按钮加入该绝对布局容器。按钮继续
  使用固定尺寸和右上边距，但不再成为任何 `ListPanel` 的布局项，因此只覆盖右上角自身面积，不改变
  原版正文、历史列或滚动区宽度。
- 修正后本机 1.4.7 与 `Bannerlord.ReferenceAssemblies 1.4.5.114824-beta`、`1.4.5.115026`、
  `1.4.6.115628` 均为 `0` 编译错误。最终客户端与编辑器 DLL 均为 `772096` 字节，SHA-256 均为
  `87A84041B59081B098125F80DF88443C26AD77F0C25F172267030FFE0C9600FD`；仓库 `_Module` 的 `24` 个
  正常客户端运行文件与实机相比缺失 `0`、哈希差异 `0`。中英文 R9 日志已同步实机；没有制作 ZIP。

## 2026-08-05 R9 原版百科扩展统一与人物地点链接恢复

- 用户再次确认所有百科扩展的唯一界面结构：保留原版页面，只在原版页面上增加灰袍按钮，按钮再打开
  灰袍自己的内容；不得注册替代原版页面的百科 VM，也不得维护整份仿原版 XML。审计确认人物页已按
  此结构修复，但家族页仍由 `GwpEncyclopediaClanPageVM` 和整份 `EncyclopediaClanPage.xml` 替换，
  “战争理由/案件总卷”之所以还能显示，正是因为自定义 VM 绕开了原版预编译页面。这与最终架构不一致。
- 家族页现恢复原版 `EncyclopediaClanPageVM` 和原版预编译页面。构造后只用
  `ConditionalWeakTable` 保存当前家族对应的战争理由、案件总卷动作；原版
  `EncyclopediaClanPage` 电影完成加载后，从原版 `RightSideScrollablePanel` 上溯到页面容器并加入两枚
  灰袍按钮。按钮仍只在灰袍家族页显示，继续打开既有战争理由弹窗和独立案件总卷，不改原版家族页的
  数据源、布局和其他模组扩展入口。
- 人物案底详情的地点链接失效与人物按钮先前消失是同一类资源路径问题：GreyWarden 的整份
  `SingleQueryPopup.xml` 给正文写了 `Command.LinkClick="ExecuteLink"`，但原版
  `SingleQueryPopUpVM` 命中 Native 的预编译弹窗，松散同名 XML 没有被使用。旧方案还需要
  `GwpNativeViewModelExtension` 反射改写原版 `ViewModel` 的私有方法绑定表，既没有进入实际控件树，
  也不符合只扩展原版页面的原则。
- 新方案保留原版 `SingleQueryPopup`。只在 `GwpLinkedInquiryState` 激活的人物案底详情中，在
  `GauntletMovie.Load` 后找到原版 `RichTextWidget Id="Description"`，赋予既有灰袍地点链接样式并
  直接监听其 `LinkClick`；点击时关闭当前弹窗并调用原版百科管理器打开定居点。ILSpy 对 1.4.7
  `TaleWorlds.GauntletUI.BaseTypes.RichTextWidget.OnLateUpdate` 确认：控件自身在鼠标松开时调用
  `EventFired("LinkClick", text)`，无需 XML 命令绑定，因此直接监听是原版事件路径。
- 已删除仓库及实机的三份原版同名页面副本：人物 `EncyclopediaHeroPage.xml`、家族
  `EncyclopediaClanPage.xml` 和 `SingleQueryPopup.xml`；同时删除已无调用者的
  `GwpNativeViewModelExtension.cs`。GreyWarden GUI 只保留唯一命名的自有页面
  `GwpCaseArchive.xml`、`GwpVillageRewardSlider.xml` 和地点链接 Brush，不再复制任何原版页面。
- 完整源码针对 `Bannerlord.ReferenceAssemblies 1.4.5.114824-beta`、`1.4.5.115026`、
  `1.4.6.115628` 以及本机 1.4.7 均为 `0` 编译错误、`43` 条既有可空性警告。1.4.7 Release 已部署
  到正常客户端与编辑器目录，两份 DLL 均为 `772096` 字节，SHA-256 均为
  `C886E2675D669B31DB3F63E6A41A826871D713FC2F7B48E05A69D4448CCE4250`。仓库 `_Module` 的
  `24` 个正常客户端运行文件与实机相比缺失 `0`、哈希差异 `0`，`17` 个 XML 解析失败 `0`；三份
  原版同名页面在仓库和实机均不存在。本轮没有创建 R9 ZIP。最终实机按钮显示与人物地点点击仍需
  游戏内验证。

## 2026-08-05 R9 人物百科按钮缺失现场排查

- 用户在最新 1.4.7 StoryMode 实机中报告人物百科原有“案底与震慑”按钮不可见；本轮先按现有
  已验证设计核对，没有在证据不足时移动控件或恢复旧的派生百科页面。该按钮按设计对所有人物页常驻，
  不以人物是否已有犯罪或震慑数据为显示条件，因此用户记忆正确，当前画面属于待定位的界面缺失。
- 本次现场日志为
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_44408.txt`、
  `rgl_log_errors_44408.txt` 和 `watchdog_log_44408.txt`。战役正常运行，错误日志为空；RGL 没有
  `Encyclopedia`、`HeroPageVM`、Gauntlet、按钮绑定或 Harmony 异常。资源收集顺序明确为 Native、
  SandBoxCore、SandBox、StoryMode、NavalDLC、GreyWarden，GreyWarden GUI 最后收集。
- 仓库与实机
  `GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml` 均为 `27905` 字节，
  SHA-256 均为
  `35CA953A4A2326CFB6504741B344952B93DFEDECD0B260C86E91A88322E068E6`；与当前 1.4.7 SandBox
  原版文件逐行比较，唯一差异正是末尾新增的 `DeterrenceButton`，因此已排除运行目录漏文件、旧 XML
  覆盖和原版模板漂移。
- 另用与游戏一致的 net472 独立探针加载当前实机 GreyWarden DLL、完整 Harmony 2.4.2 和当前游戏
  程序集。`PatchAll` 成功，`EncyclopediaHeroPageVM(EncyclopediaPageArgs)` 的补丁所有者包含探针
  Harmony ID；再对当前 TaleWorlds `ViewModel` 私有绑定存储执行同一
  `GwpNativeViewModelExtension.Attach`，可正常读取 `DeterrenceButtonText=Record and deterrence`。
  因此现阶段也排除了补丁目标消失、完整 Harmony 无法注册该后缀以及 1.4.7 私有绑定字段失配。
- 用户随后提供完整人物百科截图：右侧原版内容和滚动条均正常，但“案底与震慑”按钮整体不存在，排除
  分辨率裁剪、文字空白和按钮被人物信息遮挡。用户同时确认灰袍家族百科的“战争理由/案件总卷”按钮仍
  正常显示。这个差异最终锁定根因：家族页运行类型是自有 `GwpEncyclopediaClanPageVM`，原版没有该类型
  的预编译 Prefab，因而回退读取 GreyWarden XML；人物页为兼容其他百科扩展而保留原版
  `EncyclopediaHeroPageVM`，恰好命中 `SandBox.GauntletUI.AutoGenerated.1.dll` 内原版预编译 Prefab，
  `GauntletMovie.Load` 不再读取同名松散 XML。故之前“VM 绑定正常 + XML 含按钮”仍不能保证控件出现。
- 用户重申原始设计边界是“在原版界面上增加按钮”，不是复制并维护一份仿原版页面。最终修复保留原版
  `EncyclopediaHeroPageVM` 和原版预编译人物页；构造后只用 `ConditionalWeakTable` 保存该页面对应的
  案底详情动作。`GwpEncyclopediaHeroPageWidgetPatch` 在 `GauntletMovie.Load` 完成后仅匹配电影名
  `EncyclopediaHeroPage`，递归找到原版 `RightSideScrollablePanel` 的父容器，再添加一个按钮、文字和
  提示控件。点击继续调用既有案底、震慑、最近普通定居点和正文地点链接逻辑；其他原版页面、原版人物
  VM 以及其他模组对原版 VM 的扩展均不替换。
- 已删除仓库和实机整份
  `GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml`。该文件是原版人物页的完整副本
  加一个按钮，既没有被当前原版 VM 路径采用，也违反只扩展原版页面的既定方案。家族页的独立 VM/XML
  不受影响。当前源码分别对 `Bannerlord.ReferenceAssemblies 1.4.5.114824-beta`、`1.4.5.115026`、
  `1.4.6.115628` 和本机 `1.4.7.117484` 完整编译，四者均为 `0` 错误、`43` 条既有可空性警告。
  1.4.7 Release 已自动部署；客户端与编辑器 DLL SHA-256 均为
  `7AFC65F669F58A691FC53D0C7787FE04AECFD58B5899A37772DD81F70308289E`。待用户实机验证按钮显示、点击
  弹窗、当前位置和地点超链接。本轮没有制作 ZIP、提交或推送。

## 2026-08-05 R9 原版成就兼容

- 首次实机验收失败：用户只启用 GreyWarden、进入 StoryMode 新战役时立即弹出异常。现场
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_29720.txt` 在
  `02:10:43.309` 明确记录 `new Harmony(...)` 抛出 `FileNotFoundException`：缺少
  `MonoMod.Backports, Version=1.1.2.0`。因此不是成就条件或存档遍历崩溃，而是所有 Harmony 补丁在
  `SubModule.OnSubModuleLoad` 阶段都未安装。此前同时启用 Bannerlord.Harmony 等框架时，它们先加载的
  运行组件掩盖了 GreyWarden 自带文件不完整的问题。
- 根因是仓库 `lib/0Harmony.dll` 虽标记为 `0Harmony 2.4.2.0`，实际为 `292352` 字节的外部依赖精简
  版本，二进制引用 `MonoMod.Backports`、`MonoMod.Core` 和 `MonoMod.Utils`，但 GreyWarden 从未随包
  提供这些 DLL。现替换为 NuGet 官方 `Lib.Harmony 2.4.2` 的 `net472` 完整程序集：程序集身份仍为
  `0Harmony, Version=2.4.2.0`，不会为了单独运行而降级到另一 Harmony 版本；完整程序集本身为
  `2461696` 字节，NuGet 的 net472 依赖组为空，所需运行实现封装在同一 DLL。构建项目继续只复制
  `0Harmony.dll`，但它现在真正能够独立加载。
- 独立运行验证不是只检查文件存在：临时 net472 控制台在只有这一个 `0Harmony.dll` 的输出目录中成功
  执行 `new Harmony(...).PatchAll(...)`，并把一个禁止内联的测试方法返回值从 `1` 改为 `2`；进程退出码
  为 `0`，没有加载或复制任何外部 MonoMod DLL。这同时验证构造、补丁扫描、方法改写和补丁执行四步。
- 同一次失败日志的 watchdog 还显示用户新建的测试战役为 `Has Used Cheats=True`，尽管非官方模块历史确实
  只有 `GreyWarden`。进一步定位到
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\Configs\engine_config.txt` 的
  `cheat_mode = 1`。原版在新战役创建时即把 `Game.Current.CheatMode` 写入持久化
  `Campaign.EnabledCheatsBefore`，不要求玩家实际使用某个作弊操作；因此刚才的测试档即使修复崩溃也不会
  获得成就。已把本机配置改为 `cheat_mode = 0`，必须在该修改后重新创建 StoryMode 战役，不能复用已经
  标记作弊的测试档。本模组仍不绕过原版作弊限制。
- 原版成就不是在 Steam 层统一检查“当前是否有模组”，而由 StoryMode 的
  `AchievementsCampaignBehavior` 监听原版战役事件并把统计写入 `AchievementManager`。它在新游戏、
  读档和配置变化时调用 `DumpIntegrityCampaignBehavior.IsGameIntegrityAchieved`；后者依次检查作弊
  历史、非官方模块历史和版本降级。任一检查失败都会持久化 `_deactivateAchievements=true`、移除该
  行为的全部玩法监听器，并停止写入统计。因此只修改一次返回值不足以恢复已经因 GreyWarden 保存为禁用
  的现有存档。
- 新增 `GwpAchievementCompatibilityPatch.cs`，在原版公开完整性入口的后缀中只处理原因 ID
  `R0AbAxqX`（非官方模块）。补丁重新遍历原版同一份 `Campaign.PreviouslyUsedModules` 历史：所有非官方
  ID 都严格等于 `GreyWarden` 时才把这项结果改为通过；出现任何其他非官方模块、无法解析的记录、作弊
  历史或版本降级时均保留原版失败。平台成就服务断线发生在随后的原版统计初始化中，本补丁不拦截其
  临时禁用，也不修改任何成就 ID、统计值或完成条件。
- 对已经保存 `_deactivateAchievements=true` 的存档，在
  `AchievementsCampaignBehavior.CheckAchievementSystemActivity` 原版检查开始前读取该私有字段；只有
  上述完整性入口此时已经通过才恢复为 `false`。读档时原版会先重新注册非序列化监听器，再读取持久化
  字段，随后才执行本检查，因此恢复后原版监听和统计缓存沿正常载入流程继续，不需要复制监听列表、添加
  新存档字段或重写成就逻辑。若禁用来自作弊、降级或其他模组，字段不会被清除。
- 兼容面以公开 `IsGameIntegrityAchieved` 和 `CheckAchievementSystemActivity` 为 Harmony 目标，没有绑定
  私有的 `CheckIfModulesAreDefault` 方法。当前支持范围按 Steam 实际仍可选择的分支定义为 1.4.5 beta、
  1.4.5、1.4.6、1.4.7，不再为 Steam 已不提供的 1.4.0 至 1.4.4 beta 改动现有玩法代码。对 NuGet 中
  1.4.0 至 1.4.7 的历史参考程序集做过一次额外完整矩阵审计：1.4.3 起全部通过；1.4.0 至 1.4.2 的失败
  分别来自既有切磋出生逻辑类型和收养装备模型 API，不是本次成就补丁。用户确认这些旧 beta 已不属于
  目标范围，因此没有为它们引入兼容分支。
- 当前开发存档不能作为成就豁免验收档。最近 RGL 日志明确记录
  `Dump integrity is compromised due to cheat usage`，并列出
  `Bannerlord.Harmony; Bannerlord.ButterLib; Bannerlord.UIExtenderEx; Bannerlord.MBOptionScreen;
  Bannerlord.Diplomacy; GreyWarden; RTSCamera; Expelliarmus` 等非官方模块历史。原版作弊标志和模块历史
  都随存档保留；本补丁按需求不会清除作弊或其他模组造成的禁用。实机验收应使用未开过作弊、模块历史
  只有原版与 GreyWarden 的 StoryMode 存档，并触发一项尚未取得且容易复现的原版成就。
- 完整源码矩阵结果：1.4.5 beta 的 `1.4.5.114659-beta`、`1.4.5.114824-beta`，1.4.5 的
  `1.4.5.114896`、`1.4.5.114927`、`1.4.5.115026`，1.4.6 的 `1.4.6.115439`、
  `1.4.6.115628`，以及 1.4.7 的 `1.4.7.117131`、`1.4.7.117484` 均为 `0` 编译错误；各版本
  只保留同一组 `43` 条既有可空性警告。参考元数据逐版确认
  `AchievementsCampaignBehavior.CheckAchievementSystemActivity`、`_deactivateAchievements` 和
  `DumpIntegrityCampaignBehavior.IsGameIntegrityAchieved` 均存在。README 的公开范围已同步为 Steam
  当前四个可选分支，而不是笼统声称所有历史 beta。
- 本机 1.4.7 最终 `Release -t:Rebuild --no-restore` 为 `0` 错误、`43` 条既有警告并自动部署。
  ILSpy 反编译实机 DLL 确认两个 Harmony 目标、原因 ID 精确匹配、原版/非官方模块遍历、仅
  `GreyWarden` 放行及旧禁用字段恢复均进入产物。客户端与编辑器 DLL 均为 `772608` 字节，SHA-256
  均为 `91C3CE37BD1929A24AC7C237BCFB5DD3F6198F51C5EA16B045D06DB9E87A7155`。
  仓库 `_Module` 的 `27` 个正常客户端文件相对实机缺失 `0`、哈希差异 `0`，实机 `20` 个 XML
  解析错误 `0`；中英文 README 仓库/实机 SHA-256 分别为
  `D4BB68F4CB441FBCF9ED757C75F20D91652A181A06EF24630520D939E6292CB7` 与
  `A5E79DB1DFEC4C35435A43BA33AB33252C4D4AB4FBBD4137F8F096BAB4ACFCD9`。
  `git diff --check` 通过。本轮没有启动游戏、创建 ZIP、提交或推送；本机正式包仍是既有
  `GreyWarden-v1.4-r8.zip` 及匹配校验文件。
- 单独运行修复后再次完成 1.4.7 Release 重建和 1.4.5/1.4.6 全源码交叉构建，三者均为 `0` 错误。
  仓库、实机客户端、编辑器三份完整 `0Harmony.dll` 均为 `2461696` 字节、程序集
  `0Harmony 2.4.2.0`，SHA-256 均为
  `7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C`。客户端与编辑器
  `GreyWardenPolicePurity.dll` 均为 `772608` 字节，SHA-256 均为
  `C3A1E935A6ED0A8AE61187FF9977D9898EFB98A01684510DEA5D39E2373D02C4`。当前游戏进程为
  `0`。仓库 `_Module` 的 `27` 个正常客户端文件相对实机缺失 `0`、哈希差异 `0`，实机 XML 解析错误
  `0`；中英文 README 仓库/实机 SHA-256 分别为
  `AFD5655C960C4985B9949A263FD0D93A281675E26F283E6EFAE914E783763DCA` 与
  `CA3B5554439FA4C828B3B32992103118EFFDDBC4C9EC47364B05859085384FAF`。等待用户以只启用
  GreyWarden、`cheat_mode = 0` 后新建的 StoryMode 战役复验，不能把独立 Harmony 烟雾测试代替完整
  游戏验收。

## 2026-08-05 玩家主导悬赏宣战与高速追截队

- 最新实机诊断证明整组护送修复已经生效，但玩家接手案件后的宣战仍走旧单领主路径。案件
  `lord_6_22` 的协力组总投入战力为 `837.85`，目标区域战力为 `87.48`；在
  `campaignHour=625382.65、625383.64、625384.64`，最近协办人分别已距目标
  `1.89、1.31、2.18`，原主办人却仍距目标 `31.65、29.18、26.64`，任务始终为
  `PlayerBountyEscort; war=False`。`625384.94` 玩家主动建立地图战斗时，参战方只有
  `player_party` 与 `lord_6_22_party_1`，三名附近灰袍仍未宣战，因而无法按原版阵营关系加入。
- 根因有三层。`PlayerBountyBehavior.UpdateEscortPatrol` 只以 `_escortPolicePartyId` 对应的原主办人
  距离和独立常量判断宣战，完全不读取协力组接触者；它直接调用 `FactionManager.DeclareWar`，也不写
  案件已有的 `WarDeclared` 与 `WarTarget`。同时 `PoliceEnforcementBehavior.UpdateTasks` 遇到
  `PlayerBountyEscort` 后执行 `ClearTaskWarTracking(..., true)` 并立即跳过，既绕过普通案件宣战，
  也会把同案无领主追截队标记返程。高速追截队的生成与小时存续又只接受派生状态
  `FlowState == WarPursuit`，而 `IsPlayerBountyEscort` 会优先遮蔽该派生状态，因此单独补一次阵营宣战
  仍无法恢复完整流程。
- 最终把玩家定义为行动主办人，不实际改写 `PoliceTask.PolicePartyId`、协力组长或原版 `Army`。原灰袍
  主办人继续作为案件登记人、真实兵源和截击兵归还目标，所有原主办人与协办人仍沿已验证的原版
  `EscortParty`/`Army` 关系护送玩家。玩家进入按原版接战半径推导的接触范围后，统一调用案件
  `DeclareWar` 写入 `WarDeclared`、`WarTarget` 并正式改变阵营关系；玩家已经与悬赏目标进入同一
  `MapEvent` 时另有同入口安全网。玩家既然主动接下并带领行动，宣战不再受双方战力强弱门槛阻止；
  监控仍记录玩家战力、已投入灰袍战力和敌方区域战力，但只作诊断。
- `TrySpawnImmediateCaseInterceptor` 现在同时接受“玩家护送且本案已宣战”的组合状态。玩家部队不会被
  自动拆兵；目标理论速度高于原灰袍主办力量时，仍由原案件主办人按既有规则真实抽调灰袍健康骑兵，
  候选队必须严格快于目标才保留，同案仍最多一支。生成诊断按组织状态区分
  `player_bounty_owner`、`player_bounty_assistance_army` 与 `player_bounty_speed_dispersed`。
  `UpdateDelayPatrols` 为这类即时队新增独立存续资格，读档后继续追同一目标，案件或战争结束后仍按既有
  归队流程返还幸存者。
- 每两日大型周期支援仍只读取原 `GetEligibleDelaySupportTasks` 的普通 `WarPursuit` 集合，没有因为
  玩家悬赏的即时队资格而开放。这样玩家主导悬赏只获得既有一次性高速追截保障，不会意外产生重复的大型
  无领主增援。实现没有新增存档字段；`IsPlayerBountyEscort`、`WarDeclared`、`WarTarget` 与
  `DelayPatrolState.IsImmediateInterceptor` 原本均已持久化。
- 最终 Bannerlord 1.4.7 诊断版 `Release -t:Rebuild --no-restore` 构建通过，`0` 错误、`43` 条既有
  可空性警告，并自动部署客户端、编辑器 DLL 和两份 README。客户端与编辑器 DLL 均为 `771072` 字节，
  SHA-256 均为 `6DE01DD2252B5DB77F68E12C3F679468C6A7E7559419CAB4FD4F49661FAE021D`；仓库
  `_Module` 的 `27` 个正常客户端文件相对实机缺失 `0`、哈希差异 `0`，仓库与实机各 `20` 个 XML
  解析错误均为 `0`。中文 README 仓库与实机 SHA-256 均为
  `686F72844331E3F26F78824C5FF43001AB08440ABBBFD8382DB5FD65F9CD25C8`，英文均为
  `D09CAC03733858B4BB9D399B430A833E09C7F737DF3721915280F8B94DC5CBDD`。
- ILSpy 反编译最终实机 DLL 确认 `RefreshPlayerBountyCaseContact`、
  `UpdatePlayerBountyEscortCase`、`PLAYER_BOUNTY_CONTACT_DECLARING_WAR`、
  `strengthGateIgnored=True`、三种 `player_bounty_*` 截击触发及
  `IsActivePlayerBountyInterceptor` 均进入产物；`PlayerBountyBehavior` 中旧
  `TryDeclareWarForEscort` 与 `EscortEngageDistance` 均命中 `0`。本轮没有启动游戏、创建或改写正式
  ZIP、提交或推送；本机正式包仍是既有 `v1.4-r8` 包，等待用户用当前存档验证宣战、原版参战加入和
  高速追截队。

## 2026-08-05 玩家接手协力案件后的整组原版护送

- 实机诊断确认案件 `lord_6_22` 已进入 `PlayerBountyEscort`，但主办队
  `gw_leader_1_party_1` 的最终职责仍是 `Pursue:lord_6_22_party_1`；协力组当时为
  `speedDispersed=True`，主办人与两名协办人均已脱离原版军团并继续各自追捕。根因不是悬赏状态
  丢失，而是统一职责解析先返回协力追捕、后读取玩家护送请求；同时既有协力组的小时维护只核对
  主办人与案件 ID，没有在 `IsPlayerBountyEscort` 阶段暂停速度、战力、重组和自主追捕流程。
- 对本机 Bannerlord 1.4.7 `TaleWorlds.CampaignSystem.dll` 反编译复核：未附着的原版军团成员会
  `EscortParty` 军团长，进入军团接触距离后由 `Army.AddPartyToMergedParties` 附着；已附着成员随
  军团长整体移动。因此玩家接手未分散协力案件时，只需让军团长护送玩家并让协办人继续护送军团长；
  速度分散状态没有 `Army`，则每名登记领主都应独立使用原版 `EscortParty` 护送玩家。
- 现将玩家接手同一案件定义为协力组的临时护送状态，不删除协力关系、不清除速度分散记录。未分散
  状态继续维护真实原版 `Army`，主办人以玩家委托最高分护送玩家，协办人按原版军团关系跟随主办人；
  已分散状态下主办人与全部协办人分别护送玩家。该阶段跳过协力战力扩编、速度分散、速度重组和自主
  追捕；护送结束时要求整组重新决策并从原保存状态恢复，目标落败仍沿用既有协力结案清理。
- 新增运行时转换诊断 `ASSISTANCE_PLAYER_BOUNTY_ESCORT_STARTED` 与
  `ASSISTANCE_PLAYER_BOUNTY_ESCORT_ENDED`，记录成员数、速度分散状态和真实军团是否存在。玩家接单
  后立即通知整组重新决策；读档时不增加新存档字段，护送状态继续由已持久化的
  `PoliceTask.IsPlayerBountyEscort` 与协力组数据推导，首次协力维护会重新进入相同护送状态。
- Bannerlord 1.4.7 诊断版 `Release --no-restore` 构建通过，`0` 错误、`43` 条既有可空性警告；构建
  已自动部署客户端、编辑器 DLL 和两份 README。实机反编译确认整组护送分支、玩家委托优先分和
  两条转换诊断均进入产物。仓库 `_Module` 的 `27` 个正常客户端文件相对实机目录缺失 `0`、哈希
  不一致 `0`；客户端与编辑器 DLL 均为 `769024` 字节，SHA-256 均为
  `1D9EC501F1984CD0FD5EE7AF085F30370B83E6555B44B5A8E68119807A3D4C93`。中文 README 仓库与实机
  SHA-256 均为 `526AC4ABDB7913AF1786C713D705F7D24B43021BE0CB1DB642C821F8398B8B00`，英文均为
  `F2718A98908FD7EE5BDD0AC51156B8AA39A2E584B2FB1E526FD4B4104D014698`。尚未启动游戏，等待当前
  `speedDispersed=True` 存档验证主办人与两名协办人均转为 `Escort:player_party`。

## 2026-08-04 练兵官换防兵种比例修复

- 用户观察到练兵官梵蒂的弓箭手在行军中大量消失，表面上像是弓箭手被升级成了其他精锐。初次诊断只
  找到换防的固定选兵偏置，随后用户明确指出消失时梵蒂已经不在执行可见练兵任务。重新按战役小时核对后，
  确认这是“换防偏置、原版整批升级、原版超编逃兵”三个机制叠加，不能把路上消失全部归因于换防。
- 这次换防本身满足原设计时机：既有存档恢复了一条已经排队的练兵任务；梵蒂结束协力军团职责后，于
  `campaignHour=624930.00` 把弥瑟指定为目标和 `castle_A8` 指定为会合点，`624934.00` 时双方的
  `CurrentSettlement` 都是 `castle_A8` 并开始驻留，`624936.01` 才完成 `144` 人交换。小时状态中的
  `task=-` 只表示没有司法案件，不代表没有独立保存的练兵任务；交换完成后任务会立即从界面移除，梵蒂
  随即重新上路，因此玩家事后查看时会看到“没有练兵任务”。
- 兵种 XML 确认只有 `gwrecruit` 拥有重步兵、弓箭手、骑士三个升级目标，`gwarcher` 本身没有升级目标，
  所以弓箭手不可能沿兵种树直接变为其他精锐。当前模型给三个目标的抽签权重均为 `1`，但原版
  `PartyUpgraderCampaignBehavior` 是“每次为整批可升级新兵抽一个分支”，不是逐名各抽一次；日志中待训练
  人数按 `277 -> 243 -> 64 -> 16 -> 10 -> 6 -> 3` 成批减少，正是这种整批升级。因此三路是每批各
  `1/3` 概率，不是最终人数严格 `1:1:1`，短期构成可以明显偏斜。
- 真正的行军减员来自原版 `DesertionCampaignBehavior`。梵蒂最初 `men=385`、`sizeRatio=1.925`，严重超过
  约 `200` 人的部队上限；无人为转移和战斗时，总人数仍按每日
  `385 -> 339 -> 305 -> 280 -> 262 -> 248 -> 238 -> 230 -> 224 -> 220 -> 217` 下降。反编译本机
  `DefaultPartyDesertionModel` 确认：原版每天移除“超出上限人数的 `25%`”，并从 `MemberRoster` 最后一个
  名单项反向选择逃兵。新升级目标通常被追加在兵表尾部，因而某个刚形成的大批精锐，可能正好是弓箭手，
  会被连续优先删除；这才是玩家在路上看到整个兵种消失的直接机制。
- 超编也会在正常执法中重新出现：后段梵蒂在参与胜利战斗结算时从 `204` 人增至 `243` 人，同时新增
  `30` 名俘虏；随后 `gwp_enf_delay_40552` 又以
  `IMMEDIATE_CASE_INTERCEPTOR_REJOINED; returned=9` 实体归队，使母队达到 `252` 人。反编译原版
  `MapEvent.LootDefeatedPartyPrisoners` 与 `MapEventParty.RosterToReceiveLootMembers` 后已确认，前一段 `39`
  人增长不是普通俘虏招募，而是败方原本押着的非英雄俘虏在胜利后被作为获救人员直接加入获胜 AI 队伍的
  `MemberRoster`；该入口没有部队上限检查。原版 `RecruitPrisonersCampaignBehavior` 对 AI 每日招募自己
  押着的俘虏时反而明确按 `PartySizeLimit - TotalManCount` 限制数量，已经超编时招募数为 `0`。
- `PoliceResourceManager.PurifyParty` 随后每六小时移除所有非灰袍普通成员，并为每人等量加入一个
  `gwnewrecruit`，同样没有容量检查。于是完整链路是“胜利解救败方俘虏并绕过容量加入成员名单 -> 外族获救
  人员被等量替换为灰袍新兵 -> 练兵官给这些新兵加经验 -> 原版按整批随机分支升级 -> 原版每日删除超编
  逃兵”。这解释了为什么项目虽没有主动造兵入口，一场执法胜利仍能让灰袍名单多出几十名可训练新兵，
  并最终表现为兵种数量剧烈变化。后续 `9` 人增长则是模组把此前真实抽出的截击兵归还；它也可能让已经
  接近上限的母队再次轻微超编。
- 修复后 `PoliceResourceManager` 同时监听战斗结束，不再等待六小时维护；对参战灰袍领主队先完整移除所有
  外族普通成员，再按“移除后的原始空位 - 该队所有在外即时截击队中的灰袍幸存者”计算可用空位，只把
  不超过剩余空位的人数转成 `gwnewrecruit`，其余获救人员直接释放。只为截击队内真正的灰袍成员预留，
  截击队在战后偶然接收的外族获救人员不算精兵名额。`POLICE_ROSTER_PURIFIED` 诊断现在同时记录外族总数、
  实际转换数、释放数、原始空位、扣除预留后的空位、截击队预留数、上限、净化后人数和外族构成；定期
  六小时净化仍作为非战斗招募与异常状态的兜底。
- 即时截击队靠近来源队时，先把截击队中的灰袍幸存者暂存，再以“可继续升级的低阶兵优先、较低阶优先、
  同条件下当前大批次优先”的顺序，从来源队换出所需人数，最后把截击精兵归还。这样来源队若被战后新兵
  临时补满，低阶成员会随无领主队返回驻地退场，截击精兵不会反而被淘汰；来源队若已经是历史超编，则
  只做等人数替换，归队前后总数不再增加，也不会借此强制清理整个旧档超员。截击队中的外族成员或极端
  情况下仍无位置的剩余成员继续沿已有无领主支援队流程返回原驻地后消失。`IMMEDIATE_CASE_INTERCEPTOR_REJOINED`
  现记录截击灰袍数、换出退场数、实际归还数、剩余退场数、归队前空位、来源队归队前后人数及上限。
  截击队组建入口也明确只从灰袍兵种中抽取健康骑兵，避免来源队短暂存在的外族骑兵被误当成预留精兵。
- 既有存档中已经在旧逻辑下转成合法灰袍兵的历史超员不会被本轮强制删除或重排，仍由原版逃兵机制逐日
  回落至上限；本轮只阻断新的战后获救人员和截击队归还继续制造超员，符合不做旧档迁移的当前原则。
- 根因位于 `GreyWardenTrainingBehavior.ExchangeTroops`：原实现对同阶精锐按 `StringId` 排序后依次取人。
  三个终阶兵等级相同，而 `gwarcher` 排在最前，因此较大的换防会稳定优先抽走弓箭手。换回的低阶兵随后
  仍由原版 `PartyUpgraderCampaignBehavior` 按当次随机分支整批升级，于是最终构成看起来像“弓箭手变成了
  其他兵种”。这是另一个已经证实的构成偏置，但不是上述行军减员的主因。此前三路权重统一为 `1f` 只保证
  每批升级等概率，本来就不保证三个兵种的绝对数量相等。
- 现改为按练兵官换防前各终阶兵种的实际人数计算配额：先取比例配额的整数部分，再以最大余数法补齐剩余
  名额；交换总人数、目标队低阶兵优先回收以及原版升级机制均保持不变。新增
  `TRAINING_EXCHANGE_ROSTER` 诊断，记录请求人数、实际换出/换回人数、换出精锐构成及双方换防前后完整
  灰袍兵种数量，供实机区分比例换防、随机升级与战损。
- `Release --no-restore` 构建通过，结果为 `0` 错误、`43` 条既有可空性/离线 NuGet 警告，并已自动部署
  客户端、编辑器 DLL 与两份 README。仓库 `_Module` 的 `27` 个正常客户端文件相对实机目录缺失 `0`、
  SHA-256 不一致 `0`，实机 `20` 个 XML 解析错误 `0`；客户端与编辑器 DLL 均为 `761856` 字节，SHA-256
  均为 `ECAFE2B94D8AA0F5F9FCA5044DA4C3A8854BC9F80F125D7ADF675904413C39C9`。ILSpy 对最终实机 DLL
  确认比例换防函数、最大余数计算和 `TRAINING_EXCHANGE_ROSTER` 均已进入产物；三类兵各 `0` 至 `20` 人、
  所有合法换防人数的穷举中，人数不守恒或分配超过现有人数的失败数为 `0`。没有创建发行 ZIP、提交或推送，
  等待用户从现有存档继续实机复验。
- 本次超员修复及“截击精兵优先”完善后再次执行 `Release --no-restore`，结果为 `0` 错误、`43` 条既有
  警告。仓库 `_Module` 的 `27` 个正常客户端文件相对实机缺失 `0`、SHA-256 不一致 `0`，实机 `20` 个
  XML 解析错误 `0`，两份 README 与仓库逐字节一致；客户端与编辑器 DLL 均为 `766976` 字节，SHA-256
  均为 `1F6FE1F22D0DC2DDD96520BE1CA2073D807C3FEA7ABAF2A6DCCE2B662472D6B8`。ILSpy 对最终实机 DLL
  确认截击队灰袍预留计数、净化时扣除预留、归队临时名单、低阶优先换出、等人数归还、剩余成员返驻地
  以及两组扩展诊断均已进入产物。没有创建发行 ZIP、提交或推送，等待现有存档实机复验。

## 2026-08-04 灰袍骑枪整杆伤害与四向攻击（已放弃并完整回退）

### 2026-08-04 第六轮：实机仍失败，按用户决定放弃功能并完整回退

- 用户复验第五轮后明确反馈功能依旧不可用，并决定放弃“骑枪左右挥击、非攻击整杆持续碰撞伤害”方案。
  因此第五轮及此前各轮均不得再视为待验收功能或 r9 已实装内容；以下历史只保留为失败方案与事故证据，
  不代表当前代码仍包含这些机制。
- 已删除整个 `GwpLanceCombatBehavior.cs`，同时移除 `SubModule.OnGameStart` 对
  `GwpLanceRuntimeConfiguration.EnsureConfigured()` 的调用、任务创建时注入 `GwpLanceCombatBehavior` 的
  行为以及只供该功能使用的 `GwpIds.LanceItemId`。当前运行时不会再追加武器 usage、切换攻击用法、扫描
  枪杆碰撞、登记补充 Blow 或写入 `[GreyWarden Lance]` 诊断。该实验没有写入任何战役存档字段，因此
  不需要旧档迁移或清理；完全退出游戏并使用回退后的 DLL 后即恢复原版武器行为。
- 中英文 r9 玩家日志已删除两条骑枪实验功能说明，保留同一 r9 中已经完成的悬赏、结算队、地点与任务
  清理内容。`gwlance` 物品仍是原有合法 `TwoHandedPolearm` 锻造物品，四个原版部件依次为
  `spear_blade_6`、`spear_guard_13`、`spear_handle_19`、`spear_pommel_9`；本轮没有修改物品 XML、模型、
  装备引用或其他武器机制。
- 回退后的 `Release --no-restore` 构建通过，结果为 `0` 错误、`43` 条既有可空性/离线 NuGet 警告，
  并已自动部署客户端、编辑器 DLL 与两份 README。仓库 `_Module` 的 `27` 个正常客户端文件相对实机目录
  缺失 `0`、哈希不一致 `0`，实机 `20` 个 XML 解析错误 `0`；客户端与编辑器 DLL 均为 `759296` 字节，
  SHA-256 均为 `18368AA1C0C707FFA26DB14075FF81EC5303CB027A3E16C82027B5A8C1E31318`。反编译最终
  实机 DLL 后，`GwpLanceCombatBehavior`、`GwpLanceRuntimeConfiguration`、`[GreyWarden Lance]`、
  `polearm_block_long_swing_thrust` 和 `contactHit=` 命中数均为 `0`；`SubModule` 的任务行为列表也不再包含
  骑枪行为。游戏进程为 `0`，本功能已经从源码、编译产物、实机模块和玩家说明四处完整撤销。

### 2026-08-04 第五轮：四方向原版用法前置与持续接触重新结算

- 用户复验第四轮确认“双手持枪和防御”已经成功，但左右仍完全无法挥砍；枪杆第一次接触造成并显示
  `1` 点伤害，此后继续碰撞不再生效。最新日志
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_62220.txt` 与该现象完全一致：运行时四个 usage
  已正确生成，默认双手刺击为 `polearm_block_long_shield_thrust`，附加双手四方向用法为
  `polearm_block_long_swing_thrust`；整场却只记录一次 `direction=AttackDown, usageIndex=1`，没有任何
  `AttackLeft/AttackRight`，并且只在 `10:46:48` 登记一次 `actualDamage=1`。这排除了装备、双手标志、附加
  usage 缺失及伤害显示失败，失败点分别在输入方向暴露顺序和接触重置规则。
- 左右失败的根因是仍把纯刺 usage 当作待机默认值。原版在托管代码运行前已按当前 usage 过滤输入，纯刺
  用法根本不会把左右方向保留下来，所以事后读取 `MovementFlags` 或 `PlayerAttackDirection()` 都只能得到
  上下。本轮改为待机、持握和防御时始终先使用原版 `polearm_block_long_swing_thrust`，让引擎从一开始就
  接受四个方向；检测到上刺或下刺的起手输入时，才在原版 Agent 消费该输入前切回
  `polearm_block_long_shield_thrust`。进入 `ReadyMelee/ReleaseMelee/ParriedMelee/BlockedMelee` 后锁定本次
  已选 usage，不在动作中途切换；`PassiveUsage` 仍完全留给原版架枪。这样左右直接走原版 staff 挥击，
  上下仍走原版长杆刺击，不再尝试从已经被纯刺 usage 过滤掉的输入中恢复左右方向。
- 持续碰撞失败的根因是 `_previousContacts` 只允许“首次进入”造成伤害，必须整根三米枪杆在连续两帧间
  完全离开目标的肢体碰撞体才会重置。实际人物即使后退再撞，枪杆的其他位置仍可能擦着同一目标，于是
  该键会永久保持为已接触。本轮删除这个进入沿门槛，改为攻击者/目标组合各自拥有 `0.50` 秒重新结算
  间隔：每次仍必须重新检出真实肢体交点、达到最低相对速度并通过完整原版伤害计算，成功后才启动间隔；
  不会按渲染帧连续扣血，也不会让零相对速度的静止贴靠产生伤害。每组目标前五次成功结算都会记录
  `contactHit=1..5`，下一轮可直接确认重复碰撞是否工作。
- 中英文 r9 玩家日志已同步说明持续接触会随相对运动重新结算。`Release --no-restore` 构建通过，结果为
  `0` 错误、`43` 条既有可空性/离线 NuGet 警告，并已自动部署客户端、编辑器 DLL 与正常客户端文件。
  仓库 `_Module` 共 `39` 个文件，其中按规定不部署的 `Assets/AssetSources` 恢复源文件为 `12` 个；其余
  `27` 个实机文件缺失 `0`、哈希不一致 `0`，实机 `20` 个 XML 解析错误 `0`。客户端和编辑器 DLL 均为
  `774144` 字节，SHA-256 均为
  `34CA8B181506F016D04D8CB604E935743FEC807BD5A2C5698053637E9F45C3BD`。最终 DLL 还会为每名 Agent
  首次出现的每个原始攻击方向记录 `attack direction observed`，即使目标 usage 已经处于正确索引也会留下
  证据。反编译最终实机 DLL 已确认待机选择四方向 usage、上下选择刺击 usage、五种动作锁、`0.50` 秒
  重新结算、独立方向记录以及重复命中日志都存在；游戏
  进程为 `0`。这些静态证据只证明部署内容，左右动作和多次接触伤害仍须本轮实机验收。

### 2026-08-04 第四轮：双手默认用法与完整伤害反馈

- 用户复验第三轮后报告三项现象：防御时只用单手举杆；整杆接触能听见敌人惨叫，却没有伤害数字、
  伤害类型或明显受击动作；左右挥击尚未可靠复验。最新日志
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_3252.txt` 给出直接证据：持枪 Agent 的当前
  usage `0` 是 `onehanded_polearm_block_long_rshield_thrust`；`10:16:06` 检出身体接触并登记了
  `strike=Swing, speed=4.92, distance=3.11, damage=1`；全程只有 `AttackUp` 方向记录，没有左右输入记录。
- 单手防御的原因不是动作资源异常，而是 `TwoHandedPolearm` 锻造模板同时生成单手持盾、双手、架枪等
  多个原版用法，代码此前错误地把列表中的第一个非挥击用法当成上下刺默认用法。现在追加挥击数据时
  先按 `WeaponClass.TwoHandedPolearm`、usage 同时包含 `block/thrust` 查找真正双手刺击源；附加的
  `polearm_block_long_swing_thrust` 也继承该双手武器类别和 `NotUsableWithOneHand` 等原版标志，不再从
  `PrimaryWeapon` 的单手数据复制。持枪后即使尚无攻击输入，只要当前还是单手用法便立即切到双手刺击；
  原版 couch/bracing 的双手状态不被强制覆盖。启动日志会逐项列出全部 usage 名称和武器类别。
- 只有惨叫没有数字的原因也已确认：补充碰撞此前直接调用 `Agent.RegisterBlow`，这会扣血和播放声音，
  但绕过 `Mission.RegisterBlow` 中负责 `PrintAttackCollisionResults` 的战斗日志路径；实测又只有 `1` 点，
  并被模组主动调用的原版 `DecideAgentShrugOffBlow` 标成轻微承受，所以几乎没有视觉反馈。本轮在真实
  血量变化后构造原版 `CombatLogData` 并调用 `Mission.AddCombatLogSafe`，玩家造成或受到接触伤害时会
  显示数字及 Cut/Pierce 类型；Blow 同时设置原版常用的 `DamagedPercentage=1`、`NoIgnore=true`，并删除
  这条补充碰撞的主动 ShrugOff 标记，让 Agent 自身处理正常受击反应。伤害仍由既有原版幅度、护甲、
  部位、难度和应用模型计算，没有另写固定伤害。
- 武器包围盒最长轴的两端此前没有保证方向，可能把枪尖当作 Base，导致
  `CollisionDistanceOnWeapon` 从错误端计算。本轮以手中武器实体的 attachment origin 为握持参考，自动
  把更靠近手部的一端作为 Base，确保靠手木杆低、远端高的距离规律方向正确。接触日志新增 impact 比例、
  伤害类型、幅度、原始伤害、计算伤害和实际掉血，下一轮可以直接核对伤害是否被护甲压到极低。
- 中英文 r9 玩家日志已同步写明双手持握/防御、常驻接触的伤害显示与受击反馈。本轮仍未把“左右挥击
  成功”写成实机结论；只有下一次日志出现 `direction=AttackLeft/AttackRight` 且用户看到对应动作，才能
  验收该项。
- `Release --no-restore` 完整构建通过，结果为 `0` 错误、`43` 条既有可空性/离线 NuGet 警告；客户端与
  编辑器 DLL 已自动部署到实机测试模组，两份均为 `816128` 字节，SHA-256 均为
  `665ED4B7E7A8ACE120903E8D5184C80A1DEFC4001463BFBA443125FB72F88209`。仓库 `_Module` 的 `27`
  个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，实机 XML 解析错误 `0`。反编译最终 DLL 已确认
  双手刺击源筛选、`polearm_block_long_swing_thrust`、`Mission.AddCombatLogSafe`、`NoIgnore=true` 和
  `DamagedPercentage=1` 均存在，且旧的主动 `DecideAgentShrugOffBlow` 已移除。游戏进程为 `0`，可以开始
  本轮实机验证；这些静态证据不替代实际动作、伤害数字和受击反应验收。

### 2026-08-04 第三轮：按实测失败纠正输入源与碰撞实体

- 用户已在战场明确否定第二轮玩法结果：上下仍能刺，但左右仍被表现为刺击；不进行攻击时，骑枪接触
  敌人也完全没有伤害，因此第二轮不能记为功能验收通过。最新实测日志
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_53420.txt` 没有骑枪异常，并分别在
  `09:59:20` 和 `10:03:13` 记录 `native side-swing usage attached; item=gwlance, usages=4`，证明附加
  usage 已进入对象，却没有证明攻击方向切换或整杆碰撞真正运行。
- 左右挥击的直接原因是第二轮读取 `PlayerAttackDirection()`：该值已经经过当前纯刺 usage 的原版方向
  约束，玩家的左右输入在托管层读取前就可能被折算为可用刺击方向。本轮改为首先读取原版
  `Agent.MovementFlags` 的 `AttackLeft/AttackRight/AttackUp/AttackDown` 原始攻击标志，再选择附加的
  `polearm_block_swing_thrust` 或原刺击 usage；只有原始标志不存在时才回退到原方向接口。每次实际切换
  现在会把方向、usage 索引和 item-usage 名称写入 RGL 日志，下一轮实测可以直接区分“没读到左右输入”
  与“已切到挥击但原版动作未采用”。选择同时放在 `OnPreMissionTick`，确保在原版 Agent tick 消费攻击
  标志前完成；普通 `OnMissionTick` 再检查一次，兼容更晚才发布输入的任务视图。
- 常驻碰撞不再用主手骨骼和 authored frame 猜测枪杆轴线。现在优先取得
  `Agent.GetWeaponEntityFromEquipmentSlot` 返回的、游戏正在手中显示的真实武器实体，从其本地物理包围盒
  最长轴得到整杆两端并用实体全局 frame 转到战场坐标；物理包围盒不可用时才尝试可视包围盒，武器实体
  尚未出现的过渡帧才回退旧手骨算法。这样检测线覆盖同一实体的枪头和木杆，并随原版持握/刺击/挥击
  动画运动。首次取得每名持枪 Agent 的线段会记录来源、长度和 usage，首次身体接触与首次成功伤害也会
  分别留下日志证据。
- 按用户最新决定，已完整删除常驻接触的额外盾牌包围盒拦截及其金属碰撞反馈；友军仍在肢体检测之前
  通过 `Agent.IsEnemyOf` 排除。普通刺击与挥击本来就由原版碰撞处理盾挡，不再重复实现一层模组盾判定。
  常驻接触仍按枪杆接触点与目标的相对速度结算，最低有效速度从 `1.25 m/s` 降到 `0.35 m/s`，让缓慢
  推进或敌人主动撞上持握长杆也能进入伤害计算；零相对速度的静止贴靠仍不会反复扣血。
- 本轮仍保留原版攻击释放与被动架枪期间不生成补充伤害的防双算边界：这些状态继续由原版武器碰撞结算；
  非攻击持握才由整杆接触补足。中英文 r9 玩家日志已删除不再存在的额外盾体拦截说明，改为准确说明
  普通攻击继续采用原版盾挡与受击规则。
- 最终 Debug 构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet 警告；游戏进程数为 `0`。客户端与
  编辑器诊断 DLL 均为 `814080` 字节，SHA-256 均为
  `46C3BA036440957EB985C9EA98D9BD5E80658C83B5BABF1D0D5366B349E4C833`。反编译最终客户端 DLL
  已确认包含原始 `MovementFlags` 读取、手中武器实体和本地物理包围盒调用、接触诊断，并且不再包含
  `DoesAnyShieldInterceptSweep`。仓库 `_Module` 的 `27` 个正常客户端文件相对实机缺失 `0`、哈希不一致
  `0`，实机 XML 解析失败 `0`；中英文 README 亦与实机逐文件同哈希。本轮没有创建发行 ZIP，左右挥击
  动作与非攻击整杆伤害仍须由用户在游戏中复验，不能用构建或反编译代替玩法验收。

### 2026-08-04 第二轮：不替换物品对象的原版用法扩展

- 用户明确纠正“为了止损而限制任何数据修改”的方向：项目没有额外的人为边界，唯一验收标准是装备与
  存档能正常进入游戏，同时把需要的骑枪功能做成可玩的原版式效果。本轮因此不是停留在回滚版，而是在
  保持 `gwlance` 原有合法物品定义的基础上重新实现功能。
- 新增 `GwpLanceCombatBehavior.cs`。`GwpLanceRuntimeConfiguration` 在对象 XML 已经反序列化以后，只给
  现有 `Item.gwlance` 追加第二个 `WeaponComponentData`，复用原版
  `polearm_block_swing_thrust` 左右 staff 挥击、原骑枪重量、长度、惯量、重心、刺击和物品 frame；原
  usage 继续负责上刺/下刺。玩家或 AI 请求左/右方向时切到原版挥击 usage，请求上/下方向时切回原刺击
  usage。没有替换 `gwlance` 的锻造件、没有注册新 `CraftingPiece`、没有写入未知 item-usage ID，也没有
  改动任何 Native 文件，因此存档中的装备仍指向同一个合法 ItemObject。重复开始游戏时会先检查已有
  usage，避免向全局对象重复追加。
- 挥击数据采用原版同档次长柄劈砍枪头的 `3.8` swing factor，伤害类型为 Cut；并非另造一套杆部伤害。
  原版攻击释放仍由引擎自己的碰撞、盾挡、动作和伤害结算处理，实际碰撞距离继续参与重量、惯量、重心
  计算，所以靠近握持点的木杆命中会自然比远端轻。上/下刺仍使用未改动的第一 usage。
- 常驻接触只补足原版不处于攻击释放/被动架枪时的空缺。行为从主手动画骨骼叠加物品 authored frame
  重建当前与上一帧整杆轴线，使用当前整杆射线与十个沿杆分段的逐帧扫掠对附近肢体做连续检测，候选
  Agent 由 `AgentProximityMap` 限域。换武器、长帧或端点瞬移会重置轨迹；同一攻防双方持续贴靠只结算
  一次，完全分离后才重新武装。
- 补充伤害以接触点的世界速度减去目标速度作为相对速度，低于有效碰撞阈值不伤人；按相对速度相对枪轴
  的轴向/横向分量选择原版 Thrust/Swing 幅度模型，并继续使用实际命中距离、武器伤害系数、技能倍率、
  部位、护甲、难度与当前 `AgentApplyDamageModel`。非尖端刺中仍套用原版 non-tip 衰减。原版
  `ReleaseMelee` 和 `PassiveUsage` 期间只记录接触而不生成补充伤害，避免与原版攻击双重结算。
- 友军在任何肢体检测之前由 `Agent.IsEnemyOf` 排除。结算人物前会检查该目标四个武器槽中的真实盾牌；
  手持盾使用副手动画骨骼与物品 frame，背负盾使用其实体现有 frame，并以对应 `BodyName` /
  `CollisionBodyName` 包围盒检查当前整杆和全部分段扫掠。任一盾体截住本次整杆接触即取消人物伤害并
  播放原版金属盾碰撞反馈；本轮没有另外给这类常驻接触扣普通盾牌耐久。
- `SubModule.OnGameStart` 负责在 Agent 生成前追加用法，`OnMissionBehaviorInitialize` 在战役、自定义战斗
  与编辑器任务统一注入行为；追加用法异常会保留原始合法刺击武器并写入原版调试日志，而不是中断模块
  或破坏装备注册。中英文 r9 玩家日志已同步写入实际玩法。
- 当前代码构建已通过，`0` 错误、`44` 条既有可空性/离线 NuGet 警告；构建目标已把诊断 DLL 与模块
  文件部署到 `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`。最终客户端和
  编辑器 DLL 均为 `812544` 字节，SHA-256 均为
  `84D6516E76532670683942AF80DAF59BA81E8FF8F124CD00469E35EBDF35DAF5`；反编译确认包含
  `GwpLanceRuntimeConfiguration`、`GwpLanceCombatBehavior`、原版 swing/thrust 幅度模型调用、敌对过滤、
  肢体射线和 `RegisterBlow`。
- 仓库 `_Module` 的 `27` 个正常客户端文件相对实机缺失 `0`、哈希不一致 `0`，实机 XML 解析失败
  `0`，中英文 README 哈希一致；事故中的两个自有 XML 仍不存在，`gwlance` 仍引用原版合法
  `spear_blade_6`。验证时游戏进程数为 `0`，所以新 DLL 未被旧进程锁住。本节仍标为“待实机验收”：
  编译和静态检查不能代替用户在战场确认四个攻击方向、整杆方向与模型重合、速度伤害手感、普通盾/背盾
  拦截以及换武器后的连续性。

### 2026-08-04 装备失效事故与紧急回滚

- 用户启动存档后发现灰袍装备全部不显示并退化为垃圾物品。现场日志
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_46444.txt` 明确显示客户端载入
  `GreyWarden/ModuleData/crafting_pieces.xml` 后报告
  `gwp_spear_blade_6_swing is not a valid valid anymore.`。该自有枪头没有加入原版
  `TwoHandedPolearm` 锻造模板的 `AvailablePieces`，因此不是合法的该模板部件；`items.xml` 中
  `gwlance` 又位于其余灰袍盔甲和武器之前，非法锻造物品使后续模组物品没有正常建立，直接造成用户
  看到的整套装备丢失/垃圾占位。
- 最初口头判断为自有 `item_usage_sets.xml` 覆盖原版全局表，经日志复核后纠正：本次日志只打开了
  `Native/ModuleData/item_usage_sets.xml`，没有打开 GreyWarden 的同名文件；真正有直接日志证据的原因是
  上述非法自有锻造部件。以后不能在未把部件加入模板白名单并实机验证完整物品加载前替换 CraftedItem
  的任何原版部件，也不能把仅通过 XML 解析当成对象系统加载成功。
- 已完整回滚本轮玩家可见实现：删除仓库与实机中的 `crafting_pieces.xml`、`item_usage_sets.xml` 和
  `GwpLanceContactBehavior.cs`；`gwlance` 恢复原版 `spear_blade_6`；移除 SubModule 锻造件注册、任务行为
  注入、运行时 usage 修改、常驻碰撞 Harmony patch 以及中英文 README 中尚未成立的骑枪功能说明。
- 回滚后重新构建为 `0` 错误、`44` 条既有警告。最终诊断 DLL 为 `800768` 字节，SHA-256
  `1059CD0E86046B2A8B193FD4CD872E00A324AD6D617AC955053DC8A231EB6AD1`；客户端和编辑器 DLL 哈希一致，
  反编译类型表已确认不再包含 `GwpLanceContactBehavior` 或其防重 patch。仓库 `_Module` 的 `27` 个正常
  客户端文件相对实机目录缺失 `0`、哈希不一致 `0`；两个事故 XML 在实机均已不存在，`gwlance` 已确认
  重新引用原版枪头。游戏进程已经载入的错误对象表不能热刷新，用户必须完全退出游戏并重新启动后再
  读档确认装备恢复。

### 2026-08-04 失败实现记录（已回滚，禁止原样复用）

- `gwlance` 不再直接使用会排除 `swing` 的原版 `spear_blade_6`，改用模块自有
  `gwp_spear_blade_6_swing`。新锻造件完全复用原版模型、长度、重量、刺击数据和碰撞体，只补充横向
  切割数据；没有修改 Native 文件或影响其他使用同一枪头的原版武器。
- `_Module/ModuleData/item_usage_sets.xml` 新增
  `gwp_polearm_block_thrust_side_swing`：继承原版 `polearm_block_thrust` 的上下刺击，并逐项复用原版
  staff 左右挥击动作（步战和骑乘）。`GwpLanceContactBehavior.ConfigureLanceItemUsage` 只把
  `Item.gwlance` 的运行时 usage 指向该组合；若引擎没有载入自有 usage，检测到 native index 小于零时
  会保留自动生成的原版用法而不是写入无效 ID，并在原版调试日志留下明确诊断。
- 新增 `GwpLanceContactBehavior` 并在战役、自定义战斗和编辑器任务中统一注入。它只追踪当前主手持有
  `gwlance` 的活动 Agent，从主手动画骨骼和物品 frame 重建整杆轴线；每帧使用当前整杆射线加十个分段
  扫掠点对附近敌人肢体作连续检测，候选目标由 `AgentProximityMap` 限域。友军通过 Team 敌对关系在碰撞
  前排除，换武器、长帧或瞬移会重置上一帧，避免伪碰撞。
- 普通攻击释放和原版被动架枪仍完全交给原版；只有其他持握状态中的首次实体接触才生成补充伤害。补充
  伤害按真实接触点速度判断刺/挥，调用当前 `StrikeMagnitudeModel` 的刺击或挥击幅度，并继续使用武器
  重量、惯量、重心、命中距离、技能伤害倍率、部位护甲、战斗难度和当前 `AgentApplyDamageModel`。同一
  对象持续贴靠只结算一次，完全分离后才重新武装。
- 为防止持握接触已经结算后紧接着进入原版攻击释放而双重伤害，新增窄范围
  `MeleeHitCallback` Harmony 防重：只在相同攻防双方仍处于该次已结算接触、且原版回调确认为
  `gwlance` 身体命中时临时让受害者无敌；原版碰撞和受击反应仍保留，盾牌命中不进入此防重路径。
- 盾牌优先检测覆盖受害者四个武器槽中的真实盾牌碰撞体：当前整杆或任一分段扫掠先与盾体相交时取消
  人物伤害并播放原版金属盾碰撞反馈。手持盾使用动画化副手骨骼与物品 authored frame；背负盾优先使用
  其实体现有全局 frame。本轮先落实用户要求的“整杆碰盾即不伤人”，尚未给这条常驻补充碰撞另行扣除
  普通盾牌耐久。
- 回滚前 `dotnet build -c Debug --no-restore` 曾通过：`0` 错误、`44` 条原有可空性/离线 NuGet 警告；构建目标
  已自动把 DLL、`crafting_pieces.xml`、`item_usage_sets.xml`、物品数据和中英文 README 同步到实机
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`。仍需用户在战场验证自有
  usage 是否被当前客户端载入、整杆方向与视觉模型是否重合、伤害手感及盾牌拦截；验证前不把本节标为
  完成。随后实机物品加载已经证明这套做法失败，上述文件和代码现均已回滚。
- 最终诊断 DLL 为 `812544` 字节，SHA-256
  `988F91A3FB4F3E0F07B216C97B48075AFEC3F7BB3ABE3EF2638FEE0346511885`；客户端与编辑器 DLL 哈希一致。
  仓库 `_Module` 的 `29` 个正常客户端文件相对实机目录缺失 `0`、哈希不一致 `0`，全模块 XML 解析失败
  `0`；反编译类型表确认最终 DLL 同时包含 `GwpLanceContactBehavior` 和防重 Harmony patch。

- 用户纠正上一版“只让矛尖具有伤害体”的设计：长杆本身也是武器，灰袍骑枪应在持握期间让从尾端到
  刃尖的整个实体持续参与伤害检测。这里的“持续”是伤害体始终启用，不是让同一次贴靠按时间反复
  扣血；仍按进入接触一次、完全分离后重新武装处理。
- 随后针对用户指出的“原版长柄斧近身时已经会由木柄命中并造成很低伤害”重新核验 Bannerlord `1.4.7`
  程序集，确认用户判断正确，上一版另行切分刃部/木杆并给木杆附加自定义钝击倍率的方案不应采用。
  `Mission.RecalculateBody` 会为锻造武器建立贯穿整件武器长度的胶囊；斧类只是在这个整杆胶囊之外增加
  斧刃形状，所以木柄本来就能参与原版攻击碰撞。原版命中数据同时记录 `CollisionDistanceOnWeapon`。
- 原版低伤害不是按“命中木料”切换到另一套伤害类型，而是把实际命中点沿武器长度的比例传给
  `CalculateStrikeMagnitudeForSwing`，再结合武器重量、总惯量、重心、角速度和双方线速度计算碰撞后损失
  的动能；因此贴近握持点的斧柄命中自然远弱于远端斧刃命中。锻造件的 `BladeData.SwingDamageType` 仍是
  整个该武器用法的伤害类型，`blade_length` 没有在这条近身低伤害判定中充当材质分区。设计应复用这一
  原版“整杆碰撞 + 实际碰撞距离”机制，不再人工区分刃区、杆区，也不额外施加杆部倍率。
- 常驻状态唯一需要补足的是：原版只在合法攻击/被动架枪判定中把武器碰撞提交为伤害，不能自动让普通
  持握中的接触伤人。新增检测应取得整杆真实扫掠的接触点与 `CollisionDistanceOnWeapon`，再尽可能把该
  数据送回原版挥击/刺击幅度、伤害类型、护甲和技能模型；模组不自行发明木柄伤害公式。低到不足以形成
  有效撞击的接触仍由原版计算归零。
- 盾牌规则按用户要求进一步简化为“整杆优先”：对每个即将结算的受害者，先用上一帧到当前帧的完整
  长杆扫掠体检测附近所有真实盾牌体；只要杆、护手或刃尖任一部分在本次接触中碰到挡在路线上的盾牌，
  就取消这次人物伤害，不再另外要求刃尖也碰到盾牌。盾牌检测不依赖格挡按键或阵营，实体位置成立
  即可阻挡；盾牌可承受对应耐久与反馈，但当次即使破裂也不继续穿透伤人。
- 原版 `spear_blade_6` 明确设置 `excluded_item_usage_features="swing"`，这是当前 `gwlance` 只能刺的
  直接数据原因；`TwoHandedPolearm` 武器描述本身已经包含 `swing:thrust`，并有现成的左右 staff 挥击
  动作。不能简单删除排除项后结束：原版双手 `polearm_block_swing_thrust` 会把向上攻击改成过顶挥击，
  不符合用户要求的“上刺、下刺保持，额外增加左挥、右挥”。最终需要一个仅供 `gwlance` 使用的物品
  usage 组合，保留原版 upper-thrust、lower-thrust、left-swing、right-swing 四组现成动作，不制作新动画。
- 为避免改变同样使用 `spear_blade_6` 的原版帝国重骑枪与长枪，不能全局修改该原版锻造件。优先方案是
  给 `gwlance` 设置模组自有 usage，并只补齐该物品的挥击数据；若当前引擎不合并模组自有
  `item_usage_sets.xml`，再采用仅修改 `gwlance` 运行时 `WeaponComponentData` 的窄范围回退，不能让其他
  原版长杆获得同一动作。
- 上一节记录的矛尖专用伤害体方案现已被本节取代；本节最初记录的自定义刃/杆伤害分区又被上述原版
  机制核验纠正。逐帧连续扫掠、实际接触距离、相对速度、敌对 Team 过滤、原版伤害模型、盾牌几何和
  避免双重结算仍保留。本轮仍只修订设计并核对 Native XML/程序集，没有修改运行时代码或模块数据，
  没有更新玩家 README、构建或部署。

## 2026-08-04 灰袍骑枪常驻碰撞伤害设计调查（未实装）

- 用户要求参考本机 `C:\Users\lucif\source\repos\BattlefieldSkills` 的御剑碰撞伤害，让本模组
  `Item.gwlance` 在持握期间持续具有伤害能力：不以是否播放攻击/格挡动作作为命中前提，而以长矛的
  伤害体是否真实接触敌方为准；友军绝不受伤，目标与矛尖之间存在盾牌实体时人物不受伤。
- 御剑当前实现位于 `BattlefieldSkills\Source\FlyingSwordMissionBehavior.cs`。可复用部分是逐帧保存
  武器前一位置、对刀身/刀尖做扫掠检测、用 `Team.IsEnemyOf` 排除盟友以及通过 `RegisterBlow` 提交真实
  战斗命中；其伤害只是按受控飞行速度计算的固定合成值，且当前人物检测使用宽松中心球，也没有盾牌
  遮挡判断，因此不能整段原样复制到持握骑枪。
- 当前 `gwlance` 是 `items.xml` 中以 `TwoHandedPolearm` 模板和四个锻造部件生成的唯一灰袍骑枪。
  实现应只识别该物品 ID，并从当帧 `MissionWeapon.CurrentUsageItem.GetRealWeaponLength()` 和手中武器实体
  的动画帧取得实际矛尖位置，不扩大到原版或其他模组的所有长杆武器。
- Bannerlord 当前程序集已有完全对应的原版计算链：
  `StrikeMagnitudeCalculationModel.CalculateBaseBlowMagnitudeForPassiveUsage` 最终调用
  `CombatStatCalculator.CalculateBaseBlowMagnitudeForPassiveUsage(weaponWeight, extraLinearSpeed)`；原版被动
  架枪也是先取攻击者与受害者沿撞击方向的相对线速度，再走 `CalculatePassiveAttackDamage`、武器伤害
  倍率、穿刺系数、部位护甲和通用伤害模型。新机制应复用这条公开模型链，而不是照搬御剑的固定伤害。
- 拟采用独立 `MissionBehavior`：只追踪当前持握 `gwlance` 的活动 Agent；将矛尖最后一小段作为伤害
  胶囊，并在上一帧与当前帧之间连续扫掠，以避免低帧率穿透。相对速度取矛尖世界速度减目标接触点
  世界速度，并只保留朝矛尖轴向闭合的分量；静止接触不会反复掉血，步兵主动送上固定矛尖和骑兵高速
  撞上矛尖都能按同一物理量产生伤害。每个“矛手—目标”接触只结算一次，必须完全分离后才能再次
  武装，另丢弃换武器、出生、上马等造成的不合理瞬移帧。
- 盾牌判断不能读取“正在格挡”布尔值，而应沿用本模组巨盾被动拦截已经验证的几何路线：读取盾牌
  `CollisionBodyName` 的原始包围盒，叠加受害者动画骨骼和物品 `Frame` 得到盾牌当帧世界体积，并比较
  矛尖扫掠进入盾牌与进入身体的先后。任意手持盾或背盾实体先被命中时取消人物伤害；可让盾牌承受
  同一原版幅度的耐久伤害和命中反馈，但盾牌即使在本次撞击中破裂，也不在同一帧穿透伤害人物。
- 为避免原版普通攻击命中与常驻伤害体在同一次接触中双重结算，最终实现必须只取消 `gwlance` 的
  原版身体伤害，保留原版动画、武器碰撞和盾牌反应，再由常驻伤害体统一产生身体伤害。刀剑格挡动作
  本身不再是免伤条件；只有实际位于矛尖与身体之间的盾牌几何体能够截断伤害。候选目标使用原版
  `AgentProximityMap` 就近查询并用敌对 Team 过滤，避免大型战场中对全部 Agent 做平方级扫描。
- 本轮只完成本地参考代码、当前 `gwlance` 定义和 Bannerlord `1.4.7` 原版程序集的设计核验；没有
  修改运行时代码、物品 XML 或玩家 README，没有构建或部署，以上机制尚未实装也未经过实机碰撞验证。

## 2026-08-04 VS Code 更新器重复报错修复

- 用户从 VS Code 打开文件时反复出现 `Visual Studio Code - Updater` 弹窗，明确报错为无法删除
  `C:\Users\lucif\AppData\Local\Programs\Microsoft VS Code\1b6a188127`，`Access is denied (os error 5)`。
  该问题与本项目、C# 扩展或 .NET 运行时无关。
- 现场同时存在两代 VS Code 进程：旧主进程 `68348` 的崩溃处理器标识为 `1.130.0`，新主进程
  `29596` 的崩溃处理器标识为 `1.131.0`；另有两个来自
  `C:\Users\lucif\AppData\Roaming\Claude\temp\vscode-stable-user-x64` 的同版本安装器实例和一个
  `inno_updater.exe` 清理进程。旧进程仍占用旧哈希目录，导致已经完成主体升级的安装器无法善后。
  目标目录所有者为当前用户，当前用户、Administrators 与 SYSTEM 均有完全控制权限，排除了 ACL
  权限配置错误。
- 只终止了旧版 `1.130.0` 进程树与卡住的安装/清理进程，保留正在使用的 `1.131.0` 窗口；随后删除
  已核对父目录的旧安装目录 `1b6a188127`，并清除 Claude 临时目录中残留的安装器、更新标记和元数据，
  避免后续打开文件时再次启动同一失败更新。
- 修复后安装根目录只剩当前哈希目录 `e4c7e7b1d6`，`Code.exe` 文件版本和 `code.cmd --version` 均为
  `1.131.0`。再次通过 VS Code 打开实机模块 README 并等待检查：旧目录与临时更新缓存均未重建，
  `CodeSetup`/`inno_updater` 进程数为 `0`，重复更新弹窗未再次触发。

## 2026-08-04 当前开发版本纠正为 v1.4-r9

- 用户发现当前版本身份混乱并要求只纠正版本、不发行。在线核对确认 GitHub Latest 仍是
  `GreyWarden v1.4-r8`，标签为 `v1.4-r8`，正式发布于 `2026-07-25T15:06:23Z`；GitHub 的 ZIP 为
  `349845455` 字节，SHA-256 为
  `150009FADF4780F5CC149524DD2A99908B8EE8CFC6BFC4F2450A36DF11B955C3`。本机正式 ZIP 与其字节数、
  哈希一致，包内仍明确为模块 `v1.4.8` 和玩家版本 `v1.4-r8`，因此 r8 是已经冻结的上一正式版本。
- 问题根因是 r8 发布后的三选一悬赏、固定难度赏金、任意领主/五日结算队交付、四十五日清理、百科
  地点归一和待领奖计时修复仍被追加进 README 的 r8 条目，同时项目与模块版本继续停在 `1.4.8`。
  这些工作不是 GitHub r8 包的内容，实际应属于下一开发修订 `v1.4-r9`。
- 当前工作树已经统一纠正为 `v1.4-r9（开发中）`：`GreyWardenPolicePurity.csproj` 使用 `1.4.9`，
  `_Module/SubModule.xml` 使用 `v1.4.9`，诊断版程序集为 `1.4.9.0`。中英文 README 新建单一 r9 开发
  条目并声明相较正式 r8；正式 r8 条目恢复为 Git 标签中的原文，r7 从当前两条日志中移除。
- 普通诊断版完整重建为 `0` 错误、`44` 条既有可空性/离线 NuGet 警告。实机客户端与编辑器 DLL
  均为 `759808` 字节，程序集版本均为 `1.4.9.0`，SHA-256 均为
  `87E2C9B64DB255AD7EB15CC84A1FA33DC4B350967F690B5FCCD8B4E254174806`。仓库与实机
  `_Module` 的 `27` 个正常客户端文件缺失 `0`、哈希不一致 `0`，实机 `20` 个 XML 解析失败 `0`；
  `SubModule.xml`、中文 README、英文 README 哈希分别一致，实机已经成为 r9 诊断测试模块。
- GitHub 上缺失于本地目录的正式 r8 校验文件已直接从该 Release 恢复；游戏 `Modules` 父目录现仍只
  保留 `GreyWarden-v1.4-r8.zip` 与匹配 `.zip.sha256` 这一组正式文件。没有创建 r9 ZIP、没有构建
  无诊断玩家包、没有提交或推送、没有新建标签，也没有修改 GitHub Release；r9 仍然只是本地开发版本。

## 2026-08-03 设定按实装代码重审

- 用户否定此前把设想完整并入总纲的整理方向，明确当前模组没有任何剧情，要求删除所有未落地设定，
  只保留真实的大陆历史和已经实装的灰袍内容。`docs/grey-warden-setting.md` 仍是唯一设定文件；此前独立的
  `docs/original-history-canon.md` 与 `docs/grey-warden-history-arc.md` 继续保持删除，不再保存平行设定源。
- 对当前 C#、XML 和中文本地化进行了反向核验。仓库只有 `BountyHunterQuest` 与 `AtonementQuest` 两个
  `QuestBase` 派生类，均为沙盒状态生成的可重复悬赏/赎罪任务；没有主线或人物剧情任务。用于设定展示的
  `GreyWardenLoreBehavior.SyncData` 为空，只在会话启动时写入六名领主百科简介并注册按玩家声望变化的普通
  问候，不保存剧情阶段。全仓库代码与 XML 也没有归政、宪章、灰衣缇骑、统一终局或阿雷尼科斯相关实现。
- 唯一设定文件已删除原灰袍秘密总史、潘德拉克战役介入、阿雷尼科斯遇害参与、六名人物案件、玩家建国
  观察、统一后三结局、专属婚恋路线和开发顺序等全部未实装内容。原文中“可以”“建议”“后续”“待设计”
  形式的叙事也不再作为设定保留。
- 大陆历史保留潘德拉克之前局势、参战军事特点、三处战场、龙旗破碎、各国后果、阿雷尼科斯继位与帝国
  分裂及校订来源；它被明确标为模组采用的大陆历史，而不是灰袍参与这些事件的证明。灰袍自身只保留当前
  `spclans.xml`、`comment_strings.xml` 与六人百科已经公开的概括：她们是旧统一帝国治安体系的继承者，
  保护街道、村庄和道路，不追求王冠或领地扩张。
- 当前灰袍设定按运行时注册项重写，覆盖六人六职务、家族与职务断绝、犯罪案卷、出警和有限执法战争、
  长期震慑、玩家声望、纠察与赎罪、三档悬赏与五日结算队、成员和兵员订单、地方事务/重建/训练、村民
  酬谢与战场援军、司法公库、军队战斗特征、俘虏规则和存档连续性。原版玩家婚姻兼容只作为已实现规则
  记录，并明确没有专属恋爱剧情。
- 重审后唯一设定文件由 `1038` 行缩减为 `271` 行且只有一个一级标题；未实装设定的特征词在正文中为
  `0`。本轮只修改内部设定和维护文档，没有改变玩家可体验机制，因此不修改中英文玩家 README、不构建
  DLL，也不部署实机模块。

## 2026-08-02 结算队实体返程与藏身处地点归一

- 用户实机确认上一轮的百科范围、五日派遣和待领赏计时均已解决，同时发现结算队付款后在玩家面前
  直接消失，以及人物靠近或身处藏身处时仍可能出现无法打开的藏身处链接。最新诊断会话记录
  `14:42:56` 派出 `gwp_bounty_collect_60767`，等待 `120.41` 小时、锁定赏金 `30000`；
  `14:44:07` 正常进入付款完成。对应 `rgl_log_14484.txt` 依次记录
  `gwp_bounty_courier_start`、`gwp_bounty_courier_turnin`、`gwp_bounty_courier_response`，没有本模组
  托管异常。付款流程正确，原地消失来自本模组在对话结束回调中直接执行 `DestroyPartyAction`。
- 对照现有招募使者和纠察队的无领主部队流程，结算队改为同一实体返程：付款时选择自身当前位置最近的
  城镇或城堡，同时切换灰袍职责和原版 `SetMoveGoToSettlement` 命令，并提供短暂的不攻击玩家窗口；
  对话关闭后再次下达返程命令，清除此前接触玩家的原版追击目标。每小时继续维护返程，只有进入目标
  定居点或抵达其附近时才销毁；玩家改向灰袍领主领取、退出任务或其他路径清理任务时，已经出发的
  结算队也会收到同样的返程命令，而不是在地图上直接消失。玩家主动追上返程队时只会听到“正在
  返回驻地”的收尾对话，不会再次结算或误入战斗。
- 返程状态以“结算队 ID + 目标定居点 ID”的紧凑字符串随战役存档，不保存运行时对象引用；读档后从
  原版仍存在的实体队伍重新绑定、继续返程，并清除已经不存在的队伍记录。返程队与新的悬赏状态彼此
  分离，因此旧队正在回营时仍可正常承接下一宗任务，后续结算队也不会把旧队重新当成追赶玩家的队伍。
- 藏身处仍能漏进百科的原因不是最近距离函数，而是囚犯所在定居点、部队当前定居点以及英雄
  `CurrentSettlement/StayingInSettlement` 三条直接返回分支绕过了普通定居点过滤。现在所有人物百科
  地点统一经过 `NormalizePlayerFacingSettlement`：城镇、城堡、村庄原样保留；藏身处等特殊地点则以
  其坐标重新选择最近的普通定居点。悬赏与赎罪任务的初始目击和后续情报也统一调用同一过滤函数，
  不再产生藏身处百科链接。
- 最终 Bannerlord `1.4.7` 诊断版完整重建为 `0` 错误、`44` 条既有可空性/离线 NuGet 警告；
  `1.4.5.115026`、`1.4.6.115628` 交叉重建均为 `0` 错误、`43` 条既有警告。实机客户端与编辑器
  DLL 均为 `759808` 字节，SHA-256 均为
  `FC8379788C3AEF87D2D6288BC9B602C640D05F4B5D8C0FB3BE1749D38948E473`。
- 仓库 `_Module` 的 `27` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`；实机 `20` 个
  XML 解析失败 `0`，中文 string id 重复 `0`，且实机没有 `Assets` 或 `AssetSources`。仓库与实机
  中文 README 哈希均为 `6BF6362E10EC721E10397B5A02428DDE58ECBC3B30DEC601ABED9A7095C18E4B`，
  英文均为 `15307CFD33F657C3CEB79570434045159255397584CCA77D4C280D358E12606F`。ILSpy 对最终实机
  DLL 确认返程存档键、原版返城命令、召回/抵达诊断、返程收尾对话以及所有人物状态分支的普通
  定居点归一均已进入产物。仍需实机验证付款后实体返程、返程途中存读档、抵达后消失，以及人物
  身处藏身处时改链最近普通定居点；本轮仍是诊断版开发部署，不创建正式 ZIP，也不发布 GitHub。

## 2026-08-02 百科最近地点、五日结算队与待领赏计时修复

- 用户实机确认三选一悬赏可用后报告三项体验问题：人物百科位置总指向大型定居点；目标落败后不易
  找到忙于各自职责的灰袍领主；待领赏任务显示很大的负数剩余时间。检查最新实机诊断会话
  `2026-08-02T05:37:20+10:00` 至 `06:06:09`（程序集 `1.4.8.0`）和
  `rgl_log_53460.txt` 后，确认 `05:51:16` 已正常进入 `gwp_bounty_collect_option` 与
  `gwp_bounty_reward_response`，没有托管异常或悬赏交付报错。`rgl_log_errors_53460.txt` 只有原版
  对话语音播放记录；FMOD 句柄、缺少语音对象和资源局部读取提示也没有指向本悬赏状态。监控因此
  证明领主交付本身成功，但不会记录原版任务界面如何格式化剩余时间。
- 百科地点偏大的确定原因是 `GwpAiDeterrenceState.GetTrackingSettlement` 与
  `BuildTrackingLocation` 对地图上移动的英雄调用 `GwpCommon.FindNearestTown`，该方法只接受
  `Settlement.IsTown`，排除了村庄。新增统一的 `GwpCommon.FindNearestSettlement`，只在普通城镇、
  城堡和村庄中选地图距离最近者，排除藏身处等特殊地点；百科链接与纯文字兜底都改用同一个结果。
- 目标落败时新增并持久化待领赏起始小时。前五天仍保留玩家主动向任意灰袍领主交付的原流程；满
  五天仍未结案时，从离玩家最近的城镇或城堡派出一支 `10` 人、无英雄领队的灰袍骑士结算队。
  该队沿用原版 `CustomPartyComponent`、地图移动、`EngageParty` 接触和对话系统，带临时任务口粮及
  可选海战船只，不创建自制界面。接触玩家后使用独立玩家视角对话支付原先锁定的赏金，结束原版
  任务、执行同一战争善后并在对话关闭后销毁结算队；若玩家先向领主交付，地图上的结算队也会清理。
- 结算队不另外保存容易失配的运行时对象引用，而以 `gwp_bounty_collect_` 前缀从原版已保存的地图
  部队中重建；会话启动和每小时检查都会复用或清理现有队伍，避免存读档后重复派遣。诊断版在派出
  和付款时分别记录 `BOUNTY_COLLECTION_COURIER_DISPATCHED` 与
  `BOUNTY_COLLECTION_COURIER_PAYMENT_COMPLETE`，方便下一次实机验收直接核对后台。
- 负数剩余时间的确定原因是完成后为防止领奖阶段超时而把 `QuestDueTime` 改为
  `CampaignTime.Never`，但 `BountyHunterQuest.IsRemainingTimeHidden` 仍固定返回 `false`，原版界面
  于是把“永不超时”的特殊时间值当作普通日期相减。现在仍保留待领赏无期限，同时在可存档的
  `_readyForTurnInLogWritten` 状态为真时隐藏剩余时间；追捕阶段继续正常显示四十五日倒计时。
- 最终 Bannerlord `1.4.7` 诊断版完整重建为 `0` 错误、`44` 条既有可空性/离线 NuGet 警告；
  `1.4.5.115026`、`1.4.6.115628` 交叉重建均为 `0` 错误、`43` 条既有警告。实机客户端与编辑器
  DLL 均为 `755712` 字节，SHA-256 均为
  `4BBA39EA0D4CD10097FA9A0263BB3B0FE56789CE8079FA0BA70327DDA0FE8AF9`。
- 仓库 `_Module` 的 `27` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`；实机 `20` 个
  XML 解析失败 `0`，中文 string id 重复 `0`。仓库与实机中文 README 哈希均为
  `6DAF084D11D2DB1BCE2A437B073CC151FDF93EC0D64809F0F7C3F26EC721495E`，英文均为
  `74A60524486F35281EB606E345122BCC91C10D15B795304452D1038D0FC29DF0`。ILSpy 对最终实机 DLL
  确认百科最近地点、`120` 小时派遣门槛、结算队生成/追踪/对话/付款/清理、待领赏起始存档键以及
  条件式隐藏剩余时间均已进入产物。仍需实机分别验证百科靠近村庄时的链接、目标落败五日后结算队
  主动接触、结算前后存读档以及待领赏任务不再显示负数；本轮不制作正式 ZIP，不发布 GitHub。

## 2026-08-02 玩家悬赏三选一、统一难度赏金与状态代码收束

- 用户已经实机完成上一轮 P0 验收，结论为“完美”。本轮在该已验证生命周期上继续整理玩家悬赏，
  保留任意灰袍领主交付、目标落败消息、护送释放、待领赏无期限、战争来源保护与四十五日超时；
  未再给目标消失、第三方击败等情况建立单独失败分支。这些情况在玩家未亲自完成任务时统一留到
  四十五日超时，且失败不扣钱、不扣声望，也没有其他惩罚。
- 派单仍从原版右侧地图通知进入，没有增加模组面板、Gauntlet 电影或自建界面。玩家点击通知后，
  使用原版 `MBInformationManager.ShowMultiSelectionInquiry` 展示固定的“最近、较难、较简单”三行；
  选中一行后再用原版 `InformationManager.ShowInquiry` 查看目标、罪名、最后出现地点、评估难度、
  固定赏金、期限与交付规则，并可接受或拒绝。有效案件不足三宗时仍保留三行结构，但缺失的独立
  候选会以不可选状态说明原因。
- 候选只从仍有未结案件、目标部队存活并在地图上可追捕、且不是玩家自己的案件中产生。先选择距
  玩家最近的目标，再从剩余案件中选择战力最高者作为“较难”，最后从其余案件选择战力最低者作为
  “较简单”；三行永不重复指向同一案件。详情弹窗和最终接受动作都会重新检查目标是否仍可追捕，
  避免玩家查看契约期间目标进入城镇或案件关闭后接到失效任务。
- 难度以接单时原版 `Party.EstimatedStrength` 比较目标部队与玩家部队：不高于玩家战力的
  `0.75` 倍为较简单，不低于 `1.25` 倍为较难，中间为标准。赏金彻底取消按人数相乘，改为三个
  统一档位：较简单 `10000`、标准 `20000`、较难 `30000` 第纳尔；接受契约时锁定难度与赏金，
  后续伤亡、增兵或读档不会重新计算付款。
- 按用户明确要求不做旧悬赏存档适配：删除旧 `gwp_bounty_target_size`、
  `gwp_bounty_pending_reward`、按人数回算、旧任务加载回调、小时级二次重连标志和旧案件字段补齐。
  新流程只持久化当前目标、锁定赏金、追捕期限、待交付状态、护送和战争来源；会话启动时直接绑定
  原版 `QuestManager` 中仍在进行的悬赏任务，显示任务缺失时才按当前持久化状态重建一次。没有有效
  锁定赏金或追捕期限的旧状态直接清理，不再用猜测值修复旧档。
- 最终 Bannerlord `1.4.7` 诊断版执行 `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条
  既有可空性/离线 NuGet 警告并自动部署；同一最终源码对
  `Bannerlord.ReferenceAssemblies 1.4.5.115026` 与 `1.4.6.115628` 交叉重建均为 `0` 错误、
  `43` 条既有警告。实机客户端与编辑器 DLL 均为 `750080` 字节，SHA-256 均为
  `BE65E3CF16FF5F1A440A5B8A7FB3E0ADB4A3CE00648CA2C9D422859A544018C7`。
- 仓库 `_Module` 排除 `Assets`、`AssetSources`、`RuntimeDataCache` 后共有 `27` 个正常客户端
  文件，与实机模块相比缺失 `0`、SHA-256 不一致 `0`；实机 `20` 个 XML 解析失败 `0`，中文
  string id 重复 `0`。仓库与实机中文 README 哈希均为
  `9380D496B07459C7D7CF6B4E9950A0793DFA158A8E87F25FCC7440DD603B67DE`，英文均为
  `8679A12D9F91BDF72041E0B7D5EEEAE15EADBB3A7DE8DA0272D8B80F0DE4355D`。ILSpy 对最终实机 DLL
  确认三选一原版询问、三种候选算法、`0.75/1.25` 难度阈值、`10000/20000/30000` 固定赏金、
  新 `gwp_bounty_reward` 存档键、单次会话恢复和四十五日超时均已进入产物，并确认旧人数赏金键、
  `RewardPerTroop`、旧任务加载回调和二次重连字段均不存在。尚需实机测试三选一的显示、三个档位
  接单与新档存读档连续性；本轮只是开发部署，不创建正式 ZIP，也不更新 GitHub Release。

## 2026-08-02 玩家受托悬赏 P0 生命周期、统一交付与读档连续性

- 本轮按玩家实机痛点完成 P0，没有新增案件面板或候选契约界面。目标落败后会使用原版
  `InformationManager.ShowInquiry` 弹出一次“悬赏目标已被击败”消息，明确告知协办灰袍已经结束
  护送，并提示向任意灰袍领主结案领赏。交付对话统一挂在普通灰袍领主的既有 `lord_talk` 流程；
  玩家和领主台词只谈通缉令、报告与赏金，不暴露状态机、存档键或其他开发信息。无领主巡逻队
  不再承担交付职责，有领主的原护送队也只在目标仍存在时跟随玩家；目标落败后立即释放原版 AI
  决策权并返回灰袍正常职责。
- 领赏条件从“护送领主优先、无护送时仅族长兜底”合并为一个入口：只要处于待结案状态，任意
  正常灰袍领主均可领取同一条玩家台词并支付锁定赏金。普通领主判定继续排除无英雄巡逻队、延迟
  巡逻队、正在以玩家为目标的执法队和其他特殊任务对话，避免抢占不适用的原版对话。
- 反编译确认原版 `QuestManager.HourlyTick` 到期只调用 `QuestBase.CompleteQuestWithTimeOut`，它会
  清理任务日志、跟踪对象和 `QuestManager` 注册，却不会知道或清理 `PlayerBountyBehavior` 另存的
  目标、护送与派单锁。因此原实现会出现任务已经超时、行为状态仍继续追踪并永久阻止新派单的
  确定性缺口，不能依赖“过一段时间让原版自行恢复”。本轮为 `BountyHunterQuest.OnTimedOut` 增加
  行为层回调，并让行为层自己的绝对截止小时执行相同兜底；四十五日到期会终止任务、释放护送、
  清除目标/赏金/重连字段并恢复后续派单，不增加额外失败惩罚。
- 完成后的待领赏阶段不再继承追捕期限：`MarkReadyForTurnIn` 将原版任务截止时间改为
  `CampaignTime.Never`，并且用可存档字段保证“向任意灰袍领主报告”的完成日志不会在每次读档时
  重复追加。因此玩家击败目标后即使存档退出，也不会在寻找领主期间被原版四十五日超时错误清走。
- 行为层新增并持久化绝对截止小时、接取时玩家势力、接取时是否已与目标势力交战、以及玩家是否
  真正进入过目标战斗。读档时优先重连存活的特殊任务；旧存档缺少新截止字段时从原任务
  `QuestDueTime` 恢复，缺少原任务时才按当前阶段重建兼容任务；待领赏任务一律恢复为无期限并继续
  接受任意领主交付。目标 ID、锁定人数、赏金、护送 ID 与战争来源字段继续随档保存，完成、超时、
  目标消失和退出灰袍都经过同一个幂等清理入口。
- 原结算会无条件结束玩家势力与目标势力的当前战争。本轮只在以下条件全部成立时调停：接取时双方
  尚未交战、玩家确实进入过本悬赏目标的战斗、结算时玩家仍属于接取时的同一势力。接取前已经存在
  的正常战争绝不由本任务结束；旧存档没有来源快照时也采取不自动议和的保守行为。
- 用户明确暂不需要主动放弃、失败惩罚、三宗候选窗口或报酬重做，本轮没有实现这些扩展。现有唯一
  悬赏询问、原版任务日志和领主对话继续作为全部交互面；只有完成时按要求新增一条原版样式消息。
- 最终 Bannerlord `1.4.7` 诊断版执行 `Release -t:Rebuild --no-restore` 为 `0` 错误、`45` 条既有
  可空性/离线 NuGet 警告并自动部署；同一最终源码对 `Bannerlord.ReferenceAssemblies`
  `1.4.5.115026` 和 `1.4.6.115628` 的完整交叉构建均为 `0` 错误、`44` 条既有警告。实机客户端
  与编辑器 DLL 均为 `749568` 字节，SHA-256
  `9B8BCD422763CC408E322884572028AC61303502E1676D51D6E1113238C85CFB`。
- 仓库 `_Module` 排除 `Assets`、`AssetSources`、`RuntimeDataCache` 后共有 `27` 个正常客户端
  文件，与实机模块相比缺失 `0`、SHA-256 不一致 `0`；实机 `20` 个 XML 解析失败 `0`，中文
  string id 重复 `0`。仓库与实机中文 README 哈希均为
  `C8A5622D6E092CB7BDB77A8DB84B9299F784BABA6D3893C9AFE19225FE1C5AB0`，英文均为
  `09DC6BAB0B046822A54DAE7CD39E2BFFFF5EBADB8B4B7BA639B9BC13B6935CEF`。ILSpy 对最终实机 DLL
  确认四十五日常量、超时回调、截止/战争来源存档键、完成消息、结束护送、任意领主条件以及待领赏
  `ChangeQuestDueTime(CampaignTime.Never)` 均已进入产物。上述是构建、部署与静态产物验证；仍需
  在游戏中分别测试“追捕中存读档”“击败后存读档再向另一位灰袍领主交付”和“四十五日超时”三条
  运行路径，不能把编译和反编译代替为实机存档验收。

## 2026-07-26 v1.4-r8 正式发行、玩家说明重写与无监控玩家包

- 本轮正式版本从 `v1.4-r7` 升为 `v1.4-r8`；`SubModule.xml` 内部版本和程序集版本按
  Bannerlord 三段数字格式写为 `1.4.8`，这里的末位表示灰袍第八次修订，不是对
  Bannerlord `1.4.8` 的版本依赖。公开兼容范围仍为 Bannerlord `1.4.5`、`1.4.6`、
  `1.4.7` 共用一个玩家包。
- 中英文玩家 README 参考 Nexus Mods 常见的“开头一句说明模组改变什么—按玩家能玩到的
  系统分组—简短安装兼容信息—按版本列 Added/Fixed”结构重新编写。旧版六条笼统的
  “当前可玩内容”改成完整但不写内部公式的玩家视角介绍，覆盖活跃灰袍家族、全大陆执法、
  案件与震慑、玩家正负声望路线、加入与悬赏、声望支援、练兵与实体换防、地方事务与收养、
  司法公库与海战兼容、百科信息和专属战斗内容。更新日志严格只保留 `r8` 与 `r7` 两个正式
  版本，`r8` 只记录相对 `r7` 的本轮玩家可见变化；`r7` 收束为其自身正式内容，不再把本轮
  修复倒填进旧版本。
- `docs/grey-warden-setting.md` 同步修正了已经过时的实现状态：练兵官的训练、真实换防和
  调兵订单，以及贵族事务协调官的封地申诉都已经实装；旧“通过交易界面立即付款并直接加兵”
  说明已改为下单、实体收集、升级锁定、练兵官交付和交付时付款的真实流程。
- 当前完整源码对本机 Bannerlord `1.4.7` 执行
  `Release -t:Rebuild --no-restore`，结果为 `0` 错误、`45` 条既有可空性/离线 NuGet
  警告；同一源码对 `Bannerlord.ReferenceAssemblies 1.4.5.115026` 和 `1.4.6.115628`
  交叉重建均为 `0` 错误、`44` 条既有警告。诊断版已部署到本地普通客户端和编辑器目录，
  两份 DLL 均为 `749056` 字节，SHA-256
  `2B0C269FD8445D29CBD05F61C400621F7F735A6864566DF21BD1B9EAD3A37B38`，程序集版本
  `1.4.8.0`。
- 仓库 `_Module` 排除 `Assets`、`AssetSources` 和 `RuntimeDataCache` 后共有 `27` 个
  正常客户端文件，与
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`
  相比缺失 `0`、SHA-256 不一致 `0`；实机 `20` 个 XML 解析失败 `0`，且不存在上述三个
  编辑器目录。仓库与实机中文 README SHA-256 均为
  `E15938272AC4F01638BE62FD33D9F0B5D6CC5A87D49C5BCBF7335CEBBBD0C7D8`，英文均为
  `D30ADE33D2F3D9E96D841B546907F6CF4C0027FC1E6439169A1DFBCB5AD9FBDB`。
- 正式玩家 DLL 使用
  `GwpDiagnosticsEnabled=false`、`DeployToLiveModule=false` 单独输出到
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\release-player-v1.4-r8`，
  为 `730112` 字节，SHA-256
  `CAFE5949BB89A38301CDA08991AF670E7CF32310A1AE8426608993E236A19F0C`。它与本地测试
  诊断版哈希不同，且没有覆盖实机 DLL。ILSpy 对独立输出和最终包内 DLL 的两次反编译都确认：
  `GwpAiDiagnostics.LogPath` 返回空字符串，全部写入和捕获方法为空，两个追踪判断恒为
  `false`；二进制中也不存在 `AppendAllText`、`StreamWriter`、`FileStream`、监控日志名
  或文档目录字符串。
- 干净暂存目录为
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\package-v1.4-r8-final`。
  包含 `30` 个文件并且只有一个顶层 `GreyWarden/`；正式内容为正常客户端资源、两个玩家
  README、客户端 `0Harmony.dll`、无监控玩家 DLL 和编译后的 shader cache。包内没有
  `Assets`、`AssetSources`、`RuntimeDataCache`、编辑器 DLL、PDB、脚本、工具、日志、
  开发文档或嵌套压缩包。独立解压目录
  `build-check\verify-package-v1.4-r8-final2` 的 `30` 个文件与暂存目录相比缺失 `0`、多余 `0`、
  哈希不一致 `0`；`20` 个 XML 解析失败 `0`，中文本地化 `837` 个 string id 重复 `0`。
- 本地正式文件为
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4-r8.zip`
  及同名 `.zip.sha256`。ZIP 为 `349845455` 字节，SHA-256
  `150009FADF4780F5CC149524DD2A99908B8EE8CFC6BFC4F2450A36DF11B955C3`；校验文件为
  `90` 字节且内容与实算一致。完成文件表、解压哈希、包内 DLL 和无监控反编译审计后，
  已删除本地旧 `v1.4-r7` ZIP/校验文件，游戏 `Modules` 父目录只保留最新 `v1.4-r8`
  正式包对。
- 正式代码提交为 `634a7d4ea23154b724d9bf2d2593c821714cf48e`，`main` 与带注释标签
  `v1.4-r8` 均已推送，标签解引用后准确指向该提交。GitHub Release 地址为
  `https://github.com/Lucicain/GW/releases/tag/v1.4-r8`，已设为 latest，状态不是 draft
  或 prerelease。远端 ZIP 资产为 `349845455` 字节，GitHub 报告的 SHA-256 digest 与本地
  `150009FA...B955C3` 一致；远端校验文件为 `90` 字节，SHA-256
  `F14FEE995E2F6307DF386384E173F0CA18A1C05F085D7A32769A707A39EBC1BB`，也与本地一致。

## 2026-07-25 人物百科地点链接改回原版小弹窗

- 用户实机点击人物百科“案底与震慑”后立即出现报错弹窗，并明确否决上一版独立大面板的
  外观；最终目标改为完全保留此前原版 `SingleQueryPopup` 的小弹窗尺寸、边框、滚动区和
  “关闭”按钮，只让正文地点名多出可用的百科链接。
- 对应游戏进程 PID `57072` 的 `rgl_log_errors_57072.txt` 没有托管异常内容，
  `watchdog_log_57072.txt` 只确认运行时崩溃且用户取消 dump/报告生成，因此没有可用于
  符号化的异常栈，不能把报错精确归因到旧自制 XML 的某个控件属性。可以确认的是失败入口
  正是新增的 `GwpDeterrenceDetailsScreen` 自建屏幕层和大面板电影；该整条自制界面路径
  已删除，不再继续猜测修补。
- 反编译 Bannerlord `1.4.7` 的 `GauntletQueryManager`、
  `SingleQueryPopUpVM`、`PopUpBaseVM` 和原版 `SingleQueryPopup.xml` 后采用更窄的改法：
  人物百科重新调用 `InformationManager.ShowInquiry`，因此弹出、暂停、焦点、手柄键位、
  关闭与排队全部回归原版全局询问管理器。模组中的 `SingleQueryPopup.xml` 是本机原版文件
  的逐字派生副本；布局、尺寸和全部原版素材引用不变，只把说明正文的 `RichTextWidget`
  绑定到 `Command.LinkClick="ExecuteLink"`，并把正文刷子换成继承
  `Popup.Description.Text` 的灰袍刷子。该刷子的默认文字外观完全继承原版，仅补入原版
  `Info.Text` 使用的棕色 `Link.Settlement` 常态、悬停和按下样式，因此普通正文仍保持
  原版小弹窗观感，只有可点击地点获得明确视觉反馈。
- `SingleQueryPopUpVM` 构造后通过既有原生 VM 扩展器只追加一个
  `ExecuteLink(string)` 命令。该命令只在灰袍人物百科详情打开期间生效；点击地点时先调用
  `InformationManager.HideInquiry()` 走原版关闭路径，再把链接交给
  `Campaign.Current.EncyclopediaManager.GoToLink`。原版询问关闭时会清除激活标记，普通
  询问即使共享同一富文本模板也不会被误当成灰袍地点跳转。
- Bannerlord `1.4.7` 诊断版完整重建为 `0` 错误、`45` 条既有可空性/离线 NuGet
  警告；相同完整源码针对 `1.4.5` 与 `1.4.6` 参考程序集交叉重建均为 `0` 错误、
  `44` 条既有警告。最终诊断版已部署到普通客户端和编辑器目录，两份 DLL 均为
  `748032` 字节，SHA-256
  `290CDA215BF86BDBF857D4C1549FF72AFC55FC908F24EBE58E949F10587DA363`。
- 仓库 `_Module` 排除明确的编辑器素材例外后共有 `27` 个正常客户端文件，与实机相比
  缺失 `0`、哈希差异 `0`；实机 `20` 个 XML 解析失败 `0`。新原版派生小弹窗和链接刷子
  均已实装，旧 `GwpDeterrenceDetails.xml` 大面板已从实机删除。逐行比较确认派生弹窗
  除正文刷子和 `Command.LinkClick` 绑定外与原版布局完全一致。中英文 README 仓库/实机
  哈希分别一致为
  `7E46A5A9CD03D245D2F3A3FA0D01F66479BE058F4C37B9A490FBDA51C1C68E98` 与
  `1BEFAF91E7D25341F3FC13A0032DE6A55906A6BF4D920E2502EE7E8AEFDE668E`。
  ILSpy 从最终实机 DLL 确认人物百科入口重新调用 `InformationManager.ShowInquiry`，
  地点仍读取 `EncyclopediaLinkWithName`，询问 VM 构造补丁会附加链接命令，点击处理依次
  调用 `HideInquiry` 和 `EncyclopediaManager.GoToLink`。没有创建或替换正式玩家 ZIP；
  最终仍需实机点击复测，因为上一次用户取消 dump，且编译/反编译无法替代 Gauntlet 资源
  在真实启动顺序中的载入验证。

## 2026-07-25 人物百科案底详情改为正文内地点超链接

- 用户否决了人物百科“案底与震慑”弹窗下方另加“在百科中查看某地”选项的中间方案，要求
  与悬赏、赎罪任务日志一致：地点名称本身就是超链接，点击文字直接进入地点百科；本轮只改
  人物百科这一处，用户暂时想不起的其他界面不猜测、不扩大范围。
- 对 Bannerlord `1.4.7` 原版界面和 `RichTextWidget` 反编译确认：原版
  `SingleQueryPopup.xml` 虽用富文本控件显示说明，却没有
  `Command.LinkClick` 绑定，`SingleQueryPopUpVM` 也没有处理百科链接的
  `ExecuteLink`，因此仅把 `EncyclopediaLinkWithName` 塞进旧 `InquiryData` 只能画出
  链接样式，点击事件没有去处。人物百科原页则明确使用
  `Command.LinkClick="ExecuteLink"`，其 VM 再把链接交给
  `Campaign.Current.EncyclopediaManager.GoToLink`；最终实现复用的正是这条原版事件路径，
  没有全局替换原版通用询问窗口。
- 新增专用 `GwpDeterrenceDetails` Gauntlet 覆盖层和
  `GwpDeterrenceDetailsVM`。详情正文改用继承原版 `Info.Text` 链接样式的
  `RichTextWidget`，并绑定 `Command.LinkClick="ExecuteLink"`；地点变量直接保留
  `Settlement.EncyclopediaLinkWithName`，点击棕色地点名会先关闭覆盖层，再进入该地点百科。
  无法解析为定居点时仍显示原来的普通位置文字，不制造错误跳转。弹窗底部现在只有“关闭”，
  已删除旧“在百科中查看某地”按钮及其专用本地化条目。这个独立大面板实机打开时报错且
  外观不符合用户要求，随后被上一节的原版小弹窗派生方案完全删除。
- Bannerlord `1.4.7` 诊断版完整重建为 `0` 错误、`45` 条既有可空性/离线 NuGet
  警告；相同完整源码针对 `1.4.5` 与 `1.4.6` 参考程序集的交叉重建均为 `0` 错误、
  `44` 条既有警告。最终诊断版已部署到普通客户端和编辑器目录，两份 DLL 均为
  `748032` 字节，SHA-256
  `CC10D27B4B74EE810255DE314E7C8EA19F1C8126A899D90E76D490103ABF2054`。
- 仓库 `_Module` 排除明确的 `Assets`、`AssetSources` 编辑器素材例外后共有 `26` 个正常
  客户端文件，与实机相比缺失 `0`、哈希差异 `0`；实机 `19` 个 XML 解析失败 `0`，
  新 `GwpDeterrenceDetails.xml` 已存在并包含
  `Command.LinkClick="ExecuteLink"`。中英文 README 仓库/实机哈希分别一致为
  `EB4425BAF7599548540578339F2AB564B8FA576320CBCA444446B89442B1CE80` 与
  `0975FE2FD8B1C89310C338182897F27BA72ED339C0F9A8E58754A402FB2C54C1`。
  ILSpy 从最终实机 DLL 确认详情构造读取 `EncyclopediaLinkWithName`，入口调用专用覆盖层，
  `ExecuteLink` 先关闭覆盖层再调用 `EncyclopediaManager.GoToLink`。没有创建或替换正式
  玩家 ZIP；编译、XML 和反编译校验不能替代游戏内鼠标点击，复测应进入任意人物百科的
  “案底与震慑”，确认棕色地点名可点击且底部只有“关闭”。

## 2026-07-25 悬赏、赎罪任务地点链接与人物百科位置跳转

- 用户把地点交互范围明确限定为悬赏任务日志、赎罪任务日志和人物百科中的位置；案件总卷、
  悬赏确认窗口、左下角临时消息及其他坐标显示均不改。现有灰袍对话中也没有需要单独改造的
  同类地点说明，因此本轮没有扩大到对话、案件总卷或通知界面。
- 对本机 Bannerlord 原版程序集反编译确认，`Settlement.EncyclopediaLinkWithName` 调用
  `HyperlinkTexts.GetSettlementHyperlinkText` 生成原版 `Link.Settlement` 事件链接；原版
  间谍、家族世仇和浪子等任务都把这个 `TextObject` 直接写入任务日志。灰袍原实现却先由
  `GetNearestSettlementName` 把最近定居点降成普通 `string`，所以截图中的“庞斯”只能显示
  为纯文本。
- `GwpText` 新增保留类型的 `Create` 入口，原有 `Get` 继续返回字符串，不改变旧调用方；
  悬赏与赎罪任务的 `WriteLog` 增加 `TextObject` 重载。悬赏首次目击、后续侦情、赎罪首次
  报告和后续探报现在都保留最近 `Settlement`，并将
  `EncyclopediaLinkWithName` 作为本地化变量写入任务日志。赎罪探报的左下角消息仍单独使用
  普通地点名，符合本轮不改临时消息的范围。
- 人物百科最初采用标准 `InquiryData` 并在下方提供“在百科中查看某地”按钮；这是确认
  通用询问窗口没有内嵌链接事件后的中间实现，随后按用户要求被上一节的专用富文本覆盖层
  取代。最终界面不再保留这个额外按钮。
- 第一次完整构建发现
  `PlayerBountyBehavior.DialogueAndNotification.cs` 缺少
  `TaleWorlds.CampaignSystem.Settlements` 引用，产生一个 `CS0246 Settlement`；失败产物
  没有作为有效部署。补入命名空间后，Bannerlord `1.4.7` 诊断版完整重建为 `0` 错误、
  `45` 条既有可空性/离线 NuGet 警告；`1.4.5` 与 `1.4.6` 参考程序集完整交叉重建均为
  `0` 错误、`44` 条既有警告。
- 最终诊断版已部署到普通客户端和编辑器目录，两个 DLL 均为 `747008` 字节，SHA-256
  `6085A600F4A77AAE392411DA53FA2A6FAF753C07776A2E636B189707294B7C89`。仓库
  `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希差异 `0`；实机 `18` 个
  XML 解析失败 `0`，且不含编辑器素材目录。中英文 README 仓库/实机哈希分别一致为
  `708F49A36D230578EF46B837E03254D4009B45882D2103066CDF153140E5ECEE` 与
  `F247F11573291982FB56AC16CD709A2D7203B95681DC4B684C8B0AFC1FC1A6EE`。
  ILSpy 从当时实机 DLL 确认悬赏与赎罪四个日志入口均读取
  `EncyclopediaLinkWithName`，两个任务类都保留 `WriteLog(TextObject)`；该次人物百科
  按钮方案随后由上一节的正文超链接取代。案件总卷和临时消息仍保持普通文字。没有创建或
  替换正式玩家 ZIP。

## 2026-07-25 调兵库存不足崩溃复现与实体会合换兵

- 后续 PID `60716` 的同一路径复现终于生成完整
  `C:\ProgramData\Mount and Blade II Bannerlord\crashes\2026-07-25_11.26.48\dump.dmp`
  （约 `1.04 GiB`），并推翻了先前“报错可能来自跨地图换兵”的定位。RGL 明确记录
  `21:26:40.509` 玩家选择 `gwp_player_troop_order_file`，`21:26:42.131` 点击多选框
  “发送订单”后立即在 `MultiSelectionQueryPopUpVM.ExecuteAffirmativeAction` 内抛出
  `NullReferenceException`；灰袍日志没有 `PLAYER_TROOP_ORDER_FILED`，也没有任何会合或换兵
  记录，证明报错发生在订单回调内部，首个小时处理尚未开始。
- ButterLib 保存的完整符号栈依次为
  `TroopRosterElement.get_WoundedNumber` →
  `GreyWardenTroopRequestBehavior.CountHealthy` →
  `FileTroopOrder` →
  多选框确认回调 →
  `ExecuteAffirmativeAction`。用 Mono.Cecil 对实机 DLL 的元数据 token
  `0600290D`、`06000203`、`060001E0`、`0600084C` 和 `06000602` 逐项解析后与上述方法完全
  对应，不再只是时间相关性推测。
- 根因是 `CountHealthy` 用 `FirstOrDefault` 查指定兵种。练兵官一个该兵种都没有时，
  Bannerlord 返回默认 `TroopRosterElement`，其中 `Character == null`；其 `Number`
  getter 能返回零，但 `WoundedNumber` getter 会先直接读取 `Character.IsHero`，所以恰好在
  “库存为零”时空引用。这也准确解释了用户观察到有时正常、有时发送订单立刻报错：练兵官已经
  持有至少一个指定兵种时匹配元素有效，完全没有时才触发。
- `CountHealthy` 现在在读取 `WoundedNumber` 前先检查
  `element.Character == null`，未找到兵种直接返回 `0`，让订单正常进入后续实体收集流程；
  调兵换兵的批次转移辅助函数也加入相同的无效元素保护，防止名单发生变化时再次读取默认结构。
  实体会合状态机本身保留，但它是库存判定修好后才会运行的后续机制，不再把它误写成本次
  “点击发送立即报错”的根因。
- 根因修复后的 Bannerlord `1.4.7` 诊断版完整重建为 `0` 错误、`45` 条既有警告，
  `1.4.5` 与 `1.4.6` 交叉重建均为 `0` 错误、`44` 条既有警告并已部署。实机客户端与
  编辑器 DLL 均为 `746496` 字节，SHA-256
  `42AFFE86E7E839AD4183347EF44E03C545F351755AD9A95F08EFE214CF7C96C8`；仓库 `_Module`
  的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希差异 `0`，实机 `18` 个 XML
  解析失败 `0`，且不含编辑器素材目录。中英文 README 仓库/实机哈希分别一致为
  `9B1B615711498F225F8CFA115228B5429082894CD89D6B8DFB83D2B074ADECDD` 与
  `5FD65FE9C36FE95075D30F248A38D60C093B51AF2B82E2B33C63ABE13712F085`。
  ILSpy 从实机 DLL 确认 `CountHealthy` 在 `FirstOrDefault` 后先执行
  `val.Character == null` 返回零，之后才读取 `Number` 和 `WoundedNumber`。
- 用户再次以“玩家下达招兵任务、练兵官没有足够指定士兵”的路径触发报错。本次是新的游戏
  进程 PID `59232`，不能沿用上一轮 PID `37296` 对另一个模组战斗回调的结论。
  `rgl_log_59232.txt` 记录玩家于 `18:37:44.255` 选择调兵对话，`18:37:47` 正常结束交谈；
  Windows Application Error `1000` 随后记录启动器进程于 `18:38:01` 发生原生访问冲突
  `0xc0000005`。`watchdog_log_59232.txt` 表明用户取消了 dump/报告，RGL 错误日志也没有托管
  异常栈，所以现有证据能确认复现入口和时间关联，不能把原生崩溃符号化到某一条 CLR 指令。
- 新灰袍诊断在对话前确认练兵官梵蒂约 `201` 人，规模比例约 `0.931`，即上限约 `216`，
  符合“指定库存不足且接近满编”的复现条件。旧实现的
  `RefreshTrainerStock` 会在练兵官和另一名领主仍位于地图不同位置时直接改写双方
  `MemberRoster`，而且旧顺序先向接近满编的练兵官加入整批来兵、再移出等量旧兵；这既不是
  用户要求的实体换兵，也会让实时部队名单短暂超过原规模。由于本次没有 dump，不能宣称该
  瞬时超编就是已符号化的唯一崩溃指令，但该远程名单写法本身已被确认需要移除。
- 用户明确纠正目标：调兵订单必须复用练兵官普通 AI 换防的实体流程。现在练兵官缺少可通往
  指定兵种的兵员时，只选择一支满足普通练兵换防空闲条件的真实灰袍领主队；双方复用
  `GreyWardenTrainingBehavior.FindRendezvousSettlement`，前往两队中点附近最近的非敌对、
  未被围城城镇或城堡，并复用相同的移动意图有效期和
  `GwpTuning.Training.ExchangeStayHours` 驻留时间。两队未同时进入同一定居点并完成驻留前，
  不会改动任何士兵名单。
- 会合来源队在任务期间写入调兵订单保留状态，普通案件、协力、玩家事务及普通练兵换防不会
  再把它分配走；选择前仍要求它是活动领主队、未解散、未参军、未参战、未依附，并满足普通
  换防已有的案件、救济、重建和事务排除条件。来源失效、兵员变化或会合点失效时会清除双方
  移动意图并重新选人，不会把失效引用留在订单里。来源与会合点、驻留开始时间和上一来源均
  已加入存档字段，读档后可以继续同一段实体路程。
- 只有驻留完成后才进行一换一：练兵官交出无法升级到订单目标的健康灰袍兵，来源领主交出
  已是目标兵种或能继续升级到该目标的健康灰袍兵。双方来兵和去兵先分别暂存在无主
  `TroopRoster`，两边都腾出真实位置后再同时写回，因此两支真实部队在任何一步都不会超过
  交换前人数。一次来源不足时只交换其实际可给数量，释放该领主并在下一小时优先寻找另一名
  可用领主；全部目标兵备齐后立即解除会合来源保留，再由既有升级锁定和练兵官亲自交付流程
  接管。
- 新诊断链为
  `PLAYER_TROOP_ORDER_FILED` →
  `PLAYER_TROOP_ORDER_STOCK_RENDEZVOUS_ASSIGNED` →
  `PLAYER_TROOP_ORDER_STOCK_STAY_STARTED` →
  `PLAYER_TROOP_ORDER_STOCK_EXCHANGED` →
  `PLAYER_TROOP_ORDER_STOCK_RENDEZVOUS_RELEASED`，会记录来源、会合点、双方暂存/移入/移出
  数量和练兵官当前备齐数；离开会合点还会写
  `PLAYER_TROOP_ORDER_STOCK_STAY_RESET`。这些记录能在下一次实机测试中明确证明没有远程换兵。
- 第一次编译发现新文件缺少 `TaleWorlds.CampaignSystem.Settlements` 命名空间，产生三个
  `CS0246 Settlement`，该失败构建未作为有效产物；补入正确引用后 Bannerlord `1.4.7`
  完整重建为 `0` 错误、`45` 条既有可空性/离线 NuGet 警告，`1.4.5` 与 `1.4.6`
  参考程序集交叉重建均为 `0` 错误、`44` 条既有警告。
- 最终 `1.4.7` 诊断版已自动部署。仓库 `_Module` 排除明确的编辑器素材例外后共有 `25`
  个正常客户端文件，与实机相比缺失 `0`、哈希差异 `0`；实机 `18` 个 XML 解析失败 `0`，
  且不含 `Assets`、`AssetSources` 或 `RuntimeDataCache`。客户端与编辑器 DLL 均为
  `746496` 字节，SHA-256
  `95AEBD78452C9A5C0BB3BC7D0FB78CE609A5C2E112DB4860D015E6C7E8BA8A36`；实机中文
  README 为
  `AEB8B580031D6EA872A9E6C6653DD2A6FD0A065863A7C0283F39AE94E62451BA`，英文为
  `0964730B3B52E6E2B6ECECEE0F72ACF3564A163E533A4A76C3E234048CF3E9A6`，均与仓库一致。
- ILSpy 从实机客户端 DLL 确认：来源队被纳入订单保留判断；选人调用普通换防空闲条件，会合
  点调用普通练兵的 `FindRendezvousSettlement`；代码先同时检查两队
  `CurrentSettlement`，再等待实际常量 `2` 小时，之后才调用
  `ExchangeStockAtRendezvous`；交换方法创建两个无主 `TroopRoster` 后分别暂存双方士兵。
  因此最终产物中不存在原先跨地图直接遍历多个领主并当场换兵的入口。
- 本轮没有创建或替换正式玩家 ZIP。构建、部署和反编译能证明实体会合状态机已进入 DLL，
  但不能替代缺失的原生 dump，也不能代替下一次实机路线验证；复测应观察梵蒂与来源领主确实
  在地图上前往同一定居点、驻留后才出现 `STOCK_EXCHANGED`，库存仍不足时再出现新的
  `STOCK_RENDEZVOUS_ASSIGNED`。

## 2026-07-25 协力军团派出极速追查队

- 用户要求仍在集结追击的协力军团也能派出极速追查队，而不是必须等协力军团先因速度差完成
  分散。`UpdateTasks` 现在在案件进入 `WarPursuit` 后，对普通承办队、完整协力军团和已经速度
  分散的协力组统一调用 `TrySpawnImmediateCaseInterceptor`；同案已有一支未返程的极速追查队
  时仍不会重复派出。
- 完整协力军团的新增资格严格绑定当前案件：来源必须是协力组长、目标必须等于协力组目标，
  且来源当前必须是该协力军团的真实军团长。其他原版军团、协力成员、正在战斗的部队和玩家
  目标均不能借此入口分兵。
- 速度判断继续调用原版
  `PartySpeedCalculatingModel.CalculateBaseSpeed`。Bannerlord `1.4.7` 的
  `DefaultPartySpeedCalculatingModel.CalculateLandBaseSpeed` 会把军团长的
  `AttachedParties` 人数、骑兵、步兵、伤员、货物、负重、载重上限和俘虏一并计入，因此对
  完整协力军团调用时得到的是军团整体的理论正常速度，不是只看军团长本队缓存速度；既有封装
  仍排除战后混乱及湿地骑兵天气惩罚等临时噪声。
- 只有目标理论速度高于军团理论速度时才尝试真实分出三至八名健康骑兵。完整协力军团会从
  军团长和当前真实编入军团的登记协力领主中汇总健康骑兵，按等级优先抽调，不再错误地只检查
  军团长本队；普通承办队和已分散协力组仍只动用主理人本队。新队生成后再次用同一原版模型
  计算自身理论速度，不能超过目标就把每批士兵精确退还原来源部队并立即销毁。成功时诊断
  `IMMEDIATE_CASE_INTERCEPTOR_DEPLOYED` 新增 `trigger=assistance_army` 和实际兵源部队数，
  可与普通宣战分兵及速度分散后的分兵区分。
- `TryAssignDelayPatrolToAssistanceArmy` 新增极速追查队排除条件。否则完整协力军团派出的
  极速队会在下一次小时维护中被普通周期支援逻辑重新并回军团，立刻失去分兵意义；现在只有
  普通周期支援仍会加入军团，极速队会保持独立追击，案件结束后沿用既有返队流程把幸存者真实
  交还组长。
- Bannerlord `1.4.7` 诊断版执行 `Release -t:Rebuild --no-restore` 为 `0` 错误、`45`
  条既有可空性/离线 NuGet 警告并自动部署；相同完整源码针对 `1.4.5` 与 `1.4.6` 参考程序集
  的交叉重建均为 `0` 错误、`44` 条既有警告。实机客户端和编辑器 DLL 的 SHA-256 均为
  `DE96A4977EEB29BA3696F003DB420BA997A72B8CE3C25ADEC8587E6C2D1B06C9`。
- 仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希差异 `0`，实机
  `18` 个 XML 解析失败 `0`，且不含 `Assets`、`AssetSources` 或 `RuntimeDataCache`。
  中英文 README 仓库/实机哈希分别一致为
  `097AFEC6A182921D6B46444DFDAD9441257EE0B5BA298E99A971655DCF6B3C43` 与
  `FDC372B76EA22DDD4B777924C70A76D379FE6A9E6A2E78CB0A0B98111B683F85`。
  ILSpy 从实机 DLL 确认跨协力领主抽调、失败时按原兵源回滚、`trigger=assistance_army`、
  同案唯一极速队判断，以及 `state.IsImmediateInterceptor` 禁止重新并回协力军团均已进入
  产物。
- 本轮没有启动游戏，故构建和反编译只证明代码、接口与部署正确，不能代替实机案件推进。
  下一次验证应观察完整协力军团追赶更快目标时是否写出
  `IMMEDIATE_CASE_INTERCEPTOR_DEPLOYED; trigger=assistance_army`，随后确认极速队未加入
  原军团，并在案件结束后写出 `IMMEDIATE_CASE_INTERCEPTOR_REJOINED`。

## 2026-07-25 现有诊断日志归档与活动目录精简

- 本轮仅整理已经生成的日志，不修改灰袍监控代码、游戏逻辑或另一个模组。整理前确认没有
  Bannerlord、TaleWorlds 或 Watchdog 进程运行。
- `C:\ProgramData\Mount and Blade II Bannerlord\logs` 中 `27` 个现有文件，加上
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`
  共 `1615.80 MB`，已完整归档为
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\Log Archives\Bannerlord-logs-before-cleanup-20260725-175748.zip`。
  ZIP 含 `28` 个文件，未压缩字节数与源文件逐项校验一致；归档大小为 `13.51 MB`。
- 归档校验成功后，原 `ProgramData` 活动日志目录已精简为 `0` 个文件，灰袍活动诊断日志也已
  移走。下次启动游戏会重新生成新的 RGL 和灰袍诊断日志。另一个项目的
  `Expelliarmus-FlyingWeapon-Diagnostics.log`、`Yujian-FlyingWeapon-Diagnostics.log`
  和 `BattlefieldSkills-FlyingWeapon-Diagnostics.log` 均未改动。
- 如需恢复旧证据，必须先关闭游戏；把 ZIP 内 `ProgramData-logs/` 的文件复制回
  `C:\ProgramData\Mount and Blade II Bannerlord\logs`，把
  `Documents-diagnostics/` 的文件复制回
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord`。分析后可再次移走，不应在游戏
  运行时覆盖活动日志。

## 2026-07-25 练兵任务测试弹窗崩溃诊断：除你武器调用无效战斗 Team

- 本次运行对应 PID `37296`。Windows Application Error `1000` 记录游戏进程于
  `2026-07-25 17:37:55 +10:00` 发生原生访问冲突 `0xc0000005`，故障模块未识别；Watchdog
  确认触发崩溃事件，但用户在弹窗中取消了 dump 和报告生成，因此没有可用于最终指令级符号化的
  TaleWorlds 崩溃包。对应 `watchdog_log_37296.txt`、`rgl_log_37296.txt`、
  `rgl_log_errors_37296.txt` 和灰袍诊断日志现保存在上一节注明的 ZIP 中，Windows
  Application 事件日志仍保留在系统中。
- 灰袍订单日志确认新锁定流程已经完整成功跑过一次：`17:37:09` 梵蒂为二十名
  `gwnewrecruit` 写出 `PLAYER_TROOP_ORDER_TARGET_LOCKED`，当时真实换入三人、目标经验发放
  为零；`17:37:24` 正常写出 `PLAYER_TROOP_ORDER_DELIVERED`，`17:37:25` 又以
  `stage=None` 完成交谈并解除订单。玩家于 `17:37:44` 发起第二次下单对话，但崩溃前没有第二
  条锁定、经验、交换或交付记录，战役时间也只从 `628187.65` 走到 `628187.66`，尚未跨过
  下一次每小时订单处理。故本次访问冲突不是在
  `RefreshTrainerStock`、`LockOrderedTroopIfReady` 或升级模型中发生。
- 同一进程在更早的 `17:29:17.489` 显示“除你武器！”后，从
  `17:29:17.495` 到 `17:31:02.483` 连续写出
  `SCRIPT ERROR: Condition not satisfied: IMono_MBTeam::is_enemy:
  other_team_index_invalid!`。两份 RGL 日志因此分别膨胀到约 `683 MB`，错误文件几乎成为一条
  连续的无效 Team 调用记录。对应
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\Expelliarmus-FlyingWeapon-Diagnostics.log`
  同时显示 `disarm=True, sharedControl=True`，到 `17:31:02` 技能关闭并释放两把物理武器后
  错误才停止。
- 精确源码入口位于
  `C:\Users\lucif\source\repos\BattlefieldSkills\BattlefieldSkills\Source\FlyingSwordMissionBehavior.cs`
  的 `IsHostileTo`：它只检查 `Agent` 和 `Team` 引用不为空，然后每帧直接执行
  `caster.Team.IsEnemyOf(candidate.Team)`。本次至少一侧仍有非空 Team 对象，但其原生
  `TeamIndex` 已经无效；因此托管空检查未拦住调用，持续进入
  `IMono_MBTeam::is_enemy`。这是当前可证明的错误源，也与随后 Windows 记录的原生访问冲突
  类型吻合。
- 结论：玩家是在第二次测试练兵任务后看到崩溃弹窗，但时间上的最后操作不是代码归因。
  灰袍第一次锁定和交付已由实机日志证明成功，第二次订单处理尚未执行；本轮真正异常来自
  Expelliarmus 的敌我筛选对失效 Team 调用 `IsEnemyOf`。要消除该崩溃风险，应在
  BattlefieldSkills 中为 Team 的有效索引/当前 Mission 归属增加原生调用前保护，并在对象
  失效时清理锁定目标，而不是回退已经通过实测的灰袍订单锁定。本轮只完成诊断，未修改两个
  模组的运行代码、双语玩家 README、实机 DLL 或正式玩家 ZIP。

## 2026-07-25 满编精锐练兵官订单诊断与指定兵种临时锁定

- 玩家下达六十名灰袍新兵订单后，练兵官会立即被
  `GreyWardenTroopRequestBehavior` 预留：原有普通案件、协力、训练交换等职责会被解除，
  且订单完成前不会再接受普通灰袍案件。因此“卡单以后去接另一个案件”不会发生。
- 满编本身不会立刻卡死订单。每小时备兵刷新会从其他可用灰袍部队中寻找健康的订单目标兵种，
  并用练兵官手上无法继续升级到目标的士兵按一比一真实交换；部队总人数不变。目标若是兵种树
  根节点 `gwnewrecruit`，只有已经是 `gwnewrecruit` 的实体士兵能作为调入库存，精锐老兵和
  更高阶兵种都不能逆向降级成新兵。
- 如果其他灰袍部队合计有六十名健康新兵，练兵官即使满编且全是精锐，也能逐批用精锐换回
  六十名新兵；备齐后的下一次每小时 tick 会把订单切到交付阶段并前往玩家。若全族可调的新兵
  不足，现有逻辑不会删除精锐、凭空生成新兵、强制降级老兵或主动腾出名额重新招募，因此订单
  会停留在训练阶段，直到其他灰袍部队以后偶然产生新的可调库存。
- 原有训练逻辑还有一个会加重根节点新兵订单短缺的问题：
  `TrainForOrderIfDue` 会把“当前已经等于目标、但仍有后续升级分支”的新兵也列入经验发放
  队列。部分调入的新兵可能在原版每日升级时升成轻步兵，再次不满足“交付新兵”的计数条件。
- 训练阶段只建立“职责预留”，没有向欲望系统提交专属的招募、待命或交付移动意图。
  `TryPreparePartyForPlayerRequest` 清掉旧任务后，如果当时仍未备齐，原版 AI 欲望仍可让练兵官
  巡逻、进城或补给。玩家因此可能看到她一边处于未完成的训练订单，一边继续巡逻；这不是她
  接受了新案件，而是订单缺货时没有专属移动目标。
- 诊断结论：用户预估在“全族实际新兵库存不足”的条件下成立，但不是“练兵官满编”单独
  造成。缺货由根节点目标没有可训练来源、目标新兵仍会被加经验升级、缺货阶段没有补员意图
  三者叠加产生。
- 用户随后明确要求：指定兵种备齐后立即临时钉住，不再被练兵官经验或原版自动升级处理；
  练兵官携带原兵种前往玩家，交付、取消或资金不足终止订单后解除，恢复原版正常升级。
  `GreyWardenTroopRequestBehavior` 新增持久化
  `GWPP_PlayerTroopOrderUpgradeLocked`。每小时检查、真实换兵完成后及读档会话启动时，只要目标
  已经备齐，或旧档已经处于交付阶段，就把锁定设为真；即使途中战斗造成伤亡、订单退回训练
  阶段，锁定仍持续到订单真正结束。
- `TrainForOrderIfDue` 现在从订单经验队列排除“已经等于目标”的兵种；它仍会训练所有能够向
  目标升级的前置兵种。这样部分收集到的新兵不会再被本次订单经验主动推离目标，备齐时则会
  立即进入完整锁定。
- `PolicePartyTroopUpgradeModel.IsTroopUpgradeable` 在且仅在实际练兵官部队、当前订单指定兵种、
  锁定状态三项同时满足时返回 `false`。这会同时拦住原版每日部队 tick 和战斗结束后的
  `PartyUpgraderCampaignBehavior.UpgradeReadyTroops`；没有扣除或清零既有经验。订单结束时
  `ClearOrder` 清除锁定，下一次原版升级检查即可继续使用原有经验。
- Bannerlord 的 `TroopRoster` 按兵种聚合人数与经验，不能在同一名单项内单独标记其中六十名
  实体。因此锁定对象是练兵官名单中的“指定兵种整批”，而不是同兵种中不可辨认的部分人数；
  其他兵种仍正常训练和升级。该做法不复制、删除或替换士兵，也不改动订单的一比一真实调拨。
- Bannerlord `1.4.7` 诊断版执行 `Release -t:Rebuild --no-restore` 为 `0` 错误、`45` 条
  既有可空性/离线 NuGet 警告并自动部署；相同源码针对 `1.4.5`、`1.4.6` 参考程序集的完整
  交叉重建均为 `0` 错误、`44` 条既有警告。仓库 `_Module` 的 `25` 个正常客户端文件与实机
  相比缺失 `0`、哈希差异 `0`，`18` 个 XML 解析失败 `0`，双语 README 均只保留两条正式
  发布记录并与实机一致；实机不含 `Assets`、`AssetSources` 或 `RuntimeDataCache`。
- 实机客户端与编辑器 DLL 均为 `739840` 字节，SHA-256
  `B3945A3386A2E1E8273296A3CF42C9F1781039FDA6716124D58BDE1598E39D8E`；仓库与实机中文
  README 均为
  `AC8D74F13BF56A99444B811027A3F38AC4CEFE6CF2807255DBA620246F8F34DB`，英文均为
  `A0A06707997FB299FFE5B5F5D07062FFCEEF532B8E4DA3F20E9CEDE9475D3729`。ILSpy 从实机 DLL
  确认新存档键、备齐锁定、目标兵种经验排除、升级模型拒绝、订单结束解锁及诊断
  `PLAYER_TROOP_ORDER_TARGET_LOCKED` 均已进入产物。本轮没有启动游戏，实际订单备齐和交付
  流程由用户继续游玩验证；普通开发构建没有创建或替换正式玩家 ZIP。

## 2026-07-25 练兵官长期升级同一兵种的原版机制诊断

- 灰袍练兵代码没有直接升级兵种，也没有选择重步兵、弓箭手或骑士分支。
  `GreyWardenTrainingBehavior.TrainPartyIfDue` 每六小时只调用
  `TroopRoster.AddXpToTroop`，把经验加给仍有升级目标的灰袍士兵；真正升级仍由原版
  `PartyUpgraderCampaignBehavior` 在每日部队 tick 或战斗结束时处理。
- 本机当前 `TaleWorlds.CampaignSystem.dll` 的原版流程不是在多个分支间平均随机。
  `DefaultPartyTroopUpgradeModel.GetUpgradeChanceForTroopUpgrade` 遇到多分支兵种时，如果领主
  设置了 `PreferredUpgradeFormation`，包含该阵型的分支权重为 `9999`，其他分支各为 `1`；
  如果没有设置偏好，则由领主存档中的固定 `RandomValue`、基础兵种 id 和兵阶算出一个固定
  分支，仍给它 `9999` 权重。灰袍轻步兵的 XML 顺序是重步兵、弓箭手、骑士三支，所以固定
  分支每次约有 `9999 / 10001`，即 `99.98%` 的概率被选择；对同一领主和同一基础兵种，这个
  结果不会每天重新轮换。
- `PartyUpgraderCampaignBehavior` 选中分支后，会把该兵种当前所有满足经验、健康、工资和金币
  条件的 `PossibleUpgradeCount` 整批升级到同一目标，而不是逐名独立抽签。因此练兵官每六小时
  给一大批轻步兵同步增加经验，会进一步放大原版的固定偏向，看起来就像她始终只训练一种精锐。
  这不是经验发放代码误把分支写死，而是原版领主部队升级机制本身的设计。
- 原版使用 `9999` 不是算术溢出或随机数错误，而是有意在统一的加权抽签器里制造“几乎强制”
  的软偏好：不需要另外写一个硬选分支，同时仍给其他分支留下极小概率。领主已有阵型偏好时，
  它塑造固定兵种倾向；没有明确偏好时，稳定哈希仍让不同领主在不同读档和日期中保持自己的
  固定倾向。原版领主通常从多个不同文化兵种树招募，某一棵树偏科仍能由其他基础兵种补足阵容；
  灰袍却把绝大多数兵员集中到同一个三分支轻步兵节点，再由练兵官同步发经验，所以这个原版设计
  在灰袍树上被放大成近乎单一兵种。它不是原版所有场景都会出错，但不适合当前灰袍兵种结构。
- 用户随后明确要求三个分支直接等权。新增
  `PolicePartyTroopUpgradeModel : DefaultPartyTroopUpgradeModel` 并在战役模型中注册；只在实际
  灰袍 AI 部队、灰袍兵种且升级目标多于一个时，把每个有效目标的
  `GetUpgradeChanceForTroopUpgrade` 统一返回 `1`。当前轻步兵的重步兵、弓箭手、骑士三个
  有效分支因此各占 `1/3` 权重。其他家族、非灰袍兵种、单分支新兵以及无效索引全部继续调用
  原版模型。
- 改动只覆盖原版抽签权重：升级仍由 `PartyUpgraderCampaignBehavior` 在原版时机执行，经验、
  金币、健康、工资、所需物品/技能、一次可升级人数和实体士兵替换均未改。原版仍会把同一次
  满足条件的整批轻步兵投入当次随机选中的一个分支，所以这是用户要求的“三路等概率”，不是
  强行维护最终三系人数绝对相等。
- Bannerlord `1.4.7` 诊断版执行 `Release -t:Rebuild --no-restore` 为 `0` 错误、`45` 条
  既有可空性/离线 NuGet 警告并自动部署；相同源码针对 `1.4.5` 和 `1.4.6` 引用程序集的完整
  交叉重建均为 `0` 错误、`44` 条既有警告。仓库 `_Module` 的 `25` 个正常客户端文件与实机
  相比缺失 `0`、哈希差异 `0`，`18` 个 XML 解析失败 `0`，双语 README 都只保留两条正式
  发布记录且与实机一致；实机不含 `Assets`、`AssetSources` 或 `RuntimeDataCache`。
- 实机客户端和编辑器 DLL 均为 `738304` 字节，SHA-256
  `DE908AD382807ED336B3D8A4F302FA01B77C0C3AA92BB5ADD0AF08011353A7F6`；仓库与实机中文
  README 均为
  `88E52EFEF7ED88E94A05A8477E40FBA53EB11C19171B0D5943085A9E77B41AE4`，英文均为
  `BF9C39DE42E196C9F806E3DDC2FB2CFFA4BF17E6134560192B9C86DF1C522B91`。ILSpy 从实机
  DLL 确认 `PolicePartyTroopUpgradeModel` 对符合条件的每个索引返回 `1f`，其他情况回退
  `DefaultPartyTroopUpgradeModel`，并确认 `SubModule` 已把它注册为战役
  `PartyTroopUpgradeModel`。本轮没有启动游戏，实际三路长期分布仍由用户继续游玩观察；没有
  创建或替换正式玩家 ZIP。

## 2026-07-25 晨曦十五艘船与战帆原版过量舰船处理诊断与修复

- 用户在实机界面观察到晨曦拥有 `15` 艘船。现有
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`
  （本次读取时最后写入 `2026-07-25 16:45:53 +10:00`）尚未记录舰船数量、船型、获得来源或
  舰船交易事件，所以旧日志只能确认晨曦当时约 `158～167` 人、仍是正常活动领主队，不能单独
  证明十五艘分别在哪场战斗获得。以下结论来自对本机当前
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\bin\Win64_Shipping_Client\NavalDLC.dll`
  和
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll`
  的 ILSpy 反编译。
- 战帆原版确实有过量舰船处理，但它不是“大地图去港口卖船”的欲望。
  `ShipTradeCampaignBehavior` 每日对非玩家、非强盗且未灭亡家族做一次后台决策：
  `NavalDLCShipLimitModel` 认为领主单队理想船数为 `3`；
  `NavalDLCClanShipOwnershipModel` 认为全族理想船数为原版家族出队上限乘 `3`。当前灰袍是
  六阶、非 minor 家族，原版 `DefaultClanTierModel` 的基础出队上限为 `3`，所以未计相关技能
  时原版只把灰袍全族约 `9` 艘视为理想值，并不是允许晨曦单队留十五艘。
- 只有全族舰船总数超过上述家族理想值时，原版每日才以 `10%` 概率尝试卖一艘，而且先在
  `WarPartyComponents` 中随机抽一支队伍。被抽中的队必须在陆地、没有地图战斗或围城、领主
  有效且仍活动；`TryGetShipToSell` 还要求删掉某一艘可交易船后能提高该队的舰队构成评分。
  一次成功最多卖一艘，不会立即把十五艘裁到三艘。
- 原版出售不是让领主产生访问港口欲望，而是直接把船的所有权交易给
  `clan.MapFaction.Fiefs` 中随机一座拥有有效造船厂的本方城镇。灰袍没有自己的封地或港口，
  所以 `GetTownToSellShip` 对灰袍返回空；即使每日 `10%` 检查抽中晨曦并挑出应卖船，也没有
  合法买方，交易不会发生。这是十五艘长期留在她身上的主要原版适配缺口。每日另有 `75%`
  概率在随机两支本族陆上队伍之间尝试交换一艘，只在全族总构成评分提高时执行，能偶尔重分配，
  但不能替代卖出，而且不保证抽中晨曦。
- 海战缴获本身也有原版筛选。`MapEvent.LootDefeatedPartyShips` 先给败方存活船追加相当于最大
  耐久 `20%～50%` 的战后损伤；`NavalDLCBattleRewardModel` 再让每艘只有 `50%` 概率进入
  可缴获池。AI 胜方只有在加入该船会提高自身
  `ShipDistributionModel.GetScoreForPartyShipComposition` 时才接收；未分配的船被销毁并按
  船价给合格胜方结算掠夺金。因此原版不是无条件把所有敌船塞给胜方，但人物后来损兵、舰队构成
  改变或连续多次在不同状态下获船后仍可能形成当前过量，而上面的出售路径对无港口灰袍失效。
- 模组现有 `PoliceResourceManager.GivePoliceShips` 每小时只在船数低于
  `ceil(当前部队人数 / 50)` 时补齐重型船，晨曦当前约一百六十人只需要约四艘；该方法不会主动
  把她补到十五艘，但注释和实现都明确“只追加缺失的船，不删除现有船”，所以也不会清理原版
  缴获或转入的多余船。原版另有部队解散时把船分给族人、剩余折现的兜底，但晨曦仍是活动队伍，
  不会触发。
- 已按用户确认实施上述修复。`PoliceResourceManager` 每日检查所有活动灰袍领主队；队伍必须
  在陆地、没有战斗或围城、领主有效且并未解散，船数超过
  `ceil(当前人数 / 50)` 时才进入出售。每队每天最多出售一艘，避免一次 tick 突然清空舰队；
  从可交易且售价为正的余船中先卖最低价值者，保留足够承载本队兵力的船。
- 买方改为距离该队最近、未被围城、拥有至少一级 `building_shipyard` 且与灰袍不处于战争的
  城镇，覆盖中立和友好造船港。仍调用原版
  `ChangeShipOwnerAction.ApplyByTrade`，所以船作为真实实体转入港口，原版按船况结算价格，
  并把收入交给灰袍家族领袖钱包，也就是既有司法公库；没有直接删船或额外造钱。
- 新增 `SHIP_ACQUIRED`、`SHIP_DISPOSED` 与 `SURPLUS_SHIP_SOLD` 诊断，今后会记录船体 id、
  所有权变化类型、原/新所有者、买方、售价、出售前后船数、保留需求和公库余额。它无法回填旧
  存档中十五艘船过去的逐船来源，但更新后再次缴获、转移或出售均可追踪。
- 首次增量构建在诊断所有者兜底文本中误用了不存在的 `PartyBase.StringId`，产生唯一错误
  `CS1061`；该失败构建未生成可接受 DLL。兜底改为明确的 `"party"` 后，
  Bannerlord `1.4.7` 执行 `Release -t:Rebuild --no-restore` 为 `0` 错误、`45` 条既有
  可空性/离线 NuGet 警告，并自动部署。相同源码针对 `1.4.5` 和 `1.4.6` 的完整引用程序集
  交叉重建也分别为 `0` 错误、`44` 条既有警告。
- 仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希差异 `0`；`18` 个 XML
  解析失败 `0`，实机不含 `Assets`、`AssetSources` 或 `RuntimeDataCache`。实机客户端和
  编辑器 DLL 均为 `737792` 字节，SHA-256
  `E83D004760194F3022FB5E3C73E91198596B4694C72DFC2AFB23D69EF325F0CE`；仓库和实机中文
  README 均为
  `49DDE2A23FB76532A6F90CB86FF713A7CD922ED643A43029BE391EAFA92B151D`，英文均为
  `78E7327A928898521AB74625AE908CC4F355057DF907AB7B5479B1B2E57EA982`。
  ILSpy 从实机 DLL 确认每日入口已调用 `SellSurplusPoliceShips`，出售条件、最近非敌对一级
  船厂筛选、正价最低价值船选择、`ApplyByTrade` 和三类诊断均进入产物。本轮没有启动游戏，
  晨曦实际每日减船和公库增款仍由用户载档验证；没有创建或替换正式玩家 ZIP。

## 2026-07-25 暮光追捕 `CharacterObject_1825` 未留下高速追截队诊断

- 最新监控
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`
  （最后写入 `2026-07-25 15:14:37 +10:00`）中的最新暮光普通案件目标为
  `CharacterObject_1825_party_1`。案件首次进入本段监控时是
  `campaignHour=625653.77`、`taskFlow=Pursuit`、`war=False`，暮光距目标 `93.78`；当时刚结束
  战斗处于混乱状态，缓存速度 `1.76`，但任务正确锁定的去除临时混乱后的单队理论速度为
  `2.98`。其后暮光持续用案件点接近目标，并非案件、欲望或职责丢失。
- 到 `campaignHour=625735.77`，双方距离 `2.85`，本案正常从 `Pursuit` 切为
  `WarPursuit` 并进入普通案件高速分兵入口。入口确实从暮光的健康骑乘兵中真实取出上限
  `8` 人建立候选队 `gwp_enf_delay_74190`，但原版即时速度复算结果为候选追截队
  `4.50`、目标实际移动队 `4.55`。代码要求追截队必须严格快于目标，因此写出
  `IMMEDIATE_CASE_INTERCEPTOR_TOO_SLOW`，随即把八人无损退回暮光并销毁空队；本目标没有
  `IMMEDIATE_CASE_INTERCEPTOR_DEPLOYED`。所以地图上看不到高速追截队不是没有触发，而是
  触发后以仅差 `0.05` 的速度未通过最终有效性校验。
- 目标在前一小时监控快照中的 `baseSpeed=4.37`，而旧分兵入口记录的 `targetSpeed=4.55`
  来自当刻直接读取的实际移动主体 `MobileParty.Speed`。继续核对后确认这里的比较口径不对：
  候选队生成在暮光当前位置，目标则在约 `2.85` 距离之外，双方瞬时 `Speed` 会分别混入所在
  地形、天气、战后混乱等临时差异，不能用来判断八名骑兵按部队构成是否真追不上目标。
- 后来出现的 `gwp_enf_delay_25328` 是既有的普通无领主纠察支援队
  (`partyKind=leaderless_delay_support`)，不是上述被回滚的即时高速分兵。它从
  `campaignHour=625770.29` 起以 `DirectAttackLock` 追同一目标，在 `625790.44` 建立地图
  战斗，暮光同时加入，战斗于 `625794.00` 结束。这也证明该案最终由普通支援的强制接战保底
  碰到目标，而不是高速队部署成功。
- 用户明确要求复用协力追捕已经使用的原版理论速度推算。普通案件高速分兵现在对承办人、目标
  实际移动主体和候选追截队全部调用同一个 `GetTheoreticalBaseSpeed`：内部仍由原版
  `PartySpeedCalculatingModel.CalculateBaseSpeed` 计算具体队伍，再按协力既有规则排除战后
  混乱与骑兵/骑马步兵天气惩罚，得到正常条件下的理论速度。是否需要分兵以及候选队是否严格
  快于目标都改用这一口径，不再比较 `MobileParty.Speed`。当前瞬时速度仍写入诊断，方便同时
  看出理论构成与现场地形状态，不参与去留决定；真实移兵、三至八人限制和速度必须严格更快的
  安全条件不变。
- 修正后的 Bannerlord `1.4.7` 诊断版执行
  `dotnet build .\GreyWardenPolicePurity.slnx -c Release -t:Rebuild --no-restore`，结果为
  `0` 错误、`45` 条既有可空性/离线 NuGet 警告并自动部署。仓库 `_Module` 的 `25` 个正常
  客户端文件与实机相比缺失 `0`、哈希差异 `0`；`18` 个 XML 解析失败 `0`，中文本地化
  `837` 个 string id、重复 `0`；实机不含 `Assets`、`AssetSources`、`RuntimeDataCache`，
  双语 README 与仓库一致。客户端与编辑器 DLL 均为 `731648` 字节，SHA-256
  `6E4B2F42635747149459BAE600A5019FA9008A7D7F39C6348B3665D14E549ECF`。ILSpy 从实机
  DLL 确认 `TrySpawnImmediateCaseInterceptor` 对来源、目标和候选队均调用
  `GetTheoreticalBaseSpeed`，两个最终分支分别使用理论速度比较，并同时保留三方
  `CurrentSpeed` 诊断。本轮没有创建或替换正式玩家 ZIP；新的理论速度数值与实际出队结果需由
  下一宗实机案件验证。

## 2026-07-25 九级震慑逐级恢复下限与对话触发率

- 九级总上限不变，同一犯罪方向的第 `n` 次被捕仍新增 `n` 级本人震慑（受九级总上限限制），
  但恢复目标不再一律归零。用户进一步明确下限应逐次只提高一级，所以第 `n` 次被捕后的永久
  下限为 `min(9, n - 1)`：第一次恢复到零、第二次恢复到一级、第三次恢复到二级、第四次
  恢复到三级，此后照此递增，直到第十次及以后保持九级。举例：第二次若此前已经恢复至零，
  本次会升至二级、以后最低恢复到一级；第三次在一级下限上再增加三级，最高到四级、以后最低
  恢复到二级。
- 乡土方向（袭击村民、劫掠和烧村）与商路方向（袭击商队）继续使用各自的被捕次数，所以
  两套恢复下限互不串用。下限直接由现有的永久分类被捕数字推导，不新增存档字段；旧存档载入
  后会按已有被捕史自动得到相应下限。族人转述或同场目击而从未在该方向亲自被捕者的下限仍为
  零，可以完全恢复。
- `GwpAiDeterrenceState` 的日衰减现在以分类下限为终点；达到下限时把剩余值归为本人直接经验，
  避免永久部分继续显示为可消退的转述震慑。恢复暂停判断和百科预计时间也改为只计算当前值到
  下限之间仍可恢复的部分。英雄百科在存在下限时显示最低压制；尚未到达时显示恢复至下限的
  预计时间，达到后明确显示永久固定等级。
- `PoliceAIDeterrenceBehavior.DeterrenceGreetingChance` 从测试阶段的 `1.0` 改为 `0.5`。每次
  普通交谈仍只掷一次并缓存本次会话结果；只有掷中时才由震慑特殊开场替换普通问候，其余任务、
  执法和悬赏对话排除条件不变。
- 把下限公式改成线性增长后的第一次重建发现 TaleWorlds 提供的 `MathF.Min(float, int)` 重载
  被标记为编译错误 `CS0619: Types must match!`；把 `Math.Max(0, arrestCount - 1)` 显式转换为
  `float` 后消除。该次失败构建没有产生可接受的最终 DLL，随后以修正源码完整重建并重新部署。
- 当前 Bannerlord `1.4.7` 诊断版执行
  `dotnet build .\GreyWardenPolicePurity.slnx -c Release -t:Rebuild --no-restore`，结果为
  `0` 错误、`45` 条既有可空性/离线 NuGet 警告并自动部署。仓库 `_Module` 的 `25` 个正常
  客户端文件与实机相比缺失 `0`、哈希差异 `0`；`18` 个 XML 解析失败 `0`，中文本地化
  `837` 个 string id、重复 `0`；实机不含 `Assets`、`AssetSources`、`RuntimeDataCache`。
  客户端与编辑器 DLL 哈希一致，均为 `731648` 字节，SHA-256
  `2D7A1687E8EF206A7EB679B75B78648A73C03100698C661EC7D0CA1C335148D2`，双语 README
  与仓库一致。ILSpy 从实机客户端 DLL 确认恢复下限公式为
  `min(9, arrestCount - 1)`、乡土/商路分别读取各自被捕数、日衰减以
  下限为终点、百科包含下限三种状态文本，并确认普通交谈实际执行
  `MBRandom.RandomFloat <= 0.5f`。本轮是普通开发部署，没有创建或替换正式玩家 ZIP。

## 2026-07-25 战帆下水等待诊断：约珥欲望正常，等待舰队锚点抵达

- 最新实机监控
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`
  （最后写入 `2026-07-25 14:29:09 +10:00`）确认约珥
  `gw_leader_1_party_1` 没有在岸边反复改主意。她在
  `campaignHour=625315.99` 已把跨水域案件落实为
  `desiredNavigation=All`、`default/short=GoToPoint`；到
  `625316.99` 抵达 `(771.9366, 272.5201)` 后，位置连续保持到日志结束的
  `625342.99`，共约 `26` 个战役小时。期间每小时仍重新取得同一案件目标
  `CharacterObject_1824_party_1`，目标位置从约 `(614, 151)` 移到
  `(638, 149)`，约珥持续更新追赶点但没有离开岸边；`aiDisabled=False`、
  `doNotDecide=False`、`mapEvent=-`，排除 AI 被关闭、案件丢失或战斗锁定。
- 对应欲望拍卖同样正常。岸边等待开始时和日志结束前，案件
  `ApproachPoint` 均以 `0.99` 获胜；原版巡逻最高欲望为 `3.09`，按既有任务规则压到
  `0.03`，原版访问定居点欲望最高分别约为 `0.642` 和 `0.293`，均低于案件。最终行为始终是
  案件点的 `NavigationType.All`，不是巡逻、补给或进城欲望抢走控制权。
- 对本机当前
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\bin\Win64_Shipping_Client\NavalDLC.dll`
  反编译核对了 `NavalDLCPartyTransitionModel.GetTransitionTimeForEmbarking`。战帆的 AI
  下水不是到岸即一定完成：舰队锚点无效时固定等待 `48` 小时；锚点有效但距队伍至少
  `10` 个地图距离时，等待
  `clamp(distance^0.95 / 35, 3, 48)` 小时让舰队抵达；只有距离小于 `10` 时才即时下水。
  这直接解释“有时很久、有时没问题”：舰船锚点恰在附近时为零等待，舰队留在远方或锚点尚未
  建立时会在岸边等待 `3～48` 小时。战帆另把正常下船等待固定为 `2` 小时，和本次下水停顿
  不是同一规则。
- 当前灰袍监控只记录移动欲望、导航类型和地图位置，没有记录
  `MobileParty.Anchor.IsValid`、锚点位置、到达时间或舰船数量，因此现有日志能确定“不是欲望
  故障，而是进入战帆原版下水过渡”，但不能仅凭旧日志区分本次属于远距离舰队航行还是无效锚点
  的 `48` 小时兜底。本轮没有修改运行时代码、玩家 README、实机模块或正式玩家包；若需要继续
  精确到本次剩余等待时间，应在诊断状态中追加上述锚点字段后再做一次实机观察。

## 2026-07-25 v1.4-r7 同版本稳定性重发与正式玩家包

- 用户明确要求本轮不新增公开版本号。此前作为 `v1.4-r8（开发中）` 累积的协力追捕、战争收束、
  玩家纠察接触和切磋稳定性修正全部并入现有正式 `v1.4-r7`，不建立 `r8`。中英文玩家 README
  已删除开发版条目并把玩家可见结果合并到单一 `2026-07-25 v1.4-r7` 条目；两个 README 均只
  保留正式 `r7` 与 `r6` 两条记录，不介绍速度公式、区域战力系数、任务欲望、监控或封堵实现。
  `GreyWardenPolicePurity.csproj` 与 `SubModule.xml` 的说明也恢复为 `v1.4-r7`，内部三段式模块
  版本继续是 `1.4.7`，不构成 Bannerlord 1.4.7 硬依赖。
- 最终诊断版 1.4.7 `Release -t:Rebuild --no-restore` 构建为 `0` 错误、`45` 条既有可空性/
  离线 NuGet 警告并自动部署。正式发布前同一源码针对 1.4.5 与 1.4.6 的全源码交叉重建均为
  `0` 错误、`44` 条既有警告；本轮此后只调整版本注释和双语 README，没有再改运行时代码。
- 实机测试模块继续使用诊断版 DLL：`730112` 字节，SHA-256
  `555F0BB347438B35CB4F8F1314351B44B128825A88B7F2BB21F29D773C74814E`。仓库
  `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希差异 `0`，`18` 个 XML 解析失败
  `0`；中文本地化共有 `834` 个 string id、重复 `0`。实机不存在 `Assets`、`AssetSources`、
  `RuntimeDataCache`，并已移除一次权限检查遗留的 `.codex-permission-test.tmp`。
- 正式无监控玩家构建位于
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\release-player-v1.4-r7-reissue-20260725`。
  玩家 DLL 为 `711680` 字节，SHA-256
  `7309B0B93238BB9D8F57FF74EFEA07632741E609A5074842C95D3C9D850777CF`，与实机诊断版
  哈希不同，且构建时使用 `GwpDiagnosticsEnabled=false`、`DeployToLiveModule=false`，没有覆盖
  本地测试 DLL。ILSpy 确认其 `GwpAiDiagnostics.LogPath` 为空，所有写入方法为空，两个追踪判断
  恒为 `false`；二进制中 `AppendAllText`、`StreamWriter`、`FileStream`、诊断日志名均命中
  `0`，同时确认本轮纠察主动接触、城堡切磋离城及移除阵营连带扣分的修正均已进入玩家产物。
- 干净暂存目录为
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\package-v1.4-r7-reissue-final-20260725\GreyWarden`。
  包含单一 `GreyWarden/` 顶层下的 `28` 个运行文件：`25` 个仓库正常客户端文件、客户端
  `0Harmony.dll`、上述无监控 DLL 和已编译 shader cache。禁入的编辑器资产、运行缓存、
  wEditor DLL、PDB、脚本、工具、日志、临时文件、开发文档及嵌套压缩包命中 `0`。独立解压目录
  `build-check\verify-v1.4-r7-reissue-20260725` 与暂存目录相比缺失 `0`、多余 `0`、哈希差异
  `0`；包内 DLL、双语 README 和无监控反编译结果均再次核对通过。
- 本地正式文件仍沿用同名
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4-r7.zip`
  及其 `.zip.sha256`，没有生成更高版本。ZIP 为 `349835030` 字节，SHA-256
  `AE01062E6FF1CDEB41FE5F5CB8066AB65938EBA584C12822D4EE3EAB47FD9BEE`；校验文件为
  `88` 字节且正文准确命名同一 ZIP。`Modules` 父目录只保留这一组最新正式包。仓库与包内中文
  README SHA-256 为
  `82CCB560B69B4B93D095B6121F802B973DA3698B4CB0613A6275C9EF0BEF10ED`，英文为
  `1CDDB4AE1B9500C9509D737B3B6CD595656E8123DFF41A2CFD5757C988490DE6`。
- 正式代码提交为 `d799644433c8a51746006f9bfce1c2ac80beee98` 并已推送到 `origin/main`。
  旧的 GitHub `v1.4-r7` Release 与远端标签按用户“不升版本”的要求删除后重建；本地带注释标签、
  远端标签解引用和正式代码提交均指向上述提交。新的同名 Release 位于
  `https://github.com/Lucicain/GW/releases/tag/v1.4-r7`，状态为 latest，且不是 draft 或
  prerelease。
- GitHub 上的 `GreyWarden-v1.4-r7.zip` 为 `349835030` 字节，远端 digest 为
  `sha256:ae01062e6ff1cdeb41fe5f5cb8066ab65938eba584c12822d4ee3eab47fd9bee`，与本地正式
  ZIP 一致；远端 `.zip.sha256` 为 `88` 字节，digest 为
  `sha256:a5cf37fc3659f5844ae322d73c257c43ab41e9cb3c9bd5495d33f109fa2508cd`，也与本地文件
  一致。

## 2026-07-25 v1.4-r8：玩家阵营连带扣分、纠察接触与城堡切磋崩溃

- 最新实机监控确认，玩家加入 `empire` 后，灰袍对该国其他 AI 犯人执行案件所产生的阵营战争，
  被 `PolicePatrolBehavior.OnDailyTick` 的旧全局检查误认为玩家本人违法。该检查不核对战争
  原因，只要灰袍与 `Clan.PlayerClan.MapFaction` 交战就每两日扣 `4` 点声望并强制和平；
  `PoliceEnforcementBehavior.DelayPatrols` 在非玩家案件的自动和平路径还会再按同一阵营身份
  扣一次。结果是 AI 案件可以连带处罚玩家、生成罚款纠察队，并且强制和平还会打断仍在执行的
  同阵营案件。两处“仅凭玩家所属阵营正在交战便扣分”的路径现已删除；玩家自己的犯罪、拒绝
  纠察和既有罚款处理保持原有专用流程，AI 犯人的案件不再转嫁给玩家。
- 监控中的 `gwp_patrol_16389` 证明纠察队确实取得
  `dutyIntent=Approach:player_party`，并以 `GoToPoint` 持续接近，但该命令不会建立
  `PlayerEncounter`，所以 `MapEventStarted -> PlayerEncounter.DoMeeting()` 永远没有机会
  触发，玩家只能手动点击。招募使者已经有经过实机验证的两段接触链：远距离使用
  `Approach`，进入 `3` 距离后清除该欲望并调用原版 `SetMoveEngageParty`，地图遭遇建立后
  再用 `DoMeeting` 转入对话。纠察队现复用完全相同的接触链，并新增
  `PATROL_FORCE_MEETING_APPROACH` 诊断记录。
- 家族族长梵蒂本轮没有被原版补给或巡逻欲望困在城内。监控在
  `campaignHour=636084.98` 明确记录 `TRAINING_TASK_ASSIGNED`：
  她作为练兵官选择 `gw_leader_4_party_1` 为换防对象、会合点为 `castle_EN4`，随后持续显示
  `dutyIntent=Visit:castle_EN4`；本人已经到达，但目标领主仍在执行普通案件，
  `trainerReady=true; targetReady=false`。因此她在城堡等待属于既定练兵换防流程，本轮不修改
  欲望或练兵任务。
- 最新引擎日志 `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_40932.txt`
  显示城堡交谈于 `00:58:05` 关闭后成功打开 `battle_terrain_L` 野外切磋，战斗、胜负、
  Tab 离场和赛后对话全部正常；`00:59:34` 赛后对话关闭后却重新进入
  `[GAME MENU] castle_outside`，随后 Windows 在 `00:59:42` 记录
  `TaleWorlds.MountAndBlade.Launcher.exe` 的 `0xc0000005` 原生访问冲突。没有托管异常或
  GreyWarden 堆栈，用户也取消了转储，因此无法取得更深的原生调用栈；但时间线已经定位到
  “从仍处于定居点状态直接打开独立野外任务，任务结束后恢复过期的城堡菜单”这一不安全边界，
  而不是单挑战斗或胜负回调本身。
- 城堡等无竞技场定居点发起野外切磋时，`TryLaunchFieldSparringImmediately` 现在先调用原版
  `LeaveSettlementAction.ApplyForParty` 让玩家队伍真实离开定居点，并在下一应用帧才打开
  野外任务。这样保留“出城切磋”的玩家体验，同时保证任务和赛后地图对话都返回普通大地图，
  不再恢复旧 `castle_outside` 菜单；若原版离城动作失败，则安全取消切磋并显示既有地点不合适
  提示。
- 首次源码构建为 `0` 错误、`47` 条可空性/离线 NuGet 警告并自动部署；其中两条新增警告来自
  新增离城代码对 `MobileParty.MainParty` 的可空分析，随后已改为显式解析并守卫本地变量。
  最终 1.4.7 `Release -t:Rebuild --no-restore` 为 `0` 错误、`45` 条既有可空性/离线 NuGet
  警告并自动部署；1.4.5 与 1.4.6 全源码交叉重建均为 `0` 错误、`44` 条既有警告。
- ILSpy 对实机 DLL 确认纠察接触链中的 `SetMoveEngageParty` 与
  `PATROL_FORCE_MEETING_APPROACH`、野外切磋前的
  `LeaveSettlementAction.ApplyForParty` 均已进入产物；通用
  `TryApplyPlayerAutoPeacePenalty` 调用和纠察每日检查中的
  `ChangeReputation(-4)` 均命中 `0`。玩家自身违法的专用处罚入口未删除。
- 仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`；`18` 个 XML
  解析失败 `0`。实机客户端与编辑器 DLL 均为 `730112` 字节，SHA-256 均为
  `555F0BB347438B35CB4F8F1314351B44B128825A88B7F2BB21F29D773C74814E`。仓库与实机中文
  README SHA-256 均为
  `B66AD6AD7D2FE54A046C9C38981DED803A1E9685F0A5A758436865022C5E4396`，英文均为
  `BB28710286F596BC09DB81110303A451CEEBA9CEFF32199292C34EB27A1266D7`。没有启动游戏，
  行为验证仍由用户完成；本轮是普通开发构建，没有创建或改写正式 ZIP。

## 2026-07-25 v1.4-r8 开发：原版理论速度与对称现场战力

- 协力速度生命周期不再读取缓存的 `LastCalculatedBaseSpeed` 作为案件门槛。新增
  `GetTheoreticalBaseSpeed`，每次直接调用原版
  `Campaign.Current.Models.PartySpeedCalculatingModel.CalculateBaseSpeed`，使用原版返回的
  `ExplainedNumber.BaseNumber` 和 `SumOfFactors` 计算当前部队结构下的理论基础速度；若部队正处于
  `IsDisorganized`，只消除原版明确加入的 `-0.40` 战后混乱因子，不修改部队状态，也不复制
  兵员、坐骑、载重、伤兵、俘虏和士气等原版公式。原版把潮湿天气对骑兵与骑马步兵的两条修正
  异常地放在基础速度而非最终地形速度中，因此同时从 `ExplainedNumber.GetLines()` 精确识别并
  排除这两条有文本标识的环境修正；不排除任何结构性速度项。
- 主理人独立且首次接案时只锁定一次上述理论速度；目标则在每次协力更新中先解析到实际移动主体
  （攻城队长、军团长、附着对象或目标本人），再重新调用同一原版算法。军团仅在目标当前理论速度
  严格高于主理人接案时理论独立速度时全组分散；目标速度回落到小于等于该基准时，同一案件重新
  组军。目标因伤亡、增兵、载重或加入/离开军团发生的结构变化仍会正常改变理论速度，短暂战后
  混乱不再造成错误拆组。
- `PoliceTask` 新增 `HasTheoreticalLeaderSoloSpeedAtAssignment`，存档键为
  `gwp_lt_{i}_leader_solo_speed_theoretical`。新旧任务只有在该标志为真时才信任已保存速度；
  旧存档缺少标志时，会在主理人下一次独立状态中用新口径重新锁定一次，因此当前测试档不会继续
  沿用旧版可能受战后混乱污染的 `1.82` 等缓存值。
- 第二层宣战仍比较“实际到场的我方区域战力”与“实际到场的敌方区域战力”，不会把远处未到场
  的承诺力量虚算进来。友军扫描现按原版从行动者周围三倍接战半径查找，再限制为目标周围两倍
  接战半径；普通外围友军继续使用原版线性衰减。
- 补回了原版 `DefaultMobilePartyAIModel.GetBestInitiativeBehavior` 的共同目标分支：附近友军的
  原版 `AiBehaviorPartyBase`、同一 `MapEvent` 或模组协力职责若指向同一实际目标，该战斗组贡献
  系数直接为 `1`。因此已经位于现场并共同环绕同一犯人的多名灰袍会按完整区域战力参与第二层
  判断，不再出现敌方围城参战者全额计入、我方其他现场协力领主只剩约 `1.7%` 的不对称。
- `ASSISTANCE_DECLARATION_*` 诊断新增 `friendlyLocalGroups`，逐组记录实际计入战力和系数；
  速度诊断同时区分缓存基础速度、动态理论目标速度及主理人接案时理论速度，便于实机确认旧档
  重算、共同目标全额计入及拆组边界。
- 最终 1.4.7 `Release -t:Rebuild --no-restore` 构建为 `0` 错误、`45` 条既有可空性/离线
  NuGet 警告并自动部署；1.4.5 与 1.4.6 全源码交叉重建均为 `0` 错误、`44` 条既有警告。
  ILSpy 对实机 DLL 确认原版 `CalculateBaseSpeed` 调用、`SumOfFactors`、混乱因子恢复、
  `GetTheoreticalBaseSpeed`、共同目标全额分支、理论速度存档标志及
  `friendlyLocalGroups`，以及两条潮湿天气环境修正的剔除均已进入产物。
- 仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`；`18` 个 XML
  解析失败 `0`。实机客户端与编辑器 DLL 均为 `730112` 字节，SHA-256 均为
  `A215FAF159BA49BA77E8FA7AF56B2AEFC521C6C3D74E7A98B55674FC8D2EE430`。仓库与实机中文
  README SHA-256 均为
  `D44D71DC21F90F3F4DB43A1C619823F62D04EA8C0A4DB0E4810E75E6D8A335C3`，英文均为
  `E091EDB2F81887567967A8C9EB253CB941AF31931E956DBD79996DD8F4E5C350`。没有启动游戏，
  行为验证仍由用户完成；普通开发构建没有创建或改写正式 `v1.4-r7` ZIP，其 SHA-256 仍为
  `963AA367A2512126E5DBB92D04792A0075C3C1DE20DD7D40B61B6E2468ECE15A`。

## 2026-08-28 玩家重度通缉自动接触执法对话修复

- 实机诊断复现：玩家自定义声望为 `-12`，`PLAYER_WANTED` 案件开放且有效，
  `gw_leader_3_party_1` 已承办案件并持续追踪玩家；圣铎与玩家距离从 `12.43` 降至
  `1.98`，但其 `dutyIntent=Approach:player_party`、`default=GoToPoint`，没有
  `MAP_EVENT_STARTED` 或 `ENFORCEMENT_FINE_DIALOG_OPENED`。故障不在声望、案卷、
  任务分配或追踪，而在和平阶段没有把静态 `Approach` 转入原版 `EngageParty` 接触。
- 原设计的玩家案件必须先通过执法对话让玩家选择缴纳罚金、接受赎罪或拒捕；代码明确禁止
  玩家目标在接近时自动宣战，且现有 `OnMapEventStarted` 只能在地图遭遇已经建立后调用
  `PlayerEncounter.DoMeeting()`，不能凭空创建遭遇。普通非玩家案件的 `DeclareWar`/
  `GoAroundParty` 路径不能替代这条玩家专用入口。
- `PoliceEnforcementBehavior.MaintainPlayerEnforcementContact` 新增窄范围桥接：仅当当前
  承办人是灰袍正规领主、案件仍为玩家 `Pursuit`、双方和平、没有地图战斗/进行中对话且距离
  不超过现有 `Enforcement.WarDistance(3)` 时，清除地点接近意图并调用原版
  `SetMoveEngageParty`。接触请求在写入原版命令前记录 12 小时冷却，避免对话收尾期间每帧
  重发导致重复弹窗；遭遇/战斗/付款状态仍由既有对话和案件状态机处理。冷却写入
  `gwp_enf_player_contact_hour`，存读档后仍保持保护；新一宗玩家案件会清除旧冷却。
- 诊断仍只在开发构建启用，新增 `PLAYER_ENFORCEMENT_CONTACT_REQUESTED` 与失败重试记录；
  没有新增正式玩家监控输出。中文和英文玩家 README 的 r10 修复列表已加入该结果。
- `dotnet build GreyWardenPolicePurity\\GreyWardenPolicePurity.csproj -t:Rebuild --no-restore`
  成功，`0` 错误、`44` 条既有可空性/离线 NuGet 警告。开发 DLL 已自动同步实机，客户端
  SHA-256 为 `6A49C86403F8A4EC3730D66ECB3F5B7051BF98F3F854705AE776E287A1563605`；
  `_Module` 中排除编辑器专用 `Assets`、`AssetSources`、`RuntimeDataCache` 和
  `AssetPackages` 后，33 个可部署文件与
  `D:\\steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\GreyWarden`
  缺失 `0`、哈希差异 `0`。两份 README 也已复制并逐一核对哈希。尚未启动游戏，等待用户
  在当前负声望存档中验证自动接触、对话只出现一次，以及付款/赎罪/拒捕三条后续分支。

## 2026-08-28 执法对话结案后重复弹出的修复

- 用户实测确认自动接触已经成功，但选择认罪认罚后同一执法对话立即再次出现。结合此前调兵交接、
  封地申诉和招募使者的同类故障，根因不是案件状态没有清除，而是赎罪/缴款 consequence 在对话
  仍处于打开或收尾阶段就直接调用 `GwpCommon.TryFinishPlayerEncounter()`；原版
  `EngageParty/TargetParty` 因而可能存活到下一次地图 AI 检查，再次建立玩家遭遇。
- 赎罪和缴款分支现在只在 consequence 阶段标记 `PlayerEncounter.LeaveEncounter=true`，并注册
  `ConversationManager.ConversationEndOneShot`。对话真正结束后才调用 `TryFinishPlayerEncounter()`，
  清除承办灰袍的灰袍欲望和原版接触目标，设置短暂的 `SetDoNotAttackMainParty(2)` 安全窗，恢复
  原版 AI 思考并重置临时对话变量。拒捕分支保持原有宣战/战斗流程，不走和平结案清理。
- 删除了赎罪路径在对话仍打开时的即时 `TryFinishPlayerEncounter()`，并将缴款分支的
  `ResetDialogueState()` 移到收尾回调，避免回调失去承办队引用。新增的开发期诊断事件为
  `ENFORCEMENT_CONTACT_FINISH_QUEUED/FINISHED/FAILED`；没有向正式玩家版加入监控输出。
- `dotnet build GreyWardenPolicePurity\\GreyWardenPolicePurity.csproj -t:Rebuild --no-restore` 成功，
  `0` 错误、`44` 条既有可空性/离线 NuGet 警告。构建自动同步实机客户端和编辑器 DLL；两者均为
  `834048` 字节、SHA-256 `F48F343A8045BAAE5CA0B000B32E7AEFA427B71836390650DD21A3CA75B2B541`。
  仓库 `_Module` 与实机的 README（中文 `C3BB11C913CA06C6D91222944654D63B0F0984AF0A8BBCC2B75DFCA304CE52D6`，
  英文 `F811746C233676F246388B9D1E315C8879E528B577863FDE153F533CD44F7441`）已核对一致。尚未启动
  游戏；下一次实机验证应确认认罚/缴款后回到大地图且不再重复弹出执法对话，拒捕仍能正常进入战斗。

## 2026-08-28 纠察队改为实时追踪玩家位置

- 用户确认正规灰袍领主主动执法和和平结案已经正常，随后指出轻度负声望生成的无领主纠察队仍会
  先赶往玩家之前的位置，玩家持续移动时可能与其错过。源码确认两类追捕的职责边界不同：正规灰袍
  领主的玩家案件由 `GreyWardenPartyDesireBehavior` 以普通案件分 `0.99` 参与原版欲望竞价，远程
  `Approach` 被实现为本小时玩家坐标的 `GoToPoint`，抵达三格内才由
  `MaintainPlayerEnforcementContact` 切换 `EngageParty`；纠察队则是无领主、一次性、无需补给竞价的
  专用执法队，已有 `RequestPursuit` 的直接攻击锁可安全使用原版移动目标。
- 正规灰袍领主保持现状：案件欲望仍可被原版更高的补给、疗伤和安全需求覆盖，近距离才建立会面，
  结案后继续复用本日已验证的延迟 encounter 收尾、清除接触目标和短时不攻击玩家保护。没有把持久
  领主改成全程冻结或强制追击。
- `PolicePatrolBehavior` 的纠察队在生成时及和平追捕的小时维护中，均由 `RequestApproach` 改为
  `RequestPursuit`。由于 `GreyWardenPartyDesireBehavior.IsDisposableEnforcementParty` 已将无领主纠察队
  路由到一次性的 `SetMoveEngageParty` 并锁住小时 AI，这个目标会随玩家实时移动，不再是位置快照；
  后续小时只续期，不反复重发移动命令。纠察队付款、议和、胜负与押送结案仍通过 `RequestVisit`/
  `ApplyImmediatePatrolReturn` 释放直接追击锁、清除原版目标并返城，故不会重新引入重复交谈。
- 中英文 r10 玩家日志已加入“纠察队不再追旧位置”的简短结果。Release Rebuild 成功，`0` 错误、
  `44` 条既有可空性/离线 NuGet 警告；构建自动部署客户端与编辑器 DLL，两者均为 `834048` 字节、
  SHA-256 `73C4AC28F498AF54D5D9B935A566B37BE90278D3E32FF039EA02CA41ACC3FEFE`。实机 README 哈希为中文
  `7401B259070733FDCBA53E52781866450F6932BABC05C54E4FEF4E4AFBE4EAB5`、英文
  `AD4869DF969CF23294FCFA62EE0637C8ADF54E4DBFB3640B105FD69F59B59B50`。没有制作正式 ZIP；游戏内
  仍需验证纠察队能从远处持续跟随移动玩家、接触后正常打开一次对话，并在付款/议和后正常返城。

## 2026-08-28 双刀并入弓箭手兵种

- 用户决定取消独立双刃卫士兵种，将双刀配置并入现有 `gwarcher`。已从轻步兵升级树移除
  `gwdualbladeguard`，并删除该独立 `NPCCharacter` 与对应中文名称字符串；轻步兵仍直接升级为
  重步兵、弓箭手或骑士。
- 弓箭手的战斗装备现在固定为 `Weapon0=gwdualbladeoffhand`、`Weapon1=gwdualblademainhand`，
  以满足当前 AI 双刀资格和专用动作集的原生槽位要求；弓与箭移动到 `Weapon2/Weapon3`，保留
  弓箭手的远程能力。击倒概率代码不变，弓箭手仍使用原有三级兵种概率档。
- 已移除不再使用的 `GwpIds.DualBladeGuardId` 分支；双刀命中、伤害类型、防御击倒、拾取和 AI
  生成同步逻辑均未改动。本轮不做旧存档兼容，实机需要用新兵种树验证招募、升级、弓箭切换和双刀动作。
- `spnpccharacters.xml` 与中文字符串 XML 解析通过；`Release -t:Rebuild --no-restore`
  （`DeployToLiveModule=false`、`ReleaseLocal`）成功，`44` 条既有可空性警告、`0` 错误。诊断版 DLL
  SHA-256 为 `5BC924339689B57B9930EF71BCD7D24B9FFF5EAE13074EE599BBD3F6D17FD76C`，已同步到实机客户端
  与编辑器且两边一致；仓库 `_Module` 与实机的 `36` 个部署文件缺失 `0`、哈希差异 `0`。中英文 README
  已同步，哈希分别为 `600F6B40212EAB77058C0592E608823E3608DB59DD3980264A9462C90A091BD2`、
  `87BF6A5C28DEC7D743FDC02ED0D7FAB16C21994D37A3437824DB29B870BE10D1`。未制作正式 ZIP。

## 2026-08-28 正规灰袍领主案件欲望改为原版实时接触

- 用户进一步明确：正规灰袍领主的玩家案件应以精确 `1.0` 的欲望参加原版竞价，但该候选胜出后不能
  继续走当前玩家坐标的 `GoToPoint`；必须直接使用原版 `EngageParty`，跟随玩家移动目标。无领主
  纠察队上一轮已采用同样的实时追击目标，本轮只扩展到持久灰袍领主，不改变其补给、疗伤和其他
  原版高优先需求。
- 原版 `AiPartyThinkBehavior` 没有把 `EngageParty` 作为欲望胜出分支，而是通过
  `SetPartyAiAction.GetActionForGoingAroundParty` 落地 `GoAroundParty`。因此新增了窄范围 action
  bridge：仅当候选队伍仍有未宣战的玩家 `Pursuit` 案件且目标是玩家时，将这个胜出的
  `GoAroundParty` 转译为 `SetMoveEngageParty`；其他灰袍案件、玩家委托专员、原版 AI 候选和战斗
  行为完全不受影响。
- `GreyWardenPartyDesireBehavior.ProcessFinalDesires` 现在为上述玩家案件加入目标部队候选而不是旧坐标
  候选，诊断标记为 `PlayerEnforcementEngage:<party>`。当补给等更高原版欲望胜出时，领主仍执行原版
  维护行为；案件欲望重新胜出时会重新使用实时接触目标。对话结案仍沿用上一轮延迟关闭、清除目标和
  短时不攻击保护，避免重复弹窗。
- `Release --no-restore` 构建成功，`0` 错误、`44` 条既有可空性/离线 NuGet 警告；随后以
  `DeployToLiveModule=false`、独立 `ReleaseLocal` 输出重新编译，`44` 条既有可空性警告、`0` 错误。新的诊断版 DLL
  为 `792064` 字节，SHA-256 `3345053F9D383AF77875296653D6DF7FE74FE24146BA98F5C54541D4B2F9165D`，
  已同步到实机客户端和编辑器目录，两边哈希一致。README 已同步实机，中文 SHA-256
  `88B07C0010328BB848A33B0F163532DA51DF0BFDFD8F6908AD7B2E3642C3B45B`、英文
  `3E95BB850863B574D588182648B87C520E3340A8C1F1FC3AECF26BEF03FBA7B5`；本轮没有制作正式 ZIP。下一步
  实机应验证领主案件欲望胜出后能持续追随移动玩家，并在接触后只打开一次执法对话，同时确认领主在
  缺粮或重伤时仍可被原版维护欲望暂时覆盖。

## 2026-08-23 Bannerlord 安装目录体积调查（未执行删除）

- 实机安装目录
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord` 约为 `134.7 GiB`，其中
  `Modules` 约 `129.81 GiB`。这不是日志、崩溃转储或临时缓存失控，而是官方游戏、官方
  Modding Kit、NavalDLC 与第三方模组共同写入同一安装目录的结果。目录内日志几乎为零，
  `dump`、`tmp`、`bak`、`old` 类文件没有形成可观占用；根目录 `Shaders` 约 `1.52 GiB`，
  是官方压缩着色器数据，不能按普通缓存删除。
- Steam 清单 `appmanifest_261550.acf` 记录游戏本体 App `261550` 占用
  `94,728,857,120` 字节；已安装 depot 包含基础内容 `261551`、`261552`、Digital Companion
  `2240111`，以及独立 DLC depot `2927200`。其中 `2927200` 的清单大小为
  `33,393,276,540` 字节，落盘的 `Modules\NavalDLC` 约 `31.10 GiB`；它应通过 Steam 的
  DLC 管理取消安装，不能手工掏空目录。当前 `LauncherData.xml` 中 NavalDLC 未选中，但这
  只说明最近一次单人启动配置没有加载它，不证明用户以后不再游玩该 DLC。
- Steam 清单 `appmanifest_1393600.acf` 证明 Modding Kit 是另一个已安装产品，App
  `1393600`、depot `1393601`，清单大小 `30,921,657,359` 字节（约 `28.8 GiB`）。其主要
  内容是散布在官方模块中的 `EmAssetPackages`（约 `28.45 GiB`），另有 `SceneEditData`、
  `Win64_Shipping_wEditor`、根目录 `modding_resources` 和 `XmlEditor`。若不再使用官方编辑器，
  应从 Steam 单独卸载 `Mount & Blade II: Bannerlord - Modding Kit`，不要手工删除这些交错
  目录；若仍需恢复或编辑 GreyWarden 盾牌资产，则必须保留。项目外置
  `_GreyWardenEditorWorkspace` 是可恢复编辑状态，也不属于垃圾。
- 第三方模块共约 `21.71 GiB`。最大一组为 ROT：`ROT-Content` 约 `13.21 GiB`、
  `ROT-Map` 约 `2.58 GiB`，连同其余 ROT 模块合计约 `15.9 GiB`；当前启动器中全部 ROT
  模块未选中。ROT 内部 `EmAssetPackages` 合计约 `2.06 GiB`，但不能只凭目录名断定客户端
  不读取；若确认不再游玩 ROT，按其安装/模组管理方式整体移除整组模块，比拆删内部资产可靠。
- `Modules\Coop` 约 `5.66 GiB`，其中 `DedicatedServer` 独占约 `5.63 GiB`，普通客户端部分
  只有约 `29 MiB`。`DedicatedServer\release-info.txt` 明确说明它是 Windows x64 自包含专用
  服务器，入口为 `BannerlordCoopServer.exe`，内部 `engine` 带有独立的 Native、SandBoxCore、
  SandBox、Coop、服务器二进制和资源副本；普通客户端及玩家托管 Coop 不需要读取这一副本。
  服务器持久数据另存于
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\CoopData\DedicatedServer`。
  因此用户若明确不运行独立 Coop 服务器，可按 Coop 发布包/安装方式移除整个
  `Modules\Coop\DedicatedServer`，预计释放 `5.63 GiB`，且保留 Coop 客户端；但该服务器标记
  为 `2026-08-22` 构建并校验配套 Coop 哈希，属于近期有意部署内容，不得未经确认当垃圾删除。
- 当前启动器最近一次单人配置中，GreyWarden 与 SimaAndCaesar 处于选中状态；ROT、Coop、
  Diplomacy、RTSCamera 及 NavalDLC 未选中。启动器选中状态只是当前配置证据，不能替代用户对
  “以后是否还需要”的确认。GreyWarden 实机模块约 `0.35 GiB`，且 `Modules` 父目录当前保留
  唯一正式包对 `GreyWarden-v1.4-r9.zip` 与匹配 `.zip.sha256`，均按项目规则保留。
- 精确重复的大文件仅约 `298.6 MiB`：两个相同官方过场视频及 Native/NavalDLC 间两个重复
  tileset 页。它们属于 Steam depot，手工删除后会被校验或更新恢复，收益小且不应作为清理项。
  游戏目录 ZIP 总计约 `369 MiB`，其中约 `333.3 MiB` 是必须保留的最新 GreyWarden 正式包。
  `D:\steam\steamapps\downloading` 另有约 `16.41 GiB`，清单对应 Victoria 3 App `529340`
  的下载/更新暂存，不属于 Bannerlord，除非用户先在 Steam 取消对应下载，否则不得删除。
- 安全精简应按功能取舍分层进行：保留全部功能时没有多 GiB 的纯垃圾可删；Steam 卸载 Modding
  Kit 预计减少约 `28.8 GiB`，但失去官方资产编辑能力；Steam 取消 NavalDLC 安装预计再减少
  约 `31.1 GiB`，但失去该 DLC；确认弃用 ROT 后整体移除约 `15.9 GiB`；确认不运行独立 Coop
  服务器后可移除约 `5.63 GiB`。本次仅完成只读调查与记录，没有卸载产品、删除游戏内容或改变
  实机模块。

## 2026-07-25 v1.4-r8 诊断修正：现场友军战力与速度基准口径

- 最新监控中的案件为晨曦 `gw_leader_4_party_1` 追捕
  `lord_6_1_party_1`。第一层整案战力没有漏算：围城开始时四名协办人与主理人的承诺战力约
  `1385.61`，敌方目标、军团及围城参战者的区域战力约 `1310.04`，因此整案具备完成能力。
- 第二层的 `265～296` 不是“现场只有一个灰袍”，而是现有近场公式把行动者本人按全额计入，
  却把同样围绕该目标、位于约 `5.95` 距离的其他协力领主按外圈线性衰减到约 `1.7%`。敌方当时
  已进入围城 `MapEvent`，七支实际参战部队则按全额计入，形成了敌方全额、我方除单个行动者外
  几乎归零的不对称；这就是日志中我方现场战力异常偏低的直接原因。
- 1.4.7 原版 `DefaultMobilePartyAIModel.GetBestInitiativeBehavior` 并非单纯对所有外圈友军
  做同一衰减。附近友军若已经把同一个敌方部队或同一个战斗作为 AI 目标，
  原版 `flag2/flag3` 分支会把其贡献系数直接设为 `1`；同军团、同敌方军团和同围城目标也有
  相同的全额协同分支。现有模组近场近似遗漏了“共同追踪同一目标”的分支，所以没有忠实复用
  原版区域战力判断。
- 正确的第二层仍应是“实际到场的我方区域战力”对“实际到场的敌方区域战力”，不能把远处尚未
  到场的整案承诺战力直接算进来；但已经处在原版局部扫描区域内、且明确追踪同一案件目标的协力
  主理人和协办人，应按原版共同目标规则全额计入，而不是逐个只看单队，也不是在外圈只剩
  `1.7%`。用户现场看到约四支灰袍而日志仍只有约三百战力，确属公式缺口。
- 当前速度字段同样被上一版诊断错误称为“理论最大速度”。源码实际保存和比较的是
  `MobileParty.LastCalculatedBaseSpeed`。它虽已排除最终地形、昼夜等部分即时修正，但原版
  `CalculateBaseSpeed` 仍包含当前兵员规模、骑兵与坐骑、载重、牲畜、伤兵、俘虏、士气、潮湿
  天气，以及 `IsDisorganized` 的 `-40%` 修正，因此不是稳定的每队理论上限。
- 这解释了主理人接案速度只有 `1.82`，以及目标在围城结束后出现
  `2.21～2.33 -> 1.76 -> 2.65` 的变化：伤亡、军团附着、载重/俘虏、伤兵和战后混乱等状态
  都会使 `LastCalculatedBaseSpeed` 重新计算。目标附着于军团时还必须解析到实际移动的军团长，
  否则目标附属队自身速度会是零。
- 更合适的追赶口径是“当前队伍结构下的标准化独立基础速度”：继续调用原版速度模型并保留会
  实际影响长期追赶的兵种、坐骑、载重、伤兵、俘虏等结构性因素，但排除短暂的战后
  `IsDisorganized` 修正，也不使用地形、昼夜和当前是否正在移动。主理人在接案时只锁定一次该
  标准化独立速度；目标则始终解析到其实际移动主体，并动态重算同一口径。这样军团重组条件才是
  “目标标准化基础速度小于等于主理人接案时的标准化独立基础速度”，不会因主理人刚打完一仗
  而永久锁进过低阈值。
- 目标速度仍可能因部队伤亡、增兵、加入或离开军团、载重及俘虏变化而合理变化；应消除的是战后
  混乱等短期噪声，而不是把目标速度冻结成永远不变的常数。原版提供公开算法入口
  `Campaign.Current.Models.PartySpeedCalculatingModel.CalculateBaseSpeed`，可直接得到具体部队
  的完整 `ExplainedNumber`，包括 `BaseNumber`、`SumOfFactors` 和最终结果；因此无需复制整套
  原版速度公式。它返回的是包含当前临时状态的基础速度，不是另一个已经剔除临时修正的专用
  “理论最大速度”属性。模组可以调用该原版算法后，仅从公开因子结果中排除
  `IsDisorganized=-0.4` 等明确不应污染案件基准的短期项，得到标准化理论速度。
  `Campaign.EstimatedMaximumLordPartySpeedExceptPlayer` 则只是全局扫描估算值，不能代替具体
  目标计算。本轮只完成诊断修正，尚未修改运行时代码，也没有启动游戏。

## 2026-07-24 v1.4-r8 开发：分散协力案件追截队与藏城宣战流程

- 最新监控中的阿塞莱目标同时对应三宗灰袍案件，但只有其中两宗已经把各自任务切入
  `WarPursuit`。远星承办的 `CharacterObject_2717` 案件虽已有五名协力成员并因速度全组分散，
  任务本身仍为 `Pursuit`、`war=False`；目标 `lord_3_3_party_1` 躲在 `town_A4`，远星和晨曦
  均稳定停在距目标约 `5.95` 的原版 `GoAroundParty` 外圈。其他案件造成的势力战争不会代替
  本案自己的宣战状态，因此既有“本案宣战后驱逐并强制目标攻击主理人”入口始终没有启动。
- 根因是 `HandleShelteredCriminal` 在正常宣战判断之前提前处理并返回 `true`，调用者随即
  `continue`，所以藏城案件完全没有执行既有两层判定：第一层
  `HasAssistanceEngagementStrengthAdvantage` 比较全部已承诺协力战力与敌方区域战力；第二层
  `TryGetNativeDeclarationCandidate` 再比较进入接触范围的我方实际区域战力与敌方实际区域
  战力。正常协力路径原本已经把接触距离提高到
  `GetNativeMaximumGoAroundDistance()`，距离本身并不是缺口。
- 曾短暂把协力藏城案件的 `HandleShelteredCriminal` 宣战距离直接提高到原版环绕外圈，但该
  做法错误地把“进入判定范围”当成了“获得宣战许可”，会绕过两层战力判断；用户指出后已立即
  删除，不能作为最终实现。最终流程把藏城处理移到正常宣战判定之后：目标在城内时仍完整执行
  两层战力比较，只有两层均通过并让本案真正进入 `WarPursuit` 后，才复用既有停留计时、拉出
  定居点、禁止重新进入和强制目标攻击主理人的流程；未达到战力条件时只继续围堵和增援。
- 原高速追截分兵只在普通案件刚发生 `Pursuit -> WarPursuit` 转换的一刻调用，因此上述另外
  两宗已经宣战、但因目标更快而分散的协力案件不会派出追截队。现在任务为 `WarPursuit`、
  主理人仍有效、协力组目标与本案目标一致且 `DispersedForSpeed=true` 时，也会从主理人现有
  健康骑乘兵中真实转移 `3～8` 人建立同一类无英雄高速追截队。只有按原版即时速度确认追截队
  严格快于目标实际移动主体后才保留，否则立即把士兵无损退回。
- 同一案件仍只允许一支未返程追截队；它继续复用 `DirectAttackLock` 和原版
  `EngageParty`，只负责先建立地图战斗，不修改任何英雄领主的长期或短期欲望。案件结束后的
  幸存者仍真实返回主理人；若协力军团后来重组，现有追截队也可复用原入口加入军团。新增部署
  诊断字段 `trigger=assistance_speed_dispersed`，可与普通案件的
  `trigger=ordinary_declaration` 区分。
- 本轮只修改高速追截保底和藏城控制流，没有改变协力战力规模、两层战力公式、任务级速度基准、
  整组分散或重组条件。
- 最终 1.4.7 `Release -t:Rebuild --no-restore` 为 `0` 错误、`45` 条既有可空性/离线 NuGet
  警告并自动部署；1.4.5 与 1.4.6 全源码交叉重建均为 `0` 错误、`44` 条既有警告。ILSpy 对
  实机 DLL 确认 `TryGetNativeDeclarationCandidate` 和 `DeclareWar` 均位于
  `HandleShelteredCriminal` 调用之前；后者函数体内 `DeclareWar` 与
  `GetNativeMaximumGoAroundDistance` 均命中 `0`，只保留 `task.WarDeclared` 驱逐门槛。
  分散协力追截入口与 `assistance_speed_dispersed` 诊断仍存在。
- 仓库 `_Module` 的 `25` 个正常客户端文件与实机相比差异 `0`，`18` 个 XML 解析失败 `0`。
  实机客户端与编辑器 DLL 均为 `727552` 字节，SHA-256 均为
  `0DB0CD6BE295F0BC85DAB0FDFD6326DDEF47214D136B405107B35210621EAD76`；仓库与实机中文
  README SHA-256 均为
  `592B48796360874D6349C449DE3DD851A9A1C76C21E6CD152A00C03C82D428A9`，英文均为
  `84A025859BF27CFD7BD6D3A9B843F3BBFA01F6E186CC79E3B9D5CB21AD4E3CCF`。没有启动游戏，
  行为验证仍交给用户；普通开发构建没有创建或改写正式 `v1.4-r7` ZIP，其 SHA-256 仍为
  `963AA367A2512126E5DBB92D04792A0075C3C1DE20DD7D40B61B6E2468ECE15A`。

## 2026-07-24 v1.4-r8 开发：普通案件高速追截分兵

- 用户确认该功能只服务于普通非玩家案件。势力层已经因其他案件处于战争状态不能触发分兵；唯一触发点放在当前任务自己的 `Pursuit -> WarPursuit` 转换之后，即 `task.WarDeclared` 在本次案件更新中刚被置为真时。
- 该转换仍复用既有宣战门槛：主理人与本案目标距离不超过普通案件 `WarDistance=3`，`TryGetNativeDeclarationCandidate` 已按原版可参战范围确认我方实际区域战力严格高于敌方区域战力。协力组存在、玩家案件、主理人正在地图战斗或已属于军团时均不生成高速追截队。
- 只有目标实际移动队伍按原版 `MobileParty.Speed` 即时重算后的速度高于主理人时才尝试分兵。追截队与主理人同一 `CampaignVec2` 创建，从主理人现有名单中真实转移健康骑乘兵，不生成士兵；按高阶优先最多取 `8` 人，少于 `3` 名可用骑乘兵则放弃生成。模板自带名单和物品会先清空，只保留现有临时执法补给入口提供的口粮。
- 分兵完成后再次调用原版速度模型计算追截队实际速度；只有追截队严格快于目标实际移动队伍才保留并发令，仍追不上时立即把刚转移的士兵无损退回主理人并销毁空队，不把一个无效追截队留在地图上。
- 追截队使用既有 `gwp_enf_delay_` 无英雄临时执法部队类型，因此复用已经验证的 `DirectAttackLock`：进攻主动性 `1`、逃避主动性 `0`、关闭后续欲望决策并只对本案目标下达原版 `EngageParty`。主理人的欲望和短期行动仍完全交给原版 AI；追截队只负责先建立地图战斗。
- `DelayPatrolState` 新增并存档 `IsImmediateInterceptor`。每宗案件同时最多存在一支未返程的高速追截队；它若在追捕期间转为协力案件，仍可沿用现有入口加入协力军团，不会生成第二套军团逻辑。
- 案件结束、目标被击败或战争理由清除后，追截队解除直攻并用原版 `EscortParty` 返回原主理人；进入原版军团接触距离后，把全部健康与负伤幸存者按真实名单并回来源部队，再销毁空队。追截队战败则其实际损失保留；主理人已失去带兵资格时仍走既有返城清理兜底。
- 新增中文部队名 `灰袍高速追截队`，英文回退名为 `Grey Warden pursuit detachment`。新增诊断 `IMMEDIATE_CASE_INTERCEPTOR_DEPLOYED`、`IMMEDIATE_CASE_INTERCEPTOR_TOO_SLOW` 与 `IMMEDIATE_CASE_INTERCEPTOR_REJOINED`，分别记录本案任务、三方速度、区域战力、真实转移人数、速度不足回滚及归队人数。
- 最终 1.4.7 `Release -t:Rebuild --no-restore` 为 `0` 错误、`45` 条既有可空性/离线 NuGet 警告并自动部署；1.4.5 与 1.4.6 全源码交叉构建均为 `0` 错误、`44` 条既有警告。ILSpy 对实机 DLL 确认任务级宣战转换后的分兵入口、原版 `MobileParty.Speed` 三方重算、模板物品清空、真实名单转移、速度不足回滚、存档键 `gwp_enf_dp_immediate_interceptors` 和三条新诊断均已进入产物。
- 仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML 解析失败 `0`，`git diff --check` 通过。实机客户端与编辑器 DLL 均为 `727040` 字节，SHA-256 均为 `BE96635627FDE97179CCF3CA640717DE477C238FCF949D3B6D0601EFFDC9358F`；仓库与实机中文 README SHA-256 均为 `A0F5A75252DF2CA28D1926BFCA7D7465EC6AC6FA789A7603321ADB6B2FC1036B`，英文均为 `082DE95257E39CA1922E23078A998F5AC33850F3722CF2BCAA418679AC85884D`。按用户要求没有启动游戏，由用户进行行为验证；普通开发构建没有创建或改写正式 ZIP，本机唯一正式包仍为 `GreyWarden-v1.4-r7.zip`，SHA-256 仍为 `963AA367A2512126E5DBB92D04792A0075C3C1DE20DD7D40B61B6E2468ECE15A`。

## 2026-07-24 v1.4-r8 诊断：高速普通案件会从单人追捕拖成协力案件

- 监控确认该现象真实存在。`21:44:14-19` 的 `lord_5_91` 案件中，梵蒂战力 `458.85`，目标最初仅 `40.44`，因此最初确实属于单人足以处理的普通案件；但梵蒂基础速度 `3.61`，目标速度 `3.90`。宣战后梵蒂长期行为保持案件要求的 `GoAroundParty`，短期行为反复为 `GoToPoint`，原版即时判断记录为 `FleeToPoint`，没有生成 `EngageParty`，双方距离长期维持在约 `5.7-6.2`。
- 当前 1.4.7 原版 `AiEngagePartyBehavior` 已用 ILSpy 核验：它将追击方速度除以目标速度，并把这个比值取四次方写入接战分数；附近已经 `GoAroundParty` 或 `EngageParty` 同一目标的友军战力另行累加。因而高速目标不仅物理上更难追上，还会显著压低慢速领主的原版接战欲望；已经接战的友军到场则会同时提高原版对总友军力量的判断。
- 同一 `lord_5_91` 案件拖到 `22:05:47` 后，目标已经从 `35` 人恢复到 `99` 人，附近又出现 `lord_5_6_party_1`。区域敌军总战力变为 `243.74`，高于当时新承办人远星的 `222.78`，协力系统因此才加入梵蒂并建立军团。这证明“最初单人可办，长期追不上后敌方区域增兵，最终又转为协力”是现有机制的真实结果。
- 无英雄纠察支援队不是普通领主 AI。`GreyWardenPartyDesireBehavior` 会为其设置 `DirectAttackLock`、进攻主动性 `1`、逃避主动性 `0`，直接使用一次原版 `EngageParty` 并跳过后续小时思考。因此它能够先强行碰撞目标；地图战斗一旦建立，附近灰袍又会按原版加入。用户观察到“普通领主长期环绕，支援队先开战后其他灰袍才参战”与代码和监控均一致。
- 这也说明普通案件当前存在一个边界缺口：是否需要协力只按战力决定，单人明显更强时不会因追击速度不足提前改变组织方式；宣战后的普通领主仍完全服从原版短期 AI，而原版接战分数又主动惩罚速度劣势。
- 本次崩溃中书弦突然成为军团长是另一个独立路径：她在 `22:19:14.904` 已持有 `CharacterObject_1549` 案件，战力 `110.55` 对目标 `35.63`，无需协力；`22:19:15.217` 她被与案件无关的 `lord_1_67_party_1` 拉入野战。战斗进行到 `22:19:17.722` 时，伤亡结算让她的实时 `EstimatedStrength` 暂降到 `1.73`，小时协力评估遂把 `1.73` 与远处案件目标的 `35.63` 比较，当场把她设为军团长并征调静澜。该升级本身不再被禁止；最终修复点是她战败失去带兵资格时立即让任务失败并同步拆除军团。

## 2026-07-24 v1.4-r8 开发：战斗结算期间建立协力军团导致原生崩溃

- 最新复现不是普通原版退出崩溃，而是本模组协力军团生命周期造成的原生无效状态。Windows Application Event 1000/1001 记录 `TaleWorlds.MountAndBlade.Launcher.exe` 以 `0xc0000005`、`StackHash_dd2a` 终止；RGL 与 ButterLib 均无托管异常栈，watchdog 记录用户取消生成崩溃转储。
- 诊断时间线证明：`22:19:17.283`，书弦的 `CharacterObject_2882_party_1` 正在一场尚未结算的野战中；`22:19:17.722-17.723`，即时协力评估仍以该部队为首领创建真实 `Army`，把静澜的 `CharacterObject_2875_party_1` 加入并附着。`22:19:18.168-18.171` 书弦战败被俘，首领从部队消失，但静澜仍附着于这个只剩无领主首领部队的协力军团；诊断于 `22:19:18.552` 停止，随后发生原生访问冲突。
- 根因不是“任务在战斗中升级为协力”，而是升级后主理人战败失去带兵资格，旧任务失败门槛却没有立即命中。原有 `OnMapEventEnded` 只处理已宣战且案件目标同场的战斗，因此主办人在无关战斗中败北时既不结束任务，也不释放受协力军团保护的真实 `Army`，最终留下无有效军团长但仍有支援者附着的原生无效状态。
- 曾短暂采用两层封堵：`UpdateLordAssistance` 在主办人处于 `MapEvent` 时跳过全部协力处理，`GetAssistanceEvaluationTarget` 再次拒绝把该任务升级为协力。用户确认这会不必要地禁止合法升级后，两层封堵均已删除；正在战斗本身不再阻止普通任务升级、重组或扩充协力。
- 根因门槛同时下放到所有进行中的案件，而不只检查已有协力组：只要任务主理人参战后已失去有效带兵身份（部队失活、失去领主部队/首领、首领死亡、被俘或成为逃亡者），该任务就在本次 `MapEventEnded` 内立即失败。协力组若已存在，先授权解散真实军团并释放全部附着支援者，再清除任务、欲望和战争追踪；普通任务也走同一失败入口。小时更新保留相同条件作为非战斗生命周期变化的兜底。
- 该失败判定不依赖任务是否已经宣战，不要求案件目标参加同一场战斗，也不等待下一次小时检测。普通案件按既有 `EndTask` 语义从案件总卷删除；玩家长期通缉仍由其专门规则保留。
- 诊断动作只保留 `TASK_FAILED_OWNER_CANNOT_LEAD_AFTER_BATTLE`；已删除与旧封堵对应的 `ASSISTANCE_DEFERRED_LEADER_IN_MAP_EVENT`。
- 崩溃证据位置：`C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_16428.txt`、`rgl_log_errors_16428.txt`、`watchdog_log_16428.txt`，以及 `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`。
- 修复后 1.4.7 正式 `Release -t:Rebuild --no-restore` 构建为 `0` 错误、`44` 条警告，其中一条是 NuGet 漏洞索引暂时无法连接；1.4.5 与 1.4.6 接口交叉构建均为 `0` 错误、`43` 条既有警告。
- 实机诊断 DLL 为 `721920` 字节，SHA-256 `DAAD4D1C362A4E7AF4162F9CCA1C4932103006CDB96E672654CEAC11947C1EC6`。ILSpy 已确认 `HandleTaskOwnerMapEventEnded`、`FailTaskBecauseOwnerCannotLead`、带兵资格统一判定、小时兜底及新诊断动作均存在；`ASSISTANCE_DEFERRED_LEADER_IN_MAP_EVENT` 命中为 `0`，协力评估入口也不再检查主办人的 `MapEvent`。
- `_Module` 到实机正常客户端目录共核对 `25` 个部署文件：缺失 `0`、哈希差异 `0`；中英文 README 均逐字节一致。解析 `18` 个运行时 XML，错误 `0`。本次是普通开发部署，没有创建或替换正式发布 ZIP。

## Canonical Original-History Baseline (2026-07-21)

- Added `docs/original-history-canon.md` as the immutable historical baseline for all Grey Warden lore and quest design.
- The baseline was reconstructed from the user-designated community history transcript and checked against the locally installed Bannerlord Simplified Chinese StoryMode and SandBox localization files.
- Corrected speech-to-text duplication and names including Drosios Neretzes, Penton Neretzes, Arenicos, Raganvad, Urkhun, Solun, Mesui, Banu Sarran, and Banu Qild while preserving disputed claims as disputed rather than promoting them to fact.
- Original transcript location: `C:\Users\lucif\Documents\声音转文本\输出文件夹\188981017-1-30216_20260719_100259\188981017-1-30216.txt`.
- Original transcript SHA-256: `62C1A83BEC775C82450041A73AAE96129FB8646A5E00B6F0BA1C737452A175F4`.
- Future Grey Warden setting changes must yield to this baseline. Grey Warden history may fill gaps left by the base game, but may not alter the established battle outcome, imperial succession, Arenicos's seven-year reign and murder, or the three-way imperial split.
- Updated `docs/grey-warden-setting.md` to reference the canonical baseline and corrected its spelling and succession summary. No runtime or player-visible content changed.
- Added `docs/grey-warden-history-arc.md` as the proposed Grey Warden historical arc from the Neretzes-era secret institution through Pendraic, Arenicos's reform and murder, the 1084 player start, unification, the post-unification charter, and the later dissolution into successor institutions.
- The design deliberately keeps the base-game assassin and unofficial spy-master roles intact, makes the Grey Warden predecessor a parallel institutional network rather than a replacement for named canon characters, and leaves the exact murderer of Arenicos unresolved until the player-facing Grey Warden arc earns each conclusion.
- The six current leaders are designed as young operational officers during the 1084 assassination, not as decades-old founders. Their personal histories each cover one responsibility or concealment in the Black Seal incident, while the senior conspirator remains a separate predecessor figure.

## Goal

Improve maintainability without changing gameplay behavior:

- Reduce behavior classes that mix dialogue, AI, state, and persistence.
- Reduce cross-file coupling caused by runtime static state and timing-sensitive logic.
- Centralize IDs, tuning values, and text ownership.
- Make hidden state machines easier to read and extend.

## Phase 1

Split dialogue responsibilities out of `PoliceEnforcementBehavior`.

Status:
- Completed

Done:
- Moved enforcement dialogue registration, dialogue conditions, and dialogue consequences into a separate partial file.
- Kept enforcement state progression and punishment flow in the core behavior file.
- Preserved existing gameplay behavior.

## Phase 2

Split dialogue and notification responsibilities out of `PlayerBountyBehavior`.

Status:
- Completed

Done:
- Moved recruitment dialogue, bounty reward dialogue, and map-notification flow into a separate partial file.
- Kept bounty state progression, escort AI control, and quest recovery in the core behavior file.
- Cleaned a batch of low-risk nullable warnings in the touched bounty files.

## Phase 3

Centralize shared IDs, tuning values, and text keys.

Status:
- Completed

Scope:
- Introduce `GwpIds` for hero, clan, item, party, and text keys.
- Introduce `GwpTuning` for reputation thresholds, cooldowns, rewards, and timing values.

Done:
- Added `GwpIds`, `GwpTuning`, and `GwpTextKeys` as shared constant entry points.
- Replaced scattered literals in core bounty, enforcement, patrol, resource, lore, and submodule files.
- Reduced warning count further while touching those files.

## Phase 4

Unify runtime state access.

Status:
- Completed

Scope:
- Add a single runtime entry point for `CrimePool` and `PlayerBehaviorPool`.
- Centralize new-game init, load recovery, and session reconnect behavior.

Done:
- Added `GwpRuntimeState` as a thin runtime facade over `CrimePool` and `PlayerBehaviorPool`.
- Moved new-game reset and player-behavior load/save flow behind the unified runtime entry point.
- Switched core bounty, lore, patrol, and enforcement behaviors to read runtime state through the facade.

## Phase 5

Make core state machines explicit.

Status:
- Completed

Scope:
- Introduce explicit enums for enforcement, bounty, and atonement flows.
- Reduce branching built from `bool + string + null` combinations.

Done:
- Added shared flow enums for atonement, bounty, and police task states.
- Replaced core enforcement and bounty branch checks with explicit state helpers.
- Added a computed `PoliceTask.FlowState` so escort, war, and pursuit paths are easier to read at call sites.

## Constraints

- Each phase must compile before moving on.
- Prefer structural refactors before gameplay changes.
- Keep refactors incremental so in-game regression testing stays practical.

## Player-facing release log rule

- Every player-visible gameplay, balance, feedback, compatibility, or content
  change must update both `_Module/README.md` and `_Module/README_EN.md` in the
  same change.
- Both READMEs retain exactly the newest two formal release entries, newest
  first. An `r5` package therefore ships the `r5` and `r4` logs; publishing
  `r6` replaces the oldest entry so the files contain `r6` and `r5`.
- Fold all development iterations for one upcoming version into that version's
  single entry. Each formal entry states its comparison baseline and uses short
  added/adjusted and fixed lists.
- Write from the player's point of view and summarize meaningful outcomes.
  Omit formulas, internal trigger counts, implementation details, test history,
  and exact values that are not required for a player decision. Do not add an
  unchanged-behavior section.
- Do not hide player-visible consequences behind implementation terms such as
  callbacks, patches, synthetic blows, private fields, or diagnostic events.
- Keep developer-only build, asset-publishing, and debugging procedures in this
  maintenance document rather than in the release notes shipped to players.

## Working-directory/live-module synchronization rule

- During development, deployable files under `_Module` and their counterparts
  in `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`
  must be byte-for-byte identical before an in-game test is accepted.
- Git publication is a separate release step. The working tree may remain
  uncommitted during development; the local Git commit and GitHub are updated
  together only when the user requests an upload or release.
- Sync from the working directory to the live module after each runtime-file
  change, then verify SHA-256 hashes. The MSBuild `CopyModuleData` target is the
  canonical non-build copy path.
- Exclude editor-only `Assets`, `AssetSources`, and `RuntimeDataCache` from the
  normal-client live module. Live-only compiled `bin` and generated `Shaders`
  are not source-mirror violations.
- Keep the live module as the diagnostics-enabled development/test install.
  Formal player builds use a separate diagnostics-disabled output and must not
  overwrite the live test DLL.
- Update this maintenance document with material deployments, experiments,
  failures, conclusions, asset locations, and rollback information as work
  proceeds; do not rely on chat history.

## 2026-07-16 Bannerlord 1.4.7 startup-error isolation

- A later controlled launcher comparison materially narrowed the shutdown
  failure. The Chinese-site Mod Manager writes
  `bin\Win64_Shipping_Client\ModMasterStarter.bat` and directly launches
  `Bannerlord.exe` with `/anticheat`. It also orders the official modules as
  `Native, SandBoxCore, CustomBattle, Sandbox, StoryMode, BirthAndDeath,
  FastMode`, rather than the official launcher's observed order
  `Native, SandBoxCore, BirthAndDeath, CustomBattle, FastMode, Sandbox,
  StoryMode`.
- Two otherwise GreyWarden-only manager-style runs, PID `9752` at 20:59 and PID
  `32964` at 21:07, used the reordered list plus `/anticheat` and crashed during
  native shutdown in `TaleWorlds.Native.dll` with `0xc0000005` after
  `Managed Interface deleted`.
- Two later official-style runs, PID `28696` at 21:16 and PID `39268` at 21:20,
  used the official module order without `/anticheat` and produced no Windows
  Error Reporting crash. Both printed a non-fatal `Non-Zero Device Reference
  Count` line (`ERC1513` and `ERC1567` respectively).
- Therefore the immediate reproducible difference is the launch command built
  by the Chinese-site manager, specifically its module ordering and/or forced
  `/anticheat` flag. Do not attribute this comparison to the shield LOD package
  without a separate controlled test. For current testing, prefer the official
  launcher. If further isolation is required, vary module order and
  `/anticheat` one at a time while leaving all module files unchanged.

- The startup failure was not caused by the `Useful Skips` assembly itself.
  Disabling only `UsefulSkips` left `Bannerlord.MBOptionScreen` (MCM) enabled,
  so the same failure remained.
- `LauncherData.xml` initially had MCM `v5.12.1` selected while its declared
  dependencies ButterLib `v2.11.0` and UIExtenderEx `v2.13.2` were not
  selected. That was an invalid module selection, but it was not the whole
  cause: a controlled retry enabled Harmony, ButterLib, UIExtenderEx, and MCM
  in the declared order while leaving Useful Skips absent.
- The complete dependency-stack retry on build `117484` still failed at
  20:47:36. The exact fault was an `IndexOutOfRangeException` in
  `Bannerlord.ModuleLoader.SubModuleWrappers.Patches.MBSubModuleBasePatch.Enable`,
  invoked by `Bannerlord.ModuleLoader.Bannerlord_MBOptionScreen..ctor`. All four
  module assemblies had loaded successfully before this constructor failed.
  Therefore the immediate cause is MCM's bundled module-loader patch being
  incompatible with Bannerlord `1.4.7`, not a missing prerequisite and not
  Useful Skips.
- The installed MCM package contains game-version adapters through
  `Bannerlord.MBOptionScreen.v1.4.5.dll` but no `v1.4.6` or `v1.4.7` adapter.
  Treat MCM `v5.12.1` as not yet compatible with game build `117484` even
  though it is the newest installed Workshop release.
- A controlled build `117484` run at 20:42:59 loaded only Harmony, the official
  single-player modules, and GreyWarden. It loaded
  `GreyWarden/AssetPackages`, reached `GauntletInitialScreen` at 20:43:21, and
  produced no `rgl_log_errors` entry or GreyWarden assertion. This isolates the
  immediate startup failure to the MCM dependency stack rather than GreyWarden
  or Useful Skips.
- Current local isolation state in
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\Configs\LauncherData.xml`:
  Harmony, MCM, ButterLib, UIExtenderEx, ExceptionSentry, and Useful Skips are
  disabled; official modules and GreyWarden remain enabled. The pre-isolation
  launcher configuration is retained at
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\Configs\LauncherData.xml.before-usefulskips-fix-20260716-2040.bak`.
- Separate earlier build-117377 messages about `gw_leader_*` hair/tattoo tags
  and obsolete `civilian="true"` equipment syntax were GreyWarden XML schema
  assertions, not the later startup stop. Do not conflate those content
  assertions with the MCM dependency-resolution failure.

## Release packaging and asset layout

### Player archive

- Public artifact name: `GreyWarden-v1.4.7.zip` with a sibling SHA-256 file.
- Create archives only during a formal GitHub push/release task. Ordinary
  `dotnet build` runs must compile and synchronize the live module without
  creating, refreshing, or copying any ZIP.
- The local formal-release output directory is the game's module parent:
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules`. The ZIP and
  checksum sit beside the `GreyWarden` directory, never inside the live
  `Modules\GreyWarden` directory and never inside repository `_Module`.
- The ZIP must contain exactly one top-level `GreyWarden` directory.
- Include only runtime data: `AssetPackages`, `bin/Win64_Shipping_Client`,
  `GUI`, `ModuleData`, `ModuleSounds`, `Shaders`, `README.md`, and
  `SubModule.xml`.
- Exclude `Assets`, `AssetSources`, `RuntimeDataCache`,
  `bin/Win64_Shipping_wEditor`, PDB files, source FBX/PNG files, and diagnostic
  logs/dumps.

### Required TPAC files

- `AssetPackages/gwp_inherited_legacy_assets.tpac`
  - Size: `332,944,246` bytes
  - SHA-256: `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`
  - Contains the inherited armour, weapons, ordinary shield, materials,
    textures, and the original shield physics shapes.
- `AssetPackages/gwp_black_gold_shield.tpac`
  - Size: `37,594,977` bytes
  - SHA-256: `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B`
  - Contains `wlarge_shield_black_static`, `gwp_black`, and the three
    black-and-gold textures.
- Both files are intentionally ignored by Git because the inherited package is
  larger than GitHub's normal file limit. A distributable is not complete until
  both hashes pass.

### Modding Kit publication order

The Modding Kit clears the live `AssetPackages` directory and writes only
`pack0.tpac`. After every shield publication:

1. Ensure the Modding Kit has fully exited.
2. Preserve the new `pack0.tpac` immediately.
3. Rename it to `gwp_black_gold_shield.tpac`.
4. Copy the renamed file to repository `_Module/AssetPackages`.
5. Restore `gwp_inherited_legacy_assets.tpac` beside it.
6. Verify both file sizes and hashes before launching the client.
7. Move live `Assets`, `AssetSources`, and `RuntimeDataCache` intact to sibling
   `_GreyWardenEditorWorkspace` before a client test.

Do not concatenate the two TPAC files. They are independent valid packages.

### Editor workspace parking and restoration

The editable resource directories are currently parked intact at:

`D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\_GreyWardenEditorWorkspace`

That directory is outside the live `GreyWarden` module and contains exactly the
three editor-only directories needed to resume asset work:

- `Assets`: editable generated TPAC metadata, including the current
  `GreyWardenRecovery/dun_geo.tpac`.
- `AssetSources`: the six-LOD shield FBX and three source textures. The current
  `GreyWardenRecovery/dun.fbx` is `218,316` bytes with SHA-256
  `8FC25976E9A6E5B0663A6462EB6BB2F0F59E73C14AE899671A510825AB63B6AC`.
- `RuntimeDataCache`: generated editor cache. It is movable with the workspace
  but is not an authoritative backup and may be regenerated if necessary.

To resume editing without opening or automating the editor on the user's behalf:

1. Confirm the game and Modding Kit are fully closed.
2. Move `Assets`, `AssetSources`, and `RuntimeDataCache` from
   `_GreyWardenEditorWorkspace` back into the live module root:
   `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`.
3. Preserve both files in live `AssetPackages` before publishing; the Modding
   Kit will clear that directory and create a new `pack0.tpac`.
4. The user performs all Modding Kit/editor interaction. Do not control the
   editor for them.

Before normal-client testing or building a public archive:

1. Fully close the Modding Kit.
2. Move the same three directories back to
   `_GreyWardenEditorWorkspace`; do not split their contents across locations.
3. Confirm the live `GreyWarden` module has no `Assets`, `AssetSources`, or
   `RuntimeDataCache` directory, otherwise the client can prefer editable
   resources and ignore the complete runtime packages.
4. Restore and verify both runtime TPAC files using the sizes and hashes above.

Do not delete `_GreyWardenEditorWorkspace`. It is the current resumable editor
state. The inherited `gwp_inherited_legacy_assets.tpac` remains the authoritative
irreplaceable backup; the editor workspace does not replace it.

## Solved: black-and-gold shield shutdown failure

### Player symptom

- The black-and-gold lord shield rendered correctly, but some game exits ended
  after `Managed Interface deleted` with `0xc0000005` in
  `TaleWorlds.Native.dll`.
- The failure was intermittent, so absence of a visible dialog was not enough;
  every acceptance run also checked Windows Application/WER events and dumps.

### Important findings

- Two TPAC files, their external filenames, inherited collision bodies, texture
  names, and old/new TPAC coexistence were not sufficient causes.
- Runtime retrieval and mutation of weapon meshes/materials was unsafe during
  native teardown. The release therefore uses a statically authored lord-only
  metamesh and never recolours or swaps its material at runtime.
- Reusing a repeatedly edited generated `dun_geo.tpac` produced stale editor
  state. `Ignore`/`Apply Ignores` and editor shutdown could then fault in native
  code even though the FBX geometry was valid.
- The successful rebuild was made after the editor fully exited and the old
  generated `dun_geo.tpac` was removed. Reimporting the unchanged FBX created
  new package, geometry, and metamesh GUIDs. The model, material, import settings,
  and FBX checksum remained unchanged.
- Modding Kit shutdown can still use a different native teardown path from the
  normal client. Judge the player release only by normal-client runs; keep
  editor crashes documented separately.

### Final release state

- Lord item `wlarge_shield_black` directly references the static
  `wlarge_shield_black_static` metamesh and inherited
  `bo_cap_wlarge_shield` / `bo_wlarge_shield` physics shapes.
- The shield has six complete LODs, all bound to `gwp_black`:
  - LOD0: 1,360 vertices / 1,642 faces
  - LOD1: 1,142 / 1,312
  - LOD2: 807 / 821
  - LOD3: 413 / 328
  - LOD4: 208 / 164
  - LOD5: 104 / 82
- Offline checks found no invalid index, bad face reference, degenerate face, or
  non-finite position/normal/UV/tangent value.
- Normal-client processes `22448` and `31172` both loaded
  `GreyWarden/AssetPackages`, rendered `wlarge_shield_black`, completed a
  battle, reached `Managed Interface deleted`, and passed delayed Windows
  Application/WER/dump checks with no TaleWorlds crash.
- `Non-Zero Device Reference Count` (`ERC...`) may still appear on successful
  exits. It is not a failure unless Windows also records an application crash.

## Correct shield LOD workflow

### Blender/FBX

- Put the six static mesh objects in one FBX. A dummy/empty parent is not
  required.
- Required names:
  - `wlarge_shield_black_static`
  - `wlarge_shield_black_static.lod1` through `.lod5`
- All objects must have the same origin and applied transform, one material slot,
  `gwp_black` assigned to every face, valid UVs, and decreasing polygon counts.
  LODs do not need identical vertices or topology.
- Export only the intended six mesh objects with `Selected Objects`, object type
  `Mesh`, positive Y forward, and Z up.
- `Selected Objects` is the actual inclusion rule: all six objects, including
  the unsuffixed LOD0/base mesh, must be selected when the export starts. Making
  the base mesh the last-selected/active object is a useful visual checklist,
  but Blender's FBX exporter does not require that object to be active and
  Bannerlord does not use Blender's active-object state.
- If making the base mesh active after selecting all six, use Shift-click so the
  other five remain selected. A normal click that leaves only the base selected
  produces a one-model FBX.
- The verified six-LOD FBX contains six independent root-level mesh nodes and no
  dummy/empty parent. The base mesh is present first, each `.lod<n>` node is
  present once, and the same `gwp_black` material is connected to every node.

### Bannerlord import

- The FBX dialog should report `Geometry(6) Model(6) Material(1)`.
- Treat those counts as the authoritative export-selection check. If they do
  not read six/six/one, cancel the import and correct the Blender selection or
  FBX contents instead of trying to repair the Meta Mesh afterward.
- Enable `Import meshes` and convert units to metres.
- Leave `Convert to Z-up`, skeleton, animation, morph, and physics-shape import
  disabled for this static shield.
- In Meta Mesh Editor verify LOD0-LOD5 and `gwp_black` on every LOD. `Divide Into
  Grid` is unnecessary. Do not recompute normals/tangents unless the source is
  known to require it.
- `Remove Redundant Vertices` may appear enabled by default. It was not the
  proven shutdown fix and should not be toggled repeatedly as a diagnostic.

### If the Meta Mesh Editor becomes unstable

1. Stop changing Ignore flags.
2. Close the editor completely.
3. Back up the current generated `Assets/.../dun_geo.tpac` for diagnosis.
4. Remove only that generated TPAC; preserve `AssetSources/.../dun.fbx`, the
   material, textures, and inherited package.
5. Start a fresh editor session and allow the FBX to generate a new resource.
6. Verify the published TPAC offline before replacing the stable runtime package.

## Common failure and recovery table

| Symptom | Cause | Recovery |
|---|---|---|
| FBX import reports zero/one mesh or LOD0-LOD5 are not all present | Not all six intended mesh objects were selected when `Selected Objects` export was used, or `Import meshes` was disabled | Re-export with all six selected; confirm the unsuffixed base plus `.lod1`-`.lod5`, then require `Geometry(6) Model(6) Material(1)` before importing |
| Only the black shield appears; inherited armour is missing | Client loaded live `GreyWarden/Assets` instead of `AssetPackages` | Exit the game and move `Assets`, `AssetSources`, and `RuntimeDataCache` to `_GreyWardenEditorWorkspace`; verify the next log says `Loading packages .../GreyWarden/AssetPackages` |
| Publishing removes all inherited equipment | Modding Kit cleared `AssetPackages` and created only `pack0.tpac` | Rename the new package, then restore the verified inherited TPAC before testing |
| Ignore/Apply Ignores or editor shutdown starts faulting | Stale generated resource/editor-session state | Fully exit the editor and regenerate `dun_geo.tpac` from the preserved FBX in a fresh session |
| Black shield is rotated/reversed | Wrong FBX forward-axis declaration or double Z-up conversion | Export positive Y forward/Z up and keep Bannerlord `Convert to Z-up` disabled |
| Game exit seems clean but reliability is uncertain | Native failure was intermittent and WER can be delayed | Require actual shield rendering, complete client exit, then delayed Application/WER/dump checks |
| Six-LOD package fails again | Runtime package regression | Restore the retained single-mesh shield TPAC and repeat the same acceptance test |

### Why the final six-LOD attempt succeeded

- Correctly selecting and exporting all six meshes explains why the final FBX
  exposed a complete LOD0-LOD5 set. It can also explain earlier imports that
  contained no mesh, only one mesh, or an incomplete LOD group.
- It does not explain the later native editor/client shutdown fault. The FBX
  that faulted and the FBX that succeeded after regeneration had the same
  checksum and import contents. The change that separated those attempts was a
  fully closed editor plus regeneration of the stale generated resource, which
  produced fresh package/geometry/metamesh identifiers.
- It also does not explain the test where only the shield appeared. That client
  loaded the editable `Assets` directory instead of the two complete packages
  in `AssetPackages`, so the inherited armour package never entered that run.
- Rigged equipment is different: its FBX must include the required mesh and
  skeleton/armature data. Tutorials that select both and make the armature
  active are not evidence that a static shield's base mesh must be active.

## Legacy package recovery

- Canonical recovery directory:
  `C:\Users\lucif\Documents\GreyWarden旧资源恢复\pack0_2026-07-15`.
- Package GUID: `cec987dc-80fc-47dd-9865-6fe9e9274db3`.
- Inventory: 20 metameshes, 18 materials, 33 textures, and 2 physics shapes.
- Models and textures were exported for reconstruction, and raw/external data
  was retained. Common formats cannot recreate the original physics shapes, so
  keep the inherited TPAC itself as the authoritative backup.
- The inherited package is irreplaceable; the black-and-gold shield package is
  reproducible. Never delete or overwrite the inherited package during shield
  publication.

## Formal release checklist

1. Build the diagnostics-enabled `Release` against Bannerlord `1.4.7` and keep
   it in the live test module.
2. Confirm the live module has no `Assets`, `AssetSources`, or
   `RuntimeDataCache` directory.
3. Confirm both runtime TPAC hashes above.
4. Confirm both player READMEs describe functions/results only, retain exactly
   the newest two formal versions, and match their live copies.
5. Build a diagnostics-disabled player DLL into a separate staging directory
   with live deployment disabled. Decompile it and confirm all diagnostic write
   methods are inert and no test log can be created.
6. Stage one top-level `GreyWarden` directory without `tools`, scripts, logs,
   developer notes, editor binaries, PDBs, or source assets.
7. Commit and push the release source and documentation to GitHub as part of
   the same formal release task.
8. Create the versioned ZIP and its `.sha256` file directly under the
   game's `Modules` directory, never under `Modules\GreyWarden` or `_Module`.
9. Inspect ZIP paths, verify the packaged DLL hash equals the separate player
   build, and confirm no diagnostic/test content exists; then create/update the
   GitHub release and upload the matching ZIP and checksum.
10. Run at least one battle that renders the black shield, exit the client, and
   check delayed Windows Application/WER/dump state.

# 2026-07-16 English-base and Simplified-Chinese localization

## Scope and language architecture

- The module now treats English as the source-language fallback. Every
  player-visible C# string has a stable `{=gwp_*}` key and an English default;
  static XML names and descriptions use the same keyed-English pattern.
- Simplified Chinese is supplied by:
  - `_Module/ModuleData/Languages/CNs/language_data.xml`
  - `_Module/ModuleData/Languages/CNs/std_gwp_strings_xml-zho-CN.xml`
- The CNs table contains 507 keyed entries covering items, troops, clan and hero
  names, dialogue, quest text, encyclopedia text, map notifications, enforcement
  status, debugging UI that players can open, and dynamically formatted text.
- `GwpText.cs` is the single code-side localization boundary. It creates a
  `TextObject`, binds named variables such as `{VAR_1}`, and returns the text in
  the current game language. Named variables were used instead of interpolating
  a finished sentence so English and Chinese can change word order safely.

## Voice and terminology

- Core lore, greetings, troop requisition, bounty recruitment, atonement,
  arrest dialogue, adopted-heir encyclopedia text, and deterrence reactions were
  manually rewritten rather than accepted as raw machine translation.
- The English register is restrained and old-Imperial, not pseudo-Shakespearean.
  Canonical terms include:
  - `Grey Wardens` for 灰袍守卫
  - `the old, undivided Empire` for 统一帝国
  - `constabulary house` or `order` for the police-family institution
  - `provost patrol` / `provost` for 纠察队 / 纠察官
  - `amendment`, `atonement`, `inner ward`, `case rolls`, and `lawful fine`
- The six founders now have separate localization keys instead of the former
  shared `gw1` key. Generated female heirs use stable romanized English names
  and their original Chinese names in CNs.

## Save compatibility

- Existing saves may contain plain Chinese crime labels, while saves made in an
  English session may contain the earlier English label. `GwpText.CrimeType`
  recognizes both forms and renders the canonical label in the current game
  language wherever crime records are shown.
- Generated Grey Warden heirs are deterministically renamed by their stable hero
  ID during the existing family-presentation refresh, so old saves receive the
  current language's name set without changing which identity was selected.
- No save fields or saveable type identifiers were removed or renumbered.

## Validation evidence

- Release build succeeds against Bannerlord 1.4.7 with 0 compiler errors. The
  remaining nullable warnings pre-date localization and do not block output.
- Roslyn scan after conversion found 0 CJK-bearing C# string literals outside
  the CNs language table. Chinese source comments remain developer-only.
- Localization integrity check found no duplicate keys with differing values,
  no missing CNs entries (excluding TaleWorlds' special `{=!}` non-localized
  barter placeholder), and no English/Chinese named-placeholder mismatch.
- All shipped XML files parse successfully. The 1.4.7 civilian-equipment and
  hero-appearance schema repairs remain present.
- Ordinary `Release` build copied the bilingual module data and README to
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`.
  Runtime acceptance still requires checking both English and CNs in the normal
  client and confirming that the log loads `AssetPackages`, not the editor tree.
- After the working-directory/live-module synchronization rule was made
  mandatory, the runtime mirror was copied again with the `CopyModuleData`
  target and verified by SHA-256: all `25` deployable `_Module` files matched
  their live counterparts, the Release DLL matched the live client DLL, all
  `13` live XML files parsed successfully, the README matched, and the live
  module contained none of `Assets`, `AssetSources`, or `RuntimeDataCache`.

# 2026-07-16 returning-patrol interception crash and encyclopedia button

## Reproduction evidence and root cause

- The English encyclopedia screenshot showed the deterrence action label
  extending beyond a fixed `150`-pixel button. The Simplified Chinese label was
  shorter and remained inside the same frame, so this was a layout-width defect,
  not a missing or incorrectly selected localization entry.
- The latest client log was
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_27944.txt`.
  At `22:13:15`, the lawful-fine barter was rejected; at `22:13:36`, the player
  instead entered the smaller passage negotiation; and at `22:13:39`,
  `gwp_patrol_barter_post_success` accepted it. The patrol was dismissed and the
  encounter closed normally.
- At `22:14:00`, the player manually intercepted that returning patrol. The
  conversation character was a `Grey Warden Heavy Infantry` troop rather than a
  hero, and the log selected the native
  `default_conversation_for_wrongly_created_heroes` line. The crash dumper began
  immediately afterward. This isolates the crash from MCM, Useful Skips, the
  payment barter, and localization loading.
- A negotiated passage intentionally differs from payment of the lawful fine:
  it restores peace and grants four days of safe-conduct while leaving negative
  standing and the existing crime record in place. The user's remaining wanted
  state after paying about `300` was therefore expected; the later conversation
  fallback and crash were not.
- The former suppressed-meeting branch attempted to finish the encounter from
  inside the normal patrol dialogue condition and then returned `false`. During
  a manual interception this left the engine without an applicable GreyWarden
  start line, allowing it to fall through to the unsafe native line for the
  troop-led party.

## Repairs

- `PolicePatrolBehavior.OnSessionLaunched` now registers the high-priority
  `gwp_patrol_returning_start` dialogue before the ordinary enforcement start.
  It applies only to a GreyWarden patrol marked as returning or while patrol
  meetings are suppressed, gives a localized dismissal, and closes the player
  encounter through `GwpCommon.TryFinishPlayerEncounter`.
- `PatrolDialogCondition` no longer mutates or finishes `PlayerEncounter` from
  inside its condition when meetings are suppressed. It simply declines the
  ordinary enforcement branch, leaving the dedicated returning-patrol line to
  own the conversation.
- The encyclopedia deterrence button now uses `CoverChildren`, retains a
  `150`-pixel minimum width, allows up to `300` pixels, and gives its label
  `24`-pixel horizontal margins. This preserves the compact Chinese button while
  expanding the English button around its full label.
- The returning-patrol dismissal has English source text and a CNs entry under
  `gwp_patrol_returning_dialogue`; no save schema was changed.

## Validation and deployment

- Localization integrity after the repair is `507` English defaults and `507`
  CNs entries, with zero conflicting keys, duplicate translations, missing or
  extra entries, and named-placeholder mismatches. Every shipped XML file parses
  successfully.
- A full `dotnet build GreyWardenPolicePurity.slnx -c Release` completed with
  `0` errors and the existing `44` nullable-analysis warnings, then copied the
  module data and compiled assembly to the normal client and editor module
  directories.
- All `25` deployable source `_Module` files matched their live-module copies by
  SHA-256. The normal-client and editor assemblies matched at
  `755638561B8568A105CB6C496250A5CC0A017C8623EFFC1F6F0798F441A7ECD5`,
  and the repository/live player READMEs matched.
- The live module contained none of `Assets`, `AssetSources`, or
  `RuntimeDataCache`. The protected asset-package hashes remained unchanged:
  `gwp_black_gold_shield.tpac` =
  `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B` and
  `gwp_inherited_legacy_assets.tpac` =
  `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`.
- Final acceptance still needs an in-game retry of the exact sequence: reject
  the lawful fine, negotiate passage, catch the returning patrol, confirm its
  one-line dismissal, and verify that the encyclopedia button contains the full
  English label at the user's display scale.

## Follow-up after failed in-game retry at 22:34

- The first returning-patrol repair was incomplete. In
  `rgl_log_36420.txt`, the original enforcement meeting succeeded, the lawful
  fine was rejected at `22:33:52`, negotiated passage was accepted at
  `22:34:02`, and the first conversation ended normally at `22:34:06`.
- The player caught the patrol again almost immediately. At `22:34:07`, the
  new high-priority `gwp_patrol_returning_start` line was selected, proving that
  the native `default_conversation_for_wrongly_created_heroes` fallback had
  been eliminated. However, the log ended before `Conversation End`, and the
  crash dumper recorded an exception immediately afterward.
- The remaining defect was in the new line's consequence: its target was
  already `close_window`, but the consequence also called
  `GwpCommon.TryFinishPlayerEncounter`. That attempted a second encounter
  shutdown while the conversation engine was creating or closing the line.
  Therefore the first repair changed the crash location rather than completing
  the fix.
- The returning line now has no consequence and relies exclusively on
  Bannerlord's normal `close_window` flow. All suppression-time calls that
  force-finished `PlayerEncounter` from the hourly tick or map-event callback
  were also removed. Suppression now only selects the dedicated safe dialogue
  and prevents the ordinary enforcement dialogue from being selected.
- The first responsive-width button was also rejected visually in the in-game
  retry: its `CoverChildren`/`MaxWidth=300` layout produced a long, thin banner.
  The button has been restored to the original fixed `150 x 48` proportions,
  and the English button label is now the compact `Deterrence`. The longer
  explanatory wording remains in the hover hint and details window.
- The corrective Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. The deployed normal-client and editor assemblies
  match at SHA-256
  `47BFC370099AAFC607BF7E07BE8A1767ED31B51B93752D3DA13E0CC38033184C`.

# 2026-07-16 Old English names and split player READMEs

## Naming policy

- Direct romanizations of the former Chinese founder names were rejected. The
  six stable founder IDs use attested Old English feminine names only in
  English, while CNs preserves the original Chinese names:
  - `gw_leader_0`: `Aethelflaed` / `梵蒂`
  - `gw_leader_1`: `Cyneburh` / `约珥`
  - `gw_leader_2`: `Mildthryth` / `弥瑟`
  - `gw_leader_3`: `Wynflaed` / `圣铎`
  - `gw_leader_4`: `Eadgifu` / `晨曦`
  - `gw_leader_5`: `Wulfhild` / `暮光`
- The generated daughter/adopted-heir pool has two intentionally independent
  presentations on the same `36` stable keys. English uses charter-attested Old
  English spellings such as `Eadgyth`, `Ealhswith`, and `Leofgifu`; CNs restores
  the original pool beginning `澄音`, `祈安`, `望舒` and ending `书宁`, `凝光`,
  `夕晨` rather than phonetic translations of the English names.
- If the full pool is exhausted, English uses Roman numerals `II` through `X`;
  CNs preserves the original suffixes `二` through `十`.
- `RefreshPoliceClanFamilyPresentation` now reapplies keyed localized names to
  the six founders as well as deterministic names to generated members. This is
  required so existing saves adopt the new names when loaded; no new campaign
  is required and no save field was added or changed.
- Founder encyclopedia prose uses the appropriate displayed name in each
  language. Stable hero IDs and localization keys were deliberately kept
  unchanged for save and translation compatibility.

## Player documentation policy

- The former bilingual `_Module/README.md` was too detailed for players and has
  been replaced by two short files:
  - `_Module/README.md`: Simplified Chinese
  - `_Module/README_EN.md`: English
- Both contain the same installation, latest-update, playable-content, and
  contact information. Release notes now state only what was added or fixed;
  voice-direction rationale, implementation details, test history, and minor
  internal changes remain in this maintenance document only.

## Validation and deployment

- The English-default and CNs localization tables both contain `510` keyed
  entries. Validation found `0` conflicting defaults, duplicate keys, missing
  entries, extra entries, placeholder mismatches, or XML parse errors.
- The six founders and `36` generated-heir keys are complete in both languages.
  English and CNs deliberately use different name sets while sharing stable
  keys, so changing the game language selects the intended set.
- `_Module/README.md` and `_Module/README_EN.md` are each `38` lines. The
  English file contains no CJK text, and both live copies match their repository
  versions byte for byte.
- The final Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. The normal-client and editor DLLs match at
  SHA-256
  `2BC151327BBFC490BF3F15B6A95507B059F3D56D2C50898C669CDA204B4A42F4`.
- All `26` deployable `_Module` files match the live module. The live module
  contains no `Assets`, `AssetSources`, or `RuntimeDataCache` directory. The
  protected asset packages remain unchanged at their hashes recorded above.

# 2026-07-16 village-relief encounter side repair

- Symptom: after choosing the custom option to help villagers resist an active
  raid, the next native `encounter` screen said that the player had arrived to
  plunder the village and warned that raiding would declare war on its faction.
- In-game verification clarified that the actual battle sides were already
  correct: the player fought beside the militia against the raiders. The defect
  was presentation-only, so replacing or rebuilding the map event would be an
  unnecessary and potentially disruptive repair.
- Cause: the custom option correctly calls
  `PlayerEncounter.JoinBattle(BattleSideEnum.Defender)`, but Bannerlord retains
  the raid event's standard hostile-village `ENCOUNTER_TEXT` when the generic
  `encounter` menu initializes.
- Repair: the direct defender join remains unchanged. A one-shot
  `AfterGameMenuInitializedEvent` override now replaces `ENCOUNTER_TEXT` and
  `ATTACK_TEXT` only for the next `encounter` menu opened by the custom village
  defense action. CNs tells the player that they have joined the militia to
  resist the raiders; English conveys the same meaning. The override then clears
  itself and cannot affect ordinary encounters or a choice to aid the raiders.

# 2026-07-16 release archive placement repair

- Symptom: every ordinary build appeared to recreate
  `Modules\GreyWarden\GreyWarden-v1.4.7.zip` and its checksum inside the live
  module, adding roughly `374 MB` of non-runtime data to the test installation.
- Cause: the formal `v1.4.7-r2` archive and checksum had been parked inside
  repository `_Module`. The build target did not generate them, but its broad
  `_Module\**` copy item treated them as ordinary runtime files and recopied
  them into the live module after deletion. Exact `.gitignore` entries hid the
  misplaced source artifacts from `git status`.
- Repair:
  - Moved the `v1.4.7-r2` ZIP and checksum to
    `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules`, beside the
    live `GreyWarden` directory. The ZIP SHA-256 remains
    `673FD72AE70A467C3336D7DDC1762AA2CDE8735BAB05A0464FD38BBF1C4CEE27`,
    matching the asset published in GitHub release `v1.4.7-r2`.
  - Removed both copies from repository `_Module` and live
    `Modules\GreyWarden`.
  - Removed the two exact archive ignores so any future archive accidentally
    placed in `_Module` becomes visible to Git.
  - Added defensive `*.zip` and `*.zip.sha256` exclusions to `CopyModuleData`.
    A normal build therefore cannot copy a release archive into the live module
    even if one is mistakenly placed under `_Module` again.
- Formal archives are now created only as part of a GitHub push/release task;
  no automatic packaging target was added to compilation.

# 2026-07-17 current archive and source push

- At the user's request, refreshed the local player archive as part of the same
  task that commits and pushes the current source, without creating a tag or a
  GitHub Release.
- Output remains beside the live module rather than inside it:
  - `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7.zip`
  - `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7.zip.sha256`
- New archive size: `349,278,336` bytes. SHA-256:
  `98A1FDE4E6992675AB8CA9C463EC0A1028F04FD30092F142D03D07DCA9DBA6CC`.
- The ZIP contains one top-level `GreyWarden` directory and `28` runtime files.
  It includes both player READMEs, the current normal-client DLL, both protected
  TPACs, GUI, ModuleData/languages, sounds, shaders, and `SubModule.xml`.
- Validation found no editor tree, editor binary, PDB, nested ZIP, or diagnostic
  content. A full extraction produced `0` missing, mismatched, or extra files;
  the extracted DLL matches the live normal-client DLL. Protected TPAC hashes
  remain unchanged.
- This archive refresh is local only. No new GitHub Release is to be created;
  GitHub receives only the source commit on `main` in this task.

# 2026-07-17 mission-local battle mastery rebalance

## Player-visible rules

- Removed the former `1.5x` effective maximum-health multiplier. Grey Warden
  humans now use the active native game mode's ordinary maximum-health result.
- A real or fallback kick/shield-bash contact against an enemy human unlocks
  `1000` effective One Handed and Athletics for that individual Grey Warden for
  the rest of the current mission. The existing rank-based `40/60/80%`
  knockdown roll remains separate; a landed alternative attack unlocks mastery
  whether or not that roll produces a knockdown.
- Each Grey Warden tracks bow releases independently. The tenth missile release
  whose weapon's relevant skill is Bow unlocks `1000` effective Bow for that
  agent for the rest of the mission. Misses count because the rule is based on
  arrows fired, not targets hit; crossbows and thrown weapons do not count.
- No campaign character skill is written and no mastery field is serialized.
  State is kept by the mission behavior, removed when the agent is deleted, and
  cleared with the behavior at mission end.

## Effective-skill implementation

- Bannerlord 1.4.7's character-development model allocates `1024` skill levels
  and naturally tops out at `1023`. The requested mastery value of `1000` stays
  inside that engine range while deliberately exceeding the ordinary
  combat-stat reference value of `300`.
- `GwpAgentStatCalculateModel.ApplyBattleMastery` supplies the mission-local
  value without touching the underlying troop or hero. Native stat models also
  call their own `GetEffectiveSkill` implementations while rebuilding driven
  properties, so `GwpBattleMasteryEffectiveSkillPatch` applies the same result
  to the base, Sandbox, and available Naval implementations. Calling
  `Agent.UpdateAgentStats()` when mastery unlocks makes movement, weapon
  handling, accuracy, damage, and AI recalculate immediately.
- No separate shield skill was added. Bannerlord exposes driven shield
  properties rather than a normal `SkillObject` equivalent to One Handed or
  Athletics, and the user explicitly chose to omit that part of the bonus.

## Initial-stat and equipment rebalance

- Normal troop skills now match the native Empire counterpart of the same
  branch and tier: recruit remains equal to Imperial Recruit; heavy infantry
  matches Imperial Legionary; archer matches Imperial Palatine Guard; knight
  matches Imperial Elite Cataphract.
- The custom-battle commander no longer uses the former all-`300` skill set;
  it now uses the same native strong Empire knight-lord profile already used by
  a founding Grey Warden. The six campaign founders retain their existing
  differentiated native strong-lord profiles.
- Equipment was otherwise left unchanged. Only the recruit-exclusive
  `winfhelmet` changed, from `52` to `45` head armour. The heavy winged helmet,
  knight and lord equipment, great-shield stats, protected inherited equipment,
  and black-gold shield were not changed.

## Build and validation

- The first Release build after implementation completed with `0` errors and
  the existing `44` nullable-analysis warnings. The build target deployed the
  module data and both normal-client/editor assemblies to the live module.
- All `17` deployable XML files parse successfully. The `24` deployable source
  files have `0` missing or hash-mismatched live counterparts; both player
  READMEs therefore also match their live copies.
- The normal-client and editor DLLs match at SHA-256
  `E3C9174B967A4625F9A2AC71F8FCF2CE95946768B777EB5A8C4111CAD9A012C9`.
  The live module contains no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP,
  or checksum file, so this is a valid normal-client test layout.
- Static validation found no remaining health-multiplier reference. Parsed
  troop values exactly match the intended Empire baselines and the
  recruit-only helmet reports `45` head armour.

# 2026-07-17 incremental battle mastery follow-up

- In-game verification confirmed that the temporary effective One Handed and
  Athletics value of `1000` works and produces the intended very strong combat
  result. The rejected part was the abrupt direct unlock, not the effective
  skill mechanism or its upper limit.
- The first follow-up briefly changed the rule to `+20` One Handed/Athletics on
  a registered alternative-attack contact and `+10` Bow per arrow. The user
  then clarified that contact is the wrong trigger: the mod's native/fallback
  resolver already guarantees the control result, and growth belongs to the
  deliberate kick/shield-bash action itself.
- The final rule awards each eligible Grey Warden agent `+50` effective One
  Handed and `+50` effective Athletics when one alternative-attack action is
  accepted by the shared AI/player resolver. It is awarded before target
  selection, so the action still counts if no native collision occurs or no
  fallback target is currently available. It is no longer awarded from
  `OnAgentHit`, preventing native/fallback resolution from becoming a second
  growth event.
- Every actual Bow missile release adds `+10` effective Bow. The per-agent
  bonuses are clamped before addition and the final effective value is capped
  at `1000`; no underlying troop/hero skill or save data is changed. All Grey
  Warden troops, cavalry, founders, later clan members, and the custom-battle
  commander use the same rules. No cavalry-only kill bonus was added.
- The player-facing CN/EN release entries were deliberately compressed to one
  experience-level sentence: Grey Wardens grow stronger while fighting. Exact
  triggers and values remain here only. `AGENTS.md` now makes this the default
  rule for future player README updates: no trigger counts, stat gains, caps,
  formulas, or minor tuning unless omission would prevent a necessary player
  decision.
- The final Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. All `24` deployable files match the live module;
  both runtime DLLs match at SHA-256
  `C8A85F5F3AA3498BAC5CA34C0BD47F8864F6EA0B08AE066E368D3AE8BB4809E6`.
  All deployable XML parses, and the live normal-client layout contains no
  editor tree or archive file.

# 2026-07-17 player release-note granularity correction

- The earlier policy of reducing each player-visible update to one sentence was
  too aggressive: it hid materially different balance and progression changes
  behind one vague line. The CN/EN r4 notes now use two concise player-facing
  bullets, separating the troop rebalance from the new in-battle mastery loop.
- `AGENTS.md` now requires one short bullet per meaningful player-visible
  feature or outcome, while grouping minor tuning and keeping exact triggers,
  values, caps, formulas, implementation details, and inconsequential notes in
  this maintenance document.
- Both revised READMEs were copied to the live module. All `24` runtime
  deployable files match their live counterparts; the CN README SHA-256 is
  `6412CAD5C081F1663150D4F9B91586F7B936CC5567A3241936D21BD7AB76966D` and
  the EN README SHA-256 is
  `356221766D5367CFA22D8B436AD484C35D20422FC45008A17848DDEC77CB67F6`.
  The live module still contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, or archive file. An initial all-file comparison reported
  the repository's `12` editor-source files as missing from live; this is the
  required normal-client layout, so the corrected runtime-only comparison
  excludes those three editor trees and reports `0` mismatches.

# 2026-07-17 Grey Warden sparring mission research

- The local game install contains the user's legacy reference mod at
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\MissionDuel`.
  Its active client DLL identifies itself as MissionDuel `1.0.5` and has
  SHA-256
  `D52378DD262BCE86ABCBEDB833A4554B27954274BA96705782C2A92C6117B6E5`.
  It is a read-only reference and is not a Grey Warden dependency.
- Decompilation established that MissionDuel does not create a peaceful duel
  mission. It injects `DuelMissionLogic` into every mission, waits for an
  already-running `MissionMode.Battle` field battle, and temporarily suspends
  that battle around two existing hero agents. It therefore cannot be reused
  wholesale for the requested dialogue-triggered sparring flow.
- Its spectator technique is directly useful. When a duel starts it holds all
  non-duelist formations at their current median positions, changes them to a
  loose arrangement, orders hold fire and face-enemy, disables formation AI,
  clears each spectator's target and automatic target selection, and sets
  `Agent.MortalityState.Invulnerable`. It gives the two duelists explicit
  targets in temporary formations and periodically plays the native cheer
  yells and `act_cheer_1` through `act_cheer_4` animations. The 1.4.7 runtime
  enum was verified as `Mortal = 0`, `Invulnerable = 1`, and `Immortal = 2`.
- The legacy mod's post-duel behavior must not be copied: it restores both
  armies and resumes the real battle, and it contains unrelated gold,
  equipment, attribute, and relationship rewards. Grey Warden sparring should
  instead keep campaign parties and save data untouched, declare the winner,
  then permit the normal Tab mission exit.
- The current 1.4.7 official city path is
  `CampaignMission.OpenArenaDuelMission(...)`. The stock controller spawns only
  the player and target, uses `SimpleAgentOrigin`, converts defeat to
  unconsciousness through `ArenaAgentStateDeciderLogic`, reports the winner by
  callback, and supplies the arena exit rules. The mission should be queued
  after dialogue closes, following the stock quest pattern, rather than opened
  inside the active conversation mission.
- For a field challenge, calling `CampaignMission.OpenBattleMission(...)`
  without a genuine campaign `MapEvent` is unsafe: the stock battle opener
  dereferences `MobileParty.MainParty.MapEvent` and attaches casualty, party,
  encounter, loot, and result logic. Fabricating a temporary war encounter
  would create avoidable campaign side effects.
- The safe design still reuses the original game systems: obtain the local
  battlefield from `SceneModel.GetBattleSceneForMapPatch(...)`, create its
  initializer through `SandBoxMissions.CreateSandBoxMissionInitializerRecord`,
  open a campaign-mode `MissionMode.Battle`, spawn agents from their real
  characters and equipment with `SimpleAgentOrigin`, use native teams,
  formations, battle views and nonlethal agent-state logic, and allow Tab only
  after the duel resolves. A community source example in
  `actualAnian/RealmsForgotten` confirms that a dialogue-deferred custom duel
  can be opened with `MissionState.OpenNew` and manually spawned hero agents,
  although its hard-coded arena scene, positions, killed-only result test, and
  immediate auto-exit are not suitable for direct reuse.
- Recommended field version: spawn the player and challenged Grey Warden as
  the only combatants, then spawn the two present parties' soldiers in two
  held spectator formations. Apply the proven no-target, hold-fire,
  invulnerable and cheering layer only to spectators. On either duelist's
  unconscious state, freeze combat, display the result, enable normal Tab exit,
  and return to the campaign map without casualties, prisoners, loot, war,
  relation, experience rewards, or save-data changes.
- This entry records research and architecture only. No player-visible code,
  README, build output, or live module file was changed for the sparring
  feature in this step.

# 2026-07-17 Grey Warden sparring implementation

## Campaign entry and town path

- `GreyWardenSparringBehavior` is registered as a campaign behavior and adds a
  dialogue challenge for healthy Grey Warden lords when the player is healthy
  and not already in a map event. The option is suppressed during active
  patrol/enforcement conversations so it cannot displace a crime-resolution
  flow.
- The conversation closes before any new mission is opened. In a town, the
  behavior queues the settlement's arena through `GameMenuManager.NextLocation`
  and opens the native `CampaignMission.OpenArenaDuelMission` only after the
  town menu resumes. The stock nonlethal duel, equipment, result callback, and
  Tab-exit behavior are retained; the bout does not create campaign casualties
  or rewards.

## Field path and spectators

- A field challenge resolves the current map patch through
  `SceneModel.GetBattleSceneForMapPatch`, then opens that native battlefield as
  an `ArenaDuelMission` view with a custom mission controller. It deliberately
  does not create a `MapEvent` or attach the campaign battle casualty, loot,
  prisoner, encounter, or result systems.
- The player and challenged lord spawn as the only combatants, on foot, with
  their real characters and battle equipment. Healthy troops from the two
  present parties fill held ranks behind them up to the player's configured
  battle-size limit. Spectators have no targets, hold fire, cannot be harmed,
  and use native cheer calls and animations. If the challenged lord is already
  in the player's party, the shared roster is spawned only once and neither
  duelist is duplicated as a spectator.
- `ArenaAgentStateDeciderLogic` converts a duelist's defeat to unconsciousness.
  After that removal, the survivor stops fighting, the spectators cheer, a
  localized result is displayed, and normal Tab exit is enabled. Before a
  result, a Tab request is refused. No party roster, hero health, relation,
  gold, inventory, experience, prisoner state, or save field is changed.
- `GwpBattleReinforcementBehavior` explicitly rejects missions containing
  `GreyWardenFieldSparringMissionController`; the field scene therefore cannot
  trigger the Grey Warden reinforcement feature merely because it reports
  itself as a field battle. Normal campaign field battles are unchanged.

## Localization, build, and deployment

- English remains embedded as the base language. Ten new Chinese strings cover
  the challenge, acceptance, launch/spawn failures, start, both town/field
  results, and the pre-result exit refusal. Both player READMEs record the
  feature at experience level only under `v1.4.7-r5`.
- The final Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. The build target deployed module data and the
  normal-client/editor assemblies to the live module.
- All `17` deployable XML files parse successfully. All `24` deployable source
  files have `0` missing or hash-mismatched live counterparts. The normal-client
  and editor DLLs match at SHA-256
  `9580EDFCEFA8003853B8B122C5251839B00A3B1EA04655B483FE6EB8D275F7B2`.
  The live CN README hash is
  `03EB9D2FEF0C8E439F2516D327107A7E7C36908C040714C31F98ED51984880E6`;
  the EN README hash is
  `464B1A2F9BC17867992B8AE0C4535FD7C9B33082AD1D66D562FC887969FBC7FB`.
- The live normal-client layout contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum file. No archive, Git commit, push, tag,
  or GitHub Release was created in this implementation task.

# 2026-07-17 field sparring first-test diagnosis and repair

- The first in-game field test did not reach the custom duel at all. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_37848.txt`, the
  second challenge closes at approximately line `7015`, returns to the
  `encounter` menu, applies a `-10` Grey Warden relation change, and opens a
  stock mission named `Battle` at approximately line `7031`. The hundreds of
  soldiers attacking the player were therefore participants in a genuine
  campaign battle, not failed spectators from the custom controller.
- The cause was the still-active mobile-party `PlayerEncounter`. Closing its
  conversation mission without peacefully finishing the encounter allowed the
  stock encounter menu to interpret the return as an attack. The field
  challenge now calls `GwpCommon.TryFinishPlayerEncounter()` from the dialogue
  consequence and waits until both the encounter and any `MapEvent` are absent
  before opening the practice mission. It does not force-end the conversation
  mission from inside that consequence; the dialogue's normal `close_window`
  transition performs that step.
- The stored challenge survived the accidental battle. After the player lost,
  the same log records `DefenderVictory`, capture by Vandi, and only then
  `Opening new mission ArenaDuelMission` near line `7871`. Pending field bouts
  are now cancelled whenever the main party starts a genuine `MapEvent`, or if
  the player/opponent becomes a prisoner or otherwise invalid. A stale duel can
  no longer launch after battle or during prisoner processing.
- The delayed custom mission then failed before spawning the player. Its stack
  trace identifies `Mission.GetSpawnPathFrame(...)` from `SpawnBout`; that API
  requires the stock campaign battle spawn-path selector, which this peaceful
  no-`MapEvent` mission intentionally does not have. Spawning now derives the
  centre and orientation from the native battlefield's `battle_set` entity and
  grounds all positions against the scene terrain. It no longer depends on a
  campaign battle spawn pipeline.
- If a future battlefield is genuinely missing the required scene entity or
  agent creation otherwise fails, the controller now reports the abandoned
  bout and ends the mission automatically on the next mission tick. It no
  longer leaves a playerless mission waiting for a Tab exit. The Chinese
  failure message was updated to match this automatic return; English remains
  the embedded base text.
- The repaired Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. All `17` deployable XML files parse, and all
  `24` deployable source files have `0` missing or hash-mismatched live
  counterparts. The normal-client and editor DLLs match at SHA-256
  `33C5608133B0C6689A2A0D4241232E10FF2EAE6F60E36521A472F9D670EB0D9D`.
  The live CN README hash is
  `2AEE373C3186FB253DB2E4D6F3384485F24A67298062BE6FD9B01BFC9EC834CD`;
  the EN README hash is
  `7D144113AB9006A6D0D572FEEC19D4E3D81F183785CCAB40D447C7ECA8112CD5`.
  The live normal-client layout contains no editor tree, ZIP, or checksum file.
  No archive, Git commit, push, tag, or GitHub Release was created.

# 2026-07-17 field sparring second-test diagnosis and repair

- The second test did open the intended custom scene even though the player
  only saw the paused campaign map again. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_24584.txt`, the
  conversation closes at `05:41:18`, `Opening new mission ArenaDuelMission`
  follows immediately, `MissionScreen` activates, the map screen deactivates,
  and `battle_terrain_L` loads. The mission then fails during its first spawn
  tick, returns to the map, and makes the successful scene transition too
  brief to be visible.
- The first independent failure is a managed `NullReferenceException` at the
  old `SpawnBout` ground-position calculation. The previous repair replaced
  the stock `Mission.GetSpawnPathFrame` call with the scene's `battle_set`, but
  then converted nearby two-dimensional points through
  `WorldPosition.GetGroundVec3`; that conversion is not initialized safely in
  this deliberately MapEvent-free mission. The new code takes exact XYZ frames
  from the battlefield's authored `spawn_path_*` paths. It selects the longest
  valid path, places both duelists and the two rank centres around its midpoint,
  and uses the scene's own ground-height query only for lateral spectator
  offsets. The `battle_set` frame remains a guarded compatibility fallback for
  custom scenes without paths.
- The delayed native crash is a separate failure, not a campaign-map movement
  error. The user click merely occurred while the invalid mission state was
  still unwinding. The matching dump is
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.24584.dmp`.
  CLR inspection identifies
  `SandBox.Missions.MissionLogics.MissionAgentHandler.EarlyStart()` as the
  throwing managed frame during `Mission.AfterStart`. In Bannerlord 1.4.7 that
  handler reads `Settlement.CurrentSettlement.Position`; a field mission has
  no current settlement, so the value is null. `MissionAgentHandler` has been
  removed from the field sparring behavior list. It remains untouched in the
  native town-arena path, where a settlement and `MissionLocationLogic` exist.
- The legacy local MissionDuel DLL was checked again rather than imported. It
  never opens a mission and injects only its duel logic into an already-valid
  field battle, so it does not contain a reusable scene launcher. Its useful
  formation-hold, no-target, invulnerable-spectator and cheering behaviors are
  already retained. The new launcher follows the same field-safe principle by
  excluding settlement-only behaviors, while still avoiding the war,
  casualties and relation changes that wholesale reuse of a real battle would
  introduce.
- A successful field spawn now writes a concise diagnostic containing the
  selected authored path and spectator count. A future failure still reports
  the localized abandonment notice and exits automatically rather than leaving
  a playerless mission active.
- The repaired code completed a full Release build with `0` errors and the
  existing `44` nullable-analysis warnings; the final unchanged incremental
  build after documentation synchronization completed with `0` errors. All
  `17` deployable XML files parse, the `10` Chinese sparring ids remain present,
  and all `24` runtime source files have `0` missing or hash-mismatched live
  counterparts. The normal-client and editor DLLs match at SHA-256
  `DB08C862E63C8BE0BDC6572E9D4AA7DBF71CC9369504FBCA6BF7E56EDEC27378`.
  The live CN README hash is
  `DFC77BBB862506C22CD17DB13CC0D62FE60D53ED9BC7853499C6B673E80586BF`;
  the EN README hash is
  `210250B2F78F8000E7F07A1A1AE71F1502A7A71FFB734843BCA24C592C9643D7`.
  Decompilation of that deployed DLL confirms `GetAllSpawnPaths` and
  `GetGroundHeightAtPosition` are present while both the old `WorldPosition`
  conversion and the field `MissionAgentHandler` construction are absent.
  The live normal-client module contains no editor tree, ZIP, or checksum file.
  No archive, Git commit, push, tag, or GitHub Release was created.

# 2026-07-17 field sparring third-test lifecycle diagnosis and repair

- The third test reproduced the short loading animation, immediate return to
  the campaign map, and localized abandonment notice three times. The evidence
  is in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_28152.txt` at
  approximately lines `4275-4345`, `4442-4512`, and `4630-4700`. Each attempt
  opens `ArenaDuelMission`, deactivates `MapScreen`, activates `MissionScreen`,
  loads `battle_terrain_L`, and then throws from `SpawnBout`. This confirms that
  the campaign launcher and scene selection are working; the failure is inside
  custom mission initialization.
- The decisive ordering evidence is that the spawn exception is logged before
  `Mission-AddTeam-Defender` and `Mission-AddTeam-Attacker`. The custom
  controller called `SpawnBout` from `OnPreMissionTick`, but its teams were not
  created until `AfterStart`. Consequently `Mission.PlayerTeam` and
  `Mission.PlayerEnemyTeam` were still null when the code requested the duel
  formations. The source line reported at `SpawnBout` line `528` is the
  optimized caller sequence rather than proof of another terrain-coordinate
  failure.
- The installed Bannerlord 1.4.7
  `ArenaDuelMissionController` was decompiled again as the authoritative
  lifecycle reference. Its `AfterStart` method initializes mission teams first,
  then resolves spawn frames and immediately spawns both duelists. The field
  controller now follows that exact order: mode setup, team initialization,
  position resolution, duelists, spectators, and formation hold all occur from
  `AfterStart`. The premature `OnPreMissionTick` spawn override has been
  removed.
- Positioning has also been simplified to the battlefield's `battle_set`
  centre, which the earlier test already proved is present in
  `battle_terrain_L`. Nearby height probes retain the anchor's valid Z value as
  a fallback. This removes the unnecessary authored-path query without
  reintroducing the unsafe `WorldPosition` conversion.
- Internal phase labels now distinguish team initialization, position
  resolution, each duelist, each spectator rank, and formation holding in any
  future exception log. They are developer diagnostics only and are not shown
  to players.
- The repaired code completed a full Release build with `0` errors and the
  existing `44` nullable-analysis warnings; the final incremental build after
  README synchronization completed with `0` errors. Decompilation of the
  deployed controller confirms there is no `OnPreMissionTick` override and
  that `InitializeTeams()` precedes `SpawnBout()` inside `AfterStart`.
- All `17` deployable XML files parse, and all `24` runtime source files have
  `0` missing or hash-mismatched live counterparts. The normal-client and
  editor DLLs match at SHA-256
  `CDE6E43AAAFC356BCD52EC591C728ED7E62E3C343964941A3D3B7FC992CBBB36`.
  The live CN README hash is
  `D5299297F12C06C46B431AB942A2E6662E805565494E8E59A58F4614C04AB4E8`;
  the EN README hash is
  `EE580BFEC079A23793ADC971F61699AEC3685917AE410C4E558E3B4A6A41B793`.
  The live module contains no editor tree, ZIP, or checksum file. No archive,
  Git commit, push, tag, or GitHub Release was created.

# 2026-07-17 field sparring direct-launch and staging redesign

- The required transition is not a shorter campaign-map delay. Returning to
  the map and requiring any movement click before loading the field scene is
  itself invalid behavior. The old field launcher was subscribed to
  `CampaignEvents.TickEvent`; because campaign time is stopped when the map
  conversation closes, that callback did not advance until a player input
  resumed map movement. The movement click was therefore acting as the launch
  trigger.
- The campaign `TickEvent` listener and its delay counter have been removed.
  `SubModule.OnApplicationTick` now services only the queued field challenge.
  Once the stock `close_window` transition has disposed the conversation
  mission, the next application frame verifies that no conversation, mission,
  encounter, or `MapEvent` remains and immediately calls
  `MissionState.OpenNew`. This path does not depend on campaign time, party
  movement, or any world-map click. `MapState` is only the safe predecessor
  state between two missions, not an input gate.
- The friendly party encounter is still ended with
  `PlayerEncounter.Finish(false)` before launch so the practice bout cannot be
  converted into a campaign war, casualty event, relation penalty, capture, or
  prisoner flow. A genuine `MapEvent` involving the main party cancels the
  pending challenge rather than allowing a stale bout to open later.
- The installed Bannerlord 1.4.7 `HideoutMissionController` was decompiled as
  the authoritative staged-duel reference. Its sequence is now mirrored:
  teams begin non-hostile, the opponent waits for an in-mission conversation,
  the conversation-end callback enables hostility only for the two duelists,
  spectators are placed on `Team.Invalid` during the fight, and only the
  winner's original team is restored for the victory reaction. The local old
  MissionDuel mod remains a secondary reference for invulnerable, untargetable,
  held spectators; its launcher was not copied because it only injects logic
  into an already-running campaign battle.
- Field placement now uses
  `BattleSpawnPathSelector.FindBestInitialPath`, the same native authored path
  selection used for field deployments. The two spectator ranks are placed 25
  metres to either side of the selected midpoint and face one another, leaving
  a 50-metre central ground. The opponent waits at the midpoint; the player
  begins just ahead of the friendly rank and walks into the centre. Scenes
  without a usable authored path retain a grounded `battle_set` fallback.
- Before the bout, both teams are non-hostile. Spectators spawn in line
  formation with formation AI disabled, hold-fire orders, no automatic target,
  invulnerable mortality, and a centre-facing direction. No cheer action is
  issued during scene creation. On reaching the waiting opponent, proximity
  starts `MissionConversationLogic`; accepting the centre-field exchange ends
  that conversation and begins the duel. During the duel, both ranks remain
  invulnerable and scripted in place while looking toward their own duelist.
- After either duelist falls, hostility ends, only the winning rank is restored
  to its original team, and native `AgentVictoryLogic` schedules the standard
  high-cheer reaction. Tab remains blocked until the duel has a result.
- Three new Chinese localization ids cover the centre-field exchange and the
  approach prompt; English remains the embedded base language. There are now
  `13` Chinese sparring ids, and both public READMEs describe the corrected
  experience without exposing the implementation or numerical layout.
- The full Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings; the final incremental deployment completed with
  `0` errors. Decompilation of the deployed assembly confirms the
  `OnApplicationTick` launcher, native field path selector, mission
  conversation logic, spectator `Team.Invalid` transition, and native victory
  timer are present. It also confirms the old campaign `TickEvent` launcher,
  premature `OnPreMissionTick` spawn, and manual spectator-cheer path are
  absent.
- All `17` deployable XML files parse successfully. All `24` deployable source
  files have `0` missing or hash-mismatched live counterparts. The normal-client
  and editor DLLs match at SHA-256
  `3D7F0BD234577B65A779AE3BE0BD95F1195D92B69E22CE2FD38860742121FA48`.
  The live CN README hash is
  `85171790B676E41EE8FAF962F8FB6DD7B02975BA016ED54D27A6037C445D578A`;
  the EN README hash is
  `4621FD57662EAEF551C7EEE8141775365DF518A5C6FC8F80478C6A0E77F0DE8F`.
  The live normal-client module contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum file. No archive, Git commit, push, tag,
  or GitHub Release was created.

# 2026-07-17 field sparring native-deployment and conversation-view repair

- The fourth field test proves that the application-frame launcher repair is
  successful. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_39072.txt`, the
  campaign conversation ends at line `4111`, `Opening new mission
  ArenaDuelMission` follows at line `4115`, and the field scene reports a
  successful spawn at line `4177`. There is no intervening world-map click or
  movement command. This part must not be rolled back.
- The remaining deployment error was caused by treating the return value of
  `BattleSpawnPathSelector.FindBestInitialPath` as two completed side
  deployment frames. Its pivot is the encounter point on one authored path,
  not the defender and attacker formation bases. Manually placing both ranks a
  short fixed distance around that pivot produced the reported point-blank
  lines and made the waiting lord's approximate path-facing direction appear
  side-on or backwards.
- Bannerlord 1.4.7's installed `MenuHelper.EncounterAttackConsequence`,
  `BattleSpawnPathSelector`, `SpawnPathData`, and
  `DefaultBattleMissionAgentSpawnLogic` were decompiled as the current native
  references. The mission initializer now supplies `SceneHasMapPatch`, the
  campaign patch coordinates, and an encounter direction before scene load.
  The custom controller marks the mission as a field battle in `EarlyStart`,
  allowing the engine to initialize terrain-snapped side path data before the
  controller's `AfterStart` runs.
- The controller now reads the defender and attacker data through
  `Mission.GetInitialSpawnPathData`, applies the stock
  `ComputeSpawnPathDeploymentOffset` and `ComputeDeploymentBaseOffsets`
  calculations, and places each spectator formation at its native side base.
  The player begins just ahead of the defender line, while the opposing lord
  waits at the authored encounter point and faces the player's actual
  position. Only custom scenes without valid spawn paths retain a guarded,
  wide `battle_set` fallback.
- The local legacy reference at
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\MissionDuel`
  was decompiled again. Its useful architectural lesson is that it never
  creates a pseudo-battle or replaces native deployment: it injects into an
  already valid field battle, stops formations at their cached positions,
  orders hold fire, disables targets, and makes non-duelists invulnerable.
  Those principles are retained here without copying its obsolete API calls.
- The approach conversation was entering campaign dialogue logic correctly,
  but the UI was absent. The same test log starts the conversation at line
  `4212` and selects AI line `gwp_sparring_field_centre` at line `4219`, yet no
  player response line follows. The former `ArenaDuelMission` view set contains
  neither `MissionConversationCameraView` nor the mission conversation UI and
  also attaches an arena audience handler. That explains both the zoom with no
  dialogue and the inappropriate cheering on entry.
- The first conversation-view repair tried the stock `Alley`
  combat-with-conversation view set because it supplies both conversation
  views without an arena audience. The subsequent test proved that this view
  set also carries a boundary-crossing UI whose separate mission-logic
  dependency is absent in the lightweight field bout. The attempt was removed
  after the entry-crash dump identified that dependency; see the following
  diagnosis.
- Mission teams are briefly genuine enemies while the field-battle path and
  agents initialize, then become non-hostile for the approach and dialogue.
  Accepting the challenge restores mission-only hostility; the two spectator
  formations remain immobile, hold fire, untargetable, and invulnerable, and
  the winner's formation alone receives the native victory reaction. No
  campaign `MapEvent`, faction war, casualty ledger, capture, relation change,
  or battle reward is created. Using a real campaign `MapEvent` was rejected
  because its stock finish path necessarily applies exactly those persistent
  battle consequences.
- The first full Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings; the final incremental deployment completed with
  `0` errors. Decompilation of the deployed DLL confirms the map-patch
  initializer fields, `Alley` view selection, both native side path queries,
  stock deployment-offset calculations, encounter-frame placement, and the
  hostile/non-hostile/hostile stage transitions. The obsolete direct call to
  `FindBestInitialPath` is absent from the deployed controller.
- All `17` deployable XML files parse, all `13` Chinese sparring localization
  ids remain present, and all `24` deployable source files have `0` missing or
  hash-mismatched live counterparts. The normal-client and editor DLLs match at
  SHA-256
  `73093E43B6079025C8522FA19CE0ED6AA3D16626E3C093263916967D6EFBB6BC`.
  The live Chinese README hash is
  `D262E2C8A64031DE7C3BD9B948E208D014D7CE28A6808BB78B3755EDD63330AF`;
  the English README hash is
  `C8CCAB249D98AC03461935C2E39ADB40269FC1A9FBCB6FD84DC9803B8C91739E`.
  The live normal-client module contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum file. No archive, Git commit, push, tag,
  or GitHub Release was created. In-game confirmation of the revised positions
  and complete centre-field dialogue remains the next test.

# 2026-07-17 field sparring entry-crash diagnosis and repair

- The next test did not fail during module startup or save loading. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_27772.txt`, the
  initial challenge conversation ends at line `4094`, `Opening new mission
  Alley` appears at line `4098`, `battle_terrain_L` loads, both teams are
  created, and line `4160` reports all `209` spectators spawned. The final log
  entry is `MissionScreen-OnActivate`; the process then crashes before the
  first playable frame.
- The matching dump is
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.27772.dmp`.
  WinDbg/CDB identifies a managed `System.NullReferenceException` in
  `TaleWorlds.MountAndBlade.ViewModelCollection.BoundaryCrossingVM..ctor`,
  called by `MissionGauntletBoundaryCrossingView.OnCreateView`. This is the
  exact `Alley` view dependency described above, not an agent, roster, dialogue,
  XML, or map-state failure.
- The `Alley` view selection has been removed. Field sparring now uses the
  stock `TownMerchant` free-roaming conversation view set: it supplies
  `MissionConversationCameraView` and the Gauntlet conversation UI but carries
  neither `MissionAudienceHandler` nor the boundary-crossing view. Its optional
  barter/name-marker views remain dormant unless called, while the existing
  mission status, equipment, leave, spectator, and notification views remain
  usable during the bout.
- The same test also showed `battle_set fallback` rather than the intended side
  deployment data. The engine's internal spawn selector had not retained side
  records for this lightweight mission at `AfterStart`, although the authored
  paths were present—the earlier implementation had already proved
  `FindBestInitialPath` could resolve `spawn_path_02` at that point. The
  controller now first accepts engine-initialized side data when available and,
  otherwise, rebuilds the identical defender/attacker `SpawnPathData` pair
  through the public native selector and terrain-snapping API. It then applies
  the same stock deployment-offset calculations as before. `battle_set` is now
  reserved only for scenes with no usable authored path at all.
- The corrective full Release build completed with `0` errors and the existing
  `44` nullable-analysis warnings; the documentation-synchronizing incremental
  deployment completed with `0` errors. Decompilation of the deployed assembly
  confirms `TownMerchant`, the campaign patch fields, the engine-initialized
  path check, the late native path reconstruction, and both stock deployment
  offset calls. The crashing `Alley` mission name is absent from the assembly.
- All `17` deployable XML files parse, all `13` Chinese sparring ids remain
  present, and all `24` deployable files have `0` missing or hash-mismatched
  live counterparts. The normal-client and editor DLLs match at SHA-256
  `31781EC44F2F1FD216B4432F6B391C68B3FB7EB573498A552E804B63EA73461B`.
  The live Chinese README hash is
  `A24AEEC6D3CF04B17AE565082E514D774E920A6D0AB9F94A8F772F046D03E185`;
  the English README hash is
  `7839DE6D16A6FA2409C094E09EF51AD2650F2784FA5F42FC698395BBB490D52D`.
  The normal-client module contains no editor tree, ZIP, or checksum file. No
  archive, Git commit, push, tag, or GitHub Release was created. The next test
  should confirm entry, native deployment, the complete centre conversation,
  duel isolation, winner-only cheering, and Tab exit in that order.

# 2026-07-17 field sparring native-spawn and marching-formation redesign

- The latest user test proved that the remaining defect was architectural, not
  another short-distance or facing adjustment. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_12848.txt`,
  `TownMerchant` opens at lines `4437` and `4910`, but the old controller still
  reports `battle_set fallback` at lines `4499` and `4972`. That pseudo-battle
  spawned spectators through fixed custom frames, so the challenged lord could
  appear apart from the army, face away from the player, and skip the visible
  march from the native deployment lines. The fixed-frame/manual-`SpawnAgent`
  route is now retired rather than patched again.
- The replacement builds two `CustomBattleCombatant` instances from the healthy
  members of the real player and opponent parties, preserving each party's name,
  culture, banner, general, character equipment, and mounts. The mission now uses
  `MissionCombatantsLogic`, `BattleSpawnLogic("battle_set")`,
  `DefaultBattleMissionAgentSpawnLogic`, `BattlePowerCalculationLogic`,
  `CustomBattleAgentLogic`, and the indispensable
  `CustomBattleMissionSpawnHandler`. The handler, not the custom controller, owns
  `InitWithSinglePhase`; agents therefore enter through Bannerlord's native field
  deployment path on the first spawn-logic tick. A field challenge is rejected
  when the opponent belongs to `MobileParty.MainParty`, preventing the same
  roster and player character from being cloned onto both sides.
- The first native-spawn draft delegated directly to
  `CustomBattleTroopSupplier`, whose `CustomBattleAgentOrigin.BattleCombatant`
  returns `CustomBattleCombatant`. The 1.4.7
  `CampaignAgentComponent.OwnerParty` implementation hard-casts that value to
  `PartyBase` while starting conversation animations, so the centre conversation
  would deterministically throw `InvalidCastException`. The final
  `GreyWardenSafeTroopSupplier` preserves the stock allocation order but wraps
  every supplied troop in `GreyWardenSafePartyAgentOrigin`: its
  `BattleCombatant` is the real owning `PartyBase`, while wound, kill, route,
  removal, score-hit, and banner callbacks are no-ops. This keeps campaign stat
  and conversation code compatible without writing the practice result back to
  either roster.
- No `MapEvent` or campaign war is created. `MissionCombatantsLogic` continues to
  receive the two side-labelled `CustomBattleCombatant` objects because a real
  `PartyBase.Side` is `None` outside a `MapEvent`; the agents themselves use the
  safe party-backed origins above. The mission deliberately omits
  `BattleEndLogic` and `BattleObserverMissionLogic`, so sparring cannot produce
  campaign casualties, prisoners, loot, relation changes, or an automatic
  campaign battle settlement. `MissionCombatantsLogic` uses `NoTeamAI`: native
  teams, formations, equipment, spawn paths, and battle mode are retained, while
  the sparring controller—not a competing field-battle tactic—owns the ceremonial
  march.
- The controller does no spawning in `AfterStart`. It captures both heroes in
  `OnAgentCreated`, immediately makes human agents invulnerable and targetless,
  waits for `IsInitialSpawnOver`, then stops both spawners and disables
  reinforcements. During staging the native teams are mutually non-hostile; every
  formation holds fire, leaves AI control, and receives explicit line, facing, and
  movement orders. This prevents the former entry shouting, premature attacks,
  arrows, and victory gestures without changing the native starting positions.
- A fixed battle axis is calculated once from the two armies' native deployment
  centres. Both armies then move from those positions toward the midpoint. Target
  positions are clamped through the scene navigation mesh instead of changing a
  cached `WorldPosition` face in place. The controller measures the live front
  ranks from agent projections, corrects both sides toward a `25 m` target gap
  (accepted range `20-30 m`), and requires every active formation to reach its
  target and settle before the opponent may leave the line. A guarded timeout
  records difficult terrain and continues from the closest achieved ranks rather
  than teleporting them.
- The challenged lord remains in its native formation throughout the army march.
  Only after both ranks settle is the lord detached and ordered, using the same
  scripted-walk pattern as the hideout boss sequence, to the navigation-reachable
  midpoint at a walking speed. At the centre its scripted frame, look direction,
  and movement direction are refreshed toward the live player position so it
  cannot settle with its back to the player. The player stays under direct control
  and must approach the lord to trigger the pre-bout conversation.
- The field mission keeps the proven `TownMerchant` view set because it contains
  both `MissionConversationCameraView` and the Gauntlet conversation UI without
  the `Alley` boundary-view dependency that caused the earlier activation crash.
  The official `CombatWithDialogue` view was also audited, but it requires the
  complete battle-end/observer/boundary contract; adding that contract would
  reintroduce automatic battle settlement into a friendly bout.
- When the conversation closes, all non-duellists move to `Team.Invalid`, hold
  their current positions, remain invulnerable and targetless, and look toward
  their own champion. Only the player and challenged lord return to hostile teams
  and mortal state. On the first knockout the teams return to peace, only the
  winning spectators are restored. The controller applies the same
  `HighCheerActions` and `0.25`-to-`3`-second preset used by the stock hideout
  boss duel before calling `AgentVictoryLogic.SetTimersOfVictoryReactionsOnBattleEnd`;
  no manual yell, direct cheer action, or custom voice is issued. Tab becomes
  available only after that native victory transition has begun.
- A Release compile-only validation completed with `0` errors; the warnings are
  the repository's existing nullable-analysis warnings and the three redesigned
  sparring sources add none. Full Release deployment, live hash comparison, XML
  validation, and deployed-assembly decompilation are recorded in the validation
  bullet below after the final build.
- Final Release validation on 2026-07-17 completed with `0` errors and `44`
  pre-existing nullable warnings. The normal-client and editor
  `GreyWardenPolicePurity.dll` copies both have SHA-256
  `93F59FF5A726FCBB8545CB6C5CC2A3A805629AFE909DF5954D9F3F7E4B199027`.
  All `24` deployable `_Module` source files have present, hash-identical live
  counterparts; all `17` deployable XML files parse successfully and the Chinese
  table contains all `13` `gwp_sparring*` ids. Repository/live README hashes are
  respectively `E3BA8FAB38861B0664C21C7E7D8C8FD06D00A23F9FA526427B08A77BC1FD5DE1`
  and `9253EB54BB4E6AACE3C1C9525AA13D3FBAAE19693833F073DD2FB5F3A28F9061`
  for Chinese and English. The normal-client live module contains no `Assets`,
  `AssetSources`, `RuntimeDataCache`, ZIP, or checksum file. Decompilation of the
  deployed assembly confirms the application-tick launcher, safe party-backed
  origin, native spawn handler and spawn logic, marching state, movement orders,
  centre conversation, `Team.Invalid` spectator isolation, and native victory
  timer. It also confirms that manual `.SpawnAgent(`, `battle_set fallback`, and
  `FindBestInitialPath` paths are absent. No archive, Git commit, push, tag, or
  GitHub Release was created.

# 2026-07-17 field sparring opposite-side native deployment repair

- The next two in-game tests exposed a native-deployment contract error rather
  than a formation-distance tuning problem. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_23988.txt`, the
  native spawner completed with `177` agents and the controller prematurely
  reported a `24.3 m` front gap before remaining permanently in
  `OpponentAdvancing`. The preceding
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_6164.txt` follows
  the same sequence with `236` agents and a reported `25.0 m` gap. Neither log
  contains an exception or crash. The player observation that both armies were
  mixed at the player deployment point is therefore the authoritative symptom.
- The former `native armies spawned at their field deployment lines` log was
  unconditional. When the two army centres coincided, the controller silently
  substituted `Vec2.Forward`, so its later projection calculation could create
  a false `25 m` gap inside the mixed crowd. The controller now records
  `Mission.HasSpawnPath`, `Mission.IsFieldBattle`, both army centres, and their
  separation, and refuses to start the ceremonial march unless the engine has
  produced a valid field spawn path with at least `50 m` between the deployment
  centres. This validation is not a Tab escape path.
- Bannerlord 1.4.7's installed `Mission.AfterStart`,
  `MissionBoundaryPlacer`, `BattleSpawnPathSelector`,
  `DefaultMissionDeploymentPlan`, `DefaultBattleMissionAgentSpawnLogic`, and
  `BannerlordMissions.OpenCustomBattleMission` were decompiled as the current
  references. Their lifecycle is decisive: every behavior's `EarlyStart` runs,
  then the engine initializes the battle spawn-path selector, and only then do
  behaviors enter `AfterStart`. `MissionBoundaryPlacer.EarlyStart` supplies the
  `walk_area` boundary used by `GetPatchSceneEncounterPosition`; without that
  boundary the patch-to-scene conversion is invalid and the authored spawn path
  cannot be selected.
- The lightweight sparring mission omitted `MissionBoundaryPlacer` and labelled
  its `MissionCombatantsLogic` as `NoTeamAI`. Consequently
  `Mission.HasSpawnPath` was false and `Mission.IsFieldBattle` was false. The
  deployment logic fell back to the fixed `battle_set` markers instead of the
  scene's authored `spawn_path_01` through `spawn_path_05`. In the tested
  `battle_terrain_L` data, the attacker and defender fixed markers are only
  about `4.4 m` apart, while an authored battle path is hundreds of metres
  long. This exactly accounts for both armies spawning together.
- The mission behavior chain now includes `MissionBoundaryPlacer` and identifies
  itself as `MissionTeamAITypeEnum.FieldBattle`, matching the stock field-battle
  contract. `BattleSpawnLogic("battle_set")`, the defender/attacker combatant
  order, `CustomBattleMissionSpawnHandler`, safe party-backed agent origins, and
  all existing no-casualty controls remain unchanged. Once agents exist, the
  sparring controller continues to disable formation AI, hold fire, clear
  targets, make non-duellists invulnerable, and issue the ceremonial march and
  line orders. No real campaign `MapEvent` or persistent faction war is needed
  to obtain the native opposite-side spawn path.
- The user explicitly declined an abnormal-state Tab safety exit. No such
  fallback was added: Tab remains available only after the duel result and
  native victory reaction, as before. The root deployment contract was repaired
  instead.
- The first Release compilation after the code correction completed with `0`
  errors and the existing `44` nullable-analysis warnings. Because this
  project's build targets always mirror module data and binaries, that build
  also deployed the corrected DLL to both live binary directories. The final
  documentation-synchronized build and source/live hash validation are recorded
  below after completion.
- The documentation-synchronized incremental Release build then completed with
  `0` errors and `0` incremental warnings. Decompilation of the deployed client
  DLL confirms `MissionBoundaryPlacer`, mission team-AI enum value `1` (verified
  against the installed 1.4.7 enum as `FieldBattle`), the native-spawn validity
  check, and the unchanged result-gated `OnEndMissionRequest`. It also confirms
  the stock `AgentVictoryLogic` reaction timer and contains no abnormal-state
  Tab bypass.
- Final source/live validation reports all `24` deployable files present and
  hash-identical, all `17` XML files parseable, and all `13`
  `gwp_sparring*` Chinese localization ids present. Both deployed runtime DLLs
  have SHA-256
  `F0BE2F6E48FB66B0C23FCA6DD3B078C2821F342FCAD7265B73314E5491BEA894`.
  Repository/live README hashes match at
  `8DF3E8A030CE6A5244F7F61C374AE7CF976F3D367A13E762644C936B16475FB9`
  for Chinese and
  `7D7F4AE6338E9F15E308C7DF480975D44050CAD3490BCF819EEF857BE776E77E`
  for English. The live normal-client module contains no editor tree, ZIP,
  checksum, or other archive file. No archive, Git commit, push, tag, or GitHub
  Release was created in this repair task. In-game confirmation of opposite-side
  native deployment and the complete march-to-duel flow remains the next test.

# 2026-07-18 lone-player field-sparring staging repair

- The first test of the corrected opposite-side native deployment used a lone
  player party against an opponent with troops. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_6132.txt`, the
  native deployment check at line `3525` reports valid field spawn data,
  player centre `(302.4,856.3)`, opponent centre `(500.0,574.0)`, and `344.5 m`
  separation. Native spawning completed with `236` agents and only `3`
  formations, all belonging to the opponent. The ranks completed their march
  at line `3551`, but the log never reached `opponent reached the field centre`
  before the session ended. There was no exception or engine error; the state
  machine remained in `OpponentAdvancing`.
- The lone-player case was previously launchable but not modelled explicitly.
  `CalculateRankFrontGap` returned the desired `25 m` whenever either side had
  no non-duellist rank, so the displayed gap was synthetic. The controller now
  treats a one-sided formation set as valid without inventing a second rank,
  skips bilateral gap correction, and places the meeting point in front of the
  actual settled opponent line. If neither side has spectators, it advances
  directly from the two duelists' midpoint.
- The post-march hold order was the cause of the visible retreat and turn. It
  repeatedly issued `MovementOrderMove(formation.CachedMedianPosition)` after
  the challenged lord had left its formation; that median is recalculated as
  formation membership and slots change and therefore was not a fixed hold
  point. The replacement snapshots every non-duellist's actual `WorldPosition`
  at the instant ranks are accepted, removes formation control, and maintains
  an individual no-attack scripted frame facing the duel ground. Original team
  and formation references are retained for winner-only restoration and the
  existing native victory reaction.
- Opponent arrival no longer requires an exact `1 m` target match plus a
  simultaneously settled velocity. Arrival within the ceremonial tolerance,
  or a stopped position near the navigation-clamped destination, is accepted;
  a bounded navigation timeout accepts the nearest reached point instead of
  leaving the mission in `OpponentAdvancing` forever. Acceptance snapshots the
  lord's actual position, fixes movement there, and continues refreshing only
  its facing toward the player. The existing player-within-`3 m` conversation
  trigger, duel-result-gated Tab rule, spectator isolation, no-casualty origin,
  and victory sequence remain unchanged.
- The first Release build after the state-machine correction completed with
  `0` errors and the repository's existing `44` nullable-analysis warnings.
  The build deployed the new DLL and the then-current module data to both live
  binary targets. Final documentation-synchronized build, source/live hashes,
  XML validation, and deployed-assembly checks follow below.
- The documentation-synchronized incremental Release build completed with `0`
  errors and `0` incremental warnings. All `24` deployable `_Module` source
  files are present and hash-identical in the live normal-client module; all
  `17` deployable XML files parse and all `13` Chinese `gwp_sparring*` ids are
  present. Client and editor DLLs match at SHA-256
  `1DDAF123F3E5A7E607BC82D6818EF3946442F0B57F36AC1D15E2693364545630`.
  Repository/live README hashes match at
  `CC73CAA1389D3041C06FE0671C254EF5EFBCE93AD86AB0C8F71548BCE463FDA7`
  for Chinese and
  `A55EFD650EA4F53685806262ACBDFB610CE13C109C8064C84F45E3AF862AFFF0`
  for English. The live client module contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum entry.
- Decompilation of the deployed client assembly confirms the explicit
  single-sided formation path, `float.NaN` instead of a synthetic front gap,
  fixed spectator-frame methods, zero movement limits, bounded opponent
  advance, and fixed opponent hold. The former `HoldFinalRanks` method and its
  moving `CachedMedianPosition` hold order are absent. No archive, Git commit,
  push, tag, or GitHub Release was created. In-game confirmation of fixed ranks,
  opponent lock, centre conversation, duel, cheer, and result-gated Tab exit is
  the next test.

# 2026-07-18 field-sparring interaction and post-bout conversation follow-up

- The next in-game test confirmed the lone-player repair: native deployment,
  the march, fixed spectator ranks, opponent detachment, and the centre-field
  transition all worked. The remaining presentation issue was the opponent's
  deliberately capped `0.65` walking speed, which made the short ceremonial
  advance look like a slow shuffle. The cap is now `1.8` while the existing
  scripted no-attack/no-run route, navigation destination, arrival lock, and
  timeout remain unchanged.
- The user rejected the automatic proximity conversation. The installed 1.4.7
  `SandBox.Conversation.MissionLogics.MissionConversationLogic` was decompiled
  as the authority: its `IsThereAgentAction` supplies the native interaction
  action between `0.2` and `2 m`, and `OnAgentInteraction` calls
  `StartConversation`. The controller now enables that action only while the
  player is within the opponent's small interaction area. Its own earlier
  proximity call to `StartConversation` is removed; the controller records the
  player/opponent interaction before the native logic runs, preserving the
  custom centre-dialogue condition and the existing conversation-end duel
  callback. Frozen ranks remain non-interactable because the conversation
  behavior stays globally disabled whenever the player is outside the central
  opponent's enable radius.
- A field result now queues a separate map conversation with the challenged
  hero. `GreyWardenSparringBehavior.OnApplicationTick` waits until the sparring
  mission is closed, the campaign has returned to `MapState`, no encounter,
  map event, mission, or other conversation is active, then calls the native
  `CampaignMapConversation.OpenConversation` with no bodyguards. High-priority
  result lines provide different encouragement after a win and a loss, followed
  by one player acknowledgement; the pending state clears on the native
  one-shot conversation-end callback. The field mission's cheer timing and
  result-gated Tab behavior are otherwise unchanged.
- The first Release compile/deploy after these changes completed with `0`
  errors and the existing `44` nullable-analysis warnings. Final synchronized
  documentation build, hashes, XML/localization counts, and deployed-assembly
  inspection follow below.
- The documentation-synchronized incremental Release build completed with `0`
  errors and `0` incremental warnings. All `24` deployable files are present
  and hash-identical in the live client module; all `17` XML files parse and the
  Chinese table now contains all `16` `gwp_sparring*` ids. Client and editor
  DLLs match at SHA-256
  `248DC7B9A55662261E01F8FF96F19942705382903E9C0E7A623E36F9CA68F2B9`.
  Repository/live README hashes match at
  `A59B8F7F8818B6C64EF3D6AFEB2A344FD87E2E822E3D269631D91CD0A807F8A3`
  for Chinese and
  `FE86BE0AD28A204C663A55AB59689857A8F97CC27BA6D332D2F04CC1B7CF4279`
  for English. The live client module contains no editor tree, runtime cache,
  ZIP, or checksum entry.
- Decompilation of the deployed client DLL confirms the `1.8` opponent speed,
  controller `OnAgentInteraction`, distance-gated native interaction enable,
  queued result state, both result dialogue ids, native
  `CampaignMapConversation.OpenConversation`, and the one-shot cleanup. The
  field controller contains no direct `MissionConversationLogic.StartConversation`
  call. No archive, commit, push, tag, or GitHub Release was created. The next
  test should verify the faster advance, interaction-key prompt and centre
  exchange, then both win/loss post-bout responses after returning to the map.

# 2026-07-18 field-sparring interaction-mode and mounted-facing correction

- The next test disproved two assumptions in the preceding implementation. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_35932.txt`, ranks
  freeze at line `3517`, the opponent begins advancing at line `3518`, and does
  not lock until line `3519`, about `22.5` seconds later. Line `3522` proves the
  controller received the player's agent interaction, but no conversation UI
  followed. The user also observed no interaction prompt and continuous mounted
  rotation as the opponent tracked the player's changing position. The earlier
  `rgl_log_28612.txt` is a second speed failure: its opponent still had `20.5 m`
  remaining when the `30`-second navigation timeout fired.
- Merely enabling `MissionConversationLogic` was the failed interaction
  approach. Its installed `IsThereAgentAction` rejects several mission modes;
  this lightweight field-battle mission is outside the stock free-roam action
  contract even though the controller itself receives an interaction event.
  The controller now overrides `IsThereAgentAction` for exactly one pair—the
  main agent and challenged lord—within the central interaction radius. Its
  `OnAgentInteraction` then directly calls the native
  `MissionConversationLogic.StartConversation`. The stock behavior remains
  globally disabled so frozen spectators cannot expose talk actions. Because
  the launch is still reached only through the engine's agent-action input,
  proximity alone cannot open the dialogue.
- The `1.8` speed cap and `DoNotRun` flag were both insufficient. Opponent
  advance now permits a `4.5` maximum and removes `DoNotRun`, while retaining
  the navigation-clamped scripted destination and no-attack flag. Both travel
  and final hold use the fixed forward direction `-_battleAxis`; all per-tick
  direction-to-player calculation and `SetLookAgent(player)` calls were removed.
  On arrival the actual position is still snapshotted, speed is set to zero,
  and the same fixed forward rotation is reapplied, preventing the rider and
  mount from circling as the player moves around them.
- The first Release compile/deploy after this correction completed with `0`
  errors and the existing `44` nullable-analysis warnings. Final synchronized
  documentation build, hashes, and deployed-assembly inspection follow below.
- The documentation-synchronized incremental Release build completed with `0`
  errors and `0` incremental warnings. All `24` deployable files are present
  and hash-identical live, all `17` XML files parse, and all `16` Chinese
  `gwp_sparring*` ids remain present. Client and editor DLLs match at SHA-256
  `496B56F3F8B5E01C7EAFD57704FF083B291639C042450C9FD5B696EA4590F78A`.
  Repository/live README hashes match at
  `C65A8FB7D842E421897100B62311D71F94F4C1E61D76F5C3A453A93444DF66D7`
  for Chinese and
  `1DE0F6784632CC3A4C1993C0C1E8FFCA49AA518D17C235E85AC56F8687A2EAC9`
  for English. The normal-client module contains no editor tree, runtime cache,
  ZIP, or checksum entry.
- Decompilation of the deployed DLL confirms the `4.5` rider/mount caps, direct
  controller `IsThereAgentAction`, the interaction-event
  `MissionConversationLogic.StartConversation` call, and fixed `-_battleAxis`
  directions in both advance and hold. The advance scripted flags decompile to
  value `6` (`NoAttack | ConsiderRotation`) with no `DoNotRun`; both movement
  methods clear `SetLookAgent`, and the old player-direction helper and facing
  refresher are absent. No archive, commit, push, tag, or GitHub Release was
  created. The next in-game test should confirm a visible action prompt, an
  actual conversation after the input, fast stable advance, and no mounted
  tracking rotation.

# 2026-07-18 field-sparring missing-go-to flag repair

- The immediate retest in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_33996.txt` proved
  the opponent did not move at all: it begins advancing at line `3515`, reaches
  the `30`-second timeout at line `3516` with the full original `35.0 m`
  remaining, and is incorrectly locked in place at the army line. Because the
  mission then reported `AwaitingApproach` at the wrong location, the user's
  expected central interaction was unavailable as well. There was no exception
  or engine error.
- Decompilation of the installed `Agent.AIScriptedFrameFlags` identified the
  omitted contract: `GoToPosition=1`, `NoAttack=2`, `ConsiderRotation=4`,
  `NeverSlowDown=8`, and `DoNotRun=16`. The failed advance deployed only value
  `6` (`NoAttack | ConsiderRotation`), so it supplied a destination frame but no
  command to travel to it. The corrected advance uses value `15`, explicitly
  combining `GoToPosition | NoAttack | ConsiderRotation | NeverSlowDown`, while
  retaining the `4.5` rider/mount caps and fixed `-_battleAxis` rotation.
- The timeout is reduced to `15` seconds. It no longer accepts and locks the
  opponent at an unreached army-line position: if navigation still fails, the
  public `Agent.TeleportToPosition` fallback moves both rider and mount to the
  already navigation-clamped meeting point before the normal fixed hold and
  interaction phase. `TownMerchant` was also re-audited in the installed
  `SandBox.View.dll`; its view set includes the mission agent-status UI handler
  required to render agent actions. A one-shot `centre interaction action is
  available` log now records when the player is actually within the controller's
  action range, separating movement/distance failures from input/UI failures.
- The Release compile/deploy completed with `0` errors and the existing `44`
  nullable-analysis warnings. All `24` deployable files are hash-identical live,
  all `17` XML files parse, and all `16` Chinese sparring ids remain present.
  Client and editor DLLs match at SHA-256
  `B9E764EAC0A3A5D89FDBC0B30E97F2684C8B71729520305AD45324DF6043431D`.
  Repository/live README hashes remain
  `C65A8FB7D842E421897100B62311D71F94F4C1E61D76F5C3A453A93444DF66D7`
  and
  `1DE0F6784632CC3A4C1993C0C1E8FFCA49AA518D17C235E85AC56F8687A2EAC9`.
  Decompilation of the deployed DLL confirms scripted flag value `15`, direct
  interaction action and `StartConversation`, the shortened timeout, forced
  navigation-point fallback, and availability diagnostic. No archive, commit,
  push, tag, or GitHub Release was created; in-game confirmation is next.

# 2026-07-18 native advance, visible interaction text, and plain-language repair

- The next test in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_34760.txt`
  confirmed the deployed flag-`15` advance still did not move. The opponent
  starts at line `3518`, reaches the `15`-second timeout at line `3519`, and is
  visibly teleported by the fallback. Lines `3523` and `3531` prove the custom
  F interaction action and direct conversation launch worked, while lines
  `3538`, `3540`, and `3562` prove result queuing, victory resolution, and the
  post-bout map conversation worked. The remaining interaction defect was
  presentation: no primary/secondary interaction text was supplied to the UI,
  so F functioned without a visible prompt.
- `HideoutCinematicController` was decompiled again for the exact stock movement
  contract rather than inferring flag semantics. Its move phase clears prior
  staging separately and issues `SetScriptedPositionAndDirection` once with
  `addHumanLikeDelay=true` and `AIScriptedFrameFlags.None`; it does not use
  `GoToPosition`, `NeverSlowDown`, or repeated per-tick order replacement. The
  field controller now mirrors that sequence: the challenged lord is excluded
  from spectator freezing, detached from formation, has rider and mount
  scripted movement disabled once, has both speed caps reset to `-1`, and
  receives one stock-style scripted destination with a fixed forward rotation.
  Tick code only observes progress. The teleport fallback is removed; the
  timeout now logs remaining distance without pretending the lord arrived.
- `TownMerchant` already provides the Gauntlet agent-status view, but
  `AgentInteractionInterfaceVM` obtains its visible name, key, and action label
  exclusively through `Mission.FocusableObjectInformationProvider`. The
  lightweight mission had no callback for active conversational agents. The
  controller now registers a provider callback in `AfterStart` and, only for
  the challenged lord during `AwaitingApproach`, supplies the hero name plus
  the engine's current `CombatHotKeyCategory` action key and a localized
  `Talk/交谈` label. The existing custom F action and direct conversation call
  remain unchanged.
- All `gwp_sparring*` Chinese strings were rewritten in straightforward modern
  language; the English fallbacks were simplified at the same time. The Grey
  Wardens' ancient organizational origin no longer causes individual dialogue,
  result messages, or instructions to use archaic speech. The new interaction
  label raises the Chinese sparring-id count from `16` to `17`.
- The first Release compile/deploy after these changes completed with `0`
  errors and the existing `44` nullable-analysis warnings. Final synchronized
  documentation build, hashes, XML validation, and deployed-assembly inspection
  follow below.
- The documentation-synchronized incremental Release build completed with `0`
  errors and `0` incremental warnings. All `24` deployable files are present
  and hash-identical live, all `17` XML files parse, and all `17` Chinese
  `gwp_sparring*` ids are present. Client and editor DLLs match at SHA-256
  `FE9E5D858CB7FF20C2DD4D7BB748EE6E2508EA2708AA4BFAB2DB7FAF0A0A5839`.
  Repository/live README hashes match at
  `55DFE82B66F3145A90DC868BC84B6A9C6D3606AE9737E59E15ECBC8E650F6A27`
  for Chinese and
  `D13DE0EBF8950468CC1EFB0E3B4FD94774A1791F66D06B4554268B4369AF6E81`
  for English. The normal-client module contains no editor tree, runtime cache,
  ZIP, or checksum entry.
- Decompilation of the deployed DLL confirms the focusable-information callback,
  current combat-hotkey lookup, localized talk label, one-shot opponent movement
  order, rider/mount `-1` caps, `addHumanLikeDelay=true`, and scripted flag value
  `0`. `TeleportToPosition`, `GoToPosition`, and `NeverSlowDown` are absent from
  the deployed field controller. No archive, commit, push, tag, or GitHub
  Release was created. The next test should confirm natural physical advance,
  the visible name/key/talk prompt, and plain-language dialogue.

# 2026-07-18 field-sparring hold and native duel-boundary correction

- The next in-game test in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_42856.txt`
  confirmed the physical advance, visible interaction action, centre
  conversation, loss resolution, and post-bout map response. The controller
  recorded the opponent arriving with only `0.6 m` remaining. The user
  observed two remaining runtime defects: the mounted opponent continued to
  creep forward after that lock, and native cavalry combat AI could carry the
  opponent through the frozen spectator line.
- The old hold repeatedly sent a rider-only
  `SetScriptedPositionAndDirection` frame while merely capping both rider and
  mount speed. That left the mount's own position and residual travel state
  unsnapshotted. The corrected arrival path snapshots rider and mount world
  positions separately, clears their travel scripts once, applies zero speed
  limits to both, and gives each a position-only hold frame. Per-tick waiting
  code now maintains the caps and fixed look direction without continually
  issuing a new travel-and-rotation destination. Starting the bout explicitly
  clears the mount's hold script and look lock before restoring unrestricted
  speed.
- The settled front-line target is increased from `25 m` to `100 m`, with an
  accepted stable range of `90-110 m`. When the centre conversation starts the
  normal scene `walk_area` still remains unchanged. Only when the player begins
  the bout does the controller replace that boundary with a `100 m`-wide
  rectangle centred on the meeting point; its longitudinal edges stop `4 m`
  short of the actual opposing rank fronts. This uses Bannerlord's native
  mission-boundary input to combat navigation so mounted AI turns back into
  the open ground instead of receiving a repeated scripted correction.
- The redundant approach quick message was removed because the focusable-agent
  provider already supplies the native lord-name, interaction-key, and
  `Talk/交谈` prompt. Field win/loss quick messages no longer instruct the
  player to press Tab; they now report only the result. The unused
  `gwp_sparring_approach` localization entry was removed.
- The Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. All `24` deployable files are present and
  SHA-256-identical in the live module, all `17` XML files parse, and the
  Chinese table contains `16` unique `gwp_sparring*` ids with no remaining
  approach id. Client and editor DLLs match at SHA-256
  `BB38DC0A50BE7F36BF96E10A27A97F702DD2A881B4D202B6FD0E2A4BF9A650D1`.
  Repository/live README hashes match at
  `5F9B3843C02667FA816777959311C7A4FDD2CEE0589294210F82BB7443DF8C08`
  for Chinese and
  `1F2DD178249452BA50C26E3BC235C60BE93DD65243D3AC801890127910663E82`
  for English. The live client module contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum entry.
- Decompilation of the deployed client DLL confirms the `100/90/110 m` rank
  constants, separate rider and mount position-only hold calls (flag value
  `18`, `NoAttack | DoNotRun`), mount hold cleanup at duel start, and the
  remove/add replacement of `walk_area` immediately before combat. The
  deployed controller contains no `gwp_sparring_approach` reference, and its
  result fallbacks contain no Tab instruction. No archive, commit, push, tag,
  or GitHub Release was created. The next in-game test should cover mounted
  arrival stillness and a long cavalry exchange near both army lines.

# 2026-07-18 direct formation targets and selectable duel styles

- The next formation test with troops exposed a target-correction ordering
  defect. While both armies were still marching from native deployment, the
  controller compared their current front gap with the final `100 m` target
  every second and shifted both destination sets by up to `8 m`. Because the
  current gap naturally remained large during the approach, the destinations
  kept moving inward; after the ranks finally became too close, the same loop
  slowly moved those destinations outward again. Gap correction now runs only
  after every formation has reached its current destination and stopped.
  Initial formation-centre targets also use each formation's observed front
  offset from its cached median after the line-arrangement preparation period,
  rather than assuming the cached `Formation.Depth / 2` describes its final
  physical front. The first march should therefore end near the desired front
  gap without a second collective retreat.
- The centre conversation now asks for a duel style. Mounted combat retains
  both current mounts. Foot combat enters a peaceful preparation phase: the
  opponent repeatedly requests the engine's normal dismount action while held
  stationary, and combat begins only after both duelists are on foot. The
  player's deadline starts when the choice closes the conversation. Remaining
  mounted after `30 s` resolves the bout as a loss without applying synthetic
  damage; result state records a rule violation so the map conversation uses a
  dedicated rebuke rather than the ordinary consolation response.
- When mounted combat is selected by an unmounted player, the closest mounted
  enemy spectator is temporarily removed from the frozen-rank list and receives
  the same one-shot stock scripted travel used for the lord's physical advance.
  The courier rides to a navigation-clamped point beside the meeting centre,
  performs the normal dismount action, leaves the mount stationary, and gets a
  one-shot return order to the exact stored rank position. The courier rejoins
  the frozen spectator list after arriving on foot. Combat starts only after
  the courier has returned and the player has mounted the delivered horse.
- No custom loan-horse label or prompt is supplied. Decompilation of Bannerlord
  `1.4.7` confirmed `Mission.OnAgentInteraction` directly calls
  `Agent.Main.Mount(targetAgent)` and returns before mission behaviors whenever
  the target is a mount. The controller therefore only exposes an agent action
  for that one delivered horse; `MissionFocusableObjectInformationProvider`
  supplies the stock horse name and mount-key text, and the engine owns the
  actual mounting action. Its mission-local mount difficulty is lowered only
  when necessary to the player's current riding skill so the offered horse is
  genuinely usable. Other horses retain normal interaction and difficulty.
- The final Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. All `24` deployable files are present and
  SHA-256-identical in the live module; all `17` XML files parse; and the
  Chinese table contains `20` unique `gwp_sparring*` localization ids matching
  all `20` localized code references. The conversation-only state id
  `gwp_sparring_field_style` deliberately has no localization row because it
  is never rendered.
- Client and editor DLLs match at SHA-256
  `1A425E6D95638863FE9B8E58049FC30A6B2E5307C6C7941716F0DD94D91EEBC8`.
  Repository/live README hashes match at
  `45C2820D53AA2A807CB2D0CEF64E3251302A120941607EB9B7EFDB8F87CEB0B3`
  for Chinese and
  `2A3D2169CA6BEE5EDA33B6AA49CA90BDDF176942AA69F7082A873E39D5A02899`
  for English. The live module contains no editor tree, runtime cache, ZIP, or
  checksum entry.
- Decompilation of the deployed DLL confirms correction is gated by both
  formation-target arrival and stillness, the `30 s` foot deadline and direct
  rule-violation resolution, the mounted-loan delivery/dismount/return states,
  the loan-horse-only `1.75 m` action gate, mission-local difficulty adjustment,
  and the horse team transfer only after the player mounts. The deployed
  campaign behavior contains both style choices and the separate violation
  result conversation. No archive, commit, push, tag, or GitHub Release was
  created. In-game validation should cover a small two-sided formation, both
  foot-rule outcomes, and mounted selection by an unmounted player.

# 2026-07-18 mounted-loan courier arrival tolerance repair

- The first unmounted-player test reached the mounted-loan branch correctly:
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_38836.txt`
  records normal formation, lord advance, interaction, and the enemy cavalry
  courier beginning delivery at line `3585`. No later loan-state diagnostic or
  exception appeared during the remaining test time. Visually the courier rode
  up and stopped but never dismounted, leaving the mission stuck in
  `MountedLoanStage.Delivering` rather than proving an animation failure in
  `Dismounting`.
- Delivery previously required the mounted courier's rider position to come
  within exactly `2 m` of the navigation-clamped point. Normal agent and mount
  avoidance can stop a horse slightly farther away when the player and opposing
  lord already occupy the centre. Delivery now also accepts a courier that is
  fully stopped within `6 m`, matching the proven stopped-near principle used
  by the lord advance. At that transition the actual stop position is
  snapshotted, rider and mount travel scripts are both disabled, both speed caps
  are set to zero, combat targets are cleared, and the normal dismount request
  is issued immediately. A new one-shot diagnostic records the accepted
  distance before the existing repeated dismount requests take over.
- The Release build completed with `0` errors and the existing `44` warnings.
  All `24` deployable files are hash-identical in the live module, all `17` XML
  files parse, and the normal-client module still contains no editor tree,
  runtime cache, ZIP, or checksum entry. Client and editor DLLs match at
  SHA-256
  `E4D64178BD63EEFEA7EBC6E1A358B103668A67FD03872DBCC37266794DF01684`.
  Repository/live README hashes match at
  `7B90A4E6730D7E5A7E459E18778E71FF03497C17B8785A7A1B93898C2BDE375C`
  for Chinese and
  `23576D69BA5F11ADAFC579D01FC3BBEE68CA0BD7F362BBD9B98CEFA12AC19E53`
  for English.
- Decompilation of the deployed DLL confirms the `6 m` stopped-near constant,
  rider and mount scripted-movement cleanup, immediate normal `Mount` toggle
  used as the dismount request, repeated fallback requests, and the new accepted
  distance diagnostic. No archive, commit, push, tag, or GitHub Release was
  created. The next test should repeat the exact unmounted-player mounted-style
  path and verify courier dismount, original horse interaction, courier return,
  and combat start.

# 2026-07-18 one-agent dismount formation repair

- The immediate retest in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_6840.txt`
  disproved the remaining distance theory. Line `3608` records that the courier
  was accepted at exactly `2.0 m`, had both travel scripts cleared, and received
  the attempted `Agent.Mount(mount)` toggle. The courier still remained mounted
  through the end of the test. This proves the player-interaction toggle is not
  a reliable command channel for an AI-controlled rider even after navigation
  and velocity conditions are satisfied.
- Installed `1.4.7` decompilation identified the actual AI contract.
  `Formation.SetRidingOrder(RidingOrderDismount)` stores the formation order and
  applies `Agent.SetRidingOrder(Dismount)` to each member; the AI consumes that
  riding order rather than the transient player event-control flag set by
  `Agent.Mount`. The courier is now assigned at the delivery point to an unused
  enemy formation containing exactly that one rider. Only this temporary
  formation receives stop, hold-fire, fixed-facing, and dismount orders. Once
  the rider is on foot, the temporary formation returns to `RidingOrderFree`,
  the courier detaches, runs back under the existing one-shot script, and is
  restored to the exact original formation before being frozen at the saved
  rank position. Other spectator formations never receive a riding order.
- The same one-agent formation path now drives the challenged lord's automatic
  dismount for foot bouts, preventing the identical detached-AI failure there.
  The player's foot-style line was shortened from the rule-exposing
  `I'll dismount within 30 seconds` to simply `Let's fight on foot/步战`; the
  unchanged `30 s` forfeit rule remains internal and discoverable through play.
- The Release build completed with `0` errors and the existing `44` warnings.
  All `24` deployable files are hash-identical live, all `17` XML files parse,
  and the live module contains no editor tree, runtime cache, ZIP, or checksum
  entry. Client and editor DLLs match at SHA-256
  `31646420F3886A28084A8F6D50C8BC3E7EA90321390ACB256AAE5C2E868F4F0E`.
  Repository/live README hashes match at
  `3E3FC3081446443244B89C1691ECD17750CAEA10945B258EA414661897EB850A`
  for Chinese and
  `5C851D12D598DF039C3469B7827A8E30892C66EADE5F2E5AC228FFFC05924585`
  for English.
- Decompilation of the deployed controller confirms selection of an empty enemy
  formation, the one-agent assignment, formation and agent dismount orders,
  release to free riding, and restoration of the original courier formation.
  Deployed dialogue contains only the short foot-style choice. No archive,
  commit, push, tag, or GitHub Release was created. The next test should verify
  the temporary-formation diagnostic, physical dismount, courier return, horse
  interaction, and combat start without any other spectator dismounting.

# 2026-07-18 immediate lord dismount and independent foot-rule timer

- The next foot-bout test in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_16212.txt`
  reached the temporary-formation branch: line `3705` records creation of the
  one-agent formation and line `3706` records the foot selection, but the
  opposing lord remained mounted and combat never began. This disproves the
  formation riding order as a reliable way to dismount the challenged lord in
  his stationary centre-field state. It does **not** disprove the courier's
  one-agent formation path: that rider reaches the delivery point through a
  different scripted-movement state, and the revised courier path has not yet
  received its requested in-game test. The courier formation, dismount order,
  return movement, and original-formation restoration are therefore preserved
  unchanged for separate validation.
- Bannerlord `1.4.7` decompilation confirms that the public `Agent.Mount(mount)`
  call only raises the transient `EventControlFlag.Dismount`, while formation
  and agent riding orders remain AI requests. `Agent.MountAgent` instead has a
  private setter that calls native `IMBAgent.SetMountAgent`; native callbacks
  update the rider/mount caches, mount-without-rider registry, formation state,
  components, mission behaviors, and driven stats. Foot selection now invokes
  that setter for the opposing lord through one cached `MethodInfo`, verifies
  both `lord.MountAgent == null` and `mount.RiderAgent == null`, grounds the lord
  at the established meeting point, hides the now riderless duel mount, and
  starts combat immediately. A missing setter or failed native separation
  aborts with an explicit diagnostic instead of leaving preparation stuck.
- Player compliance no longer gates combat startup. The internal deadline is
  now `20 s` from foot-style selection and is checked during the live fight.
  The first observed player dismount permanently satisfies the check and hides
  the riderless player duel mount; remaining mounted through the deadline
  resolves the existing rule-violation loss and result rebuke. The dialogue
  remains the short `Foot combat/步战` choice and does not explain the timer.
- The Release build completed with `0` errors and the existing `44` nullable
  warnings. All `24` deployable files are SHA-256-identical in the live module,
  all `17` XML files parse, and the live normal-client module contains no
  `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum entry. Client
  and editor DLLs match at SHA-256
  `5EBCC4C445C001610DA9C72EE032B7C2C5730CED517637ED6E81001316FB0592`.
  Repository/live README hashes match at
  `0EFDBE3DDA1DA8546353D3E02FDC1D2D78D5F2499BFD4612B24AB2ABA204DE63`
  for Chinese and
  `4B2CB58B4FD31D798D156DD07FA48C5992320A94C8E4B1DAF453B21AF19519AF`
  for English.
- Decompilation of the deployed DLL confirms the `20 s` constant, cached
  private mount setter, immediate forced-opponent-dismount call, post-detach
  rider/mount validation, and absence of the old `waiting for both lords`
  branch. It also confirms zero opponent-lord calls and two courier calls to
  `ApplyTemporaryDismountOrder`, preserving the one-agent courier experiment.
  No archive, commit, push, tag, or GitHub Release was created. The next
  in-game test should select foot combat while both lords are mounted, verify
  that the opponent appears on foot and attacks immediately, then separately
  test player dismount before and after the `20 s` deadline. The mounted-loan
  courier remains a later, independent test.

# 2026-07-18 native animated dismount controller

- The combined retest in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_36036.txt`
  disproved both remaining implementations. In the first bout, line `4252`
  records the private-setter mount detachment and line `4258` immediately
  starts combat. In game the horse vanished, but the lord retained a mounted
  pose and repeatedly changed height/position while fighting. This proves
  `IMBAgent.SetMountAgent(-1)` updates the rider/mount relationship but does
  not itself execute the animation-state transition required for a built
  agent. Teleporting the rider afterward compounded the visible jitter. The
  private setter, reflection dependency, forced rider teleport, and all
  associated diagnostics have now been removed.
- The second bout in the same log independently tests the loan courier. Line
  `4551` starts the unmounted-player mounted branch; line `4557` creates the
  one-agent formation and line `4559` accepts the courier exactly `2.0 m` from
  the delivery point. The rider then remained mounted until the test ended.
  This formally disproves the temporary formation plus
  `Formation.SetRidingOrder(Dismount)`/`Agent.SetRidingOrder(Dismount)` as a
  sufficient physical-dismount trigger in this lightweight field mission.
  Keeping the courier in an isolated formation is still useful for ownership
  and formation safety, but the riding order is now only a supporting order,
  not the mechanism expected to complete the action.
- Installed `1.4.7` code was re-audited before the replacement. The public
  `Agent.Mount(currentMount)` path sets `Agent.EventControlFlag.Dismount`; the
  native action system then selects and runs the correct dismount animation,
  and only animation completion invokes `Agent.OnDismount`. That callback is
  what notifies the formation, every agent component, mission behaviors,
  mounted-state listeners, driven-stat recalculation, both rider/mount caches,
  and the mission's free-mount registry. The public `RidingOrder` API only
  passes an AI intention to native code. Official `Agent.Controller` handling
  also confirms that moving an AI human to `AgentControllerType.None` removes
  its `HumanAIComponent`, while restoring `AI` adds the component back. Local
  TaleWorlds documentation contains animation taxonomy but no higher-level
  public API for forcing an arbitrary AI rider to dismount. Web searches for
  current community examples found no `1.4.7` path more complete than these
  official engine contracts.
- Both challenged-lord and courier dismounts now use the same native animated
  state machine. At the stable stop point, scripted travel is disabled, rider
  and mount speeds are held at zero, the previous controller is stored, an AI
  rider is temporarily changed to `None`, and the stock
  `EventControlFlag.Dismount` is submitted immediately and on every preparation
  tick. The controller logs when `GetCurrentActionType(0)` first becomes the
  engine's `Dismount` action, then waits for `MountAgent == null`; it never
  changes the mount relationship itself. On the real `OnDismount` result, the
  prior controller and free riding order are restored. The lord proceeds to
  foot combat; the courier retains the one-agent formation until completion,
  then detaches and returns to his exact saved rank position on foot.
- Foot preparation deliberately no longer calls the centre hold routine while
  the lord is dismounting, preventing the old scripted frame lock from
  interrupting the native animation. The courier is already removed from the
  frozen spectator list during delivery. Each native dismount has a `10 s`
  diagnostic timeout; failure records the current action type, event flags,
  and controller and safely cancels the mission through the existing tick
  exception boundary instead of silently hanging. The player's independent
  `20 s` foot-rule timer and short dialogue remain unchanged.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical in
  the live module; all `17` XML files parse; and the live normal-client module
  contains no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum
  entry. Client and editor DLLs match at SHA-256
  `28D0555894E459C5D87CE09A51E4522127FE5ADB58980E4EED4B47073D94CEA2`.
  Repository/live README hashes match at
  `4A46D84F35E33E60FBBC886217B967E1A2E128CF2D370CA768873C3F0216B8AE`
  for Chinese and
  `E09CD4AB410687F8CB54B14761C6A11BEFE9FB0F858D96D12478CFF865B28FA0`
  for English.
- Decompilation of the deployed client DLL confirms both the internal `20 s`
  player grace period and `10 s` native-action timeout; it contains controller
  suspension to `None`, repeated stock dismount event submission, action-type
  detection, and controller restoration. It contains no reflection import,
  private `MountAgent` setter, direct mount detachment, or forced rider
  teleport. The courier still receives its isolated formation and supporting
  riding order without affecting any spectator formation. No archive, commit,
  push, tag, or GitHub Release was created. The next in-game test should first
  verify visible lord dismount animation and clean on-foot combat, then verify
  courier dismount, usable horse, foot return, and combat start. If either
  stalls, the new `entered the native dismount animation` line and the timeout
  diagnostic will distinguish event rejection from animation completion.

# 2026-07-18 preserve ordinary dismounted horses

- The successful native-animation retest exposed one remaining presentation
  defect: after either duelist dismounted for foot combat, the horse vanished.
  This was not an engine cleanup. The controller still called the old
  `HideFootDuelMount` helper after a confirmed dismount; that helper changed
  the loose horse to `Team.Invalid`, capped its speed, made it invulnerable,
  and called `FadeOut(true)`. The same helper ran when the player satisfied the
  foot rule, which explains why both the opposing lord's and player's horses
  disappeared consistently.
- Ordinary duel mounts now receive no custom cleanup. The player horse is not
  stored or modified after dismount at all. The opposing lord's horse only has
  the scripted movement and zero-speed staging restrictions removed because
  those restrictions were installed earlier to hold the mounted lord at the
  meeting point; its team, mortality, interaction, damage, AI, and subsequent
  battlefield behavior remain native. The helper no longer calls `SetTeam`,
  `SetMortalityState`, or `FadeOut`. The special mounted-loan horse remains
  separate and retains only the delivery-flow handling needed to make the
  offered horse usable by the player.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical in the
  live module, all `17` XML files parse, and the normal-client module contains
  no editor tree, runtime cache, ZIP, or checksum entry. Client and editor DLLs
  match at SHA-256
  `B04933C7ECBDE148E460B169DC573A516C50A44DA8C7543E743EFF268E1E34A0`.
  Repository/live README hashes match at
  `55C142AB400902C5B62DCD95111AF342FC8CB2038502BC957A162C369F0BD5AA`
  for Chinese and
  `5B62A75941ED35E98106195514F6F25A79546D7E74F2065B565A90B306DFD68B`
  for English.
- Decompilation of the deployed client DLL confirms that the ordinary-foot
  mount path now contains only `DisableScriptedMovement` and removal of the
  temporary speed cap for the opposing lord's loose horse. It contains no
  `FadeOut`, team change, or mortality change in that helper, and there is no
  stored player mount or player-horse cleanup call. Existing spectator and
  purpose-built loan-horse safety code remains intentionally separate. No
  archive, commit, push, tag, or GitHub Release was created.

# 2026-07-18 player excluded from alternative-attack fallback targeting

- Grey Warden kick and shield-bash actions use two distinct paths. A real
  native collision is observed by `OnAgentHit` and receives the existing
  damage-model knockdown decision. If the animation produces no native hit,
  `GwpAlternativeAttackControl.GetNearestEnemyTarget` selects one enemy within
  two metres and later registers the small synthetic control contact. The
  reported player knockdown without a visible hit came from this fallback
  target selection, not from the real collision path.
- The requested exception is implemented only at the point where the nearby
  fallback candidate is chosen: `candidate.IsMainAgent` candidates are skipped.
  No duplicate guard was retained in the delayed resolver or synthetic-contact
  helper after review, keeping this small rule at its single source of truth.
  Consequently the player cannot be selected for an off-target nearby fallback,
  while a genuine native kick or shield-bash collision against the player still
  reaches the unchanged damage model and can knock the player down normally.
  NPC fallback behavior and all existing probabilities remain unchanged.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical in the
  live module, all `17` XML files parse, and the normal-client module contains
  no editor tree, runtime cache, ZIP, or checksum entry. Client and editor DLLs
  match at SHA-256
  `7D53DC7E2540F8F992D609C600D4E0A255037907588BF2318F219EF38CB792C4`.
  Repository/live README hashes match at
  `A3C2A8CE7747812CC789872E2223424D3FE9606C24B672F76B64AB84E4490A4B`
  for Chinese and
  `E6BE00960848ECBEA9B9E9FC9DCA899898400DD31B92555C0248F0E96AC00069`
  for English.
- Decompilation of the deployed client DLL confirms exactly one
  `IsMainAgent` check in `GetNearestEnemyTarget` and no such check in
  `GwpAlternativeAttackControl.Apply`. No archive, commit, push, tag, or GitHub
  Release was created. In-game validation should place the player beside an NPC
  target during a missed Grey Warden alternative attack, then separately take
  a direct kick or shield-bash hit.

# 2026-07-18 duel arena lateral width adjustment

- Only the duel boundary's lateral half-width changed, from `50 m` to `100 m`.
  The resulting total cross-field width is `200 m`. The approximately `100 m`
  separation between the two army fronts, the dynamically calculated
  longitudinal boundary, and the `4 m` clearance before each spectator line
  are unchanged.
- The final deployed client decompilation confirms
  `DuelArenaHalfWidth = 100f`, `DesiredFrontGap = 100f`, and
  `DuelArenaLineClearance = 4f`. The Release build completed with `0` errors;
  all `24` deployable files match the live module and all `17` XML files parse.
  Client and editor DLLs match at SHA-256
  `BF791CC037419FF0B40AD83F8CBE03A085032849A387F49E78337381311B1B7E`.

# 2026-07-18 formal v1.4.7-r3 release

- The player release baseline is GitHub release/tag `v1.4.7-r2`, not the
  intermediate local development labels that were used while the field-duel
  work was being tested. Both player READMEs were rewritten as one concise
  `2026-07-18 v1.4.7-r3` entry describing only the differences a player sees
  after upgrading from `r2`: complete English/CNs support, troop and
  mission-local mastery rebalance, the rebuilt field-sparring mode, its wider
  mounted-combat ground, the player alternative-attack fallback exception, and
  the campaign UI/returning-patrol/village-defense text repairs. The former
  `r3/r4/r5/r6` step-by-step development history was removed from the shipped
  README and remains documented only in this maintenance history.
- The protected runtime asset packages remain byte-identical between repository
  and live module:
  - `gwp_inherited_legacy_assets.tpac`:
    `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`
  - `gwp_black_gold_shield.tpac`:
    `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B`
- The formal archive was rebuilt beside the live module at
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7.zip`
  with its checksum at the same path plus `.sha256`. The ZIP contains one
  top-level `GreyWarden` directory and `27` runtime files. It deliberately
  excludes `Assets`, `AssetSources`, `RuntimeDataCache`, editor binaries, PDBs,
  `shader_compile_report.log`, nested archives, and diagnostic content. Size:
  `349,302,387` bytes. SHA-256:
  `13B5B4BDED0563E24027B14808512B3500BFDB4E073268D88AECE567754D0FBF`.
- The archive was extracted twice: once in its unique package workspace before
  replacing the prior local archive, and again from the final Modules-directory
  path. Both checks found `27` extracted files, `0` missing/hash-mismatched
  files, and `0` extra files against the selected live runtime set. Final
  archive/live hashes match for the client DLL at
  `BF791CC037419FF0B40AD83F8CBE03A085032849A387F49E78337381311B1B7E`,
  Chinese README at
  `F34F0D011A93764F41DE4F53E4308565072F9EC939E459BB5824FBE2B32AC31B`,
  and English README at
  `F86D25C29DF39DB059E1CE21F1DF18909912D5C0DF2881EDFC4C26E3FE4273FE`.
- This entry is the rollback point for the formal source commit, `main` push,
  `v1.4.7-r3` tag, and GitHub Release. The release assets must be the exact ZIP
  and checksum recorded above; do not regenerate them after the source commit.

# 2026-07-18 native town-arena duel transition repair

- In-game testing of the town challenge found that accepting the bout ended in
  the ordinary arena walkabout beside the arena master, with no fight. The
  previous transition set `GameMenuManager.NextLocation` to the arena before
  ending the conversation mission and then attempted to call
  `CampaignMission.OpenArenaDuelMission` from `BeforeGameMenuOpened`. The
  location transition and the duel mission competed for the same state change;
  the ordinary location mission won.
- The original `SandBox.dll` was decompiled with the repository-local
  `.codex_tmp\ilspy\ilspycmd.exe`. `SandBox.SandBoxMissions` confirms that
  `OpenArenaDuelMission` directly opens mission type `ArenaDuelMission`, uses
  `ArenaDuelMissionController`, and spawns the player and named opponent from
  two different `sp_arena` frames. With `requireCivilianEquipment=false` and
  `spawnBothSidesWithHorse=false`, both combatants use their first battle
  equipment and no horse. `SandBox.View.dll` confirms the same mission type
  automatically loads `MissionAudienceHandler`, the native status/equipment
  views, cheer-bark handling, and campaign spectator view.
- Town challenges no longer set `NextLocation` or subscribe to the town-menu
  opening event. After the conversation mission closes and `MapState` is
  active, the campaign behavior opens the native duel mission directly. The
  stock duel controller remains responsible for teams, arena spawn markers,
  equipment, AI, defeat detection, audience, and cheers; no custom arena
  combat controller was added.
- The duel result now enters the existing post-bout conversation queue. The
  campaign behavior closes the finished native duel mission on the next
  application tick and then opens the same concise victory/defeat response
  conversation used by field sparring. This deliberately replaces the former
  one-line quick-information result.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module, all `17` XML files parse, and the normal-client module has
  no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum entry.
  Client and editor DLLs match at SHA-256
  `093FA5B6C765195AC4830BD9BF4265FA71F95F49DE1BD8910AF6080930E69A81`.
  Repository/live README hashes match at
  `242DC17C3B0944C5F6FE017E7F209214CCAB846065CD7D1D619B87C636CF83B4`
  for Chinese and
  `68D0F72AA1698634899CD28342C8475B58697C1F814E5FF9EEACBBAB4AEDA517`
  for English.
- Decompilation of the deployed client DLL confirms that the town launcher no
  longer contains a `NextLocation` transition and calls
  `OpenArenaDuelMission(scene, arena, opponent, false, false, callback, 100f)`.
  It also confirms that the callback queues the post-bout conversation and the
  application tick closes the completed mission. No archive, commit, push,
  tag, or GitHub Release was created. The next in-game test should verify the
  two separate arena spawns, complete personal equipment, absence of mounts,
  audience audio, and both victory and defeat return conversations.

# 2026-07-18 town-arena knockdown and return-conversation repair

- The first native-arena retest confirmed that both combatants and the fight
  now spawn correctly, but exposed two follow-up defects. The application tick
  ended the mission immediately when `ArenaDuelMissionController` reported a
  winner, cutting off the visible fall. The result was also sent through
  `CampaignMapConversation.OpenConversation`, which is the correct field-bout
  path but selects a world-map conversation scene and therefore moved a town
  challenge far away from its original presentation.
- Decompiled stock `ArenaDuelQuestTask.MissionTick` establishes the native
  timing: once either arena agent becomes inactive, it starts a
  `BasicMissionTimer` and ends the mission only after `4 s`. The town callback
  now follows the same delay before closing `ArenaDuelMission`, allowing the
  knockdown and audience response to finish visibly.
- At challenge acceptance, the behavior records the active conversation
  mission's `SceneName` and `SceneLevels`. After the delayed arena exit, town
  results now call the stock `CampaignMission.OpenConversationMission` with
  those recorded values and two no-horse, no-bodyguard, post-fight character
  records. This recreates the original town conversation presentation and lets
  the existing concise win/loss dialogue finish the entire flow. Field results
  retain their previous map-conversation route and horse behavior.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical live, all
  `17` XML files parse, and the normal-client module contains no editor tree,
  runtime cache, ZIP, or checksum entry. Client and editor DLLs match at
  `9E1F6DC02F0FA77D6323FAFFABB1B0B3DFABA16340F89C41F5B8002B883BA00E`.
  Repository/live README hashes match at
  `A6ABFE089D5C43C50448B451AA134B870DD8DC768C32408669F0E0663C864ABC`
  for Chinese and
  `3DEA5AC311B82C4B22D19AE94C1553E404D912A733BF26382A92770B623BDF1B`
  for English.
- Decompilation of the deployed client DLL confirms the `4 s` timer gate,
  capture of the original `SceneName` and `SceneLevels`, the town-only call to
  `OpenConversationMission`, and retention of
  `CampaignMapConversation.OpenConversation` for field results. No archive,
  commit, push, tag, or GitHub Release was created.

# 2026-07-18 town meeting background and longer result hold

- The next retest confirmed that the four-second hold works, but the player
  requested another five seconds. The total post-result arena hold is now
  `9 s` before the mission closes.
- The latest `rgl_log_29624.txt` proves why the result conversation still used
  an outdoor background. The queued diagnostic recorded `scene=` with an empty
  value. The challenge began from a menu/tableau lord conversation, not a live
  three-dimensional mission, so `Mission.Current` was null and there was no
  original `SceneName` to preserve. Passing the empty value to
  `OpenConversationMission` correctly triggered its stock map-scene fallback,
  which loaded `conversation_scene_forest`.
- Town results now select the stock `MeetingSceneData` whose culture matches
  the current settlement and pass its explicit `meeting_castle_*` scene ID to
  `OpenConversationMission`. These scenes are registered by the original
  `meeting_scenes.xml` and include the official meeting conversation spawn
  prefab. An empire meeting scene is retained only as a fallback if the game
  data contains no matching culture entry. The invalid attempt to remember a
  nonexistent menu-conversation mission scene was removed.
- The same log contains no managed exception or Grey Warden stack trace during
  shutdown. It records successful save, screen cleanup, `There are no living
  managed objects`, and normal managed-interface deletion. The only error log
  entries are repeated FMOD event-not-found messages without a call stack;
  these begin during general startup/audio loading rather than at the sparring
  shutdown. No code change was attributed to an unproven exit crash.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical live, all
  `17` XML files parse, and the normal-client module contains no editor tree,
  runtime cache, ZIP, or checksum entry. Client and editor DLLs match at
  `A706973F3661DD6F0FD2970B524088509E1B9C565E22C1EC6B70B932591E7542`.
  Repository/live README hashes match at
  `D91223270C82B38421285739AB89048E6895E2E0D69CA4C156DCEC0C71DFABA6`
  for Chinese and
  `42F2BC76D3173B60743FB40E5FB253BD00CF871D0C7360A9F6E01C82E12C2C8E`
  for English.
- Decompilation of the deployed client DLL confirms the `9 s` timer gate, the
  culture-string lookup over `GameSceneDataManager.Instance.MeetingScenes`,
  the stock empire meeting fallback, and the explicit town
  `OpenConversationMission` call. No archive, commit, push, tag, or GitHub
  Release was created.

# 2026-07-18 use the actual nearest lord-hall interior

- The generic culture-matched `meeting_castle_*` scene was rejected in favour
  of the actual settlement interior. Town-result scene selection now first
  uses `Settlement.CurrentSettlement` when it is a town or castle with a
  `lordshall` location. If mission-state transitions temporarily clear the
  current settlement, it compares the main party's current map position with
  every town and castle that has a lord hall and selects the nearest one.
- The selected settlement's `lordshall` location resolves its authored scene
  through `GetSceneName(settlement.Town.GetWallLevel())`. The explicit result
  passed to `OpenConversationMission` is therefore the real keep interior for
  that settlement and upgrade level, not an outdoor map conversation or a
  generic culture meeting scene.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical live, all
  `17` XML files parse, and the normal-client module contains no editor tree,
  runtime cache, ZIP, or checksum entry. Client and editor DLLs match at
  `CA3A4CA82F9D3AA1C80E5F40CBE6121CFDC2E7959539895BFC96BBAEA3CCF6AA`.
  Repository/live README hashes match at
  `7AEC0F333286599FEF32F824BAAC0EDB4EFED2EB6B6111A453837196D7D542CE`
  for Chinese and
  `59BDB1B596F992355D9754E33651EB264CF584DD9EB01296D24BD39F993BADE1`
  for English.
- Decompilation of the deployed DLL confirms current-settlement priority,
  squared-distance ordering over fortifications as the fallback, the explicit
  `lordshall` lookup, upgrade-level scene resolution, and retention of the
  `9 s` result hold. No archive, commit, push, tag, or GitHub Release was
  created.

# 2026-07-18 remove automatic town-arena exit

- The fixed post-result timer was removed at the player's request. The stock
  `ArenaDuelMissionController` already marks the duel as finished, allows the
  normal leave request, and displays the native leave-key prompt. The mod no
  longer calls `Mission.EndMission` after a town result or owns any town-duel
  exit timer.
- The result conversation is still queued as soon as the winner is known, but
  `TryOpenPostBoutConversation` naturally waits while the arena mission exists.
  The player may therefore watch the knockdown and crowd reaction for as long
  as desired, press Tab to leave through the original mission flow, and only
  then enter the selected lord-hall response conversation.

# 2026-07-18 trigger the native large arena cheer on the result

- The stock `MissionAudienceHandler` already supplies the arena spectators and
  ambient crowd, but its largest reaction normally waits for mission shutdown.
  That did not match the manual-Tab flow, where the player can remain in the
  arena after the duel has been decided.
- The town-duel result callback now plays the stock
  `event:/mission/ambient/detail/arena/cheer_big` one-shot at the scene's
  authored `arena_sound` entity as soon as either fighter wins. No lord victory
  animation was added, and the mod still leaves mission exit entirely to the
  player's Tab input.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module, all `17` XML files parse, and the normal-client module has
  no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum entry.
  Client and editor DLLs match at SHA-256
  `D4FF7C3904E2533DF5FDBE28982229874A11A867273FFB5912FF60FA135C8E19`.
  Repository/live README hashes match at
  `2B1AB72FC4866457E0DB7D9AF05009F21D879C432DE1D09DE59B2BA17B2CCE59`
  for Chinese and
  `BF856714EFACF5EB7BCEE0124B90C39607DFF5A5812C966B1CE2765DE2529CD1`
  for English.
- Decompilation of the deployed client DLL confirms that
  `OnTownBoutEnded` calls `PlayArenaVictoryCheer`, which resolves the authored
  `arena_sound` entity and starts the stock `cheer_big` event before queuing
  the result conversation. No town-duel mission-end call was reintroduced; the
  player still leaves with Tab. No archive, commit, push, tag, or GitHub
  Release was created.

# 2026-07-18 exit-crash investigation and leave-message flood repair

- The latest reported exit failure was investigated from
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_33532.txt`, its
  watchdog log, and Windows Application events. Both town arena bouts in that
  run completed their result callbacks, accepted Tab, opened the real lord-hall
  conversation, and finalized their arena scenes normally. The later crash
  occurred after a separate field sparring mission had opened with `433`
  agents and the game was closed while that bout was still unfinished.
- Windows recorded a native access violation in `TaleWorlds.Native.dll` at
  offset `0x74b3f1`, with no managed exception or Grey Warden stack. The same
  `0x74b1f0`, `0x74b34a`, and `0x74b3f1` native shutdown-offset family appears
  repeatedly in Application events from earlier dates and builds. A town-only
  run at 07:28 followed the same arena and lord-hall path and exited normally,
  so the evidence does not support attributing the native crash to the town
  result callback or the newly added crowd cheer.
- One concrete mod-side defect was visible immediately before the latest
  failure: holding Tab while the field bout was unfinished called
  `OnEndMissionRequest` every frame and generated hundreds of identical
  quick-information messages. The field controller now simply refuses the
  premature leave request without allocating and queueing another message on
  every frame. The rule that an unfinished field bout cannot be abandoned is
  unchanged.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module, all `17` XML files parse, and the normal-client module has
  no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum entry.
  Client and editor DLLs match at SHA-256
  `DEBFDE1AC44AA22822EF07F557574E87560436D8CE92777ADC4DC1D7D0E2699E`.
  Repository/live README hashes match at
  `8ABCF7D461A0899196D9173280FB41736AC50353BEFC3342A1745B2588FA35B9`
  for Chinese and
  `C4F6D0535A536B2F07696D1CC726672546DB35CC6166A2149F9004090D0F2480`
  for English.
- Decompilation of the deployed client DLL confirms that the field
  `OnEndMissionRequest` now only returns `_canLeave` and no longer calls
  `AddQuickInformation`. No archive, commit, push, tag, or GitHub Release was
  created. The next test should finish a town bout, leave with Tab, close the
  result dialogue, and exit the game before starting a field bout; a separate
  test should hold Tab during an unfinished field bout and verify that the
  message no longer floods the log.

# 2026-07-18 native encounter-region field-rank planning

- The `433`-agent valley test exposed a structural error in the original rank
  planner. It used the midpoint of the two spawned army centres as the duel
  centre and concatenated every active formation into one lateral line. Seven
  formations capped at `80 m` each could request more than `500 m` of width;
  individual navigation clamps then stranded formations at unrelated slopes or
  valley mouths. The readiness gate required every formation to reach and stop
  at its target with a `90-110 m` front gap, so a single unreachable target
  held the flow until the old `150 s` fallback.
- Decompiled stock `BattleSpawnPathSelector` confirms that the engine already
  predicts this battle's encounter region. For patch scenes it converts the
  campaign encounter coordinate through `GetPatchSceneEncounterPosition` and
  uses the encounter direction to select the initial spawn path. The sparring
  controller now uses that native encounter position as the preferred duel
  centre. Only missions without patch encounter data fall back to the centre
  of the current `walk_area` boundary resolved on the player-to-opponent battle
  axes.
- The preferred point is not accepted blindly. The controller searches outward
  in `8 m` rings up to `80 m`, testing candidates inside the current mission
  boundary and on navigation mesh. A candidate must provide reasonable paths
  from both native army sides to their lines and from both lines to the centre.
  It also measures continuous navigable width on both sides of the battle axis
  and accepts only a symmetric corridor wide enough for the ceremony.
- After the reachable centre is fixed, the two rank baselines are derived at
  `50 m` on either side. Each baseline's actual continuous width is measured.
  Formation widths are kept when space permits; when terrain is narrow, width
  is distributed by the square root of formation size and applied through the
  stock custom form order, increasing depth instead of sending ranks into
  walls. The duel boundary uses the same verified symmetric corridor rather
  than always claiming the full configured width.
- The selected native encounter centre remains fixed after the armies settle;
  the old recalculation from their actual front edges was removed because a
  stalled formation could drag the duel back toward an unsuitable choke point.
  Formation fallback was reduced from `150 s` to `45 s`. If the opposing lord
  still cannot finish the final approach within `15 s`, the controller locks
  him at the nearest position he actually reached and exposes the interaction
  instead of logging forever.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module, all `17` XML files parse, and the normal-client module has
  no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum entry.
  Client and editor DLLs match at SHA-256
  `E8F01E0365FF5F29639CE814B54E32547677345851F1E16E5220566E079F0B6F`.
  Repository/live README hashes match at
  `6FD82577F5FBE16D9DBE88C080B3902E7CEFE0B66745F3BB75F14383ABB346F0`
  for Chinese and
  `490D953E792AFB30A23DA6D00BAD78EB6D6F92B334F034749E54CFD6E3B51E6D`
  for English.
- Decompilation of the deployed client DLL confirms the native encounter-region
  lookup, boundary fallback, concentric reachable-centre search, two `50 m`
  rank offsets, terrain-width allocation through `FormOrderCustom`, adaptive
  duel width, the `45 s` formation fallback, and the `15 s` opposing-lord
  nearest-position fallback. No archive, commit, push, tag, or GitHub Release
  was created.

# 2026-07-18 shared-navigation-route field-rank correction

- The next valley test disproved the preceding interpretation of
  `Mission.GetPatchSceneEncounterPosition`. The engine only uses that patch
  coordinate to score authored spawn paths and select a pivot in
  `BattleSpawnPathSelector.FindBestInitialPath`; it is not a predicted field
  battle destination. The failing run logged native army centres at
  `(389.7,511.8)` and `(424.2,853.3)`, but the patch coordinate
  `(330.6,841.8)` caused the search to choose `(386.0,873.8)`, beside the
  attacker deployment rather than between the armies.
- That false centre provided only `24 m` of measured width. Seven active
  formations were compressed into roughly `16 m` after edge clearance, and
  the two fronts were still `275.3 m` apart when the `45 s` march fallback
  fired. The opposing lord then stopped behind an obstacle and the old
  `15 s` fallback exposed conversation while he remained `5.2 m` from the
  requested point. This confirms the player's visual report and rules out
  formation speed as the primary cause.
- Decompiled stock deployment code confirms that `DefaultDeploymentPlan`
  places both initial armies along the selected authored spawn path, while
  `NavigationPath` exposes the actual navmesh route between their deployed
  positions. The controller now builds that shared route directly with
  `Scene.GetPathBetweenAIFaces` and treats its arc-length midpoint as the
  preferred encounter region. It searches forward and backward only along
  that route, so a valley bend or obstacle changes the candidate route instead
  of leaving the candidate on an inaccessible straight line.
- Each candidate derives both rank centres at `50 m` of route distance from
  the meeting point and derives the battle axis from those two centres. The
  candidate is rejected unless both ranks, the meeting point, and every final
  formation centre are on navmesh and reachable from their actual formation
  origins. The measured symmetric corridor must remain usable at the player
  line, meeting point, and opponent line; the selector prefers the widest
  valid candidate near the route midpoint.
- Formation targets are no longer independently clamped toward an invalid
  destination. Once a complete layout passes validation, the exact validated
  positions are ordered as one layout. The later automatic gap correction was
  removed because it could move a validated line back into a wall. The
  opposing lord's meeting point is likewise required to have a real path from
  his settled rank; it is no longer replaced by a silently truncated point.
- The first Release compilation exposed only two uses of the modern `^1`
  index syntax, which is unavailable to this `net472` build; replacing them
  with explicit final indices produced a successful final build with `0`
  errors and the existing `44` nullable warnings. All `24` deployable source
  files are SHA-256-identical to the live module and all `17` XML files parse.
  The live normal-client module has no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum entry.
- Client and editor DLLs match at SHA-256
  `CE12F5CD22C755FA6E28137504BFDA2E3658801BAC8E53FDDB03340A689F2E5A`.
  Repository/live README hashes match at
  `424A18445804794C45FD1B88D6EA3634F9AF7DB14664AB25D5C8A632CE88BA76`
  for Chinese and
  `CF82640FDF145F5461CB67DE325D66A5244C463A03F6FE4C33BBB324E2565E3A`
  for English. Decompilation of the deployed client DLL confirms
  `GetPathBetweenAIFaces`, route-midpoint candidate search, explicit player
  and opponent rank centres, full-layout reachability checks, and the
  opposing-lord route requirement. It contains neither the old
  `GetPatchSceneEncounterPosition` centre lookup nor `CorrectFormationGap`.
  No archive, commit, push, tag, or GitHub Release was created.

# 2026-07-18 symmetric field advance and shared rank baseline

- The first shared-navigation-route test still selected an unacceptable
  candidate. The route between the native army centres was `351.8 m`, so its
  arc-length midpoint was `175.9 m`, but the width-first score selected offset
  `71.9 m` at `(408.6,581.2)`. This left the defender rank only about `21.9 m`
  from its start while the attacker rank had roughly `229.9 m` to advance.
  The player's report that the friendly army barely moved, the enemy advanced
  much farther, and the opposing lord stopped near the friendly line is
  therefore fully explained by the logged coordinates.
- Width may no longer outweigh symmetry. The selector now starts at the exact
  navigation-route midpoint and permits only a very small `8 m` correction
  when the midpoint itself cannot host the complete layout. The log records
  each side's requested advance distance and their imbalance explicitly. The
  two rank baselines remain `50 m` on either side of the selected centre,
  preserving the requested `100 m` fighting space.
- The enemy cavalry, infantry, and ranged formations were separated in depth
  because the former planner added a different forward offset derived from
  each formation's current depth and front edge. That compensation has been
  removed. Every formation on a side now receives a target on the same lateral
  baseline; only its left/right offset differs.
- Decompiled `LineFormation.GetLocalPositionOfUnit` confirms that a
  formation's `OrderPosition` is the centre of its front rank and later ranks
  extend backward along negative local Y. Consequently a reduced
  `FormOrderCustom` width naturally increases depth without moving cavalry or
  infantry away from the common front line. Candidate width requirements now
  use only the minimum front width needed for each active formation plus gaps
  and edge clearance; they no longer reject the balanced midpoint merely
  because the full preferred frontage does not fit.
- March readiness now estimates the moving front-rank anchor from the
  formation median plus half its current depth instead of reading the already
  assigned order coordinate. Per-formation logs record formation class, unit
  count, start, target, requested distance, assigned width, and any remaining
  distance at timeout. The march fallback was extended to `75 s` for the much
  longer but symmetric advances expected on this valley route.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module and all `17` XML files parse. The live normal-client module
  contains no editor-only directory, ZIP, or checksum entry. Client and editor
  DLLs match at SHA-256
  `185C8534DF632BCECA814225698E0E263C2BBF5B5BA3E82D0F631DEC7EBD8A0C`.
  Repository/live README hashes match at
  `E7645BEDDDAC2C5A41AC4E8E89074543197CDE8A2D00F8233540CB22F1836319`
  for Chinese and
  `4DC0E272EDD1167880E2A1502C2F0B500C7C3187CAB64E619F9BA013F8CAA3DE`
  for English.
- Decompilation of the deployed client DLL confirms the `8 m` maximum centre
  correction, `75 s` march timeout, player/opponent advance and imbalance
  logging, shared formation targets, reduced `FormOrderCustom` widths,
  front-anchor readiness checks, and minimum-only corridor-width validation.
  `GetFormationFrontOffset` is absent. No archive, commit, push, tag, or GitHub
  Release was created.

# 2026-07-18 fixed midpoint and no terrain-width cancellation

- The next test never reached formation movement. Both attempts on
  `battle_terrain_031` logged the correct `351.8 m` deployment route, then
  threw `no complete and sufficiently wide formation layout was found` during
  `NativeSpawning`. The user-facing terrain-cancellation message and immediate
  `EndMission` were therefore caused by the planner's remaining hard width
  gate, not by an unusable battle scene.
- The field layout is now deliberately independent of terrain width. Its
  centre is always the exact arc-length midpoint of the route between the two
  native deployments. The rank baselines are always taken exactly `50 m` of
  route distance on either side. There is no nearby-candidate search and no
  corridor-width acceptance test capable of changing or rejecting those
  points.
- Width measurement now has one purpose only: selecting each formation's
  `FormOrderCustom` frontage. All same-side formations keep the same front-rank
  baseline. If the measured frontage is narrow, each formation is compressed
  to its minimum front width and the stock line arrangement extends backward,
  away from the duel ground. Lateral target points are navigation-clamped
  locally and logged, but this cannot cancel the bout.
- A missing navmesh route now falls back to the direct native deployment axis
  instead of ending the mission. This fallback preserves the same midpoint
  and `50 m` rank-offset rule.
- No field-initialization exception is shown to the player. The obsolete
  `gwp_sparring_spawn_failed` localization entry and the quick-information call
  were removed completely; diagnostics remain in the engine log only.
- The quit after the forced cancellation produced a native access violation
  in `TaleWorlds.Native.dll` at offset `0x74b3f1`. The managed log completed
  mission teardown and reported no living managed objects. Windows recorded
  the identical native offset at `07:49` and `09:21`, so it is not a managed
  Grey Warden stack. The field controller nevertheless now explicitly removes
  its focusable-object interaction callback in `OnEndMission`, rather than
  relying solely on the provider's later finalization. More importantly, the
  terrain-width failure no longer drives the newly opened mission through an
  immediate exception-and-EndMission cleanup path.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical to the
  live module and all `17` XML files parse. Client and editor DLLs match at
  SHA-256
  `6B78B687781B278B4AC11A774E513CEA5171E68C0A1E0FAB30576C3CA46787FB`.
  Repository/live README hashes match at
  `1A337836FE5A72993E6FCED74E3E5D7F56A202FF8720F3ED6B876867C6AD7232`
  for Chinese and
  `7D86E688B5FDBA40CB243326AB4A3220625C322EFA621F7A97C3E2BFE9BB77C8`
  for English. A repository-wide search finds no remaining
  `gwp_sparring_spawn_failed`, terrain-cancellation text, or generic sparring
  exception prompt. No archive, commit, push, tag, or GitHub Release was
  created.

# 2026-07-18 map-author route centre and strict shared rank

- The user confirmed that the route-authored solution should apply to every
  field map. `Mission.GetInitialSpawnPath()` exposes the exact `spawn_path`
  selected by the stock battle setup for the current encounter. The field
  controller now reads its authored points instead of building a generic
  shortest navigation route between the two armies.
- Each native army centre is projected onto that authored polyline. The duel
  centre is the arc-length midpoint between the two projections, not the
  midpoint of the entire scene path. The two rank centres are sampled on that
  same path exactly `50 m` toward their respective armies, preserving the
  requested `100 m` duel space while giving both sides comparable travel.
  Reversed author paths are handled by reversing the sampled point list after
  projection. A scene without a valid initial author path falls back to the
  direct line between the native deployments and never cancels the bout or
  displays an exception message.
- This replaces the rejected `GetPathBetweenAIFaces` approach. That API found
  a reachable shortest route, but on `battle_terrain_031` its midpoint was on
  the valley side rather than the map author's low road. The selected
  `spawn_path_01` stays at roughly `z=6–12 m`; projection of the observed army
  centres placed its midpoint at approximately `(465.9,675.6,z6.6)`, matching
  the low, flat combat area and nearby stock tactical markers.
- Formation targets no longer run through an independent navigation clamp
  that can move cavalry, infantry, or ranged troops forward or backward by
  dozens of metres. Each side has one immutable front-rank centre and all of
  its formations differ only by lateral offset. If a requested lateral point
  is unavailable, the point is progressively compressed toward that same
  rank centre. `FormOrderCustom` narrows the frontage and the stock line
  formation adds ranks behind it; no terrain-width check can reject the bout.
- Runtime diagnostics now record whether the author path or direct fallback
  was used, authored and sampled path lengths, both projection points and
  offsets, projection distances, selected centre height, exact rank centres,
  advance balance, line width, and any lateral-only formation compression.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module and all `17` XML files parse. The live normal-client module
  contains no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum
  entry. Client and editor DLLs match at SHA-256
  `98513B9A95F07DD3721AE2AEF1FBCB0182E4EF61A97E1749B38A3360FB956CA4`.
  Repository/live README hashes match at
  `DE8B1D6D185BC88072C963E71844C3BD2E15F252C048C146A4935CC9DACE8FBD`
  for Chinese and
  `1F440DFA20B6CE250CC4A405CA01A59E12E9EAE9C662560EB1BDB794AE47D8E4`
  for English.
- ILSpy decompilation of the deployed client DLL is stored at
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\deployed_author_path\GreyWardenPolicePurity.GreyWardenFieldSparringMissionController.decompiled.cs`.
  It confirms `Mission.GetInitialSpawnPath`, authored point extraction, both
  polyline projections, midpoint and fixed `100 m` gap calculation,
  lateral-only `CreateFormationRankPosition`, and `FormOrderCustom`. It has no
  runtime `GetPathBetweenAIFaces` call. No archive, commit, push, tag, or
  GitHub Release was created.

# 2026-07-18 fixed 200 m arena and low-ground rank sampling

- The latest `rgl_log_40152.txt` proved that the author-path midpoint was
  correct, but `GetUsableLineWidth` was still coupled to the duel boundary.
  On `battle_terrain_031` the measured corridor width was only `24.0 m`, and
  the installed boundary therefore logged `width=24.0`. This was the direct
  cause of the visibly narrow arena.
- Arena size and formation frontage are now separate concepts. The native
  `walk_area` boundary always uses `DuelArenaHalfWidth=100`, preserving a
  fixed `200 m` crosswise battlefield. Its length remains bounded by the two
  rank fronts, which are planned `50 m` on either side of the meeting point.
  Terrain width can no longer shrink the duel boundary.
- After the map-author path identifies the route midpoint and battle axis, the
  controller samples the complete `200 m` perpendicular line at `2 m`
  intervals and chooses the lowest navigable, reasonably connected point as
  the actual meeting location. It then moves exactly `50 m` toward each army,
  samples a new `200 m` perpendicular line for each side, and independently
  chooses the lowest reachable point as that side's shared front-rank centre.
  Lateral movement does not alter the planned `100 m` projection between the
  two fronts.
- Formation frontage is capped separately at `100 m`. If the current desired
  formation widths fit, they remain natural; when troop demand exceeds the
  available terrain or the cap, `FormOrderCustom` consumes the usable width
  and the stock line formation adds depth behind the shared front line. The
  lateral target compressor now also verifies connectivity to the selected
  rank centre, preventing a target on an isolated navmesh island.
- Runtime logs now print the base point, selected low point, lateral offset,
  and height for the meeting, player-rank, and opponent-rank lines, followed
  by the fixed `200 m` arena width and separate usable formation frontage.
- The final Release build completed with `0` errors. All `24` deployable
  source files are SHA-256-identical to the live module and all `17` XML files
  parse. The live normal-client module has no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum entry. Client and editor DLLs match at
  SHA-256
  `8008E7A9FD5B345E5F5685FE6AE893C6330D50A58440651D237E53AB01CCC880`.
  Repository/live README hashes match at
  `64761C3151D1F0CAF408B315ABA33E0154A528C3CD82ADAFB511F5275962C84E`
  for Chinese and
  `C97903F6421A7EC3C1CD32E60D3954B4B55B36B7A590975660FDA69B7C625DBF`
  for English.
- ILSpy output for the deployed client DLL is stored at
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\deployed_fixed_200m\GreyWardenPolicePurity.GreyWardenFieldSparringMissionController.decompiled.cs`.
  It confirms constants `DuelArenaHalfWidth=100`,
  `MaximumFormationFrontWidth=100`, and `TerrainLineSampleStep=2`, all three
  `FindLowestPointOnBattleLine` calls, formation-width measurement capped at
  `50 m` per side, and a runtime boundary log of exactly `width=200`. No
  archive, commit, push, tag, or GitHub Release was created.

# 2026-07-18 native obstacle-aware formation frontage

- Screenshots and `rgl_log_7812.txt` showed that the fixed `200 m` duel
  boundary worked, but both armies still formed into narrow, deep blocks near
  rocks. In the largest test the three low-point lines were selected as
  intended and the front gap settled at `99.0 m`, yet the shared corridor
  calculation reported only `24 m`; a later test reported `16 m`, with the
  enemy formations receiving only `14 m` total frontage. The opposing lord's
  blocked advance was a consequence of that excessive depth, not a separate
  lord-movement defect.
- The rejected implementation measured from the low point toward each side,
  stopped at the first unavailable navmesh sample, doubled the shorter side,
  and then compressed every formation centre back toward the low point. A
  single rock or small navmesh gap could therefore discard open ground on the
  other side and reduce a hundred-metre formation plan to a few metres.
- Stock code confirms that this preprocessing duplicated and defeated the
  engine's own formation placement. `LineFormation` maintains ordered and
  available unit-position indices and obtains the per-slot world-position
  table through `Formation.BatchUnitPositions`. `IFormation.GetIsLocalPositionAvailable`
  calls `Mission.IsFormationUnitPositionAvailableMT` for every local slot.
  Unavailable slots inside a rectangular formation are therefore omitted or
  filled around by the native arrangement rather than requiring the entire
  frontage to end at the first obstacle.
- Formation planning now keeps the selected low point as the rank reference,
  calculates natural widths from the active formations, and caps their
  combined frontage at `100 m`. It no longer calls a continuous-terrain width
  measurement, scales the layout to the shorter side, or compresses individual
  formation centres toward the low point. Each formation receives its full
  geometric centre and `FormOrderCustom` width; Bannerlord then evaluates the
  complete width-and-depth rectangle and distributes units around rocks and
  other unusable slots. Excess troop demand increases depth only.
- The only retained validity requirement is the shared low-point rank origin,
  which is already chosen from a navigable point. A formation's laterally
  offset geometric centre is deliberately not pre-clamped. `MovementOrder`
  handles an unusable formation origin with the stock alternate-position path,
  while the width remains unchanged, preserving the same behavior as a player
  dragging a formation across obstructed ground.
- Natural frontage now uses each line formation's stock `MaximumWidth`, which
  represents its one-rank width at the current native interval. If the sum is
  below `100 m`, small forces remain naturally narrower. If it exceeds the
  limit, all active formations share the complete `100 m` frontage in
  proportion to those natural widths; the result is no longer limited by
  their earlier, already-deep `Formation.Width` values. After each movement
  order is applied, the stored arrival target is refreshed from the engine's
  actual `Formation.OrderPosition`, so a stock alternate origin cannot cause a
  false march timeout.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module and all `17` XML files parse. The live normal-client module
  has no editor-only directory, ZIP, or checksum entry. Client and editor DLLs
  match at SHA-256
  `473C67F45319C2A7F39D5498528E24B3459318EE601E10FB2E467F0D2A5B6DD2`.
  Repository/live README hashes match at
  `0F9464F91F7272703CA6252FB4A6D30D9223D580391AAD25EA5F0D57194AB6EC`
  for Chinese and
  `6715E7AEAE81DCA0A2CEBEAA3C3B62C942A3861CDDD90CA8BCF352FFD6AE1412`
  for English.
- ILSpy output for the deployed client DLL is stored at
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\deployed_native_obstacle_rank\GreyWardenPolicePurity.GreyWardenFieldSparringMissionController.decompiled.cs`.
  It confirms the `100 m` limit, `Formation.MaximumWidth`-based allocation,
  unchanged `FormOrderCustom` widths, native order-position refresh, and the
  fixed-`200 m` low-line selection. It contains no `GetUsableLineWidth`,
  `MeasureNavigableLineSide`, or lateral-scale logic. No archive, commit,
  push, tag, or GitHub Release was created.

# 2026-07-18 final v1.4.7-r3 replacement release

- The user accepted the completed town/field sparring system as the actual
  `v1.4.7-r3`. The earlier Git tag and GitHub Release with the same name were
  an incomplete intermediate publication. This formal task intentionally
  replaces that tag, release description, ZIP, and checksum rather than
  creating `r4`.
- The player-facing Chinese and English READMEs were rewritten against the
  real public baseline, GitHub release/tag `v1.4.7-r2`. They now contain one
  concise formal `2026-07-18 v1.4.7-r3` entry covering only player-visible
  differences: full English/CNs support; the troop and mission-local combat
  mastery rebalance; complete native town-arena and formation-based field
  sparring; authored-route, low-ground, fixed-width and native obstacle-aware
  field formation behavior; the player alternative-attack fallback exception;
  and the encyclopedia, returning-patrol, and village-defense text fixes.
  Intermediate `r3/r4/testing` wording and step-by-step test history remain
  absent from the shipped README.
- Contact information now distinguishes personal QQ `157652226` from the
  public QQ discussion/download group `981323752`. The group is described as
  a place for discussion, feedback, and file downloads in both READMEs.
- A final Release build succeeded with `0` errors. Repository and live module
  contain `24` matching deployable source files; all `17` XML files parse;
  client and editor binaries match. The deployed client DLL SHA-256 is
  `473C67F45319C2A7F39D5498528E24B3459318EE601E10FB2E467F0D2A5B6DD2`.
- The protected asset packages remain byte-identical:
  - `gwp_inherited_legacy_assets.tpac`:
    `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`
  - `gwp_black_gold_shield.tpac`:
    `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B`
- The formal archive was rebuilt from the verified live runtime at
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7.zip`
  with its sibling `.sha256`. It contains one top-level `GreyWarden` directory
  and `28` runtime files: both asset packages, client binaries, GUI,
  ModuleData, ModuleSounds, Shaders, `SubModule.xml`, and both player READMEs.
  It contains no editor workspace, editor binary, PDB, source asset, nested
  archive, checksum, or diagnostic file. Size: `350,379,743` bytes. SHA-256:
  `925B3D2B9CFAF92A6BFDF29172A9642247E839778018A8218C7EAD5E0493C4FE`.
- The new archive was extracted under
  `C:\tmp\gwp-r3-final-extract\GreyWarden` and compared file by file against
  the staging tree at `C:\tmp\gwp-r3-final-package\GreyWarden`: `0` missing,
  `0` hash mismatches, `0` extra files, and `0` forbidden entries. The final
  Modules-directory archive hash matches the verified temporary archive.
- This entry is the source of truth for the replacement `main` commit,
  force-updated annotated `v1.4.7-r3` tag, and updated GitHub Release assets.

# 2026-07-19 clean-shutdown native crash diagnosis

- The latest session was reported as started through the desktop shortcut
  `C:\Users\lucif\Desktop\骑砍中文站Mod管理器.lnk`, whose verified target is
  `C:\Users\lucif\AppData\Local\Programs\modmaster\骑砍中文站Mod管理器.exe`.
  Game process `27920` ran from `04:26:52` to `04:57:40`; gameplay and mission
  teardown completed normally. `rgl_log_27920.txt` reached `Start Game Final Cleanup`,
  deleted the game and managed interface, and reported `There are no living
  managed objects` before logging stopped. `rgl_log_errors_27920.txt` contains
  no managed exception or Grey Warden error.
- Windows Application Error event `1000` then recorded a real native access
  violation in the official executable: `Bannerlord.exe` / game build
  `v1.4.7.117484`, faulting module `TaleWorlds.Native.dll`, exception
  `0xc0000005`, offset `0x000000000074B3F1`. WER event `1001` archived report
  ID `4319394d-6688-45bc-bc7f-c49a4d437382` under
  `C:\ProgramData\Microsoft\Windows\WER\ReportArchive\AppCrash_Bannerlord.exe_5ef6f7d0c155a37c4ed93439c7161f7f5191c8_ebd6fe1f_a8b9f016-7f7f-4ca8-bf54-055dce82cb3b`.
- The same native module and exact `0x74B3F1` offset occurred at `10:19:34` on
  2026-07-18. That earlier `rgl_log_40152.txt` also reached the identical clean
  managed-interface shutdown and reported no living managed objects. Nearby
  native cleanup offsets `0x74B13F` and `0x74B34A` were also observed on
  2026-07-16. This is therefore a repeatable final native-cleanup failure, not
  a field/town sparring state-machine exception or a newly introduced managed
  object leak.
- The watchdog arguments match the manager-generated command already isolated
  on 2026-07-16: `/anticheat` is forced and the official modules are ordered
  `Native, SandBoxCore, CustomBattle, Sandbox, StoryMode, BirthAndDeath,
  FastMode`. The manager itself does not need to inject a DLL for that launch
  command to change shutdown behavior.
- This recurrence is consistent with the earlier controlled comparison: two
  manager-style GreyWarden-only runs crashed after `Managed Interface deleted`,
  while two official-style runs using the official module order without
  `/anticheat` exited without a WER crash. The established unresolved variable
  remains the manager's module ordering and/or forced `/anticheat`, not the
  sparring implementation. No further user test or Grey Warden code change was
  requested for this recurrence; retain it as accumulated evidence.
# 2026-07-19 permanent AI criminal ledger and split deterrence

- Replaced the capacity-limited pending AI `CrimePool._pool` and the separate
  deterrence dictionary with one canonical `CrimeRecord` per non-player lord,
  keyed by stable `Hero.StringId`. Each record stores the latest case metadata,
  the oldest unresolved-case time, permanent total crime count, permanent total
  Grey Warden arrest count, open/closed state, direct deterrence, clan-shared
  deterrence, and the few timestamps needed for recovery. Save growth is
  therefore bounded by the number of lords rather than the number of offenses.
- Police tasks now store only `TargetCrimeId` plus live workflow flags. Their
  `TargetCrime` property resolves the canonical ledger record, eliminating the
  former duplicate embedded crime copy. Failed or displaced tasks call
  `ReopenCase` and do not increment the permanent crime count. This keeps the
  small task table because it represents active police operations rather than
  historical data.
- Removed the `PoliceClanMemberCount` admission cap and the crime monitor's
  `IsAccepting` early exits. Every distinct detected raid, villager attack, or
  caravan attack can increment the responsible lord's permanent count even if
  the same lord already has an unresolved case. Dispatch scans unassigned open
  cases by oldest `OccurredTime`, using police distance only as the tie-break.
- `HeroPrisonerTaken(PartyBase, Hero)` is now the authoritative arrest event.
  A capture increments the permanent arrest total only when the captor is a
  Grey Warden clan party, regular patrol, or delayed enforcement patrol and the
  prisoner already has a real criminal record. This avoids treating a mere
  battle defeat as an arrest and is independent of map-event cleanup ordering.
- Direct and clan-shared deterrence are stored separately but share the existing
  total cap and crime-desire multiplier. A real arrest adds direct deterrence
  based on the permanent arrest count; the new direct gain has priority at the
  cap and each eligible clan member receives half of that new gain as shared
  deterrence. Recovery subtracts once from the combined total and scales both
  components proportionally. Reaching zero clears only temporary deterrence;
  permanent crime and arrest totals remain in the save.
- Deterrence conversation source is selected from the larger effective
  component, with direct winning ties. Responses are split into personal-arrest
  and clan-event families, then chosen from honorable/merciful, calculating,
  valorous, hostile, or neutral attitudes. The old uniformly fearful wound and
  nightmare lines are no longer used. English source text and Simplified
  Chinese entries were added for every new line.
- The encyclopedia button now presents a permanent criminal record together
  with direct deterrence, clan deterrence, combined deterrence, arrest totals,
  crime totals, suppression multipliers, last enforcement, status, and
  location. Player-facing Chinese and English READMEs were updated in the same
  change.
- Old `gwp_cp_*` and `gwp_det_*` save layouts are intentionally not migrated,
  per the user's explicit decision that old-save compatibility is unnecessary
  at the current pre-adoption stage. New canonical keys use `gwp_ledger_*`,
  `gwp_l_*`, and `gwp_lt_*`.
- Final Release build completed with `0` errors and the pre-existing nullable
  warnings only. The deploy step copied the runtime module and editor binary.
  Post-deploy verification found `24` deployable files with `0` missing/hash
  mismatches, `17` XML files with `0` parse failures, matching client/editor
  DLLs, matching Chinese/English READMEs, and `0` forbidden live root entries
  (`Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum). The deployed
  client DLL SHA-256 is
  `2115F1677BEA1DA93524CCE514D83F14E11C1E9EB4D014D8728B5432061BBEDA`.
- ILSpy `10.1.1.8388` successfully decompiled the deployed client DLL into
  `.codex_tmp/deployed-ai-ledger-final` (`83` C# files). Decompiled-output probes
  confirmed the new `gwp_ledger_count` save path, `HeroPrisonerTaken`
  subscription, permanent `TotalCrimeCount`, split
  `DirectDeterrencePoints`, and the new personality dialogue keys.

# 2026-07-19 nearest-open-case dispatch adjustment

- Confirmed that `CrimeRecord.HasOpenCase` is the lightweight operational
  marker: permanent crime/arrest totals remain after it is cleared. A real
  Grey Warden capture clears the marker through `CrimePool.RecordArrest`.
- Changed `CrimePool.GetNearest` and `GetNearestNonPlayer` from oldest-case-first
  ordering to pure distance ordering across valid, unassigned records whose
  `HasOpenCase` marker is set. Each newly idle police party therefore selects
  the nearest currently pursuable unresolved offender from its own position.
- Removed the now-unused oldest-then-distance selector. Crime occurrence times
  remain in the canonical ledger for history and future presentation, but no
  longer control dispatch priority.
- Updated both player-facing READMEs to describe nearest-offender dispatch.
- Release build after the distance-priority adjustment succeeded with `0`
  errors. Repository/live verification again found `24` deployable files with
  `0` differences and `17` valid XML files with `0` failures; client/editor
  DLLs and both READMEs match. Current deployed DLL SHA-256:
  `BB4519B95901D48956B7075241688BD0472D94F48ABED4845DD296933420B24B`.
- ILSpy decompilation of the deployed `CrimePool` was saved at
  `.codex_tmp/CrimePool-nearest-final.cs` and confirms that both police dispatch
  entry points now call the distance-only `SelectNearest` ordering.

# 2026-07-19 co-captured witness deterrence

- Tightened `HeroPrisonerTaken`: an AI lord now gains a permanent Grey Warden
  arrest and direct deterrence only when that lord's canonical ledger has
  `HasOpenCase == true` at the moment of police capture. A lord with only old,
  closed criminal history is treated like any other non-offender in that
  battle and does not receive another arrest or direct deterrence.
- Added a non-serialized `PoliceCaptureBatch` keyed by the active `MapEvent`.
  It records actual heroes captured by Grey Warden/patrol parties, separates
  open-case offenders from no-open-case witnesses, and records each offender's
  newly added direct deterrence divided by two. The batch is discarded when
  `MapEventEnded` fires and is never written to the save.
- At map-event completion, every co-captured eligible witness receives the
  half-strength amount as `SharedDeterrencePoints` only. This changes neither
  `TotalCrimeCount`, `TotalArrestCount`, nor `HasOpenCase`, and the witness's
  clan is not traversed, so there is no second-generation propagation.
- The actual offender's existing first-generation clan shock remains. A
  co-captured witness belonging to that same clan is skipped by the witness
  pass for that offender because the clan pass already granted the identical
  half-strength amount; this prevents double credit for the same arrest.
- Multiple open-case offenders captured in one battle each contribute one
  half-strength witness event. All additions remain subject to the common
  nine-point cap. `RegisterPoliceArrest` already gives direct deterrence
  priority: new direct points replace shared points at the cap before the
  combined total can exceed nine.
- Updated both player-facing READMEs. No new localization keys were required;
  witness responses intentionally use the existing clan/shared-deterrence
  dialogue family.
- Final Release deployment completed with `0` errors. A first verification
  rebuild was launched without permission to write the external game folder;
  MSBuild could compile the assembly but failed while replacing the live
  client DLL. Re-running the same Release build with the required game-folder
  permission immediately restored and deployed the client and editor DLLs.
  This was a filesystem permission failure, not a compiler or gameplay-code
  failure.
- Post-deploy verification found `24` deployable source files with `0` missing
  files or hash mismatches, `17` XML files with `0` parse failures, matching
  client/editor DLLs, matching Chinese/English READMEs, and `0` forbidden live
  root entries (`Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or
  checksum). The deployed client DLL SHA-256 is
  `2DF60090A1DDD012E78CBE567C183918256CB338498D3E5EEF7888F62E3B076C`.
- ILSpy decompiled the deployed `PoliceAIDeterrenceBehavior` into
  `.codex_tmp/PoliceAIDeterrenceBehavior-witness-final.cs`. The output confirms
  the `HasOpenCase` gate, `PoliceCaptureBatch`, `MapEventEnded` batch handling,
  same-clan duplicate suppression, and witness-only calls to
  `RegisterSharedFamilyDeterrence`.

# 2026-07-19 layered AI deterrence dialogue and player identity

- Replaced the previous fixed-priority deterrence greeting with a four-layer response model. The selected line now combines the dominant deterrence source (`Personal` or `Family`), current combined deterrence tier (`0.25-3` low, `>3-6` medium, `>6-9` high), a personality-weighted voice, and whether the player has accepted Grey Warden recruitment.
- Direct deterrence is the personal source and shared deterrence is the family/witness source. The larger current effective component selects the family of lines; direct wins an exact tie. Both components continue to use their existing proportional decay before dialogue selection, so the displayed attitude follows current rather than historical peak deterrence.
- Personality no longer locks a lord to the first matching trait. Positive Honor, Calculating, Mercy, and Valor select the honorable, calculating, merciful, and valorous voices; negative Mercy selects the cruel voice. Each active trait uses weight `1 + 2 * trait level`, while neutral always keeps weight `1`. This makes stronger traits more likely without making them deterministic, and lets mixed personalities produce several believable responses.
- Player identity is read from the existing saved `gwp_recruitment_accepted` state exposed by `PlayerBountyBehavior.IsRecruitedByGreyWardens`; the Grey Warden clan id is also recognized for future direct membership. Outsider lines advise or warn the player and never imply that the player made the arrest. Recruited-player lines recognize the black-robed bounty-hunter role and address how the player should use Grey Warden authority. Membership wording deliberately does not assume the player is currently wearing the black outfit, because the saved recruitment identity persists when equipment changes.
- Added `GwpAiDeterrenceDialogueCatalog.cs` with six source/tier introductions, 36 source/tier/personality cores, and 36 player-role/tier/personality audience clauses. This produces 72 complete response combinations while keeping authorship and localization maintainable. English source text and all 78 Simplified Chinese localization entries are present. Obsolete uniformly fearful `gwp_ai_deterrence_*` wound/nightmare strings were removed.
- The deterrence greeting remains a 10% replacement during an ordinary one-to-one lord conversation and remains disabled in immediate `CapturedLord` and `FreeOrCapturePrisonerHero` prisoner-decision contexts. The random trigger and selected intro/response are now cached for the conversation, because Bannerlord evaluates both dialogue nodes separately; without caching, the second node could reroll a different personality or fail after the first line had already appeared.
- The first Release build exposed one new compiler error: `MBRandom` was unresolved in `GwpAiDeterrenceState.cs`. The cause was the missing `TaleWorlds.Core` import; adding that namespace fixed it. The full rebuild then succeeded with `0` errors and only the existing 44 nullable warnings. A final incremental Release build after README updates succeeded with `0` warnings and `0` errors.
- Final deployment verification found `24` deployable source files with `0` missing/hash mismatches, `17` XML files with `0` parse failures, identical client/editor DLLs, identical Chinese/English source and live READMEs, and `0` forbidden live root entries (`Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum). Deployed client DLL SHA-256: `8CED48CCA26963D2EF5F27F1892AA07B81541AE646734B97555C5F1A87A61EC7`.
- ILSpy `10.1.1.8388` decompiled the deployed classes into `.codex_tmp/GwpAiDeterrenceState-layered-final.cs`, `.codex_tmp/GwpAiDeterrenceDialogueCatalog-layered-final.cs`, and `.codex_tmp/PoliceAIDeterrenceBehavior-layered-final.cs`. Probes confirmed the `3/6` tier boundaries, weighted selector, recruitment-state role check, Warden high/honorable key, 10% chance, and per-conversation cached output.
# 2026-07-19 official five-axis deterrence dialogue test build

- Verified the current game build directly rather than inferring its personality
  model from the encyclopedia display. The installed
  `TaleWorlds.CampaignSystem.dll` defines five non-hidden personality traits,
  each ranging from `-2` to `2`: `Honor`, `Valor`, `Mercy`, `Generosity`, and
  `Calculating`. The official Simplified Chinese names are 荣誉、胆气、善恶、胸怀、谋略.
  `CampaignUIHelper.GetHeroTraits()` returns all five, while
  `EncyclopediaHeroPageVM` omits any trait whose value is zero. A particular
  hero can therefore show four dimensions in the encyclopedia even though the
  underlying system has five.
- Replaced the incomplete personality selector with ten signed response voices:
  high/low Honor, Valor, Mercy, Generosity, and Calculating. Selection weight is
  the absolute value of each nonzero native trait, so a `+2` or `-2` direction
  is twice as likely as a `+1` or `-1` direction. The neutral voice is available
  only when all five values are zero. The existing dominant personal/family
  source, low/medium/high deterrence tier, and outsider/recruited-player layers
  remain intact.
- The regenerated bilingual catalog contains `6` source/tier introductions,
  `66` source/tier/voice cores, and `66` player-role/tier/voice clauses, for
  `138` localized keys. This covers all ten official positive/negative trait
  poles plus the all-zero fallback across both deterrence sources and all three
  intensity tiers. The Simplified Chinese XML contains every referenced key.
- For this explicit test build, changed `DeterrenceGreetingChance` from `0.1f`
  to `1f`. Any otherwise eligible ordinary one-to-one conversation with at
  least `0.25` effective deterrence now passes the greeting roll. Main-hero,
  Grey-Warden-hero, immediate captured-lord, and prisoner-decision exclusions
  remain unchanged, and the result is still cached once per conversation.
- Fixed premature localization resolution in `GreyWardenAdoptionLogEntry`.
  Previously `GwpText.Get()` converted the template to a string before
  `HERO.LINK` and `VILLAGE` were assigned, producing a sentence with both
  values blank. The entry now creates the `TextObject` first, assigns both
  variables, and only then lets the engine resolve it. Applied the same fix to
  deterrence introductions so `HERO_NAME` is not erased before assignment.
- Final Release build succeeded with `0` warnings and `0` errors after the
  README update. Deployment verification found `24` runtime deployable files
  with `0` missing/hash mismatches after excluding the intentional `Assets` and
  `AssetSources` editor-recovery trees, `17` XML files with `0` parse failures,
  identical client/editor DLLs, matching Chinese/English source and live
  READMEs, and no forbidden editor directory, ZIP, or checksum in the live
  module. Deployed DLL SHA-256:
  `2C169C242FA99F305961FECB8E7A6FECA004BF5032769760999462CA6F37D725`.
- ILSpy `10.1.1.8388` decompiled the four deployed classes into
  `.codex_tmp/deployed-deterrence-test-final`. Probes confirmed all five native
  trait reads, all ten signed voice poles, the always-passing `<= 1f` test roll,
  the new catalog poles, and deferred `HERO.LINK`, `VILLAGE`, and `HERO_NAME`
  variable assignment. No Git commit, push, tag, package, or release was made.

# 2026-07-19 persistent righteous-kill reputation progress

- Confirmed the previous positive-reputation path used `playerKillCount / 10`
  independently in each qualifying victory. It awarded one point per complete
  ten personal kills in that battle and discarded every remainder at battle
  end. The same isolated calculation existed both for ordinary good-deed
  battles and for helping Grey Wardens apprehend a criminal.
- Added `PlayerBehaviorPool.GoodDeedKillProgress`, constrained to `0..9`.
  `AccumulateGoodDeedKills` combines the saved remainder with the current
  qualifying battle's personal kills, returns one reputation point per complete
  ten, and retains only the remainder. Thus `6 + 3 + 1` across three qualifying
  victories grants one point, while `27` with no prior remainder grants two and
  retains seven.
- Both positive-reputation paths now use this common accumulator: defeating
  bandits, rescuing villagers or caravans, defending a village from a raid, and
  helping Grey Wardens defeat an offender. Non-qualifying battles, sparring,
  and criminal-side battles do not add to or clear this progress.
- Persisted the remainder under `gwp_good_deed_kill_progress` in the existing
  player behavior save block. New games and `ClearAll` reset it to zero; loading
  restores and clamps it to `0..9`. This adds one integer per save rather than a
  battle history.
- The negative-reputation paths were deliberately left unchanged. Criminal
  actions and battles still settle their existing loss independently per event
  or battle; losses neither consume nor reset the saved righteous-kill remainder.
- Updated both player-facing READMEs. No localization key was added because the
  existing reputation-award notifications remain unchanged and appear only
  when at least one full ten-kill unit is converted into reputation.
- Final Release build succeeded with `0` warnings and `0` errors. Live
  verification found `24` runtime deployable files with `0` hash mismatches,
  `17` XML files with `0` parse failures, identical client/editor DLLs,
  matching Chinese and English READMEs, and no live editor-only directory, ZIP,
  or checksum. Deployed DLL SHA-256:
  `2280184ABAC0008FB005FF25F7F881882D0C07AB077984D42F3645093C29A51F`.
- ILSpy `10.1.1.8388` decompiled the deployed `PlayerBehaviorPool`,
  `GwpRuntimeState`, and `PlayerBehaviorMonitor` into
  `.codex_tmp/deployed-persistent-reputation-final`. The output confirms the
  `/ 10` and `% 10` accumulator, the save key, both positive accumulation call
  sites, and the removal of per-battle `playerKillCount / 10` calculations. No
  commit, push, archive, tag, or release was created.


# 2026-07-19 CourierMessenger hero-page compatibility

- Controlled tests with GreyWarden, `CourierMessenger v1.2.2`, and its four
  prerequisites established that new campaigns start and reload, GreyWarden's
  encyclopedia controls work, and Courier's injected button appears but remains
  disabled. The user repeated the test after the first compatibility build in
  both the existing test save and another new campaign; the button was still
  disabled. Old-save migration is explicitly outside the requested scope.
- The exact original conflict remains proven: Courier injects
  `EncyclopediaHeroPageInject.xml`, but declares
  `[ViewModelMixin("RefreshValues")]` only for the exact native
  `EncyclopediaHeroPageVM`. GreyWarden previously registered the derived
  `[EncyclopediaViewModel(typeof(Hero))] GwpEncyclopediaHeroPageVM`, so Courier's
  exact-type lookup did not initialize its button properties and command.
- The first attempted bridge copied Courier's mixin type into UIExtenderEx's
  dictionary under `GwpEncyclopediaHeroPageVM`. This was insufficient and is now
  removed. UIExtenderEx also patches each registered VM constructor and refresh
  method; changing only its dictionary never installed those patches for the
  derived GreyWarden page. The missing success marker in the 10:51 test log was
  not by itself reliable because `Debug.Print` messages are not generally
  emitted into `rgl_log`, but the repeated disabled-button test proves the
  bridge did not produce a live mixin instance.
- The replacement-page architecture has now been removed instead of adding
  another Courier-specific workaround. GreyWarden no longer declares any hero
  `EncyclopediaViewModel` and no longer supplies a
  `GwpEncyclopediaHeroPageVM` subclass. A Harmony postfix on the native
  `EncyclopediaHeroPageVM(EncyclopediaPageArgs)` constructor attaches only
  GreyWarden's `DeterrenceButtonText`, `DeterrenceButtonHint`, and
  `ExecuteOpenDeterrenceDetails` bindings to that individual native VM. The
  existing GreyWarden prefab still displays the record button, while the page's
  runtime type remains exactly native for Courier and other exact-type mixins.
- `GwpNativeViewModelExtension` creates an instance-local copy of Bannerlord's
  native binding dictionaries before adding the three GreyWarden members. It
  uses only the already bundled Harmony and TaleWorlds runtime; there is no
  compile-time or load-time reference to CourierMessenger, UIExtenderEx,
  ButterLib, or MCM. GreyWarden therefore retains zero external prerequisites.
  Courier is neither detected nor invoked by GreyWarden and continues to own
  its availability rules, price, hint, click event, and campaign behavior.
- Release build succeeded with zero errors. Final deployed DLL version is
  `1.4.7.0`, SHA-256
  `3379B6E1AFEF560421A87B945C94ABE2BCC52A6B9FF0F518637287196AFC7EE0`.
  Mirror verification found `24` deployable files with `0` missing/hash
  mismatches, `17` XML files with `0` parse failures, identical client/editor
  DLLs, matching source/live Chinese and English READMEs, and no live
  `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum artifacts.
- ILSpy decompiled the deployed build into
  `.codex_tmp/deployed-native-hero-page`. The deployed type list contains
  `GwpEncyclopediaHeroPageExtension`, its native-constructor patch, and
  `GwpNativeViewModelExtension`; it contains neither
  `GwpEncyclopediaHeroPageVM` nor the removed Courier compatibility bridge.
  In-game compatibility still requires the next user test; static validation
  proves the native-page architecture and deployment, not Messenger's runtime
  result.
- No Git commit, push, archive, tag, or release was made in this iteration.

# 2026-07-19 formal v1.4.7-r4 release

- The user verified in game that the native hero-page refactor fixes the
  GreyWarden/CourierMessenger conflict: GreyWarden's record control and
  Messenger's formerly disabled encyclopedia button now work together.
  GreyWarden still has no external prerequisite requirement.
- The repository, module metadata, and existing public releases all target
  Bannerlord/GreyWarden `1.4.7`; the spoken `1.4.6` reference was therefore
  treated as voice-transcription drift. This publication is tagged
  `v1.4.7-r4` and compares against the actual preceding public release,
  `v1.4.7-r3`.
- Both player READMEs were rewritten as a single concise `v1.4.7-r4` entry.
  Earlier release sections were removed. The new entry describes only visible
  differences from `r3`: permanent AI crime/arrest history; separate open-case,
  personal-deterrence, clan-deterrence, witness-capture, and nearest-offender
  handling; personality/source/intensity/player-role deterrence greetings;
  cross-battle righteous-kill reputation progress; corrected adoption text;
  and native-page Messenger compatibility. The current-playable summary and QQ
  group `981323752` remain.
- Release build succeeded with zero errors. The deployed client DLL version is
  `1.4.7.0`, SHA-256
  `3379B6E1AFEF560421A87B945C94ABE2BCC52A6B9FF0F518637287196AFC7EE0`.
  Repository/live verification found `24` deployable files with `0`
  missing/hash mismatches, `17` XML files with `0` parse failures, identical
  client/editor DLLs, matching source/live Chinese and English READMEs, and no
  forbidden editor directory, nested ZIP, or checksum inside the live module.
- The formal player archive is
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7-r4.zip`
  with its sibling `.sha256`. It contains one top-level `GreyWarden` directory
  and `27` runtime files. It excludes `Assets`, `AssetSources`,
  `RuntimeDataCache`, editor binaries, PDBs, source FBX/PNG files, logs, dumps,
  nested archives, and checksums. Size: `349,741,341` bytes. SHA-256:
  `E3073DE8B81A38395A930260BE3A3B8C2AACE12F3B786F5A9BC20284E5A4E3C1`.
- The final archive was extracted to `C:\tmp\gwp-r4-extract\GreyWarden` and
  compared file by file with `C:\tmp\gwp-r4-package\GreyWarden`: `27` files
  on each side, `0` missing/hash mismatches/extras, and `0` forbidden entries.
  Protected TPAC hashes remain:
  - `gwp_inherited_legacy_assets.tpac`:
    `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`
  - `gwp_black_gold_shield.tpac`:
    `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B`
- GitHub release retention is now explicit: for the same Bannerlord/mod version,
  retain only the newest two public releases. After publishing `v1.4.7-r4`,
  keep `v1.4.7-r3` and `v1.4.7-r4`; delete the older `v1.4.7` and
  `v1.4.7-r2` GitHub Releases and their release tags. Milestone tags such as
  `bannerlord-v1.4.7` are not public release records and remain untouched.
# 2026-07-19 release archive filename rule

- Every formal player archive must preserve the complete public release
  identifier in its filename, including the revision suffix. Required pattern:
  `GreyWarden-v{game/mod-version}-r{revision}.zip`; examples:
  `GreyWarden-v1.4.7-r3.zip` and `GreyWarden-v1.4.7-r4.zip`.
- Never shorten a formal revision archive to `GreyWarden-v1.4.7.zip`. The
  sibling checksum must use the same complete basename plus `.sha256`, and its
  text must name that exact ZIP. Before reporting a package complete, verify the
  ZIP filename, checksum filename, checksum text, release tag, and README
  version all carry the same revision identifier.
- The current local r4 archive and checksum were renamed accordingly; their
  bytes and SHA-256 remain unchanged.


# 2026-07-19 Grey Warden native desire integration

- Scope: replaced Grey Warden strategic-map command injection with a final-registered `GreyWardenPartyDesireBehavior`. It participates in Bannerlord v1.4.7's `PartyThinkParams` score auction after native and existing mod listeners have produced their candidates. No Harmony patch to the engine's think loop was required.
- Case duty is represented by `AiBehavior.GoAroundParty`. This works before a police war exists, so the assigned party can approach a neutral offender; the existing enforcement hourly distance check still declares war at the configured range, after which the native tactical AI can engage. Player targets retain their existing dialogue-before-war path.
- Resource arbitration has three states. Ready parties receive a stable police-duty floor. Low-resource parties keep the case but reduce its duty score below a boosted settlement visit. Critical parties temporarily add no duty candidate and give an eligible town visit the strongest score. Once food, strength, and wounded ratios recover, the retained task naturally wins again.
- Native `GoToSettlement` and `PatrolAroundPoint` candidates remain available. Raid, siege, army, generic war-target, and other incompatible kingdom duties are zeroed only for managed Grey Warden parties. The visit path therefore continues to drive native market food purchases, recruitment, healing, loot sales, and prisoner sales.
- Temporary map roles use expiring desire requests rather than movement commands: approach, escort, or visit. This covers player pickets, recruitment heralds, enforcement-delay patrols, bounty escorts, captive escorts, village relief travel/stay, and return/disband travel. Requests are refreshed by their owning behavior and disappear after eight hours if not renewed.
- Removed all Grey Warden strategic uses of `SetDoNotMakeNewDecisions(true)`, `SetMoveEngageParty`, `SetMoveEscortParty`, `SetMoveGoToSettlement`, and `SetMoveModeHold`. The desire layer only clears legacy locks, resets initiative, and requests a rethink; it never writes a map destination.
- Removed the per-member daily `1000`-denar salary call. `PoliceResourceManager` no longer generates grain or fills a party to its size limit after a task or in a town. Existing compatibility entry points now only request an AI rethink and never create resources or issue movement orders.
- Formal lord parties now use the native lord-party spawn roster and native recruitment. The existing six-hour purification remains the sole conversion step: any recruited non-Grey-Warden regular becomes `gwrecruit`. Ship assignment was not changed in this pass because the user explicitly deferred the wider economy redesign and scoped the immediate resource removal to money, food, and free troop refills.
- Static validation: `dotnet msbuild ... /t:Compile /p:Configuration=Release` completed with zero compiler errors. Source audit found no remaining Grey Warden strategic `SetDoNotMakeNewDecisions(true)` or direct engage/escort/settlement/hold movement call. `GreyWardenDesertersCampaignBehavior` retains its independent bandit patrol command and battle-mission formation orders remain untouched because neither controls Grey Warden police parties.
- Runtime validation still required in game: neutral approach, near-distance war declaration, temporary supply diversion and case resumption, genuine market spending, native recruitment plus six-hour purification, loot/prisoner sale, player dialogue/capture escort, village relief, temporary patrol return, and save/reload during both pursuit and recovery.
- No commit, tag, archive, or GitHub release was created for this development build.

- Final local compile artifact: `GreyWardenPolicePurity/obj/Release/GreyWardenPolicePurity.dll`, size `466432` bytes, SHA-256 `8EA585F12E8B063787EF43E9D0C95379E024548939BF12C5FBC67138E87E6E74`. ILSpy confirms the deployed-intent type and `PartyThinkParams.AddBehaviorScore` call are present.
- Live deployment is not complete. The managed environment rejected the required elevated `dotnet build` because its approval/usage quota was exhausted. The attempted ordinary build could compile but could not write outside the repository. A subsequent read showed the live normal-client module currently has no `GreyWardenPolicePurity.dll` and no root README/SubModule files visible, while the editor directory still contains the previous `464896`-byte DLL. Do not launch an in-game test until an unsandboxed `dotnet build GreyWardenPolicePurity\GreyWardenPolicePurity.csproj -c Release --no-restore` succeeds and repository/live hashes are rechecked.


## 2026-07-19 pursuit-continuity correction

- Review of Bannerlord v1.4.7 `MobilePartyAi` confirmed the concern raised during handoff. `GoAroundParty` is not a permanent direct chase: `GetGoAroundPartyBehavior` may choose a defensive/interception point around a faster target, while ordinary initiative AI only converts a nearby enemy to `EngageParty` after its own local strength/priority evaluation. The task itself was retained, but relying on `GoAroundParty` alone did not provide a sufficiently explicit guarantee that a declared police target would remain continuously followed.
- Added a separate `Pursue` intent. Before war, police still use `GoAroundParty` to approach a neutral offender without illegal hostility. After the existing distance rule declares war, the same retained task changes to native `EscortParty`; Bannerlord's `GetFollowBehavior` continuously updates the destination to the target party's current position and therefore does not abandon the assignment merely because the offender is faster.
- When a pursued offender enters a settlement, the intent temporarily falls back to `GoAroundParty` rather than attempting to enter a hostile settlement. The existing sheltered-target gate watch, declaration, and expulsion path remains responsible for that state.
- `PoliceMobilePartyAIModel.GetBestInitiativeBehavior` now supplies a narrow close-range guarantee only when the currently selected long-term behavior is the matching pursuit `EscortParty`. Within the native initiative radius, and only while the two factions are at war and the morale/navigation attack checks pass, the original engine receives `EngageParty` for the assigned offender with a score above unrelated nearby enemies. It does not write a movement command and it cannot override a selected town-recovery desire.
- Enforcement-delay patrols and post-refusal player pickets now request the same `Pursue` intent. The existing two-day persistent-war scan still spawns one delay patrol per tracked offender; both the primary party and relief party keep following until battle, target invalidation, peace, or a critical-resource diversion.


## 2026-07-19 pursuit rule clarification

- Final user rule: only speed disadvantage is forbidden from cancelling or lowering the long-term police pursuit. Native strength, nearby ally/enemy power, morale, navigation, and other tactical engagement checks remain authoritative.
- Removed the provisional close-range `GetBestInitiativeBehavior` override because its fixed high `EngageParty` score would have bypassed the native strength comparison. The custom `PoliceMobilePartyAIModel` again delegates ordinary engagement scoring to Bannerlord.
- The final split is deliberate: a declared case selects persistent native `EscortParty` as its long-term tracking behavior, so a faster offender cannot make the task disappear; `MobilePartyAi.GetBestInitiativeBehavior` remains native, so an outmatched Warden follows without attacking. When a relief party arrives or the power balance changes, native initiative can select `EngageParty` and start the battle.


## 2026-07-19 native-desire final build and deployment

- The earlier “live deployment is not complete” entry above records the failed
  restricted-environment attempt and is now superseded. With the required
  filesystem access available, the final `Release --no-restore` build completed
  with `0` errors. Its `46` warnings are the existing nullable diagnostics plus
  `NU1900` because NuGet vulnerability metadata was unavailable; none prevented
  compilation or deployment.
- Final pursuit semantics match the clarified rule exactly. The desire layer
  emits `GoAroundParty` before police war and persistent `EscortParty` after
  war. No custom `GetBestInitiativeBehavior` override exists, so only speed is
  excluded as a reason to abandon long-term tracking; native strength, nearby
  support, morale, navigation, and other tactical checks still decide whether
  the tracking party actually changes to `EngageParty`.
- Static source audit found no managed Grey Warden strategic use of
  `SetDoNotMakeNewDecisions(true)`, direct engage/escort/settlement/hold movement
  commands, the removed salary symbol, generated food, or free troop filling.
  `git diff --check` reported no whitespace error.
- The build synchronized repository `_Module` files to
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`.
  Final verification compared `24` deployable source files: `0` missing and `0`
  hash mismatches. All `17` live XML files parsed successfully. Client and
  editor DLLs are identical with SHA-256
  `E4B1250E306D648B5EB99239F4A44CF9B407FC8EF65898A005E9B03746CA94B5`.
  The live module contains no `Assets`, `AssetSources`, or `RuntimeDataCache`
  directory and no nested archive.
- ILSpy decompilation of the deployed DLL confirms the desire type writes
  `PartyThinkParams` scores using engine enum values `8` (`GoAroundParty`) and
  `14` (`EscortParty`). The deployed `PoliceMobilePartyAIModel` contains only
  its existing `ShouldConsiderAttacking` override and no initiative override.
  Runtime behavior still requires the focused in-game pursuit/power/resource
  tests described in the handoff; no commit, tag, archive, or release was made.


## 2026-07-19 native-first allowlist and fallback-case correction

- The first desire integration was still too custom. It retained native
  `PartyThinkParams`, but imposed its own food/strength/wounded states, raised a
  chosen town to custom `7.5`/`12` floors, assigned police pursuit `6.5`/`7.25`,
  and separately modified idle patrol scores. The user's in-game observation
  that a shrinking party continued pursuing instead of resupplying showed that
  this architecture did not satisfy “native desires minus forbidden desires,
  plus a fallback police desire.” Those custom resource and patrol arbitration
  paths are now removed rather than retuned.
- Local decompilation of installed Bannerlord `v1.4.7`
  `AiVisitSettlementBehavior` is the authoritative basis for the replacement.
  Native `GoToSettlement` already combines food and food consumption, wounded
  ratio, party-size ratio, available wage budget, volunteers, leader/party
  money, sellable non-food items and mounts, prisoners, settlement food stock,
  distance, and settlement safety into the visit score. Buying food,
  recruitment, healing, selling loot, and selling prisoners therefore share
  one strategic settlement desire and execute through their existing campaign
  behaviors after arrival; they are not separate Grey Warden destinations.
- `GreyWardenPartyDesireBehavior` is now a final-registered allowlist filter.
  For managed Grey Warden parties it preserves every score generated by native
  `GoToSettlement`, `MoveToNearestLandOrPort`, `Hold`, and `None`. Native
  `PatrolAroundPoint` is preserved only when the party has no case or temporary
  police duty. Raid, siege, defence, army/join, arbitrary enemy pursuit, and all
  other kingdom-military candidates are set to zero. Native short-term fleeing,
  local power comparison, morale, and navigation initiative are outside
  `PartyThinkParams` and remain untouched.
- All police duties now enter the same auction at the single fallback score
  `1.0`. A healthy party with no stronger native need can pursue its case; a
  native settlement visit elevated by food, wounds, missing troops, loot, or
  prisoners can win without the mod detecting or estimating those needs. The
  case record remains assigned while the party visits town, so the same
  fallback candidate reappears after native needs subside. Assigned pursuit
  remains `GoAroundParty` before war and `EscortParty` after war, preserving the
  rule that speed cannot erase long-term tracking while native strength checks
  still decide engagement.
- Repeated requests for the same temporary intent now refresh only its expiry
  and do not force another rethink. The former every-hour rethink in the main
  case loop and sheltered-target loop was removed. Legacy AI unlocking and
  genuine task-state changes may request the next native hourly auction, but no
  code now calls `SetInitiative`, sets an aggressive destination, or clears a
  live native short-term flee/engage decision.
- `CrimePool.BeginTask` now enforces the assignment invariant at its data
  boundary: one police party has at most one task and a crime already assigned
  to another party cannot be assigned again. `PoliceMobilePartyAIModel` also
  rejects proactive Grey Warden attacks against parties other than the current
  case/temporary pursuit target or a bandit. This prevents an enforcement war
  against a faction from turning its unrelated lords, villagers, or caravans
  into extra police targets.
- Important economy diagnostic for the next in-game test: in native `v1.4.7`,
  `MobileParty.PartyTradeGold` returns `LeaderHero.Gold` for a lord party, while
  `Clan.Gold` returns only `Clan.Leader.Gold`. The clan-screen wealth category
  therefore does not prove that a non-leader Grey Warden commander personally
  has the `>100` denars required by the native food-visit scoring branch. No
  money was injected in this pass; if the party chooses town correctly but
  cannot buy food, inspect that individual commander's gold as a separate
  economy issue rather than raising the pursuit or resupply scores again.
- Final `Release --no-restore` build completed with `0` errors and only the
  offline NuGet vulnerability-metadata warning. ILSpy of the deployed DLL
  confirms fallback constant `1.0`, pursuit enum values `8`/`14`, the native
  allowlist (`0..2`, `13` only without a duty, and `17`), the attack-target
  gate, and the bidirectional task uniqueness check. It contains no resource
  threshold method or initiative reset.
- Live mirror verification compared `24` deployable files with `0` missing and
  `0` hash mismatches; all `17` XML files parsed; client/editor DLLs match; no
  editor-only directory or nested archive is present. Deployed DLL SHA-256:
  `5D7BF58A54C67020C937DF6BEBF9C98F107801D5439BB86643C946162C761BA7`.
  No commit, tag, archive, or release was created.


## 2026-07-20 Grey Warden case-ledger encyclopedia interface

- Added a second Grey-Warden-only button to the clan encyclopedia page. The
  existing war/adoption-details button remains unchanged; the new `案件总卷`
  button opens a dedicated modal Gauntlet overlay rather than placing an
  unbounded ledger inside the normal encyclopedia scroll area.
- `GwpCaseArchiveVM` reads `CrimePool.LedgerRecords` directly. This is the
  permanent, save-backed record collection and therefore includes both open
  and closed records. The ledger is intentionally one aggregate record per
  offender, not one row per individual criminal act. Rows are ordered by
  `LastCrimeTime` descending, then by record key for deterministic ties.
- Each row exposes the offender, open/closed state, latest offence time, current
  case-open time, latest offence type and victim, total recorded offences and
  arrests, saved map location, current assignee, and task stage. Assignment is
  resolved from `CrimePool.ActiveTasks` by `TargetCrimeId`, which reflects the
  same one-police/one-case relationship used by enforcement. Open records with
  no matching task are explicitly shown as `无人承办`; closed records remain
  visible and are marked as having no active tracker.
- The header summarizes total/open/assigned/unassigned/closed counts. A
  `刷新案卷` command rebuilds the view from live campaign state without closing
  the encyclopedia, allowing assignment and stage transitions to be observed
  during testing. The overlay is scrollable and blocks input to the underlying
  encyclopedia until closed.
- New runtime surface: `GUI/Prefabs/GwpCaseArchive.xml`; modified surface:
  `GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaClanPage.xml`.
  All new player text has English fallback strings and Simplified Chinese
  entries. Both player READMEs record the feature under `2026-07-20
  v1.4.7-r5-dev`.
- Validation: final `Release --no-restore` build completed with `0` errors and
  `46` existing nullable/offline-NuGet warnings. ILSpy of the deployed DLL
  confirms the clan command calls `GwpCaseArchiveScreen.Show`, and the ledger VM
  enumerates `CrimePool.LedgerRecords`, sorts by `LastCrimeTime`, joins current
  tasks by `TargetCrimeId`, and supplies the refresh command.
- Live deployment verification compared `25` deployable source files with `0`
  missing and `0` hash mismatches. All `18` repository and live XML files parse
  successfully; all `33` new localization references exist with no duplicate
  localization IDs. Client/editor DLLs are byte-identical, size `474624`,
  SHA-256
  `249E17C67A81D0E4EE3E448FB00ADE40BB05AA16223382AA39F99CFF367B37BB`.
  The live README matches the repository, the live case prefab matches its
  source, and the live module contains no editor-only directories or nested
  archives. No commit, tag, archive, or release was created.
- Focused in-game test: open Encyclopedia -> Clans -> Grey Wardens, confirm both
  top-right buttons appear, open `案件总卷`, verify newest-first ordering and
  scrolling, compare each open case's assignee with the corresponding map
  party, then leave the window open through an assignment change and press
  `刷新案卷`. After an arrest, confirm the record remains present but changes to
  `已结案`; create another offence for the same lord and confirm the same row is
  reopened and moved to the top rather than duplicated.


## 2026-07-20 case-save reload crash and ledger scrolling correction

- Player reproduction: start a new campaign, allow at least one AI case to be
  recorded, save and exit, then reload that save. The load raised an error
  before entering the campaign. The save itself completed successfully; this
  was a deterministic initialization-order crash during deserialization, not a
  corrupt or incomplete save write.
- Primary evidence is the local Windows Error Reporting dump
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.45916.dmp`.
  WinDbg `!analyze -v` reports `System.ArgumentNullException: Value cannot be
  null` with the managed stack `System.Linq.Enumerable.FirstOrDefault ->
  Hero.FindFirst -> CrimeRecord.get_OffenderHero -> CrimePool.Clean ->
  CrimePool.SyncData -> PoliceEnforcementBehavior.SyncData ->
  CampaignBehaviorDataStore.LoadBehaviorData -> Campaign.OnInitialize`.
  Independent dump
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.34736.dmp`
  contains the same exception and stack, ruling out a one-off load failure.
- Root cause: `CrimePool.SyncData` rebuilt the primitive case rows correctly,
  but immediately called `Clean()` while `Campaign.OnInitialize` was still
  constructing global campaign collections. A non-empty case ledger caused
  `CrimeRecord.OffenderHero` to call `Hero.FindFirst`; its internal enumerable
  source was still null at that exact phase. Empty ledgers did not enter the
  failing path, which is why the bug appeared only after cases existed.
- Correction: the loading branch now limits itself to reconstructing saved
  records and tasks. `CrimeState.Clean()` runs at the start of
  `OnSessionLaunched`, after Bannerlord has completed campaign initialization.
  `CrimeRecord.OffenderHero` additionally catches the specific early-load
  `ArgumentNullException` and leaves the lazy reference unresolved so the same
  lookup can succeed later. No keys or stored values were changed, so the
  player's already-created affected save should load directly after updating;
  a new campaign is not required.
- The first `GwpCaseArchive.xml` placed the list itself directly under the
  `ScrollablePanel` while using the surrounding region as its clip target.
  Although the first rows rendered, the scroll panel had no correctly linked
  internal scrolling canvas and therefore could not translate the list.
  Rebuilt it to match the native encyclopedia topology: the scroll panel owns
  `CaseListClip`, that clip owns the cover-children `CaseList`, and the panel
  references `InnerPanel="CaseListClip\CaseList"`, `ClipRect="CaseListClip"`,
  and its sibling vertical scrollbar. Mouse wheel and scrollbar movement now
  operate on the complete list rather than only the initially visible rows.
- Validation: final `Release --no-restore` build completed with `0` errors.
  ILSpy of the deployed DLL confirms `CrimePool.SyncData` no longer calls
  `Clean`, the lazy hero getter contains the `ArgumentNullException` guard, and
  `OnSessionLaunched` performs the deferred cleanup. Static prefab validation
  confirms the new inner-panel/clip/list relationship and cover-children list.
  All `18` repository and live XML files parse successfully.
- Live mirror verification compared `25` deployable files with `0` missing and
  `0` hash mismatches. Client and editor DLLs are byte-identical, size `474624`,
  SHA-256
  `D05FB25D38340D98280BB3D41F233750B58BAB6EE0C06F72EBC49F757597B87B`.
  Repository/live README and case-prefab hashes match; no editor-only directory
  or nested archive exists in the live module. No commit, tag, archive, or
  release was created.
- Required focused runtime retest: load the existing
  `Ironman3cJKGr9YHkab.sav` directly, confirm the campaign reaches the map and
  the saved case rows/assignees remain present, then save/reload it once more.
  Open the clan `案件总卷` with enough rows to overflow and verify both mouse
  wheel and dragging the right scrollbar can reach the oldest record.


## 2026-07-20 open-case-only ledger and numeric-history separation

- This section supersedes the earlier case-ledger description that treated
  `CrimeRecord` as a permanent combined row and instructed testers to expect a
  closed row to remain visible. The required model is now two independent
  stores: `_ledger` contains only current open cases and their event details;
  `_history` contains only per-hero cumulative crime/arrest counts and the
  numeric personal/clan deterrence state used by the hero encyclopedia.
- `CrimeRecord` no longer owns cumulative counts or deterrence fields.
  `HeroCrimeStats` deliberately has no crime type, occurrence time, last-crime
  time, map position, victim, offender-party ID, assignment, or open/closed
  flag. This prevents a closed case from surviving indirectly merely because
  its offender still needs a permanent numeric history.
- Closing an AI case through `CrimePool.EndTask`, removing an unassigned
  pending case, ending the player hunt, or cleaning a dead offender now removes
  the case row from `_ledger`. Failed enforcement remains recoverable:
  `PoliceTask` caches the current `CrimeRecord` object before removal and
  `ReopenCase` puts that same live case back into `_ledger` without increasing
  the crime count. Thus failure/reassignment does not lose an unresolved case,
  while genuine closure deletes all of its event details.
- Every detected AI crime increments `HeroCrimeStats.TotalCrimeCount`; a real
  Grey Warden arrest increments `TotalArrestCount` and updates deterrence in
  the same numeric record. The hero encyclopedia therefore keeps the numbers
  the user expects even after the current case row disappears.
- Save compatibility is explicit. Loading first reads the legacy `gwp_l_*`
  rows and extracts their old count/deterrence fields into `_history`, but adds
  only rows whose saved `open` flag is true to `_ledger`. It then reads the new
  `gwp_history_count` / `gwp_h_*` numeric store when present; those new rows
  replace the duplicated migration values. Consequently an old save keeps its
  accumulated numbers and open cases, silently discards closed-case details,
  and writes only the split format on its next save.
- `案件总卷` now filters to open cases, summarizes current/assigned/unassigned
  counts, and no longer displays closed state or cumulative history in each
  case row. The clan-page hint, English fallback strings, Simplified Chinese
  localization, player READMEs, and `docs/grey-warden-setting.md` all describe
  the same current-only behavior.
- Validation: final `Release --no-restore` build completed with `0` errors and
  `46` existing nullable/offline-NuGet warnings. ILSpy of the deployed DLL
  confirms `EndTask` removes non-player cases, `ReopenCase` re-inserts failed
  cases, legacy loading calls `MergeLegacyHistory` but admits only
  `HasOpenCase` rows, the new `gwp_history_count` / `gwp_h_*` keys are present,
  and `GwpCaseArchiveVM` contains no closed-case or cumulative-count display.
- Live mirror verification compared `25` deployable files with `0` missing and
  `0` hash mismatches; all `18` XML files parsed; all `31` case-screen/clan
  button localization references have Simplified Chinese entries with no
  duplicate IDs. Client/editor DLLs are byte-identical, size `476672`, SHA-256
  `EEF941E19D2F2C8F86D0A6790882A400D0C9DE6982822A03A654D296DEF0AD80`.
  Repository/live README files match; no editor-only directory or nested
  archive exists in the live module. No commit, tag, archive, or release was
  created.
- Focused in-game retest: load `Ironman3cJKGr9YHkab.sav`; confirm only its
  still-open cases appear and their assignees survive. Let one AI lord commit a
  new tracked offence, note the hero-page crime total, save/reload before
  resolution, then let the Grey Wardens arrest and close the case. After
  refreshing the ledger the row must disappear, while the same hero page keeps
  the crime total and increased arrest total. Save/reload once more and confirm
  the closed row does not return. Also let a pursuing Warden lose once and
  confirm that unresolved case is reopened/reassigned rather than deleted.


## 2026-07-20 assigned-case patrol suppression correction

- Player observation: the case ledger showed a Grey Warden as the assigned
  tracker, but the same map party still displayed/continued a patrol action.
  Raising the police fallback score would violate the native-first design and
  could again starve food, healing, recruitment, loot, or prisoner needs, so
  this was corrected at the allowlist and target-resolution boundaries rather
  than by increasing the `1.0` duty score.
- `ResolveIntent` previously searched `MobileParty.All` only by the case row's
  stored `OffenderPartyId`. A lord's party can be destroyed/recreated or change
  after captivity, leaving that ID stale even though `CrimeRecord.Offender`
  can resolve the hero's live party. The desire layer now uses the live
  `TargetCrime.Offender` resolver, which also refreshes the stored party ID.
- The native filter now treats `CrimePool.HasTask(party.StringId)` as an
  assigned duty even during the short interval in which a target party cannot
  be resolved. With a duty, `PatrolAroundPoint`, `Hold`, and `None` are all
  suppressed; `GoToSettlement` and `MoveToNearestLandOrPort` remain allowed,
  and native short-term flee/strength logic remains outside this filter. Thus a
  Warden may still buy food, recruit, heal, sell cargo/prisoners, or recover
  navigation, but cannot choose ordinary patrol/idle instead of an assigned
  case once those needs no longer win.
- The change does not force an immediate destination. Taking a case requests
  Bannerlord's next hourly rethink; the pursuit remains a native auction
  candidate rather than a direct movement order. A just-assigned party may
  therefore retain its old status until that hourly decision occurs, but it
  cannot select patrol again while the task exists.
- Player READMEs record the visible correction. Final `Release --no-restore`
  build completed with `0` errors and `46` existing nullable/offline-NuGet
  warnings. ILSpy of the deployed DLL confirms the independent `HasTask` gate
  and live `TargetCrime.Offender` resolution. Live verification found `25`
  deployable files and `0` hash mismatches; client/editor DLLs are identical,
  size `476160`, SHA-256
  `BD2A5E2BFA895031C002FD59D7F91C5C5154862C2E484C1CE1F7F62D08541F62`.
  No commit, tag, archive, or release was created.
- Focused retest: assign a currently patrolling Grey Warden in `案件总卷`, let
  one campaign hour pass, and confirm the party changes to approaching or
  following the offender. If it first visits a settlement for a real native
  need, allow that visit to complete and confirm it returns to the same case
  rather than resuming patrol. Repeat with an offender whose party was recently
  destroyed/recreated or released from captivity to verify live target
  resolution.


## 2026-07-20 native application-shutdown crash after successful save

- Player reproduction: choose save-and-exit, return to the initial menu, then
  close Bannerlord. Windows recorded
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.31280.dmp`
  at `2026-07-20 01:15:38 +10:00`.
- This was not a save failure. `rgl_log_31280.txt` reports the save starting at
  `01:15:30.856` and `Successfully saved` at `01:15:32.504`. The resulting
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\Game Saves\Ironman3cJKGr9YHkab.sav`
  is `5,342,092` bytes, timestamped `01:15:32`, with SHA-256
  `75AB58DF8AE0DDA8BD0D3911A511004FC2F38D1021D232BC2065DB4C3AF81EC2`.
- The crash occurred only during final native application teardown. The log
  had already returned to the initial menu, deleted the campaign game, deleted
  resources, completed `Pre Finalizing Managed Interface`, reported `There are
  no living managed objects`, and printed `Managed Interface deleted`.
  WinDbg then identifies an invalid-pointer read (`0xc0000005`) at
  `TaleWorlds.Native.dll+0x74b1f0`; the stack is native-only and contains no
  GreyWarden managed frame. Full local analysis is preserved at
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\crash-31280-analysis.txt`.
- This signature predates the current case-ledger and party-desire revisions.
  Archived WER report
  `C:\ProgramData\Microsoft\Windows\WER\ReportArchive\AppCrash_TaleWorlds.Mount_e978be8b478a106fd93641f8edccc0dc8de319ed_7140fc22_ba6e017d-275e-4c7e-b707-98f8e000306c\Report.wer`
  records the same fault module, exception code, and exact offset at
  `2026-07-18 07:03:29 +10:00`. Therefore the latest desire correction did not
  introduce this crash, and the evidence does not justify a speculative
  managed-state or serialization change.
- The observed timing does leave a practical native teardown race as the most
  useful next check: the player closed the application about `1.7` seconds
  after the initial menu activated and its background video began. Retest by
  loading the saved game, saving and exiting again, waiting at least ten
  seconds on the initial menu, and then closing the application. If it still
  crashes, retain the new dump and compare its native offset; the exact same
  offset would confirm the recurring shutdown fault, while a managed stack or
  different offset would be a separate issue.
- No gameplay/runtime source, README, build, or live module was changed for
  this diagnosis. This avoids claiming an unproven fix for a crash that occurs
  after Bannerlord has already destroyed the managed interface.


## 2026-07-20 staged long-range case approach and strict logistics priority

- Player observation superseding the first patrol-suppression correction: an
  assigned Grey Warden no longer selected ordinary patrol, but also did not
  begin travelling toward a far-away offender. The required priority order is
  now explicit: native food/recruitment/healing/sale/prisoner/ship needs first;
  case duty only when none of those needs owns a viable native candidate;
  ordinary patrol/idle is unavailable while a duty exists.
- Decompiled local `v1.4.7` source is preserved under
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\vanilla-ai`.
  `AiEngagePartyBehavior` searches only locatable parties around the acting
  party, using `EncounterModel.NeededMaximum*Distance... * 45` as its radius.
  It therefore cannot originate a normal cross-continent enemy pursuit.
  `AiPartyThinkBehavior` handles `PatrolAroundPoint`, `GoToSettlement`,
  `EscortParty`, and `GoAroundParty`, but has no winning-candidate branch for
  `GoToPoint` or `EngageParty`. The native hostile chase candidate produced by
  `AiEngagePartyBehavior` is in fact `GoAroundParty`.
- Long-range pre-war approach now inserts a point candidate at the offender's
  current known `CampaignVec2`, using `PatrolAroundPoint` solely as the native
  desire resolver's supported point-movement carrier. It does not rely on the
  offender being inside the acting party's locator/search radius. Navigation
  capability is resolved for land/naval travel. Once the existing enforcement
  layer observes the offender inside `WarDistance` and marks the task at war,
  the duty switches to `GoAroundParty`; the native short-term layer still owns
  strength, fleeing, and actual engagement.
- The point carrier is not ordinary patrol. Native
  `SetPartyAiAction.GetActionForPatrollingAroundPoint` compares the existing
  behavior and navigation type but does not compare the newly selected point,
  so a moving offender's refreshed location would otherwise be ignored after
  the first auction win. `GwpLocationDutyRefreshPatch` updates the point only
  when the location duty has already won the native auction and the known
  location moved by more than `0.25`; it never creates a destination outside
  that auction. `GwpLocationDutyBehaviorTextPatch` replaces the misleading
  stock patrol label with “travelling toward the last known position” for this
  state only. The Simplified Chinese localization is
  `gwp_location_duty_travelling`.
- Logistics priority is structural rather than another score adjustment.
  `HasNativeLogisticsNeed` recognizes the same observable categories used by
  `AiVisitSettlementBehavior`: low food days with purchasing money, materially
  wounded parties, prisoners, affordable vacancies for recruitment,
  meaningful sellable cargo, and damaged ships. While such a need exists,
  native `GoToSettlement` candidates remain intact. If any protected
  `GoToSettlement` or `MoveToNearestLandOrPort` candidate is present after
  filtering, the Grey Warden behavior adds no police candidate at all that
  hour; therefore neither the location approach nor hostile chase score can
  outrank the protected native action. Without a logistics need, routine
  settlement visits and ordinary `PatrolAroundPoint`/`Hold`/`None` candidates
  are suppressed so the assigned case becomes the true fallback.
- `EscortParty` remains only for actual escort duties. It is no longer used as
  hostile pursuit after war, avoiding friend-follow semantics on an enemy.
  Existing one-case-per-Warden ownership, automatic declaration range,
  strength-aware non-engagement, and support-party behavior are unchanged.
- Player-facing Chinese and English READMEs describe the staged approach,
  strict native logistics precedence, and corrected map status text.
- Validation: final `Release --no-restore` build completed with `0` errors and
  `46` existing nullable/offline-NuGet warnings. The build copied source module
  data and the assembly into both live client and editor module locations.
  All `18` repository XML files parse successfully. Live-mirror verification
  compared `25` deployable files with `0` missing and `0` hash mismatches.
  Client and editor DLLs are byte-identical, size `479744`, SHA-256
  `5745F65870DB34ECCBE9A688AFDB7A5123C3233B007002D550691771D93BD830`.
  ILSpy of the deployed DLL confirms the point candidate, protected-native
  early return, logistics detector, `GoAroundParty` war stage, and point-refresh
  patch. Repository/live Chinese README, English README, and Simplified Chinese
  localization hashes match. No commit, tag, archive, or release was created.
- Focused runtime retest: assign a case whose offender is on the far side of
  the continent. With a healthy, supplied, full-strength, low-cargo Warden,
  allow one native AI rethink and verify its map text becomes “正在前往…最后出现
  的位置” and its route advances toward the offender. Move the offender for
  several hours and verify the destination refreshes rather than stopping at
  the first snapshot. Then test separate Wardens with low food, recruitable
  vacancies, significant wounds, sellable loot/prisoners, or a damaged ship:
  each must finish the appropriate native settlement visit before resuming the
  same case. Finally allow a non-player offender inside `3` map units; verify
  war is declared and the Warden changes to continuous hostile chase.


## 2026-07-20 real point movement, assigned-only ledger, and AI score telemetry

- Player runtime observation disproved the previous point-carrier conclusion:
  the map label changed to the case-tracking text, but the party still did not
  travel toward the offender and continued the old patrol movement. The same
  session did not provide enough evidence to distinguish a missing native
  resupply candidate from a candidate that was later filtered or lost, so the
  resupply cause must now be decided from recorded scores rather than another
  speculative weight change.
- The exact movement cause is confirmed in the local `v1.4.7` decompile.
  `AiPartyThinkBehavior.PartyHourlyAiTick` has a winner branch for
  `PatrolAroundPoint`, but `MobileParty.RecalculateShortTermBehavior` has no
  matching `PatrolAroundPoint` branch. Calling
  `SetPartyAiAction.GetActionForPatrollingAroundPoint` can therefore set the
  displayed/default patrol state without creating the required point-to-point
  short-term movement. Updating the patrol point alone could never repair this.
- `GwpLocationDutyRefreshPatch` now translates only a winning Grey Warden
  approach carrier at the native action boundary. Its prefix replaces the
  stock point-patrol action with `SetMoveGoToPoint(position, navigationType)`
  and skips the stock call. It does not run while a settlement/logistics
  candidate wins and does not issue movement outside the completed native
  desire auction. The tracking status override now requires the resulting
  `GoToPoint` default behavior, so the UI can no longer claim location travel
  while the party remains in a patrol default state.
- `GwpAiDiagnostics` is diagnostic-only and does not enter save data or AI
  decisions. Every loaded campaign session resets
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`.
  For each managed Grey Warden party it records hourly state, raw native
  scores, filtered final scores, active police intent, whether logistics was
  protected, the police candidate added, the resolved default/short-term
  behavior, real target settlement/party/point, food, food change, calculated
  food days, party/leader/clan money, lord/patrol and faction eligibility,
  strength/recruitment ratio, wounds, prisoners, case ID, war stage, offender,
  and distance. The point-action translation also writes
  an explicit `POINT_WINNER_TO_GOTOPOINT` row.
- `tools\Watch-GreyWardenAI.ps1` tails that file while Bannerlord runs. Optional
  `-Party <id-or-name>` limits output, `-Once` prints a snapshot and exits, and
  `-Tail <count>` controls retained rows. PowerShell parser validation passes;
  before a campaign with this assembly is loaded, its expected `-Once` result
  is the explicit “diagnostic log does not exist yet” error.
- The Case Ledger now enumerates only `CrimePool.ActiveTasks`, resolves each
  task's current open `TargetCrime`, deduplicates by case ID, and sorts those
  assigned cases by latest offence time. Unassigned open records remain in the
  internal pool for future assignment but are intentionally hidden from this
  testing UI. Button hint, empty state, summary, Simplified Chinese strings,
  and both player READMEs were updated accordingly.
- Validation: `Release --no-restore` completed with `0` errors and `47`
  existing nullable/offline-NuGet warnings. ILSpy of the deployed assembly
  confirms `SetMoveGoToPoint`, assigned-task enumeration, and raw/final score
  logging. All `18` repository XML files parse. Live comparison found `25`
  deployable files and `0` missing or hash mismatches. Client/editor DLLs are
  byte-identical, size `488448`, SHA-256
  `790C93C2486A62EA854AF4893FB43661DD43D84253DCBA470FB47AF361437B47`.
  Repository/live Chinese README hashes both equal
  `CD905DD5BA4217C0DA10968F4FFA8A5877292BE320D8F31E7DE62E4A70017C20`;
  English README hashes both equal
  `67C2A1BC1C2F9E89DE4CF9D7497D10D0415E1B37D0A707F425CA3266032B263B`.
  No commit, tag, archive, or release was created.
- Focused runtime retest: load a campaign, identify one assigned case in the
  assigned-only ledger, and let at least one AI decision hour pass. A healthy
  party should produce an `AUCTION` row whose final top candidate is the
  approach carrier, followed by `ACTION ... POINT_WINNER_TO_GOTOPOINT` and a
  `RESOLVED` row with `default=GoToPoint`; its map position must then converge
  on `targetPoint`. Continue until food falls below the native threshold. The
  corresponding `AUCTION` row will show whether a raw `GoToSettlement` exists,
  whether `logisticsProtected=True`, and whether that candidate remains in
  `finalScores`; the next correction, if needed, must address the first stage
  where this chain actually breaks.


## 2026-07-20 confirmed desire-event ordering fault and final-auction hook

- The first instrumented runtime test produced decisive evidence rather than
  another ambiguous map label. The session log contains `75` `AUCTION` rows;
  all `75` report `rawScores=[]`, and the whole file contains `0`
  `POINT_WINNER_TO_GOTOPOINT` action rows. Every Grey Warden had a resolved
  `default=PatrolAroundPoint` and continued toward the same old patrol point
  around `town_EN5`, even though the early snapshot showed the correct offender
  coordinate as the sole police candidate. This proves that the police handler
  executed before the native score producers, then later native patrol scores
  overwrote its work before `AiPartyThinkBehavior` selected the winner.
- Local `v1.4.7` decompilation explains the ordering. `MbEvent<T1,T2>` inserts
  every `AddNonSerializedListener` at the head of a singly linked list and
  invokes from the head, so listener order is LIFO. Registering the Grey Warden
  behavior “last” therefore made its `AiHourlyTick` listener run first, not
  last. The prior registration-order assumption was false; consequently the
  filter never saw native food, recruitment, healing, sale, prisoner, patrol,
  or idle scores, and the diagnostic `finalScores` field was only a premature
  snapshot rather than the actual final auction.
- The behavior no longer subscribes directly to `AiHourlyTickEvent`.
  `GwpFinalDesireAuctionPatch` is a postfix on
  `CampaignEventDispatcher.AiHourlyTick`, which returns only after all event
  receivers and all native score listeners have completed. The postfix calls
  `GreyWardenPartyDesireBehavior.ProcessFinalDesires` immediately before
  control returns to `AiPartyThinkBehavior` and its winner loop. Raw diagnostics,
  identity filtering, protected native logistics, suppression of patrol/idle
  while assigned, and addition of the fallback police candidate now all operate
  on the genuinely complete score collection.
- This preserves the required priority model without a forced destination:
  native score producers still create their own settlement and survival
  desires; the Grey Warden layer removes forbidden kingdom/criminal activity,
  protects valid logistics candidates, and adds the case only as the fallback.
  If the final case point wins, the already deployed action-boundary patch then
  translates that winner to real `GoToPoint` movement. The next runtime log
  must therefore show non-empty `rawScores`, a filtered `finalScores`, followed
  by `ACTION ... POINT_WINNER_TO_GOTOPOINT` and `default=GoToPoint` for a
  supplied assigned Warden.
- On session launch, every currently managed Grey Warden party is marked for
  one immediate native rethink. This clears the practical test delay from an
  old save whose party still holds the previous patrol winner; it does not set
  a destination or bypass the corrected auction.
- Player README wording now states that the corrected filtering occurs after
  all native needs have been scored. This supersedes the earlier claim that
  adding the behavior last was sufficient.
- Validation: the final source-changing `Release --no-restore` build completed
  with `0` errors and `47` existing nullable/offline-NuGet warnings. ILSpy
  confirms the deployed assembly patches
  `CampaignEventDispatcher.AiHourlyTick`, calls `ProcessFinalDesires` from its
  postfix, and no longer subscribes that processor to `AiHourlyTickEvent`.
  All `18` XML files parse. Live comparison found `25` deployable files and
  `0` missing or hash mismatches. Client/editor DLLs are byte-identical, size
  `488960`, SHA-256
  `2A31B39F6510F37E2CCDD2C041FC35C4C5F5766E60D171E588828A9D791E15A4`.
  Repository/live Chinese README hashes both equal
  `B2477E6CDE86DA2CF331F5F32FE7C2DA1428F4CA364FC93DB7FF87E59C8E929F`;
  English README hashes both equal
  `2F826B365B8A40BD724F834D1E4F0F5AE5F4FC1DC45DEF88E057D1521D23DAB1`.
  No commit, tag, archive, or release was created.


## 2026-07-20 founder-only desire and treasury diagnostics

- The first successful final-auction runtime log confirms that native desires
  and the case fallback now coexist. Across `244` recorded auctions the
  diagnostic contains `27` `POINT_WINNER_TO_GOTOPOINT` actions; assigned
  Wardens also independently selected recruitment and settlement visits.
  Consequently the former all-patrol symptom is resolved rather than merely
  relabelled in the UI.
- The reported leader stop is reproducible in the existing log for
  `gw_leader_0_party_1` (梵蒂). At campaign hour `624935.38`, the open intent is
  still `Approach:lord_1_37_party_1`, but the native auction contains
  `GoToSettlement@castle_village_ES4_2=0.3840` and the same score for
  `village_ES1_4`. Because `logisticsProtected=True`, the assigned-case
  fallback is deliberately not added. Patrol candidates reach `3.0900` in the
  raw list but are correctly reduced to zero while the case is assigned. The
  selected village is already at the party's exact position, so the resolved
  map behavior remains `Hold` rather than issuing another movement order.
- This is a protected native settlement visit, not a return to patrol and not
  loss of the case. The same row shows `221/221` men, `0` wounded, `0`
  prisoners, `21.49` days of food, and `91,934` clan gold. Those values rule
  out low food, recruitment, healing, and prisoner delivery under the current
  protection predicate. The remaining possible predicates are sale of mounts
  or other cargo, or repair of a damaged ship; cargo sale is the most likely
  explanation, but the old log did not record the exact matched predicate and
  therefore cannot prove which of those remaining cases won.
- Diagnostics now deliberately include only parties led by the six founders
  `gw_leader_0` through `gw_leader_5`; later children, temporary patrols, and
  support parties no longer flood the trace. Every auction now includes an
  explicit `logisticsReason` value (`low_food`, `wounded`, `prisoners`,
  `recruitment`, `sell_mounts`, `sell_cargo`, `repair_ship`, or `none`). Party
  state retains `party/leader/clan` money separately; `clanGold` is the shared
  Grey Warden family treasury requested for economic observation.
- `tools\Watch-GreyWardenAI.ps1` now prints the founder-only scope and identifies
  `clanGold` as the family treasury before tailing the log. This turn changes
  diagnostics only; it does not alter AI scores, logistics thresholds, money,
  or player-visible gameplay, so no new player README entry was added.
- Validation: the `Release` build completed with `0` errors and `48` existing
  nullable/offline-NuGet warnings. The watcher parses successfully. Deployed
  assembly strings confirm the founder scope and exact logistics-reason
  instrumentation. All `18` repository XML files parse. Live comparison found
  `25` deployable files and `0` missing or hash mismatches. Client/editor DLLs
  are byte-identical, size `489472`, SHA-256
  `F3411665B4B1C1E62E1AFE4EB34A4944C803A86A3B2FB960DE771ED3FEBDE4C3`.
  No commit, tag, archive, or release was created.
- Focused retest: start or reload a campaign with this assembly and let the
  stopped founder reach the next six-hour auction. The new `AUCTION` row will
  state the exact `logisticsReason`. If the reason remains `sell_mounts` or
  `sell_cargo` while the founder repeatedly holds at the same village and the
  relevant inventory value never falls, the next gameplay correction should
  make only genuinely serviceable settlement visits protected, rather than
  weakening food, recruitment, healing, or case priorities globally.


## 2026-07-20 temporary-party economy audit and serviceable settlement filter

- The two one-use party types retain their dedicated lifecycles. Player
  pickets (`gwp_patrol_*`) continue to approach/pursue only the player, escort
  after victory when applicable, return after settlement or resolution, and
  are destroyed on arrival. Interception support parties (`gwp_enf_delay_*`)
  continue to pursue the recorded fast offender, mark themselves returning
  after their target/battle/war reason ends, request the recorded return town,
  and are destroyed within three map units or immediately after entering a
  settlement. The final-auction desire layer carries these existing explicit
  intents; founder-only diagnostics merely stopped logging them and did not
  remove their intents.
- Local Bannerlord `v1.4.7` decompilation confirms both temporary types use
  `CustomPartyComponent`, not `WarPartyComponent`. `DefaultClanFinanceModel`
  charges the leader party and entries in `Clan.WarPartyComponents`, caravans,
  and garrisons; it does not enumerate these leaderless custom parties. Their
  troop wages therefore do not reduce Grey Warden clan gold. Their independent
  party purse is now also explicitly initialized to zero for clarity and to
  prevent it from being mistaken for part of the family economy.
- The food side was not previously correct. `CustomPartyComponent` initializes
  only the troop roster, while `DefaultMobilePartyFoodConsumptionModel`
  considers these active, leaderless, non-bandit custom parties food-consuming.
  At the same time `AiVisitSettlementBehavior` rejects a leaderless party of
  the non-kingdom/non-minor Grey Warden faction, and
  `PartiesBuyFoodCampaignBehavior` requires a non-null leader. Thus a spawned
  picket/support party had no initial food and no native way to buy any.
- Both temporary types now receive twenty days of grain once, immediately
  after their roster is filled, calculated from the native base consumption of
  one food unit per twenty men per day. The provision is created for that
  one-use party and never charged to clan gold; unused food disappears with the
  party. Session launch also repairs an old-save temporary party only when its
  inventory contains no food, so repeatedly loading does not refill partially
  consumed rations.
- The stopped-founder hypothesis was corrected using the native source rather
  than retained as speculation. `PartiesSellLootCampaignBehavior` sells lord
  loot only when `settlement.IsTown`; it never sells at a village. At a town it
  sells only the amount covered by the town's current gold and does not contain
  any explicit “wait here until treasury refresh” state. The previous Grey
  Warden filter detected a cargo-sale need globally but then protected every
  native `GoToSettlement` candidate, allowing a generic village score
  (`0.3840`) to defeat the slightly lower town score even though the village
  could not perform the sale. This—not an empty village treasury—explains the
  observed hold at `castle_village_ES4_2`.
- Protected settlement candidates are now service-specific: food may select a
  town or village; recruitment a town or village but not a castle; healing and
  prisoner delivery require a fortification; loot/mount sale requires a town;
  ship repair requires a port. If the native auction contains no settlement
  able to satisfy the matched need, the assigned case fallback is restored in
  that auction instead of letting an unrelated settlement suppress police
  work. Player READMEs were updated with the temporary-party exception and the
  village-sale stall fix.
- Validation: the final `Release --no-restore` build completed with `0` errors
  and `47` existing nullable/offline-NuGet warnings. ILSpy of the deployed DLL
  confirms the twenty-day temporary provision, session repair, and
  `IsSettlementAbleToServeLogistics` filter. All `18` XML files parse. Live
  comparison found `25` deployable files and `0` missing or hash mismatches.
  Client/editor DLLs are byte-identical, size `489984`, SHA-256
  `0F8BEDE950A5BC05E0DBE45C611D1BD70B976D6AE6A900105CF9D282B95B85CF`.
  Repository/live Chinese README hashes both equal
  `3085FFE735F891DA461B110C8BA21DD7DCAF176B38209FBC41056F6A7465414F`;
  English README hashes both equal
  `2FDD40926E66BA444DE284251643346143D17192CF1679DAC240588E44ADE826`.
  No commit, tag, archive, or release was created.
- Focused retest: reload the current save. The leader's next auction should either
  preserve a town-only `GoToSettlement` candidate for `sell_mounts`/
  `sell_cargo`, or resume `ApproachPoint` if no serviceable town candidate
  survives. A newly spawned or old zero-food temporary party should show zero
  independent gold and approximately twenty days of food; it should retain its
  player/offender pursuit and existing return-destroy sequence.


## 2026-07-20 native-auction boundary correction: case outranks patrol only

- User correction: the mod must not classify Bannerlord's food, recruitment,
  trade, healing, prisoner, repair, safety, or other native desires and then
  decide which settlements are acceptable. The Grey Warden addition is only a
  persistent case duty whose score is slightly above the native patrol desire.
  The service-specific settlement filter described above was therefore an
  overreach and is superseded by this section.
- `GreyWardenPartyDesireBehavior` no longer contains
  `HasNativeLogisticsNeed`, `IsSettlementAbleToServeLogistics`, a settlement
  allowlist, or any manual cargo/food/wound/prisoner/recruitment/ship threshold.
  It also no longer suppresses `PatrolAroundPoint`, `Hold`, `None`, or any other
  native candidate. The final native score list is copied for diagnostics but
  every original tuple and score remains untouched.
- For a party with a current case or temporary duty, the layer reads only the
  highest positive native `PatrolAroundPoint` score. The added duty score is
  the next representable `float` above that patrol score. If no positive patrol
  candidate exists, the duty receives the minimal fallback `0.03`. Thus there
  is no possible score between patrol and duty: any original candidate truly
  above patrol is at least tied with the added duty and wins because the native
  tuple occurs earlier and Bannerlord replaces its winner only on a strictly
  greater score. `Hold` and `None` are not specially suppressed.
- Duty candidates are always appended with `AddBehaviorScore`. Even if a native
  producer already created an identical behavior and target, the mod does not
  call `SetBehaviorScore` or mutate that native tuple. Remote approach still
  uses a point-patrol carrier which is translated to real `GoToPoint` only if
  the added case candidate wins; declared-war pursuit remains native
  `GoAroundParty`, preserving native flee and strength decisions.
- Diagnostics now state `nativeScoresPreserved=True`, round-trip-precision
  `patrolCeiling`, the
  dynamically calculated `dutyScore`, and `dutyAdded`. A direct invariant check
  is possible: every entry in `rawScores` must remain in `finalScores` with the
  same behavior, target, and score; the only extra entry is the one duty
  candidate. The former logistics classifications and idle-suppression fields
  are absent because the mod no longer makes those decisions.
- The temporary-party economy result remains valid and independent: leaderless
  custom pickets and interception support parties remain outside clan wage
  accounting, keep zero independent gold, receive one fixed twenty-day ration
  at creation (or once for an old zero-food save), and retain their dedicated
  pursue/return/destroy intents. This is a spawn/lifecycle rule, not a
  replacement for native lord desires.
- Player READMEs now describe the exact boundary: case duty is above patrol,
  but no original candidate is removed, reduced, overwritten, reclassified, or
  routed to a mod-selected settlement.
- Validation: the final `Release --no-restore` build completed with `0` errors
  and `47` existing nullable/offline-NuGet warnings. ILSpy of the deployed
  assembly confirms `GetPatrolCeiling`, next-representable-float duty scoring,
  and `AddBehaviorScore`; it contains
  no `SetBehaviorScore`, idle suppression, or manual logistics/settlement
  filter in this behavior. Deployed diagnostics contain
  `nativeScoresPreserved=True`, `patrolCeiling`, and `dutyScore`. All `18`
  repository XML files parse. Live comparison found `25` deployable files and
  `0` missing or hash mismatches. Client/editor DLLs are byte-identical, size
  `487936`, SHA-256
  `8842261D15D436F30E4584C17A1FD84453CE24730EC5AD798079283CCDD1AFF4`.
  Repository/live Chinese README hashes both equal
  `0223C7F2E0CACEE7D01F8B45806E5073FCE662F9AD79D90E4D399BBC5C1B5BE2`;
  English README hashes both equal
  `9EFF8C86923C586F9703D0C427107912A1F3D7972197140E4BABC3FCC8556DAD`.
  No commit, tag, archive, or release was created.
- Focused retest: reload the current save and let one founder with an assigned
  case reach the next auction. The new row should show
  `nativeScoresPreserved=True`, a `patrolCeiling` equal to the greatest raw
  patrol score, a `dutyScore` just above that value, and an added case tuple.
  Every raw tuple must still appear unchanged in `finalScores`. If another
  native desire has a higher score, accept its target and action as Bannerlord's
  own decision; when only ordinary patrol would win, the founder must instead
  resume the same case.


## 2026-07-20 暮光连续办案与兵员流失实测诊断

- 本次运行日志包含 `1564` 行，其中暮光（`gw_leader_5_party_1`）有
  `287` 行状态、动作和竞价记录。她在战役小时 `625207.05` 结清
  `lord_1_41` 案件，`625208.06` 即被分配新案件
  `CharacterObject_1592`。代码侧原因与日志一致：
  `PoliceResourceManager.IsReady` 当前无条件返回 `true`，空闲警察扫描时
  只检查其没有现案，因而没有结案冷却、恢复阶段或兵力准备条件。
- 暮光并未挨饿或因无钱无法补粮。观察段内粮食天数约为 `39.26` 至
  `68.34` 天，部队钱袋为 `5000` 至 `6914`，家族金库由 `46918`
  降至 `28906`。因此本轮兵员下降不能归因于断粮，也没有证据表明
  补粮欲望被案件错误删除。
- 原版进城欲望确实存在，但没有胜出。暮光的最高原版巡逻分约
  `2.55` 至 `2.64`，案件候选按当前规则取巡逻分的下一个浮点数；而
  她的最高 `GoToSettlement` 通常只有 `0.38` 至 `0.85`，全段最高
  仅 `1.0167`。即使已有 `15` 至 `19` 名俘虏、兵力比降至约
  `0.29`，原版仍把这些进城候选排在巡逻之下。因此当前实现严格兑现
  “案件略高于巡逻”，但日志推翻了“补兵、交俘等原版需求必然高于
  巡逻”的前提：案件继承了巡逻的高基准，也就一并压过了这些较低的
  原版维护欲望。
- 暮光并非在该段反复领取多个领主案。`CharacterObject_1592` 从
  `625208.06` 一直持续到日志末尾。沿途看似“马上又去打别人”的行为
  来自原版短期交战：日志记录她先后以 `EngageParty` 攻击
  `deserters_1`、`deserters_91930`、`looters_91957` 和
  `looters_1862`。`GreyWardenPartyDesireBehavior.IsAuthorizedAttackTarget`
  当前对任何 `target.IsBandit` 都直接放行，所以所有灰袍领主都会在办案
  途中攻击野怪，而不仅是规划中负责逃兵或劫匪的专职角色。
- 兵员变化也与交战而非饥饿相符：新案接手前后由 `73` 降至 `63`；
  追踪途中多次与逃兵、劫匪交战后逐步降至 `50`。在
  `625309` 对案件目标宣战并进入持续 `EngageParty` 后，兵力由 `50`
  降至 `25`，伤员由 `0` 增至 `24`，目标距离固定约 `0.50`；这是正在
  结算的地图战斗所造成的连续伤亡。日志结束时战斗尚未结算，因而尚未
  观察到战后下一轮原版竞价。
- 结论：问题不是“原版欲望没有接入”，而是两项现行规则共同造成：
  （1）空闲即刻接新案，没有任何恢复窗口；（2）案件按最高巡逻分定价，
  而原版的补兵、交俘和一般进城分并不保证高于巡逻。此外，全部灰袍均
  可被短期 AI 分流去打野怪，加速了非专职角色的损耗。
- 后续修正需要先由设计层确定边界。若仍坚持完全保留原版候选，最小
  方案应围绕“结案后的原版恢复窗口”和“按岗位限制途中野怪交战”处理，
  而不是伪造粮食或直接强制进城。若继续让案件严格继承最高巡逻分，则
  必须接受原版中低于巡逻的招兵、交俘和一般进城欲望不会中断案件。


## 2026-07-20 接案期间仅压低巡逻欲望与家族资金流向核验

- 用户最终确定的欲望边界是不安排结案恢复流程，也不由模组判断何时补粮、
  招兵、疗伤、交易或选择哪个聚落。灰袍没有案件时不得介入原版欲望；接到
  案件后只压低 `PatrolAroundPoint`，案件追踪仅略高于压低后的巡逻，其余
  原版候选和分数原样竞争。该设计取代上一节提出但未实施的“恢复窗口”。
- `GreyWardenPartyDesireBehavior.ProcessFinalDesires` 现在先保留完整原版竞价
  快照；存在有效案件或临时职责时，仅将高于 `0.03` 的原版
  `PatrolAroundPoint` 候选封顶为 `0.03`。这个值不是自定的后勤阈值，而是
  Bannerlord 1.4.7 `AiPartyThinkBehavior` 对巡逻/进城行为使用的最低执行
  阈值。案件候选取该巡逻上限的下一个可表示 `float`，因此能作为真正可执行
  的保底职责，同时任何高于最低门槛的补给、招兵、疗伤、交易、交俘、安全
  等原版欲望都会自然排在案件前面。没有职责时不改任何候选，也不添加案件
  候选。
- 诊断字段相应改为 `originalPatrolCeiling`、`suppressedPatrolCeiling`、
  `suppressedPatrolCount`、`dutyScore` 和
  `nativeNonPatrolScoresPreserved=True`。状态行另增 `dailyWage`、
  `wageLimit` 与 `unpaidWages`，下一轮测试可以直接核对六支常驻部队每日
  工资、原版动态工资上限和欠薪比例。
- 对 2026-07-20 08:34 会话日志的资金复核覆盖战役小时 `625200.94` 至
  `625327.62`（约 `5.28` 天）。族长资金即家族资金，由 `46918` 降至
  `28906`；六名创始人的可支配现金合计约由 `76425` 降至 `59654`，净少
  `16771`。这不是隐藏的模组扣款。
- 原版 `DefaultClanFinanceModel` 已给出精确流向：灰袍为 6 级、无封地、
  非王国的 AI 家族，每日只有 `6 × 80 = 480` 第纳尔无地家族补贴；族长
  部队工资直接由族长/家族资金支付。其余领主部队先从各自
  `PartyTradeGold`（对领主队伍就是领主个人金钱）扣工资，若扣完低于原版
  下限 `5000`，家族资金再补回 `5000`。因此日志里约珥等人长期停在
  `5000` 并不代表没花工资，而是家族每天替他们填平工资缺口。
- 六次原版每日结算与日志完全对上：家族金库净变化依次为 `-3359`、
  `-2468`、`-4229`、`-2356`、`-1986`、`-2098`；加回每日 `480`
  补贴，说明金库当日承担的“族长队工资＋其他领主补回 5000 的转账”为
  `3839`、`2948`、`4709`、`2836`、`2466`、`2578`。这些数不是六队
  完整工资，因为其他领主高于 5000 的部分会先由个人账户支付。把同一结算
  前后六人的现金合计起来、抵消家族内部转账后，六队当日总工资约为
  `6002`、`4096`、`4932`、`4770`、`3134`、`3883`。也就是说当前常态
  被动收入只有 `480/日`，而六队工资约 `3100～6000/日`，仅靠被动收入
  必然持续亏损。
- 日结算以外的资金变化来自队伍自己的原版行动。族长凡蒂的队伍钱袋就是
  家族金库，因此她招兵、买粮或交易会直接改变 `clanGold`；日志在
  `625250` 附近资金减少 `437` 的同时增员 8 人、`625256` 附近减少
  `1074` 的同时增员 7 人，符合原版招募/采购。其他领主先使用个人钱；
  圣铎曾随俘虏增长收入 `2084`，暮光收入 `1914` 和 `472`，晨曦收入
  `7053`，说明战斗战利品收入确实进入各自钱袋，但不足以稳定覆盖六队工资。
- 当前轮只修正欲望边界和补充工资诊断，不新增补钱、免薪或被动收入。经济
  结论是后续经济系统的设计依据：若不希望家族最终破产，需要独立增加可感知
  的稳定收入或调整常驻队伍开支，而不是误判为原版补给欲望没有生效。
- Release `--no-restore` 构建通过，`0` 错误、`47` 个既有 nullable/离线
  NuGet 警告。部署后共核对 `25` 个 `_Module` 运行文件，缺失或哈希不一致
  为 `0`；`18` 个 XML 全部可解析。客户端与编辑器 DLL 字节一致，大小
  `488960`，SHA-256 为
  `FD34AE899A850E163F9A28EA61362619683EC372634680A4518F335ACD8BD8C6`。
  中英文 README 仓库/实机哈希分别一致，SHA-256 为
  `ECCFB00DE8B6FED8D5EBB0D63D2A0806DA8BF771D338EAC3AF9C0236E0963D41`
  与 `32C62FD9A7BE5E99EE621DF79CF5AE4354A3342EF94054F8527973F9A72B5D63`。
  ILSpy 对实机 DLL 的反编译确认存在仅限巡逻的 `SetBehaviorScore(...,
  0.03f)`、以 `Math.Max(0.03f, patrolCeiling)` 取下一浮点值，以及单独的
  `AddBehaviorScore` 案件候选。未创建提交、标签或发布包。
- 下一轮实测应在有案和无案的创始人之间对照：有案竞价应显示原始巡逻约
  `2～3`、压低后巡逻 `0.03`、案件为紧邻其上的浮点数；原版
  `GoToSettlement` 只要高于该值就先胜出。无案竞价的
  `suppressedPatrolCount` 必须为 `0`。同时使用新增的 `dailyWage`、
  `wageLimit`、`unpaidWages` 继续观察家族资金是否进入欠薪阶段。


## 2026-07-20 当前罚款去向与灰袍可用收入边界

- 代码核验确认，现有玩家罚款**不会进入灰袍领主个人钱袋或家族金库**。
  `PoliceResourceManager.CollectFine` 与 `CollectFineGoldOnly` 都只调用
  `Hero.MainHero.ChangeHeroGold(-goldTaken)`，没有对应的
  `GiveGoldAction`、灰袍领主 `ChangeHeroGold(+...)` 或
  `PartyTradeGold += ...`。因此金币在当前实现中被直接销毁。
- 直接向常驻灰袍领主认罚的路径在
  `PoliceEnforcementBehavior.Dialogue.OnEnforcementPayAcceptedConsequence`
  调用 `CollectFine`；谈判界面的 `GwpBribeBarterable.Apply` 是空实现，只
  用于表达接受条件，并不转账。战败押送后的领主罚款与临时纠察队罚款分别
  调用 `CollectFineGoldOnly`，结果同样只是扣除玩家金币。故“交给领主”和
  “交给巡逻队”当前没有经济去向上的区别。
- `CollectFine` 在金币不足时调用 `ConfiscateItems`，只从玩家
  `ItemRoster` 删除物品并把物品基础价值计入“已缴数”；物品没有加入承办
  领主、临时队或家族库存，也没有按估值给家族加钱，因而同样被销毁。
- 当前灰袍实际可获得的收入只有：（1）无地、非王国、6 级 AI 家族的原版
  `480/日` 基础补贴；（2）常驻领主打赢案件目标、劫匪或逃兵后获得的原版
  战利品、俘虏赎金及进城出售收入；（3）非族长领主个人钱袋超过 `10000`
  后，原版家族财务每日抽取“超出部分的十分之一”进入族长/家族金库。
  玩家悬赏奖励与村民声望奖励目前直接给玩家生成金币，也没有从灰袍金库
  扣款。
- 相比普通 NPC 家族，灰袍按既定限制不能依赖封地税收、村庄税、城镇关税、
  王国预算补助、统治家族收入、雇佣兵工资、贡金与战争协议收入，也没有已
  配置的工坊或商队；警察身份又排除了劫掠村庄、攻击村民/商队等掠夺收入。
  因此现有固定 `480/日` 与行动战利品无法稳定承担六至十五支常驻队伍。
- 尚未实施的推荐经济结构是统一使用“灰袍司法公库”（运行时可直接复用
  `Clan.Leader.Gold`，无需再造一套货币）：所有玩家罚金和罚没品拍卖价进入
  公库；成功结案获得商会、受害聚落或旧帝国治安契约的办案拨款；再由旧帝国
  治安公产/商路保护捐金提供稳定基础收入。若要求货币守恒，可从受保护城镇的
  `TradeTaxAccumulated` 提取极小比例，而不是凭空发固定人头工资；灰袍加入
  玩家国家后则可由玩家王国 `KingdomBudgetWallet` 承担国家警察拨款。该方案
  目前只是设计建议，未改代码、README、实机 DLL 或存档结构。


## 2026-07-20 司法公库罚款入账与结案归因核验

- 用户确认直接复用族长钱包作为“灰袍司法公库”，符合原版结构：
  `Clan.Gold` 本来就是 `Clan.Leader.Gold`，非族长领主的原版部队盈余也会
  逐步上交到这里。因此没有新增第二套金钱或存档字段。
- `PoliceResourceManager.CollectFine` 和 `CollectFineGoldOnly` 已改为通过
  `GiveGoldAction.ApplyBetweenCharacters` 把玩家实缴金币转入灰袍族长钱包。
  常驻领主与临时纠察队调用的是同一入口，临时队只代收、不持有罚款。金币
  不足时被 `ConfiscateItems` 移除的物品视为统一拍卖，其现有估值通过
  `CreditJudicialTreasury` 进入族长钱包。族长不可用的异常兜底仍只扣玩家，
  避免罚款流程因数据损坏而中断。
- 为未来“灰袍成功结案奖励 5000”核验现有结案路径时发现一个真实漏洞：
  `PoliceEnforcementBehavior.OnMapEventEnded` 过去只要求承办警察参加并赢得
  一场战斗；非玩家案件没有核对罪犯是否也参加，因此承办人途中打赢无关
  劫匪也可能错误结案。现已新增 `WasTaskOffenderInEvent`，按实时引用、案卷
  保存的罪犯部队 ID 和英雄 ID 三重核验参战方。只有罪犯与承办警察同场且
  承办警察在胜方，才属于常驻灰袍成功结案；承办警察失败则案件重新入池。
- 犯罪目标被完全无关的王国、领主或野怪击败时，承办警察不在该场战斗，
  不会进入上述成功分支；后续 `UpdateTasks` 发现目标失效后只清理案件，不应
  获得未来办案拨款。一次性追截支援队属于灰袍自身，其胜利由
  `DelayPatrolWonBattle` 与输家 ID 明确核验，未来可视为灰袍成功完成。当前
  只修复归因基础，尚未发放每案 `5000`。
- 关于案件保底分：`0.03` 不是任意接近零的值，而是 Bannerlord 1.4.7
  `AiPartyThinkBehavior` 对 `PatrolAroundPoint`/`GoToSettlement` 的最低执行
  阈值；降得更低会出现案件即使赢得竞价也难以真正换成行动。对 08:34 测试
  日志全部原版竞价重新统计，原版 `GoToSettlement` 的最低正分为 `0.0386`，
  共 `1918` 个候选中没有一个低于或等于 `0.03`；原版巡逻最低为 `1.5641`。
  因而该实测中案件 `0.030000...` 低于所有原版进城欲望，不会压过补给。
  但不能把一次存档的最小值当成全局定理，新诊断增加
  `minimumPositiveNonPatrolScore` 与 `nonPatrolAtOrBelowDutyCount`，可直接
  发现未来版本/场景是否出现低于案件的非巡逻欲望，而无需凭感觉改分。
- 村庄的 `Village.Hearth` 是“户数/人口与生产规模”，不是村民钱包。
  若按每户每日 `0.1` 计算后再从 `Hearth` 扣除同等数值，数学上等于每天
  消灭约 10% 的家庭，会迅速摧毁全大陆村庄。正确的守恒实现应当是用
  `Hearth × 0.1` 计算当日治安公产应缴额，但从每村原版已有的
  `Village.TradeTaxAccumulated`（村庄贸易税池）扣款并转入司法公库，且以
  税池现有余额为上限；`Hearth` 只作为计费基数，不应减少。该每日收入尚未
  实装，等待确认扣款载体后再加入，玩家统一后的国家拨款也未提前设计。
- Release `--no-restore` 构建通过，`0` 错误、`47` 个既有警告。部署后
  `25` 个 `_Module` 运行文件全部一致，`18` 个 XML 全部可解析；客户端与
  编辑器 DLL 字节一致，大小 `490496`，SHA-256 为
  `65635ADE224EA99683E0C444DA78C839E735D0F025024B85BFAB4E2F8B1C1EAD`。
  中英文 README 仓库/实机 SHA-256 分别为
  `57FFD966AE47721F8E760CB2A28410D885F9C9A89C1597BE593E5740DE0EF0A3`
  和 `AB73151FFAA4367717D2B7D000263F306F6E954177308F5DD2B330EE66B01C6A`，
  均完全一致。ILSpy 对实机 DLL 确认金币转账、罚没品公库入账、罪犯参战
  核验以及两个新增欲望诊断字段均存在。未提交 Git。


## 2026-07-20 司法公库稳定收入、胜案拨款与失败删案

- 用户最终确认司法公库的三项当前期收入规则：（1）常驻灰袍领主确实击败
  自己承办案件的目标，公库增加 `5000`；（2）追截支援队确实位于胜方并
  击败仍在案件池中的目标，公库增加 `5000`；（3）全大陆每个村庄每日按
  当日 `Village.Hearth` 每户一第纳尔缴纳保护费。灰袍战败、外部势力或
  野怪代为击败目标、目标自然失效、无案可结等路径均不得获得胜案拨款。
- `PoliceResourceManager.SuccessfulCaseReward` 固定为 `5000`，所有拨款只经
  `CreditSuccessfulCaseCompletion` 进入族长钱包所代表的司法公库。没有把
  奖励放入通用 `CrimePool.EndTask`，因为该入口同时用于战败、失效、取消和
  行政调度。常驻领主拨款只在 `OnMapEventEnded` 已通过
  `WasTaskOffenderInEvent` 三重参战核验且承办灰袍位于胜方后调用；玩家案件
  在灰袍取得本场胜利并开始押送时只计发一次，不在押送结束时重复计发。
- 追截支援队仍先由 `DelayPatrolWonBattle` 确认支援队属于胜方，再逐一核对
  败方部队。`ResolveTrackedOffenderDefeatByDelayPatrol` 现在只有在确实结束
  一项活动任务或删除一项未分派开放案件时才将 `resolvedCase` 置为真，并且
  每名罪犯/单一开放案件只拨款一次；支援队打赢无案目标不会凭空领款。
- 普通 AI 领主案件的执法失败语义已按用户要求改为“失败即从当前案件池删除”。
  `PoliceEnforcementBehavior.UpdateTasks` 在常驻部队失活、首领被俘/死亡时
  直接 `EndTask`，不再 `Reassign`；`OnMapEventEnded` 在承办灰袍战败或该
  部队于目标参战事件中消失时同样只 `EndTask`；`CrimePool.Clean` 清理消失
  承办部队时也改为 `EndTask`。案件的时间、地点、罪名等当前记录被删除，
  但 `HeroCrimeStats.TotalCrimeCount` 长期数字不回退，罪犯日后再次犯罪仍会
  生成一宗新案。玩家通缉使用单独的长期追捕记录，仍维持既有的战败后继续
  通缉规则，不把一次警察战败解释为清除玩家全部通缉状态。
- `ReopenCase` 没有被全局删除：玩家案件挤占普通案件、同一警察被明确改派、
  村庄救济等行政移交仍需要保留旧案。其注释已经明确它不再是战败默认路径，
  防止以后误把所有 `EndTask` 都恢复入池。
- `PoliceResourceManager.OnDailyTick` 新增
  `CollectDailyVillageProtectionContributions`：逐个读取 `Village.All` 的
  当前 `Hearth`，按 `floor(Hearth)` 累计公库收入，然后把该村户数改为
  `max(10, Hearth × 0.99)`。因此恰好 `100` 户的村庄本日贡献 `100`，结算
  后变为 `99` 户；小数户数按现值衰减，收入只取完整户数。保底 `10` 与原版
  `Village.DailyTick` 的户数下限一致。没有排除被劫掠村庄，也没有动
  `TradeTaxAccumulated`，因为用户明确要求全地图村庄直接以户数缴费并承担
  `1%` 户数损耗。
- 用户提出“有案时让巡逻分等于全部原版欲望中的最低值，再让案件分略高于
  最低值”。该方案本轮没有实装，因为 Bannerlord 的欲望拍卖选择**最高分**，
  不是只检查案件是否高于最低分。例如原版候选为 `0.04 / 0.40 / 0.80`，
  案件即使取 `0.040001`，仍会永远输给 `0.80`；动态跟随最低值只改变了
  巡逻和案件在队尾的顺序，不能保证案件获得执行机会。下一步若要同时避免
  “永久不办案”和“办案压死补给”，应采用会随等待时间逐渐提高、并在案件
  实际胜出后重置的职责紧迫度，或明确划分原版维护行动与办案行动的时间窗；
  在用户确认前不擅自重排原版补给、招兵、交易、疗伤和逃跑欲望。
- 最终 Release `--no-restore` 构建通过，`0` 错误、`47` 个既有 nullable/
  离线 NuGet 警告。自动部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希
  不一致为 `0`；`18` 个 XML 全部可解析。客户端与编辑器 DLL 字节一致，
  大小 `491008`，SHA-256 为
  `FD5FCF97B74544382BC6907B755B8A3FA3352CBF9C5E81FAD45C5491FC99341B`。
  中英文 README 仓库/实机哈希分别一致，SHA-256 为
  `71EEE85A11BC53B04A0A8E5594616F96AAF30BEAF1EB2A88BA8E257EE42D68FC`
  与 `9B1219306AE8FFA3F22A377C656B392874FAFCB3882188C7525729DB8B2D34CB`。
  ILSpy 对实机 DLL 确认存在 `SuccessfulCaseReward = 5000`、两条胜案拨款调用、
  `Village.All` 日结算及 `Hearth × 0.99`，并确认失败路径不再调用
  `Reassign`。`docs/grey-warden-setting.md` 的当前玩法章也已同步司法公库、
  原版欲望接入和失败删案现状。未创建 Git 提交。


## 2026-07-20 原版欲望分布复核与案件基准分建议

- 重新统计文档目录下 `GreyWarden-AI-Diagnostics.log` 的 2026-07-20 08:34
  会话，共有 `112` 次六名
  创始人原版欲望竞价。该日志产生于“案件继承最高巡逻分”的旧实现，不能
  直接代表当前 `0.03` 案件分的胜负结果，但其 `rawScores` 是未修改的原版
  候选，适合确定原版分数尺度。
- 原版 `PatrolAroundPoint` 共 `2157` 个候选，单项范围 `1.5641～3.09`；
  每次竞价的最高巡逻分中位数约 `2.5576`。这证明巡逻与后勤不在同一常用
  分数带，案件继承巡逻分必然长期压过普通进城需求；现行“有案只把巡逻压到
  `0.03`”的方向是正确的。
- 原版 `GoToSettlement` 共 `1918` 个候选，单项范围 `0.0386～19.5966`；
  每次竞价的最高进城分范围 `0.3505～19.5966`，中位数 `0.5376`，90 分位
  约 `2.0441`。全部 `112/112` 次竞价都至少有一个进城候选高于 `0.03`，
  因此当前案件若永久固定在紧邻 `0.03` 的分数，按最高分拍卖确实可能一次
  都无法胜出；让案件只略高于“最低原版欲望”同样无效。
- 日志显示原版进城分具有可用的自然分层：状态正常时最高进城分多在
  `0.35～0.85`；需要交付大量俘虏、恢复低编制或处理一般维护时会接近
  `1.0～2.0`；真正紧急的缺粮和大量伤员恢复会跃升到 `3.7～19.6`。
  具体例子包括梵蒂/约珥在只剩约 `4～5` 天粮时达到 `10.99～19.60`，弥瑟
  有 `24～40` 名伤员时达到 `3.72～13.12`。这些高分原版需求应继续无条件
  压过办案。
- 建议下一版先采用最小、可解释的固定案件基准，而不是立刻增加复杂状态机：
  有案时仍只把巡逻压到 `0.03`；案件候选取 `1.0` 的下一个可表示浮点值；
  其他全部原版候选不改。按本次日志回放，最高进城分大于 `1.0` 的竞价为
  `27/112`，这些时刻由原版维护行动胜出；其余 `85/112` 次由案件压过普通
  低分进城访问并获得执行机会。这个比例正好实现“原版需求强时先维护，原版
  需求低时去办案”，且不需要识别某个 `GoToSettlement` 究竟是在买粮、招兵、
  卖货、交俘还是疗伤。
- 初版不建议同时加入随时间无限增长的案件分。固定 `1.0` 已能利用原版分数
  自己判断维护强度；若后续实测仍出现某支部队因长期维持 `1.0～1.2` 而永不
  出警，再增加“连续等待若干小时后缓慢升至 `1.5`、案件实际胜出后归零”的
  有上限老化机制。先固定基准再观察，可以避免一次同时引入两个变量，也不会
  让案件最终上涨到压过 `3.7～19.6` 的饥荒和重伤恢复需求。


## 2026-07-20 案件固定中间权重实装

- 用户确认采用固定分界，但要求案件分“比一稍微低一点”。
  `GreyWardenPartyDesireBehavior` 新增 `AssignedDutyScore = 0.99f`；只要存在
  有效案件或临时警务职责，远距定点接近、宣战后追击、真实护送与职责性进城
  都统一以 `0.99` 加入最终原版欲望拍卖。旧调用传入的 `priority` 参数继续为
  兼容签名而保留，但不允许不同调用方重新放大职责分数。
- 有职责时，原版 `PatrolAroundPoint` 仍只被封顶到 `0.03`；除巡逻外的
  `rawScores` 不删除、不改分。低于 `0.99` 的普通定居点访问会输给案件；
  高于 `0.99` 的补给、招兵、交易、疗伤、交俘、修船及其他原版维护候选仍
  正常胜出。无职责时既不压巡逻，也不添加 `0.99` 候选。
- 已删除按压低后巡逻分取“下一个可表示浮点值”的 `GetDutyScore` 与
  `NextRepresentableFloat`。诊断日志继续输出 `originalPatrolCeiling`、
  `suppressedPatrolCeiling`、`dutyScore`、原版最低非巡逻分以及低于案件分的
  原版候选数量；新实测中有案部队应稳定显示 `dutyScore=0.99`，而不是
  `0.030000...` 或继承原巡逻的 `2～3`。
- 最终 Release 构建通过，`0` 错误；增量构建仅保留 `1` 个离线 NuGet
  漏洞元数据警告。自动部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希
  不一致为 `0`；`18` 个 XML 全部可解析。客户端与编辑器实机 DLL 字节一致，
  大小 `490496`，SHA-256 为
  `A8322799DFF6E1B0605CD643EFD2DAA2D03A7A2D2B5FB89B2099A2CB4193651B`。
  中文 README 仓库/实机 SHA-256 均为
  `AEBF00036CACA87CE8842B38F95C1F0C42027DD5E041F53910AB42AD29588E60`；
  英文 README 仓库/实机 SHA-256 均为
  `CCB9E98F82556EE70D4286A42D5F15F2355A353FAF779133D0E651AF6BCC7C2B`。
- ILSpy 对实机 DLL 的反编译确认存在 `AssignedDutyScore = 0.99f`、
  `AssignedPatrolScoreCeiling = 0.03f`、最终欲望竞价中的固定职责分和巡逻
  封顶，并确认旧 `GetDutyScore` / `NextRepresentableFloat` 已不存在。未创建
  Git 提交、标签或发布包。


## 2026-07-20 固定案件分实测：司法公库暴涨与圣铎驻村招募

- 最新诊断会话覆盖战役小时 `625327.84～625556.36`，约 `9.52` 个战役日。
  灰袍家族资金从 `28906` 增至 `882193`，净增 `853287`。日志中出现 `9`
  次约十万规模的日结算跳涨，观测增量合计 `886043`、平均约 `98449/日`；
  同期只有两次可明确识别的 `+5000` 胜案拨款。由此可确认暴富并非办案收入，
  而是当前 `floor(Village.Hearth)` 全大陆逐村日缴规则把全地图约十万户的
  户数几乎一比一转成了每日金币。六支常驻队最新工资合计约 `6070/日`，即使
  再计招兵、买粮和家族内部补款，也远低于约十万的保护费，因此公库必然快速
  变成巨富。每日跳涨由约 `105283` 逐步降至约 `95649`，也与每村同时执行
  `Hearth × 0.99` 的人口衰减相符。当前轮只完成诊断，尚未擅自更改用户此前
  确认的每户一第纳尔与每日衰减规则。
- 用户所称“圣泽”按日志对应创始人 `圣铎`。他从战役小时约 `625448` 起停在
  `castle_village_K8_1`（埃泽努尔）的精确地图坐标 `(719.985, 262.71)`；
  `CurrentSettlement` 虽显示为空、默认行为显示 `Hold`，但原版
  `MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation` 会把
  位于村庄图标上的队伍当作当前在村庄，原版 `RecruitmentCampaignBehavior`
  因而仍会每小时调用 `CheckRecruiting`。这不是灰袍冻结或无法进入聚落。
- 圣铎确实一直在招兵：驻留该村期间兵力由约 `78` 增至 `93`，最近状态为
  `93/约175`（`sizeRatio=0.531`）；个人钱袋随招募多次由 `5000` 下降到
  `4830/4660/4490/4320/4116`，并由原版家族财务在日结算时补回最低周转金。
  `foodDays=19.16`、`dailyWage=753`、`wageLimit=10000`、`unpaidWages=0`，
  家族资金又超过八十八万，所以饥饿、现金、工资预算和欠薪都没有禁止招募。
- 他没有一次拿走玩家界面中可见的全部兵，是原版 AI 招募的明确限制。原版
  `RecruitVolunteersFromNotable` 每小时对每名要人至多招一人，还要先通过随机
  起始槽位和随当前编制比例变化的随机判定。`DefaultVolunteerModel` 又按与
  要人的关系、聚落阵营和买方身份限制可用槽位：灰袍与普通异阵营村庄要人
  在零关系下通常只能使用前两个槽位，而玩家可能因关系、同阵营或难度奖励
  看到更多可招槽位。因此界面中后排仍有许多兵，不代表圣铎可以直接招走；
  他会消耗前两格的可用兵，然后在村庄等待这些格子按日刷新。
- 欲望日志也证明他是在主动选择补兵而非案件失效：驻村期间埃泽努尔的原版
  `GoToSettlement` 分长期约为 `1.78～3.31`，持续高于固定案件分 `0.99`，
  所以原版补员需求获胜并让他留在村庄；与此同时其兵力仍缓慢上升。这个结果
  正是当前“案件只压过普通巡逻和弱访问、不压过高分原版维护”的规则。若不
  希望领主为了补到高编制而在单一村庄停留多日，后续应单独决定是否接受原版
  慢速招募，不能把它误判为案件欲望没有落实。
- 本轮没有修改运行代码、README 或实机模组，也没有重新构建、部署或创建
  Git 提交；只增加了上述诊断结论与原版反编译依据。


## 2026-07-20 村庄保护费降档与灰袍—要人恒定满关系

- 用户根据上一轮实测确认将保护费从每户每日 `1` 第纳尔降为 `0.1`，但保留
  每村每日 `Hearth × 0.99`、最低 `10` 户的既有损耗规则。
  `PoliceResourceManager.CollectDailyVillageProtectionContributions` 现先以
  `double` 汇总全大陆 `Hearth × 0.1`，最后统一向下取整入司法公库；没有逐村
  向下取整，避免大量小数尾款因村庄数量被反复吞掉。按 10:05 诊断会话约
  十万总户数估算，新日收入应由约十万降到约一万。已有存档中已经进入公库的
  钱不倒扣，下一次日结算起才使用新费率。
- 新增 `GreyWardenNotableRelationsBehavior` 并注册为战役行为。读档、会话启动、
  每日结算会遍历灰袍家族全部存活成员与 `Settlement.All` 的全部存活要人，把
  基础关系设为 `100`；新要人生成、新灰袍成员生成和灰袍后继者成年时也会立即
  补齐。该状态不新增独立存档字段，直接使用原版 `CharacterRelationManager`
  的双英雄关系数据，所以旧档无需迁移结构。
- 为落实用户要求的“强制拉满、不会变化”，仅靠每日回填不够。新增两个窄范围
  Harmony 前缀：（1）任何 `CharacterRelationManager.SetHeroRelation` 写入若
  配对恰好是一名灰袍家族成员与一名 `Hero.IsNotable`，写入值强制改为 `100`；
  （2）`ChangeRelationAction.ApplyInternal` 遇到同一受保护配对时直接确认满值
  并截断动作，防止实际关系没下降却仍广播负关系通知。其他英雄关系、灰袍与
  普通领主关系、玩家与要人关系均不受影响。
- 原版 `DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero` 已复核：关系
  `100` 提供最高关系档，最终槽位数封顶为 `6`；即使灰袍与聚落不同阵营，乃至
  临时处于战争关系，关系加成在计算中仍足以抵消异阵营惩罚并保持六格。因而
  圣铎一类灰袍 AI 不会再因零关系只能使用前两格而长时间等待它们刷新。原版
  每小时招募随机判定、现金门槛、工资预算、编制上限及每名要人每次至多招一人
  等其余规则没有修改。
- 最终 Release `--no-restore` 增量构建通过，`0` 错误，仅有 `1` 个离线 NuGet
  漏洞元数据警告。部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希不一致
  为 `0`；`18` 个 XML 全部可解析。客户端与编辑器 DLL 字节一致，大小
  `493568`，SHA-256 为
  `5CDBB31CA637C5FAE4BFA19FAEC71C8A34D262DB85356FC4D8756D399BEF0650`。
  中文 README 仓库/实机 SHA-256 均为
  `BE6D1030C1CE3B837C2549A2B5B61CE048DDAB81C51E6D6ABE468364BA7ACDE1`；
  英文 README 仓库/实机 SHA-256 均为
  `F68AF5827337DA2160F415FA840AFB1E58063C3D5901CBADC957BBF9243AC905`。
- ILSpy 对实机 DLL 确认保护费使用 `Hearth × 0.1`、人口仍使用
  `Hearth × 0.99`，新关系行为已经注册，并存在关系写入与关系动作两个受保护
  前缀。中英文玩家日志与 `docs/grey-warden-setting.md` 已同步。未创建 Git
  提交、标签或发布包。


## 2026-07-20 无英雄临时执法队关闭欲望并单次直攻

- 用户明确要求无英雄临时部队不要持续接收移动命令，也不要像领主一样参与
  欲望思考。范围只包括无 `LeaderHero` 的临时纠察队与追截支援队，且只在
  已锁定敌对目标的 `Pursue` 阶段生效；常驻灰袍领主的固定 `0.99` 案件分、
  原版实力判断及全部非巡逻欲望均不改。
- `SetDirectAttackIntent` 现在首次锁定目标时只调用一次
  `SetMoveEngageParty`，随后设置 `SetDoNotMakeNewDecisions(true)`。任务每小时
  刷新时若目标未变，只延长运行时意图有效期，不再重复写入移动命令。
- `AiPartyThinkBehavior.PartyHourlyAiTick` 前缀在该直攻锁存在期间直接跳过整个
  小时思考回合，因此原版和模组评分器都不会为这支临时队生成巡逻、补给、
  逃避、实力衡量或其他候选，也不会让空竞价的后续解析覆盖首次攻击命令。
  小时维护只检查目标是否仍有效，不再重发攻击命令。
- 目标失效、任务清理或支援队转入返程时，`ReleaseDirectAttackLock` 会解除
  `DoNotMakeNewDecisions` 并允许下一小时重新思考；因此返回驻地和销毁流程仍
  可使用既有的访问意图，不会被战斗阶段的锁永久冻结。
- 最终 Release `--no-restore` 构建通过，`0` 错误、`47` 个既有 nullable/离线
  NuGet 警告。自动部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希不一致
  为 `0`；`18` 个 XML 全部可解析。客户端与编辑器 DLL 字节一致，大小
  `495104`，SHA-256 为
  `9AE957FF2E92984AB21F38DF0EF6760819D6012814046524A064FCA073880E08`。
  中文 README 仓库/实机 SHA-256 均为
  `E52B6AEDFAFD172E530E6B0A267FF32BE3B449868CAE7F3208EC410ABF5CEAF0`；
  英文 README 仓库/实机 SHA-256 均为
  `17BA5D8DF6F45940C5D1434D9D45815B6DA4E609F7DA393E4065B54666308A61`。
  实机 DLL 字符串核验存在 `HasDirectAttackLock` 与 `StartDirectAttack`，且旧的
  `ForceDirectAttack` 已不存在。未创建 Git 提交、标签或发布包。


## 2026-07-20 无英雄临时队接触后立即和平的 Git 对比诊断

- 用户实测新直攻锁后，追截支援队会冲上去接触目标，但没有持续完成交战，
  随即散开、和平并撤退。本轮按要求只对比和诊断，没有修改运行代码、README
  或实机模组，也没有重新构建部署。
- Git `6bd3871`（后续一直保留到当前基线 `a51f9d9`）中的无领主追截支援队
  完整战斗配置不是单独的 `SetMoveEngageParty`，而是连续执行：
  `SetDoNotMakeNewDecisions(true)`、`SetInitiative(1f, 0f, 999f)`、
  `SetMoveEngageParty(target)`。当前直攻层只恢复了冻结与 `EngageParty`，漏掉
  `SetInitiative`。因此当前并没有完整复原用户所说的旧版良好行为。
- Bannerlord 1.4.7 实机 `MobilePartyAi` 反编译确认，`SetInitiative` 三个参数
  分别写入 `_attackInitiative`、`_avoidInitiative` 与恢复时间。原版
  `DefaultMobilePartyAIModel` 的进攻评分直接乘 `AttackInitiative`，逃避评分与
  逃避距离直接使用 `AvoidInitiative`。旧值 `1f, 0f, 999f` 的含义正是保持正常
  进攻主动性、把逃避主动性降为零并长期维持；它不是欲望分，也不会恢复领主式
  补给、巡逻或战略思考。
- 第二个放大问题是既有战后收束仍然过宽：
  `HandleDelayPatrolBattleEnded` 只要看到任何追截支援队参与某次已结束的
  `MapEvent`，就无条件 `MarkDelayPatrolReturning`；随后
  `TryResolveDelayPatrolWarTargetImmediately` 在当前案由已经被战斗结算清掉时
  立刻 `SetNeutral` 并把同目标的支援队全部标记返程。该逻辑在旧 Git 中已经
  存在，但旧版的零逃避主动性使队伍更稳定地把接触推进到明确胜负；当前漏掉
  主动性配置后，一次短促或非决定性接触也会被这条无条件收束链解释成任务结束，
  于是玩家看到“撞一下、散开、立即和平”。
- 结论：当前症状不是欲望重新开启。最直接的回归是直攻层漏掉旧版
  `SetInitiative(1f, 0f, 999f)`；和平与撤退则由既有的“任何支援队战斗结束均
  返程”和“无剩余案由立即和平”继续完成。下一轮修复应先完整恢复旧版三件套，
  同时把支援队返程/和平限制为案件目标已经真正战败或任务确实失效，而不是仅凭
  任意一次 `MapEventEnded`。


## 2026-07-20 无英雄临时队持续完成战斗任务

- 按用户确认的最终 AI 边界实现：常驻灰袍领主仍只在有任务时把普通
  `PatrolAroundPoint` 封顶到 `0.03`，并添加固定 `0.99` 案件候选；其他全部
  原版欲望、分数、聚落选择和战力判断不改。无领主临时纠察/追截支援队则没有
  战略欲望和自主强弱判断，只执行明确任务。
- `StartDirectAttack` 恢复 Git `6bd3871` 的完整战术配置：先
  `SetInitiative(1f, 0f, 999f)`，再 `SetMoveEngageParty`，最后冻结新决策。
  这保持正常进攻主动性、把逃避主动性降为零，且不生成巡逻、补给或逃跑欲望。
- `HandleDelayPatrolBattleEnded` 不再见到任意 `MapEventEnded` 就无条件返程。
  现在只有目标部队失活或 `NumberOfHealthyMembers <= 0` 才视为真正战败；目标
  仍有可战人员时保留直攻锁，并只登记一次战后续攻。续攻会在地图事件完全清除
  后补发一次 `EngageParty`，不是每小时持续重发命令。
- 支援队胜利后的案件清理与 `5000` 办案拨款同样增加“目标真正战败”门槛；
  敌方撤退、脱离或非决定性交锋不会删案、发款或触发和平。常驻领主胜利结案
  也使用同一可战人员门槛，防止目标撤退却被错误认定为已抓获。
- `PoliceAntiWarDeclaration` 在战后恢复和平前新增现有合法战争理由核验。只要
  同一阵营仍有活动案件、悬赏或玩家纠察理由，就维持战争；最后一项理由真正
  消失后，原有自动和平流程才会执行。
- Release `--no-restore` 构建通过，`0` 错误、`47` 个既有 nullable/离线 NuGet
  警告。部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希不一致为 `0`；
  `18` 个 XML 全部可解析。客户端与编辑器 DLL 字节一致，大小 `496640`，
  SHA-256 为
  `6BFAD5823F18300C47FA08B5C663CA38049D44C5BC1003E61C41A49BA6D1A661`。
  中文 README 仓库/实机 SHA-256 均为
  `5A71E2D5FE85010F390EC4F1B953240B7B5ADD41F18FD562DEF8F06844A42E49`；
  英文 README 仓库/实机 SHA-256 均为
  `349EC09F359DCA4078015FF7F92A4DAE5A00DCB23914056A7B6071854F5B57F9`。
- ILSpy 对实机 DLL 确认直攻入口存在 `SetInitiative(1f, 0f, 999f)`、
  `DirectAttackRefreshPending` 与一次性战后续攻；案件和支援队胜利结算均检查
  `NumberOfHealthyMembers <= 0`；战后和平前存在 `HasLegitimateWarReason`
  守卫。源码同时复核领主侧仍只有 `AssignedDutyScore = 0.99f` 与
  `AssignedPatrolScoreCeiling = 0.03f` 两项欲望干预。未创建 Git 提交、标签
  或发布包。


## 2026-07-20 最新 AI 脚本输出与两类部队执行链复核

- 最新 `GreyWarden-AI-Diagnostics.log` 截止本地时间 `11:55:36`，大小约
  `8.6 MB`。日志中 `gwp_enf_delay_*`、`gwp_patrol_*`、`leader=-` 与
  `DIRECT_ATTACK_STATE` 均为 `0` 条，但这不能证明无领主队伍没有生成：
  `GwpAiDiagnostics.ShouldTraceFounderParty` 明确只允许六名创始英雄 ID 写入，
  所有无 `LeaderHero` 的临时队都会被日志过滤。现有脚本只能直接验证创始领主，
  不能观察临时纠察队或追截支援队。
- 玩家声望 `-1～-10` 时，每日检查若当前没有纠察队且不在贿赂保护期，会从
  玩家最近城镇生成一支 `gwp_patrol_*`。它无 `LeaderHero`，规模为声望绝对值
  乘当前 `PatrolSize=10`。敌对前用 `Approach` 接近玩家并触发谈判；玩家拒绝且
  宣战后才进入无欲望直攻锁。声望降至 `-11` 以下则撤回临时纠察队，改由正式
  灰袍领主接管通缉。
- 追截支援队不是每小时或每案立即生成。每两日检查一次已经进入
  `WarPursuit`、非玩家目标且战争仍有效的案件；按敌对阵营收集所有仍被追踪的
  罪犯部队，每个尚无活动支援队的罪犯生成一支 `gwp_enf_delay_*`。生成点优先
  取承办灰袍附近城镇，否则取目标附近城镇；固定 `50` 人，约六成重步兵、四成
  弓手，无 `LeaderHero`，保存来源案件、目标部队、敌对阵营与返程城镇后立即
  进入无欲望 `EngageParty`。
- 无领主队的边界按阶段不同：追截支援队的敌对执行阶段完全跳过小时思考，
  使用 `SetInitiative(1,0,999)`、单次 `EngageParty` 和决策冻结；非决定性战斗
  后只补发一次续攻。临时纠察队在尚未宣战、需要和平接触玩家时仍用 `Approach`
  欲望；两类队伍任务结束返程时当前仍用 `Visit` 职责候选，而不是直攻锁。
- 正式领主案件在 `WarDeclared=false` 时解析为 `Approach`：每次原版竞价以目标
  当时坐标生成一个地点候选，案件候选胜出后转成真实 `GoToPoint`。它不是永远
  固定的城镇或一次性旧坐标；后续竞价会读取目标的新位置。非玩家罪犯距离低于
  `WarDistance=3` 时，小时任务更新调用 `DeclareWar`，案件阶段切为 `Pursue`。
- 宣战后案件候选是目标部队上的 `GoAroundParty=0.99`，普通巡逻仍压到 `0.03`，
  其他原版候选完全保留。它的含义是持续锁定、跟随和拦截目标，不是强制
  `EngageParty`。原版短期主动性继续根据局部战力、士气、附近敌军和导航条件
  选择真正接战或 `FleeToPoint`。最新日志多次显示 `war=True`、长期行为
  `GoAroundParty`、短期行为 `FleeToPoint`，正好证明案件仍在而原版强弱判断
  也仍然生效。
- 晨曦的日志从 `war=False`、目标距离约 `2.37` 进入后续 `war=True`，符合小时
  检查在三格内宣战；日志快照发生在移动之后，所以下一条显示的距离可重新超过
  `3`。约珥在宣战后目标一度远至八十多格，仍保持 `GoAroundParty` 和相同案件，
  说明宣战后的目标引用不会因距离拉大而丢失。玩家目标和躲入定居点的罪犯另有
  对话/门口守候分支，不完全走上述非玩家野外自动宣战流程。


## 2026-07-20 AI 诊断扩展至全部无领主灰袍部队

- 用户要求现有脚本除六名创始领主外，也必须能观测所有灰袍无领主 AI。
  `GwpAiDiagnostics` 的过滤器由 `ShouldTraceFounderParty` 扩展为
  `ShouldTraceParty`：继续记录六名创始英雄，并记录所有 `LeaderHero == null`
  且属于灰袍家族/阵营、临时纠察队前缀或追截支援队前缀的活动部队。其他王国、
  野怪和非创始灰袍领主不会因此混入日志。
- 每行前缀新增 `partyKind`：`founder_lord`、`leaderless_picket`、
  `leaderless_delay_support` 或 `leaderless_grey_warden`。状态字段新增
  `dutyIntent` 与 `directAttackLock`；无领主队没有 `PoliceTask` 时，`offender`
  和 `offenderDistance` 会回退读取职责层的运行时目标，因此能直接看到临时队
  正在接近、直攻或返程的对象。
- `Watch-GreyWardenAI.ps1` 的说明已同步，并新增可选 `-Kind` 过滤。例如
  `-Kind leaderless_delay_support` 只观察追截支援队，`-Kind leaderless_picket`
  只观察临时纠察队；原有 `-Party`、`-Once` 和 `-Tail` 继续可用并可组合。
- Release `--no-restore` 构建通过，`0` 错误、`47` 个既有 nullable/离线 NuGet
  警告。部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希不一致为 `0`；
  PowerShell 对更新后的观察脚本解析成功。客户端与编辑器 DLL 字节一致，大小
  `497664`，SHA-256 为
  `2B4F6D20057D7158E91FBD90FBBD78AEA229A4B59E666470AB59111BFA59E67B`。
  ILSpy 对实机 DLL 确认 `ShouldTraceParty`、全部三种无领主 `partyKind`、
  `dutyIntent` 和 `directAttackLock` 均已部署。未创建 Git 提交、标签或发布包。


## 2026-07-20 双领主协办思路的原版战力核验

- 用户提出：承办领主宣战后若原版判断打不过，可临时调最近另一名灰袍领主
  前来共同进攻。该方向与原版 AI 机制兼容，但不能简单给两支队各自一个相同
  追击目标；若双方相距太远，它们会各自只用自身附近战力判断，并可能同时
  `FleeToPoint`。
- Bannerlord 1.4.7 实机 `DefaultMobilePartyAIModel.GetBestInitiativeBehavior`
  反编译确认，短期主动性先在约 `3 × GetEncounterJoiningRadius` 范围扫描相关
  部队，然后把同阵营、可参与同场事件的附近友军战力加入局部优势计算。因而
  第二名灰袍真正靠近主办领主后，原版确实会重新用合并后的附近战力比较敌方，
  并可能自然从逃避切换为 `EngageParty`；若目标是更大的军团，两人合力仍不足，
  原版继续不打也是正确结果。
- 建议实现为独立的“主办—协办”关系，不修改 `CrimePool` 的一案一主办约束。
  主办案件、结案归因和办案拨款保持唯一；协办领主通过临时外部职责以现有
  `0.99` 分跟随主办领主，原任务保留但暂停。进入原版共同参战范围后无需模组
  强制开战，由原版局部战力重算决定；主办案件结束、目标失效或协办失效时清除
  临时职责，协办自动恢复原任务。
- 触发不能只看一次 `FleeToPoint`，因为最新日志显示短期逃避目标有时是案件目标
  附近的另一支敌军。较稳妥的门槛应为：主办已经宣战、长期目标仍是案犯、处于
  接敌区域并连续若干小时由原版选择逃避；每案最多一名协办，避免六名领主全部
  被一个军团吸走或形成循环支援。协办选择应排除被俘、战斗中、押送玩家、悬赏
  护送和村庄救济中的部队，并优先最近的可用灰袍领主。
- 本轮只完成可行性与原版依据核验，没有实现双领主协办状态、没有改变玩家
  README，也没有为此再次构建部署。无领主诊断扩展是本轮唯一已部署的改动。


## 2026-07-20 六领主逐级协力任务与完整任务池

- 已实现案件绑定的领主协力机制。只观察六名创始领主的非玩家正式案件；必须
  同时满足案卷仍开放、承办关系仍指向当前领主、案件已进入 `WarPursuit`、目标
  部队仍有效且有健康兵员、双方仍实际交战、长期行为仍为该目标上的
  `GoAroundParty`，并在十二格内连续三个小时由原版选择逃避，才生成协力任务。
  单纯与同一国家交战、另一个案件尚未结束或旧目标残留均不能触发。
- 协力任务生成即强制分配给距主办人最近的可用创始领主，不进入普通等待领取
  流程。候选排除被俘、战斗中、玩家押送、悬赏护送、玩家案件和村庄救济；已经
  是协力主办人或协办成员的领主也不可被其他小队抢走。同一小时的竞争按稳定的
  创始部队 ID 顺序处理，先成功建立的关系立即占用双方，避免 A/B 循环求援。
- 协办人原有普通案件调用 `EndTask` 后立即 `ReopenCase`，完整退回公共案件池；
  不保存或恢复旧任务。协力结束后只解除 `EscortParty=0.99` 外部职责，该领主在
  同一小时后续正常调度中重新领取当时距离最近的未分配案件。
- 每个主办案件可形成一个多成员小队。已有成员必须全部进入主办人五格内，才会
  重新累计主办人的连续逃避；若集合后仍连续三个小时逃避，再逐次增加一名最近
  可用领主。成员只跟随主办人，不各自远程追击案犯；进入原版共同参战范围后，
  原版继续决定是否按合并附近战力接战。案件、目标、主办人或战争失效时整组
  解散，成员回到普通调度。
- 协力状态与每名成员的分配时间已加入存档。AI 诊断每行新增 `assistance`：可见
  `independent:blocked`、`leader:members/blocked/target` 或
  `member:leader/distance/target`；增援加入和释放另写
  `ASSISTANCE_ADDED/JOINED/RELEASED/DISBANDED` 动作行。协办人也被授权把来源
  案件目标作为合法短期攻击对象，但没有新增强制接战命令。
- 案件总卷改为完整任务池视图。普通开放案件、已分配普通任务和每名协办人的
  强制协力任务共同占用最多一百条容量并全部显示；所有已分配任务置顶，再列
  未分配案件，两组内部均按时间从新到旧。容量溢出时只移除最旧且无人承办的
  普通案件，不移除玩家通缉、已分配案件或协力任务。摘要显示总数、已分配数和
  等待数。
- 首次实现构建曾因案件总卷缺少 `System.Collections.Generic` 及
  `CampaignTime.Hours` 的 `double/float` 参数差异出现三个编译错误；补充引用并
  显式转为 `float` 后构建恢复通过。这是实现过程中的已解决错误，不是运行时
  缺陷。
- 最终 Release `--no-restore` 构建通过，零错误；新增协力存档代码的可空警告已
  收敛，剩余均为既有项目警告及离线 NuGet 漏洞源警告。构建自动同步 `_Module`
  后复核二十五个可部署源文件，实机目录缺失或哈希不一致均为零，仓库与实机
  `README.md` 哈希一致。客户端和编辑器 DLL 字节一致，大小 `514048`，SHA-256
  为 `46CA1EBF9B6C319B6175D7BB6325EF25D0502CCB0D0BBAE5CE30CE9073B4C3C8`。
  中文语言 XML、案件总卷 prefab XML 和观察脚本均通过解析；未创建 Git 提交、
  标签或发布包。


## 2026-07-20 完整任务池触发无领主支援队爆量的修复

- 用户实测发现地图上出现大量无领主灰袍支援队。读取最新
  `GreyWarden-AI-Diagnostics.log` 后确认，本次会话累计生成过 `77` 支
  `leaderless_delay_support`；生成呈严格的两日批次，最近七批分别一次生成
  `16、8、10、12、9、7、15` 支。最近一批在战役小时 `626720.68～626721.30`
  之间生成十五支，分别追击十五个不同目标，因此不是同一目标的重复状态日志，
  而是真实批量生成。
- 根因位于 `CheckPersistentWarTargetsEveryTwoDays`：它先从任意
  `WarPursuit` 任务得到敌对势力，再调用 `GetTrackedOffendersByFaction` 把该势力
  所有开放案件目标交给 `SpawnDelayPatrolsForOffenders`。旧案件池很小时问题不
  明显；案件总卷与池容量扩展到一百后，大量无人承办案件仍属于同一敌对势力，
  因而被另一宗已宣战案件错误带入生成范围。日志中最近批次的十五支各自对应
  不同 `CharacterObject_*` 或 `lord_*`，与该调用链完全一致。
- 生成口径已改为具体承办任务。`GetEligibleDelaySupportTasks` 只接受当前仍在
  `CrimeState.ActiveTasks`、案卷开放、目标有效且有健康兵员、非玩家、阶段为
  `WarPursuit`、战争仍有效的任务；每个任务只把自己的 `TargetCrime.Offender`
  交给生成器。无人承办的任务池案件不会再因同国另一宗案件宣战而生成支援。
  每个具体目标仍通过 `_delayPatrolStates` 保证最多一支活动支援队。
- `UpdateDelayPatrols` 每小时用相同资格集合复核现有支援队。旧版本批量生成、但
  已不对应一宗当前有效承办案件的队伍会标记 `Returning`，下一步
  `RequestVisit` 自动解除直攻锁并返城解散；因此旧存档无需重新开档或手工清队。
- 修复后同时活动的追截支援队数量只可能等于当前符合条件的正式领主
  `WarPursuit` 目标数。六名创始领主各自承办一案时理论上最多六支；正在执行
  协力任务的成员没有普通承办案件，因此实际通常低于六支。支援队战败而对应
  正式案件仍有效时，下一次两日检查仍可按原设计补发一支。
- 最终增量 Release 构建通过，零错误；输出只报告离线 NuGet 漏洞源警告。自动
  部署后复核二十五个可部署文件，实机缺失或哈希不一致均为零。客户端与编辑器
  DLL 均为 `514048` 字节，SHA-256 为
  `C4A6BF8E48EF5E92D7DB12B6A22E43F8D9494BE962E039C8E944785FB8309AC8`。


## 2026-07-20 无王国原版军团协力

- 用户否定了 `EscortParty=0.99` 的“多支队伍靠近后各自判断”方案，要求协办人
  真正加入主办人的原版军团；同时明确灰袍不会建国、领主数量不能限制为六名，
  未来收养成长的全部灰袍领主都必须纳入。
- Bannerlord 1.4.7 反编译确认 `Army(Kingdom, MobileParty, ArmyTypes)` 的
  `Kingdom` setter 对 `null` 安全，构造函数会把军团长写入 `LeaderParty.Army`；
  `member.Army = army` 会调用原版 `OnAddPartyInternal`，同家族的
  `CalculatePartyInfluenceCost` 明确返回零；原版 `AiArmyMemberBehavior` 随后为
  未合并成员生成高分 `EscortParty`，接触后 `Army.Tick` 或
  `AddPartyToMergedParties` 通过 `AttachedTo` 完成真实附着和同场参战。因此实现
  使用 `new Army(null, leader, ArmyTypes.Patrolling)`，没有创建隐藏王国，也没有
  自制军团对象。
- `PoliceEnforcementBehavior.Assistance` 已改为识别灰袍家族下所有有效
  `IsLordParty`，不再检查 `gw_leader_0..5`。触发条件仍是合法案件、有效目标、
  `WarPursuit`、实际战争、十二格内连续三小时由原版选择逃避。首次触发创建原版
  无王国军团，最近可用灰袍领主将旧案退回任务池后通过 `MobileParty.Army` 加入；
  到达原版军团接触距离后通过 `AddPartyToMergedParties` 附着。全员附着后若军团长
  再连续三小时逃避，则继续加入下一名最近可用领主，没有成员上限，直到没有
  可用领主。
- 军团长仍通过既有案件 `GoAroundParty=0.99` 参加最终欲望拍卖，其余原版补给、
  招兵、疗伤、交俘和安全需求不改；协办人不再获得模组自定义跟随欲望，只依赖
  原版军团成员行为。若一次原版进城行动曾为 Army 写入聚落目标，案件追击重新
  胜出时会清除该过期 `AiBehaviorObject`，避免军团每小时逻辑把短暂停留的军团长
  再拉回旧集合点。
- 案件目标真正被击败或主办方战败时立即
  `DisbandArmyAction.ApplyByObjectiveFinished`，其余案件/目标/战争失效由下一个
  小时清理执行相同原版解散；成员解除 `Army/AttachedTo` 后进入普通调度，按当时
  距离领取最近任务，不恢复被协力打断的旧任务。保存数据继续只记录主办、案件、
  目标、成员和分配时间；读档后若 Army 对象未随对象图恢复，会按这些 ID 重建
  原版无王国 Army 并重新加入成员。
- 原版 `Army.HourlyTick` 的 `CheckArmyDispersion` 会随机按“战争中是否存在有封地
  势力”解散军团；独立家族追捕无封地目标天然不满足。因此只对当前仍绑定合法
  协力组的无王国 Army 窄拦截 `NoActiveWar`。最初实现曾同时拦截
  `FoodProblem/CohesionDepleted/Inactivity/UnknownReason`，复核用户要求后确认范围
  过宽并已撤销：超过半数部队断粮、长期停滞、军团长失效以及原版 AI 主动取消
  均重新按原版解散。案件主动结束仍走原版 `ObjectiveFinished`。
- 原版同家族加入只保证影响力成本为零，并不会自动停止凝聚力变化；默认模型仍
  有基础日衰减和成员数量衰减。当前合法灰袍协力 Army 的
  `CalculateDailyCohesionChange` 结果被窄改为零，使同家族执法军团凝聚力保持现值，
  不需要花影响力维护；非协力军团的原版凝聚力公式完全不变。
- 收紧后的最终 Release `--no-restore` 构建通过，零错误，仅有离线 NuGet 漏洞源
  警告。自动部署后复核二十五个可部署文件，缺失与哈希不一致均为零，仓库与
  实机 README 一致。客户端和编辑器 DLL 字节一致，大小 `519168`，SHA-256 为
  `B80A43BB4F85FF2F090B76E8F1E9479E734042FF6A51183D3F78D20672525113`。


## 2026-07-20 三百余人军团仍不接战的日志诊断

- 最新 `GreyWarden-AI-Diagnostics.log` 显示弥瑟承办 `lord_1_5` 时先后召集暮光与
  约珥。军团建立早期 `armyMemberCount=3` 只表示三支部队已加入 Army，并不表示
  三支都已合并：弥瑟约一百五十余人、暮光一百二十余人已经 `AttachedTo`，约珥
  一百余人仍在三十至四十格外集合，因此当时短期判断实际可立即参战的是约二百
  八十人，而不是界面合计的三百八十余人。
- 约珥后来也成功附着；战役小时约 `626850～626860` 的军团状态为
  `members=2,attached=2`，三支合计约三百八十人。此时案件欲望没有被补给或其他
  欲望击败：最近可见的一次主办人拍卖中，模组加入的
  `GoAroundParty@lord_1_5=0.99` 排第一，最高原版进城候选仅约 `0.22`。问题不在
  长期欲望权重。
- 原版 `MobilePartyAi.GetGoAroundPartyBehavior` 的语义不是强制接战。只要它能在
  目标周围找到一个防守/环绕位置，就把长期 `GoAroundParty` 落实成短期
  `GoToPoint`；只有找不到该位置时才直接变成 `EngageParty`。全员附着后的日志
  正是 `default=GoAroundParty, short=GoToPoint, shortTarget=lord_1_5`，军团停在目标
  约 `5.95` 格处长时间不动。
- 原版短期主动性确实把已附着军团的 `Army.EstimatedStrength` 用作本方基础战力，
  因而不是“军团成员完全没有计入”。但它还会把扫描半径内同阵营敌军加入敌方
  局部战力，并用攻击/逃避主动性、距离、速度等共同决定是否用 `EngageParty`
  覆盖长期行为。军团形成前，弥瑟与已附着的暮光一直在逃避附近另一支敌军
  `lord_1_20`，而不是案件目标 `lord_1_5`，说明该区域存在额外敌方战力。全员
  附着后逃跑停止，却仍没有产生足以覆盖环绕行为的进攻分数。
- 当前协力升级条件只把近距离 `FleeToPoint` 认作“仍然打不了”。全员附着后短期
  行为变为 `GoToPoint`，`BlockedHours` 因此保持零，机制错误地认为阻塞已经解除，
  不会继续召集第四名领主。这是“三百余人停着但不继续加人”的直接逻辑缺口。
- 本轮仅完成日志与原版代码诊断，没有修改协力触发或追击行为，也没有重新构建
  部署。后续修正应至少把“全员附着、案件目标仍合法、近距离持续
  `GoAroundParty + GoToPoint/Hold` 且没有进入战斗”计为持续阻塞；若所有可用领主
  都已加入后仍保持该状态，再决定是否把最终阶段切换为一次原版
  `EngageParty`，以符合“全员集结后发动大战”的目标。
- 反编译还发现玩家与任何 Army 相遇时的 `army_encounter` 背景代码直接读取
  `Army.Kingdom.Culture`。已只对当前灰袍无王国执法军团使用原版
  `wait_fallback` 背景，避免玩家点击相遇菜单时空引用；普通王国军团菜单不改。
- 诊断范围从六名创始人扩展为所有灰袍领主及所有无领主灰袍部队。每行新增
  `armyLeader/armyMemberCount/attachedTo/armyKingdom`，协力摘要改为
  `armyLeader` 或 `armyMember` 并显示附着数量；动作日志使用
  `ASSISTANCE_ARMY_*`。案件总卷把任务类型显示为“强制军团协力”。
- 首轮与运行时保护增量 Release 构建均通过，零错误；最终构建仍只有既有可空
  警告和离线 NuGet 漏洞源警告。构建自动同步后复核二十五个可部署源文件，实机
  缺失与哈希不一致均为零；仓库和实机 `README.md` SHA-256 同为
  `0E5735F909F84F273E0719B04399EBF60F7E0CB1104D77DAA9F37375056F74ED`。
  客户端与编辑器 DLL 字节一致，大小 `519680`，SHA-256 为
  `576C165D2F1604E286759391AA2E706361F9A780A0AD89F313B36F56DC464312`；中文
  语言 XML 与案件总卷 prefab XML 均解析通过。未创建 Git 提交、标签或发布包。


## 2026-07-20 单承办人新档测试模式与攻城诊断增强

- 为隔离普通执法经济与调度过程，当前测试版把普通 AI 犯罪案件和玩家通缉案件
  都限定为灰袍家族当前族长的部队领取。其余灰袍领主不再从普通案件池接案，但
  仍是原版军团协力和村庄收养善后的合法强制候选；村庄善后候选主动排除族长，
  避免唯一普通承办人被收养任务占用。该规则没有限制后续成长领主的数量。
- 案件总卷的数据源补入村庄善后任务。等待分配的善后列在等待区；已分配任务与
  普通案件、协力任务一同置顶，并显示补给、赶路或驻村阶段及驻村剩余时间。
  普通案件、协力成员任务和收养善后共同占用一百条任务池/显示上限，新增强制
  任务时会优先裁掉最早且无人承办的普通案件。
- 为查明三百余人军团未加入攻城战的问题，AI 每行状态新增普通案件资格、任务
  流程、己方地图事件/攻城营地，以及罪犯的 active、健康兵员、战争状态、原版
  Army 军团长、AttachedTo、攻城营地领队、地图事件和双方是否处于同一事件。
  地图事件开始/结束还会输出事件类型、攻守方领队、聚落和全部参战部队 ID。
- 协力清理不再只留 `case_or_party_invalid` 这个笼统结果；解散前新增
  `ASSISTANCE_VALIDATION_FAILED`，逐项记录 task 是否存在、承办人是否匹配、
  WarDeclared、FlowState、案件是否开放、罪犯是否 active/有健康兵员、MapFaction、
  实际战争关系、案件 ID 是否匹配，以及目标当时的军团/攻城/地图事件状态。这样
  可直接判断攻城结算瞬间究竟是哪一项暂时失效。
- 对旧日志的进一步核对确认，`gw_leader_2_party_1` 的三支军团并非原版自行因
  粮食、凝聚力或无战争解散，而是模组小时清理在战役小时 `626860.52` 主动以
  `case_or_party_invalid` 解散。下一小时案件仍为 `lord_1_5`、`war=True` 且目标可
  解析，证明不是正常结案；高度疑似攻城/战斗结算切换中罪犯的 active、健康兵员、
  MapFaction 或战争关系短暂失效。旧日志没有逐项字段，需由本轮增强日志在新档
  复现后定案。
- 原版 `AiEngagePartyBehavior` 还明确跳过所有“在 Army 中但不是军团长”的敌方
  部队。因此若案件罪犯是攻城军团附属成员，长期 `GoAroundParty` 虽能跟到其
  周围，普通主动进攻扫描却不会把这个罪犯对象当成合法 `EngageParty` 目标；本轮
  新增的 `offenderArmyLeader/offenderAttachedTo/offenderSiegeLeader` 正是为了验证
  该高度可疑路径。本轮没有提前改写攻击对象或强制参战，先保留可验证证据。
- 最终 Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet
  警告。中文语言 XML 与案件总卷 prefab XML 均解析通过；自动部署后复核二十五个
  可部署源文件，实机缺失或哈希不一致均为零。客户端与编辑器 DLL 均为
  `530944` 字节，SHA-256 为
  `72B3F795E3CE71F8E4640645B422908AE3C158F6274D6D546BE776BCCDA398D4`；仓库与
  实机中文 README SHA-256 同为
  `1C6397837B0CAF5B9398BEE51D93530F4997E14B9A6CFDEE892618FD2391F4D8`。


## 2026-07-20 玩家案件与普通案件路径复核

- 单承办人规则已经覆盖玩家通缉的专门分配入口：声望达到重度通缉条件时，
  `EnsureNearestPoliceForWantedPlayer` 不再选择最近的任意灰袍领主，而只返回当前
  灰袍家族族长部队。因此玩家案件会由族长收到，其他领主仍只参与协力和收养。
- 玩家案件进入 `PoliceTask` 后，与普通案件共用 `GreyWardenPartyDesireBehavior`：
  宣战前是 `Approach` 定点候选，宣战后是 `Pursue/GoAroundParty`，固定任务分数和
  仅压制普通巡逻的规则相同；非巡逻的原版补给、招兵、疗伤、安全等欲望评分
  没有被删除，宣战后也继续由原版短期 AI 判断是否接战。
- 但当前玩家流程并非完全等同普通案件。专门分配入口会调用
  `PoliceResourceManager.CancelResupply` 并可通过 `TryAssignPlayerCrimeToPolice` 挤掉
  族长已有的普通案件；普通案件只在承办人 `IsReady` 且无任务时领取。玩家接触
  后不会自动宣战，而是先进入缴款/赎罪/拒捕对话，只有玩家选择拒捕才宣战。
- 玩家目标还被明确排除在无领主追截支援与领主军团协力之外：
  `GetEligibleDelaySupportTasks` 跳过 `offender.IsMainParty`，
  `GetValidAssistanceOffender` 也拒绝玩家目标。因此现状满足“不强制 AI 直接打
  玩家”，但若后续目标是把玩家案当作真正同级普通案件，还需单独决定是否取消
  分配时的补给中断/强制顶案，并明确玩家是否永远不获得协力支援。
- 本轮只复核并记录现状，没有修改玩家案件行为、构建或部署。


## 2026-07-20 恢复协力军团原版攻击欲望与玩家协力

- 原版 `AiEngagePartyBehavior.AiHourlyTick` 对“本队是 Army 军团长且 ArmyType 不是
  Defender”的部队直接 return。灰袍执法军团必须保持 `Patrolling` 才不会改变
  原版军团任务语义，因此没有把 Army 永久改成 Defender；新增 Harmony 窄补丁只
  在这一个原版攻击欲望评分器执行期间临时暴露为 Defender，并在 postfix/finalizer
  中立即恢复原类型。最终加入拍卖的敌军候选、战力比、速度、距离和分数仍全部由
  原版 `AiEngagePartyBehavior` 计算，模组没有写固定攻击分数或移动命令。
- `GoAroundParty=0.99` 仍是案件长期追踪候选。执法军团现在能同时获得原版主动
  攻击欲望：军团战力足够时原版攻击候选可胜出，不足时继续包围/跟踪；原版补给、
  招兵、疗伤和安全欲望不变。
- 原版主动攻击扫描跳过敌方 Army 的非军团长成员。协力攻击授权因此把案件罪犯
  的 `BesiegerCamp.LeaderParty`、`Army.LeaderParty` 或 `AttachedTo` 解析为同案的
  合法战斗入口；案件归属和结案核验仍保存原罪犯，不会把敌方军团长改成罪犯。
- 玩家案件保留“强制顶掉族长现有普通案件”的优先级，但删除玩家案分配/续期时
  的 `CancelResupply`。宣战前仍以 `Approach=0.99` 接近并进入缴款/赎罪/拒捕对话；
  玩家拒捕宣战后与普通案件相同使用 `GoAroundParty=0.99`，原版后勤和短期强弱
  判断继续介入。
- `GetValidAssistanceOffender` 删除玩家目标排除。玩家拒捕且正式进入
  `WarPursuit` 后，族长在近距离连续由原版逃避时可建立同一套无王国原版军团并
  逐步召集其他灰袍领主。无领主追截支援仍排除玩家，避免对玩家使用“无欲望、
  直接攻击”的临时队伍逻辑；玩家协力只有保留原版判断的领主参与。
- 最终 Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet
  警告。ILSpy 反编译实机 DLL 确认 `AiEngagePartyBehavior.AiHourlyTick` 前缀只对
  当前合法协力军团暂时写入 `ArmyTypes.Defender`，postfix/finalizer 均恢复保存的
  原 ArmyType。自动部署后复核二十五个可部署源文件，实机缺失或哈希不一致均为
  零；客户端与编辑器 DLL 均为 `531968` 字节，SHA-256 为
  `A28BC3799E8433BF53FA7DFD81BA8B83D9CB641CF808C4B041C594ADE2285C06`。仓库与实机
  中文 README SHA-256 同为
  `2BFF217D592E1E606E800C59F6285F18F0FB20CB1DC2688FED90F4B4F8A78B16`。


## 2026-07-20 新档实测：原版接战与连续无领主支援

- 用户实测观察到灰袍军团在认为战力足够后参与战斗。日志对应案件为族长梵蒂
  承办的 `lord_1_52`，处于 `WarPursuit`，长期案件候选始终保持
  `GoAroundParty@lord_1_52=0.99`；最高可见原版进城欲望约 `0.38`，未覆盖案件。
- 战役小时 `624745.56`，梵蒂建立无王国原版军团并召集约珥：
  `armyMemberCount=2`、`members=1`。但直到案件结束始终是 `attached=0`，约珥仍在
  远方赶来，因此最终战斗没有约珥参战。原版 Army 总人数显示与实际立即可参战
  队伍需要继续区分；下一次完整集结测试应确认 `attached=1` 后是否由军团自身
  发起战斗。
- 同一案件先后只存在一支活跃无领主支援，而不是并发批量生成：
  `gwp_enf_delay_73116` 于 `624729.20` 出发并在 `624746.74` 战败；
  `gwp_enf_delay_86856` 于 `624777.19` 出发并在 `624799.83` 战败；案件和目标仍
  有效后，`gwp_enf_delay_63725` 于 `624825.21` 再次出发。该序列符合“每案一支，
  被消灭后经过下一轮两日检查再补发”的设计，没有发现同一时刻超额刷队。
- 三支临时队在任务有效期间全部保持 `directAttackLock=True`、
  `default=EngageParty`、`short=EngageParty`，没有产生战略欲望或中途改向。前两支
  被包含多支帝国领主的敌方集团击败；第三支在 `624839.62` 左右触发最终
  FieldBattle，梵蒂随后从 `GoAroundParty + FleeToPoint` 切到
  `GoAroundParty + EngageParty` 并加入同一地图事件。
- 最终地图事件参战方明确为
  `gwp_enf_delay_63725, gw_leader_0_party_1, lord_1_52_party_1`，结果为
  `DefenderVictory`。案件目标健康兵员在战斗中逐步降至零；梵蒂从约一百九十名
  健康兵下降到一百六十五名并产生二十四名伤员，临时支援队剩三十名健康、
  十六名伤员，证明两支灰袍确实共同参加并完成战斗，而不是只在地图旁观。
- 战斗结束后案件被正常结案，下一次协力清理记录
  `taskExists=False/caseOpen=False`，随后释放约珥并解散军团。这次
  `ASSISTANCE_VALIDATION_FAILED` 是任务已成功结束后的预期清理，不是此前攻城
  切换中“案件仍在却瞬时无效”的异常。支援队则解除直攻并转为前往城镇收尾。
- 本轮只读取并归档实测证据，没有修改代码、构建或部署。结论是当前“持续锁案、
  无领主支援逐次消耗、附近领主加入最终战斗、胜利后结案解散”的闭环已经成立；
  尚待独立验证的是所有协力领主完成 AttachedTo 后，完整军团在没有无领主支援
  先开战的情况下能否自行产生原版攻击并发起战斗。


## 2026-07-20 外交和平回退与全领主恢复接案

- 确认存在真实软卡死路径：`PoliceTask.WarDeclared` 是独立保存字段，原版外交或
  其他系统恢复和平时不会自动清零。欲望层仍会按该字段产生
  `Pursue/GoAroundParty=0.99`，但原版攻击欲望不会攻击中立目标；协力和无领主
  支援又要求实际战争，因此案件可能永久停在目标周围。
- 按用户要求只增加小时一致性检查，没有添加 MakePeace 事件监听。每小时在更新
  支援与协力前检查所有 `WarDeclared=true` 且具有正式 `WarTarget` 的案件；若灰袍
  与罪犯当前实际势力已非战争状态，则把 `WarDeclared=false`、`WarTarget=null`，
  标记该案无领主支援返程，解散该案协力军团，并要求承办领主下一次重新拍卖。
  野怪案件没有正式 WarTarget，不参与该检查，仍保留其原有直接敌对语义。
- 回退不删除案件。随后同一个小时的既有 `UpdateTasks` 自然接管：距离外恢复
  `Approach=0.99`；普通罪犯仍在宣战距离内时可按原规则重新宣战；玩家案件重新
  接近并走缴款、赎罪或拒捕对话。日志新增 `CASE_WAR_STATE_RESET`，记录旧战争
  目标、罪犯当前势力和回退原因。
- 删除单承办人测试限制。`AssignTasks` 再次遍历灰袍家族全部有效领主，排除已有
  案件、协力、收养善后、失效首领和资源未就绪者后，为每人领取距离最近的无人
  承办案件。玩家重度通缉也恢复为从全部可用领主中选择离玩家最近的一支，并仍
  保留强制顶掉该领主普通案件的玩家优先级。
- 村庄收养善后候选不再排除族长；所有有效灰袍领主与未来成长领主重新处于同一
  普通案件、协力和收养调度体系。诊断字段 `ordinaryCaseEligible` 现在对全部符合
  条件的灰袍领主为 true，而不再只标记族长。
- 最终 Release `--no-restore` 构建通过，零错误；增量构建只报告离线 NuGet 漏洞
  数据源警告。中文语言 XML 与案件总卷 prefab XML 均解析通过；自动部署后复核
  二十五个可部署源文件，实机缺失或哈希不一致均为零。客户端与编辑器 DLL 均为
  `532480` 字节，SHA-256 为
  `4C1DF820D92E535A2A807D232D9BA3521593616E32615A2E96AEA9D9E97B7FDE`；仓库与实机
  中文 README SHA-256 同为
  `D61B4B3EBC07F46C4282447F62B19DFF56BB7CF18A60D484421602AF0DC6F81B`。


## 2026-07-20 协力任务改为完全绑定主办案件生命周期

- 明确拆分“协力创建资格”和“协力成立后的存续资格”。创建前仍要求主办领主处于
  合法战时追捕、目标可战、实际外交为战争，并连续出现原版近距离避战；这些条件
  只负责证明此刻需要新增协办人，不再重复用于判断已接下的协力任务是否有效。
- 已成立协力组现在只核验三项稳定身份：主办人的 `PoliceTask` 仍存在、任务仍归属
  同一主办部队、`TargetCrimeId` 仍等于协力组保存的 `CrimeId`。暂时和平、
  `WarDeclared=false`、流程退回接近、罪犯短暂失活、健康兵员瞬时归零、势力变化或
  地图事件结算均不会再强制释放协办人或解散军团。
- `UpdateLordAssistance` 先维护已经存在的协力组，再按严格条件解析当前可追捕目标。
  目标暂时不可用于追捕时只清零本轮受阻计时并保留原版 Army；案件重新宣战并回到
  `WarPursuit` 后，既有成员直接继续随主办人行动。只有尚未建立协力组的领主才走
  原有严格触发判断。
- 小时外交一致性检查仍会把不一致案件的 `WarDeclared` 与 `WarTarget` 清零，并让
  无领主追截支援返程，但不再调用 `ReleaseAssistanceGroup`。因此同一案件可以在
  原版外交恢复和平后保留领主军团，等待现有案件流程重新宣战或重新完成玩家执法
  对话。
- 原版 Army 因其自身规则暂时消失时，不再把“Army 当前不可用”解释为协力任务
  失败；只要主办案件仍是同一案件，系统会保留协力记录，并在后续小时重新恢复该
  无王国原版军团。真正 `EndTask`、任务换案或任务归属变化仍会输出
  `ASSISTANCE_CASE_ENDED`，随后释放全部协办人并解散属于该组的军团。
- Release `--no-restore` 最终构建通过，零错误、四十四条既有可空/离线 NuGet
  警告。十八个仓库 XML 全部解析通过；自动部署后核对二十五个可部署文件，实机
  缺失与哈希不一致均为零。客户端与编辑器 DLL 字节一致，大小 `533504`，
  SHA-256 为
  `73A8D5975EA3B93C8492A6C8BF0A6CD1DE53FFAF89BF2A1B443123F47F1FABD5`。
  仓库与实机中文 README SHA-256 均为
  `A022BFB323354A3869FC63EAB791936EDA395E3E27CDC657F10A6B023CFEDDEE`；英文
  README SHA-256 均为
  `FF5BAF7D70D3F1FA4C2405A87A5EDAA4B9620D198173BADFBD3ACC3D49689BBB`。


## 2026-07-20 梵蒂在攻城结束后的城外对峙诊断

- 本轮只读取 `GreyWarden-AI-Diagnostics.log`，没有修改运行代码。梵蒂承办
  `lord_5_17`，案件一直保持 `WarPursuit/war=True`，长期候选为
  `GoAroundParty@lord_5_17_party_1=0.99`。目标在攻城期间属于
  `lord_5_20_party_1` 军团并附着于军团长；梵蒂的短期行为长期是以敌方军团长或
  邻近成员为目标的 `FleeToPoint/FleeToParty`，与玩家看到的城外折返一致。
- 梵蒂在战役小时 `624868.54` 召集约珥，在 `624947.50` 召集晨曦。第二次召集
  发生在攻城仍未结束时，而不是城池攻下之后。攻城于 `624962.52` 以进攻方胜利
  结束；之后案件罪犯的 `armyLeader/attachedTo` 从 `lord_5_20_party_1` 变为 `-`，
  证明敌方原军团已经解散。
- 最新状态约 `625080.62`：梵蒂的协力摘要为
  `members=2,attached=1,blocked=0`，Army 内显示三支部队；约珥已真正附着于梵蒂，
  晨曦仍距军团长约 `51.78`，正在以 `EscortParty` 赶来。因此地图上看见的增援是
  已经下达的第二个协力任务仍在执行，并非当前每小时继续新增协办人。
- 梵蒂当前仍在目标约 `8.8～9.1` 距离外反复于 `FleeToPoint`、`GoToPoint` 和短暂
  `None` 之间切换。最新欲望拍卖只有案件的 `GoAroundParty=0.99` 与若干原版进城
  候选，没有生成 `EngageParty` 候选。这证明卡点不是案件欲望被其他欲望覆盖，而是
  原版当前没有给城内/受聚落保护的目标生成可执行的野战攻击；灰袍逻辑本身也没有
  攻城任务，因此只能继续在城外跟踪。
- 现有诊断没有记录罪犯的 `CurrentSettlement`，也不记录每支敌方领主自己的欲望
  拍卖，所以不能仅凭日志严格证明“所有敌方领主均因灰袍更强而拒绝出城”。但攻城
  胜利、敌军团解散、目标仍在同一聚落区域、灰袍无攻击候选且持续折返的组合，与
  玩家观察的双方城内外互相威慑完全一致。后续若要精确区分，应为罪犯状态增加
  `currentSettlement`，并记录目标聚落内敌方领主数、健康兵员合计和驻军/民兵战力。
- 当前升级机制还有一层自然闸门：只有所有已召协办人均真正附着后，梵蒂再次满足
  近距离 `GoAroundParty + Flee` 才会累计 `BlockedHours`。目前晨曦未集合，所以
  `blocked=0`；并且现有 `IsCasePursuitBlocked` 在罪犯处于 `CurrentSettlement` 时
  直接返回 false。若罪犯确实仍在城内，即使晨曦抵达，当前代码也不会继续召集第三
  名协办人；只有罪犯离城后再次形成近距离连续避战，才会继续升级。
- 继续反编译 Bannerlord `v1.4.7` 的 `DefaultMobilePartyAIModel` 后，已确认城内领主
  可以导致城外军团长逃跑。原版局部主动性会以军团长及其 `AttachedParties` 的
  `Army.EstimatedStrength` 作为灰袍本方战力，并把扫描半径内的其他敌对领主部队
  合并进敌方局部战力；敌方领主处于 `CurrentSettlement` 并不会从避战计算中排除。
  因此梵蒂追捕的罪犯虽然本人只有八十余名健康兵，但同城其他领主仍可共同抬高
  `FleeToPoint` 分数。驻军本身被普通领主主动性扫描排除，日志所见逃避更可能来自
  城内多支领主部队，而不是单独把驻军与民兵全部直接相加。
- 原版同时明确把处于定居点且未参加地图事件的目标 `attackScore` 设为零，
  `AiEngagePartyBehavior` 也跳过位于要塞中的敌军。因此即使灰袍军团已经明显占优，
  它也不会隔着城墙生成野战 `EngageParty`；占优的直接效果首先是让避战分数下降，
  军团长回到案件的 `GoAroundParty`，通常落实为靠近目标周边的 `GoToPoint`。
- `Army.EstimatedStrength` 只累加军团长和已经进入 `LeaderParty.AttachedParties` 的
  成员。仅显示在 `Army.Parties`、仍在路上的晨曦不计入梵蒂当前短期强弱判断；她
  真正到达并 `AttachedTo=gw_leader_0_party_1` 后才会使本方局部战力立即增加。若增加
  后超过城内敌方领主的合计局部战力，梵蒂应停止逃跑并靠近，但这不是按人数必然
  触发：兵种战力、伤员、附近城外敌军、距离和原版进攻/避战主动性都会参与结果。
- 灰袍已有城内目标保底流程。案件目标仍在城内时，主办人若能靠到城门三格内并
  连续一小时移动不超过 `0.35`，`HandleShelteredCriminal` 会通过原版
  `LeaveSettlementAction` 令罪犯离城；若目标仍属于同城军团则先赶军团长出城。
  目标离城后才重新允许原版野战攻击判断。当前僵局的关键因此不是缺少后续流程，
  而是梵蒂的原版避战尚未允许她进入这段城门触发距离。


## 2026-07-20 无领主追截支援改为协力军团增援

- 按用户要求改变同案已有领主协力军团后的支援用途。没有合法协力军团时，
  无领主追截支援仍沿用既有“关闭其他欲望、零逃避主动性、一次直接攻击”路径；
  同案一旦存在有效 `LordAssistanceGroup`，新生成及地图上已有的对应支援队都会解除
  直攻锁，改为 `EscortParty` 前往主办领主并加入同一个原版 `Army`，不再逐队撞向
  强敌送死。
- 支援绑定优先使用保存的 `SourceTaskPolicePartyId` 查找主办协力组；旧存档状态若
  缺少该 ID，则按同一 `TargetPartyId` 回退匹配。只有协力组仍绑定主办人的同一
  `TargetCrimeId`、军团长仍为有效灰袍领主时才允许加入，避免把支援吸入其他案件
  的军团。
- 支援设置 `MobileParty.Army` 后使用原版 Army 集合和附着流程；到达原版军团接触
  距离后调用既有 `AddPartyToMergedParties`。因此仅在路上的五十人支援不计入
  `Army.EstimatedStrength`，真正 `AttachedTo` 军团长以后才作为同一军团的可参战
  战力参与原版进攻/逃避判断。
- Bannerlord 的 `Army.OnAddPartyInternal` 会对每个加入者调用
  `CalculatePartyInfluenceCost`，而无领主支援没有 `LeaderHero`，直接走原版公式会
  空引用。新增窄 Harmony 前缀只在“无英雄追截支援正在加入当前合法灰袍协力军团”
  时返回零影响力费用；普通领主、其他无英雄部队和所有非灰袍协力军团继续使用
  原版公式。
- 案件失效或协力结束后，支援先从 Army 脱离，再使用既有返程欲望和到达销毁流程。
  原版军团因断粮等规则暂时解散、但主办案件仍未结束时，支援记录不会丢失；后续
  小时会随协力军团恢复而重新加入。旧版“卡进城即清理”兼容逻辑现在跳过仍属于
  合法协力军团的支援，避免军团长进城时误删已经附着的增援。
- AI 诊断中，军团长的协力摘要新增 `supports/attachedSupports`；无领主增援显示
  `armySupport:leader/attached/distance/target`，并在首次加入时输出
  `DELAY_SUPPORT_ARMY_JOINED`。这能直接区分支援仅在军团名单中赶路，还是已经
  真正并入并计入战力。
- 最终 Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet
  警告。十八个 XML 全部解析通过；自动部署后核对二十五个可部署文件，实机缺失
  与哈希不一致均为零。客户端与编辑器 DLL 均为 `535552` 字节，SHA-256 为
  `2D53D8A77A585C811A0323690CBD8DF2899217C8A036D60D7888299E5CB41170`。
  仓库与实机中文 README SHA-256 均为
  `4AB58279082167806F36B664BFFC66BA4A6459489EF0E0D7C27DC47EAA96D6A4`；英文
  README SHA-256 均为
  `0B2671B91FE51DA3D587F53E55426598B63692A2EFE793CAD33AF80836DA46BB`。


## 2026-07-20 城外围堵驱逐范围扩大

- 用户选择沿用现有城内目标驱逐流程，只扩大触发范围，不新增强制靠近城门的
  高权重欲望。已撤销本轮尚未构建的 `GateApproach` 临时代码，案件追踪仍只有
  `Approach/Pursue/Escort/Visit` 四种职责，继续由原版欲望拍卖决定实际移动。
- `GwpTuning.Enforcement.ShelteredGateDistance` 从 `3f` 提高到 `12f`。最新实测中
  梵蒂停在案件目标约 `5.95` 距离，原三格触发线无法覆盖，而十二格与现有协力接触
  观察范围一致，可覆盖原版 `GoAroundParty` 常用的城外围堵位置。
- 这里只扩大距离，不放宽案件资格。驱逐仍要求非玩家案件、目标当前躲在定居点、
  本案件已经宣战，并且承办部队在外围停稳达到既有时长；目标离城后仍交回原版
  野战攻击与强弱判断。
- Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet 警告；
  十八个 XML 全部解析通过。自动部署后核对二十五个可部署文件，实机缺失与哈希
  不一致均为零。客户端与编辑器 DLL 均为 `535552` 字节，SHA-256 为
  `E63CD33BC54FAFA82893173F83F21C0A886E8944FDB2BCEE41F52EE7077D3B89`。
  仓库与实机中文 README SHA-256 均为
  `D2C9160813F314BF573F2A2879E4926BC7CB0C99C7367597B1CD8BF2EFEF843D`；英文
  README SHA-256 均为
  `2AD438CD9CCFF5E7B48240AD809E1E5EB4D36A8CC9C6D21F2507B71D567DCA5D`。
- `git diff --check` 仍报告工作树既有的 `GwpAiDeterrenceState.cs:379` 文件末尾空行；
  本轮未修改该文件，也没有为处理无关用户改动而重写它。


## 2026-07-20 驱逐范围扩大后的实测诊断

- 用户进入同一存档继续等待约一至两天，确认无领主支援已经真正附着于梵蒂的
  原版军团，但地图上没有观察到案件目标被驱逐出城。本轮只读取日志、反编译实机
  DLL 并对比 Git 历史，没有修改运行行为、构建或部署。
- 最新诊断从战役小时 `625132.34` 持续到 `625159.62`：梵蒂案件始终为
  `lord_5_17/WarPursuit/war=True`，军团成员由 `attached=2` 保持不变，无领主支援
  最终从 `attachedSupports=0` 变为 `attachedSupports=1`。梵蒂位置始终为
  `(272.7485,409.1314)`，目标距离始终为 `5.95`，证明至少连续二十七小时没有发生
  超过 `0.35` 的位移，既有一小时停稳条件应已满足。
- 欲望拍卖不是阻断点。梵蒂每轮都是案件 `GoAroundParty=0.99` 胜出，原版最高进城
  候选仅约 `0.42`，短期行为在 `GoToPoint/None` 间切换；没有其他欲望夺走案件，
  也没有重新补给、逃跑、进入地图事件或进入定居点。
- 实机 DLL 反编译确认当前常量确实为 `ShelteredGateDistance=12f`，驱逐条件仍编译为
  `WarDeclared && distToGate <= 12f && stoppedHours >= 1`。根据 SandBox 的
  `settlements.xml`，梵蒂当前围堵的是 `castle_EW5`，其城门坐标为
  `(267.33,406.6704)`；梵蒂到城门的实际距离为 `5.95119`，明确处于十二格内。
- 当前诊断存在盲区：`HandleShelteredCriminal` 没有记录条件值或驱逐结果，
  `TryForceExpelShelteredCriminal` 又会吞掉全部异常。因此仅凭现有日志不能严格区分
  “`LeaveSettlementAction` 回调异常”与“成功离城后立刻重新进城”，但可以排除距离、
  停稳、案件战争状态和灰袍欲望拍卖这四类原因。
- Git 对比发现更直接的回归点：`v1.4.7-r4` 在
  `LeaveSettlementAction.ApplyForParty(expelParty)` 后会对军团长、附着成员和案件
  目标各执行一次 `SetMoveModeHold()`，注释明确说明是为了防止刚被逐出的目标立即
  钻回定居点。AI 欲望整理时，为满足“不直接写地图移动命令”，这一组一次性 Hold
  与其他长期强制追击代码一起被删除；当前版本只执行离城动作，然后立刻把目标交回
  原版 AI。目标仍处于城门坐标且面对更强灰袍军团时，原版安全/进城决策可在玩家
  可见之前重新进入同一座城。这是当前最符合代码历史与实测现象的根因。
- 后续修复应保持范围狭窄：恢复“驱逐成功后的短时防回城窗口”，或使用等价的一次性
  状态阻止同小时重新进入，而不恢复围堵期间的 AI 冻结、无限主动性或持续强制追击。
  同时应新增 `SHELTERED_EXPULSION_CHECK/ATTEMPT/RESULT` 诊断，记录聚落、城门距离、
  每小时位移、停稳小时、实际被逐出的军团长、调用结果及异常类型，以便下一次实测
  能严格确认离城动作是否成功以及何时重新进城。


## 2026-07-20 城内目标驱逐诊断增强

- 按用户要求本轮只增强后台侦查，不修改驱逐距离、停稳条件、原版欲望或目标离城
  后的行为。`HandleShelteredCriminal` 现在每个目标躲城小时输出
  `SHELTERED_EXPULSION_CHECK`，记录案件、目标、聚落、灰袍/聚落/城门坐标、到聚落
  与城门距离、阈值、目标躲城小时、本小时灰袍移动距离、停稳容差、连续停稳小时，
  以及战争、距离和停稳三项条件分别是否通过。
- 条件全部通过时先输出 `SHELTERED_EXPULSION_ATTEMPT`，明确原版离城动作实际作用于
  案件目标还是同城敌方军团长，并记录该部队进入前所在城和附着成员数。调用后输出
  `SHELTERED_EXPULSION_SUCCEEDED` 或 `SHELTERED_EXPULSION_FAILED`，同时记录目标与
  实际驱逐部队调用后的 `CurrentSettlement`；异常不再无声吞掉，而是写入异常类型和
  消息后仍沿用原有失败返回，不影响游戏继续运行。
- 普通 `HOURLY_STATE/AUCTION/RESOLVED` 的 `offenderState` 新增罪犯当前聚落和地图坐标。
  因此若驱逐当小时成功、下一小时又返回同一座城，日志可以直接用
  `SUCCEEDED` 后的 `currentSettlement=-` 与后续状态的
  `currentSettlement=原聚落` 串起完整证据。
- `tools/Watch-GreyWardenAI.ps1` 新增 `-Sheltered` 过滤器，只显示上述驱逐检查、尝试、
  结果和跳过事件；同时把领主类型过滤值从旧的 `founder_lord` 修正为运行日志实际
  使用的 `grey_warden_lord`，并把说明范围改为全部当前及未来灰袍领主。
- Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet 警告；
  十八个 XML 全部解析通过。自动部署后核对二十五个可部署文件，实机缺失与哈希
  不一致均为零。客户端与编辑器 DLL 均为 `538624` 字节，SHA-256 为
  `5FA519949EDFD2C26C7B16B9A64F89A258379B54403C883CA93177E120A602A2`。
- `Watch-GreyWardenAI.ps1 -Once -Tail 1 -Sheltered` 参数解析与过滤器启动检查通过；
  当前旧会话日志尚无新增事件是预期结果，重新载入使用本 DLL 的战役后才会开始
  产生 `SHELTERED_EXPULSION_*` 行。


## 2026-07-20 驱逐成功后即时回城的实测定论与正式版诊断清理

- 用户使用诊断增强版本快进数小时后，日志给出确定闭环。梵蒂到
  `castle_EW5` 城门距离始终为 `5.951/12`，每小时位移为 `0.000`；第二个连续小时
  `warPassed/distancePassed/holdPassed` 全为 true，驱逐资格正常成立，不存在欲望、
  距离或停稳条件未通过。
- 战役小时 `625162.14`、`625164.13`、`625166.18`、`625168.18` 均出现完整的
  `CHECK -> ATTEMPT -> SUCCEEDED`。实际驱逐部队就是案件目标
  `lord_5_17_party_1`，不是敌军团长；调用前 `CurrentSettlement=castle_EW5`，调用后
  立即变为 `-`，没有异常，证明 `LeaveSettlementAction.ApplyForParty` 每次都成功。
- 每次成功后约 `0.45～0.49` 个战役小时，普通状态日志已经重新显示
  `currentSettlement=castle_EW5`，目标坐标仍为城门 `(267.33,406.6704)`。下一小时
  驱逐跟踪又从 `shelteredHours=1/stoppedHours=0` 重新开始。因此现象已严格证明为
  “目标成功离城后不到半小时立即进入同一座城”，而不是驱逐未触发或调用失败。
- 修复方向应只处理驱逐后的防回城窗口。历史版本的一次性 `SetMoveModeHold` 正是为
  此问题存在；若恢复，应限定在驱逐成功后的目标/同城军团成员，不恢复围堵期间的
  长期 AI 冻结、强制攻击或持续地图命令。
- 正式发行清理要求：仓库目前只有一个外部 PowerShell 查看器
  `tools/Watch-GreyWardenAI.ps1`，它不位于 `_Module`、当前也没有任何 `.ps1` 或测试
  文件被部署到游戏模组，因此可留在仓库开发工具区而不进入发行包。真正需要在正式
  构建前处理的是编入 DLL 的 `GwpAiDiagnostics.cs` 及八个源码文件中的调用：当前它
  会自动创建并持续写入玩家 Documents 下的 `GreyWarden-AI-Diagnostics.log`。
- 在发布候选阶段增加硬性清理项：关闭或条件编译掉 `StartSession/WriteState/
  WriteAuction/WriteResolved/WriteAction/WriteMapEvent` 全部运行时写盘路径，并验证正式
  DLL 不再包含诊断日志文件名或 `SHELTERED_EXPULSION_*` 字符串；保留仓库 `tools`
  脚本和维护历史即可。若未来仍需玩家协助排障，应另做默认关闭、明确手动启用的诊断
  开关，而不是正式版每小时全量写盘。


## 2026-07-20 恢复驱逐后短暂停留并保留通用 AI 观测

- 按用户要求修复已证明的“成功离城后约半小时立即回城”。恢复 `v1.4.7-r4` 已验证
  方案：`LeaveSettlementAction.ApplyForParty` 成功后，对实际被逐出的部队、其附着
  成员及案件目标各执行一次 `SetMoveModeHold()`。该命令只清掉刚出城时立即选择的
  回城移动；下一次原版 AI 思考、移动或接战会自然接管，不保存禁城状态，也不会让
  目标永久无法进入任何聚落。
- 驱逐资格保持不变：非玩家案件、目标在城内、本案件已宣战、灰袍距城门不超过
  十二格并在外围连续停稳一小时。围堵期间没有恢复旧版 AI 冻结、无限主动性、自动
  补粮或持续强制攻击；只有驱逐成功后的目标侧使用一次 Hold。
- 用户澄清诊断需求：开发期仍需持续观察全部当前及未来灰袍领主、所有无领主队伍、
  任务、欲望拍卖、军团/协力、兵力和经济字段。因此保留并恢复通用
  `GwpAiDiagnostics`、军团/地图事件动作记录和 `tools/Watch-GreyWardenAI.ps1`。
- 已删除刚才为城墙问题新增的全部 `SHELTERED_EXPULSION_*` 专项检查、尝试、结果和
  异常日志，也删除脚本的 `-Sheltered` 过滤器；通用日志不再逐小时输出城门距离、
  停稳计数等已解决问题的噪声。查看脚本继续支持按领主/无领主类型及具体 Party ID
  过滤，普通状态仍含任务、欲望、军团、附着、粮食、粮耗、可用天数、领主/家族资金、
  工资、欠薪、兵力、伤员和俘虏。
- 正式发布原则不变：PowerShell 查看器留在仓库 `tools`，不进入 `_Module`；当前
  DLL 内通用诊断只用于开发测试，正式发布候选时再统一关闭运行时日志写盘，而不是
  在当前仍需经济和 AI 测试的阶段提前删除。
- Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet 警告；
  十八个 XML 全部解析通过。自动部署后核对二十五个可部署文件，实机缺失与哈希
  不一致均为零。客户端与编辑器 DLL 均为 `535552` 字节，SHA-256 为
  `928D31CEB6F556E9B16A1F7BB40A8501D9E5C9E25058E6DCBE6B7E9E8B6DDB3B`。
- ILSpy 反编译实机 DLL 确认 `TryForceExpelShelteredCriminal` 在原版
  `LeaveSettlementAction.ApplyForParty` 后依次调用实际驱逐部队、附着成员和案件
  目标的 `SetMoveModeHold`；通用诊断仍包含 `GreyWarden-AI-Diagnostics.log`，但 DLL
  与源码均不再包含任何 `SHELTERED_EXPULSION_*` 事件。查看脚本参数启动检查通过。
- 仓库与实机中文 README SHA-256 均为
  `FE5AE7ACAE36083CF294BFB630ADBB4557489E48D1B06C8ADD5064147180AB21`；英文 README
  SHA-256 均为
  `046AF2A4C5C176755A96DD6B3F66CEE884931905A4FF72DBEA590134FB7D6E54`。


## 2026-07-20 已宣战案件目标禁止重新进入聚落

- 用户复测确认一次性 `SetMoveModeHold` 仍不足：它只清空当前移动，原版下一次短期
  AI 决策仍会立即再次调用进城。因此保留驱逐后的 Hold 作为第一帧清理，并新增真正
  的案件期进城拦截，不再依赖下一次 AI 是否重新选择安全聚落。
- 反编译 Bannerlord `v1.4.7` 确认所有普通部队进城最终统一经过
  `EnterSettlementAction.ApplyForParty(MobileParty, Settlement)`，该方法会直接设置
  `CurrentSettlement` 并触发聚落进入事件。新增 Harmony 前缀只在进入部队属于一宗
  当前 `WarDeclared=true` 且目标仍有效的非玩家案件时返回 false，并清掉本次进城
  移动；其他所有部队和所有非案件进城继续执行原版方法。
- 拦截对象包括案件罪犯本人，以及罪犯当前原版 `Army` 中的军团长和成员。这样无论
  是领主协力军团还是同案无领主支援先把目标拖入野战，同一个承办案件都会阻止目标
  或同军团成员先钻回聚落再把其他成员吸回去。无领主支援不需要另建第二套禁城状态。
- 禁城生命周期完全绑定案件现有数据，不保存永久名单：案件结束、换案、目标失效、
  `WarDeclared` 因外交和平检查重置，或目标退出该军团后，前缀下一次检查自然放行。
  玩家案件明确排除，仍保留原有玩家对话、补给和进城规则。
- 通用 AI/经济诊断保持不变，仍观察全部灰袍领主和无领主队伍；没有重新加入任何
  `SHELTERED_EXPULSION_*` 城墙专项日志。
- Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet 警告；
  十八个 XML 全部解析通过。自动部署后核对二十五个可部署文件，实机缺失与哈希
  不一致均为零。客户端与编辑器 DLL 均为 `536064` 字节，SHA-256 为
  `8EC2F78CB8777B841308B8003089370E835ACF07B2586576728E39E771C77468`。
- ILSpy 反编译实机 DLL 确认 `GwpCaseSettlementEntryPatch` 精确 Harmony patch 原版
  `EnterSettlementAction.ApplyForParty`，案件目标命中时执行一次 Hold 并返回 false；
  `PoliceEnforcementBehavior.IsSettlementEntryBlockedByActiveCase` 实机方法包含
  `WarDeclared`、目标有效、非玩家、罪犯本人及同 Army 成员判断。源码中的
  `SHELTERED_EXPULSION_*` 计数为零。


## 2026-07-20 驱逐目标改为案件期间持续 Hold

- 用户进一步明确目标不是“阻止进城但仍允许原版在野外移动”，而是驱逐成功后始终
  `Hold`，直到案件生命周期结束。此前一次性 Hold 和仅拦截进城入口都不足以满足该
  语义，因此新增每帧维护的案件级 Hold 集合。
- 驱逐前记录实际被逐出的原版军团长、当时全部附着成员和案件罪犯的 Party ID；
  `LeaveSettlementAction` 成功后立即 Hold，并把这批 ID 绑定到承办任务 ID。之后
  `CampaignEvents.TickEvent` 每帧对仍活跃、未处于地图事件且不在聚落内的记录部队
  重复 `SetMoveModeHold()`，原版小时/短期 AI 即使重新选择移动也会被持续覆盖。
- 进入战斗时 `MapEvent != null`，持续 Hold 暂停，避免干扰地图事件建立和战斗结算；
  原版 `EnterSettlementAction.ApplyForParty` 前缀仍作为保险，只对当前 Hold 集合中的
  部队拒绝进城。
- Hold 集合不写入存档，也不永久绑定英雄。每帧先检查原承办任务是否仍存在、
  `WarDeclared=true`、目标有效且不是玩家；案件结束、换案、和平回退、目标失效或
  读档重建时集合被删除/清空，相关部队随即恢复原版移动和进城。
- 该机制由承办案件建立，因此领主协力军团和同案无领主支援共享同一个被 Hold 的
  目标集合；无需按攻击者类型重复实现。通用 AI/经济诊断继续保留，城墙专项日志
  仍为零。
- Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet 警告；
  十八个 XML 全部解析通过。自动部署后核对二十五个可部署文件，实机缺失与哈希
  不一致均为零。客户端与编辑器 DLL 均为 `538112` 字节，SHA-256 为
  `8A52ACC0A6E52EE9067A636BE7DD2B6B7227FF2EB5A5CCC7EEC6BAA53CFFB39A`。
- ILSpy 反编译实机 DLL 确认 `OnTick` 在任何玩家俘虏早退之前调用
  `MaintainShelteredCaseHolds`，并逐帧遍历任务绑定 ID 调用 `SetMoveModeHold`；
  `TryForceExpelShelteredCriminal` 在离城前保存军团长、附着成员与罪犯 ID，随后建立
  Hold 集合。通用诊断保留，源码中的 `SHELTERED_EXPULSION_*` 计数仍为零。
- 仓库与实机中文 README SHA-256 均为
  `2D4D1D0F2645316EF45F325750D0EA8DCBB9DACE04C05A2DE28A190A18BF0B51`；英文 README
  SHA-256 均为
  `86310457F97CD6CD3F6019D81C7ABE633A78E2B6F2E7F152367A3875B71828EF`。


## 2026-07-20 v1.4.7-r5 正式发行分层、安装包与验证

- 正式版本号定为 `v1.4.7-r5`。玩家 README 的当前条目只写相较 `v1.4.7-r4` 的玩法结果：
  案件总卷、原版欲望办案、灰袍真实经济、协力军团、无领主支援及本轮关键卡死修复；
  同时按用户新增规则原样保留上一正式版 `v1.4.7-r4` 的玩家日志。以后每次正式发布均
  只保留最新两个版本，新版本在前，第三旧版本移除。诊断字段、内部权重、测试过程和
  构建细节不进入玩家发布说明。
- 明确分离开发测试构建和玩家发行构建。`GwpDiagnosticsEnabled` 默认保持 `true`，普通
  Release 构建继续把完整 `GwpAiDiagnostics` 编入 DLL，并自动同步到
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`，因此
  工作目录源码、查看脚本和本地实机测试模块仍可持续输出并读取 AI/任务/军团/经济日志。
- 正式包使用
  `-p:GwpDiagnosticsEnabled=false -p:DeployToLiveModule=false` 单独编译到
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\release-player`。
  该分支把 `GwpAiDiagnostics` 编译为空壳，全部写入方法为空、`ShouldTraceParty=false`、
  `LogPath=string.Empty`；同时禁止自动覆盖本地实机测试模块。ILSpy 反编译已确认发行 DLL
  不创建目录、不创建文件、也不写入任何测试日志。
- 本地实机测试 DLL 为 `537600` 字节，SHA-256
  `99A9FE9781AD62DB62038FC806F176D0D3D3049218BE67DF144FDA18DBEC25BD`；玩家发行 DLL
  为 `526848` 字节，SHA-256
  `4E3EF8BA10061EBF00D1FD35B2BB7165EB1B55344EBC8520F7720950DCB9A154`。二者分开保存，
  玩家包的制作没有替换实机测试 DLL。
- 下述 ZIP 哈希是加入“双版本玩家日志”规则前的首轮候选值，已废弃，不能上传；最终
  README、ZIP 和校验值见本节后续的最终验证补记。首轮候选安装包位于
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7-r5.zip`，
  大小 `349767453` 字节，SHA-256
  `5275559FB95B3979F7B9D742670EA1C937801E0D8E329C9526878C893B9E89A9`；校验文件为同目录
  `GreyWarden-v1.4.7-r5.zip.sha256`。ZIP 顶层唯一目录为 `GreyWarden/`，发行 DLL 从包内
  解出后与独立发行构建哈希完全一致。
- 安装包沿用 `v1.4.7-r4` 的正常客户端结构，并加入本版案件总卷界面。内容检查确认不含
  `tools`、PowerShell、测试日志、诊断目录、PDB、编辑器二进制、`Assets`、`AssetSources`、
  `RuntimeDataCache` 或任何嵌套压缩包；仓库中的 `tools/Watch-GreyWardenAI.ps1` 只供本地
  开发观测，不进入 ZIP 或 GitHub Release 资产内部。
- 开发构建与独立发行构建均零错误；现有四十三至四十四条可空性/离线 NuGet 警告未新增
  运行阻断。十八个 XML 全部解析通过。仓库 `_Module` 的二十五个可部署文件与实机测试
  模块逐文件核对，缺失与哈希不一致均为零。
- 正式发布沿用仓库既有流程：提交到 `main`，打注释标签 `v1.4.7-r5`，并将上述 ZIP 与
  `.sha256` 作为 GitHub Release 资产发布；不为本次正式版本额外创建中间 PR。

### 双版本玩家日志规则后的最终候选

- 用户在正式发布前补充长期规则：中英文玩家 README 永远保留最近两个正式版本的
  玩家日志。本次已从 `v1.4.7-r4` 标签恢复 `2026-07-19 v1.4.7-r4` 原始中英文条目，
  放在精简后的 `2026-07-20 v1.4.7-r5` 条目之后；当前两份文件均只含 `r5`、`r4`，
  顺序正确。根目录 `AGENTS.md`、本维护文档的发布日志规则和正式发布清单均已同步，
  并一并固化“开发测试诊断可保留、玩家包必须使用独立无诊断 DLL”的边界。
- 双版本日志更新后重新执行普通 Release 构建，实机测试模块继续得到诊断启用 DLL；
  `_Module` 二十五个可部署文件与实机逐文件一致。最终中文 README SHA-256 为
  `FB0D2723CEE63EFE995970BA6078DDAED50353550FB67044D18B57C0194374B2`，英文为
  `B691FFEE5A5864D26C7DD52F862B4E382E9FFD59E3D54A51999AD6975E802B07`。
- 最终安装包仍位于
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7-r5.zip`，
  已覆盖首轮候选，最终大小 `349768562` 字节，SHA-256
  `FA4C3157415D5B67431DF1C0269CD5882D013A1613D1B811D033FD46331CCEC2`；对应
  `.sha256` 已同步重写。最终 ZIP 继续使用哈希为
  `4E3EF8BA10061EBF00D1FD35B2BB7165EB1B55344EBC8520F7720950DCB9A154` 的独立无诊断
  玩家 DLL；首轮候选哈希 `5275559F...` 作废，禁止上传。


## 2026-07-21 最近实机灰袍家族资金来源复核

- 本次只诊断、不调整经济规则。开发日志位于
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`，
  当前会话覆盖战役小时 `625782.99～625973.04`，约 `7.92` 天。灰袍族长梵蒂的
  `leaderGold` 与 `clanGold` 始终相同，家族公库由 `551151` 增至 `645761`，净增
  `94610`；玩家看到的“非常富有”确实是家族共享金库增长，不是其他领主个人钱袋
  被界面误算。
- 会话内共有八次稳定的日结算正跳变，依次为 `+7784`、`+7773`、`+7765`、
  `+7764`、`+7765`、`+7759`、`+7747`、`+7735`，合计 `+62092`。这些跳变每隔约
  二十四个战役小时出现，且缓慢下降；与当前
  `CollectDailyVillageProtectionContributions` 每日汇总全大陆
  `Village.Hearth × 0.1`、同时把各村户数乘以 `0.99` 的规则完全吻合。日志按小时
  取样，因此该数字是保护费、原版每日 `480` 无地补贴、族长工资及其他领主补回
  `5000` 周转金在同一日结算后的可见净跳变，不应误写成保护费的精确毛收入；但它
  已直接证明保护费日结算仍给公库留下约 `7700～7800/日` 的稳定净增量。
- 另有六次可由案件目标真实战败对齐的拨款，每次 `5000`，合计 `30000`：战役小时
  约 `625785.99`、`625825.66`、`625830.91`、`625952.86`、`625963.25`、
  `625967.95`。案件拨款仍是源码常量 `SuccessfulCaseReward = 5000`。普通劫匪战、
  目标未实际覆灭或外部势力代打没有产生该固定拨款；`625930.90` 的追截支援队是
  进攻方而战斗结果为 `DefenderVictory`，也没有胜案拨款。
- 八次日结算净增与六次胜案拨款合计 `92092`，比会话总净增少 `2518`。差额代表
  其余原版现金流的合并净收入：梵蒂自己的招募、采购、工资和交易直接作用于家族
  金库，其他领主日结算补款也由公库承担；梵蒂参战所得、其他领主超过原版阈值后的
  上缴等收入会抵消并略微超过这些支出。日志中例如 `625875.43` 梵蒂参加玩家战斗后
  公库瞬时增加 `9890`，随后同一小时又出现 `-2820`、`-2116` 的原版结算支出；
  `625930.62` 梵蒂位于 `town_B4` 时又增加 `5002`，符合进城出售战利品/货物。这些
  原版行动确有贡献，但合并净值只占总增长约 `2.7%`，不是持续暴富的主因。
- 结论：当前增长由“全大陆村庄保护费”提供稳定底盘、频繁成功案件的固定拨款进一步
  放大；六支常驻队的原版开支和其他零散收支整体不足以抵消二者。按本会话净速度约
  `11948/战役日`，若案件频率与村庄规模近似不变，公库仍会继续上升。若后续要压低
  财富，应优先决定调整保护费毛收入、成功案件 `5000` 拨款或增加与公库规模相关的
  支出；不能只调部队个人钱袋或把现象归因于战利品。


## 2026-07-21 司法公库降档与案件池全覆盖拨款

- 根据最近实机公库增长复核，村庄保护费从当前人口的每户 `0.1` 第纳尔降为
  每户 `0.05` 第纳尔。日结算仍以全大陆 `Village.Hearth` 汇总后统一向下取整，
  但不再写入或衰减 `Village.Hearth`；村庄人口现在只受原版成长、劫掠等机制影响，
  不会因灰袍保护费额外减少。
- `SuccessfulCaseReward` 从 `5000` 降为 `3000`。原版战利品、俘虏赎金、城镇交易、
  招募、购粮、工资和家族内部现金流没有改动。
- 用户明确拨款以案件总卷的**完成条目**为准，而承办者只需属于灰袍势力，不限领主。
  普通案和玩家案在灰袍领主实际击败目标时各拨一次；同一时刻仍在总卷中的每条领主
  协力任务也各拨一次，因此强敌案件的多名协力领主均对应自己的完成条目，但不会因
  同一协力条目重复结算。玩家案胜利后即使仍进入押送流程，灰袍已经完成击败目标的
  条目，照此时点结算其承办和协力拨款。
- 村庄收养善后是总卷中的独立任务：仅当灰袍领主已经抵达村庄、完成完整驻村时间并
  正常结束善后时拨一次；部队失效、目标/村庄无效、运营中止和读档清理路径仍不发。
  无领主追截支援队实际击败目标时同样属于灰袍势力完成：已分派源案件拨一次，并为
  当时仍在总卷中的各协力条目分别拨一次；若删掉的是未分派待办案，则该待办条目拨
  一次。外部领主、罪犯的敌人或其他第三方击败目标只会让案件失效，不经过灰袍胜利
  入口，因此不发这项拨款。
- 玩家 README 已建立 `2026-07-21 v1.4.7-r6` 条目，并按双版本规则保留 `r6` 与
  `r5`、移除 `r4`。条目只描述降低公库收入、四类任务拨款和取消人口损耗；精确
  数值与路径保留在本维护记录和 `docs/grey-warden-setting.md`。
- 最终 `Release --no-restore` 构建通过，`0` 错误、`44` 条既有可空性/离线 NuGet
  警告。自动部署后核对 `_Module` 的 `25` 个可部署文件，实机缺失与哈希不一致均为
  `0`；`18` 个 XML 全部解析通过。客户端与编辑器 DLL 字节一致，SHA-256 均为
  `C828B9B8DE0FA8C476601E88499CF281FAAB1F3FF766FCE0FD52235C279FD73B`。
- ILSpy 反编译实机 DLL 确认 `SuccessfulCaseReward = 3000`、日结算只读取
  `Hearth` 后乘 `0.05` 而不再写回村庄人口；普通/玩家胜案均调用
  `CompleteAssistanceTasks`，该方法为总卷当前每条协力任务各调用一次拨款并立即
  释放协力军团；村庄 `FinishRelief(..., shouldAdopt: true)` 才调用拨款，所有中止
  和读档清理路径均传入 `false`。追截队 `ResolveTrackedOffenderDefeatByDelayPatrol`
  在支援队实际胜利后为删除的已分派源案件及其协力条目分别拨款，或在成功删除未
  分派待办案时拨一次；外部势力代打路径不调用该方法。
- 仓库与实机玩家 README 哈希均一致：中文
  `220D6EC8024A60A59492E439F7C8147E561B896AD6122C8C740B5985D5FF9EC8`，英文
  `D54B05A7252D72CD583BA9EC7D25BBDA603FE83A098F77849FB9C92F0BBC501C`。


## 2026-07-21 战斗强化成长对象核查

- 用户要求确认踢击、盾击和弓箭成长是否作用于单个士兵，而非整个兵种。
  当前实现已满足该边界：`GwpAlternativeAttackControlBehavior` 的近战和弓箭成长表
  都以战场 `Agent.Index` 为键，`GwpAgentStatCalculateModel.ApplyBattleMastery`
  每次按当前传入的 `Agent` 查询；同一个 `CharacterObject` 生成的其他士兵不会读取
  该人的成长值。
- 近战动作每次给发起动作的那个 Agent 记录一次成长，弓箭每次由该 Agent 发射一箭
  记录一次；成长只存在当前 Mission。Agent 删除时移除对应键，Mission 行为结束时
  清空全部键，不写回兵种模板、Hero 技能或存档。因此本次核查没有代码、数值或玩家
  可见行为改动，也没有更新玩家 README。
- 若以后要求同一个战役单兵跨多场战斗继续保留成长，不能继续使用 Mission 的
  `Agent.Index`；需要为战役部队中的单兵建立可保存、可随伤亡/招募迁移的唯一身份，
  并在 `TroopRoster` 与战斗 Agent 之间维护映射。这属于新的持久化设计，当前未实现。


## 2026-07-22 独立灰袍协力军团与敌军长期僵持诊断

- 用户在稳定性测试中保存了可复现场景：族长梵蒂带领协力军团追捕
  `lord_5_1_party_1`，双方在城旁长期僵持且敌军没有进城。复现存档为
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\Game Saves\save001.sav`，
  修改时间 `2026-07-22 16:11:55 +10:00`，大小 `6178969` 字节，SHA-256
  `1980BFFE52E3A684E74B5868898DC8B3A631CE2E5AF670F2ACB8A8006F2DF52F`。对应监控为
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`，
  本次取证时大小 `8906121` 字节，SHA-256
  `0B4048DFE2CC89DFC3D88B9ECD832C8B74F59246240085A5C9EF30162E8DB588`。
- 存档前最后状态已经排除“AI 被锁死”和“双方已经进入战斗/攻城事件”：梵蒂
  `aiDisabled=False`、`doNotDecide=False`、`mapEvent=-`、`siege=-`，案件仍为
  `WarPursuit`；目标存活 `133` 人，`armyLeader=-`、`attachedTo=-`、`mapEvent=-`、
  `siege=-`。梵蒂始终把该目标作为 `GoAroundParty` 目标，但距离约 `8.7～9.1` 时，
  原版短期行为反复落成 `FleeToPoint` 或极短的 `GoToPoint`，没有进入接战。
- 最直接的拍卖证据在战役小时 `626324.20`、`626327.23`、`626330.27`：原始候选中
  只有补给/访问聚落候选，**没有任何原版追敌候选**；模组随后固定加入
  `GoAroundParty@lord_5_1_party_1 = 0.99` 并使其获胜。获胜后的原版短期解析却连续为
  `FleeToPoint`。因此这不是“别的欲望抢走追捕”，而是独立灰袍军团根本没有获得
  原版的可接战追敌分数，只能靠模组的通用环绕追逐候选维持目标，最终形成追而不打。
- 已对照当前游戏 DLL 的 `AiEngagePartyBehavior.AiHourlyTick` 反编译代码。现有
  `GwpAssistanceArmyNativeEngageDesirePatch` 只在该方法执行期间把协力军团的
  `ArmyType` 临时改成 `Defender`，绕过了第一层“非 Defender 军团领袖直接返回”；
  但原版紧接着还有第二层硬条件：部队所属地图势力既不是王国、又不是玩家地图势力
  时同样直接返回。灰袍是独立氏族 `mapFaction=gw`、`factionKingdom=False`，所以仍在
  第二层退出。此前补丁注释中“原版只跳过非 Defender 军团领袖”的判断不完整，已由
  本次原始拍卖列表和反编译双重证伪。
- 协力增援状态是同时存在的次要现象，但不是用户所指的核心原因：梵蒂的军团当前登记
  三名领主协力，其中约珥和暮光已经
  贴合，另有一支无领主支援队贴合；弥瑟仍在 `57.86` 距离外赶来。现场至少已有
  梵蒂 `204` 人、约珥 `137` 人、暮光 `145` 人和支援队 `50` 人贴合，但
  `UpdateLordAssistance` 必须等 `AreAllMembersAssembled` 为真才开始累计
  `BlockedHours`，所以诊断始终显示 `blocked=0`，不会把当前“贴脸仍逃跑”识别为阻塞。
  弥瑟在战役小时 `626243.59` 被加入时距离 `279.97`，到存档时仍未贴合；这使已有
  大量现场兵力的军团又等待了约 `88.5` 战役小时而不进入下一步处理。用户随后明确
  现场问题是敌我双方都在害怕、互相逃避；因此不能把远方增援当作本次僵持主因，修复
  前必须先取得敌方主动性判定。
- 关于“敌军为什么没有进城”，当前监控只完整记录受管灰袍部队；嵌入的目标摘要没有
  记录目标自己的 `DefaultBehavior`、`ShortTermBehavior`、`TargetSettlement` 和
  `CurrentSettlement`，因此日志可确认目标当时不在军团、战斗或围城中，却不能单独
  证明它的进城命令为何未完成。当前可确认我方的战略案件欲望获胜后，被原版临场
  主动性判断覆盖为逃跑；敌方是否以及为何得到同样的逃跑结论，需由新增双向监控定案。
- 本次仅诊断并固化复现证据，没有修改玩法代码或玩家 README。后续修复不能只继续
  临时伪装 `ArmyType`，也不能先按增援数量强制接战。应先使用下述双向主动性监控确认
  双方各自计算出的目标、逃跑分数、兵力和原始欲望，再决定只修正灰袍军团的错误估值
  还是处理城口双方互相逃避的通用死锁。


## 2026-07-22 案件目标双向欲望与主动性监控

- 为复现“敌我双方都害怕”而只看得到灰袍半边的问题，开发诊断现会把所有当前案件
  目标，以及目标所属军团/围城的实际战斗领袖纳入只读观测。每次原版 AI 拍卖记录为
  `OBSERVED_AUCTION`，解析后的最终行为记录为 `OBSERVED_RESOLVED`；两者均带
  `observedForCases`，可反查承办灰袍部队和案件编号。监控不向敌方加入、删除或改写
  任何欲望分数。
- 新增的 `GwpInitiativeDiagnosticsPatch` 只在诊断构建中对
  `DefaultMobilePartyAIModel.GetBestInitiativeBehavior` 做后置观察，缓存敌我双方原版
  临场主动性结果：`行为@目标`、最终分数、综合敌人方向和战役小时。状态行同时补充
  单队/军团估算战力、进攻与回避主动性、侵略性、警戒状态和基础速度。这样可区分：
  战略欲望本身选择逃跑、案件追捕欲望获胜后被临场主动性覆盖、双方估算战力范围不同，
  以及敌方其实在执行进城但被逃跑临时行为打断等情况。
- 这次改动只扩展本地诊断 DLL；正式玩家构建仍将整个补丁排除并保留空诊断实现，未改
  玩法、案件、军团、数值或玩家 README。下一轮只需加载上述 `save001.sav`，让时间
  运行约 `2～3` 个战役小时；不需要长期挂机或另开新档。退出后读取新会话日志中的
  `gw_leader_0_party_1` 与 `lord_5_1_party_1` 对照行即可定案。
- 最终 `Release --no-restore` 构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet
  警告；未新增诊断代码警告。构建已自动部署到本地实机模块，仓库 `_Module` 的
  `25` 个可部署文件逐一对照实机后缺失 `0`、哈希不一致 `0`，`18` 个 XML 全部可
  解析。正常客户端与编辑器诊断 DLL 大小均为 `542208` 字节，SHA-256 均为
  `89B3BBA964DF403D9F99702F8F006080E344FE9944DA63113CC19382F1D770B7`。

### 观测范围与开销收紧

- 双向监控不是给全地图每个角色建立独立监控器，也不枚举 Hero。Harmony 后置观察仍随
  原版部队 AI 回调进入，但首先按部队 ID 过滤；只有灰袍领主/无领主灰袍队，以及当前
  案件目标和其实际战斗领袖会缓存主动性、写欲望和最终行为日志。其余地图部队只经过
  常数时间的集合查询，不创建快照、不格式化状态、不写磁盘。
- 第一版目标过滤会在每个相关 AI 回调里遍历 `CrimePool.ActiveTasks`，虽然通常案件数很
  少，但案件池硬上限为 `100`，没有必要把这项线性扫描留在稳定性测试版。现已改为每次
  灰袍小时结算结束后根据案件池重建一次 `ObservedPartyIds` 与案件说明缓存；之后每次
  回调只做一次大小受案件池约束的 `HashSet<string>.Contains`。新会话第一次使用时也只
  延迟构建一次。正式玩家构建继续编译为空实现，完全没有这项监控成本。
- 缓存优化后的 `Release --no-restore` 构建通过，`0` 错误、`43` 条既有警告；仓库
  `_Module` 的 `25` 个可部署文件与实机相比缺失 `0`、哈希差异 `0`，`18` 个 XML
  全部解析通过。客户端和编辑器诊断 DLL 大小均为 `542720` 字节，SHA-256 均为
  `E0834DEA9C5ECEFDC30B6A190B85FEF778A16E597187C6B3E4FC93FCBBB64CB8`。

### 双向主动性复现结论

- 用户于 `2026-07-22 17:04` 从 `save001.sav` 继续运行约 `18` 个战役小时。本次日志为
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`，
  取证时大小 `649653` 字节、SHA-256
  `18F3208EFFBFE225319E958309BEB993CCFDE3E15AB89D628B59145E26D79405`；新存档大小
  `6180123` 字节，时间 `17:04:43`。梵蒂军团的已贴合战力从约 `892` 增至 `901`，现场
  包含梵蒂 `204`、约珥约 `137～138`、暮光 `145` 和无领主支援 `50`；弥瑟 `56` 人仍从
  距离 `56.39` 赶到 `18.34`。目标卡拉多格 `133` 人、单队估算战力约 `202`，双方距离
  全程约 `8.4～9.2`，均没有进入地图事件或围城。
- 灰袍战略欲望本身稳定正确：每次拍卖都由模组的
  `GoAroundParty@lord_5_1_party_1=0.99` 获胜。但原版临场主动性在本次采样中约 `24` 次
  给梵蒂返回 `FleeToPoint@lord_5_1_party_1`，分数稳定约 `1.50～1.52`；只短暂出现 `3`
  次 `EngageParty`。主动性目标还短暂切到 `lord_5_10_party_1`、`lord_5_14_party_1`，与
  用户现场所见“大量敌方部队在附近徘徊”一致：原版比较的不是卡拉多格 `202` 对灰袍
  `901` 的单一比值，而会把目标周围同阵营、可加入交战的附近力量计入局部敌军强度。
- 卡拉多格同样把灰袍军团判为危险：其主动性多次返回
  `FleeToPoint@gw_leader_0_party_1`，分数约 `1.27～1.43`。其原始战略拍卖却明确要返回
  `town_B1`，该候选分数约 `41～44`；最终行为 `18` 次采样中 `15` 次为 `Hold`、仅 `2`
  次实际 `GoToSettlement@town_B1`、一次 `FleeToGate`。因此双方不是没有欲望，而是敌方
  的回城欲望和灰袍的追捕欲望都被短期逃跑判断反复打断。
- 已对照当前原版 `DefaultMobilePartyAIModel.GetBestInitiativeBehavior` 与
  `MobilePartyAi.GetFleeBehavior/CalculateFleePosition`。原版先在近距离统计敌我可支援战力，
  再把所有危险方向压成八个候选方位并选逃离危险总量最大的方向，随后只做局部可达性、
  左右绕障和短程路径修正；它不是会规划“从另一条大路绕经中立国”的全局撤退规划器。
  此外，只有 `MapFaction.IsKingdomFaction` 的领主才进入“寻找附近聚落逃亡”分支；独立
  灰袍氏族不满足该条件，只能生成普通逃跑点。中立国家没有与灰袍交战，并不自动使其
  城镇成为灰袍原版逃亡目的地。
- 该僵持是原版局部主动性与模组永久案件追捕共同造成的反馈环：临场判断令灰袍退开，
  下一次小时拍卖又以 `0.99` 把灰袍拉回目标，无法形成真正撤退；敌方则在回城、逃门和
  原地等待之间切换。修复应只处理受管协力军团：对授权案件目标按可立即参战的双方局部
  总战力决定胜方；灰袍胜时允许其持续接战，灰袍劣势时暂停追捕并形成有持续时间的真实
  后撤，不能简单让每小时案件欲望继续覆盖原版逃跑，也不应改写全地图敌方 AI。

### 未结宣战案件持续支援

- 用户明确否决“接战承诺期”及按临场主动性改写追逃结果的方案，要求保留原版欲望和
  主动性判断。新的解法只扩展既有无领主纠察支援：一宗合格案件只要仍为开放的
  `WarPursuit`、具体目标仍存活且灰袍与案件目标势力仍在战争中，每次既有两日检查都会
  再生成一支 `50` 人支援队；不再因为 `_delayPatrolStates` 中已有同目标队伍而跳过。
- 初次发现战争时的即时生成仍保留原有去重，避免同一触发瞬间重复建队；只有后续两日
  周期持续追加。每支新队继续携带独立任务粮食，并优先加入该案件现存的独立协力军团；
  没有军团时先按原支援追捕逻辑行动，军团建立后仍会尝试并入。按用户要求不设累计队数
  上限，让案件存续期间的灰袍兵力持续增长，直到原版主动性认为足以接战。
- 案件结束、目标失效或战争理由消失后，`UpdateDelayPatrols`、
  `MarkDelayPatrolsReturningForTask/Target` 仍会把所有该目标支援队逐一解除军团并返程销毁，
  不改变既有结案拨款边界。由于支援现在可以长期累积，生成 ID 同时改为检查运行状态和
  当前地图部队，随机碰撞时重新取值，避免后生成队覆盖旧支援状态。
- 用户随后再次明确两个硬边界：案件池中尚未分派、没有承办灰袍队伍的案件不得生成周期
  支援；目标为玩家主队的案件也不得生成。虽然现有数据结构已用 `ActiveTasks` 区分未分派
  案件，并已有 `offender.IsMainParty` 排除玩家，筛选仍增加显式防线：`PolicePartyId` 必须
  非空，且必须能解析到当前存活、属于灰袍的承办队伍；之后才检查非玩家目标、开放案件、
  `WarPursuit` 和战争状态。这样即使以后案件池或任务迁移逻辑变化，也不会扩大增援资格。
- 最终 `Release --no-restore` 构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet
  警告。已部署到正常客户端和编辑器目录；两处 DLL 均为 `543232` 字节、SHA-256
  `0916757FC42E50905D25FF0796A77D867DCD43674CD75A674BC4235143860F05`。仓库 `_Module`
  的 `25` 个正常客户端可部署文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML
  全部解析通过，中英文 README 与实机版本一致。

### 2026-07-22 16:05 弹窗退出取证

- 用户回忆该次异常发生在玩家跟随军团、被劫匪拦截附近。第一段引擎日志
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_47812.txt` 证明会话在
  `16:04:33～16:04:51` 连续经历两次劫匪遭遇；两场都已经记录
  `DefenderVictory`，第二场还进入 `WaitingRemoval`。日志随后在 `16:05:00` 的每日
  Tick、创建 `lord_1_54` 英雄模板后突然终止。第二段 `rgl_log_66780.txt` 是重启后
  会话并正常退出。案件总览当时虽然处于打开状态，但最终调用栈中没有
  `GwpCaseArchiveScreen`；界面只是遮住了大地图上同时进行的战斗结算，不是崩溃来源。
- Windows 应用程序事件已补到确定证据：`2026-07-22 16:05:22` 的 Application Error
  `1000` 与 `16:05:32` 的 Windows Error Reporting `1001` 均记录
  `TaleWorlds.MountAndBlade.Launcher.exe` 致命 `APPCRASH`；异常码 `0xe0434352` 是 CLR
  托管异常，故障汇总模块为 `KERNELBASE.dll`。WER 报告保存在
  `C:\ProgramData\Microsoft\Windows\WER\ReportArchive\AppCrash_TaleWorlds.Mount_4c92ceedbf38debdbac93aeb2c485593f16f0f0_7140fc22_0558f2bf-cd2a-476b-9546-e965319479ab\Report.wer`，
  Report ID 为 `1158abf0-009f-48fb-b0dc-a8df773d007c`。
- 后续在 `C:\Users\lucif\AppData\Local\CrashDumps` 找到系统保留的完整目标转储
  `TaleWorlds.MountAndBlade.Launcher.exe.47812.dmp`（`117521332` 字节，时间
  `2026-07-22 16:05:36`）。WinDbg/SOS 解析得到未处理异常
  `System.InvalidOperationException: Collection was modified; enumeration operation may not execute.`；
  栈为 `List<T>.Enumerator.MoveNextRare` → 灰袍 DLL 方法 →
  `CampaignEvents.OnMapEventEnded` → `MapEvent.FinalizeEventAux`，证明异常发生在大地图
  战斗结束结算，而非每日家族刷新或案件总览 UI。
- 崩溃 DLL 已不在实机目录，故用崩溃前源码形态重建元数据顺序；转储中的灰袍方法令牌
  `0x06000517` 与类型令牌 `0x02000060` 均精确映射到
  `PolicePrisonerImmunityBehavior.OnMapEventEnded`。该方法原先直接遍历
  `loserSide.Parties`，并在循环内部对败给非玩家势力的灰袍领主调用
  `MakeHeroFugitiveAction.Apply`；该动作可能从当前地图事件中移除领主队伍，导致下一次
  `MoveNext` 发现集合版本变化并抛错。这也说明玩家刚刚完成的两次劫匪自动结算只是同一
  时段的可见事件；实际触发条件是另一个被非玩家击败的灰袍领主进入免俘处理。
- 修复改为两阶段处理：遍历败方时只去重并收集灰袍领主，完全退出
  `loserSide.Parties` 枚举后才逐个执行逃亡/免俘动作。没有增加任何新监控，也没有改变
  玩家击败灰袍时仍可俘虏、非玩家击败灰袍时仍会脱身的既定规则。
- 修复后的 `Release --no-restore` 构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet
  警告。构建已部署到正常客户端和编辑器目录；两处 DLL 均为 `542720` 字节，SHA-256
  均为 `ADDFE62999F7A6ADAD2FBDBF30AE4378C70433DDE4E4491CE129C177CDDEC786`。仓库
  `_Module` 的 `25` 个正常客户端可部署文件与实机相比缺失 `0`、哈希不一致 `0`，
  `18` 个 XML 全部解析通过，中英文 README 均已同步且哈希一致。
- 进一步核对提交历史与事件注册顺序：出错的免俘循环自 `2026-04-01` 的
  `fc8094e` 起已经存在，协力军团则到 `2026-07-20` 的 `737e751` 才加入，所以军团没有
  创造这段错误代码。也不是“领主战败后又被拉进军团”：`PolicePrisonerImmunityBehavior`
  在 `PoliceEnforcementBehavior` 之前收到 `MapEventEnded`，协力军团的败北释放处理反而在
  后面执行。此前多数单队败北会先触发 `HeroPrisonerTaken`，或由原版先把领主设为逃亡，
  到本循环时 `hero.IsFugitive` 已为真而不再销毁队伍；军团/多队共同败北更容易留下仍绑定
  实体部队的灰袍领主，使 `MakeHeroFugitiveAction` 在枚举中调用 `DestroyPartyAction`，从而
  暴露旧漏洞。转储缺少该地图事件的完整堆对象，故能证明军团会提高触发概率，但不能
  证明本次具体败仗一定就是协力军团败仗。

### 收养任务与改名路径复核

- 用户补充回忆：崩溃前似乎看到“烧毁村庄收人”任务发挥作用，怀疑任务完成后给新成员
  改名时抛错。源码时序是：村庄善后完整驻留结束后，`FinishRelief` 先调用
  `HeroCreator.CreateSpecialHero`，模板固定为 `gw_leader_0`；随后记录收养来源，最后由
  `RefreshPoliceClanFamilyPresentation` 给所有生成灰袍成员分配稳定姓名。任务进入待办、
  分派、补给、赶路或驻村阶段都不会创建英雄，也不会触发这次新成员改名。
- 已反编译当前 `TaleWorlds.CampaignSystem.HeroCreator` 验证：`CreateSpecialHero` 必经
  `CreateHero(..., useCharacterAsTemplate: true)`，该方法在创建对象前无条件输出
  `creating hero from template with id: <template>`。崩溃会话的
  `rgl_log_47812.txt` 全部英雄创建只有 `lord_1_55_1`、
  `spc_notable_rural_notable_1`、`lord_6_23`、`spc_wanderer_battania_2` 和最后的
  `lord_1_54`，没有 `gw_leader_0`。因此现有证据排除了“该会话中收养完成并在紧随其后的
  新成员改名处崩溃”；用户看到的更可能是任务被建立、分派或进入执行阶段。
- 仍不能只凭日志排除灰袍家族的**每日**展示刷新：`GreyWardenFamilyBehavior.OnDailyTick`
  每日都会重跑现有生成成员的性别模板、姓名和百科文案刷新，而异常恰好落在 Daily Tick
  内，且缺失托管调用栈。曾短暂加入收养创建及家族刷新前后的低频诊断路标；用户明确
  不需要继续增加这类监控，故在下一次实机测试前已完整撤回，未让该方案进入测试基线。

### 战时追捕战争边界与案件总卷可读性

- 用户在灰袍家族百科的宣战情况中发现：两个案件都尚未进入宣战阶段，对应势力战争却
  仍被保留。根因位于 `GwpPoliceWarReasonService.HasLegitimateWarReason` 和
  `CollectCurrentWarReasons`：旧逻辑只要 `ActiveTasks` 中有能匹配该势力的案件就认定为
  合法案由，没有检查 `WarDeclared`/`PoliceTaskFlowState.WarPursuit`。因此跟踪、接近或
  准备出动阶段的案件既会阻止两日和平清理，也会被宣战情况界面错误列作战争理由。
- 新增唯一判定 `TaskMaintainsFactionWar`，普通案件只有实际进入 `WarPursuit` 才能维持
  势力战争。`HasLegitimateWarReason` 与宣战情况汇总共同调用它；同一势力只要仍有任一
  战时追捕就继续维持，否则由既有两日检查恢复和平。玩家悬赏战争与纠察战争仍使用各自
  的有效状态判断，不被普通案件阶段门槛误伤。若界面在定期清理前读到无有效案由的残留
  战争，会明确提示它将在下一次两日检查中恢复和平，而不再展示前置阶段案件冒充案由。
- 案件总卷中文文本原有 `13` 个全角空格 `U+3000`，Bannerlord 当前字体会把该字符显示
  成缺字方框。所有总卷字段现统一改用可稳定渲染的半角 ` | ` 分隔符；本地化文件复核后
  `U+3000` 数量为 `0`。英文默认文本也同步使用相同分隔结构。
- 协力条目旧版把内部 `CrimeId` 直接作为“来源案件”展示，队伍解析失败时还会退回
  `LeaderPartyId`、`HelperPartyId` 或 `TargetPartyId`。现在从案件账本解析犯人姓名和本地化
  罪行，显示“主办灰袍追击受阻，需要共同围捕”的真实协力原因；人物或队伍已经失联时
  只显示本地化的“未知目标/未知灰袍队伍”，普通案件与收养善后条目也不再把内部编号作为
  可见名称兜底。
- `Release --no-restore` 构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet 警告。
  已自动部署到正常客户端和编辑器目录；两处 DLL 均为 `543744` 字节、SHA-256 均为
  `6E6BFDBEE202D947C040BE769F6EAEB9611EEC4052C8C4CA30AA482190C5E4F3`。仓库 `_Module`
  的 `25` 个正常客户端可部署文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML
  全部解析通过，中英文 README 与实机版本一致。另用 `ilspycmd` 反编译实机客户端 DLL，
  已确认两个战争理由入口都调用 `TaskMaintainsFactionWar`，其唯一普通案件通过条件为
  `FlowState == WarPursuit`；悬赏与纠察专用理由仍存在。协力详情的编译结果只把
  `CrimeId` 用于账本查询，实际可见变量为解析后的目标姓名和罪行，不再输出该编号。

### 无领主协力队进入军团交谈菜单弹窗退出

- 用户从同一复现档进入灰袍协力军团，选择“与其他成员交谈”后立即弹窗退出。引擎日志
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_37716.txt` 在
  `2026-07-22 17:50:49` 明确记录菜单顺序
  `encounter_meeting` → `army_encounter` →
  `game_menu_army_talk_to_other_members`，随后 `ExecuteAction` 内发生
  `NullReferenceException`。该日志大小 `322457` 字节、SHA-256
  `091768A3FA0A0C3ACE39DB547506D8B64557042AB06CE94C868334838EFFD7CD`。
- 同次完整转储位于
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.37716.dmp`，
  大小 `117571644` 字节、SHA-256
  `9EA0D02679C4F73513C19ED269BC5AA00BBF34F757D1373179015303BCD6A5AE`。WinDbg/SOS
  解析内层异常后，首个托管栈帧精确落在原版
  `EncounterGameMenuBehavior.game_menu_army_talk_to_other_members_item_on_condition`。
  反编译 Bannerlord `1.4.7` 原版实现可见，该方法先执行
  `mobileParty?.LeaderHero.Name`，然后才检查 `LeaderHero != null`；当重复对象是灰袍的无
  领主 `50` 人支援队时，访问 `.Name` 已经发生空引用。因此此次故障不是案件总卷、对话
  文本或灰袍领主本身造成，而是原版军团交谈菜单不支持无领主附属成员。
- 修复保留无领主支援队的军团、粮食、兵力和作战功能，只在原版交谈菜单边界屏蔽它们：
  针对有效灰袍协力军团，重复成员条件在识别到
  `IsEnforcementDelayPatrolParty && LeaderHero == null` 时直接返回不可见，不让原版空引用
  行继续执行；如果军团附属成员全部都是无领主队，父级“与其他成员交谈”入口也隐藏，
  避免打开空菜单。有真实领主的军团成员仍按原版正常显示和交谈。
- `Release --no-restore` 构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet 警告。
  已自动部署到正常客户端和编辑器目录；两处 DLL 均为 `544768` 字节、SHA-256 均为
  `27B51C332FD781145A47199D26FDF119B485B37C3771247BFC9459F59D0A0528`。仓库 `_Module`
  的 `25` 个正常客户端可部署文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML
  全部解析通过，中英文 README 与实机版本一致。`ilspycmd` 反编译实机 DLL 已确认两个
  Harmony 补丁分别命中父级菜单条件和重复成员条件，且无领主支援项返回 `false`。

## 2026-07-22 村庄重建职务、可断绝后继与震慑衰减调整

### 需求边界与经济依据

- 新任务不是现有“村庄收养善后”的改名或延长，而是独立的村庄重建总卷。对象必须是
  原版 `VillageState == Looted` 的荒废村庄；重建领主抵达后工作二十四小时、从司法公库
  支付重建款，并把村庄恢复到玩家可进入的正常状态。任务低于强制领主协力和收养善后，
  但对专职重建领主高于普通案件；没有可执行重建或公库低于安全线时才允许其接普通案件。
- 最近诊断样本中的司法公库约 `725240`，七支常驻部队每日工资合计约 `7959`；二十日
  样本公库净增 `77819`，即平均每日仍净增约 `3892`。因此费用采用当前公库的 `3%`，
  以百元取整并限制在 `15000～30000`。按该样本一次费用为约 `21800`；任务属于案件总卷，
  由灰袍完成后仍按全局边界获得 `3000` 结案拨款，净支出约 `18800`。
- 财政红线取 `max(50000, 当前全部英雄领主每日工资 × 7)`。只有扣除完整重建款后仍不低于
  红线才允许派遣和结算；资金不足的任务保留等待，重建领主可转做普通案件。扣款只发生在
  二十四小时工作完成且村庄仍处于 `Looted` 时；若原版或外部事件已经恢复村庄，任务失效、
  不扣款也不发 `3000`。若原版恢复调用意外未完成状态切换，已扣款会退回且不发结案拨款。

### 原版恢复接口与任务实现

- 反编译 Bannerlord `1.4.7` 后确认：村庄不可交互的门槛是 `Village.VillageState == Looted`，
  不是人口、户数或村庄现金。完成时调用原版
  `IncreaseSettlementHealthAction.Apply(settlement, 1 - SettlementHitPoints)`；该动作把聚落
  恢复度补到 `1`，再经原版 `ChangeVillageStateAction` 切回 `Normal`、触发状态事件并恢复
  村庄入口，避免直接写私有状态或只加户数造成“看似恢复但仍不能进入”。
- 新增 `GreyWardenVillageReconstructionBehavior`。它同时监听 `VillageLooted`，并在载入旧档
  时扫描当前全部村庄，因此更新前已经烧毁的村庄也会进入总卷；同一 `Settlement.StringId`
  只保存一条。任务保存村庄 ID/可读名称、承办队伍、入池时间、施工开始标记和施工结束时间，
  阶段为等待分配、前往村庄、驻村重建。读档后外部移动意图由每小时更新重新建立。
- 承办人失活但仍有其他重建职务持有者时，任务退回等待池而不是删除；强制协力和收养善后
  也会释放当前重建承办关系，施工进度清零但尚未发生支出。任务结束、目标自然恢复或职务
  断绝时才清理。案件总卷新增重建条目，显示村庄、承办人、职务、阶段、剩余时间、预计费用
  和当前公库红线；重建队伍不会同时被普通案件分派。

### 职务继承、断绝与成年简介

- 当前只启用两种实际 AI 职务：`Ordinary` 与 `Reconstruction`。六位初始领主中
  `gw_leader_4`（晨曦/Eadgifu）固定为首位重建官，其余五位暂归普通执法；原设定中的六席
  文字职责继续保留，训练领主及其他独立 AI 类型本次没有实现。
- `GreyWardenFamilyBehavior.SyncData` 新增 `GWPP_DutyHeroIds` 与 `GWPP_DutyKinds` 两组并行
  数据。生成成员成年时只在“当前至少仍有一名成年在世持有者”的职务中选择人数较少的一类；
  现阶段普通为五人、重建为一人，所以首批成年后继会优先补充重建。相同人数时优先重建，
  分配后永久随档保存，不会每日重算。
- 职务断绝是硬边界：某类最后一名持有者死亡后，计数为零，该类从后续成年分配候选中排除；
  最后一名重建官死亡时，现存重建总卷与移动意图一并清空，之后的新焚毁村庄不再入池。
  后继者不能凭空恢复已经断绝的功能。旧档首次载入时，现有成年生成成员会按同一规则补发
  一次职务；如果重建线在旧档中已经随晨曦死亡而归零，迁移同样不会复活它。
- 成年收养成员和家族出生成员的百科正文现在写出正式职务；案件总卷的重建承办字段也显示
  本地化“灰袍村庄重建官/灰袍巡法官”。未成年介绍继续按儿童与受训阶段显示，不提前受职。

### 震慑与验证

- `BaseRecoveryPerDay` 从 `0.09` 降为 `0.009`，上下限从 `0.04～0.175` 同比降为
  `0.004～0.0175`；勇敢、荣誉、仁慈和谋略的每日修正也全部乘以 `0.1`，确保不是只改
  基础值却被性格修正或上下限抵消。累计犯罪与被捕数字、震慑上限和欲望倍率没有改变。
- `Release --no-restore` 构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet 警告。
  已部署到正常客户端和编辑器目录；两处 DLL 均为 `569856` 字节、SHA-256 均为
  `116A5A2632D64F16D5DDC300EF8646E383603BADA2474535AB85541060E6FB0E`。仓库 `_Module`
  的 `25` 个正常客户端可部署文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML
  全部解析通过，中英文 README 与实机版本一致。
- 已用 `ilspycmd 9.1.0` 反编译实机客户端 DLL，确认完成链实际调用
  `TrySpendVillageReconstructionFunds` → `IncreaseSettlementHealthAction.Apply` →
  `CreditSuccessfulCaseCompletion`，失败分支存在退款；财政实现确认为扣款后至少保留
  `max(50000, 7 × 日工资)`，成年职务实现只保留 `Count > 0` 的存续类别，震慑反编译常量
  为 `0.009/0.004/0.0175` 及同比缩小的性格修正。尚未伪造一条实机村庄劫掠或跳过二十四
  小时，因此下一轮游戏内验收应重点观察旧档烧毁村庄入池、晨曦赶路/驻留、完成后重新可进入、
  公库净扣款与中途保存读档；训练士兵领主明确留待后续单独设计。

## 2026-07-22 六席职务分流、原版请求任务与分类震慑

### 最终需求边界

- 六名初始领主按顺序保存六条可断绝职务：`gw_leader_0` 训练、`gw_leader_1`
  商队保护、`gw_leader_2` 村民与村庄保护、`gw_leader_3` 原版地方请求、
  `gw_leader_4` 村庄重建、`gw_leader_5` 未来玩家请求。本轮实际新增商队案件优先、
  村庄案件优先、原版请求和既有重建四项行为；训练与玩家请求只有人物方向，没有伪造尚未
  设计的玩法。既有玩家通缉案继续属于旧犯罪系统，不因第六职务尚未实现而被关闭。
- 专职持有者先领取本职目标；本职没有可领工作时可以进入共享池接其他工作。重建和原版
  请求的跨职务承办关系也会被普通案件分派器识别为占用，不能一人同时再接一宗普通案件。
  强制协力和村庄收养善后仍可释放普通、重建或请求承办关系后优先调动该领主。
- 职务保存键继续使用 `GWPP_DutyHeroIds/GWPP_DutyKinds`。为兼容已写入存档的重建职务，
  `LegacyOrdinary=0`、`Reconstruction=1` 数值保持不变，新职务从 `2` 起追加。核心六人每次
  校准为固定顺序；旧档中尚为 `LegacyOrdinary` 的成年后继只会补入仍有成年在世持有者的
  职务。某类最后一名持有者死亡后计数归零，之后成年者不能复活该功能。
- 新普通犯罪只有在对应职务仍存续时才进入池：商队案要求商队保护职务存续，攻击村民、
  劫掠和烧村要求村庄保护职务存续。已经进入池的旧案按既有承诺继续完成，不在职务死亡时
  强行删除。入池检查必须先于改写同一罪犯的现有案卷；否则一条来自已断绝职务的新事件会
  把仍有效的旧案罪名覆盖，本轮已把该检查顺序修正。

### 普通一百与其他类型无上限

- `CrimePool.MaxTaskPoolEntries=100` 只统计 `CrimePool` 中 `HasOpenCase` 的普通犯罪案。
  过去各调用点用 `100 - 协力任务数 - 收养任务数` 裁剪，等于让其他任务挤占普通容量；
  现已统一改为 `TrimOpenCasesToCapacity(100)`，并删除不再有调用意义的
  `GetForcedTaskCount`。
- 原版地方请求直接读取 `IssueManager.Issues`，重建和收养各自保存独立列表，协力任务继续
  使用其独立集合。它们不复制进 `CrimePool`，没有共同总数上限，因此全部任务总数可以超过
  一百；案件总卷分别显示“普通案件 x/100”和“其他任务 x（无上限）”。
- 各类型仍按自身对象去重和失效规则清理，不把“无总数上限”误解成保留已经结束的死条目。
  原版请求被外部解决、村庄自然恢复或协力源案失效时都会正常撤卷，且不发结案拨款。

### 原版请求接口调查与六小时结案

- 反编译本地 Bannerlord `1.4.7` 的 `IssuesCampaignBehavior.OnSettlementEntered`：普通 AI
  领主进入自家聚落时只有 `0.05`、进入其他聚落时只有 `0.01` 的抽取机会；抽中后仍要求
  `CanBeCompletedByAI()` 且 `IsOngoingWithoutQuest`，然后直接调用
  `CompleteIssueWithAiLord`。`IssueBase.CompleteIssueWithAiLord` 会派发
  `IssueFinishedByAILord` 并执行 `IssueFinalized()`。原版这里没有统一的“额外增加繁荣度”
  代码；实际效果来自具体请求的原版结案事件和停止其持续影响。
- 新增 `GreyWardenIssueResolutionBehavior`，把所有仍在进行、未转为玩家任务、允许 AI
  完成、且发布者能解析到城镇或村庄的原版请求视为无上限动态任务池。没有按要人类型排除，
  因而村庄首领、城镇商人及流氓头目均可进入。
- 承办队抵达发布聚落后记录 `当前小时 + 6`，持续发出访问/驻留意图；六小时届满才调用
  原版 `CompleteIssueWithAiLord(承办领主)`，成功后调用统一的
  `CreditSuccessfulCaseCompletion()` 发三千第纳尔。途中或驻留时如果请求已由外部完成，
  任务撤销且不发钱。
- 持久化使用四组并行键：`GWPP_IssueDutyIssueIds`、`GWPP_IssueDutyOwnerIds`、
  `GWPP_IssueDutyPartyIds`、`GWPP_IssueDutyWorkEndHours`。请求用“请求 StringId + 发布者
  Hero StringId”组成稳定键，避免不同发布者使用相同请求类型时互相覆盖。最后一名请求职务
  持有者死亡时，承办和移动意图一并释放，动态请求池不再展示。

### 分类震慑与 AI 欲望入口

- 旧存档的 `DirectDeterrencePoints/SharedDeterrencePoints` 保留为村庄暴力方向，继续压制
  攻击村民和劫掠村庄。商队方向新增独立的被捕次数、本人点数、共享点数、共享次数、更新
  时间和最近执法时间，并以 `gwp_h_i_caravan_*` 六个字段随长期人物档案保存。
- 抓捕时从当宗 `CrimeRecord.CrimeCategory` 生成带分类的 `CaptureShock`；本人、同族和同场
  目击传播全部只写入相同方向。两套点数分别衰减，均沿用已降到早期十分之一的恢复速度。
- 村庄劫掠继续通过 `PoliceRaidDeterrenceModel` 使用村庄倍率。对移动商队和村民的原版攻击
  欲望，在所有 `AiHourlyTick` 评分器运行完毕后的 dispatcher postfix 中检查目标
  `CaravanPartyComponent/VillagerPartyComponent`，分别乘商队或村庄倍率；
  `PoliceMobilePartyAIModel.ShouldConsiderAttacking` 的最终许可也改为按目标类型读取同一套
  分类倍率，避免村庄震慑错误压制商队或反之。

### 人物文案、构建与静态验证

- 六名核心人物和成年后继者的百科正文只通过操练场、商路、村庄、地方请愿、废墟重建或
  特别来函等经历暗示日常分工，不在个人简介中直接写“某某官”。内部职务标题仍保留给案件
  总卷承办字段和开发诊断，以便确认调度结果。
- 中英文玩家 README 已合并为 `2026-07-22 v1.4.7-r6`，各自只保留 `r6/r5` 两条正式记录；
  设定文档原先“待实现”的旧六岗位方案已替换为本轮实际职务、共享池和待设计边界。
- 最终 `Release --no-restore` 构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet 警告。
  正常客户端与编辑器 DLL 均为 `598016` 字节，SHA-256 均为
  `C436DE293AF3F4D6C1AA9A3A2EA188DDAD180E681AFB3D8116E5D7359171671C`。
  仓库 `_Module` 的 `25` 个正常客户端可部署文件与实机相比缺失 `0`、哈希不一致 `0`；
  `18` 个 XML 全部解析通过，两个 README 和简中词条文件的仓库/实机哈希分别一致。
- 用 `ilspycmd 9.1.0` 反编译最终实机客户端 DLL：全部十处普通案卷裁剪均为固定 `100`，
  `GetForcedTaskCount` 与任何“100 减其他任务”引用均为 `0`；请求完成链存在唯一
  `CompleteIssueWithAiLord` 调用和六小时常量；案件总卷包含“Other tasks (uncapped)”；
  最终 DLL 同时存在商队/村民两类评分入口、六席固定映射和跨职务重建占用保护。
- 尚未在游戏中制造超过一百条其他任务，也未完整跑完一宗六小时原版请求；下一轮实机验收
  应优先观察旧档请求池数量、圣铎驻留六小时后原版任务消失与公库增加三千、专职无目标时
  跨职务补位，以及两类震慑在百科和 AI 行为上的独立变化。

## 2026-07-22 原版请求的地方发展、静默结案与百科姓名修复

- 实机首轮观察未发现本轮职务/请求系统报错。用户要求撤掉原版请求完成时出现在左下角的
  机制说明，因为这属于灰袍后台工作，不需要主动打断玩家；同时希望完成请求对发布地有
  一点实际发展收益，并报告族长等核心人物的百科正文姓名与页面显示姓名不一致。
- 左下角文本来自 `GreyWardenIssueResolutionBehavior.UpdateAssignments` 成功分支新增的
  `InformationManager.DisplayMessage`，不是原版请求消息或调度诊断。本轮只移除这条后台
  结案通知；案件总卷仍能查询请求状态，玩家自身赎罪、被捕、任务交付等真正需要反馈的
  消息没有删除。成功分支保留 diagnostics-only 的 `ISSUE_DUTY_COMPLETED` 行，正式玩家
  DLL 中诊断实现仍按既有发布规则关闭。
- 地方收益放在原版 `CompleteIssueWithAiLord` 成功调用之后、三千第纳尔结案拨款之前。
  发布地是村庄时给 `Village.Hearth += 5`，是城镇时给 `Town.Prosperity += 5`；数值明显低于
  许多原版玩家任务常见的三十至一百点一次性收益，只作为灰袍确实解决地方矛盾的稳定小额
  回报。请求被其他人抢先完成或自然失效时不会进入该分支，因此既不加发展也不发拨款。
- 姓名错位根因已经确认：人物显示名使用 `gwp_hero_*` 本地化，简中为“梵蒂、约珥、弥瑟、
  圣铎、晨曦、暮光”；百科 `gwp_enc_*` 却把英文内部模板名“埃塞尔弗莱德、米尔德斯里斯、
  温弗莱德、伍尔夫希尔德”等直接写死在简中文本里。现把六篇核心百科统一改成
  `{HERO_NAME}` 模板，并在写入 `hero.EncyclopediaText` 时注入该英雄当时的 `hero.Name`。
  因而简中族长正文会显示“梵蒂”，其他语言、改名或后续本地化也会自动与页面姓名一致，
  不再维护两套可能漂移的人名。
- 中英文 `v1.4.7-r6` 玩家日志已合并记录静默结案、少量地方发展与百科姓名修复；设定文档
  同步记录村庄五点户数/城镇五点繁荣度的准确实现。最终 `Release --no-restore` 构建通过，
  `0` 错误、`43` 条既有可空性/离线 NuGet 警告。客户端与编辑器 DLL 均为 `598016` 字节，
  SHA-256 均为 `D813829AABCC669F70F2BEB3D6479291F9BB9E6AA13121ED0BA35D574F56A415`；
  `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML
  全部解析通过。
- 最终反编译实机 DLL 已确认请求完成链为 `CompleteIssueWithAiLord` →
  `ApplyLocalDevelopmentGain` → 三千拨款，村庄/城镇分支常量均为 `5f`；该行为类中已经没有
  `DisplayMessage/InformationMessage`。核心百科六条模板均含 `{HERO_NAME}`，写入点用
  `GwpText.Get(..., "HERO_NAME", hero.Name)` 解析当前姓名。下一次实机只需观察完成一条村庄
  与一条城镇请求后的户数/繁荣度各增加五点，以及六名核心人物正文姓名是否与标题一致。

## 2026-07-22 原版犯罪欲望边界与跨职务犯罪优先

- 用户确认练兵方案仍需继续构思；本轮讨论中提出的“练兵领主主动并入其他任务军团并让
  同军团士兵快速获得经验”明确作废，没有写入代码、设定或存档。训练职务继续只保存人物
  方向，梵蒂没有新增自动组军或经验加成。
- 反编译 Bannerlord `1.4.7` 的 `AiEngagePartyBehavior.AiHourlyTick` 后确认：攻击商队和攻击
  村民由同一个原版接战评分器产生，最终行为枚举同为 `GoAroundParty`，所以原版没有两个
  可直接开关的“商队欲望/村民欲望”字段；但评分器会为每一个具体目标分别创建
  `AIBehaviorData(targetParty, ...)` 并加入独立分数元组。灰袍在所有原版评分结束后的最终
  auction postfix 中读取每个元组的目标组件，因此能够把 `CaravanPartyComponent` 元组只乘
  商队震慑倍率，把 `VillagerPartyComponent` 元组只乘乡土震慑倍率。二者虽然来自同一个
  原版评分器，实际候选分数已经切实分开，不是只在界面上换了名称。
- 烧村不走上述移动部队接战元组，而走聚落劫掠/`ArmyTypes.Raider` 目标评分；现有
  `PoliceRaidDeterrenceModel` 单独在该入口乘乡土震慑倍率。因此最终分类是两套：商队震慑
  只压制商队目标；乡土震慑同时压制村民目标和烧村目标，符合此前“村民与烧村为同一职责”
  的边界。
- 任务回退优先级进一步明确：专职请求者和专职重建者仍先完成自己的本职，这是六席差异；
  其他职务只有在本职无可领工作时才跨池补位。跨职务补位现在先检查
  `CrimePool.IsDispatchReady`：只要仍有一宗未分配且可追捕的普通犯罪，空闲领主不会被
  原版地方请求或重建任务抢走，而会留给普通案件分派器处理商路/乡土案件。只有普通犯罪
  已清空，才跨职务帮助没有时效压力的地方请求和村庄重建。
- 中英文 `v1.4.7-r6` 玩家日志和设定文档已合并这一优先级。最终 `Release --no-restore`
  构建通过，`0` 错误、`43` 条既有警告；客户端与编辑器 DLL 均为 `598016` 字节，SHA-256
  均为 `A36FED96BE3702A46BE18E37F36B82FC4F1E4CD339B476DA21F2E6BA53171F3B`。
  `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML
  全部解析通过。反编译实机 DLL 已确认请求与重建两个跨职务候选分支都先要求
  `!CrimePool.IsDispatchReady`，专职分支仍保持本职优先。

## 2026-07-22 跨职务回退次序与人物震慑界面精简

- 用户进一步明确空闲领主跨职务补位的完整次序应为“解决犯罪 → 帮助重建 → 处理原版地方
  请求”。此前只保证了犯罪优先，但犯罪清空后，地方请求仍可能先于重建取得空闲队伍。本轮在
  `GreyWardenIssueResolutionBehavior.IsCandidate` 的跨职务分支加入
  `!GreyWardenVillageReconstructionBehavior.HasAvailableReconstruction()`：只要存在可接的重建
  任务，跨职务领主不会先接原版请求。专职请求者仍先做请求、专职重建者仍先做重建，六席本职
  差异没有被全局回退次序抹掉。
- 人物百科旧“案件记录与震慑”弹窗同时列出个人点数、家族点数、总点数、分类总点数和三种
  倍率，既重复又容易把多个来源混成一个概念。本轮把正文压缩成两组实际行为：乡土组显示
  “攻击村民欲望”和“烧村欲望”，商路组显示“攻击商队欲望”；另保留一行总案件/抓捕记录和
  最近执法、地图状态、位置，删除玩家无法据此作出不同判断的中间合计。
- 攻击村民与烧村现在在界面中读取同一个 `VillageMultiplier`，烧村一行明确写出“与攻击村民
  共用同一压制”，从展示层保证二者百分比口径完全一致；攻击商队单独读取
  `CaravanMultiplier`。这与实际 AI 入口一致：乡土倍率同时作用于村民部队和烧村，商路倍率
  只作用于商队。
- 来源拆分不再写“家族震慑”。每一组改为“本人 / 他人传递”：本人是该人物亲自被灰袍抓捕
  累积的直接经验；他人传递沿用共享字段，实际包含同族成员传递和现场目击传播。界面同时判断
  哪一边较高，显示“本人经历为主”“族人转述或亲眼目击为主”“两者相当”或“暂无有效压制”，
  因而能直接回答当前行为主要受本人教训还是外部消息影响。
- 简中新增的 `gwp_det_ui_*` 词条均已进入正常客户端语言文件；中英文 `v1.4.7-r6` 玩家日志
  与设定文档已合并记录新回退次序和精简后的可见信息。最终 `Release --no-restore` 构建通过，
  `0` 错误、`43` 条既有警告；客户端与编辑器 DLL 均为 `598528` 字节，SHA-256 均为
  `129CD671BB926C7D5143A7E0BA1DE87AB539CCE3A7E3833AC1867A55A65ACEC2`。
- 仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML
  全部解析通过。反编译最终实机 DLL 已确认：跨职务请求候选只有在普通犯罪和可用重建都为空
  时才成立；村民与烧村两行都读取同一个 `VillageMultiplier`，商队读取独立
  `CaravanMultiplier`；活动弹窗不再引用旧的个人/家族/总震慑重复词条。下一轮实机可直接
  检查同一人物两条乡土欲望百分比始终相同，以及抓捕本人或传播给同族/目击者后来源主次是否
  按预期变化。

## 2026-07-22 玩家招募使者断粮回城修复

- 长时间实机稳定性测试未再出现报错，但玩家声望已经达到二十后始终没有收到灰袍受托猎手
  邀请。现行门槛和存档标记本身仍然有效：`PlayerBountyBehavior` 每小时在声望不低于二十、
  `gwp_recruitment_offered/accepted` 均为假时生成一次招募使者。
- 实机诊断精确确认招募确实已经触发。游戏小时 `627035.29` 出现
  `gwp_recruit_85981`，职责为 `Approach:player_party`、距玩家 `20.41`，但其生成库存
  `food=0.00`；到 `627035.99`，它已经改为 `Visit:town_S5`。之后使者长期停在该城，粮食
  降到 `-1`，最终二十人全部负伤。故障不在声望计算或邀请存档标记，而在使者生成后没有携带
  任何粮食，同时小时维护把“无粮”解释成放弃邀请并立即返城。
- 新使者生成后现在调用 `PoliceResourceManager.ProvisionTemporaryDutyParty`，按其他一次性灰袍
  队相同规则携带二十日口粮；尚未送达邀请时如果旧档中的健康使者断粮，也会补足口粮而不是
  返城。完成或拒绝招募后仍按原设计返回并销毁，不会成为永久地图部队。
- 为修复当前测试档，小时维护发现唯一受追踪使者已经没有健康成员时，会销毁该失效队伍；同一
  小时末仍满足声望条件便重新生成健康且带粮的使者。返回队抵达目标聚落的判断同时增加
  `CurrentSettlement == target`，避免处在城内却因城门坐标与聚落中心距离不同而无法清理。
- 统一欲望层的远距离 `Approach` 只负责追到玩家的动态位置，本身不会保证中立接触打开对话。
  因此使者距离玩家三以内时清除地点接近意图，恢复原版 `EngageParty` 接触；现有
  `OnMapEventStarted` 随后把这次中立遭遇转成招募对话，接受/拒绝、发放指挥官装备和招募状态
  保存链保持不变。
- 用户补充要求玩家持续移动时不能让一支使者无限追赶。每次派遣现在保存
  `gwp_recruitment_patrol_dispatch_hour`；五个游戏日仍未接触玩家，使者就结束追赶，进入离
  自己最近的城镇后销毁。下一次每小时资格检查仍读取玩家当时的位置，经既有
  `FindNearestTown(MainParty.Position)` 从离玩家最近的城镇重新派出。计时随存档保存，读档
  不会重新获得完整五日；接受或拒绝邀请后仍走原有返城链，不会触发换班重派。
- 中英文 `v1.4.7-r6` 玩家日志已合并该修复。最终 `Release --no-restore` 构建通过，`0` 错误、
  `43` 条既有可空性/离线 NuGet 警告，并已自动部署正常客户端、编辑器 DLL 与 ModuleData。
  两份 DLL 均为 `599040` 字节，SHA-256 均为
  `C5A5C5C891BA829E5EF5797CD17FD27510648AC5FBBE23B8ADDCFC5BC5ED9225`。反编译最终实机
  客户端 DLL 已确认：生成链含 `ProvisionTemporaryDutyParty`；小时链含零健康成员替换、健康
  断粮使者补粮；三单位接触范围内会执行 `ClearIntent` 后的 `SetMoveEngageParty`；派遣计时
  已用 `SyncData<double>` 保存，超时常量编译为 `120` 小时，超时返城目标重新按使者当前位置
  选择。下一次载入当前档后应在一个游戏小时内看到旧的全员负伤使者被替换；新使者会从附近
  城镇携粮追上玩家并主动打开邀请对话。

## 2026-07-22 灰袍受托身份、重新加入与主动退出

- 本轮把“公开声望”和“当前受托身份”明确分开。身份仍使用旧档键
  `gwp_recruitment_accepted` 保存，不需要重开档；声望达标本身不再能代替加入状态。
  玩家向灰袍领主征调兵员时，对话入口和每个具体兵员选项都重新检查“已加入”与
  对应声望门槛；因此退出后即使声望仍很高，也不能再调兵。
- 招募使者的首次拒绝仍会让该使者返城，也不会再派第二名使者骚扰玩家；但拒绝不再是
  永久锁死。首次拒绝不算“主动退出”，因此当公开声望仍不低于二十时，玩家可在普通交谈中
  向任意有领主的灰袍将领表示重新考虑。确认后把 `accepted` 恢复为真，并再发一套指挥官装备，与首次接受使者
  邀请保持一致。该选项排除无领主巡逻/支援队和正在对玩家执法的灰袍部队，避免与执法对话冲突。
- 已加入的玩家可以向任意普通灰袍领主提出退出，并需经过二次确认。退出后保留已发放的
  装备，但该装备不再代表任何组织权限；已接的悬赏立即以“退出灰袍”原因取消，恢复相关和平，
  释放任务护送灰袍的 AI 限制，清除待领赏和追踪状态；旧悬赏通知的 `IsValid` 也会因身份失效而
  自动消失。
- 主动退出次数新增存档键 `gwp_recruitment_voluntary_exit_count`，旧档缺少该键时自然从零开始。
  第一次主动退出后，领主处的下次申请要求四十声望；第二次退出后要求六十；第三次退出后
  计数固定为三，重新加入对话永久不再成立。四十和六十阶段只能由玩家主动寻找领主；使者生成
  条件仍要求 `gwp_recruitment_offered=false`，而首次回应或任意一次退出都会使其为真，因此后两阶段
  不会再派使者。退出确认文本会在前两次直接告知下次门槛，第三次则明确警告这是永久退出。
- 对玩家实际发放的灰袍福利已统一梳理：悬赏任务需要“已加入 + 声望二十 + 穿齐指挥官套装”，
  接下悬赏时可调用已有承办灰袍作为任务护送；付费征调兵员需要“已加入 + 各级声望门槛”；
  `GwpBattleReinforcementBehavior` 所实现的危急野战增援就是用户回忆中的“支援”福利，本轮也改为
  只在当前已加入且声望不低于二十时进行两次原有概率检查。村民因正向公开声望筹集的金钱是
  社会感谢，不是成员薪津，因此仍只看正向声望，不因退出灰袍而被清零。
- 中英文 `v1.4.7-r6` 玩家日志和设定文档已同步。最终 `Release --no-restore` 完整构建通过，
  `0` 错误、`43` 条既有可空性/无法连接 NuGet 漏洞索引警告。客户端与编辑器 DLL 均为
  `603648` 字节，SHA-256 均为
  `20F2A39E7CB2CF2CBC38671634DF12B7EA862223B9183674C085C1CC2A928024`。仓库 `_Module` 的 `25` 个正常客户端文件
  与实机相比缺失 `0`、哈希不一致 `0`；`18` 个 XML 全部解析通过，新增九个简中词条的 ID
  均恰好出现一次，两份 README 都只保留 `r6/r5` 两条正式记录。
- 用 `ilspycmd 9.1.0` 反编译最终实机客户端 DLL 已确认：新计数以 `SyncData<int>` 存档并夹到
  `0..3`；重新加入门槛编译为 `20 + exitCount * 20`且先要求 `exitCount < 3`；每次确认退出使计数
  最多增加到三；退出提示的前两次分支动态写入四十/六十，第三次分支使用永久退出文本。
  调兵对话入口与具体选项均先读取
  `IsRecruitedByGreyWardens`；野战增援在原有声望检查前同时读取该身份；领主对话存在重新加入、
  退出二次确认和取消分支；退出后会调用 `FailQuestMembershipEnded` 并清理悬赏状态；悬赏通知的
  有效性检查同样读取加入状态。使者的五日换班也仍编译为 `120.0` 小时，没有被本轮对话改动覆盖。

## 2026-07-22 玩家完成案件池追捕时复用灰袍震慑

- 用户要求玩家完成已进入案件池的任务时，对案件目标施加与普通灰袍领主相同的震慑。
  实现边界是玩家已正式接下的两条现有路径：受托猎手悬赏和负声望赎罪任务。玩家随机路过并打败
  一名虽在案件池但没有交给玩家的罪犯，不会被伪装成玩家完成的灰袍任务。
- `PoliceAIDeterrenceBehavior.RegisterPlayerCompletedCase` 统一复用原有震慑入口：对目标调用
  `GwpAiDeterrenceState.RegisterPoliceArrest`，因而增加同类案件的被捕累计和本人震慑；再按原有一半
  传递量向目标同族成员和同场落败方的合格领主施加共享震慑。已有未结案的其他罪犯不被当作普通目击者，
  目标同族也不会同时叠加“族人传递 + 现场目击”两份分数。
- 案件类别原样传入：袭击商队只增加商路本人/共享震慑，袭击村民、劫掠和烧村只增加
  乡土本人/共享震慑。玩家是否在战后手动收下该领主不再影响任务结算；只要玩家亲自位于胜方并击败
  指定目标，即按“已完成灰袍案件”记入震慑。
- 玩家护送灰袍可能同场参战并由原有 `HeroPrisonerTaken` 链先登记一次。为避免玩家任务结算再登记第二次，
  新逻辑以 `MapEvent + offender hero id` 去重：尚未结算的原有捕获批次和已结算的近期批次都会拒绝
  同场同人的重复计数；近期去重集每日清理，不进入存档也不长期持有旧战斗对象。
- 为保证读档后完成任务仍能找回正确英雄和分类，受托悬赏新增
  `gwp_bounty_target_hero_id/gwp_bounty_crime_category`，赎罪任务新增
  `gwp_enf_atone_target_hero_id/gwp_enf_atone_crime_category`。旧档缺失分类时会先从当前案件记录回填；
  每小时维护还会在案件尚未被其他结算链移除前，将英雄与类别补入运行时字段，覆盖更新前已经接下且尚未
  完成的旧悬赏/赎罪任务。最终仍无法分类时沿用旧捕获逻辑的保守乡土分支。
- 这一后台结果没有新增左下角机制说明；玩家可以在目标及其族人/目击者的百科震慑界面中验证结果。
  中英文 `v1.4.7-r6` 玩家日志和设定文档已同步。
- 最终 `Release --no-restore` 完整构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet 警告；随后文档同步的
  增量构建为 `0` 错误、`1` 条离线 NuGet 警告。客户端与编辑器 DLL 均为 `607744` 字节，SHA-256 均为
  `2C1E546793AD09CBD19B47862906F076DD008DE24DDA1B51D6215FD86B93B11E`。仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、
  哈希不一致 `0`；`18` 个 XML 全部解析通过，两份 README 仍只保留 `r6/r5` 两条正式记录。
- 用 `ilspycmd 9.1.0` 反编译最终实机客户端 DLL 已确认：受托悬赏和赎罪胜利各有唯一一处
  `RegisterPlayerCompletedCase` 调用；共用入口中存在本人登记、族人传递、落败方目击传递和场次去重；
  四个新存档键及两条旧任务每小时回填逻辑全部进入最终 DLL。

## 2026-07-22 重建通知、公库总览与灰袍会面语气

- 村庄重建成功的左下角通知继续保留，但现在只显示承办灰袍与完成重建的村庄，不再显示本次
  公库支出或支出后的余额。实际 `cost/grant/reserve` 仍只写入诊断日志，便于本地测试经济闭环，
  不再作为玩家通知内容。
- 案件总卷的 `GwpCaseArchiveVM` 新增 `TreasuryText`，每次打开总卷或点击刷新时直接读取灰袍
  族长钱包这一司法公库的实时余额。余额单独显示在任务池摘要下方，案件列表相应下移，避免把
  余额塞进过长的汇总句或与首条案件重叠。该读取接口只返回非负余额，不进行任何资金转移。
- 已相识灰袍领主原有的百分之五十动态问候概率和声望分段不变；七档问候与后续承接句全部改为
  直接、自然的白话表达，删去“旧帝国法不奖赏空话”“法先于刀兵”“行止端正”等容易显得古风
  或训诫化的措辞。正负声望仍分别表达信任、普通接待、纠正机会、案件未结和即将采取行动。
- 中英文 `v1.4.7-r6` 玩家日志与设定文档已同步。`Release --no-restore` 完整构建通过，`0` 错误、
  `43` 条既有可空性/离线 NuGet 警告，并已自动部署客户端、编辑器 DLL、GUI、ModuleData 与
  两份玩家 README。客户端和编辑器 DLL 均为 `607744` 字节，SHA-256 均为
  `0A527D1A4969E739190719110A4549F7EB101CD9B0B68DDCB177553A8F6A557D`。仓库 `_Module` 的
  `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`；`18` 个 XML 全部解析通过，
  新增公库余额词条及九个调整词条的 ID 都恰好出现一次，两份 README 仍只保留 `r6/r5`。
- 用 `ilspycmd 9.1.0` 反编译最终实机客户端 DLL 已确认：重建完成通知只构造 `VAR_1/VAR_2`
  四元素参数数组，金额只留在紧随其后的诊断调用；`TreasuryText`、
  `GetJudicialTreasuryBalance` 与刷新时的余额赋值均进入 DLL；七档新问候及新的后续承接句全部
  进入最终程序集。实机 Prefab 同时含 `@TreasuryText` 绑定，案件列表上边距已从 `126` 调整为
  `158`，为余额行留出独立空间。

## 2026-07-22 招募使者主动接触后的重复对话

- 用户用同一存档对照出两条路径：让招募使者主动撞上玩家并接受后，使者无法离开且会反复重开
  同一段招募对话；读档后由玩家主动点击使者再接受，则使者正常离开。拒绝路径尚未实测，但与
  接受共用同一套遭遇收尾，必须一并修复。
- 实机诊断中的 `gwp_recruit_29300` 与症状一致：接近玩家后由 `Approach` 切到
  `EngageParty:player_party`；会面时欲望一度清空并出现返回城镇的原版行为，但下一小时又恢复
  `EngageParty:player_party`。代码对照确认只有使者主动接触会依赖
  `OnMapEventStarted -> PlayerEncounter.DoMeeting()` 这条额外强制会面入口；该入口此前不检查
  `_recruitmentOffered/_recruitmentAccepted`，而接受/拒绝后又在对话和遭遇尚未完全关闭时立即下达
  返城命令，原版遭遇收尾可以覆盖该命令并再次触发会面。
- 接受、拒绝和“招募已经处理”的兜底对话现在统一调用
  `CloseRecruitmentEncounterAndReturn`：先写入返回状态和 `LeaveEncounter`，立即给出一次返城意图，
  再通过 `ConversationEndOneShot` 等待对话真正关闭。关闭后若当前仍是与招募使者的玩家遭遇，
  明确调用 `PlayerEncounter.Finish()`，随后再次下达返城命令，避免该命令被遭遇清理覆盖。
- 首轮修复令 `OnMapEventStarted` 在邀请已经接受或拒绝后直接返回，不再调用 `DoMeeting()`；其
  设计目标是让使者主动接触与玩家主动点击走向同一结果。拒绝同样应结束遭遇、返城销毁，并保留
  以后向灰袍领主重新申请的资格。该轮改动不改变邀请门槛、装备发放、主动退出次数或五日换班规则。
- 中英文 `v1.4.7-r6` 玩家日志已同步。`Release --no-restore` 完整构建通过，`0` 错误、`43` 条
  既有可空性/离线 NuGet 警告，并已自动部署客户端、编辑器 DLL 和两份 README。两份 DLL 均为
  `607744` 字节，SHA-256 均为
  `3B130D60605BD14551B8B6EB1E6C1D5C7D193E6E23E845B28EF95B43F579ABC9`。仓库 `_Module` 的
  `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`；`18` 个 XML 全部解析通过，
  两份 README 仍只保留 `r6/r5` 两条正式记录，`git diff --check` 通过。
- 用 `ilspycmd 9.1.0` 反编译最终实机客户端 DLL 已确认：接受、拒绝和兜底对话都调用统一收尾；
  收尾注册了 `ConversationEndOneShot`，回调中包含招募使者身份复核、`LeaveEncounter`、
  `PlayerEncounter.Finish()` 和第二次返城命令；强制会面入口在两个招募完成状态任一为真时都会
  提前返回。
- 用户随后用使者主动接触路径复测，确认首轮遭遇收尾修复仍不足。新一轮实机日志中的
  `gwp_recruit_48414` 在 `campaignHour=627225.17` 暂时离开玩家并转向城镇，但
  `campaignHour=627226.19` 又重新获得 `Approach:player_party`，随后再次切回
  `EngageParty:player_party`。这证明问题不只是返城命令被覆盖：玩家已经点击接受，但
  `_recruitmentOffered/_recruitmentAccepted` 在后续小时检查时仍为假，决定本身根本没有提交。
- 进一步对照两条对话路径确定根因：接受与拒绝的状态修改原本挂在使者最后一句 NPC 回应的
  consequence 上，而不是玩家点击选项的 consequence 上。使者主动拦截形成的强制会面会在 NPC
  回应 consequence 执行前重新启动对话；玩家主动点击使者时能完整走完回应，所以只有后者正常。
  现在接受与拒绝 consequence 均移到对应的 `AddPlayerLine`：玩家点击选项的同一刻就提交状态、
  发放接受奖励或记录拒绝，并启动统一遭遇收尾；NPC 回应只负责显示文本，不再承担关键状态修改。
- 按用户要求为这条流程加入专用、低频事件监控，不扫描额外角色，也不增加逐帧负担。开发构建会
  在既有 `GreyWarden-AI-Diagnostics.log` 中记录 `RECRUIT_DIALOG_OPENED`、接受/拒绝的
  `SELECTED` 与 `COMMITTED`、`RECRUIT_CLOSE_QUEUED`、初次及对话关闭后的返城命令、地图事件、
  强制会面请求/阻断以及遭遇关闭异常。每条记录同时携带 offered、accepted、returning、当前
  encounter 与 conversation party，若复测仍异常即可精确看出流程停在哪一步；正式玩家构建中的
  诊断实现仍为空，不会创建日志。
- 第二轮 `Release --no-restore` 完整构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet 警告，
  并已部署客户端与编辑器 DLL。两份 DLL 均为 `610816` 字节，SHA-256 均为
  `07677544951423DF8743CABACD78EAAF9D83E4671523F5F7B652ABD1E989B188`。用 `ilspycmd 9.1.0`
  反编译最终实机客户端 DLL 已确认：接受/拒绝 consequence 位于两条玩家选项上，两条 NPC 回应
  consequence 均为空；状态写入发生在统一收尾之前；新招募事件监控、会后 `PlayerEncounter.Finish()`
  与强制会面完成态阻断均已进入最终程序集。仓库 `_Module` 的 `25` 个正常客户端文件与实机相比
  缺失 `0`、哈希不一致 `0`；`18` 个 XML 全部解析通过，两份 README 仍只保留 `r6/r5` 两条
  正式记录，`git diff --check` 通过。
- 用户第三次实机复测提供了决定性证据：`gwp_recruit_64730` 在点击接受时依次记录
  `RECRUIT_ACCEPT_SELECTED` 与 `RECRUIT_ACCEPT_COMMITTED`，且后者明确为
  `offered=True; accepted=True`，证明第二轮已经修正状态提交。每次关闭兜底对话后也都记录
  `RECRUIT_CONVERSATION_ENDED`，随后 `encounterActive=False`，证明 `PlayerEncounter.Finish()`
  确实生效；但约 `0.2` 至 `0.7` 秒后又产生新的 encounter，并再次显示“事务已经了结”。
  同时日志中的使者原版状态始终残留 `default=EngageParty; targetParty=player_party`，而新增的
  `Visit:town_EN5` 只是等待下一个 AI 检查兑现的欲望。根因因此最终确定为：结束 encounter 并不
  清除原版追击命令或双方的物理重叠，下一次 AI 检查前引擎已先创建了全新的 encounter。
- 为立刻阻断循环，曾短暂试作在 `ConversationEndOneShot` 结束 encounter 后直接销毁一次性使者；
  对应中间 DLL 为 `610816` 字节，SHA-256
  `39C3F7AF128DCFF609DAEAEEEFE0078F17DCCE4BBED2A44BC54228470A8FB6A1`。用户明确指出一整队人突然
  消失不符合世界表现，因此该方案在用户实测前即被否决并撤回；这个哈希只作为失败方案回滚点，
  不再是当前实机版本。
- 随后直接反编译本机当前游戏版本的 `TaleWorlds.CampaignSystem.dll`，核对了原版
  `MobileParty.ResetAllMovementParameters`、`SetMoveGoToSettlement` 与
  `PlayerEncounter.FinishEncounterInternal`。证据显示 `SetMoveGoToSettlement` 会立即清空
  `TargetParty`、短期目标和旧 `EngageParty`，并当场把默认行为改成 `GoToSettlement`；原版在战后
  分离双方时还会调用 `MobilePartyAi.SetDoNotAttackMainParty(2)`，给予两小时不再接触玩家的安全窗。
  这正是本问题需要的原版级非瞬移解法，无需销毁或传送使者。
- 当前修复保留完整实体：`TriggerPatrolReturn` 仍写入灰袍的 `Visit` 欲望，但同时立即调用原版
  `SetDoNotAttackMainParty(2)` 和 `SetMoveGoToSettlement`。因此关闭对话时，使者的原版追击目标会
  立刻清空并转为返城，而不是等到下一小时欲望竞拍才转身；两小时安全窗只防止双方尚在接触半径内
  时重开遭遇，不影响使者正常移动。监控新增 `RECRUIT_NATIVE_RETURN_APPLIED/FAILED`，可直接确认
  原版即时返城命令是否落地。已卡在“事务已经了结”循环的旧存档，在新 DLL 下关闭一次兜底对话
  即会改为正常返城，队伍不会消失。
- 第四轮 `Release --no-restore` 完整构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet 警告，
  并已部署客户端与编辑器 DLL。两份 DLL 均为 `611328` 字节，SHA-256 均为
  `3DC301C9955CBBB939F9D83B395829BD69B5C1838101E60FDA0BF7D66C638001`。反编译最终实机客户端
  DLL 已确认 `SetDoNotAttackMainParty(2)`、`SetMoveGoToSettlement` 与新监控均存在，且
  `RECRUIT_HERALD_DESTROYING` 已完全移除。仓库 `_Module` 的 `25` 个正常客户端文件与实机相比
  缺失 `0`、哈希不一致 `0`；`18` 个 XML 全部解析通过，两份 README 仍只保留 `r6/r5` 两条
  正式记录，`git diff --check` 通过。

## 2026-07-22 无领主灰袍队伍的交谈收尾边界

- 招募使者修复经用户实测确认完全解决后，继续审计了会主动与玩家交谈的无领主灰袍队伍。负声望
  纠察队原本已有“结束执法、返城、抵达后销毁”的生命周期，也会在缴清罚金或谈妥释放时压制后续
  执法会面；但其 `ReturnAllPatrols` 与每小时返程维护只调用 `RequestVisit`，仍留下与招募使者相同的
  时间窗：原版 `EngageParty/TargetParty` 要等下一次 AI 检查才会被灰袍欲望层覆盖。
- 新增 `ApplyImmediatePatrolReturn`，统一在返程开始及每小时维护时同时写入灰袍 `Visit` 意图、调用
  `SetDoNotAttackMainParty(2)`、恢复原版 AI 决策并调用 `SetMoveGoToSettlement`。这样缴款、谈判释放、
  玩家胜利或声望恢复等已经决定撤回纠察队的路径都会立即清除旧追击目标，随后保留实体返城并在
  抵达驻地后销毁。拒绝执法并进入战斗不是和平结案，仍按设计追击/交战；战斗结算后才走返城链。
- 这不是一个对所有无领主部队的全局“交谈后解散”钩子。招募使者和负声望纠察队是有专用玩家
  对话的两类一次性队伍；协力支援队等无领主临时队伍本来不应成为玩家对话对象，军团成员菜单继续
  由 `GwpLeaderlessSupportConversationItemPatch/MenuPatch` 屏蔽，避免把正在履行任务的支援队误撤回。
  正常灰袍领主拥有领主本人和持久领主队伍，交谈结束后恢复其案件/原版 AI，也不会返城销毁。
- 中英文 `v1.4.7-r6` 玩家日志已同步。`Release --no-restore` 完整构建通过，`0` 错误、`43` 条
  既有可空性/离线 NuGet 警告，并已自动部署客户端、编辑器 DLL 与两份 README。客户端和编辑器
  DLL 均为 `611840` 字节，SHA-256 均为
  `C9583215AA4E10EEE7FBBED96D97534D563507F374F773F83595FC92FA010CD5`。反编译实机客户端 DLL
  已确认三处返程入口均调用 `ApplyImmediatePatrolReturn`，方法体包含
  `SetDoNotAttackMainParty(2)`、`SetMoveGoToSettlement` 与
  `PATROL_NATIVE_RETURN_APPLIED/FAILED` 诊断事件。
## 2026-07-22 v1.4.7-r6 正式发布归档

- 发布前通过 `git fetch --tags --prune` 与 `gh release list/view` 核对 GitHub：远端 `main`、最新标签和
  Latest Release 均为 `v1.4.7-r5`（提交 `737e751`），因此本轮正式版本确定为
  `v1.4.7-r6`。开发期间 README 曾使用的 `r7/r8` 只是未发布占位编号，已统一更正为正式的
  `r6`，不能跳过 GitHub 上实际存在的上一版本。
- 中英文玩家 README 已按用户要求进一步精简：只保留安装、最近两个正式版本的玩法变化、当前
  可玩内容和联系信息；当前顺序严格为 `r6`、`r5`。内部公式、AI 欲望、监控事件、构建流程和
  测试过程继续只保存在本维护文档，不进入玩家说明。
- 本地实机继续使用带诊断的开发 DLL：客户端与编辑器 DLL 均为 `611840` 字节，SHA-256 均为
  `C9583215AA4E10EEE7FBBED96D97534D563507F374F773F83595FC92FA010CD5`。正式玩家 DLL 使用
  `-p:GwpDiagnosticsEnabled=false -p:DeployToLiveModule=false` 独立构建于
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\release-player`，大小
  `596992` 字节，SHA-256 为
  `DC97EB1B01B7A7925DE31948ACEC1B3FCA2A31469E62C2AF17F2E0B9BD9A1D67`；制作玩家包没有
  覆盖实机测试 DLL。
- ILSpy 对独立玩家 DLL 的 `GwpAiDiagnostics` 反编译结果保存在
  `.codex_tmp/release-r6-diagnostics.cs`。`LogPath` 返回空字符串，所有 Start/Write/Capture/Refresh
  方法均为空，两个 ShouldTrace 方法均返回 false；反编译结果不含 File、Directory、StreamWriter、
  AppendAllText、Documents 或诊断日志路径。因此玩家 DLL 无法创建或写入本地测试日志。
- 首轮压缩候选大小 `349793092` 字节、SHA-256
  `CE550AA42206A4547D854F2F443EA700D032FF7B449ABA126024C0C5DB1607DD`，路径审计发现从实机
  Shaders 目录带入 `GreyWarden/Shaders/D3D11/shader_compile_report.log`。该候选立即作废，未
  上传；发行暂存只保留运行所需的 `compressed_shader_cache.sack`。这进一步固化了发行版和压缩包
  不得包含监控脚本、监控日志、编译日志或其他开发诊断内容的既有规则。
- 最终发行暂存位于
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\package-r6-final-20260722\GreyWarden`。
  正式压缩包及校验文件位于：
  - `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7-r6.zip`
  - `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7-r6.zip.sha256`
  最终 ZIP 大小 `349792752` 字节，SHA-256
  `FBA36A71F94818297D955A926C5D6A45D950BEB369A5C0708BD64278F809E0DB`；校验文件正文准确命名
  `GreyWarden-v1.4.7-r6.zip`。
- 最终 ZIP 只有一个顶层 `GreyWarden/`，共 `28` 个正常客户端运行文件。路径检查确认
  `Assets`、`AssetSources`、`RuntimeDataCache`、`tools`、编辑器目录、PDB、PowerShell、日志、
  dump、嵌套 ZIP 和校验文件数量均为 `0`。包内 DLL 与独立无诊断构建哈希一致；完整解压后与发行
  暂存逐文件比较为缺失 `0`、哈希不一致 `0`、额外文件 `0`，包内两份 README 也与仓库一致。
- 受保护资源哈希保持不变：`gwp_black_gold_shield.tpac` 为
  `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B`，
  `gwp_inherited_legacy_assets.tpac` 为
  `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`。
- 开发构建与玩家构建均为 `0` 错误；完整编译仍只有 `43` 条既有可空性/离线 NuGet 警告。
  仓库 `_Module` 的 `25` 个可部署文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML 全部
  解析通过，`git diff --check` 通过。正式发布使用 `main`、标签 `v1.4.7-r6` 和 GitHub Release
  `https://github.com/Lucicain/GW/releases/tag/v1.4.7-r6`，并只上传上述最终 ZIP 与匹配校验文件。

## 2026-07-22 Bannerlord 1.4.5/1.4.7 无硬版本门槛兼容

- 用户转述网友判断“1.4.5 不支持本模组”后，本机 Steam 分支也已切到 `v1.4.5`：
  `D:\steam\steamapps\appmanifest_261550.acf` 的 `BetaKey` 为 `v1.4.5`，实机
  `bin\Win64_Shipping_Client\Version.xml` 为 `v1.4.5`，运行日志构建号为 `115026`。切换发生前的
  AI 监控文件头只写 `assembly=1.4.7.0`，那是模组程序集版本而非游戏版本，不能用于判断实机版本。
- 首个根因是 `_Module/SubModule.xml` 把 Native、SandBoxCore、Sandbox、CustomBattle、StoryMode
  全部精确写成 `DependentVersion="v1.4.7"`。本机反编译 1.4.5 的官方
  `TaleWorlds.MountAndBlade.Launcher.Library.LauncherVM.ExecuteStartGame` 证实，启动器逐项调用
  `dependedModule.Version.IsSame(actual, checkChangeSet:false)`，主次修订号必须精确相等。只删除
  `DependentVersion` 属性也无效：官方 `ModuleInfo` 会把它解析成 `ApplicationVersion.Empty`，仍与
  实机版本不相等。因此最终按用户要求删除整个 `DependedModules` 精确依赖块，而不是维护 1.4.5、
  1.4.7 两份限制清单。
- 清单现在只用无版本号的 `DependedModuleMetadatas` 表达五个原版核心模块必须先加载。用户进一步
  明确“不用理 optional 依赖”，所以 `Bannerlord.Harmony` 和 `CourierMessenger` 两条可选排序元数据
  也已完全删除；GreyWarden 不再对任何第三方模组声明依赖、可选依赖或兼容承诺。曾短暂新增的
  `tools/New-GreyWardenVersionManifest.ps1` 双清单生成方案已在同一轮撤回并删除，不可恢复为正式流程。
- 第二个根因是真实 DLL API 差异。相同 r6 源码对 1.4.5 实机程序集首次编译报
  `CS0535`：`GreyWardenAdoptionLogEntry` 未实现
  `IEncyclopediaLog.IsVisibleInEncyclopediaPageOf<T>(T)`。反编译确认 1.4.5 接口要求泛型方法，而
  已发布 r6 的 1.4.7 玩家 DLL 使用 `IsVisibleInEncyclopediaPageOf(MBObjectBase)`。当前类同时保留
  非泛型 1.4.7 方法并新增泛型 1.4.5 转发方法；1.4.5 完整编译通过，最终实机 DLL 反编译也同时
  显示两种签名。新增方法只调用本类既有非泛型实现，不改变收养记录可见性或存档字段。
- `tools/Watch-GreyWardenAI.ps1` 现在读取实机 `Version.xml` 并显示当前游戏版本，同时检查实机
  GreyWarden 清单是否仍存在任何 `DependentVersion`。最终输出为
  `Installed Bannerlord: v1.4.5` 与 `GreyWarden hard game-version dependencies: none`；它不再把
  模组程序集版本当作游戏兼容性结论。
- 失败前的直接运行证据保存在
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_22928.txt`：1.4.5 构建号 `115026`
  已加载 GreyWarden DLL，随后明确记录 `GreyWarden could not be loaded correctly` 和
  `dependency conflict`。最终清单与双接口 DLL 部署后的完整正常模组序列测试保存在
  `rgl_log_51852.txt`：同一构建号加载实机 GreyWarden DLL，于 `22:56:46` 到达
  `GauntletInitialScreen::HandleActivate`，GreyWarden dependency-conflict、加载失败及盾击补丁失败均为
  `0`。测试到达主菜单后由自动验证进程关闭，没有载入或改写玩家存档。
- 1.4.7 是 r6 发布前已经通过实机运行验证的开发基准；当前机器已切换到 1.4.5，故本轮没有伪造一次
  新的 1.4.7 实机启动。兼容改动保留了 1.4.7 已验证的非泛型接口和全部原有行为，只额外增加
  1.4.5 泛型入口并移除启动器限制。下一次切回 1.4.7 时仍应重复主菜单和存档加载测试，但不存在
  会阻止 1.4.7 加载的新接口替换。
- 中英文玩家 README 已建立单一 `v1.4.7-r7（开发中）` 条目并只保留上一正式 `r6`，明确说明无
  Bannerlord 版本硬门槛及 1.4.5/1.4.7 已验证范围。本轮只更新开发工作树和实机测试模块；没有
  创建玩家 ZIP、提交、标签或 GitHub Release。
- 最终 `Release -t:Rebuild --no-restore` 为 `0` 错误、`43` 条既有可空性/离线 NuGet 警告。
  自动部署后 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`；`18` 个
  XML 全部解析通过，中英文 README 与实机逐字节一致，清单内 `DependentVersion`、
  `Bannerlord.Harmony`、`CourierMessenger` 和 `optional=` 命中均为 `0`。客户端与编辑器 DLL 均为
  `611328` 字节，SHA-256 均为
  `2364002026DBB613439C8686E5AE4931C4027A31D5786353A914F901F7AB0D6E`；`git diff --check` 通过。

## 2026-07-22 v1.4-r6 统一 1.4.x 入口与正式发行

- 用户在 Bannerlord 1.4.5 完成实机复测，确认界面和现有玩法表现正常，并决定本轮直接作为第六次正式修订发行。从本版起，公开版本号不再把开发基准 `1.4.7` 写进模组版本，而统一命名为 `v1.4-r6`；同一玩家包明确覆盖当前 1.4 系列的 `1.4.5`、`1.4.6`、`1.4.7`，不按游戏修订号拆包，也不恢复任何启动器硬版本限制。
- TaleWorlds 的 Bannerlord `v1.4.6` 正式更新说明只列出玩法修复和崩溃修复，没有列出 Modding/API 改动；这只能作为初步线索，不能代替二进制验证。进一步取得 `Bannerlord.ReferenceAssemblies 1.4.6.115628` 的精确参考元数据，反编译确认 `IEncyclopediaLog` 仍要求 `IsVisibleInEncyclopediaPageOf<T>(T obj) where T : MBObjectBase`，与 1.4.5 相同；1.4.7 才使用非泛型 `IsVisibleInEncyclopediaPageOf(MBObjectBase)`。当前 `GreyWardenAdoptionLogEntry` 同时实现两种签名，因此三个版本都能绑定到各自需要的入口。
- 不只核对单个接口：临时交叉构建项目位于 `.codex_tmp\compat-audit\build146\Build146.csproj`，用 `Bannerlord.ReferenceAssemblies 1.4.6.115628` 对当前全部源码做了完整 `net472` Release 编译，结果 `0` 错误、`42` 条既有可空性警告。1.4.5 则已在真实安装程序集上完成 `0` 错误构建、正常启动到主菜单及用户界面实测；1.4.7 是此前 r6 开发和日常实机验证基准。由此，1.4.6 不是靠猜测纳入，而是已经通过完整 ABI 交叉构建。
- `SubModule.xml` 仍只保留五个原版核心模块的无版本加载顺序；`DependentVersion`、`Bannerlord.Harmony`、`CourierMessenger` 和 `optional=` 均为 `0`。公开标签、README、ZIP 和校验文件统一使用 `v1.4-r6`。Bannerlord 模组清单只接受三段数字版本，所以模组自身内部版本表示为 `v1.4.6`，程序集为 `1.4.6.0`；这里最后一段表示 `r6`，不是对 Bannerlord 1.4.6 的依赖声明。监控脚本会分别显示实机 Bannerlord 版本、硬依赖检查和模组程序集版本，避免再次混淆。
- 中英文玩家 README 已建立明确的 `Bannerlord 1.4.x` 单一兼容入口，列出 1.4.5、1.4.6、1.4.7 共用同一安装包；最近更新严格只保留 `v1.4-r6` 与上一正式 `v1.4.7-r5` 两条。兼容修复已经折叠进 r6，不再创建此前工作树中的 `r7（开发中）`。
- 当前带诊断的本地实机 DLL 为 `611328` 字节，SHA-256 为 `C6B1ABD0B3508286F89B4913A77711A2400884FC765C060E77553990364D81E6`。完整 `Release -t:Rebuild --no-restore` 结果为 `0` 错误、`43` 条既有可空性/离线 NuGet 警告；客户端与编辑器 DLL 保持一致。仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`；`18` 个 XML 全部解析通过，两份 README 与实机逐字节一致。
- 正式玩家 DLL 使用 `-p:GwpDiagnosticsEnabled=false -p:DeployToLiveModule=false` 单独构建于 `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\release-player-v1.4-r6`，为 `596480` 字节，SHA-256 为 `4D7C9D6EC8B6D01FFF69ED90206B98EA71DCC5BC20B311339EDED486B7EED1E7`；它没有覆盖本地带诊断 DLL。ILSpy 反编译结果保存在 `.codex_tmp\release-v1.4-r6-diagnostics.cs`：`LogPath` 返回空字符串，全部写入/捕获方法为空，两个 `ShouldTrace` 方法返回 `false`，且不含 `File`、`Directory`、`StreamWriter`、`AppendAllText`、`Documents` 或诊断日志路径。
- 最终发行暂存位于 `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\package-v1.4-r6-final-20260722\GreyWarden`。正式文件位于：
  - `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4-r6.zip`
  - `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4-r6.zip.sha256`
  ZIP 为 `349792645` 字节，SHA-256 为 `1536E5A4FABA30CF2407B75FED1A75C3E8493CE47E1C7E7003DD69375AEE85D5`；校验文件正文准确命名 `GreyWarden-v1.4-r6.zip`。
- 最终 ZIP 只有一个顶层 `GreyWarden/`，共 `28` 个正常客户端运行文件。路径检查确认 `Assets`、`AssetSources`、`RuntimeDataCache`、`tools`、编辑器目录、PDB、PowerShell、日志、dump、嵌套 ZIP 和校验文件均为 `0`；包内 DLL 与独立无诊断构建哈希一致，包内中英文 README 与仓库一致。发布使用 `main`、标签 `v1.4-r6` 和 GitHub Release `https://github.com/Lucicain/GW/releases/tag/v1.4-r6`，只上传上述 ZIP 与匹配校验文件；成功后删除旧的 `v1.4.7-r6` Release/标签及本地旧名包，`v1.4.7-r5` 继续作为上一正式版本保留。

## 2026-07-22 v1.4-r6 的 1.4.7 CLR 接口绑定修复

- `v1.4-r6` 首次统一包是在真实 1.4.5 安装程序集上构建。用户随后切回 1.4.7（构建号 `117484`）尝试启动，最新两次失败日志为 `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_18192.txt` 与 `rgl_log_65176.txt`。两份日志都完成 GreyWarden DLL 加载和绝大多数类型枚举，但在类型加载末尾明确记录：`GreyWardenAdoptionLogEntry` 的 `IsVisibleInEncyclopediaPageOf` “does not have an implementation”，随后 GreyWarden 因 dependency conflict 被拒绝载入。该证据排除了清单版本号、可选依赖、资源和存档问题。
- 根因比“同时写两个重载”更深：C# 编译器只会把当前参考程序集接口所匹配的方法发行为 `final newslot virtual` 接口槽。旧实现用 1.4.5 编译时，泛型重载是虚接口槽，而非泛型 1.4.7 重载只是普通实例方法；因此 1.4.7 CLR 即使能看见同名非泛型方法，也不会把它当作接口实现。反过来只在 1.4.7 编译也会给 1.4.5 留下同类风险。
- 最终修复取消 `GreyWardenAdoptionLogEntry` 的 `sealed`，并把泛型、非泛型两个重载都显式声明为 `public virtual`。这样无论使用 1.4.5、1.4.6 还是 1.4.7 参考程序集构建，两个签名都固定拥有可供 CLR 映射的虚方法槽；方法体和收养记录可见性规则没有变化。
- 修复后完成三版本构建矩阵：真实 1.4.7 完整 `Release -t:Rebuild --no-restore` 为 `0` 错误、`43` 条既有警告；`.codex_tmp\compat-audit\build145\Build145.csproj` 对 `Bannerlord.ReferenceAssemblies 1.4.5.115026` 完整交叉构建为 `0` 错误、`42` 条既有警告；既有 `build146\Build146.csproj` 对 `1.4.6.115628` 同样为 `0` 错误、`42` 条既有警告。ILSpy 对三个产物均确认泛型与非泛型入口都是 `public hidebysig newslot virtual`。
- 自动 1.4.7 运行验证使用当前启用模组序列并加 `/continuegame` 才能绕过启动器交互；不带该参数直接启动 `Bannerlord.exe` 或 `Bannerlord.Native.exe` 只停留在启动器/原生入口，未进入托管初始化，因此不能作为模组失败证据。成功日志为 `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_42140.txt`：构建号 `117484`，`Module Initialize end` 正常完成，随后到达 `GauntletInitialScreen::HandleActivate`；`GreyWarden could not be loaded correctly`、`Loader Exceptions` 和 dependency conflict 命中均为 `0`。测试在初始界面即关闭，没有等待大地图时间推进。
- 用户随后在自己的正常 1.4.7 启动流程中实机验证，确认游戏能够打开且一切正常。这是本轮最终的玩家侧运行验收；1.4.5 此前也已由用户确认界面与玩法正常，1.4.6 则由完整参考程序集交叉构建覆盖。
- 修复后的本地诊断 DLL 为 `611328` 字节，SHA-256 `F25209F83EE9AB2C78D0270D4D97AA79ED3FAA873548BCD7954A5AD1688D35C6`。仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`；`18` 个 XML 全部解析通过，中英文 README 与实机一致。
- 修复后的正式玩家 DLL 独立构建于 `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\release-player-v1.4-r6-interface-fix`，为 `596480` 字节，SHA-256 `E4CDC522BA4EA86DD407EF6EB2A5C514922899DA8479F515B886CB450ADE8D90`。ILSpy 确认两个兼容入口均为虚方法；`GwpAiDiagnostics` 仍是空实现，文件、目录、流写入和诊断日志路径命中为 `0`。
- 修正版发行暂存位于 `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\package-v1.4-r6-interface-fix-20260722\GreyWarden`。最终 `GreyWarden-v1.4-r6.zip` 为 `349792649` 字节，SHA-256 `46CDF0F52D3EC562ACD44B823FF4AFF9D75BC171687C64B160600433906617AB`；校验文件正文准确命名该 ZIP。最终包仍只有一个 `GreyWarden/` 顶层和 `28` 个运行文件，禁止内容为 `0`；完整解压后与暂存相比缺失 `0`、哈希不一致 `0`、额外文件 `0`，包内 DLL 与独立玩家构建哈希一致。
- 公开版本继续使用 `v1.4-r6`，不新增 r7。GitHub Release 的同名 ZIP 与校验文件用修正版覆盖，标签移动到包含本修复的最终提交；此前 SHA-256 `1536E5A4...` 的首次统一包作废，不再作为可下载发行物。

## 2026-07-22 本地正式压缩包只保留最新版

- 用户明确区分两类保留规则：`GreyWardenPolicePurity/_Module/README.md` 与 `README_EN.md` 继续保留最近两个正式版本的玩家更新记录；但游戏父级 `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules` 只能保留最新版 GreyWarden ZIP 与其配套 `.sha256`，不能同时保留两个版本的压缩包。
- 核对 `GreyWardenPolicePurity.csproj` 后确认普通编译不会生成 ZIP，且部署目标已明确排除所有 `.zip`/`.zip.sha256`；这次出现两套包不是编译代码自动产生，而是此前正式发布流程在生成 r6 后没有删除 r5 本地归档。
- 已删除 `GreyWarden-v1.4.7-r5.zip` 与 `GreyWarden-v1.4.7-r5.zip.sha256`。最终 Modules 父目录只剩 `GreyWarden-v1.4-r6.zip`（`349792649` 字节，SHA-256 `46CDF0F52D3EC562ACD44B823FF4AFF9D75BC171687C64B160600433906617AB`）及匹配的 `GreyWarden-v1.4-r6.zip.sha256`。
- 根目录 `AGENTS.md` 已新增强制规则：正式新版包验证后必须删除所有旧的本地 GreyWarden ZIP/校验文件，始终只保留最新版一套；普通开发构建不得创建发行 ZIP。GitHub 历史 Release 与 README 最近两版日志不受这条“本地只留一套”规则影响。


## 2026-07-23 敌方军团解散后协力军团无法追上单队的诊断

- 用户实测发现：案件目标原先依靠敌方军团总战力触发灰袍协力军团；敌方军团随后
  解散，目标缩成高速单队，但后续领主协办与无领主周期支援仍持续并入灰袍军团，
  使军团速度继续下降，无法追上目标。初次取证先只诊断现行生命周期；随后按用户
  确认的保底规则完成玩法修复。
- 最新开发日志
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`
  （取证时最后写入 `2026-07-23 00:20:00 +10:00`）与现场完全一致：主办
  `gw_leader_0_party_1` 追捕 `CharacterObject_1543_party_1`；目标只有 `37` 名健康
  成员，`armyLeader=-`、`attachedTo=-`、基础速度 `4.69`。灰袍一方仍是八队协力军团，
  三名领主协办和三支无领主支援已经附着，另有一支新支援在赶来；军团估算战力
  `1336.05`，军团长当前基础速度只有 `1.88`，双方距离约 `8.98`。这证明现行协力状态
  没有把“案件目标已经脱离军团”当作解散条件。
- 当前军团不会靠无战争或凝聚力自然结束。`GwpAssistanceArmyNoWarDisbandPatch` 会对仍
  绑定案件的合法无王国协力军团跳过原版 `ApplyByNoActiveWar`；
  `GwpAssistanceArmyCohesionPatch` 又把其每日凝聚力变化固定为零。原版不活动计数只对
  `Hold`、特定 `GoToSettlement` 和 `PatrolAroundPoint` 等状态增长，现场军团长长期为
  `GoAroundParty`，因此这条追逐通常也不会因不活动自动解散。
- 原版仍保留断粮解散：`Army.CheckArmyDispersion` 在军团长与已附着成员中超过半数
  `IsStarving` 时调用 `ApplyByFoodProblem`。现场粮食尚未满足该门槛：军团长约
  `32.38` 日，已观察协办约 `9.55～25.75` 日，无领主支援约 `11～19` 日；每两日还会
  继续生成携带二十日口粮的新支援。因此断粮既很慢，也可能被不断加入的新队推迟。
- 更关键的是，断粮只会让原版 `Army` 对象暂时解散，不会结束模组保存的
  `LordAssistanceGroup`。只要主办人的同一案件仍存在，下一次
  `UpdateLordAssistance -> MaintainAssistanceArmy` 会新建无王国 `Army`，重新把已登记
  领主成员加入；`UpdateDelayPatrols` 也会让仍有效的周期支援再次并入。因此“等粮食
  吃光”不是此僵局的可靠终止条件，而可能表现为解散后再次组建。
- 已新增协力军团速度保底。每次小时协力维护在案件目标有效时读取目标与军团长的
  `LastCalculatedBaseSpeed`；只要目标速度严格高于当前协力军团速度，立即以原版
  `DisbandArmyAction.ApplyByObjectiveFinished` 解散军团，并把协力组持久标记为
  `DispersedForSpeed`。比较要求双方速度已经得到有效正值，避免读档初始尚未计算速度时
  误触发；现有现场的 `4.69 > 1.88` 会在下一次小时维护直接命中。
- 速度解散不等于撤销协力任务。主办人继续使用原案件追捕；所有已登记协力领主改为
  各自 `Pursue` 同一目标，并在后续小时续期。已经生成的无领主支援会先脱离 Army，
  然后恢复既有无欲望直攻目标。协力组仍占用这些领主、保留攻击授权、案件总卷条目和
  结案拨款归属，直到原主办案件真正结束；不会让分散领主中途领取其他案件。
- `DispersedForSpeed` 已加入存档字段 `gwp_enf_assist_speed_dispersed`。旧档缺少该字段时
  默认仍为未分散，并会对现存军团执行第一次速度比较；新档读回分散状态后不会重建
  Army。`TryGetDelaySupportAssistanceArmy` 同样拒绝为已速度分散的组返回军团，因此以后
  每两日生成的新支援会直接追捕目标，不会重新加入旧军团。诊断摘要新增
  `speedDispersed`，触发时写入 `ASSISTANCE_ARMY_SPEED_DISPERSED` 及双方速度、领主成员数、
  支援数。
- 最终 `Release --no-restore` 构建通过，`0` 错误；仅保留离线 NuGet 漏洞源警告。自动
  部署后核对 `_Module` 的 `25` 个正常客户端可部署文件，实机缺失与哈希不一致均为
  `0`；`18` 个 XML 全部解析成功。客户端与编辑器 DLL 均为 `614400` 字节，SHA-256
  均为 `A962904F72CDFF0E57DD64D2396D6C1BECD68DC3E9A2B6F084AEE5DDA0F70020`；仓库与实机
  中文 README SHA-256 均为
  `C7D659A8E49382D202424868C96AAD1ECB3B613C9FE096BAE1833A144B925869`。
- ILSpy 对实机 DLL 复核确认：存在目标/军团 `LastCalculatedBaseSpeed` 严格比较、
  `DisbandArmyAction.ApplyByObjectiveFinished`、领主逐队 `RequestPursuit`、支援队脱离后
  `RequestPursuit`、速度分散存档字段，以及 `TryGetDelaySupportAssistanceArmy` 对
  `DispersedForSpeed` 的拒绝分支。本轮为普通开发构建，没有创建发行 ZIP。

## 2026-07-23 战后灰袍领主队变成无领主壳队的首轮取证

- 用户从同一存档反复复现：一场战斗结束后，地图上一支灰袍领主队失去首领并继续以无领主部队存在。最新开发日志 `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`（会话 `2026-07-23T00:39:40+10:00`）已锁定新发生的队伍为 `CharacterObject_2877_party_90754`。该队在 `campaignHour=627727.12` 仍名为“雨谣的部队”，`leader=雨谣`、`men=148`；到 `627727.77` 同一个 party id 已变成“灰袍守卫的部队”，`leader=-`、`men=116`，并因承办人失效写出 `ISSUE_DUTY_RELEASED | reason=assignee_invalid`；`627728.01` 后继续以 `leaderless_grey_warden` 参加原版 AI 拍卖。
- 本轮唯一记录到的地图事件在 `627724.20` 结束，参战方只有目标 `CharacterObject_1543_party_1` 与四支无领主纠察支援队；雨谣队不在 `involved` 列表，前后状态也都是 `mapEvent=-`。雨谣在该战后仍正常存在约三小时。因此已排除“雨谣本人在这场可见战斗里战败/被俘后立即丢队”以及协力军团速度解散代码直接删除领主；可见战斗只是同一时段的前置事件，不是监控已证明的直接触发点。
- 存档中还同时存在旧的无领主壳队 `CharacterObject_2877_party_1`，而同一个动态英雄前缀又拥有新队 `CharacterObject_2877_party_90754`。结合 `PoliceResourceManager.SpawnIdleHeroes` 会把非 Active、无有效队伍的合格灰袍英雄强制改回 Active 并重新 `SpawnLordParty`，这强烈说明该英雄以前已经历过至少一次“原队留壳、英雄被恢复并另建新队”，而不是一次性的粮尽解散。
- 现有证据更符合原版英雄生命周期/延迟传送/解散链先把英雄从队伍解绑，随后灰袍恢复器又为英雄另建队伍的循环。原版 `TeleportationCampaignBehavior`、`DisbandPartyCampaignBehavior` 都存在“先失去队长、壳队继续等待换将或解散”的合法中间态；灰袍恢复器目前既不识别原版待传送记录，也不清理失去领主的旧 LordParty 壳。另一方面，`MakeHeroFugitiveAction` 在英雄仍是领队时会直接 `DestroyPartyAction` 并清空全队，与现场保留 `116` 人的同 id 活动壳队不完全相符，所以目前不能把免俘回调直接定为这次解绑动作。
- 旧监控只在小时/AI 拍卖采样中看到了“解绑前”和“解绑后”，没有记录 Hero 状态改变与原版队伍解散事件，因而尚不能在“死亡、逃亡、延迟传送换将、开始解散”中确定唯一入口。已扩展仅开发版启用的诊断：每条队伍状态新增 `isDisbanding`，并记录领主 id、HeroState、年龄、存活、是否非战斗员/指挥官、Traveling/Fugitive、当前所属队与俘虏队；同时新增 `HERO_BECAME_FUGITIVE`、`HERO_KILLED`、`HERO_TELEPORT_REQUESTED`、`HERO_PRISONER_TAKEN`、`PARTY_DISBAND_STARTED`、`PARTY_DISBANDED` 生命周期行，并保存英雄最后一次观测到的 party id。下一次读取同一存档复现时可直接确定是哪一个原版事件先发生。
- 首次编译暴露三处诊断格式使用了 `PartyBase.StringId`，而 1.4.7 的 `PartyBase` 没有该成员；已改为从 `MobileParty.StringId` 或 `Settlement.StringId` 取标识。随后 `Release --no-restore` 构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet 警告。
- 诊断增强已部署到正常客户端与编辑器测试目录；两处 DLL 均为 `619008` 字节，SHA-256 均为 `87D4135993ADA61A83784A808CC4EF78A43F8328B5080DD6B43584B83D9ACAAD`。仓库 `_Module` 的 `25` 个正常客户端可部署文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML 全部解析通过，仓库与实机中文 README 哈希一致。此次只增强本机诊断、未改变玩家可见玩法，因此没有改玩家发行日志，也没有创建发行 ZIP。

## 2026-07-23 收养后继者被原版换将并留下无领主壳队的根因与修复

- 用户再次读取同一存档并稳定复现。增强后的日志在
  `campaignHour=627727.38` 捕获了完整先后顺序：`雨谣`（`CharacterObject_2877`）先收到
  `HERO_BECAME_FUGITIVE`，当时 `currentParty=-`、`lastObservedParty=CharacterObject_2877_party_90754`、
  `state=Fugitive`、`noncombatant=True`、`commander=True`；紧接着 `约珥`（`gw_leader_1`）收到
  `HERO_TELEPORT_REQUESTED`，目标正是该队，细节为 `DelayedTeleportToPartyAsPartyLeader`。此前该队
  `leader=雨谣`、`men=148`、`isDisbanding=False`；之后成为 `leader=-`、`men=116`、
  `isDisbanding=False`。全程没有 `PARTY_DISBAND_STARTED`，所以这不是粮尽、战败或模组解散队伍。
- 反编译 1.4.7 原版
  `.codex_tmp/campaign-decompiled/TaleWorlds.CampaignSystem.CampaignBehaviors/TeleportationCampaignBehavior.cs`
  的 `DailyTickParty` 与日志逐项吻合：活动 LordParty 在 `Army == null`、`MapEvent == null` 且领主被
  `DefaultHeroCreationModel` 判为非战斗员时，原版会寻找同家族无队伍的战斗指挥官，依次调用
  `RemovePartyLeader`、`MakeHeroFugitiveAction.Apply`，再以
  `TeleportHeroAction.ApplyDelayedTeleportToPartyAsPartyLeader` 安排换将。雨谣所有用于判定的战斗技能
  都低于原版阈值 `100`，成年后虽然已经是灰袍指挥官，仍被判成 `IsNoncombatant=True`；约珥正好是
  无队伍的可用替换者。
- 因此用户对“无领主支援队”的怀疑只命中了发生时机，不是直接原因。原版换将条件要求雨谣队
  `Army == null`，所以支援战斗结束、协力军团解散后才稳定触发，看起来像支援队导致；实际删除领主的
  是原版每日传送/换将行为。反复读取同一存档会越过相同的 DailyTickParty，因而每次都能复现。
- 首版修复曾暂时保留 `SpawnIdleHeroes` 强制建队，并在 `ApplyCommanderLoadout` 中把非战斗员的单手
  技能补到 `100`。该版本构建与部署成功，但用户随后明确最终设计不是“用技能补丁配合模组强制带兵”，
  而是“所有成年灰袍都是战斗员，部队由原版自然组建”。因此这版仅作为已验证的中间回滚点保留记录，
  不再是当前实现。
- 原版这里有两个彼此独立的限制，不能混为一个：`IsNoncombatant` 不是家族名额，而是
  `HeroCreationModel.IsHeroCombatant` 按六类武器技能是否至少一项达到 `100` 动态计算；家族能有几支
  LordParty 则由 `ClanTierModel.GetPartyLimitForTier` 决定，普通非小家族按阶级通常只有 `1/2/3` 支，
  另受领袖 perk 修正。只把英雄改成战斗员不会自动突破部队数量上限。
- 当前最终实现新增 `PoliceHeroCreationModel`：对在世、未禁用且达到成年年龄的灰袍家族成员直接返回
  `IsHeroCombatant=true`，不再依赖武器技能阈值，也不改其技能；其他家族完整沿用原版模型。读档、会话
  启动、每日结算和成年事件仍执行 invariant 检查；正常情况下模型已经保证通过，只有别的模组在更晚
  阶段覆盖 HeroCreationModel、导致成年灰袍仍为非战斗员时，才用单手 `100` 的兜底并写
  `ADULT_COMBATANT_INVARIANT_REPAIRED`。
- 家族部队上限的突破其实已经由同一历史版本中的 `PoliceClanTierModel` 实现：灰袍家族上限至少等于
  当前所有在世成年灰袍 Lord 的数量。因此无需再用另一套强制建队代码绕过上限。已删除
  `SpawnIdleHeroes`、强制改回 Active、清除失效 Party 引用、移除总督、瞬移到城镇以及直接
  `MobilePartyHelper.SpawnLordParty` 的整条恢复链；空闲成年灰袍是否以及何时建立领主队，现在完全交给
  原版 `HeroSpawnCampaignBehavior` 在扩展后的上限内按其状态、经济和每日结算自然处理。
- 过去直接 SpawnLordParty 时会顺便发放舰船。改为原版自然建队后，舰船保障移到灰袍领主队首次小时
  维护，继续调用幂等的 `GivePoliceShips`，不会因为删除强制建队入口而让新领主队失去航海能力。
- 读档与会话启动时同时清理灰袍家族中仍活动、`IsLordParty=True` 且 `LeaderHero=null` 的历史壳队，
  并写 `LEADERLESS_POLICE_LORD_PARTY_CLEANUP` 诊断。过滤条件不匹配设计上就没有领主的支援队/纠察队，
  因此不会误删正常无领主支援。已存在的 `CharacterObject_2877_party_1` 一类旧壳会被销毁，当前有效的
  雨谣领主队则保留。
- 中英文玩家 README 的当前 r6 条目已同步记录成年灰袍战斗员、扩展家族部队容量、原版自然建队、
  无领主壳队修复，以及同轮协力军团速度保底。最终 `Release --no-restore` 构建为 `0` 错误、`43` 条
  既有可空性/离线 NuGet 警告。自动部署后
  `_Module` 的 `25` 个正常客户端可部署文件与实机缺失 `0`、哈希不一致 `0`；`18` 个 XML 全部解析
  成功，中英文 README 与实机哈希一致。客户端与编辑器 DLL 均为 `619008` 字节，SHA-256 均为
  `6D42C28628DCAD0331DDCAB4538764B7A6791A7B4A90532EF9D9A1FC0D9D88AD` 的中间版随后已被最终设计替换。
  最终客户端与编辑器 DLL 均为 `617472` 字节，SHA-256 均为
  `175475EF4BF1353B57824DF805F82CF8C8AFE1842D319BF62DB5C4A2CCB5FC34`。本轮为普通开发部署，
  没有创建或改写正式玩家 ZIP。
- ILSpy 对最终实机客户端 DLL 的复核输出保存在
  `.codex_tmp/adult-combatant-native-parties-20260723/`。`PoliceHeroCreationModel` 明确对成年灰袍返回 true，
  `PoliceClanTierModel` 明确按成年灰袍 Lord 数量提高上限；`PoliceResourceManager` 包含读档/每日 invariant、
  壳队清理和小时舰船保障，但 `SpawnIdleHeroes` 与 `SpawnLordParty` 命中均为 `0`。这确认部署产物已经是
  “战斗员模型 + 扩展上限 + 原版自然建队”，不是只改了未部署源码或仍暗中强制带兵。

## 2026-07-23 协力军团短暂组建后按速度解散，以及退出游戏原生崩溃取证

- 用户看到梵蒂追捕古兰受阻并发出协力任务，但地图上似乎没有拉起军团。最新开发诊断已经证明
  不是“直接跳过组军”：`2026-07-23 12:15:47 +10:00`，梵蒂队
  `gw_leader_0_party_1` 先写出 `ASSISTANCE_ARMY_MEMBER_ADDED`，暮光队
  `gw_leader_5_party_1` 随即写出 `ASSISTANCE_ARMY_JOINED`；两条记录中的
  `armyMemberCount=2`，即军团长加一名协办领主，顺序符合“先组军团”。
- 下一次小时维护发生在约一秒后的 `12:15:48`：目标古兰队速度为 `3.44`，协力军团当前速度为
  `3.38`，因此命中已实现的严格比较并写出 `ASSISTANCE_ARMY_SPEED_DISPERSED`。差值只有 `0.06`，
  而组建到解散只跨一个游戏小时、现实约一秒，所以地图视觉上很容易表现成只看见协力任务、没看见
  军团。其他现场样本也重复出现“成员加入后下一小时按速度解散”，说明现行顺序稳定执行，并非此次
  没有建立 `Army` 对象。本轮没有修改速度门槛或增加宽限，因为当前结果正符合用户已确认的
  “先拉军团，目标更快才分开追捕”规则。
- 同次退出游戏的 `rgl_log_47044.txt` 显示存档已经成功完成，随后正常进入最终清理：
  `Deleting Game... OK!`、`Pre Finalizing Managed Interface... OK!`，并明确写出
  `There are no living managed objects` 与 `Managed Interface deleted`。日志中没有托管异常、
  GreyWarden 异常或模组加载错误。
- Windows Application Error 事件 `1000` 在 `2026-07-23 12:18:59 +10:00` 记录：崩溃进程为
  `TaleWorlds.MountAndBlade.Launcher.exe`，故障模块为原版 `TaleWorlds.Native.dll`，异常码
  `0xc0000005`，偏移 `0x74b1f0`，报告 ID
  `81dafa58-6c8a-476e-8c0e-f5e94369cc97`。本机 WER 归档中共有 `37` 份 Bannerlord 崩溃报告，
  其中 `9` 份同为 `TaleWorlds.Native.dll / 0xc0000005 / 0x74b1f0`，且同一签名至少在
  `2026-07-20` 与 `2026-07-22` 的旧开发构建中已经反复出现。因此它不是本轮协力或成年战斗员代码
  新引入的托管异常，而是托管层已经完全销毁后发生的原生退出清理崩溃。
- 社区与 TaleWorlds 技术支持中确实能找到其他玩家的 `TaleWorlds.Native.dll`、`0xc0000005` 以及
  退出/离开场景时崩溃报告；TaleWorlds 自己也曾在 v1.2.8 正式补丁说明中列出“修复关闭游戏时发生的
  崩溃”，证明退出清理崩溃这一故障类别并非模组圈臆测。官方通用排查建议包括校验游戏文件、绕过启动器做对照测试、禁用 Steam
  Overlay，并在仍可复现时提交 Crash Uploader ID。不过公开报告没有匹配到本机当前版本的精确
  `0x74b1f0` 退出偏移，且本次进程同时加载了 Steam/NVIDIA/Nahimic 等原生叠加层，所以现有证据只能
  定性为“原版原生退出路径或其与叠加层的交互”，不能仅凭故障模块断言纯净原版必现。最小鉴别测试是
  保留同一存档，先只关闭 Steam 与 NVIDIA 游戏叠加层退出一次；若仍以同偏移崩溃，再用无模组原版
  模块序列退出一次，并保存 Crash Uploader ID。
- 本轮只完成现场诊断与维护记录，没有改变玩家可见行为、没有重新编译或部署 DLL，也没有创建发行 ZIP。

## 2026-07-23 指挥官盾牌资格、练兵换防任务与实体调兵

- 用户明确了三项联动设计。第一，玩家首次接受招募和以后重新加入时都应额外获得新制作的指挥官盾牌，
  而悬赏任务必须实际使用该盾才能触发。第二，练兵职务领主仍像普通灰袍领主一样承办犯人追捕、原版
  请求、村庄重建等全部既有工作，同时持续把自己部队训练到满级；满级且空闲后新增“练兵任务”，与
  一名低练度灰袍领主前往双方之间的定居点，驻留六小时后等量互换精锐与低级兵。第三，玩家向灰袍
  领主调兵不再凭空生成，只能取得当前交谈领主花名册中真实存在且数量足够的兵。
- `GwpIds.CommanderSetItemIds` 已加入 `wlarge_shield_black`。招募接受与重新加入原本就统一调用
  `GiveCommanderEquipment`，因此现在每次都会连同护甲和马具发放一面黑曜指挥官盾；同一集合也由
  `IsWearingCommanderSet` 遍历玩家 `BattleEquipment`，所以悬赏的小时触发、通知和接受入口现在都要求
  该盾实际装备在战斗栏位中，仅放在行李里不会取得任务资格。
- 新增 `GreyWardenTrainingBehavior` 并注册为存档行为。所有持有 `DutyKind.Training` 的成年灰袍领主仍
  进入普通案件的两轮调度，也仍可作为原版请求与重建的跨职务协办；训练职责与普通任务并行。首版曾
  每六小时由模组直接替换约 `10%` 的花名册并自行选择兵种分支；用户随后明确要求“模组只给经验，
  升级完全交给原版”，因此该直接升级路径已经删除，当前实现见下一节。
- 练兵领主全队灰袍正规兵都满级且当前没有案件、协力、军团、地图战斗、村庄救济、重建或请愿任务时，
  才会创建换防任务。目标必须是另一支同样空闲、拥有可升级灰袍士兵的领主队；从符合条件者中选择离
  练兵领主最近的一支。会合地点从未被围攻、与灰袍不交战的城市或城堡中选择离双方位置中点最近者，
  两队直接以原版访问欲望前往，不建立 `Army`。
- 练兵任务期间两队都从普通案件、协力、重建、请愿和收养救济候选中暂时保留，避免另一个调度器覆盖
  会合。只有两队同时处于指定定居点才开始累计六小时；任一方离开会把连续驻留计时清零。时间满足后，
  练兵领主按最高阶优先交出满级兵，目标领主按最低阶优先交出可升级兵，交换量为双方可供数量的较小值，
  严格一换一并保留每种兵的伤员数量。练兵领主带回低级兵后自动开始下一轮训练，接受方则真实获得
  精锐，双方总人数不会由任务增殖。
- 练兵任务以训练领主、目标领主、会合定居点、创建时刻和连续驻留起点保存到新存档键；读档会按英雄
  id 重新解析当前领主队并清理重复或失效记录。案件总卷已经纳入该任务类型，显示会合双方、目的地、
  行进或驻留阶段和剩余时间。开发诊断新增 `TRAINING_TROOP_XP_GRANTED`、
  `TRAINING_TASK_ASSIGNED`、`TRAINING_STAY_STARTED/RESET`、`TRAINING_TASK_COMPLETED/CANCELLED`，
  便于实机核对整条循环。
- `GreyWardenTroopRequestBehavior` 现在在显示具体兵种选项、选择报价、打开 barter 和最终成交四个阶段
  都核对最初交谈领主的实际队伍及该兵种数量。数量不足的固定档位不会显示；成交后从源队花名册扣除
  同样数量，再加入玩家主队，健康兵优先转移、确需包含伤员时同步转移对应伤员数。诊断写出
  `PLAYER_TROOP_TRANSFERRED`。相关中英文对话已经明确说明调兵来源是该领主自己的现役部队。
- 对照 `origin/main` 与标签 `v1.4-r6` 后确认正式 r6 已固定在提交 `efd5c03`，日期为
  `2026-07-22`；此前把 7 月 23 日的新功能直接并入 r6 并继续保留 r5 是错误的版本归档。中英文玩家
  README 现已改为单一 `v1.4-r7（开发中）` 条目，并逐字保留 Git 上的正式 r6 条目、移除 r5，只保留
  当前开发版与上一正式版两条。`SubModule.xml` 与程序集内部版本同步改为 `1.4.7`，最后一段表示模组
  `r7` 修订，不是 Bannerlord 1.4.7 硬依赖。
- 首版直接升级实现的 `Release --no-restore` 构建通过，`0` 错误、`43` 条既有可空性/离线 NuGet
  漏洞源警告。该中间版客户端与编辑器测试 DLL 均为 `638976` 字节，SHA-256 均为
  `FB31669B5C21757C0DC8C39374699D99ACB154BC1F50BC8B414164E1FE6A3F80`。ILSpy 对实机 DLL 的
  复核输出保存在 `.codex_tmp/training-real-transfer-20260723/`，确认指挥官集合含
  `wlarge_shield_black`、悬赏各入口仍统一调用装备检查、练兵分级/会合/等量交换和实体调兵扣减均进入
  部署产物。仓库 `_Module` 的 `25` 个正常客户端可部署文件与实机缺失 `0`、哈希不一致 `0`，`18` 个
  XML 全部解析成功；仓库与实机中文 README SHA-256 均为
  `D072C37BAA3EDA5159386FEACC09787EE42CE7821681700772F8B76A2EFF2535`，英文 README 均为
  `27113004740B6255DF258733876FF7AA315EA9E53AA14C482F573550D57F50E6`。该中间版随后已被下一节的
  “只给经验、原版升级”设计替换；全程没有创建或改写正式玩家 ZIP。

## 2026-07-23 练兵改为原版经验升级，并重构三终端灰袍兵种树

- 用户进一步明确：练兵领主不应由模组代码直接把士兵替换成高阶兵，而应定期给予大量士兵经验，由
  Bannerlord 原版自行决定何时升级、能否支付升级费用以及选择哪一条兵种分支。同时，现有
  `轻步兵 -> 重步兵 -> 骑士` 会让强力重步兵最终全部继续转骑兵；新兵种树必须让重步兵、弓箭手和
  骑士都成为并列终点，并在现有轻步兵下方再增加一层装备简陋的新兵。该单位最初曾按参考模板暂称
  “贵族见习兵”，用户随后明确她们根本不是贵族，因此玩家文本、代码常量和内部对象 id 都已彻底改为
  普通“新兵”，不能再恢复贵族称呼。
- 已反编译本机 1.4.7 原版 `MobilePartyTrainingBehavior` 与 `PartyUpgraderCampaignBehavior`。原版训练
  使用 `TroopRoster.AddXpToTroop` 累积整组经验；原版升级器在每日 tick 或战斗结束时读取经验、伤员、
  升级金币、工资上限、坐骑/物品要求和领主 `PreferredUpgradeFormation`，再通过
  `GetUpgradeChanceForTroopUpgrade` 选择分支并扣除经验。因此当前练兵代码只在每六小时为每名仍可
  晋升的灰袍士兵增加 `750` 经验并写 `TRAINING_TROOP_XP_GRANTED`，不再调用花名册增减来完成升级，
  也不存在 `ChooseUpgradeTarget`；升级本身完全由上述原版每日升级器执行。
- 新增 `gwnewrecruit`（灰袍新兵）作为唯一 `is_basic_troop` 底层单位，仅参考原版
  `SandBoxCore/ModuleData/spnpccharacters.xml` 中 `imperial_vigla_recruit` 的低阶装备结构与三套
  装备模板。三套模板分别使用帝国软甲、帝国衬甲和皮外衣，搭配原版低阶长矛、短剑、布帽、皮靴与
  手套；唯一固定的灰袍专属部件是弓箭手的 `warchshoulder` 鹰徽肩饰，三套模板人人佩戴，用低数值
  小型肩饰表达灰袍身份而不是提前穿整套灰袍甲。用户随后指出新兵不应比轻步兵更早持盾，因此三套
  新兵模板现已全部移除盾牌；轻步兵改为两套模板，一套无盾、一套使用原版轻盾，重步兵则继续全员
  使用灰袍大盾。所有灰袍仍能踢击，但只有实际装备盾牌的轻步兵和重步兵能够使用盾击。
- 当前升级树为 `gwnewrecruit -> gwrecruit -> gwheavyinfantry / gwarcher / gwknight`。
  `gwrecruit` 调整为真正的中阶轻步兵并拥有三个直接升级目标；已删除重步兵到骑士的后续升级目标，
  重步兵、弓箭手、骑士的 `UpgradeTargets` 均为空，三类都是并列终点。对应的灰袍踢击/盾击阶级也
  调整为：新兵低阶、轻步兵中阶、三类终端精锐同属最高阶，不再让骑兵单独享有更高终端判定。
- `gwMBP_template` 在保持原总人数范围不变的前提下，把原有轻步兵份额均分为新兵与轻步兵；
  灰袍净化非法兵种、纠察支援缺省兵和玩家低声望调兵也都改用新的底层新兵。旧档中已经存在的
  `gwrecruit` 不会被删除，只会按新树由原版继续分流到三种终端，因此不需要存档迁移。
- 玩家 README 的 `v1.4-r7（开发中）` 条目已同步改写为“定期授予经验、原版决定升级”，并加入新底层
  新兵与三条并列终端；正式 `v1.4-r6` 中英文内容仍与 Git 标签逐字一致。本轮仍是普通开发工作，
  没有创建、改写或清理正式玩家 ZIP。
- 用户随后纠正各阶装备：新兵不应携带原版低阶长矛或帝国短剑，三套模板现均只装备模组单手剑
  `gwonehandedsword`，继续保持无盾。轻步兵仍是两套：无盾套完整保留原轻步兵的 `winfarmor`、
  `winfgloves`、`winflegs` 与 `winfhelmet`；持盾套不再使用原版轻盾或轻/重步兵衣甲，而是装备
  重步兵和骑士共用的银色模组盾 `wlarge_shield`，并完整采用弓箭手的 `warcharmor`、
  `warchgloves`、`warchlegs`、`warchhelmet` 与 `warchshoulder`。两套轻步兵都只使用
  `gwonehandedsword`，没有把重步兵护甲误配给持盾轻步兵。
- 最终 `Release -t:Rebuild --no-restore` 为 `0` 错误、`43` 条既有可空性/离线 NuGet 警告；`18`
  个 XML 全部解析成功。结构复核确认只有 `gwnewrecruit` 是底层基础兵且含三套装备模板，三套都
  固定佩戴 `warchshoulder`；`gwrecruit` 只有重步、弓手、骑士三个目标，而三个目标自身升级目标均为
  空。源码与最终反编译产物中 `ChooseUpgradeTarget`、`TRAINING_TROOPS_UPGRADED` 命中均为 `0`，
  `GreyWardenTrainingBehavior` 只以 `AddXpToTroop` 发放训练经验；ILSpy 复核输出保存在
  `.codex_tmp/training-native-upgrades-r7-20260723-b/`，程序集版本为 `1.4.7.0`。
- 自动部署后 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`。客户端与
  编辑器 DLL 均为 `637952` 字节，SHA-256 均为
  `7A55610CC649F63D0960E43D64BBD2B86E92BFDF805DE814DB0F780792D8487F`；仓库与实机中文 README
  SHA-256 均为 `EFA9F3C1E01FF49BDF78D621323C6757CB347AEA3383385E53D52FEFD8AA4FA6`，英文均为
  `6F9BE3736EA04F682E929BE285E7B2CE382A7185F9731133773F08774E868C7B`。本地正式包仍只有未改动的
  `GreyWarden-v1.4-r6.zip` 与匹配校验文件，没有为开发中的 r7 生成 ZIP。
- 最终部署后曾进行一次自动启动到初始界面的加载检查，日志为
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_30984.txt`：`Module Initialize end` 与
  `GauntletInitialScreen::HandleActivate` 各出现一次，GreyWarden 加载失败、Loader Exceptions 与
  dependency conflict 均为 `0`；该检查没有进入存档或推进大地图。用户随后明确“验证只能由用户进行”，
  因此这次记录只能作为既已发生的加载事实，不能当作本功能的玩法验收；从此不再由开发代理启动游戏、
  载入存档或宣称游戏内验证，练兵经验、原版分支选择和新兵种外观均留给用户亲自测试。
- 上述最终装备纠正完成后再次执行 `Release -t:Rebuild --no-restore`，结果为 `0` 错误、`43` 条既有
  可空性/离线 NuGet 警告。静态解析的三套 `gwnewrecruit` 均只有 `gwonehandedsword`，长矛、帝国短剑
  与盾牌命中均为 `0`；`gwrecruit` 恰有两套装备，其中无盾套仍为完整轻步兵甲，持盾套恰有一面
  `wlarge_shield` 并完整使用五件弓箭手穿戴部件。`18` 个 XML 全部解析成功，仓库 `_Module` 的
  `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`。客户端与编辑器 DLL 仍为 `637952`
  字节，SHA-256 均为 `7A55610CC649F63D0960E43D64BBD2B86E92BFDF805DE814DB0F780792D8487F`；
  `spnpccharacters.xml` SHA-256 为
  `55521528ECE5D53A45A928792F1DE245C3F99946D1C4FADD01AC06AA90F5E388`；仓库与实机中文 README
  SHA-256 均为 `A294D8F542ED99ABE4EAC69659F8A3DAC97F7684B6A177529920C24FD02315C8`，英文均为
  `C9E8899D2C95348604E36FE90EFE0E49CDB4F1BC0A910505936D196BFE02CF3D`。本轮没有启动游戏，也没有
  创建或改写正式玩家 ZIP；实际外观与战场行为仍由用户验证。
- 用户实测发现原版升级器明显偏爱骑士分支：三类精锐虽然都是轻步兵的直接终点，但骑士
  `gwknight` 为 `level=31`，重步兵与弓箭手均为 `level=26`，使骑士在原版分支评价中具有额外优势。
  现已只把骑士等级降为 `26`，令 `gwheavyinfantry`、`gwarcher`、`gwknight` 三个终端完全同级；
  武器技能、装备和升级结构均未改变。`Release -t:Rebuild --no-restore` 结果为 `0` 错误、`43` 条
  既有警告；静态复核确认三个终端均为 `level=26` 且升级目标均为空，`18` 个 XML 全部解析成功。
  自动部署后仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`；
  `spnpccharacters.xml` SHA-256 为
  `F5545A630F198448C0831A2341B64486F225570FDFB587FCA2B414C23EDED106`，仓库与实机中文 README
  SHA-256 均为 `1B70EF3897908AE8156117A20BADA9AD22CB1534AE07AE2185563AAA868B945D`，英文均为
  `4F044C935C1391FAB05D5DF815F0FF9F83232D2166A154B9B79E219CADBEB0E4`。本轮没有启动游戏，也没有
  创建或改写正式玩家 ZIP。

## 2026-07-23 玩家封地申诉与练兵官调兵订单

- 用户最终确认的玩家任务流程不采用“向接单领主当场付款”。玩家可以先向任意正常灰袍领主免费
  登记：封地申诉转给“贵族事务协调官”，调兵订单转给练兵官。对应专员先暂停普通案件、协力、
  重建、地方请求和自主练兵换防，以玩家任务专用的最高地图欲望分数前往玩家；只有专员与玩家实际
  见面后才收款。谈话延期、取消、付款或交付后会立即清除追随命令并让原版 AI 重算，避免反复拦截。
- 新增 `GreyWardenPlayerRequestBehavior`，保存最近一个月内的城镇/城堡易主记录、玩家攻城参与标记、
  首次封地决议结果、自动触发抽签和当前申诉阶段。只有玩家仍是灰袍成员、仍在同一王国、确实参加
  了攻城、首次决议已结束且封地未授予玩家时才可申请。首次落选后，以当前灰袍声望百分比进行一次
  自动受理抽签；手动申请与自动申请都只是登记，不提前扣款。
- 贵族事务协调官到达玩家身边后收取固定十万第纳尔。`TryCollectPlayerRequestPayment` 使用真实
  `GiveGoldAction` 把玩家金币全额转入灰袍族长钱包；该钱包就是现有司法公库/家族公库，而不是经手
  领主私款。付款后，协调官前往争议封地连续驻留二十四小时；离开定居点会重置驻留计时。若王国、
  所有权、成员资格等前提在新投票开启前失效，则关闭任务并全额退款。
- 新增 `GwpSettlementReconsiderationDecision : SettlementClaimantDecision`，并在
  `GwpSaveableTypeDefiner` 以本地类型号 `5` 注册，保证投票跨存档保存。候选名单保留原版评价最高的
  其他家族，同时强制加入玩家家族；裁决仍使用原版投票与 `ApplyChosenOutcome`，不会先清空当前
  封地主。玩家声望在受理付款时锁定为民间支持：低、中、高、极高档分别提供温和递增的功绩加成，
  不保证投票获胜，只保证玩家获得候选席位。
- `GreyWardenTroopRequestBehavior` 已从“与当前交谈领主直接买固定少量士兵”重构为单一持久化订单。
  声望决定可选兵种、每单数量上限与折扣；玩家向任意领主登记后不付款。练兵官从自己的真实花名册
  开始准备，缺少正确兵种路线时会与其他未在战斗中的灰袍部队进行一换一真实换兵，不改变双方总
  兵数；随后只通过 `TroopRoster.AddXpToTroop` 给能够通往目标的士兵经验，最终分支和升级费用继续
  完全由原版升级器控制。错误分支不会被代码直接改兵，后续周期会继续用真实兵员等量换入可训练
  批次。备足健康目标兵后，练兵官亲自赶到玩家身边，才收取报价并把真实士兵转入玩家队伍；延期或
  取消均不收费。
- 玩家订单价格改为更适合批量购兵的阶梯定价：基础价格按新兵、重步兵、弓箭手、骑士区分，声望
  逐档提供折扣；常规订单上限随声望由小批提高到大批，骑士仍受更低的专项数量上限约束。具体数值
  位于 `GwpTuning.TroopRequest`，玩家 README 只描述决策所需的趋势，不暴露内部公式。
- `GreyWardenPartyDesireBehavior.Intent` 现在真正保存调用者传入的优先级。普通灰袍职责继续使用
  `0.99`，两类玩家专员移动使用 `10`，因此玩家任务在灰袍任务拍卖中最高；这仍是原版欲望候选，
  没有冻结 AI，也不会覆盖正在发生的地图战斗。案件总卷新增封地申诉和调兵订单行，显示承办专员、
  阶段、费用/公库状态、民间支持、驻留剩余时间以及已备兵数。
- 新行为使用平行基础类型字段保存捕获记录和当前流程；调兵订单同样只保存兵种 id、数量、报价、
  阶段与时间，不保存 `MobileParty` 实例。协调官和练兵官每次均按职务持有人重新解析，因此职务
  继承后可继续任务。新增中文字符串已写入现有 `std_gwp_strings_xml-zho-CN.xml`，没有创建平行
  本地化文件。
- 本节属于 `v1.4-r7（开发中）` 的同一开发迭代，不新增第三条玩家更新记录，也没有创建正式 ZIP。
  用户明确游戏内验证只能由其本人完成；本轮只进行编译、XML 解析、静态结构和实时目录哈希核对，
  不启动游戏、不载入存档，也不把静态成功描述成玩法验收。
- 最终 `Release -t:Rebuild --no-restore` 构建为 `0` 错误、`44` 条既有可空性/离线 NuGet 警告；
  本次涉及的全部本地化 id 缺失 `0`、重复 `0`，`_Module` 内所有 XML 解析失败 `0`。自动部署后，
  仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致 `0`，实机继续没有
  `Assets`。客户端与编辑器 DLL 均为 `676352` 字节，SHA-256 均为
  `3949E7DBE68017ED510955B849C7F27069CC53CA3C225499AFB93598AC622D7E`；仓库与实机中文 README
  SHA-256 均为 `25087DA69B5CCA1E6608D514F908F42712DC2AF0A05E3FE3E7CC4FFCFB2A0A5F`，英文均为
  `4DC635C167AEA34F6A953ECC2B096AF4F24DA0B2ABCB99396AFC4D65C9892764`。本地正式发布文件仍只有
  未改动的 `GreyWarden-v1.4-r6.zip` 与匹配 `.sha256`，本轮没有生成开发版 ZIP，也没有启动游戏。

## 2026-07-23 封地申诉投票被立即自动裁定修复

- 用户实测协调官已经收取十万第纳尔并完成封地请愿，但没有出现可参与的重新投票，封地由系统直接
  重新分配。实机 `GreyWarden-AI-Diagnostics.log` 证明流程本身没有卡住：暮光于现实时间
  `18:56:06` 收款，`18:58:24` 抵达 `castle_EN7` 开始驻留，游戏时间推进满二十四小时后于
  `19:00:12` 记录 `PLAYER_FIEF_APPEAL_DECISION_OPENED`；不到一个游戏小时，暮光已被释放并接到
  其他案件，说明新决议已被原版自动裁定。
- 原因不是原版偶发重投，也不是协调官没有发起决议，而是
  `GwpSettlementReconsiderationDecision` 把原版 `KingdomDecision.HoursToWait` 从四十八小时覆写成了
  `0`。原版 `KingdomDecisionProposalBehavior.UpdateKingdomDecisions` 会在 `TriggerTime.IsPast` 且
  玩家不是最终裁决者时调用 `StartElectionWithoutPlayer()`；零等待令新决议加入王国列表后立刻满足
  该条件，因此玩家来不及看到或参加投票。
- 已删除零小时覆写，恢复 `KingdomDecision` 自带的四十八小时投票期限。二十四小时请愿完成后仍立即
  创建新封地决议，但接下来保留原版投票窗口；玩家家族仍由本模组保证进入候选名单，声望支持加成及
  原版最终授地逻辑不变。决议结束时新增
  `PLAYER_FIEF_APPEAL_DECISION_CONCLUDED` 诊断，记录目标封地、获胜家族和玩家是否参与，便于后续
  直接区分“已开启待投票”和“已完成裁决”。已经在本次测试中被瞬间裁定的旧申诉不会追溯重开，
  修复作用于之后创建的申诉决议。
- `Release -t:Rebuild --no-restore` 构建结果为 `0` 错误、`44` 条既有可空性/离线 NuGet 警告。
  反编译实机 DLL 已确认 `GwpSettlementReconsiderationDecision` 不再覆写 `HoursToWait`，并包含新的
  裁决完成诊断。仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，
  `18` 个 XML 全部解析成功，实机继续没有 `Assets`、`AssetSources` 或 `RuntimeDataCache`。客户端与
  编辑器 DLL 均为 `691712` 字节，SHA-256 均为
  `CAAD66C0866CBA56CDDDE4C27CAA408598430DB1F06E2FA15C728674BD564056`；仓库与实机中文 README
  SHA-256 均为 `484474A2E628C19CCC5DD7A6E2770188031647F95E53A1CED9CB11C14A9AEE53`，英文均为
  `7FB029C2EF8495CD1CED84424A35669CF6B42D536FCC5A034E410E92454B8D6A`。本轮没有启动游戏，也没有
  创建或改写正式玩家 ZIP；实际投票显示仍由用户验证。

## 2026-07-23 玩家专员交接对话与反复拦截修复

- 用户实测在向练兵官梵蒂提交简单调兵订单后，练兵官抵达玩家身边却进入旧的普通自我介绍，
  没有出现交兵选项、没有转移士兵，并因持续重新接触而让玩家无法离开。实机
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_29016.txt` 显示
  `14:33:26` 提交 `gwp_player_troop_order_file` 后，`14:35:42` 首次被梵蒂拦截，此后在
  `14:35:55`、`14:36:01`、`14:36:05`、`14:36:13`、`14:36:17` 等时间反复重开对话，
  期间从未进入 `gwp_player_troop_delivery_offer`。同时
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`
  显示梵蒂（`gw_leader_0`，练兵职务）反复以 `EngageParty` 追向 `player_party`，
  `dutyIntent=none`，且没有 `PLAYER_TROOP_ORDER_DELIVERED`。监控与用户现象一致。
- 根因是专员主动接触以对话状态 `start` 开场，而调兵交付和封地收款入口只挂在
  `lord_talk_speak_diplomacy_2`；旧的普通灰袍寒暄先取得了开场，任务入口依赖后续状态转换，
  失败后 `_nextContactHour` 仍允许下一次小时更新立即重新下达接触命令。因此不是兵员被虚空删除，
  而是交接对话入口和接触失败后的重试保护同时缺失。
- 调兵交付和封地申诉收款现各自增加优先级高于普通寒暄的 `start` 直达入口；普通灰袍寒暄在当前
  对话英雄正承担这两种玩家专员会面时明确退出。付款、交兵、延期、取消后的答复全部直接进入
  `close_window`，不再回到普通领主菜单。地图接触开始时还会先把下一次重试推迟十二小时并写入
  `PLAYER_TROOP_ORDER_CONTACT_STARTED` 或 `PLAYER_FIEF_APPEAL_CONTACT_STARTED`，即使其他模组
  干扰了对话，也不会在数秒内连续重新拦截。
- 新增资金不足保底：练兵官交兵或贵族事务协调官收款时，如果玩家当时付不起完整报价，专员会直接
  告知任务取消，任务状态和专员追随命令随即清除，不扣除任何金币；调兵取消另写
  `PLAYER_TROOP_ORDER_CANCELLED_INSUFFICIENT_FUNDS`，封地申诉沿用
  `PLAYER_FIEF_APPEAL_CANCELLED` 并记录 `reason=insufficient_player_funds`。
- 调兵多选列表的悬停说明已改为空，不再向玩家解释真实兵员、经验或原版升级器等内部实现；订单行
  本身只保留兵种、数量和总价，标题说明只保留声望会影响上限与可选兵种的必要决策信息。
- 本轮依旧只进行源码、构建、XML 与实机镜像核验，不启动游戏、不载入存档；交接能否正常弹出、
  士兵是否到账、资金不足自动取消以及是否仍会反复拦截，均留给用户本人按同一存档复测。
- `Release -t:Rebuild --no-restore` 最终构建为 `0` 错误、`44` 条既有可空性/离线 NuGet 警告。
  `_Module` 内 `18` 个 XML 解析失败 `0`，中文本地化 id 重复 `0`；自动部署后仓库 `_Module`
  的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致 `0`，实机继续没有 `Assets`
  或 `AssetSources`。客户端与编辑器 DLL 均为 `678400` 字节，SHA-256 均为
  `CFB7BA5CC1B8C6DF4DA84CBD5B40165AB4ECF415DCDC6ED29D1DD002EDF1B9CA`；仓库与实机中文
  README SHA-256 均为
  `09C8CBDC681239D6FD5C7F64C5545D0872808AF0DF84EA05372BC77E1D3191FE`，英文均为
  `3FF4C4D91D8D222DA46BC9517C1CD5AE1D1560555AF51FA6D6AB9D5518FFF17E`。本轮没有创建或改写
  正式 ZIP，也没有启动游戏。

## 2026-07-23 调兵交接后误入战斗修复

- 用户在上一轮修复后确认练兵官已能直接进入正确的交兵对话，士兵也成功到账，但关闭对话后引擎
  又拉起了战斗界面。新一轮实机
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`
  明确记录：`14:50:25` 梵蒂完成 `PLAYER_TROOP_ORDER_DELIVERED`，转移 `20` 名 `gwknight`、
  收取 `7200` 第纳尔；仅约一秒后，同一支梵蒂部队与 `player_party` 产生
  `MAP_EVENT_STARTED eventType=FieldBattle`。因此交兵本身已经成功，错误发生在对话关闭后的原版
  遭遇清理阶段。
- 根因是专员用 `SetMoveEngageParty` 触发玩家会面。虽然交接后已经清除移动欲望并下达 Hold，
  但只把对话终点设为 `close_window` 并不会自动和平结束底层 `PlayerEncounter`；残留的 Engage
  遭遇会在对话返回大地图时继续解释成 FieldBattle。项目既有招募使者和野外切磋流程已经证明，
  这类会面必须先设置 `PlayerEncounter.LeaveEncounter=true`，再在
  `ConversationEndOneShot` 回调中执行 `PlayerEncounter.Finish(false)`，之后重新下达离开命令。
- 调兵的付款交兵、延期、主动取消和资金不足取消现都会提前标记和平离开，并在对话彻底结束后调用
  `GwpCommon.TryFinishPlayerEncounter()`；随后再次停止练兵官接触并按订单是否结束释放其任务占用。
  回调写入 `PLAYER_TROOP_ORDER_ENCOUNTER_FINISHED`，便于确认遭遇已经关闭且没有生成 FieldBattle。
- 同构的封地申诉付款、延期、撤回和资金不足取消也加入相同保护。协调官付款成功后，会在和平关闭
  玩家遭遇之后重新取得前往争议封地的 Visit 指令，避免原版遭遇清理覆盖她接下来的行程；对应监控
  为 `PLAYER_FIEF_APPEAL_ENCOUNTER_FINISHED`。
- 本轮仍不由开发代理启动游戏。用户可继续使用当前存档重下订单，重点核对交兵后是否直接回到大
  地图、是否不再出现战斗准备界面，以及监控中交兵后是否出现
  `PLAYER_TROOP_ORDER_ENCOUNTER_FINISHED` 而不再紧跟 `MAP_EVENT_STARTED FieldBattle`。
- `Release -t:Rebuild --no-restore` 最终为 `0` 错误、`44` 条既有警告；`18` 个 XML 解析失败
  `0`。自动部署后仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致
  `0`。客户端与编辑器 DLL 均为 `679424` 字节，SHA-256 均为
  `380C0A7A0EE2437257DD239D9CC2814E57107F6C4555716BF7FB14220584C952`；仓库与实机中文
  README SHA-256 均为
  `10383789F30A9A51A6481792C738B2C17F46D582CC92CA0AE4999752F31D0E76`，英文均为
  `136D3ED6367AFF0E941F3691998D1F310AFD1A637C2B5FFBEBE7C52DE6E4595F`。没有创建正式 ZIP，也没有
  启动游戏。

## 2026-07-23 练兵任务强制排队与两小时交接

- 用户实测练兵官梵蒂已经把部队训练至兵种树终点，却仍连续承接普通案件，始终不做练兵换防。
  `GreyWarden-AI-Diagnostics.log` 中只有多轮 `TRAINING_TROOP_XP_GRANTED`，没有任何
  `TRAINING_TASK_ASSIGNED/COMPLETED/CANCELLED`；随后梵蒂持续显示
  `ordinaryCaseEligible=True` 并承办 `lord_6_13` 等普通案件。
- 根因并非 Visit 欲望分数输给普通任务，而是旧实现只有在练兵官和接受换防的领主同时空闲后才
  创建 `TrainingAssignment`。在任务对象建立以前没有练兵欲望，也没有任务占用；普通派案行为又
  先于练兵行为执行，所以练兵官刚结束一案就可能立即再接一案，永远无法进入任务创建入口。
- 新流程在练兵官的全部灰袍正规士兵都没有后续升级目标时立即写入一个没有接受方、没有会合地点
  的待办练兵任务，并将其作为练兵官的下一项强制职责。现有任务不会被中途丢弃，但普通案件、
  协力支援、收养救济、重建和地方请求的后续派发都会识别这项预留，不再把新任务插到它前面。
- 练兵官完成当前任务后，系统选择距离她最近、不是练兵官且仍有可升级灰袍士兵的领主作为接受方；
  接受方即使正在处理自己的任务也会被预留，完成手头任务后不再承接下一项普通职责。双方分别在
  空闲时取得前往同一安全城镇或城堡的 Visit 欲望，不要求同时空闲才建任务，也不拉军团。
- 双方进入同一会合定居点且都已结束旧任务后开始驻留；交接时间由六小时缩短为两小时。完成时仍
  按一换一真实转移兵员：练兵官优先交出最高阶终点兵，接受方优先交出最低阶可升级兵，伤兵状态
  保留。接受方失效或其待训练士兵自然升满时，任务保留并重新寻找接受方，而不是把练兵官放回普通
  派案池。
- 案件总卷现在区分“已列为下一项任务”“等待双方完成当前任务”“前往会合地点”和“两小时换兵”；
  尚未选定接受方的练兵任务计入等待任务，而不是伪装成已经承办。新增监控事件
  `TRAINING_TASK_QUEUED` 与 `TRAINING_RECIPIENT_REQUEUED`，原有分配、驻留、完成事件继续保留。
- 每名可升级士兵每六小时获得的经验最终从 `750` 下调至 `200`；用户认为前一轮暂定的 `500`
  仍然过高，因此再次下砍。模组仍只调用
  `TroopRoster.AddXpToTroop`，不直接改兵或选择升级分支，升级继续完全由原版控制。
- 本轮不启动游戏、不载入存档，玩法验证仍由用户本人完成。开发代理只进行编译、XML/本地化静态
  检查、部署和仓库到实机的哈希核对；本节属于 `v1.4-r7（开发中）` 的同一迭代，不生成开发 ZIP。
- 最终 `Release -t:Rebuild --no-restore` 构建为 `0` 错误、`44` 条既有可空性/离线 NuGet
  警告；`18` 个 XML 解析失败 `0`，中文本地化 `824` 个 id、重复 `0`。源码断言确认“满级即排队、
  最近非练兵官、双方分别等待当前任务、经验 `200`、驻留 `2` 小时”全部为真。自动部署后仓库
  `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致 `0`。客户端与编辑器
  DLL 均为 `683008` 字节，SHA-256 均为
  `CBB7F9D3E524F51F1B4CF9DBD8B0D061C7A8B310E5D666D2550483212AF0767A`；仓库与实机中文
  README SHA-256 均为
  `273F9B9B5EA5CB39A14ECF84BFAC6766084C312197991265F0BC60E70A683347`，英文均为
  `069C1E8011E402ABE89F1C7BE15FCC247A6B2FA2942E4CCFF732862484C0105F`。没有启动游戏，也没有
  创建或改写正式 ZIP。

## 2026-07-23 练兵任务排队后被补员取消

- 用户继续实测时再次观察到梵蒂长期承办普通案件。最新实机监控给出了完整因果链：
  `15:19:44`、游戏时 `628882.94`，梵蒂以 `198` 名终点兵成功写入
  `TRAINING_TASK_QUEUED`；约十个游戏小时后部队人数从 `199` 增至 `200`，监控随即写入
  `TRAINING_TASK_CANCELLED reason=trainer_role_or_roster_changed`。之后再无分配、驻留或完成事件，
  梵蒂重新显示 `ordinaryCaseEligible=True` 并连续接案。
- 取消后的监控持续出现 `TRAINING_TROOP_XP_GRANTED`，待训练人数先为个位数，随后因战斗损失和
  原版/模组补员增加至 `64`。这证明任务曾经正确排队，问题不是经验没有发放、欲望分数不足或找
  不到会合城，而是 `UpdateAssignments` 每小时重新要求练兵官仍保持“零名可升级士兵”；任意新兵
  加入都会撤销已经锁定的下一职责。
- 按用户定义，“确认全员满级”只负责触发一次强制排队，排队成功后必须锁存，不能再被后来补入的
  新兵推翻。现已移除活动任务对 `IsFullyTrained` 的持续取消条件；只有练兵官失去练兵职务才取消。
  后续新增的低级兵可以继续获得训练经验，但不会解除练兵官和接受方的任务预留，也不会允许新的
  普通案件插队。
- `Release -t:Rebuild --no-restore` 构建为 `0` 错误、`44` 条既有警告；源码检查确认活动任务
  只剩职务失效取消条件，不再包含 `!IsFullyTrained(trainer)`。自动部署后仓库 `_Module` 的
  `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致 `0`。客户端与编辑器 DLL 均为
  `683008` 字节，SHA-256 均为
  `1FB21C2F1C85D10C5AD8CF9F1F950279405BA85329226C69E5E67DB435196112`；仓库与实机中文
  README SHA-256 均为
  `4A369522762EFAFE4C15F11C4CEF142A615A18B98F36EE4ACC1EA3D6AA98931B`，英文均为
  `5AF68F8998AA8DDA4E1E8C5BB5B395960C0CB6B132D2130D5C1C16104E603DEC`。没有启动游戏，也没有
  创建正式 ZIP。

## 2026-07-23 新成年领主滞留城内：撤回欲望压制并定位原版来源

- 用户指出不能直接压制“前往当前城镇”欲望，必须先查明它为何产生。上一轮尚未经过用户验证的
  `SuppressAssignedSelfSettlementScores` 已完整撤回，中文和英文玩家日志中对应的“已修复”
  表述也已删除；当前实机 DLL 不再修改这条原版欲望的分数。
- 实机监控仍证明静澜并非没有部队：`CharacterObject_2875_party_1` 是活动领主队，
  `isLordParty=True`、人数 `147`、零伤兵、满编、零俘虏，并携带约三十天食物。她已承办
  `CharacterObject_1546`，但 `GoToSettlement@town_K3` 长期保持约 `1.2278–1.2318`，高于
  模组职责候选 `0.99`。
- 对 1.4.7 实机 `TaleWorlds.CampaignSystem.dll` 的 `AiVisitSettlementBehavior` 反编译确认：
  领主的 `GoToSettlement` 候选只由原版访问定居点行为建立；分数综合缺粮、伤兵、缺员招募、
  出售物品、俘虏、家乡访问、航行类型和延续当前目标等因素。现有监控已经排除缺粮、治疗、招募
  和交俘，因此这次不是补给欲望。
- 由原版公式反算，健康满编领主在当前城镇的基础项、`1.2` 倍当前目标延续和 `1.5` 倍相同航行
  类型相乘后为 `0.4608`；实际分数约 `1.2278`，恰好还存在约 `2.664` 倍的城镇出售物品因子。
  原版 `PartiesSellLootCampaignBehavior` 只在 `SettlementEntered` 事件发生时出售战利品；如果
  部队已经在城内时才获得物品，或城镇资金不足未能全部买下，剩余物品会继续产生“访问当前城镇
  以出售”的欲望，却不会再次触发入城出售事件。这是当前最符合日志与原版公式的原因，而不是粮食
  补给。
- 诊断状态新增 `nonFoodCargoValue`、`mountCargoValue`、`nativeSellFactor`、
  `currentSettlementGold`、`leaderHomeSettlement` 和 `leaderTimeAtHome`，不改变任何欲望或
  行动。用户再次载入并运行后，可以直接区分是未出售货物、城镇无钱，还是家乡访问因子；确认后
  再决定应修正物品出售时机还是新部队生成顺序。
- `Release -t:Rebuild --no-restore` 构建为 `0` 错误、`44` 条既有警告；`18` 个 XML 解析失败
  `0`。自动部署后仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致
  `0`。客户端与编辑器 DLL 均为 `684032` 字节，SHA-256 均为
  `6C38F4F825A5C77CF2AA7CFFAD249944702D9D0FE9680E0D576F605C5503EE3C`；仓库与实机中文
  README SHA-256 均为
  `4A369522762EFAFE4C15F11C4CEF142A615A18B98F36EE4ACC1EA3D6AA98931B`，英文均为
  `5AF68F8998AA8DDA4E1E8C5BB5B395960C0CB6B132D2130D5C1C16104E603DEC`。没有启动游戏，也没有
  创建或改写正式 ZIP。

## 2026-07-23 新成年领主离城原因与领主套装补正

- 用户继续运行后观察到静澜终于离开 `town_K3`。最新监控证明这次并不是城内出售需求自行完成：
  她的非食物货物价值仍为 `840`、坐骑货值仍为 `9905`、原版出售因子仍为 `2.676`，
  `GoToSettlement@town_K3` 也仍有 `1.2331`，所有这些数据在离城前后都没有下降。
- 实际离城触发点是上一宗 `CharacterObject_1546` 案件随
  `MAP_EVENT_ENDED eventType=SallyOut` 结束并从她身上释放。游戏时 `629878.97` 的下一轮原版
  AI 竞价里，她没有模组职责候选，而原版
  `PatrolAroundPoint@town_EN5=3.0900` 明显高于仍存在的
  `GoToSettlement@town_K3=1.2331`；最终行为因此变为前往 `town_EN5` 巡逻，她才从城内出来。
  再下一小时新案件 `lord_6_22` 生成后，职责候选为 `0.99`，此时旧城访问候选已经降至
  `0.8221`，所以她继续直接追案。这轮没有压制或修改任何访问城镇欲望。
- `1.2331` 到 `0.8221` 的比例恰好是原版 `AiVisitSettlementBehavior` 中相同航行类型延续
  奖励 `1.5` 的有无差异。为让下次直接看到这一状态，监控新增
  `desiredNavigation` 字段；这只是诊断，不改变欲望分数或移动行为。
- 同一名新成年领主没有穿灰袍领主装备的原因已经定位。收养时她先使用原版儿童初始装备；
  `PoliceResourceManager` 虽然监听了 `HeroComesOfAgeEvent` 并尝试换上
  `gw_leader_0` 的领主套装，但原版
  `AgingCampaignBehavior.OnHeroComesOfAge` 也监听同一事件，并在自己的回调中分别调用
  `GetEquipmentForHeroComeOfAge` 重新生成文化战斗装与平民装。事件监听器顺序不能保证，
  因此原版成年换装能够在模组换装之后再次覆盖它。
- 新增 `GwpAdultCommanderLoadoutPatch`，直接后缀原版
  `AgingCampaignBehavior.OnHeroComesOfAge(Hero)`：等原版两套装备都分配完毕后，再把
  `gw_leader_0` 的完整战斗/平民领主套装复制给成年灰袍。修正只在成年事件发生一次，不做逐小时
  强制换装。为兼容当前旧档，载入时还会检查所有非六名初始领主的成年灰袍成员，只在装备与模板
  不一致时补正；静澜在下次载入当前存档时即可得到正确套装。成功补正会写入
  `ADULT_COMMANDER_LOADOUT_APPLIED`，并标明 `native_coming_of_age_postfix` 或
  `existing_save_repair`。
- `Release -t:Rebuild --no-restore` 构建为 `0` 错误、`44` 条既有警告；实机反射确认
  `AgingCampaignBehavior` 为公开类型且存在唯一
  `Void OnHeroComesOfAge(Hero)` 目标。`18` 个 XML 解析失败 `0`。自动部署后仓库
  `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致 `0`。客户端与编辑器
  DLL 均为 `685568` 字节，SHA-256 均为
  `AFF24ABE53127C068536DE6541D129368733EEFA91017F766A2805018BF002BB`；仓库与实机中文
  README SHA-256 均为
  `9A8AF45DD9CB3BCE52F3E24A476915CB3EC29602EEE6F26D2E80561863044903`，英文均为
  `C696E6248AFFC158701D833610A68B565838D5BA89D409F323EBC49FBB57C789`。没有启动游戏，也没有
  创建正式 ZIP；玩法验证仍由用户本人完成。

## 2026-07-23 练兵官训练经验小幅上调

- 最新实机监控中，梵蒂共触发 `66` 次 `TRAINING_TROOP_XP_GRANTED`，但
  `TRAINING_TASK_QUEUED/ASSIGNED/COMPLETED/CANCELLED` 全部为 `0`。可升级兵最低一度降至
  `4` 人，却在入城补员后随部队人数约从 `159` 增至 `188` 而重新升至 `34` 人；因此并非练兵
  任务生成后卡在欲望、接收人或会合阶段，而是“可升级兵必须为零”的生成条件始终没有达到。
- 按用户决定，不改变全员满级后才排入练兵任务的规则，也不直接控制升级或阻止原版补员；仅将
  练兵官每六小时给予每名可升级士兵的经验从 `200` 提高到 `250`。升级时机和兵种分支仍完全由
  原版系统决定。
- `Release -t:Rebuild --no-restore` 构建为 `0` 错误、`44` 条既有警告；`18` 个 XML 解析失败
  `0`。自动部署后仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256
  不一致 `0`。客户端与编辑器 DLL 均为 `685568` 字节，SHA-256 均为
  `C9AA98ACF6BE61C60AD8DFFA147BB183B4EAFB51310770E7D4A9FBF61450F831`；仓库与实机中文
  README SHA-256 均为
  `4FBE9C8255C35E11F3840DF41687B180B377C5710AA352B7993F973CB70A6970`，英文均为
  `ED6B2B06C1754D1F4DF73A0D349215CCC91C0BA4D113901D9DC5BAE735022245`。没有启动游戏，也没有
  创建正式 ZIP。

## 2026-07-23 被逐出定居点的犯人强制反攻承办领主

- 协力军团因高速目标而解散后，目标仍可能钻入定居点。既有围堵保底会在承办领主抵达城门并停滞
  足够时间后用 `LeaveSettlementAction` 把目标拉出城；若目标属于军团，则拉出军团领队及附属
  部队。问题在于逐出后代码立即对这些部队下达 `SetMoveModeHold`，而每帧维护和定居点禁入补丁
  又会继续下达 Hold。目标因此固定停在城门口；城内多支敌方小部队与城外灰袍领主又可能被原版
  安全判断互相阻止接战，形成长期僵局。
- 曾讨论过让目标与所有定居点保持约 `15` 的排斥距离，但最终没有采用地图半径和强制寻路方案。
  用户确定的更直接规则是：犯人被拉出城后，立即给被拉出的目标下达“进攻本案承办领主”的移动
  指令。
- `TryForceExpelShelteredCriminal` 现在不再 Hold 目标，而是读取当前任务的
  `PolicePartyId` 作为承办领主并调用 `SetMoveEngageParty`。若犯人在军团内，实际移动指令发给
  军团领队；否则直接发给犯人部队。`OnTick` 在案件有效期间持续重发该指令，避免原版大地图 AI
  重新把目标停在城门口。
- 任务期间禁止重新进入定居点的保护继续保留，但
  `GwpCaseSettlementEntryPatch` 拦下进城动作后不再 Hold，而是再次把目标或其当前军团领队
  重定向到承办领主。跟踪成员会按犯人当前军团实时刷新，军团解散后不再永久控制已经脱离犯人的
  旧附属部队。案件结束、撤销、战争追捕状态消失或目标失效后，跟踪和强制指令自动解除。
- 本轮仍不启动游戏；实际城门僵局是否按预期进入接战由用户本人复测。静态验证完成：
  `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条既有可空性/离线 NuGet 警告，
  `18` 个 XML 解析失败 `0`。自动部署后仓库 `_Module` 的 `25` 个正常客户端文件与实机相比
  缺失 `0`、SHA-256 不一致 `0`。客户端与编辑器 DLL 均为 `686080` 字节，SHA-256 均为
  `097B98D48CE8BD47F0F71D70B576118993DB517D4C65C60A64377814DFB1263B`；仓库与实机中文
  README SHA-256 均为
  `F5C1366696C6609C558BF4C16FD01317C5157AE01FABA2C5277FCD3D1D37344C`，英文均为
  `71893EC432E19E22A6BC3BDD111D599CD6CCBCB0248D44F4941871D501ECFBCB`。没有创建或改写正式
  ZIP。

## 2026-07-23 英雄百科震慑恢复时间与恢复速度再次减半

- 英雄百科现有“案底与震慑”入口已经分别展示侵害村民与侵害商队的当前压制、来源和欲望倍率，
  但玩家无法从震慑数值判断还要经过多久才会恢复正常。本轮在两类震慑各自的来源行后加入独立的
  “预计恢复原状”时间。
- 预计时间不以数值严格降到 `0` 为终点，而是使用玩法实际恢复点
  `GwpTuning.Deterrence.ForgetThreshold`：震慑降至该阈值时，
  `GetCrimeDesireMultiplier` 已返回原版倍率 `1`，玩家实际不再受到灰袍压制。计算式为
  `(当前分类震慑 - ForgetThreshold) / 当前人物每日恢复量`，村庄与商队震慑并行恢复，因此分别
  计算和显示。少于一天时显示小时，其余显示游戏天数。
- 震慑恢复只在人物拥有活动部队或停留于定居点时进行；被俘或没有有效落脚状态时原有逻辑会暂停
  衰退。百科会明确显示“当前暂停恢复”，并同时给出恢复条件重新满足后仍需的大致时间；已经低于
  有效阈值的分类显示“已经恢复”。
- 用户要求恢复速度再次减半。为保证基础值、人物性格修正及最小/最大恢复边界全部等比例变慢，
  没有只改 `BaseRecoveryPerDay`，而是在既有性格计算和边界裁剪完成后统一乘
  `RecoverySpeedMultiplier = 0.5`。村庄与商队两套衰退都调用同一个
  `GetRecoveryPerDay`，因此实际恢复和百科预计时间使用完全相同的速度。
- `v1.4-r7（开发中）` 的中英文玩家日志已同步说明百科预计时间和震慑持续时间调整。新增五条中文
  本地化文本；中文表共 `829` 个 string id、重复 `0`。
- 本轮不启动游戏，界面显示与存档时间推进仍由用户本人验证。最终
  `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条既有可空性/离线 NuGet 警告；
  `18` 个 XML 解析失败 `0`。自动部署后仓库 `_Module` 的 `25` 个正常客户端文件与实机相比
  缺失 `0`、SHA-256 不一致 `0`。客户端与编辑器 DLL 均为 `688128` 字节，SHA-256 均为
  `5B77131F3059C18CD187D3BD02355329F525A4F758A896FB734A26BB4563970E`；仓库与实机中文
  README SHA-256 均为
  `FFF4E320C84DF5B7783A9507DDFD837BBD20F78FA5F283ED1FC7F4172CC33F47`，英文均为
  `B56480B2A47454669A4DDC8C067527B426EC80EE648825499746475179974790`。没有创建或改写正式
  ZIP。

## 2026-07-23 被逐出定居点的犯人锁定反攻欲望

- 上一轮虽然逐帧重发“进攻本案承办领主”的移动指令，但目标的原版大地图 AI 仍可在两次重发
  之间重新竞价其他欲望，因此可能在进攻方向和原版目标之间左右摇摆。仅仅提高指令重发频率不能
  保证“案件结束前只做反攻”。
- `TryForceShelteredCaseAttack` 现在会先临时允许一次 AI 指令写入，调用
  `SetMoveEngageParty` 后立即启用 `SetDoNotMakeNewDecisions(true)`。这不会删除目标原有的
  欲望数据，但会停止新的原版大地图决策，使当前唯一可执行的移动目标保持为承办领主；即使写入
  进攻指令时发生异常，也会再次尝试维持决策锁定。案件仍有效时逐帧维护继续校准进攻目标。
- 犯人在军团中时锁定实际控制移动的军团领队；军团成员关系改变时会刷新跟踪，并立即恢复已经
  脱离当前犯人移动组的旧控制者。军团解散后，下一轮维护会转为锁定犯人自己的部队。
- 解除路径采用双重保底：任务、案件或目标失效时，逐帧维护调用
  `ReleaseShelteredForcedAttack`；正常任务战争跟踪清理时，
  `ClearTaskWarTracking` 也会立即调用同一方法。解除会执行
  `SetDoNotMakeNewDecisions(false)` 并要求原版 AI 在下一小时重新思考，确保案件结束后恢复
  正常欲望系统。任务与受控部队的对应关系还会用两列 string id 写入存档；中途保存再载入后会
  继续维持唯一进攻指令，并且仍能在案件结束时准确恢复这些部队，而不是因运行时字典重建而永久
  锁住 AI。
- 一次中间重构把异常保护误放进相邻的逐出方法，导致重建出现
  `CS0103: forceParty does not exist`；该位置已立即撤回并移到
  `TryForceShelteredCaseAttack` 的异常分支。最终
  `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条既有可空性/离线 NuGet 警告；
  `18` 个 XML 解析失败 `0`，中文本地化共 `829` 个 string id、重复 `0`。
- 自动部署后仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致
  `0`。Release 构建产物、客户端 DLL 与编辑器 DLL 均为 `690176` 字节，SHA-256 均为
  `7E1CBFA7AEB0826FBEFEFFC20640795EBAA51A697CFA1E27B1E38900108717D3`；仓库与实机中文
  README SHA-256 均为
  `82018FFC770298AF6C585EF9CE947458525B0D0C05969F8DD3C446599A20031B`，英文均为
  `966208D2F5FC30999E3889F31075A4610F05708EA789121DD3FF89B42A3C2D6F`。本轮没有启动游戏，
  玩法验证仍由用户本人完成，也没有创建或改写正式 ZIP。

## 2026-07-23 弱势追捕触发协力与原版动态相向集合

- 实机监控确认本轮未触发协力的直接原因不是灰袍没有打不过，而是
  `IsCasePursuitBlocked` 同时要求长期行为必须为
  `GoAroundParty@本案犯人`。梵蒂追捕 `lord_1_14_party_1` 时，原版短期行为已经连续为
  `FleeToPoint`，`shortTarget` 也明确是该犯人，双方距离最低约 `0.48`；但长期行为被原版补给
  竞价切成 `GoToSettlement`，所以旧判定每小时都把受阻累计清零，监控始终显示
  `assistance=none`。
- 协力受阻判定现改为读取原版已经完成的强弱判断：只有短期行为属于原版逃跑行为，而且
  `ShortTermTargetParty` 是本案犯人、犯人当前战斗控制者或其同一支军团时才累计。长期行为可以
  同时是补给、访问或其他原版选择，不再要求必须恰好保持 `GoAroundParty`；因此仍然只在原版
  确认“正在躲这个案件目标”时求援，不会把所有普通移动或逃离其他敌人的情况误算为协力。
- 直接反编译当前 Bannerlord 1.4.7 的 `TaleWorlds.CampaignSystem.dll` 后确认，原版没有一个
  “以可配置的更远半径动态监视敌军”的公开移动模式。`GoAroundParty` 使用
  `EncounterJoiningRadius * 1.15` 的固定近距离防守环；`EscortParty` 会由引擎持续刷新被跟随
  部队的实时位置，但它本身是靠近精确目标；只有 `GoToPoint` 能指定任意远处坐标，却是静态点，
  若拿它监视移动目标就必须反复重算。故没有采用循环 `GoToPoint`。
- 协力军团新增独立集合阶段：支援领主加入原版 `Army` 后，支援方仍按原版军团逻辑护送发起人；
  发起人则临时使用原版 `SetMoveEscortParty` 动态跟随最近的尚未并入军团成员，让双方相向靠拢。
  引擎自己刷新两队的位置，模组只在集合目标改变或命令被原版状态打断时重发一次，不逐帧、不每
  小时重算地图坐标。集合期间暂时停止发起人的新战略竞价，防止 `AiPartyThinkBehavior` 用其他
  长期行为覆盖集合命令或拆散无王国军团；原版短期逃跑和接战判断不被清空。
- 所有支援领主真正 `AttachedTo` 发起人后立即解除集合控制，并要求原版在下一小时重新拍卖，
  回到案件追捕。案件结束、玩家高优先委托接管、军团因目标速度过快而解散、成员失效和旧存档
  恢复都设有解锁路径。集合控制状态已写入存档，避免集合中途存读后遗留永久决策锁。
- 同时修正原有手工合并距离比较：原代码把二维距离平方直接与线性接触半径比较，现改为与接触
  半径平方比较，确保进入原版允许的陆地或海上军团接触范围即可并入。
- 静态断言确认：协力判定不再要求长期 `GoAroundParty`、使用
  `ShortTermTargetParty`、集合使用原版 `SetMoveEscortParty`、协力代码不包含
  `GoToPoint`、集合控制已持久化且欲望最终拍卖会避让集合状态。中英文 `v1.4-r7（开发中）`
  玩家日志已同步。ILSpy 还把实机 DLL 反编译到
  `.codex_tmp/PoliceEnforcementBehavior.Assistance-final.cs`，再次确认部署产物包含动态
  `SetMoveEscortParty`、短期逃跑目标、集合存档键及接触半径平方比较。
- 最终 `Release -t:Rebuild --no-restore` 构建为 `0` 错误、`44` 条既有可空性/离线 NuGet
  警告；`18` 个 XML 解析失败 `0`，中文本地化共 `829` 个 string id、重复 `0`。自动部署后
  仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致 `0`，实机
  没有 `Assets`、`AssetSources` 或 `RuntimeDataCache`。客户端与编辑器 DLL 均为
  `692224` 字节，SHA-256 均为
  `6C5C334CF886195C5472954180D0C442AFD833AA40344086D738804D2D2FA054`；仓库与实机中文
  README SHA-256 均为
  `80F73B7451FC3923BB5DDB0C47EE5348AF8B2C566197AAC4FAB53840774DA637`，英文均为
  `C29E562C62C37CE8BC704B8B9B5F122FCD2EF3EBB64A6A339652835874D00FD6`。没有启动游戏，
  玩法验证仍由用户本人完成，也没有创建或改写正式 ZIP。

## 2026-07-23 撤回协力集合决策锁并扩大 GoAroundParty 监视环

- 用户明确纠正：灰袍领主的长期欲望必须始终开启。协力任务只能像其他任务一样向原版欲望拍卖
  加入一个固定分值候选；任何更高的原版长期欲望都应正常获胜。上一小节所记的“发起人直接
  `SetMoveEscortParty` 并启用 `SetDoNotMakeNewDecisions(true)`”方案因此在用户实机验证前
  完整撤回，不再作为当前实现。
- 再次反编译 `MobilePartyAi.GetBehaviors`、`GetGoAroundPartyBehavior`、
  `GetFollowBehavior`、`GetLandPatrolBehavior` 和 `AiPartyThinkBehavior.PartyHourlyAiTick`
  后确认：`EscortParty` 会追到指定部队的精确实时位置；`PatrolAroundPoint` 只围绕一个静态
  中心并最终落实为 `GoToPoint`；`DefendSettlement` 只接受定居点；逃跑行为属于原版短期强弱
  判断。原版长期行为中，只有 `GoAroundParty` 同时满足“动态跟随敌军、保留原版逃跑判断、不过
  度远离目标”，所以发起人的任务欲望继续使用它，而不是另造坐标循环。
- `GoAroundParty` 贴得过近的根因也已确认：原版传入
  `GetEncounterJoiningRadius * 1.15`，当前值约为 `3.45`；随后
  `GetDefendingPosition` 又用 `defendRadius² / 2` 生成监视位置，最大只在目标外约 `5.95`
  地图单位。现在只在协力军团尚未集合完成、且本次 `GoAroundParty` 确实指向本案犯人时，把
  传给原版防守位置算法的半径提高到 `5.5`，实际外围距离约为 `15`。位置仍由原版
  `GoAroundParty` 动态刷新，模组不写 `GoToPoint`，也不逐帧计算目标坐标；集合结束后立即恢复
  原版普通半径。
- 协力组现在直接向统一欲望层提供任务候选：发起人为
  `GoAroundParty@本案犯人 = 0.99`，支援领主为
  `EscortParty@发起人 = 0.99`；高速解散后的各领主则都是
  `GoAroundParty@本案犯人 = 0.99`。这不是直接移动命令，也不关闭 AI。协力候选还标记为
  `PreserveAllNativeDesires`，因此连普通案件会压低的原版巡逻分也不改动；补给、疗伤、招兵、
  访问、巡逻等任意原版长期欲望只要高于 `0.99` 就照常先执行，之后再回到协力任务。
- 已移除 `AssemblyControlActive`、`gwp_enf_assist_assembly_control`、集合期间的
  `SetDoNotMakeNewDecisions(true)`、直接 `SetMoveEscortParty` 与相应欲望拍卖短路。为兼容
  可能用上一轮短暂 DLL 保存的本地测试档，灰袍每小时既有旧锁清理和协力释放时的
  `RestoreAi` 仍会解除保存于原版 `MobilePartyAi` 的遗留锁。
- 扩大原版半径使用一个窄范围 Harmony 前缀，目标是
  `MobilePartyAi.GetDefendingPosition`，并同时核对当前 AI 所属部队、本案目标坐标和“仍有
  未附着支援领主”三项条件。私有接口定位带 `Prepare` 保底：若某个兼容游戏修订中目标签名或
  `_mobileParty` 字段不存在，只跳过半径增强，不阻止模组其余补丁载入。
- 源码与部署 DLL 反编译断言确认：协力文件不再含集合决策锁和直接 `GoToPoint`；发起人
  `GoAroundParty`、支援人 `EscortParty`、完整保留原版分数的标记及窄范围半径补丁均已进入
  实机 DLL。中英文 `v1.4-r7（开发中）` 玩家日志已改为当前最终行为。
- 最终 `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条既有可空性/离线 NuGet
  警告；`18` 个 XML 解析失败 `0`，中文本地化共 `829` 个 string id、重复 `0`。自动部署后
  仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致 `0`，实机
  没有 `Assets`、`AssetSources` 或 `RuntimeDataCache`。客户端与编辑器 DLL 均为
  `691200` 字节，SHA-256 均为
  `675C00CE614C39C29A59DC0442BF8903EB964A8B49CE821BBD9154CDBEC3809C`；仓库与实机中文
  README SHA-256 均为
  `345FAF578C663900D6926C2FE62F0F624046808074FE8998F05BA931F2C123AC`，英文均为
  `CE050384ECB7374E409B4587174223AD1E2D9D240C7ECA7A8C63A5A076D01DD5`。本轮没有启动游戏，
  玩法验证仍由用户本人完成，也没有创建或改写正式 ZIP。

## 2026-07-23 玩家专员按完成任务延期、申诉重提与民间支持重算

- 用户实测发现三个相互关联的问题：自动受理的封地申诉因资金不足取消后，会被同一条成功抽签记录
  再次自动激活；协调官和练兵官的“之后再来”只使用短小时冷却，导致很快再次拦截；重新投票虽然
  已能正常打开，但旧实现只给玩家候选功绩乘以最高 `1.20`，无法形成用户要求的明确民间基础票。
  同时用户把请愿费用由十万下调为五万，并要求一个月内多块合格封地可由玩家明确选择。
- 捕获记录新增持久化位 `AutoOfferConsumed=64`，把“随机抽中自动受理”和“这次自动受理已经实际
  发出”分开。每次自动或手动激活一条已经抽中的记录都会消费自动机会；取消只清除
  `RequestFiled`，不会清除消费位。因此同一封地不会再次自动弹出，但 `IsEligible` 不排除消费位，
  玩家仍可在攻下后的三十天内找任意灰袍领主手动重申。载入旧档时，若一条带
  `AutoTriggered` 的请求已经处于活动阶段，会自动补记消费位，避免旧档取消后复发。
- `GetEligibleCaptures` 继续列出一个月内所有合格城镇和城堡，并按 `SettlementId` 去重、保留最近
  一次捕获记录；对话中的原有封地多选询问现在可稳定显示多块不同封地，而同一封地不会因反复易主
  出现重复行。一次仍只承办一份活动申诉，撤回后可重新打开列表选择仍在期限内的目标。
- 协调官资金不足时不再自动关闭对话和取消任务，而是进入与正常收款相同的选择页：玩家可搁置或
  撤回，付款选项因余额不足自然不可用。请愿费常量改为 `50000`，收款、退款、案件总卷与中英文
  对话全部读取同一常量或对应五万文本，款项仍全额进入司法公库。
- 两类玩家专员新增持久化 `DeferredTasksRemaining`。主动选择延期时设为 `2`、清除玩家接触欲望并
  立即释放专员；延期期间 `IsPartyReservedForPlayerRequest`/
  `IsTrainerReservedForPlayerOrder` 返回 false，专员重新参加普通任务拍卖，付款或交兵对话也不会
  因玩家偶遇而提前打开。只有成功完成犯罪案件、协力案件、村庄重建、地方请求、村庄救济或 AI
  练兵换防才会由 `GwpPlayerRequestDeferral` 扣减一次；取消、目标失效或被玩家任务抢占不计数。
  完成第二项任务后，保留的原申诉/原调兵订单立即重新获得玩家任务优先级，专员再来找玩家。案件
  总卷会显示还差几项任务，不再显示误导性的“正在前往玩家身边”。
- 付款时锁定的民间支持改为 `clamp(灰袍声望, 0, 100) / 2`，即声望 `80` 对应 `40%`、声望
  `100` 对应 `50%`。删除原来只乘候选功绩的最高 `20%` 加成，新增仅作用于
  `GwpSettlementReconsiderationDecision` 的 `KingdomElection.DetermineOfficialSupport` 后缀：
  在原版各家族投票完成后，为玩家候选补入恰好达到该基础占比所需的民间支持点，并重新计算所有
  候选 `WinChance`；如果原版支持本来已经更高则不下调。随后玩家仍通过原版投票界面自行投入影响力，
  原版国王裁决、关系变化、影响力消费与最终授地逻辑均不替换。
- `Release -t:Rebuild --no-restore` 构建结果为 `0` 错误、`44` 条既有可空性/离线 NuGet 警告。
  反编译实机 DLL 已确认：费用为 `50000`、延期计数为 `2`、声望除以 `2`、自动机会消费位、两类
  延期完成诊断、按封地去重，以及民间支持 Harmony 后缀均已进入部署产物；各普通任务完成路径共
  有九个专员延期通知调用。`18` 个 XML 全部解析成功，中文本地化共有 `833` 个 string id、重复
  `0`。仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致 `0`，实机
  没有 `Assets`、`AssetSources` 或 `RuntimeDataCache`。客户端与编辑器 DLL 均为 `697856` 字节，
  SHA-256 均为
  `37CE069E23D14C7D5A4D44CDCD809D937CCD664952894390662A9FE20C1BB089`；仓库与实机中文
  README SHA-256 均为
  `2AEE0900516D0351BC4D6A74573F4F76CC05FA86ABD83FA228AA74E2AE6A5132`，英文均为
  `DF1E2C39147A679C98461E347B65801F7F22E695395B9C148596358B2C6442B1`。本轮没有启动游戏，
  没有创建或改写正式玩家 ZIP，实际延期和投票显示留给用户重新验证。

## 2026-07-23 旧档申诉仍保存旧民间支持值

- 用户再次读取测试档后看到重新投票中玩家仍只有约一成支持。监控证明民间支持补丁并非按新公式
  得到了较高数值：本轮日志从 `19:36:26` 的 `PLAYER_FIEF_APPEAL_LOBBYING_STARTED` 直接开始，
  没有新的 `PLAYER_FIEF_APPEAL_PAID`；`19:36:48` 开票记录明确为
  `PLAYER_FIEF_APPEAL_DECISION_OPENED ... support=15`，随后 `19:37:08` 由
  `clan_empire_north_2` 获胜。这说明用户读取的是已经在上一版付款、把旧阶梯公式结果 `15` 写入
  存档的请求；此前改成“声望除以二”只在新付款时执行，没有迁移这条活动请求。
- 新增持久化 `GWPP_PlayerRequestSupportFormulaVersion`。旧档在会话启动、所有玩家运行状态已恢复后，
  如果仍有已付款活动申诉，会用当前灰袍声望重新计算基础支持，并同步刷新已经存在于
  `Kingdom.UnresolvedDecisions` 中的本模组封地决议；迁移记录为
  `PLAYER_FIEF_PUBLIC_SUPPORT_MIGRATED`，包含旧值、新值、当前声望和任务阶段。新付款请求直接写入
  当前公式版本，仍保持付款时锁定支持的正常规则。
- 民间支持后缀新增 `PLAYER_FIEF_PUBLIC_SUPPORT_APPLIED` 诊断，逐次记录目标封地、配置支持百分比、
  原版家族票百分比、补足后的最终百分比和新增支持点。下次测试可以直接从后台区分“存档仍为旧值”
  与“Harmony 已补足但界面显示异常”，不再只依靠投票画面反推。
- `Release -t:Rebuild --no-restore` 结果为 `0` 错误、`44` 条既有警告；反编译实机 DLL 确认公式
  版本迁移、活动决议刷新及两条新诊断均已部署。`18` 个 XML 解析失败 `0`；仓库 `_Module` 的
  `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，实机仍没有编辑器目录。客户端与
  编辑器 DLL 均为 `699392` 字节，SHA-256 均为
  `D6FA92155A4376E9F7A9DF79F87F0EB7552B357B057B343103CF26DF2984914B`；中英文 README
  SHA-256 分别为 `66CAD6529981C46387BE0FCC10612B3F98D4834DB72FF959460E82FF037E39E6` 与
  `CA62118FDC683FF9B3D733C9A2C0E1E506357B8BC00C31DC774F4DF0D0E06609`。没有启动游戏或创建
  正式 ZIP，仍由用户读取同一测试档复验。

## 2026-07-23 玩家给自己投票时支持率不增长

- 用户继续实测后纠正了上一节诊断：本次活动请求显示的基础民间支持已经正确，真正的问题是玩家在
  原版投票界面继续投入影响力支持自己时，自己的百分比不再增长；把同样的影响力投给其他候选时，
  对方的百分比可以正常增长。用户同时明确本功能不需要为旧测试档迁移旧公式，因此上一节临时加入的
  `GWPP_PlayerRequestSupportFormulaVersion`、会话启动迁移、活动决议改值入口及
  `PLAYER_FIEF_PUBLIC_SUPPORT_MIGRATED` 已全部撤回。新请求仍在付款时按当时灰袍声望锁定基础支持。
- 反编译当前游戏的 `DecisionItemBaseVM` 和 `KingdomElection` 后确认原版刷新顺序：
  `OnPlayerSupport` 先把玩家 `Supporter` 放入所选结果，界面随后调用
  `DetermineOfficialSupport`，该方法把 `SlightlyFavor/StronglyFavor/FullyPush` 分别换成
  `1/2/3` 个支持点并重算百分比。原版没有禁止玩家支持自己的候选，也没有漏加玩家票。
- 根因在模组原来的“保底比例”算法：每次原版把玩家新投入的支持点加入后，后缀又根据新的原版总票
  重新计算“刚好达到基础比例”所需的民间点数。只要玩家投入后仍未单靠家族票超过基础比例，民间点
  数就会等量缩减，最终百分比始终停在原值；投给其他人时则不会从玩家候选中抵消，所以表现为只有
  给自己投票无效。
- 当前实现先从各候选的 `SupporterList` 中单独识别并扣出玩家本次投入的 `1/2/3` 个点，以不含
  玩家手动票的原版基线一次性计算固定民间支持点，再把原版已经加入的玩家票叠加回来。这样未投票时
  仍保持“灰袍声望的一半”为基础支持；给自己投入影响力会在此基础上继续上升，给其他候选也会照常
  改变比例。极端的原版零票局使用总量 `10` 的固定合成基线，仍保留玩家投入的可见增量。诊断
  `PLAYER_FIEF_PUBLIC_SUPPORT_APPLIED` 新增 `playerVotePoints`，可直接比较连续三档投入后的
  `finalPercent`。
- 中英文 `v1.4-r7（开发中）` 玩家日志已删除旧档迁移承诺，并改为说明民间支持是固定基础、玩家
  给自己追加影响力会继续提高得票。`Release -t:Rebuild --no-restore` 构建成功，`0` 错误、
  `44` 条既有可空性/离线 NuGet 警告；实机 DLL 反编译确认部署产物包含扣除玩家手动票后计算基线、
  零票总量 `10`、`playerVotePoints` 诊断，且不再包含旧档公式迁移符号。
- `18` 个正常客户端 XML 全部解析成功。仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失
  `0`、SHA-256 不一致 `0`，实机没有 `Assets`、`AssetSources` 或 `RuntimeDataCache`。客户端
  与编辑器 DLL 均为 `699904` 字节，SHA-256 均为
  `785E8095C119BD3BCF3C95FF5E02792CB7582A1C4EA56DB0C70B4E96C592ABA3`；仓库与实机中文
  README SHA-256 均为
  `96ABF4AD54942C3D1A12A44CEFF22595CD58B71998EEE345475E98A53D7E6196`，英文均为
  `7ABAAD64AD7ADBB20C5B3540074D44FFC652DD1CFD5C9562580BB0C05768DAA6`。没有启动游戏，
  没有创建或改写正式玩家 ZIP，投票界面的实际变化仍由用户本人验证。

## 2026-07-23 v1.4-r7 正式发行、玩家文案收束与无监控玩家包

- 用户完成本轮实机复验并确认已无法继续发现问题，同意将当前开发内容正式发布为
  `v1.4-r7`。中英文玩家 README 的本版条目已改为正式版本，并只保留 `r7` 与 `r6` 两个最近
  版本；`r7` 内容压缩为玩家实际能看到和使用的新玩法、调整与修复，不再解释任务欲望、延期计数、
  Harmony 补丁、支持点换算过程或监控实现。
- 封地申诉与调兵交付的搁置对话、案件总卷阶段和再次来访提示已删除“完成两个任务/两个回合后再来”
  一类机制说明。玩家现在只会得知请求已保留或交付已延期，并在合适时机继续；内部仍以完成两项
  普通任务作为延期节奏，该事实只保留在开发维护记录中。资金不足的封地申诉仍可由玩家选择搁置
  或撤回，不会扣款。
- 当前完整源码分别针对 Bannerlord `1.4.5.115026`、`1.4.6` 和本机 `1.4.7` 接口完成交叉
  重建，三者均为 `0` 错误；警告分别为 `43`、`43`、`44` 条既有可空性/离线 NuGet 警告。
  这证明当前新增代码没有引入 1.4.5/1.4.6 缺失的编译期 API，但不能代替旧版本上的 Harmony
  装载、存档读取与界面流程实机冒烟验证。
- 正常 `Release -t:Rebuild --no-restore` 构建已部署到本地测试模组。仓库 `_Module` 的
  `25` 个正常客户端文件与
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`
  相比缺失 `0`、SHA-256 不一致 `0`；`18` 个 XML 解析失败 `0`，中文本地化共有 `833` 个
  string id、重复 `0`，实机不存在 `Assets`、`AssetSources` 或 `RuntimeDataCache`。客户端
  与编辑器测试 DLL 均为 `699392` 字节，SHA-256 均为
  `0C7FB36B7AB106AD106F0BA85B236CEDCD5625C7A4172DD3D2B4D3D2C5050B51`。仓库与实机中文
  README SHA-256 均为
  `5C44489ACBB3DA45656C1A4D01E93E978F20ABC38BBD27AA3CE60ACF2C24CF4E`，英文均为
  `9B5F69467CD9E0A41064B92487A18E028A541F9B729AE8EDA5AB4D114907E368`。
- 正式玩家 DLL 使用 `GwpDiagnosticsEnabled=false` 与 `DeployToLiveModule=false` 单独输出到
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\release-player-v1.4-r7`，
  文件为 `680448` 字节，SHA-256 为
  `C3F1A7AFAA8E0F1D724AAEAAEA6D48FD8BED4986E21A1C733E19B527EC9DD63F`。ILSpy 反编译
  确认 `GwpAiDiagnostics.LogPath` 为空、所有写入/捕获方法为空、两个追踪判断恒为 `false`，
  DLL 字符串中也不存在 `AppendAllText`、`StreamWriter`、`FileStream`、监控日志名或文档目录；
  本地测试 DLL 哈希保持不同，未被无监控玩家 DLL 覆盖。
- 干净暂存目录位于
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\package-v1.4-r7-final-20260723`。
  正式包含 `28` 个文件且只有一个顶层 `GreyWarden/`：仓库的正常客户端内容、客户端
  `0Harmony.dll`、上述无监控 DLL 与已编译 shader cache。包内没有 `Assets`、`AssetSources`、
  `RuntimeDataCache`、编辑器 DLL、PDB、脚本、工具、日志、监控输出、开发文档或嵌套压缩包。
  解压到独立核验目录后的 `28` 个文件与暂存目录逐文件比较为缺失 `0`、多余 `0`、哈希不一致
  `0`；包内 DLL 与无监控输出哈希一致，包内双语 README 与仓库一致，且再次反编译确认监控实现
  为空。
- 本地正式文件为
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4-r7.zip`
  和同名 `.zip.sha256`。ZIP 为 `349824265` 字节，SHA-256 为
  `DC5268FBD036C5BC55245234B6C7371E0393D6F5D6D5540949DA0F2FC82AB5BC`，校验文件内容与
  实算一致。完成全部审计后已删除本地旧 `v1.4-r6` ZIP/校验文件，游戏 `Modules` 父目录只保留
  最新 `v1.4-r7` 正式包对。GitHub 正式发布地址为
  `https://github.com/Lucicain/GW/releases/tag/v1.4-r7`。
- 正式代码提交为 `589d350fd1dd4c8ac0015ca7d5da51e542b69ace`，`main` 与带注释标签
  `v1.4-r7` 均已推送，标签准确指向该提交。GitHub Release 已成功发布且为当前 latest，状态不是
  draft 或 prerelease；远端 ZIP 资产为 `349824265` 字节，GitHub 报告的 SHA-256 digest 与
  本地 `DC5268...AB5BC` 一致，远端校验文件为 `88` 字节且 SHA-256 为
  `F2FE4C52FBDFC60508759C8B52A3951566D92D8DB50FAFE602A8A437C74488E0`，与本地文件一致。

## 2026-07-23 v1.4-r7 紧急重发前修正：任务期间巡逻无条件降级与密泽亚巡逻根因

- 本节固化灰袍任务欲望的不可破坏边界：**只要一支受管理部队存在任何有效任务意图，所有
  `PatrolAroundPoint` 候选都必须压到原版最低执行阈值 `0.03`。**这条规则不因普通案件、协力
  组长、协力支援者、练兵换防、地方任务或玩家委托而例外。应完整保留的是补给、疗伤、招兵、
  访问等所有**非巡逻**原版长期欲望及其原始分数；其中任一分数高于任务分时仍可先执行。无任务
  时完全不修改原版巡逻。今后“保留原版欲望”不得再次解释为“任务期间也保留高分巡逻”。
- 用户实机监控确认协力组长明明持有任务却继续巡逻。当前会话中共有 `71` 次协力组长竞价出现
  相同状态；清珑和梵蒂的协力追捕任务分均为 `0.99`，原版巡逻最高分别约为 `3.09` 和
  `3.05`，日志同时显示 `suppressedPatrolCount=0`，最终持续选择巡逻。任务、目标和协力组均
  正常存在，因此排除“任务未生成”或旧档目标丢失。
- 根因是 `ResolveIntent` 为所有协力职责设置了 `PreserveAllNativeDesires=true`，而最终欲望
  处理把这个标记直接用作跳过巡逻压制的条件。该实现把“长期欲望永远开启”错误扩大成了“连
  巡逻也永远保持原分数”。现已删除这个标记和豁免分支：只要 `intent != null` 就统一调用
  `SuppressAssignedPatrolScores`；非巡逻候选没有任何改分或删除，协力任务仍通过原版欲望拍卖，
  不冻结 AI，也不关闭长期欲望。
- 为解释灰袍无任务时几乎总选择密泽亚，已直接反编译 Bannerlord 1.4.7 的
  `AiPatrollingBehavior` 与 `DefaultTargetScoreCalculatingModel`。灰袍家族没有封地，因此
  原版走“无 faction settlement”备用分支：先在部队周边枚举城镇，再按
  `平均相邻城镇距离 × 5 / max(平均距离, HomeSettlement 到目标城镇距离)` 调整每座城的巡逻
  分。无家族封地时，原版对所有候选的归属偏好又是同一个中性值，最后主要差异就是它们离
  `HomeSettlement` 的距离。
- `spclans.xml` 把灰袍家族的 `initial_home_settlement` 设为 `village_EN5_1`，实机监控也显示
  所有这些领主的 `leaderHomeSettlement=village_EN5_1`。原版中文本地化确认 `town_EN5` 就是
  密泽亚；它离这个共同家园最近，所以稳定得到最高分。监控中的同一轮原始分数为
  `town_EN5=3.0900`、`town_EN4=2.9035`，随后依距离继续下降。原版实际上也生成了许多其他
  城镇候选，并非硬编码只允许密泽亚，只是共同家园和稳定的距离公式使密泽亚几乎总是获胜。
  当前不永久关闭无任务巡逻，也不改写原版巡逻目标选择；任务繁多时它自然很少有机会生效，只有
  真正无任务时才保留为兜底活动。
- 用户将此问题定性为正式 `v1.4-r7` 刚发布即发现的内部缺陷，并要求撤下首个包后以同一个
  `v1.4-r7` 修正版重发，不建立 `r8`，也不在玩家日志中加入玩家尚未来得及遇到的内部缺陷。
  因此中英文玩家 README 恢复为正式 `r7` 与 `r6` 两个条目，`r7` 玩家变更列表保持不变；
  本节完整技术记录只保留在开发维护文档。`Release -t:Rebuild --no-restore` 构建成功，`0` 错误、
  `44` 条既有可空性/离线 NuGet 警告；自动部署后仓库 `_Module` 的 `25` 个正常客户端文件与
  实机相比缺失 `0`、SHA-256 不一致 `0`，`18` 个 XML 解析失败 `0`。客户端和编辑器 DLL 均为
  `699392` 字节，SHA-256 均为
  `BB674EDE0DA1A2190BCBF5FAABE986A68E892D3A177DE6808B79D317FEDAD555`；反编译实机 DLL
  确认 `PreserveAllNativeDesires` 已不存在，协力职责仍正常解析且所有有效 intent 进入统一巡逻
  压制路径。仓库与实机中文 README SHA-256 均为
  `5C44489ACBB3DA45656C1A4D01E93E978F20ABC38BBD27AA3CE60ACF2C24CF4E`，英文均为
  `9B5F69467CD9E0A41064B92487A18E028A541F9B729AE8EDA5AB4D114907E368`。没有启动游戏。
- 修正版无监控玩家 DLL 单独输出到
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\release-player-v1.4-r7-hotfix-20260723`，
  文件为 `680448` 字节，SHA-256 为
  `C8F8754A4906ED1452815B0F93A38C5723D0F443036409A61BBB12AB223BC08E`。反编译确认监控
  `LogPath` 为空、追踪判断恒为 false、没有文件写入 API，同时确认巡逻豁免符号不存在且统一压制
  方法已经进入玩家 DLL；本地测试 DLL 仍为不同哈希，没有被覆盖。
- 修正版干净暂存目录为
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\package-v1.4-r7-hotfix-final-20260723`。
  包内仍为单一顶层 `GreyWarden/` 和 `28` 个运行文件，禁入项命中 `0`。独立解压后的文件与暂存
  目录相比缺失 `0`、多余 `0`、哈希不一致 `0`；包内 DLL 与上述无监控 DLL 哈希一致，包内
  README 与仓库一致，反编译再次确认监控为空且巡逻修正存在。修正版已替换本地同名正式文件
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4-r7.zip`
  及 `.zip.sha256`；ZIP 为 `349824267` 字节，SHA-256 为
  `963AA367A2512126E5DBB92D04792A0075C3C1DE20DD7D40B61B6E2468ECE15A`，校验内容与实算
  一致，`Modules` 父目录仍只保留这一组正式包。
- 修正版代码提交为 `4e87bcca50fec8fcd2584c107b261aa9a3ae62cf`。旧 GitHub
  `v1.4-r7` Release 及其远端标签已先完整删除，带注释的同名标签随后重建并准确指向该修正版
  提交；`main`、本地标签和远端标签解析结果一致。新的同名 Release 已发布为 latest，非 draft、
  非 prerelease；远端 ZIP 资产为 `349824267` 字节且 GitHub digest 为
  `sha256:963aa367a2512126e5dbb92d04792a0075c3c1de20dd7d40b61b6e2468ece15a`，与本地一致。
  远端校验文件为 `88` 字节，SHA-256 为
  `8A5F963D1DB3AF6D45B53BF7D31EDB8A9157765643100A978A04AC9747438CB6`，同样与本地一致。

## 2026-07-23 v1.4-r8 开发：协力军团可重复组建与纯速度生命周期

- 用户重新定义协力军团的生命周期边界：一个案件不能一生只拉一次军团；因目标过快而分散后，
  同一案件必须仍可再次组建。协力任务仍有效期间，军团不能再因原版无战争、缺粮、停滞、凝聚力、
  AI 取消或未知原因自行解散；主动分散只取决于当前目标速度是否高于已组建军团速度。案件完成、
  任务被替换、领主失效或玩家最高优先任务接管时的清理属于任务生命周期结束，不是追捕中的速度
  分散。
- 现状中的“一次性”来自 `LordAssistanceGroup.DispersedForSpeed`：一旦速度分散便永久保持 true，
  后续每小时只让各领主分头追捕，直到案件结束，从未存在恢复为 false 的路径。现新增持久化的
  `LastArmySpeedAtDispersal`；实际分散时记录当时真实军团速度。目标后来降到不高于这个可追赶
  阈值时，原协力组会清除分散状态，使用同一批成员重新创建真实 `Army`、集合并再次检查实际
  军团速度。若重新集合后仍追不上，会再次分散并记录新的真实速度，因此同一案件可以在
  “组军—分散—再组军”之间反复切换，而不是永久锁死在第一次结果。
- 用户怀疑非宣战阶段的快目标无法触发分散，该判断由源码证实：旧速度检查复用了
  `GetValidAssistanceOffender`，而该入口要求 `WarDeclared=true`、流程为 `WarPursuit` 且双方
  当前实际交战；只要其中一项不满足就返回 null，随后旧代码直接保留军团并跳过速度比较。现在将
  “案件仍有效且目标部队活跃”的 `activeTarget` 与“允许战争追捕”的 `groupOffender` 分开：
  速度比较始终读取 activeTarget，不再依赖宣战状态；非战争阶段分散后的领主使用 Approach，
  战争追捕阶段才使用 Pursue，避免在和平阶段错误下达敌对追击欲望。
- 原版 `DisbandArmyAction` 共有无战争、缺粮、停滞、凝聚力耗尽、成员不足、未知原因等多条入口；
  旧实现只拦截 `ApplyByNoActiveWar`，因此“其他原因也会解散、之后再重建”的行为与新边界冲突。
  现改为窄范围 Harmony 前缀拦截私有统一入口 `ApplyInternal`：只有
  `IsActiveAssistanceArmy` 为 true 时才阻止全部原版自动解散，并记录
  `ASSISTANCE_ARMY_NATIVE_DISBAND_BLOCKED` 及原版原因；普通王国军团完全不受影响。模组自己的
  速度分散和任务结束清理通过线程局部授权作用域调用原版 `ApplyByObjectiveFinished`，不会被
  该守卫拦截，也不会把授权泄漏给其他军团。
- 旧存档没有新的速度阈值时使用当前组内灰袍领主的最慢独立速度作为一次保守估计；重建真实军团
  后立即回到实际军团速度判断。新增存档键
  `gwp_enf_assist_speed_thresholds` 与原协力组顺序同步，不改变既有组、成员、案件和分散状态
  的载入方式。
- 中英文玩家 README 已开启 `v1.4-r8（开发中）`，只以一条玩家结果说明协力军团可按速度分散并
  在同一任务中再次集结，不公开上述状态机和原版拦截细节；README 只保留开发中 `r8` 与正式
  `r7`。`Release -t:Rebuild --no-restore` 构建成功，`0` 错误、`44` 条既有可空性/离线 NuGet
  警告。反编译实机 DLL 确认速度阈值持久化、重新组军日志、非战争 activeTarget、Approach
  回退、`ApplyInternal` 守卫、原版解散拦截诊断和授权解散入口均已进入部署产物。
- 自动部署后仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、SHA-256 不一致
  `0`，`18` 个 XML 解析失败 `0`。客户端与编辑器 DLL 均为 `700928` 字节，SHA-256 均为
  `CD1791F8E256A10D42BEB3047FF83DB337A466865048A275579EE150FFED34DC`；仓库与实机中文
  README SHA-256 均为
  `79BD33185B04DA89BEF68387177D032FC3AE97FA73FD1F8162F1DC8B4740D0EE`，英文均为
  `DC8E25059DC7D8336EE58E0AC4687858BF17E5CDCB84164D0EA9F38DEB52BDA6`。没有启动游戏，
  没有创建或改写正式 `v1.4-r7` ZIP。
- 同一份完整源码继续使用既有兼容审计工程分别引用 Bannerlord `1.4.5` 与 `1.4.6` 程序集交叉
  构建；两次均为 `0` 错误、`43` 条既有警告，确认本轮新增的私有解散入口守卫、速度阈值存档及
  重组逻辑没有引入这两个目标版本的编译断裂。

### 2026-07-23 实机监控：附属目标速度为零阻止重新组军

- 最新监控中的协力案件由 `CharacterObject_2875_party_1` 主办，已有 `5` 名协办人；已承诺战力
  `1980.46`，目标实际军团战力 `950.12`，因此不是战力不足或增员失败。组内保持
  `speedDispersed=true` 且无 `Army`，说明流程停在速度分散后的重组门槛。
- 案件目标 `CharacterObject_1584_party_1` 已附着于敌方军团长 `lord_1_41_party_1`。附属目标
  自身 `LastCalculatedBaseSpeed=0.00`，实际移动的军团长为 `2.73`。旧重组判断直接读取附属目标
  并把速度小于等于 `0.01` 当成“不可重组”，因此永久返回 false；战力已足够也无法重新建立军团。
- 速度生命周期现与战力目标使用相同的实际主体解析顺序：攻城队长、军团长、附着对象、案件目标。
  分散和重组均比较该实际移动主体；速度为零表示目标没有比上次军团更快，允许重新组军，不再作为
  禁止条件。速度诊断同时记录 `speedTarget`、案件目标自身速度、实际移动主体速度及上次军团速度，
  便于区分附属队伍与真实移动对象。玩家 README 现有“按速度分散并可在同案重组”说明已经准确，
  本次只补充“目标加入其他军团后仍可重组”的玩家结果，不公开内部状态机。
- 修复后的 1.4.7 `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条既有警告；1.4.5 和
  1.4.6 完整源码交叉构建均为 `0` 错误、`43` 条既有警告。ILSpy 对实机 DLL 确认
  `ResolveAssistanceMovementTarget`、速度分散/重组调用及新增速度对象诊断均已进入产物。
  实机客户端与编辑器 DLL 均为 `703488` 字节，SHA-256 均为
  `7184FD829C4248AA378881D075321FCA95DF390D59411CF5E16260BCDB779F99`。仓库 `_Module`
  的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`；`18` 个 XML 解析失败 `0`。
  仓库与实机中文 README SHA-256 均为
  `0D030E9792C26256CA85086A74A864A185127B2ED4876131E7862D763E9A71F4`，英文均为
  `08C333442D8C058226377C5ACD4F21ECAD112C1C85CB46C7BA81845D52B49E15`。
  没有启动游戏，没有创建或改写正式 `v1.4-r7` ZIP。

### 2026-07-23 协力速度分散改为逐名脱离

- 用户进一步纠正速度生命周期：军团慢于目标时不能一次解散整支军团，而应每次只让一名协力领主
  脱离，只要已有一名独立领主或剩余军团速度严格高于目标，就停止继续拆分。第一版曾在同一次
  小时更新中通过 `SpeedExplained` 立即刷新刚脱离领主并继续循环拆人；后续实机监控证明这个
  即时数值仍带有军团附着状态，不能作为独立速度使用，该方案已在 2026-07-24 撤销。
- `LordAssistanceGroup` 新增持久化的 `SpeedDetachedPartyIds`，存档键为
  `gwp_enf_assist_speed_detached`。未脱离成员继续使用原版 `EscortParty` 追随主办人并保留在
  真实 `Army`；已脱离成员改为独立追踪案件目标。部分分散期间原版无战争、粮食、凝聚力等自动
  解散仍被守卫拦截，不能把剩余军团误拆掉。目标速度回落到完整军团可追赶范围时，清空逐名脱离
  记录并让全部成员重新集结，因此同一案件仍可反复拆分和重组。
- 每次速度更新最多只能实际脱离一人，必须等该领主在后续更新中确认 `Army == null` 且
  `AttachedTo == null` 后，才读取稳定的独立速度并决定是否需要下一名。新诊断
  `ASSISTANCE_ARMY_SPEED_SPLIT_STARTED`、`ASSISTANCE_SPEED_MEMBER_DETACHED` 和
  `ASSISTANCE_SPEED_CATCHER_READY` 分别记录开始拆分、实际脱离者、当前追得上的领主，以及案件
  目标/实际移动目标速度、剩余军团速度和脱离人数。总览诊断增加 `speedDetached` 与
  `speedCatcher`，可以直接确认系统是否已停止继续拆分。
- 旧版速度分散存档没有逐名列表且当时已把全组实际解散。载入这种状态时将主办人与全部既有协办
  人迁移为已脱离者，保持地图现状，不会为了填新字段突然强制重组；之后仍按目标速度正常判断是否
  重新集结。玩家 README 只说明“逐名派出直到有人能追上，并保留其余军团”的玩法结果，不公开
  存档字段与逐小时状态机。
- 最终 1.4.7 `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条既有警告；1.4.5 和
  1.4.6 完整源码交叉构建均为 `0` 错误、`43` 条既有警告。ILSpy 对实机 DLL 确认新存档键、
  三条逐名速度诊断及 `AdvanceAssistanceSpeedDispersal` 已进入产物，旧整军速度解散诊断
  `ASSISTANCE_ARMY_SPEED_DISPERSED` 命中为 `0`。实机客户端与编辑器 DLL 均为 `709120`
  字节，SHA-256 均为
  `54A5E6C5243FE4D206FE2CB2192819DA026908148550156DDDFF09AF06806C86`。仓库 `_Module`
  的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML 解析失败 `0`；
  中英文 README SHA-256 分别为
  `CD50209D3B9D3D9661BD48EE011417F9C966497F0CB8325251F76F61B4DE05F2` 与
  `B99A58BC0EFAEF77B875C3FB402D3AEB74179AC96E4588C87941968FAC8D5E68`，仓库与实机一致。
  `git diff --check` 通过。没有启动游戏，没有创建或改写正式 `v1.4-r7` ZIP。

## 2026-07-23 v1.4-r8 开发：协力战力组军与速度解散的最终边界

- 用户最终明确协力只有两类相互独立的判定。战力只负责决定是否需要协力、需要加入多少领主，
  以及全部合格领主仍不足时是否判定案件失败；速度只负责已经形成的军团是否解散。协力总战力
  高于目标后不得因战力占优解散，必须保持军团继续追捕。只有目标速度严格高于当前军团速度时
  才进行追捕中的主动解散；目标后来不再更快时，同一案件重新建立军团，因此一个案件可以反复
  经历组军、速度解散和再次组军。
- 旧触发依赖主办领主在近距离连续进入原版逃跑短期行为，后续增员也依赖再次逃跑。现已完全删除
  `IsCasePursuitBlocked`、独立/军团受阻小时计数及相关距离、连续小时调参。普通 AI 案件在
  `AssignTasks` 建立后同一小时即进入协力评估；玩家案件保留既有对话边界，只有玩家拒捕并进入
  战争后才允许武装协力。
- 双方当前实力直接读取原版生成的 `PartyBase.EstimatedStrength` 与
  `Army.EstimatedStrength`。目标属于原版军团时比较其实际军团战力；目标属于攻城营地时使用
  原版 `GetInvolvedPartiesForEventType()` 中各参战部队的 `EstimatedStrength` 合计。模组没有
  自制兵种等级、人数或装备权重公式。主办人不足时按距离依次加入当前合格灰袍领主，直到已承诺
  的原版战力严格高于目标；目标战力在任务中增长时继续补人。
- 若主办人、既有协办人和当时所有合格候选的原版战力总和仍不高于目标，输出
  `ASSISTANCE_CASE_FAILED_STRENGTH`，释放既有协办人、结束主办任务，并通过现有
  `CrimeState.EndTask` 将普通案件从任务池和案件账本移除，不再让无法完成的案件永久占用调度。
- 军团因目标更快而解散后，既有成员仍各自继续接近或追捕目标。分散期间若目标战力继续增长，
  系统仍可追加合格协办人；这些人先作为同案独立追捕者加入记录。目标速度回落到不高于上次真实
  军团速度时，全部记录成员重新进入同一个原版无王国 `Army`，到齐后重新读取真实军团速度；
  若仍追不上便再次按速度解散。
- 开发中曾短暂实现“协力战力高于目标后立即解散军团”，并让主办人在集合期间获得原版
  `Hold` 欲望。用户指出两者均不符合需求：战力不应控制解散，主办人也不能原地等待。两项代码
  均在部署前撤销；最终主办人始终沿用案件的 Approach/GoAroundParty 继续找目标，协办人使用
  原版 EscortParty 追赶并加入。
- 中英文玩家 README 的 `v1.4-r8` 条目只说明按战力召集、仅因速度分散、同案可重组及总战力
  仍不足时案件失败，不公开战力来源、状态字段和失败实现。仓库内独立输出构建成功，`0` 错误、
  `44` 条既有可空性/离线 NuGet 警告；正式开发构建同样为 `0` 错误、`44` 条既有警告并自动
  部署。1.4.5 与 1.4.6 完整源码交叉构建均为 `0` 错误、`43` 条既有警告。反编译实机 DLL
  确认原版战力读取、战力失败、速度分散和速度重组均进入产物，且不存在旧逃跑阻塞函数、战力
  解散日志或集合等待欲望。仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希
  不一致 `0`，`18` 个 XML 解析失败 `0`。客户端与编辑器 DLL 均为 `703488` 字节，SHA-256
  均为 `CDE585E19701B908EF2966E295D1905D40413980044B4D3D1EB58FA0DDE613F1`；中英文
  README SHA-256 分别为
  `43DE0B48E0235C64BE2420CC1BB5EE8F56F49E9FBC79758FAEBC927299D72049` 与
  `FC8F7E84315826378EC7DB48F930D78372449B1C2174E92DB90D082114943E13`。没有启动游戏；
  正式 `v1.4-r7` ZIP 未改写，SHA-256 仍为
  `963AA367A2512126E5DBB92D04792A0075C3C1DE20DD7D40B61B6E2468ECE15A`。


## 2026-07-24 v1.4-r8 开发：保留军团主体与核算周边参战力量

- 最新实机监控
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`
  （最后写入 `2026-07-23 23:53:21 +10:00`）确认“逐名脱离”第一版仍会把整组拆光。静澜案件
  `CharacterObject_2875_party_1 -> CharacterObject_1584_party_1` 中，实际移动目标是敌军军团长
  `lord_1_41_party_1`，完整协力军团速度约 `2.28`、目标军团约 `2.73`。在同一个
  `campaignHour=633870.80` 内，旧循环连续拆出五名协办人，并分别读到 `2.60`、`2.48`、
  `2.06`、`2.03`、`1.61` 的即时速度；后续稳定监控却显示第一名梵蒂
  `gw_leader_0_party_1` 已有约 `3.85`，单独一人便足以追上。根因不是没人更快，而是
  `Army = null` 后立即读取的 `SpeedExplained`/`LastCalculatedBaseSpeed` 仍处于军团附着
  过渡状态，同一小时的 `while` 循环因此把过渡值误判为独立速度。
- `AdvanceAssistanceSpeedDispersal` 已删除同小时循环与主动刷新 `SpeedExplained` 的做法。
  现在一次协力小时更新最多拆出一名领主；刚拆出的领主必须在后续更新中同时满足
  `Army == null`、`AttachedTo == null`，才允许用其稳定的原版基础速度判断能否追上目标。
  只要找到一名独立协办领主严格快于实际移动目标，就保留且仅保留这名领主独立追击，其他曾被
  多拆出的协办人会重新分配给主办人的真实 `Army`。这不仅修复新拆分，也会主动收拢旧存档中
  已被上一开发版全部拆散的组；新增 `ASSISTANCE_SPEED_SPLIT_CONSOLIDATED` 记录追击者、收拢
  人数和恢复后的军团，`ASSISTANCE_SPEED_CATCHER_READY` 与脱离日志同时记录 `Army` 和
  `AttachedTo`，便于确认使用的是稳定独立状态。
- 协力敌方战力不再只取案件目标本人或其直属军团。新的
  `GetNativeCombatStrengthSnapshot` 仍只使用原版 `PartyBase.EstimatedStrength` /
  `Army.EstimatedStrength`，但会先汇总目标当前 `MapEvent` 同侧全部参战部队，再以原版
  `EncounterModel.GetEncounterJoiningRadius` 为半径搜索周围部队。已有战斗时调用原版
  `MapEvent.CanPartyJoinBattle` 判断能否加入目标一侧；尚未开战时计入目标同势力，以及已与
  灰袍交战且不会攻击目标势力的附近部队。军团成员统一折叠到军团长并按 ID 去重，避免一个军团
  被重复计算。诊断现在输出 `targetJoiningRadius` 和带逐组原版战力的
  `targetCombatGroups`，可直接检查目标附近的第二支军团或其他参战力量是否已进入协力门槛。
- 最终 1.4.7 `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条既有警告；1.4.5 与
  1.4.6 完整源码交叉构建均为 `0` 错误、`43` 条既有警告。ILSpy 对实机 DLL 确认
  `GetNativeCombatStrengthSnapshot`、`StartFindingLocatablesAroundPosition`、
  `CanPartyJoinBattle`、稳定独立追击者筛选及
  `ASSISTANCE_SPEED_SPLIT_CONSOLIDATED` 已进入产物；协力类型内 `SpeedExplained`、
  `while (true)` 和旧整军解散诊断 `ASSISTANCE_ARMY_SPEED_DISPERSED` 均命中 `0`。
  实机客户端与编辑器 DLL 均为 `712704` 字节，SHA-256 均为
  `323FCDA9CBE91704D61BF2FF981AC0C9666EAE4FCF2E57709F543DCD2958E87F`。
- 仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，
  `18` 个 XML 解析失败 `0`；中英文 README SHA-256 分别为
  `049643DB6ED9E70DB0DCFEA624011F5E932173707F8C1EEE9C07D5FCF7F7D48F` 与
  `5034EA822FD9F3C2C1763E4300A2C2933206B1011A56ECE74BE3C9256C07591F`，仓库与实机
  一致。`git diff --check` 通过。没有启动游戏，等待用户实机验证；没有创建或改写正式
  `v1.4-r7` ZIP，其 SHA-256 仍为
  `963AA367A2512126E5DBB92D04792A0075C3C1DE20DD7D40B61B6E2468ECE15A`。

## 2026-07-24 v1.4-r8 开发：协力军团持续移动与实际接战

- 最新实机监控
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`
  （最后写入 `2026-07-24 00:30:52 +10:00`）证明梵蒂案件并非战力不足。主办队
  `gw_leader_0_party_1` 已有三名协办人全部附着，真实军团战力约 `1403.01`，目标攻城军团
  `CharacterObject_2765_party_1` 为 `1227.07`；但案件仍为 `Pursuit`、`war=False`，
  主办人的长期行为是 `GoAroundParty`，在距目标 `5.95` 处停止，而非玩家自动宣战距离只有
  `3`。暮光案件也出现相同结构：当前已到位军团约 `1597.72`，目标约 `1138.20`，但因尚有
  一名远途协办人未附着而停在约 `15.13`。因此“实力已经超过仍不敢打”的直接原因是和平阶段
  环绕行为永远进不了宣战距离，不是原版战力比较拒绝进攻。
- 宣战前的协力主办人和速度脱离追击者现使用原版 `EscortParty` 跟随
  `ResolveAssistanceMovementTarget` 解析出的实际移动主体；普通协办人继续护送主办人。
  军团不会为了等待远处成员停住，也不再扩大 `GoAroundParty` 防守半径。任一登记在同案中的
  灰袍领主进入接触距离，都可代表协力组触发宣战；宣战后主办人及全部协办人立即请求重新决策，
  再切回敌对 `GoAroundParty` 与原版短期接战/逃跑判断。
- 为避免移动中的主办人早于援军抵达便宣战，未分散军团的首次接战只使用当前真实
  `Army.EstimatedStrength`，不把仍在路上的已承诺协办人算作到位战力。当前军团严格高于目标
  与其周围威胁后才允许触发宣战；速度分散组仍以整案已承诺力量为同一接触，允许最快追击者先
  接触目标。总览诊断新增 `engagementStrength`、`movementTarget`、`contactParty` 和
  `contactDistance`；宣战时新增 `ASSISTANCE_CONTACT_DECLARING_WAR`，可直接确认由谁接触、
  当时双方战力及实际移动目标。
- 原周边敌军扫描只使用原版实际战斗加入半径 `3`，但原版
  `DefaultMobilePartyAIModel.GetBestInitiativeBehavior` 在决定攻击或逃跑时，会把该半径两倍
  范围内、能够支援目标的同势力部队纳入威胁计算。这会漏掉玩家看到的第二支贴近军团，造成模组
  认为已经占优、原版短期 AI 却仍选择回避。现保留原版 `GetEncounterJoiningRadius` 作为实际
  接触值，同时按原版主动性判断范围计算 `ThreatRadius`，仍只汇总
  `PartyBase.EstimatedStrength` / `Army.EstimatedStrength` 并按军团去重；攻城营地和已有
  `MapEvent` 继续使用原版参战集合。诊断新增 `targetThreatRadius`，每个计入的敌方战斗组仍写入
  `targetCombatGroups`。
- 删除了两处 `CanAssistanceGroupEverOverpower` 理论预判。现在无论理论总和看起来是否足够，
  `EnsureCommittedStrengthAdvantage` 都会按距离逐名实际调用 `TryAddAssistanceMember`，直到
  已承诺力量严格高于动态敌方威胁；只有下一次实际招募返回“没有合格领主”且仍不足时，才调用
  `FailAssistanceCase` 结束案件并移出任务池。这与“不断扩大，耗尽可招人员后才失败”的边界
  一致，也避免候选状态在理论求和与实际接案之间变化造成提前判败。
- 最终 1.4.7 `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条既有警告；1.4.5 与
  1.4.6 完整源码交叉构建均为 `0` 错误、`43` 条既有警告。ILSpy 对实机 DLL 确认
  `ThreatRadius`、`HasAssistanceEngagementStrengthAdvantage`、
  `GetAssistanceContactDistance`、和平阶段 `RequestEscort` 及
  `ASSISTANCE_CONTACT_DECLARING_WAR` 均已进入产物，旧
  `GwpAssistanceGoAroundRadiusPatch` 已不存在，原版自动解散守卫仍保留。
- 仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个
  XML 解析失败 `0`。实机客户端与编辑器 DLL 均为 `713216` 字节，SHA-256 均为
  `32A1AA846F861AD8A9F1C68DF611F661681199A2AC50A273061B943E9BC3AF75`；仓库与实机中文
  README SHA-256 均为
  `4C581E8B2A5D141765BB7783135C214E1D0019D6EB5869682B8C879AE8438329`，英文均为
  `B3A582EBE0E3DF01021FB6F2A9D6E5D027B2AD4DB4FBF7FB89FB76E6AAE14304`。
  `git diff --check` 通过。没有启动游戏，等待用户实机验证；没有创建或改写正式
  `v1.4-r7` ZIP。

## 2026-07-24 v1.4-r8 开发：协力最终简化为区域战力与整组速度两项判定

- 用户否定了上一轮“逐名脱离、等待单人速度稳定、按实际到场战力宣战”的复杂状态机，并重新
  确认唯一边界：敌方区域总战力只决定协力军团规模，军团规模只增不减；速度只决定保持军团或
  全组分散。主办人、既有协办人和当前所有可投入领主的总战力仍不高于敌方区域总战力时，案件
  必须立即失败、退出任务池并恢复和平。正在执行玩家委托、练兵、村庄救济、重建或地方请求等
  强制职责的领主不属于“当前可投入力量”，不得先撤销其职责再计入理论上限。
- 对本机当前原版
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll`
  反编译核对了 `DefaultMobilePartyAIModel.GetBestInitiativeBehavior` 与
  `MobilePartyAi.GetGoAroundPartyBehavior`。原版以目标本队/真实军团或围城参战集合为基础，
  再使用 `EncounterModel.GetEncounterJoiningRadius` 的内圈和最多两倍半径的外圈评估目标
  周围支援；外圈力量按距离递减，不是全部满额相加。原版 `GoAroundParty` 使用
  `joiningRadius * 1.15` 的防守半径，并优先尝试
  `defendRadius² * 0.5` 的最外层合法点；默认接触半径为 `3` 时，该最外圈约为 `5.95`。
- `GetNativeCombatStrengthSnapshot(observer, offender)` 现在采用上述原版口径：目标已经进入
  `MapEvent` 时汇总目标侧真实参与者并以 `MapEvent.CanPartyJoinBattle` 核验新增支援；尚未
  开战时只统计目标同势力、具有攻击性的真实移动战斗组，排除附着成员、民兵、驻军、其他战斗
  和被围城内的部队，并把 `3～6` 的外圈战力线性折算。军团仍按军团长键去重，所有数值继续
  使用原版 `PartyBase.EstimatedStrength` / `Army.EstimatedStrength`。诊断同时记录区域内
  原版已计算基础速度最高的敌方战斗组。
- `EnsureCommittedStrengthAdvantage` 先计算当前全部合格候选的最大可投入原版战力。若理论
  上限仍不高于敌方区域总战力，则不再先抢走一个普通任务承办人再宣布失败，而是原地失败；
  理论上可以取胜时才按距离逐名征调，直到已承诺战力严格高于敌方。成员不会因敌方后来变弱而
  被主动移出。`FailAssistanceCase` 在结束案件后再次核验战争理由，没有其他合法灰袍战争理由
  时调用既有中立化入口立即恢复和平。
- 速度生命周期改为两态。军团状态下，用原版已经计算的军团长
  `LastCalculatedBaseSpeed` 与敌方区域内所有目标战斗组的最高同类速度比较；敌方最高速度严格
  更高时，同一更新中让主办人与全部协办领主退出 `Army` 并各自追捕。分散状态不再寻找单一
  `speedCatcher`，目标最高速度回落到不高于解散前真实军团速度时才全组重建军团。旧的逐名
  `AdvanceAssistanceSpeedDispersal` 路径不再被调用。
- 协力主办人与速度分散成员在宣战前后都使用原版 `GoAroundParty` 长期欲望；未分散协办人
  只用 `EscortParty` 追随主办人。协力案件的自动宣战距离从普通 `3` 扩大到原版
  `GoAroundParty` 最外圈计算值，使环绕行为本身能够进入宣战范围，不再切换成面向友军的
  `EscortParty` 追目标。接战许可仍使用已承诺总战力对同一敌方区域快照的比较，不再额外创造
  “当前实际到场战力”边界。
- 最终 1.4.7 `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条既有警告并自动部署；
  1.4.5 与 1.4.6 完整源码交叉构建均为 `0` 错误、`43` 条既有警告。ILSpy 对实机 DLL 确认
  区域战力快照、外圈折算、敌方最高速度、理论最大可投入战力预判、强制职责排除、全组速度分散、
  原版 `GoAroundParty` 枚举值、外圈宣战距离和失败后和平入口均已进入产物；最终实机客户端与
  编辑器 DLL 均为 `715264` 字节，SHA-256 均为
  `CE418F4A057E7C562C1F77B9EA088B6929A9F8B0094623A91355972D9A5B548A`。
  仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML
  解析失败 `0`；仓库与实机中文 README SHA-256 均为
  `CA10D0391945E0A1B93CD7FDA837BD96D3D1C12706B4FCB2580DD75016765E0A`，英文均为
  `F9BA4FBBAE950AD6411EFA6F15D8803CE05976792F4413E3A638685F05F2A329`。
  `git diff --check` 通过。没有启动游戏，等待用户实机验证；没有创建或改写正式
  `v1.4-r7` ZIP，其 SHA-256 仍为
  `963AA367A2512126E5DBB92D04792A0075C3C1DE20DD7D40B61B6E2468ECE15A`。

## 2026-07-24 v1.4-r8 开发：两层协力判定与任务级独立速度基准

- 本轮最终确认协力的战力和宣战必须分成两层，不能再用“已承诺成员战力总和高于目标区域”
  一个条件同时代表增援规模与实际接战。第一层继续用原版
  `PartyBase.EstimatedStrength` / `Army.EstimatedStrength`、目标真实军团或围城参战集合，
  以及原版加入半径的内圈和递减外圈，决定协力组需要征调多少当前无强制职责的领主；成员只增
  不减，全部合格力量仍不够才失败并恢复和平。第二层只负责宣战：进入外圈宣战距离后，按原版
  本地区域战力口径核算实际行动者与当前能够加入战斗的友军，再与目标、目标军团及附近能够支援
  目标的敌军比较；我方实际区域战力严格高于敌方实际区域战力便允许宣战。这里不预测
  `EngageParty`，也不额外检查速度、追及时间、士气、目标暴露状态或其他行为条件；宣战后只请求
  原版重新思考，不给英雄领主写强制攻击短期命令，也不关闭或替换原版欲望。
- 未分散的协力军团只有军团长能够代表该原版战斗组通过第二层判定，仍在路上的协办人不能凭自己
  更靠近目标而提前触发战争；速度分散状态下，每名独立追捕者都可以按自己的本地区域战力成为
  宣战行动者。本地友军和敌军均按实际军团/战斗组去重，加入半径之外的已承诺协办人不再被当作
  “此刻会一起开打”的本地支援。新增
  `ASSISTANCE_DECLARATION_WAITING_LOCAL_STRENGTH` 与
  `ASSISTANCE_DECLARATION_LOCAL_STRENGTH_READY`，记录行动者、双方本地区域战力、整案已承诺
  战力、距离和强弱结果，便于下一次实机监控直接区分“整案已经征调够人”与“现场力量已经真正
  足够宣战”。
- 军团速度条件改为任务级固定基准。普通案件交给主办领主时，若其尚未进入军团且原版
  `LastCalculatedBaseSpeed` 有效，立即把该独立速度写入 `PoliceTask`；若当刻尚无有效值，则只在
  建立协力军团前的首个有效独立状态补记一次，随后整个案件、反复解散/重组及存读档均不再改变。
  新存档键为 `gwp_lt_{i}_leader_solo_speed`。
- 军团是否分散不再读取敌方区域内最快部队，也不再读取已经变慢的协力军团当前速度。只解析案件
  目标的实际移动主体：
  `BesiegerCamp.LeaderParty -> Army.LeaderParty -> AttachedTo -> offender`，并在该主体速度
  严格高于主办人接案时独立速度时全组分散。目标实际移动主体速度重新不高于固定基准时，同一案件
  才重建军团。目标本人附着军团后显示 `0` 速度不会再误判，目标附近另一支更快敌军也不会错误
  拆散协力军团。诊断同时保留区域最快敌军速度作对照，但它不参与速度状态转换。
- 最终 1.4.7 `Release -t:Rebuild --no-restore` 为 `0` 错误、`44` 条既有可空性/离线 NuGet
  警告并自动部署；1.4.5 与 1.4.6 全源码交叉构建均为 `0` 错误、`43` 条既有警告。ILSpy 对
  实机 DLL 确认 `LeaderSoloSpeedAtAssignment`、`gwp_lt_{i}_leader_solo_speed`、
  `EvaluateLocalDeclarationStrength`、`GetNativeFriendlyLocalStrength`、两条新宣战诊断，
  以及 `targetMovementSpeed > leaderSoloSpeed` / `<=` 的分散与重组边界均已进入产物。
  仓库 `_Module` 的 `25` 个正常客户端文件与实机相比缺失 `0`、哈希不一致 `0`，`18` 个 XML
  解析失败 `0`。实机客户端与编辑器 DLL 均为 `720384` 字节，SHA-256 均为
  `AB3AA79D47AE365912626FADFF4D696679047FB2BBBBCE57BC7BD6A9D98C9C9F`；仓库与实机中文
  README SHA-256 均为
  `416C8D84B2341F741B3F85559DD5FE6C372FA12BC024DC97318BBD0BAC660D0E`，英文均为
  `AA1C862B688A513D851DE77F598A6C7044C98E90FC33CF5273B805B20C0B56D2`。按用户要求没有
  启动游戏，由用户进行行为验证；普通开发构建没有创建或改写正式 ZIP，本机唯一正式包仍为
  `GreyWarden-v1.4-r7.zip`，SHA-256 仍为
  `963AA367A2512126E5DBB92D04792A0075C3C1DE20DD7D40B61B6E2468ECE15A`。
