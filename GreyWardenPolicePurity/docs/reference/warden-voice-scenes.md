# 灰袍语音场景与触发登记

这是骑砍的目标场景表，还没有接入游戏。每行的「来源」使用 Field Officer 音库事件后缀（`Auto_`、`Manual_`）或实际一级动作切换值（`S:`）；同一源可用于多个目标。证据等级描述骑砍可用的入口，不承诺该条件已经上线。新增以骑砍实际场景为准，原作时钟、阈值和机制另留在原声标签里。

- **换声**：已有骑砍发声槽，保留原版请求时点。**事件**：已找到可观察的游戏结果或回调，需写语音条件和调度。**观察**：拟新增条件，需先验证骑砍能观测的动作/状态及边沿。**互动**：任务、轮盘、会面等明确选择或脚本。**专场**：需新增专门任务、演练或短剧情，普通战场不启用。
- 下表的角色、语句和所处阵营三者都要匹配。长句提交前复听原音与措辞；重复记录共用录音。带专名、立场冲突、过场原声或未复听文本的录音不可按普通战场池自动选中：保留原件用于受控演练、引语或逐段审校；需进入灰袍常规战斗时录制中性的同声线版本。
- 普通女兵以自身动作、同袍互动、近处预警为主；领主、要人和当场真实队长承担指挥及任务台词。每人按条件互斥、每队按目标和同一事件去重；原生换声与同义新增语音不能同时播。

