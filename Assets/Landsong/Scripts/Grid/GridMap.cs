using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.GridSystem
{
    public sealed class GridMap
    {
        private readonly GridCell[,] cells;

        public GridMap(int width, int height, bool defaultBuildable = true)
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
            cells = new GridCell[width, height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    cells[x, y] = new GridCell(new GridPosition(x, y), defaultBuildable);
                }
            }
        }

        public int Width { get; }
        public int Height { get; }

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

        public bool CanOccupy(GridPosition origin, Vector2Int size)
        {
            return CanOccupy(origin, size, out _);
        }

        public bool CanOccupy(GridPosition origin, Vector2Int size, out GridPlacementFailureReason failureReason, string ignoredOccupantId = null)
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
            if (string.IsNullOrWhiteSpace(occupantId))
            {
                failureReason = GridPlacementFailureReason.InvalidOccupantId;
                return false;
            }

            if (!CanOccupy(origin, size, out failureReason))
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

        public int ClearOccupant(string occupantId)
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
                clearedCount++;
            }

            return clearedCount;
        }

        public int ClearOccupants(GridFootprint footprint, string requiredOccupantId = null)
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
                clearedCount++;
            }

            return clearedCount;
        }
    }
}
