using System;
using System.Collections.Generic;
using System.Globalization;

namespace VoidFall.Core
{
    public enum UpgradeOptionKind
    {
        Weapon,
        Support,
        Late,
        Evolution,
        Repair,
    }

    public sealed class UpgradeProgress
    {
        public int[] WeaponRanks = new int[ContentOrder.Weapons.Length];
        public int[] SupportRanks = new int[ExtendedCatalog.SupportCount];
        public int[] LateRanks = new int[ContentCatalog.LateUpgrades.Length];
        public bool[] Evolved = new bool[ContentOrder.Weapons.Length];
    }

    public sealed class UpgradeOptionDefinition
    {
        public string Id;
        public UpgradeOptionKind Kind;
        public string TargetId;
        public string Name;
        public string Description;
        public int CurrentRank;
        public int NextRank;
        public int MaxRank;
        public string Accent;
        public double Weight;
    }

    public static class UpgradeRules
    {
        public const int BaseWeaponSlots = 3;
        public const int ExpandedWeaponSlots = 4;
        public const int MaxedWeaponsForExtraSlot = 2;

        public static int StartingWeaponIndex()
        {
            return WeaponIndex(ContentCatalog.Operative.StartingWeapon);
        }

        public static int WeaponSlotLimit(UpgradeProgress progress)
        {
            var maxed = 0;
            for (var index = 0; index < progress.WeaponRanks.Length; index++)
            {
                if (progress.WeaponRanks[index] >= ProgressionRules.MaxWeaponRank) maxed++;
            }

            return maxed >= MaxedWeaponsForExtraSlot ? ExpandedWeaponSlots : BaseWeaponSlots;
        }

        public static bool CoreProgressionComplete(UpgradeProgress progress)
        {
            var owned = 0;
            for (var index = 0; index < progress.WeaponRanks.Length; index++)
            {
                if (progress.WeaponRanks[index] <= 0) continue;
                owned++;
                
                var requiresEvolution = false;
                for (var i = 0; i < ContentCatalog.Evolutions.Length; i++)
                {
                    if (Array.IndexOf(ContentOrder.Weapons, WeaponIdFromName(ContentCatalog.Evolutions[i].WeaponId)) == index)
                    {
                        requiresEvolution = true;
                        break;
                    }
                }

                if (progress.WeaponRanks[index] < ProgressionRules.MaxWeaponRank || (requiresEvolution && !progress.Evolved[index])) return false;
            }

            if (owned < WeaponSlotLimit(progress)) return false;
            for (var index = 0; index < ExtendedCatalog.SupportCount; index++)
            {
                if (progress.SupportRanks[index] < ExtendedCatalog.AllSupports()[index].MaxRank) return false;
            }

            return true;
        }

