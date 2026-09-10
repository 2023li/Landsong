using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    /// <summary>
    /// Immutable-at-runtime gameplay data for one authored map cell.
    /// Visual meshes and colliders are deliberately not stored here.
    /// </summary>
    [Serializable]
    public sealed class GridMapCellRecord
    {
        [SerializeField, LabelText("逻辑坐标"), ReadOnly]
        private GridPosition position;

        [SerializeField, LabelText("允许建造"), ReadOnly]
        private bool buildable = true;

        [SerializeField, LabelText("允许通行"), ReadOnly]
        private bool traversable = true;

        [SerializeField, LabelText("高度层级"), ReadOnly]
        private int elevationLevel;

        [SerializeField, LabelText("表面层级"), ReadOnly]
        private int surfaceLayer;

        [SerializeField, LabelText("主地形"), ReadOnly]
        private string primaryTerrainKey = GridTerrainKeys.Land;

        [SerializeField, LabelText("叠加地形"), ReadOnly]
        private List<string> overlayTerrainKeys = new List<string>();

        public GridMapCellRecord(
            GridPosition position,
            bool buildable,
            bool traversable,
            int elevationLevel,
            int surfaceLayer,
            string primaryTerrainKey,
            IEnumerable<string> overlayTerrainKeys = null)
        {
            this.position = position;
            this.buildable = buildable;
            this.traversable = traversable;
            this.elevationLevel = elevationLevel;
            this.surfaceLayer = Mathf.Max(0, surfaceLayer);
            this.primaryTerrainKey = NormalizePrimaryTerrainKey(primaryTerrainKey);
            this.overlayTerrainKeys = NormalizeOverlayTerrainKeys(overlayTerrainKeys, this.primaryTerrainKey);
        }

        public GridPosition Position => position;
        public bool Buildable => buildable;
        public bool Traversable => traversable;
        public int ElevationLevel => elevationLevel;
        public int SurfaceLayer => Mathf.Max(0, surfaceLayer);
        public string PrimaryTerrainKey => string.IsNullOrEmpty(primaryTerrainKey)
            ? GridTerrainKeys.Land
            : primaryTerrainKey;
        public IReadOnlyList<string> OverlayTerrainKeys =>
            overlayTerrainKeys != null ? overlayTerrainKeys : EmptyTerrainKeys;

        private static readonly IReadOnlyList<string> EmptyTerrainKeys = Array.Empty<string>();

        public bool HasTerrainKey(string terrainKey)
        {
            var normalized = GridTerrainKeys.Normalize(terrainKey);
            return HasNormalizedTerrainKey(normalized);
        }

        internal bool HasNormalizedTerrainKey(string normalizedTerrainKey)
        {
            if (string.IsNullOrEmpty(normalizedTerrainKey))
            {
                return false;
            }

            if (string.Equals(PrimaryTerrainKey, normalizedTerrainKey, StringComparison.Ordinal))
            {
                return true;
            }

            if (overlayTerrainKeys == null)
            {
                return false;
            }

            for (var i = 0; i < overlayTerrainKeys.Count; i++)
            {
                if (string.Equals(
                        overlayTerrainKeys[i],
                        normalizedTerrainKey,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        internal void Normalize()
        {
            primaryTerrainKey = NormalizePrimaryTerrainKey(primaryTerrainKey);
            overlayTerrainKeys = NormalizeOverlayTerrainKeys(overlayTerrainKeys, primaryTerrainKey);
            surfaceLayer = Mathf.Max(0, surfaceLayer);
        }

        private static string NormalizePrimaryTerrainKey(string terrainKey)
        {
            var normalized = GridTerrainKeys.Normalize(terrainKey);
            return string.IsNullOrEmpty(normalized) ? GridTerrainKeys.Land : normalized;
        }

        private static List<string> NormalizeOverlayTerrainKeys(
            IEnumerable<string> terrainKeys,
            string primaryTerrainKey)
        {
            var normalizedKeys = new List<string>();
            if (terrainKeys == null)
            {
                return normalizedKeys;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var terrainKey in terrainKeys)
            {
                var normalized = GridTerrainKeys.Normalize(terrainKey);
                if (string.IsNullOrEmpty(normalized)
                    || string.Equals(normalized, primaryTerrainKey, StringComparison.Ordinal)
                    || !seen.Add(normalized))
                {
                    continue;
                }

                normalizedKeys.Add(normalized);
            }

            normalizedKeys.Sort(StringComparer.Ordinal);
            return normalizedKeys;
        }
    }

    /// <summary>
    /// Baked gameplay truth shared by placement, traversal, resources and saves.
    /// It can be produced by TileWorldCreator today and another authoring tool later.
    /// </summary>
    [CreateAssetMenu(menuName = "Landsong/地图/逻辑网格", fileName = "地图_逻辑网格")]
    public sealed class GridMapDefinition : ScriptableObject, IGridPlacementSurfaceData
    {
        [SerializeField, TitleGroup("地图范围"), LabelText("格子原点"), ReadOnly]
        private Vector2Int cellOrigin;

        [SerializeField, TitleGroup("地图范围"), LabelText("TWC 编辑画布尺寸"), ReadOnly]
        [PropertyTooltip("来自 TWC Configuration 的 Width/Height，只用于校验和编辑器可视化，不控制相机或可放置范围。")]
        private Vector2Int mapSize = new Vector2Int(1, 1);

        [SerializeField, TitleGroup("地图范围"), LabelText("单格世界尺寸"), Min(0.01f), ReadOnly]
        private float cellSize = 1f;

        [SerializeField, TitleGroup("地图范围"), LabelText("高度最小单位"), Min(0.001f), ReadOnly]
        private float elevationWorldStep = 1f;

        [SerializeField, TitleGroup("烘焙格子"), LabelText("有效逻辑格"), ReadOnly]
        [PropertyTooltip("只有列表中的格子才属于运行时地图；外框内未列出的格子会被判定为 OutOfBounds。")]
        [ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 20)]
        private List<GridMapCellRecord> cells = new List<GridMapCellRecord>();

        [SerializeField, TitleGroup("烘焙来源"), LabelText("源资产 GUID"), ReadOnly]
        private string sourceAssetGuid;

        [SerializeField, TitleGroup("烘焙来源"), LabelText("源资产名称"), ReadOnly]
        private string sourceDisplayName;

        [SerializeField, TitleGroup("烘焙来源"), LabelText("源数据哈希"), ReadOnly]
        private string sourceHash;

        [SerializeField, TitleGroup("烘焙来源"), LabelText("烘焙时间（UTC）"), ReadOnly]
        private string bakedAtUtc;

        [ShowInInspector, TitleGroup("烘焙格子"), LabelText("已烘焙格子数量"), ReadOnly, PropertyOrder(-1)]
        private int BakedCellCount => cells?.Count ?? 0;

        [NonSerialized] private Dictionary<GridPosition, GridMapCellRecord> cellsByPosition;
        [NonSerialized] private BoundsInt bakedCellBounds;
        [NonSerialized] private int[] elevationLevels = Array.Empty<int>();

        public Vector2Int CellOrigin => cellOrigin;
        public Vector2Int MapSize => new Vector2Int(Mathf.Max(1, mapSize.x), Mathf.Max(1, mapSize.y));
        public float CellSize => Mathf.Max(0.01f, cellSize);
        public float ElevationWorldStep => Mathf.Max(0.001f, elevationWorldStep);
        public IReadOnlyList<int> ElevationLevels
        {
            get
            {
                EnsureLookup();
                return elevationLevels;
            }
        }
        public IReadOnlyList<GridMapCellRecord> Cells =>
            cells != null ? cells : EmptyCells;
        public string SourceAssetGuid => sourceAssetGuid ?? string.Empty;
        public string SourceDisplayName => sourceDisplayName ?? string.Empty;
        public string SourceHash => sourceHash ?? string.Empty;
        public string BakedAtUtc => bakedAtUtc ?? string.Empty;
        public BoundsInt DeclaredCellBounds => new BoundsInt(
            cellOrigin.x,
            0,
            cellOrigin.y,
            MapSize.x,
            1,
            MapSize.y);
        [ShowInInspector, TitleGroup("烘焙格子"), LabelText("Base 实际格子范围"), ReadOnly]
        [PropertyTooltip("根据实际烘焙格计算。运行时相机和地图边界使用该范围，而不是 TWC 编辑画布尺寸。")]
        public BoundsInt BakedCellBounds
        {
            get
            {
                EnsureLookup();
                return bakedCellBounds;
            }
        }

        private static readonly IReadOnlyList<GridMapCellRecord> EmptyCells = Array.Empty<GridMapCellRecord>();

        private void OnEnable()
        {
            NormalizeCellRecords();
            RebuildLookup();
        }

        private void OnValidate()
        {
            mapSize.x = Mathf.Max(1, mapSize.x);
            mapSize.y = Mathf.Max(1, mapSize.y);
            cellSize = Mathf.Max(0.01f, cellSize);
            elevationWorldStep = Mathf.Max(0.001f, elevationWorldStep);
            cells ??= new List<GridMapCellRecord>();
            NormalizeCellRecords();
            RebuildLookup();
        }

        public bool HasCell(GridPosition position)
        {
            EnsureLookup();
            return cellsByPosition.ContainsKey(position);
        }

        public bool TryGetCell(GridPosition position, out GridMapCellRecord cell)
        {
            EnsureLookup();
            return cellsByPosition.TryGetValue(position, out cell);
        }

        public bool TryGetSurfaceCell(GridPosition position, out GridSurfaceCell surfaceCell)
        {
            if (TryGetCell(position, out var cell))
            {
                surfaceCell = new GridSurfaceCell(cell.ElevationLevel, cell.SurfaceLayer);
                return true;
            }

            surfaceCell = default;
            return false;
        }

        public bool IsBuildable(GridPosition position)
        {
            return TryGetCell(position, out var cell) && cell.Buildable;
        }

        public bool IsTraversable(GridPosition position)
        {
            return TryGetCell(position, out var cell) && cell.Traversable;
        }

        public bool HasTerrainKey(GridPosition position, string terrainKey)
        {
            return TryGetCell(position, out var cell) && cell.HasTerrainKey(terrainKey);
        }

        internal bool HasNormalizedTerrainKey(GridPosition position, string normalizedTerrainKey)
        {
            return TryGetCell(position, out var cell)
                   && cell.HasNormalizedTerrainKey(normalizedTerrainKey);
        }

        public string GetPrimaryTerrainKey(GridPosition position, string fallbackTerrainKey = GridTerrainKeys.Land)
        {
            return TryGetCell(position, out var cell)
                ? cell.PrimaryTerrainKey
                : GridTerrainKeys.Normalize(fallbackTerrainKey);
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (mapSize.x <= 0 || mapSize.y <= 0)
            {
                error = "Map size must be positive.";
                return false;
            }

            if (cellSize <= 0f || elevationWorldStep <= 0f)
            {
                error = "Cell size and elevation world step must be positive.";
                return false;
            }

            if (cells == null || cells.Count == 0)
            {
                error = "The baked map has no cells.";
                return false;
            }

            var bounds = DeclaredCellBounds;
            var uniquePositions = new HashSet<GridPosition>();
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell == null)
                {
                    error = $"Cell record {i} is null.";
                    return false;
                }

                var position = cell.Position;
                if (!bounds.Contains(position.ToWorldPlaneVector3Int()))
                {
                    error = $"Cell {position} is outside declared bounds {bounds}.";
                    return false;
                }

                if (!uniquePositions.Add(position))
                {
                    error = $"Cell {position} is duplicated.";
                    return false;
                }

            }

            RebuildLookup();
            return true;
        }

#if UNITY_EDITOR
        public void ReplaceBakedData(
            Vector2Int newCellOrigin,
            Vector2Int newMapSize,
            float newCellSize,
            float newElevationWorldStep,
            IEnumerable<GridMapCellRecord> newCells,
            string newSourceAssetGuid,
            string newSourceDisplayName,
            string newSourceHash,
            string newBakedAtUtc)
        {
            cellOrigin = newCellOrigin;
            mapSize = new Vector2Int(Mathf.Max(1, newMapSize.x), Mathf.Max(1, newMapSize.y));
            cellSize = Mathf.Max(0.01f, newCellSize);
            elevationWorldStep = Mathf.Max(0.001f, newElevationWorldStep);
            cells = newCells == null
                ? new List<GridMapCellRecord>()
                : new List<GridMapCellRecord>(newCells);
            NormalizeCellRecords();
            sourceAssetGuid = newSourceAssetGuid ?? string.Empty;
            sourceDisplayName = newSourceDisplayName ?? string.Empty;
            sourceHash = newSourceHash ?? string.Empty;
            bakedAtUtc = newBakedAtUtc ?? string.Empty;
            RebuildLookup();
        }
#endif

        private void EnsureLookup()
        {
            if (cellsByPosition == null || cellsByPosition.Count != (cells?.Count ?? 0))
            {
                RebuildLookup();
            }
        }

        private void RebuildLookup()
        {
            cellsByPosition = new Dictionary<GridPosition, GridMapCellRecord>();
            bakedCellBounds = new BoundsInt(0, 0, 0, 0, 1, 0);
            var uniqueElevationLevels = new HashSet<int>();
            if (cells == null)
            {
                elevationLevels = Array.Empty<int>();
                return;
            }

            var hasCell = false;
            var minX = 0;
            var minZ = 0;
            var maxX = 0;
            var maxZ = 0;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell != null)
                {
                    cellsByPosition[cell.Position] = cell;
                    uniqueElevationLevels.Add(cell.ElevationLevel);
                    if (!hasCell)
                    {
                        minX = maxX = cell.Position.X;
                        minZ = maxZ = cell.Position.Z;
                        hasCell = true;
                    }
                    else
                    {
                        minX = Mathf.Min(minX, cell.Position.X);
                        minZ = Mathf.Min(minZ, cell.Position.Z);
                        maxX = Mathf.Max(maxX, cell.Position.X);
                        maxZ = Mathf.Max(maxZ, cell.Position.Z);
                    }
                }
            }

            if (hasCell)
            {
                bakedCellBounds = new BoundsInt(
                    minX,
                    0,
                    minZ,
                    maxX - minX + 1,
                    1,
                    maxZ - minZ + 1);
            }

            elevationLevels = new int[uniqueElevationLevels.Count];
            uniqueElevationLevels.CopyTo(elevationLevels);
            Array.Sort(elevationLevels);
        }

        private void NormalizeCellRecords()
        {
            if (cells == null)
            {
                return;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                cells[i]?.Normalize();
            }
        }
    }
}
