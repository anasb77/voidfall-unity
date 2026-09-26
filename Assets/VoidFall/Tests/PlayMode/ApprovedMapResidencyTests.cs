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
    public sealed class ApprovedMapResidencyTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private SimulationProfileScope _profile;
        private bool _enabled;
        private ArenaResidencyManager Residency => (ArenaResidencyManager)Get("_arenaResidency");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _enabled = _runtime.enabled;
            _runtime.enabled = false;
            _profile = new SimulationProfileScope(_runtime);
            Call("StartRunInternal", false, false);
            Call("PrepareMenuArenaCatalogue");
            yield return WaitFor(ArenaId.Hydra);
            yield return WaitFor(ArenaId.NullCity);
            yield return WaitFor(ArenaId.MonochromeCourt);
        }

        [TearDown]
        public void TearDown()
        {
            _profile?.Dispose();
            if (_runtime != null) _runtime.enabled = _enabled;
        }

        [UnityTest]
        public IEnumerator Released_map_detaches_views_and_caches_and_can_be_loaded_again()
        {
            var sprite = (Sprite)Call("ApprovedMapSprite", "hydra-legacy-0");
            Assert.That(sprite, Is.Not.Null);
            var city = (Sprite)Call("ApprovedMapSprite", "city-0");
            var views = (SpriteRenderer[])Get("_enemyViews");
            views[0].sprite = sprite; views[1].sprite = city;
            ((SpriteRenderer[])Get("_deathGhostViews"))[0].sprite = sprite;
            ((SpriteRenderer)Get("_backdropView")).sprite = sprite;
            ((Sprite[])Get("_approvedHydraLegacySprites"))[0] = sprite;
            var states = (Array)Get("_approvedEnemies");
            var state = states.GetValue(0);
            Put(state, "Frames", new[] { sprite }); Put(state, "Shield", 24f); Put(state, "SpriteId", "hydra-test");
            states.SetValue(state, 0);
            Call("ReconcileArenaResidency", ArenaResidencyPlanner.Steady(Key(ArenaId.NullCity)));
            Assert.That(Residency.Count, Is.EqualTo(1));
            Assert.That(Residency.Status(Key(ArenaId.Hydra)), Is.EqualTo(ArenaPackageLoadStatus.Missing));
            Assert.That(views[0].sprite, Is.Null);
            Assert.That(views[1].sprite, Is.SameAs(city), "retained packages keep their consumers");
            Assert.That(((SpriteRenderer[])Get("_deathGhostViews"))[0].sprite, Is.Null);
            Assert.That(((SpriteRenderer)Get("_backdropView")).sprite, Is.Null);
            Assert.That(((Sprite[])Get("_approvedHydraLegacySprites"))[0], Is.Null);
            Assert.That(Field(states.GetValue(0), "Frames"), Is.Null);
            Assert.That(Field(states.GetValue(0), "Shield"), Is.EqualTo(24f), "asset cleanup cannot reset combat state");
            Assert.That(Call("ApprovedMapSprite", "hydra-legacy-0"), Is.Null, "no Resources fallback may retain a released family");
            Call("ReconcileArenaResidency", ArenaResidencyPlanner.Steady(Key(ArenaId.Hydra)));
            yield return WaitFor(ArenaId.Hydra);
            Assert.That(Call("ApprovedMapSprite", "hydra-legacy-0"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Enemy_render_retries_when_its_asynchronous_art_arrives()
        {
            Call("DestroyEnemiesForVoidTransition");
            typeof(VoidFallGameRuntime).GetField("_arenaId", Flags).SetValue(_runtime, ArenaId.MonochromeCourt);
            Call("ReconcileArenaResidency", ArenaResidencyPlanner.Steady(Key(ArenaId.Void)));
            Assert.That(Call("SpawnEnemy", "court-armored-knight-iii", (Vector2?)Vector2.zero), Is.EqualTo(true));
            var sim = Get("_gameSim");
            var enemies = (Array)Field(sim, "Enemies");
            var slot = 0;
            while (!(bool)Field(enemies.GetValue(slot), "Active")) slot++;
            var enemy = enemies.GetValue(slot);
            Assert.That(Call("TryRenderApprovedEnemy", slot, enemy), Is.EqualTo(false));
            var states = (Array)Get("_approvedEnemies");
            Assert.That(Field(states.GetValue(slot), "Frames"), Is.Null, "loading must not allocate null frame arrays each frame");
            Call("ReconcileArenaResidency", ArenaResidencyPlanner.Steady(Key(ArenaId.MonochromeCourt)));
            yield return WaitFor(ArenaId.MonochromeCourt);
            Assert.That(Call("TryRenderApprovedEnemy", slot, enemy), Is.EqualTo(true));
            Assert.That(((SpriteRenderer[])Get("_enemyViews"))[slot].sprite.name, Does.Contain("armored-knight-3"));
        }

        private IEnumerator WaitFor(ArenaId arena)
        {
            var deadline = Time.realtimeSinceStartup + 30f;
            while (Residency.Status(Key(arena)) == ArenaPackageLoadStatus.Loading && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Residency.Status(Key(arena)), Is.EqualTo(ArenaPackageLoadStatus.Ready), Residency.LastFailure);
        }
        private ArenaPackageKey Key(ArenaId arena) => (ArenaPackageKey)Call("ArenaPackageFor", arena);
        private object Get(string name) => Field(_runtime, name);
        private static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Put(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private object Call(string name, params object[] args) => RuntimeTestReflection.Invoke(_runtime, name, args);
    }
}
