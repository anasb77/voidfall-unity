using System;

namespace VoidFall.Core
{
    public static class DirectorRules
    {
        public const double WarningSeconds = 2.5;
        public const double FirstEventSeconds = 30;
        public const double MinEventGapSeconds = 42;
        public const double MaxEventGapSeconds = 52;
        public const double RecoverySeconds = 8;
        public const double BossMinIntervalSeconds = 150;
        public const double BossMaxIntervalSeconds = 210;
        public const double BossMinRecoverySeconds = 15;
        public const double BossMaxRecoverySeconds = 30;
        public const double MultiBossStartSeconds = 20 * 60;
        public const double EnemyPopulationMultiplier = 1.6;
        public const int BaseMaxActiveEnemies = 120;
        public const int MaxActiveEnemies = 192;
        public const double InitialVoidDoubleBossChance = 0.3;

        public static int ScaledEnemyCount(double baseCount)
        {
            return (int)Math.Ceiling(Math.Max(0, baseCount) * EnemyPopulationMultiplier);
        }

        public static int ActiveEnemyCap(double elapsedSeconds, int activeBossCount)
        {
            var bossActive = Math.Max(0, activeBossCount) > 0;
            var safeTime = Math.Max(0, elapsedSeconds);
            var baseCap = bossActive
                ? 50
                : Math.Min(BaseMaxActiveEnemies, 28 + (int)Math.Floor(safeTime * 0.3));
            return Math.Min(MaxActiveEnemies, ScaledEnemyCount(baseCap));
        }

        public static double ScaledSpawnDelay(double baseDelaySeconds)
        {
            return Math.Max(0, baseDelaySeconds) / EnemyPopulationMultiplier;
        }

        public static double EnemyThreatCost(string id)
        {
            switch (id)
            {
                case "chaser": return 1;
                case "runner": return 1;
                case "dasher": return 1.5;
                case "gunner": return 1.8;
                case "twinGunner": return 3.2;
                case "exploder": return 2.2;
                case "guard": return 2.6;
                case "brute": return 2.8;
                case "technician": return 2.4;
                case "mortar": return 3;
                case "splitter": return 2.5;
                case "bulwark": return 3.4;
                case "harvester": return 2.7;
                case "carrier": return 4.2;
                default: return 0;
            }
        }

        public static double ActiveThreatBudget(double elapsedSeconds, int activeBossCount)
        {
            var bossActive = Math.Max(0, activeBossCount) > 0;
            var populationCap = ActiveEnemyCap(elapsedSeconds, activeBossCount);
            return populationCap * (bossActive ? 1.45 : 1.6);
        }

        /// <summary>
        /// Remaps run time onto the clock used to pick an ambient spawn band.
        /// </summary>
        /// <remarks>
        /// The spawn timeline and every enemy's NaturalStartSeconds are generated
        /// parity content and must not be edited, but the browser schedule is
        /// front-loaded: seven of fourteen types are eligible by t=120, then the
        /// 120-240 band is a two-minute plateau that introduces nothing new. So
        /// the data stays untouched and the clock reading it is reshaped instead.
        ///
        /// Piecewise-linear and monotonic, anchored at (real -> roster):
        ///   0 -> 0, 120 -> 70, 240 -> 240, 360 -> 420, then +60 offset.
        /// Early time runs slow, which stretches the opening reveal to five types
        /// by t=120 instead of seven; time then runs fast, which fills the dead
        /// plateau and lands the full roster around a minute sooner than before.
        /// Deliberate divergence from the browser engine, not a parity bug.
        /// </remarks>
        public static double RosterRevealTime(double elapsedSeconds)
        {
            var time = !double.IsNaN(elapsedSeconds) && !double.IsInfinity(elapsedSeconds)
                ? Math.Max(0, elapsedSeconds)
                : 0;
            if (time <= 120) return time * (70.0 / 120.0);
            if (time <= 240) return 70 + (time - 120) * ((240.0 - 70.0) / 120.0);
            if (time <= 360) return 240 + (time - 240) * ((420.0 - 240.0) / 120.0);
            return time + 60;
        }

        public static int SwarmEnemyCount(double elapsedSeconds)
        {
            var original = 12 + (int)Math.Floor(Math.Max(0, elapsedSeconds) / 15);
            return Math.Max(10, Math.Min(30, original - 2));
        }

        public static int RusherEnemyCount(double elapsedSeconds)
        {
            return Math.Min(24, 13 + (int)Math.Floor(Math.Max(0, elapsedSeconds) / 100));
        }

