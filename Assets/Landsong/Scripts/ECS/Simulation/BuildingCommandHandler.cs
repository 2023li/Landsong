using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class BuildingCommandHandler
    {
        public static ResultCode Execute(EntityManager em, Entity root, BuildRequest request) => BuildingPlacementCommands.Build(em, root, request);
        public static ResultCode Execute(EntityManager em, Entity root, BuildRoadRequest request) => BuildingRoadOps.Build(em, root, request);
        public static ResultCode Execute(EntityManager em, Entity root, MoveBuildingRequest request) => BuildingPlacementCommands.Move(em, root, WorldQueries.Find(em, request.Building), GridOps.Cell(em.GetComponentData<GridData>(root), request.Position), request.Rotation);
        public static ResultCode Execute(EntityManager em, Entity root, DemolishBuildingRequest request) => BuildingLifecycle.Demolish(em, root, WorldQueries.Find(em, request.Building));
        public static ResultCode Execute(EntityManager em, Entity root, RepairBuildingRequest request) => BuildingLifecycle.Repair(em, root, WorldQueries.Find(em, request.Building));
        public static ResultCode Execute(EntityManager em, Entity root, UpgradeBuildingRequest request) => BuildingUpgradeCommands.Apply(em, root, WorldQueries.Find(em, request.Building));
        public static ResultCode Execute(EntityManager em, Entity root, RenameBuildingRequest request) => BuildingNaming.Rename(em, WorldQueries.Find(em, request.Building), request.Name);
        public static ResultCode Execute(EntityManager em, Entity root, ChangeWorkersRequest request) => WorkforceSettlement.ChangeWorkers(em, root, WorldQueries.Find(em, request.Building), request.Delta);
        public static ResultCode Execute(EntityManager em, Entity root, SetWorkforceTargetRequest request) => WorkforceOps.SetTarget(em, root, WorldQueries.Find(em, request.Building), request.DesiredWorkers);
        public static ResultCode Execute(EntityManager em, Entity root, SetWorkforceBudgetRequest request)
        {
            var target = WorldQueries.Find(em, request.Building);
            return request.Relative == 1 ? WorkforceOps.AdjustBudget(em, root, target, request.Budget) : request.Relative == 0 ? WorkforceOps.SetBudget(em, root, target, request.Budget) : ResultCode.InvalidContent;
        }

        public static ResultCode Execute(EntityManager em, Entity root, ChangeBuildingSkinRequest request)
        {
            var target = WorldQueries.Find(em, request.Building);
            if (!BuildingStatus.Operational(em, target) || !em.HasBuffer<BuildingVisualSlot>(target) || request.Skin.Length > 60)
                return ResultCode.InvalidTarget;
            bool found = false;
            foreach (var slot in em.GetBuffer<BuildingVisualSlot>(target))
                if (slot.Purpose == BuildingVisualPurpose.Operational && slot.Skin == request.Skin)
                {
                    found = true;
                    break;
                }

            if (!found)
                return ResultCode.InvalidContent;
            BuildingAppearanceState stateAppearance = em.GetComponentData<BuildingAppearanceState>(target);
            stateAppearance.Skin = request.Skin;
            {
                em.SetComponentData(target, stateAppearance);
            }

            return ResultCode.Success;
        }

        public static ResultCode Execute(EntityManager em, Entity root, ClearCropRequest request)
        {
            var target = WorldQueries.Find(em, request.Building);
            if (!BuildingStatus.Operational(em, target))
                return ResultCode.InvalidTarget;
            BuildingFarmingState stateFarming = em.GetComponentData<BuildingFarmingState>(target);
            if (!stateFarming.Crop.IsValid)
                return ResultCode.Unavailable;
            stateFarming.Crop = CropId.None;
            stateFarming.Progress = 0;
            stateFarming.Seed = 0;
            stateFarming.FullCycle = 0;
            {
                em.SetComponentData(target, stateFarming);
            }

            BuildingChangeNotifications.Publish(em, root);
            return ResultCode.Success;
        }

        public static ResultCode Execute(EntityManager em, Entity root, RecruitWorkersRequest request)
        {
            if (request.Count <= 0 || request.ExpectedGoldCostPerWorker < 0)
                return ResultCode.InvalidContent;
            var target = WorldQueries.Find(em, request.Building);
            if (!BuildingStatus.Operational(em, target))
                return ResultCode.InvalidTarget;
            if (WorkforceOps.Quote(em, root, target).RecruitCost != request.ExpectedGoldCostPerWorker)
                return ResultCode.Unavailable;
            return WorkforceSettlement.ChangeWorkers(em, root, target, request.Count);
        }

        public static ResultCode Execute(EntityManager em, Entity root, SetAutoHarvestRequest request)
        {
            var target = WorldQueries.Find(em, request.Building);
            if (!BuildingStatus.Operational(em, target))
                return ResultCode.InvalidTarget;
            BuildingFarmingState stateFarming = em.GetComponentData<BuildingFarmingState>(target);
            stateFarming.AutoHarvest = (byte)(request.Enabled != 0 ? 1 : 0);
            {
                em.SetComponentData(target, stateFarming);
            }

            return ResultCode.Success;
        }

        public static ResultCode Execute(EntityManager em, Entity root, SetOfferingRequest request)
        {
            var target = WorldQueries.Find(em, request.Building);
            if (!BuildingStatus.Operational(em, target))
                return ResultCode.InvalidTarget;
            BuildingSanctumState stateSanctum = em.GetComponentData<BuildingSanctumState>(target);
            stateSanctum.Offering = (byte)(request.Enabled != 0 ? 1 : 0);
            {
                em.SetComponentData(target, stateSanctum);
            }

            return ResultCode.Success;
        }

        public static ResultCode Execute(EntityManager em, Entity root, SetSubsidyRequest request)
        {
            var target = WorldQueries.Find(em, request.Building);
            if (!BuildingStatus.Operational(em, target))
                return ResultCode.InvalidTarget;
            return WorkforceOps.SetTarget(em, root, target, request.Enabled == 0 ? 0 : request.TargetWorkers > 0 ? request.TargetWorkers : em.GetComponentData<BuildingWorkforceStats>(target).Capacity);
        }

        public static ResultCode Execute(EntityManager em, Entity root, PlantCropRequest request)
        {
            var target = WorldQueries.Find(em, request.Building);
            if (!BuildingStatus.Operational(em, target) || !CropDefinitions.IsValid(em, root, request.Crop))
                return ResultCode.InvalidContent;
            var state = em.GetComponentData<Building>(target);
            BuildingFarmingState stateFarming = em.GetComponentData<BuildingFarmingState>(target);
            if (stateFarming.Crop.IsValid)
                return ResultCode.Busy;
            ref var crops = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(target).Definition).Capabilities.Farming.Crops;
            bool available = false;
            for (int i = 0; i < crops.Length; i++)
                if ((crops[i].Level == 0 || crops[i].Level == state.Level) && crops[i].Crop == request.Crop)
                {
                    available = true;
                    break;
                }

            ref var costs = ref CropDefinitions.Get(em, root, request.Crop).PlantingCosts;
            if (!available || !InventoryPayments.Pay(em, root, ref costs))
                return ResultCode.Unavailable;
            stateFarming.Crop = request.Crop;
            stateFarming.Progress = 0;
            stateFarming.FullCycle = 1;
            stateFarming.Seed = SimulationRandom.NextRandom(em, root);
            {
                em.SetComponentData(target, state);
                em.SetComponentData(target, stateFarming);
            }

            return ResultCode.Success;
        }

        public static ResultCode Execute(EntityManager em, Entity root, HarvestBuildingRequest request)
        {
            var target = WorldQueries.Find(em, request.Building);
            if (!BuildingStatus.Operational(em, target))
                return ResultCode.InvalidTarget;
            var state = em.GetComponentData<Building>(target);
            BuildingFarmingState stateFarming = em.GetComponentData<BuildingFarmingState>(target);
            BuildingGatheringState stateGathering = em.GetComponentData<BuildingGatheringState>(target);
            if (stateFarming.Crop.IsValid)
                return CropHarvest.Harvest(em, root, target, false);
            ref var gathering = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(target).Definition).Capabilities.Gathering;
            int selected = -1;
            for (int i = 0; i < gathering.Levels.Length; i++)
                if (gathering.Levels[i].Level == 0 || gathering.Levels[i].Level == state.Level)
                {
                    selected = i;
                    break;
                }

            if (selected < 0 || stateGathering.RemainingUses <= 0)
                return ResultCode.Unavailable;
            var harvest = gathering.Levels[selected];
            using (var transaction = new InventoryTransaction(em, root))
            {
                if (harvest.Item.IsValid)
                {
                    if (InventoryOps.Add(em, root, harvest.Item, harvest.AmountPerUse) != harvest.AmountPerUse)
                        return ResultCode.NoCapacity;
                }
                else if (stateGathering.RemainingUses == 1)
                {
                    for (int i = 0; i < gathering.Rewards.Length; i++)
                    {
                        var reward = gathering.Rewards[i];
                        if (reward.Level != 0 && reward.Level != state.Level)
                            continue;
                        if (InventoryOps.Add(em, root, reward.Item, reward.Quantity) != reward.Quantity)
                            return ResultCode.NoCapacity;
                    }
                }

                transaction.Commit();
            }

            stateGathering.RemainingUses--;
            {
                em.SetComponentData(target, state);
                em.SetComponentData(target, stateFarming);
                em.SetComponentData(target, stateGathering);
            }

            if (stateGathering.RemainingUses <= 0)
            {
                GridOps.Occupy(em, root, target, true);
                em.DestroyEntity(target);
            }

            return ResultCode.Success;
        }
    }
}
