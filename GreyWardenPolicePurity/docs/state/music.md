# 辛迪加分段战场配乐

> 当前状态：原版音乐路由与−9dB效果器已查实，响度/峰值校准已部署；用户本轮反馈未发现问题
> 最后验收：2026-10-18 用户反馈前轮开发未发现问题（工作树构建74d7c8c）；具体滑块/暂停等边界仍按待实测核对
> 覆盖源码：`GwpMusic*.cs` `GwpFlacReader.cs` `GwpSyndicateMusicBehavior.cs` `tools/MusicTests/**` `tools/*Music*.py` `tools/native/inspect-fmod-mix.py` `tools/tests/test_native_music_mix.py`
> 待实测：同一主音量/音乐音量设置下比较普通野战的响度、鼓点峰值及低中高档起伏；滑块、切后台与暂停正常；持续来援的战斗（野战援军、进城搜捕撤离）里换牌段只在第1、3波响，一场最多两次，第一次后不再回低档，第二次后一直是高潮

**回退路径**：回到用户验收的 `b68b43c` 预渲染版时，从该提交取回
`GreyWardenPolicePurity/GwpMusicScore.cs`、`GreyWardenPolicePurity/_Module/Music/Lab/`（26 个 `.pcm`
与 `score.json`、`manifest.json`）、`tools/Export-SyndicateMusic.py`、`tools/MusicTests/`；
删掉 `GreyWardenPolicePurity/GwpFlacReader.cs` 与 `_Module/Music/Lab/*.flac`，重新构建，按
[build-and-deploy](build-and-deploy.md) 镜像并比对哈希。

用户要求按原版实际声音链查证后修正偏响，不采用任意降低几个dB作为准确对齐的依据。新调查不改变战斗资格、曲目和强度策略。（依据：用户裁定2026-10-18）

## 何时启用

开战布阵阶段一次性判定，之后不再开关：

- **任意一方开战普通士兵中灰袍占比 >= 50%**。
- 分母是普通士兵：英雄、玩家、马**不计入**；普通灰袍与精锐均按人数。
- 不是战力占比，不是全场双方混算。
- 名单来自共享 `IGwpBattleSceneSource.GetAllTroopsForSide`（野战转接原生供应器，城内接真实城防名册），**含原生预备兵**，在玩家 Agent / PlayerTeam
  与双方出兵名册就绪后读取。
