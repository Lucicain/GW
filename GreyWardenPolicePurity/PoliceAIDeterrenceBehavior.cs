using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 记录被灰袍执法击败过的 AI，并在恢复期内提供对话反馈。
    /// 评分修正在 PoliceRaidDeterrenceModel 中生效。
    /// </summary>
    public sealed class PoliceAIDeterrenceBehavior : CampaignBehaviorBase
    {
        private const float DeterrenceGreetingChance = 0.1f;
        private static GwpRuntimeState.CrimeState CrimeState => GwpRuntimeState.Crime;
        private static string _lastDeterrenceConversationKey = string.Empty;
        private static bool _lastDeterrenceConversationResult;

        private readonly struct DirectDeterrenceTarget
        {
            public DirectDeterrenceTarget(Hero leader, MobileParty? sourceParty)
            {
                Leader = leader;
                SourceParty = sourceParty;
            }

            public Hero Leader { get; }
            public MobileParty? SourceParty { get; }
        }

        private readonly struct SharedDeterrenceTarget
        {
            public SharedDeterrenceTarget(Hero leader, MobileParty? sourceParty, float penaltyPoints)
            {
                Leader = leader;
                SourceParty = sourceParty;
                PenaltyPoints = penaltyPoints;
            }

            public Hero Leader { get; }
            public MobileParty? SourceParty { get; }
            public float PenaltyPoints { get; }
        }

        private readonly struct DirectDeterrenceResult
        {
            public DirectDeterrenceResult(Hero leader, float penaltyPoints)
            {
                Leader = leader;
                PenaltyPoints = penaltyPoints;
            }

            public Hero Leader { get; }
            public float PenaltyPoints { get; }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            GwpAiDeterrenceState.SyncData(dataStore);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddDialogLine(
                "gwp_ai_deterrence_intro",
                "start",
                "gwp_ai_deterrence_followup",
                "{" + GwpTextKeys.AiDeterrenceIntro + "}",
                DeterrenceGreetingCondition,
                null,
                205);

            starter.AddDialogLine(
                "gwp_ai_deterrence_followup",
                "gwp_ai_deterrence_followup",
                "lord_talk_speak_diplomacy_2",
                "{" + GwpTextKeys.AiDeterrenceFollowup + "}",
                DeterrenceGreetingCondition,
                null,
                205);

        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            _ = starter;
            GwpAiDeterrenceState.ClearAll();
        }

        private void OnDailyTick()
        {
            GwpAiDeterrenceState.DailyCleanup();
        }

        private void OnConversationEnded(IEnumerable<CharacterObject> characters)
        {
            _ = characters;
            _lastDeterrenceConversationKey = string.Empty;
            _lastDeterrenceConversationResult = false;
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (!TryCollectDeterrenceTargets(
                    mapEvent,
                    out List<DirectDeterrenceTarget> directTargets,
                    out List<SharedDeterrenceTarget> sharedTargets))
                return;

            List<DirectDeterrenceResult> directResults = new List<DirectDeterrenceResult>();
            foreach (DirectDeterrenceTarget target in directTargets)
            {
                float penaltyPoints = GwpAiDeterrenceState.RegisterDirectDeterrence(target.Leader, target.SourceParty);
                if (penaltyPoints > 0f)
                {
                    directResults.Add(new DirectDeterrenceResult(target.Leader, penaltyPoints));
                }
            }

            foreach (SharedDeterrenceTarget sharedTarget in sharedTargets)
            {
                if (sharedTarget.PenaltyPoints <= 0f)
                    continue;

                GwpAiDeterrenceState.RegisterSharedFamilyDeterrence(sharedTarget.Leader, sharedTarget.PenaltyPoints);
            }

            foreach (DirectDeterrenceResult directResult in directResults)
            {
                ApplyClanShock(directResult.Leader, directResult.PenaltyPoints);
            }

        }

        internal static void RegisterEnforcementVictoryAgainst(MobileParty offender)
        {
            if (offender == null || offender.IsMainParty) return;
            GwpAiDeterrenceState.RegisterEnforcementDefeat(offender);
        }

        internal static void RegisterEnforcementVictoryAgainst(Hero leader, MobileParty? sourceParty = null)
        {
            if (leader == null || leader == Hero.MainHero)
                return;

            GwpAiDeterrenceState.RegisterEnforcementDefeat(leader, sourceParty);
        }

        internal static bool TryBuildHighestDeterrenceSnapshot(out string text)
        {
            return GwpAiDeterrenceState.TryBuildHighestDeterrenceSnapshot(out text);
        }

        private static bool TryCollectDeterrenceTargets(
            MapEvent? mapEvent,
            out List<DirectDeterrenceTarget> directTargets,
            out List<SharedDeterrenceTarget> sharedTargets)
        {
            directTargets = new List<DirectDeterrenceTarget>();
            sharedTargets = new List<SharedDeterrenceTarget>();
            if (mapEvent == null)
                return false;

            bool attackerHasPolice = SideHasPolice(mapEvent.AttackerSide);
            bool defenderHasPolice = SideHasPolice(mapEvent.DefenderSide);
            if (attackerHasPolice == defenderHasPolice)
                return false;

            MapEventSide policeSide = attackerHasPolice ? mapEvent.AttackerSide : mapEvent.DefenderSide;
            if (!SideHasLivingPolice(policeSide))
                return false;

            MapEventSide enemySide = attackerHasPolice ? mapEvent.DefenderSide : mapEvent.AttackerSide;
            HashSet<string> directEnemyPartyIds = CollectDirectEnemyPartyIds(policeSide, enemySide);
            HashSet<string> processedHeroIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<(Hero Leader, MobileParty? Party)> undecidedTargets = new List<(Hero, MobileParty?)>();

            foreach (var involvedParty in enemySide.Parties)
            {
                PartyBase? enemyPartyBase = involvedParty?.Party;
                MobileParty? enemyParty = enemyPartyBase?.MobileParty;
                Hero? leader = enemyParty?.LeaderHero
                               ?? enemyPartyBase?.LeaderHero
                               ?? enemyParty?.Owner
                               ?? enemyPartyBase?.Owner;
                if (leader == null || string.IsNullOrWhiteSpace(leader.StringId))
                    continue;

                if (leader == Hero.MainHero || enemyParty?.IsMainParty == true)
                    continue;

                if (IsPoliceHero(leader) || IsPoliceParty(enemyParty))
                    continue;

                if (!processedHeroIds.Add(leader.StringId))
                    continue;

                string enemyPartyId = enemyParty?.StringId ?? string.Empty;
                if (directEnemyPartyIds.Count == 0 ||
                    (!string.IsNullOrEmpty(enemyPartyId) && directEnemyPartyIds.Contains(enemyPartyId)))
                {
                    directTargets.Add(new DirectDeterrenceTarget(leader, enemyParty));
                }
                else
                {
                    undecidedTargets.Add((leader, enemyParty));
                }
            }

            if (directTargets.Count == 0)
            {
                foreach ((Hero leader, MobileParty? sourceParty) in undecidedTargets)
                    directTargets.Add(new DirectDeterrenceTarget(leader, sourceParty));

                return directTargets.Count > 0;
            }

            float sharedPenalty = 0f;
            foreach (DirectDeterrenceTarget target in directTargets)
            {
                float currentPenalty = GwpAiDeterrenceState.GetCurrentPenalty(target.Leader);
                if (currentPenalty > sharedPenalty)
                    sharedPenalty = currentPenalty;
            }

            sharedPenalty *= 0.5f;
            if (sharedPenalty > GwpTuning.Deterrence.ForgetThreshold)
            {
                foreach ((Hero leader, MobileParty? sourceParty) in undecidedTargets)
                    sharedTargets.Add(new SharedDeterrenceTarget(leader, sourceParty, sharedPenalty));
            }

            return directTargets.Count > 0 || sharedTargets.Count > 0;
        }

        private static HashSet<string> CollectDirectEnemyPartyIds(MapEventSide policeSide, MapEventSide enemySide)
        {
            HashSet<string> enemyPartyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> partiesOnEnemySide = new HashSet<string>(
                enemySide.Parties
                    .Select(p => p?.Party?.MobileParty?.StringId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))!
                    .Cast<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (var involvedParty in policeSide.Parties)
            {
                string? policePartyId = involvedParty?.Party?.MobileParty?.StringId;
                if (string.IsNullOrWhiteSpace(policePartyId))
                    continue;

                PoliceTask? task = CrimeState.GetTask(policePartyId);
                string? offenderPartyId = task?.TargetCrime?.Offender?.StringId;
                if (string.IsNullOrWhiteSpace(offenderPartyId))
                    continue;

                if (partiesOnEnemySide.Contains(offenderPartyId))
                    enemyPartyIds.Add(offenderPartyId!);
            }

            return enemyPartyIds;
        }

        private static void ApplyClanShock(Hero offender, float offenderPenaltyPoints)
        {
            if (offender == null || offender.Clan == null)
                return;

            float sharedPenalty = offenderPenaltyPoints * 0.5f;
            if (sharedPenalty <= GwpTuning.Deterrence.ForgetThreshold)
                return;

            foreach (Hero clanMember in offender.Clan.Heroes.Where(IsEligibleClanShockTarget))
            {
                if (clanMember == offender)
                    continue;

                GwpAiDeterrenceState.RegisterSharedFamilyDeterrence(clanMember, sharedPenalty);
            }
        }

        private static bool SideHasPolice(MapEventSide? side)
        {
            if (side == null)
                return false;

            return side.Parties.Any(p => IsPoliceParty(p?.Party?.MobileParty));
        }

        private static bool SideHasLivingPolice(MapEventSide? side)
        {
            if (side == null)
                return false;

            return side.Parties.Any(p =>
            {
                MobileParty? party = p?.Party?.MobileParty;
                return IsPoliceParty(party) && IsPartyAliveAfterBattle(party);
            });
        }

        private static bool IsEligibleClanShockTarget(Hero? hero)
        {
            if (hero == null || !hero.IsAlive || hero == Hero.MainHero)
                return false;

            return !IsPoliceHero(hero);
        }

        private static bool IsPoliceParty(MobileParty? party)
        {
            if (party == null)
                return false;

            if (party.ActualClan != null &&
                string.Equals(party.ActualClan.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return GwpCommon.IsPatrolParty(party) || GwpCommon.IsEnforcementDelayPatrolParty(party);
        }

        private static bool IsPoliceHero(Hero? hero)
        {
            return hero?.Clan != null &&
                   string.Equals(hero.Clan.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPartyAliveAfterBattle(MobileParty? party)
        {
            return party != null && (party.IsActive || party.MemberRoster.TotalManCount > 0);
        }

        private static bool DeterrenceGreetingCondition()
        {
            if (IsPostBattleCaptureConversation())
                return false;

            Hero? conversationHero = Hero.OneToOneConversationHero;
            if (conversationHero == null || conversationHero == Hero.MainHero)
                return false;

            if (conversationHero.Clan != null &&
                string.Equals(conversationHero.Clan.StringId, GwpIds.PoliceClanId, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!RollDeterrenceGreetingChance(conversationHero))
                return false;

            if (!GwpAiDeterrenceState.TryBuildPainDialogue(conversationHero, out var intro, out var followup))
                return false;

            MBTextManager.SetTextVariable(GwpTextKeys.AiDeterrenceIntro, intro);
            MBTextManager.SetTextVariable(GwpTextKeys.AiDeterrenceFollowup, followup);
            return true;
        }

        private static bool IsPostBattleCaptureConversation()
        {
            Campaign? campaign = Campaign.Current;
            if (campaign == null)
                return false;

            return campaign.CurrentConversationContext == ConversationContext.CapturedLord ||
                   campaign.CurrentConversationContext == ConversationContext.FreeOrCapturePrisonerHero;
        }

        private static bool RollDeterrenceGreetingChance(Hero conversationHero)
        {
            Campaign? campaign = Campaign.Current;
            string heroId = conversationHero.StringId ?? string.Empty;
            string partyId = MobileParty.ConversationParty?.StringId ?? string.Empty;
            string key = $"{campaign?.CurrentConversationContext}|{heroId}|{partyId}";

            if (!string.Equals(_lastDeterrenceConversationKey, key, StringComparison.Ordinal))
            {
                _lastDeterrenceConversationKey = key;
                _lastDeterrenceConversationResult = MBRandom.RandomFloat <= DeterrenceGreetingChance;
            }

            return _lastDeterrenceConversationResult;
        }
    }
}