| ID | 类型 | 来源 | 谁说 | 骑砍动作和状态 | 播放边沿 | 观察入口或实现责任 |
| --- | --- | --- | --- | --- | --- | --- |
| act.grunt | 换声 | S:GruntSmall S:GruntMedium S:GruntLarge S:GruntSubtle S:Grunt_Subtle S:AttackShout | 出招者 | 近战实际出力 | 原版请求时 | 原生 Grunt；强度变体另证，不猜测 |
| act.jump | 换声 | S:Jump_TakeOff S:None_Jump | 起跳者 | 人类 Agent 起跳 | 原版请求时 | 原生 Jump |
| act.pain | 换声 | S:Personality_Pain1 S:Personality_Pain2 S:Personality_Pain3 | 受击者 | 非致命损伤；选无词痛呼 | 原版请求时 | 原生 Pain |
| act.pain_tier | 事件 | S:Personality_Pain1 S:Personality_Pain2 S:Personality_Pain3 | 受击者 | 有效敌伤；按骑砍实际损血比例分档，格挡排除 | 伤害结算后；仅用于分档替代或可区分的重伤反应 | OnScoreHit；同一次击中不再另叠原版同义痛呼 |
| act.death | 换声 | S:Personality_Death S:DeathEpic | 倒下者 | 原版明确请求死亡短声 | 原版请求时 | Death；不能把旁人喊话当死者短声 |
| act.melee_prep | 观察 | S:WindUp S:Overhead_WindUp S:Stab_WindUp S:Slash_WindUp S:OverheadHeavy_Windup S:StabHeavy_Windup S:SlashHeavy_Windup S:InitiativeAttack_Windup S:Special_Windup S:Personality_RampUp S:RampUp | 备击者 | 有敌交战时进入备击；劈、刺、横砍按可验证方向与武器匹配 | 备击动画进入，非每帧 | 动作通道；先核实原生 Grunt 是否同帧，无法拆分时合并池 |
| act.melee_hold | 观察 | S:Personality_Holding S:FistSpecial_Holding S:Overhead_Holding_bak S:Stab_Holding_bak S:Inhale | 备击者 | 保持确实可见的备击姿势 | 稳定进入持续举持时；取消即释放状态 | 动作通道，区分举持与松手 |
| act.melee_cancel | 观察 | S:slash_cancel S:stab_cancel S:overhead_cancel S:SlashHeavy_Cancel S:StabHeavy_Cancel S:OverheadHeavy_Cancel S:Personality_Frustrated | 中断者 | 备击未释放即撤销、改向或被打断；按来源短声选池 | 从备击退出且无对应释放 | 动作状态转换；动画结束不能单独证明取消 |
| act.melee_release | 观察 | S:InitiativeAttack_Release S:Lunge | 攻击者 | 实际迈步突刺或近战释放 | 出手动画到释放边沿 | 动作通道；若原版 Grunt 已覆盖用其原生槽 |
| act.melee_contact | 事件 | S:Overhead_Thwack S:Stab_Thwack S:Special_HitSuccess S:SprintAttack_HitSuccessbak | 攻击者 | 对敌真实接触且满足该动作类别，不用攻击输入推断 | 有效伤害结算后 | OnScoreHit 结合方向、武器和最近动作 |
| act.melee_fail | 观察 | S:Miss_Recovery S:None_WorldHit S:None_Blocked | 攻击者 | 出手后落空、撞实物或被防住；三类分条件选池 | 碰撞或收招确认时 | 碰撞结果与短窗口；需证实没有命中再判落空 |
| act.parry | 事件 | S:Parry_ParrySuccess S:SprintShove_Blocked S:none_parrybreak | 防守者或被破防者 | 武器招架、防住冲撞、格挡被击破各取相符子池和发声者 | 碰撞结果确定时 | OnRegisterBlow/OnScoreHit 候选；识别招架与破防位后启用 |
| act.bash | 观察 | S:Shove_Windup S:SprintShove_WindUp S:SprintAttack_WindUp S:SprintCharge_Windup | 起手者 | 踢、盾击或带速度的冲撞进入实际起手，不把普通移动当冲撞 | 动作初次开始 | 原版动作通道；模组现有踢/盾击动作证据 |
| act.bow_draw | 观察 | S:ShootBow_Draw | 拉弓者 | 装有箭、拉弓动画确实起始 | 拉弦开始 | 动作通道及实际武器；不播弩 |
| act.bow_release | 事件 | S:ShootBow_Fire | 放箭者 | 弓箭已射出 | 成功发射回调 | OnAgentShootMissile 识别弓；不要混入弩或投掷 |
| act.bow_cancel | 观察 | S:Shootbow_Cancel | 拉弓者 | 真实拉弓后收回，箭未射出 | 从拉弓返回并核对同次无发射 | 动作状态与发射记录 |
| act.throw_prep | 观察 | S:Throw_WindUp | 投掷者 | 标枪、飞斧等进入备投动作 | 备投边沿 | 实际武器和动作通道 |
| act.throw_release | 事件 | S:Throw_Fire | 投掷者 | 标枪、飞斧等实际脱手 | 成功发射回调 | OnAgentShootMissile 识别投掷 |
| act.lance_enter | 观察 | S:LanceCharge_WindUp S:LanceCharge_Charge S:LanceCharge_ChargeWeak S:SpearCharge_WindUp S:SpearPlant_WindUp | 骑手或枪兵 | 进入架枪冲锋或站稳架矛；按骑乘、武器和姿态分开 | 对应动作或状态首次进入 | 骑枪架枪动作、骑马速度、步兵架矛动作 |
| act.lance_end | 事件 | S:LanceCharge_HitSuccess S:LanceCharge_Recovery | 骑手 | 架枪命中、或冲锋结束开始收枪；按记录分池 | 实际命中或已核收枪边沿 | OnScoreHit 与最近架枪动作；收枪另核 |
| act.evasion | 观察 | S:None_Dash S:DashBack_Dash S:DashLeft_Dash S:DashRight_Dash S:None_SprintStop | 闪避者 | 交战中快速侧撤、回步或急停；按方向区别 | 动作或持续移动到停止的边沿 | 玩家或 AI 移动方向/动作，不伪造骑砍没有的闪避键 |
| act.stagger | 观察 | S:None_Stagger S:None_Flailing S:Personality_OffBalance S:Disoriented S:Personality_Disoriented S:None_DownedStagger S:None_Knockdown S:Gasp | 失衡者 | 被打断、踉跄或击倒但仍在场；较轻和真正倒地分开 | 反应动画首次进入 | 骨骼动作通道，检查先前仍活动 |
| act.get_up | 观察 | S:None_GetUp | 倒下者 | 仍存活，已经转入站起动作 | 起身动作开始 | 动作通道；不能以昏迷出场回调代替 |
| act.fall | 观察 | S:None_Falling S:Falling S:Whoa | 跌落者 | 城墙、船缘、坠马等确实离开支撑；尖叫仅重大落差 | 确认下坠后、撞地前 | 垂直位移与腾空边沿；过滤正常跳跃 |
| act.burning | 事件 | S:Burning | 实际承受火伤的角色 | War Sails 原版玩家立于燃烧甲板时有真实扣血路径；可另接明确标记的火场任务演员 | 第一跳实际火伤；离开火源后复位 | AgentNavalComponent 的 RegisterBurnBlow；Mission 的扣血路径绕开 OnScoreHit，原版判据只确认玩家 |
| act.climb | 观察 | S:none_climbing | 登梯者 | 实际在攻城梯、攀登动画或任务梯段 | 进入攀登后第一步，长攀登可稀疏触发 | 使用梯具与动作，不用下达架梯替代 |
| act.siege_effort | 观察 | S:none_siege_charging S:none_siege_release | 器械手 | 实际装填上弦或触发释放，分两个阶段 | 上弦开始或射出 | 器械实例、实际操作者和动作 |
| act.breath | 观察 | S:BreathingStamina S:LowStamina S:BreathingLaboured S:BreathingGrowl S:CatchBreath S:StaminaFail S:ExhaustedBAK | 近处活动者 | 连续快跑、冲锋或反复出力；分持续喘、极度吃力和恢复。是语音条件，不加战斗耐力条 | 活动累计达到拟定门槛后允许喘；停下后可播恢复；离场立即停 | 语音专用活动计量，有距离与同场限额；不能冒充骑砍原生耐力 |
| act.bleed | 专场 | S:Personality_BledOut Auto_DeathGen | 重伤角色 | 明确剧情进入致命失血；无词气绝声可人工复听另归死亡槽 | 剧情确认失血倒下或死亡 | 伤兵救护/战后撤离任务；普通野战没有同款机制 |
| act.wound_limb | 专场 | S:DeathArm S:Crippled Auto_DeathArm Auto_DeathLeg Auto_Crippled | 伤兵 | 明示肢体受创并仍有反应；若对白明确说断肢，必须有匹配演出 | 表演动作或实际事件发生时 | 伤兵现场或受控演练；不能以武器命中骨位宣布断肢 |
| act.wound_arm | 事件 | — | 手臂实际重伤且仍存活的战士 | 确认对应手臂受重伤；只选喊手臂疼、未宣称失去手臂的句子 | 有效命中已结算时；重伤台词让位于致命反应 | OnScoreHit 的有效伤害，按命中骨位复核；只收逐录音指定素材 |
| act.last_resort | 专场 | S:Suicide | 剧情角色 | 有明确且已设计的临终选择；无词短声允许审听后另归真实死亡槽 | 指定剧情镜头或动作 | 禁止作为普通被击倒、撤退或逃跑声 |
| fight.cry | 换声 | Manual_BattleCry Manual_BattleCryRunning Manual_BattleCry_Tenosia | 战士 | 已有战吼；静止和跑动都成立才共池 | 原版请求时 | Yell；带专名另路由 |
| fight.cry_advance | 观察 | Manual_BattleCryRunning Auto_OffenseCharge | 随队战士 | 部署完毕、接受集体冲锋且已在跑向敌阵 | 第一批真正冲起来时每编队选少数人 | 已生效冲锋与移动速度；避开原版冲锋后群响 |
| fight.win_cheer | 换声 | Manual_Cheer | 胜方战士 | 实际获胜或已有主动欢呼 | 原版请求时 | Victory；带专名另路由 |
| fight.result | 事件 | Manual_Cheer Auto_AttackWinning Auto_DefendWinning Auto_DefendWinning_Tenosia | 胜方领主或兵士 | 真正获胜；台词说「我们快赢了」只进入优势场景 | 结果可用后，错开原版欢呼 | OnMissionResultReady；不得拿退出战场当胜利 |
| fight.advantage | 观察 | Auto_AttackWinning Auto_DefendWinning Auto_DefendWinning_Tenosia | 正在带队的指挥者 | 持续交战中获得明确优势，尚未胜利；攻守立场一致 | 稳定由劣势/均势进入优势，仅一次 | 复用已有战场战力信号，阈值后续验证 |
| order.follow | 事件 | Manual_FollowMe | 队长或领主 | 真实跟随指令已生效 | 第一次生效；已有口令则替换同槽 | OrderController 或 AI 编队实际状态 |
| order.advance | 事件 | Manual_Forward Auto_Attack Auto_Attack_Tenosia | 队长或领主 | 部署完毕，真实推进或交战指令 | 状态首次生效 | 编队状态变化；不可把阵型调整当推进 |
| order.charge | 事件 | Auto_OffenseCharge Auto_CavalryCharge | 队长或领主 | 真正冲锋；骑兵台词只给骑兵目标编队 | 冲锋转换后、群声前 | 显式发令与 AI 状态共用判重 |
| order.hold | 事件 | Manual_Hold Auto_Defend Auto_Defend_Tenosia | 防守指挥者 | 实际停住或任务要求守住位置，攻防用词匹配 | 实际进入坚守 | 编队移动状态或脚本守点 |
| order.shields | 事件 | Auto_DefenseShields | 队长或领主 | 有带盾士兵的编队真正切到盾墙 | 阵型实际应用后 | OnAfterArrangementOrderApplied |
| order.regroup | 观察 | Manual_PO4_Reform | 队长或领主 | 失序小队开始向集结位移动或重新成阵 | 确认从散开转入集结 | 有效队长、编队收拢及命令变化；非任意阵型改动 |
| order.retreat | 事件 | Manual_Retreat Manual_Retreat_Tenosia | 队长或领主 | 主动撤出危险区或收到组织撤退；跟个人士气溃逃分开 | 撤退命令首次生效 | 真实编队命令或战术边沿；只选措辞相符的短句 |
| order.fire | 事件 | Auto_OffenseFire | 射手队长 | 本编队确实解除停火，装有弹药且有射界 | 火力命令实际改变 | 观察 FiringOrder 的状态边沿 |
| order.archers | 互动 | Manual_PO3_KillArchers | 指挥者 | 已识别敌方弓弩编队，真实点名或下令 | 确认目标时 | 战术入口；文字提示和实际命令分别处理 |
| fight.prepare | 观察 | Auto_DefensePrepare Auto_DefensePrepare_Tenosia | 防守指挥者 | 防守已就位、敌军逼近且仍未交手 | 敌方首次进预警范围 | 战斗阶段和双方位置 |
| fight.incoming | 观察 | Manual_Incoming Manual_Incoming_Tenosia | 发现者 | 已确认交战对象真正逼近；主动标记可共享此池 | 预警首次成立或主动确认 | 队友可听距离；禁止后台全图喊话 |
| fight.enemy_cavalry | 观察 | Auto_CavalryIncomingEnemy Auto_CavalryIncomingEnemy_Tenosia Auto_CavalryWarning | 观察到的己方战士 | 敌骑集体逼近且有可见威胁 | 第一次进入本队预警范围，退出后才允许再触发 | 队伍级快照：敌对、骑乘、运动方向和距离 |
| fight.ally_cavalry | 事件 | Auto_CavalryIncomingAlly | 已在场的己方观察者 | 实际到场的是本方骑兵增援 | 首批确实入场且进入可见区 | 原版与自建援军批次事件 |
| fight.help | 观察 | Manual_Help | 有队友在近处的受压者 | 受围、退路被挡或守点告急；也可明确主动求援 | 首次进入求援状态或确认轮盘 | 敌我距离、当前状态与冷却；不影响战斗 AI |
| fight.archer_hit | 事件 | Auto_KillArchers | 遭射击的战士 | 敌方弓弩伤害真实发生且未致命 | OnScoreHit 确认后，低频 | 实际伤害和伤源；自己不是发令者时只作反应 |
| fight.kill | 事件 | Auto_Positive | 造成有效击倒的战士或器械操作者 | 敌方确实被杀或击倒，且台词与结果一致 | 结果确认后 | OnAgentRemoved / 器械归属；排除友伤 |
| fight.loss | 事件 | Auto_Negative | 近处目击者或误伤者 | 附近同袍被击倒、或本人误伤，按语义拆池 | 伤亡或误伤实际成立 | OnAgentRemoved 或 OnScoreHit，不能两路重复播 |
| fight.odd_hit | 事件 | Auto_Rage | 受伤者 | 被真踢中、盾击、马撞或特别器械击中，非致命；台词须语义匹配 | 有效结果确认后 | OnScoreHit 或可识别的替代攻击命中；不对所有普通砍击触发 |
| fight.dry | 事件 | Auto_NoAmmo | 射手或实际器械手 | 从有弹变为打空；不把仍上膛或库存可装填算作空 | 最后一发确认后；按空失败另核 | OnAgentShootMissile 后查弹；攻城装填独立 |
| fight.denied | 互动 | Auto_Invalid | 操作者 | 已确认某互动被拒，且此录音适合该拒绝原因 | 游戏反馈拒绝的时点 | 绑定具体的交互或任务，不接通用按键失败 |
| horse.mount | 事件 | Auto_HorseMount S:HorseMount | 上马者 | 真正成为坐骑骑手 | Mount 回调 | 骑乘配对；排除重复挂接 |
| horse.accel | 观察 | Auto_HorseAccelerate | 骑手 | 有骑手的马开始持续加速，且本次速度达到策马条件 | 加速建立后 | 速度/控制状态；与原生 HorseRally 去重 |
| horse.brake | 观察 | Auto_HorseDecelerate Auto_HorseControl S:Whoa | 骑手 | 从高速实际急刹或控马转向；普通减速分开 | 急减速开始，单次 | 骑乘速度变化；不移植原作 800 |
| horse.lost | 事件 | Auto_HorseDeath | 原骑手 | 坐骑确实死亡且该人刚脱离骑乘 | 马匹死亡与配对解绑汇合时 | OnAgentRemoved、骑乘解绑前缓存，不以普通下马代替 |
| horse.calm | 互动 | Auto_HorseSoothe | 驯马人或骑手 | 马厩或任务中与马主动互动，明确执行安抚 | 完成安抚动作时 | 马厩互动脚本；战斗未发现原生安抚入口 |
| siege.gate_attack | 事件 | Auto_GateAttack | 攻方指挥者 | 目标确实是可攻的城门，攻门已经下达 | 对这扇门第一次进入攻门任务 | 编队目标、CastleGate、攻守方 |
| siege.gate_defend | 观察 | Auto_GateDefend Auto_HoldWalls | 守方指挥者 | 正守对应的门或墙段，敌人已威胁该处 | 阶段或敌威胁第一次生效 | 设施/战术位置与双方接近；按门墙分池 |
| siege.gate_falls | 事件 | Auto_GateWon Auto_GateLost | 对应攻方或守方观察者 | 指定城门真正被摧毁 | OnDestroyed 后一次 | 区分开门、内外门和破坏阵营 |
| siege.escort | 观察 | Auto_SiegeDefend Auto_SiegeRam Auto_SiegeTower | 操作组或指挥者 | 真正护送、推进冲车或攻城塔；三者分别匹配 | 本次开始推或遭攻击时 | 设施目标、当前操作者与移动边沿 |
| siege.stop | 事件 | Auto_SiegeDestroy Auto_SiegeRamStop Auto_SiegeTowerStop | 守方队长、器械手或观察者 | 保护对象被毁、真实停推、或器械任务被阻止；不同语义分别取池 | 对应结果第一次成立 | 器械被毁、到站、停止原因；正常停靠不当敌人击毁 |
| siege.fire | 事件 | Auto_SiegeFire | 操作组或发令队长 | 实际装好待发、下令开火或器械真实发射；按语义匹配 | 对应操作边沿 | 器械已装填与实际发射；普通射手排除 |
| naval.phase | 事件 | Manual_Forward Manual_Hold Auto_OffenseCharge Auto_Defend Manual_Cheer | 舰队指挥者或甲板战士 | 命令登船、实际接舷、守桥、夺船成功分别建池；仅收没有陆地限定的短句 | 各航战阶段首次成立 | NavalShipsLogic 的登船命令、接舷、搭桥、夺船事件 |
| mission.burn | 互动 | Auto_ObjBurn | 执行者或任务领队 | 真正的受控点火/纵火任务；目标可被点燃 | 接下焚毁目标，或起火完成，按句义区分 | 任务目标、阵营和火源脚本 |
| mission.target | 互动 | Auto_ObjKill Auto_ObjLordDefend Auto_ObjLordKill Auto_ObjLordKill_Tenosia | 护卫、追捕队长或目击者 | 任务明确指定敌对目标或要保护的领主 | 目标进入战区或保护/攻击命令确认 | 本模组搜捕、护卫或剧情任务专属；不把任何敌方领主都当任务目标 |
| mission.civilians | 互动 | Auto_ObjPeasantDefend Auto_ObjPeasantKill | 护卫或明确剧情中的袭击者 | 同村民保护或袭击目标相符 | 任务目标公布或实际遭袭时 | 灰袍保护台词与敌对剧情角色分池；不让灰袍随机喊杀村民 |
| mission.supplies | 互动 | Auto_ObjSupplyDefend Auto_ObjSupplyDefend_Tenosia Auto_ObjSupplySteal Auto_ObjSupplySteal_Tenosia | 护卫、押运或劫掠任务角色 | 真实物资实体与阵营目标已登记 | 物资受到威胁、夺取开始或完成，按台词区分 | 任务和物资目标状态，不用普通战场箱子代替 |
| rescue.start | 专场 | Auto_Reviving S:Reviving | 救护者 | 伤员仍活着，救护者已经开始扶起/救治 | 救助动作开始 | 新增伤兵撤离任务；只影响任务演员，不改普通战场死亡 |
| rescue.done | 专场 | Auto_Revived S:Revived | 受助者 | 同伴帮助后确实恢复、站起或安全撤离；按台词匹配 | 实际完成且受助者在场 | 救护任务完成状态，不用同伴进入战场或战后菜单代替 |
| rescue.bind | 专场 | Auto_Pain | 正被处理伤口的伤员 | 当场使用布带或明确在处理伤口 | 绷带动作开始 | 救护演出，和被打痛呼分开 |
| rescue.down | 专场 | Manual_AcceptDeath Manual_RefuseDeath Manual_RequestDeath Manual_RequestHelp S:Downed | 仍清醒的重伤者 | 任务控制的倒地演员尚能说话；求救、拒绝、接受各有明确选项 | 落地或选中相应交互 | 独立剧情/救护选择；不复活普通退场的 Agent |
| fight.last_stand | 事件 | — | 触发现有死撑效果且仍在作战的灰袍战士 | 确实进入本模组已有锁血窗口；不能选提到倒地、爬起或被救的台词 | 本次锁血窗口首次成立 | GwpLastStand 的进入边沿；只收逐录音指定素材 |
| act.recover_footing | 观察 | — | 被击倒后仍在场的角色 | 实际受击摔倒并重新站起；不说是同袍救起 | 站稳后，抢不过新痛呼 | 动作通道；只收逐录音指定素材 |
| social.greet | 互动 | Manual_Hello Manual_Welcome | 有对白资格者 | 营地、领主大厅、路遇初见或欢迎重逢 | 符合语义的问候节点实际到达 | 说话人身份、字幕及语言；不覆盖报姓名领地节点 |
| social.farewell | 互动 | Manual_Goodbye | 对话对象 | 对话结束、领主离开或营地告别 | 告别节点确认时 | 对话节点；关闭 UI 不自动当作告别 |
| social.response | 互动 | Manual_Yes Manual_No Manual_Oops | 对话对象或主动发声者 | 明确答应、拒绝或承认小失误 | 玩家到达对应选项或确认互动 | 对话/轮盘，与其他答复分开 |
| social.thanks | 互动 | Manual_Thanks Auto_Thanks | 得到帮助的人 | 同伴或玩家实际完成帮助，或主动感谢 | 助人事实成立或选项确认 | 对话、任务回执；不从闲置源事件反推条件 |
| social.apology | 互动 | Auto_Sorry Manual_Oops | 误伤或做错事的人 | 实际误伤、弄坏任务物件或明确主动道歉 | 友伤/错误确认后，或选项确认 | OnScoreHit 加互动语义；同一友伤只一次 |
| social.respect | 互动 | Manual_PO2_Respect Manual_PO2_Respect_Tenosia | 领主、队长或切磋者 | 战前认可对手、切磋结果后认可技艺 | 确认交涉选项或决斗结果 | 切磋/战前对话；带阵营词隔离 |
| social.rebuke | 互动 | Manual_PO1_Disappointment Manual_PO5_GoAway Manual_PO6_HowDareYou | 适合说这句的对话角色 | 劝退、斥责、失望各自对着明确对象 | 交涉或剧情选项确认 | 不挂到随机路人或所有战败场合 |
| social.taunt | 互动 | Manual_Taunt Manual_Taunt_Tenosia Manual_Laugh | 参战者或剧情角色 | 对可见敌人挑衅，或确有可笑事件 | 主动轮盘确认或演出提示 | 单人互动扩展；闲聊长句与自动战斗分池 |
| social.yield | 互动 | Manual_BegCower Manual_BegCower_Tenosia | 求饶或被俘者 | 投降、释放、关押谈判或有明确退缩动作 | 投降已选择或演出节点到达 | 战前交涉/战后俘虏；勿做普通伤害反应 |
| social.rest | 互动 | Manual_Sit | 可互动角色 | 营地、驻屯或故事场景中真的坐下 | 动作完成或台词节点 | 自定义互动；原作未证实调用不影响此设计 |
| scene.arena | 专场 | Hippodrome | 明确的领队、参赛者或任务人物 | 竞技场或骑术比试开场；只用不违背比试规则的台词 | 比试开场或演出到指定语音标记 | TournamentFightMissionController / TournamentJoustingMissionController 的 StartMatch；不照搬原作时间 |
| scene.arena_threat | 专场 | — | 生死对峙中的参战角色 | 出现明确威吓对象；切磋和训练须是角色演练而非真宣称杀人 | 对峙台词节点 | 比武或任务演出；只收逐录音指定素材 |
| scene.arena_formation | 专场 | — | 正在带队演练的教官 | 队伍确实需要守位或保持间距；按句子分别控制 | 编队就位后首次提醒，或间距被打乱 | 竞技场编队演练；只收逐录音指定素材 |
| scene.preview | 互动 | Customization | 正在展示的角色声组 | 军营试音、百科或声组预览 | 玩家明确选择预览 | 已用于战斗的短句只存一份；预览计作附加入口 |
| scene.monologue | 专场 | Soliloquy | 凡蒂或获授权具名角色 | 夜间扎营、战前巡视、俘虏交涉或剧情独白；逐句匹配 | 玩家明确交谈或进入指定演出 | 不能随机在混战里整段播放 |
| scene.historical_quote | 专场 | — | 明确扮演角色的教官、参演者或讲述者 | 军营战术演练或指定引语； UI/场景明确展示是模拟阵营或原声档案 | 对应训练口令实际发出，或玩家确认听引语 | 新增演练及标识；常规灰袍战斗用中性同池音频或重录 |
| scene.scripted_role | 专场 | — | 与措辞立场相符的任务人物 | 审核后的台词确实描述说话人的行动、对手和任务 | 脚本事实成立或玩家选择时 | 敌方台词、营地表演或任务情节；常规灰袍池排除 |

