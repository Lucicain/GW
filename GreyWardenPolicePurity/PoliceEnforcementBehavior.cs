using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 警察惩戒系统
    ///
    /// 玩家被抓完整流程：
    /// 1. OnMapEventEnded：标记 IsEscortingPlayer，冻结AI，SetMoveGoToSettlement 朝最近城堡行进
    /// 2. UpdateTasks（每小时）：维持AI冻结 + 重发行进命令（防止引擎覆盖）
    /// 3. OnTick（每帧）：距城堡 &lt; EscortPunishDistance(3格) 时执行惩罚；
    ///    CurrentSettlement!=null 为兜底防崩溃路径
    /// 4. ExecutePunishment：★关键顺序★ 先 EndCaptivity + 清空花名册，
    ///    再 MakePeace，避免 SetNeutral 内部触发二次释放导致崩溃
    /// </summary>
    public class PoliceEnforcementBehavior : CampaignBehaviorBase
    {
        private const float WarDistance = 3f;
        private const float WarDistancePlayer = 15f;

        // 距城堡多少格时触发惩罚。
        // 3格 > 引擎自动入城触发距离（约1-2格），确保 OnTick 距离判断先于自动入城发生。
        // 兜底：CurrentSettlement != null 紧急检测防止极端情况下的崩溃。
        private const float EscortPunishDistance = 3f;

        // 对话临时变量（警察执法拦截：让玩家选择缴纳或战斗）
        private int _dialogFine = 0;
        private MobileParty _dialogPolice = null;
        private PoliceTask _dialogTask = null;

        public override void RegisterEvents()
        {
            PoliceCrimeMonitorEnhanced.OnCrimeDetected += HandleCrimeDetected;
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        }

        public override void SyncData(IDataStore dataStore) => CrimePool.SyncData(dataStore);

        #region 对话系统（执法拦截：玩家可选择缴纳罚金或战斗）

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // 警察执法开场白（高优先级100，确保覆盖默认对话）
            starter.AddDialogLine(
                "gwp_enforcement_start",
                "start",
                "gwp_enforcement_options",
                "{GWP_ENFORCEMENT_GREETING}",
                EnforcementDialogCondition,
                null,
                100);

            // 选项1：缴纳罚金
            starter.AddPlayerLine(
                "gwp_enforcement_pay",
                "gwp_enforcement_options",
                "gwp_enforcement_pay_response",
                "{GWP_ENFORCEMENT_PAY_TEXT}",
                EnforcementPayCondition,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_response",
                "gwp_enforcement_pay_response",
                "close_window",
                "明智之举。灰袍守卫将撤销对你的通缉，你可以离开了。",
                null,
                OnEnforcementPayConsequence,
                100);

            // 选项2：拒绝，武力解决
            starter.AddPlayerLine(
                "gwp_enforcement_fight",
                "gwp_enforcement_options",
                "gwp_enforcement_fight_response",
                "你来试试！",
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_fight_response",
                "gwp_enforcement_fight_response",
                "close_window",
                "你自找的！灰袍守卫，准备战斗！",
                null,
                OnEnforcementFightConsequence,
                100);
        }

        /// <summary>
        /// 对话触发条件：对话方是正式警察部队（gw家族，非纠察队前缀）
        /// 且该部队有针对玩家的任务、尚未宣战
        /// </summary>
        private bool EnforcementDialogCondition()
        {
            MobileParty conversationParty = MobileParty.ConversationParty;
            if (conversationParty == null) return false;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return false;

            // 只处理正式警察部队（gw家族，非纠察队 gwp_patrol_ 前缀）
            if (conversationParty.ActualClan != policeClan) return false;
            if (GwpCommon.IsPatrolParty(conversationParty)) return false;

            // 该警察必须有针对玩家的任务，且尚未宣战（宣战后走战斗流程）
            var task = CrimePool.GetTask(conversationParty.StringId);
            if (task == null) return false;
            if (task.TargetCrime?.Offender?.IsMainParty != true) return false;
            if (task.WarDeclared) return false;
            if (task.IsEscortingPlayer) return false;

            int rep = PlayerBehaviorPool.Reputation;
            _dialogFine = Math.Abs(rep) * 300;
            _dialogPolice = conversationParty;
            _dialogTask = task;

            int playerGold = Hero.MainHero.Gold;
            string payInfo = playerGold >= _dialogFine
                ? $"你有 {playerGold} 金，足够缴纳。"
                : $"你只有 {playerGold} 金，不足全额，差额将没收行李中的物品充抵。";

            MBTextManager.SetTextVariable("GWP_ENFORCEMENT_GREETING",
                $"站住！灰袍守卫奉命逮捕你！你有 {Math.Abs(rep)} 条严重违法记录，" +
                $"可缴纳 {_dialogFine} 金消除通缉，否则我们将动用武力！{payInfo}");

            return true;
        }

        /// <summary>
        /// 缴纳按钮条件：同时动态设置按钮文本
        /// </summary>
        private bool EnforcementPayCondition()
        {
            int playerGold = Hero.MainHero.Gold;
            string text = playerGold >= _dialogFine
                ? $"我愿意缴纳罚金（{_dialogFine} 金）"
                : $"我愿意缴纳所有金币并以物品充抵（共 {_dialogFine} 金）";
            MBTextManager.SetTextVariable("GWP_ENFORCEMENT_PAY_TEXT", text);
            return true;
        }

        /// <summary>
        /// 缴纳结果：扣款 + 声望归零 + 任务结束 + 和平
        /// </summary>
        private void OnEnforcementPayConsequence()
        {
            try
            {
                int paid = PoliceResourceManager.CollectFine(_dialogFine);

                PlayerBehaviorPool.ResetReputation(0);
                CrimePool.EndPlayerHunt();

                if (_dialogPolice != null && _dialogPolice.IsActive)
                {
                    RestoreAi(_dialogPolice);
                    PoliceResourceManager.StartResupply(_dialogPolice);
                    // ★ 修复 Bug 2（真正原因）：StartResupply 只标记，不立即发移动命令（等每小时 tick）
                    // 警察停在玩家接触范围内 → 遭遇结束后立刻重新触发 → 对话循环
                    // 修复：立即 SetMoveGoToSettlement，模式与 ReturnAllPatrols() 一致
                    PoliceResourceManager.ForceImmediateMoveToResupply(_dialogPolice);
                }

                MakePeaceWithPoliceAndVictims();

                InformationManager.DisplayMessage(new InformationMessage(
                    $"缴纳罚金 {paid} 金，通缉已解除",
                    Colors.Yellow));

                try
                {
                    if (PlayerEncounter.IsActive)
                    {
                        PlayerEncounter.LeaveEncounter = true;  // ★ 修复 Bug 2：主动点击触发的遭遇需显式设置，否则引擎重新触发对话
                        PlayerEncounter.Finish(false);
                    }
                }
                catch { }
            }
            catch { }
            finally
            {
                _dialogFine = 0;
                _dialogPolice = null;
                _dialogTask = null;
            }
        }

        /// <summary>
        /// 拒绝结果：宣战 + 警察立即接战
        /// </summary>
        private void OnEnforcementFightConsequence()
        {
            try
            {
                if (_dialogTask != null && MobileParty.MainParty != null)
                    DeclareWar(_dialogTask, MobileParty.MainParty);

                if (_dialogPolice != null && _dialogPolice.IsActive)
                    _dialogPolice.SetMoveEngageParty(MobileParty.MainParty, NavigationType.Default);

                InformationManager.DisplayMessage(new InformationMessage(
                    "你拒绝缴纳！灰袍守卫准备强制执法！",
                    Colors.Red));
            }
            catch { }
            finally
            {
                _dialogFine = 0;
                _dialogPolice = null;
                _dialogTask = null;
            }
        }

        #endregion

        #region 遭遇拦截（强制对话，在宣战前让玩家选择）

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null) return;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            // 警察接近玩家（警察是进攻方）→ 强制对话
            bool policeHasPlayerTask = false;
            bool playerInvolved = false;

            foreach (var p in mapEvent.InvolvedParties)
            {
                if (p?.MobileParty == null) continue;

                if (p.MobileParty.IsMainParty)
                {
                    playerInvolved = true;
                    continue;
                }

                // 正式警察部队，且有玩家任务、尚未宣战、非押送中
                var task = CrimePool.GetTask(p.MobileParty.StringId);
                if (task != null &&
                    task.TargetCrime?.Offender?.IsMainParty == true &&
                    !task.WarDeclared &&
                    !task.IsEscortingPlayer &&
                    p.MobileParty.ActualClan == policeClan)
                {
                    policeHasPlayerTask = true;
                }
            }

            if (!policeHasPlayerTask || !playerInvolved) return;

            // 尚未宣战时，强制进入对话模式（让玩家选择缴纳或战斗，而非直接开打）
            IFaction pf = Clan.PlayerClan?.MapFaction;
            bool atWar = pf != null && FactionManager.IsAtWarAgainstFaction(policeClan, pf);

            if (!atWar && PlayerEncounter.IsActive && PlayerEncounter.EncounteredParty != null)
            {
                try { PlayerEncounter.DoMeeting(); } catch { }
            }
        }

        #endregion

        #region 犯罪通知

        private void HandleCrimeDetected(string crimeType, MobileParty offender, Vec2 location, string victimName)
        {
            CrimePool.TryAdd(crimeType, offender, location, victimName);
        }

        #endregion

        #region 每帧检查 - 距城堡距离触发惩罚

        private void OnTick(float dt)
        {
            try
            {
                if (!PlayerCaptivity.IsCaptive) return;

                // 确认玩家被警察俘虏
                PartyBase captorParty = PlayerCaptivity.CaptorParty;
                if (captorParty == null) return;

                Clan policeClan = PoliceStats.GetPoliceClan();
                if (policeClan == null) return;

                bool isCapturedByPolice = false;
                if (captorParty.IsMobile && captorParty.MobileParty?.ActualClan == policeClan)
                    isCapturedByPolice = true;
                if (captorParty.IsSettlement && captorParty.Settlement?.OwnerClan == policeClan)
                    isCapturedByPolice = true;

                if (!isCapturedByPolice) return;

                // 找到押送任务
                var escortTask = CrimePool.ActiveTasks.Values.FirstOrDefault(t =>
                    t.IsEscortingPlayer &&
                    t.TargetCrime?.Offender?.IsMainParty == true);

                if (escortTask == null) return;

                var policeParty = MobileParty.All.FirstOrDefault(p => p.StringId == escortTask.PolicePartyId);
                if (policeParty == null || !policeParty.IsActive) return;

                // 紧急检测：若警察已进入任意定居点（引擎自动进城），立即执行惩罚。
                // 必须在引擎的俘虏交付逻辑运行前清空花名册，否则崩溃。
                if (policeParty.CurrentSettlement != null)
                {
                    ExecutePunishment(policeParty.CurrentSettlement, escortTask);
                    return;
                }

                Settlement castle = escortTask.EscortSettlement;
                if (castle == null) return;

                // 正常触发路径：警察通过混合寻路接近城堡，距离 < EscortPunishDistance 时执行惩罚。
                // 近距离段用 GatePosition 直线导航，不触发自动入城，确保此距离判断先于入城发生。
                float distToCastle = policeParty.GetPosition2D.Distance(castle.GetPosition2D);
                if (distToCastle < EscortPunishDistance)
                {
                    ExecutePunishment(castle, escortTask);
                }
            }
            catch { }
        }

        /// <summary>
        /// 执行惩罚（距城堡3格内触发，或进入定居点时紧急触发）。
        ///
        /// ★关键顺序★：先 EndCaptivity + 清空花名册，再 MakePeaceWithPoliceAndVictims。
        ///
        /// 原因：Bannerlord 的 FactionManager.SetNeutral 内部会触发自动释放俘虏逻辑。
        /// 若玩家仍在花名册中时 SetNeutral 被调用，引擎会尝试"释放"已被
        /// EndCaptivity 管理的玩家，造成状态不一致，警察进城补给时或退城时崩溃。
        /// 先清理俘虏状态，再和平，彻底消除双重释放隐患。
        /// </summary>
        private void ExecutePunishment(Settlement castle, PoliceTask escortTask)
        {
            try
            {
                // 提前获取警察部队引用（任务被移除后局部变量仍有效）
                var policeParty = MobileParty.All.FirstOrDefault(p => p.StringId == escortTask.PolicePartyId);

                // ★步骤1★ 先释放玩家（设 IsCaptive = false，移除玩家主英雄的俘虏状态）
                try { if (PlayerCaptivity.IsCaptive) PlayerCaptivity.EndCaptivity(); } catch { }

                // ★步骤1b★ 传送玩家到城堡大门（视觉效果：玩家被"押进"城堡）
                // 必须在 EndCaptivity 之后、花名册清理之前，此时玩家党派已脱离俘虏链
                try
                {
                    Settlement teleportTarget = castle ?? escortTask?.EscortSettlement ?? FindNearestCastle();
                    if (teleportTarget != null && MobileParty.MainParty != null)
                    {
                        MobileParty.MainParty.Position = teleportTarget.GatePosition;
                    }
                }
                catch { }

                // ★步骤2★ 强制清空花名册（防止 EndCaptivity 未完全清理，
                //          后续补给进城时引擎再次处理"主英雄俘虏"导致崩溃）
                try
                {
                    if (policeParty != null && policeParty.IsActive
                        && policeParty.PrisonRoster.TotalManCount > 0)
                    {
                        policeParty.PrisonRoster.Clear();
                    }
                }
                catch { }

                // ★步骤3★ 现在玩家已完全释放，再调用和平（SetNeutral 不会二次触发释放）
                MakePeaceWithPoliceAndVictims();

                // 步骤4：罚款（每点300金，不足时没收行李物品充抵）
                int rep = PlayerBehaviorPool.Reputation;
                int fine = Math.Abs(rep) * 300;
                int collected = PoliceResourceManager.CollectFine(fine);

                // 步骤5：声望归零 + 解除通缉（从 ActiveTasks 移除任务）
                PlayerBehaviorPool.ResetReputation(0);
                CrimePool.EndPlayerHunt();

                // 步骤6：显示消息
                string castleName = castle?.Name?.ToString() ?? "堡垒";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"你被押送至 {castleName}，缴纳罚款 {collected} 金，通缉已解除",
                    Colors.Yellow));

                // 步骤7：恢复警察AI，开始补给
                //（此时花名册已清空，进城补给时 ReleasePrisoners 不会遇到玩家俘虏）
                if (policeParty != null && policeParty.IsActive)
                {
                    RestoreAi(policeParty);
                    PoliceResourceManager.StartResupply(policeParty);
                }

                // 步骤8：安全调用 EndTask（EndPlayerHunt 已移除任务，此处幂等）
                CrimePool.EndTask(escortTask.PolicePartyId);
            }
            catch { }
        }

        #endregion

        #region 每小时

        private void OnHourlyTick()
        {
            CrimePool.Clean();
            AssignTasks();
            UpdateTasks();
            CrimePool.RefreshAccepting();
        }

        private void AssignTasks()
        {
            foreach (var pp in PoliceStats.GetAllPoliceParties())
            {
                // ★ 兜底：跳过无首领或首领失效的部队（防止因纠察队/英雄失效导致无首领部队接任务）
                if (pp.LeaderHero == null || !pp.LeaderHero.IsActive) continue;
                if (CrimePool.HasTask(pp.StringId)) continue;
                if (!PoliceResourceManager.IsReady(pp)) continue;

                CrimeRecord crime = CrimePool.GetNearest(pp.GetPosition2D);
                if (crime == null) continue;

                BeginTask(pp, crime);
            }
        }

        private void BeginTask(MobileParty police, CrimeRecord crime)
        {
            CrimePool.BeginTask(police.StringId, crime);

            police.SetMoveEngageParty(crime.Offender, NavigationType.Default);
            police.Ai.SetDoNotMakeNewDecisions(true);
            police.Ai.SetInitiative(1f, 0f, 999f);

            // 内部调度日志（开发调试）
            // InformationManager.DisplayMessage(new InformationMessage(
            //     $"[GWP 出警] {police.Name} → {crime.Offender.Name}（{crime.CrimeType}）",
            //     Colors.Cyan));
        }

        private void UpdateTasks()
        {
            foreach (var kvp in CrimePool.ActiveTasks.ToList())
            {
                var task = kvp.Value;
                var pp = MobileParty.All.FirstOrDefault(p => p.StringId == task.PolicePartyId);

                if (pp == null || !pp.IsActive)
                {
                    CrimePool.EndTask(kvp.Key);
                    if (task.IsTargetValid()) Reassign(task.TargetCrime);
                    continue;
                }

                // ★ 兜底：任务进行中首领失效（被俘/死亡）→ 结束任务，案件归池
                if (pp.LeaderHero == null || !pp.LeaderHero.IsActive)
                {
                    CrimePool.EndTask(kvp.Key);
                    if (task.IsTargetValid()) Reassign(task.TargetCrime);
                    continue;
                }

                // ★ 正在为玩家悬赏护送时，完全由 PlayerBountyBehavior 管理此部队的 AI，跳过。
                if (task.IsPlayerBountyEscort)
                    continue;

                // 押送阶段：冻结AI，每小时重发行军命令（防止引擎覆盖方向）
                if (task.IsEscortingPlayer)
                {
                    pp.Ai.SetDoNotMakeNewDecisions(true);

                    // 安全网：若玩家已被外部机制提前释放（例如某段和平逻辑绕过了守卫），
                    // 仍执行惩罚以确保罚款和声望清零。
                    // ExecutePunishment 内部先检查 IsCaptive 再调用 EndCaptivity，安全。
                    if (!PlayerCaptivity.IsCaptive)
                    {
                        ExecutePunishment(task.EscortSettlement, task);
                        continue;
                    }

                    if (task.EscortSettlement != null)
                    {
                        // 每小时重发行进命令（防止引擎覆盖），地形寻路朝城堡行进
                        pp.SetMoveGoToSettlement(task.EscortSettlement, NavigationType.Default, false);
                    }
                    continue;
                }

                // 食物耗尽 → 案件归池，前往补给，等补完后重新接案
                if (pp.ItemRoster.TotalFood <= 0)
                {
                    // 警察部队粮草耗尽内部运营（不显示给玩家）
                    // InformationManager.DisplayMessage(new InformationMessage(
                    //     $"[GWP] {pp.Name} 粮草耗尽，案件归池，前往补给", Colors.Yellow));
                    RestoreAi(pp);
                    CrimePool.EndTask(kvp.Key);
                    if (task.IsTargetValid()) Reassign(task.TargetCrime);
                    PoliceResourceManager.StartResupply(pp);
                    continue;
                }

                if (!task.IsTargetValid())
                {
                    RestoreAi(pp);
                    CrimePool.EndTask(kvp.Key);
                    PoliceResourceManager.StartResupply(pp);
                    continue;
                }

                // 正常追击
                MobileParty criminal = task.TargetCrime.Offender;
                float dist = pp.GetPosition2D.Distance(criminal.GetPosition2D);

                float warDist = criminal.IsMainParty ? WarDistancePlayer : WarDistance;

                bool isPatrolRange = criminal.IsMainParty &&
                    PlayerBehaviorPool.Reputation >= -4 &&
                    PlayerBehaviorPool.Reputation <= -1;

                // 玩家目标不自动宣战——改由对话系统让玩家选择缴纳或战斗。
                // 只有玩家在对话中选择"战斗"后（OnEnforcementFightConsequence）才宣战。
                // 非玩家目标仍在接近时自动宣战。
                if (!task.WarDeclared && dist < warDist && !isPatrolRange && !criminal.IsMainParty)
                {
                    DeclareWar(task, criminal);
                }

                try
                {
                    pp.SetMoveEngageParty(criminal, NavigationType.Default);
                }
                catch { }
            }
        }

        private void DeclareWar(PoliceTask task, MobileParty criminal)
        {
            try
            {
                Clan policeClan = PoliceStats.GetPoliceClan();
                if (policeClan == null) return;

                Clan criminalClan = criminal.ActualClan;
                if (criminalClan == null) return;

                task.WarDeclared = true;

                if (criminalClan.IsOutlaw && criminalClan.IsBanditFaction) return;

                IFaction target = criminalClan.MapFaction;
                if (target == null) return;

                if (target == policeClan || target == policeClan.MapFaction) return;

                task.WarTarget = target;

                if (!FactionManager.IsAtWarAgainstFaction(policeClan, target))
                {
                    FactionManager.DeclareWar(policeClan, target);
                }
            }
            catch { }
        }

        #endregion

        #region 战斗结束

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null) return;
            if (!mapEvent.IsFieldBattle) return;

            foreach (var kvp in CrimePool.ActiveTasks.ToList())
            {
                var task = kvp.Value;
                if (!task.WarDeclared) continue;

                var pp = MobileParty.All.FirstOrDefault(p => p.StringId == task.PolicePartyId);

                if (pp == null)
                {
                    if (!InEvent(task.TargetCrime.Offender, mapEvent)) continue;
                    CrimePool.EndTask(kvp.Key);
                    Reassign(task.TargetCrime);
                    CrimePool.RefreshAccepting();
                    continue;
                }

                if (!InEvent(pp, mapEvent)) continue;
                if (!InEvent(task.TargetCrime.Offender, mapEvent)) continue;

                bool policeWon = IsOnWinningSide(pp, mapEvent);

                if (policeWon)
                {
                    // ★关键修复★：不能用 CrimePool.IsPlayerCrime() 判断——
                    // 玩家被击败后 MainParty.IsActive == false，
                    // IsPlayerCrime 内部调用 IsOffenderValid() → Offender.IsActive → false，
                    // 导致误判为非玩家犯罪，走错路径（StartResupply → 进城补给 → 崩溃）。
                    // 改用 Offender.IsMainParty 直接判断，不依赖 IsActive。
                    if (task.TargetCrime?.Offender?.IsMainParty == true)
                    {
                        // 玩家被击败 → 押送至最近城堡（IsCastle）→ OnTick 距离触发惩罚
                        task.IsEscortingPlayer = true;

                        Settlement targetCastle = FindNearestCastle();
                        task.EscortSettlement = targetCastle;

                        // 冻结AI，地形寻路朝城堡行进
                        pp.Ai.SetDoNotMakeNewDecisions(true);
                        if (targetCastle != null)
                        {
                            pp.SetMoveGoToSettlement(targetCastle, NavigationType.Default, false);
                        }

                        string castleName = targetCastle?.Name?.ToString() ?? "堡垒";
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"你被 {pp.Name} 击败！正被押送至 {castleName}...",
                            Colors.Yellow));

                        continue;
                    }

                    RestoreAi(pp);
                    CrimePool.EndTask(kvp.Key);
                    PoliceResourceManager.StartResupply(pp);
                }
                else
                {
                    RestoreAi(pp);
                    CrimePool.EndTask(kvp.Key);
                    Reassign(task.TargetCrime);
                    PoliceResourceManager.StartResupply(pp);
                }

                CrimePool.RefreshAccepting();
            }
        }

        #endregion

        #region 辅助

        private bool InEvent(MobileParty party, MapEvent mapEvent)
        {
            if (party == null || mapEvent == null) return false;
            return mapEvent.InvolvedParties.Any(p => p.MobileParty == party);
        }

        private bool IsOnWinningSide(MobileParty party, MapEvent mapEvent)
        {
            if (!mapEvent.HasWinner || mapEvent.Winner == null) return false;

            foreach (var p in mapEvent.Winner.Parties)
            {
                if (p?.Party?.IsMobile == true && p.Party.MobileParty == party)
                    return true;
            }
            return false;
        }

        private void RestoreAi(MobileParty party)
        {
            if (party == null || !party.IsActive) return;
            try
            {
                party.Ai.SetDoNotMakeNewDecisions(false);
                party.Ai.SetInitiative(0f, 0f, 0f);
            }
            catch { }
        }

        private void MakePeaceWithPoliceAndVictims()
        {
            try
            {
                IFaction playerFaction = Clan.PlayerClan?.MapFaction;
                if (playerFaction == null) return;

                Clan policeClan = PoliceStats.GetPoliceClan();
                GwpCommon.TrySetNeutral(policeClan, playerFaction);

                foreach (var victim in PlayerBehaviorPool.VictimFactions)
                {
                    if (victim == null || victim == playerFaction) continue;
                    if (!FactionManager.IsAtWarAgainstFaction(playerFaction, victim)) continue;

                    try
                    {
                        MakePeaceAction.Apply(playerFaction, victim);
                    }
                    catch { }
                }

                PlayerBehaviorPool.ClearVictimFactions();
            }
            catch { }
        }

        private Settlement FindNearestTown()
        {
            var player = MobileParty.MainParty;
            if (player == null) return null;

            Vec2 pos = player.GetPosition2D;
            Settlement best = null;
            float bestDist = float.MaxValue;

            foreach (Settlement s in Settlement.All)
            {
                if (!s.IsTown) continue;
                float d = pos.Distance(s.GetPosition2D);
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        /// <summary>
        /// 查找玩家附近最近的城堡（严格使用 IsCastle）。
        ///
        /// 修复说明：原 FindNearestFortress 使用 (!s.IsCastle &amp;&amp; !s.IsFortification) 条件，
        /// 但 IsFortification 在 Bannerlord 中对城镇和城堡均为 true，
        /// 导致函数实际上也会返回城镇，警察带着俘虏进城触发引擎崩溃。
        /// 现在只用 IsCastle 精确匹配，城堡通常不允许非所有者自由进出。
        /// </summary>
        private Settlement FindNearestCastle()
        {
            var player = MobileParty.MainParty;
            if (player == null) return null;

            Vec2 pos = player.GetPosition2D;
            Settlement best = null;
            float bestDist = float.MaxValue;

            foreach (Settlement s in Settlement.All)
            {
                if (!s.IsCastle) continue;  // 只选城堡，IsFortification 会误包含城镇
                float d = pos.Distance(s.GetPosition2D);
                if (d < bestDist) { bestDist = d; best = s; }
            }

            // 极端情况：地图上找不到城堡，降级用城镇
            if (best == null)
                best = FindNearestTown();

            return best;
        }

        private void Reassign(CrimeRecord crime)
        {
            CrimePool.TryAdd(crime.CrimeType, crime.Offender, crime.Location, crime.VictimName);
        }

        #endregion
    }
}
