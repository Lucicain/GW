using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    internal enum GwpDispatchPurpose
    {
        /// <summary>带着上缴的钱物、必要时带着俘虏，替玩家去向灰袍复命。</summary>
        Report = 0,
        /// <summary>把求援的话带到灰袍手里。到达之后支援才成立。</summary>
        Support = 1,
        /// <summary>把练兵订单和预付的订金带到灰袍手里。到达之后订单才成立，
        /// 练好的兵仍由灰袍领主亲自送来——使者不运兵。</summary>
        TroopOrder = 2,
        /// <summary>请灰袍出面，把玩家替他们打出来的那些战争一并了结。空手去空手回。</summary>
        PeaceRequest = 3
    }

    internal enum GwpDispatchPhase
    {
        Outbound = 0,
        Returning = 1,
        Rejoined = 2
    }

    /// <summary>
    /// 一支真正从玩家队里分出去的队伍。它带走的是真的人、真的钱和真的俘虏，
    /// 路上会遇敌、会挨打、要发饷，办完事再把剩下的全部带回来。
    /// </summary>
    internal sealed class GwpDispatchRecord
    {
        internal string PartyId = string.Empty;
        internal GwpDispatchPurpose Purpose;
        internal GwpDispatchPhase Phase;
        internal string ReceiverPartyId = string.Empty;
        /// <summary>随队的案件款。这笔钱不许拿去发工资。</summary>
        internal int CaseGoldFloor;
        internal string CargoState = string.Empty;
        internal string CaseHeroId = string.Empty;
        /// <summary>玩家出发前就定好的说法，士兵不替他改口。</summary>
        internal bool ReportLie;
        internal string PrisonerHeroId = string.Empty;
        internal double DispatchedHours;
        /// <summary>上一次已经报过的险情。同一种情况不重复通知。（运行时，不入存档）</summary>
        internal string LastWarning = string.Empty;

        /// <summary>上一次记下的与收件人的距离，以及记下它的时刻。用来发现"走不动了"。</summary>
        internal float LastDistance = float.MaxValue;
        internal double LastProgressHours;
        internal double NextTownBusinessHours;
        internal string SupplyTownId = string.Empty;
        internal bool HandoverPending = false;

        /// <summary>练兵订单随队带走的单据：兵种、数量、玩家已预付的订金。</summary>
        internal string OrderTroopId = string.Empty;
        internal int OrderCount;
        internal int OrderPrice;

        internal string Serialize() => string.Join("|",
            PartyId, ((int)Purpose).ToString(), ((int)Phase).ToString(), ReceiverPartyId,
            CaseGoldFloor.ToString(), ReportLie ? "1" : "0", PrisonerHeroId,
            DispatchedHours.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            LastProgressHours.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            NextTownBusinessHours.ToString("R", System.Globalization.CultureInfo.InvariantCulture), CargoState, CaseHeroId,
            OrderTroopId, OrderCount.ToString(), OrderPrice.ToString());

        internal static GwpDispatchRecord? Deserialize(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            string[] parts = line!.Split('|');
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            return new GwpDispatchRecord
            {
                PartyId = parts[0],
                Purpose = (GwpDispatchPurpose)ParseInt(parts[1]),
                Phase = (GwpDispatchPhase)ParseInt(parts[2]),
                ReceiverPartyId = parts[3],
                CaseGoldFloor = ParseInt(parts[4]),
                ReportLie = parts[5] == "1",
                PrisonerHeroId = parts[6],
                DispatchedHours = double.Parse(parts[7], invariant),
                LastProgressHours = double.Parse(parts[8], invariant),
                NextTownBusinessHours = double.Parse(parts[9], invariant),
                CargoState = parts[10],
                CaseHeroId = parts[11],
                OrderTroopId = parts[12],
                OrderCount = ParseInt(parts[13]),
                OrderPrice = ParseInt(parts[14])
            };
        }

        private static int ParseInt(string value) =>
            int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
