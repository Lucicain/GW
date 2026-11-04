# 游戏测试桥（MCP 服务）

让没有屏幕控制能力的 AI 自己跑实机自动化测试：一次工具调用办完一件事，返回结构化 JSON，不靠截图。
边界按 `AGENTS.md`「实机」（用户裁定 2026-11-04）：只在用户没在玩时启动游戏、只读写 `gwp_test_` 开头的测试存档、
不操控屏幕键鼠、跑完关掉游戏。

```
tools/gametest/
  server.py          MCP 入口（stdio）：python tools/gametest/server.py
  PROTOCOL.md        与游戏内桥接模组 GreyWardenTestBridge 的协议（第 1 版）
  gametest/          实现：config / host（进程）/ fences（安全栅栏）/ protocol（TCP 客户端）/ controller / scenario / server
  scenarios/*.json   场景：命令序列加断言
  tests/             单元测试，假的 TCP 桥接服务器与假进程，Linux 上也能跑
  bridge/            （之后）GreyWardenTestBridge 的 C# 源码，见 PROTOCOL.md §7
```

现状：Python 端完成并通过单元测试；游戏内桥接模组 `GreyWardenTestBridge` 还没写，所以现在 `game_start`
会被「找不到桥接构建产物」挡下。

## 依赖

Python 3.10+，官方 MCP SDK：`pip install mcp`。2.x（`MCPServer`）与 1.x（`FastMCP`，需 1.10 以上才有结构化输出）都支持。

## 注册

### Claude Code

仓库根目录建 `.mcp.json`（项目级，Claude Code 首次使用会询问是否信任）：

```json
{
  "mcpServers": {
    "gwp-gametest": {
      "type": "stdio",
      "command": "python",
      "args": ["tools/gametest/server.py"]
    }
  }
}
```

`args` 里是相对路径，所以要在仓库根目录启动 Claude Code。也可以用命令行只加到本机（不入库）：

```bash
claude mcp add --scope local gwp-gametest -- python tools/gametest/server.py
```

`game_start` 和场景可能要几分钟（启动游戏 + 读档）。如果客户端在工具返回前报超时，启动 Claude Code 前设
`MCP_TOOL_TIMEOUT=900000`（毫秒）。

### Codex

`~/.codex/config.toml`（路径换成本机仓库位置；Windows 路径用单引号字面量字符串）：

```toml
[mcp_servers.gwp-gametest]
command = "python"
args = ['D:\path\to\GW\tools\gametest\server.py']
startup_timeout_sec = 30
tool_timeout_sec = 900
```

Codex 默认的工具超时只有 60 秒，**一定要把 `tool_timeout_sec` 调大**，否则 `game_start` 会被 Codex 中途放弃
（游戏还在跑，下一次 `game_status` / `game_stop` 能接回并关掉它）。也可以用命令行：

```bash
codex mcp add gwp-gametest -- python D:\path\to\GW\tools\gametest\server.py
```

加完再到 `config.toml` 里补 `tool_timeout_sec`。

## 本机配置

默认值见 `gametest/config.py`：游戏目录 `D:\steam\steamapps\common\Mount & Blade II Bannerlord`，存档目录
`~/Documents/Mount and Blade II Bannerlord/Game Saves`，原版日志 `C:\ProgramData\Mount and Blade II Bannerlord\logs`。
不同就建 `tools/gametest/config.local.json`（已在 `.gitignore`），只写要改的键，例如：

```json
{
  "game_dir": "E:/Games/Mount & Blade II Bannerlord",
  "extra_modules": ["ButterLib", "Bannerlord.UIExtenderEx", "Bannerlord.MBOptionScreen"],
  "load_wait_s": 600
}
```

只改游戏目录也可以用环境变量 `GWP_GAME_DIR`。未知键会报错，避免拼错了却悄悄用默认值。

## 工具

| 工具 | 作用 |
|---|---|
| `game_status` | 没在运行：给出启动前安全检查（`can_start`、`blockers`、`warnings`、哈希等 `facts`）；运行中：桥接报告的 `state`、战役时间 |
| `game_start(save, modules?)` | 过栅栏 → 启动 → 等 `bridge.json` → 握手 → 等主菜单 → 读档 → 确认在战役中才返回。`modules` 插在原版前置与 GreyWarden 之间。任一步失败或超时都杀进程，返回 `stage`、`error` 与证据（退出码、按 pid 取的 `rgl_log` 尾） |
| `game_stop` | 先发 `quit`（不存档），`stop_wait_s` 内没退出就强杀。另一条命令卡住时直接强杀 |
| `game_load(save)` / `game_save(name, overwrite?)` | 运行中换读测试存档 / 存成测试存档；同名存档不加 `overwrite` 拒绝 |
| `run_console(command)` | 原版控制台命令，经 `CommandLineFunctionality.CallFunction` |
| `advance(hours)` | 推进战役时间，推进完才返回；超时 = `command_timeout_s + hours × advance_s_per_hour` |
| `query(kind, args?)` | `cases` / `envoys` / `case_parties` / `party` / `sentinels` / `kinds` |
| `list_scenarios` / `run_scenario(name, keep_running?)` | 列出 / 跑场景，一次返回通过与证据 |

