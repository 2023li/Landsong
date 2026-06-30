using System.Collections.Generic;

namespace Landsong.GridSystem
{
    public sealed class GridCell
    {
        private List<string> additionalTerrainKeys;

        public GridCell(GridPosition position, string terrainKey = GridTerrainKeys.Land, bool isBuildable = true)
        {
            Position = position;
            TerrainKey = GridTerrainKeys.Normalize(terrainKey);
            if (string.IsNullOrEmpty(TerrainKey))
            {
                TerrainKey = GridTerrainKeys.Land;
            }

            IsBuildable = isBuildable;
        }

        public GridPosition Position { get; }
        public string TerrainKey { get; private set; }
        public bool IsWater => HasTerrainKey(GridTerrainKeys.Water);
        public bool IsBuildable { get; private set; }
        public string OccupantId { get; private set; }
        public bool IsOccupied => !string.IsNullOrEmpty(OccupantId);

        public void SetBuildable(bool isBuildable)
        {
            IsBuildable = isBuildable;
        }

        public void SetTerrainKey(string terrainKey)
        {
            var normalizedKey = GridTerrainKeys.Normalize(terrainKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                return;
            }

            TerrainKey = normalizedKey;
            additionalTerrainKeys?.Remove(normalizedKey);
        }

        public void AddTerrainKey(string terrainKey)
        {
            var normalizedKey = GridTerrainKeys.Normalize(terrainKey);
            if (string.IsNullOrEmpty(normalizedKey) || normalizedKey == TerrainKey)
            {
                return;
            }

            additionalTerrainKeys ??= new List<string>();
            if (!additionalTerrainKeys.Contains(normalizedKey))
            {
                additionalTerrainKeys.Add(normalizedKey);
            }
        }

        public bool HasTerrainKey(string terrainKey)
        {
            var normalizedKey = GridTerrainKeys.Normalize(terrainKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                return false;
            }

            if (normalizedKey == TerrainKey)
            {
                return true;
            }

            return additionalTerrainKeys != null && additionalTerrainKeys.Contains(normalizedKey);
        }

        public bool HasAllTerrainKeys(IReadOnlyList<string> terrainKeys)
        {
            if (terrainKeys == null || terrainKeys.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < terrainKeys.Count; i++)
            {
                if (!HasTerrainKey(terrainKeys[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal void Occupy(string occupantId)
        {
            OccupantId = occupantId;
        }

        internal void ClearOccupant()
        {
            OccupantId = null;
        }
    }
}
