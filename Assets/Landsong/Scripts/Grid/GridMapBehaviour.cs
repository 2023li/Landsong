using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Landsong.GridSystem
{
    [RequireComponent(typeof(UnityEngine.Grid))]
    public sealed class GridMapBehaviour : MonoBehaviour
    {
        [SerializeField, LabelText("Awake 时初始化")] private bool initializeOnAwake = true;
        [SerializeField, BoxGroup("规则"), LabelText("默认地形 Key")] private string defaultTerrainKey = GridTerrainKeys.Land;
        [SerializeField, BoxGroup("规则"), LabelText("默认可建造")] private bool defaultBuildable = true;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("Tilemap_基础层")] private Tilemap baseTilemap;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("Tilemap_占用层")] private Tilemap occupancyTilemap;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("占用层 填充瓦片")] private TileBase occupiedTile;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("Tilemap_高亮层")] private Tilemap highlightTilemap;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("初始化时清空占用层")] private bool clearOccupancyLayerOnInitialize = true;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("初始化时清空高亮层")] private bool clearHighlightLayerOnInitialize = true;
        [SerializeField, BoxGroup("地形层"), LabelText("地形 Tilemap 层")] private List<GridTerrainLayer> terrainLayers = new List<GridTerrainLayer>();
        [SerializeField, BoxGroup("寻路"), LabelText("普通格行动力消耗"), Min(1)] private int defaultTraversalActionCost = 10;
        [SerializeField, BoxGroup("寻路"), LabelText("地形行动力消耗")] private List<GridTraversalCostRule> traversalCostRules = new List<GridTraversalCostRule>
        {
            new GridTraversalCostRule(GridTerrainKeys.Road, 5),
            new GridTraversalCostRule(GridTerrainKeys.AdvancedRoad, 3)
        };

        private readonly Dictionary<GridPosition, GridOccupancyData> occupiedCells = new Dictionary<GridPosition, GridOccupancyData>();
        private readonly Dictionary<string, List<GridPosition>> occupiedPositionsById = new Dictionary<string, List<GridPosition>>(StringComparer.Ordinal);
        private readonly List<GridPosition> clearedOccupancyPositions = new List<GridPosition>();
        private UnityEngine.Grid cachedUnityGrid;
        private bool hasValidBaseTilemap;
        private bool loggedInvalidBaseTilemap;

        public BoundsInt BaseCellBounds => baseTilemap == null ? new BoundsInt(0, 0, 0, 0, 0, 1) : baseTilemap.cellBounds;
        public Vector3 WorldOrigin => GridLayoutService.GetOrigin(UnityGrid);
        public GridPlaneMode PlaneMode => GridLayoutService.GetPlaneMode(UnityGrid);
        public Tilemap HighlightTilemap => highlightTilemap;
        public GridLayoutService Layout { get; private set; }
        public bool IsInitialized => hasValidBaseTilemap && Layout != null;
        public int DefaultTraversalActionCost => Mathf.Max(1, defaultTraversalActionCost);
        public IReadOnlyList<GridTraversalCostRule> TraversalCostRules => traversalCostRules;

        private bool HasOccupancyTilemapVisualization => occupancyTilemap != null && occupiedTile != null;

        private UnityEngine.Grid UnityGrid
        {
            get
            {
                if (cachedUnityGrid == null)
                {
                    cachedUnityGrid = GetComponent<UnityEngine.Grid>();
                }

                return cachedUnityGrid;
            }
        }

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        private void OnValidate()
        {
            cachedUnityGrid = GetComponent<UnityEngine.Grid>();
            NormalizeSerializedTerrainKeys();
            NormalizeTraversalCosts();
        }

        public void Initialize()
        {
            NormalizeSerializedTerrainKeys();
            NormalizeTraversalCosts();
            hasValidBaseTilemap = TryValidateBaseTilemap(true);
            loggedInvalidBaseTilemap = hasValidBaseTilemap ? false : loggedInvalidBaseTilemap;

            occupiedCells.Clear();
            occupiedPositionsById.Clear();

            if (clearOccupancyLayerOnInitialize)
            {
                ClearOccupancyTilemap();
            }

            if (clearHighlightLayerOnInitialize)
            {
                ClearHighlightTilemap();
            }

            RefreshLayout();
        }

        public void RefreshLayout()
        {
            Layout = CreateLayoutSnapshot();
        }

        public GridLayoutService CreateLayoutSnapshot()
        {
            return new GridLayoutService(UnityGrid);
        }

        private bool TryGetGridPositionFromRay(Ray ray, out GridPosition position)
        {
            EnsureInitialized();

            if (Layout == null)
            {
                position = default;
                return false;
            }

            if (!Layout.TryGetGridPosition(ray, out position))
            {
                return false;
            }

            return HasBaseTile(position);
        }

        private bool TryGetGridPointFromRay(Ray ray, out Vector2 gridPoint)
        {
            EnsureInitialized();

            if (Layout == null || !Layout.TryRaycastToGridPlane(ray, out var worldPosition))
            {
                gridPoint = default;
                return false;
            }

            gridPoint = Layout.WorldToGridPoint(worldPosition);
            return true;
        }

        public bool TryGetGridPositionFromScreenPosition(Camera sourceCamera, Vector2 screenPosition, out GridPosition position)
        {
            if (sourceCamera == null)
            {
                position = default;
                return false;
            }

            var ray = sourceCamera.ScreenPointToRay(screenPosition);
            return TryGetGridPositionFromRay(ray, out position);
        }

        public bool TryGetGridPointFromScreenPosition(Camera sourceCamera, Vector2 screenPosition, out Vector2 gridPoint)
        {
            if (sourceCamera == null)
            {
                gridPoint = default;
                return false;
            }

            var ray = sourceCamera.ScreenPointToRay(screenPosition);
            return TryGetGridPointFromRay(ray, out gridPoint);
        }

        public bool TryOccupy(
            GridPosition origin,
            Vector2Int size,
            string occupantId,
            IReadOnlyList<string> requiredTerrainKeys,
            out GridPlacementFailureReason failureReason)
        {
            return TryOccupy(origin, size, occupantId, requiredTerrainKeys, 0, out failureReason);
        }

        public bool TryOccupy(
            GridPosition origin,
            Vector2Int size,
            string occupantId,
            IReadOnlyList<string> requiredTerrainKeys,
            int movementResistance,
            out GridPlacementFailureReason failureReason)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(occupantId))
            {
                failureReason = GridPlacementFailureReason.InvalidOccupantId;
                return false;
            }

            if (!CanOccupy(origin, size, requiredTerrainKeys, out failureReason))
            {
                return false;
            }

            var footprint = new GridFootprint(origin, size);
            var occupiedPositions = GetOrCreateOccupiedPositions(occupantId);
            var occupancyData = new GridOccupancyData(occupantId, movementResistance);
            foreach (var position in footprint.Positions())
            {
                occupiedCells[position] = occupancyData;
                occupiedPositions.Add(position);
            }

            SetOccupancyTiles(footprint);
            failureReason = GridPlacementFailureReason.None;
            return true;
        }

        public bool CanOccupy(
            GridPosition origin,
            Vector2Int size,
            IReadOnlyList<string> requiredTerrainKeys,
            out GridPlacementFailureReason failureReason,
            string ignoredOccupantId = null)
        {
            EnsureInitialized();
            if (size.x <= 0 || size.y <= 0)
            {
                failureReason = GridPlacementFailureReason.InvalidSize;
                return false;
            }

            if (!IsInitialized)
            {
                failureReason = GridPlacementFailureReason.OutOfBounds;
                return false;
            }

            var footprint = new GridFootprint(origin, size);
            foreach (var position in footprint.Positions())
            {
                if (!HasBaseTile(position))
                {
                    failureReason = GridPlacementFailureReason.OutOfBounds;
                    return false;
                }

                if (!IsBuildable(position))
                {
                    failureReason = GridPlacementFailureReason.NotBuildable;
                    return false;
                }

                if (!HasAllTerrainKeys(position, requiredTerrainKeys))
                {
                    failureReason = GridPlacementFailureReason.TerrainMismatch;
                    return false;
                }

                if (occupiedCells.TryGetValue(position, out var occupancyData) && occupancyData.OccupantId != ignoredOccupantId)
                {
                    failureReason = GridPlacementFailureReason.Occupied;
                    return false;
                }
            }

            failureReason = GridPlacementFailureReason.None;
            return true;
        }

        public bool HasBaseTileAt(GridPosition position)
        {
            EnsureInitialized();
            return HasBaseTile(position);
        }

        public bool HasTerrainKey(GridPosition position, string terrainKey)
        {
            EnsureInitialized();
            return HasTerrainKeyInternal(position, terrainKey);
        }

        public int GetTerrainTraversalActionCost(GridPosition position)
        {
            EnsureInitialized();
            if (!HasBaseTile(position))
            {
                return DefaultTraversalActionCost;
            }

            var bestCost = DefaultTraversalActionCost;
            if (traversalCostRules == null)
            {
                return bestCost;
            }

            for (var i = 0; i < traversalCostRules.Count; i++)
            {
                var rule = traversalCostRules[i];
                if (rule == null || !rule.IsValid)
                {
                    continue;
                }

                if (HasTerrainKeyInternal(position, rule.TerrainKey))
                {
                    bestCost = Mathf.Min(bestCost, rule.ActionCost);
                }
            }

            return Mathf.Max(1, bestCost);
        }

        public bool CanTraverse(GridPosition position, string ignoredOccupantId = null)
        {
            EnsureInitialized();
            if (!HasBaseTile(position))
            {
                return false;
            }

            if (!occupiedCells.TryGetValue(position, out var occupancyData))
            {
                return true;
            }

            return occupancyData.OccupantId == ignoredOccupantId || occupancyData.MovementResistance > 0;
        }

        public int GetTraversalActionCost(GridPosition position, string ignoredOccupantId = null)
        {
            EnsureInitialized();
            if (!HasBaseTile(position))
            {
                return int.MaxValue;
            }

            if (occupiedCells.TryGetValue(position, out var occupancyData)
                && occupancyData.OccupantId != ignoredOccupantId)
            {
                return occupancyData.MovementResistance > 0
                    ? occupancyData.MovementResistance
                    : int.MaxValue;
            }

            return GetTerrainTraversalActionCost(position);
        }

        public bool TryGetOccupantId(GridPosition position, out string occupantId)
        {
            EnsureInitialized();
            if (occupiedCells.TryGetValue(position, out var occupancyData))
            {
                occupantId = occupancyData.OccupantId;
                return true;
            }

            occupantId = null;
            return false;
        }

        public Vector3 GetFootprintCenter(GridPosition origin, Vector2Int size)
        {
            EnsureInitialized();
            return Layout.GridToWorldPoint(origin.X + size.x * 0.5f, origin.Y + size.y * 0.5f);
        }

        public int ClearOccupant(string occupantId)
        {
            EnsureInitialized();
            clearedOccupancyPositions.Clear();
            if (string.IsNullOrWhiteSpace(occupantId))
            {
                return 0;
            }

            if (occupiedPositionsById.TryGetValue(occupantId, out var positions))
            {
                for (var i = 0; i < positions.Count; i++)
                {
                    var position = positions[i];
                    if (occupiedCells.TryGetValue(position, out var currentOccupancyData)
                        && currentOccupancyData.OccupantId == occupantId)
                    {
                        occupiedCells.Remove(position);
                        clearedOccupancyPositions.Add(position);
                    }
                }

                occupiedPositionsById.Remove(occupantId);
            }
            else
            {
                foreach (var pair in occupiedCells)
                {
                    if (pair.Value.OccupantId == occupantId)
                    {
                        clearedOccupancyPositions.Add(pair.Key);
                    }
                }

                for (var i = 0; i < clearedOccupancyPositions.Count; i++)
                {
                    occupiedCells.Remove(clearedOccupancyPositions[i]);
                }
            }

            if (clearedOccupancyPositions.Count > 0)
            {
                ClearOccupancyTiles(clearedOccupancyPositions);
            }

            return clearedOccupancyPositions.Count;
        }

        private void ClearHighlightTilemap()
        {
            if (highlightTilemap != null)
            {
                highlightTilemap.ClearAllTiles();
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                Initialize();
            }
        }

