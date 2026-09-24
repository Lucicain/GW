# 辛迪加分段战场配乐

> 当前状态：**已部署待实测**：原版片段实时混音（FLAC 无损，387 MB → 80.6 MB）已获用户认可；本轮加 +8 dB 输出增益与峰值限制器、放慢升档节奏
> 最后验收：**`b68b43c`**（本地检查点，未发布；那时是预渲染整段 PCM）
> 覆盖源码：`GwpMusic*.cs` `GwpFlacReader.cs` `GwpSyndicateMusicBehavior.cs` `tools/MusicTests/**` `tools/Export-SyndicateMusic.py`
> 待实测：用户已确认实时混音“效果不错”（2026-09-24）；待听 +8 dB 后与原版音乐的响度是否接近、有无被压扁感；升档节奏是否合适

**回退路径**：回到用户验收的 `b68b43c` 预渲染版时，从该提交取回
`GreyWardenPolicePurity/GwpMusicScore.cs`、`GreyWardenPolicePurity/_Module/Music/Lab/`（26 个 `.pcm`
与 `score.json`、`manifest.json`）、`tools/Export-SyndicateMusic.py`、`tools/MusicTests/`；
删掉 `GreyWardenPolicePurity/GwpFlacReader.cs` 与 `_Module/Music/Lab/*.flac`，重新构建，按
[build-and-deploy](build-and-deploy.md) 镜像并比对哈希。

## 何时启用

开战布阵阶段一次性判定，之后不再开关：

- **任意一方开战普通士兵中灰袍占比 >= 50%**。
- 分母是普通士兵：英雄、玩家、马**不计入**；普通灰袍与精锐均按人数。
- 不是战力占比，不是全场双方混算。
- 名单来自原生 `GetAllTroopsForSide`，**含原生预备兵**，在玩家 Agent / PlayerTeam
  与双方出兵名册就绪后读取。
- 之后的伤亡、兵力上限、增援都不重新开关资格。
- 无玩家、多人、友好切磋、无正常战斗 spawn logic 时不启用。
- 仅凭玩家身份或单个灰袍英雄**不再**跳过比例门槛。

资格判定在 `GwpBattleSceneContext`。音乐资格与援军资格是**两套独立判据**，见
[`battle-support.md`](battle-support.md)。

## 强度映射

```
Exchange = max(近 20 秒对敌伤害 / (force × 0.16), 近 20 秒阵亡 / (force × 0.10))，限 0–1
Tension  = Exchange × (0.7 + 0.2 × Attrition + 0.1 × Pressure)
```

- `force` 是双方现存战力，加上近 20 秒阵亡，避免只剩幸存者时一点伤害被放大。
- 旧式 `0.7*Exchange + 0.2*Attrition + 0.1*Pressure` 已废除：它让历史伤亡和劣势形成
  最高 0.3 的永久底座。现在历史损失只能**放大**真实的近期交战，不能单独撑住档位。

### 节奏（2026-09-24 按用户“升档太快”重调）

依据：用户那场 16 分钟、双方战力约 1600 的实机日志（`GreyWarden-Faults.log` 08:20–08:37）。
- 第 123 秒第一支箭命中就进了中档，Exchange 只有 0.005。
- 第 197 秒进高潮。
- 之后约 13 分钟几乎一直是高潮：主力接战每 20 秒阵亡占 5–19%，旧门槛 6% 就满格。

现行规则（常量在 `GwpMusicBattlePolicy` 顶部）：

| 规则 | 现值 | 旧值 |
|---|---|---|
| 满格门槛（每 20 秒） | 伤害 16%、阵亡 10% | 10%、6% |
| 进中档 | Tension ≥ 0.12，连续 4 秒 | 任何命中，2 秒 |
| 进高潮 | Tension ≥ 0.55，且最近 2.5 秒有命中，且 5 秒窗口 `RecentExchange` ≥ 0.55，**且已在中档持续 40 秒**（打过一次高潮后再升只需 20 秒），连续 6 秒 | 前两项，3 秒 |
| 保持高潮 | Tension ≥ 0.35，至少 30 秒 | 0.40，12 秒 |
| 降档 | 连续 6 秒 | 同 |
| 其他持档 | 低 2 秒、中 12 秒 | 同 |
| 回低档 | 12 秒无接触 | 同 |

