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
    public sealed class ArsenalIntegrationTests
    {
        private VoidFallGameRuntime _runtime;
        private object _sim;
        private object _store, _profile;
        private bool _enabled;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _enabled = _runtime.enabled;
            _runtime.enabled = false;
            _store = Get("_saveStore"); _profile = Get("_saveData");
            Set("_saveStore", new SaveStore(Path.Combine(Path.GetTempPath(), "voidfall-arsenal-" + Guid.NewGuid().ToString("N"), "profile.json")));
            Set("_saveData", SaveStore.CreateDefault());
            Set("_runSaved", true);
            Call("StartRunInternal", false, false);
            _sim = Get("_gameSim");
            Array.Clear(Enemies, 0, Enemies.Length);
            _sim.GetType().GetMethod("ResetEnemyOrder").Invoke(_sim, null);
            Call("RebuildEnemyGrid");
            Set("_critChance", 0f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Set("_runSaved", true);
                Call("EnterMainMenu");
                Set("_saveStore", _store); Set("_saveData", _profile);
                _runtime.enabled = _enabled;
            }
            yield return null;
        }

        private void Equip(int weapon, int rank = 1, bool evolved = false)
        {
            var progress = new UpgradeProgress(); progress.WeaponRanks[weapon] = rank; progress.Evolved[weapon] = evolved;
            Set("_upgradeProgress", progress);
            Call("RecalculatePlayerStats", false);
            Set("_critChance", 0f);
        }
        private void Enemy(int slot, Vector2 position, int identity = 0)
        {
            var enemy = Activator.CreateInstance(Enemies.GetType().GetElementType());
            FieldSet(enemy, "Active", true); FieldSet(enemy, "Id", "chaser"); FieldSet(enemy, "Position", position);
            FieldSet(enemy, "Radius", 12f); FieldSet(enemy, "Health", 1000f); FieldSet(enemy, "MaxHealth", 1000f); FieldSet(enemy, "Age", 1f); FieldSet(enemy, "SpawnId", identity == 0 ? 100 + slot : identity);
            Enemies.SetValue(enemy, slot);
            _sim.GetType().GetMethod("AppendEnemyOrder").Invoke(_sim, new object[] { slot }); Call("RebuildEnemyGrid");
        }
        private void Step(float seconds)
        {
            for (var i = 0; i < Mathf.CeilToInt(seconds * 120); i++)
            {
                Set("_time", (float)Get("_time") + 1f / 120);
                Call("UpdateArsenalWeapons", 1f / 120);
            }
        }
        private int Active(string field)
        {
            var count = 0; foreach (var entry in (Array)Get(field)) if ((bool)entry.GetType().GetField("Active").GetValue(entry)) count++; return count;
        }

        [Test]
        public void Mines_arm_before_exploding_and_report_damage()
        {
            Equip(6); Enemy(0, new Vector2(10, 0));
            Step(.25f); Assert.That(Health(0), Is.EqualTo(1000));
            Step(.4f); Assert.That(Health(0), Is.EqualTo(940).Within(.01));
            Assert.That(((double[])Get("_weaponDamage"))[6], Is.GreaterThan(0));
        }

        [Test]
        public void Mine_freeze_expires_and_cannot_transfer_to_reused_slots()
        {
            Equip(6, 6, true); Enemy(0, new Vector2(10, 0)); Step(.7f);
            Assert.That(((float[])Get("_arsenalFreeze"))[0], Is.GreaterThan(2));
            var old = Enemies.GetValue(0);
            var replacement = Enemies.GetValue(0); FieldSet(replacement, "SpawnId", (int)Field(old, "SpawnId") + 1000);
            Assert.That((bool)Call("AdvanceArsenalFrozenEnemy", 0, replacement, .1f), Is.False);
            for (var i = 0; i < 30; i++) Call("AdvanceArsenalFrozenEnemy", 0, old, .1f);
            Assert.That((bool)Call("AdvanceArsenalFrozenEnemy", 0, old, .1f), Is.False);
        }

        [Test]
        public void Idle_summons_persist_follow_and_engage_nearby_targets()
        {
            Equip(7); Enemy(0, new Vector2(1000, 0)); Step(15);
            Assert.That(Active("_arsenalSummons"), Is.EqualTo(2));
            Assert.That(Health(0), Is.EqualTo(1000));
            PlayerPosition = new Vector2(-150, 0); Step(1);
            foreach (var entry in (Array)Get("_arsenalSummons"))
            {
                if (!(bool)entry.GetType().GetField("Active").GetValue(entry)) continue;
                Assert.That((float)entry.GetType().GetField("Age").GetValue(entry), Is.GreaterThan(14));
                Assert.That(Vector2.Distance((Vector2)entry.GetType().GetField("Position").GetValue(entry), PlayerPosition), Is.LessThan(85));
            }
            Enemy(1, PlayerPosition + new Vector2(100, 0)); Step(1);
            Assert.That(Health(1), Is.LessThan(1000));
        }

        [Test]
        public void Frozen_enemies_keep_damage_reception_timers_advancing()
        {
            Equip(6, 6, true); Enemy(0, new Vector2(10, 0)); Step(.7f);
            var enemy = Enemies.GetValue(0);
            FieldSet(enemy, "BladeCooldown", .1f); FieldSet(enemy, "HollowCooldown", .1f); Enemies.SetValue(enemy, 0);
            Call("UpdateEnemies", .2f);
            enemy = Enemies.GetValue(0);
            Assert.That((float)Field(enemy, "BladeCooldown"), Is.Zero);
            Assert.That((float)Field(enemy, "HollowCooldown"), Is.Zero);
            Assert.That(((float[])Get("_arsenalFreeze"))[0], Is.GreaterThan(0));
        }

        [Test]
        public void Evolved_summons_damage_the_area_around_their_target()
        {
            Equip(7, 1, true); Enemy(0, new Vector2(80, 0)); Enemy(1, new Vector2(80, 30)); Step(.7f);
            Assert.That(Health(0), Is.LessThan(1000)); Assert.That(Health(1), Is.LessThan(1000));
        }

        [Test]
        public void Clock_hits_nearby_targets_with_a_repeat_gate_and_rotates_clockwise()
        {
            Equip(8); Enemy(0, new Vector2(75, 0)); Enemy(1, new Vector2(260, 0)); Step(.05f);
            Assert.That(Health(0), Is.EqualTo(980).Within(.01));
            Assert.That(Health(1), Is.EqualTo(1000));
            Assert.That((float)Get("_arsenalClockAngle"), Is.GreaterThan(Mathf.PI));
        }

        [Test]
        public void Boomerang_bounces_between_distinct_targets_and_returns_harmlessly()
        {
            Equip(9); Enemy(0, new Vector2(80, 0)); Enemy(1, new Vector2(140, 20)); Step(1.3f);
            Assert.That(Health(0), Is.EqualTo(976).Within(.01));
            Assert.That(Health(1), Is.EqualTo(976).Within(.01));
        }

        [Test]
        public void Evolved_boomerang_launches_three_and_transition_clears_all_new_state()
        {
            Equip(9, 6, true); Enemy(0, new Vector2(300, 0)); Step(.01f);
            Assert.That(Active("_arsenalBoomerangs"), Is.EqualTo(3));
            Call("RenderArsenalWeapons"); Call("ClearTransitionProjectiles");
            Assert.That(Active("_arsenalBoomerangs"), Is.Zero);
            foreach (var renderer in (SpriteRenderer[])Get("_arsenalBoomerangViews")) if (renderer != null) Assert.That(renderer.enabled, Is.False);
        }

        [Test]
        public void New_weapons_damage_bosses_and_clock_keeps_approved_opacity()
        {
            Equip(8, 6, true);
            var bosses = (Array)Field(_sim, "Bosses");
            var boss = Activator.CreateInstance(bosses.GetType().GetElementType());
            FieldSet(boss, "Active", true); FieldSet(boss, "Id", "herald"); FieldSet(boss, "Position", new Vector2(75, 0)); FieldSet(boss, "Radius", 20f); FieldSet(boss, "Health", 1000f); FieldSet(boss, "MaxHealth", 1000f); FieldSet(boss, "TelemetryInstanceId", 501);
            bosses.SetValue(boss, 0); ((int[])Field(_sim, "BossOrder"))[0] = 0; FieldSet(_sim, "BossOrderCount", 1);
            Step(.05f); Assert.That((float)Field(bosses.GetValue(0), "Health"), Is.LessThan(1000));
            Call("RenderArsenalWeapons");
            Assert.That(((SpriteRenderer)Get("_arsenalClockFace")).color.a, Is.EqualTo(.35f));
            var hands = (SpriteRenderer[])Get("_arsenalClockHands");
            Assert.That(hands[0].enabled && hands[1].enabled, Is.True);
            Assert.That(hands[0].color.a, Is.EqualTo(.5f));
        }

        [Test]
        public void Roulette_new_card_cannot_bypass_weapon_slots()
        {
            var progress = new UpgradeProgress();
            progress.WeaponRanks[0] = progress.WeaponRanks[1] = progress.WeaponRanks[2] = 1;
            for (var i = 0; i < progress.SupportRanks.Length; i++) progress.SupportRanks[i] = ExtendedCatalog.AllSupports()[i].MaxRank;
            Set("_upgradeProgress", progress); Set("_rouletteRng", new Rng(7));
            Call("GrantNewCardRank");
            Assert.That(Array.FindAll(progress.WeaponRanks, rank => rank > 0).Length, Is.EqualTo(3));
        }

        [Test]
        public void Evolution_supports_are_visible_in_build_hud()
        {
            Equip(7);
            var progress = (UpgradeProgress)Get("_upgradeProgress");
            var supports = ExtendedCatalog.AllSupports();
            var dodge = Array.FindIndex(supports, s => s.Id == "dodge");
            var speed = Array.FindIndex(supports, s => s.Id == "projectileSpeed");
            progress.SupportRanks[dodge] = 1; progress.SupportRanks[speed] = 1;
            Call("UpdateBuildChipHud");
            var chips = (UnityEngine.UI.Image[])Get("_supportChipBackgrounds");
            Assert.That(chips.Length, Is.EqualTo(supports.Length));
            Assert.That(chips[dodge].enabled && chips[speed].enabled, Is.True);
        }

        [TestCase("mines")]
        [TestCase("summons")]
        [TestCase("clock")]
        [TestCase("boomerang")]
        public void Every_rank_has_distinct_authored_pixels(string id)
        {
            var previous = 0UL;
            for (var rank = 1; rank <= 6; rank++)
            {
                var factory = typeof(VoidFallGameRuntime).Assembly.GetType("VoidFall.Runtime.ProceduralSpriteFactory");
                var sprite = (Sprite)factory.GetMethod("ArsenalWeapon").Invoke(null, new object[] { id, rank, false });
                var hash = 14695981039346656037UL;
                foreach (var pixel in sprite.texture.GetPixels32()) { hash = (hash ^ pixel.r) * 1099511628211UL; hash = (hash ^ pixel.g) * 1099511628211UL; hash = (hash ^ pixel.b) * 1099511628211UL; hash = (hash ^ pixel.a) * 1099511628211UL; }
                Assert.That(hash, Is.Not.EqualTo(previous), "Rank " + rank + " should change weapon appearance"); previous = hash;
            }
        }

        private object Get(string name) => typeof(VoidFallGameRuntime).GetField(name, Flags).GetValue(_runtime);
        private static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void FieldSet(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private Array Enemies => (Array)Field(_sim, "Enemies");
        private float Health(int slot) => (float)Field(Enemies.GetValue(slot), "Health");
        private Vector2 PlayerPosition
        {
            get => (Vector2)Field(Field(_sim, "Player"), "Position");
            set { var player = Field(_sim, "Player"); FieldSet(player, "Position", value); FieldSet(_sim, "Player", player); }
        }
        private void Set(string name, object value) => typeof(VoidFallGameRuntime).GetField(name, Flags).SetValue(_runtime, value);
        private object Call(string name, params object[] arguments)
        {
            foreach (var method in typeof(VoidFallGameRuntime).GetMethods(Flags))
                if (method.Name == name && method.GetParameters().Length == arguments.Length)
                    try { return method.Invoke(_runtime, arguments); } catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
            throw new MissingMethodException(name);
        }
    }
}
