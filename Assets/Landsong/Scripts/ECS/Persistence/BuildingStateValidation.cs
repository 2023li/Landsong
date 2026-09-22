using System;
using System.Collections.Generic;
using System.IO;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS.Persistence
{
    internal static class BuildingStateValidation
    {
        internal static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot data, BuildingSnapshot record)
        {
            if (!BuildingDefinitions.IsValid(em, root, record.Definition))
                throw new InvalidDataException("Invalid building definition");
            bool prefabFound = false;
            if (em.HasBuffer<BuildingPrefab>(root))
                foreach (var entry in em.GetBuffer<BuildingPrefab>(root))
                    if (entry.Definition == record.Definition && entry.Prefab != Entity.Null && em.Exists(entry.Prefab))
                    {
                        prefabFound = true;
                        break;
                    }

            if (!prefabFound)
                throw new InvalidDataException("Missing building prefab");
            ref var definition = ref BuildingDefinitions.Get(em, root, record.Definition);
            SnapshotValidation.Health(record.Health);
            var workforce = record.BuildingWorkforce;
            int capacity = 0;
            for (int i = 0; i < definition.Capabilities.Workforce.Levels.Length; i++)
            {
                var row = definition.Capabilities.Workforce.Levels[i];
                if (row.Level == 0 || row.Level == record.Building.Level)
                    capacity = row.Capacity;
            }

            if (record.Building.Level < 1 || record.Building.Level > definition.MaximumLevel || (byte)record.Building.Stage > (byte)LifeStage.Repairing || workforce.Workers < 0 || record.BuildingHousing.Population < 0 || workforce.SubsidyBudget < 0 || workforce.SubsidyBudget > Math.Max(0, capacity) || workforce.PaidSubsidy < 0 || workforce.PaidSubsidyTurn < 0 || workforce.PaidSubsidyTurn > data.Clock.Turn)
                throw new InvalidDataException("Invalid building state");
            if (record.BuildingFarming.Crop.IsValid && !CropDefinitions.IsValid(em, root, record.BuildingFarming.Crop))
                throw new InvalidDataException("Invalid crop definition");
            if (record.Food == null || record.Offers == null || record.ExpeditionHistory == null || record.Investment == null || record.RepairMaterials == null)
                throw new InvalidDataException("Missing building buffers");
            foreach (var food in record.Food)
                if (!ItemDefinitions.IsValid(em, root, food.Item) || !ItemGroupDefinitions.IsValid(em, root, food.Group))
                    throw new InvalidDataException("Invalid residential food selection");
            foreach (var cost in record.Investment)
                if (!ItemDefinitions.IsValid(em, root, cost.Item) || cost.Amount < 0)
                    throw new InvalidDataException("Invalid building investment");
            foreach (var cost in record.RepairMaterials)
                if (!ItemDefinitions.IsValid(em, root, cost.Item) || cost.Amount < 0)
                    throw new InvalidDataException("Invalid repair material");
        }
    }
}
