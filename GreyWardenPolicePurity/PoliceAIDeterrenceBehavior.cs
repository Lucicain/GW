using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
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
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            GwpAiDeterrenceState.SyncData(dataStore);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddDialogLine(
                "gwp_ai_deterrence_greeting",
                "start",
                "lord_talk_speak_diplomacy_2",
                "{" + GwpTextKeys.AiDeterrenceGreeting + "}",
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

        internal static void RegisterEnforcementVictoryAgainst(MobileParty offender)
        {
            if (offender == null || offender.IsMainParty) return;
            GwpAiDeterrenceState.RegisterEnforcementDefeat(offender);
        }

        private static bool DeterrenceGreetingCondition()
        {
            Hero? conversationHero = Hero.OneToOneConversationHero;
            if (conversationHero == null || conversationHero == Hero.MainHero)
                return false;

            if (conversationHero.Clan != null &&
                string.Equals(conversationHero.Clan.StringId, GwpIds.PoliceClanId, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!GwpAiDeterrenceState.TryBuildPainDialogue(conversationHero, out var text))
                return false;

            MBTextManager.SetTextVariable(GwpTextKeys.AiDeterrenceGreeting, text);
            return true;
        }
    }
}
