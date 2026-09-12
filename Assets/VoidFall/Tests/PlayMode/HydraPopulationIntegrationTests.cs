using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Persistence;
using VoidFall.Runtime;
using VoidFall.Core;

namespace VoidFall.Tests.PlayMode
{
    public sealed class HydraPopulationIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private object _oldStore, _oldProfile;
        private bool _oldEnabled;
        private string _directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);
            _oldEnabled = _runtime.enabled;
            _runtime.enabled = false;
            _oldStore = Get(_runtime, "_saveStore");
            _oldProfile = Get(_runtime, "_saveData");
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-hydra-population-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Set(_runtime, "_saveStore", new SaveStore(Path.Combine(_directory, "profile.json")));
            Set(_runtime, "_saveData", SaveStore.CreateDefault());
            Set(_runtime, "_runExportDirectoryOverride", Path.Combine(_directory, "RunExports"));
            Call("StartRunInternal", true, false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Call("FinishRunExport", "test_finished");
                Set(_runtime, "_runExportDirectoryOverride", null);
                Set(_runtime, "_saveStore", _oldStore);
                Set(_runtime, "_saveData", _oldProfile);
                Set(_runtime, "_runSaved", true);
                Call("EnterMainMenu");
                _runtime.enabled = _oldEnabled;
            }
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            yield return null;
        }

        [Test]
        public void Split_is_exactly_once_and_fragments_cannot_split_recursively()
        {
            var slot = SpawnSpecimen((int)HydraPopulationKind.Cleft);
            Call("KillEnemy", slot);
            Call("UpdateHydraPopulationBirths");
            var fragments = ActiveSlots().Where(i => (bool)Get(Enemies.GetValue(i), "SplitterFragment")).ToArray();
            Assert.That(fragments, Has.Length.EqualTo(2));
            foreach (var fragment in fragments) Call("KillEnemy", fragment);
            Assert.That(ActiveSlots(), Is.Empty);
            var events = FinishHistory();
            Assert.That(events.Count(e => e.kind == "hydra_population_ability" && e.sourceId == "split" && e.reason == "queued"), Is.EqualTo(1));
            Assert.That(events.Where(e => e.kind == "hydra_population_ability" && e.sourceId == "split" && e.reason == "released").Sum(e => e.amount), Is.EqualTo(2));
        }

        [Test]
        public void Full_pool_split_waits_for_slots_and_preserves_both_children_and_ancestry()
        {
            var parent = SpawnSpecimen((int)HydraPopulationKind.Cleft);
            var parentIdentity = (int)Get(Enemies.GetValue(parent),"SpawnId");
            var actors = (Array)Get(_runtime,"_factionActors");
            var parentRoot = (long)Get(actors.GetValue(parent),"Root");
            Assert.That(parentRoot,Is.Not.EqualTo(0));
            while (ActiveSlots().Length < Enemies.Length) Assert.That(Call("SpawnEnemy","chaser"),Is.True);
            Call("KillEnemy",parent);
            Assert.That((int)Get(_runtime,"_hydraPopulationBirthCount"),Is.EqualTo(2));
            Call("UpdateHydraPopulationBirths");
            Assert.That((int)Get(_runtime,"_hydraPopulationBirthCount"),Is.EqualTo(1));
            var firstChild=ActiveSlots().Single(i=>(bool)Get(Enemies.GetValue(i),"SplitterFragment"));
            Assert.That((long)Get(actors.GetValue(firstChild),"Root"),Is.EqualTo(parentRoot));
            var ordinary=ActiveSlots().First(i=>!(bool)Get(Enemies.GetValue(i),"SplitterFragment"));
            Call("KillEnemy",ordinary);
            Call("UpdateHydraPopulationBirths");
            Call("UpdateHydraPopulationBirths");
            Assert.That((int)Get(_runtime,"_hydraPopulationBirthCount"),Is.Zero);
            var children=ActiveSlots().Where(i=>(bool)Get(Enemies.GetValue(i),"SplitterFragment")).ToArray();
            Assert.That(children,Has.Length.EqualTo(2));
            foreach(var child in children)
            {
                Assert.That((long)Get(actors.GetValue(child),"Root"),Is.EqualTo(parentRoot));
                Call("KillEnemy",child);
            }
            Call("UpdateHydraPopulationBirths");
            Assert.That((int)Get(_runtime,"_hydraPopulationBirthCount"),Is.Zero);
            var released=FinishHistory().Where(e=>e.sourceId=="split" && e.reason=="released" && e.instanceId==parentIdentity).ToArray();
            Assert.That(released,Has.Length.EqualTo(2));
            Assert.That(released.Select(e=>e.relatedInstanceId).Distinct().Count(),Is.EqualTo(2));
        }

        [Test]
        public void Reset_releases_pending_children_without_spawning_them_in_another_arena()
        {
            var parent=SpawnSpecimen((int)HydraPopulationKind.Riftkin);
            Call("KillEnemy",parent);
            Assert.That((int)Get(_runtime,"_hydraPopulationBirthCount"),Is.EqualTo(2));
            Call("ResetHydraPopulation");
            Call("UpdateHydraPopulationBirths");
            Assert.That((int)Get(_runtime,"_hydraPopulationBirthCount"),Is.Zero);
            Assert.That(ActiveSlots(),Is.Empty);
            Assert.That(FinishHistory().Count(e=>e.sourceId=="split" && e.reason=="arena_reset"),Is.EqualTo(2));
        }

        [Test]
        public void Bastion_warns_then_fires_three_real_shots_and_reports_parents()
        {
            var slot = SpawnSpecimen((int)HydraPopulationKind.Bastion);
            Step(slot, 2.1f);
            Assert.That(ActivePoolCount("HostileShots"), Is.Zero);
            Step(slot, .71f);
            Assert.That(ActivePoolCount("HostileShots"), Is.EqualTo(3));
            var events = FinishHistory();
            Assert.That(events.Any(e => e.kind == "hybrid" && e.id == "hydra-bastion" && e.detail == "brute+gunner"), Is.True);
            Assert.That(events.Any(e => e.sourceId == "fan" && e.reason == "fired" && e.amount == 3), Is.True);
        }

        [Test]
        public void Aegis_blocks_the_front_but_exposes_its_back()
        {
            var slot = SpawnSpecimen((int)HydraPopulationKind.Aegis);
            var enemy = Enemies.GetValue(slot); Set(enemy, "Facing", Vector2.right); Enemies.SetValue(enemy,slot);
            Assert.That(Call("HydraPopulationIncomingDamageMultiplier", enemy, Vector2.left), Is.EqualTo(.15f));
            Assert.That(Call("HydraPopulationIncomingDamageMultiplier", enemy, Vector2.right), Is.EqualTo(1f));
        }

        [Test]
        public void Reclaimer_requires_harvested_xp_and_preserves_it_for_restitution()
        {
            var slot = SpawnSpecimen((int)HydraPopulationKind.Reclaimer);
            Step(slot, 2.1f);
            Assert.That((float)Get(Population.GetValue(slot), "Warning"), Is.Zero);
            // One XP is fully harvested before firing; no remaining pickup can increase StoredXp.
            Call("SpawnPickup", Vector2.right * 8f, 1f);
            Step(slot, .01f);
            var stored = (float)Get(Enemies.GetValue(slot), "StoredXp");
            Assert.That(stored, Is.GreaterThan(0));
            Assert.That((float)Get(Population.GetValue(slot), "Warning"), Is.GreaterThan(0));
            Step(slot, 1.11f);
            Assert.That((float)Get(Enemies.GetValue(slot), "StoredXp"), Is.EqualTo(stored));
            Assert.That(FinishHistory().Any(e => e.id == "hydra-reclaimer" && e.reason == "detonated"), Is.True);
        }

        [Test]
        public void Broodsmith_caps_drones_at_two_and_repairs_a_damaged_ally()
        {
            var slot = SpawnSpecimen((int)HydraPopulationKind.Broodsmith);
            var ally = SpawnSpecimen((int)HydraPopulationKind.Cleft);
            var enemy = Enemies.GetValue(ally); var health = (float)Get(enemy,"Health") - 10f;
            Set(enemy,"Health",health); Enemies.SetValue(enemy,ally);
            for(var i=0;i<20;i++) Step(slot,.5f);
            Assert.That((int)Get(Population.GetValue(slot),"Drones"), Is.EqualTo(2));
            Assert.That((float)Get(Enemies.GetValue(ally),"Health"), Is.GreaterThan(health));
            Assert.That(FinishHistory().Count(e => e.id == "hydra-broodsmith" && e.reason == "released"), Is.EqualTo(2));
        }

        [Test]
        public void Graft_regenerates_and_slot_reuse_does_not_inherit_an_old_specimen()
        {
            var slot = SpawnSpecimen((int)HydraPopulationKind.Graft);
            var enemy = Enemies.GetValue(slot); var max = (float)Get(enemy,"MaxHealth");
            Set(enemy,"Health",max*.5f); Enemies.SetValue(enemy,slot);
            Step(slot,1f);
            Assert.That((float)Get(Enemies.GetValue(slot),"Health"), Is.GreaterThan(max*.5f));
            Call("KillEnemy",slot); Call("SpawnEnemy","chaser");
            var ordinary = Enemies.GetValue(ActiveSlots().Single());
            Assert.That(Call("IsHydraPopulation",ordinary), Is.False);
        }

        [TestCase(HydraPopulationKind.Hook)]
        [TestCase(HydraPopulationKind.Aegis)]
        [TestCase(HydraPopulationKind.Riftkin)]
        public void Lunge_specimens_warn_then_commit_to_the_captured_direction(HydraPopulationKind kind)
        {
            var slot=SpawnSpecimen((int)kind);
            Step(slot,2.1f); Step(slot,.71f); Step(slot,.1f);
            Assert.That((float)Get(Population.GetValue(slot),"Dash"),Is.GreaterThan(0));
            Assert.That(((Vector2)Get(Enemies.GetValue(slot),"Velocity")).x,Is.GreaterThan((float)Get(Enemies.GetValue(slot),"Speed")));
        }

        [Test]
        public void Rachis_fires_five_shots_and_bloat_retires_after_its_warned_burst()
        {
            var rachis=SpawnSpecimen((int)HydraPopulationKind.Rachis);
            Step(rachis,2.1f); Step(rachis,.71f);
            Assert.That(ActivePoolCount("HostileShots"),Is.EqualTo(5));
            var bloat=SpawnSpecimen((int)HydraPopulationKind.Bloat);
            Step(bloat,2.1f); Step(bloat,.71f);
            Assert.That((bool)Get(Enemies.GetValue(bloat),"Active"),Is.False);
            Assert.That(FinishHistory().Any(e=>e.id=="hydra-bloat" && e.reason=="detonated"),Is.True);
        }

        private object Game => Get(_runtime,"_gameSim");
        private Array Enemies => (Array)Get(Game,"Enemies");
        private Array Population => (Array)Get(_runtime,"_hydraPopulation");
        private int[] ActiveSlots() => Enumerable.Range(0,Enemies.Length).Where(i => (bool)Get(Enemies.GetValue(i),"Active")).ToArray();
        private int ActivePoolCount(string pool) => ((Array)Get(Game,pool)).Cast<object>().Count(e => (bool)Get(e,"Active"));
        private int SpawnSpecimen(int kind)
        {
            Set(_runtime,"_time",2f);
            Assert.That(Call("SpawnEnemy",HydraPopulationRules.BaseId(kind)),Is.True);
            var slot=ActiveSlots().Last();
            var enemy=Enemies.GetValue(slot); Set(enemy,"Position",Vector2.zero);
            var args=new object[]{slot,enemy,kind}; CallArgs("InitHydraPopulation",args); Enemies.SetValue(args[1],slot);
            return slot;
        }
        private void Step(int slot,float dt)
        {
            var args=new object[]{Enemies.GetValue(slot),dt,100f,Vector2.right,0f};
            CallArgs("TryUpdateHydraPopulation",args); Enemies.SetValue(args[0],slot);
        }
        private UnityTelemetryHistoryEvent[] FinishHistory()
        {
            Call("FinishRunExport","test_finished");
            return File.ReadAllLines(Directory.GetFiles(Path.Combine(_directory,"RunExports"),"*.jsonl").Single())
                .Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
        }
        private static object Get(object target,string name) => target.GetType().GetField(name,Flags).GetValue(target);
        private static void Set(object target,string name,object value) => target.GetType().GetField(name,Flags).SetValue(target,value);
        private object Call(string name,params object[] args) => CallArgs(name,args);
        private object CallArgs(string name,object[] args)
        {
            var method=_runtime.GetType().GetMethods(Flags).Single(m=>m.Name==name && m.GetParameters().Length==args.Length &&
                m.GetParameters().Select((p,i)=>args[i]==null || (p.ParameterType.IsByRef?p.ParameterType.GetElementType():p.ParameterType).IsInstanceOfType(args[i])).All(x=>x));
            return method.Invoke(_runtime,args);
        }
    }
}
