using System;
using UnityEngine;

namespace Landsong.GridSystem
{
    public sealed class GridLayoutService
    {
        public const float IsometricDiamondHeightRatio = 0.5f;

        private readonly UnityEngine.Grid unityGrid;

        public GridLayoutService(UnityEngine.Grid unityGrid)
        {
            if (unityGrid == null)
            {
                throw new ArgumentNullException(nameof(unityGrid));
            }

            this.unityGrid = unityGrid;
            CellSize = GetCellSize(unityGrid);
            Origin = GetOrigin(unityGrid);
            PlaneMode = GetPlaneMode(unityGrid);
        }

        public GridLayoutService(float cellSize, Vector3 origin, GridPlaneMode planeMode = GridPlaneMode.XZ)
        {
            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Grid cell size must be positive.");
            }

            CellSize = cellSize;
            Origin = origin;
            PlaneMode = planeMode;
        }

        public float CellSize { get; }
        public Vector3 Origin { get; }
        public GridPlaneMode PlaneMode { get; }
        public bool IsIsometricDiamond => PlaneMode == GridPlaneMode.IsometricDiamondXY || PlaneMode == GridPlaneMode.IsometricDiamondXZ;
        public float IsometricDiamondWidth => CellSize;
        public float IsometricDiamondHeight => CellSize * IsometricDiamondHeightRatio;

        public static float GetCellSize(UnityEngine.Grid grid)
        {
            if (grid == null)
            {
                return 1f;
            }

            return Mathf.Max(0.01f, Mathf.Abs(grid.cellSize.x));
        }

        public static Vector3 GetOrigin(UnityEngine.Grid grid)
        {
            return grid == null ? Vector3.zero : grid.CellToWorld(Vector3Int.zero);
        }

        public static GridPlaneMode GetPlaneMode(UnityEngine.Grid grid)
        {
            if (grid == null)
            {
                return GridPlaneMode.XY;
            }

            switch (grid.cellLayout)
            {
                case GridLayout.CellLayout.Isometric:
                case GridLayout.CellLayout.IsometricZAsY:
                    return GridPlaneMode.IsometricDiamondXY;
                case GridLayout.CellLayout.Rectangle:
                case GridLayout.CellLayout.Hexagon:
                default:
                    return GridPlaneMode.XY;
            }
        }

        public GridPosition WorldToGridPosition(Vector3 worldPosition)
        {
            var gridPoint = WorldToGridPoint(worldPosition);
            return new GridPosition(Mathf.FloorToInt(gridPoint.x), Mathf.FloorToInt(gridPoint.y));
        }

        public Vector2 WorldToGridPoint(Vector3 worldPosition)
        {
            if (unityGrid != null)
            {
                var localPosition = unityGrid.transform.InverseTransformPoint(worldPosition);
                var cellPosition = unityGrid.LocalToCellInterpolated(localPosition);
                return new Vector2(cellPosition.x, cellPosition.y);
            }

            var local = worldPosition - Origin;
            switch (PlaneMode)
            {
                case GridPlaneMode.XZ:
                    return new Vector2(local.x / CellSize, local.z / CellSize);
                case GridPlaneMode.XY:
                    return new Vector2(local.x / CellSize, local.y / CellSize);
                case GridPlaneMode.IsometricDiamondXY:
                    return IsometricWorldToGridPoint(local.x, local.y);
                case GridPlaneMode.IsometricDiamondXZ:
                    return IsometricWorldToGridPoint(local.x, local.z);
                default:
                    throw new ArgumentOutOfRangeException(nameof(PlaneMode), PlaneMode, "Unsupported grid plane mode.");
            }
        }

        public Vector3 GridToWorldMin(GridPosition position)
        {
            return GridToWorldPoint(position.X, position.Y);
        }

        public Vector3 GridToWorldCenter(GridPosition position)
        {
            return GridToWorldPoint(position.X + 0.5f, position.Y + 0.5f);
        }

