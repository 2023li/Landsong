using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Landsong.GridSystem
{
    public readonly struct InitialBuildingTemplate
    {
        public InitialBuildingTemplate(
            BuildingBase buildingPrefab,
            GridPosition origin,
            UnityEngine.Object context,
            bool previewAligned)
        {
            BuildingPrefab = buildingPrefab;
            Origin = origin;
            Context = context;
            PreviewAligned = previewAligned;
        }

        public BuildingBase BuildingPrefab { get; }
        public GridPosition Origin { get; }
        public UnityEngine.Object Context { get; }
        public bool PreviewAligned { get; }
        public bool IsValid => BuildingPrefab != null && BuildingPrefab.HasDefinition;
        public string DisplayName => Context == null ? "<null>" : Context.name;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    [AddComponentMenu("Landsong/Map/Map Content Authoring")]
    public sealed class MapContentAuthoring : MonoBehaviour
    {
        [SerializeField, LabelText("地图 Grid")]
        private UnityEngine.Grid unityGrid;

        [SerializeField, LabelText("Base Tilemap")]
        private Tilemap baseTilemap;

        [SerializeField, LabelText("地形 Tilemap 层")]
        private List<GridTerrainLayer> terrainLayers = new List<GridTerrainLayer>();

        [SerializeField, BoxGroup("初始建筑"), LabelText("显示占地 Gizmo")]
        private bool showInitialBuildingFootprints = true;

        [SerializeField, BoxGroup("初始建筑"), LabelText("显示建筑文字"), Tooltip("在 Scene 视图中显示初始建筑的名称、阶段或等级、StyleId，并提示未吸附状态。不会创建建筑美术预览对象。")]
        private bool showInitialBuildingLabels = true;

        [SerializeField, BoxGroup("初始建筑"), LabelText("对齐容差"), Min(0.0001f)]
        private float initialBuildingAlignmentTolerance = 0.01f;

        public UnityEngine.Grid UnityGrid => unityGrid;
        public Tilemap BaseTilemap => baseTilemap;
        public IReadOnlyList<GridTerrainLayer> TerrainLayers => terrainLayers == null
            ? (IReadOnlyList<GridTerrainLayer>)Array.Empty<GridTerrainLayer>()
            : terrainLayers;
        public bool IsValid => TryValidateConfiguration(out _);

        public bool TryValidateConfiguration(out string error)
        {
            error = string.Empty;
            if (unityGrid == null)
            {
                error = "MapContentAuthoring 没有绑定 Unity Grid。";
                return false;
            }

            if (baseTilemap == null || baseTilemap.GetUsedTilesCount() <= 0)
            {
                error = "MapContentAuthoring 的 Base Tilemap 缺失或为空。";
                return false;
            }

            if (!BelongsToBoundGrid(baseTilemap))
            {
                error = "Base Tilemap 不属于 MapContentAuthoring 绑定的 Unity Grid。";
                return false;
            }

            if (terrainLayers == null)
            {
                return true;
            }

            for (var i = 0; i < terrainLayers.Count; i++)
            {
                var layer = terrainLayers[i];
                if (layer == null || !layer.IsValid)
                {
                    error = $"Terrain Layers[{i}] 配置无效。";
                    return false;
                }

                if (!BelongsToBoundGrid(layer.Tilemap))
                {
                    error = $"Terrain Layers[{i}] ({layer.Key}) 不属于 MapContentAuthoring 绑定的 Unity Grid。";
                    return false;
                }
            }

            return true;
        }

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var buildings = GetInitialBuildingObjects();
            for (var i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                if (building != null && building.gameObject.activeSelf)
                {
                    building.gameObject.SetActive(false);
                }
            }
        }

        public InitialBuildingTemplate[] GetInitialBuildingTemplates()
        {
            var buildings = GetInitialBuildingObjects();
            var result = new List<InitialBuildingTemplate>(buildings.Length);
            if (unityGrid == null)
            {
                return result.ToArray();
            }

            var layout = new GridLayoutService(unityGrid);
            for (var i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                if (building == null)
                {
                    continue;
                }

                var size = building.HasDefinition ? building.Definition.Size : Vector2Int.one;
                var origin = GetNearestOrigin(layout, building.transform.position, size);
                var expectedPosition = layout.GridToWorldPoint(
                    origin.X + size.x * 0.5f,
                    origin.Y + size.y * 0.5f);
                var positionAligned = (building.transform.position - expectedPosition).sqrMagnitude
                                      <= initialBuildingAlignmentTolerance * initialBuildingAlignmentTolerance;
                var rotationAligned = Quaternion.Angle(
                                          building.transform.rotation,
                                          Quaternion.identity)
                                      <= 0.01f;
                result.Add(new InitialBuildingTemplate(
                    building,
                    origin,
                    building,
                    positionAligned && rotationAligned));
            }

            return result.ToArray();
        }

        public static bool IsInitialBuildingTemplate(BuildingBase building)
        {
            return building != null
                   && building.GetComponentInParent<MapContentAuthoring>(true) != null;
        }

        [Button("吸附初始建筑到格子")]
        [ContextMenu("吸附初始建筑到格子")]
        public void SnapInitialBuildingsToGrid()
        {
            if (unityGrid == null)
            {
                Debug.LogWarning("MapContentAuthoring 尚未绑定 Grid。", this);
                return;
            }

            var layout = new GridLayoutService(unityGrid);
            var buildings = GetInitialBuildingObjects();
            for (var i = 0; i < buildings.Length; i++)
            {
                var building = buildings[i];
                if (building == null || !building.HasDefinition)
                {
                    continue;
                }

                var size = building.Definition.Size;
                var origin = GetNearestOrigin(layout, building.transform.position, size);
                var position = layout.GridToWorldPoint(
                    origin.X + size.x * 0.5f,
                    origin.Y + size.y * 0.5f);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Undo.RecordObject(building.transform, "Snap Initial Building To Grid");
                }
#endif
                building.transform.position = position;
                building.transform.rotation = Quaternion.identity;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(building.transform);
                    EditorUtility.SetDirty(building.transform);
                }
#endif
            }
        }

        private BuildingBase[] GetInitialBuildingObjects()
        {
            return GetComponentsInChildren<BuildingBase>(true);
        }

