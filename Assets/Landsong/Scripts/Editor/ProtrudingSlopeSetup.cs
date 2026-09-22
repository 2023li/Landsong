using Landsong.ECS;
using System;
using System.Linq;
using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class ProtrudingSlopeSetup
    {
        public const string PresetPath=SlopeAssetTools.Root+"/My斜坡.asset";
        public static string AlignCurrentVisualOffsets()
        {
            if(Application.isPlaying)throw new InvalidOperationException("请先退出运行模式。");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var manager=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TileWorldCreatorManager>(true)).Single();
            var config=manager.configuration;
            var blueprints=config.blueprintLayerFolders.SelectMany(f=>f.blueprintLayers).ToDictionary(b=>b.guid);
            var lands=config.buildLayerFolders.SelectMany(f=>f.buildLayers).OfType<TilesBuildLayer>()
                .Where(b=>b.isEnabled && blueprints.TryGetValue(b.assignedBlueprintLayerGuid,out var bp) && BlueprintRuleNames.Key(bp.layerName)=="陆地").ToArray();
            if(lands.Length==0 || lands.Any(b=>b.placeOnTop || b.scaleOffset!=Vector3.one || b.tileLayers.Count!=1 || b.tileLayers[0].heightOffset!=0) || config.cellSize!=1)
                throw new InvalidOperationException("陆地表现设置不适合自动同步，请在斜坡 Global Offsets 手动设置并检查端点。");
            var offsets=lands.Select(b=>b.layerYOffset).Distinct().ToArray();
            if(offsets.Length!=1 || !float.IsFinite(offsets[0]))throw new InvalidOperationException("上下层陆地偏移不一致，无法统一平移斜坡。");
            var slopes=config.buildLayerFolders.SelectMany(f=>f.buildLayers).OfType<ProtrudingSlopeBuildLayer>().Where(b=>b.isEnabled).ToArray();
            if(slopes.Length==0)throw new InvalidOperationException("当前地图没有启用的 Landsong Slope 构建层。");
            foreach(var layer in slopes)LayerTerrainCompiler.Compile(config,layer.rules);
            Undo.RecordObjects(slopes,"同步斜坡视觉偏移");
            foreach(var layer in slopes){layer.layerYOffset=offsets[0];EditorUtility.SetDirty(layer);}
            EditorUtility.SetDirty(config);
            foreach(var layer in slopes)layer.ExecuteLayer(config,manager.gameObject,manager);
            slopes[0].PostExecuteLayer(config,manager.gameObject,manager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            return string.Join("\n",slopes.Select(b=>b.layerName+" Layer Y Offset="+b.layerYOffset+"；已重建 "+b.LayerObject.transform.childCount+" 个斜坡瓦片。"))+"\n场景和 TWC 配置保留为未保存，逻辑地形与预制体资产未修改。";
        }
        [MenuItem("Landsong/地图/配置突出式斜坡 My斜坡")]
        public static void ConfigureCurrent()
        {
            SlopeAssetTools.ImportAndVerify();
            var preset=AssetDatabase.LoadAssetAtPath<ProtrudingSlopeTilePreset>(PresetPath);
            if(preset==null) { preset=ScriptableObject.CreateInstance<ProtrudingSlopeTilePreset>();AssetDatabase.CreateAsset(preset,PresetPath); }
            preset.Left=AssetDatabase.LoadAssetAtPath<GameObject>(SlopeAssetTools.Root+"/Prefabs/斜坡/Slope_Left.prefab");
            preset.Middle=AssetDatabase.LoadAssetAtPath<GameObject>(SlopeAssetTools.Root+"/Prefabs/斜坡/Slope_Middle.prefab");
            preset.Right=AssetDatabase.LoadAssetAtPath<GameObject>(SlopeAssetTools.Root+"/Prefabs/斜坡/Slope_Right.prefab");
            preset.gridtype=TilePreset.GridType.standard;
            // Preview slots only; the dedicated builder selects end caps using the platform direction.
            preset.NRMGRD_deadEndTile=preset.Left;preset.NRMGRD_edgeWayTile=preset.Middle;preset.NRMGRD_fillTile=preset.Middle;
            EditorUtility.SetDirty(preset);AssetDatabase.SaveAssetIfDirty(preset);
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var managers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TileWorldCreatorManager>(true)).ToArray();
            if(managers.Length!=1) throw new InvalidOperationException("当前地图需要唯一 TWC Manager。");
            var content=managers[0].GetComponent<MapContentAuthoring>();
            if(content==null || content.TerrainRules==null || !content.TerrainRules.useLayerBlueprintRules) throw new InvalidOperationException("请先启用 Layer / Blueprint 规则。");
            Configure(managers[0].configuration,content.TerrainRules,preset);
            if(content.BakeProfile!=null) { content.BakeProfile.ApplyContent(content);EditorUtility.SetDirty(content.BakeProfile);AssetDatabase.SaveAssetIfDirty(content.BakeProfile); }
            LayerTerrainCompiler.Compile(managers[0].configuration,content);
            Debug.Log("My斜坡 已配置。每个 Layer1 及以上新增空的 L<n>_斜坡 Blueprint 和对应 Landsong Slope 构建层；请在平台外侧绘制。",content);
        }
        public static void Configure(Configuration config,MapTerrainRules rules,ProtrudingSlopeTilePreset preset)
        {
            foreach(var folder in config.blueprintLayerFolders.ToArray())
            {
                if(!folder.folderName.StartsWith("Layer") || !int.TryParse(folder.folderName.Substring(5),out int height) || height<1) continue;
                string name="L"+height+"_斜坡";
                var bp=folder.blueprintLayers.SingleOrDefault(b=>b.layerName==name);
                if(bp==null)
                {
                    bp=ScriptableObject.CreateInstance<BlueprintLayer>();bp.name=bp.layerName=name;bp.defaultLayerHeight=height;
                    AssetDatabase.AddObjectToAsset(bp,config);var data=new SerializedObject(bp);data.FindProperty("configuration").objectReferenceValue=config;data.ApplyModifiedPropertiesWithoutUndo();
                    folder.blueprintLayers.Add(bp);folder.assignedBlueprintLayers.Add(bp.guid);EditorUtility.SetDirty(bp);
                }
                var rule=rules.blueprintRules.SingleOrDefault(r=>r.BlueprintLayerName=="斜坡");
                if(rule==null) { rule=new BlueprintTerrainRule{BlueprintLayerName="斜坡"};rules.blueprintRules.Add(rule); }
                rule.Kind=BlueprintLogicKind.ProtrudingSlope;rule.Terrain=TerrainType.None;rule.Buildable=false;rule.Traversable=true;
                var build=config.buildLayerFolders.SelectMany(f=>f.buildLayers).OfType<ProtrudingSlopeBuildLayer>().SingleOrDefault(b=>b.assignedBlueprintLayerGuid==bp.guid);
                if(build==null)
                {
                    var view=config.buildLayerFolders.FirstOrDefault(f=>f.folderName=="LayerView_"+height);
                    if(view==null) { view=new BuildLayerFolder("LayerView_"+height);config.buildLayerFolders.Add(view); }
                    build=ScriptableObject.CreateInstance<ProtrudingSlopeBuildLayer>();build.name=build.layerName="LV"+height+"_斜坡";build.assignedBlueprintLayerGuid=bp.guid;build.asset=config;
                    AssetDatabase.AddObjectToAsset(build,config);view.buildLayers.Add(build);
                }
                build.tileSet=preset;build.rules=rules;EditorUtility.SetDirty(build);
            }
            EditorUtility.SetDirty(rules);EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(rules);AssetDatabase.SaveAssetIfDirty(config);
        }
    }
}
