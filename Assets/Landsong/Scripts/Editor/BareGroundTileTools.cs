using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GiantGrey.TileWorldCreator;
using GiantGrey.TileWorldCreator.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.EditorTools
{
    public static class BareGroundTileTools
    {
        const string Root="Assets/TileWorldCreator/Tiles URP/MyTile";
        static readonly string[] Models={"无草Cornerl","无草InteriorCorner","无草Edge","无草Fill","无草MergedCorner"};
        static readonly string[] Names={"拐角","三边","边","中心","对角"};
        static GameObject[] Slots(TilePreset p)=>new[]{p.DUALGRD_cornerTile,p.DUALGRD_invertedCornerTile,p.DUALGRD_edgeTile,p.DUALGRD_fillTile,p.DUALGRD_doubleInteriorCornerTile};
        [MenuItem("Landsong/地图/配置无草土地与石头地")]
        static void Menu()=>Debug.Log(Configure());
        public static string Configure()
        {
            var report=new StringBuilder();
            var reference=AssetDatabase.LoadAssetAtPath<TilePreset>(Root+"/My陆地.asset");
            if(reference==null)throw new InvalidOperationException("Missing My陆地 reference");
            var assets=Models.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Mesh_无草/"+n+".fbx")).ToArray();
            if(assets.Any(a=>a==null))throw new InvalidOperationException("无草网格未导入完整。");
            var fill=UnityEngine.Object.Instantiate(assets[3]);float scale;
            try
            {
                fill.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);fill.transform.localScale=Vector3.one;
                var vertices=fill.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v))).ToArray();
                float width=vertices.Max(v=>v.x)-vertices.Min(v=>v.x),depth=vertices.Max(v=>v.z)-vertices.Min(v=>v.z);
                if(width<=0 || Mathf.Abs(width-depth)>.001f)throw new InvalidOperationException("无草中心不是正方形。");
                scale=1/width;
                if(vertices.Any(v=>Mathf.Abs(v.y*scale-.5f)>.0001f))throw new InvalidOperationException("无草中心无法按现有 Y=0.5 界面对齐。");
            }
            finally{UnityEngine.Object.DestroyImmediate(fill);}
            foreach(string model in Models)
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/Mesh_无草/"+model+".fbx");
                if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            }
            foreach(var set in new[]{("土地","棕土色"),("石头地","石头灰")})
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Landsong/Art/Shared/Materials/"+set.Item2+".mat");
                var preset=AssetDatabase.LoadAssetAtPath<TilePreset>(Root+"/My"+set.Item1+".asset");
                if(material==null || preset==null)throw new InvalidOperationException("Missing material or preset: "+set.Item1);
                string folder=Root+"/Prefabs/"+set.Item1;
                if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(Root+"/Prefabs",set.Item1);
                var prefabs=new GameObject[5];
                for(int i=0;i<Models.Length;i++)
                {
                    var wrapper=new GameObject(Names[i]);
                    try
                    {
                        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Mesh_无草/"+Models[i]+".fbx");
                        var child=(GameObject)PrefabUtility.InstantiatePrefab(source,wrapper.transform);
                        child.transform.localPosition=Vector3.zero;child.transform.localRotation=Quaternion.identity;child.transform.localScale=Vector3.one*scale;
                        foreach(var renderer in child.GetComponentsInChildren<MeshRenderer>())
                            renderer.sharedMaterials=Enumerable.Repeat(material,renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount).ToArray();
                        var points=child.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v))).ToArray();
                        var existing=UnityEngine.Object.Instantiate(Slots(reference)[i]);
                        float error;
                        try
                        {
                            var original=existing.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v))).ToArray();
                            error=points.Max(p=>original.Min(q=>(p-q).magnitude));
                        }
                        finally{UnityEngine.Object.DestroyImmediate(existing);}
                        // Allow small authored differences in the bare exports while checking orientation.
                        if(error>.025f)throw new InvalidOperationException(Models[i]+" 与原陆地网格朝向/位置不一致，误差="+error);
                        prefabs[i]=PrefabUtility.SaveAsPrefabAsset(wrapper,folder+"/"+Names[i]+".prefab");
                        report.AppendLine("PASS "+set.Item1+"/"+Names[i]+" <- "+Models[i]+"; material="+material.name+"; scale="+scale+"; alignment error="+error.ToString("G4"));
                    }
                    finally{UnityEngine.Object.DestroyImmediate(wrapper);}
                }
                preset.gridtype=TilePreset.GridType.dual;
                preset.DUALGRD_cornerTile=prefabs[0];preset.DUALGRD_invertedCornerTile=prefabs[1];preset.DUALGRD_edgeTile=prefabs[2];preset.DUALGRD_fillTile=prefabs[3];preset.DUALGRD_doubleInteriorCornerTile=prefabs[4];
                preset.cornerTileYRotationOffset=reference.cornerTileYRotationOffset;preset.invertedCornerTileYRotationOffset=reference.invertedCornerTileYRotationOffset;
                preset.edgeTileYRotationOffset=reference.edgeTileYRotationOffset;preset.fillTileYRotationOffset=reference.fillTileYRotationOffset;preset.doubleInteriorCornerTileYRotationOffset=reference.doubleInteriorCornerTileYRotationOffset;
                var data=new SerializedObject(preset);data.FindProperty("materialOverride").objectReferenceValue=null;data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(preset);AssetDatabase.SaveAssetIfDirty(preset);
                if(Slots(preset).Any(p=>p==null))throw new InvalidOperationException("Missing slot: "+set.Item1);
            }
            Directory.CreateDirectory("Library/LandsongEcs");File.WriteAllText("Library/LandsongEcs/bare-ground-verification.txt",report.ToString());return report.ToString();
        }
        [MenuItem("Landsong/地图/预览无草土地与石头地")]
        static void PreviewMenu()=>Debug.Log(Preview());
        public static string Preview()
        {
            var scene=EditorSceneManager.NewPreviewScene();var owned=new List<UnityEngine.Object>();
            T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>();owned.Add(a);return a; }
            RenderTexture rt=null;Texture2D pixels=null;Camera camera=null;var old=RenderTexture.active;var oldRandom=UnityEngine.Random.state;
            try
            {
                var config=Asset<Configuration>();config.width=18;config.height=9;config.cellSize=config.lastCellSize=1;config.cellSizeOld=0;config.clusterCellSize=8;
                config.mergeTiles=false;config.useGlobalRandomSeed=true;config.globalRandomSeed=714;
                config.blueprintLayerFolders.Clear();config.buildLayerFolders.Clear();
                config.blueprintLayerFolders.Add(new BlueprintLayerFolder("Preview"));config.buildLayerFolders.Add(new BuildLayerFolder("Preview"));
                var presets=new[]{"土地","石头地"}.Select(n=>AssetDatabase.LoadAssetAtPath<TilePreset>(Root+"/My"+n+".asset")).ToArray();
                var builds=new List<TilesBuildLayer>();
                for(int index=0;index<2;index++)
                {
                    var bp=Asset<BlueprintLayer>();bp.name=bp.layerName=presets[index].name;
                    var data=new SerializedObject(bp);data.FindProperty("configuration").objectReferenceValue=config;data.ApplyModifiedPropertiesWithoutUndo();
                    var points=new HashSet<Vector2>();
                    for(int x=1;x<=5;x++)for(int z=1;z<=5;z++)if(x!=3 || z!=3)points.Add(new Vector2(x+index*9,z));
                    points.Add(new Vector2(6+index*9,6));bp.AddCells(points);
                    config.blueprintLayerFolders[0].blueprintLayers.Add(bp);config.blueprintLayerFolders[0].assignedBlueprintLayers.Add(bp.guid);
                    var build=Asset<TilesBuildLayer>();build.layerName=bp.layerName;build.asset=build.configuration=config;build.assignedBlueprintLayerGuid=bp.guid;
                    build.useDualGrid=true;build.scaleTileToCellSize=true;build.meshGenerationOverride=true;build.mergeTiles=false;build.SetNewTilePreset(presets[index]);
                    build.tileLayers.Add(new TilesBuildLayer.TileLayers{name="Surface"});config.buildLayerFolders[0].buildLayers.Add(build);builds.Add(build);
                }
                var go=new GameObject("无草地形预览");SceneManager.MoveGameObjectToScene(go,scene);var manager=go.AddComponent<TileWorldCreatorManager>();manager.configuration=config;
                EditorCoroutines.RunToCompletion(()=>{manager.ExecuteBlueprintLayers();manager.ExecuteBuildLayers(ExecutionMode.FromScratch);});
                var report=new StringBuilder();
                for(int i=0;i<2;i++)
                {
                    var renderers=builds[i].LayerObject.GetComponentsInChildren<MeshRenderer>();
                    if(renderers.Length==0 || renderers.Any(r=>r.sharedMaterials.Any(m=>m==null)))throw new InvalidOperationException("TWC 空输出或缺少材质。");
                    var meshes=builds[i].LayerObject.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToHashSet();
                    foreach(var prefab in Slots(presets[i]))
                        if(!prefab.GetComponentsInChildren<MeshFilter>().All(f=>meshes.Contains(f.sharedMesh)))throw new InvalidOperationException("预览未覆盖全部五种瓦片："+prefab.name);
                    report.AppendLine("PASS "+presets[i].name+": actual TWC build; all five tile meshes used; "+renderers.Length+" renderers; materials valid.");
                }
                var cam=new GameObject("Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cam,scene);camera=cam.GetComponent<Camera>();camera.scene=scene;
                camera.orthographic=true;camera.orthographicSize=6.7f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.11f,.14f);
                camera.transform.position=new Vector3(16,15,-10);camera.transform.LookAt(new Vector3(8,0,3.5f));
                foreach(var angle in new[]{new Vector3(45,-25,0),new Vector3(35,145,0)})
                { var lamp=new GameObject("Light",typeof(Light));SceneManager.MoveGameObjectToScene(lamp,scene);lamp.transform.eulerAngles=angle;var light=lamp.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1; }
                rt=new RenderTexture(1600,950,24);rt.Create();camera.targetTexture=rt;pixels=new Texture2D(1600,950,TextureFormat.RGBA32,false);
                void Render(string path){camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1600,950),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
                Directory.CreateDirectory("ArtSource/BareGroundTiles");Render("ArtSource/BareGroundTiles/preview.png");
                if(!AssetDatabase.IsValidFolder(Root+"/Previews"))AssetDatabase.CreateFolder(Root,"Previews");
                for(int i=0;i<2;i++)
                {
                    for(int j=0;j<2;j++)builds[j].LayerObject.SetActive(i==j);
                    var target=new Vector3(3.5f+i*9,0,3.5f);camera.orthographicSize=4.5f;camera.transform.position=target+new Vector3(7,10,-9);camera.transform.LookAt(target);
                    string path=Root+"/Previews/"+presets[i].name+".png";Render(path);AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.SaveAndReimport();
                    presets[i].previewThumbnail=AssetDatabase.LoadAssetAtPath<Texture2D>(path);EditorUtility.SetDirty(presets[i]);AssetDatabase.SaveAssetIfDirty(presets[i]);
                }
                File.AppendAllText("Library/LandsongEcs/bare-ground-verification.txt",report.ToString());return report.ToString();
            }
            finally
            {
                UnityEngine.Random.state=oldRandom;if(camera!=null)camera.targetTexture=null;RenderTexture.active=old;
                if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                EditorSceneManager.ClosePreviewScene(scene);foreach(var o in owned.AsEnumerable().Reverse())if(o!=null)UnityEngine.Object.DestroyImmediate(o);
            }
        }
        public static string Inspect()
        {
            var report=new StringBuilder();
            foreach(string name in Models)
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Mesh_无草/"+name+".fbx");
                if(asset==null)throw new InvalidOperationException("Missing "+name);
                var instance=UnityEngine.Object.Instantiate(asset);
                try
                {
                    report.AppendLine(name+" root position="+instance.transform.position+" scale="+instance.transform.localScale+" rotation="+instance.transform.eulerAngles);
                    foreach(var f in instance.GetComponentsInChildren<MeshFilter>())
                    {
                        var points=f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)).ToArray();
                        var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
                        report.AppendLine("  "+f.name+" vertices="+points.Length+" bounds="+bounds.min.ToString("F5")+" .. "+bounds.max.ToString("F5")+" materials="+string.Join(",",f.GetComponent<Renderer>().sharedMaterials.Select(m=>m==null?"null":m.name+" ("+AssetDatabase.GetAssetPath(m)+")")));
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(instance);}
            }
            Directory.CreateDirectory("Library/LandsongEcs");File.WriteAllText("Library/LandsongEcs/bare-ground-inspection.txt",report.ToString());return report.ToString();
        }
    }
}
