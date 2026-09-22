using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildRequest : IGameRequest, IHistoryNamedRequest
    {
        public BuildingId Definition;
        public float3 Position;
        public int Rotation;
        public CommandKind Kind => CommandKind.Build;
        public ulong Target => 0;

        public FixedString128Bytes HistoryName(EntityManager em, Entity root) => BuildingDefinitions.IsValid(em, root, Definition) ? BuildingDefinitions.Get(em, root, Definition).Metadata.Name : new FixedString128Bytes("建筑");
    }

    public struct BuildRoadRequest : IGameRequest, IHistoryNamedRequest
    {
        public BuildingId Definition;
        public float3 Start, End;
        public CommandKind Kind => CommandKind.BuildRoad;
        public ulong Target => 0;

        public FixedString128Bytes HistoryName(EntityManager em, Entity root) => BuildingDefinitions.IsValid(em, root, Definition) ? BuildingDefinitions.Get(em, root, Definition).Metadata.Name : new FixedString128Bytes("建筑");
    }

    public struct MoveBuildingRequest : IGameRequest
    {
        public ulong Building;
        public float3 Position;
        public int Rotation;
        public CommandKind Kind => CommandKind.MoveBuilding;
        public ulong Target => Building;
    }

    public struct DemolishBuildingRequest : IGameRequest
    {
        public ulong Building;
        public CommandKind Kind => CommandKind.Demolish;
        public ulong Target => Building;
    }

    public struct RepairBuildingRequest : IGameRequest
    {
        public ulong Building;
        public CommandKind Kind => CommandKind.Repair;
        public ulong Target => Building;
    }

    public struct UpgradeBuildingRequest : IGameRequest
    {
        public ulong Building;
        public CommandKind Kind => CommandKind.Upgrade;
        public ulong Target => Building;
    }

    public struct HarvestBuildingRequest : IGameRequest
    {
        public ulong Building;
        public CommandKind Kind => CommandKind.Harvest;
        public ulong Target => Building;
    }

    public struct ClearCropRequest : IGameRequest
    {
        public ulong Building;
        public CommandKind Kind => CommandKind.ClearCrop;
        public ulong Target => Building;
    }

    public struct RenameBuildingRequest : IGameRequest
    {
        public ulong Building;
        public FixedString128Bytes Name;
        public CommandKind Kind => CommandKind.Rename;
        public ulong Target => Building;
    }

    public struct ChangeBuildingSkinRequest : IGameRequest
    {
        public ulong Building;
        public FixedString64Bytes Skin;
        public CommandKind Kind => CommandKind.ChangeBuildingSkin;
        public ulong Target => Building;
    }

    public struct ChangeWorkersRequest : IGameRequest
    {
        public ulong Building;
        public int Delta;
        public CommandKind Kind => CommandKind.Workers;
        public ulong Target => Building;
    }

    public struct RecruitWorkersRequest : IGameRequest
    {
        public ulong Building;
        public int Count, ExpectedGoldCostPerWorker;
        public CommandKind Kind => CommandKind.RecruitWorkerAtQuotedCost;
        public ulong Target => Building;
    }

    public struct SetWorkforceBudgetRequest : IGameRequest
    {
        public ulong Building;
        public int Budget;
        public byte Relative;
        public CommandKind Kind => CommandKind.WorkforceBudget;
        public ulong Target => Building;
    }

    public struct SetWorkforceTargetRequest : IGameRequest
    {
        public ulong Building;
        public int DesiredWorkers;
        public CommandKind Kind => CommandKind.WorkforceTarget;
        public ulong Target => Building;
    }

    public struct SetAutoHarvestRequest : IGameRequest
    {
        public ulong Building;
        public byte Enabled;
        public CommandKind Kind => CommandKind.AutoHarvest;
        public ulong Target => Building;
    }

    public struct SetOfferingRequest : IGameRequest
    {
        public ulong Building;
        public byte Enabled;
        public CommandKind Kind => CommandKind.Offering;
        public ulong Target => Building;
    }

    public struct SetSubsidyRequest : IGameRequest
    {
        public ulong Building;
        public byte Enabled;
        public int TargetWorkers;
        public CommandKind Kind => CommandKind.Subsidy;
        public ulong Target => Building;
    }

    public struct PlantCropRequest : IGameRequest
    {
        public ulong Building;
        public CropId Crop;
        public CommandKind Kind => CommandKind.Plant;
        public ulong Target => Building;
    }
}
