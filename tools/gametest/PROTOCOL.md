# 游戏测试桥协议（第 1 版）

本文件是游戏内桥接模组 `GreyWardenTestBridge`（C#）与 Python 端（`tools/gametest/gametest/`）之间的约定。
Python 端已按此实现，单元测试里的假服务器 `tests/fakes.py` 的 `FakeBridge` 就是服务端的参考行为。
改协议要同时改两边，不兼容的改动把 `protocol` 加一。

## 1. 启动与发现

1. Python 端启动游戏：

   ```
   <游戏目录>\bin\Win64_Shipping_Client\Bannerlord.exe /singleplayer _MODULES_*Native*SandBoxCore*Sandbox*StoryMode*CustomBattle*<额外模组>*GreyWarden*GreyWardenTestBridge*_MODULES_
   ```

   工作目录是 exe 所在目录，环境变量里带 `GWP_TEST_BRIDGE_SESSION=<32 位十六进制会话号>`。
2. **桥接只在这个环境变量存在时才工作。** 没有它（例如用户在启动器里误勾了桥接模组），桥接什么都不做：
   不监听、不写文件。所以用户自己开的游戏永远不会被测试工具连上或杀掉。
3. 桥接在 `127.0.0.1` 上监听 TCP（端口用 0 让系统分配），**只绑回环地址**。
4. 监听就绪后写 `Modules/GreyWardenTestBridge/bridge.json`，先写临时文件再改名，避免读到半截：

   ```json
   {
     "protocol": 1,
     "host": "127.0.0.1",
     "port": 51234,
     "token": "每次启动随机生成，至少 128 位熵，十六进制，至少 16 个字符",
     "session": "原样抄环境变量 GWP_TEST_BRIDGE_SESSION",
     "pid": 12345,
     "game_version": "v1.4.8",
     "bridge_version": "0.1.0"
   }
   ```

   Python 端只认 `session` 与本次启动一致、`pid` 等于它启动的进程、`host` 为 `127.0.0.1` 的文件；
   其他（旧会话残留）一律忽略。游戏退出时尽量删掉这个文件，删不掉也不影响（靠 session 区分）。

## 2. 帧格式

- 一行一个 UTF-8 JSON 对象，以 `\n` 结尾（容忍 `\r\n`）。单行不超过 16 MiB。
- 请求：`{"id": 1, "cmd": "status", "args": {}}`。`id` 是客户端递增的整数，`args` 总是对象。
- 成功回复：`{"id": 1, "ok": true, "result": <任意 JSON>}`
- 失败回复：`{"id": 1, "ok": false, "error": {"code": "invalid_state", "message": "人能读懂的中文原因", "data": <可选>}}`
- 回复的 `id` 必须与请求相同。一条连接上客户端同一时间只有一个未完成请求，所以按序回复即可。
- 同一时间只服务一条连接；新连接 `hello` 成功后关闭旧连接。

## 3. 线程模型（给 C# 实现者）

- 网络线程只负责收发和排队，**不碰任何游戏对象**。命令放进队列，由主线程在模组的 tick
  （`MBSubModuleBase.OnApplicationTick`）里取出执行。
- 跨帧的命令（`load_save`、`save`、`advance`、`quit`）在主线程上推进，**完成时**才回复；Python 端按命令设超时，
  超时就认定游戏卡死并强杀，所以不要为了「先回个收到」提前回复。
- 主线程执行命令时抛出的异常，转成 `internal` 错误回复，`message` 带异常类型与消息，`data` 可带堆栈；
  这是协议要求的回复，不是兜底吞错（按 `.claude/rules/csharp-gotchas.md` 写 `依据：` 并记 `GwpFaultTrace`）。

## 4. 鉴权

- 每条连接的第一条请求必须是 `hello`，否则回 `unauthorized` 并断开。
- `hello.args`：`{"token": "...", "session": "...", "protocol": 1}`。令牌用定长比较；令牌或会话不符回
  `unauthorized` 并断开；`protocol` 不支持回 `unsupported_protocol`。
- 成功结果：`{"protocol": 1, "pid": 12345, "game_version": "v1.4.8", "bridge_version": "0.1.0"}`。

## 5. 命令

存档名在两端都校验：`^gwp_test_[A-Za-z0-9_-]{1,64}$`，不带扩展名。不合规回 `refused`，**桥接永远不读写其他存档**。

