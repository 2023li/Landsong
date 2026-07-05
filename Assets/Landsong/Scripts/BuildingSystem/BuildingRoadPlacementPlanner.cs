using System;
using System.Collections.Generic;
using Landsong.BuildingSystem.Buildings;
using Landsong.GridSystem;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public static class BuildingRoadPlacementPlanner
    {
        private static readonly List<GridPosition> CandidateA = new List<GridPosition>();
        private static readonly List<GridPosition> CandidateB = new List<GridPosition>();

        public static void SelectRoadPath(
            GridMapBehaviour gridMap,
            IReadOnlyList<BuildingBase> buildings,
            GridPosition start,
            GridPosition end,
            BuildingDefinition definition,
            List<GridPosition> selectedPath,
            out bool canPlace)
        {
            selectedPath.Clear();
            canPlace = false;

            if (definition == null || gridMap == null)
            {
                return;
            }

            BuildSingleTurnRoadPath(start, end, true, CandidateA);
            BuildSingleTurnRoadPath(start, end, false, CandidateB);

            var invalidHorizontalFirstCells = CountInvalidRoadPathCells(gridMap, buildings, CandidateA, definition);
            var invalidVerticalFirstCells = CountInvalidRoadPathCells(gridMap, buildings, CandidateB, definition);

            var useHorizontalFirst = invalidHorizontalFirstCells == 0
                                     || (invalidVerticalFirstCells != 0
                                         && invalidHorizontalFirstCells <= invalidVerticalFirstCells);
            var selectedCandidate = useHorizontalFirst ? CandidateA : CandidateB;
            var selectedInvalidCells = useHorizontalFirst ? invalidHorizontalFirstCells : invalidVerticalFirstCells;

            selectedPath.AddRange(selectedCandidate);
            canPlace = selectedPath.Count > 0 && selectedInvalidCells == 0;
        }

        public static bool CanPlaceRoadPath(
            GridMapBehaviour gridMap,
            IReadOnlyList<BuildingBase> buildings,
            IReadOnlyList<GridPosition> roadPath,
            BuildingDefinition definition)
        {
            return roadPath != null
                   && roadPath.Count > 0
                   && CountInvalidRoadPathCells(gridMap, buildings, roadPath, definition) == 0;
        }

        public static int CountInvalidRoadPathCells(
            GridMapBehaviour gridMap,
            IReadOnlyList<BuildingBase> buildings,
            IReadOnlyList<GridPosition> roadPath,
            BuildingDefinition definition)
        {
            if (roadPath == null || roadPath.Count == 0 || definition == null || gridMap == null)
            {
                return int.MaxValue;
            }

            var invalidCells = 0;
            for (var i = 0; i < roadPath.Count; i++)
            {
                if (!IsRoadPathOriginValid(gridMap, buildings, roadPath[i], definition))
                {
                    invalidCells++;
                }
            }

            return invalidCells;
        }

        public static void CollectRoadOriginsToPlace(
            GridMapBehaviour gridMap,
            IReadOnlyList<BuildingBase> buildings,
            IReadOnlyList<GridPosition> roadPath,
            BuildingDefinition definition,
            List<GridPosition> output)
        {
            output.Clear();
            if (roadPath == null || definition == null)
            {
                return;
            }

            for (var i = 0; i < roadPath.Count; i++)
            {
                var origin = roadPath[i];
                if (IsExistingRoadAt(gridMap, buildings, origin, definition))
                {
                    continue;
                }

                output.Add(origin);
            }
        }

        public static bool IsRoadPathOriginValid(
            GridMapBehaviour gridMap,
            IReadOnlyList<BuildingBase> buildings,
            GridPosition origin,
            BuildingDefinition definition)
        {
            if (definition == null || gridMap == null)
            {
                return false;
            }

            return gridMap.CanOccupy(origin, definition.Size, definition.RequiredTerrainKeys, out _)
                   || IsExistingRoadAt(gridMap, buildings, origin, definition);
        }

        public static bool IsExistingRoadAt(
            GridMapBehaviour gridMap,
            IReadOnlyList<BuildingBase> buildings,
            GridPosition origin,
            BuildingDefinition definition)
        {
            if (definition == null || gridMap == null)
            {
                return false;
            }

            var footprint = new GridFootprint(origin, definition.Size);
            foreach (var position in footprint.Positions())
            {
                if (!TryGetExistingRoadAt(gridMap, buildings, position, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetExistingRoadAt(
            GridMapBehaviour gridMap,
            IReadOnlyList<BuildingBase> buildings,
            GridPosition position,
            out RoadBuilding road)
        {
            road = null;
            if (gridMap == null
                || buildings == null
                || !gridMap.TryGetOccupantId(position, out var occupantId)
                || string.IsNullOrWhiteSpace(occupantId))
            {
                return false;
            }

            for (var i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] is not RoadBuilding candidate
                    || !candidate.HasPlacement
                    || candidate.GridMap != gridMap
                    || candidate.IsDemolishing
                    || !string.Equals(candidate.GridOccupancyId, occupantId, StringComparison.Ordinal))
                {
                    continue;
                }

                road = candidate;
                return true;
            }

            return false;
        }

        private static void BuildSingleTurnRoadPath(
            GridPosition start,
            GridPosition end,
            bool horizontalFirst,
            List<GridPosition> output)
        {
            output.Clear();

            var corner = horizontalFirst
                ? new GridPosition(end.X, start.Y)
                : new GridPosition(start.X, end.Y);

            AppendGridLine(start, corner, output, false);
            AppendGridLine(corner, end, output, true);
        }

        private static void AppendGridLine(GridPosition from, GridPosition to, List<GridPosition> output, bool skipFirst)
        {
            var x = from.X;
            var y = from.Y;
            if (!skipFirst)
            {
                output.Add(new GridPosition(x, y));
            }

            var stepX = to.X.CompareTo(from.X);
            var stepY = to.Y.CompareTo(from.Y);
            while (x != to.X || y != to.Y)
            {
                if (x != to.X)
                {
                    x += stepX;
                }
                else
                {
                    y += stepY;
                }

                output.Add(new GridPosition(x, y));
            }
        }
    }
}