- 不能从低档直接跳高潮。
- 回到低档后，下一次升高潮重新按首次的 40 秒计。
- 候选不满足条件就留在中档候选，不会等持档结束后把旧爆发补升。
- 新战斗清空两个窗口；增援不抹除交战历史。
- 测试里的猛烈接战：6.5 秒进中档，52 秒进高潮。
- 这是骑砍适配设计，**不是 GWENT 原版规则**。

## 过渡与播放

- 35 条过渡规则，涵盖原版桥段、低中同时间位置切换、重叠包络样本、开场循环、
  增援打断/恢复最新档位、结尾取消待切换且只播一次。
- 增援到场按 Redraw 完整过渡通知一次，**不逐兵切歌**。双方援军各通知一次。
- 音乐收尾按双方分别查询待生援军（早期版本只防玩家侧，已修）。
- 播放器走原版留给模组的 PSAI 音轨接口；策略请求的档位**不等于**听到换段的时刻，
  还受原版节拍、乐句、过渡段和 Redraw 优先规则约束。

## 渲染

不再预渲染乐段。`GwpMusicScore` 按 soundbank 数据实时混音：

- 每个乐段由若干音轨组成，每条音轨放一个或多个**原版源片段**。片段在乐段时间轴上的位置、
  首尾裁剪来自 `fPlayAt` / `fBeginTrimOffset` / `fEndTrimOffset` / `fSrcDuration`；
  音轨静态音量 `volumeDb` 按 `10^(dB/20)`。
- **片段自动化包络**（本次补上）：我们用到的 34 条音轨里 15 条有包络，共 20 条：
  淡入 3、淡出 9、音量自动化 8。时间以片段起点（裁剪后）为 0，单位秒。
  - 音量自动化存的是“线性增益减 1”（Wwise `ScalingFromLin_dB`），−0.247 即增益 0.753。
  - 淡入淡出存 0–1 的线性增益。
  - 点与点之间按左侧点的曲线插值，Constant 保持左值。
- **曲线**：包络与交叉淡化都用 `Interpolate`，照搬 Wwise 引擎 `AkInterpolation::InterpolateNoCheck`
  （多项式常数来自 wwiser 对引擎的还原，见 `D:\lucigames\assets\syndicate\research\music-arrangement\tools\reference\bnode_rtpc.py`）。
  交叉淡化读规则里的 `eFadeCurve`（16 条同时间位置规则都是 Log1，淡入 `t(3−t)/2`，淡出为其 1 减）。
  旧版用的 `log10(1+9x)` 是浏览器实验室的近似，已废除。
- **输出增益与限制器**（2026-09-24，用户反馈比原版小）：
  - 响度实测（EBU R128）：我们 320 秒指纹场景混音 −23.5 LUFS，峰值 −5.2 dBFS；
    骑砍原版 51 首战斗/攻城曲（`Modules/Native/Music/PC/Battle_*`、`Siege_*`）中位 −13.3 LUFS，
    按时长能量平均 −12.9。内容差约 10 dB。
  - 原版音乐音量由引擎原生层施加，托管代码看不到，所以不知道引擎是否对原版音乐再加减增益。
  - 现在 `GwpMusicOutput` 在“主音量 × 音乐音量”之外固定加 `MakeupDb = +8 dB`，保守地略低于原版。
  - 之后接 `GwpMusicLimiter`，参数照原版 Wwise 主总线：门限 −1 dBFS、提前 10 ms、释放 20 ms，
    输出延迟 10 ms。
  - 两个音量都在 100% 时，指纹场景约 1% 的 20 ms 块被压，最深 3.8 dB；音量调低后压得更少。
    低于门限的信号原样通过。
- **仍未还原**：
  - 源侧淡出偏移 `iFadeOffset=2000 ms`：正负号的含义没有权威依据，交叉淡化仍在切换点同时开始。
  - Music 总线 −6 dB：未施加；响度改为向骑砍原版对齐，见上。
  - 20 Hz 高通：听不出。
  - 原版这条音乐路径**没有混响**，不存在“缺混响”。