| cmd | args | result | 说明与错误 |
|---|---|---|---|
| `hello` | 见上 | 见上 | |
| `status` | `{}` | `{"state": ..., "campaign_time": {"hours": 1234.5, "text": "..."}, "save_name": "...", "paused": true}` | 任何时候都能调。`state` 取值：`starting`（还没到主菜单）、`main_menu`、`loading`、`campaign`（地图上，可以执行战役命令）、`mission`（场景/战斗中）、`other`。`campaign_time` 只在战役存在时给，`hours` 用 `CampaignTime.ToHours` |
| `load_save` | `{"name": "gwp_test_x"}` | `{"name": "gwp_test_x"}` | 在 `main_menu` 或 `campaign` 时可用，经原版 `SandBoxSaveHelper.TryLoadSave` 读档；**进入战役地图并跑完第一帧后**才回复。错误：`refused`、`not_found`、`invalid_state`、`load_failed`（`data` 带原版给的原因） |
| `save` | `{"name": "gwp_test_x", "overwrite": false}` | `{"name": "gwp_test_x"}` | 仅 `campaign`。同名存档已存在且 `overwrite` 为假回 `exists`。存档写完才回复。错误：`refused`、`exists`、`invalid_state`、`save_failed` |
| `console` | `{"command": "campaign.add_gold_to_hero 1000"}` | `{"output": "原版返回的文本", "found": true}` | 首个空白前是命令名，其余按空白拆成参数，调原版 `CommandLineFunctionality.CallFunction`。命令名含 `save`/`load`/`quit`/`exit` 的回 `refused`（Python 端也拦）。找不到命令回 `unknown_command` |
| `advance` | `{"hours": 24}` | `{"hours_requested": 24, "hours_advanced": 24.0, "campaign_time_before": {...}, "campaign_time_after": {...}}` | 仅 `campaign`。用原版正常的战役 tick 推进（快进），不跳过 tick，各行为照常运行；到达目标时间后暂停再回复。弹出需要玩家处理的界面（询问框、对话、遭遇菜单）导致时间停走超过 10 秒（真实时间）时回 `blocked`，`data` 给 `{"reason": "inquiry/conversation/encounter/...", "title": "...", "hours_advanced": 3.5}` |
| `query` | `{"kind": "...", "args": {...}}` | 见下表 | 只读，不改游戏状态。未知 kind 回 `unknown_kind` |
| `quit` | `{}` | `{"quitting": true}` | 先回复，再在下一帧退出游戏，**不存档**。Python 端等进程退出，等不到就强杀 |

### query 的 kind

| kind | args | result |
|---|---|---|
| `kinds` | `{}` | `{"kinds": ["cases", "envoys", ...]}`：本桥接支持的全部 kind |
| `cases` | `{}` | `{"cases": [{"id": ..., "state": ..., ...}]}`：灰袍案件快照 |
| `envoys` | `{}` | `{"envoys": [...]}`：使者快照 |
| `case_parties` | `{}` | `{"case_parties": [...]}`：办案队快照 |
| `party` | `{"id": "部队 StringId"}` | `{"party": {...}}`：单个部队（位置、人数、行为、目标等）；找不到回 `not_found` |
| `sentinels` | `{}` | `{"sentinels": {"哨兵名": {"passes": 经过次数, "triggers": 触发次数}}}`：本次启动以来的复发哨兵计数（见 `docs/state/diagnostics.md`）。哨兵只编进诊断版，非诊断版回 `unavailable` |

快照字段由桥接实现时定，原则：

- 谁算灰袍、案件状态、差事占用、战力等共享概念只经归属方的唯一入口读取（`docs/state/code-health.md`「共享概念的归属」），
  桥接不另写一套判断。
- 用稳定 Id（`StringId`）和原始数值，不给本地化文本以外的显示用字符串；时间统一给 `hours`。
- 新增 kind 不算协议不兼容，`kinds` 里列出来即可。

## 6. 错误码

| code | 含义 | Python 端处理 |
|---|---|---|
| `unauthorized` | 没握手、令牌或会话不符 | 断开；启动阶段失败则强杀 |
| `unsupported_protocol` | 协议版本不支持 | 同上 |
| `bad_request` | 缺参数、类型不对 | 原样返回给 AI，游戏保留 |
| `unknown_command` / `unknown_kind` | 不认识的命令或 kind | 同上 |
| `refused` | 违反存档名或命令栅栏 | 同上 |
| `invalid_state` | 当前状态不能执行（如不在战役中） | 同上 |
| `not_found` | 存档、部队等不存在 | 同上 |
| `exists` | 存档已存在且未允许覆盖 | 同上 |
| `blocked` | 推进时间被需要玩家处理的界面卡住 | 同上，`data` 带原因 |
| `load_failed` / `save_failed` | 原版读档/存档失败 | 同上；启动阶段的读档失败会强杀 |
| `unavailable` | 功能在本构建里不存在 | 同上 |
| `internal` | 主线程执行时异常 | 同上 |

Python 端自己产生的码：`timeout`（超时，已强杀）、`disconnected`（连接断开，已强杀）、`protocol_error`
（回复不合格或 bridge.json 不合规，已强杀）、`bridge_missing`、`connect_failed`、`exited`（游戏自己退出）、
`not_running`、`already_running`。

## 7. 安全要求汇总（C# 端必须做到）

1. 没有 `GWP_TEST_BRIDGE_SESSION` 环境变量就完全不启用。
2. 只绑 `127.0.0.1`；令牌每次启动随机、定长比较；首条必须 `hello`。
3. 存档名前缀与字符集校验，与 Python 端同一条正则。
4. 控制台命令拒绝存读档与退出类命令名。
5. 不模拟键盘鼠标、不操作窗口；需要玩家点击的界面一律报 `blocked`，不替玩家点。
6. 桥接是独立的测试模组，源码放 `tools/gametest/bridge/GreyWardenTestBridge/`（Python 端按
   `tools/gametest/bridge/**/obj/**/GreyWardenTestBridge.dll` 找仓库构建来比哈希，放别处要改
   `config.local.json` 的 `bridge_dll_globs`），不放进 `GreyWardenPolicePurity/_Module`，也不进玩家 ZIP。（设计）
