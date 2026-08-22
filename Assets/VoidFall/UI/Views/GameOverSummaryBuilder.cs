using System;
using System.Collections.Generic;
using VoidFall.Core;

namespace VoidFall.UI
{
    /// <summary>
    /// Assembles the run-result screen's summary from explicit inputs.
    /// Extracted from the runtime so the projection is unit-testable; the
    /// runtime remains responsible for gathering state and building recap
    /// chips.
    /// </summary>
    public static class GameOverSummaryBuilder
    {
        public static GameOverSummary Build(
            bool victory,
            int score,
            float elapsedSeconds,
            int kills,
            int eliteKills,
            int bossKills,
            int level,
            int partsEarned,
            bool isBest,
            bool saved,
            int[] weaponRanks,
            double[] weaponDamage,
            double totalDamageDealt,
            List<UIBuildChip> buildChips)
        {
            var summary = new GameOverSummary
            {
                Victory = victory,
                Score = score,
                ElapsedSeconds = elapsedSeconds,
                Kills = kills,
                EliteKills = eliteKills,
                BossKills = bossKills,
                Level = level,
                PartsEarned = partsEarned,
                IsBest = isBest,
                Saved = saved,
                Weapons = new List<WeaponStatSummary>(),
                BuildChips = buildChips
            };

            if (weaponRanks != null)
            {
                // The browser divides each weapon's damage by total damage
                // dealt across ALL sources - floored at one so an all-zero
                // profile cannot divide by zero - then clamps to 0..1 so a
                // single weapon can never exceed the whole bar.
                var denominator = (float)Math.Max(1.0, totalDamageDealt);
                var weaponCount = Math.Min(
                    ContentCatalog.Weapons.Length,
                    weaponRanks.Length);
                for (var index = 0; index < weaponCount; index++)
                {
                    var rank = weaponRanks[index];
                    if (rank <= 0) continue;
                    var damage = weaponDamage != null && index < weaponDamage.Length
                        ? (long)weaponDamage[index]
                        : 0L;
                    summary.Weapons.Add(new WeaponStatSummary
                    {
                        Name = ContentCatalog.Weapons[index].Name,
                        Rank = rank,
                        Damage = damage,
                        DamagePercent = UnityEngine.Mathf.Clamp01(
                            (float)damage / denominator)
                    });
                }
            }

            return summary;
        }
    }
}
