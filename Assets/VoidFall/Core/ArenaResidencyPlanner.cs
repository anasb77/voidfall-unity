using System;

namespace VoidFall.Core
{
    public readonly struct ArenaResidentSet
    {
        public ArenaResidentSet(ArenaPackageKey[] items)
        {
            Items = items ?? Array.Empty<ArenaPackageKey>();
        }

        public ArenaPackageKey[] Items { get; }
        public int Count => Items?.Length ?? 0;

        public bool Contains(ArenaPackageKey key)
        {
            if (!key.IsValid || Items == null) return false;
            for (var index = 0; index < Items.Length; index++)
                if (Items[index] == key) return true;
            return false;
        }
    }

    public readonly struct ArenaResidencyTransition
    {
        public ArenaResidencyTransition(
            ArenaPackageKey[] releaseBeforeAcquire,
            ArenaPackageKey[] acquire,
            ArenaPackageKey[] releaseAfterTransition)
        {
            ReleaseBeforeAcquire = releaseBeforeAcquire ?? Array.Empty<ArenaPackageKey>();
            Acquire = acquire ?? Array.Empty<ArenaPackageKey>();
            ReleaseAfterTransition = releaseAfterTransition ?? Array.Empty<ArenaPackageKey>();
        }

        public ArenaPackageKey[] ReleaseBeforeAcquire { get; }
        public ArenaPackageKey[] Acquire { get; }
        public ArenaPackageKey[] ReleaseAfterTransition { get; }
    }

    public static class ArenaResidencyPlanner
    {
        public const int MaximumResidentPackages = 5;

        public static ArenaResidentSet Steady(
            ArenaPackageKey current,
            ArenaPackageKey exitA = default,
            ArenaPackageKey exitB = default)
        {
            var items = new ArenaPackageKey[3];
            var count = 0;
            AddUnique(items, ref count, current);
            AddUnique(items, ref count, exitA);
            AddUnique(items, ref count, exitB);
            if (count == items.Length) return new ArenaResidentSet(items);
            var compact = new ArenaPackageKey[count];
            Array.Copy(items, compact, count);
            return new ArenaResidentSet(compact);
        }

        /// <summary>
        /// The menu is a freely scrollable catalogue rather than a route node.
        /// Keep every prepared preview resident so cycling never exposes the
        /// lightweight fallback while a newly selected arena starts loading.
        /// Gameplay continues to use <see cref="Steady"/> and its three-package
        /// current-plus-exits budget.
        /// </summary>
        public static ArenaResidentSet MenuCatalogue(params ArenaPackageKey[] packages)
        {
            if (packages == null || packages.Length == 0)
                return new ArenaResidentSet(Array.Empty<ArenaPackageKey>());

            var items = new ArenaPackageKey[MaximumResidentPackages];
            var count = 0;
            for (var index = 0; index < packages.Length && count < items.Length; index++)
                AddUnique(items, ref count, packages[index]);
            if (count == items.Length) return new ArenaResidentSet(items);

            var compact = new ArenaPackageKey[count];
            Array.Copy(items, compact, count);
            return new ArenaResidentSet(compact);
        }

        public static ArenaResidencyTransition Transition(
            ArenaResidentSet steady,
            ArenaPackageKey oldCurrent,
            ArenaPackageKey chosen,
            ArenaPackageKey rejected,
            ArenaPackageKey nextExitA = default,
            ArenaPackageKey nextExitB = default)
        {
            if (!chosen.IsValid || !steady.Contains(chosen))
                throw new ArgumentException("Chosen arena must already be resident.", nameof(chosen));

            var releaseBefore = rejected.IsValid && rejected != chosen && steady.Contains(rejected)
                ? new[] { rejected }
                : Array.Empty<ArenaPackageKey>();

            var acquireBuffer = new ArenaPackageKey[2];
            var acquireCount = 0;
            AddIfNew(steady, releaseBefore, acquireBuffer, ref acquireCount, nextExitA);
            AddIfNew(steady, releaseBefore, acquireBuffer, ref acquireCount, nextExitB);
            var acquire = new ArenaPackageKey[acquireCount];
            Array.Copy(acquireBuffer, acquire, acquireCount);

            var releaseAfter = oldCurrent.IsValid && oldCurrent != chosen && steady.Contains(oldCurrent)
                ? new[] { oldCurrent }
                : Array.Empty<ArenaPackageKey>();
            return new ArenaResidencyTransition(releaseBefore, acquire, releaseAfter);
        }

        private static void AddIfNew(
            ArenaResidentSet steady,
            ArenaPackageKey[] released,
            ArenaPackageKey[] output,
            ref int count,
            ArenaPackageKey key)
        {
            if (!key.IsValid) return;
            var remainsResident = steady.Contains(key) && !Contains(released, key);
            if (remainsResident || Contains(output, count, key)) return;
            output[count++] = key;
        }

        private static void AddUnique(ArenaPackageKey[] output, ref int count, ArenaPackageKey key)
        {
            if (!key.IsValid || Contains(output, count, key)) return;
            output[count++] = key;
        }

        private static bool Contains(ArenaPackageKey[] items, ArenaPackageKey key)
        {
            return Contains(items, items?.Length ?? 0, key);
        }

        private static bool Contains(ArenaPackageKey[] items, int count, ArenaPackageKey key)
        {
            if (items == null) return false;
            for (var index = 0; index < count; index++)
                if (items[index] == key) return true;
            return false;
        }
    }
}
