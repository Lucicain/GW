using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 只有当前背负未结案件且被灰袍实际俘获的领主，才增加被捕次数与本人震慑。
    /// 同场被灰袍俘获的无未结案件领主只获得目击型家族震慑，不向其家族继续传播。
    /// </summary>
    public sealed class PoliceAIDeterrenceBehavior : CampaignBehaviorBase
    {
        private sealed class CaptureShock
        {
            public string OffenderClanId { get; init; } = string.Empty;
            public float SharedGain { get; init; }
            public GwpCrimeCategory Category { get; init; }
        }

        private sealed class PoliceCaptureBatch
        {
            public Dictionary<string, Hero> Witnesses { get; } =
                new Dictionary<string, Hero>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> OffenderIds { get; } =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<CaptureShock> Shocks { get; } = new List<CaptureShock>();
        }

        private const float DeterrenceGreetingChance = 0.5f;
        private static string _lastDeterrenceConversationKey = string.Empty;
        private static bool _lastDeterrenceConversationResult;
        private static TextObject? _lastDeterrenceIntro;
        private static TextObject? _lastDeterrenceFollowup;
        private readonly Dictionary<MapEvent, PoliceCaptureBatch> _captureBatches =
            new Dictionary<MapEvent, PoliceCaptureBatch>();
        private readonly Dictionary<MapEvent, HashSet<string>> _recentProcessedOffenders =
            new Dictionary<MapEvent, HashSet<string>>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // 累计次数与两类威慑由 CrimePool 的长期数字档案保存；单场目击批次只存在于运行时。
            GwpAiDeterrenceState.SyncData(dataStore);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _captureBatches.Clear();
            _recentProcessedOffenders.Clear();

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
            _captureBatches.Clear();
            _recentProcessedOffenders.Clear();
            GwpAiDeterrenceState.ClearAll();
        }

        private void OnDailyTick()
        {
            _recentProcessedOffenders.Clear();
            GwpAiDeterrenceState.DailyCleanup();
        }

        private void OnConversationEnded(IEnumerable<CharacterObject> characters)
        {
            _ = characters;
            _lastDeterrenceConversationKey = string.Empty;
            _lastDeterrenceConversationResult = false;
            _lastDeterrenceIntro = null;
            _lastDeterrenceFollowup = null;
        }

        /// <summary>
        /// 玩家受托办案时，他就是这宗案子的承办人。因此他亲手押下一个还背着未结案件的
        /// 领主，和灰袍领主押下同一个人，对犯人来说是同一件事：一样记被捕、一样吃震慑、
        /// 一样把消息传给同族和同场目击者。之后玩家交不交差、交多少，是玩家和灰袍之间
        /// 的账，与犯人已经受到的惩戒无关。
        /// </summary>
        private void OnHeroPrisonerTaken(PartyBase capturerParty, Hero prisoner)
        {
            MobileParty? policeParty = capturerParty?.MobileParty;
            bool capturedByPlayer = policeParty?.IsMainParty == true;
            if ((!IsPoliceParty(policeParty) && !capturedByPlayer) ||
                prisoner == null || prisoner == Hero.MainHero ||
                string.IsNullOrWhiteSpace(prisoner.StringId))
                return;

            MapEvent? mapEvent = policeParty?.MapEvent;
            PoliceCaptureBatch? batch = null;
            if (mapEvent != null)
            {
                if (!_captureBatches.TryGetValue(mapEvent, out batch))
                {
                    batch = new PoliceCaptureBatch();
                    _captureBatches[mapEvent] = batch;
                }
            }

            CrimeRecord? record = CrimePool.GetRecord(prisoner);
            bool hasOpenCase = record?.HasOpenCase == true;
            if (!hasOpenCase)
            {
                if (batch != null && !batch.OffenderIds.Contains(prisoner.StringId))
                    batch.Witnesses[prisoner.StringId] = prisoner;
                return;
            }

            if (batch != null)
            {
                batch.Witnesses.Remove(prisoner.StringId);
                if (!batch.OffenderIds.Add(prisoner.StringId))
                    return;
            }

            GwpCrimeCategory category = record?.CrimeCategory == GwpCrimeCategory.CaravanAttack
                ? GwpCrimeCategory.CaravanAttack
                : GwpCrimeCategory.VillageViolence;
            float directGain = GwpAiDeterrenceState.RegisterPoliceArrest(prisoner, category);
            float sharedGain = directGain * 0.5f;
            if (sharedGain <= GwpTuning.Deterrence.ForgetThreshold)
                return;

            ApplyClanShock(prisoner, sharedGain, category);
            batch?.Shocks.Add(new CaptureShock
            {
                OffenderClanId = prisoner.Clan?.StringId ?? string.Empty,
                SharedGain = sharedGain,
                Category = category
            });
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || !_captureBatches.TryGetValue(mapEvent, out PoliceCaptureBatch? batch))
                return;

            _captureBatches.Remove(mapEvent);
            _recentProcessedOffenders[mapEvent] = new HashSet<string>(
                batch.OffenderIds,
                StringComparer.OrdinalIgnoreCase);
            if (batch.Shocks.Count == 0 || batch.Witnesses.Count == 0)
                return;

            foreach (Hero witness in batch.Witnesses.Values)
            {
                if (!IsEligibleWitness(witness))
                    continue;

                foreach (CaptureShock shock in batch.Shocks)
                {
                    // 同族成员已经由主犯的家族震慑获得本次分数，不能因同时在场再重复获得。
                    if (!string.IsNullOrWhiteSpace(shock.OffenderClanId) &&
                        string.Equals(witness.Clan?.StringId, shock.OffenderClanId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    GwpAiDeterrenceState.RegisterSharedFamilyDeterrence(witness,
                        shock.SharedGain, shock.Category);
                }
            }
        }

        /// <summary>
        /// 玩家以灰袍受托人或赎罪执行人身份，亲自完成一宗已进入案件池的案件时，
        /// 复用普通灰袍实际抓捕的完整震慑链：本人、同族与同场目击者。
        /// 如果同场的灰袍护送队已经抓获该目标，则使用场次+英雄去重，不重复计入被捕和震慑。
        /// </summary>
        internal void RegisterPlayerCompletedCase(
            MapEvent? mapEvent,
            Hero? offender,
            GwpCrimeCategory category) =>
            RegisterPlayerEnforcementOutcome(mapEvent, offender, category, countAsArrest: true);

        /// <summary>
        /// 玩家把惩戒落到了这个人身上——缴清罚金、兑现谈成的处置、被打垮或被押走。
        /// 惩戒既然落地，就和灰袍自己办的一样算一次抓获，走完全相同的登记路径：
        /// 履历、震慑、同族转述、同场目击。玩家这条线不另设规则。
        /// 和平了结没有战场，因此不编造目击者。
        /// </summary>
        internal void RegisterPlayerEnforcementSuccess(
            MapEvent? mapEvent,
            Hero? offender,
            GwpCrimeCategory category) =>
            RegisterPlayerEnforcementOutcome(mapEvent, offender, category, countAsArrest: true);

        /// <summary>同一个人在这么多小时内的重复登记，视为同一次惩戒。</summary>
        private const double SamePunishmentHours = 1d;

        private readonly Dictionary<string, double> _lastPlayerEnforcementHours =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        private void RegisterPlayerEnforcementOutcome(
            MapEvent? mapEvent,
            Hero? offender,
            GwpCrimeCategory category,
            bool countAsArrest)
        {
            if (offender == null || offender == Hero.MainHero ||
                string.IsNullOrWhiteSpace(offender.StringId))
                return;

            // 一次惩戒只记一次。投降被俘那条路上，野外结算和原版俘虏事件会先后打进来，
            // 说的却是同一件事。
            double now = CampaignTime.Now.ToHours;
            if (_lastPlayerEnforcementHours.TryGetValue(offender.StringId, out double last) &&
                now - last < SamePunishmentHours)
                return;
            _lastPlayerEnforcementHours[offender.StringId] = now;

            category = category == GwpCrimeCategory.CaravanAttack
                ? GwpCrimeCategory.CaravanAttack
                : GwpCrimeCategory.VillageViolence;

            if (mapEvent == null)
            {
                // 缴款和谈成的处置没有战场，只登记本人与同族，不虚构目击者。
                float peacefulGain = GwpAiDeterrenceState.RegisterEnforcementOutcomeFor(
                    offender, category, countAsArrest);
                float peacefulShared = peacefulGain * 0.5f;
                if (peacefulShared > GwpTuning.Deterrence.ForgetThreshold)
                    ApplyClanShock(offender, peacefulShared, category);
                return;
            }

            if (_captureBatches.TryGetValue(mapEvent, out PoliceCaptureBatch? pendingBatch))
            {
                if (!pendingBatch.OffenderIds.Add(offender.StringId))
                    return;

                float pendingDirectGain = GwpAiDeterrenceState.RegisterEnforcementOutcomeFor(
                    offender, category, countAsArrest);
                float pendingSharedGain = pendingDirectGain * 0.5f;
                if (pendingSharedGain <= GwpTuning.Deterrence.ForgetThreshold)
                    return;

                ApplyClanShock(offender, pendingSharedGain, category);
                AddBattleWitnesses(pendingBatch, mapEvent, offender);
                pendingBatch.Shocks.Add(new CaptureShock
                {
                    OffenderClanId = offender.Clan?.StringId ?? string.Empty,
                    SharedGain = pendingSharedGain,
                    Category = category
                });
                return;
            }

            if (!_recentProcessedOffenders.TryGetValue(mapEvent, out HashSet<string>? processed))
            {
                processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _recentProcessedOffenders[mapEvent] = processed;
            }
            if (!processed.Add(offender.StringId))
                return;

            float directGain = GwpAiDeterrenceState.RegisterEnforcementOutcomeFor(
                offender, category, countAsArrest);
            float sharedGain = directGain * 0.5f;
            if (sharedGain <= GwpTuning.Deterrence.ForgetThreshold)
                return;

            ApplyClanShock(offender, sharedGain, category);
            ApplyBattleWitnessShock(mapEvent, offender, sharedGain, category);
        }

        private static void AddBattleWitnesses(
            PoliceCaptureBatch batch,
            MapEvent mapEvent,
            Hero offender)
        {
            foreach (Hero witness in GetDefeatedSideWitnesses(mapEvent, offender))
                batch.Witnesses[witness.StringId] = witness;
        }

        private static void ApplyBattleWitnessShock(
            MapEvent mapEvent,
            Hero offender,
            float sharedGain,
            GwpCrimeCategory category)
        {
            foreach (Hero witness in GetDefeatedSideWitnesses(mapEvent, offender))
            {
                if (string.Equals(witness.Clan?.StringId, offender.Clan?.StringId,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                GwpAiDeterrenceState.RegisterSharedFamilyDeterrence(
                    witness,
                    sharedGain,
                    category);
            }
        }

        private static IEnumerable<Hero> GetDefeatedSideWitnesses(
            MapEvent mapEvent,
            Hero offender)
        {
            if (!mapEvent.HasWinner || mapEvent.Winner == null)
                yield break;

            MapEventSide loserSide = mapEvent.Winner == mapEvent.AttackerSide
                ? mapEvent.DefenderSide
                : mapEvent.AttackerSide;
            if (loserSide == null)
                yield break;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in loserSide.Parties)
            {
                Hero? witness = entry?.Party?.MobileParty?.LeaderHero;
                if (!IsEligibleWitness(witness) || witness == offender ||
                    string.IsNullOrWhiteSpace(witness!.StringId) ||
                    CrimePool.GetRecord(witness)?.HasOpenCase == true ||
                    !seen.Add(witness.StringId))
                    continue;

                yield return witness;
            }
        }

        private static bool IsEligibleWitness(Hero? hero) =>
            hero != null && hero != Hero.MainHero && hero.IsAlive && !IsPoliceHero(hero);

        private static void ApplyClanShock(Hero offender, float sharedGain,
            GwpCrimeCategory category)
        {
            if (offender.Clan == null || sharedGain <= GwpTuning.Deterrence.ForgetThreshold)
                return;

            foreach (Hero clanMember in offender.Clan.Heroes)
            {
                if (clanMember == offender || !IsEligibleWitness(clanMember))
                    continue;

                GwpAiDeterrenceState.RegisterSharedFamilyDeterrence(clanMember,
                    sharedGain, category);
            }
        }

        private static bool IsPoliceParty(MobileParty? party)
        {
            if (party == null) return false;
            if (party.ActualClan != null &&
                string.Equals(party.ActualClan.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase))
                return true;

            return GwpCommon.IsPatrolParty(party) || GwpCommon.IsEnforcementDelayPatrolParty(party);
        }

        private static bool IsPoliceHero(Hero? hero) =>
            hero?.Clan != null &&
            string.Equals(hero.Clan.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase);

        private static bool DeterrenceGreetingCondition()
        {
            if (IsPostBattleCaptureConversation())
                return false;

            Hero? conversationHero = Hero.OneToOneConversationHero;
            if (conversationHero == null || conversationHero == Hero.MainHero || IsPoliceHero(conversationHero))
                return false;

            if (!TryGetDeterrenceGreeting(conversationHero, out TextObject intro, out TextObject followup))
                return false;

            MBTextManager.SetTextVariable(GwpTextKeys.AiDeterrenceIntro, intro);
            MBTextManager.SetTextVariable(GwpTextKeys.AiDeterrenceFollowup, followup);
            return true;
        }

        private static bool IsPostBattleCaptureConversation()
        {
            Campaign? campaign = Campaign.Current;
            return campaign != null &&
                   (campaign.CurrentConversationContext == ConversationContext.CapturedLord ||
                    campaign.CurrentConversationContext == ConversationContext.FreeOrCapturePrisonerHero);
        }

        private static bool TryGetDeterrenceGreeting(
            Hero conversationHero,
            out TextObject intro,
            out TextObject followup)
        {
            intro = new TextObject(string.Empty);
            followup = new TextObject(string.Empty);
            Campaign? campaign = Campaign.Current;
            string heroId = conversationHero.StringId ?? string.Empty;
            string partyId = MobileParty.ConversationParty?.StringId ?? string.Empty;
            string key = (campaign?.CurrentConversationContext.ToString() ?? string.Empty) + "|" + heroId + "|" + partyId;

            if (!string.Equals(_lastDeterrenceConversationKey, key, StringComparison.Ordinal))
            {
                _lastDeterrenceConversationKey = key;
                _lastDeterrenceConversationResult = MBRandom.RandomFloat <= DeterrenceGreetingChance;
                _lastDeterrenceIntro = null;
                _lastDeterrenceFollowup = null;

                if (_lastDeterrenceConversationResult)
                {
                    _lastDeterrenceConversationResult = GwpAiDeterrenceState.TryBuildPainDialogue(
                        conversationHero,
                        out TextObject selectedIntro,
                        out TextObject selectedFollowup);
                    if (_lastDeterrenceConversationResult)
                    {
                        _lastDeterrenceIntro = selectedIntro;
                        _lastDeterrenceFollowup = selectedFollowup;
                    }
                }
            }

            if (!_lastDeterrenceConversationResult ||
                _lastDeterrenceIntro == null ||
                _lastDeterrenceFollowup == null)
                return false;

            intro = _lastDeterrenceIntro;
            followup = _lastDeterrenceFollowup;
            return true;
        }

    }
}