所有工具都返回 `{"ok": true/false, ...}`，失败带 `error.code`（码表见 `PROTOCOL.md` §6），不抛异常。
命令超时或连接断开即认定游戏卡死：杀进程，结果里 `killed: true`。

## 安全栅栏

都在 `gametest/fences.py` 与 `controller.py`，每条都有单元测试：

- **用户在玩就不启动**：有名字以 `Bannerlord`、`TaleWorlds` 开头或叫 `Watchdog` 的进程；或 live 的
  `GreyWardenPolicePurity.dll` / `GreyWardenTestBridge.dll` 被占用（以读写方式打开失败，只打开不写）。
- **live 与仓库不一致就不启动**：live DLL 与仓库最新构建（`GreyWardenPolicePurity/obj/**`、
  `tools/gametest/bridge/**/obj/**` 里最新的那个）SHA-256 不同；`_Module` 下任一可部署文件在 live 缺失或不同
  （例外目录与 `tools/Verify-LiveModule.ps1` 一致）。有 `.cs` 比构建新只给警告。
- **只用测试存档**：读写的存档名必须匹配 `^gwp_test_[A-Za-z0-9_-]{1,64}$`，在发给游戏前拦下；存档已存在时不带
  `overwrite` 不覆盖；控制台里与存读档、退出相关的命令名一律拒绝。桥接端再校验一遍。
- **只杀自己的进程**：会话号经环境变量 `GWP_TEST_BRIDGE_SESSION` 传给游戏，只有测试桥接会写回 `bridge.json`；
  MCP 服务重启后只接回会话号对得上的进程（记在 `tools/gametest/.runtime/session.json`），用户自己开的游戏不会被接管。
- **只连 127.0.0.1**：`bridge.json` 的 `host` 不是回环地址就报协议错误；首条请求带令牌握手。
- **跑完关游戏**：场景默认跑完调 `game_stop`；MCP 服务退出时也会关掉它启动的游戏。

## 场景

`scenarios/<名字>.json`，示例见 `scenarios/smoke.json`：

```json
{
  "description": "这个场景验证什么",
  "save": "gwp_test_xxx",
  "modules": [],
  "stop_after": true,
  "stop_on_failure": true,
  "steps": [
    {"do": "query", "args": {"kind": "sentinels"}, "as": "before"},
    {"do": "advance", "args": {"hours": 24}},
    {"do": "query", "args": {"kind": "sentinels"}, "as": "after"},
    {"assert": {"path": "after.sentinels.CaseSettle.triggers", "op": "eq", "value": 0, "message": "不应复发"}},
    {"assert": {"path": "after.sentinels.CaseSettle.passes", "op": "gt", "ref": "before.sentinels.CaseSettle.passes"}}
  ]
}
```

- 游戏没在运行就用 `save` 启动；已在运行且写了 `save` 就重新读档，保证从同一个起点开始。
- `do`：`status`、`console`（`command`）、`advance`（`hours`）、`query`（`kind`、`args`）、`load_save`（`name`）、
  `save`（`name`、`overwrite`）。`as` 把结果存成捕获名；`expect_error: "错误码"` 表示这一步应当失败；`note` 是注释。
- `assert`：`path` 是 `捕获名.键.下标`（键里有点就写成数组 `["after", "a.b"]`）；`op` 取 `eq ne gt ge lt le approx
  exists absent contains len_eq len_ge len_le truthy falsy`；比较对象写 `value`（字面量）或 `ref`（另一个路径）二选一；
  `approx` 用 `tolerance`。
- 结果：`passed`、每步的 `ok/passed/duration_s`、`failures`（断言给出 `actual` 与 `expected`）、`captures`（每个捕获最多
  `evidence_chars` 字符）、`stop`。`ok: false` 表示场景没能跑起来（启动被拒、文件不合法），与「跑了但没通过」分开。

## 测试

```bash
python -m unittest discover -s tools/gametest/tests
```

不需要游戏和 Windows：`tests/fakes.py` 里有按 `PROTOCOL.md` 实现的假桥接服务器、假进程和临时游戏/仓库目录。
