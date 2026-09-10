using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.GridSystem
{
    [Serializable]
    public struct GridFootprint
    {
        [SerializeField] private GridPosition origin;
        [SerializeField] private Vector2Int baseSize;
        [SerializeField] private BuildingOrientation orientation;
        [SerializeField] private int elevationLevel;
        [SerializeField, Min(0)] private int surfaceLayer;

        public GridFootprint(GridPosition origin, Vector2Int size)
            : this(origin, size, BuildingOrientation.North)
        {
        }

        public GridFootprint(
            GridPosition origin,
            Vector2Int baseSize,
            BuildingOrientation orientation,
            int elevationLevel = 0,
            int surfaceLayer = 0)
        {
            if (baseSize.x <= 0 || baseSize.y <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseSize), "Grid footprint size must be positive.");
            }

            this.origin = origin;
            this.baseSize = baseSize;
            this.orientation = BuildingOrientationUtility.Normalize(orientation);
            this.elevationLevel = elevationLevel;
            this.surfaceLayer = Mathf.Max(0, surfaceLayer);
        }

        public GridPosition Origin => origin;
        public Vector2Int BaseSize => baseSize;
        public BuildingOrientation Orientation => BuildingOrientationUtility.Normalize(orientation);
        public Vector2Int EffectiveSize => Orientation.GetEffectiveSize(baseSize);
        public Vector2Int Size => EffectiveSize;
        public int ElevationLevel => elevationLevel;
        public int SurfaceLayer => Mathf.Max(0, surfaceLayer);
        private int MinX => origin.X;
        private int MinZ => origin.Z;
        private int MaxXExclusive => origin.X + EffectiveSize.x;
        private int MaxZExclusive => origin.Z + EffectiveSize.y;

        public GridFootprint WithOrigin(GridPosition newOrigin)
        {
            return new GridFootprint(
                newOrigin,
                baseSize,
                Orientation,
                elevationLevel,
                SurfaceLayer);
        }

        public bool Contains(GridPosition position)
        {
            return position.X >= MinX
                   && position.X < MaxXExclusive
                   && position.Z >= MinZ
                   && position.Z < MaxZExclusive;
        }

        public IEnumerable<GridPosition> Positions()
        {
            for (var z = MinZ; z < MaxZExclusive; z++)
            {
                for (var x = MinX; x < MaxXExclusive; x++)
                {
                    yield return new GridPosition(x, z);
                }
            }
        }
    }
}
