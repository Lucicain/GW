using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchBehavior
    {
        private readonly GwpPersuasionRound _hallRound = new GwpPersuasionRound("GWP_CS_HALL");
        private readonly GwpPersuasionRound _choiceRound = new GwpPersuasionRound("GWP_CS_CHOICE");
        // 这三个量只在不可存档的对话里使用，切场景前已经选定，不进入存档。
        private bool _opponentDuel;
        private bool _playerDuel;
        private bool _choicePending;
        private bool _combatChoicePrepared;

        private void RegisterHallDialogue(CampaignGameStarter starter)
        {
            RegisterSupportDialogue(starter);
            starter.AddDialogLine("gwp_cs_hall_start", "start", "gwp_cs_hall_options",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_start), () => Scene?.IsHallTalk == true, null, 220);
            starter.AddPlayerLine("gwp_cs_hall_warrant", "gwp_cs_hall_options", "gwp_cs_hall_next",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_warrant), null, BeginHallPersuasion);
            starter.AddPlayerLine("gwp_cs_hall_leave", "gwp_cs_hall_options", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_leave), null, () => Scene?.LeaveHall());
            starter.AddDialogLine("gwp_cs_hall_surrender", "gwp_cs_hall_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_surrender), () => _hallRound.IsSatisfied,
                () => { _hallRound.End(); Scene?.AcceptHallSurrender(); }, 110);
            starter.AddDialogLine("gwp_cs_hall_reservation", "gwp_cs_hall_next", "gwp_cs_hall_argument",
                "{=!}{GWP_CS_HALL_RESERVATION}", _hallRound.PrepareReservation, null, 105);
            starter.AddDialogLine("gwp_cs_hall_refuse", "gwp_cs_hall_next", "gwp_cs_hall_combat_choice",
                "{=!}{GWP_CS_OPPONENT_CHOICE}", PrepareCombatChoice, () => _hallRound.End(), 100);
            _hallRound.RegisterArguments(starter, "gwp_cs_hall_argument", "gwp_cs_hall_argument", "gwp_cs_hall_reaction");
            starter.AddDialogLine("gwp_cs_hall_reaction", "gwp_cs_hall_reaction", "gwp_cs_hall_next",
                "{=!}{PERSUASION_REACTION}", () => _hallRound.PrepareReaction(
                    GwpTextCatalog.gwp_cs_hall_reaction_success, GwpTextCatalog.gwp_cs_hall_reaction_fail,
                    GwpTextCatalog.gwp_cs_hall_reaction_critical), null);
            // 对面提议单挑时玩家应战；对面提议群殴时玩家改提单挑（用户裁定 2026-10-26）。
            starter.AddPlayerLine("gwp_cs_hall_duel", "gwp_cs_hall_combat_choice", "gwp_cs_hall_choice_next",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_duel), () => _opponentDuel, () => ChooseCombat(true));
            starter.AddPlayerLine("gwp_cs_hall_request_duel", "gwp_cs_hall_combat_choice", "gwp_cs_hall_choice_next",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_request_duel), () => !_opponentDuel, () => ChooseCombat(true));
            starter.AddPlayerLine("gwp_cs_hall_melee", "gwp_cs_hall_combat_choice", "gwp_cs_hall_choice_next",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_melee), null, () => ChooseCombat(false));
            // 对方选了单挑、玩家要群殴：对方是弱势一方，不再谈判，直接开打（用户裁定 2026-10-26）。
            starter.AddDialogLine("gwp_cs_choice_forced_melee", "gwp_cs_hall_choice_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_choice_forced_melee), () => _opponentDuel && !_playerDuel,
                () => Scene?.ChooseHallCombat(false), 125);
            // 双方都选群殴同样不用谈；双方都选单挑见下一行（用户裁定 2026-10-26）。
            starter.AddDialogLine("gwp_cs_choice_agreed_melee", "gwp_cs_hall_choice_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_choice_agreed_melee), () => !_opponentDuel && !_playerDuel,
                () => Scene?.ChooseHallCombat(false), 122);
            starter.AddDialogLine("gwp_cs_choice_agreed", "gwp_cs_hall_choice_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_choice_agreed), () => _opponentDuel && _playerDuel && !UsesHallDuel,
                () => Scene?.ChooseHallCombat(_playerDuel), 120);
            starter.AddDialogLine("gwp_cs_choice_hall_duel", "gwp_cs_hall_choice_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_duel_clear), () => _opponentDuel && _playerDuel && UsesHallDuel,
                () => Scene?.ChooseHallCombat(true), 121);
            starter.AddDialogLine("gwp_cs_choice_hall_duel_won", "gwp_cs_hall_choice_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_duel_clear), () => _choicePending && _choiceRound.IsSatisfied && UsesHallDuel,
                () => { _choiceRound.End(); Scene?.ChooseHallCombat(true); }, 116);
            starter.AddDialogLine("gwp_cs_choice_won", "gwp_cs_hall_choice_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_choice_won), () => _choicePending && _choiceRound.IsSatisfied && !UsesHallDuel,
                () => { _choiceRound.End(); Scene?.ChooseHallCombat(_playerDuel); }, 115);
            starter.AddDialogLine("gwp_cs_choice_reservation", "gwp_cs_hall_choice_next", "gwp_cs_choice_argument",
                "{=!}{GWP_CS_CHOICE_RESERVATION}", () => _choicePending && _choiceRound.PrepareReservation(), null, 110);
            starter.AddDialogLine("gwp_cs_choice_lost", "gwp_cs_hall_choice_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_choice_lost), () => _choicePending && _choiceRound.IsFailed,
                () => { _choiceRound.End(); Scene?.ChooseHallCombat(_opponentDuel); });
            starter.AddDialogLine("gwp_cs_hall_duel_ready", "start", "gwp_cs_hall_duel_ready_options",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_duel_ready), () => Scene?.IsHallDuelTalk == true, null, 240);
            starter.AddPlayerLine("gwp_cs_hall_duel_start", "gwp_cs_hall_duel_ready_options", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_duel_start), null, () => Scene?.ChooseHallDuelStart());
            starter.AddPlayerLine("gwp_cs_hall_duel_wait", "gwp_cs_hall_duel_ready_options", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_duel_wait), null, null);
            _choiceRound.RegisterArguments(starter, "gwp_cs_choice_argument", "gwp_cs_choice_argument", "gwp_cs_choice_reaction");
            starter.AddDialogLine("gwp_cs_choice_reaction", "gwp_cs_choice_reaction", "gwp_cs_hall_choice_next",
                "{=!}{PERSUASION_REACTION}", () => _choiceRound.PrepareReaction(
                    GwpTextCatalog.gwp_cs_hall_reaction_success, GwpTextCatalog.gwp_cs_hall_reaction_fail,
                    GwpTextCatalog.gwp_cs_hall_reaction_critical), null);
        }

        private PersuasionTask HallTask(string line, params GwpPersuasionRound.Argument[] args) =>
            GwpPersuasionRound.Task(GwpText.Create(line), StrengthAgainst(SearchTarget.CharacterObject),
                GwpText.Create(GwpTextCatalog.gwp_cs_guard_fail_immediate), GwpText.Create(GwpTextCatalog.gwp_cs_guard_fail_final),
                GwpText.Create(GwpTextCatalog.gwp_cs_guard_try_later), args);

        private void BeginHallPersuasion()
        {
            _combatChoicePrepared = false;
            PersuasionTask[] tasks =
            {
                HallTask(GwpTextCatalog.gwp_cs_hall_law,
                    new(DefaultSkills.Leadership, DefaultTraits.Honor, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_law)),
                    new(DefaultSkills.Charm, DefaultTraits.Mercy, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_people)),
                    new(DefaultSkills.Roguery, DefaultTraits.Calculating, TraitEffect.Positive, true, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_risk)),
                    new(DefaultSkills.Trade, DefaultTraits.Generosity, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_terms))),
                HallTask(GwpTextCatalog.gwp_cs_hall_safety,
                    new(DefaultSkills.Charm, DefaultTraits.Honor, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_word)),
                    new(DefaultSkills.Tactics, DefaultTraits.Calculating, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_guard)),
                    new(DefaultSkills.Leadership, DefaultTraits.Valor, TraitEffect.Positive, true, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_courage)),
                    new(DefaultSkills.Steward, DefaultTraits.Mercy, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_trial)))
            };
            BeginSearchPersuasion(_hallRound, tasks);
        }

        private bool PrepareCombatChoice()
        {
            if (!_hallRound.IsFailed) return false;
            if (_combatChoicePrepared) return true;
            _combatChoicePrepared = true;
            // 对方先选：按双方在场参战者的原版实力，打不过才选单挑（用户裁定 2026-10-26）。
            _opponentDuel = Scene!.OpponentPrefersDuel();
            MBTextManager.SetTextVariable("GWP_CS_OPPONENT_CHOICE", GwpText.Create(_opponentDuel
                ? GwpTextCatalog.gwp_cs_opponent_duel : GwpTextCatalog.gwp_cs_opponent_melee));
            return true;
        }

        private void ChooseCombat(bool duel)
        {
            _playerDuel = duel;
            // 只有对方占优选了群殴、玩家仍要单挑时才谈；反过来玩家选群殴直接开打。
            _choicePending = duel && !_opponentDuel;
            if (!_choicePending) return;
            BeginSearchPersuasion(_choiceRound, new[] { HallTask(GwpTextCatalog.gwp_cs_choice_reservation,
                new(DefaultSkills.Charm, DefaultTraits.Honor, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_choice_arg_fair)),
                new(DefaultSkills.Leadership, DefaultTraits.Valor, TraitEffect.Positive, true, GwpText.Create(GwpTextCatalog.gwp_cs_choice_arg_brave)),
                new(DefaultSkills.Tactics, DefaultTraits.Calculating, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_choice_arg_losses)),
                new(DefaultSkills.Roguery, DefaultTraits.Honor, TraitEffect.Negative, true, GwpText.Create(GwpTextCatalog.gwp_cs_choice_arg_shame)))
            });
        }
    }
}
