using System;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private readonly int[] _sentinelBursts=new int[CourtRookCount];
        private readonly int[] _sentinelWarnings=new int[CourtRookCount];
        private readonly LineRenderer[] _sentinelTerritories=new LineRenderer[CourtRookCount];
        private float _courtShieldNoticeCooldown;
        private void ResetApprovedCourt()
        {
            Array.Clear(_approvedEnemies,0,_approvedEnemies.Length);
            _courtShieldNoticeCooldown=0;
            for(var i=0;i<CourtRookCount;i++){_sentinelBursts[i]=_sentinelWarnings[i]=-1;if(_sentinelTerritories[i]!=null)_sentinelTerritories[i].enabled=false;}
            HideApprovedEnemyOverlays();
        }
        private string SelectApprovedCourtSpawn()
        {
            var roll=_gameSim.Rng.Next();
            var type=roll<.36?0:roll<.57?5:roll<.72?3:roll<.84?2:roll<.95?1:4;
            var rank=ApprovedMapRules.CourtTier(_monochromeSurvivalElapsed,_gameSim.Rng.Next());
            if(type==3&&roll<.64)return ApprovedMapContent.OriginalKnightId;
            if(type==1&&_monochromeSurvivalElapsed>=120&&_gameSim.Rng.Next()<.22)rank=3;
            return ApprovedMapContent.CourtId(type,rank);
        }
        private void SpawnApprovedPawnFormation(string id,Vector2 center,CourtFaction faction)
        {
            for(var i=0;i<3;i++)
            {
                if(ActiveEnemies()>=DirectorRules.ActiveEnemyCap(_time,0))break;
                var position=CourtSpawnPosition(center+Vector2.up*(i==0?-96:i==1?-32:32));
                if(!SpawnEnemy(id,position,forcedRoster:EnemyRoster.One))break;
                var identity=_nextEnemyId-1;
                for(var j=0;j<_gameSim.Enemies.Length;j++)if(_gameSim.Enemies[j].Active&&_gameSim.Enemies[j].SpawnId==identity)
                {var e=_gameSim.Enemies[j];e.Seed=faction==CourtFaction.Black?-Mathf.Abs(e.Seed)-1:Mathf.Abs(e.Seed)+1;_gameSim.Enemies[j]=e;break;}
            }
            RecordRunHistory("court_pawn_formation",id,position:center,detail:"fourPositions=-96,-32,32,96;centerGap=true");
        }
        private bool CourtTerritoryContains(CourtRook rook,Vector2 position)
        {
            if(rook==null||rook.Fallen||rook.SpawnId==0)return false;
            var cell=CourtCellIndex(position);var origin=CourtCellIndex(rook.Position);
            return cell>=0&&origin>=0&&ApprovedMapRules.InTerritory(cell%CourtBoardColumns,cell/CourtBoardColumns,origin%CourtBoardColumns,origin/CourtBoardColumns);
        }
        private void SyncCourtRookPositions(bool constrain=false)
        {
            // Crowd separation deliberately moves Sentinels. Their eye, shield and attack follow the body.
            for(var i=0;i<_gameSim.Enemies.Length;i++)
            {
                var enemy=_gameSim.Enemies[i];if(!enemy.Active||!IsCourtSentinel(enemy))continue;
                foreach(var rook in _courtRooks)
                {
                    if(rook==null||rook.Fallen||rook.SpawnId!=enemy.SpawnId)continue;
                    if(constrain)
                    {
                        enemy.Position=MonochromeRuntimeRules.ClampToBoard(enemy.Position,_monochromeBoardOrigin,
                            new Vector2(CourtBoardColumns,CourtBoardRows)*(float)MonochromeEncounterRules.TileSize,enemy.Radius);
                        _gameSim.Enemies[i]=enemy;
                    }
                    rook.Position=enemy.Position;break;
                }
            }
        }
        private bool SentinelCellMatches(int x,int y,int slot) =>
            ApprovedMapRules.CellMatches(x,y,ApprovedMapRules.SentinelWhite(_monochromeSurvivalElapsed,slot));
        private void CourtCellBurstFx(int x,int y)
        {
            if(_saveData?.settings?.reducedMotion??false)return;
            var position=CourtCellCentre(x,y);var delta=position-RenderCameraCentre();
            var half=GameplayViewportHalfExtent()+_monochromeBoardTileSize;
            if(Mathf.Abs(delta.x)>half.x||Mathf.Abs(delta.y)>half.y)return;
            BurstFx(position,new Color(1,.58f,.35f),4,150,.45f,.7f);
        }
        private float ApprovedCourtCellStage(int x,int y)
        {
            var result=0f;
            for(var i=0;i<CourtRookCount;i++)
            {
                if(!SentinelCellMatches(x,y,i)||!CourtTerritoryContains(_courtRooks[i],CourtCellCentre(x,y)))continue;
                var age=ApprovedMapRules.SentinelAge(_monochromeSurvivalElapsed,i);
                result=Mathf.Max(result,age<3?1:age<3.45f?2:0);
            }
            return result;
        }
        private float ApprovedCourtBurstProgress(int x,int y)
        {
            if(_monochromeBossEncounterActive)return Mathf.Clamp01((_monochromeBossElapsed%5f-3.4f)/.45f);
            for(var i=0;i<CourtRookCount;i++)if(SentinelCellMatches(x,y,i)&&CourtTerritoryContains(_courtRooks[i],CourtCellCentre(x,y)))
            {var age=ApprovedMapRules.SentinelAge(_monochromeSurvivalElapsed,i);if(age>=3&&age<3.45f)return (age-3)/.45f;}
            return 1;
        }
        private void StepApprovedCourt(float dt)
        {
            if(!CurrentVoidIsMonochrome||!_courtFieldReady)return;
            SyncCourtRookPositions(true);
            _courtShieldNoticeCooldown=Mathf.Max(0,_courtShieldNoticeCooldown-dt);
            var grantedShield=false;
            if(!_monochromeBossEncounterActive)for(var i=0;i<CourtRookCount;i++)
            {
                var rook=_courtRooks[i];if(rook==null||rook.Fallen||rook.SpawnId==0)continue;
                var cycle=ApprovedMapRules.SentinelCycle(_monochromeSurvivalElapsed,i);
                var age=ApprovedMapRules.SentinelAge(_monochromeSurvivalElapsed,i);
                var color=ApprovedMapRules.SentinelWhite(_monochromeSurvivalElapsed,i)?"white":"black";
                if(age<3&&_sentinelWarnings[i]!=cycle)
                {
                    _sentinelWarnings[i]=cycle;
                    RecordRunHistory("court_sentinel_warning","court-sentinel",instanceId:rook.SpawnId,durationSeconds:3,position:rook.Position,detail:"cells=4x4;color="+color+";cycle="+cycle+";followsRook=true");
                }
                if(age>=3&&age<3.45f&&_sentinelBursts[i]!=cycle)
                {
                    _sentinelBursts[i]=cycle;
                    var playerCell=CourtCellIndex(_gameSim.Player.Position);
                    var hit=playerCell>=0&&CourtTerritoryContains(rook,_gameSim.Player.Position)&&SentinelCellMatches(playerCell%CourtBoardColumns,playerCell/CourtBoardColumns,i);
                    if(hit)DamagePlayer(20,Vector2.zero);
                    var cell=CourtCellIndex(rook.Position);var count=0;
                    for(var dy=-2;dy<2;dy++)for(var dx=-2;dx<2;dx++)
                    {
                        var x=cell%CourtBoardColumns+dx;var y=cell/CourtBoardColumns+dy;
                        if(x<0||y<0||x>=CourtBoardColumns||y>=CourtBoardRows||!SentinelCellMatches(x,y,i))continue;
                        count++;CourtCellBurstFx(x,y);
                    }
                    RecordRunHistory("court_sentinel_burst","court-sentinel",instanceId:rook.SpawnId,position:rook.Position,amount:count,detail:"color="+color+";cycle="+cycle+";playerHit="+hit+";enemiesImmune=true");
                }
            }
            for(var i=0;i<_gameSim.Enemies.Length;i++)
            {
                var enemy=_gameSim.Enemies[i];if(!enemy.Active||IsCourtSentinel(enemy))continue;
                ref var state=ref ApprovedState(enemy);var inside=false;
                if(!_monochromeBossEncounterActive)foreach(var rook in _courtRooks)if(CourtTerritoryContains(rook,enemy.Position)){inside=true;break;}
                if(!inside)
                {
                    if(state.Inside)RecordRunHistory("court_territory_shield",enemy.Id,instanceId:enemy.SpawnId,reason:"left_territory",position:enemy.Position);
                    state.Shield=0;state.ShieldCooldown=0;state.Inside=false;continue;
                }
                state.ShieldCooldown=Mathf.Max(0,state.ShieldCooldown-dt);
                if((!state.Inside||state.Shield<=0)&&state.ShieldCooldown<=0)
                {
                    state.Shield=ApprovedMapRules.TerritoryShield;
                    grantedShield=true;
                    RecordRunHistory("court_territory_shield",enemy.Id,instanceId:enemy.SpawnId,reason:state.Inside?"recharged":"entered",amount:state.Shield,position:enemy.Position);
                }
                state.Inside=true;
            }
            if(grantedShield&&_courtShieldNoticeCooldown<=0)
            {
                ShowArenaToast("Enemy shielded",1.5f,ToastKind.Info);
                _courtShieldNoticeCooldown=1.5f;
            }
        }
        private void RenderApprovedTerritories()
        {
            for(var i=0;i<CourtRookCount;i++)
            {
                var rook=_courtRooks[i];
                if(_arenaId!=ArenaId.MonochromeCourt||_monochromeBossEncounterActive||rook==null||rook.Fallen||rook.SpawnId==0)
                {if(_sentinelTerritories[i]!=null)_sentinelTerritories[i].enabled=false;continue;}
                var cell=CourtCellIndex(rook.Position);var min=_monochromeBoardOrigin+new Vector2(cell%CourtBoardColumns-2,cell/CourtBoardColumns-2)*(float)MonochromeEncounterRules.TileSize;
                var size=4*(float)MonochromeEncounterRules.TileSize;
                if(_sentinelTerritories[i]==null)_sentinelTerritories[i]=CreateLineView("Sentinel fixed territory "+i,-65);
                var line=_sentinelTerritories[i];line.positionCount=5;line.SetPosition(0,min);line.SetPosition(1,min+Vector2.right*size);line.SetPosition(2,min+Vector2.one*size);line.SetPosition(3,min+Vector2.up*size);line.SetPosition(4,min);
                line.startWidth=line.endWidth=1.5f;line.startColor=line.endColor=new Color(.62f,.81f,.75f,.5f);line.enabled=true;
            }
        }
        private void UpdateApprovedCourtEnemy(ref EnemyState e,ref ApprovedEnemyState s,float dt,float distance,Vector2 direction)
        {
            var type=ApprovedMapContent.CourtType(e.Id);var rank=ApprovedMapContent.CourtRank(e.Id);
            e.Facing=direction;e.Spin=0;e.Rotation=0;
            if(type==0||type==5){e.Velocity=direction*e.Speed;return;}
            if(type==2||type==4)
            {
                if(e.State==1)
                {
                    e.Velocity=Vector2.zero;e.StateTimer-=dt;if(e.StateTimer>0)return;
                    if(type==2)
                    {
                        var angle=Mathf.Atan2(s.Target.y-e.Position.y,s.Target.x-e.Position.x);
                        for(var j=0;j<(rank==2?2:1);j++){var a=angle+j*Mathf.PI*.5f;SpawnHostileShot(e.Position,new Vector2(Mathf.Cos(a),Mathf.Sin(a)),e.Damage,300,0,sourceId:e.Id);}
                    }
                    else
                    {
                        PromoteApprovedPawn(e,s.TargetSlot,s.TargetIdentity,rank);
                        if(s.SecondTargetSlot>=0)PromoteApprovedPawn(e,s.SecondTargetSlot,s.SecondTargetIdentity,rank);
                    }
                    e.State=0;e.AttackCooldown=type==4?7f:4.2f;return;
                }
                e.Velocity=direction*e.Speed*(distance>360?1:distance<220?-.6f:0);
                if(e.AttackCooldown>0||!CanCommitDirectorAttack(e))return;
                s.Target=_gameSim.Player.Position;
                if(type==2)
                {
                    var a=Mathf.Atan2(direction.y,direction.x);
                    a=Mathf.Round((a-Mathf.PI*.25f)/(Mathf.PI*.5f))*Mathf.PI*.5f+Mathf.PI*.25f;
                    s.Target=e.Position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*440;
                }
                if(type==4)
                {
                    s.TargetSlot=s.SecondTargetSlot=-1;
                    for(var i=0;i<_gameSim.Enemies.Length;i++)
                    {
                        var pawn=_gameSim.Enemies[i];if(!pawn.Active||ApprovedMapContent.CourtType(pawn.Id)!=0||CourtFactionOf(pawn)!=CourtFactionOf(e)||Vector2.Distance(pawn.Position,e.Position)>430)continue;
                        if(ApprovedMapContent.CourtRank(pawn.Id)>=2&&rank<2)continue;
                        if(s.TargetSlot<0){s.TargetSlot=i;s.TargetIdentity=pawn.SpawnId;s.Target=pawn.Position;if(rank==0)break;}
                        else{s.SecondTargetSlot=i;s.SecondTargetIdentity=pawn.SpawnId;break;}
                    }
                    if(s.TargetSlot<0){e.AttackCooldown=1;return;}
                }
                e.State=1;e.StateTimer=type==4?2.7f:1.15f;
                RecordRunHistory(type==4?"court_promotion_warning":"court_bishop_warning",e.Id,instanceId:e.SpawnId,relatedInstanceId:s.TargetIdentity,durationSeconds:e.StateTimer,position:s.Target);
                return;
            }
            if(e.State==0)
            {
                e.Velocity=direction*e.Speed;
                if(e.AttackCooldown>0||distance>550||!CanCommitDirectorAttack(e))return;
                s.Origin=e.Position;
                if(type==3)
                {
                    if(!PlanApprovedKnightPath(e.Position,_gameSim.Player.Position,out s.Corner,out s.Target)){e.AttackCooldown=1;return;}
                }
                else
                {
                    var axis=Mathf.Abs(direction.x)>Mathf.Abs(direction.y)?new Vector2(Mathf.Sign(direction.x),0):new Vector2(0,Mathf.Sign(direction.y));
                    s.Corner=s.Target=MonochromeRuntimeRules.ClampToBoard(e.Position+axis*400,_monochromeBoardOrigin,
                        new Vector2(CourtBoardColumns,CourtBoardRows)*(float)MonochromeEncounterRules.TileSize,e.Radius);
                }
                e.State=1;e.StateTimer=rank==0?1.2f:rank==1?1.05f:rank==2?.95f:1.25f;
                RecordRunHistory("court_path_warning",e.Id,instanceId:e.SpawnId,durationSeconds:e.StateTimer,position:s.Target,detail:"fixedPath=true");return;
            }
            if(e.State==1||e.State==3||e.State==5)
            {
                e.Velocity=Vector2.zero;e.StateTimer-=dt;if(e.StateTimer>0)return;
                if(e.State==5){e.State=0;e.AttackCooldown=4.2f;return;}
                e.State=e.State==1?2:4;
            }
            var target=e.State==2?s.Corner:s.Target;var delta=target-e.Position;var speed=type==3?430f:390f;
            if(delta.magnitude<=speed*dt+1)
            {
                e.Position=target;e.Velocity=Vector2.zero;e.State=type==3&&e.State==2?3:5;e.StateTimer=e.State==3?.16f:.6f;
            }
            else e.Velocity=delta.normalized*speed;
        }
        private bool PlanApprovedKnightPath(Vector2 start,Vector2 target,out Vector2 corner,out Vector2 end)
        {
            corner=end=start;var best=float.MaxValue;var tile=(float)MonochromeEncounterRules.TileSize;
            for(var h=0;h<2;h++)for(var sx=-1;sx<=1;sx+=2)for(var sy=-1;sy<=1;sy+=2)
            {
                var c=start+(h==0?Vector2.right*sx:Vector2.up*sy)*tile*2;
                var e=c+(h==0?Vector2.up*sy:Vector2.right*sx)*tile;
                var boardSize=new Vector2(CourtBoardColumns,CourtBoardRows)*tile;
                if(MonochromeRuntimeRules.ClampToBoard(c,_monochromeBoardOrigin,boardSize,60)!=c ||
                    MonochromeRuntimeRules.ClampToBoard(e,_monochromeBoardOrigin,boardSize,60)!=e)continue;
                var blocked=false;foreach(var rook in _courtRooks)
                    if(rook!=null&&!rook.Fallen&&(DistanceToApprovedSegment(rook.Position,start,c)<145||DistanceToApprovedSegment(rook.Position,c,e)<145)){blocked=true;break;}
                if(blocked)continue;var score=(e-target).sqrMagnitude;if(score>=best)continue;best=score;corner=c;end=e;
            }
            return best<float.MaxValue;
        }
        private static float DistanceToApprovedSegment(Vector2 p,Vector2 a,Vector2 b)
        {var d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.001f,d.sqrMagnitude)));}
        private void PromoteApprovedPawn(EnemyState queen,int slot,int identity,int queenRank)
        {
            if(slot<0)return;var pawn=_gameSim.Enemies[slot];
            if(!pawn.Active||pawn.SpawnId!=identity||Vector2.Distance(queen.Position,pawn.Position)>430)return;
            var rank=ApprovedMapContent.CourtRank(pawn.Id);
            if(rank>=2)
            {
                if(queenRank>=2){ref var s=ref ApprovedState(pawn);s.Ward=24;s.WardTime=6;RecordRunHistory("court_queen_ward",pawn.Id,instanceId:pawn.SpawnId,sourceId:queen.Id,amount:24,durationSeconds:6,position:pawn.Position);}return;
            }
            var id=ApprovedMapContent.CourtId(0,rank+1);var next=ApprovedMapContent.FindEnemy(id);var previous=ApprovedMapContent.FindEnemy(pawn.Id);
            // Retain the native director/visit modifiers when changing the underlying rank.
            var maximum=pawn.MaxHealth*(float)(next.Health/previous.Health);var gain=maximum-pawn.MaxHealth;
            pawn.Id=id;pawn.MaxHealth=maximum;pawn.Health=Mathf.Min(maximum,pawn.Health+Mathf.Max(0,gain));
            pawn.Speed*=(float)(next.Speed/previous.Speed);pawn.Radius*=(float)(next.Radius/previous.Radius);pawn.Damage*=(float)(next.ContactDamage/previous.ContactDamage);
            _gameSim.Enemies[slot]=pawn;
            RecordRunHistory("court_pawn_promoted",id,sourceId:queen.Id,instanceId:pawn.SpawnId,relatedInstanceId:queen.SpawnId,position:pawn.Position,detail:"rank="+(rank+2));
        }
    }
}
