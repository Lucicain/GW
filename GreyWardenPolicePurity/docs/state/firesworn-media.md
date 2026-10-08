# 火誓者仪式媒体：卡面与两种声音

> 当前状态：仪式配图、双声音交接已由用户确认完全修复；声音健康路径诊断退休
> 最后验收：`774d2e2`（2026-10-08，仪式媒体）
> 已复核至：`a898237`（2026-10-08）
> 覆盖源码：`GwpFireswornArt.cs` `GwpFireswornSoundView.cs` `GwpRitualSoundOutput.cs` `GwpRitualMapSoundView.cs` `_Module/GUI/Images/**` `_Module/ModuleData/module_sounds.xml` `_Module/ModuleSounds/gwp_firesworn_*.wav`
> 待实测：无

[仪式行为](firesworn-rituals.md) · [诊断退休规则](diagnostics.md)

## 素材与用户要求

- 传教、烧人有配图和配音：用辛迪加的素材——卡面原画（烈火责罚 202352、集会传教 202374）和卡牌闪卡音效。（依据：用户裁定 2026-10-06，取代此前的「卡面原画不用，只作参考」）
  以后要像比武大会那样，能进去参与或闲逛。（依据：用户裁定）
  原画源文件为 `D:\lucigames\toys\syndicate-cards\app\media\art\202352.webp` 与同目录 `202374.webp`，身份为当前辛迪加卡面素材。
  配图颜色已由用户实测确认；声音只播放一次，关闭仪式界面必须立即停止，声音与辛迪加素材一致。
- 以后自制美术时风格是黑灰色，不是红色。（依据：用户裁定）
- 大地图上，仪式进行时那座村子放闪卡音效（烈火责罚 `463341571`，集会传教 `241188017`）；点进仪式画面随机放一段打出音效。（依据：用户裁定 2026-10-07，取代「只用闪卡、不用打出」）
  打出音效：烈火责罚 `58628604` `146933260` `594860666`（无人声）；集会传教 `827145150` `362102572` 带人声台词（卡牌站 `sound-rules.json` 标 voice），
  用户要求传教画面也随机放打出音效（2026-10-07）。
