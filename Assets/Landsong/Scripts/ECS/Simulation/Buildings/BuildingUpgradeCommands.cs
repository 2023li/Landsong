using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class BuildingUpgradeCommands
    {
        public static BuildingActionQuote Check(EntityManager em, Entity root, Entity e)
        {
            var q = new BuildingActionQuote();
            if (!BuildingStatus.Operational(em, e))
                return q.Fail(ResultCode.InvalidTarget, "施工、荒废和修复中不能升级");
            if (em.GetComponentData<Session>(root).Phase != Phase.Day)
                return q.Fail(ResultCode.WrongPhase, "只能在白天升级");
            var b = em.GetComponentData<Building>(e);
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
            BuildingHousingState bHousing = em.GetComponentData<BuildingHousingState>(e);
            BuildingExperienceState bExperience = em.GetComponentData<BuildingExperienceState>(e);
            BuildingMaintenanceState bMaintenance = em.GetComponentData<BuildingMaintenanceState>(e);
            var definition = em.GetComponentData<BuildingDefinitionRef>(e).Definition;
            ref var d = ref BuildingDefinitions.Get(em, root, definition);
            q.Costs = BuildingCostOps.Upgrade(em, root, definition, b.Level + 1);
            if (b.Level >= d.MaximumLevel)
                return q.Fail(ResultCode.Unavailable, "已达到最高等级");
            if (!BuildingBlueprints.Has(em, root, definition, b.Level + 1))
                return q.Fail(ResultCode.MissingResearch, "尚未获得下一等级蓝图");
            if (WorkforceSettlement.Locked(em, em.GetComponentData<Identity>(e).Id))
                return q.Fail(ResultCode.Busy, "远征在途，不能升级驻地");
            ref var upgrade = ref d.Capabilities.Upgrade;
            int requiredExperience = 0;
            for (int i = 0; i < upgrade.Experience.Length; i++)
                if (upgrade.Experience[i].Level == 0 || upgrade.Experience[i].Level == b.Level)
                {
                    requiredExperience = upgrade.Experience[i].UpgradeExperience;
                    break;
                }

            if (bExperience.Experience < requiredExperience)
                return q.Fail(ResultCode.Unavailable, "经验不足：" + bExperience.Experience + "/" + requiredExperience);
            for (int i = 0; i < upgrade.Workers.Length; i++)
            {
                var row = upgrade.Workers[i];
                if ((row.TargetLevel == 0 || row.TargetLevel == b.Level + 1) && bWorkforce.Workers < row.Required)
                    return q.Fail(ResultCode.InsufficientPopulation, "升级需要工人：" + row.Required);
            }

            for (int i = 0; i < upgrade.Residents.Length; i++)
            {
                var row = upgrade.Residents[i];
                if ((row.TargetLevel == 0 || row.TargetLevel == b.Level + 1) && bHousing.Population < row.Required)
                    return q.Fail(ResultCode.InsufficientPopulation, "升级需要居民：" + row.Required);
            }

            for (int i = 0; i < upgrade.MaintenanceRequirements.Length; i++)
                if ((upgrade.MaintenanceRequirements[i].TargetLevel == 0 || upgrade.MaintenanceRequirements[i].TargetLevel == b.Level + 1) && bMaintenance.Maintained == 0)
                    return q.Fail(ResultCode.Unavailable, "须完成本回合维护");
            if (!BuildingCostOps.CanPay(em, root, q.Costs))
                return q.Fail(ResultCode.InsufficientResources, "升级材料不足");
            return q;
        }

        public static ResultCode Apply(EntityManager em, Entity root, Entity e)
        {
            var check = BuildingUpgradeCommands.Check(em, root, e);
            if (!check.Allowed)
                return check.Code;
            var b = em.GetComponentData<Building>(e);
            var definition = em.GetComponentData<BuildingDefinitionRef>(e).Definition;
            if (!BuildingCostOps.Pay(em, root, check.Costs))
                return ResultCode.InsufficientResources;
            BuildingCostOps.RecordInvestment(em, e, check.Costs);
            b.Level++;
            em.SetComponentData(e, b);
            BuildingLevelConfiguration.Apply(em, root, e, false);
            GridOps.Occupy(em, root, e);
            GarrisonOps.ReconcileGarrisons(em, root);
            BuildingChangeNotifications.Publish(em, root);
            return ResultCode.Success;
        }
    }
}
