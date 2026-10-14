using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍相关的讲和只走这里：选原版讲和方式、避开玩家未收尾的战斗、
    /// 灰袍与第三方讲和前看它是否仍有合法交战理由。规则见 warden-mediation.md。
    /// </summary>
    internal static class GwpPeace
    {
        private sealed class Pending
        {
            internal IFaction Left = null!;
            internal IFaction Right = null!;
            internal bool PoliceWar;
        }

        // 玩家战斗 finalize 前任何立场变化都会触发原版 CheckMapEvents 拆这场战斗：
        // 2026-09-25 败方 0 人队留在地图、会面对话空引用；2026-10-14 抓捕回调中结案、
        // DoApplyMapEventResults 空引用。先记下，等这场战斗 MapEventEnded（已 WaitingRemoval）再办。
        // 遭遇进行中不能存档，所以不进存档。
        private static readonly List<Pending> _afterPlayerBattle = new List<Pending>();

        /// <summary>灰袍与 target 结束交战；target 仍有合法交战理由时保持战争。</summary>
        /// <returns>false 只表示灰袍仍有合法交战理由、战争保留；等玩家战斗收尾的也算已讲和。</returns>
        internal static bool EndPoliceWar(IFaction? target)
        {
            Clan? police = PoliceStats.GetPoliceClan();
            if (police == null || target == null) return true;
            return Make(police, target, policeWar: true);
        }

        /// <summary>两个势力讲和，不看灰袍的交战理由（玩家缴罚、调停、赎罪等已了结的场合）。</summary>
        internal static bool Make(IFaction? left, IFaction? right) => Make(left, right, policeWar: false);

        private static bool Make(IFaction? left, IFaction? right, bool policeWar)
        {
            if (left == null || right == null || left == right) return true;
            if (!FactionManager.IsAtWarAgainstFaction(left, right)) return true;
            if (policeWar && GwpPoliceWarReasonService.HasLegitimateWarReason(right)) return false;

            MapEvent? playerBattle = PlayerEncounter.Battle ?? MapEvent.PlayerMapEvent;
            if (playerBattle is { IsFinalized: false })
            {
                if (!_afterPlayerBattle.Any(p => SamePair(p, left, right)))
                    _afterPlayerBattle.Add(new Pending { Left = left, Right = right, PoliceWar = policeWar });
                return true;
            }

            IFaction? player = Hero.MainHero?.MapFaction;
            // 玩家参与的讲和走原版 MakePeaceAction：裸 SetNeutral 不把定居点标记重绘，
            // 对方领地会在玩家地图上一直红着，也不派发原版和平事件。
            if (left == player || right == player)
                MakePeaceAction.Apply(left, right);
            else
                FactionManager.SetNeutral(left, right);
            return true;
        }

        /// <summary>任一战斗 MapEventEnded 时调用；玩家战斗已收尾才办推迟的讲和。</summary>
        internal static void OnMapEventEnded()
        {
            if (_afterPlayerBattle.Count == 0) return;
            if ((PlayerEncounter.Battle ?? MapEvent.PlayerMapEvent) is { IsFinalized: false }) return;
            List<Pending> due = _afterPlayerBattle.ToList();
            _afterPlayerBattle.Clear();
            foreach (Pending p in due)
                Make(p.Left, p.Right, p.PoliceWar);
        }

        /// <summary>
        /// 玩家的灰袍案件了结（缴罚、受罚获释）：与灰袍讲和；受害势力已正式交战的讲和，
        /// 只有犯罪评级的清掉评级。
        /// </summary>
        internal static void SettlePlayerCase()
        {
            IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
            if (playerFaction == null) return;

            Make(PoliceStats.GetPoliceClan(), playerFaction);

            foreach (IFaction victim in PlayerBehaviorPool.VictimFactions)
            {
                if (victim == null || victim == playerFaction) continue;
                // 灰袍只对玩家阵营宣战，受害方多半没有正式交战，只有犯罪评级。
                if (FactionManager.IsAtWarAgainstFaction(playerFaction, victim))
                {
                    Make(playerFaction, victim);
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get(GwpTextCatalog.gwp_policepatrolbehavior_helpers_001, "VAR_1", victim.Name),
                        Colors.Green));
                }
                else
                {
                    ChangeCrimeRatingAction.Apply(victim, -1000f, true);
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get(GwpTextCatalog.gwp_policepatrolbehavior_helpers_002, "VAR_1", victim.Name),
                        Colors.Green));
                }
            }

            PlayerBehaviorPool.ClearVictimFactions();
        }

        private static bool SamePair(Pending p, IFaction left, IFaction right) =>
            (p.Left == left && p.Right == right) || (p.Left == right && p.Right == left);
    }
}
