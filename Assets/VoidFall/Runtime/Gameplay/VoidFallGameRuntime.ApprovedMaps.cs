using System;
using System.Collections.Generic;
using UnityEngine;
using VoidFall.Core;

namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {
        private struct ApprovedEnemyState
        {
            public int Identity, TargetIdentity, TargetSlot, SecondTargetIdentity, SecondTargetSlot;
            public float Shield, ShieldCooldown, Ward, WardTime, Fuse, BlockFlash;
            public bool Inside, Exploded;
            public Vector2 Origin, Corner, Target;
            public string SpriteId;
            public bool SpriteWhite;
            public Sprite[] Frames;
        }
        private readonly ApprovedEnemyState[] _approvedEnemies = new ApprovedEnemyState[MaxEnemies];
        private readonly LineRenderer[] _approvedWarnings = new LineRenderer[MaxEnemies];
        private readonly LineRenderer[] _approvedSecondaryWarnings = new LineRenderer[MaxEnemies];
        private readonly LineRenderer[] _approvedShields = new LineRenderer[MaxEnemies];
        private readonly Dictionary<string,Sprite> _approvedSprites = new Dictionary<string,Sprite>();
        private Sprite ApprovedMapSprite(string name)
        {
            if (!_approvedSprites.TryGetValue(name,out var sprite))
            { sprite=Resources.Load<Sprite>("VoidFall/ApprovedMaps/"+name); _approvedSprites.Add(name,sprite); }
            return sprite;
        }
        private Vector2 ApprovedMapViewport(float height)
        {
            var aspect=Screen.height>0?Mathf.Max(.5f,(float)Screen.width/Screen.height):16f/9f;
            return new Vector2(height*.5f*aspect,height*.5f)*_spatialZoomScale;
        }
        private ref ApprovedEnemyState ApprovedState(EnemyState enemy)
        {
            ref var state=ref _approvedEnemies[enemy.View];
            if(state.Identity!=enemy.SpawnId) state=new ApprovedEnemyState { Identity=enemy.SpawnId,TargetSlot=-1,Fuse=-1f };
            return ref state;
        }
        private static string ApprovedCourtBaseId(string id)
        { var type=ApprovedMapContent.CourtType(id); return type<0?id:ApprovedMapContent.CourtId(type,0); }
        private bool TryUpdateApprovedEnemy(ref EnemyState enemy,float dt,float distance,Vector2 direction)
        {
            ref var state=ref ApprovedState(enemy);
            state.BlockFlash=Mathf.Max(0,state.BlockFlash-dt);
            state.WardTime=Mathf.Max(0,state.WardTime-dt);if(state.WardTime==0)state.Ward=0;
            var city=NullCityContent.EnemyIndex(enemy.Id);
            if(city>=12){UpdateApprovedCityEnemy(ref enemy,ref state,city-12,dt,distance,direction);return true;}
            if(enemy.Id.StartsWith("hydra-",StringComparison.Ordinal)){UpdateApprovedHydraEnemy(ref enemy,ref state,dt,distance,direction);return true;}
            if(IsCourtEnemy(enemy.Id)&&!IsCourtSentinel(enemy)){UpdateApprovedCourtEnemy(ref enemy,ref state,dt,distance,direction);return true;}
            return false;
        }
        private bool ApplyApprovedEnemyDefense(int index,ref EnemyState enemy,ref float damage,int weaponIndex)
        {
            ref var state=ref ApprovedState(enemy);
            if(ApprovedMapContent.CourtType(enemy.Id)==5 && _damageFaction==CombatFaction.Player && damage>0 && _gameSim.Rng.Next()<.2)
            {
                state.BlockFlash=.18f;
                SpawnFloater(enemy.Position+Vector2.up*enemy.Radius,"BLOCK",new Color(.85f,.9f,.85f),10);
                RecordRunHistory("court_armored_block",enemy.Id,instanceId:enemy.SpawnId,amount:damage,position:enemy.Position);
                return true;
            }
            var absorbed=Mathf.Min(damage,state.Shield);state.Shield-=absorbed;damage-=absorbed;
            var ward=Mathf.Min(damage,state.Ward);state.Ward-=ward;damage-=ward;
            if(absorbed+ward>0)
            {
                RecordEnemyDamage(index,enemy,absorbed+ward);
                if(_damageFaction==CombatFaction.Player){_damageDealt+=absorbed+ward;TrackWeaponDamage(weaponIndex,absorbed+ward);}
            }
            if(absorbed>0 && state.Shield<=0)state.ShieldCooldown=4f;
            if(enemy.Id=="hydra-blisterbeetle" && !state.Exploded && damage>=enemy.Health)
            {
                damage=Mathf.Max(0,enemy.Health-1f);
                state.Fuse=state.Fuse<0?.45f:Mathf.Min(.45f,state.Fuse);
                RecordRunHistory("hydra_insect_fuse",enemy.Id,instanceId:enemy.SpawnId,durationSeconds:state.Fuse,reason:"lethal_hit",position:enemy.Position);
            }
            return false;
        }
        private void HideApprovedEnemyOverlays()
        {
            foreach(var view in _approvedWarnings)if(view!=null)view.enabled=false;
            foreach(var view in _approvedSecondaryWarnings)if(view!=null)view.enabled=false;
            foreach(var view in _approvedShields)if(view!=null)view.enabled=false;
        }
        private bool TryRenderApprovedEnemy(int index,EnemyState enemy)
        {
            ref var state=ref ApprovedState(enemy);
            string name=null;
            var type=ApprovedMapContent.CourtType(enemy.Id);
            var sentinel=IsCourtSentinel(enemy);
            var white=CourtFactionOf(enemy)==CourtFaction.White;
            var city=NullCityContent.EnemyIndex(enemy.Id);
            var insect=ApprovedMapContent.InsectKind(enemy.Id);
            var animated=type==5||city>=12||insect>=0||enemy.Id=="hydra-mantis-matriarch"||enemy.Id=="hydra-iron-carapace";
            if(state.SpriteId!=enemy.Id||state.SpriteWhite!=white)
            {
            if(sentinel)
            {
                var slot=0;for(var i=0;i<_courtRooks.Length;i++)if(_courtRooks[i]!=null&&_courtRooks[i].SpawnId==enemy.SpawnId){slot=i;break;}
                name="sentinel-"+(slot%3)+"-"+(CourtFactionOf(enemy)==CourtFaction.White?"white":"black");
            }
            else if(type>=0)name="court-"+ApprovedMapContent.CourtFamilies[type]+"-"+(ApprovedMapContent.CourtRank(enemy.Id)+1)+"-"+(CourtFactionOf(enemy)==CourtFaction.White?"white":"black");
            if(city>=12)name="city-"+(city-12);
            if(insect>=0)name="insect-"+insect;
            if(enemy.Id=="hydra-hive")name="hydra-hive";
            if(enemy.Id=="hydra-mantis-matriarch")name="guardian-0";
            if(enemy.Id=="hydra-iron-carapace")name="guardian-1";
            state.SpriteId=enemy.Id;state.SpriteWhite=white;state.Frames=null;
            if(name!=null)
            {
                state.Frames=new Sprite[animated?4:1];state.Frames[0]=ApprovedMapSprite(name);
                if(animated)for(var f=1;f<4;f++)state.Frames[f]=ApprovedMapSprite(name+"-frame"+f);
            }
            }
            var frame=animated&&enemy.Velocity.sqrMagnitude>1&&!(_saveData?.settings?.reducedMotion??false)?Mathf.FloorToInt(enemy.Age*8)%4:0;
            var ownsAttackPreview=type>=0||city>=12||insect>=0||enemy.Id=="hydra-hive"||enemy.Id=="hydra-mantis-matriarch"||enemy.Id=="hydra-iron-carapace";
            RenderApprovedEnemyOverlays(index,enemy,state,type,ownsAttackPreview);
            if(state.Frames==null)return false;
            var sprite=state.Frames[frame];if(sprite==null)return false;
            var view=_enemyViews[index];view.sprite=sprite;view.transform.position=enemy.Position;
            var angle=(insect>=0||enemy.Id=="hydra-mantis-matriarch"||enemy.Id=="hydra-iron-carapace")?Mathf.Atan2(enemy.Facing.y,enemy.Facing.x)*Mathf.Rad2Deg-90f:0f;
            view.transform.rotation=Quaternion.Euler(0,0,angle);
            view.transform.localScale=Vector3.one;view.flipX=type==5&&enemy.Facing.x<0;
            view.color=enemy.HitTimer>0||state.BlockFlash>0?new Color(1f,.85f,.7f):Color.white;
            view.enabled=true;return true;
        }
        private void RenderApprovedEnemyOverlays(int index,EnemyState enemy,ApprovedEnemyState state,int type,bool ownsAttackPreview)
        {
            if(state.Shield>0||state.Ward>0)NullCityCircle(ref _approvedShields[index],"Court protection",enemy.Position,enemy.Radius+8,new Color(.56f,.8f,.73f,.9f));
            // Shared roster enemies use their own telegraphs. Their map-specific
            // Target is unset, so drawing it creates a bogus line to world origin.
            if(!ownsAttackPreview||JourneyStopsCombat)return;
            if(state.Fuse>=0&&!state.Exploded)NullCityCircle(ref _approvedWarnings[index],"Blister warning",enemy.Position,100,new Color(1f,.64f,.34f,.8f));
            else if((enemy.State==1 || enemy.Id=="null-grav-loom"&&enemy.State==2) && !IsCourtSentinel(enemy))
            {
                if(type==3||type==1)
                {
                    if(_approvedWarnings[index]==null)_approvedWarnings[index]=CreateLineView("Court committed path",25);
                    var line=_approvedWarnings[index];line.positionCount=3;line.SetPosition(0,state.Origin);line.SetPosition(1,state.Corner);line.SetPosition(2,state.Target);
                    line.startWidth=line.endWidth=2.5f;line.startColor=line.endColor=new Color(.9f,.75f,.55f,.7f);line.enabled=true;
                }
                else if(enemy.Id=="null-grav-loom"||enemy.Id=="hydra-iron-carapace")NullCityCircle(ref _approvedWarnings[index],"Committed impact",state.Target,enemy.Id=="null-grav-loom"?50:150,new Color(1f,.75f,.45f,.85f));
                else NullCityLine(ref _approvedWarnings[index],"Approved attack warning",enemy.Position,state.Target,2,new Color(.9f,.8f,.5f,.65f));
                if(type==2&&ApprovedMapContent.CourtRank(enemy.Id)==2)
                {var d=state.Target-enemy.Position;NullCityLine(ref _approvedSecondaryWarnings[index],"Bishop second diagonal",enemy.Position,enemy.Position+new Vector2(-d.y,d.x),2,new Color(.9f,.8f,.5f,.65f));}
                if(type==4&&state.SecondTargetSlot>=0)
                {var target=_gameSim.Enemies[state.SecondTargetSlot];if(target.Active&&target.SpawnId==state.SecondTargetIdentity)NullCityLine(ref _approvedSecondaryWarnings[index],"Queen second promotion",enemy.Position,target.Position,2,new Color(.9f,.8f,.5f,.65f));}
            }
        }
        private void OnApprovedEnemyDeath(EnemyState enemy)
        {
            foreach(var hive in _approvedHives)
            {
                if(hive==null||hive.Identity!=enemy.SpawnId||hive.Dead)continue;
                hive.Dead=true;hive.Pending=0;hive.GuardianPending=true;
                RecordRunHistory("hydra_hive_destroyed","hydra-hive",instanceId:enemy.SpawnId,position:enemy.Position,detail:"broodsStopped=true;guardianQueued=true");
            }
        }
    }
}