#if UNITY_EDITOR
        [Button("一键配置基础 Tilemap")]
        [ContextMenu("Configure Required Tilemaps")]
        private void ConfigureRequiredTilemaps()
        {
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();

            Undo.RecordObject(this, "Configure Required Tilemaps");

            EnsureUnityGridComponent();
            baseTilemap = EnsureTilemapLayer(baseTilemap, "Base Tilemap", 0);
            occupancyTilemap = EnsureTilemapLayer(occupancyTilemap, "Occupancy Tilemap", 100);
            highlightTilemap = EnsureTilemapLayer(highlightTilemap, "Highlight Tilemap", 200);

            EditorUtility.SetDirty(this);
            Undo.CollapseUndoOperations(undoGroup);
        }

        private void EnsureUnityGridComponent()
        {
            var unityGrid = GetComponent<UnityEngine.Grid>();
            if (unityGrid == null)
            {
                unityGrid = Undo.AddComponent<UnityEngine.Grid>(gameObject);
            }
            else
            {
                Undo.RecordObject(unityGrid, "Configure Unity Grid");
            }

            cachedUnityGrid = unityGrid;
            EditorUtility.SetDirty(unityGrid);
        }

        private Tilemap EnsureTilemapLayer(Tilemap currentTilemap, string childName, int sortingOrder)
        {
            var tilemap = currentTilemap;
            if (tilemap == null)
            {
                var child = transform.Find(childName);
                tilemap = child == null ? null : child.GetComponent<Tilemap>();
            }

            if (tilemap == null)
            {
                var tilemapObject = new GameObject(childName);
                Undo.RegisterCreatedObjectUndo(tilemapObject, $"Create {childName}");
                tilemapObject.transform.SetParent(transform, false);
                tilemap = tilemapObject.AddComponent<Tilemap>();
            }
            else
            {
                Undo.RecordObject(tilemap, $"Configure {childName}");
            }

            var tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
            if (tilemapRenderer == null)
            {
                tilemapRenderer = Undo.AddComponent<TilemapRenderer>(tilemap.gameObject);
            }
            else
            {
                Undo.RecordObject(tilemapRenderer, $"Configure {childName} Renderer");
            }

            tilemapRenderer.sortingOrder = sortingOrder;
            EditorUtility.SetDirty(tilemap);
            EditorUtility.SetDirty(tilemapRenderer);
            return tilemap;
        }
