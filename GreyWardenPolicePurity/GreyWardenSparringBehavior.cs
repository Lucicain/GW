using System;
using System.Collections.Generic;
using System.Linq;
using SandBox;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.MissionSpawnHandlers;
using TaleWorlds.MountAndBlade.Source.Missions;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Dialogue entry and deferred mission launcher for friendly practice
    /// bouts with Grey Warden lords. Town challenges use the stock arena duel;
    /// field challenges use the local battle scene without creating a MapEvent.
    /// </summary>
    public sealed class GreyWardenSparringBehavior : CampaignBehaviorBase
    {
        private static GreyWardenSparringBehavior? _activeBehavior;

        private enum PendingSparringKind
        {
            None,
            TownArena,
            Field
        }

        private enum PostBoutConversationKind
        {
            None,
            Town,
            Field
        }

        private PendingSparringKind _pendingKind;
        private Hero? _pendingOpponent;
        private MobileParty? _pendingOpponentParty;
        private Settlement? _pendingSettlement;
        private Location? _pendingArena;
        private Hero? _pendingPostBoutOpponent;
        private Settlement? _pendingPostBoutSettlement;
        private string _pendingPostBoutConversationScene = string.Empty;
        private PostBoutConversationKind _pendingPostBoutKind;
        private bool _pendingPostBoutPlayerWon;
        private bool _pendingPostBoutRuleViolation;
        private bool _postBoutConversationQueued;
        private bool _postBoutConversationActive;

        public override void RegisterEvents()
        {
            _activeBehavior = this;
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this,
                OnSessionLaunched);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(
                this,
                OnMapEventStarted);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // A pending bout exists only while the conversation mission closes;
            // it is intentionally not persisted into a campaign save.
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            ClearPendingSparring();
            ClearPostBoutConversation();

            starter.AddPlayerLine(
                "gwp_sparring_challenge",
                "lord_talk_speak_diplomacy_2",
                "gwp_sparring_accept",
                GwpText.Get(
                    "{=gwp_sparring_challenge}Would you like to spar with me? I want to test my skill."),
                CanChallengeConversationHero,
                null,
                115);

            starter.AddDialogLine(
                "gwp_sparring_accept",
                "gwp_sparring_accept",
                "close_window",
                GwpText.Get(
                    "{=gwp_sparring_accept}Of course. Just a friendly match."),
                null,
                QueueSparring,
                115);

            starter.AddDialogLine(
                "gwp_sparring_field_centre",
                "start",
                "gwp_sparring_field_style",
                GwpText.Get(
                    "{=gwp_sparring_field_centre}How do you want to fight?"),
                IsFieldStartConversation,
                null,
                200);

            starter.AddPlayerLine(
                "gwp_sparring_field_mounted",
                "gwp_sparring_field_style",
                "close_window",
                GwpText.Get(
                    "{=gwp_sparring_field_mounted}Let's fight on horseback."),
                CanChooseMountedFieldDuel,
                BeginMountedFieldDuelAfterConversation,
                200);

            starter.AddPlayerLine(
                "gwp_sparring_field_foot",
                "gwp_sparring_field_style",
                "close_window",
                GwpText.Get(
                    "{=gwp_sparring_field_foot}Let's fight on foot."),
                null,
                BeginFootFieldDuelAfterConversation,
                200);

            starter.AddDialogLine(
                "gwp_sparring_field_post_win",
                "start",
                "gwp_sparring_field_post_reply",
                GwpText.Get(
                    "{=gwp_sparring_field_post_win}You fought well and earned that win. Keep it up."),
                IsFieldPostBoutWinConversation,
                null,
                250);

            starter.AddDialogLine(
                "gwp_sparring_field_post_rule_violation",
                "start",
                "gwp_sparring_field_post_rule_reply",
                GwpText.Get(
                    "{=gwp_sparring_field_post_rule_violation}We agreed to fight on foot, but you refused to dismount. The loss is yours."),
                IsFieldPostBoutRuleViolationConversation,
                null,
                260);

            starter.AddPlayerLine(
                "gwp_sparring_field_post_rule_reply",
                "gwp_sparring_field_post_rule_reply",
                "close_window",
                GwpText.Get(
                    "{=gwp_sparring_field_post_rule_reply}Understood."),
                null,
                null,
                260);

            starter.AddDialogLine(
                "gwp_sparring_field_post_loss",
                "start",
                "gwp_sparring_field_post_reply",
                GwpText.Get(
                    "{=gwp_sparring_field_post_loss}Don't be discouraged. You fought well, and you'll be stronger next time."),
                IsFieldPostBoutLossConversation,
                null,
                250);

            starter.AddPlayerLine(
                "gwp_sparring_field_post_reply",
                "gwp_sparring_field_post_reply",
                "close_window",
                GwpText.Get(
                    "{=gwp_sparring_field_post_reply}Thanks. Let's spar again sometime."),
                null,
                null,
                250);
        }

        private bool CanChallengeConversationHero()
        {
            Hero? opponent = Hero.OneToOneConversationHero;
            if (!GwpCommon.IsGreyWardenLord(opponent)
                || opponent == null
                || opponent.IsDead
                || opponent.IsPrisoner
                || opponent.IsWounded
                || Hero.MainHero.IsWounded
                || MobileParty.MainParty == null
                || MobileParty.MainParty.MapEvent != null
                || FactionManager.IsAtWarAgainstFaction(
                    Hero.MainHero.MapFaction,
                    opponent.MapFaction))
            {
                return false;
            }

            if (IsPoliceInteractionConversation())
                return false;

            if (Settlement.CurrentSettlement?.IsTown == true)
            {
                return Settlement.CurrentSettlement.LocationComplex?
                    .GetLocationWithId("arena") != null;
            }

            MobileParty? opponentParty = MobileParty.ConversationParty
                ?? opponent.PartyBelongedTo;
            return opponentParty?.IsActive == true
                && opponentParty != MobileParty.MainParty;
        }

        private void QueueSparring()
        {
            Hero? opponent = Hero.OneToOneConversationHero;
            if (opponent == null)
                return;

            _pendingOpponent = opponent;
            _pendingOpponentParty = MobileParty.ConversationParty
                ?? opponent.PartyBelongedTo;
            _pendingSettlement = Settlement.CurrentSettlement;
            _pendingArena = _pendingSettlement?.LocationComplex?
                .GetLocationWithId("arena");

            if (_pendingSettlement?.IsTown == true && _pendingArena != null)
            {
                _pendingKind = PendingSparringKind.TownArena;
            }
            else
            {
                _pendingKind = PendingSparringKind.Field;
                // A mobile-party conversation still belongs to a
                // PlayerEncounter. Close that encounter peacefully before
                // launching an unrelated mission; otherwise the encounter
                // menu interprets the closed conversation as a real attack.
                GwpCommon.TryFinishPlayerEncounter();
                return;
            }

            // Close the conversation mission first. The arena duel is opened
            // directly after map state resumes; entering the arena location
            // here would launch the ordinary arena-master walkabout instead.
            Mission.Current?.EndMission();
        }

        private void TryLaunchTownSparringImmediately()
        {
            if (_pendingKind != PendingSparringKind.TownArena
                || _pendingOpponent == null
                || _pendingSettlement == null
                || _pendingArena == null
                || Settlement.CurrentSettlement != _pendingSettlement
                || Mission.Current != null
                || Campaign.Current.ConversationManager.IsConversationInProgress
                || GameStateManager.Current.ActiveState is not MapState)
            {
                return;
            }

            if (Hero.MainHero.IsPrisoner
                || _pendingOpponent.IsDead
                || _pendingOpponent.IsPrisoner)
            {
                ClearPendingSparring();
                return;
            }

            Hero opponent = _pendingOpponent;
            Settlement settlement = _pendingSettlement;
            Location arena = _pendingArena;
            ClearPendingSparring();

            try
            {
                string scene = arena.GetSceneName(
                    settlement.Town.GetWallLevel());
                CampaignMission.OpenArenaDuelMission(
                    scene,
                    arena,
                    opponent.CharacterObject,
                    requireCivilianEquipment: false,
                    spawnBothSidesWithHorse: false,
                    winner => OnTownBoutEnded(
                        winner,
                        opponent,
                        settlement),
                    customAgentHealth: 100f);
                Debug.Print(
                    "[GreyWarden Sparring] native town arena duel opened; "
                    + $"scene={scene}; opponent={opponent.StringId}");
            }
            catch (Exception exception)
            {
                Debug.Print(
                    "[GreyWarden Sparring] town arena launch failed: "
                    + exception);
                MBInformationManager.AddQuickInformation(
                    new TextObject(
                        GwpText.Get(
                            "{=gwp_sparring_launch_failed}This isn't a good place to spar, so we'll stop here.")));
            }
        }

        internal static void OnApplicationTick()
        {
            GreyWardenSparringBehavior? behavior = _activeBehavior;
            if (behavior == null
                || Campaign.Current == null
                || Game.Current?.GameType is not Campaign)
            {
                return;
            }

            behavior.TryLaunchTownSparringImmediately();
            behavior.TryLaunchFieldSparringImmediately();
            behavior.TryOpenPostBoutConversation();
        }

        private static void OnTownBoutEnded(
            CharacterObject winner,
            Hero opponent,
            Settlement settlement)
        {
            GreyWardenSparringBehavior? behavior = _activeBehavior;
            if (behavior == null)
                return;

            PlayArenaVictoryCheer();

            Settlement conversationSettlement =
                ResolveTownPostBoutSettlement(settlement);

            behavior.QueueTownResultConversation(
                opponent,
                conversationSettlement,
                winner == CharacterObject.PlayerCharacter,
                GetLordHallConversationScene(conversationSettlement));
            Debug.Print(
                "[GreyWarden Sparring] town arena duel resolved; "
                + $"playerWon={winner == CharacterObject.PlayerCharacter}; "
                + "waiting for the player to leave with Tab");
        }

        private static void PlayArenaVictoryCheer()
        {
            Mission? mission = Mission.Current;
            if (mission == null)
                return;

            GameEntity arenaSoundEntity =
                mission.Scene.FindEntityWithTag("arena_sound");
            Vec3 soundPosition = arenaSoundEntity != null
                ? arenaSoundEntity.GlobalPosition
                : Vec3.Zero;
            SoundManager.StartOneShotEvent(
                "event:/mission/ambient/detail/arena/cheer_big",
                soundPosition);
        }

        private void QueueTownResultConversation(
            Hero opponent,
            Settlement settlement,
            bool playerWon,
            string conversationScene)
        {
            _pendingPostBoutOpponent = opponent;
            _pendingPostBoutSettlement = settlement;
            _pendingPostBoutConversationScene = conversationScene;
            _pendingPostBoutKind = PostBoutConversationKind.Town;
            _pendingPostBoutPlayerWon = playerWon;
            _pendingPostBoutRuleViolation = false;
            _postBoutConversationQueued = true;
            _postBoutConversationActive = false;
            Debug.Print(
                "[GreyWarden Sparring] town post-bout conversation queued; "
                + $"playerWon={playerWon}; scene={conversationScene}");
        }

        internal static void QueueFieldResultConversation(
            Hero opponent,
            bool playerWon,
            bool ruleViolation = false)
        {
            GreyWardenSparringBehavior? behavior = _activeBehavior;
            if (behavior == null)
                return;

            behavior._pendingPostBoutOpponent = opponent;
            behavior._pendingPostBoutSettlement = null;
            behavior._pendingPostBoutConversationScene = string.Empty;
            behavior._pendingPostBoutKind = PostBoutConversationKind.Field;
            behavior._pendingPostBoutPlayerWon = playerWon;
            behavior._pendingPostBoutRuleViolation = ruleViolation;
            behavior._postBoutConversationQueued = true;
            behavior._postBoutConversationActive = false;
            Debug.Print(
                "[GreyWarden Sparring] post-bout map conversation queued; "
                + $"playerWon={playerWon}; ruleViolation={ruleViolation}");
        }

        private void TryLaunchFieldSparringImmediately()
        {
            if (_pendingKind != PendingSparringKind.Field
                || _pendingOpponent == null
                || Mission.Current != null
                || Campaign.Current.ConversationManager.IsConversationInProgress
                || GameStateManager.Current.ActiveState is not MapState)
            {
                return;
            }

            MobileParty? mainParty = MobileParty.MainParty;
            if (mainParty == null)
            {
                ClearPendingSparring();
                return;
            }

            if (Hero.MainHero.IsPrisoner
                || _pendingOpponent.IsDead
                || _pendingOpponent.IsPrisoner)
            {
                ClearPendingSparring();
                return;
            }

            // A castle conversation leaves the main party inside the settlement.
            // Opening a free-standing field mission from that state makes the
            // engine restore the stale castle_outside menu after the post-bout
            // map conversation. The native menu/encounter state can then fault
            // after every managed sparring callback has already completed.
            // Actually leave the settlement first and launch on the following
            // application frame; this also matches the visible fiction that the
            // two parties step outside the walls to spar.
            Settlement? currentSettlement =
                mainParty.CurrentSettlement
                ?? Settlement.CurrentSettlement;
            if (currentSettlement != null)
            {
                try
                {
                    LeaveSettlementAction.ApplyForParty(
                        mainParty);
                    Debug.Print(
                        "[GreyWarden Sparring] left settlement before field "
                        + $"launch; settlement={currentSettlement.StringId}");
                }
                catch (Exception exception)
                {
                    Debug.Print(
                        "[GreyWarden Sparring] could not leave settlement "
                        + "before field launch: " + exception);
                    MBInformationManager.AddQuickInformation(
                        new TextObject(
                            GwpText.Get(
                                "{=gwp_sparring_launch_failed}This isn't a good place to spar, so we'll stop here.")));
                    ClearPendingSparring();
                }

                return;
            }

            // Wait until the friendly party meeting has been fully removed.
            // Never open a stored practice mission on top of a MapEvent.
            if (PlayerEncounter.IsActive
                || mainParty.MapEvent != null)
            {
                try
                {
                    if (PlayerEncounter.IsActive)
                    {
                        PlayerEncounter.LeaveEncounter = true;
                        PlayerEncounter.Finish(false);
                    }
                }
                catch (Exception exception)
                {
                    Debug.Print(
                        "[GreyWarden Sparring] encounter close before field "
                        + "launch failed: " + exception);
                }

                if (PlayerEncounter.IsActive
                    || mainParty.MapEvent != null)
                {
                    return;
                }
            }

            Hero opponent = _pendingOpponent;
            MobileParty? opponentParty = _pendingOpponentParty;
            ClearPendingSparring();

            try
            {
                OpenFieldSparringMission(opponent, opponentParty);
            }
            catch (Exception exception)
            {
                Debug.Print("[GreyWarden Sparring] field launch failed: " + exception);
                MBInformationManager.AddQuickInformation(
                    new TextObject(
                        GwpText.Get(
                            "{=gwp_sparring_launch_failed}This isn't a good place to spar, so we'll stop here.")));
            }
        }

        private static bool IsFieldStartConversation()
        {
            GreyWardenFieldSparringMissionController? controller = Mission.Current?
                .GetMissionBehavior<GreyWardenFieldSparringMissionController>();
            return controller?.IsStartConversationActive == true
                && Hero.OneToOneConversationHero == controller.Opponent;
        }

        private bool IsFieldPostBoutWinConversation()
        {
            return _postBoutConversationActive
                && _pendingPostBoutPlayerWon
                && Hero.OneToOneConversationHero
                    == _pendingPostBoutOpponent;
        }

        private bool IsFieldPostBoutLossConversation()
        {
            return _postBoutConversationActive
                && !_pendingPostBoutPlayerWon
                && !_pendingPostBoutRuleViolation
                && Hero.OneToOneConversationHero
                    == _pendingPostBoutOpponent;
        }

        private bool IsFieldPostBoutRuleViolationConversation()
        {
            return _postBoutConversationActive
                && _pendingPostBoutRuleViolation
                && Hero.OneToOneConversationHero
                    == _pendingPostBoutOpponent;
        }

        private static bool CanChooseMountedFieldDuel()
        {
            GreyWardenFieldSparringMissionController? controller = Mission.Current?
                .GetMissionBehavior<GreyWardenFieldSparringMissionController>();
            return controller?.CanStartMountedDuel == true;
        }

        private static void BeginMountedFieldDuelAfterConversation()
        {
            Campaign.Current.ConversationManager.ConversationEndOneShot +=
                GreyWardenFieldSparringMissionController.StartMountedDuel;
        }

        private static void BeginFootFieldDuelAfterConversation()
        {
            Campaign.Current.ConversationManager.ConversationEndOneShot +=
                GreyWardenFieldSparringMissionController.StartFootDuel;
        }

        private void TryOpenPostBoutConversation()
        {
            if (!_postBoutConversationQueued
                || _pendingPostBoutOpponent == null
                || Mission.Current != null
                || Campaign.Current.ConversationManager
                    .IsConversationInProgress
                || GameStateManager.Current.ActiveState is not MapState)
            {
                return;
            }

            if (_pendingPostBoutKind == PostBoutConversationKind.Field
                && (PlayerEncounter.IsActive
                    || MobileParty.MainParty.MapEvent != null))
            {
                return;
            }

            if (_pendingPostBoutKind == PostBoutConversationKind.Town
                && _pendingPostBoutSettlement == null)
            {
                return;
            }

            Hero opponent = _pendingPostBoutOpponent;
            if (opponent.IsDead || opponent.IsPrisoner)
            {
                ClearPostBoutConversation();
                return;
            }

            _postBoutConversationQueued = false;
            _postBoutConversationActive = true;
            ConversationManager conversationManager =
                Campaign.Current.ConversationManager;
            conversationManager.ConversationEndOneShot +=
                ClearPostBoutConversation;
            try
            {
                bool isTownConversation = _pendingPostBoutKind
                    == PostBoutConversationKind.Town;
                ConversationCharacterData playerData =
                    new ConversationCharacterData(
                        CharacterObject.PlayerCharacter,
                        MobileParty.MainParty.Party,
                        noHorse: isTownConversation,
                        spawnAfterFight: true,
                        noBodyguards: true);
                ConversationCharacterData opponentData =
                    new ConversationCharacterData(
                        opponent.CharacterObject,
                        opponent.PartyBelongedTo?.Party,
                        noHorse: isTownConversation,
                        spawnAfterFight: true,
                        noBodyguards: true);

                if (isTownConversation)
                {
                    CampaignMission.OpenConversationMission(
                        playerData,
                        opponentData,
                        _pendingPostBoutConversationScene,
                        string.Empty);
                }
                else
                {
                    CampaignMapConversation.OpenConversation(
                        playerData,
                        opponentData);
                }
                Debug.Print(
                    "[GreyWarden Sparring] post-bout conversation opened; "
                    + $"kind={_pendingPostBoutKind}");
            }
            catch (Exception exception)
            {
                conversationManager.ConversationEndOneShot -=
                    ClearPostBoutConversation;
                Debug.Print(
                    "[GreyWarden Sparring] post-bout map conversation "
                    + "failed: " + exception);
                ClearPostBoutConversation();
            }
        }

        private void OnMapEventStarted(
            MapEvent mapEvent,
            PartyBase attackerParty,
            PartyBase defenderParty)
        {
            _ = mapEvent;
            if (_pendingKind != PendingSparringKind.Field)
                return;

            if (attackerParty == PartyBase.MainParty
                || defenderParty == PartyBase.MainParty)
            {
                // A genuine campaign fight always wins over a queued friendly
                // bout. Most importantly, do not launch that bout after the
                // battle or during prisoner processing.
                ClearPendingSparring();
            }
        }

        private static void OpenFieldSparringMission(
            Hero opponent,
            MobileParty? opponentParty)
        {
            MobileParty fieldOpponentParty = opponentParty
                ?? throw new InvalidOperationException(
                    "the challenged hero has no active field party");
            if (!fieldOpponentParty.IsActive)
                throw new InvalidOperationException(
                    "the challenged hero has no active field party");
            if (fieldOpponentParty == MobileParty.MainParty)
                throw new InvalidOperationException(
                    "the challenged hero belongs to the player party");

            Campaign campaign = Campaign.Current;
            var mapPatch = campaign.MapSceneWrapper.GetMapPatchAtPosition(
                MobileParty.MainParty.Position);
            string scene = campaign.Models.SceneModel.GetBattleSceneForMapPatch(
                mapPatch,
                false);

            MissionInitializerRecord initializer =
                SandBoxMissions.CreateSandBoxMissionInitializerRecord(
                    scene,
                    string.Empty,
                    doNotUseLoadingScreen: false,
                    DecalAtlasGroup.Battle);

            // Stock field battles use the campaign patch and encounter
            // direction to select and orient an authored spawn path. Without
            // these values the engine deliberately chooses a random path and
            // pivot, which is unsuitable for an arranged bout.
            Vec2 playerMapPosition = MobileParty.MainParty.Position.ToVec2();
            Vec2 opponentMapPosition =
                fieldOpponentParty.Position.ToVec2();
            Vec2 encounterDirection = opponentMapPosition - playerMapPosition;
            if (encounterDirection.LengthSquared < 0.01f)
                encounterDirection = Vec2.Forward;
            else
                encounterDirection.Normalize();

            initializer.NeedsRandomTerrain = false;
            initializer.RandomTerrainSeed = MBRandom.RandomInt(10000);
            initializer.SceneHasMapPatch = true;
            initializer.PatchCoordinates = mapPatch.normalizedCoordinates;
            initializer.PatchEncounterDir = encounterDirection;

            CustomBattleCombatant playerCombatant = BuildFieldCombatant(
                MobileParty.MainParty,
                Hero.MainHero,
                BattleSideEnum.Defender);
            CustomBattleCombatant opponentCombatant = BuildFieldCombatant(
                fieldOpponentParty,
                opponent,
                BattleSideEnum.Attacker);
            var combatants = new IBattleCombatant[]
            {
                playerCombatant,
                opponentCombatant
            };
            var troopSuppliers = new IMissionTroopSupplier[2];
            troopSuppliers[(int)BattleSideEnum.Defender] =
                new GreyWardenSafeTroopSupplier(
                    playerCombatant,
                    MobileParty.MainParty.Party,
                    isPlayerSide: true,
                    isPlayerGeneral: true,
                    isSallyOut: false);
            troopSuppliers[(int)BattleSideEnum.Attacker] =
                new GreyWardenSafeTroopSupplier(
                    opponentCombatant,
                    fieldOpponentParty.Party,
                    isPlayerSide: false,
                    isPlayerGeneral: false,
                    isSallyOut: false);

            var spawnLogic = new DefaultBattleMissionAgentSpawnLogic(
                troopSuppliers,
                BattleSideEnum.Defender,
                Mission.BattleSizeType.Battle);

            MissionState.OpenNew(
                // TownMerchant supplies a safe free-roaming conversation
                // camera and combat status UI without requiring deployment,
                // order-of-battle, or boundary view dependencies.
                "TownMerchant",
                initializer,
                mission => new MissionBehavior[]
                {
                    spawnLogic,
                    new BattlePowerCalculationLogic(),
                    new CustomBattleAgentLogic(),
                    new BattleSpawnLogic("battle_set"),
                    // The stock field-battle spawn-path selector converts the
                    // campaign patch into scene coordinates from this
                    // boundary.  Without it the selector falls back to the
                    // compact fixed battle_set markers and both armies appear
                    // on the same side.
                    new MissionBoundaryPlacer(),
                    new CustomBattleMissionSpawnHandler(
                        playerCombatant,
                        opponentCombatant),
                    new MissionOptionsComponent(),
                    new MissionCombatantsLogic(
                        combatants,
                        playerCombatant,
                        playerCombatant,
                        opponentCombatant,
                        Mission.MissionTeamAITypeEnum.FieldBattle,
                        isPlayerSergeant: false),
                    new CampaignMissionComponent(),
                    new GreyWardenFieldSparringMissionController(opponent),
                    new MissionConversationLogic(),
                    new AgentHumanAILogic(),
                    new AgentVictoryLogic(),
                    new MissionAgentPanicHandler(),
                    new ArenaAgentStateDeciderLogic(),
                    new VisualTrackerMissionBehavior(),
                    new EquipmentControllerLeaveLogic()
                });
        }

        private static CustomBattleCombatant BuildFieldCombatant(
            MobileParty party,
            Hero general,
            BattleSideEnum side)
        {
            PartyBase partyBase = party.Party;
            var combatant = new CustomBattleCombatant(
                partyBase.Name,
                partyBase.BasicCulture,
                partyBase.Banner)
            {
                Side = side
            };

            bool generalIncluded = false;
            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster())
            {
                CharacterObject character = element.Character;
                int healthyCount = Math.Max(
                    0,
                    element.Number - element.WoundedNumber);
                if (character == null || healthyCount == 0)
                    continue;

                combatant.AddCharacter(character, healthyCount);
                if (character == general.CharacterObject)
                    generalIncluded = true;
            }

            if (!generalIncluded)
                combatant.AddCharacter(general.CharacterObject, 1);

            combatant.SetGeneral(general.CharacterObject);
            return combatant;
        }

        private static bool IsPoliceInteractionConversation()
        {
            MobileParty? conversationParty = MobileParty.ConversationParty;
            if (conversationParty == null)
                return false;

            if (GwpCommon.IsPatrolParty(conversationParty)
                || GwpCommon.IsEnforcementDelayPatrolParty(conversationParty))
            {
                return true;
            }

            PoliceTask? task = GwpRuntimeState.Crime.GetTask(
                conversationParty.StringId);
            return task?.TargetCrime?.Offender?.IsMainParty == true;
        }

        private void ClearPostBoutConversation()
        {
            _pendingPostBoutOpponent = null;
            _pendingPostBoutSettlement = null;
            _pendingPostBoutConversationScene = string.Empty;
            _pendingPostBoutKind = PostBoutConversationKind.None;
            _pendingPostBoutPlayerWon = false;
            _pendingPostBoutRuleViolation = false;
            _postBoutConversationQueued = false;
            _postBoutConversationActive = false;
        }

        private void ClearPendingSparring()
        {
            _pendingKind = PendingSparringKind.None;
            _pendingOpponent = null;
            _pendingOpponentParty = null;
            _pendingSettlement = null;
            _pendingArena = null;
        }

        private static Settlement ResolveTownPostBoutSettlement(
            Settlement preferredSettlement)
        {
            Settlement? currentSettlement = Settlement.CurrentSettlement;
            if (HasLordHall(currentSettlement))
                return currentSettlement!;

            Vec2 playerPosition = MobileParty.MainParty.Position.ToVec2();
            Settlement? nearestFortification = Settlement.All
                .Where(HasLordHall)
                .OrderBy(settlement => settlement.GetPosition2D.DistanceSquared(
                    playerPosition))
                .FirstOrDefault();

            return nearestFortification ?? preferredSettlement;
        }

        private static bool HasLordHall(Settlement? settlement)
        {
            return settlement != null
                && (settlement.IsTown || settlement.IsCastle)
                && settlement.Town != null
                && settlement.LocationComplex?
                    .GetLocationWithId("lordshall") != null;
        }

        private static string GetLordHallConversationScene(
            Settlement settlement)
        {
            Location lordHall = settlement.LocationComplex
                .GetLocationWithId("lordshall")
                ?? throw new InvalidOperationException(
                    "the selected fortification has no lord hall");
            return lordHall.GetSceneName(settlement.Town.GetWallLevel());
        }
    }
}