按录音还有两个相互独立的去向：**原音情境**保留源录音（适合则直接接，专名则在军营演练以模拟阵营/引语/明确播放名牌使用）；**灰袍常规实战版本**允许同声线新录、重配或逐句调词。训练标签只标资产来源，不能替代这两栏的准入判断。现阶段原音回顾入口只是计划内的受控互动，不算已实现的战场覆盖。

落地按信号分批：先做能验证实际变化的战场事件（AI 口令、弹尽、援军、重伤、破门）；再做动作通道和状态观察（松弦、招架、起身、敌骑、急刹、喘息）；然后做有真实任务对象的营地、护送、救护、俘虏和演练。验收每行至少覆盖一个应播和一个应静默的例子，先看触发原因，再听素材与对象是否相符。专场、受控互动和复听都不能计作普通野战已经覆盖。

本地逐录音页从既有音库索引和校对稿生成，不改旧标签、不复制原始音频：

```powershell
python tools/Build-WardenVoiceSceneCatalog.py <media-event-paths.json> <原声校对稿.txt> <Field_Officer音频目录> <输出HTML>
```

脚本交叉核对媒体 ID、原文件名、录音路径、音库关联及一级切换；每条显示可试听原音、目标情境和限制。生成结果仅供分配与审核，不是游戏运行配置。
