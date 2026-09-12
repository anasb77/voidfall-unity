using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class MineBalanceIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const float Dt = 1f / 120;
        private VoidFallGameRuntime _runtime;
        private object _sim, _oldStore, _oldProfile, _oldExport;
        private bool _oldEnabled;
        private string _directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _oldEnabled = _runtime.enabled; _runtime.enabled = false;
            _oldStore = Get(_runtime, "_saveStore"); _oldProfile = Get(_runtime, "_saveData");
            _oldExport = Get(_runtime, "_runExportDirectoryOverride");
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-mine-balance-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Set(_runtime, "_saveStore", new SaveStore(Path.Combine(_directory, "profile.json")));
            Set(_runtime, "_saveData", SaveStore.CreateDefault());
            Set(_runtime, "_runExportDirectoryOverride", Path.Combine(_directory, "RunExports"));
            Set(_runtime, "_runSaved", true);
            Call("StartRunInternal", true, false);
            _sim = Get(_runtime, "_gameSim");
            Array.Clear(Enemies, 0, Enemies.Length);
            _sim.GetType().GetMethod("ResetEnemyOrder").Invoke(_sim, null);
            Call("RebuildEnemyGrid");
            Equip(6, true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Call("FinishRunExport", "test_finished");
                Set(_runtime, "_runSaved", true); Call("EnterMainMenu");
                Set(_runtime, "_saveStore", _oldStore); Set(_runtime, "_saveData", _oldProfile);
                Set(_runtime, "_runExportDirectoryOverride", _oldExport); _runtime.enabled = _oldEnabled;
            }
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            yield return null;
        }

        [TestCase(1, 2.4f)] [TestCase(2, 2.4f)] [TestCase(3, 2.1f)]
        [TestCase(4, 2.1f)] [TestCase(5, 1.9f)] [TestCase(6, 1.8f)]
        public void Moving_player_places_mines_at_reduced_rank_cadence(int rank, float delay)
        {
            Equip(rank, false);
            var placements = PlacementTimes(6);
            Assert.That(placements.Length, Is.GreaterThanOrEqualTo(3));
            for (var i = 1; i < placements.Length; i++)
                Assert.That(placements[i] - placements[i - 1], Is.EqualTo(delay).Within(.012f));
        }

        [Test]
        public void Maximum_recovery_and_overclock_cannot_place_faster_than_point_nine_seconds()
        {
            MaximumRecovery();
            Assert.That((float)Call("WeaponRecoveryScale"), Is.LessThan(.5f));
            var placements = PlacementTimes(6);
            Assert.That(placements.Length, Is.InRange(6, 7));
            for (var i = 1; i < placements.Length; i++)
                Assert.That(placements[i] - placements[i - 1], Is.InRange(.899f, .912f));
        }

        [Test]
        public void Repeated_blasts_cannot_refresh_freeze_or_skip_mobile_recovery()
        {
            Enemy(100); Detonate();
            var frozenTicks = 0;
            for (var tick = 0; tick < 280; tick++)
            {
                if (tick > 0 && tick % 30 == 0) Detonate();
                if ((bool)Call("AdvanceArsenalFrozenEnemy", 0, Enemies.GetValue(0), Dt)) frozenTicks++;
            }
            Assert.That(frozenTicks, Is.InRange(144, 145), "Overlapping blasts must leave a full mobile recovery period.");
            Assert.That((bool)Call("AdvanceArsenalFrozenEnemy", 0, Enemies.GetValue(0), .1f), Is.False);
            Detonate();
            Assert.That((bool)Call("AdvanceArsenalFrozenEnemy", 0, Enemies.GetValue(0), Dt), Is.True);
        }

        [Test]
        public void Replacement_identity_and_stale_callback_cannot_inherit_or_consume_control_state()
        {
            Enemy(100); Detonate();
            var old = Enemies.GetValue(0);
            Enemy(101, false); Detonate();
            var freeze = ((float[])Get(_runtime, "_arsenalFreeze"))[0];
            Assert.That(freeze, Is.InRange(1.19f, 1.21f));
            Assert.That((bool)Call("AdvanceArsenalFrozenEnemy", 0, old, 1f), Is.False);
            Assert.That(((float[])Get(_runtime, "_arsenalFreeze"))[0], Is.EqualTo(freeze));
            Assert.That((bool)Call("AdvanceArsenalFrozenEnemy", 0, Enemies.GetValue(0), Dt), Is.True);
            Call("ClearTransitionProjectiles");
            Assert.That((bool)Call("AdvanceArsenalFrozenEnemy", 0, Enemies.GetValue(0), Dt), Is.False);
            Detonate();
            Assert.That((bool)Call("AdvanceArsenalFrozenEnemy", 0, Enemies.GetValue(0), Dt), Is.True);
            Call("ResetArsenalWeapons");
            Assert.That((bool)Call("AdvanceArsenalFrozenEnemy", 0, Enemies.GetValue(0), Dt), Is.False);
        }

        [Test]
        public void Export_records_committed_placements_and_aggregate_freeze_rejections()
        {
            Enemy(100);
            TickArsenal();
            Call("StepArsenalMines", .6f);
            Detonate();
            var history = FinishHistory();
            Assert.That(history.Any(e => e.kind == "mine_placement" && e.reason == "placed" && e.sourceId == "mines"), Is.True);
            var blasts = history.Where(e => e.kind == "mine_detonated").ToArray();
            Assert.That(blasts, Has.Length.EqualTo(2));
            Assert.That(blasts[0].amount, Is.EqualTo(1));
            Assert.That(blasts[0].durationSeconds, Is.EqualTo(1.2f));
            Assert.That(blasts[1].amount, Is.Zero);
            Assert.That(blasts[1].blockedAttempts, Is.EqualTo(1));
        }

        [TestCase("warden")] [TestCase("matriarch")] [TestCase("reaver")]
        public void Boss_matchup_runs_actual_controller_and_records_mine_damage(string id)
        {
            MaximumRecovery();
            // Hermetic diagnostic: mine-only rank VI, fixed seed, stationary invulnerable
            // target. Real spawned boss, adds, shots and controllers advance at 120 Hz.
            Set(_sim, "Rng", new Rng(74821));
            Call("SpawnBoss", id, 1d, 1d, 0);
            var bosses = (Array)Get(_sim, "Bosses");
            var initial = bosses.GetValue(0);
            var maxHp = (float)Get(initial, "MaxHealth");
            var firstPosition = (Vector2)Get(initial, "Position");
            var moved = false; var activeTicks = 0;
            for (var tick = 0; tick < 30 * 120; tick++)
            {
                var player = Get(_sim, "Player"); Set(player, "Health", 100000f); Set(_sim, "Player", player);
                Set(_runtime, "_time", tick * Dt);
                Call("UpdateEnemies", Dt); Call("UpdateArsenalWeapons", Dt);
                Call("UpdateHostileShots", Dt); Call("UpdateBosses", Dt);
                var boss = bosses.GetValue(0);
                moved |= Vector2.Distance((Vector2)Get(boss, "Position"), firstPosition) > 5;
                if (!(bool)Get(boss, "Active")) break;
                activeTicks++;
                Call("RebuildEnemyGrid");
            }
            var final = bosses.GetValue(0);
            var history = FinishHistory();
            var damage = history.Where(e => e.kind == "boss_damage" && e.id == id).Sum(e => e.amount);
            TestContext.WriteLine("MINE_MATCHUP boss=" + id + " seed=74821 seconds=" + (activeTicks * Dt).ToString("F3") +
                " startHp=" + maxHp + " endHp=" + Get(final, "Health") + " damage=" + damage +
                " placements=" + history.Count(e => e.kind == "mine_placement" && e.reason == "placed") +
                " detonations=" + history.Count(e => e.kind == "mine_detonated") +
                " weaponDamage=" + ((double[])Get(_runtime, "_weaponDamage"))[6]);
            Assert.That(activeTicks, Is.GreaterThan(120));
            Assert.That(moved, Is.True, "Mine control must not stop the boss controller.");
            Assert.That(damage, Is.GreaterThan(0), "Boss must meet real armed mines during the fixture.");
        }

        private float[] PlacementTimes(float seconds)
        {
            // Count actual new pool entries too, so the cadence test fails before telemetry exists.
            var times = new System.Collections.Generic.List<float>();
            var previous = 0;
            for (var tick = 0; tick < seconds * 120; tick++)
            {
                var player = Get(_sim, "Player"); Set(player, "Position", new Vector2(tick, 0)); Set(_sim, "Player", player);
                TickArsenal();
                var active = ((Array)Get(_runtime, "_arsenalMines")).Cast<object>().Count(m => (bool)Get(m, "Active"));
                if (active > previous) times.Add(tick * Dt);
                previous = active;
            }
            return times.ToArray();
        }

        private void Equip(int rank, bool evolved)
        {
            var progress = new UpgradeProgress(); progress.WeaponRanks[6] = rank; progress.Evolved[6] = evolved;
            Set(_runtime, "_upgradeProgress", progress); Call("RecalculatePlayerStats", false); Set(_runtime, "_critChance", 0f);
        }
        private void MaximumRecovery()
        {
            var progress = (UpgradeProgress)Get(_runtime, "_upgradeProgress");
            var supports = ExtendedCatalog.AllSupports();
            foreach (var id in new[] { "cycling", "adrenal" })
            {
                var index = Array.FindIndex(supports, s => s.Id == id);
                progress.SupportRanks[index] = supports[index].MaxRank;
            }
            Call("RecalculatePlayerStats", false); Set(_runtime, "_critChance", 0f); Set(_runtime, "_adrenalTimer", 60f);
            var overclock = new OverclockState(); for (var i = 0; i < 3; i++) overclock.ApplyPickup();
            Set(_runtime, "_overclock", overclock);
        }
        private void Enemy(int identity, bool append = true)
        {
            var enemy = Activator.CreateInstance(Enemies.GetType().GetElementType());
            Set(enemy, "Active", true); Set(enemy, "Id", "chaser"); Set(enemy, "Position", new Vector2(10, 0));
            Set(enemy, "Radius", 12f); Set(enemy, "Health", 100000f); Set(enemy, "MaxHealth", 100000f);
            Set(enemy, "Age", 1f); Set(enemy, "SpawnId", identity); Enemies.SetValue(enemy, 0);
            if (append) _sim.GetType().GetMethod("AppendEnemyOrder").Invoke(_sim, new object[] { 0 });
            Call("RebuildEnemyGrid");
        }
        private void Detonate()
        {
            var mines = (Array)Get(_runtime, "_arsenalMines");
            var mine = Activator.CreateInstance(mines.GetType().GetElementType());
            Set(mine, "Active", true); Set(mine, "Rank", 6); Set(mine, "Evolved", true); Set(mine, "Age", .6f);
            Set(mine, "Position", Vector2.zero); mines.SetValue(mine, 0); Call("StepArsenalMines", Dt);
        }
        private void TickArsenal()
        {
            Set(_runtime, "_time", (float)Get(_runtime, "_time") + Dt); Call("UpdateArsenalWeapons", Dt);
        }
        private UnityTelemetryHistoryEvent[] FinishHistory()
        {
            Call("FinishRunExport", "mine_balance_diagnostic");
            return File.ReadAllLines(Directory.GetFiles(Path.Combine(_directory, "RunExports"), "*.jsonl").Single())
                .Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
        }
        private Array Enemies => (Array)Get(_sim, "Enemies");
        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private object Call(string name, params object[] args)
        {
            var method = _runtime.GetType().GetMethods(Flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length &&
                m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x));
            try { return method.Invoke(_runtime, args); } catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
        }
    }
}
