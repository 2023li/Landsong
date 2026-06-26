using System;
using UnityEngine;

namespace Landsong.GridSystem
{
    [Serializable]
    public struct GridPosition : IEquatable<GridPosition>
    {
        [SerializeField] private int x;
        [SerializeField] private int y;

        public GridPosition(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public int X => x;
        public int Y => y;

        public static GridPosition Zero => new GridPosition(0, 0);

        public Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, y);
        }

        public bool Equals(GridPosition other)
        {
            return x == other.x && y == other.y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ y;
            }
        }

        public override string ToString()
        {
            return $"({x}, {y})";
        }

        public static GridPosition operator +(GridPosition left, GridPosition right)
        {
            return new GridPosition(left.x + right.x, left.y + right.y);
        }

        public static GridPosition operator -(GridPosition left, GridPosition right)
        {
            return new GridPosition(left.x - right.x, left.y - right.y);
        }

        public static bool operator ==(GridPosition left, GridPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridPosition left, GridPosition right)
        {
            return !left.Equals(right);
        }
    }
}
