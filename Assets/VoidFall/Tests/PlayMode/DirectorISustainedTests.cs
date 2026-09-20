using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Runtime;
using VoidFall.Core;

namespace VoidFall.Tests.PlayMode
{
    public sealed class DirectorISustainedTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private VoidFallGameRuntime _runtime;
        private SimulationProfileScope _profile;
        private bool _enabled;
        [UnitySetUp] public IEnumerator SetUp()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _enabled = _runtime.enabled; _runtime.enabled = false;
            _profile = new SimulationProfileScope(_runtime);
            Invoke("StartRunInternal", true, false);
            yield return null;
        }
        [TearDown] public void TearDown() { _profile?.Dispose(); _runtime.enabled = _enabled; }

        [Test] public void Director_I_can_admit_750_bodies_and_rejects_the_751st()
        {
            Set(_runtime, "_time", 20f);
            for (var i = 0; i < 750; i++) Assert.That(Invoke("SpawnEnemy", "chaser"), Is.True, "Spawn " + i);
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(750));
            Assert.That(Invoke("SpawnEnemy", "chaser"), Is.False);
            Invoke("ApplyEnemyDamage", 749, 100000f);
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(749));
            Assert.That(Invoke("SpawnEnemy", "chaser"), Is.True);
            var pool = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            Assert.That(Get(pool.GetValue(749), "SpawnId"), Is.EqualTo(751));
        }

        [Test] public void Standard_elites_spawn_on_their_own_clock_even_while_ambient_timer_is_waiting()
        {
            Set(_runtime, "_time", 60f);
            ((VoidObjectiveTracker)Get(_runtime, "_objectives")).Step(60);
            Set(_runtime, "_spawnTimer", 99f);
            Invoke("UpdateSpawns", .016f);
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            Assert.That(enemies.Cast<object>().Count(e => (bool)Get(e, "Active") && (string)Get(e, "Id") == "elite"), Is.EqualTo(1));
            Assert.That((float)Get(_runtime, "_nextEliteTime"), Is.GreaterThan(140));
            Invoke("UpdateSpawns", .016f);
            Assert.That(enemies.Cast<object>().Count(e => (bool)Get(e, "Active") && (string)Get(e, "Id") == "elite"), Is.EqualTo(1));
        }

        [Test] public void Dense_fodder_does_not_use_the_legacy_budget_to_starve_variants()
        {
            Set(_runtime, "_time", 200f); Set(_runtime, "_nextEliteTime", 999f); Set(_runtime, "_nextEliteVariantTime", 0f);
            ((VoidObjectiveTracker)Get(_runtime, "_objectives")).Step(200);
            var introduced = (bool[])Get(_runtime, "_restorationIntroduced");
            for (var i = 0; i < introduced.Length; i++) introduced[i] = true;
            for (var i = 0; i < 200; i++) Invoke("SpawnEnemy", "chaser");
            Invoke("UpdateSustainedElites", false, 200f);
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            Assert.That(enemies.Cast<object>().Count(e => (bool)Get(e, "Active") && (bool)Get(e, "Elite")), Is.EqualTo(1));
            Assert.That((float)Get(_runtime, "_nextEliteVariantTime"), Is.GreaterThan(200));
        }

        [Test] public void Horde_clear_gets_a_short_breather_then_bounded_offscreen_reinforcements()
        {
            Set(_runtime, "_time", 30f);
            ((VoidObjectiveTracker)Get(_runtime, "_objectives")).Step(30);
            for (var i = 0; i < 60; i++) Invoke("SpawnEnemy", "chaser");
            Invoke("UpdateSustainedRefill", .01f, false);
            for (var i = 0; i < 60; i++) Invoke("ApplyEnemyDamage", i, 100000f);
            for (var i = 0; i < 100; i++) Invoke("UpdateSustainedRefill", .01f, false);
            Assert.That(_runtime.ActiveEnemiesCount, Is.Zero, "A wipe buys a real breather.");
            for (var i = 0; i < 250; i++) Invoke("UpdateSustainedRefill", .01f, false);
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(120));
            Assert.That(Get(_runtime, "_refillStage"), Is.EqualTo(0));
            var half = (Vector2)Invoke("GameplayViewportHalfExtent");
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            foreach (var enemy in enemies.Cast<object>().Where(e => (bool)Get(e, "Active")))
            {
                var p = (Vector2)Get(enemy, "Position");
                Assert.That(Mathf.Abs(p.x) > half.x || Mathf.Abs(p.y) > half.y, Is.True, "Never appear on-screen.");
            }
            Set(_runtime, "_refillStage", 2); Set(_runtime, "_refillRemaining", 3f);
            Invoke("UpdateSustainedRefill", .1f, true);
            Assert.That(Get(_runtime, "_refillStage"), Is.EqualTo(0), "Boss windows cancel refill.");
        }

        [Test] public void Ordinary_arrivals_continue_during_a_tactical_encounter()
        {
            Set(_runtime, "_time", 60f);
            _runtime.ForceEncounterForDiagnostics("volley");
            Invoke("UpdateSpawns", 3f);
            var before = _runtime.ActiveEnemiesCount;
            for (var i = 0; i < 20; i++) Invoke("UpdateSpawns", .1f);
            Assert.That(_runtime.ActiveEnemiesCount, Is.GreaterThan(before));
        }

        [Test] public void Ordinary_pursuers_do_not_expire_after_48_seconds()
        {
            Invoke("SpawnEnemy", "chaser");
            Set(_runtime, "_time", 90f);
            Invoke("UpdateEnemies", .1f);
            Invoke("UpdateEnemies", 3f);
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(1));
        }

        [Test] public void Major_incident_does_not_suspend_all_ordinary_arrivals()
        {
            Set(_runtime, "_time", 90f);
            Assert.That(_runtime.ForceMajorIncidentForDiagnostics("eclipse"), Is.True);
            for (var i = 0; i < 30; i++) Invoke("UpdateSpawns", .1f);
            Assert.That(_runtime.ActiveEnemiesCount, Is.GreaterThan(0));
        }

        [Test] public void Demanding_attacks_are_staggered_but_waiting_dashers_keep_moving()
        {
            Set(_runtime, "_time", 10f);
            for (var i = 0; i < 4; i++) Invoke("SpawnEnemy", "dasher");
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            for (var i = 0; i < 4; i++)
            {
                var enemy = enemies.GetValue(i);
                Set(enemy, "Position", new Vector2(150, i * 10)); Set(enemy, "Age", 1f);
                enemies.SetValue(enemy, i);
            }
            Invoke("UpdateEnemies", .016f);
            var committed = 0; var movingWaiters = 0;
            for (var i = 0; i < 4; i++)
            {
                var enemy = enemies.GetValue(i);
                if ((int)Get(enemy, "State") == 1) committed++;
                else if (((Vector2)Get(enemy, "Velocity")).sqrMagnitude > 1) movingWaiters++;
            }
            Assert.That(committed, Is.InRange(1, 2));
            Assert.That(movingWaiters, Is.GreaterThan(0));
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);

        [Test] public void Damage_recovery_expires_and_new_beats_can_resume()
        {
            Set(_runtime, "_time", 50f);
            Invoke("DamagePlayer", 1f, Vector2.right);
            Assert.That((float)Get(_runtime, "_pressureReliefTimer"), Is.GreaterThan(0));
            for (var i = 0; i < 240; i++) Invoke("Simulate", 1d / 60);
            Assert.That((float)Get(_runtime, "_pressureReliefTimer"), Is.Zero);
            Set(_runtime, "_nextEncounterTime", 0f);
            Invoke("UpdateSpawns", .016f);
            Assert.That(_runtime.CurrentEncounterPhase, Is.Not.EqualTo("Flow"));
        }

        [Test] public void Arena_encounter_reset_keeps_run_owned_scripted_probe_input()
        {
            Set(_runtime, "_directorPlaytestActive", true);
            Invoke("ResetEncounterDirector");
            Assert.That(Get(_runtime, "_directorPlaytestActive"), Is.True);
        }

        [Test] public void Native_queued_burst_keeps_a_reservation_after_its_windup()
        {
            Invoke("SpawnEnemy", "null-patrol"); Invoke("SpawnEnemy", "dasher"); Invoke("SpawnEnemy", "dasher");
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            Assert.That(Invoke("CanCommitDirectorAttack", enemies.GetValue(0)), Is.True);
            var second = enemies.GetValue(1); Set(second, "State", 1); enemies.SetValue(second, 1);
            Assert.That(Invoke("CanCommitDirectorAttack", second), Is.True);
            var native = (Array)Get(_runtime, "_nullCityUnits"); var state = native.GetValue(0);
            Set(state, "Identity", Get(enemies.GetValue(0), "SpawnId")); Set(state, "Shots", 3); native.SetValue(state, 0);
            Invoke("PrepareDirectorAttackBudget");
            Assert.That(Invoke("CanCommitDirectorAttack", enemies.GetValue(2)), Is.False);
            Set(state, "Shots", 0); native.SetValue(state, 0); Invoke("PrepareDirectorAttackBudget");
            Assert.That(Invoke("CanCommitDirectorAttack", enemies.GetValue(2)), Is.True);
        }

        [Test] public void A_waiting_gunner_does_not_permanently_block_major_incident_selection()
        {
            Set(_runtime, "_time", 100f); Set(_runtime, "_nextIncidentOpportunity", 0f);
            ((VoidObjectiveTracker)Get(_runtime, "_objectives")).Step(70);
            for (var i = 0; i < 9; i++) Invoke("SpawnEnemy", "chaser");
            Invoke("SpawnEnemy", "gunner");
            Invoke("StepMajorIncidents", .016f);
            Assert.That(_runtime.CurrentMajorIncident, Is.Not.EqualTo("None"));
        }

        [Test] public void Natural_beat_members_rejoin_combat_when_the_player_outpaces_them()
        {
            Set(_runtime, "_time", 60f); _runtime.ForceEncounterForDiagnostics("volley");
            Invoke("UpdateSpawns", 1f);
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            var enemy = enemies.GetValue(0); Set(enemy, "Position", new Vector2(5000, 0)); enemies.SetValue(enemy, 0);
            Invoke("UpdateEnemies", .016f);
            Assert.That(((Vector2)Get(enemies.GetValue(0), "Position")).magnitude, Is.LessThan(1750));
        }

        [Test] public void Projectile_reservations_survive_owner_death_and_slot_reuse()
        {
            Set(_runtime, "_time", 10f);
            for (var i = 0; i < 2; i++) Invoke("SpawnEnemy", "gunner");
            var game = Get(_runtime, "_gameSim"); var enemies = (Array)Get(game, "Enemies");
            for (var i = 0; i < 2; i++)
            {
                var enemy = enemies.GetValue(i); Set(enemy, "Position", new Vector2(250, i * 30));
                Set(enemy, "AttackCooldown", 0f); enemies.SetValue(enemy, i);
            }
            Invoke("UpdateEnemies", .016f); Invoke("UpdateEnemies", 2f);
            var shots = (Array)Get(game, "HostileShots");
            Assert.That(shots.Cast<object>().Count(e => (bool)Get(e, "Active")), Is.GreaterThan(0));
            var old = enemies.GetValue(0); Set(old, "Active", false); enemies.SetValue(old, 0); Invoke("RemoveEnemyOrder", 0);
            Invoke("SpawnEnemy", "dasher");
            var replacement = enemies.GetValue(0); Set(replacement, "Position", new Vector2(150, 0)); Set(replacement, "Age", 1f);
            enemies.SetValue(replacement, 0);
            Invoke("UpdateEnemies", .016f);
            Assert.That(Get(enemies.GetValue(0), "State"), Is.EqualTo(0), "Old shots still occupy both slots.");
            for (var i = 0; i < shots.Length; i++) { var shot = shots.GetValue(i); Set(shot, "Active", false); shots.SetValue(shot, i); }
            Invoke("UpdateEnemies", .016f);
            Assert.That(Get(enemies.GetValue(0), "State"), Is.EqualTo(1), "Expired threats release their reservations.");
        }

        [Test] public void Frozen_committed_attacks_do_not_release_their_slots_with_elapsed_time()
        {
            Set(_runtime, "_time", 10f);
            var game = Get(_runtime, "_gameSim"); var enemies = (Array)Get(game, "Enemies");
            for (var i = 0; i < 2; i++)
            {
                Invoke("SpawnEnemy", "dasher"); var enemy = enemies.GetValue(i);
                Set(enemy, "Position", new Vector2(150, i * 20)); Set(enemy, "Age", 1f); enemies.SetValue(enemy, i);
            }
            Invoke("UpdateEnemies", .016f);
            var freeze = (float[])Get(_runtime, "_arsenalFreeze"); var freezeIds = (int[])Get(_runtime, "_arsenalFreezeIds");
            for (var i = 0; i < 2; i++) { freeze[i] = 100; freezeIds[i] = (int)Get(enemies.GetValue(i), "SpawnId"); }
            Invoke("SpawnEnemy", "dasher"); var waiting = enemies.GetValue(2);
            Set(waiting, "Position", new Vector2(150, 50)); Set(waiting, "Age", 1f); enemies.SetValue(waiting, 2);
            Set(_runtime, "_time", 80f); Invoke("UpdateEnemies", .016f);
            Assert.That(Get(enemies.GetValue(2), "State"), Is.EqualTo(0));
        }

        [Test] public void Sustained_clock_recovers_without_withdrawing_survivors()
        {
            var clock = new CombatEncounterClock(); clock.BeginSustained(CombatEncounterKind.Hunt);
            clock.Step(.75, 0, false, true); clock.CommitDeployment(10);
            clock.Step(14, 10, true, false);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Recovery));
            Assert.That(clock.NeedsWithdrawal, Is.False);
            clock.Step(5, 10, true, false);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Flow));
        }

        [Test] public void Beat_selection_excludes_formations_and_two_recent_situations()
        {
            CombatEncounterKind? previous = null, beforePrevious = null;
            for (var i = 0; i < 20; i++)
            {
                var chosen = (CombatEncounterKind)Invoke("ChooseSustainedBeat", 120f);
                Assert.That(chosen, Is.Not.EqualTo(CombatEncounterKind.Crossing));
                Assert.That(chosen, Is.Not.EqualTo(CombatEncounterKind.Volley));
                Assert.That(chosen, Is.Not.EqualTo(previous)); Assert.That(chosen, Is.Not.EqualTo(beforePrevious));
                Invoke("BeginSustainedBeat", chosen); beforePrevious = previous; previous = chosen;
            }
        }
        private void PrepareMomentum(float local, int stage = 0)
        {
            Set(_runtime, "_time", stage * 420f + local);
            Set(_runtime, "_pressureStageIndex", stage);
            ((VoidObjectiveTracker)Get(_runtime, "_objectives")).Step(local);
            Set(_runtime, "_rosterIntroductionReadyAt", float.MaxValue);
            Set(_runtime, "_nextLegacySwarmAt", float.MaxValue);
            Set(_runtime, "_nextEliteTime", float.MaxValue);
            Set(_runtime, "_nextEliteVariantTime", float.MaxValue);
            Set(_runtime, "_nextEncounterTime", float.MaxValue);
            Set(_runtime, "_spawnTimer", 0f);
        }

        [TestCase(0f, 80, 92)]
        [TestCase(45f, 120, 136)]
        public void Later_void_actual_arrivals_keep_momentum_without_filling_the_pool(float local, int minimum, int maximum)
        {
            PrepareMomentum(local, 1);
            for (var i = 0; i < 240; i++) Invoke("UpdateSpawns", 1f / 60);
            Assert.That(_runtime.ActiveEnemiesCount, Is.InRange(minimum, maximum));
        }

        [Test]
        public void Later_void_arrival_grace_and_damage_relief_override_the_rate_floor()
        {
            PrepareMomentum(0, 1);
            Set(_runtime, "_arrivalGrace", 2.5f);
            for (var i = 0; i < 120; i++) Invoke("UpdateSpawns", 1f / 60);
            Assert.That(_runtime.ActiveEnemiesCount, Is.Zero);
            Set(_runtime, "_arrivalGrace", 0f); Set(_runtime, "_pressureReliefTimer", 3f);
            for (var i = 0; i < 120; i++) Invoke("UpdateSpawns", 1f / 60);
            Assert.That(_runtime.ActiveEnemiesCount, Is.InRange(10, 14));
        }

        [TestCase(270f, "elite", 1, 31)]
        [TestCase(315f, "runner", 10, 28)]
        public void Late_Abyss_signatures_warn_then_admit_their_composition_once(float local, string enemyId, int count, int total)
        {
            PrepareMomentum(local);
            var introduced = (bool[])Get(_runtime, "_restorationIntroduced");
            for (var i = 0; i < introduced.Length; i++) introduced[i] = true;
            Invoke("TryScheduleMomentumBeat", local);
            var clock = (CombatEncounterClock)Get(_runtime, "_encounter");
            clock.Step(1.99, 0, false, false);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Warning));
            Assert.That(_runtime.ActiveEnemiesCount, Is.Zero);
            clock.Step(.01, 0, false, false); Invoke("DeploySustainedBeat");
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(total));
            Assert.That(enemies.Cast<object>().Count(e => (bool)Get(e, "Active") && (string)Get(e, "Id") == enemyId), Is.EqualTo(count));
            Invoke("CancelEncounterDirector"); Invoke("TryScheduleMomentumBeat", local);
            Assert.That(clock.Phase, Is.EqualTo(CombatEncounterPhase.Flow));
        }

        [Test]
        public void Signature_defers_under_damage_and_cancels_if_hit_during_warning()
        {
            PrepareMomentum(270);
            Set(_runtime, "_pressureReliefTimer", 2f); Invoke("TryScheduleMomentumBeat", 270f);
            Assert.That(_runtime.CurrentEncounterPhase, Is.EqualTo("Flow"));
            Set(_runtime, "_pressureReliefTimer", 0f); Invoke("TryScheduleMomentumBeat", 271f);
            ((CombatEncounterClock)Get(_runtime, "_encounter")).Step(2, 0, false, false);
            Set(_runtime, "_pressureReliefTimer", 2f); Invoke("DeploySustainedBeat");
            Assert.That(_runtime.ActiveEnemiesCount, Is.Zero);
            Assert.That(_runtime.CurrentEncounterPhase, Is.EqualTo("Recovery"));
        }

        [Test]
        public void Both_new_families_receive_small_early_introductions_and_learning_grace()
        {
            for (var seconds = 15; seconds <= 59; seconds++)
            {
                Set(_runtime, "_time", (float)seconds);
                ((VoidObjectiveTracker)Get(_runtime, "_objectives")).Step(1);
                if (seconds == 15) ((VoidObjectiveTracker)Get(_runtime, "_objectives")).Step(15);
                Invoke("TryIntroduceRestorationEnemy");
            }
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            foreach (var id in new[] { "spiky", "shuriken" })
                Assert.That(enemies.Cast<object>().Count(e => (bool)Get(e, "Active") && (string)Get(e, "Id") == id), Is.EqualTo(3), id);
            Assert.That(Invoke("RestorationTypeIntroduced", "shuriken"), Is.True);
            Assert.That(Invoke("RestorationTypeIntroduced", "spiky"), Is.False, "54-second intro still has its twelve-second grace");
        }

        [Test]
        public void Safety_delayed_Spiky_is_introduced_before_later_gunners_and_dashers()
        {
            PrepareMomentum(80);
            Set(_runtime, "_rosterIntroductionReadyAt", 0f);
            var introduced = (bool[])Get(_runtime, "_restorationIntroduced");
            introduced[1] = introduced[2] = introduced[5] = true; // Runner, green swarmer, Shuriken already taught.
            Assert.That(Invoke("TryIntroduceRestorationEnemy"), Is.True);
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            Assert.That(enemies.Cast<object>().Count(e => (bool)Get(e, "Active") && (string)Get(e, "Id") == "spiky"), Is.EqualTo(3));
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(3));
        }

        [Test]
        public void Meteor_blocked_incident_gets_warned_fodder_instead_and_resets_next_arena()
        {
            PrepareMomentum(100, 1); Set(_runtime, "_arenaId", ArenaId.RedNebula);
            var sim = Get(_runtime, "_gameSim"); var meteors = (Array)Get(sim, "Meteors");
            for (var i = 0; i < 3; i++) { var meteor = meteors.GetValue(i); Set(meteor, "Active", true); meteors.SetValue(meteor, i); }
            for (var i = 0; i < 8; i++) Invoke("SpawnEnemy", "chaser");
            Set(_runtime, "_nextIncidentOpportunity", 0f);
            Invoke("StepMajorIncidents", .016f);
            Assert.That(_runtime.CurrentMajorIncident, Is.EqualTo("None"));
            Assert.That(_runtime.CurrentEncounterPhase, Is.EqualTo("Warning"));
            ((CombatEncounterClock)Get(_runtime, "_encounter")).Step(2, 0, false, false);
            Invoke("DeploySustainedBeat");
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(32));
            Assert.That(Get(_runtime, "_arenaIncidentOpportunities"), Is.EqualTo(1));
            Invoke("ResetEncounterDirector");
            Assert.That(Get(_runtime, "_arenaIncidentOpportunities"), Is.EqualTo(0));
            Assert.That((float)Get(_runtime, "_nextIncidentOpportunity"), Is.EqualTo(580f));
        }

        [Test]
        public void Incident_safety_deferrals_expire_without_accumulating_a_backlog()
        {
            PrepareMomentum(100); Set(_runtime, "_nextIncidentOpportunity", 0f);
            Set(_runtime, "_pressureReliefTimer", 3f);
            Invoke("StepMajorIncidents", .016f);
            Set(_runtime, "_time", 149f); Invoke("StepMajorIncidents", .016f);
            Assert.That(Get(_runtime, "_arenaIncidentOpportunities"), Is.EqualTo(1));
            Assert.That(_runtime.CurrentMajorIncident, Is.EqualTo("None"));
            Assert.That(_runtime.CurrentEncounterPhase, Is.EqualTo("Flow"));
            Assert.That((float)Get(_runtime, "_nextIncidentOpportunity"), Is.EqualTo(249f));
        }

        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private object Invoke(string name, params object[] args) => RuntimeTestReflection.Invoke(_runtime, name, args);
    }
}
