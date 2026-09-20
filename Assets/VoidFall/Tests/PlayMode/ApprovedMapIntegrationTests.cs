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
    public sealed class ApprovedMapIntegrationTests
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
        public void Hives_spawn_mixed_broods_and_one_guardian_after_destruction()
        {
            Enter("hydra",ArenaId.Hydra);Call("StepApprovedHydra",0f);
            Assert.That(Active().Count(i=>Id(i)=="hydra-hive"),Is.EqualTo(3));
            Call("StepApprovedHydra",3f);
            Assert.That(Active().Count(i=>ApprovedMapContent.InsectKind(Id(i))>=0),Is.EqualTo(6));
            Assert.That(Active().Length,Is.EqualTo(18));
            var hive=Active().First(i=>Id(i)=="hydra-hive");Call("KillEnemy",hive);Call("StepApprovedHydra",.01f);
            Assert.That(Active().Count(i=>Id(i)=="hydra-mantis-matriarch"||Id(i)=="hydra-iron-carapace"),Is.EqualTo(1));
            Call("StepApprovedHydra",3f);
            Assert.That(Active().Length,Is.EqualTo(28));
            Call("FinishRunExport","test_finished");
            var history=File.ReadAllText(Directory.GetFiles(Path.Combine(_directory,"RunExports"),"*.jsonl").Single());
            StringAssert.Contains("hydra_hive_destroyed",history);StringAssert.Contains("hydra_hive_child",history);
        }
        [Test]
        public void Sentinel_shields_are_nonstacking_and_disappear_when_owner_dies()
        {
            Enter("monochrome-court",ArenaId.MonochromeCourt);Call("EnsureCourtField");
            var rooks=(Array)Get(_runtime,"_courtRooks");var rook=rooks.Cast<object>().First(r=>r!=null&&!(bool)Get(r,"Fallen"));
            var position=(Vector2)Get(rook,"Position");Assert.That(Call("SpawnEnemy","court-pawn",(Vector2?)position),Is.True);
            var slot=Active().Last(i=>Id(i)=="court-pawn");Call("StepApprovedCourt",.01f);
            var sidecars=(Array)Get(_runtime,"_approvedEnemies");Assert.That((float)Get(sidecars.GetValue(slot),"Shield"),Is.EqualTo(24));
            Call("StepApprovedCourt",.01f);Assert.That((float)Get(sidecars.GetValue(slot),"Shield"),Is.EqualTo(24));
            var sentinel=Active().First(i=>(int)Get(Enemies.GetValue(i),"SpawnId")== (int)Get(rook,"SpawnId"));
            Call("KillEnemy",sentinel);Call("StepApprovedCourt",.01f);
            Assert.That((float)Get(sidecars.GetValue(slot),"Shield"),Is.Zero);
        }
        [Test]
        public void Armored_horse_has_approved_sprite_and_only_pursues()
        {
            Enter("monochrome-court",ArenaId.MonochromeCourt);Call("EnsureCourtField");
            Assert.That(Call("SpawnEnemy","court-armored-knight-iii",(Vector2?)Vector2.zero),Is.True);
            var slot=Active().Last(i=>Id(i)=="court-armored-knight-iii");var e=Enemies.GetValue(slot);
            var args=new object[]{e,.1f,500f,Vector2.right};CallArgs("TryUpdateApprovedEnemy",args);Enemies.SetValue(args[0],slot);
            Assert.That((int)Get(args[0],"State"),Is.Zero);
            Assert.That(((Vector2)Get(args[0],"Velocity")).x,Is.EqualTo((float)Get(args[0],"Speed")));
            Assert.That(Call("TryRenderApprovedEnemy",slot,args[0]),Is.True);
            var views=(SpriteRenderer[])Get(_runtime,"_enemyViews");Assert.That(views[slot].sprite.name,Does.Contain("armored-knight-3"));
        }
        [Test]
        public void Native_camera_matches_approved_slider_values()
        {
            foreach(var id in new[]{"null-city","hydra","monochrome-court"})
            {
                Enter(id,id=="null-city"?ArenaId.NullCity:id=="hydra"?ArenaId.Hydra:ArenaId.MonochromeCourt);
                Assert.That(((Vector2)Call("GameplayViewportHalfExtent")).y*2,Is.EqualTo(ApprovedMapRules.CameraHeight(id)).Within(.01));
            }
        }
        private void Enter(string id,ArenaId arena)
        {
            Call("ClearCombatForJourney");Call("ResetJourney");
            Set(_runtime,"_voidRoute",new VoidRouteRun(new[]{new VoidRouteNode(id,id,0,1,"","","","")},id));
            Set(_runtime,"_arenaId",arena);Call("BeginObjectiveForCurrentArena");
        }
        [Test]
        public void Queen_promotion_retains_native_progression_modifiers()
        {
            Enter("monochrome-court",ArenaId.MonochromeCourt);
            Call("SpawnEnemy","court-pawn",(Vector2?)Vector2.zero);
            Call("SpawnEnemy","court-queen",(Vector2?)(Vector2.right*100));
            var pawnSlot=Active().Single(i=>Id(i)=="court-pawn");var pawn=Enemies.GetValue(pawnSlot);
            Set(pawn,"MaxHealth",60f);Set(pawn,"Health",40f);Set(pawn,"Speed",138f);Enemies.SetValue(pawn,pawnSlot);
            var queen=Enemies.GetValue(Active().Single(i=>Id(i)=="court-queen"));
            Call("PromoteApprovedPawn",queen,pawnSlot,(int)Get(pawn,"SpawnId"),0);
            pawn=Enemies.GetValue(pawnSlot);
            Assert.That((string)Get(pawn,"Id"),Is.EqualTo("court-pawn-ii"));
            Assert.That((float)Get(pawn,"MaxHealth"),Is.EqualTo(99f).Within(.001));
            Assert.That((float)Get(pawn,"Health"),Is.EqualTo(79f).Within(.001));
            Assert.That((float)Get(pawn,"Speed"),Is.EqualTo(150.42f).Within(.001));
        }
        [Test]
        public void Blister_lethal_hit_arms_one_warned_blast_instead_of_silent_death()
        {
            Enter("hydra",ArenaId.Hydra);
            Call("SpawnEnemy","hydra-blisterbeetle",(Vector2?)Vector2.zero);
            var beetle=Active().Single(i=>Id(i)=="hydra-blisterbeetle");
            Call("SpawnEnemy","hydra-needlewasp",(Vector2?)(Vector2.right*20));
            var victim=Active().Single(i=>Id(i)=="hydra-needlewasp");var before=(float)Get(Enemies.GetValue(victim),"Health");
            Call("ApplyEnemyDamage",beetle,100000f,Vector2.zero,0f,false);
            Assert.That((bool)Get(Enemies.GetValue(beetle),"Active"),Is.True);
            var states=(Array)Get(_runtime,"_approvedEnemies");Assert.That((float)Get(states.GetValue(beetle),"Fuse"),Is.EqualTo(.45f));
            var args=new object[]{Enemies.GetValue(beetle),.5f,1000f,Vector2.right};CallArgs("TryUpdateApprovedEnemy",args);Enemies.SetValue(args[0],beetle);
            Assert.That((bool)Get(Enemies.GetValue(beetle),"Active"),Is.False);
            Assert.That((float)Get(Enemies.GetValue(victim),"Health"),Is.EqualTo(before-24).Within(.001));
        }
        [Test]
        public void Full_pool_defers_whole_hive_broods_and_drains_without_silent_loss()
        {
            Enter("hydra",ArenaId.Hydra);Call("StepApprovedHydra",0f);
            var enemyType=Enemies.GetType().GetElementType();
            try
            {
                for(var i=0;i<Enemies.Length;i++)if(!(bool)Get(Enemies.GetValue(i),"Active"))
                {var e=Activator.CreateInstance(enemyType);Set(e,"Active",true);Set(e,"SpawnId",10000+i);Set(e,"Id","chaser");Set(e,"View",i);Enemies.SetValue(e,i);}
                Call("StepApprovedHydra",3f);
                var hives=(Array)Get(_runtime,"_approvedHives");Assert.That(hives.Cast<object>().Sum(h=>(int)Get(h,"Pending")),Is.EqualTo(15));
                for(var i=Enemies.Length-5;i<Enemies.Length;i++)Enemies.SetValue(Activator.CreateInstance(enemyType),i);
                Call("StepApprovedHydra",0f);Assert.That(hives.Cast<object>().Sum(h=>(int)Get(h,"Pending")),Is.EqualTo(10));
            }
            finally
            {for(var i=0;i<Enemies.Length;i++)if((int)Get(Enemies.GetValue(i),"SpawnId")>=10000)Enemies.SetValue(Activator.CreateInstance(enemyType),i);}
        }
        private Array Enemies => (Array)Get(Get(_runtime,"_gameSim"),"Enemies");
        private string Id(int slot)=>(string)Get(Enemies.GetValue(slot),"Id");
        private int[] Active()=>Enumerable.Range(0,Enemies.Length).Where(i=>(bool)Get(Enemies.GetValue(i),"Active")).ToArray();
        private static object Get(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        private object Call(string name,params object[] args)=>CallArgs(name,args);
        private object CallArgs(string name,object[] args)
        {
            var method=_runtime.GetType().GetMethods(Flags).Where(m=>m.Name==name&&m.GetParameters().Length>=args.Length&&m.GetParameters().Skip(args.Length).All(p=>p.IsOptional)&&m.GetParameters().Take(args.Length).Select((p,i)=>args[i]==null||(p.ParameterType.IsByRef?p.ParameterType.GetElementType():p.ParameterType).IsInstanceOfType(args[i])).All(x=>x)).OrderBy(m=>m.GetParameters().Length).First();
            var values=new object[method.GetParameters().Length];Array.Copy(args,values,args.Length);
            for(var i=args.Length;i<values.Length;i++)values[i]=method.GetParameters()[i].DefaultValue;
            var result=method.Invoke(_runtime,values);Array.Copy(values,args,args.Length);return result;
        }
    }
}
