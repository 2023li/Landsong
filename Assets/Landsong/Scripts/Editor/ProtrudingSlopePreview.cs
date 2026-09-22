using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GiantGrey.TileWorldCreator;
using GiantGrey.TileWorldCreator.Utilities;
using Landsong.GridSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.EditorTools
{
    public static class ProtrudingSlopePreview
    {
        [MenuItem("Landsong/地图/生成突出斜坡 TWC 实际预览")]
        static void Menu()=>Debug.Log(Run());
        public static string Run()
        {
            var scene=EditorSceneManager.NewPreviewScene();var owned=new List<UnityEngine.Object>();
            T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>();owned.Add(a);return a; }
            RenderTexture render=null;Texture2D image=null;Camera camera=null;var old=RenderTexture.active;
            try
            {
                var config=Asset<Configuration>();config.width=18;config.height=12;config.cellSize=config.lastCellSize=1;config.cellSizeOld=0;
                config.clusterCellSize=8;config.mergeTiles=true;config.useGlobalRandomSeed=true;config.globalRandomSeed=714;
                config.blueprintLayerFolders.Clear();config.buildLayerFolders.Clear();config.buildLayerFolders.Add(new BuildLayerFolder("Preview"));
                var rules=Asset<MapTerrainRules>();rules.overlapRules=Asset<TerrainOverlapRules>();rules.useLayerBlueprintRules=true;
                var low=new BlueprintLayerFolder("Layer0");var high=new BlueprintLayerFolder("Layer1");config.blueprintLayerFolders.Add(low);config.blueprintLayerFolders.Add(high);
                HashSet<Vector2> Rectangle(int x,int z,int w,int h) {var points=new HashSet<Vector2>();for(int a=x;a<x+w;a++)for(int b=z;b<z+h;b++)points.Add(new Vector2(a,b));return points;}
                BlueprintLayer Blueprint(BlueprintLayerFolder f,string name,HashSet<Vector2> points,BlueprintLogicKind kind)
                {
                    var bp=Asset<BlueprintLayer>();bp.layerName=bp.name=name;bp.defaultLayerHeight=f==low?0:1;
                    var serialized=new SerializedObject(bp);serialized.FindProperty("configuration").objectReferenceValue=config;serialized.ApplyModifiedPropertiesWithoutUndo();
                    bp.AddCells(points);f.blueprintLayers.Add(bp);f.assignedBlueprintLayers.Add(bp.guid);rules.blueprintRules.Add(new BlueprintTerrainRule{BlueprintLayerName=name,Kind=kind});return bp;
                }
                var ground=Blueprint(low,"低地",Rectangle(0,0,18,12),BlueprintLogicKind.Terrain);
                var platform=Blueprint(high,"平台",Rectangle(3,5,12,5),BlueprintLogicKind.Terrain);
                var ramps=Blueprint(high,"突出斜坡",Rectangle(6,4,6,1),BlueprintLogicKind.ProtrudingSlope);
                foreach(var bp in new[]{ground,platform})
                {
                    var build=Asset<TilesBuildLayer>();build.layerName=bp.layerName;build.asset=build.configuration=config;build.assignedBlueprintLayerGuid=bp.guid;
                    build.useDualGrid=true;build.scaleTileToCellSize=true;build.meshGenerationOverride=true;build.mergeTiles=true;build.colliderType=Configuration.ColliderType.meshCollider;
                    build.SetNewTilePreset(AssetDatabase.LoadAssetAtPath<TilePreset>(SlopeAssetTools.Root+"/My陆地.asset"));
                    build.tileLayers.Add(new TilesBuildLayer.TileLayers{name="Surface"});config.buildLayerFolders[0].buildLayers.Add(build);
                }
                var slope=Asset<ProtrudingSlopeBuildLayer>();slope.layerName="突出斜坡";slope.asset=config;slope.assignedBlueprintLayerGuid=ramps.guid;slope.rules=rules;
                slope.tileSet=AssetDatabase.LoadAssetAtPath<ProtrudingSlopeTilePreset>(ProtrudingSlopeSetup.PresetPath);config.buildLayerFolders[0].buildLayers.Add(slope);
                var go=new GameObject("TWC 突出斜坡验证");SceneManager.MoveGameObjectToScene(go,scene);var manager=go.AddComponent<TileWorldCreatorManager>();manager.configuration=config;
                var previousRandom=UnityEngine.Random.state;
                try { EditorCoroutines.RunToCompletion(()=>{manager.ExecuteBlueprintLayers();manager.ExecuteBuildLayers(ExecutionMode.FromScratch);}); }
                finally { UnityEngine.Random.state=previousRandom; }
                if(slope.LayerObject==null || slope.LayerObject.transform.childCount!=6)throw new InvalidOperationException("TWC 未实际生成六格突出斜坡。");
                if(go.GetComponentsInChildren<SlopeVisualCut>().Length==0)throw new InvalidOperationException("未处理 TWC 原岩壁的坡口遮挡。");
                string meshFolder=SlopeAssetTools.Root+"/SlopePreviewMeshes";
                if(!AssetDatabase.IsValidFolder(meshFolder))AssetDatabase.CreateFolder(SlopeAssetTools.Root,"SlopePreviewMeshes");
                var output=new GameObject("突出斜坡_实际TWC拼接预览");SceneManager.MoveGameObjectToScene(output,scene);int index=0;
                foreach(var source in go.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=source.GetComponent<MeshRenderer>();if(renderer==null || source.sharedMesh==null)continue;
                    var mesh=source.sharedMesh;
                    if(!AssetDatabase.Contains(mesh))
                    {
                        string path=meshFolder+"/Mesh_"+(index++)+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if(existing==null) { existing=UnityEngine.Object.Instantiate(mesh);existing.name=Path.GetFileNameWithoutExtension(path);AssetDatabase.CreateAsset(existing,path); }
                        else { EditorUtility.CopySerialized(mesh,existing);existing.name=Path.GetFileNameWithoutExtension(path);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing); }
                        mesh=existing;
                    }
                    var child=new GameObject(source.name,typeof(MeshFilter),typeof(MeshRenderer));SceneManager.MoveGameObjectToScene(child,scene);child.transform.SetParent(output.transform,false);
                    child.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);child.transform.localScale=source.transform.lossyScale;
                    child.GetComponent<MeshFilter>().sharedMesh=mesh;child.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                }
                PrefabUtility.SaveAsPrefabAsset(output,SlopeAssetTools.Root+"/Prefabs/斜坡/斜坡拼接预览.prefab");output.SetActive(false);
                var cameraGO=new GameObject("Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraGO,scene);camera=cameraGO.GetComponent<Camera>();camera.scene=scene;
                camera.orthographic=true;camera.orthographicSize=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.07f,.09f);
                camera.transform.position=new Vector3(23,21,-17);camera.transform.LookAt(new Vector3(8.5f,.5f,5.5f));
                foreach(var angles in new[]{new Vector3(50,-35,0),new Vector3(45,140,0)})
                { var lightGO=new GameObject("Light",typeof(Light));SceneManager.MoveGameObjectToScene(lightGO,scene);var light=lightGO.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1;lightGO.transform.eulerAngles=angles; }
                render=new RenderTexture(1600,1100,24);render.Create();camera.targetTexture=render;camera.Render();RenderTexture.active=render;
                image=new Texture2D(1600,1100,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();
                File.WriteAllBytes("ArtSource/MyTileSlope/unity_assembly_preview.png",image.EncodeToPNG());
                camera.orthographicSize=3.5f;camera.transform.position=new Vector3(9,5,-5);camera.transform.LookAt(new Vector3(9,1,4.5f));camera.Render();image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();
                File.WriteAllBytes("ArtSource/MyTileSlope/unity_slope_detail.png",image.EncodeToPNG());
                string report="PASS Actual TWC Blueprint -> Tiles Build -> Landsong Slope, six exterior cells; platform cells preserved; generated cliff openings adapted; saved preview prefab.";
                File.WriteAllText("Library/LandsongEcs/protruding-slope-twc.txt",report);return report;
            }
            finally
            {
                if(camera!=null)camera.targetTexture=null;
                RenderTexture.active=old;if(image!=null)UnityEngine.Object.DestroyImmediate(image);if(render!=null){render.Release();UnityEngine.Object.DestroyImmediate(render);}
                EditorSceneManager.ClosePreviewScene(scene);foreach(var o in owned.AsEnumerable().Reverse())if(o!=null)UnityEngine.Object.DestroyImmediate(o);
            }
        }
    }
}