- 之后的伤亡、兵力上限、增援都不重新开关资格。
- 无玩家、多人、友好切磋、没有就绪的共享战斗名册来源时不启用。
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
- 增援到场按 Redraw 完整过渡，**不逐兵切歌**。原版每批援军、灰袍援军每方首批、进城搜捕每批城防援军各算一波，不分敌我。
- 增援提示（局间换牌段）：第1波来援播放，隔一波再播（第3波），一场最多两次；所有用辛迪加配乐的战斗都按这条。（依据：用户裁定2026-10-26）
- 增援提示把战斗分成三局，档位仍由交战强度决定，只按局限定范围：第1局（尚未提示）低/中/高，第2局（第一次提示后）中/高，第3局（第二次提示后）只有高。没有来援的战斗一直是第1局。辛迪加原作是第1局低～中、第2局全档、第3局中～高，此处按用户要求整体上调。（依据：用户裁定2026-10-26；原作规则见 `D:\lucigames\toys\syndicate-cards\app\music\data.json` rounds）
- 实现：`GwpMusicBattlePolicy.RecordArrivalWave` 计波数并决定是否提示，`Round` 为已提示次数加一，`Shown` 按局把强度档位抬到下限后交给播放器；战斗初始化不清波数，开战前到场的提示留到开战时播。旧代码每次通知都触发换牌段且无上限，搜捕每个守军各通知一次，实测换牌段一直在响。（依据：用户实测、源码与离线回归2026-10-26）
- 音乐收尾按双方分别查询待生援军（早期版本只防玩家侧，已修）。
- 播放器使用WinMM输出已解码PCM，进入时暂停原版PSAI，离开恢复；档位切换仍由素材乐句、过渡规则和Redraw决定，策略请求时刻不等于立即换段。

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
- **输出响度与峰值**：`ProcessSamples`先施加素材归一补偿`+10.26 dB`，再走既有限幅器，最后施加原版音乐路径`−9 dB`和主音量×音乐音量。小信号净增益`+1.26 dB`；用户滑块在限幅后缩放，不随音量改变压缩效果。（依据：用户要求准确、原版音频库解析、离线校准2026-10-18）
  - 原版v1.4.8的`event:/music`输入总线→`bus:/Music/music/gamemusic`→`bus:/Music/music`→`port:/Music`；各bus fader为0dB，但`music`父总线的pre-fader效果器`0f70651a-7c75-4a3b-84fe-ef00ed2e563e`为FMOD type7 FADER，GAIN参数`−9.0dB`。Music/Master VCA静态值0dB，Music继承Master。此固定效果不由PSAI的VolumeBoost=0反映。
  - 可复核解析入口`tools/native/inspect-fmod-mix.py`，读本机`Modules/Native/Sounds/PC/MasterBank.bank`、`MasterBank.strings.bank`与`music.bank`，版本0x8e/0x8c；路由、效果和参数格式参考[FModBankParser源码](https://github.com/Masusder/FModBankParser)，FADER增益单位参照[FMOD官方文档](https://www.fmod.com/docs/2.04/api/core-api-common-dsp-effects.html#fmod_dsp_fader)。只读metadata，不加载或启动游戏。
  - 原生调用补充证据：Music.SetVolume→Native+0x4D3130→sound vtable+0x1e8/0x255390→event volume vtable+0x100/0x48D5C0→FMOD event setVolume/0x92A400；Music滑块经0x254D10调用vca:/Music，输入被夹到0～1而非额外加dB。这些地址只对本机v1.4.8有效。（依据：C++反汇编2026-10-18）
  - EBU R128实测当前320秒seed43混音`−23.56 LUFS`、`−5.18 dBTP`；52首Battle_/Siege_源文件中位`−13.295 LUFS`。以该中位数作普通战斗参考并计入−9dB，目标约`−22.30 LUFS`；实际生产输出`−22.56 LUFS`，差`−0.26 LU`，真峰值`−9.81 dBTP`。这是静态普通音乐路由的客观校准，不代表每首曲子、每个瞬间同响。
  - 测量事实保存在[校准报告](../reference/music-calibration-20261018.json)，可用MusicTests `--fingerprint --dump <raw.f32> --output-dump <out.f32>`加`Measure-MusicCalibration.py --score-pcm <raw.f32> --output-pcm <out.f32>`复测；PCM为48kHz双声道float32，ffmpeg loudnorm只测input_i并丢弃音频输出。
  - 限幅参数仍为−1dBFS门限、10ms提前、20ms释放；它在−9dB总线之前工作，输出样本上限−10dBFS。真实曲目验证压缩785/16000个20ms块（4.91%），最深6.08dB。输出后的用户滑块线性缩放、静音含延迟样本均通过回归。
- **精确范围**：WinMM仍绕过FMOD动态快照。已解析到过场/视频/最小化对music bus施加−80dB，通知/警示为0dB；本轮不声称实现全部快照、路由效果或主观编曲平衡。常规战斗的固定增益与参考响度已校准，过场/视频与特殊状态要做时再接原版实际状态，不用“战斗时统一压低”代替未知行为。（依据：bank解析2026-10-18）
- **仍未还原**：
  - 源侧淡出偏移 `iFadeOffset=2000 ms`：正负号的含义没有权威依据，交叉淡化仍在切换点同时开始。
  - 辛迪加源作Music总线−6dB未单独套用；此处以完整素材响度归一到骑砍参考，避免重复衰减。
  - 20 Hz 高通：听不出。
  - 原版这条音乐路径**没有混响**，不存在“缺混响”。
- 解码：`GwpFlacReader`，只支持本素材的 16 位立体声、固定块长 FLAC。导出时记下每帧字节位置，
  随机定位只解一帧（约 96 ms）。每个播放中的乐段最多 2 个片段同时解码。
- 性能：离线渲染 320 秒（含多次换段与交叉淡化）连同进程启动共约 2 秒，约 160 倍实时。
  未测游戏内 CPU/FPS 影响。
- 每 20 毫秒的 Render/Current 保持无 LINQ、无闭包、无临时集合；解码缓冲与 LPC 系数数组复用。

## 测试

`tools/MusicTests`：

- 完整运行 58,847,444 条断言，**绝大多数是逐采样检查**（解码值在 16 位范围内、输出为有限值），
  不等于这么多场景。
- **无损**：27 个源片段逐帧解码，SHA-256 与导出时记下的原始 16 位 PCM 一致；随机定位与顺序解码一致。
- **曲线**：9 种 Wwise 曲线端点与单调性；Log1 中点为 0.625。
- **包络**：开场 02 的 SineRecip 淡出先保持、终点归零、中点等于 cos(π/4)；低强度 01 的音量自动化按“增益减 1”。
- **交叉淡化**：低→中同时间位置切换在 15.025 秒处定位，逐采样等于两个声部各自的未淡化输出乘以独立算出的 Log1 曲线。
- **限制器**：低于门限原样通过，只延迟 481 个采样；+12 dB 方波与尖峰不越过 −1 dBFS；
  真实生产链满音量样本峰值不超过−10dBFS，另验证调小音量不改变压缩过程、静音覆盖限幅器里的延迟样本。
- **增援提示与局**：8波来援只在第1、3波提示；第1局保持原档，第2局低抬到中，第3局只有高；战斗初始化不清已计波数。
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

这批辛迪加配乐是**过渡素材**：用户确认可以随包分发（2026-09-25 发 r13 时），之后换成自制配乐，并计划做配音。
发版时不必再为它的分发提醒。改音乐系统时让素材能整批替换，现有片段的 id 和时长只放在 `score.json`，
不进 C# 逻辑。配音开工时另建 state 文件。（依据：用户裁定 2026-09-25）

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