- 大地图上的闪卡音效不无缝循环：放一遍，隔一会儿再放；点进任何菜单就停掉、关了从头再放，免得和画面里的打出音效叠成回声。（依据：用户裁定、实机观察 2026-10-07、2026-10-08）
  响度：都校到 −23.1 LUFS。卡牌站里打出音效走的通道另加 +3 dB、闪卡 0 dB，原件打出 −23.3、闪卡 −20.2 LUFS，原作里两者一样响，所以校到同一值。
  原先的 −23 LUFS FLAC 目录已不存在；现从卡牌站 `D:\lucigames\toys\syndicate-cards\app\media\sfx\` 的 Opus 原件
  增益校到 −23.1 LUFS，当前部署为 48000 Hz 立体声 16 位 PCM WAV，放在
  `_Module/ModuleSounds/gwp_firesworn_{damnation,congregation}.wav`；从现有校准后的 Vorbis 解码，未变速或移调。
- 配音可以用，男声台词也可以用（用户 2026-10-07 要求传教画面放带人声的打出音效）。
- 祭司、狂信者的装备原版凑不出来，要新做袍子和民兵装备；源资源重建见 `../reference/asset-pipeline.md`。

## 播放与配图实现

大地图闪卡与菜单随机打出两种声音都必须保留，行为不变，允许简化实现；只在菜单入口交接播放，不改原生声音底层。（依据：用户裁定 2026-10-08）声音修复回退点 `4c0de6d`：取回两个声音视图；若需恢复此次退休的诊断，从 `774d2e2` 取回两个视图及其 Trace 文件。

- **配图配音**（`GwpFireswornArt.cs`）：仪式画面的背景图直接换成卡面原画（烈火责罚 202352、集会传教 202374，
  卡牌站 `art/*.webp` 转成的 PNG，放在 `GUI/Images/`），不另弹窗（用户裁定 2026-10-06：做原版替换）。
  原版菜单背景是按名字从全局精灵表取的精灵（Native `GameMenu.xml`：`GameMenuWidget Sprite="@Background"`）。
  原画在运行时读成贴图，以 `gwp_firesworn_damnation` / `gwp_firesworn_congregation` 登记进 `UIResourceManager.SpriteData.Sprites`，
  菜单背景设成这个名字。PNG 必须是 RGBA（全不透明 Alpha），RGB 像素与卡面源图逐像素一致。
  三通道 PNG 在 `Texture.CreateFromMemory` 路径被当作 BGR，红蓝互换；四通道路径正确转换。
  v1.4.8 Native RVA `0x525850` → `0x2eeba0`：3 通道设格式 `0x2a` 并原样复制，
  `0x2f01b0` 将它扩成格式 2，RGB 字节仍原样排列；4 通道在 `0x2eef70` 转 BGRA 后用格式 1。
  （依据：C++反汇编、源图像素核对、用户截图 2026-10-07）
  原版竞技场菜单 `PlayerTownVisitCampaignBehavior.game_menu_town_menu_arena_on_init` 分别设置 panel 音效与 arena 环境声。
  `MenuViewContext` 以空 Scene 创建声音，`OnMenuCreate` / `OnDeactivate` 调 `StopAllSounds`，菜单切换/停用时释放声音。
  `GwpFireswornSoundView.ActivateMenu` 每次激活仪式菜单先调用地图视图的 `StopForMenu`，再查 `_started`，保证重复激活也停地图声，而菜单声每次访问只播一次；刷新、播完后都不重播。
  离开选项在离开聚落前停止；切换菜单、停用视图、销毁视图也立即停止。该播放标记只是界面状态，不存档。
  `GwpRitualSoundOutput` 按 WAV 头部 48000 Hz 提交一个有限的 PCM 缓冲，无循环标记、无变速/移调调用，
  自然播完释放设备，退出调用 `waveOutReset` 丢弃队列；主音量 × 音效音量在提交前乘入 PCM。
  音频输出设备使用 Windows 默认设备（与 `GwpMusicOutput` 一致），不跟随骑砍的单独 SoundDevice 选择。
  传教 555649 帧（11.576 秒）、烧人 624405 帧（13.008 秒）；PCM 与旧 Vorbis 解码逐字节一致。
  仪式画面里放哪一段：`GwpFireswornArt.SoundPath` 在 `{背景名}_play_N.wav` 里随机挑（烧人 3 段、传教 2 段）。
  （依据：C#反编译 v1.4.8、Native 类别说明、用户实测、离线解码与静音设备检查 2026-10-07）
- **大地图上的仪式声音**（`GwpRitualMapSoundView.cs`）：照原版地图战斗的声音——在战场位置建 3D 声音，看不见就暂停、结束时停
  （`SandBox.GauntletUI.Map.GauntletMapEventVisual`；原版 `event:/map/ambient/node/*` 可听距离 20）。
  我们在仪式进行时，在村子位置（地面高度 + 2）放闪卡音效。卡牌站里闪卡音效每次只放一遍（`card-audio.js`，规则里只有 Play），
  而登记的类别 `campaign_node` 会自己循环（Native 说明「persistent」；用户实测听到无缝重复），所以放满 WAV 时长就停，
  隔 15 秒（现实时间，试玩值）仪式还在就再放。打开任何菜单（`MapState.AtMenu`）就停掉，关了从头再放。是停不是暂停：原版地图界面每次激活、恢复都把场景声音整体恢复（`RestartAmbientSounds` → `ResumeSceneSounds`），菜单开着时也会（对话、背包回来），暂停的会被放出来。
  原版菜单建立后地图帧更新可能不再执行，因此仪式菜单入口主动调用同一个 `StopForMenu`，不依赖下一帧发现 `AtMenu`；原版暂停/恢复仍由地图界面处理，销毁时全部停掉。用户已确认完全修复，声音取证已退休（2026-10-08）。
  声音登记在 `module_sounds.xml`。不存档：读档后按正在进行的仪式重建。
  视图在 `MapScreen.OnInitialize` 后加（`GwpRitualMapSoundViewPatch`），每个游戏小时对一次账。（依据：C#反编译 v1.4.8）
  原版声音库里有一个代码没用上的 `event:/map/ambient/node/burning_village`（烧着的村子），用户选了闪卡音效，没用它。

## 回退


仪式媒体修复前存档为 `a1d6479`：取回 `GwpFireswornArt.cs`、`GwpFireswornMenus.cs`、
`_Module/ModuleData/module_sounds.xml`、两张仪式 PNG 与两段 OGG，移除 `GwpFireswornSoundView.cs`、
`GwpRitualSoundOutput.cs` 与两段 WAV，构建、同步 live 并比哈希；不涉及存档数据。
