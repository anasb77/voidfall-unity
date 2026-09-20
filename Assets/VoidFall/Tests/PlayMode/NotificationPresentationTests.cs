using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class NotificationPresentationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private SimulationProfileScope _profile;
        private bool _enabled;
        [UnitySetUp] public IEnumerator SetUp()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            _enabled = _runtime.enabled; _runtime.enabled = false;
            _profile = new SimulationProfileScope(_runtime);
            Call("StartRunInternal", true, false);
            yield return null;
        }
        [TearDown] public void TearDown() { _profile.Dispose(); _runtime.enabled = _enabled; }

        [TestCase("raid", "DESTROYER RAID INCOMING")]
        [TestCase("eclipse", "ECLIPSE INCOMING")]
        [TestCase("black-hole", "BLACK HOLE")]
        public void Incident_notice_survives_rewards_and_hidden_time(string incident, string title)
        {
            Call("ClearToasts"); _runtime.ForceMajorIncidentForDiagnostics(incident);
            for (var i = 0; i < 6; i++) Call("ShowArenaToast", "reward " + i, 2f);
            var states = (Array)Get(_runtime, "_toastStates");
            Assert.That(states.Cast<object>().Count(t => (string)Get(t, "Text") == title), Is.EqualTo(1));
            Set(_runtime, "_paused", true); Call("UpdateToastTimers", 10f);
            Assert.That(states.Cast<object>().Any(t => (string)Get(t, "Text") == title && (float)Get(t, "Remaining") == 8f), Is.True);
            Set(_runtime, "_paused", false); Call("UpdateToastTimers", .5f); Call("UpdateToastViews");
            var views = (Text[])Get(_runtime, "_toastViews");
            var view = views.Single(t => t.enabled && t.text == title);
            Assert.That(view.font.name, Is.EqualTo("ChakraPetch-Bold"));
            Assert.That(view.color.a, Is.GreaterThan(.5));
            Call("UpdateToastTimers", 5f); Call("UpdateToastViews");
            Assert.That(view.enabled && view.color.a > .9f, Is.True, "Event remains fully readable after five seconds");
        }

        [Test] public void Raid_admits_eight_and_keeps_all_five_roles()
        {
            Call("StartDestroyerRaid", Vector2.zero);
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            var ids = enemies.Cast<object>().Where(e => (bool)Get(e, "Active")).Select(e => (string)Get(e, "Id")).ToArray();
            Assert.That(ids.Length, Is.EqualTo(8));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(5));
            foreach (var id in new[] { "destroyer-maw", "destroyer-razor", "destroyer-spite" })
                Assert.That(ids.Count(x => x == id), Is.EqualTo(2));
        }

        [TestCase("gunner", 0f)] [TestCase("gunner", 12000f)]
        [TestCase("dasher", 0f)] [TestCase("dasher", 12000f)]
        public void Ranged_and_dash_preview_geometry_stays_near_its_owner(string id, float offset)
        {
            Call("SpawnEnemy", id);
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            var enemy = enemies.GetValue(0); var origin = new Vector2(offset, offset);
            Set(enemy, "Position", origin); Set(enemy, "Roster", EnemyRoster.Two);
            Set(enemy, "State", 1); Set(enemy, "DashDirection", Vector2.right); enemies.SetValue(enemy, 0);
            Call("RenderEnemyTelegraphs");
            var lines = (LineRenderer[])Get(_runtime, "_rosterBlastWarnings");
            Assert.That(lines.Count(l => l != null && l.enabled), Is.GreaterThan(0));
            foreach (var line in lines.Where(l => l != null && l.enabled))
            {
                Assert.That(Vector3.Distance(line.GetPosition(0), origin), Is.LessThan(1));
                Assert.That(Vector3.Distance(line.GetPosition(1), origin), Is.InRange(200, 250));
                var mesh = new Mesh();
                try
                {
                    line.BakeMesh(mesh, (Camera)Get(_runtime, "_camera"), false);
                    Assert.That(mesh.bounds.size.magnitude, Is.LessThan(280), "Baked geometry must not produce a screen-crossing spike");
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
        }

        [TestCase("gunner")] [TestCase("dasher")] [TestCase("twinGunner")] [TestCase("mortar")]
        public void Shared_roster_never_draws_a_map_enemy_warning_to_world_origin(string id)
        {
            Call("SpawnEnemy", id);
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            var enemy = enemies.GetValue(0);
            Set(enemy, "Position", new Vector2(12000, 8000)); Set(enemy, "State", 1); Set(enemy, "Age", 5f);
            Set(enemy, "DashDirection", Vector2.right); enemies.SetValue(enemy, 0);
            Call("Render");
            var warnings = (LineRenderer[])Get(_runtime, "_approvedWarnings");
            Assert.That(warnings.All(line => line == null || !line.enabled), Is.True,
                "Map-specific warnings must not render for a shared enemy with an unset map target");
        }

        [Test] public void Map_enemy_keeps_its_committed_impact_preview()
        {
            Assert.That(Call("SpawnEnemy", "null-grav-loom"), Is.True);
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            var enemy = enemies.GetValue(0); Set(enemy, "State", 1); Set(enemy, "Age", 5f); enemies.SetValue(enemy, 0);
            Call("Render");
            var states = (Array)Get(_runtime, "_approvedEnemies"); var state = states.GetValue(0);
            var target = new Vector2(220, 160); Set(state, "Target", target); states.SetValue(state, 0);
            Call("Render");
            var warning = ((LineRenderer[])Get(_runtime, "_approvedWarnings"))[0];
            Assert.That(warning.enabled, Is.True);
            for (var n = 0; n < warning.positionCount; n++)
                Assert.That(Vector2.Distance(warning.GetPosition(n), target), Is.EqualTo(50).Within(.01f));
        }

        [Test] public void Duplicate_raider_roles_damage_each_target_once_per_dash()
        {
            Call("StartDestroyerRaid", Vector2.zero); Call("SpawnEnemy", "chaser");
            var enemies = (Array)Get(Get(_runtime, "_gameSim"), "Enemies");
            var slots = Enumerable.Range(0, enemies.Length).Where(i => (bool)Get(enemies.GetValue(i), "Active")).ToArray();
            var targetSlot = slots.Single(i => (string)Get(enemies.GetValue(i), "Id") == "chaser");
            var target = enemies.GetValue(targetSlot);
            Set(target, "Position", Vector2.zero); Set(target, "Health", 10000f); Set(target, "MaxHealth", 10000f);
            enemies.SetValue(target, targetSlot);
            var maws = slots.Where(i => (string)Get(enemies.GetValue(i), "Id") == "destroyer-maw").ToArray();
            var actors = (Array)Get(_runtime, "_destroyers");
            var expected = 10000f;
            foreach (var slot in maws)
            {
                var enemy = enemies.GetValue(slot); Set(enemy, "Position", Vector2.zero); enemies.SetValue(enemy, slot);
                var actor = actors.GetValue(slot); Set(actor, "SweepThisStep", true); Set(actor, "AttackSerial", slot + 100);
                actors.SetValue(actor, slot); expected -= (float)Get(enemy, "Damage");
            }
            for (var frame = 0; frame < 2; frame++)
                foreach (var slot in maws) Call("ResolveDestroyerSweep", enemies.GetValue(slot), Vector2.left * 10);
            Assert.That((float)Get(enemies.GetValue(targetSlot), "Health"), Is.EqualTo(expected).Within(.01f));
        }

        [Test] public void Incident_cues_are_cached_distinct_non_silent_clips()
        {
            var clips = (AudioClip[])Get(Get(_runtime, "_audio"), "_clips");
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var cue in new[] { ProceduralAudio.Cue.RaidNotice, ProceduralAudio.Cue.EclipseNotice, ProceduralAudio.Cue.BlackHoleNotice })
            {
                var clip = clips[(int)cue]; Assert.That(clip, Is.Not.Null); Assert.That(names.Add(clip.name), Is.True);
                var samples = new float[clip.samples * clip.channels]; Assert.That(clip.GetData(samples, 0), Is.True);
                Assert.That(samples.Max(v => Mathf.Abs(v)), Is.InRange(.01f, 1f));
            }
        }
        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private object Call(string name, params object[] args) => RuntimeTestReflection.Invoke(_runtime, name, args);
    }
}
