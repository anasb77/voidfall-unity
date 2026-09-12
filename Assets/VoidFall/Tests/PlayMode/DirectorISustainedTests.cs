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
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private object Invoke(string name, params object[] args) => RuntimeTestReflection.Invoke(_runtime, name, args);
    }
}
