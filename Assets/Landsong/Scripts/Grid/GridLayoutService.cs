using System;
using UnityEngine;

namespace Landsong.GridSystem
{
    public sealed class GridLayoutService
    {
        private readonly UnityEngine.Grid unityGrid;

        public GridLayoutService(UnityEngine.Grid unityGrid)
        {
            if (unityGrid == null)
            {
                throw new ArgumentNullException(nameof(unityGrid));
            }

            this.unityGrid = unityGrid;
            Origin = GetOrigin(unityGrid);
            PlaneMode = GetPlaneMode(unityGrid);
        }

        public Vector3 Origin { get; }
        public GridPlaneMode PlaneMode { get; }

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
            var localPosition = unityGrid.transform.InverseTransformPoint(worldPosition);
            var cellPosition = unityGrid.LocalToCellInterpolated(localPosition);
            return new Vector2(cellPosition.x, cellPosition.y);
        }

        public Vector3 GridToWorldCenter(GridPosition position)
        {
            return GridToWorldPoint(position.X + 0.5f, position.Y + 0.5f);
        }

        public Vector3 GridToWorldPoint(float gridX, float gridY)
        {
            var localPosition = unityGrid.CellToLocalInterpolated(new Vector3(gridX, gridY, 0f));
            return unityGrid.transform.TransformPoint(localPosition);
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
    }
}
