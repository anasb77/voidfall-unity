using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private SpriteMask _approvedTransitMask;
        private readonly LineRenderer[] _approvedPortals=new LineRenderer[2];
        private readonly LineRenderer[] _approvedRoadArcs=new LineRenderer[32];
        private readonly LineRenderer[] _approvedRoadGlow=new LineRenderer[2];
        private void HideApprovedRoadEnergy()
        {
            foreach(var line in _approvedRoadArcs)if(line!=null)line.enabled=false;
            foreach(var line in _approvedRoadGlow)if(line!=null)line.enabled=false;
        }
        private void RenderApprovedRoadEnergy(NullCityPurge h,Vector2 offset,bool reduced)
        {
            var vertical=h.Lane>=2;
            var center=NullCityWorld((float)(h.X+h.Width*.5),(float)(h.Y+h.Height*.5))+offset;
            var axis=vertical?Vector2.down:Vector2.right;var cross=new Vector2(-axis.y,axis.x);
            var length=(float)(vertical?h.Height:h.Width)*NullCityRules.WorldScale;
            for(var i=0;i<2;i++)NullCityLine(ref _approvedRoadGlow[i],"Road energy bloom",center-axis*length*.5f,center+axis*length*.5f,i==0?24:10,
                i==0?new Color(1,.52f,.25f,.22f):new Color(1,.82f,.56f,.75f),-72);
            var time=_mainMenuBrowsing?_ambientClock:_nullCityElapsed;
            for(var i=0;i<_approvedRoadArcs.Length&&i*68+40<length;i++)
            {
                if(_approvedRoadArcs[i]==null)_approvedRoadArcs[i]=CreateLineView("Road electrical arc "+i,-70);
                var line=_approvedRoadArcs[i];var start=center+axis*(i*68-length*.5f);var off=Mathf.Sin((i*68+Mathf.Floor(time*(reduced?0:16)))*1.73f)*15;
                line.positionCount=4;line.SetPosition(0,start-cross*13);line.SetPosition(1,start+axis*13+cross*off);line.SetPosition(2,start+axis*27-cross*off*.5f);line.SetPosition(3,start+axis*40+cross*11);
                line.startWidth=line.endWidth=1.2f;line.startColor=line.endColor=new Color(1,.87f,.64f,.65f);line.enabled=true;
            }
        }
        private void RenderApprovedTransit(float time)
        {
            if(_nullCityTransitView==null||_nullCityTransitView.sprite==null)return;
            var left=NullCityWorld(279,230);var right=NullCityWorld(1320,230);
            if(_approvedTransitMask==null)
            {
                var obj=new GameObject("Transit portal clipping");obj.transform.SetParent(_worldRoot,false);
                _approvedTransitMask=obj.AddComponent<SpriteMask>();
                _approvedTransitMask.sprite=ArenaPlateFactory.SpriteFromPixels(new[]{new Color32(255,255,255,255)},1,1,"Transit clip");
                _approvedTransitMask.isCustomRangeActive=true;_approvedTransitMask.backSortingOrder=-89;_approvedTransitMask.frontSortingOrder=-88;
            }
            _approvedTransitMask.transform.position=(left+right)*.5f;
            _approvedTransitMask.transform.localScale=new Vector3(right.x-left.x,110,1);
            var journey=(right.x-left.x+190)/96f;var phase=time%(journey+2);
            _nullCityTransitView.enabled=phase<journey;
            _nullCityTransitView.maskInteraction=SpriteMaskInteraction.VisibleInsideMask;
            _nullCityTransitView.transform.position=new Vector2(left.x-95+Mathf.Min(phase,journey)*96,left.y-8);
            var size=_nullCityTransitView.sprite.bounds.size;
            _nullCityTransitView.transform.localScale=new Vector3(190/size.x,80/size.y,1);
            for(var j=0;j<2;j++)
            {
                if(_approvedPortals[j]==null)_approvedPortals[j]=CreateLineView("Transit portal mouth "+j,-87);
                var portal=_approvedPortals[j];portal.positionCount=49;var center=j==0?left:right;
                for(var k=0;k<=48;k++){var a=k*Mathf.PI*2/48;portal.SetPosition(k,center+new Vector2(Mathf.Cos(a)*14,Mathf.Sin(a)*27));}
                portal.startWidth=portal.endWidth=3;portal.startColor=portal.endColor=new Color(.43f,.68f,.73f,.9f);portal.enabled=true;
            }
        }
        private void UpdateApprovedCityEnemy(ref EnemyState e,ref ApprovedEnemyState s,int kind,float dt,float distance,Vector2 direction)
        {
            e.Facing=direction;e.Rotation=0;e.Spin=0;
            if(kind>=5){e.Velocity=direction*e.Speed*(NullCityLockdown?.7f:1f);return;}
            if(NullCityLockdown){e.State=0;e.AttackCooldown=Mathf.Max(1,e.AttackCooldown);e.Velocity=direction*e.Speed*.3f;return;}
            if(e.State==2)
            {
                e.StateTimer-=dt;e.Velocity=kind==2?e.DashDirection*360:Vector2.zero;
                if(e.StateTimer<=0)
                {
                    if(kind==3){if(Vector2.Distance(s.Target,_gameSim.Player.Position)<50+PlayerRadius)DamagePlayer(18,Vector2.zero);SpawnRingWave(s.Target,10,55,.3f,new Color(1,.7f,.4f,.8f));}
                    e.State=0;
                }
                return;
            }
            if(e.State==1)
            {
                e.Velocity=Vector2.zero;e.StateTimer-=dt;if(e.StateTimer>0)return;
                var aim=(s.Target-e.Position).normalized;
                if(kind==0||kind==1)
                {
                    var count=kind==0?3:2;
                    for(var j=0;j<count;j++)
                    {
                        var a=Mathf.Atan2(aim.y,aim.x)+(j-(count-1)*.5f)*(kind==0?.16f:.26f);
                        SpawnHostileShot(e.Position,new Vector2(Mathf.Cos(a),Mathf.Sin(a)),12,kind==0?230:200,0,sourceId:e.Id);
                    }
                }
                if(kind==2){e.State=2;e.StateTimer=.42f;e.DashDirection=aim;}
                else if(kind==3){e.State=2;e.StateTimer=1.25f;}
                else
                {
                    if(kind==4 && s.TargetSlot>=0)
                    {
                        var target=_gameSim.Enemies[s.TargetSlot];
                        if(target.Active&&target.SpawnId==s.TargetIdentity&&Vector2.Distance(e.Position,target.Position)<170)
                        {
                            var healed=Mathf.Min(28,target.MaxHealth-target.Health);target.Health+=healed;_gameSim.Enemies[s.TargetSlot]=target;
                            SpawnRingWave(target.Position,10,30,.4f,new Color(.6f,.85f,1f,.8f));
                            RecordRunHistory("city_repair",target.Id,sourceId:e.Id,instanceId:e.SpawnId,relatedInstanceId:target.SpawnId,amount:healed,position:target.Position);
                        }
                    }
                    e.State=0;
                }
                return;
            }
            if(kind==1)e.Velocity=new Vector2(-direction.y,direction.x)*e.Speed*.7f*(e.SpawnId%2==0?1:-1)+direction*e.Speed*(distance>205?.55f:distance<140?-.6f:0);
            else if(kind==2)e.Velocity=direction*e.Speed;
            else {var range=kind==3?260:210;e.Velocity=direction*e.Speed*(distance>range?1:distance<range-60?-.55f:0);}
            if(e.AttackCooldown>0||distance>460||!CanCommitDirectorAttack(e))return;
            if(kind==4)
            {
                s.TargetSlot=-1;
                for(var i=0;i<_gameSim.Enemies.Length;i++)
                {
                    var target=_gameSim.Enemies[i];if(!target.Active||target.SpawnId==e.SpawnId||target.Health>=target.MaxHealth||Vector2.Distance(e.Position,target.Position)>=150)continue;
                    s.TargetSlot=i;s.TargetIdentity=target.SpawnId;s.Target=target.Position;break;
                }
                if(s.TargetSlot<0){e.AttackCooldown=.7f;return;}
            }
            else s.Target=_gameSim.Player.Position;
            e.State=1;e.StateTimer=kind==2?.9f:1.1f;e.AttackCooldown=kind==0?4.6f:kind==1?4:kind==2?5.8f:kind==3?6.5f:5;
            RecordRunHistory("city_attack_warning",e.Id,instanceId:e.SpawnId,durationSeconds:e.StateTimer,position:s.Target);
        }
    }
}
