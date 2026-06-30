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
        [SerializeField, Min(1), BoxGroup("地图"), LabelText("宽度")] private int width = 256;
        [SerializeField, Min(1), BoxGroup("地图"), LabelText("高度")] private int height = 256;
        [SerializeField, LabelText("Awake 时初始化")] private bool initializeOnAwake = true;
        [SerializeField, BoxGroup("规则"), LabelText("默认地形 Key")] private string defaultTerrainKey = GridTerrainKeys.Land;
        [SerializeField, BoxGroup("规则"), LabelText("默认可建造")] private bool defaultBuildable = true;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("Tilemap_基础层")] private Tilemap baseTilemap;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("基础层 填充瓦片")] private TileBase baseTile;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("初始化时填充基础层")] private bool fillBaseLayerOnInitialize = true;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("Tilemap_占用层")] private Tilemap occupancyTilemap;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("占用层 填充瓦片")] private TileBase occupiedTile;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("Tilemap_高亮层")] private Tilemap highlightTilemap;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("初始化时清空占用层")] private bool clearOccupancyLayerOnInitialize = true;
        [SerializeField, BoxGroup("底层TileMap"), LabelText("初始化时清空高亮层")] private bool clearHighlightLayerOnInitialize = true;
        [SerializeField, BoxGroup("地形层"), LabelText("地形 Tilemap 层")] private List<GridTerrainLayer> terrainLayers = new List<GridTerrainLayer>();

        private readonly List<GridPosition> clearedOccupancyPositions = new List<GridPosition>();
        private UnityEngine.Grid cachedUnityGrid;

        public int Width => width;
        public int Height => height;
        public float CellSize => GridLayoutService.GetCellSize(UnityGrid);
        public Vector3 WorldOrigin => GridLayoutService.GetOrigin(UnityGrid);
        public GridPlaneMode PlaneMode => GridLayoutService.GetPlaneMode(UnityGrid);
        public string DefaultTerrainKey => defaultTerrainKey;
        public bool DefaultBuildable => defaultBuildable;
        public Tilemap BaseTilemap => baseTilemap;
        public TileBase BaseTile => baseTile;
        public IReadOnlyList<GridTerrainLayer> TerrainLayers => terrainLayers ?? (terrainLayers = new List<GridTerrainLayer>());
        public Tilemap OccupancyTilemap => occupancyTilemap;
        public TileBase OccupiedTile => occupiedTile;
        public bool HasOccupancyTilemapVisualization => occupancyTilemap != null && occupiedTile != null;
        public Tilemap HighlightTilemap => highlightTilemap;
        public bool HasHighlightTilemapVisualization => highlightTilemap != null;
        public GridMap Map { get; private set; }
        public GridLayoutService Layout { get; private set; }
        public bool IsInitialized => Map != null && Layout != null;
        public event Action GridStateChanged;
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
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cachedUnityGrid = GetComponent<UnityEngine.Grid>();
            NormalizeSerializedTerrainKeys();
        }

        public void Initialize()
        {
            NormalizeSerializedTerrainKeys();
            if (fillBaseLayerOnInitialize)
            {
                FillBaseTilemapBySize();
            }

            Map = new GridMap(width, height, defaultBuildable, defaultTerrainKey);
            ApplyBaseLayer();
            ApplyTerrainLayers();
            if (clearOccupancyLayerOnInitialize)
            {
                ClearOccupancyTilemap();
            }

            if (clearHighlightLayerOnInitialize)
            {
                ClearHighlightTilemap();
            }

            RefreshLayout();
            RaiseGridStateChanged();
        }

        public void RefreshLayout()
        {
            Layout = CreateLayoutSnapshot();
        }

        public GridLayoutService CreateLayoutSnapshot()
        {
            return UnityGrid == null
                ? new GridLayoutService(1f, transform.position, GridPlaneMode.XY)
                : new GridLayoutService(UnityGrid);
        }

        public bool TryGetGridPositionFromRay(Ray ray, out GridPosition position)
        {
            EnsureInitialized();

            if (!Layout.TryGetGridPosition(ray, out position))
            {
                return false;
            }

            return Map.Contains(position) && HasBaseTile(position);
        }

        public bool ContainsPosition(GridPosition position)
        {
            return IsInBounds(position) && HasBaseTile(position);
        }

        public bool TryGetGridPointFromRay(Ray ray, out Vector2 gridPoint)
        {
            EnsureInitialized();

            if (!Layout.TryRaycastToGridPlane(ray, out var worldPosition))
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

        public Vector3 GetCellCenter(GridPosition position)
        {
            EnsureInitialized();
            return Layout.GridToWorldCenter(position);
        }

        public bool TryOccupy(GridPosition origin, Vector2Int size, string occupantId, out GridPlacementFailureReason failureReason)
        {
            EnsureInitialized();
            var occupied = Map.TryOccupy(origin, size, occupantId, out failureReason);
            if (occupied)
            {
                SetOccupancyTiles(new GridFootprint(origin, size));
                RaiseGridStateChanged();
            }

            return occupied;
        }

        public bool TryOccupy(
            GridPosition origin,
            Vector2Int size,
            string occupantId,
            IReadOnlyList<string> requiredTerrainKeys,
            out GridPlacementFailureReason failureReason)
        {
            EnsureInitialized();
            var occupied = Map.TryOccupy(origin, size, occupantId, requiredTerrainKeys, out failureReason);
            if (occupied)
            {
                SetOccupancyTiles(new GridFootprint(origin, size));
                RaiseGridStateChanged();
            }

            return occupied;
        }

        public bool CanOccupy(GridPosition origin, Vector2Int size, out GridPlacementFailureReason failureReason, string ignoredOccupantId = null)
        {
            EnsureInitialized();
            return Map.CanOccupy(origin, size, out failureReason, ignoredOccupantId);
        }

        public bool CanOccupy(
            GridPosition origin,
            Vector2Int size,
            IReadOnlyList<string> requiredTerrainKeys,
            out GridPlacementFailureReason failureReason,
            string ignoredOccupantId = null)
        {
            EnsureInitialized();
            return Map.CanOccupy(origin, size, requiredTerrainKeys, out failureReason, ignoredOccupantId);
        }

        public string GetTerrainKey(GridPosition position)
        {
            EnsureInitialized();
            return HasBaseTile(position) ? Map.GetTerrainKey(position) : null;
        }

        public bool HasTerrainKey(GridPosition position, string terrainKey)
        {
            EnsureInitialized();
            return HasBaseTile(position) && Map.HasTerrainKey(position, terrainKey);
        }

        public bool IsWater(GridPosition position)
        {
            EnsureInitialized();
            return HasBaseTile(position) && Map.IsWater(position);
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
            var clearedCount = Map.ClearOccupant(occupantId, clearedOccupancyPositions);
            if (clearedCount > 0)
            {
                ClearOccupancyTiles(clearedOccupancyPositions);
                RaiseGridStateChanged();
            }

            return clearedCount;
        }

        public void RefreshOccupancyTilemap()
        {
            ClearOccupancyTilemap();
            if (Map == null || !HasOccupancyTilemapVisualization)
            {
                return;
            }

            foreach (var cell in Map.Cells)
            {
                if (cell.IsOccupied)
                {
                    SetOccupancyTile(cell.Position);
                }
            }
        }

        public void ClearHighlightTilemap()
        {
            if (highlightTilemap != null)
            {
                highlightTilemap.ClearAllTiles();
            }
        }

        public bool SetBuildable(GridPosition position, bool isBuildable)
        {
            EnsureInitialized();

            if (!Map.Contains(position) || !HasBaseTile(position))
            {
                return false;
            }

            Map.SetBuildable(position, isBuildable);
            RaiseGridStateChanged();
            return true;
        }

        public bool TryOccupy(GridPosition origin, Vector2Int size, string occupantId)
        {
            return TryOccupy(origin, size, occupantId, out _);
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
            FillBaseTilemapFromInspector();

            EditorUtility.SetDirty(this);
            Undo.CollapseUndoOperations(undoGroup);
        }

        [Button("按长宽填充 Base 层")]
        [ContextMenu("Fill Base Tilemap")]
        private void FillBaseTilemapFromInspector()
        {
            if (baseTilemap == null)
            {
                Debug.LogWarning($"GridMapBehaviour '{name}' has no Base Tilemap.", this);
                return;
            }

            Undo.RecordObject(baseTilemap, "Fill Base Tilemap");
            FillBaseTilemapBySize();
            EditorUtility.SetDirty(baseTilemap);
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

        private void FillBaseTilemapBySize()
        {
            if (baseTilemap == null)
            {
                return;
            }

            if (baseTile == null)
            {
                Debug.LogWarning($"GridMapBehaviour '{name}' has no Base Tile assigned.", this);
                return;
            }

            baseTilemap.ClearAllTiles();

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    baseTilemap.SetTile(new Vector3Int(x, y, 0), baseTile);
                }
            }
        }

        private void ApplyBaseLayer()
        {
            if (Map == null || baseTilemap == null)
            {
                return;
            }

            foreach (var cell in Map.Cells)
            {
                Map.SetBuildable(cell.Position, defaultBuildable && HasBaseTile(cell.Position));
            }
        }

        private void ApplyTerrainLayers()
        {
            if (Map == null || terrainLayers == null)
            {
                return;
            }

            for (var i = 0; i < terrainLayers.Count; i++)
            {
                ApplyTerrainLayer(terrainLayers[i]);
            }
        }

        private void ApplyTerrainLayer(GridTerrainLayer layer)
        {
            if (layer == null || !layer.IsValid)
            {
                return;
            }

            var tilemap = layer.Tilemap;
            var terrainKey = layer.Key;
            foreach (var tilemapPosition in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(tilemapPosition))
                {
                    continue;
                }

                var gridPosition = layer.TilemapCellToGridPosition(tilemapPosition);
                if (!Map.Contains(gridPosition) || !HasBaseTile(gridPosition))
                {
                    continue;
                }

                if (layer.ReplaceDefaultTerrainKey)
                {
                    Map.SetTerrainKey(gridPosition, terrainKey);
                }
                else
                {
                    Map.AddTerrainKey(gridPosition, terrainKey);
                }
            }
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

        private bool IsInBounds(GridPosition position)
        {
            return position.X >= 0
                   && position.X < width
                   && position.Y >= 0
                   && position.Y < height;
        }

        private bool HasBaseTile(GridPosition position)
        {
            return IsInBounds(position)
                   && (baseTilemap == null || baseTilemap.HasTile(GridPositionToTilemapCell(position)));
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

        private void RaiseGridStateChanged()
        {
            GridStateChanged?.Invoke();
        }
    }
}
