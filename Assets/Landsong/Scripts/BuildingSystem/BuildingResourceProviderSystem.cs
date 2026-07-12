using System;
using System.Collections.Generic;
using Landsong.GridSystem;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 资源提供点可按自身运行状态临时停止对外供给。
    /// 未实现此接口的资源提供点只要仍有效，默认可供给。
    /// </summary>
    public interface IBuildingResourceProviderOperationalState
    {
        bool IsResourceProviderOperational { get; }
    }

    /// <summary>
    /// 接收资源供给明细，并在整回合结束时进行统一结算的资源提供点。
    /// </summary>
    public interface IBuildingResourceProvisionAccounting : IBuildingResourceProviderOperationalState
    {
        void BeginResourceProvisionTurn();
        void RecordProvidedResource(BuildingBase consumer, BuildingResourceChange resource);
        void CompleteResourceProvisionTurn();
    }

    /// <summary>
    /// 一次资源消费选中的实际提供点及路径代价。
    /// </summary>
    public readonly struct ResourceProviderSelection
    {
        public ResourceProviderSelection(BuildingBase provider, int actionCost)
        {
            Provider = provider;
            ActionCost = Math.Max(0, actionCost);
        }

        public BuildingBase Provider { get; }
        public int ActionCost { get; }
        public bool IsValid => Provider != null;
    }

    /// <summary>
    /// 统一解析资源消费者实际使用的提供点。
    /// 规则：先选可达点中优先级最高的；同优先级时选路径行动力代价最低的；再以稳定键打破完全相同的并列。
    /// </summary>
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

            var buildings = consumer.GameSystem.Services.Buildings.Buildings;
            if (buildings == null || buildings.Count == 0)
            {
                return false;
            }

            var providerCells = new Dictionary<GridPosition, List<BuildingBase>>();
            var targetCells = new HashSet<GridPosition>();
            for (var i = 0; i < buildings.Count; i++)
            {
                var candidate = buildings[i];
                if (!CanUseAsProvider(consumer, candidate))
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

            if (targetCells.Count == 0)
            {
                return false;
            }

            var ownCells = CreateFootprintCellSet(consumer);
            var reachable = GridManhattanPathfinder.FindReachable(
                consumer.GridMap,
                ownCells,
                consumer.BuildingActionPower,
                position => CanEnterSearchCell(consumer, position, ownCells, targetCells),
                position => GetSearchActionCost(consumer, position, targetCells));

            BuildingBase bestProvider = null;
            var bestActionCost = int.MaxValue;
            for (var i = 0; i < reachable.Count; i++)
            {
                var node = reachable[i];
                if (!providerCells.TryGetValue(node.Position, out var providersAtCell))
                {
                    continue;
                }

                for (var j = 0; j < providersAtCell.Count; j++)
                {
                    var candidate = providersAtCell[j];
                    if (IsBetterCandidate(candidate, node.ActionCost, bestProvider, bestActionCost))
                    {
                        bestProvider = candidate;
                        bestActionCost = node.ActionCost;
                    }
                }
            }

            if (bestProvider == null)
            {
                return false;
            }

            selection = new ResourceProviderSelection(bestProvider, bestActionCost);
            return true;
        }

        public static void RecordProvidedResource(
            ResourceProviderSelection selection,
            BuildingBase consumer,
            BuildingResourceChange resource)
        {
            if (!selection.IsValid || !resource.IsValid)
            {
                return;
            }

            if (selection.Provider is IBuildingResourceProvisionAccounting accounting)
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
                if (buildings[i] is IBuildingResourceProvisionAccounting provider)
                {
                    action(provider);
                }
            }
        }

        private static bool CanUseAsProvider(BuildingBase consumer, BuildingBase candidate)
        {
            if (candidate == null
                || candidate == consumer
                || !candidate.isActiveAndEnabled
                || candidate.IsDemolishing
                || !candidate.HasPlacement
                || candidate.GridMap != consumer.GridMap
                || !candidate.IsResourceProviderPoint)
            {
                return false;
            }

            return candidate is not IBuildingResourceProviderOperationalState operational
                   || operational.IsResourceProviderOperational;
        }

        private static bool IsBetterCandidate(
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

            if (candidate.ResourceProviderPriority != bestCandidate.ResourceProviderPriority)
            {
                return candidate.ResourceProviderPriority > bestCandidate.ResourceProviderPriority;
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
                left.Definition == null ? string.Empty : left.Definition.BuildingId,
                right.Definition == null ? string.Empty : right.Definition.BuildingId,
                StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            return string.Compare(left.GridOccupancyId, right.GridOccupancyId, StringComparison.Ordinal);
        }

        private static HashSet<GridPosition> CreateFootprintCellSet(BuildingBase building)
        {
            var cells = new HashSet<GridPosition>();
            if (building == null || !building.HasPlacement)
            {
                return cells;
            }

            foreach (var position in building.Footprint.Positions())
            {
                cells.Add(position);
            }

            return cells;
        }

        private static bool CanEnterSearchCell(
            BuildingBase consumer,
            GridPosition position,
            HashSet<GridPosition> ownCells,
            HashSet<GridPosition> targetCells)
        {
            if (consumer.GridMap == null || !consumer.GridMap.HasBaseTileAt(position))
            {
                return false;
            }

            return ownCells.Contains(position)
                   || targetCells.Contains(position)
                   || consumer.GridMap.CanTraverse(position, consumer.GridOccupancyId);
        }

        private static int GetSearchActionCost(
            BuildingBase consumer,
            GridPosition position,
            HashSet<GridPosition> targetCells)
        {
            if (!targetCells.Contains(position) || consumer.GridMap.CanTraverse(position, consumer.GridOccupancyId))
            {
                return consumer.GridMap.GetTraversalActionCost(position, consumer.GridOccupancyId);
            }

            return consumer.GridMap.GetTerrainTraversalActionCost(position);
        }
    }
}
