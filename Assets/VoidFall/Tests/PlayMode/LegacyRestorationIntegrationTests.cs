using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class LegacyRestorationIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private SimulationProfileScope _profile;
        private bool _enabled;
        private object Game => Get(_runtime, "_gameSim");
        private Array Enemies => (Array)Get(Game, "Enemies");
        [UnitySetUp] public IEnumerator SetUp()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            _enabled = _runtime.enabled; _runtime.enabled = false;
            _profile = new SimulationProfileScope(_runtime);
            Call("StartRunInternal", true, false);
            yield return null;
        }
        [TearDown] public void TearDown() { _profile?.Dispose(); _runtime.enabled = _enabled; }

        [Test] public void Second_wind_heals_level_hp_once_then_waits_180_seconds()
        {
            var progress = (UpgradeProgress)Get(_runtime, "_upgradeProgress");
            progress.SupportRanks[Array.FindIndex(ExtendedCatalog.AllSupports(), s => s.Id == "secondWind")] = 1;
            Set(_runtime, "_level", 12);
            Player("Health", 10f); Player("MaxHealth", 100f);
            Call("TrySecondWind");
            Assert.That(Get(Get(Game, "Player"), "Health"), Is.EqualTo(22f));
            Assert.That(Get(_runtime, "_secondWindRemaining"), Is.EqualTo(180f));
            Player("Health", 5f); Call("TrySecondWind");
            Assert.That(Get(Get(Game, "Player"), "Health"), Is.EqualTo(5f));
            Set(_runtime, "_secondWindRemaining", 0f); Player("Health", 0f); Call("TrySecondWind");
            Assert.That(Get(Get(Game, "Player"), "Health"), Is.EqualTo(0f));
        }

        [Test] public void Spiky_chain_is_delayed_and_cannot_hurt_the_player()
        {
            Call("SpawnEnemy", "spiky"); Call("SpawnEnemy", "spiky");
            for (var i = 0; i < 2; i++) Enemy(i, "Position", new Vector2(20 + i * 30, 0));
            Call("RebuildEnemyGrid");
            var hp = Get(Get(Game, "Player"), "Health");
            Call("ApplyEnemyDamage", 0, 100000f);
            Assert.That(Get(Enemies.GetValue(1), "Active"), Is.True);
            Call("StepLegacyRestoration", .1f);
            Assert.That(Get(Enemies.GetValue(1), "Active"), Is.False);
            Assert.That(Get(_runtime, "_spikyBurstCount"), Is.EqualTo(1), "Child blast waits for another tick.");
            Assert.That(Get(Get(Game, "Player"), "Health"), Is.EqualTo(hp));
        }

        [Test] public void Spiky_body_and_collision_follow_the_same_half_second_phases()
        {
            Call("SpawnEnemy", "spiky");
            Enemy(0, "SpawnId", 17); Enemy(0, "Age", .15f);
            Call("UpdateEnemies", .1f);
            Assert.That((float)Get(Enemies.GetValue(0), "Radius"), Is.EqualTo(19.5f).Within(.01f));
            Enemy(0, "Age", .65f); Call("UpdateEnemies", .1f);
            Assert.That((float)Get(Enemies.GetValue(0), "Radius"), Is.EqualTo(58.5f).Within(.01f));
            Enemy(0, "Age", 1.15f); Call("UpdateEnemies", .1f);
            Assert.That((float)Get(Enemies.GetValue(0), "Radius"), Is.EqualTo(19.5f).Within(.01f));
        }

        [Test] public void Spiky_expansion_shoves_neighbors_outward_but_shrink_and_anchors_do_not_move_them()
        {
            Call("SpawnEnemy", "spiky"); Call("SpawnEnemy", "exploder"); Call("SpawnEnemy", "chaser"); Call("SpawnEnemy", "chaser");
            Enemy(0, "SpawnId", 17); Enemy(0, "Position", new Vector2(300, 0)); Enemy(0, "Age", .51f); Enemy(0, "Speed", 0f);
            Enemy(1, "Position", new Vector2(340, 0)); Enemy(1, "Speed", 0f);
            Enemy(2, "Position", new Vector2(260, 0)); Enemy(2, "Speed", 0f);
            Enemy(3, "Position", new Vector2(300, 30)); Enemy(3, "Speed", 0f);
            Call("UpdateEnemies", .06f);
            Enemy(3, "MatriarchBodyguard", true);
            var anchor = (Vector2)Get(Enemies.GetValue(3), "Position");
            var hp = Get(Get(Game, "Player"), "Health");
            Call("ApplySpikyGrowthPushes");
            Assert.That(((Vector2)Get(Enemies.GetValue(1), "Position")).x, Is.GreaterThan(340));
            Assert.That(((Vector2)Get(Enemies.GetValue(2), "Position")).x, Is.LessThan(260));
            Assert.That(((Vector2)Get(Enemies.GetValue(1), "Knockback")).magnitude, Is.InRange(1, 220));
            Assert.That(Get(Enemies.GetValue(3), "Position"), Is.EqualTo(anchor));
            Assert.That(Get(Get(Game, "Player"), "Health"), Is.EqualTo(hp));
            var after = Get(Enemies.GetValue(1), "Position");
            Call("ApplySpikyGrowthPushes");
            Assert.That(Get(Enemies.GetValue(1), "Position"), Is.EqualTo(after), "A growth delta is consumed once.");
            Enemy(0, "Age", 1.15f); Call("UpdateEnemies", .01f);
            after = Get(Enemies.GetValue(1), "Position"); Call("ApplySpikyGrowthPushes");
            Assert.That(Get(Enemies.GetValue(1), "Position"), Is.EqualTo(after), "Shrinking cannot pull neighbors inward.");
        }

        [Test] public void Signature_ring_is_uniform_and_does_not_wait_for_a_tactical_event()
        {
            Set(_runtime, "_time", 30f);
            ((bool[])Get(_runtime, "_restorationIntroduced"))[1] = true;
            _runtime.ForceEncounterForDiagnostics("volley");
            var beforePhase = _runtime.CurrentEncounterPhase;
            Call("TryDeployLegacySwarm");
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(11));
            foreach (var e in Enemies) if ((bool)Get(e, "Active")) Assert.That(Get(e, "Id"), Is.EqualTo("runner"));
            Assert.That(Get(_runtime, "_nextLegacySwarmAt"), Is.EqualTo(64f));
            Assert.That(_runtime.CurrentEncounterPhase, Is.EqualTo(beforePhase));
            Call("TryDeployLegacySwarm"); Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(11));
            Set(_runtime, "_time", 64f); Call("TryDeployLegacySwarm");
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(24));
        }

        [Test] public void Monitor_choice_survives_runtime_settings_snapshot_and_restore()
        {
            var ui = (VoidFall.UI.UIManager)Get(_runtime, "_ui");
            ui.Callbacks.SetMonitor(1);
            var bridge = (VoidFall.UI.IGameBridge)Get(_runtime, "_gameBridge");
            var snapshot = bridge.CloneLiveSettings();
            Assert.That(snapshot.monitorIndex, Is.EqualTo(1));
            ui.Callbacks.SetMonitor(-1);
            bridge.RestoreSettings(snapshot);
            Assert.That(bridge.CloneLiveSettings().monitorIndex, Is.EqualTo(1));
            ui.SwitchToGameplay();
            var mute = (UnityEngine.UI.Button)Get(ui, "_muteButton");
            var press = mute.GetComponent<VoidFall.UI.UIPressFeedback>();
            press.OnPointerDown(null); press.OnPointerUp(null);
            Assert.That(((RectTransform)mute.transform).sizeDelta.x, Is.EqualTo(28.6f).Within(.001));
            Assert.That(mute.transform.localScale.x, Is.EqualTo(1));
            Assert.That(((RectTransform)mute.transform).anchorMax.y, Is.EqualTo(1));
        }

        [Test] public void Signature_dasher_telegraphs_then_commits_and_recovers()
        {
            Call("SpawnEnemy", "dasher");
            ((int[])Get(_runtime, "_legacyRushIdentities"))[0] = (int)Get(Enemies.GetValue(0), "SpawnId");
            Enemy(0, "Position", new Vector2(200, 0)); Enemy(0, "Age", 1f);
            Call("UpdateEnemies", .01f);
            Assert.That(Get(Enemies.GetValue(0), "State"), Is.EqualTo(1));
            Assert.That(Get(Enemies.GetValue(0), "StateTimer"), Is.EqualTo(.45f));
            Call("UpdateEnemies", .46f);
            Assert.That(Get(Enemies.GetValue(0), "State"), Is.EqualTo(2));
            var locked = (Vector2)Get(Enemies.GetValue(0), "DashDirection");
            Player("Position", new Vector2(0, 300)); Call("UpdateEnemies", .1f);
            Assert.That((Vector2)Get(Enemies.GetValue(0), "Velocity"), Is.EqualTo(locked * 570));
            Call("UpdateEnemies", .3f);
            Assert.That(Get(Enemies.GetValue(0), "State"), Is.EqualTo(3));
            Assert.That(Get(Enemies.GetValue(0), "StateTimer"), Is.EqualTo(.7f));
        }

        [Test] public void Shuriken_weaves_both_ways_and_spins()
        {
            Call("SpawnEnemy", "shuriken"); Enemy(0, "Seed", 0f);
            Enemy(0, "Position", new Vector2(200, 0)); Enemy(0, "Age", .65f); Call("UpdateEnemies", .1f);
            var first = (Vector2)Get(Enemies.GetValue(0), "Velocity");
            var rotation = (float)Get(Enemies.GetValue(0), "Rotation");
            Enemy(0, "Position", new Vector2(200, 0)); Enemy(0, "Age", 2.14f); Call("UpdateEnemies", .1f);
            var second = (Vector2)Get(Enemies.GetValue(0), "Velocity");
            Assert.That(first.x, Is.LessThan(0)); Assert.That(second.x, Is.LessThan(0));
            Assert.That(first.y * second.y, Is.LessThan(0));
            Assert.That((float)Get(Enemies.GetValue(0), "Rotation"), Is.EqualTo(2.24f * 14f).Within(.001f));
        }

        [Test] public void Projectile_cards_apply_to_actual_pistol_rounds()
        {
            var progress = (UpgradeProgress)Get(_runtime, "_upgradeProgress");
            progress.SupportRanks[Array.FindIndex(ExtendedCatalog.AllSupports(), s => s.Id == "phaseRounds")] = 2;
            progress.SupportRanks[Array.FindIndex(ExtendedCatalog.AllSupports(), s => s.Id == "split-pistol")] = 1;
            Call("SpawnEnemy", "chaser"); Enemy(0, "Position", new Vector2(200, 0)); Enemy(0, "Age", 2f);
            Call("RecalculatePlayerStats", false); Call("RebuildEnemyGrid");
            ((float[])Get(_runtime, "_weaponCooldowns"))[0] = 0;
            Call("UpdateWeapons", .016f);
            var bullets = (Array)Get(Game, "Bullets");
            var count = 0;
            foreach (var bullet in bullets)
            {
                if (!(bool)Get(bullet, "Active")) continue;
                count++;
                Assert.That(Get(bullet, "PierceRemaining"), Is.EqualTo(2));
            }
            Assert.That(count, Is.EqualTo(2));
        }

        [Test] public void Giant_slayer_boosts_elites_without_boosting_regular_fodder()
        {
            var progress = (UpgradeProgress)Get(_runtime, "_upgradeProgress");
            progress.SupportRanks[Array.FindIndex(ExtendedCatalog.AllSupports(), s => s.Id == "giantSlayer")] = 3;
            Call("SpawnEnemy", "chaser"); Call("SpawnEnemy", "elite");
            Enemy(0, "Health", 1000f); Enemy(1, "Health", 1000f);
            Call("ApplyEnemyDamage", 0, 10f); Call("ApplyEnemyDamage", 1, 10f);
            Assert.That((float)Get(Enemies.GetValue(0), "Health"), Is.EqualTo(990f).Within(.01f));
            Assert.That((float)Get(Enemies.GetValue(1), "Health"), Is.EqualTo(985.5f).Within(.01f));
        }

        [Test] public void Crossing_keeps_camera_still_until_covered_then_settles_before_control()
        {
            Player("Position", new Vector2(1700, 900));
            Set(_runtime, "_cameraFollowPosition", new Vector2(1700, 900));
            Call("OnVoidObjectiveCompleted"); Call("StepVoidCompletionDelay", 10f);
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Travel"));
            Assert.That(Get(_runtime, "_cameraFollowPosition"), Is.EqualTo(new Vector2(1700, 900)));
            Call("StepRiftTransition", .4f);
            Assert.That(Get(_runtime, "_cameraFollowPosition"), Is.EqualTo(new Vector2(1700, 900)));
            Call("StepRiftTransition", .4f);
            Assert.That(Get(_runtime, "_cameraFollowPosition"), Is.EqualTo(Vector2.zero));
            for (var i = 0; i < 12; i++) Call("UpdateJourneyFlow", .1f);
            Assert.That(_runtime.JourneyStatus, Is.EqualTo("Junction"));
        }
        private void Enemy(int i, string field, object value) { var e = Enemies.GetValue(i); Set(e, field, value); Enemies.SetValue(e, i); }
        private void Player(string field, object value) { var p = Get(Game, "Player"); Set(p, field, value); Set(Game, "Player", p); }
        private static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
        private object Call(string method, params object[] args) => RuntimeTestReflection.Invoke(_runtime, method, args);
    }
}
