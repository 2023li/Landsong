using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.GridSystem
{
    [Serializable]
    public struct GridFootprint
    {
        [SerializeField] private GridPosition origin;
        [SerializeField] private Vector2Int size;

        public GridFootprint(GridPosition origin, Vector2Int size)
        {
            if (size.x <= 0 || size.y <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Grid footprint size must be positive.");
            }

            this.origin = origin;
            this.size = size;
        }

        public GridPosition Origin => origin;
        public Vector2Int Size => size;
        public int MinX => origin.X;
        public int MinY => origin.Y;
        public int MaxXExclusive => origin.X + size.x;
        public int MaxYExclusive => origin.Y + size.y;

        public bool Contains(GridPosition position)
        {
            return position.X >= MinX
                   && position.X < MaxXExclusive
                   && position.Y >= MinY
                   && position.Y < MaxYExclusive;
        }

        public IEnumerable<GridPosition> Positions()
        {
            for (var y = MinY; y < MaxYExclusive; y++)
            {
                for (var x = MinX; x < MaxXExclusive; x++)
                {
                    yield return new GridPosition(x, y);
                }
            }
        }
    }
}
