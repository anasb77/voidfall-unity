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
    public sealed class CourtFieldIntegrationTests
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
            _oldEnabled = _runtime.enabled; _runtime.enabled = false;
            _oldStore = Get(_runtime, "_saveStore"); _oldProfile = Get(_runtime, "_saveData");
            _directory = Path.Combine(Path.GetTempPath(), "voidfall-court-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Set(_runtime, "_saveStore", new SaveStore(Path.Combine(_directory, "profile.json")));
            Set(_runtime, "_saveData", SaveStore.CreateDefault());
            Set(_runtime, "_runExportDirectoryOverride", Path.Combine(_directory, "RunExports"));
            Call("StartRunInternal", true, false);
            Set(_runtime, "_arenaId", ArenaId.MonochromeCourt);
            Call("EnsureCourtField");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null)
            {
                Call("FinishRunExport", "test_finished");
                Set(_runtime, "_runExportDirectoryOverride", null);
                Set(_runtime, "_saveStore", _oldStore); Set(_runtime, "_saveData", _oldProfile);
                Set(_runtime, "_runSaved", true); Call("EnterMainMenu"); _runtime.enabled = _oldEnabled;
            }
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            yield return null;
        }

        [Test]
        public void Fixed_board_and_spaced_sentinels_survive_boss_transition()
        {
            var origin = (Vector2)Get(_runtime, "_monochromeBoardOrigin");
            var rooks = ((Array)Get(_runtime, "_courtRooks")).Cast<object>().Where(r => r != null).ToArray();
            Assert.That(rooks.Length, Is.GreaterThanOrEqualTo(8));
            for (var i = 0; i < rooks.Length; i++)
            for (var j = i + 1; j < rooks.Length; j++)
                Assert.That(Vector2.Distance((Vector2)Get(rooks[i], "Position"), (Vector2)Get(rooks[j], "Position")), Is.GreaterThanOrEqualTo(630f));
            var enemies = Enemies;
            var slot = Enumerable.Range(0, enemies.Length).First(i => Sentinel(enemies.GetValue(i)));
            var enemy = enemies.GetValue(slot);
            var identity = (int)Get(enemy, "SpawnId");
            var health = (float)Get(enemy, "Health");
            Assert.That(health, Is.InRange(100000f,150000f));
            Set(enemy,"Health", health - 321f); enemies.SetValue(enemy,slot);
            Call("BeginMonochromeBossEncounter");
            Assert.That((Vector2)Get(_runtime,"_monochromeBoardOrigin"), Is.EqualTo(origin));
            Assert.That((int)Get(enemies.GetValue(slot),"SpawnId"), Is.EqualTo(identity));
            Assert.That((float)Get(enemies.GetValue(slot),"Health"), Is.EqualTo(health-321f));
            Assert.That((Vector2)Get(_runtime,"_monochromeBoardTileSize"), Is.EqualTo(Vector2.one*129.6f));
        }

        [Test]
        public void Sacrifice_spawns_five_or_six_regulars_once_and_exports_location()
        {
            var enemies = Enemies;
            var slot = Enumerable.Range(0,enemies.Length).First(i=>Sentinel(enemies.GetValue(i)));
            var enemy = enemies.GetValue(slot);
            var identity = (int)Get(enemy,"SpawnId");
            var position = (Vector2)Get(enemy,"Position");
            Call("ApplyEnemyDamage",slot,1000000f);
            Call("OnCourtSentinelKilled",enemy);
            Call("DrainCourtSacrifices");
            Call("DrainCourtSacrifices");
            var children = enemies.Cast<object>().Where(e=>(bool)Get(e,"Active") && (string)Get(e,"Id")=="chaser").ToArray();
            Assert.That(children.Length, Is.InRange(5,6));
            Assert.That(children.All(e=>(EnemyRoster)Get(e,"Roster")==EnemyRoster.One), Is.True);
            Call("FinishRunExport","quit");
            var events = History;
            var sacrifice = events.Single(e=>e.kind=="court_rook_sacrificed" && e.instanceId==identity);
            Assert.That(sacrifice.x,Is.EqualTo(position.x)); Assert.That(sacrifice.y,Is.EqualTo(position.y));
            Assert.That(sacrifice.amount,Is.EqualTo(children.Length));
            Assert.That(events.Any(e=>e.kind=="court_board_created"),Is.True);
        }

        [Test]
        public void Floor_scope_is_snapshotted_arms_for_two_seconds_and_bursts_once()
        {
            Call("BeginMonochromeBossEncounter");
            Set(_runtime,"_monochromeBossElapsed",0f); Call("StepMonochromeBossEncounter",0f);
            var scope = ((int[])Get(_runtime,"_courtArmingOrder")).ToArray();
            Assert.That(scope.Count(i=>i>0), Is.InRange(1,75));
            var game=Get(_runtime,"_gameSim"); var player=Get(game,"Player");
            Set(player,"Position",new Vector2(10000f,10000f)); Set(game,"Player",player);
            Set(_runtime,"_monochromeBossElapsed",1f); Call("StepMonochromeBossEncounter",0f);
            Assert.That((int[])Get(_runtime,"_courtArmingOrder"),Is.EqualTo(scope));
            Assert.That((int)Get(_runtime,"_courtArmedCount"),Is.EqualTo(scope.Count(i=>i>0)/2));
            Set(_runtime,"_monochromeBossElapsed",2f); Call("StepMonochromeBossEncounter",0f);
            Assert.That((int)Get(_runtime,"_courtArmedCount"),Is.EqualTo(scope.Count(i=>i>0)));
            Set(_runtime,"_monochromeBossElapsed",3.4f); Call("StepMonochromeBossEncounter",0f); Call("StepMonochromeBossEncounter",0f);
            Call("FinishRunExport","quit");
            Assert.That(History.Count(e=>e.kind=="court_floor_burst"),Is.EqualTo(1));
            Assert.That(History.Single(e=>e.kind=="court_floor_scope").options.Length,Is.EqualTo(scope.Count(i=>i>0)));
            Assert.That(History.Count(e=>e.kind=="court_floor_arming"),Is.EqualTo(1));
        }

        [Test]
        public void Burst_only_hits_warned_cells_across_player_enemies_and_shared_boss_pool()
        {
            Call("BeginMonochromeBossEncounter");
            var game = Get(_runtime,"_gameSim");
            var origin = (Vector2)Get(_runtime,"_monochromeBoardOrigin");
            var danger = origin + Vector2.one * (14.5f * 129.6f);
            var safe = danger + Vector2.right * 129.6f;
            var player = Get(game,"Player"); Set(player,"Position",danger); Set(player,"Health",1000f);
            Set(player,"MaxHealth",1000f); Set(player,"Iframes",0f); Set(game,"Player",player);
            var bosses = (Array)Get(game,"Bosses");
            for (var i=0;i<bosses.Length;i++)
            {
                var boss = bosses.GetValue(i); if (!(bool)Get(boss,"Active")) continue;
                Set(boss,"Position",danger); Set(boss,"State",0); bosses.SetValue(boss,i);
            }
            var enemies=Enemies;
            Assert.That(Call("SpawnEnemy","chaser"),Is.True);
            var firstId=(int)Get(_runtime,"_nextEnemyId")-1;
            Assert.That(Call("SpawnEnemy","chaser"),Is.True);
            var secondId=(int)Get(_runtime,"_nextEnemyId")-1;
            var first=-1; var second=-1;
            for(var i=0;i<enemies.Length;i++)
            {
                var enemy=enemies.GetValue(i); var id=(int)Get(enemy,"SpawnId");
                if (!(bool)Get(enemy,"Active") || (id!=firstId && id!=secondId)) continue;
                Set(enemy,"Position",id==firstId?danger:safe); Set(enemy,"Health",500f); Set(enemy,"MaxHealth",500f);
                enemies.SetValue(enemy,i); if(id==firstId) first=i; else second=i;
            }
            var bossHealth=(float)Get(_runtime,"_monochromeSharedHealth");
            Set(_runtime,"_monochromeBossElapsed",0f); Call("StepMonochromeBossEncounter",0f);
            Set(_runtime,"_monochromeBossElapsed",3.4f); Call("StepMonochromeBossEncounter",0f);
            Assert.That((float)Get(Get(game,"Player"),"Health"),Is.EqualTo(978f));
            Assert.That((float)Get(enemies.GetValue(first),"Health"),Is.EqualTo(440f));
            Assert.That((float)Get(enemies.GetValue(second),"Health"),Is.EqualTo(500f));
            Assert.That((float)Get(_runtime,"_monochromeSharedHealth"),Is.EqualTo(bossHealth-120f));
            Call("StepMonochromeBossEncounter",0f);
            Assert.That((float)Get(enemies.GetValue(first),"Health"),Is.EqualTo(440f));
        }

        [Test]
        public void Grandmasters_warn_before_firing_one_three_and_five_shot_volley()
        {
            Call("BeginMonochromeBossEncounter");
            var game = Get(_runtime,"_gameSim"); var bosses = (Array)Get(game,"Bosses");
            for (var i=0;i<bosses.Length;i++)
            {
                var boss=bosses.GetValue(i); if (!(bool)Get(boss,"Active")) continue;
                Set(boss,"AttackCooldown",0f); Set(boss,"State",0); bosses.SetValue(boss,i);
            }
            Call("UpdateBosses",0f);
            Assert.That(bosses.Cast<object>().Where(b=>(bool)Get(b,"Active")).All(b=>(int)Get(b,"State")==1),Is.True);
            var shots=(Array)Get(game,"HostileShots");
            Assert.That(shots.Cast<object>().Count(s=>(bool)Get(s,"Active")),Is.Zero);
            Call("UpdateBosses",.5f); Call("UpdateBosses",.5f);
            Assert.That(shots.Cast<object>().Count(s=>(bool)Get(s,"Active")),Is.Zero);
            Call("UpdateBosses",.06f);
            Assert.That(shots.Cast<object>().Count(s=>(bool)Get(s,"Active")),Is.EqualTo(8));
            Call("UpdateBosses",.1f);
            Assert.That(shots.Cast<object>().Count(s=>(bool)Get(s,"Active")),Is.EqualTo(8));
            Call("FinishRunExport","quit");
            Assert.That(History.Count(e=>e.kind=="court_boss_attack_warning"),Is.EqualTo(2));
            Assert.That(History.Where(e=>e.kind=="court_boss_attack_fired").Select(e=>e.amount),Is.EquivalentTo(new[]{3f,5f}));
        }

        [Test]
        public void Court_player_and_spawn_positions_remain_inside_fixed_board()
        {
            var game=Get(_runtime,"_gameSim"); var player=Get(game,"Player");
            var origin=(Vector2)Get(_runtime,"_monochromeBoardOrigin");
            Set(player,"Position",new Vector2(-10000f,10000f)); Set(player,"Velocity",new Vector2(-50f,50f)); Set(game,"Player",player);
            Call("ClampCourtPlayer");
            player=Get(game,"Player"); var position=(Vector2)Get(player,"Position");
            Assert.That(position.x,Is.GreaterThan(origin.x));
            Assert.That(position.y,Is.LessThan(origin.y+28f*129.6f));
            Assert.That((Vector2)Get(player,"Velocity"),Is.EqualTo(Vector2.zero));
            var spawn=(Vector2)Call("CourtSpawnPosition",new Vector2(-10000f,10000f));
            Assert.That(spawn.x,Is.InRange(origin.x+60f,origin.x+28f*129.6f-60f));
            Assert.That(spawn.y,Is.InRange(origin.y+60f,origin.y+28f*129.6f-60f));
            Assert.That(Vector2.Distance(spawn,position),Is.GreaterThanOrEqualTo(300f));
        }

        [Test]
        public void Wide_board_recycling_starts_beyond_the_visible_spawn_ring()
        {
            var half=(Vector2)Call("GameplayViewportHalfExtent");
            var recycle=(float)Call("ApprovedMapEnemyRecycleDistance");
            Assert.That(recycle,Is.GreaterThan(half.magnitude+130f));
            var enemy=Enemies.GetValue(0);
            Set(enemy,"Position",new Vector2(99999,-99999)); Set(enemy,"Radius",12f);
            var args=new object[]{enemy};
            _runtime.GetType().GetMethod("ConstrainCourtEnemy",Flags).Invoke(_runtime,args);
            var origin=(Vector2)Get(_runtime,"_monochromeBoardOrigin");
            var pos=(Vector2)Get(args[0],"Position");
            Assert.That(pos.x,Is.InRange(origin.x+12,origin.x+28f*129.6f-12));
            Assert.That(pos.y,Is.InRange(origin.y+12,origin.y+28f*129.6f-12));
        }

        [Test]
        public void Player_readability_changes_only_render_scale_and_restores_on_abyss()
        {
            var game=Get(_runtime,"_gameSim"); var player=Get(game,"Player");
            var beforePosition=(Vector2)Get(player,"Position");
            Call("UpdateGameplayCameraViewport");
            Call("Render");
            var view=(SpriteRenderer)Get(_runtime,"_playerView");
            var courtScale=view.transform.localScale.x;
            Assert.That((Vector2)Get(Get(game,"Player"),"Position"),Is.EqualTo(beforePosition));
            Set(_runtime,"_arenaId",ArenaId.Void); Call("Render");
            Assert.That(view.transform.localScale.x,Is.LessThanOrEqualTo(courtScale));
            var once=view.transform.localScale; Call("Render");
            Assert.That(view.transform.localScale,Is.EqualTo(once));
        }

        [Test]
        public void Court_prepared_contours_follow_faction_without_reusing_the_wrong_cache_entry()
        {
            var enemy=Enemies.GetValue(0); Set(enemy,"Id","court-pawn"); Set(enemy,"Elite",false);
            Set(enemy,"Seed",1f);
            var white=Call("CachedEnemySpriteAccent",enemy);
            Set(enemy,"Seed",-1f);
            var black=Call("CachedEnemySpriteAccent",enemy);
            Assert.That(white,Is.EqualTo(Color.black));
            Assert.That(black,Is.EqualTo(Color.white));
        }

        private Array Enemies => (Array)Get(Get(_runtime,"_gameSim"),"Enemies");
        private static bool Sentinel(object e) => (bool)Get(e,"Active") && (string)Get(e,"Id")=="court-rook" && (int)Get(e,"State")==90;
        private UnityTelemetryHistoryEvent[] History => File.ReadAllLines(Directory.GetFiles(Path.Combine(_directory,"RunExports"),"*.jsonl").Single()).Select(JsonUtility.FromJson<UnityTelemetryHistoryEvent>).ToArray();
        private static object Get(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        private object Call(string name,params object[] args)
        {
            var method=_runtime.GetType().GetMethods(Flags).Single(m=>m.Name==name && m.GetParameters().Length==args.Length && m.GetParameters().Select((p,i)=>args[i]==null||p.ParameterType.IsInstanceOfType(args[i])).All(x=>x));
            return method.Invoke(_runtime,args);
        }
    }
}
