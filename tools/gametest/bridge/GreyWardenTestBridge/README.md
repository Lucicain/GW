# GreyWardenTestBridge（游戏测试桥的游戏内一侧）

实现 `tools/gametest/PROTOCOL.md`（第 1 版）。独立测试模组：不进 `GreyWardenPolicePurity/_Module`，不进玩家 ZIP。
只在 MCP 服务经环境变量 `GWP_TEST_BRIDGE_SESSION` 启动游戏时启用；用户自己开游戏时即使勾了本模组也什么都不做。

## 构建与部署

先构建主项目（诊断版，默认即是），再构建本项目：

```bash
dotnet build GreyWardenPolicePurity/GreyWardenPolicePurity.csproj -c Debug
```

```bash
dotnet build tools/gametest/bridge/GreyWardenTestBridge/GreyWardenTestBridge.csproj -c Debug
```

- 引用 live 模组里的 `GreyWardenPolicePurity.dll`，所以主项目要先部署。主 DLL 只在诊断版
  （`GWP_DIAGNOSTICS`）向本程序集开放 internal 成员；`GwpDiagnosticsEnabled=false` 构建的桥接里，
  `cases` / `envoys` / `case_parties` / `sentinels` 回 `unavailable`，部队快照不带灰袍字段。
- DLL 输出到 `<游戏>/Modules/GreyWardenTestBridge/bin/<平台>/`，构建后把 `_Module/SubModule.xml` 同步到模组根目录。
- 中间产物留在本目录 `obj/`：Python 端按 `tools/gametest/bridge/**/obj/**/GreyWardenTestBridge.dll` 找仓库构建，
  与 live 比 SHA-256，不一致就拒绝启动。

## 文件

| 文件 | 内容 |
|---|---|
| `BridgeSubModule.cs` | 模组入口：有环境变量才启用；`OnApplicationTick` 主线程执行命令 |
| `BridgeServer.cs` | 网络线程：只绑 127.0.0.1，一行一个 JSON，`hello` 鉴权（令牌定长比较），新连接顶掉旧连接 |
| `BridgeHost.cs` | 主线程分发、`bridge.json`（临时文件改名写入）、`status` / `console` / `quit`，异常转 `internal` |
| `BridgeGameState.cs` | 判断 `state`；记下询问框、场景通知标题，给 `blocked` / `load_failed` 当证据 |
| `BridgeSaves.cs` | `load_save`（`MBSaveLoad.GetSaveFiles` + `SandBoxSaveHelper.TryLoadSave`）、`save`（`SaveHandler.SaveAs` + `OnSaveOverEvent`） |
| `BridgeAdvance.cs` | `advance`：原版 `UnstoppableFastForward` 推进到目标时间后暂停；停走 10 秒报 `blocked` |
| `BridgeQuery*.cs` | `query`：`kinds`、`party`、`cases`、`envoys`、`case_parties`、`sentinels` |
| `BridgeSnapshot.cs` | 内部状态对象转 JSON：只读字段，不调 getter；StringId 与原始数值，时间给 `hours` |

## 快照字段

- `cases`：`CrimePool.LedgerRecords` 每宗案卷：`id`、`state`（`open`/`closed`，即 `HasOpenCase`）、
  `held_by_player`（经 `GwpPlayerCaseAssignment`）、`tasks`（`CrimePool.ActiveTasks` 里指向它的承办任务原始字段）、
  `envoy`（`PoliceEnforcementBehavior.GetEnvoyStatus`）、`fields`（案卷原始字段）。
- `envoys`：`envoys` 是使者进度表 `_envoyCases` 的原始字段加使者队快照，`reports` 是最近的谈判结果。
- `case_parties`：在外小队表 `_delayPatrolStates` 的原始字段加部队快照；城内办案队是 `CityCrimeId` 非空的那些，
  桥接不另作分类。
- `party`：原版部队属性（位置、人数、行为、目标、军团、所在聚落）；诊断版另带 `grey_warden`：灰袍身份
  （`GwpCommon.IsGreyWarden*`）、战力（`GwpStrength.Party`）、差事占用（`GwpWardenDuties.HoldsAny`）、承办任务。
- `sentinels`：主项目还没有复发哨兵登记表，诊断版回空表并带 `note`。

网络线程上的 `catch (IOException)` 等只接住连接的正常终点（对端断开、新连接顶掉旧连接、请求不是合法 JSON），写了 `依据：`
但不记 `GwpFaultTrace`：断线是常态，记了会破坏 Faults.log「健康即空文件」；网络线程不调任何可能对外部输入抛异常的方法，
因为那里的未处理异常会带走整个游戏进程。

私有表按字段名反射只读；字段改名时抛异常，按协议回 `internal`，不静默返回空表。

## 本地核对

源码里标 `TODO(本地核对)` 的地方是云端拿不到原版方法体、只核对了签名的调用（云端用 NuGet 的
`Bannerlord.ReferenceAssemblies` 1.4.8.119303 编译通过）。本地编译后跑 `smoke` 场景逐条确认。
