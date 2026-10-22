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
                    GwpTextCatalog.gwp_cs_hall_reaction_success, GwpTextCatalog.gwp_cs_guard_reaction_fail,
                    GwpTextCatalog.gwp_cs_guard_reaction_critical), null);
            starter.AddPlayerLine("gwp_cs_hall_duel", "gwp_cs_hall_combat_choice", "gwp_cs_hall_choice_next",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_duel), null, () => ChooseCombat(true));
            starter.AddPlayerLine("gwp_cs_hall_melee", "gwp_cs_hall_combat_choice", "gwp_cs_hall_choice_next",
                GwpText.Get(GwpTextCatalog.gwp_cs_hall_melee), null, () => ChooseCombat(false));
            starter.AddDialogLine("gwp_cs_choice_agreed", "gwp_cs_hall_choice_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_choice_agreed), () => !_choicePending,
                () => Scene?.ChooseHallCombat(_playerDuel), 120);
            starter.AddDialogLine("gwp_cs_choice_won", "gwp_cs_hall_choice_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_choice_won), () => _choicePending && _choiceRound.IsSatisfied,
                () => { _choiceRound.End(); Scene?.ChooseHallCombat(_playerDuel); }, 115);
            starter.AddDialogLine("gwp_cs_choice_reservation", "gwp_cs_hall_choice_next", "gwp_cs_choice_argument",
                "{=!}{GWP_CS_CHOICE_RESERVATION}", () => _choicePending && _choiceRound.PrepareReservation(), null, 110);
            starter.AddDialogLine("gwp_cs_choice_lost", "gwp_cs_hall_choice_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_choice_lost), () => _choicePending && _choiceRound.IsFailed,
                () => { _choiceRound.End(); Scene?.ChooseHallCombat(_opponentDuel); });
            _choiceRound.RegisterArguments(starter, "gwp_cs_choice_argument", "gwp_cs_choice_argument", "gwp_cs_choice_reaction");
            starter.AddDialogLine("gwp_cs_choice_reaction", "gwp_cs_choice_reaction", "gwp_cs_hall_choice_next",
                "{=!}{PERSUASION_REACTION}", () => _choiceRound.PrepareReaction(
                    GwpTextCatalog.gwp_cs_hall_reaction_success, GwpTextCatalog.gwp_cs_guard_reaction_fail,
                    GwpTextCatalog.gwp_cs_guard_reaction_critical), null);
        }

        private float HallSituation() => Hero.MainHero.GetRelation(SearchTarget) * GwpTuning.CitySearch.RelationStep
            - (Scene?.Settlement.MapFaction.MainHeroCrimeRating ?? 0f) * GwpTuning.CitySearch.CrimeStep
            - GwpTuning.CitySearch.HallPenalty;

        private PersuasionTask HallTask(string line, PersuasionArgumentStrength strength, params GwpPersuasionRound.Argument[] args) =>
            GwpPersuasionRound.Task(GwpText.Create(line), strength,
                GwpText.Create(GwpTextCatalog.gwp_cs_guard_fail_immediate), GwpText.Create(GwpTextCatalog.gwp_cs_guard_fail_final),
                GwpText.Create(GwpTextCatalog.gwp_cs_guard_try_later), args);

        private void BeginHallPersuasion()
        {
            _combatChoicePrepared = false;
            PersuasionArgumentStrength strength = (PersuasionArgumentStrength)((int)GuardStrength(
                GwpFieldArrestBehavior.CurrentStandingTier()) - 1);
            _hallRound.Begin(new[]
            {
                HallTask(GwpTextCatalog.gwp_cs_hall_law, strength,
                    new(DefaultSkills.Leadership, DefaultTraits.Honor, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_law)),
                    new(DefaultSkills.Charm, DefaultTraits.Mercy, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_people)),
                    new(DefaultSkills.Roguery, DefaultTraits.Calculating, TraitEffect.Positive, true, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_risk)),
                    new(DefaultSkills.Trade, DefaultTraits.Generosity, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_terms))),
                HallTask(GwpTextCatalog.gwp_cs_hall_safety, strength,
                    new(DefaultSkills.Charm, DefaultTraits.Honor, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_word)),
                    new(DefaultSkills.Tactics, DefaultTraits.Calculating, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_guard)),
                    new(DefaultSkills.Leadership, DefaultTraits.Valor, TraitEffect.Positive, true, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_courage)),
                    new(DefaultSkills.Steward, DefaultTraits.Mercy, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_hall_arg_trial)))
            }, PersuasionDifficulty.Hard);
        }

        private bool PrepareCombatChoice()
        {
            if (!_hallRound.IsFailed) return false;
            if (_combatChoicePrepared) return true;
            _combatChoicePrepared = true;
            // 对方先选：勇武、荣誉倾向单挑，算计倾向借助卫兵；概率是设计值。
            float duelChance = GwpTuning.CitySearch.DuelBaseChance + GwpTuning.CitySearch.DuelTraitStep
                * (SearchTarget.GetTraitLevel(DefaultTraits.Valor) + SearchTarget.GetTraitLevel(DefaultTraits.Honor)
                    - SearchTarget.GetTraitLevel(DefaultTraits.Calculating));
            _opponentDuel = MBRandom.RandomFloat < Math.Max(0f, Math.Min(1f, duelChance));
            MBTextManager.SetTextVariable("GWP_CS_OPPONENT_CHOICE", GwpText.Create(_opponentDuel
                ? GwpTextCatalog.gwp_cs_opponent_duel : GwpTextCatalog.gwp_cs_opponent_melee));
            return true;
        }

        private void ChooseCombat(bool duel)
        {
            _playerDuel = duel;
            _choicePending = duel != _opponentDuel;
            if (!_choicePending) return;
            _choiceRound.Begin(new[] { HallTask(GwpTextCatalog.gwp_cs_choice_reservation, PersuasionArgumentStrength.Hard,
                new(DefaultSkills.Charm, DefaultTraits.Honor, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_choice_arg_fair)),
                new(DefaultSkills.Leadership, DefaultTraits.Valor, TraitEffect.Positive, true, GwpText.Create(GwpTextCatalog.gwp_cs_choice_arg_brave)),
                new(DefaultSkills.Tactics, DefaultTraits.Calculating, TraitEffect.Positive, false, GwpText.Create(GwpTextCatalog.gwp_cs_choice_arg_losses)),
                new(DefaultSkills.Roguery, DefaultTraits.Honor, TraitEffect.Negative, true, GwpText.Create(GwpTextCatalog.gwp_cs_choice_arg_shame)))
            }, PersuasionDifficulty.Hard);
        }
    }
}
