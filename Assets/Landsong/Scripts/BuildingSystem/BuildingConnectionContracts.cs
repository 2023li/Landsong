using System;
using System.Collections.Generic;
using Landsong.GridSystem;

namespace Landsong.BuildingSystem
{
    public static class BuildingConnectionTypes
    {
        public const string Resource = "Resource";

        public static string Normalize(string connectionTypeId)
        {
            return string.IsNullOrWhiteSpace(connectionTypeId) ? string.Empty : connectionTypeId.Trim();
        }
    }

    public interface IBuildingConnectionConsumer
    {
        IReadOnlyList<string> RequiredConnectionTypeIds { get; }
    }

    public interface IBuildingConnectionConsumerModule
    {
        IReadOnlyList<string> RequiredConnectionTypeIds { get; }
    }

    public interface IBuildingConnectionProviderSource
    {
        bool ProvidesConnectionType(string connectionTypeId);
        int GetConnectionProviderPriority(string connectionTypeId);
        bool IsConnectionProviderOperational(string connectionTypeId);
    }

    public readonly struct ResourceConsumerProbe
    {
        public ResourceConsumerProbe(
            GridMapBehaviour gridMap,
            GridFootprint footprint,
            int actionPower,
            string connectionTypeId,
            string ignoredOccupantId = null,
            BuildingBase runtimeBuilding = null)
        {
            GridMap = gridMap;
            Footprint = footprint;
            ActionPower = Math.Max(0, actionPower);
            ConnectionTypeId = BuildingConnectionTypes.Normalize(connectionTypeId);
            IgnoredOccupantId = string.IsNullOrWhiteSpace(ignoredOccupantId)
                ? string.Empty
                : ignoredOccupantId.Trim();
            RuntimeBuilding = runtimeBuilding;
        }

        public GridMapBehaviour GridMap { get; }
        public GridFootprint Footprint { get; }
        public int ActionPower { get; }
        public string ConnectionTypeId { get; }
        public string IgnoredOccupantId { get; }
        public BuildingBase RuntimeBuilding { get; }
        public bool IsValid => GridMap != null && !string.IsNullOrWhiteSpace(ConnectionTypeId);
    }

    public readonly struct ReachableConnectionProvider
    {
        public ReachableConnectionProvider(BuildingBase provider, int actionCost)
        {
            Provider = provider;
            ActionCost = Math.Max(0, actionCost);
        }

        public BuildingBase Provider { get; }
        public int ActionCost { get; }
        public bool IsValid => Provider != null;
    }

    public sealed class BuildingConnectionQueryResult
    {
        public BuildingConnectionQueryResult(
            ResourceConsumerProbe probe,
            IReadOnlyList<GridManhattanPathNode> reachableNodes,
            IReadOnlyList<ReachableConnectionProvider> reachableProviders,
            ResourceProviderSelection selection)
        {
            Probe = probe;
            ReachableNodes = reachableNodes ?? Array.Empty<GridManhattanPathNode>();
            ReachableProviders = reachableProviders ?? Array.Empty<ReachableConnectionProvider>();
            Selection = selection;
        }

        public ResourceConsumerProbe Probe { get; }
        public IReadOnlyList<GridManhattanPathNode> ReachableNodes { get; }
        public IReadOnlyList<ReachableConnectionProvider> ReachableProviders { get; }
        public ResourceProviderSelection Selection { get; }
        public bool HasSelectedProvider => Selection.IsValid;
    }
}
