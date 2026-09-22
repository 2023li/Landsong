using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GiantGrey.TileWorldCreator;
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
    public static class NavigationLabBuilder
    {
        public const string Folder = "Assets/Landsong/GameMaps/Map_NavigationLab";
        public const string ScenePath = Folder + "/Map_NavigationLab.unity";
        public const string Data = Folder + "/Map_NavigationLabData";
        public const string SourcePath = Data + "/Source/Map_NavigationLab_TWC配置.asset";
        [MenuItem("Landsong/地图验证/打开多高度导航实验")]
        public static void Open()
        {
            if (!File.Exists(ScenePath))
                Create();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在编辑模式创建实验场景。");
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("实验场景已存在，不自动覆盖。");
            GameMapPaths.Folder(Data + "/Source");
            GameMapPaths.Folder(Data + "/Generated");
            var config = AssetDatabase.LoadAssetAtPath<Configuration>(SourcePath);
            if (config == null)
                config = CreateSource();
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("多高度导航实验（独立运行）");
                var lab = root.AddComponent<NavigationLab>();
                lab.TwcConfiguration = config;
                var source = new GameObject("TWC蓝图源（浮点高度）");
                source.transform.SetParent(root.transform);
                source.transform.position = new Vector3(.25f, 0, .25f);
                source.AddComponent<TileWorldCreatorManager>().configuration = config;
                lab.Geometry = new GameObject("验证几何（由TWC蓝图生成）").transform;
                lab.Geometry.SetParent(root.transform);
                BuildGeometry(lab, config);
                var endpoints = new GameObject("路径起终点").transform;
                endpoints.SetParent(root.transform);
                lab.Starts = new Transform[3];
                lab.Ends = new Transform[3];
                lab.Probes = new NavMeshAgent[3];
                lab.Routes = new LineRenderer[3];
                Vector3[] starts =
                {
                    new Vector3(9, 5.15f, 14),
                    new Vector3(16, .35f, 4),
                    new Vector3(6.5f, .35f, 1)
                };
                Vector3[] ends =
                {
                    new Vector3(23, 5.15f, 14),
                    new Vector3(16, .35f, 24),
                    new Vector3(6.5f, 5.15f, 12)
                };
                string[] names =
                {
                    "桥面",
                    "桥下",
                    "阶梯"
                };
                Color[] colors =
                {
                    new Color(.12f, .9f, 1),
                    new Color(1, .24f, .22f),
                    new Color(1, .76f, .12f)
                };
                for (int i = 0; i < 3; i++)
                {
                    var mat = Material("Route" + i, colors[i], true);
                    lab.Starts[i] = Marker(endpoints, names[i] + "起点", starts[i], mat);
                    lab.Ends[i] = Marker(endpoints, names[i] + "终点", ends[i], mat);
                    var line = new GameObject(names[i] + "实际导航路径").AddComponent<LineRenderer>();
                    line.transform.SetParent(endpoints);
                    line.sharedMaterial = mat;
                    line.widthMultiplier = .09f;
                    line.useWorldSpace = true;
                    line.positionCount = 0;
                    line.numCornerVertices = 3;
                    lab.Routes[i] = line;
                    var actor = new GameObject(names[i] + "测试单位");
                    actor.transform.SetParent(root.transform);
                    actor.transform.position = starts[i];
                    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "显示";
                    body.transform.SetParent(actor.transform, false);
                    body.transform.localPosition = new Vector3(0, .6f, 0);
                    body.transform.localScale = new Vector3(.44f, .6f, .44f);
                    Object.DestroyImmediate(body.GetComponent<Collider>());
                    body.GetComponent<Renderer>().sharedMaterial = mat;
                    var agent = actor.AddComponent<NavMeshAgent>();
                    agent.enabled = false;
                    agent.radius = lab.AgentRadius;
                    agent.height = lab.AgentHeight;
                    agent.speed = 3.3f;
                    agent.acceleration = 16;
                    agent.angularSpeed = 360;
                    agent.stoppingDistance = .05f;
                    agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                    agent.autoBraking = false;
                    lab.Probes[i] = agent;
                    actor.SetActive(false);
                }

                var camera = new GameObject("实验相机").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 20;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 150;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.055f, .08f, .11f);
                camera.transform.rotation = Quaternion.Euler(48, 35, 0);
                camera.transform.position = new Vector3(16, 1.8f, 12.5f) - camera.transform.forward * 44;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.gameObject.AddComponent<AudioListener>();
                lab.View = camera;
                var light = new GameObject("实验主光").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(55, -30, 0);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.55f, .61f, .68f);
                RenderSettings.fog = false;
                Label(root.transform, "WEST  /  5.15", new Vector3(8, 5.2f, 17), .25f);
                Label(root.transform, "EAST  /  5.15", new Vector3(24, 5.2f, 17), .25f);
                Label(root.transform, "UNDERPASS  /  0.35", new Vector3(16, .4f, 23), .22f);
                Label(root.transform, "16 STEPS  /  +0.30", new Vector3(4, .5f, 3), .16f);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("验证场景保存失败。");
                AssetDatabase.SaveAssetIfDirty(config);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
            }
        }

        static Configuration CreateSource()
        {
            var config = ScriptableObject.CreateInstance<Configuration>();
            config.name = "Map_NavigationLab_TWC配置";
            config.width = 64;
            config.height = 56;
            config.cellSizeOld = 0;
            config.cellSize = config.lastCellSize = .5f;
            config.mergePreviewTextures = false;
            config.showGizmos = false;
            config.blueprintLayerFolders.Add(new BlueprintLayerFolder("01 地形与范围"));
            config.blueprintLayerFolders.Add(new BlueprintLayerFolder("02 桥面（独立通行面）"));
            config.blueprintLayerFolders.Add(new BlueprintLayerFolder("03 阶梯（浮点高度）"));
            AssetDatabase.CreateAsset(config, SourcePath);
            Layer(config, 0, "Base", new Rect(0, 0, 32, 28), .35f, Color.gray);
            Layer(config, 0, "相机软范围_仅预留", new Rect(2, 2, 28, 24), .35f, Color.white);
            Layer(config, 0, "land_谷底", new Rect(0, 0, 32, 28), .35f, new Color(.22f, .32f, .27f));
            Layer(config, 0, "land_西高地", new Rect(4, 10, 8, 8), 5.15f, new Color(.35f, .47f, .37f));
            Layer(config, 0, "land_东高地", new Rect(20, 10, 8, 8), 5.15f, new Color(.35f, .47f, .37f));
            Layer(config, 1, "bridge_吊桥", new Rect(12, 13, 8, 2), 5.15f, new Color(.6f, .34f, .15f));
            for (int i = 0; i < 16; i++)
                Layer(config, 2, "stairs_" + (i + 1).ToString("00"), new Rect(5, 2 + i * .5f, 3, .5f), .35f + (i + 1) * .3f, new Color(.63f, .59f, .46f));
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            return config;
        }

        static void Layer(Configuration config, int folder, string name, Rect rect, float height, Color color)
        {
            var layer = ScriptableObject.CreateInstance<BlueprintLayer>();
            layer.name = layer.layerName = name;
            layer.defaultLayerHeight = height;
            layer.layerColor = color;
            AssetDatabase.AddObjectToAsset(layer, config);
            var serialized = new SerializedObject(layer);
            serialized.FindProperty("configuration").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var cells = new HashSet<Vector2>();
            for (int z = Mathf.RoundToInt(rect.yMin / config.cellSize); z < Mathf.RoundToInt(rect.yMax / config.cellSize); z++)
                for (int x = Mathf.RoundToInt(rect.xMin / config.cellSize); x < Mathf.RoundToInt(rect.xMax / config.cellSize); x++)
                    cells.Add(new Vector2(x, z));
            layer.AddCells(cells);
            config.blueprintLayerFolders[folder].blueprintLayers.Add(layer);
            config.blueprintLayerFolders[folder].assignedBlueprintLayers.Add(layer.guid);
            EditorUtility.SetDirty(layer);
        }

        public static void BuildGeometry(NavigationLab lab, Configuration config)
        {
            lab.Bridge = new GameObject("可建造_吊桥");
            lab.Bridge.transform.SetParent(lab.Geometry, false);
            lab.Stairs = new GameObject("可建造_阶梯");
            lab.Stairs.transform.SetParent(lab.Geometry, false);
            foreach (var layer in config.blueprintLayerFolders.SelectMany(f => f.blueprintLayers))
            {
                if (!layer.isEnabled || layer.layerName == "Base" || layer.layerName.StartsWith("相机"))
                    continue;
                var positions = layer.allPositions;
                if (positions.Count == 0)
                    throw new InvalidOperationException("实验蓝图为空：" + layer.layerName);
                float minX = positions.Min(p => p.x) * config.cellSize, maxX = (positions.Max(p => p.x) + 1) * config.cellSize;
                float minZ = positions.Min(p => p.y) * config.cellSize, maxZ = (positions.Max(p => p.y) + 1) * config.cellSize;
                if (positions.Count != Mathf.RoundToInt((maxX - minX) * (maxZ - minZ) / (config.cellSize * config.cellSize)))
                    throw new InvalidOperationException("实验几何生成器仅支持填满的矩形层：" + layer.layerName);
                bool bridge = layer.layerName.StartsWith("bridge_"), stairs = layer.layerName.StartsWith("stairs_");
                float thickness = bridge ? .22f : layer.defaultLayerHeight + .65f;
                var parent = bridge ? lab.Bridge.transform : stairs ? lab.Stairs.transform : lab.Geometry;
                var cube = Cube(parent, layer.layerName, new Vector3((minX + maxX) * .5f, layer.defaultLayerHeight - thickness * .5f, (minZ + maxZ) * .5f), new Vector3(maxX - minX, thickness, maxZ - minZ), Material(bridge ? "Bridge" : stairs ? "Stairs" : layer.layerName, layer.layerColor));
                cube.AddComponent<NavigationLabSurface>().NavigationArea = bridge ? 3 : stairs ? 4 : 0;
                if (bridge)
                {
                    var rope = Material("Rope", new Color(.8f, .7f, .48f));
                    for (int side = 0; side < 2; side++)
                    {
                        float z = side == 0 ? minZ + .08f : maxZ - .08f;
                        for (float x = minX; x <= maxX + .01f; x += 2)
                            Decoration(parent, "吊桥栏柱", new Vector3(x, layer.defaultLayerHeight + .5f, z), new Vector3(.09f, 1, .09f), rope);
                        Decoration(parent, "吊桥扶索", new Vector3((minX + maxX) * .5f, layer.defaultLayerHeight + .92f, z), new Vector3(maxX - minX, .06f, .06f), rope);
                    }

                    for (float x = minX + .1f; x < maxX; x += .4f)
                        Decoration(parent, "桥面木板", new Vector3(x, layer.defaultLayerHeight + .015f, (minZ + maxZ) * .5f), new Vector3(.34f, .025f, maxZ - minZ), rope);
                }
            }
        }

        static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        static void Decoration(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var item = Cube(parent, name, position, size, material);
            Object.DestroyImmediate(item.GetComponent<Collider>());
        }

        static Transform Marker(Transform parent, string name, Vector3 point, Material material)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent);
            marker.position = point;
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "位置标记";
            disc.transform.SetParent(marker, false);
            disc.transform.localPosition = new Vector3(0, .025f, 0);
            disc.transform.localScale = new Vector3(.6f, .025f, .6f);
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.GetComponent<Renderer>().sharedMaterial = material;
            return marker;
        }

        static void Label(Transform parent, string text, Vector3 position, float size)
        {
            var item = new GameObject(text).AddComponent<TextMesh>();
            item.transform.SetParent(parent);
            item.transform.position = position;
            item.transform.rotation = Quaternion.Euler(90, 0, 0);
            item.text = text;
            item.fontSize = 48;
            item.characterSize = size;
            item.anchor = TextAnchor.MiddleCenter;
            item.color = new Color(.85f, .93f, .88f);
        }

        static Material Material(string name, Color color, bool unlit = false)
        {
            string path = Data + "/Generated/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"))
            {
                name = name
            };
            material.SetColor("_BaseColor", color);
            if (!unlit)
                material.SetFloat("_Smoothness", .15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }

    [CustomEditor(typeof(NavigationLab))]
    public sealed class NavigationLabInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("独立 NavMesh 实验。直接打开场景并 Play。TWC 蓝图保存在 Data/Source，几何按原始浮点高度生成；不使用正式地图的单层烘焙按钮。", MessageType.Info);
            if (GUILayout.Button("打开 TWC 蓝图"))
                AssetDatabase.OpenAsset(((NavigationLab)target).TwcConfiguration);
            DrawDefaultInspector();
        }
    }
}
