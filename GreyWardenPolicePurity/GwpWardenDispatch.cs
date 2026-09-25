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
        internal double SupplyStartedHours = 0d;
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
            if (parts.Length < 8 || string.IsNullOrWhiteSpace(parts[0])) return null;
            return new GwpDispatchRecord
            {
                PartyId = parts[0],
                CargoState = parts.Length > 10 ? parts[10] : string.Empty,
                CaseHeroId = parts.Length > 11 ? parts[11] : string.Empty,
                // 12 之后是后加的练兵订单字段；旧档没有，按缺省留空。
                OrderTroopId = parts.Length > 12 ? parts[12] : string.Empty,
                OrderCount = parts.Length > 13 ? ParseInt(parts[13]) : 0,
                OrderPrice = parts.Length > 14 ? ParseInt(parts[14]) : 0,
                Purpose = (GwpDispatchPurpose)ParseInt(parts[1]),
                Phase = (GwpDispatchPhase)ParseInt(parts[2]),
                ReceiverPartyId = parts[3] ?? string.Empty,
                CaseGoldFloor = ParseInt(parts[4]),
                ReportLie = parts[5] == "1",
                PrisonerHeroId = parts[6] ?? string.Empty,
                DispatchedHours = double.TryParse(parts[7],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double hours) ? hours : 0d,
                LastProgressHours = parts.Length > 8 && double.TryParse(parts[8],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double progress) ? progress : 0d,
                NextTownBusinessHours = parts.Length > 9 && double.TryParse(parts[9],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double retry) ? retry : 0d
            };
        }

        private static int ParseInt(string? value) =>
            int.TryParse(value, out int parsed) ? parsed : 0;
    }
}
