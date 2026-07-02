using System;
using System.Collections.Generic;

namespace Landsong.GridSystem
{
    public readonly struct GridManhattanPathNode
    {
        public GridManhattanPathNode(GridPosition position, int actionCost)
        {
            Position = position;
            ActionCost = actionCost < 0 ? 0 : actionCost;
        }

        public GridPosition Position { get; }
        public int ActionCost { get; }
    }

    public static class GridManhattanPathfinder
    {
        private static readonly GridPosition[] Directions =
        {
            new GridPosition(1, 0),
            new GridPosition(-1, 0),
            new GridPosition(0, 1),
            new GridPosition(0, -1)
        };

        public static List<GridManhattanPathNode> FindReachable(
            GridMapBehaviour gridMap,
            IEnumerable<GridPosition> startPositions,
            int maxActionCost,
            Func<GridPosition, bool> canEnter,
            Func<GridPosition, int> getActionCost = null)
        {
            var reachable = new List<GridManhattanPathNode>();
            if (gridMap == null || startPositions == null || maxActionCost < 0)
            {
                return reachable;
            }

            canEnter ??= position => gridMap.HasBaseTileAt(position);
            getActionCost ??= position => gridMap.GetTraversalActionCost(position);

            var open = new List<GridManhattanPathNode>();
            var bestCosts = new Dictionary<GridPosition, int>();

            foreach (var start in startPositions)
            {
                if (!canEnter(start))
                {
                    continue;
                }

                if (bestCosts.TryGetValue(start, out var knownCost) && knownCost <= 0)
                {
                    continue;
                }

                bestCosts[start] = 0;
                open.Add(new GridManhattanPathNode(start, 0));
            }

            while (open.Count > 0)
            {
                var cheapestIndex = GetCheapestOpenNodeIndex(open);
                var current = open[cheapestIndex];
                open.RemoveAt(cheapestIndex);

                if (bestCosts.TryGetValue(current.Position, out var knownCost) && knownCost < current.ActionCost)
                {
                    continue;
                }

                reachable.Add(current);

                for (var i = 0; i < Directions.Length; i++)
                {
                    var direction = Directions[i];
                    var neighbor = new GridPosition(
                        current.Position.X + direction.X,
                        current.Position.Y + direction.Y);

                    if (!canEnter(neighbor))
                    {
                        continue;
                    }

                    var stepActionCost = getActionCost(neighbor);
                    if (stepActionCost <= 0 || stepActionCost == int.MaxValue)
                    {
                        continue;
                    }

                    var nextActionCost = current.ActionCost + stepActionCost;
                    if (nextActionCost < current.ActionCost)
                    {
                        continue;
                    }

                    if (nextActionCost > maxActionCost)
                    {
                        continue;
                    }

                    if (bestCosts.TryGetValue(neighbor, out knownCost) && knownCost <= nextActionCost)
                    {
                        continue;
                    }

                    bestCosts[neighbor] = nextActionCost;
                    open.Add(new GridManhattanPathNode(neighbor, nextActionCost));
                }
            }

            return reachable;
        }

        private static int GetCheapestOpenNodeIndex(IReadOnlyList<GridManhattanPathNode> open)
        {
            var cheapestIndex = 0;
            var cheapestCost = open[0].ActionCost;

            for (var i = 1; i < open.Count; i++)
            {
                if (open[i].ActionCost >= cheapestCost)
                {
                    continue;
                }

                cheapestIndex = i;
                cheapestCost = open[i].ActionCost;
            }

            return cheapestIndex;
        }
    }
}
