using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GiantGrey.TileWorldCreator;
using GiantGrey.TileWorldCreator.Utilities;
using Landsong.Verification;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Landsong.EditorTools
{
    public static partial class TwcFlowVerification
    {
        public const string Folder = "Assets/Landsong/GameMaps/Map_TWCFlowLab";
        public const string ScenePath = Folder + "/Map_TWCFlowLab.unity";
        public const string Data = Folder + "/Map_TWCFlowLabData";
        public const string ConfigPath = Data + "/Source/Map_TWCFlowLab_TWC配置.asset";
        const string PresetRoot = "Assets/TileWorldCreator/Tiles URP/BaseBlockTiles/";
        [MenuItem("Landsong/地图验证/打开 TWC 实际斜坡验证")]
        public static void Open()
        {
            if (!File.Exists(ScenePath))
                Create();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Landsong/地图验证/重新生成当前 TWC 实验场景")]
        public static void RegenerateCurrent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play。");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("请先打开 Map_TWCFlowLab 实验场景。");
            var lab = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TwcFlowLab>()).Single();
            var manager = lab.Geometry.GetComponent<TileWorldCreatorManager>();
            Generate(manager);
            PersistMeshes(manager);
            SaveConfig(manager.configuration);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("TWC 已重新生成，合并 Mesh 已持久化。请保存实验场景。", lab);
        }

        public static void Create()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("TWC 验证场景已存在，使用重新生成按钮。");
            GameMapPaths.Folder(Data + "/Source");
            GameMapPaths.Folder(Data + "/Generated/Meshes");
            var config = AssetDatabase.LoadAssetAtPath<Configuration>(ConfigPath) ?? CreateConfig();
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("TWC 实际生成结果");
                var manager = root.AddComponent<TileWorldCreatorManager>();
                manager.configuration = config;
                var lab = new GameObject("TWC 导航验证控制器").AddComponent<TwcFlowLab>();
                lab.Geometry = root.transform;
                Generate(manager);
                ConfigureProbes(lab);
                var camera = new GameObject("验证相机").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 17;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 120;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.06f, .08f, .11f);
                camera.transform.rotation = Quaternion.Euler(50, 25, 0);
                camera.transform.position = new Vector3(16, 0, 11) - camera.transform.forward * 40;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.gameObject.AddComponent<AudioListener>();
                lab.View = camera;
                var light = new GameObject("验证主光").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(55, -25, 0);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.6f, .65f, .7f);
                RenderSettings.fog = false;
                PersistMeshes(manager);
                lab.GenerationEvidence = "TWC ExecuteBlueprintLayers + ExecuteBuildLayers(FromScratch); official Green + GreenRamp presets";
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("场景保存失败");
                SaveConfig(config);
                Dump(lab);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
            }
        }

        static Configuration CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<Configuration>();
            config.name = "Map_TWCFlowLab_TWC配置";
            config.width = 32;
            config.height = 24;
            config.cellSizeOld = 0;
            config.cellSize = config.lastCellSize = 1;
            config.clusterCellSize = 8;
            config.mergeTiles = true;
            config.colliderType = Configuration.ColliderType.meshCollider;
            config.useGlobalRandomSeed = true;
            config.globalRandomSeed = 714;
            config.blueprintLayerFolders.Add(new BlueprintLayerFolder("真实瓦片与斜坡"));
            config.buildLayerFolders.Add(new BuildLayerFolder("官方 Tiles Build"));
            AssetDatabase.CreateAsset(config, ConfigPath);
            var ground = AddBlueprint(config, "Base_谷底", -.15f, RectCells(0, 0, 32, 24));
            var hill = AddBlueprint(config, "land_坡顶", .85f, RectCells(7, 8, 9, 9));
            var ramp = AddBlueprint(config, "ramp_南入口", .85f, RectCells(10, 8, 4, 1));
            var raised = RectCells(20, 10, 8, 3);
            raised.UnionWith(RectCells(18, 8, 3, 7));
            raised.UnionWith(RectCells(27, 8, 4, 7));
            var bridge = AddBlueprint(config, "bridge_高架通行面", 2.85f, raised);
            AddTiles(config, ground, "谷底瓦片");
            var high = AddTiles(config, hill, "坡顶与斜坡瓦片");
            high.tileLayers[0].layerOverrides.Add(new TilesBuildLayer.TilePresetOverride { blueprintOverrideLayer = ramp.guid, requiredNeighbourCount = 1, preset = AssetDatabase.LoadAssetAtPath<TilePreset>(PresetRoot + "BaseBlockPresetGreenRamp.asset") });
            AddTiles(config, bridge, "高架瓦片");
            SaveConfig(config);
            return config;
        }

        static HashSet<Vector2> RectCells(int x, int z, int width, int depth)
        {
            var points = new HashSet<Vector2>();
            for (int i = x; i < x + width; i++)
                for (int j = z; j < z + depth; j++)
                    points.Add(new Vector2(i, j));
            return points;
        }

        static BlueprintLayer AddBlueprint(Configuration config, string name, float height, HashSet<Vector2> points)
        {
            var layer = ScriptableObject.CreateInstance<BlueprintLayer>();
            layer.name = layer.layerName = name;
            layer.defaultLayerHeight = height;
            layer.layerColor = new Color(.4f, .7f, .5f);
            AssetDatabase.AddObjectToAsset(layer, config);
            var so = new SerializedObject(layer);
            so.FindProperty("configuration").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            layer.AddCells(points);
            config.blueprintLayerFolders[0].blueprintLayers.Add(layer);
            config.blueprintLayerFolders[0].assignedBlueprintLayers.Add(layer.guid);
            return layer;
        }

        static TilesBuildLayer AddTiles(Configuration config, BlueprintLayer blueprint, string name)
        {
            var layer = ScriptableObject.CreateInstance<TilesBuildLayer>();
            layer.name = layer.layerName = name;
            layer.asset = layer.configuration = config;
            layer.assignedBlueprintLayerGuid = blueprint.guid;
            layer.useDualGrid = true;
            layer.scaleTileToCellSize = true;
            layer.meshGenerationOverride = true;
            layer.mergeTiles = true;
            layer.colliderType = Configuration.ColliderType.meshCollider;
            layer.shadowCastingMode = ShadowCastingMode.On;
            layer.tilePresetsTop.Add(new TilesBuildLayer.TilePresetSelection { preset = AssetDatabase.LoadAssetAtPath<TilePreset>(PresetRoot + "BaseBlockPresetGreen.asset"), weight = 1 });
            layer.tileLayers.Add(new TilesBuildLayer.TileLayers { name = "表面", heightOffset = 0 });
            AssetDatabase.AddObjectToAsset(layer, config);
            config.buildLayerFolders[0].buildLayers.Add(layer);
            return layer;
        }

        static void SaveConfig(Configuration config)
        {
            foreach (var item in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(config)))
                EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssetIfDirty(config);
        }

        static void Generate(TileWorldCreatorManager manager)
        {
            var previousRandom = UnityEngine.Random.state;
            try
            {
                EditorCoroutines.RunToCompletion(() =>
                {
                    manager.ExecuteBlueprintLayers();
                    manager.ExecuteBuildLayers(ExecutionMode.FromScratch);
                });
            }
            finally
            {
                UnityEngine.Random.state = previousRandom;
            }

            Physics.SyncTransforms();
        }

        static void PersistMeshes(TileWorldCreatorManager manager)
        {
            var copies = new Dictionary<Mesh, Mesh>();
            int index = 0;
            Mesh Save(Mesh source)
            {
                if (source == null)
                    throw new InvalidOperationException("TWC 生成了空网格引用");
                if (copies.TryGetValue(source, out var cached))
                    return cached;
                if (AssetDatabase.Contains(source))
                    return source;
                string path = Data + "/Generated/Meshes/TWC_" + (index++).ToString("D3") + ".asset";
                var target = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (target == null)
                {
                    target = Object.Instantiate(source);
                    target.name = Path.GetFileNameWithoutExtension(path);
                    AssetDatabase.CreateAsset(target, path);
                }
                else
                {
                    EditorUtility.CopySerialized(source, target);
                    EditorUtility.SetDirty(target);
                }

                target.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.SaveAssetIfDirty(target);
                copies.Add(source, target);
                return target;
            }

            foreach (var filter in manager.GetComponentsInChildren<MeshFilter>(true))
                filter.sharedMesh = Save(filter.sharedMesh);
            foreach (var collider in manager.GetComponentsInChildren<MeshCollider>(true))
                collider.sharedMesh = Save(collider.sharedMesh);
        }

        static void ConfigureProbes(TwcFlowLab lab)
        {
            Vector3[] starts =
            {
                new Vector3(11.5f, .35f, 5),
                new Vector3(19, 3.35f, 11),
                new Vector3(24, .35f, 5)
            };
            Vector3[] ends =
            {
                new Vector3(11.5f, 1.35f, 12),
                new Vector3(29, 3.35f, 11),
                new Vector3(24, .35f, 18)
            };
            Color[] colors =
            {
                new Color(1, .75f, .08f),
                Color.cyan,
                new Color(1, .2f, .3f)
            };
            string[] names =
            {
                "斜坡",
                "高架",
                "桥下"
            };
            lab.Starts = new Transform[3];
            lab.Ends = new Transform[3];
            lab.Agents = new NavMeshAgent[3];
            lab.Lines = new LineRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.SetColor("_BaseColor", colors[i]);
                AssetDatabase.CreateAsset(mat, Data + "/Generated/Route" + i + ".mat");
                lab.Starts[i] = new GameObject(names[i] + "起点").transform;
                lab.Starts[i].SetParent(lab.transform);
                lab.Starts[i].position = starts[i];
                lab.Ends[i] = new GameObject(names[i] + "终点").transform;
                lab.Ends[i].SetParent(lab.transform);
                lab.Ends[i].position = ends[i];
                var line = new GameObject(names[i] + "导航路径").AddComponent<LineRenderer>();
                line.transform.SetParent(lab.transform);
                line.sharedMaterial = mat;
                line.widthMultiplier = .08f;
                line.positionCount = 0;
                lab.Lines[i] = line;
                var actor = new GameObject(names[i] + "探针");
                actor.transform.SetParent(lab.transform);
                actor.transform.position = starts[i];
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                mesh.transform.SetParent(actor.transform, false);
                mesh.transform.localPosition = Vector3.up * .5f;
                mesh.transform.localScale = new Vector3(.3f, .5f, .3f);
                Object.DestroyImmediate(mesh.GetComponent<Collider>());
                mesh.GetComponent<Renderer>().sharedMaterial = mat;
                var agent = actor.AddComponent<NavMeshAgent>();
                agent.enabled = false;
                agent.radius = .15f;
                agent.height = 1.1f;
                agent.speed = 2;
                agent.acceleration = 12;
                agent.stoppingDistance = .03f;
                agent.autoBraking = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                lab.Agents[i] = agent;
                actor.SetActive(false);
            }
        }

        static void Dump(TwcFlowLab lab)
        {
            var lines = new List<string>();
            foreach (var m in lab.Geometry.GetComponentsInChildren<MeshFilter>())
                lines.Add("Mesh " + m.name + " " + m.sharedMesh.vertexCount + " " + m.sharedMesh.bounds);
            for (float z = 5; z <= 10; z += .25f)
                if (lab.Ground(new Vector3(11.5f, 0, z), out var hit))
                    lines.Add("Ramp z=" + z + " y=" + hit.point.y + " normal=" + hit.normal);
            lab.BuildNavigation();
            for (float x = 9; x <= 14; x += .125f)
                if (lab.Ground(new Vector3(x, 0, 6.9f), out var hit))
                    lines.Add("Seam x=" + x + " y=" + hit.point.y + " normal=" + hit.normal + " nav=" + NavMesh.SamplePosition(hit.point, out var nav, .18f, lab.Filter) + " hit=" + nav.position);
            for (int i = 0; i < 3; i++)
                lines.Add("Path " + i + ": " + lab.Path(lab.Starts[i].position, lab.Ends[i].position, out var path) + " " + string.Join(";", path.corners.Select(p => p.ToString())));
            lab.Release();
            File.WriteAllLines("Library/LandsongEcs/twc-flow-geometry.txt", lines);
        }

        public static void InspectSaved()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Dump(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TwcFlowLab>()).Single());
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
            }
        }
    }
}
