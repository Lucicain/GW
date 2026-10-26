using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>固定圆形场地的几何判定。导航、静态物理与到达检查由现场提供；不随机扩大或缩小圆圈。</summary>
    internal static class GwpDuelGround
    {
        internal static Vec3 Seat(Vec3 center, int index) => Offset(center, GwpTuning.CitySearch.CourtDuelRadius,
            2d * Math.PI * index / GwpTuning.CitySearch.CourtDuelSeats);

        private static Vec3 Offset(Vec3 center, float radius, double angle) => new Vec3(
            center.x + radius * (float)Math.Cos(angle), center.y + radius * (float)Math.Sin(angle), center.z);

        internal static bool TryPick(IEnumerable<Vec3> candidates, Vec3 entrance,
            Func<Vec3, Vec3?> project, Func<Vec3, Vec3, bool> clear, Func<Vec3, bool> reachable, out Vec3 selected)
        {
            foreach (Vec3 candidate in candidates.OrderBy(point => point.DistanceSquared(entrance))
                         .Take(GwpTuning.CitySearch.CourtDuelCandidateLimit))
            {
                Vec3? center = project(candidate);
                if (center.HasValue && IsClear(center.Value, project, clear) && reachable(center.Value))
                {
                    selected = center.Value;
                    return true;
                }
            }
            selected = Vec3.Invalid;
            return false;
        }

        internal static bool IsClear(Vec3 center, Func<Vec3, Vec3?> project, Func<Vec3, Vec3, bool> clear)
        {
            float radius = GwpTuning.CitySearch.CourtDuelRadius + GwpTuning.CitySearch.CourtDuelClearance;
            bool Point(Vec3 sample)
            {
                Vec3? ground = project(sample);
                return ground.HasValue && Math.Abs(ground.Value.z - center.z) <= GwpTuning.CitySearch.CourtDuelHeightTolerance
                    && clear(center, ground.Value);
            }
            // 先查外围与固定站位，再查整个圆盘；只检查圆心/圆周会漏掉圈内的桌椅、台阶和坑。
            for (int seat = 0; seat < GwpTuning.CitySearch.CourtDuelSeats; seat++)
                if (!Point(Seat(center, seat)) || !Point(Offset(center, radius,
                        2d * Math.PI * seat / GwpTuning.CitySearch.CourtDuelSeats))) return false;
            float step = GwpTuning.CitySearch.CourtDuelGridStep;
            for (float x = -radius; x <= radius; x += step)
                for (float y = -radius; y <= radius; y += step)
                    if (x * x + y * y <= radius * radius && !Point(new Vec3(center.x + x, center.y + y, center.z)))
                        return false;
            return Point(new Vec3(center.x - GwpTuning.CitySearch.CourtDuelStartOffset, center.y, center.z))
                && Point(new Vec3(center.x + GwpTuning.CitySearch.CourtDuelStartOffset, center.y, center.z));
        }
    }
}
