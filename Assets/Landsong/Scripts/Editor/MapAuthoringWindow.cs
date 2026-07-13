#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.GridSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MapAuthoringWindow : EditorWindow
{
    public const string PendingMapIdEditorPref = "Landsong.MapAuthoring.PendingMapId";
    private const string StartScenePath = "Assets/Landsong/Scenes/Start.unity";

    [SerializeField] private MapDataCatalog catalog;
    [SerializeField] private MapDefinition definition;

    [MenuItem("Landsong/地图/地图目录与校验")]
    private static void OpenWindow()
    {
        GetWindow<MapAuthoringWindow>("Landsong 地图");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("地图策划工作流", EditorStyles.boldLabel);
        catalog = (MapDataCatalog)EditorGUILayout.ObjectField("Map Catalog", catalog, typeof(MapDataCatalog), false);
        definition = (MapDefinition)EditorGUILayout.ObjectField("Map Definition", definition, typeof(MapDefinition), false);

        using (new EditorGUI.DisabledScope(definition == null))
        {
            if (GUILayout.Button("打开 Content Scene"))
            {
                OpenContentScene(definition);
            }

            if (GUILayout.Button("校验当前地图"))
            {
                ValidateDefinition(definition, true);
            }

            if (GUILayout.Button("登记到 Catalog"))
            {
                if (catalog == null)
                {
                    Debug.LogError("请先绑定 MapDataCatalog。");
                }
                else
                {
                    catalog.RegisterDefinition(definition);
                    Debug.Log($"已登记地图：{definition.MapId}", definition);
                }
            }

            if (GUILayout.Button("从当前地图开始 Play"))
            {
                if (!ValidateDefinition(definition, true))
                {
                    return;
                }

                EditorPrefs.SetString(PendingMapIdEditorPref, definition.MapId);
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(StartScenePath);
                EditorApplication.isPlaying = true;
            }
        }
    }

    private static void OpenContentScene(MapDefinition map)
    {
        if (!TryGetScenePath(map, out var path))
        {
            Debug.LogError("MapDefinition 没有绑定有效的 Addressable Scene。", map);
            return;
        }

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    private static bool ValidateDefinition(MapDefinition map, bool log)
    {
        var errors = new List<string>();
        if (map == null || !map.IsValid)
        {
            errors.Add("MapDefinition 无效：MapId 或 Content Scene 缺失。");
        }

        if (!TryGetScenePath(map, out var path))
        {
            errors.Add("无法解析 Content Scene 资产路径。");
        }
        else
        {
            var existing = SceneManager.GetSceneByPath(path);
            var openedTemporarily = !existing.IsValid() || !existing.isLoaded;
            var scene = openedTemporarily
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
                : existing;
            ValidateScene(scene, errors);
            if (openedTemporarily)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        if (log)
        {
            if (errors.Count == 0)
            {
                Debug.Log($"地图校验通过：{map.MapId}", map);
            }
            else
            {
                Debug.LogError($"地图校验失败：{map?.name}\n- {string.Join("\n- ", errors)}", map);
            }
        }

        return errors.Count == 0;
    }

    private static void ValidateScene(Scene scene, List<string> errors)
    {
        var contents = new List<MapContentAuthoring>();
        var roots = scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            contents.AddRange(roots[i].GetComponentsInChildren<MapContentAuthoring>(true));
        }

        if (contents.Count != 1)
        {
            errors.Add($"Content Scene 必须且只能有一个 MapContentAuthoring，当前为 {contents.Count}。");
            return;
        }

        var content = contents[0];
        if (!content.TryValidateConfiguration(out var contentError))
        {
            errors.Add(contentError);
            return;
        }

        var unmanagedBuildings = new List<BuildingBase>();
        for (var i = 0; i < roots.Length; i++)
        {
            unmanagedBuildings.AddRange(roots[i].GetComponentsInChildren<BuildingBase>(true));
        }

        for (var i = 0; i < unmanagedBuildings.Count; i++)
        {
            var building = unmanagedBuildings[i];
            if (building != null
                && building.GetComponentInParent<MapContentAuthoring>(true) != content)
            {
                errors.Add($"初始建筑必须位于 MapContentAuthoring 层级下：{building.name}");
            }
        }

        var occupied = new HashSet<GridPosition>();
        var templates = content.GetInitialBuildingTemplates();
        for (var i = 0; i < templates.Length; i++)
        {
            var template = templates[i];
            if (!template.IsValid)
            {
                errors.Add($"初始建筑模板无效：{template.DisplayName}");
                continue;
            }

            if (!template.PreviewAligned)
            {
                errors.Add($"建筑预览与实际占地未对齐，请执行“吸附初始建筑到格子”：{template.DisplayName}");
            }

            foreach (var cell in template.BuildingPrefab.Definition.CreateFootprint(template.Origin).Positions())
            {
                var tileCell = new Vector3Int(cell.X, cell.Y, 0);
                if (!content.BaseTilemap.HasTile(tileCell))
                {
                    errors.Add($"初始建筑越出 Base 地图：{template.DisplayName}，{cell}");
                }

                if (!occupied.Add(cell))
                {
                    errors.Add($"初始建筑相互重叠：{template.DisplayName}，{cell}");
                }
            }
        }
    }

    private static bool TryGetScenePath(MapDefinition map, out string path)
    {
        path = string.Empty;
        if (map?.ContentScene == null)
        {
            return false;
        }

        var sceneAsset = map.ContentScene.editorAsset as SceneAsset;
        if (sceneAsset == null)
        {
            return false;
        }

        path = AssetDatabase.GetAssetPath(sceneAsset);
        return !string.IsNullOrWhiteSpace(path);
    }
}
#endif
