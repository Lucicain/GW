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
        }

        private sealed class PoliceCaptureBatch
        {
            public Dictionary<string, Hero> Witnesses { get; } =
                new Dictionary<string, Hero>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> OffenderIds { get; } =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<CaptureShock> Shocks { get; } = new List<CaptureShock>();
        }

        private const float DeterrenceGreetingChance = 1f;
        private static string _lastDeterrenceConversationKey = string.Empty;
        private static bool _lastDeterrenceConversationResult;
        private static TextObject? _lastDeterrenceIntro;
        private static TextObject? _lastDeterrenceFollowup;
        private readonly Dictionary<MapEvent, PoliceCaptureBatch> _captureBatches =
            new Dictionary<MapEvent, PoliceCaptureBatch>();

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
            GwpAiDeterrenceState.ClearAll();
        }

        private void OnDailyTick() => GwpAiDeterrenceState.DailyCleanup();

        private void OnConversationEnded(IEnumerable<CharacterObject> characters)
        {
            _ = characters;
            _lastDeterrenceConversationKey = string.Empty;
            _lastDeterrenceConversationResult = false;
            _lastDeterrenceIntro = null;
            _lastDeterrenceFollowup = null;
        }

        private void OnHeroPrisonerTaken(PartyBase capturerParty, Hero prisoner)
        {
            MobileParty? policeParty = capturerParty?.MobileParty;
            if (!IsPoliceParty(policeParty) || prisoner == null || prisoner == Hero.MainHero ||
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

            float directGain = GwpAiDeterrenceState.RegisterPoliceArrest(prisoner);
            float sharedGain = directGain * 0.5f;
            if (sharedGain <= GwpTuning.Deterrence.ForgetThreshold)
                return;

            ApplyClanShock(prisoner, sharedGain);
            batch?.Shocks.Add(new CaptureShock
            {
                OffenderClanId = prisoner.Clan?.StringId ?? string.Empty,
                SharedGain = sharedGain
            });
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || !_captureBatches.TryGetValue(mapEvent, out PoliceCaptureBatch? batch))
                return;

            _captureBatches.Remove(mapEvent);
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

                    GwpAiDeterrenceState.RegisterSharedFamilyDeterrence(witness, shock.SharedGain);
                }
            }
        }

        private static bool IsEligibleWitness(Hero? hero) =>
            hero != null && hero != Hero.MainHero && hero.IsAlive && !IsPoliceHero(hero);

        private static void ApplyClanShock(Hero offender, float sharedGain)
        {
            if (offender.Clan == null || sharedGain <= GwpTuning.Deterrence.ForgetThreshold)
                return;

            foreach (Hero clanMember in offender.Clan.Heroes)
            {
                if (clanMember == offender || !IsEligibleWitness(clanMember))
                    continue;

                GwpAiDeterrenceState.RegisterSharedFamilyDeterrence(clanMember, sharedGain);
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
