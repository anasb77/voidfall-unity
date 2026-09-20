using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private sealed class ApprovedHive
        {
            public int Identity,Pending,Released,GuardianKind;
            public float Clock=3;
            public bool Dead,GuardianPending,Blocked;
            public long RewardRoot;
            public Vector2 Position;
        }
        private readonly ApprovedHive[] _approvedHives=new ApprovedHive[3];
        private bool _approvedHydraReady;
        private void ResetApprovedHydra()
        {
            foreach(var hive in _approvedHives)if(hive!=null)
            {
                if(hive.Pending>0||hive.GuardianPending)RecordRunHistory("hydra_hive_queue_cancelled","hydra-hive",instanceId:hive.Identity,amount:hive.Pending,reason:"encounter_ended",position:hive.Position);
                ReleaseFactionBirthRoot(hive.RewardRoot);
            }
            System.Array.Clear(_approvedHives,0,_approvedHives.Length);_approvedHydraReady=false;
        }
        private void StepApprovedHydra(float dt)
        {
            if(!CurrentVoidIsHydra||_mainMenuBrowsing)return;
            if(_hydraBossEncounterActive||_hydraBossSpawnedForVoid){if(_approvedHydraReady)ResetApprovedHydra();return;}
            if(JourneyStopsCombat)return;
            if(!_approvedHydraReady)
            {
                for(var i=0;i<3;i++)
                {
                    if(_approvedHives[i]!=null)continue;
                    var angle=i*Mathf.PI*2/3+.3f+(float)_gameSim.Rng.Next()*.45f;
                    var position=_gameSim.Player.Position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(470+(float)_gameSim.Rng.Next()*160);
                    if(!SpawnEnemy("hydra-hive",position,forcedRoster:EnemyRoster.One))return;
                    var identity=_nextEnemyId-1;long root=0;
                    for(var j=0;j<_gameSim.Enemies.Length;j++)if(_gameSim.Enemies[j].Active&&_gameSim.Enemies[j].SpawnId==identity){root=_factionActors[j].Root;_rewardRoots.Retain(root);break;}
                    _approvedHives[i]=new ApprovedHive{Identity=identity,Position=position,GuardianKind=i%2,RewardRoot=root};
                    RecordRunHistory("hydra_hive_spawn","hydra-hive",instanceId:identity,hp:180,maxHp:180,position:position,detail:"period=3;brood=5;legacy=3;insects=2");
                }
                _approvedHydraReady=true;
                RecordRunHistory("arena_map_policy","hydra-hives-v3",reason:"entered",sourceId:"hydra",detail:"cameraHeight=908;sliderPercent=60;stationaryGround=true;hives=3;legacyRosterPreserved=true");
            }
            foreach(var hive in _approvedHives)
            {
                if(hive==null)continue;
                if(!hive.Dead)
                {
                    hive.Clock-=dt;
                    while(hive.Clock<=0)
                    {
                        hive.Clock+=3;hive.Pending+=5;
                        RecordRunHistory("hydra_hive_brood_queued","hydra-hive",instanceId:hive.Identity,amount:5,position:hive.Position,detail:"pending="+hive.Pending);
                    }
                    // Bounded work per tick; counts retain every requested child through pool pressure.
                    for(var n=0;n<5&&hive.Pending>0;n++)
                    {
                        if(FindInactive(_gameSim.Enemies)<0){if(!hive.Blocked)RecordRunHistory("hydra_hive_brood_deferred","hydra-hive",instanceId:hive.Identity,amount:hive.Pending,reason:"pool_full");hive.Blocked=true;break;}
                        var old=hive.Released%5<3;var kind=(hive.Released/5+hive.Released)%HydraPopulationRules.Count;
                        var id=old?HydraPopulationRules.BaseId(kind):ApprovedMapContent.InsectIds[(hive.Released/5+hive.Released)%5];
                        var a=hive.Released*2.39996f;var position=hive.Position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*110;
                        var previous=_hydraPopulationChildKind;bool spawned;
                        try
                        {
                            _hydraPopulationChildKind=old?kind:-1;
                            using(FactionBirthScope(hive.RewardRoot))spawned=SpawnEnemy(id,position,forcedRoster:EnemyRoster.One);
                        }
                        finally{_hydraPopulationChildKind=previous;}
                        if(!spawned)break;
                        hive.Pending--;hive.Released++;hive.Blocked=false;
                        RecordRunHistory("hydra_hive_child",id,sourceId:"hydra-hive",instanceId:_nextEnemyId-1,relatedInstanceId:hive.Identity,position:position);
                    }
                }
                if(hive.GuardianPending&&FindInactive(_gameSim.Enemies)>=0)
                {
                    var id=hive.GuardianKind==0?"hydra-mantis-matriarch":"hydra-iron-carapace";bool spawned;
                    using(FactionBirthScope(hive.RewardRoot))spawned=SpawnEnemy(id,hive.Position,forcedRoster:EnemyRoster.One);
                    if(spawned){hive.GuardianPending=false;RecordRunHistory("hydra_hive_guardian",id,instanceId:_nextEnemyId-1,relatedInstanceId:hive.Identity,position:hive.Position);}
                }
            }
        }
        private void UpdateApprovedHydraEnemy(ref EnemyState e,ref ApprovedEnemyState s,float dt,float distance,Vector2 direction)
        {
            e.Facing=direction;e.Rotation=0;e.Spin=0;
            if(e.Id=="hydra-hive"){e.Velocity=Vector2.zero;e.Knockback=Vector2.zero;return;}
            var kind=ApprovedMapContent.InsectKind(e.Id);var mantis=e.Id=="hydra-mantis-matriarch";var carapace=e.Id=="hydra-iron-carapace";
            e.Velocity=direction*e.Speed;
            if(kind==2)
            {
                if(s.Fuse<0&&distance<115)
                {s.Fuse=.9f;RecordRunHistory("hydra_insect_fuse",e.Id,instanceId:e.SpawnId,durationSeconds:.9f,reason:"proximity",position:e.Position);}
                if(s.Fuse>=0)
                {
                    e.Velocity=Vector2.zero;s.Fuse-=dt;
                    if(s.Fuse<=0&&!s.Exploded)
                    {
                        s.Exploded=true;
                        var position=e.Position;
                        if(Vector2.Distance(position,_gameSim.Player.Position)<100+PlayerRadius)DamagePlayer(18,Vector2.zero);
                        var previousFaction=_damageFaction;
                        try
                        {
                            // Environmental blast can chain other beetles without recursively detonating them.
                            _damageFaction=CombatFaction.Destroyer;
                            for(var i=0;i<_gameSim.Enemies.Length;i++)
                            {
                                var target=_gameSim.Enemies[i];if(!target.Active||target.SpawnId==e.SpawnId||Vector2.Distance(position,target.Position)>100+target.Radius)continue;
                                ApplyEnemyDamage(i,24,Vector2.zero,0,false);
                            }
                        }
                        finally{_damageFaction=previousFaction;}
                        SpawnRingWave(position,5,100,.3f,new Color(1,.67f,.3f,.8f));
                        RecordRunHistory("hydra_insect_explosion",e.Id,instanceId:e.SpawnId,position:position,amount:100);
                        _gameSim.Enemies[e.View]=e;KillEnemy(e.View);e=_gameSim.Enemies[e.View];
                    }
                }
                return;
            }
            if(e.State==2)
            {
                e.Velocity=mantis?e.DashDirection*390:Vector2.zero;if(mantis)e.Facing=e.DashDirection;
                e.StateTimer-=dt;if(e.StateTimer<=0){e.State=0;e.AttackCooldown=4.5f;}return;
            }
            if(e.State==1)
            {
                e.Velocity=Vector2.zero;e.StateTimer-=dt;if(e.StateTimer>0)return;
                if(mantis){e.State=2;e.StateTimer=.7f;e.DashDirection=(s.Target-e.Position).normalized;return;}
                if(carapace)
                {
                    if(Vector2.Distance(_gameSim.Player.Position,s.Target)<150+PlayerRadius)DamagePlayer(22,Vector2.zero);
                    SpawnRingWave(s.Target,10,150,.45f,new Color(.78f,.77f,.44f,.8f));
                }
                else
                {
                    var aim=(s.Target-e.Position).normalized;var count=kind==4?3:1;
                    for(var i=0;i<count;i++){var a=Mathf.Atan2(aim.y,aim.x)+(i-(count-1)*.5f)*.15f;SpawnHostileShot(e.Position,new Vector2(Mathf.Cos(a),Mathf.Sin(a)),10,220,0,sourceId:e.Id);}
                }
                e.State=0;e.AttackCooldown=carapace?5.5f:4.5f;return;
            }
            if(!(mantis||carapace||kind==0||kind==4))return;
            if(kind==0||kind==4)e.Velocity=direction*e.Speed*(distance>240?1:distance<150?-.5f:0);
            if(e.AttackCooldown>0||distance>(carapace?210:450)||!CanCommitDirectorAttack(e))return;
            s.Target=carapace?e.Position:_gameSim.Player.Position;e.State=1;e.StateTimer=mantis?1.15f:carapace?1.3f:1f;
            RecordRunHistory("hydra_insect_warning",e.Id,instanceId:e.SpawnId,durationSeconds:e.StateTimer,position:s.Target);
        }
    }
}