        public static int EncircleEnemyCount(double elapsedSeconds)
        {
            var original = 16 + (int)Math.Floor(Math.Max(0, elapsedSeconds) / 14);
            return Math.Max(14, Math.Min(32, original - 2));
        }

        public static DirectorEventDefinition Event(uint seed, int index)
        {
            var safeIndex = Math.Max(0, index);
            var hash = EventHash(seed, safeIndex);
            var id = safeIndex % 4 == 0
                ? "swarm"
                : safeIndex % 4 == 1
                    ? "rushers"
                    : safeIndex % 4 == 2 ? "encircle" : "surge";
            var startsAt = EventStart(seed, safeIndex);
            var count = id == "swarm"
                ? SwarmEnemyCount(startsAt)
                : id == "encircle"
                    ? EncircleEnemyCount(startsAt)
                    : id == "rushers" ? RusherEnemyCount(startsAt) : 0;
            return new DirectorEventDefinition(
                safeIndex,
                id,
                startsAt - WarningSeconds,
                startsAt,
                id == "swarm" || id == "encircle" ? 0.45 : id == "rushers" ? 6 : 7,
                RecoverySeconds,
                (int)((hash >> 8) % 4),
                ((hash >> 10) / (double)0x3fffff) * Math.PI * 2,
                count);
        }

        public static string[] BossOrder(uint seed, int cycle)
        {
            var order = new string[ContentCatalog.Bosses.Length];
            for (var i = 0; i < order.Length; i++) order[i] = ContentCatalog.Bosses[i].Id;
            var safeCycle = Math.Max(0, cycle);
            var cycleSeed = seed ^ ((uint)(safeCycle + 1) * 0x85ebca6bu);
            for (var index = order.Length - 1; index > 0; index--)
            {
                var swap = (int)(EventHash(cycleSeed, index) % (uint)(index + 1));
                var hold = order[index];
                order[index] = order[swap];
                order[swap] = hold;
            }

            return order;
        }

        public static int BossIntervalSeconds(uint seed, int index)
        {
            var safeIndex = Math.Max(0, index);
            return (int)(BossMinIntervalSeconds + EventHash(seed ^ 0x6d2b79f5u, safeIndex) %
                (uint)(BossMaxIntervalSeconds - BossMinIntervalSeconds + 1));
        }

        public static bool InitialVoidDoubleBoss(uint seed, int encounterIndex)
        {
            var safeIndex = Math.Max(0, encounterIndex);
            var roll = EventHash(seed ^ 0x51ed270bu, safeIndex) / 4294967296.0;
            return roll < InitialVoidDoubleBossChance;
        }

        public static double NextBossTimeAfterSpawn(double currentTime, uint seed, int nextEncounterIndex)
        {
            var now = !double.IsNaN(currentTime) && !double.IsInfinity(currentTime)
                ? Math.Max(0, currentTime)
                : 0;
            return now + BossIntervalSeconds(seed, nextEncounterIndex);
        }

        public static int BossCapacityAt(double timeSeconds)
        {
            if (timeSeconds < MultiBossStartSeconds) return 1;
            return timeSeconds < MultiBossStartSeconds + 15 * 60 ? 2 : 3;
        }

        public static int BossPressureTierAt(double timeSeconds)
        {
            var time = !double.IsNaN(timeSeconds) && !double.IsInfinity(timeSeconds)
                ? Math.Max(0, timeSeconds)
                : 0;
            if (time < 10 * 60) return 0;
            return Math.Min(4, 1 + (int)Math.Floor((time - 10 * 60) / (10 * 60)));
        }

        public static double BossHealthScaleAt(double timeSeconds, string bossId)
        {
            var time = !double.IsNaN(timeSeconds) && !double.IsInfinity(timeSeconds)
                ? Math.Max(0, timeSeconds)
                : 0;
            var stepsAfterFirstBoss = Math.Max(0, (time - 3 * 60) / (3 * 60));
            var pressure = 1 + 0.45 * Math.Pow(stepsAfterFirstBoss, 1.45);
            var heraldLateBlend = Math.Min(1, Math.Max(0, (time - 8 * 60) / (6 * 60)));
            var identityCorrection = bossId == "herald" ? 1 + 0.45 * heraldLateBlend : 1;
            return Math.Min(30, pressure * identityCorrection);
        }

