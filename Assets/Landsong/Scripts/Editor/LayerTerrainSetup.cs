using Landsong.ECS;
using System;
using System.Linq;
using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class LayerTerrainSetup
    {
        public const string OverlapPath = "Assets/Landsong/GameMaps/地形覆盖排序.asset";
        public static TerrainOverlapRules EnsureOverlapDefaults()
        {
            var order = AssetDatabase.LoadAssetAtPath<TerrainOverlapRules>(OverlapPath);
            if (order == null)
            {
                order = ScriptableObject.CreateInstance<TerrainOverlapRules>();
                AssetDatabase.CreateAsset(order, OverlapPath);
            }

            return order;
        }

        public static MapTerrainRules EnsureDefaults()
        {
            var rules = AssetDatabase.LoadAssetAtPath<MapTerrainRules>(GameMapPaths.LayeredRules);
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<MapTerrainRules>();
                rules.useLayerBlueprintRules = true;
                rules.elevationWorldStep = 1;
                AssetDatabase.CreateAsset(rules, GameMapPaths.LayeredRules);
            }

            if (rules.overlapRules == null)
            {
                rules.overlapRules = EnsureOverlapDefaults();
                EditorUtility.SetDirty(rules);
                AssetDatabase.SaveAssetIfDirty(rules);
            }

            // Shared semantic rules apply to every Layer; new heights need no duplicate entries.
            if (rules.blueprintRules.Count == 0)
            {
                foreach (var terrain in new[]
                {
                    TerrainType.陆地,
                    TerrainType.水域,
                    TerrainType.石地,
                    TerrainType.泥地,
                    TerrainType.沼泽
                }

                )
                {
                    rules.blueprintRules.Add(new BlueprintTerrainRule { BlueprintLayerName = terrain.ToString(), Terrain = terrain, Buildable = terrain != TerrainType.水域, Traversable = terrain != TerrainType.水域 });
                }

                rules.blueprintRules.Add(new BlueprintTerrainRule { BlueprintLayerName = "斜坡", Kind = BlueprintLogicKind.ProtrudingSlope, Terrain = TerrainType.None, Buildable = false });
                EditorUtility.SetDirty(rules);
                AssetDatabase.SaveAssetIfDirty(rules);
            }

            if (AssetDatabase.LoadAssetAtPath<Configuration>(GameMapPaths.LayeredTemplate) == null)
            {
                var config = ScriptableObject.CreateInstance<Configuration>();
                config.width = config.height = 128;
                config.cellSize = 1;
                config.useGlobalRandomSeed = true;
                config.globalRandomSeed = 1;
                AssetDatabase.CreateAsset(config, GameMapPaths.LayeredTemplate);
                Add(config, 0, "陆地", Preset("My陆地"));
                Add(config, 0, "水域", Preset("My水域"));
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
            }

            return rules;
        }

        static TilePreset Preset(string name) => AssetDatabase.LoadAssetAtPath<TilePreset>("Assets/TileWorldCreator/Tiles URP/MyTile/" + name + ".asset") ?? throw new InvalidOperationException("找不到预设：" + name);
        static void Add(Configuration config, int height, string terrain, TilePreset preset)
        {
            var folder = config.blueprintLayerFolders.FirstOrDefault(f => f.folderName == "Layer" + height);
            if (folder == null)
            {
                folder = new BlueprintLayerFolder("Layer" + height);
                config.blueprintLayerFolders.Add(folder);
            }

            var view = config.buildLayerFolders.FirstOrDefault(f => f.folderName == "LayerView_" + height);
            if (view == null)
            {
                view = new BuildLayerFolder("LayerView_" + height);
                config.buildLayerFolders.Add(view);
            }

            var blueprint = ScriptableObject.CreateInstance<BlueprintLayer>();
            blueprint.layerName = blueprint.name = "L" + height + "_" + terrain;
            blueprint.defaultLayerHeight = height;
            AssetDatabase.AddObjectToAsset(blueprint, config);
            var data = new SerializedObject(blueprint);
            data.FindProperty("configuration").objectReferenceValue = config;
            data.ApplyModifiedPropertiesWithoutUndo();
            folder.blueprintLayers.Add(blueprint);
            folder.assignedBlueprintLayers.Add(blueprint.guid);
            var build = ScriptableObject.CreateInstance<TilesBuildLayer>();
            build.layerName = build.name = "LV" + height + "_" + terrain;
            build.SetBlueprintLayer(blueprint);
            build.SetNewTilePreset(preset);
            build.useDualGrid = true;
            build.tileLayers.Add(new TilesBuildLayer.TileLayers());
            AssetDatabase.AddObjectToAsset(build, config);
            var buildData = new SerializedObject(build);
            buildData.FindProperty("configuration").objectReferenceValue = config;
            buildData.ApplyModifiedPropertiesWithoutUndo();
            view.buildLayers.Add(build);
            EditorUtility.SetDirty(build);
            EditorUtility.SetDirty(blueprint);
        }

        [MenuItem("Landsong/地图/启用当前地图的 Layer 地形规则")]
        public static void ConfigureCurrent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出运行模式。");
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var managers = active.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TileWorldCreatorManager>(true)).ToArray();
            if (managers.Length != 1)
                throw new InvalidOperationException("当前场景需要唯一的 TWC Manager。");
            var manager = managers[0];
            var rules = EnsureDefaults();
            // Validate existing painting before binding; an empty edge mask is allowed in a draft.
            var preview = LayerTerrainCompiler.Compile(manager.configuration, rules);
            var content = manager.GetComponent<MapContentAuthoring>();
            if (content == null)
                content = Undo.AddComponent<MapContentAuthoring>(manager.gameObject);
            Undo.RecordObject(content, "绑定 Layer 地形规则");
            content.TerrainRules = rules;
            content.TwcConfiguration = manager.configuration;
            var navigation = GameMapWorkflow.Child(content, "导航地表与连接");
            if (navigation.GetComponent<MapNavigationAuthoring>() == null)
                Undo.AddComponent<MapNavigationAuthoring>(navigation);
            if (content.BakeProfile != null)
            {
                content.BakeProfile.ApplyContent(content);
                EditorUtility.SetDirty(content.BakeProfile);
                AssetDatabase.SaveAssetIfDirty(content.BakeProfile);
            }

            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssetIfDirty(manager.configuration);
            EditorSceneManager.MarkSceneDirty(active);
            Debug.Log("Layer 规则已绑定：" + preview.Groups.Count + " 个高度组，" + preview.Primary.Count + " 个主地表格，" + preview.Additional.Count + " 个下层导航格。源场景保留未保存状态。", content);
        }
    }
}
