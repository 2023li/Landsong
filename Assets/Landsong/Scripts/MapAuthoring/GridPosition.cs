using System;
using UnityEngine;

namespace Landsong.GridSystem
{
    [Serializable]
    public struct GridPosition : IEquatable<GridPosition>
    {
        [SerializeField] private int x;
        [SerializeField] private int z;

        public GridPosition(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public int X => x;
        public int Z => z;

        public Vector3Int ToWorldPlaneVector3Int()
        {
            return new Vector3Int(x, 0, z);
        }

        public bool Equals(GridPosition other)
        {
            return x == other.x && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ z;
            }
        }

        public override string ToString()
        {
            return $"({x}, {z})";
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
