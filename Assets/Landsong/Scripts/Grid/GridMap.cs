using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.GridSystem
{
    public sealed class GridMap
    {
        private readonly GridCell[,] cells;

        public GridMap(int width, int height, bool defaultBuildable = true, string defaultTerrainKey = GridTerrainKeys.Land)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Grid map width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Grid map height must be positive.");
            }

            Width = width;
            Height = height;
            DefaultTerrainKey = GridTerrainKeys.Normalize(defaultTerrainKey);
            if (string.IsNullOrEmpty(DefaultTerrainKey))
            {
                DefaultTerrainKey = GridTerrainKeys.Land;
            }

            DefaultBuildable = defaultBuildable;
            cells = new GridCell[width, height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    cells[x, y] = new GridCell(new GridPosition(x, y), DefaultTerrainKey, defaultBuildable);
                }
            }
        }

        public int Width { get; }
        public int Height { get; }
        public string DefaultTerrainKey { get; }
        public bool DefaultBuildable { get; }

        public IEnumerable<GridCell> Cells
        {
            get
            {
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        yield return cells[x, y];
                    }
                }
            }
        }

        public bool Contains(GridPosition position)
        {
            return position.X >= 0
                   && position.X < Width
                   && position.Y >= 0
                   && position.Y < Height;
        }

        public bool Contains(GridFootprint footprint)
        {
            return footprint.MinX >= 0
                   && footprint.MinY >= 0
                   && footprint.MaxXExclusive <= Width
                   && footprint.MaxYExclusive <= Height;
        }

        public GridCell GetCell(GridPosition position)
        {
            if (!Contains(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), $"Grid position {position} is outside the map.");
            }

            return cells[position.X, position.Y];
        }

        public bool TryGetCell(GridPosition position, out GridCell cell)
        {
            if (!Contains(position))
            {
                cell = null;
                return false;
            }

            cell = cells[position.X, position.Y];
            return true;
        }

        public bool IsBuildable(GridPosition position)
        {
            return TryGetCell(position, out var cell) && cell.IsBuildable;
        }

        public string GetTerrainKey(GridPosition position)
        {
            return TryGetCell(position, out var cell) ? cell.TerrainKey : DefaultTerrainKey;
        }

        public bool HasTerrainKey(GridPosition position, string terrainKey)
        {
            return TryGetCell(position, out var cell) && cell.HasTerrainKey(terrainKey);
        }

        public bool IsWater(GridPosition position)
        {
            return HasTerrainKey(position, GridTerrainKeys.Water);
        }

        public bool IsOccupied(GridPosition position)
        {
            return TryGetCell(position, out var cell) && cell.IsOccupied;
        }

        public void SetBuildable(GridPosition position, bool isBuildable)
        {
            GetCell(position).SetBuildable(isBuildable);
        }

        public void SetBuildable(GridFootprint footprint, bool isBuildable)
        {
            foreach (var position in footprint.Positions())
            {
                SetBuildable(position, isBuildable);
            }
        }

        public void SetTerrainKey(GridPosition position, string terrainKey)
        {
            GetCell(position).SetTerrainKey(terrainKey);
        }

        public void AddTerrainKey(GridPosition position, string terrainKey)
        {
            GetCell(position).AddTerrainKey(terrainKey);
        }

        public bool CanOccupy(GridPosition origin, Vector2Int size)
        {
            return CanOccupy(origin, size, out _);
        }

        public bool CanOccupy(GridPosition origin, Vector2Int size, out GridPlacementFailureReason failureReason, string ignoredOccupantId = null)
        {
            return CanOccupy(origin, size, null, out failureReason, ignoredOccupantId);
        }

        public bool CanOccupy(
            GridPosition origin,
            Vector2Int size,
            IReadOnlyList<string> requiredTerrainKeys,
            out GridPlacementFailureReason failureReason,
            string ignoredOccupantId = null)
        {
            if (size.x <= 0 || size.y <= 0)
            {
                failureReason = GridPlacementFailureReason.InvalidSize;
                return false;
            }

            var footprint = new GridFootprint(origin, size);
            if (!Contains(footprint))
            {
                failureReason = GridPlacementFailureReason.OutOfBounds;
                return false;
            }

            foreach (var position in footprint.Positions())
            {
                var cell = GetCell(position);
                if (!cell.IsBuildable)
                {
                    failureReason = GridPlacementFailureReason.NotBuildable;
                    return false;
                }

                if (!cell.HasAllTerrainKeys(requiredTerrainKeys))
                {
                    failureReason = GridPlacementFailureReason.TerrainMismatch;
                    return false;
                }

                if (cell.IsOccupied && cell.OccupantId != ignoredOccupantId)
                {
                    failureReason = GridPlacementFailureReason.Occupied;
                    return false;
                }
            }

            failureReason = GridPlacementFailureReason.None;
            return true;
        }

        public bool TryOccupy(GridPosition origin, Vector2Int size, string occupantId)
        {
            return TryOccupy(origin, size, occupantId, out _);
        }

        public bool TryOccupy(GridPosition origin, Vector2Int size, string occupantId, out GridPlacementFailureReason failureReason)
        {
            return TryOccupy(origin, size, occupantId, null, out failureReason);
        }

        public bool TryOccupy(
            GridPosition origin,
            Vector2Int size,
            string occupantId,
            IReadOnlyList<string> requiredTerrainKeys,
            out GridPlacementFailureReason failureReason)
        {
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
            foreach (var position in footprint.Positions())
            {
                GetCell(position).Occupy(occupantId);
            }

            failureReason = GridPlacementFailureReason.None;
            return true;
        }

        public int ClearOccupant(string occupantId, ICollection<GridPosition> clearedPositions = null)
        {
            if (string.IsNullOrWhiteSpace(occupantId))
            {
                return 0;
            }

            var clearedCount = 0;
            foreach (var cell in Cells)
            {
                if (cell.OccupantId != occupantId)
                {
                    continue;
                }

                cell.ClearOccupant();
                clearedPositions?.Add(cell.Position);
                clearedCount++;
            }

            return clearedCount;
        }

        public int ClearOccupants(GridFootprint footprint, string requiredOccupantId = null, ICollection<GridPosition> clearedPositions = null)
        {
            if (!Contains(footprint))
            {
                return 0;
            }

            var clearedCount = 0;
            foreach (var position in footprint.Positions())
            {
                var cell = GetCell(position);
                if (!cell.IsOccupied)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(requiredOccupantId) && cell.OccupantId != requiredOccupantId)
                {
                    continue;
                }

                cell.ClearOccupant();
                clearedPositions?.Add(position);
                clearedCount++;
            }

            return clearedCount;
        }
    }
}