        public Vector3 GridToWorldPoint(float gridX, float gridY)
        {
            if (unityGrid != null)
            {
                var localPosition = unityGrid.CellToLocalInterpolated(new Vector3(gridX, gridY, 0f));
                return unityGrid.transform.TransformPoint(localPosition);
            }

            switch (PlaneMode)
            {
                case GridPlaneMode.XZ:
                    return Origin + new Vector3(gridX * CellSize, 0f, gridY * CellSize);
                case GridPlaneMode.XY:
                    return Origin + new Vector3(gridX * CellSize, gridY * CellSize, 0f);
                case GridPlaneMode.IsometricDiamondXY:
                {
                    var point = IsometricGridToWorldPoint(gridX, gridY);
                    return Origin + new Vector3(point.x, point.y, 0f);
                }
                case GridPlaneMode.IsometricDiamondXZ:
                {
                    var point = IsometricGridToWorldPoint(gridX, gridY);
                    return Origin + new Vector3(point.x, 0f, point.y);
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(PlaneMode), PlaneMode, "Unsupported grid plane mode.");
            }
        }

        public Vector3 GetCellWorldSize(float thickness = 0.02f)
        {
            switch (PlaneMode)
            {
                case GridPlaneMode.XZ:
                    return new Vector3(CellSize, thickness, CellSize);
                case GridPlaneMode.XY:
                    return new Vector3(CellSize, CellSize, thickness);
                case GridPlaneMode.IsometricDiamondXY:
                    return new Vector3(IsometricDiamondWidth, IsometricDiamondHeight, thickness);
                case GridPlaneMode.IsometricDiamondXZ:
                    return new Vector3(IsometricDiamondWidth, thickness, IsometricDiamondHeight);
                default:
                    throw new ArgumentOutOfRangeException(nameof(PlaneMode), PlaneMode, "Unsupported grid plane mode.");
            }
        }

        public Vector3[] GetCellCorners(GridPosition position)
        {
            return new[]
            {
                GridToWorldPoint(position.X, position.Y),
                GridToWorldPoint(position.X + 1, position.Y),
                GridToWorldPoint(position.X + 1, position.Y + 1),
                GridToWorldPoint(position.X, position.Y + 1)
            };
        }

        public bool TryRaycastToGridPlane(Ray ray, out Vector3 worldPosition)
        {
            var plane = PlaneMode == GridPlaneMode.XZ || PlaneMode == GridPlaneMode.IsometricDiamondXZ
                ? new UnityEngine.Plane(Vector3.up, Origin)
                : new UnityEngine.Plane(Vector3.forward, Origin);

            if (!plane.Raycast(ray, out var enter))
            {
                worldPosition = default;
                return false;
            }

            worldPosition = ray.GetPoint(enter);
            return true;
        }

        public bool TryGetGridPosition(Ray ray, out GridPosition position)
        {
            if (!TryRaycastToGridPlane(ray, out var worldPosition))
            {
                position = default;
                return false;
            }

            position = WorldToGridPosition(worldPosition);
            return true;
        }

        private Vector2 IsometricWorldToGridPoint(float worldX, float worldY)
        {
            var halfWidth = IsometricDiamondWidth * 0.5f;
            var halfHeight = IsometricDiamondHeight * 0.5f;
            var projectedX = worldX / halfWidth;
            var projectedY = worldY / halfHeight;
            var gridX = (projectedX + projectedY) * 0.5f;
            var gridY = (projectedY - projectedX) * 0.5f;

            return new Vector2(gridX, gridY);
        }

        private Vector2 IsometricGridToWorldPoint(float gridX, float gridY)
        {
            var halfWidth = IsometricDiamondWidth * 0.5f;
            var halfHeight = IsometricDiamondHeight * 0.5f;
            return new Vector2((gridX - gridY) * halfWidth, (gridX + gridY) * halfHeight);
        }
    }
}
