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
            PlaneNormal = GetPlaneNormal(unityGrid);
        }

        public Vector3 Origin { get; }
        public GridPlaneMode PlaneMode { get; }
        public Vector3 PlaneNormal { get; }

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

            var worldNormal = grid.transform.TransformDirection(Vector3.forward).normalized;
            var isWorldXZ = Mathf.Abs(Vector3.Dot(worldNormal, Vector3.up)) >= 0.999f;
            switch (grid.cellLayout)
            {
                case GridLayout.CellLayout.Isometric:
                case GridLayout.CellLayout.IsometricZAsY:
                    return isWorldXZ
                        ? GridPlaneMode.IsometricDiamondXZ
                        : GridPlaneMode.IsometricDiamondXY;
                case GridLayout.CellLayout.Rectangle:
                case GridLayout.CellLayout.Hexagon:
                default:
                    return isWorldXZ ? GridPlaneMode.XZ : GridPlaneMode.XY;
            }
        }

        public static Vector3 GetPlaneNormal(UnityEngine.Grid grid)
        {
            if (grid == null)
            {
                return Vector3.forward;
            }

            var normal = grid.transform.TransformDirection(Vector3.forward).normalized;
            // A +90 degree X rotation maps Unity Grid local XY to TWC local XZ,
            // but points the local forward vector downward. Keep height offsets and
            // ray hit normals consistently on the visible/top side of the map.
            if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) >= 0.999f
                && Vector3.Dot(normal, Vector3.up) < 0f)
            {
                normal = -normal;
            }

            return normal;
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
            return GridToWorldPoint(position.X + 0.5f, position.Z + 0.5f);
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
                GridToWorldPoint(position.X, position.Z),
                GridToWorldPoint(position.X + 1, position.Z),
                GridToWorldPoint(position.X + 1, position.Z + 1),
                GridToWorldPoint(position.X, position.Z + 1)
            };
        }

        public bool TryRaycastToGridPlane(Ray ray, out Vector3 worldPosition)
        {
            return TryRaycastToGridPlane(ray, 0f, out worldPosition, out _);
        }

        public bool TryRaycastToGridPlane(
            Ray ray,
            float heightOffset,
            out Vector3 worldPosition,
            out float distance)
        {
            var plane = new UnityEngine.Plane(
                PlaneNormal,
                Origin + PlaneNormal * heightOffset);

            if (!plane.Raycast(ray, out distance))
            {
                worldPosition = default;
                distance = 0f;
                return false;
            }

            worldPosition = ray.GetPoint(distance);
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