        public static UpgradeOptionDefinition[] RollProgressionOptions(
            UpgradeProgress progress,
            Rng random,
            int choiceCount = 3)
        {
            var pool = new List<UpgradeOptionDefinition>();
            var slots = 0;
            for (var index = 0; index < progress.WeaponRanks.Length; index++)
            {
                if (progress.WeaponRanks[index] > 0) slots++;
            }

            var slotLimit = WeaponSlotLimit(progress);
            for (var index = 0; index < ContentCatalog.Weapons.Length; index++)
            {
                var weapon = ContentCatalog.Weapons[index];
                var rankIndex = Array.IndexOf(ContentOrder.Weapons, WeaponIdFromName(weapon.Id));
                if (rankIndex < 0) continue;

                var current = progress.WeaponRanks[rankIndex];
                if (current == 0 && slots >= slotLimit) continue;
                if (current >= ProgressionRules.MaxWeaponRank) continue;
                var next = current + 1;
                pool.Add(new UpgradeOptionDefinition
                {
                    Id = "weapon:" + weapon.Id,
                    TargetId = weapon.Id,
                    Kind = UpgradeOptionKind.Weapon,
                    Name = weapon.Name,
                    Description = WeaponUpgradeDescription(weapon, next),
                    CurrentRank = current,
                    NextRank = next,
                    MaxRank = ProgressionRules.MaxWeaponRank,
                    Accent = weapon.Accent,
                    Weight = current == 0 ? 8 : 10,
                });
            }

            for (var index = 0; index < ExtendedCatalog.SupportCount; index++)
            {
                var support = ExtendedCatalog.AllSupports()[index];
                var current = progress.SupportRanks[index];
                if (current >= support.MaxRank) continue;
                var next = current + 1;
                var descriptions = support.Descriptions ?? new string[0];
                var description = descriptions.Length >= next ? descriptions[next - 1] : support.Name;
                pool.Add(new UpgradeOptionDefinition
                {
                    Id = "support:" + support.Id,
                    TargetId = support.Id,
                    Kind = UpgradeOptionKind.Support,
                    Name = support.Name,
                    Description = description,
                    CurrentRank = current,
                    NextRank = next,
                    MaxRank = support.MaxRank,
                    Accent = support.Accent,
                    Weight = support.Weight,
                });
            }

            var picked = new List<UpgradeOptionDefinition>();
            var ready = ReadyEvolutions(progress);
            for (var index = 0; index < ready.Count && picked.Count < choiceCount; index++) picked.Add(ready[index]);

            if (pool.Count < choiceCount && CoreProgressionComplete(progress))
            {
                for (var index = 0; index < ContentCatalog.LateUpgrades.Length; index++)
                {
                    var late = ContentCatalog.LateUpgrades[index];
                    var current = progress.LateRanks[index];
                    if (current >= late.MaxRank) continue;
                    pool.Add(new UpgradeOptionDefinition
                    {
                        Id = "late:" + late.Id,
                        TargetId = late.Id,
                        Kind = UpgradeOptionKind.Late,
                        Name = late.Name,
                        Description = late.Description,
                        CurrentRank = current,
                        NextRank = current + 1,
                        MaxRank = late.MaxRank,
                        Accent = late.Accent,
                        Weight = 5,
                    });
                }
            }

            while (picked.Count < choiceCount && pool.Count > 0)
            {
                var total = 0.0;
                foreach (var option in pool) total += option.Weight;
                var sample = Math.Min(1 - 2.2204460492503131e-16, Math.Max(0, random.Next()));
                var roll = sample * total;
                var selected = 0;
                for (var index = 0; index < pool.Count; index++)
                {
                    roll -= pool[index].Weight;
                    if (roll <= 0)
                    {
                        selected = index;
                        break;
                    }
                }

                picked.Add(pool[selected]);
                pool.RemoveAt(selected);
            }

            return picked.ToArray();
        }

        private static string WeaponUpgradeDescription(WeaponDefinition weapon, int nextRank)
        {
            if (nextRank <= 1) return WeaponAcquisitionDescription(weapon);

            var current = weapon.Ranks[nextRank - 1].Stats;
            var previous = weapon.Ranks[nextRank - 2].Stats;
            var changes = new List<string>();
            AppendStatChange(changes, "Damage", previous.Damage, current.Damage);
            AppendStatChange(changes, "Fire delay", previous.Cooldown, current.Cooldown, " seconds", 2);
            AppendStatChange(changes, "Range", previous.Range, current.Range);
            AppendStatChange(changes, "Projectile speed", previous.ProjectileSpeed, current.ProjectileSpeed);
            AppendStatChange(changes, "Projectiles", previous.ProjectileCount, current.ProjectileCount);
            AppendStatChange(changes, "Spread", previous.SpreadDegrees, current.SpreadDegrees, " degrees");
            AppendStatChange(changes, "Targets passed through", previous.Pierce, current.Pierce);
            AppendStatChange(changes, "Knockback", previous.Knockback, current.Knockback);
            AppendStatChange(changes, "Shot width", previous.ProjectileRadius, current.ProjectileRadius);
            AppendStatChange(changes, "Blast radius", previous.BlastRadius, current.BlastRadius);
            AppendStatChange(changes, "Blades", previous.OrbitCount, current.OrbitCount);
            AppendStatChange(changes, "Orbit radius", previous.OrbitRadius, current.OrbitRadius);
            AppendStatChange(changes, "Orbit speed", previous.OrbitSpeed, current.OrbitSpeed, "", 1);
            AppendStatChange(changes, "Repeat delay", previous.HitCooldown, current.HitCooldown, " seconds", 2);
            AppendStatChange(changes, "Arc jumps", previous.ChainCount, current.ChainCount);
            return string.Join(". ", changes) + ".";
        }

