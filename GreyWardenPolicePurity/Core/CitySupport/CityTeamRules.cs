using System;

namespace GreyWardenPolicePurity.Core.CitySupport
{
    /// <summary>
    /// 一支城内办案队此刻的局面，由游戏层从实际部队、案卷和搜捕现场整理出来。
    /// 逻辑层只看这些字段，不碰任何游戏对象；调用方按决定执行实际动作。
    /// </summary>
    public sealed class CityTeamView
    {
        /// <summary>地图暂停或存读后，搜捕现场仍占用这支队。</summary>
        public bool UsedByActiveSearchScene;
        public bool PartyExists;
        public bool CaseOpen;
        /// <summary>目标英雄存在、活着、没被俘。</summary>
        public bool TargetAvailable;
        public bool InBattle;
        public int HealthyMembers;
        /// <summary>原版把这支队停在原地（被打过一仗后常见）。</summary>
        public bool Holding;
        public bool ForPlayer;
        public bool PlayerHoldsCase;
        /// <summary>目标此刻住在城镇或城堡里，可以接触。</summary>
        public bool ShelterKnown;
        public bool AtWarWithShelter;
        public bool ArrivedAtShelter;

    }

    public enum CityTeamAction
    {
        /// <summary>这一小时什么都不做（打仗中、目标暂时不可接触、现场占用中）。</summary>
        Wait,
        /// <summary>搜捕现场占用的队已经不在了：结束现场。</summary>
        FinishSearchScene,
        /// <summary>案子结了、目标没了或与目标所在势力开战：返程。</summary>
        Return,
        /// <summary>队伍没了或全员负伤：按原指派另派一支。</summary>
        Replace,
        /// <summary>玩家仍持有住城委托：跟着玩家。</summary>
        EscortPlayer,
        /// <summary>前往目标所在的城。</summary>
        GoToShelter,
        /// <summary>已到目标所在的城：收款结案，然后返程。</summary>
        Settle,
    }

    public readonly struct CityTeamDecision : IEquatable<CityTeamDecision>
    {
        public CityTeamDecision(CityTeamAction action, bool rethink = false, bool dropPlayerLink = false)
        {
            Action = action;
            Rethink = rethink;
            DropPlayerLink = dropPlayerLink;
        }

        public CityTeamAction Action { get; }
        /// <summary>先让原版立刻重新决策（队伍被停在原地时）。</summary>
        public bool Rethink { get; }
        /// <summary>玩家已不再持有这宗委托：这支队改为独立办理。</summary>
        public bool DropPlayerLink { get; }

        public bool Equals(CityTeamDecision other) =>
            Action == other.Action && Rethink == other.Rethink && DropPlayerLink == other.DropPlayerLink;

        public override bool Equals(object? obj) => obj is CityTeamDecision other && Equals(other);

        public override int GetHashCode() => ((int)Action * 4) + (Rethink ? 2 : 0) + (DropPlayerLink ? 1 : 0);

    }

    /// <summary>
    /// 城内办案队每小时的去留。规则见 docs/state/city-search-support.md 与 case-enforcement.md「藏城目标」：
    /// 去留只归这里，通用的临时队战后处理不替它判「目标已败」。
    /// </summary>
    public static class CityTeamRules
    {
        public static CityTeamDecision Decide(CityTeamView view)
        {
            if (view.UsedByActiveSearchScene)
                return new CityTeamDecision(view.PartyExists ? CityTeamAction.Wait : CityTeamAction.FinishSearchScene);
            if (!view.CaseOpen || !view.TargetAvailable) return new CityTeamDecision(CityTeamAction.Return);
            if (view.PartyExists && view.InBattle) return new CityTeamDecision(CityTeamAction.Wait);
            // 派遣本身不是玩家的办理次数；失队后保留原指派，直到真实兵员能补派。
            if (!view.PartyExists || view.HealthyMembers == 0) return new CityTeamDecision(CityTeamAction.Replace);
            bool rethink = view.Holding;
            bool drop = false;
            if (view.ForPlayer)
            {
                if (view.PlayerHoldsCase) return new CityTeamDecision(CityTeamAction.EscortPlayer, rethink);
                drop = true;
            }
            // 同催缴：目标暂时不可接触时保留案卷和办案队，不当作成功或重派。
            if (!view.ShelterKnown) return new CityTeamDecision(CityTeamAction.Wait, rethink, drop);
            if (view.AtWarWithShelter) return new CityTeamDecision(CityTeamAction.Return, rethink, drop);
            if (!view.ArrivedAtShelter) return new CityTeamDecision(CityTeamAction.GoToShelter, rethink, drop);
            return new CityTeamDecision(CityTeamAction.Settle, rethink, drop);
        }
    }
}