#if UNITY_EDITOR
        private static void DrawInitialBuildingLabels(
            IReadOnlyList<InitialBuildingTemplate> templates)
        {
            var normalStyle = CreateInitialBuildingLabelStyle(Color.white);
            var warningStyle = CreateInitialBuildingLabelStyle(new Color(1f, 0.65f, 0.1f));

            for (var i = 0; i < templates.Count; i++)
            {
                var template = templates[i];
                var building = template.Context as BuildingBase;
                if (!template.IsValid || building == null)
                {
                    continue;
                }

                var displayName = template.BuildingPrefab.Definition.DisplayName;
                var stageText = building.Stage == BuildingLifecycleStage.Construction
                    ? "施工"
                    : $"LV{building.CurrentLevel}";
                var styleText = string.IsNullOrWhiteSpace(building.StyleId)
                    ? string.Empty
                    : $" · {building.StyleId}";
                var alignmentText = template.PreviewAligned ? string.Empty : " · 未吸附";
                var content = new GUIContent(
                    $"{displayName}\n{stageText}{styleText}{alignmentText}");
                var labelPosition = building.transform.position
                                    + Vector3.up
                                    * HandleUtility.GetHandleSize(building.transform.position)
                                    * 0.15f;
                Handles.Label(
                    labelPosition,
                    content,
                    template.PreviewAligned ? normalStyle : warningStyle);
            }
        }

        private static GUIStyle CreateInitialBuildingLabelStyle(Color textColor)
        {
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            style.normal.textColor = textColor;
            return style;
        }

        [Button("一键生成基础 Tilemap")]
        [ContextMenu("一键生成基础 Tilemap")]
        private void ConfigureBaseTilemaps()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("请在编辑模式下生成基础 Tilemap。", this);
                return;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure Map Content Tilemaps");
            Undo.RecordObject(this, "Configure Map Content Tilemaps");

            if (!TryResolveOrCreateGrid(out var targetGrid))
            {
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            unityGrid = targetGrid;
            baseTilemap = EnsureTilemapLayer(
                targetGrid,
                baseTilemap,
                "Base Tilemap",
                0);
            var waterTilemap = EnsureTilemapLayer(
                targetGrid,
                GetTerrainLayerTilemap(GridTerrainKeys.Water),
                "水域 Tilemap",
                10);
            var obstacleTilemap = EnsureTilemapLayer(
                targetGrid,
                GetTerrainLayerTilemap(GridTerrainKeys.Obstacle),
                "障碍 Tilemap",
                20);
            var stoneDepositTilemap = EnsureTilemapLayer(
                targetGrid,
                GetTerrainLayerTilemap(GridTerrainKeys.StoneDeposit),
                "石矿 Tilemap",
                15);
            EnsureTerrainLayer(GridTerrainKeys.Water, waterTilemap, true);
            EnsureTerrainLayer(GridTerrainKeys.Obstacle, obstacleTilemap, true);
            EnsureTerrainLayer(GridTerrainKeys.StoneDeposit, stoneDepositTilemap, false);

            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("已生成 Base/水域/石矿/障碍 Tilemap；石矿作为不替换陆地的资源覆盖层。", this);
        }

        private void EnsureTerrainLayer(string key, Tilemap tilemap, bool replaceDefault)
        {
            terrainLayers ??= new List<GridTerrainLayer>();
            for (var i = 0; i < terrainLayers.Count; i++)
            {
                var layer = terrainLayers[i];
                if (layer != null
                    && string.Equals(layer.Key, GridTerrainKeys.Normalize(key), StringComparison.Ordinal))
                {
                    layer.Configure(key, tilemap, replaceDefault);
                    return;
                }
            }

            var created = new GridTerrainLayer();
            created.Configure(key, tilemap, replaceDefault);
            terrainLayers.Add(created);
        }

        private Tilemap GetTerrainLayerTilemap(string key)
        {
            if (terrainLayers == null)
            {
                return null;
            }

            key = GridTerrainKeys.Normalize(key);
            for (var i = 0; i < terrainLayers.Count; i++)
            {
                var layer = terrainLayers[i];
                if (layer != null && string.Equals(layer.Key, key, StringComparison.Ordinal))
                {
                    return layer.Tilemap;
                }
            }

            return null;
        }

        private bool TryResolveOrCreateGrid(out UnityEngine.Grid targetGrid)
        {
            targetGrid = unityGrid;
            if (targetGrid != null)
            {
                return true;
            }

            var grids = new List<UnityEngine.Grid>();
            if (gameObject.scene.IsValid())
            {
                var roots = gameObject.scene.GetRootGameObjects();
                for (var i = 0; i < roots.Length; i++)
                {
                    grids.AddRange(roots[i].GetComponentsInChildren<UnityEngine.Grid>(true));
                }
            }

            if (grids.Count == 1)
            {
                targetGrid = grids[0];
                return true;
            }

            if (grids.Count > 1)
            {
                Debug.LogError(
                    "Content Scene 中存在多个 Grid，无法自动判断。请先绑定 MapContentAuthoring.Unity Grid。",
                    this);
                return false;
            }

            var gridObject = new GameObject("Grid");
            Undo.RegisterCreatedObjectUndo(gridObject, "Create Map Content Grid");
            gridObject.transform.SetParent(transform, false);
            targetGrid = Undo.AddComponent<UnityEngine.Grid>(gridObject);
            targetGrid.cellSize = new Vector3(1f, 0.5f, 1f);
            targetGrid.cellGap = Vector3.zero;
            targetGrid.cellLayout = GridLayout.CellLayout.Isometric;
            targetGrid.cellSwizzle = GridLayout.CellSwizzle.XYZ;
            EditorUtility.SetDirty(targetGrid);
            return true;
        }

        private static Tilemap EnsureTilemapLayer(
            UnityEngine.Grid targetGrid,
            Tilemap current,
            string objectName,
            int sortingOrder)
        {
            var tilemap = current != null
                          && current.GetComponentInParent<UnityEngine.Grid>() == targetGrid
                ? current
                : null;
            if (tilemap == null)
            {
                var candidates = targetGrid.GetComponentsInChildren<Tilemap>(true);
                for (var i = 0; i < candidates.Length; i++)
                {
                    if (candidates[i] != null
                        && string.Equals(candidates[i].name, objectName, StringComparison.OrdinalIgnoreCase))
                    {
                        tilemap = candidates[i];
                        break;
                    }
                }
            }

            if (tilemap == null)
            {
                var tilemapObject = new GameObject(objectName);
                Undo.RegisterCreatedObjectUndo(tilemapObject, $"Create {objectName}");
                tilemapObject.transform.SetParent(targetGrid.transform, false);
                tilemap = Undo.AddComponent<Tilemap>(tilemapObject);
            }
            else
            {
                Undo.RecordObject(tilemap, $"Configure {objectName}");
            }

            var renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer == null)
            {
                renderer = Undo.AddComponent<TilemapRenderer>(tilemap.gameObject);
            }
            else
            {
                Undo.RecordObject(renderer, $"Configure {objectName} Renderer");
            }

            renderer.sortingOrder = sortingOrder;
            EditorUtility.SetDirty(tilemap);
            EditorUtility.SetDirty(renderer);
            return tilemap;
        }
