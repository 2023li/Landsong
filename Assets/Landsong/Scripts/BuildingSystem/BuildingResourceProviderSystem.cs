using System;
using System.Collections.Generic;
using Landsong.GridSystem;

namespace Landsong.BuildingSystem
{
    public interface IBuildingResourceProviderOperationalState
    {
        bool IsResourceProviderOperational { get; }
    }

    public interface IBuildingResourceProvisionAccounting : IBuildingResourceProviderOperationalState
    {
        void BeginResourceProvisionTurn();
        void RecordProvidedResource(BuildingBase consumer, BuildingResourceChange resource);
        void CompleteResourceProvisionTurn();
    }

    public readonly struct ResourceProviderSelection
    {
        public ResourceProviderSelection(
            BuildingBase provider,
            int actionCost,
            IReadOnlyList<GridPosition> path = null)
        {
            Provider = provider;
            ActionCost = Math.Max(0, actionCost);
            Path = path ?? Array.Empty<GridPosition>();
        }

        public BuildingBase Provider { get; }
        public int ActionCost { get; }
        public IReadOnlyList<GridPosition> Path { get; }
        public bool IsValid => Provider != null;
    }

    public static class BuildingResourceProviderSystem
    {
        public static bool TrySelectProvider(BuildingBase consumer, out ResourceProviderSelection selection)
        {
            selection = default;
            if (consumer == null
                || !consumer.HasPlacement
                || consumer.GridMap == null
                || consumer.GameSystem?.Services?.Buildings == null)
            {
                return false;
            }

            var probe = new ResourceConsumerProbe(
                consumer.GridMap,
                consumer.Footprint,
                consumer.BuildingActionPower,
                BuildingConnectionTypes.Resource,
                consumer.GridOccupancyId,
                consumer);
            if (!TryQuery(probe, consumer.GameSystem.Services.Buildings.Buildings, out var result)
                || !result.HasSelectedProvider)
            {
                return false;
            }

            selection = result.Selection;
            return true;
        }

        public static bool TryQuery(
            ResourceConsumerProbe probe,
            IReadOnlyList<BuildingBase> buildings,
            out BuildingConnectionQueryResult result)
        {
            result = new BuildingConnectionQueryResult(probe, null, null, default);
            if (!probe.IsValid || buildings == null)
            {
                return false;
            }

            var providerCells = new Dictionary<GridPosition, List<BuildingBase>>();
            var targetCells = new HashSet<GridPosition>();
            for (var i = 0; i < buildings.Count; i++)
            {
                var candidate = buildings[i];
                if (!CanUseAsProvider(probe, candidate))
                {
                    continue;
                }

                foreach (var position in candidate.Footprint.Positions())
                {
                    targetCells.Add(position);
                    if (!providerCells.TryGetValue(position, out var providersAtCell))
                    {
                        providersAtCell = new List<BuildingBase>();
                        providerCells.Add(position, providersAtCell);
                    }

                    providersAtCell.Add(candidate);
                }
            }

            var ownCells = CreateFootprintCellSet(probe.Footprint);
            var pathResult = GridManhattanPathfinder.FindPaths(
                probe.GridMap,
                ownCells,
                probe.ActionPower,
                position => CanEnterSearchCell(probe, position, ownCells, targetCells),
                position => GetSearchActionCost(probe, position, targetCells));

            var providerBestCosts = new Dictionary<BuildingBase, int>();
            BuildingBase bestProvider = null;
            var bestActionCost = int.MaxValue;
            var bestTargetCell = default(GridPosition);
            for (var i = 0; i < pathResult.Nodes.Count; i++)
            {
                var node = pathResult.Nodes[i];
                if (!providerCells.TryGetValue(node.Position, out var providersAtCell))
                {
                    continue;
                }

                for (var j = 0; j < providersAtCell.Count; j++)
                {
                    var candidate = providersAtCell[j];
                    if (!providerBestCosts.TryGetValue(candidate, out var knownCost)
                        || node.ActionCost < knownCost)
                    {
                        providerBestCosts[candidate] = node.ActionCost;
                    }

                    if (IsBetterCandidate(probe, candidate, node.ActionCost, bestProvider, bestActionCost))
                    {
                        bestProvider = candidate;
                        bestActionCost = node.ActionCost;
                        bestTargetCell = node.Position;
                    }
                }
            }

            var reachableProviders = new List<ReachableConnectionProvider>(providerBestCosts.Count);
            foreach (var pair in providerBestCosts)
            {
                reachableProviders.Add(new ReachableConnectionProvider(pair.Key, pair.Value));
            }

            reachableProviders.Sort((left, right) =>
            {
                if (left.ActionCost != right.ActionCost)
                {
                    return left.ActionCost.CompareTo(right.ActionCost);
                }

                return CompareStableProviderKey(left.Provider, right.Provider);
            });

            ResourceProviderSelection selection = default;
            if (bestProvider != null)
            {
                pathResult.TryBuildPath(bestTargetCell, out var path);
                selection = new ResourceProviderSelection(bestProvider, bestActionCost, path);
            }

            result = new BuildingConnectionQueryResult(
                probe,
                pathResult.Nodes,
                reachableProviders,
                selection);
            return true;
        }

