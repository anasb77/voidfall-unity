using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace VoidFall.Runtime
{
    public static class ArenaMeshAuditProbe
    {
        [Serializable] private class Measurement { public string arena,role,mode;public bool live;public int triangles,frames;public double medianFrameMs,meanGpuMs; }
        [Serializable] private class Report { public string gpu;public int width,height;public Measurement[] measurements; }
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        public static IEnumerator Run(VoidFallGameRuntime runtime,string output)
        {
            var catalog=Resources.Load<ArenaMeshAuditCatalog>("ArenaMeshAudit");
            if(catalog==null)throw new InvalidOperationException("Missing audit catalogue");
            runtime.enabled=false;Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            var camera=(Camera)runtime.GetType().GetField("_camera",Flags).GetValue(runtime);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=540;
            foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.enabled=false;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            var go=new GameObject("Single map sprite audit");go.layer=30;var renderer=go.AddComponent<SpriteRenderer>();
            var results=new List<Measurement>();
            yield return new WaitForSecondsRealtime(1f);
            foreach(var entry in catalog.entries)
            {
                if (!entry.path.Contains("MonochromeCourt/Details.png")) continue;
                // Fullscreen layers all get an actual GPU test. Small terrain/props
                // need one only if their imported geometry exceeds a trivial mesh.
                if(entry.role!="base"&&entry.role!="details"&&!entry.role.StartsWith("ground-")&&!entry.role.StartsWith("wash-")&&entry.triangles<1000)continue;
                renderer.sprite=entry.sprite;renderer.color=Color.white;
                go.transform.localScale=new Vector3(1920*entry.sprite.pixelsPerUnit/entry.sprite.rect.width,1080*entry.sprite.pixelsPerUnit/entry.sprite.rect.height,1);
                var pivot=entry.sprite.pivot;var rect=entry.sprite.rect;
                go.transform.position=new Vector3((pivot.x/rect.width-.5f)*1920,(pivot.y/rect.height-.5f)*1080,0);
                yield return Measure(entry,"imported",results);
                var quad=Sprite.Create(entry.sprite.texture,rect,new Vector2(pivot.x/rect.width,pivot.y/rect.height),entry.sprite.pixelsPerUnit,0,SpriteMeshType.FullRect);
                renderer.sprite=quad;
                yield return Measure(entry,"rectangle",results);
                renderer.sprite=null;UnityEngine.Object.Destroy(quad);
                File.WriteAllText(output,JsonUtility.ToJson(new Report{gpu=SystemInfo.graphicsDeviceName,width=Screen.width,height=Screen.height,measurements=results.ToArray()},true));
            }
            runtime.GetType().GetField("_runSaved",Flags).SetValue(runtime,true);
            Debug.Log("ARENA MESH AUDIT COMPLETE cases="+results.Count);Application.Quit(0);
        }
        private static IEnumerator Measure(ArenaMeshAuditCatalog.Entry entry,string mode,List<Measurement> results)
        {
            var start=Time.realtimeSinceStartupAsDouble;var frames=new List<double>();var timings=new FrameTiming[1];var gpu=0d;var count=0;
            while(Time.realtimeSinceStartupAsDouble-start<2.2)
            {
                FrameTimingManager.CaptureFrameTimings();yield return null;
                if(Time.realtimeSinceStartupAsDouble-start<.6)continue;
                frames.Add(Time.unscaledDeltaTime*1000d);
                if(FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].gpuFrameTime>0){gpu+=timings[0].gpuFrameTime;count++;}
            }
            frames.Sort();var result=new Measurement{arena=entry.arena,role=entry.role,live=entry.live,mode=mode,triangles=mode=="rectangle"?2:entry.triangles,
                frames=frames.Count,medianFrameMs=frames.Count>0?frames[frames.Count/2]:0,meanGpuMs=count>0?gpu/count:0};
            results.Add(result);Debug.Log("ARENA MESH GPU "+JsonUtility.ToJson(result));
        }
    }
}
