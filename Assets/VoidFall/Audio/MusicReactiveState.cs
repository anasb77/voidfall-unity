using System;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public readonly struct MusicReactiveState
    {
        public MusicReactiveState(
            int overclockTier,
            int overclockStreak,
            bool criticalHealth,
            bool levelUpOpen,
            float magnetIntensity,
            bool gameplayActive)
        {
            OverclockTier = Math.Max(0, Math.Min(3, overclockTier));
            OverclockStreak = Math.Max(0, overclockStreak);
            CriticalHealth = criticalHealth;
            LevelUpOpen = levelUpOpen;
            MagnetIntensity = Math.Max(0f, Math.Min(1f, magnetIntensity));
            GameplayActive = gameplayActive;
        }

        public int OverclockTier { get; }
        public int OverclockStreak { get; }
        public bool CriticalHealth { get; }
        public bool LevelUpOpen { get; }
        public float MagnetIntensity { get; }
        public bool GameplayActive { get; }
    }

    public readonly struct MusicMixTargets
    {
        public MusicMixTargets(
            float playbackRate,
            float lowPassHz,
            float lowPassResonance,
            float stereoWidth,
            float criticalWarp,
            float submersion,
            float visualDamping)
        {
            PlaybackRate = playbackRate;
            LowPassHz = lowPassHz;
            LowPassResonance = lowPassResonance;
            StereoWidth = stereoWidth;
            CriticalWarp = criticalWarp;
            Submersion = submersion;
            VisualDamping = visualDamping;
        }

        public float PlaybackRate { get; }
        public float LowPassHz { get; }
        public float LowPassResonance { get; }
        public float StereoWidth { get; }
        public float CriticalWarp { get; }
        public float Submersion { get; }
        public float VisualDamping { get; }
    }

    public static class MusicStateComposer
    {
        public const float OpenFilterHz = 22000f;
        public const float SubmergedFilterHz = 390f;

        public static MusicMixTargets Compose(in MusicReactiveState state, float criticalPulse)
        {
            if (!state.GameplayActive)
                return new MusicMixTargets(1f, OpenFilterHz, 1f, 1f, 0f, 0f, 1f);

            var tierRate = (float)OverclockRules.MusicRate(state.OverclockTier);
            var critical = state.CriticalHealth ? Math.Max(0f, Math.Min(1f, criticalPulse)) : 0f;
            var submerged = state.LevelUpOpen ? 1f : 0f;

            // Critical health does not cancel the fast tape rate. It drives a
            // separate warp envelope and darkens the track, preserving a clear
            // FAST + DRAGGED combination without pretending this is true
            // pitch-independent time stretching.
            var rate = tierRate;
            if (state.CriticalHealth && state.OverclockTier <= 0)
            {
                rate = 0.50f + critical * 0.14f;
            }
            if (state.LevelUpOpen) rate = 1f;
            var criticalDarkening = state.CriticalHealth ? 0.48f + critical * 0.22f : 0f;
            var lowPass = LogLerp(OpenFilterHz, SubmergedFilterHz, Math.Max(submerged, criticalDarkening));
            var resonance = 1f + submerged + critical * 0.7f;
            var width = Math.Max(0.24f, 1f - state.MagnetIntensity * 0.62f - critical * 0.14f);
            var visualDamping = state.LevelUpOpen ? 0.28f : state.CriticalHealth ? 0.82f : 1f;
            return new MusicMixTargets(rate, lowPass, resonance, width, critical, submerged, visualDamping);
        }

        private static float LogLerp(float from, float to, float amount)
        {
            return (float)Math.Exp(Math.Log(from) + (Math.Log(to) - Math.Log(from)) * amount);
        }

        // Menu-dialog submersion. The quit confirmation muffles the menu theme
        // the same way the upgrade screen submerges the combat OST, pushed 20%
        // deeper into the log-space filter sweep because the menu theme has no
        // combat SFX bed competing for the muffle to read against.
        public const float MenuDialogSubmersion = 1.2f;

        /// <summary>
        /// Mix for the main menu's quit dialog: same shape as the level-up
        /// submersion (rate pinned to 1, deep low-pass, raised resonance), with
        /// the submersion pushed past the authored filter point.
        /// </summary>
        public static MusicMixTargets ComposeMenuDialog()
        {
            var lowPass = LogLerp(OpenFilterHz, SubmergedFilterHz, MenuDialogSubmersion);
            return new MusicMixTargets(
                1f,
                lowPass,
                1f + MenuDialogSubmersion,
                1f,
                0f,
                MenuDialogSubmersion,
                1f);
        }
    }

    public static class MusicReactiveMath
    {
        public static float DamageScratchSeconds(float healthFraction, bool lethal)
        {
            if (lethal) return 0.34f;
            var fraction = Math.Max(0f, Math.Min(1f, healthFraction));
            return 0.055f + (0.20f - 0.055f) * Math.Min(1f, fraction * 3.2f);
        }

        public static float MagnetTarget(int pulledShardCount, float pulledXpValue)
        {
            if (pulledShardCount <= 0 || pulledXpValue <= 0f) return 0f;
            var count = Math.Log(1.0 + Math.Min(280, pulledShardCount)) / Math.Log(281.0);
            var value = Math.Log(1.0 + Math.Min(560f, pulledXpValue)) / Math.Log(561.0);
            return (float)Math.Min(1.0, count * 0.82 + value * 0.28);
        }
    }
}
