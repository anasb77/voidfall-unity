using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using VoidFall.Core;
using VoidFall.Runtime;
namespace VoidFall.EditorTools
{
    public static class ArenaMeshAuditBuild
    {
        [Serializable] private class Report { public ArenaMeshAuditCatalog.Entry[] entries; }
        private const string CatalogPath="Assets/VoidFall/Resources/ArenaMeshAudit.asset";
        private const string Output="../voidfall-unity/Logs/MonochromeMeshFix";
        public static void Build()
        {
            try
            {
                var entries=new List<ArenaMeshAuditCatalog.Entry>();
                foreach(var arena in ContentOrder.PreparedArenas)
                {
                    var id=ArenaCatalogRules.StableId(arena);
                    var plate=AssetDatabase.LoadAssetAtPath<ArenaPlateAsset>(VoidFall.Editor.ArenaAddressableMigration.PlatePath(arena));
                    if(plate==null)throw new InvalidOperationException("Missing plate: "+id);
                    var legacy=arena==ArenaId.EonSea||arena==ArenaId.Crascendo;
                    Add(entries,id,"base",plate.BaseSprite,!legacy);
                    Add(entries,id,"details",plate.DetailSprite,!legacy);
                    if(plate.EonSeaVisuals!=null)
                    {
                        for(var i=0;i<4;i++)Add(entries,id,"ground-"+i,plate.EonSeaVisuals.Ground(i),true);
                        for(var kind=0;kind<4;kind++)for(var i=0;i<8;i++)Add(entries,id,"ice-"+kind+"-"+i,plate.EonSeaVisuals.Ice(kind,i),true);
                        Add(entries,id,"slippery",plate.EonSeaVisuals.Slippery,true);
                    }
                    if(plate.CrascendoVisuals!=null)
                        for(var i=0;i<3;i++){Add(entries,id,"ground-"+i,plate.CrascendoVisuals.Ground(i),true);Add(entries,id,"wash-"+i,plate.CrascendoVisuals.Wash(i),true);}
                    if(plate.NullCityVisuals!=null)
                    {
                        var v=plate.NullCityVisuals;
                        Add(entries,id,"transit",v.Transit,true);Add(entries,id,"hangar-open",v.HangarOpen,true);Add(entries,id,"hangar-closed",v.HangarClosed,true);
                        Add(entries,id,"traffic",v.Traffic,true);Add(entries,id,"traffic-lockdown",v.TrafficLockdown,true);
                        Add(entries,id,"lcd",v.LcdSurveillance,true);Add(entries,id,"lcd-lockdown",v.LcdLockdown,true);
                    }
                }
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output+"/geometry.json",JsonUtility.ToJson(new Report{entries=entries.ToArray()},true));
                var catalog=AssetDatabase.LoadAssetAtPath<ArenaMeshAuditCatalog>(CatalogPath);
                if(catalog==null){catalog=ScriptableObject.CreateInstance<ArenaMeshAuditCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
                catalog.entries=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
                PlayerSettings.enableFrameTimingStats=true;
                var build=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/SampleScene.unity"},"../CourtPerfPlayer/VoidFall.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
                Debug.Log("ARENA MESH AUDIT BUILD "+build.summary.result+" entries="+entries.Count);
                EditorApplication.Exit(build.summary.result==BuildResult.Succeeded?0:1);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        private static void Add(List<ArenaMeshAuditCatalog.Entry> entries,string arena,string role,Sprite sprite,bool live)
        {
            if(sprite==null)throw new InvalidOperationException("Missing sprite "+arena+"/"+role);
            var path=AssetDatabase.GetAssetPath(sprite);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            var settings=new TextureImporterSettings();if(importer!=null)importer.ReadTextureSettings(settings);
            entries.Add(new ArenaMeshAuditCatalog.Entry{arena=arena,role=role,path=path,live=live,sprite=sprite,
                width=(int)sprite.rect.width,height=(int)sprite.rect.height,vertices=sprite.vertices.Length,triangles=sprite.triangles.Length/3,
                meshType=importer!=null?settings.spriteMeshType.ToString():"runtime"});
            Debug.Log("ARENA MESH "+arena+"/"+role+" vertices="+sprite.vertices.Length+" triangles="+sprite.triangles.Length/3+" live="+live);
        }
    }
}
