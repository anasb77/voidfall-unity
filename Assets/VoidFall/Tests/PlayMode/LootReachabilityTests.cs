using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Core;
using VoidFall.Runtime;

namespace VoidFall.Tests.PlayMode
{
    public sealed class LootReachabilityTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private VoidFallGameRuntime _runtime;
        private SimulationProfileScope _profile;
        private bool _enabled;
        private string _directory;
        private Array Pickups => (Array)Get(Get(_runtime, "_gameSim"), "Pickups");
        [UnitySetUp] public IEnumerator SetUp()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            _enabled = _runtime.enabled; _runtime.enabled = false;
            _profile = new SimulationProfileScope(_runtime);
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-loot-" + Guid.NewGuid().ToString("N"));
            Set(_runtime, "_runExportDirectoryOverride", _directory);
            Call("StartRunInternal", true, false);
            yield return null;
        }
        [TearDown] public void TearDown()
        {
            Call("FinishRunExport", "test_finished");
            Set(_runtime, "_runExportDirectoryOverride", null);
            _profile?.Dispose(); _runtime.enabled = _enabled;
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
        private void FillXp()
        {
            for (var i = 0; i < Pickups.Length + 30; i++) Call("SpawnPickup", new Vector2(-9000 + i * 3, 0), 1f);
        }
        private object Kind(string name) => Enum.Parse(_runtime.GetType().GetMethod("SpawnSpecialPickup", Flags).GetParameters()[2].ParameterType, name);
        private bool SpawnSpecial(string kind, Vector2 position, float amount = 1) => (bool)Call("SpawnSpecialPickup", position, amount, Kind(kind));
        private float Total(string kind) => Pickups.Cast<object>().Where(p => (bool)Get(p,"Active") && Get(p,"Kind").ToString()==kind).Sum(p => (float)Get(p,"Value"));

        [Test] public void Fresh_gems_remain_separate_for_two_seconds_then_merge_without_losing_xp()
        {
            for (var i = 0; i < 300; i++) Call("SpawnPickup", new Vector2(300 + i % 12, 0), 1f);
            Assert.That(Pickups.Cast<object>().Count(p => (bool)Get(p,"Active")), Is.EqualTo(300));
            Call("UpdatePickups", 1.9f);
            Assert.That(Pickups.Cast<object>().Count(p => (bool)Get(p,"Active")), Is.EqualTo(300));
            Call("UpdatePickups", .11f);
            Assert.That(Pickups.Cast<object>().Count(p => (bool)Get(p,"Active")), Is.LessThan(300));
            Assert.That(Total("Xp"), Is.EqualTo(300));
            Assert.That((float)Get(_runtime,"_xp"), Is.Zero);
            Call("FinishRunExport", "test_finished");
            var history = File.ReadAllLines(Directory.GetFiles(_directory,"*.jsonl").Single()).Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
            Assert.That(history.Any(e => e.kind == "drop_consolidated" && e.reason == "merge_delay_elapsed"), Is.True);
            Assert.That(history.Any(e => e.kind == "drop_merged"), Is.False);
            var report = JsonUtility.FromJson<UnityTelemetryReport>(File.ReadAllText(Directory.GetFiles(_directory,"*.json").Single()));
            Assert.That(report.context.xpMergeDelaySeconds, Is.EqualTo(2));
            Assert.That(report.context.freshXpPickupSlots, Is.EqualTo(1024));
        }

        [Test] public void A_fresh_gem_can_be_collected_during_the_merge_delay()
        {
            Call("SpawnPickup", Vector2.zero, 1f); Call("UpdatePickups", .016f);
            Assert.That((float)Get(_runtime,"_xp"), Is.GreaterThan(0));
            Assert.That(Total("Xp"), Is.Zero);
        }

        [Test] public void A_full_horde_clear_has_visible_fresh_rewards_and_keeps_special_space()
        {
            for (var i = 0; i < 750; i++) Call("SpawnPickup", new Vector2(300 + i % 40, i % 17), 8f);
            Assert.That(Total("Xp"), Is.EqualTo(6000));
            Assert.That(Pickups.Cast<object>().Count(p => (bool)Get(p,"Active") && (float)Get(p,"MergeDelay") > 0), Is.GreaterThanOrEqualTo(750));
            Assert.That(SpawnSpecial("Repair", Vector2.right * 400), Is.True);
            Call("FinishRunExport", "test_finished");
            var history = File.ReadAllText(Directory.GetFiles(_directory,"*.jsonl").Single());
            Assert.That(history, Does.Not.Contain("fresh_reserve_exhausted"));
        }

        [Test] public void Full_xp_pool_keeps_new_rewards_near_the_kill_without_losing_value()
        {
            FillXp(); var before = Total("Xp");
            Call("SpawnPickup", Vector2.zero, 7f);
            Assert.That(Total("Xp"), Is.EqualTo(before + 7));
            Assert.That(Pickups.Cast<object>().Any(p => (bool)Get(p,"Active") &&
                Get(p,"Kind").ToString()=="Xp" && ((Vector2)Get(p,"Position")).magnitude < 180), Is.True);
            Assert.That((float)Get(_runtime, "_xp"), Is.Zero, "Relocation is not an automatic XP grant.");
        }

        [Test] public void Repairs_and_magnets_can_spawn_when_xp_has_filled_its_budget()
        {
            FillXp(); var before = Total("Xp");
            Assert.That(SpawnSpecial("Repair", Vector2.zero), Is.True);
            Assert.That(SpawnSpecial("Magnet", Vector2.right * 100), Is.True);
            Assert.That(Total("Xp"), Is.EqualTo(before));
            Call("FinishRunExport", "test_finished");
            var history=File.ReadAllLines(Directory.GetFiles(_directory,"*.jsonl").Single()).Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
            Assert.That(history.Any(e=>e.kind=="drop_spawn" && e.id=="repair"), Is.True);
            Assert.That(history.Any(e=>e.kind=="drop_rejected" && (e.id=="repair" || e.id=="magnet")), Is.False);
            var report=JsonUtility.FromJson<UnityTelemetryReport>(File.ReadAllText(Directory.GetFiles(_directory,"*.json").Single()));
            Assert.That(report.context.lootPolicyVersion,Is.EqualTo(3));
            Assert.That(report.context.survivalSeconds,Is.EqualTo(360));
        }

        [Test] public void Full_parts_pool_frees_space_without_destroying_earned_parts()
        {
            for (var i=0;i<Pickups.Length-1;i++) Assert.That(SpawnSpecial("Part",new Vector2(-9000+i,0)),Is.True);
            var before=Total("Part");
            Assert.That(SpawnSpecial("Repair",Vector2.zero),Is.True);
            Assert.That(Total("Part"),Is.EqualTo(before));
        }

        [Test] public void Full_special_pool_preserves_every_charge_and_consumes_each_only_once()
        {
            for (var i=0;i<Pickups.Length-1;i++) Assert.That(SpawnSpecial("Repair",new Vector2(-9000+i,0)),Is.True);
            Assert.That(SpawnSpecial("Magnet",new Vector2(3000,0)),Is.True);
            Assert.That(Total("Repair"),Is.EqualTo(Pickups.Length-1));
            var slot=Enumerable.Range(0,Pickups.Length).First(i=>(bool)Get(Pickups.GetValue(i),"Active") &&
                Get(Pickups.GetValue(i),"Kind").ToString()=="Repair" && (float)Get(Pickups.GetValue(i),"Value")>=2);
            var pickup=Pickups.GetValue(slot); Set(pickup,"Position",Vector2.zero); Set(pickup,"Velocity",Vector2.zero); Pickups.SetValue(pickup,slot);
            var game=Get(_runtime,"_gameSim"); var player=Get(game,"Player"); Set(player,"Health",1f); Set(game,"Player",player);
            Call("UpdatePickups",.016f); Call("UpdatePickups",.016f);
            Assert.That((float)Get(Get(game,"Player"),"Health"),Is.EqualTo(45f).Within(.01));
            Assert.That(Total("Repair"),Is.EqualTo(Pickups.Length-3));
        }

        [Test] public void Distant_loot_returns_within_reach_but_greed_still_requires_collection()
        {
            Call("ActivateWildCard",WildCardId.Greed,false);
            Call("SpawnPickup",new Vector2(9000,0),1f);
            Call("UpdatePickups",.3f);
            var pickup=Pickups.Cast<object>().First(p=>(bool)Get(p,"Active"));
            Assert.That(((Vector2)Get(pickup,"Position")).magnitude,Is.LessThan(1500));
            Assert.That((float)Get(_runtime,"_xp"),Is.Zero);
            var game=Get(_runtime,"_gameSim"); var player=Get(game,"Player"); Set(player,"Position",Get(pickup,"Position")); Set(game,"Player",player);
            Call("UpdatePickups",.016f);
            Assert.That((float)Get(_runtime,"_xp"),Is.GreaterThan(0));
            Assert.That(Total("Xp"),Is.Zero);
        }
        private static object Get(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);

        [Test] public void Greed_cancels_existing_magnetic_pull_before_reachability_recovery()
        {
            Call("SpawnPickup",new Vector2(500,0),1f);
            var pickup=Pickups.GetValue(0); Set(pickup,"Pull",true); Set(pickup,"Speed",950f);
            Set(pickup,"Velocity",new Vector2(-950,0)); Pickups.SetValue(pickup,0);
            Call("ActivateWildCard",WildCardId.Greed,false); Call("UpdatePickups",.016f);
            Assert.That(Get(Pickups.GetValue(0),"Pull"),Is.False);
            Assert.That((float)Get(_runtime,"_xp"),Is.Zero);
        }

        [Test] public void City_loot_spawns_inside_scaled_bounds_at_a_nonzero_origin()
        {
            Set(_runtime,"_arenaId",ArenaId.NullCity);
            Set(_runtime,"_nullCityOrigin",new Vector2(2100,-700));
            Call("SpawnPickup",new Vector2(-9000,9000),1f);
            SpawnSpecial("Repair",new Vector2(9000,-9000));
            foreach(var pickup in Pickups)
            {
                if(!(bool)Get(pickup,"Active"))continue;
                var position=(Vector2)Call("NullCityCanvas",(Vector2)Get(pickup,"Position"));
                Assert.That(position.x,Is.InRange((float)NullCityRules.ArenaLeft,(float)NullCityRules.ArenaRight));
                Assert.That(position.y,Is.InRange((float)NullCityRules.ArenaTop,(float)NullCityRules.ArenaBottom));
            }
        }

        [Test] public void Court_boundary_recovery_keeps_currency_without_auto_collecting()
        {
            Set(_runtime,"_arenaId",ArenaId.MonochromeCourt); Call("EnsureCourtField");
            Call("SpawnPickup",Vector2.zero,1f);
            var pickup=Pickups.GetValue(0); Set(pickup,"Position",new Vector2(99999,99999)); Pickups.SetValue(pickup,0);
            Set(_runtime,"_lootRecoveryTimer",0f); Call("UpdateLootReachability",.25f);
            var point=(Vector2)Get(Pickups.GetValue(0),"Position");
            var origin=(Vector2)Get(_runtime,"_monochromeBoardOrigin");
            Assert.That(point.x,Is.LessThanOrEqualTo(origin.x+28*129.6f-32));
            Assert.That((float)Get(Pickups.GetValue(0),"Value"),Is.EqualTo(1));
            Assert.That((float)Get(_runtime,"_xp"),Is.Zero);
        }

        [Test] public void Hydra_boss_recovered_loot_stays_inside_the_original_rib_boundary()
        {
            Set(_runtime,"_arenaId",ArenaId.Hydra); Set(_runtime,"_hydraBossEncounterActive",true);
            var center=new Vector2(300,500); Set(_runtime,"_hydraArenaCentre",center);
            var position=(Vector2)Call("ConstrainLootPosition",new Vector2(99999,99999));
            var delta=position-center;
            Assert.That(delta.x*delta.x/(468f*468f)+delta.y*delta.y/(298f*298f),Is.LessThanOrEqualTo(1.0001f));
        }

        [Test] public void Bomb_collection_and_pool_compaction_do_not_collect_newborn_rewards_in_the_same_tick()
        {
            Call("UpdatePickups",0f);
            var original=(Action<int,int,bool>)Get(_runtime,"_pickupCollectedHook");
            for (var i=0;i<Pickups.Length-1;i++) SpawnSpecial("Part",new Vector2(9000+i,0));
            Assert.That(SpawnSpecial("Bomb",Vector2.zero,2),Is.True);
            var identities=(int[])Get(_runtime,"_telemetryPickupIds");
            var initial=new HashSet<int>(Enumerable.Range(0,Pickups.Length)
                .Where(i=>(bool)Get(Pickups.GetValue(i),"Active")).Select(i=>identities[i]));
            Set(_runtime,"_pickupCollectedHook",(Action<int,int,bool>)((slot,order,pulled)=>
            {
                var bomb=Get(Pickups.GetValue(slot),"Kind").ToString()=="Bomb";
                original(slot,order,pulled);
                if (bomb) { SpawnSpecial("Repair",Vector2.zero); SpawnSpecial("Repair",Vector2.zero); }
            }));
            try
            {
                Call("UpdatePickups",.016f);
                Call("FinishRunExport","test_finished");
                var collected=File.ReadAllLines(Directory.GetFiles(_directory,"*.jsonl").Single())
                    .Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).Where(e=>e.kind=="drop_collected").ToArray();
                Assert.That(collected,Has.Length.EqualTo(1));
                Assert.That(collected.All(e=>initial.Contains(e.instanceId)),Is.True);
            }
            finally { Set(_runtime,"_pickupCollectedHook",original); }
        }
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        private object Call(string name,params object[] args)=>_runtime.GetType().GetMethods(Flags)
            .Single(m=>m.Name==name && m.GetParameters().Length==args.Length).Invoke(_runtime,args);
    }
}
