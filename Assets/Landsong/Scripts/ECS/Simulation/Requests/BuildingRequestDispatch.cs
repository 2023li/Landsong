using Unity.Entities;

namespace Landsong.ECS
{
    internal static class BuildingRequestDispatch
    {
        internal static bool TryExecute(EntityManager em, Entity root, Entity payload, out ResultCode result)
        {
            if (em.HasComponent<BuildRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<BuildRequest>(payload));
                return true;
            }

            if (em.HasComponent<BuildRoadRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<BuildRoadRequest>(payload));
                return true;
            }

            if (em.HasComponent<MoveBuildingRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<MoveBuildingRequest>(payload));
                return true;
            }

            if (em.HasComponent<DemolishBuildingRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<DemolishBuildingRequest>(payload));
                return true;
            }

            if (em.HasComponent<RepairBuildingRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<RepairBuildingRequest>(payload));
                return true;
            }

            if (em.HasComponent<UpgradeBuildingRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<UpgradeBuildingRequest>(payload));
                return true;
            }

            if (em.HasComponent<HarvestBuildingRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<HarvestBuildingRequest>(payload));
                return true;
            }

            if (em.HasComponent<ClearCropRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<ClearCropRequest>(payload));
                return true;
            }

            if (em.HasComponent<RenameBuildingRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<RenameBuildingRequest>(payload));
                return true;
            }

            if (em.HasComponent<ChangeBuildingSkinRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<ChangeBuildingSkinRequest>(payload));
                return true;
            }

            if (em.HasComponent<ChangeWorkersRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<ChangeWorkersRequest>(payload));
                return true;
            }

            if (em.HasComponent<RecruitWorkersRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<RecruitWorkersRequest>(payload));
                return true;
            }

            if (em.HasComponent<SetWorkforceBudgetRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<SetWorkforceBudgetRequest>(payload));
                return true;
            }

            if (em.HasComponent<SetWorkforceTargetRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<SetWorkforceTargetRequest>(payload));
                return true;
            }

            if (em.HasComponent<SetAutoHarvestRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<SetAutoHarvestRequest>(payload));
                return true;
            }

            if (em.HasComponent<SetOfferingRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<SetOfferingRequest>(payload));
                return true;
            }

            if (em.HasComponent<SetSubsidyRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<SetSubsidyRequest>(payload));
                return true;
            }

            if (em.HasComponent<PlantCropRequest>(payload))
            {
                result = BuildingCommandHandler.Execute(em, root, em.GetComponentData<PlantCropRequest>(payload));
                return true;
            }

            result = ResultCode.Unavailable;
            return false;
        }
    }
}