        private static string WeaponAcquisitionDescription(WeaponDefinition weapon)
        {
            var first = weapon.Ranks[0].Stats;
            switch (weapon.Kind)
            {
                case "orbit":
                    return first.OrbitCount + " " + (first.OrbitCount == 1 ? "blade" : "blades") + ", " +
                        FormatSourceNumber(first.Damage) + " damage per hit, " +
                        FormatFixed(first.HitCooldown, 2) + " second repeat delay.";
                case "scatter":
                    return FormatSourceNumber(first.ProjectileCount) + " pellets at " +
                        FormatSourceNumber(first.Damage) + " damage each, " +
                        FormatFixed(first.Cooldown, 2) + " second fire delay.";
                case "rail":
                    return FormatSourceNumber(first.Damage) + " damage, passes through " +
                        FormatSourceNumber(first.Pierce) + " targets, " +
                        FormatFixed(first.Cooldown, 2) + " second fire delay.";
                case "chain":
                    return FormatSourceNumber(first.Damage) + " damage, jumps to " +
                        FormatSourceNumber(first.ChainCount) + " more enemies, " +
                        FormatFixed(first.Cooldown, 2) + " second fire delay.";
                case "homing":
                    return FormatSourceNumber(first.ProjectileCount) + " homing " +
                        (first.ProjectileCount == 1 ? "missile" : "missiles") + " at " +
                        FormatSourceNumber(first.Damage) + " damage, " +
                        FormatSourceNumber(first.BlastRadius) + " blast, " +
                        FormatFixed(first.Cooldown, 2) + " second fire delay.";
                default:
                    return FormatSourceNumber(first.Damage) + " damage, " +
                        FormatFixed(first.Cooldown, 2) + " second fire delay, " +
                        FormatSourceNumber(first.Range) + " range.";
            }
        }

        private static void AppendStatChange(
            List<string> changes,
            string label,
            double before,
            double after,
            string suffix = "",
            int fixedDigits = -1)
        {
            if (before == after) return;
            var beforeText = fixedDigits < 0 ? FormatSourceNumber(before) : FormatFixed(before, fixedDigits);
            var afterText = fixedDigits < 0 ? FormatSourceNumber(after) : FormatFixed(after, fixedDigits);
            changes.Add(label + " " + beforeText + " to " + afterText + suffix);
        }

        private static string FormatSourceNumber(double value)
        {
            return value.ToString("0.################", CultureInfo.InvariantCulture);
        }

        private static string FormatFixed(double value, int digits)
        {
            return value.ToString(digits == 1 ? "0.0" : "0.00", CultureInfo.InvariantCulture);
        }

