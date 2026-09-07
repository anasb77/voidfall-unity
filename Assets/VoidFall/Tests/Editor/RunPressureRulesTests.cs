using System;
using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class RunPressureRulesTests
    {
        [Test]
        public void New_pressure_state_starts_empty_and_unfrozen()
        {
            var pressure = new RunPressureState();

            Assert.That(pressure.PressureHundredths, Is.EqualTo(0));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(0));
            Assert.That(pressure.IsFrozen, Is.False);
        }

        [Test]
        public void Survival_and_boss_progress_have_independent_high_water_marks()
        {
            var pressure = new RunPressureState();
            pressure.Reset(300, 6);

            pressure.ObserveStage(0, 1, 0);
            Assert.That(pressure.PressureHundredths, Is.EqualTo(40));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(300));

            pressure.ObserveStage(0, 1, 1);
            Assert.That(pressure.PressureHundredths, Is.EqualTo(50));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(360));

            pressure.ObserveStage(0, 0.2, 0.1);
            Assert.That(pressure.PressureHundredths, Is.EqualTo(50));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(360));
        }

        [Test]
        public void Partial_progress_carries_across_stages_and_floors_once()
        {
            var pressure = new RunPressureState();
            pressure.Reset(500, 2);

            pressure.ObserveStage(0, 0.5, 0.25);
            pressure.ObserveStage(1, 0.25, 0.5);

            Assert.That(pressure.PressureHundredths, Is.EqualTo(187));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(270));
        }

        [Test]
        public void Duplicate_and_regressing_reports_never_change_earned_progress()
        {
            var pressure = new RunPressureState();
            pressure.Reset(300, 2);
            pressure.ObserveStage(1, 0.6, 0.7);

            var earnedPressure = pressure.PressureHundredths;
            var earnedSeconds = pressure.CreditedProgressSeconds;

            pressure.ObserveStage(1, 0.6, 0.7);
            pressure.ObserveStage(1, 0.9, 0.4);
            pressure.ObserveStage(1, 0.3, 0.9);

            Assert.That(pressure.PressureHundredths, Is.EqualTo(135));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(324));
            Assert.That(earnedPressure, Is.EqualTo(93));
            Assert.That(earnedSeconds, Is.EqualTo(222));

            pressure.ObserveStage(1, 0.5, 0.8);
            Assert.That(pressure.PressureHundredths, Is.EqualTo(135));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(324));
        }

        [Test]
        public void Invalid_reports_are_ignored_and_finite_fractions_are_clamped()
        {
            var pressure = new RunPressureState();
            pressure.Reset(300, 2);

            pressure.ObserveStage(-1, 1, 1);
            pressure.ObserveStage(2, 1, 1);
            pressure.ObserveStage(0, double.NaN, double.PositiveInfinity);
            pressure.ObserveStage(0, double.NegativeInfinity, double.NaN);
            Assert.That(pressure.PressureHundredths, Is.EqualTo(0));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(0));

            pressure.ObserveStage(0, -4, 8);
            Assert.That(pressure.PressureHundredths, Is.EqualTo(30));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(60));
        }

        [Test]
        public void Reset_sanitizes_limits_and_clears_frozen_progress()
        {
            var pressure = new RunPressureState();
            pressure.Reset(300, 1);
            pressure.ObserveStage(0, 1, 1);
            pressure.Freeze();

            pressure.Reset(-25, 0);
            pressure.ObserveStage(0, 1, 1);

            Assert.That(pressure.PressureHundredths, Is.EqualTo(0));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(360));
            Assert.That(pressure.IsFrozen, Is.False);
        }

        [Test]
        public void Freeze_blocks_reports_until_the_next_reset()
        {
            var pressure = new RunPressureState();
            pressure.Reset(300, 2);
            pressure.ObserveStage(0, 0.5, 0);
            pressure.Freeze();

            pressure.ObserveStage(0, 1, 1);
            pressure.ObserveStage(1, 1, 1);

            Assert.That(pressure.PressureHundredths, Is.EqualTo(60));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(150));
            Assert.That(pressure.IsFrozen, Is.True);
        }

        [Test]
        public void Fully_completed_stages_reach_the_exact_pressure_ceiling()
        {
            var pressure = new RunPressureState();
            pressure.Reset(300, 6);

            for (var stage = 0; stage < 6; stage++)
                pressure.ObserveStage(stage, 1, 1);

            Assert.That(pressure.PressureHundredths, Is.EqualTo(300));
            Assert.That(pressure.CreditedProgressSeconds, Is.EqualTo(2160));
        }

        [Test]
        public void Score_uses_the_published_hundredths_with_a_one_times_floor()
        {
            Assert.That(RunScoreRules.Version, Is.EqualTo(1));
            Assert.That(RunScoreRules.FinalScore(1000000, 200), Is.EqualTo(2000000));
            Assert.That(RunScoreRules.FinalScore(10000000, 300), Is.EqualTo(30000000));
            Assert.That(RunScoreRules.FinalScore(1000, 37), Is.EqualTo(1000));
        }

        [Test]
        public void Score_rounds_half_up_once_and_clamps_negative_inputs()
        {
            Assert.That(RunScoreRules.FinalScore(105, 150), Is.EqualTo(158));
            Assert.That(RunScoreRules.FinalScore(1, 150), Is.EqualTo(2));
            Assert.That(RunScoreRules.FinalScore(-10, 300), Is.EqualTo(0));
            Assert.That(RunScoreRules.FinalScore(1000, -50), Is.EqualTo(1000));
        }

        [Test]
        public void Score_saturates_without_overflowing_long()
        {
            Assert.That(RunScoreRules.FinalScore(long.MaxValue, 100), Is.EqualTo(long.MaxValue));
            Assert.That(RunScoreRules.FinalScore(long.MaxValue, 101), Is.EqualTo(long.MaxValue));
            Assert.That(RunScoreRules.FinalScore(long.MaxValue, int.MaxValue), Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void Frozen_score_exposes_the_sanitized_inputs_and_result()
        {
            var score = new FrozenRunScore(105, 150);
            var sanitized = new FrozenRunScore(-1, -1);

            Assert.That(score.BaseScore, Is.EqualTo(105));
            Assert.That(score.PressureHundredths, Is.EqualTo(150));
            Assert.That(score.MultiplierHundredths, Is.EqualTo(150));
            Assert.That(score.FinalScore, Is.EqualTo(158));
            Assert.That(sanitized.BaseScore, Is.EqualTo(0));
            Assert.That(sanitized.PressureHundredths, Is.EqualTo(0));
            Assert.That(sanitized.MultiplierHundredths, Is.EqualTo(100));
            Assert.That(sanitized.FinalScore, Is.EqualTo(0));
        }

        [Test]
        public void Profiles_expose_the_approved_identity_ceiling_and_recovery()
        {
            var standard = DirectorProfiles.For(DirectorProfileId.Standard);
            var veteran = DirectorProfiles.For(DirectorProfileId.Veteran);
            var extreme = DirectorProfiles.For(DirectorProfileId.Extreme);

            Assert.That(standard.Name, Is.EqualTo("Director I"));
            Assert.That(standard.Recommendation, Does.Contain("beginner"));
            Assert.That(standard.PressureCeilingHundredths, Is.EqualTo(300));
            Assert.That(standard.RecoverySeconds, Is.EqualTo(6));

            Assert.That(veteran.Name, Is.EqualTo("Director II"));
            Assert.That(veteran.Recommendation, Does.Contain("veteran"));
            Assert.That(veteran.PressureCeilingHundredths, Is.EqualTo(500));
            Assert.That(veteran.RecoverySeconds, Is.EqualTo(4.5));

            Assert.That(extreme.Name, Is.EqualTo("Director III"));
            Assert.That(extreme.Recommendation, Does.Contain("extreme"));
            Assert.That(extreme.Recommendation, Does.Contain("YOU WILL NOT SURVIVE"));
            Assert.That(extreme.PressureCeilingHundredths, Is.EqualTo(900));
            Assert.That(extreme.RecoverySeconds, Is.EqualTo(3));
        }

        [Test]
        public void Population_limits_use_all_profile_pressure_boundaries()
        {
            AssertPopulationBoundaries(DirectorProfileId.Standard, 90, 210);
            AssertPopulationBoundaries(DirectorProfileId.Veteran, 150, 350);
            AssertPopulationBoundaries(DirectorProfileId.Extreme, 270, 630);
        }

        [Test]
        public void Attack_limits_use_each_profiles_approved_tiers()
        {
            AssertAttackBoundaries(DirectorProfileId.Standard, 90, 210, 1, 1, 2);
            AssertAttackBoundaries(DirectorProfileId.Veteran, 150, 350, 1, 2, 2);
            AssertAttackBoundaries(DirectorProfileId.Extreme, 270, 630, 2, 2, 3);
        }

        [Test]
        public void Invalid_profile_falls_back_to_standard_rules()
        {
            var invalid = (DirectorProfileId)999;
            var profile = DirectorProfiles.For(invalid);

            Assert.That(profile.Id, Is.EqualTo(DirectorProfileId.Standard));
            Assert.That(profile.Name, Is.EqualTo("Director I"));
            Assert.That(DirectorProfiles.PopulationLimit(invalid, 209), Is.EqualTo(128));
            Assert.That(DirectorProfiles.AttackLimit(invalid, 210), Is.EqualTo(2));
        }

        private static void AssertPopulationBoundaries(
            DirectorProfileId id,
            int thirtyPercent,
            int seventyPercent)
        {
            Assert.That(DirectorProfiles.PopulationLimit(id, -1), Is.EqualTo(64));
            Assert.That(DirectorProfiles.PopulationLimit(id, thirtyPercent - 1), Is.EqualTo(64));
            Assert.That(DirectorProfiles.PopulationLimit(id, thirtyPercent), Is.EqualTo(128));
            Assert.That(DirectorProfiles.PopulationLimit(id, seventyPercent - 1), Is.EqualTo(128));
            Assert.That(DirectorProfiles.PopulationLimit(id, seventyPercent), Is.EqualTo(192));
            Assert.That(DirectorProfiles.PopulationLimit(id, int.MaxValue), Is.EqualTo(192));
        }

        private static void AssertAttackBoundaries(
            DirectorProfileId id,
            int thirtyPercent,
            int seventyPercent,
            int low,
            int middle,
            int high)
        {
            Assert.That(DirectorProfiles.AttackLimit(id, thirtyPercent - 1), Is.EqualTo(low));
            Assert.That(DirectorProfiles.AttackLimit(id, thirtyPercent), Is.EqualTo(middle));
            Assert.That(DirectorProfiles.AttackLimit(id, seventyPercent - 1), Is.EqualTo(middle));
            Assert.That(DirectorProfiles.AttackLimit(id, seventyPercent), Is.EqualTo(high));
        }
    }
}
