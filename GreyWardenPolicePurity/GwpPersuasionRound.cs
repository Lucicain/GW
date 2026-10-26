using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 一轮原版说服：若干道关卡，每道四种说法，各道可考不同的技能与性格，由原版掷骰。
    /// 进城搜捕的卫兵盘问和大厅交涉都用它；野外执法有自己的两层谈判，不经过这里。
    /// 对话里用到的文本变量带上各自前缀，两轮说服不会互相覆盖。
    /// </summary>
    internal sealed class GwpPersuasionRound
    {
        private const int Arguments = 4;
        private readonly string _variablePrefix;
        private readonly List<PersuasionTask> _tasks = new List<PersuasionTask>();
        private PersuasionTask? _lastTask;
        private PersuasionOptionArgs? _pending;
        private PersuasionOptionResult _lastResult;

        internal GwpPersuasionRound(string variablePrefix) => _variablePrefix = variablePrefix;

        internal float DifficultyMultiplier { get; private set; }

        /// <summary>原版负责进度、掷骰与特长；本轮的现场难度同时用于界面概率与实际掷骰。</summary>
        internal void Begin(IEnumerable<PersuasionTask> tasks, float difficultyMultiplier)
        {
            End();
            DifficultyMultiplier = difficultyMultiplier;
            _tasks.AddRange(tasks);
            _lastTask = null;
            _pending = null;
            ConversationManager.StartPersuasion(Math.Max(1, _tasks.Count), 1f, 1f, 2f, 2f, 0f, PersuasionDifficulty.Medium);
        }

        internal void End()
        {
            if (ConversationManager.GetPersuasionIsActive())
                ConversationManager.EndPersuasion();
            _tasks.Clear();
        }

        internal bool IsSatisfied =>
            ConversationManager.GetPersuasionIsActive() && ConversationManager.GetPersuasionProgressSatisfied();

        /// <summary>说服没在进行、被当场顶回，或说法用光还没到目标，都算谈崩。对话因此总有出口。</summary>
        internal bool IsFailed =>
            !ConversationManager.GetPersuasionIsActive() || ConversationManager.GetPersuasionIsFailure()
            || (!ConversationManager.GetPersuasionProgressSatisfied() && Current == null);

        internal bool Owns(PersuasionOptionArgs option) =>
            _tasks.Any(task => task.Options.Any(candidate => ReferenceEquals(candidate, option)));

        private PersuasionTask? Current => _tasks.FirstOrDefault(task => !task.Options.All(option => option.IsBlocked));

        internal bool PrepareReservation()
        {
            PersuasionTask? task = Current;
            // 说服没在进行或原版已判失败时不再给下一道顾虑，直接走谈崩。
            if (task == null || IsSatisfied || IsFailed) return false;
            MBTextManager.SetTextVariable(_variablePrefix + "_RESERVATION", task.SpokenLine);
            for (int index = 0; index < Arguments; index++)
                MBTextManager.SetTextVariable(_variablePrefix + "_ARGUMENT_" + index,
                    index < task.Options.Count ? task.Options[index].Line : new TextObject(string.Empty));
            return true;
        }

        /// <summary>注册四条说法：玩家选一条，原版在选完后掷骰并发 PersuasionProgressCommitted。</summary>
        internal void RegisterArguments(CampaignGameStarter starter, string idPrefix, string inputToken, string outputToken)
        {
            for (int index = 0; index < Arguments; index++)
            {
                int captured = index;
                starter.AddPlayerLine(idPrefix + "_" + index, inputToken, outputToken,
                    "{=!}{" + _variablePrefix + "_ARGUMENT_" + index + "}",
                    () => Current is { } task && captured < task.Options.Count && !task.Options[captured].IsBlocked,
                    () =>
                    {
                        PersuasionTask? task = Current;
                        if (task == null || captured >= task.Options.Count) return;
                        _lastTask = task;
                        _pending = task.Options[captured];
                    },
                    100, null,
                    () => Current is { } task && captured < task.Options.Count ? task.Options[captured] : null);
            }
        }

        internal void OnCommitted(Tuple<PersuasionOptionArgs, PersuasionOptionResult> outcome)
        {
            if (_pending == null || !ReferenceEquals(outcome.Item1, _pending)) return;
            PersuasionOptionArgs option = _pending;
            _pending = null;
            _lastResult = outcome.Item2;
            if (_lastResult == PersuasionOptionResult.CriticalFailure)
                foreach (PersuasionTask task in _tasks) task.BlockAllOptions();
            else if (option.CanMoveToTheNextReservation &&
                     (_lastResult == PersuasionOptionResult.Success || _lastResult == PersuasionOptionResult.CriticalSuccess))
                _lastTask?.BlockAllOptions();
        }

        /// <summary>对方对刚才那句话的反应：成功、顶回、或被激怒。</summary>
        internal bool PrepareReaction(string successKey, string failureKey, string criticalKey)
        {
            MBTextManager.SetTextVariable("PERSUASION_REACTION", GwpText.Create(_lastResult switch
            {
                PersuasionOptionResult.CriticalFailure => criticalKey,
                PersuasionOptionResult.Failure or PersuasionOptionResult.Miss => failureKey,
                _ => successKey
            }));
            return true;
        }

        internal string LastResult => _lastResult.ToString();

        /// <summary>一种说法：考哪项技能、哪种性格（正向或反向），成功时是否算大成功。</summary>
        internal sealed record Argument(SkillObject Skill, TraitObject Trait, TraitEffect Effect,
            bool GivesCriticalSuccess, TextObject Line);

        /// <summary>一道关卡：对方的一句顾虑，玩家四种说法，每种说法的强弱由调用方按对象算。最后一个参数让说服成功后进下一道。</summary>
        internal static PersuasionTask Task(TextObject spoken, Func<Argument, PersuasionArgumentStrength> strength,
            TextObject immediateFail, TextObject finalFail, TextObject tryLater, params Argument[] arguments)
        {
            var task = new PersuasionTask(0)
            {
                SpokenLine = spoken,
                ImmediateFailLine = immediateFail,
                FinalFailLine = finalFail,
                TryLaterLine = tryLater
            };
            foreach (Argument argument in arguments.Take(Arguments))
                task.AddOptionToTask(new PersuasionOptionArgs(argument.Skill, argument.Trait, argument.Effect,
                    strength(argument), argument.GivesCriticalSuccess, argument.Line, null, false, true));
            return task;
        }
    }
}
