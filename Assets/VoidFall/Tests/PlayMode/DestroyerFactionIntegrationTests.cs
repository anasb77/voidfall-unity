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
    public sealed class DestroyerFactionIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private SimulationProfileScope _profile;
        private object _sim;
        private Array _enemies;
        private bool _enabled;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>(); Assert.That(_runtime, Is.Not.Null);
            _enabled = _runtime.enabled; _runtime.enabled = false;
            _profile = new SimulationProfileScope(_runtime); Call("StartRunInternal", false, false);
            _sim = Get(_runtime, "_gameSim"); _enemies = (Array)Get(_sim, "Enemies");
            Array.Clear(_enemies, 0, _enemies.Length); _sim.GetType().GetMethod("ResetEnemyOrder").Invoke(_sim, null);
            Call("ResetFactionAndRewardArena"); Call("ResetFactionRunDiagnostics");
            Put(_runtime, "_encounterInitialized", false); Put(_runtime, "_destroyerRaidActive", true);
            Player("Position", new Vector2(1500, 500)); Player("Iframes", 0f);
        }
        [TearDown]
        public void TearDown()
        {
            Call("EndDestroyerRaid"); _profile?.Dispose();
            if (_runtime != null) _runtime.enabled = _enabled;
        }
        [TestCase("destroyer-maw")]
        [TestCase("destroyer-razor")]
        [TestCase("destroyer-husk")]
        [TestCase("destroyer-grasp")]
        [TestCase("destroyer-spite")]
        public void All_five_archetypes_inflict_real_damage_and_receive_ordinary_contact(string id)
        {
            var raider = Spawn(id, Vector2.zero); var ordinary = Spawn("chaser", new Vector2(35, 0));
            Change(ordinary, "Health", 10000f); Change(ordinary, "MaxHealth", 10000f); Change(ordinary, "Speed", 0f);
            var raiderBefore = Health(raider); var dealtBefore = (double)Get(_runtime, "_damageDealt");
            for (var tick = 0; tick < 260; tick++) Step(.02f);
            Assert.That(Health(ordinary), Is.LessThan(10000));
            Assert.That(Health(raider), Is.LessThan(raiderBefore));
            Assert.That((double)Get(_runtime, "_damageDealt"), Is.EqualTo(dealtBefore), "NPC combat cannot receive STANDSTILL/player damage attribution");
        }
        [Test]
        public void Warning_keeps_original_point_when_opponent_moves_dies_and_slot_recycles()
        {
            var raider = Spawn("destroyer-maw", Vector2.zero); var target = Spawn("chaser", new Vector2(110, 0));
            Step(.1f); Assert.That((int)Get(_enemies.GetValue(raider), "State"), Is.EqualTo(1));
            var direction = (Vector2)Get(_enemies.GetValue(raider), "DashDirection");
            Change(target, "Active", false); Call("RemoveEnemyOrder", target);
            var replacement = Spawn("chaser", new Vector2(-110, 0)); Assert.That(replacement, Is.EqualTo(target));
            for (var tick = 0; tick < 110; tick++) Step(.02f);
            Assert.That((Vector2)Get(_enemies.GetValue(raider), "DashDirection"), Is.EqualTo(direction));
            Assert.That(Vector2.Distance((Vector2)Get(_enemies.GetValue(raider), "Position"), direction * (620 * .47f)), Is.LessThan(.1f));
        }
        [Test]
        public void Same_side_damage_is_immune_and_partial_player_damage_earns_assist_score_and_xp()
        {
            var raider = Spawn("destroyer-maw", Vector2.zero); var ordinary = Spawn("chaser", new Vector2(100, 0));
            var hp = Health(ordinary); var actor = _enemies.GetValue(ordinary);
            using ((IDisposable)Call("EnemyFactionScope", actor)) Call("ApplyEnemyDamage", ordinary, 10000f);
            Assert.That(Health(ordinary), Is.EqualTo(hp));
            Call("ApplyEnemyDamage", ordinary, hp * .25f);
            var initialScore = (int)Get(_runtime, "_score");
            using ((IDisposable)Call("EnemyFactionScope", _enemies.GetValue(raider))) Call("ApplyEnemyDamage", ordinary, 10000f);
            Assert.That(_runtime.AssistedDefeats, Is.EqualTo(1));
            Assert.That((int)Get(_runtime, "_score") - initialScore, Is.EqualTo(2));
            Assert.That((double)Get(_runtime, "_fractionalFactionScore"), Is.EqualTo(.5).Within(.001));
            var pickups = (Array)Get(_sim, "Pickups"); var xp = 0f;
            foreach (var item in pickups) if ((bool)Get(item, "Active") && Get(item, "Kind").ToString() == "Xp") xp += (float)Get(item, "Value");
            Assert.That(xp, Is.GreaterThan(0));
            Call("KillEnemy", ordinary); Assert.That(_runtime.AssistedDefeats, Is.EqualTo(1), "each death once");
        }
        [Test]
        public void Npc_hits_ignore_player_standstill_bonus_and_player_damage_statistics()
        {
            var source = Spawn("destroyer-maw", Vector2.zero); var target = Spawn("chaser", new Vector2(100, 0));
            ((System.Collections.Generic.HashSet<WildCardId>)Get(_runtime, "_activeWildCards")).Add(WildCardId.Standstill);
            Put(_runtime, "_standstillSeconds", 10d);
            var before = Health(target); var statistics = (double)Get(_runtime, "_damageDealt");
            using ((IDisposable)Call("EnemyFactionScope", _enemies.GetValue(source))) Call("ApplyEnemyDamage", target, 3f);
            Assert.That(Health(target), Is.EqualTo(before - 3f).Within(.001));
            Assert.That((double)Get(_runtime, "_damageDealt"), Is.EqualTo(statistics));
        }
        [Test]
        public void Rival_projectile_hits_nearer_ordinary_body_before_player_and_clears_on_reuse()
        {
            var source = Spawn("destroyer-spite", Vector2.zero); var target = Spawn("chaser", new Vector2(80, 0));
            Player("Position", new Vector2(160, 0)); var playerBefore = (float)Get(Get(_sim, "Player"), "Health");
            var enemyBefore = Health(target);
            using ((IDisposable)Call("EnemyFactionScope", _enemies.GetValue(source)))
                Call("SpawnHostileShot", Vector2.zero, Vector2.right, 10f, 2000f, 0f, false, -1, 0f);
            Call("RebuildEnemyGrid"); Call("UpdateHostileShots", .1f);
            Assert.That(Health(target), Is.LessThan(enemyBefore));
            Assert.That((float)Get(Get(_sim, "Player"), "Health"), Is.EqualTo(playerBefore));
            Call("SpawnHostileShot", Vector2.zero, Vector2.right, 10f, 100f, 0f, false, -1, 0f);
            var sources = (Array)Get(_sim, "HostileShotSources");
            Assert.That((CombatFaction)Get(sources.GetValue(0), "Faction"), Is.EqualTo(CombatFaction.Enemy));
            Assert.That((int)Get(sources.GetValue(0), "SpawnId"), Is.Zero);
        }
        [Test]
        public void Withdrawal_cancels_raider_projectiles_and_ends_without_kill_rewards()
        {
            Call("EndDestroyerRaid"); Call("StartDestroyerRaid", Vector2.zero);
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(5));
            var first = FirstActive(); var score = Get(_runtime, "_score");
            using ((IDisposable)Call("EnemyFactionScope", _enemies.GetValue(first)))
                Call("SpawnHostileShot", Vector2.zero, Vector2.right, 10f, 100f, 0f, false, -1, 0f);
            Call("StepDestroyerRaid", 0f, true);
            Assert.That((bool)Get(((Array)Get(_sim, "HostileShots")).GetValue(0), "Active"), Is.True, "paused lifecycle cannot advance");
            Call("StepDestroyerRaid", .1f, true); Call("EndDestroyerRaid");
            Assert.That(_runtime.ActiveEnemiesCount, Is.Zero); Assert.That(Get(_runtime, "_score"), Is.EqualTo(score));
            Assert.That((bool)Get(((Array)Get(_sim, "HostileShots")).GetValue(0), "Active"), Is.False);
        }
        [Test]
        public void Repeated_carrier_and_boss_children_share_finite_reward_roots()
        {
            var carrier = Spawn("carrier", Vector2.zero); var rewarded = 0;
            using ((IDisposable)Call("EnemyFactionScope", _enemies.GetValue(carrier)))
                for (var n = 0; n < 80; n++)
                {
                    var child = Spawn("runner", new Vector2(200, 0));
                    var actors = (Array)Get(_runtime, "_factionActors");
                    if ((bool)Get(actors.GetValue(child), "Rewardable")) rewarded++;
                    Call("KillEnemy", child);
                }
            Assert.That(rewarded, Is.EqualTo(FactionRewardRules.AmbientOffspringAllowance));
            var root = (long)Call("BossRewardRoot", 1234); rewarded = 0;
            using ((IDisposable)Call("FactionBirthScope", root))
                for (var n = 0; n < 90; n++)
                {
                    var child = Spawn("runner", new Vector2(200, 0));
                    if ((bool)Get(((Array)Get(_runtime, "_factionActors")).GetValue(child), "Rewardable")) rewarded++;
                    Call("KillEnemy", child);
                }
            Assert.That(rewarded, Is.EqualTo(FactionRewardRules.BossOffspringAllowance));
        }
        [Test]
        public void Coincident_bodies_separate_deterministically_without_rng_draws()
        {
            var a = Spawn("chaser", Vector2.zero); var b = Spawn("chaser", Vector2.zero);
            var rng = (Rng)Get(_sim, "Rng"); var draws = rng.Draws;
            Call("RebuildEnemyGrid"); _sim.GetType().GetMethod("SeparateEnemies").Invoke(_sim, null);
            var first = (Vector2)Get(_enemies.GetValue(a), "Position");
            var second = (Vector2)Get(_enemies.GetValue(b), "Position");
            Assert.That(Vector2.Distance(first, second), Is.GreaterThan(0));
            Change(a, "Position", Vector2.zero); Change(b, "Position", Vector2.zero);
            Call("RebuildEnemyGrid"); _sim.GetType().GetMethod("SeparateEnemies").Invoke(_sim, null);
            Assert.That((Vector2)Get(_enemies.GetValue(a), "Position"), Is.EqualTo(first));
            Assert.That(rng.Draws, Is.EqualTo(draws));
        }
        [Test]
        public void Raid_warnings_obey_profile_attention_limit_and_do_not_advance_at_zero_dt()
        {
            Player("Position", new Vector2(70, 0));
            for (var i = 0; i < 5; i++) Spawn(DestroyerContent.Enemies[i].Id, Vector2.zero);
            Step(0);
            foreach (var actor in _enemies) if ((bool)Get(actor, "Active")) Assert.That((int)Get(actor, "State"), Is.Zero);
            Step(.1f); var warnings = 0;
            foreach (var actor in _enemies) if ((bool)Get(actor, "Active") && (int)Get(actor, "State") == 1) warnings++;
            Assert.That(warnings, Is.GreaterThan(0));
            Assert.That(warnings, Is.LessThanOrEqualTo(DirectorProfiles.AttackLimit((DirectorProfileId)Get(_runtime, "_runDirectorProfile"), _runtime.PressureHundredths)));
        }
        private void Step(float dt) { Call("RebuildEnemyGrid"); Call("UpdateEnemies", dt); Call("RebuildEnemyGrid"); Call("UpdateHostileShots", dt); }
        private int Spawn(string id, Vector2 position)
        {
            var identity = (int)Get(_runtime, "_nextEnemyId");
            Assert.That((bool)Call("SpawnEnemy", id, (Vector2?)position, null, false, false, 0, 1f, (EnemyRoster?)EnemyRoster.One, -1, null), Is.True);
            for (var i = 0; i < _enemies.Length; i++) if ((bool)Get(_enemies.GetValue(i), "Active") && (int)Get(_enemies.GetValue(i), "SpawnId") == identity)
            { Change(i, "Age", .5f); return i; }
            throw new Exception("spawn missing");
        }
        private int FirstActive() { for (var i = 0; i < _enemies.Length; i++) if ((bool)Get(_enemies.GetValue(i), "Active")) return i; return -1; }
        private float Health(int slot) => (float)Get(_enemies.GetValue(slot), "Health");
        private void Change(int slot, string field, object value) { var actor = _enemies.GetValue(slot); Put(actor, field, value); _enemies.SetValue(actor, slot); }
        private void Player(string field, object value) { var player = Get(_sim, "Player"); Put(player, field, value); Put(_sim, "Player", player); }
        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Put(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private object Call(string name, params object[] args)
        {
            foreach (var method in _runtime.GetType().GetMethods(Flags))
                if (method.Name == name && method.GetParameters().Length == args.Length)
                    try { return method.Invoke(_runtime, args); } catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
            throw new MissingMethodException(name);
        }
    }
}
