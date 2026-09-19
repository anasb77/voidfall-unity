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
        public void Boss_completion_credit_survives_an_upgrade_prompt_in_the_same_tick()
        {
            Tracker.Step(VoidProgressionRules.SurvivalSeconds);Set(_runtime,"_voidBossEncounterSpawned",true);
            SetBoss(0,"herald",100,100,101);Tracker.NotifyNamedSpawned("herald");Tracker.Step(0);
            Invoke(_runtime,"StepRunPressure");
            SetBoss(0,"herald",0,100,101,false);Tracker.NotifyNamedKilled("herald");
            Invoke(_runtime,"OpenLevelUp");Set(_runtime,"_levelUpTimer",.001f);
            Invoke(_runtime,"Simulate",1.0/60);
            Assert.That(Get(_runtime,"_levelUpActive"),Is.True);
            Assert.That(Pressure.PressureHundredths,Is.EqualTo(133));
            Assert.That(Pressure.CreditedProgressSeconds,Is.EqualTo(360));
        }

        [Test]
        public void Terminal_tick_credits_boss_damage_before_freezing_the_result()
        {
            Tracker.Step(VoidProgressionRules.SurvivalSeconds);Set(_runtime,"_voidBossEncounterSpawned",true);
            SetBoss(0,"herald",100,100,101);Invoke(_runtime,"StepRunPressure");
            SetBoss(0,"herald",40,100,101);
            var game=Get(_runtime,"_gameSim");var bosses=(Array)Get(game,"Bosses");var boss=bosses.GetValue(0);
            Set(boss,"Position",new Vector2(1000,1000));bosses.SetValue(boss,0);
            var player=Get(game,"Player");Set(player,"Health",0f);Set(player,"DyingTimer",.001f);Set(game,"Player",player);
            Set(_runtime,"_revivesRemaining",0);Invoke(_runtime,"Simulate",1.0/60);
            Assert.That(Get(_runtime,"_gameOver"),Is.True);
            Assert.That(_runtime.TerminalRunScore.PressureHundredths,Is.EqualTo(130));
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
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(100));
            Assert.That(Invoke(_runtime, "CurrentEarnedBaseScore"), Is.EqualTo(score));
            Tracker.Step((VoidProgressionRules.SurvivalSeconds - 1.5) / 2);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.CreditedProgressSeconds, Is.EqualTo(150).Within(.0001));
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(113));
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
            Tracker.Step(VoidProgressionRules.SurvivalSeconds);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(126));
            Set(_runtime, "_completedVoids", 1);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(126));
            var stage = Get(_runtime, "_journeyStage");
            Set(_runtime, "_journeyStage", Enum.Parse(stage.GetType(), "Travel"));
            Invoke(_runtime, "BeginPressureArena");
            Tracker.Begin(VoidObjectives.ForArena("red-nebula"));
            Tracker.Step(VoidProgressionRules.SurvivalSeconds / 2);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(126));
            Set(_runtime, "_journeyStage", stage);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(140));
        }

        [Test]
        public void CompleteBossRegistryRetainsDefeatedMembersAndRejectsHealingCredit()
        {
            Tracker.Step(VoidProgressionRules.SurvivalSeconds);
            SetBoss(0, "herald", 100, 100, 101);
            SetBoss(1, "warden", 100, 100, 102);
            Invoke(_runtime, "StepRunPressure");
            SetBoss(0, "herald", 0, 100, 101, false);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(130));
            SetBoss(1, "warden", 50, 100, 102);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(131));
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
            Tracker.Step(VoidProgressionRules.SurvivalSeconds);
            SetBoss(0, "court-grandmaster-black", 100, 100, 111);
            SetBoss(1, "court-grandmaster-white", 100, 100, 112);
            Set(_runtime, "_monochromeSharedMaxHealth", 200f);
            Set(_runtime, "_monochromeSharedHealth", 100f);
            Invoke(_runtime, "StepRunPressure");
            Assert.That(Get(_runtime, "_pressureBossCount"), Is.EqualTo(1));
            Assert.That(Pressure.PressureHundredths, Is.EqualTo(130));
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
            Tracker.Step(VoidProgressionRules.SurvivalSeconds);
            Invoke(_runtime, "StepRunPressure");
            Invoke(_runtime, "FreezePressureAndScore");
            Assert.That(_runtime.TerminalRunScore.FinalScore, Is.EqualTo(135));
            Assert.That(_runtime.TerminalRunScore.PressureHundredths, Is.EqualTo(100));
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