        public static double BossRecoverySeconds(double randomValue)
        {
            var roll = !double.IsNaN(randomValue) && !double.IsInfinity(randomValue)
                ? Math.Max(0, Math.Min(1, randomValue))
                : 0;
            var span = BossMaxRecoverySeconds - BossMinRecoverySeconds + 1;
            return Math.Min(BossMaxRecoverySeconds, BossMinRecoverySeconds + Math.Floor(roll * span));
        }

        public static BossScheduleResult BossScheduleAfterClear(
            double currentTime,
            double scheduledBossTime,
            double randomValue)
        {
            var now = !double.IsNaN(currentTime) && !double.IsInfinity(currentTime)
                ? Math.Max(0, currentTime)
                : 0;
            var scheduled = !double.IsNaN(scheduledBossTime) && !double.IsInfinity(scheduledBossTime)
                ? Math.Max(0, scheduledBossTime)
                : 0;
            var recoveryUntil = now + BossRecoverySeconds(randomValue);
            return new BossScheduleResult(recoveryUntil, Math.Max(scheduled, recoveryUntil));
        }

        public static BossEncounterDefinition BossEncounter(uint seed, int index)
        {
            var safeIndex = Math.Max(0, index);
            var cycle = safeIndex / ContentCatalog.Bosses.Length;
            var order = BossOrder(seed, cycle);
            var id = order[safeIndex % order.Length];
            return new BossEncounterDefinition(
                safeIndex,
                cycle,
                id,
                BossStartSeconds(seed, safeIndex),
                1,
                1 + cycle * 0.25);
        }

        private static double EventStart(uint seed, int index)
        {
            var start = FirstEventSeconds;
            for (var cursor = 0; cursor < index; cursor++)
            {
                start += MinEventGapSeconds + EventHash(seed, cursor) %
                    (uint)(MaxEventGapSeconds - MinEventGapSeconds + 1);
            }

            return start;
        }

        private static double BossStartSeconds(uint seed, int index)
        {
            var startsAt = 0;
            for (var cursor = 0; cursor <= index; cursor++) startsAt += BossIntervalSeconds(seed, cursor);
            return startsAt;
        }

        private static uint EventHash(uint seed, int index)
        {
            unchecked
            {
                return Mix32(seed ^ ((uint)(index + 1) * 0x9e3779b9u));
            }
        }

        private static uint Mix32(uint value)
        {
            unchecked
            {
                value = (value ^ (value >> 16)) * 0x7feb352du;
                value = (value ^ (value >> 15)) * 0x846ca68bu;
                return value ^ (value >> 16);
            }
        }
    }

    public readonly struct DirectorEventDefinition
    {
        public DirectorEventDefinition(int index, string id, double warningAtSeconds, double startsAtSeconds,
            double durationSeconds, double recoverySeconds, int spawnEdge, double safeGapAngle, int enemyCount)
        {
            Index = index;
            Id = id;
            WarningAtSeconds = warningAtSeconds;
            StartsAtSeconds = startsAtSeconds;
            DurationSeconds = durationSeconds;
            RecoverySeconds = recoverySeconds;
            SpawnEdge = spawnEdge;
            SafeGapAngle = safeGapAngle;
            EnemyCount = enemyCount;
        }

        public int Index { get; }
        public string Id { get; }
        public double WarningAtSeconds { get; }
        public double StartsAtSeconds { get; }
        public double DurationSeconds { get; }
        public double RecoverySeconds { get; }
        public int SpawnEdge { get; }
        public double SafeGapAngle { get; }
        public int EnemyCount { get; }
    }

    public readonly struct BossScheduleResult
    {
        public BossScheduleResult(double recoveryUntil, double nextBossTime)
        {
            RecoveryUntil = recoveryUntil;
            NextBossTime = nextBossTime;
        }

        public double RecoveryUntil { get; }
        public double NextBossTime { get; }
    }

    public readonly struct BossEncounterDefinition
    {
        public BossEncounterDefinition(int index, int cycle, string id, double startsAtSeconds,
            double healthScale, double damageScale)
        {
            Index = index;
            Cycle = cycle;
            Id = id;
            StartsAtSeconds = startsAtSeconds;
            HealthScale = healthScale;
            DamageScale = damageScale;
        }

        public int Index { get; }
        public int Cycle { get; }
        public string Id { get; }
        public double StartsAtSeconds { get; }
        public double HealthScale { get; }
        public double DamageScale { get; }
    }
}