#endif

        private bool BelongsToBoundGrid(Tilemap tilemap)
        {
            return tilemap != null
                   && tilemap.GetComponentInParent<UnityEngine.Grid>() == unityGrid;
        }

        private void Reset()
        {
            unityGrid = GetComponentInChildren<UnityEngine.Grid>(true);
            if (unityGrid != null && baseTilemap == null)
            {
                var tilemaps = unityGrid.GetComponentsInChildren<Tilemap>(true);
                for (var i = 0; i < tilemaps.Length; i++)
                {
                    if (tilemaps[i] != null
                        && tilemaps[i].name.IndexOf("Base", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        baseTilemap = tilemaps[i];
                        break;
                    }
                }
            }
        }

        private void OnValidate()
        {
            terrainLayers ??= new List<GridTerrainLayer>();
            initialBuildingAlignmentTolerance = Mathf.Max(0.0001f, initialBuildingAlignmentTolerance);
        }

        private static GridPosition GetNearestOrigin(
            GridLayoutService layout,
            Vector3 worldPosition,
            Vector2Int size)
        {
            var gridPoint = layout.WorldToGridPoint(worldPosition);
            return new GridPosition(
                Mathf.RoundToInt(gridPoint.x - size.x * 0.5f),
                Mathf.RoundToInt(gridPoint.y - size.y * 0.5f));
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying || unityGrid == null)
            {
                return;
            }

            var templates = GetInitialBuildingTemplates();
#if UNITY_EDITOR
            if (showInitialBuildingLabels)
            {
                DrawInitialBuildingLabels(templates);
            }
#endif
            if (!showInitialBuildingFootprints)
            {
                return;
            }

            var layout = new GridLayoutService(unityGrid);
            for (var i = 0; i < templates.Length; i++)
            {
                var template = templates[i];
                if (!template.IsValid)
                {
                    continue;
                }

                DrawFootprint(
                    layout,
                    template.BuildingPrefab.Definition.CreateFootprint(template.Origin),
                    template.PreviewAligned
                        ? new Color(0f, 1f, 1f, 0.22f)
                        : new Color(1f, 0.55f, 0f, 0.3f));
            }
        }

        private static void DrawFootprint(
            GridLayoutService layout,
            GridFootprint footprint,
            Color color)
        {
            foreach (var position in footprint.Positions())
            {
                var corners = layout.GetCellCorners(position);
#if UNITY_EDITOR
                Handles.color = color;
                Handles.DrawAAConvexPolygon(corners);
                Handles.color = new Color(color.r, color.g, color.b, 1f);
                var closed = new Vector3[corners.Length + 1];
                for (var i = 0; i < corners.Length; i++)
                {
                    closed[i] = corners[i];
                }

                closed[corners.Length] = corners[0];
                Handles.DrawAAPolyLine(3f, closed);
#else
                Gizmos.color = color;
                for (var i = 0; i < corners.Length; i++)
                {
                    Gizmos.DrawLine(corners[i], corners[(i + 1) % corners.Length]);
                }
#endif
            }
        }
    }
}
