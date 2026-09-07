using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;
using VoidFall.UI;

namespace VoidFall.Tests.PlayMode
{
    public sealed class DirectorPressureIntegrationTests
    {
        private VoidFallGameRuntime _runtime;
        private SaveData _oldProfile;
        private SaveStore _oldStore;
        private bool _oldEnabled;
        private string _directory;
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private SaveData Profile => (SaveData)Get(_runtime, "_saveData");
        private VoidObjectiveTracker Tracker => (VoidObjectiveTracker)Get(_runtime, "_objectives");
        private RunPressureState Pressure => (RunPressureState)Get(_runtime, "_runPressure");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _oldEnabled = _runtime.enabled;
            _runtime.enabled = false;
            _oldProfile = Profile;
            _oldStore = (SaveStore)Get(_runtime, "_saveStore");
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-pressure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            var store = new SaveStore(Path.Combine(_directory, "profile.json"));
            var profile = SaveStore.CreateDefault();
            store.Save(profile);
            Set(_runtime, "_saveStore", store);
            Set(_runtime, "_saveData", profile);
            Invoke(_runtime, "StartRunInternal", true, true);
            Invoke(_runtime, "DestroyEnemiesForVoidTransition");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Invoke(_runtime, "CancelDirectorSelection");
                Set(_runtime, "_runSaved", true);
                Set(_runtime, "_gameOver", false);
                Set(_runtime, "_saveData", _oldProfile);
                Set(_runtime, "_saveStore", _oldStore);
                Invoke(_runtime, "EnterMainMenu");
                _runtime.enabled = _oldEnabled;
            }
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            yield return null;
        }

        [Test]
        public void OpeningHasNoCreditAndWallClockCannotFarmPressureOrScore()
        {
            Tracker.Step(1.5);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.CreditedProgressSeconds, Is.Zero);
            var score = Invoke(_runtime, "CurrentEarnedBaseScore");
            Set(_runtime, "_time", 9000f);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.Zero);
            Assert.That(Invoke(_runtime, "CurrentEarnedBaseScore"), Is.EqualTo(score));
            Tracker.Step(149.25);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.CreditedProgressSeconds, Is.EqualTo(150).Within(.0001));
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(20));
        }

        [TestCase("_paused")]
        [TestCase("_levelUpActive")]
        [TestCase("_revivePending")]
        [TestCase("_rouletteActive")]
        [TestCase("_prizeRevealActive")]
        [TestCase("_routeMapOpen")]
        [TestCase("_riftTransitionActive")]
        public void IneligibleRuntimeStatesDoNotCreditProgress(string flag)
        {
            Tracker.Step(150);
            Set(_runtime, flag, true);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.CreditedProgressSeconds, Is.Zero);
            Set(_runtime, flag, false);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.CreditedProgressSeconds, Is.GreaterThan(0));
        }

        [Test]
        public void TravelCarriesPressureAndOnlyTheCapturedVisitReceivesCredit()
        {
            Tracker.Step(300);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(40));
            Set(_runtime, "_completedVoids", 1);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(40));
            var stage = Get(_runtime, "_journeyStage");
            Set(_runtime, "_journeyStage", Enum.Parse(stage.GetType(), "Travel"));
            Invoke(_runtime, "BeginPressureArena");
            Tracker.Begin(VoidObjectives.ForArena("red-nebula"));
            Tracker.Step(150);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(40));
            Set(_runtime, "_journeyStage", stage);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(60));
        }

        [Test]
        public void CompleteBossRegistryRetainsDefeatedMembersAndRejectsHealingCredit()
        {
            Tracker.Step(300);
            SetBoss(0, "herald", 100, 100, 101);
            SetBoss(1, "warden", 100, 100, 102);
            Invoke(_runtime, "StepRunPressure");
            SetBoss(0, "herald", 0, 100, 101, false);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(45));
            SetBoss(1, "warden", 50, 100, 102);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(47));
            var credited = Pressure.CreditedProgressSeconds;
            SetBoss(1, "warden", 100, 100, 102);
            Invoke(_runtime, "StepRunPressure");
            SetBoss(1, "warden", 50, 100, 102);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.CreditedProgressSeconds, Is.EqualTo(credited));
        }

        [Test]
        public void CourtSharedHealthIsOneEncounterPool()
        {
            Tracker.Step(300);
            SetBoss(0, "court-grandmaster-black", 100, 100, 111);
            SetBoss(1, "court-grandmaster-white", 100, 100, 112);
            Set(_runtime, "_monochromeSharedMaxHealth", 200f);
            Set(_runtime, "_monochromeSharedHealth", 100f);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Get(_runtime, "_pressureBossCount"), Is.EqualTo(1));
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(45));
            Assert.That(Pressure.CreditedProgressSeconds, Is.EqualTo(330));
        }

        [Test]
        public void FreezeIncludesSettledLevelRewardAndRemainsImmutableAcrossRetry()
        {
            Set(_runtime, "_score", 100f);
            Set(_runtime, "_xp", Get(_runtime, "_xpNeed"));
            Invoke(_runtime, "AdvanceRunLevelUps", 0f);
            Assert.That(Get(_runtime, "_level"), Is.EqualTo(2));
            Invoke(_runtime, "FreezePressureAndScore");
            Assert.That(_runtime.TerminalRunScore.BaseScore, Is.EqualTo(135));
            Set(_runtime, "_score", 10000f);
            Tracker.Step(300);
            Invoke(_runtime, "StepRunPressure");
            Invoke(_runtime, "FreezePressureAndScore");
            Assert.That(_runtime.TerminalRunScore.FinalScore, Is.EqualTo(135));
            Assert.That(_runtime.TerminalRunScore.PressureHundredths, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstCompletedLossOrWinIntroducesSelectionAndThenRemembersChoice(bool victory)
        {
            Assert.That(Invoke(_runtime, "TryOpenDirectorSelection"), Is.False);
            Assert.That(_runtime.RunDirectorProfile, Is.EqualTo(DirectorProfileId.Standard));
            Set(_runtime, "_runVictory", victory);
            Invoke(_runtime, "EndRun");
            Invoke(_runtime, "AcknowledgeDirectorResult");
            Assert.That(Invoke(_runtime, "TryOpenDirectorSelection"), Is.True);
            Invoke(_runtime, "CommitDirectorSelection", DirectorProfileId.Veteran);
            Assert.That(Profile.directorOnboardingSeen, Is.True);
            Assert.That(_runtime.RunDirectorProfile, Is.EqualTo(DirectorProfileId.Veteran));
            Assert.That(Invoke(_runtime, "TryOpenDirectorSelection"), Is.False);
        }

        [Test]
        public void LegacyProfileSelectionFailureRollsBackAndCanRetry()
        {
            Profile.stats.totalRuns = 2;
            Assert.That(Invoke(_runtime, "TryOpenDirectorSelection"), Is.True);
            var store = Get(_runtime, "_saveStore");
            var blocked = Path.Combine(_directory, "blocked");
            File.WriteAllText(blocked, "occupied");
            Set(_runtime, "_saveStore", new SaveStore(Path.Combine(blocked, "profile.json")));
            Invoke(_runtime, "CommitDirectorSelection", DirectorProfileId.Extreme);
            Assert.That(Profile.directorOnboardingSeen, Is.False);
            Assert.That(Profile.directorId, Is.Zero);
            Assert.That(Get(_runtime, "_directorSelectionOpen"), Is.True);
            Set(_runtime, "_saveStore", store);
            Invoke(_runtime, "CommitDirectorSelection", DirectorProfileId.Extreme);
            Assert.That(Profile.directorOnboardingSeen, Is.True);
            Assert.That(_runtime.RunDirectorProfile, Is.EqualTo(DirectorProfileId.Extreme));
        }

        [Test]
        public void ReviveAndAbandonedLiveRunDoNotCountAsACompletedFirstRun()
        {
            Set(_runtime, "_revivesRemaining", 1);
            Set(_runtime, "_revivePending", true);
            Invoke(_runtime, "AcceptRevive");
            Assert.That(Profile.stats.totalRuns, Is.Zero);
            Invoke(_runtime, "SaveRun");
            Invoke(_runtime, "EnterMainMenu");
            Assert.That(Profile.stats.totalRuns, Is.Zero);
            Assert.That(Invoke(_runtime, "TryOpenDirectorSelection"), Is.False);
        }

        private void SetBoss(int slot, string id, float health, float maximum, int identity, bool active = true)
        {
            var bosses = (Array)Get(Get(_runtime, "_gameSim"), "Bosses");
            var boss = Activator.CreateInstance(bosses.GetType().GetElementType());
            Set(boss, "Active", active); Set(boss, "Id", id); Set(boss, "Health", health);
            Set(boss, "MaxHealth", maximum); Set(boss, "TelemetryInstanceId", identity);
            bosses.SetValue(boss, slot);
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, Flags);
            field.SetValue(target, field.FieldType.IsPrimitive ? Convert.ChangeType(value, field.FieldType) : value);
        }
        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Flags).Invoke(target, args);
    }
}