#endif

        private bool TryValidateBaseTilemap(bool logWarnings)
        {
            if (baseTilemap == null)
            {
                LogInvalidBaseTilemap("GridMapBehaviour requires a manually painted Base Tilemap.", this, logWarnings);
                return false;
            }

            var bounds = baseTilemap.cellBounds;
            if (bounds.size.x <= 0 || bounds.size.y <= 0 || baseTilemap.GetUsedTilesCount() <= 0)
            {
                LogInvalidBaseTilemap($"GridMapBehaviour '{name}' Base Tilemap is empty. Paint Base tiles in the editor before entering play mode.", baseTilemap, logWarnings);
                return false;
            }

            return true;
        }

        private bool IsBuildable(GridPosition position)
        {
            return HasBaseTile(position) && defaultBuildable;
        }

        private bool HasAllTerrainKeys(GridPosition position, IReadOnlyList<string> terrainKeys)
        {
            if (terrainKeys == null || terrainKeys.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < terrainKeys.Count; i++)
            {
                if (!HasTerrainKeyInternal(position, terrainKeys[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private string GetPrimaryTerrainKey(GridPosition position)
        {
            var terrainKey = defaultTerrainKey;
            for (var i = 0; i < terrainLayers.Count; i++)
            {
                var layer = terrainLayers[i];
                if (layer == null || !layer.IsValid || !layer.ReplaceDefaultTerrainKey)
                {
                    continue;
                }

                if (layer.Tilemap.HasTile(GridPositionToTilemapCell(position)))
                {
                    terrainKey = layer.Key;
                }
            }

            return terrainKey;
        }

        private bool HasTerrainKeyInternal(GridPosition position, string terrainKey)
        {
            var normalizedKey = GridTerrainKeys.Normalize(terrainKey);
            if (string.IsNullOrEmpty(normalizedKey) || !HasBaseTile(position))
            {
                return false;
            }

            if (normalizedKey == GetPrimaryTerrainKey(position))
            {
                return true;
            }

            for (var i = 0; i < terrainLayers.Count; i++)
            {
                var layer = terrainLayers[i];
                if (layer == null || !layer.IsValid || layer.ReplaceDefaultTerrainKey || layer.Key != normalizedKey)
                {
                    continue;
                }

                if (layer.Tilemap.HasTile(GridPositionToTilemapCell(position)))
                {
                    return true;
                }
            }

            return false;
        }

        private List<GridPosition> GetOrCreateOccupiedPositions(string occupantId)
        {
            if (!occupiedPositionsById.TryGetValue(occupantId, out var positions))
            {
                positions = new List<GridPosition>();
                occupiedPositionsById.Add(occupantId, positions);
            }

            return positions;
        }

        private void SetOccupancyTiles(GridFootprint footprint)
        {
            if (!HasOccupancyTilemapVisualization)
            {
                return;
            }

            foreach (var position in footprint.Positions())
            {
                SetOccupancyTile(position);
            }
        }

        private void SetOccupancyTile(GridPosition position)
        {
            occupancyTilemap.SetTile(GridPositionToTilemapCell(position), occupiedTile);
        }

        private void ClearOccupancyTiles(IReadOnlyList<GridPosition> positions)
        {
            if (occupancyTilemap == null || positions == null)
            {
                return;
            }

            for (var i = 0; i < positions.Count; i++)
            {
                occupancyTilemap.SetTile(GridPositionToTilemapCell(positions[i]), null);
            }
        }

        private void ClearOccupancyTilemap()
        {
            if (occupancyTilemap != null)
            {
                occupancyTilemap.ClearAllTiles();
            }
        }

        private static Vector3Int GridPositionToTilemapCell(GridPosition position)
        {
            return new Vector3Int(position.X, position.Y, 0);
        }

        private bool HasBaseTile(GridPosition position)
        {
            return baseTilemap != null && baseTilemap.HasTile(GridPositionToTilemapCell(position));
        }

        private void NormalizeSerializedTerrainKeys()
        {
            defaultTerrainKey = GridTerrainKeys.Normalize(defaultTerrainKey);
            if (string.IsNullOrEmpty(defaultTerrainKey))
            {
                defaultTerrainKey = GridTerrainKeys.Land;
            }

            terrainLayers ??= new List<GridTerrainLayer>();
        }

        private void NormalizeTraversalCosts()
        {
            defaultTraversalActionCost = Mathf.Max(1, defaultTraversalActionCost);
            if (traversalCostRules == null || traversalCostRules.Count == 0)
            {
                traversalCostRules = new List<GridTraversalCostRule>
                {
                    new GridTraversalCostRule(GridTerrainKeys.Road, 5),
                    new GridTraversalCostRule(GridTerrainKeys.AdvancedRoad, 3)
                };
            }

            for (var i = 0; i < traversalCostRules.Count; i++)
            {
                traversalCostRules[i]?.Normalize(defaultTraversalActionCost);
            }
        }

        private void LogInvalidBaseTilemap(string message, UnityEngine.Object context, bool logWarnings)
        {
            if (!logWarnings || loggedInvalidBaseTilemap)
            {
                return;
            }

            loggedInvalidBaseTilemap = true;
            Debug.LogWarning(message, context);
        }

        private readonly struct GridOccupancyData
        {
            public GridOccupancyData(string occupantId, int movementResistance)
            {
                OccupantId = string.IsNullOrWhiteSpace(occupantId) ? string.Empty : occupantId;
                MovementResistance = movementResistance;
            }

            public string OccupantId { get; }
            public int MovementResistance { get; }
        }
    }
}
