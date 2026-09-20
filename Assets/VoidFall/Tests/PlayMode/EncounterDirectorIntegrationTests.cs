using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoidFall.Runtime;
using VoidFall.Core;

namespace VoidFall.Tests.PlayMode
{
    public sealed class EncounterDirectorIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private VoidFallGameRuntime _runtime;
        private SimulationProfileScope _profile;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runtime = UnityEngine.Object.FindAnyObjectByType<VoidFallGameRuntime>();
            Assert.That(_runtime, Is.Not.Null);yield return null;
            _profile = new SimulationProfileScope(_runtime);
            Invoke("StartRunInternal", true, false);
        }

        [TearDown] public void TearDown() => _profile?.Dispose();

        [Test]
        public void New_run_has_a_real_empty_opening_and_one_times_pressure()
        {
            Assert.That(_runtime.ActiveEnemiesCount, Is.Zero);
            Assert.That(_runtime.PressureHundredths, Is.EqualTo(100));
            for (var i = 0; i < 60; i++) Invoke("Simulate", 1.0 / 60.0);
            Assert.That(_runtime.ActiveEnemiesCount, Is.Zero);
            Assert.That(_runtime.PressureHundredths, Is.EqualTo(100));
            for (var i = 0; i < 100; i++) Invoke("Simulate", 1.0 / 60.0);
            Assert.That(_runtime.ActiveEnemiesCount, Is.GreaterThan(0));
        }

        [Test]
        public void Blocked_population_discards_timer_debt_and_does_not_refill_a_clear_next_tick()
        {
            Set(_runtime, "_time", 10f);
            for (var i = 0; i < 760; i++) Invoke("SpawnEnemy", "chaser");
            Assert.That(_runtime.ActiveEnemiesCount, Is.EqualTo(750));
            Set(_runtime,"_spawnTimer",-30f);Invoke("UpdateSpawns",.5f);
            Assert.That((float)Get(_runtime,"_spawnTimer"),Is.GreaterThanOrEqualTo(0));
            RemoveEnemies(30);var count=_runtime.ActiveEnemiesCount;
            Invoke("UpdateSpawns",1f/60);
            Assert.That(_runtime.ActiveEnemiesCount,Is.EqualTo(count));
        }

        [Test]
        public void Crossing_pack_keeps_its_warned_axis_after_player_moves()
        {
            Set(_runtime, "_runDirectorProfile", DirectorProfileId.Veteran);
            Set(_runtime,"_time",10f);_runtime.ForceEncounterForDiagnostics("crossing");
            Invoke("UpdateSpawns",2.5f);Invoke("UpdateEnemies",.1f);
            var enemies=(Array)Get(Get(_runtime,"_gameSim"),"Enemies");
            var slot=FirstActive(enemies);var first=enemies.GetValue(slot);
            var direction=(Vector2)Get(first,"Velocity");
            var game=Get(_runtime,"_gameSim");var player=Get(game,"Player");
            Set(player,"Position",new Vector2(500,500));Set(game,"Player",player);
            Invoke("UpdateEnemies",.1f);
            Assert.That((Vector2)Get(enemies.GetValue(slot),"Velocity"),Is.EqualTo(direction));
            Assert.That(direction.magnitude,Is.EqualTo(150).Within(.01));
        }

        [Test]
        public void Ending_deployment_does_not_start_recovery_while_pack_is_alive()
        {
            Set(_runtime, "_runDirectorProfile", DirectorProfileId.Veteran);
            Set(_runtime,"_time",10f);_runtime.ForceEncounterForDiagnostics("crossing");
            Invoke("UpdateSpawns",2.5f);Invoke("UpdateSpawns",.45f);
            Assert.That(_runtime.ActiveEnemiesCount,Is.GreaterThan(0));
            Assert.That(_runtime.CurrentEncounterPhase,Is.EqualTo("ActiveThreat"));
        }

        [Test]
        public void Legacy_director_boss_has_two_finite_reinforcement_waves_then_stays_open()
        {
            Set(_runtime, "_runDirectorProfile", DirectorProfileId.Veteran);
            Set(_runtime,"_time",300f);Invoke("SpawnBoss","herald",1d,1d,0);
            Invoke("UpdateSpawns",.1f);
            Set(_runtime,"_time",309f);Invoke("UpdateSpawns",.1f);
            Assert.That(_runtime.ActiveEnemiesCount,Is.EqualTo(8));RemoveEnemies(1000);
            Set(_runtime,"_time",329f);Invoke("UpdateSpawns",.1f);
            Assert.That(_runtime.ActiveEnemiesCount,Is.EqualTo(8));RemoveEnemies(1000);
            for(var i=0;i<5;i++){Set(_runtime,"_time",360f+i*30);Invoke("UpdateSpawns",.1f);}
            Assert.That(_runtime.ActiveEnemiesCount,Is.Zero);
        }

        private void RemoveEnemies(int count)
        {
            var array=(Array)Get(Get(_runtime,"_gameSim"),"Enemies");
            for(var i=0;i<array.Length&&count>0;i++){
                var enemy=array.GetValue(i);if(!(bool)Get(enemy,"Active"))continue;
                Set(enemy,"Active",false);array.SetValue(enemy,i);Invoke("RemoveEnemyOrder",i);count--;
            }
        }
        private static int FirstActive(Array array){for(var i=0;i<array.Length;i++)if((bool)Get(array.GetValue(i),"Active"))return i;Assert.Fail("No deployed actor.");return -1;}
        private static object Get(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        private object Invoke(string name,params object[] args){foreach(var method in _runtime.GetType().GetMethods(Flags))if(method.Name==name&&method.GetParameters().Length==args.Length)return method.Invoke(_runtime,args);throw new MissingMethodException(name);}
    }
}
