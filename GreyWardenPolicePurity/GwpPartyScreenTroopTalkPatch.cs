using System;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 原版的队伍界面只让英雄有交谈按钮。灰袍的普通士兵是玩家能真正派出去办事的人，
    /// 所以只为"自己队里的灰袍兵"额外开这个按钮，并把它接到派遣对话上。
    /// 其余角色、其余界面一律不碰，英雄仍然走原版那条路。
    /// </summary>
    internal static class GwpPartyScreenTroopTalk
    {
        internal static bool IsDispatchableTroop(PartyCharacterVM? character)
        {
            try
            {
                CharacterObject? troop = character?.Troop.Character;
                return character != null &&
                       character.Side == PartyScreenLogic.PartyRosterSide.Right &&
                       character.Type == PartyScreenLogic.TroopType.Member &&
                       troop != null && !troop.IsHero &&
                       GwpWardenDispatchDialogue.CanTalkToTroop(troop);
            }
            catch
            {
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(PartyCharacterVM), nameof(PartyCharacterVM.UpdateTalkable))]
    internal static class GwpPartyCharacterTalkablePatch
    {
        [HarmonyPostfix]
        private static void Postfix(PartyCharacterVM __instance)
        {
            if (__instance == null || __instance.IsTalkableCharacter) return;
            if (!GwpPartyScreenTroopTalk.IsDispatchableTroop(__instance)) return;

            try
            {
                __instance.IsTalkableCharacter = true;
                __instance.CanTalk = true;
                if (__instance.TalkHint == null) return;
                __instance.TalkHint.HintText = GwpText.Create(
                    "{=gwp_dispatch_talk_hint}Send these men on an errand");
            }
            catch (Exception ex)
            {
                GwpAiDiagnostics.WriteFieldArrest("DISPATCH_TALK_BUTTON_FAILED", ex.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(PartyCharacterVM), nameof(PartyCharacterVM.ExecuteTalk))]
    internal static class GwpPartyCharacterExecuteTalkPatch
    {
        /// <summary>
        /// 原版这条路只处理英雄，普通士兵走进去会空引用。命中灰袍兵时整条拦下来，
        /// 先关掉队伍界面，再用原版的地图对话打开派遣选项。
        /// </summary>
        [HarmonyPrefix]
        private static bool Prefix(PartyCharacterVM __instance)
        {
            if (!GwpPartyScreenTroopTalk.IsDispatchableTroop(__instance))
            {
                // A stale troop button must not open the native hero-only conversation.
                CharacterObject? blockedTroop = __instance?.Troop.Character;
                return blockedTroop == null || blockedTroop.IsHero || !GwpCommon.IsGreyWardenTroop(blockedTroop);
            }

            CharacterObject? troop = __instance?.Troop.Character;
            try
            {
                PartyScreenHelper.CloseScreen(true, false);
            }
            catch (Exception ex)
            {
                GwpAiDiagnostics.WriteFieldArrest("DISPATCH_SCREEN_CLOSE_FAILED", ex.ToString());
                return false;
            }

            GwpWardenDispatchDialogue.OpenWithTroop(troop);
            return false;
        }
    }
}
