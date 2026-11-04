using System;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

namespace GreyWardenTestBridge
{
    /// <summary>
    /// 推进战役时间：用原版快进（<see cref="CampaignTimeControlMode.UnstoppableFastForward"/>），走正常的战役 tick，
    /// 不跳 tick，各行为照常运行；到达目标 <see cref="CampaignTime"/> 后暂停再回复（PROTOCOL.md §5 advance）。
    /// 弹出要玩家处理的界面导致时间停走超过 <see cref="BridgeProtocol.AdvanceStallSeconds"/> 秒（真实时间）就报 blocked，
    /// 不替玩家点（PROTOCOL.md §7.5）。
    /// </summary>
    internal sealed class BridgeAdvance : BridgePending
    {
        /// <summary>与 Python 端 Config.max_advance_hours 相同。</summary>
        private const double MaxHours = 24.0 * 100;

        private readonly BridgeGameState _game;
        private readonly JToken _requested;
        private readonly CampaignTime _before;
        private readonly double _targetHours;
        private readonly Stopwatch _sinceProgress = Stopwatch.StartNew();
        private double _lastHours;

        private BridgeAdvance(BridgeRequest request, BridgeGameState game, JToken requested, double hours)
            : base(request)
        {
            _game = game;
            _requested = requested;
            _before = CampaignTime.Now;
            _lastHours = _before.ToHours;
            _targetHours = _lastHours + hours;
        }

        internal static (BridgePending?, BridgeReply?) Create(BridgeRequest request, BridgeGameState game)
        {
            JToken? token = request.Args["hours"];
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
                return (null, BridgeReply.Fail("bad_request", "advance 需要数字参数 hours"));
            double hours = token.Value<double>();
            if (!(hours > 0 && hours <= MaxHours))
                return (null, BridgeReply.Fail("bad_request", "hours 必须在 (0, " + MaxHours + "] 内：" + hours));
            if (!game.IsCampaignMap)
                return (null, BridgeReply.Fail("invalid_state", "只能在战役地图上推进时间，当前 " + game.Describe()));

            var command = new BridgeAdvance(request, game, token.DeepClone(), hours);
            command.Run();
            return (command, null);
        }

        internal override BridgeReply? Tick()
        {
            Campaign? campaign = Campaign.Current;
            if (campaign == null)
                return BridgeReply.Fail("invalid_state", "推进途中战役没了", Progress(null));

            double now = CampaignTime.Now.ToHours;
            if (now >= _targetHours)
            {
                campaign.TimeControlMode = CampaignTimeControlMode.Stop;
                return BridgeReply.Ok(Progress(CampaignTime.Now));
            }

            if (now > _lastHours)
            {
                _lastHours = now;
                _sinceProgress.Restart();
            }

            if (_sinceProgress.Elapsed.TotalSeconds > BridgeProtocol.AdvanceStallSeconds)
            {
                JObject data = _game.DescribeBlocker();
                data["hours_advanced"] = Math.Round(now - _before.ToHours, 3);
                if (_game.IsCampaignMap) campaign.TimeControlMode = CampaignTimeControlMode.Stop;
                return BridgeReply.Fail("blocked", "时间停走超过 " + BridgeProtocol.AdvanceStallSeconds + " 秒，原因：" +
                    data.Value<string>("reason") + (data.Value<string>("title") is string title ? "（" + title + "）" : string.Empty), data);
            }

            Run();
            return null;
        }

        /// <summary>
        /// 没有弹窗、对话挡着时保持快进。原版遇事会把时间设回暂停（例如遭遇、询问框），弹窗还在时不去覆盖它，
        /// 由停走计时报 blocked。
        /// </summary>
        private void Run()
        {
            Campaign campaign = Campaign.Current;
            if (!_game.IsCampaignMap || _game.ModalBlocker() != null
                || campaign.ConversationManager?.IsConversationInProgress == true) return;
            if (campaign.TimeControlMode != CampaignTimeControlMode.UnstoppableFastForward)
                campaign.TimeControlMode = CampaignTimeControlMode.UnstoppableFastForward;
        }

        private JObject Progress(CampaignTime? after)
        {
            var result = new JObject
            {
                ["hours_requested"] = _requested,
                ["hours_advanced"] = after.HasValue ? Math.Round(after.Value.ToHours - _before.ToHours, 3) : (double?)null,
                ["campaign_time_before"] = BridgeGameState.Time(_before),
            };
            if (after.HasValue) result["campaign_time_after"] = BridgeGameState.Time(after.Value);
            return result;
        }
    }
}