        public static void RecordProvidedResource(
            ResourceProviderSelection selection,
            BuildingBase consumer,
            BuildingResourceChange resource)
        {
            if (selection.IsValid
                && resource.IsValid
                && selection.Provider.TryGetCapability<IBuildingResourceProvisionAccounting>(out var accounting))
            {
                accounting.RecordProvidedResource(consumer, resource);
            }
        }

        public static void BeginResourceProvisionTurn(IReadOnlyList<BuildingBase> buildings)
        {
            ForEachAccountingProvider(buildings, provider => provider.BeginResourceProvisionTurn());
        }

        public static void CompleteResourceProvisionTurn(IReadOnlyList<BuildingBase> buildings)
        {
            ForEachAccountingProvider(buildings, provider => provider.CompleteResourceProvisionTurn());
        }

        private static void ForEachAccountingProvider(
            IReadOnlyList<BuildingBase> buildings,
            Action<IBuildingResourceProvisionAccounting> action)
        {
            if (buildings == null || action == null)
            {
                return;
            }

            for (var i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null
                    && buildings[i].TryGetCapability<IBuildingResourceProvisionAccounting>(out var provider))
                {
                    action(provider);
                }
            }
        }

        private static bool CanUseAsProvider(ResourceConsumerProbe probe, BuildingBase candidate)
        {
            if (candidate == null
                || candidate == probe.RuntimeBuilding
                || !candidate.isActiveAndEnabled
                || candidate.IsDemolishing
                || !candidate.HasPlacement
                || candidate.GridMap != probe.GridMap
                || !candidate.ProvidesConnectionType(probe.ConnectionTypeId))
            {
                return false;
            }

            return candidate.IsConnectionProviderOperational(probe.ConnectionTypeId);
        }

        private static bool IsBetterCandidate(
            ResourceConsumerProbe probe,
            BuildingBase candidate,
            int candidateActionCost,
            BuildingBase bestCandidate,
            int bestActionCost)
        {
            if (candidate == null)
            {
                return false;
            }

            if (bestCandidate == null)
            {
                return true;
            }

            var candidatePriority = candidate.GetConnectionProviderPriority(probe.ConnectionTypeId);
            var bestPriority = bestCandidate.GetConnectionProviderPriority(probe.ConnectionTypeId);
            if (candidatePriority != bestPriority)
            {
                return candidatePriority > bestPriority;
            }

            if (candidateActionCost != bestActionCost)
            {
                return candidateActionCost < bestActionCost;
            }

            return CompareStableProviderKey(candidate, bestCandidate) < 0;
        }

        private static int CompareStableProviderKey(BuildingBase left, BuildingBase right)
        {
            var comparison = left.Origin.X.CompareTo(right.Origin.X);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Origin.Y.CompareTo(right.Origin.Y);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(
                left.FamilyId,
                right.FamilyId,
                StringComparison.Ordinal);
            return comparison != 0
                ? comparison
                : string.Compare(left.GridOccupancyId, right.GridOccupancyId, StringComparison.Ordinal);
        }

        private static HashSet<GridPosition> CreateFootprintCellSet(GridFootprint footprint)
        {
            var cells = new HashSet<GridPosition>();
            foreach (var position in footprint.Positions())
            {
                cells.Add(position);
            }

            return cells;
        }

        private static bool CanEnterSearchCell(
            ResourceConsumerProbe probe,
            GridPosition position,
            HashSet<GridPosition> ownCells,
            HashSet<GridPosition> targetCells)
        {
            if (!probe.GridMap.HasBaseTileAt(position))
            {
                return false;
            }

            return ownCells.Contains(position)
                   || targetCells.Contains(position)
                   || probe.GridMap.CanTraverse(position, probe.IgnoredOccupantId);
        }

        private static int GetSearchActionCost(
            ResourceConsumerProbe probe,
            GridPosition position,
            HashSet<GridPosition> targetCells)
        {
            if (!targetCells.Contains(position)
                || probe.GridMap.CanTraverse(position, probe.IgnoredOccupantId))
            {
                return probe.GridMap.GetTraversalActionCost(position, probe.IgnoredOccupantId);
            }

            return probe.GridMap.GetTerrainTraversalActionCost(position);
        }
    }
}