        public static bool Apply(UpgradeProgress progress, UpgradeOptionDefinition option)
        {
            var index = Array.IndexOf(ContentOrder.Weapons, WeaponIdFromName(option.TargetId));
            if (option.Kind == UpgradeOptionKind.Weapon && index >= 0)
            {
                if (progress.WeaponRanks[index] != option.CurrentRank || option.NextRank > ProgressionRules.MaxWeaponRank) return false;
                progress.WeaponRanks[index] = option.NextRank;
                return true;
            }

            if (option.Kind == UpgradeOptionKind.Evolution && index >= 0)
            {
                if (!IsEvolutionReady(progress, index)) return false;
                progress.Evolved[index] = true;
                return true;
            }

            if (option.Kind == UpgradeOptionKind.Support)
            {
                var supportIndex = SupportIndex(option.TargetId);
                if (supportIndex < 0 || progress.SupportRanks[supportIndex] != option.CurrentRank) return false;
                progress.SupportRanks[supportIndex] = option.NextRank;
                return true;
            }

            if (option.Kind == UpgradeOptionKind.Late)
            {
                var lateIndex = LateIndex(option.TargetId);
                if (lateIndex < 0 || progress.LateRanks[lateIndex] != option.CurrentRank) return false;
                progress.LateRanks[lateIndex] = option.NextRank;
                return true;
            }

            return option.Kind == UpgradeOptionKind.Repair;
        }

        private static List<UpgradeOptionDefinition> ReadyEvolutions(UpgradeProgress progress)
        {
            var ready = new List<UpgradeOptionDefinition>();
            foreach (var evolution in ContentCatalog.Evolutions)
            {
                var weaponIndex = WeaponIndex(evolution.WeaponId);
                var rankIndex = Array.IndexOf(ContentOrder.Weapons, WeaponIdFromName(evolution.WeaponId));
                var supportIndex = SupportIndex(evolution.SupportId);
                if (!IsEvolutionReady(progress, rankIndex, supportIndex)) continue;
                var weapon = ContentCatalog.Weapons[weaponIndex];
                var support = ContentCatalog.Supports[supportIndex];
                ready.Add(new UpgradeOptionDefinition
                {
                    Id = "evolution:" + evolution.WeaponId,
                    TargetId = evolution.WeaponId,
                    Kind = UpgradeOptionKind.Evolution,
                    Name = evolution.Name,
                    Description = evolution.Description + " Requires " + weapon.Name + " VI and " + support.Name + " " + support.MaxRank + ".",
                    CurrentRank = 0,
                    NextRank = 1,
                    MaxRank = 1,
                    Accent = evolution.Accent,
                    Weight = 0,
                });
            }

            return ready;
        }

        private static bool IsEvolutionReady(UpgradeProgress progress, int rankIndex, int supportIndex = -1)
        {
            if (rankIndex < 0) return false;
            if (supportIndex < 0)
            {
                foreach (var evolution in ContentCatalog.Evolutions)
                {
                    if (Array.IndexOf(ContentOrder.Weapons, WeaponIdFromName(evolution.WeaponId)) == rankIndex)
                    {
                        supportIndex = SupportIndex(evolution.SupportId);
                        break;
                    }
                }
            }

            return supportIndex >= 0 && progress.WeaponRanks[rankIndex] >= ProgressionRules.MaxWeaponRank &&
                progress.SupportRanks[supportIndex] >= ExtendedCatalog.AllSupports()[supportIndex].MaxRank &&
                !progress.Evolved[rankIndex];
        }

        private static int WeaponIndex(string id)
        {
            for (var index = 0; index < ContentCatalog.Weapons.Length; index++) if (ContentCatalog.Weapons[index].Id == id) return index;
            return -1;
        }

        private static WeaponId WeaponIdFromName(string id)
        {
            if (Enum.TryParse<WeaponId>(id, true, out var wId)) return wId;
            return (WeaponId)(-1);
        }

        private static int SupportIndex(string id)
        {
            for (var index = 0; index < ExtendedCatalog.SupportCount; index++) if (ExtendedCatalog.AllSupports()[index].Id == id) return index;
            return -1;
        }

        private static int LateIndex(string id)
        {
            for (var index = 0; index < ContentCatalog.LateUpgrades.Length; index++) if (ContentCatalog.LateUpgrades[index].Id == id) return index;
            return -1;
        }
    }
}
