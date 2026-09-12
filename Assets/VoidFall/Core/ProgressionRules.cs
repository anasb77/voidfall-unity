using System;

namespace VoidFall.Core
{
    public static class ProgressionRules
    {
        public const int MaxWeaponRank = 6;

        // Owner-established arsenal size: four automatic weapon slots from the
        // start, a fifth earned by taking two weapons to rank VI, and a
        // separate manual legendary slot (design spec §06) that is not counted
        // here. Six total when the legendary slot is filled.
        public const int BaseWeaponSlots = 4;
        public const int ExpandedWeaponSlots = 5;
        public const int MaxedWeaponsForExtraSlot = 2;

        public static int WeaponSlotLimit(int[] ranks)
        {
            var maxed = 0;
            foreach (var rank in ranks ?? Array.Empty<int>())
            {
                if (rank >= MaxWeaponRank) maxed++;
            }

            return maxed >= MaxedWeaponsForExtraSlot ? ExpandedWeaponSlots : BaseWeaponSlots;
        }
    }
}
