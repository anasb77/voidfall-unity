using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering.Universal;
using VoidFall.Core;
using VoidFall.Persistence;

namespace VoidFall.Runtime
{
    // Temporary isolated investigation; present only in the diagnostic checkout/build.
    public static class CourtPerformanceProbe
    {
        public static bool Active;
        private static readonly Dictionary<string, double> Last = new Dictionary<string, double>();
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        [Serializable] private class Stage
        {
            public string name; public int frames, width, height, enemies, packages;
            public double meanFrameMs, medianFrameMs, p95FrameMs, maxFrameMs, cpuUpdateMs, cpuRenderMs,
                cpuArenaMs, cpuCourtMs, cpuSimMs, gpuFrameMs, timingCpuFrameMs;
            public int gpuSamples, gcCollections; public long managedBytes, graphicsBytes;
        }
        [Serializable] private class Report
        {
            public string gpu, cpu; public int vramMB; public string quality="high";
            public Stage[] stages;
        }
        public static void Observe(string phase, double milliseconds) { if (Active) Last[phase] = milliseconds; }
        private static object Get(object target,string field) => target.GetType().GetField(field,Flags).GetValue(target);
        private static void Set(object target,string field,object value) => target.GetType().GetField(field,Flags).SetValue(target,value);
        private static void Call(object target,string name,params object[] args)
        {
            foreach(var method in target.GetType().GetMethods(Flags))
                if(method.Name==name && method.GetParameters().Length==args.Length){method.Invoke(target,args);return;}
            throw new MissingMethodException(name);
        }
        private static double Phase(string name) => Last.TryGetValue(name,out var value) ? value : 0;
        public static IEnumerator Run(VoidFallGameRuntime runtime,string output)
        {
            Active=true; Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=-1;
            var profile=(SaveData)Get(runtime,"_saveData");
            profile.settings.resolutionWidth=1920; profile.settings.resolutionHeight=1080; profile.settings.fullscreenMode=3;
            Call(runtime,"ApplyVideoSettings");
            var route=new VoidRouteRun(new[]{
                new VoidRouteNode("abyss","Abyss",0,1,"","","","","monochrome-court"),
                new VoidRouteNode("monochrome-court","Monochrome Court",1,1,"","","","", "red-nebula"),
                new VoidRouteNode("red-nebula","Red Nebula",2,1,"","","","","")},"abyss");
            Set(runtime,"_voidRoute",route); Set(runtime,"_time",777.5f); Set(runtime,"_xpNeed",1000000);
            Call(runtime,"PrepareArenaNeighborhood");
            var stages=new List<Stage>();
            yield return Measure(runtime,"abyss-warm",8f,stages,2f);
            Call(runtime,"OnVoidObjectiveCompleted"); Set(runtime,"_voidCompletionDelayRemaining",0f);
            Call(runtime,"StepVoidCompletionDelay",0f);
            yield return Measure(runtime,"court-transition",5f,stages,0f);
            yield return Measure(runtime,"court-active",10f,stages,0f);
            runtime.enabled=false;
            yield return Measure(runtime,"court-render-only-frozen",7f,stages,1f);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-vfprofile-bisect")>=0)
            {
                var enabled=new List<Renderer>();
                foreach(var view in runtime.GetComponentsInChildren<Renderer>())
                    if(view.enabled && view.gameObject.activeInHierarchy)enabled.Add(view);
                Debug.Log("COURTPERF BISECT count="+enabled.Count);
                foreach(var view in enabled)view.enabled=false;
                yield return Measure(runtime,"all-world-renderers-off",3f,stages,.7f);
                var candidates=new List<Renderer>(enabled);
                var round=0;
                while(candidates.Count>1 && round<12)
                {
                    foreach(var view in enabled)view.enabled=false;
                    var half=(candidates.Count+1)/2;
                    for(var i=0;i<half;i++)candidates[i].enabled=true;
                    yield return Measure(runtime,"bisect-"+round+"-first-"+half,3f,stages,.7f);
                    var cost=stages[stages.Count-1].medianFrameMs;
                    if(cost>25)candidates.RemoveRange(half,candidates.Count-half);
                    else candidates.RemoveRange(0,half);
                    round++;
                }
                foreach(var view in enabled)view.enabled=false;
                foreach(var view in candidates)
                {
                    view.enabled=true;
                    Debug.Log("COURTPERF CULPRIT "+view.name+" type="+view.GetType().Name+" shader="+view.sharedMaterial?.shader?.name+" bounds="+view.bounds.size);
                    if(view is LineRenderer line)Debug.Log("COURTPERF LINE points="+line.positionCount+" widths="+line.startWidth+","+line.endWidth);
                    if(view is MeshRenderer mesh)Debug.Log("COURTPERF MESH verts="+mesh.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount);
                }
                yield return Measure(runtime,"culprit-only",4f,stages,.8f);
                foreach(var view in enabled)view.enabled=!candidates.Contains(view);
                yield return Measure(runtime,"everything-except-culprit",4f,stages,.8f);
                foreach(var view in enabled)view.enabled=true;
                if(candidates.Count==1 && candidates[0] is SpriteRenderer culprit)
                {
                    var original=culprit.sprite;
                    Debug.Log("COURTPERF ORIGINAL SPRITE verts="+original.vertices.Length+" indices="+original.triangles.Length+" packed="+original.packed+" ppu="+original.pixelsPerUnit+" rect="+original.rect);
                    var quad=Sprite.Create(original.texture,original.rect,
                        new Vector2(original.pivot.x/original.rect.width,original.pivot.y/original.rect.height),
                        original.pixelsPerUnit,0,SpriteMeshType.FullRect);
                    culprit.sprite=quad;
                    yield return Measure(runtime,"same-texture-full-rect-mesh",5f,stages,.8f);
                    culprit.sprite=original;
                    yield return Measure(runtime,"original-sprite-restored",4f,stages,.8f);
                    culprit.sprite=quad;
                    var details=(Sprite[])Get(runtime,"_arenaPlateDetailSprites"); details[(int)ArenaId.MonochromeCourt]=quad;
                    runtime.enabled=true;
                    yield return Measure(runtime,"full-rect-active-gameplay",7f,stages,1f);
                }
                File.WriteAllText(output,JsonUtility.ToJson(new Report{gpu=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,vramMB=SystemInfo.graphicsMemorySize,stages=stages.ToArray()},true));
                Set(runtime,"_runSaved",true); Debug.Log("COURTPERF BISECT COMPLETE");Application.Quit(0);yield break;
            }
            var splits=(SpriteRenderer[])Get(runtime,"_courtSplitViews");
            foreach(var view in runtime.GetComponentsInChildren<SpriteRenderer>())
                if(view.enabled && view.bounds.size.sqrMagnitude>90000)
                    Debug.Log("COURTPERF SURFACE " + view.name + " bounds=" + view.bounds.size + " shader=" + view.sharedMaterial?.shader?.name + " texture=" + view.sprite?.texture?.width + "x" + view.sprite?.texture?.height);
            foreach(var view in splits)if(view!=null)view.enabled=false;
            yield return Measure(runtime,"court-no-split-planes",7f,stages,1f);
            foreach(var view in splits)if(view!=null)view.enabled=true;
            yield return Measure(runtime,"court-split-planes-restored",7f,stages,1f);
            var perimeter=(Behaviour)Get(runtime,"_musicPerimeter");
            perimeter.enabled=false;
            yield return Measure(runtime,"court-no-music-perimeter",7f,stages,1f);
            perimeter.enabled=true;
            yield return Measure(runtime,"court-music-perimeter-restored",7f,stages,1f);
            var hud=(Canvas)Get(runtime,"_canvas");
            hud.enabled=false;
            yield return Measure(runtime,"court-no-hud",7f,stages,1f);
            hud.enabled=true;
            runtime.enabled=true;
            var camera=(Camera)Get(runtime,"_camera");
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            yield return Measure(runtime,"court-no-post",7f,stages,1f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            profile.settings.resolutionWidth=960; profile.settings.resolutionHeight=540;
            Call(runtime,"ApplyVideoSettings");
            yield return Measure(runtime,"court-half-resolution",7f,stages,2f);
            profile.settings.resolutionWidth=1920; profile.settings.resolutionHeight=1080;
            Call(runtime,"ApplyVideoSettings");
            yield return Measure(runtime,"court-full-resolution-again",7f,stages,2f);
            var report=new Report { gpu=SystemInfo.graphicsDeviceName, cpu=SystemInfo.processorType,
                vramMB=SystemInfo.graphicsMemorySize, stages=stages.ToArray() };
            File.WriteAllText(output,JsonUtility.ToJson(report,true));
            Set(runtime,"_runSaved",true); Debug.Log("COURTPERF COMPLETE " + output); Application.Quit(0);
        }
        private static IEnumerator Measure(VoidFallGameRuntime runtime,string name,float seconds,List<Stage> results,float warm)
        {
            var frames=new List<double>(); var stage=new Stage{name=name};
            var timings=new FrameTiming[1]; var begin=Time.realtimeSinceStartupAsDouble;
            var gc=GC.CollectionCount(0);
            while(Time.realtimeSinceStartupAsDouble-begin < seconds)
            {
                Call(runtime,"SetApplicationActive",true);
                if(!(bool)Get(runtime,"_rouletteActive") && !(bool)Get(runtime,"_levelUpActive")) Set(runtime,"_paused",false);
                var sim=Get(runtime,"_gameSim"); var playerField=sim.GetType().GetField("Player",Flags); var player=playerField.GetValue(sim);
                player.GetType().GetField("Iframes",Flags).SetValue(player,10000f); playerField.SetValue(sim,player);
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                if(Time.realtimeSinceStartupAsDouble-begin < warm)continue;
                var ms=Time.unscaledDeltaTime*1000d; frames.Add(ms);
                if(runtime.enabled)
                {
                    stage.cpuUpdateMs+=Phase("update-total"); stage.cpuRenderMs+=Phase("render");
                    stage.cpuArenaMs+=Phase("arena-render"); stage.cpuCourtMs+=Phase("court-render"); stage.cpuSimMs+=Phase("simulation");
                }
                if(FrameTimingManager.GetLatestTimings(1,timings)>0 && timings[0].gpuFrameTime>0)
                { stage.gpuSamples++; stage.gpuFrameMs+=timings[0].gpuFrameTime; stage.timingCpuFrameMs+=timings[0].cpuFrameTime; }
            }
            frames.Sort(); stage.frames=frames.Count; stage.width=Screen.width; stage.height=Screen.height;
            foreach(var ms in frames)stage.meanFrameMs+=ms;
            var count=Math.Max(1,frames.Count); stage.meanFrameMs/=count;
            if(frames.Count>0){stage.medianFrameMs=frames[frames.Count/2]; stage.p95FrameMs=frames[(int)((frames.Count-1)*.95)]; stage.maxFrameMs=frames[frames.Count-1];}
            stage.cpuUpdateMs/=count;stage.cpuRenderMs/=count;stage.cpuArenaMs/=count;stage.cpuCourtMs/=count;stage.cpuSimMs/=count;
            stage.gpuFrameMs/=Math.Max(1,stage.gpuSamples); stage.timingCpuFrameMs/=Math.Max(1,stage.gpuSamples);
            stage.enemies=runtime.ActiveEnemiesCount;stage.packages=((ArenaResidencyManager)Get(runtime,"_arenaResidency")).Count;
            stage.gcCollections=GC.CollectionCount(0)-gc;stage.managedBytes=GC.GetTotalMemory(false);stage.graphicsBytes=Profiler.GetAllocatedMemoryForGraphicsDriver();
            results.Add(stage); Debug.Log("COURTPERF " + JsonUtility.ToJson(stage));
        }
    }
}
