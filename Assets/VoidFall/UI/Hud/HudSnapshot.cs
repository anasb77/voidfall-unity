using UnityEngine;

namespace VoidFall.UI
{
    /// <summary>
    /// Immutable per-frame HUD data contract, built by the runtime once per
    /// rendered frame (~25 assignments) and consumed by
    /// <see cref="HudPresenter"/>. The presenter never references
    /// VoidFallGameRuntime, GameSim, or any live object - feed it snapshots
    /// and it drives the view sink. Additive-only when sim fields migrate.
    /// </summary>
    public readonly struct HudSnapshot
    {
        public HudSnapshot(
            float health,
            float maxHealth,
            float timeSeconds,
            int level,
            int kills,
            int partsEarned,
            int xp,
            int xpNeed,
            int score,
            bool overclockActive,
            int overclockPowerTier,
            int overclockStreak,
            float overclockRemainingSeconds,
            int activeBossCount,
            float bossHealth,
            float bossMaxHealth,
            string firstBossName,
            bool hudVisible)
        {
            Health = health;
            MaxHealth = maxHealth;
            TimeSeconds = timeSeconds;
            Level = level;
            Kills = kills;
            PartsEarned = partsEarned;
            Xp = xp;
            XpNeed = xpNeed;
            Score = score;
            OverclockActive = overclockActive;
            OverclockPowerTier = overclockPowerTier;
            OverclockStreak = overclockStreak;
            OverclockRemainingSeconds = overclockRemainingSeconds;
            ActiveBossCount = activeBossCount;
            BossHealth = bossHealth;
            BossMaxHealth = bossMaxHealth;
            FirstBossName = firstBossName ?? string.Empty;
            HudVisible = hudVisible;
        }

        public float Health { get; }
        public float MaxHealth { get; }
        public float TimeSeconds { get; }
        public int Level { get; }
        public int Kills { get; }
        public int PartsEarned { get; }
        public int Xp { get; }
        public int XpNeed { get; }
        public int Score { get; }
        public bool OverclockActive { get; }
        public int OverclockPowerTier { get; }
        public int OverclockStreak { get; }
        public float OverclockRemainingSeconds { get; }
        public int ActiveBossCount { get; }
        public float BossHealth { get; }
        public float BossMaxHealth { get; }
        public string FirstBossName { get; }
        public bool HudVisible { get; }

        /// <summary>Clamped 0..1 health fraction; zero when MaxHealth is zero.</summary>
        public float HealthFraction =>
            MaxHealth > 0 ? Mathf.Clamp01(Health / MaxHealth) : 0;

        /// <summary>Clamped 0..1 xp fraction; zero when XpNeed is zero.</summary>
        public float XpFraction => XpNeed > 0 ? Mathf.Clamp01(Xp / (float)XpNeed) : 0;

        /// <summary>
        /// Total boss health fraction across all active bosses; zero when no
        /// boss is active or totals are zero.
        /// </summary>
        public float BossFraction =>
            BossMaxHealth > 0 ? Mathf.Clamp01(BossHealth / BossMaxHealth) : 0;

        /// <summary>Boss header: single boss shows its name, multiples count.</summary>
        public string BossHeader =>
            ActiveBossCount == 1
                ? FirstBossName.ToUpperInvariant()
                : ActiveBossCount > 1 ? $"{ActiveBossCount} BOSSES" : string.Empty;
    }
}