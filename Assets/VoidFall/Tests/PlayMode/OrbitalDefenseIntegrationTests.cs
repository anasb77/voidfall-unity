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

namespace VoidFall.Tests.PlayMode
{
    public sealed class OrbitalDefenseIntegrationTests
    {
        private VoidFallGameRuntime _runtime;
        private object _sim, _store, _profile;
        private bool _enabled;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Put(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, Flags); Assert.That(field, Is.Not.Null, "Missing " + name); field.SetValue(target, value);
        }
        private object Call(string name, params object[] args)
        {
            return RuntimeTestReflection.Invoke(_runtime, name, args);
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>(); Assert.That(_runtime, Is.Not.Null);
            _enabled = _runtime.enabled; _runtime.enabled = false;
            _store = Get(_runtime, "_saveStore"); _profile = Get(_runtime, "_saveData");
            Put(_runtime, "_saveStore", new SaveStore(Path.Combine(Path.GetTempPath(), "voidfall-orbital-" + Guid.NewGuid().ToString("N"), "profile.json")));
            Put(_runtime, "_saveData", SaveStore.CreateDefault()); Put(_runtime, "_runSaved", true);
            Call("StartRunInternal", false, false);
            _sim = Get(_runtime, "_gameSim");
            var enemies = (Array)Get(_sim, "Enemies"); Array.Clear(enemies, 0, enemies.Length);
            _sim.GetType().GetMethod("ResetEnemyOrder").Invoke(_sim, null); Call("RebuildEnemyGrid");
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null) { Put(_runtime, "_runSaved", true); Call("EnterMainMenu"); Put(_runtime, "_saveStore", _store); Put(_runtime, "_saveData", _profile); _runtime.enabled = _enabled; }
            yield return null;
        }
        private UpgradeProgress Equip(int weapon)
        {
            var progress = new UpgradeProgress(); progress.WeaponRanks[weapon] = 1; Put(_runtime, "_upgradeProgress", progress); Call("RecalculatePlayerStats", false); return progress;
        }
        private void Support(UpgradeProgress progress, string id, int rank)
            => progress.SupportRanks[Array.FindIndex(ExtendedCatalog.AllSupports(), s => s.Id == id)] = rank;
        private void Spawn(bool blockable, Vector2 from, Vector2 direction, float speed, float curvature = 0)
        {
            Put(_runtime, "_ordinaryEnemyShotContext", blockable);
            Call("SpawnHostileShot", from, direction, 10f, speed, curvature, false, -1, 0f);
            Put(_runtime, "_ordinaryEnemyShotContext", false);
        }
        private bool ShotActive(int slot = 0) => (bool)Get(((Array)Get(_sim, "HostileShots")).GetValue(slot), "Active");

        [Test]
        public void Blades_and_clock_stack_projectile_speed_recovery_and_overclock_once()
        {
            var progress = Equip(3); progress.WeaponRanks[8] = 1;
            progress.Evolved[3] = true;
            Support(progress, "cycling", 1); Support(progress, "projectileSpeed", 1); Call("RecalculatePlayerStats", false);
            var overclock = new OverclockState(); overclock.ApplyPickup(); Put(_runtime, "_overclock", overclock);
            Put(_runtime, "_bladeAngle", 0f); Put(_runtime, "_arsenalClockAngle", 0f);
            Put(_runtime, "_hollowBladeActive", true); Put(_runtime, "_hollowBladeAge", 0f);
            Call("UpdateBlades", .01f); Call("UpdateArsenalWeapons", .01f);
            var scale = 1.1f * 1.35f / .92f;
            Assert.That((float)Get(_runtime, "_bladeAngle"), Is.EqualTo((float)ContentCatalog.Weapons[3].Ranks[0].Stats.OrbitSpeed * .01f * scale).Within(.0001));
            Assert.That((float)Get(_runtime, "_hollowBladeAge"), Is.EqualTo(.01f * scale).Within(.0001));
            var clockStep = Mathf.Abs(Mathf.DeltaAngle(0, (float)Get(_runtime, "_arsenalClockAngle") * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            Assert.That(clockStep, Is.EqualTo((float)ContentCatalog.Weapons[8].Ranks[0].Stats.OrbitSpeed * .01f * scale).Within(.0001));
        }
        [Test]
        public void Velocity_card_applies_zoom_on_normal_stat_recalculation()
        {
            var progress = Equip(8); Call("UpdateGameplayCameraViewport");
            var camera = (Camera)Get(_runtime, "_camera"); var before = camera.orthographicSize;
            Support(progress, "projectileSpeed", 2); Call("RecalculatePlayerStats", false); Call("UpdateGameplayCameraViewport");
            Assert.That((float)Get(_runtime, "_spatialZoomScale"), Is.EqualTo(1.1f).Within(.0001));
            Assert.That(camera.orthographicSize, Is.EqualTo(before * 1.1f).Within(.001));
        }
        [TestCase(3)]
        [TestCase(8)]
        public void Ordinary_shots_are_intercepted_before_player_impact(int weapon)
        {
            Equip(weapon); Put(_runtime, "_bladeAngle", 0f); Put(_runtime, "_arsenalClockAngle", 0f);
            Call("UpdateBlades", 0f); Call("UpdateArsenalWeapons", 0f);
            var health = (float)Get(Get(_sim, "Player"), "Health");
            Spawn(true, new Vector2(120, 0), Vector2.left, 1200);
            Call("UpdateHostileShots", .1f);
            Assert.That(ShotActive(), Is.False);
            Assert.That((float)Get(Get(_sim, "Player"), "Health"), Is.EqualTo(health));
        }
        [TestCase(3)]
        [TestCase(8)]
        public void Boss_elite_and_unknown_sources_cannot_be_intercepted(int weapon)
        {
            Equip(weapon); Put(_runtime, "_bladeAngle", 0f); Put(_runtime, "_arsenalClockAngle", 0f);
            Call("UpdateBlades", 0f); Call("UpdateArsenalWeapons", 0f);
            Spawn(false, new Vector2(76, -35), Vector2.up, 700);
            Call("UpdateHostileShots", .1f);
            Assert.That(ShotActive(), Is.True);
        }

        [Test]
        public void Rank_three_seconds_hand_intercepts_only_inside_its_short_reach()
        {
            var progress = Equip(8); progress.WeaponRanks[8] = 3;
            Put(_runtime, "_orbitalClockStartAngle", Mathf.PI / 2);
            Put(_runtime, "_arsenalClockAngle", Mathf.PI / 2);
            Spawn(true, new Vector2(-45, -20), Vector2.up, 400);
            Assert.That((bool)Call("TryInterceptHostileShot", 0, new Vector2(-45, -20), new Vector2(-45, 20), 5f), Is.True);
            Assert.That((bool)Call("TryInterceptHostileShot", 0, new Vector2(-100, -20), new Vector2(-100, 20), 5f), Is.False);
            progress.WeaponRanks[8] = 2;
            Assert.That((bool)Call("TryInterceptHostileShot", 0, new Vector2(-45, -20), new Vector2(-45, 20), 5f), Is.False);
        }
        [Test]
        public void Blades_do_not_create_a_solid_shield_inside_the_orbit()
        {
            Equip(3); Put(_runtime, "_bladeAngle", 0f); Call("UpdateBlades", 0f);
            Spawn(true, new Vector2(35, -35), Vector2.up, 700); Call("UpdateHostileShots", .1f);
            Assert.That(ShotActive(), Is.True);
        }
        [Test]
        public void Interception_retires_curved_shots_and_clears_reused_slot_eligibility()
        {
            Equip(3); Put(_runtime, "_bladeAngle", 0f); Call("UpdateBlades", 0f);
            Spawn(true, new Vector2(76, -35), Vector2.up, 700, .01f); Call("UpdateHostileShots", .1f);
            Assert.That(ShotActive(), Is.False); Assert.That((int)Get(_sim, "CurvedShotCount"), Is.Zero);
            Spawn(false, new Vector2(76, -35), Vector2.up, 700); Call("UpdateHostileShots", .1f);
            Assert.That(ShotActive(), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Actual_enemy_controller_marks_only_non_elite_projectiles(bool elite)
        {
            Equip(3);
            var enemies = (Array)Get(_sim, "Enemies");
            var enemy = Activator.CreateInstance(enemies.GetType().GetElementType());
            Put(enemy, "Active", true); Put(enemy, "Id", "gunner"); Put(enemy, "Position", new Vector2(220, 0));
            Put(enemy, "Health", 100f); Put(enemy, "MaxHealth", 100f); Put(enemy, "Age", 2f); Put(enemy, "Radius", 12f);
            Put(enemy, "SpawnId", 777); Put(enemy, "State", 1); Put(enemy, "StateTimer", 0f); Put(enemy, "DashDirection", Vector2.left); Put(enemy, "Damage", 10f);
            Put(enemy, "Elite", elite); if (elite) Put(enemy, "EliteKind", EliteVariantId.Gunner);
            enemies.SetValue(enemy, 0); _sim.GetType().GetMethod("AppendEnemyOrder").Invoke(_sim, new object[] { 0 });
            Call("UpdateEnemies", .02f);
            var shots = (Array)Get(_sim, "HostileShots"); var blockable = (bool[])Get(_sim, "HostileShotBlockable");
            var active = 0;
            for (var i = 0; i < shots.Length; i++)
            {
                if (!(bool)Get(shots.GetValue(i), "Active")) continue;
                active++; Assert.That(blockable[i], Is.EqualTo(!elite));
            }
            Assert.That(active, Is.GreaterThan(0));
            Assert.That((bool)Get(_runtime, "_ordinaryEnemyShotContext"), Is.False);
        }

        [Test]
        public void Clock_does_not_mix_old_hand_pose_with_future_bullet_position()
        {
            Equip(8);
            Put(_runtime, "_orbitalClockStartAngle", 0f); Put(_runtime, "_arsenalClockAngle", -.2f);
            Spawn(true, new Vector2(120, 13), Vector2.down, 30);
            var intercepted = (bool)Call("TryInterceptHostileShot", 0, new Vector2(120, 13), new Vector2(120, 10), 5f);
            Assert.That(intercepted, Is.False, "The moving hand and shot never touch at the same instant");
        }

        [Test]
        public void Launched_hollow_blade_also_intercepts_ordinary_shots()
        {
            var progress = Equip(3); progress.Evolved[3] = true;
            Put(_runtime, "_hollowBladeActive", true); Put(_runtime, "_hollowBladeAge", .65f); Put(_runtime, "_hollowBladeAngle", 0f);
            Call("UpdateBlades", 0f); Call("UpdateBlades", 0f);
            var position = (Vector2)((SpriteRenderer)Get(_runtime, "_hollowBladeView")).transform.position;
            Assert.That(position.magnitude, Is.GreaterThan(150));
            Spawn(true, position + Vector2.down * 35, Vector2.up, 700);
            Call("UpdateHostileShots", .1f);
            Assert.That(ShotActive(), Is.False);
        }
    }
}
