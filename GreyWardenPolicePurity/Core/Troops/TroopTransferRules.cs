using System;

namespace GreyWardenPolicePurity.Core.Troops
{
    public static class TroopTransferRules
    {
        public static int SelectedExperience(int experience, int count, int moving, int capacityPerTroop)
        {
            int proportional = moving * (experience / count);
            // 原版按搬走后的人数截断经验，超出留队容量的经验随选中批次转走。
            long retainedCapacity = (long)(count - moving) * capacityPerTroop;
            return (int)Math.Max(proportional, experience - retainedCapacity);
        }
    }
}