- 解码：`GwpFlacReader`，只支持本素材的 16 位立体声、固定块长 FLAC。导出时记下每帧字节位置，
  随机定位只解一帧（约 96 ms）。每个播放中的乐段最多 2 个片段同时解码。
- 性能：离线渲染 320 秒（含多次换段与交叉淡化）连同进程启动共约 2 秒，约 160 倍实时。
  未测游戏内 CPU/FPS 影响。
- 每 20 毫秒的 Render/Current 保持无 LINQ、无闭包、无临时集合；解码缓冲与 LPC 系数数组复用。

## 测试

`tools/MusicTests`：

- 完整运行 58,770,640 条断言，**绝大多数是逐采样检查**（解码值在 16 位范围内、输出为有限值），
  不等于这么多场景。
- **无损**：27 个源片段逐帧解码，SHA-256 与导出时记下的原始 16 位 PCM 一致；随机定位与顺序解码一致。
- **曲线**：9 种 Wwise 曲线端点与单调性；Log1 中点为 0.625。
- **包络**：开场 02 的 SineRecip 淡出先保持、终点归零、中点等于 cos(π/4)；低强度 01 的音量自动化按“增益减 1”。
- **交叉淡化**：低→中同时间位置切换在 15.025 秒处定位，逐采样等于两个声部各自的未淡化输出乘以独立算出的 Log1 曲线。
- **限制器**：低于门限原样通过，只延迟 481 个采样；+12 dB 方波与尖峰不越过 −1 dBFS；
  真实乐谱在 +8 dB、满音量下不越过 −1 dBFS，被压的块少于 2%、深度小于 4 dB。
- **节奏**：零星命中停在低档；猛烈接战先中后高，高潮至少等中档 40 秒，从不由低直跳高。
- 35 条过渡规则、增援返回最新档位、结尾只播一次、静音 WinMM 生命周期（提交首 buffer 前 `Volume = 0`）。
- `--fingerprint`：固定 seed 43、320 秒场景（intro/low/medium/high、低中互切、分批增援、回落、outro）。
  当前 `3504DD98272E5E2A268E1384E41AB026FC5C4EB579589105C4FE4169DC426D53`。
  实时混音与新曲线本来就会改变声音，所以与旧值 `B13DB9B2…` 不同。
- `--compare-old <旧 pcm 目录>`：一次性对照。去掉包络后逐乐段与 `b68b43c` 的预渲染 PCM 比较。
  2026-09-24 结果：26 段最大差 1.34×10⁻⁷，即旧版 24 位存储的量化级，说明片段放置与旧渲染逐采样一致。
  旧 PCM 已不在工作树，需要时从 `b68b43c` 取。

## 素材

`_Module/Music/Lab/`：27 个 `<sourceID>.flac`（共 80.6 MB）、`score.json`、`manifest.json`。

- `score.json`：乐段、音轨、片段、包络、播放列表、过渡规则，以及每个源片段的字节数、样本数、
  块长和帧偏移表。
- `manifest.json`：每个源片段的 WAV、PCM、FLAC SHA-256；WAV 路径相对于研究目录，模组里不含本机路径。游戏运行时不读它，只供测试核对。
- 原 26 个预渲染 `.pcm` 共 387 MB（32 位浮点），已删除。

导出脚本 `tools/Export-SyndicateMusic.py`，需要 ffmpeg。数据源：

- 乐谱数据：`D:\lucigames\toys\syndicate-cards\app\music\data.json`，与旧 `score.json` 的乐段、播放列表、规则逐项相同。
- 源片段：`D:\lucigames\assets\syndicate\research\music-arrangement\decoded\*.wav`（vgmstream 解码的原版 WEM）。
- 包络：同目录 `clip-automation-audit.json`。

脚本做这些事：

- 用 FLAC 等级 8 编码，确认 ffmpeg 解回的 PCM 与 WAV 逐字节相同。
- 扫描帧偏移（同步码、帧号与头部 CRC-8 三重校验）。
- 镜像到 live 并比对哈希，删掉两边多余的 `.pcm` 和 `.flac`。
