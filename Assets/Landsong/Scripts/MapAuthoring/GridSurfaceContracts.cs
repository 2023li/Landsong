using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.GridSystem
{
    /// <summary>
    /// 单个逻辑格子的玩法地表信息。当前地图适配器统一返回零高度，字段用于未来离散高低差和桥面分层。
    /// </summary>
    [Serializable]
    public readonly struct GridSurfaceCell
    {
        public GridSurfaceCell(int elevationLevel, int surfaceLayer = 0)
        {
            ElevationLevel = elevationLevel;
            SurfaceLayer = Mathf.Max(0, surfaceLayer);
        }

        public int ElevationLevel { get; }
        public int SurfaceLayer { get; }
    }

    public interface IGridSurfaceData
    {
        bool TryGetSurfaceCell(GridPosition position, out GridSurfaceCell surfaceCell);
    }

    /// <summary>
    /// Placement-facing, read-only view of a baked map. Dynamic occupancy is
    /// deliberately excluded so editor previews and runtime placement share the
    /// same static terrain rules without coupling authoring code to scene state.
    /// </summary>
    public interface IGridPlacementSurfaceData : IGridSurfaceData
    {
        bool HasCell(GridPosition position);
        bool IsBuildable(GridPosition position);
        bool HasTerrainKey(GridPosition position, string terrainKey);
    }

    public static class GridPlacementRuleEvaluator
    {
        /// <summary>
        /// Resolves an X/Z footprint against the baked surface. The first cell chooses
        /// the logical height and surface layer; every occupied cell must match it.
        /// </summary>
        public static bool TryResolveFlatFootprint(
            IGridPlacementSurfaceData surface,
            GridPosition origin,
            Vector2Int baseSize,
            BuildingOrientation orientation,
            out GridFootprint footprint,
            out GridPlacementFailureReason failure,
            out GridPosition failureCell)
        {
            failureCell = origin;
            if (surface == null)
            {
                footprint = default;
                failure = GridPlacementFailureReason.OutOfBounds;
                return false;
            }

            if (baseSize.x <= 0 || baseSize.y <= 0)
            {
                footprint = default;
                failure = GridPlacementFailureReason.InvalidSize;
                return false;
            }

            if (!surface.TryGetSurfaceCell(origin, out var originSurface))
            {
                footprint = default;
                failure = GridPlacementFailureReason.OutOfBounds;
                return false;
            }

            footprint = new GridFootprint(
                origin,
                baseSize,
                orientation,
                originSurface.ElevationLevel,
                originSurface.SurfaceLayer);
            foreach (var position in footprint.Positions())
            {
                failureCell = position;
                if (!surface.TryGetSurfaceCell(position, out var surfaceCell))
                {
                    failure = GridPlacementFailureReason.OutOfBounds;
                    return false;
                }

                if (surfaceCell.ElevationLevel != footprint.ElevationLevel
                    || surfaceCell.SurfaceLayer != footprint.SurfaceLayer)
                {
                    failure = GridPlacementFailureReason.TerrainMismatch;
                    return false;
                }
            }

            failure = GridPlacementFailureReason.None;
            return true;
        }

        public static bool TryValidateStaticPlacement(
            IGridPlacementSurfaceData surface,
            GridFootprint footprint,
            IReadOnlyList<string> requiredTerrainKeys,
            IReadOnlyList<string> requiredAnyFootprintTerrainKeys,
            out GridPlacementFailureReason failure,
            out GridPosition failureCell)
        {
            failureCell = footprint.Origin;
            if (surface == null || footprint.EffectiveSize.x <= 0 || footprint.EffectiveSize.y <= 0)
            {
                failure = surface == null
                    ? GridPlacementFailureReason.OutOfBounds
                    : GridPlacementFailureReason.InvalidSize;
                return false;
            }

            foreach (var position in footprint.Positions())
            {
                failureCell = position;
                if (!surface.HasCell(position))
                {
                    failure = GridPlacementFailureReason.OutOfBounds;
                    return false;
                }

                if (!surface.IsBuildable(position))
                {
                    failure = GridPlacementFailureReason.NotBuildable;
                    return false;
                }

                if (!surface.TryGetSurfaceCell(position, out var surfaceCell)
                    || surfaceCell.ElevationLevel != footprint.ElevationLevel
                    || surfaceCell.SurfaceLayer != footprint.SurfaceLayer
                    || !HasAllTerrainKeys(surface, position, requiredTerrainKeys))
                {
                    failure = GridPlacementFailureReason.TerrainMismatch;
                    return false;
                }
            }

            if (!HasAnyRequiredTerrainWithinFootprint(
                    surface,
                    footprint,
                    requiredAnyFootprintTerrainKeys,
                    out failureCell))
            {
                failure = GridPlacementFailureReason.TerrainMismatch;
                return false;
            }

            failure = GridPlacementFailureReason.None;
            return true;
        }

        private static bool HasAllTerrainKeys(
            IGridPlacementSurfaceData surface,
            GridPosition position,
            IReadOnlyList<string> terrainKeys)
        {
            if (terrainKeys == null)
            {
                return true;
            }

            for (var i = 0; i < terrainKeys.Count; i++)
            {
                if (!surface.HasTerrainKey(position, terrainKeys[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasAnyRequiredTerrainWithinFootprint(
            IGridPlacementSurfaceData surface,
            GridFootprint footprint,
            IReadOnlyList<string> terrainKeys,
            out GridPosition failureCell)
        {
            failureCell = footprint.Origin;
            if (terrainKeys == null)
            {
                return true;
            }

            for (var keyIndex = 0; keyIndex < terrainKeys.Count; keyIndex++)
            {
                var found = false;
                foreach (var position in footprint.Positions())
                {
                    if (!surface.HasTerrainKey(position, terrainKeys[keyIndex]))
                    {
                        continue;
                    }

                    found = true;
                    break;
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
