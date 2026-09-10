using System;
using UnityEngine;

namespace Landsong.GridSystem
{
    /// <summary>
    /// 建筑玩法朝向。禁止使用任意浮点欧拉角作为占地、存档或道路规则的真相。
    /// </summary>
    public enum BuildingOrientation
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public static class BuildingOrientationUtility
    {
        public static BuildingOrientation Normalize(BuildingOrientation orientation)
        {
            var quarterTurns = ((int)orientation % 4 + 4) % 4;
            return (BuildingOrientation)quarterTurns;
        }

        public static BuildingOrientation RotateClockwise(this BuildingOrientation orientation)
        {
            return Normalize((BuildingOrientation)((int)orientation + 1));
        }

        public static BuildingOrientation RotateCounterClockwise(this BuildingOrientation orientation)
        {
            return Normalize((BuildingOrientation)((int)orientation - 1));
        }

        public static bool SwapsFootprintAxes(this BuildingOrientation orientation)
        {
            orientation = Normalize(orientation);
            return orientation == BuildingOrientation.East || orientation == BuildingOrientation.West;
        }

        public static Vector2Int GetEffectiveSize(this BuildingOrientation orientation, Vector2Int baseSize)
        {
            baseSize = new Vector2Int(Mathf.Max(1, baseSize.x), Mathf.Max(1, baseSize.y));
            return orientation.SwapsFootprintAxes()
                ? new Vector2Int(baseSize.y, baseSize.x)
                : baseSize;
        }

        public static Quaternion ToWorldRotation(this BuildingOrientation orientation, GridPlaneMode planeMode)
        {
            var degrees = (int)Normalize(orientation) * 90f;
            return planeMode == GridPlaneMode.XZ || planeMode == GridPlaneMode.IsometricDiamondXZ
                ? Quaternion.Euler(0f, degrees, 0f)
                : Quaternion.Euler(0f, 0f, -degrees);
        }

        public static BuildingOrientation FromWorldRotation(Quaternion rotation, GridPlaneMode planeMode)
        {
            var angle = planeMode == GridPlaneMode.XZ || planeMode == GridPlaneMode.IsometricDiamondXZ
                ? rotation.eulerAngles.y
                : -rotation.eulerAngles.z;
            return Normalize((BuildingOrientation)Mathf.RoundToInt(angle / 90f));
        }

        public static string ToShortLabel(this BuildingOrientation orientation)
        {
            switch (Normalize(orientation))
            {
                case BuildingOrientation.East:
                    return "E";
                case BuildingOrientation.South:
                    return "S";
                case BuildingOrientation.West:
                    return "W";
                case BuildingOrientation.North:
                default:
                    return "N";
            }
        }
    }
}
