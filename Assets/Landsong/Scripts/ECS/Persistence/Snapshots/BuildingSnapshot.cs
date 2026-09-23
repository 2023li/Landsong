using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Landsong.ECS.Persistence
{
    public sealed class BuildingSnapshot : EntitySnapshot
    {
        public BuildingId Definition;
        public Health Health;
        public Building Building;
        public BuildingPlacementState BuildingPlacement;
        public BuildingAppearanceState BuildingAppearance;
        public BuildingConstructionState BuildingConstruction;
        public BuildingWorkforceState BuildingWorkforce;
        public BuildingHousingState BuildingHousing;
        public BuildingProductionState BuildingProduction;
        public BuildingFarmingState BuildingFarming;
        public BuildingFireState BuildingFire;
        public BuildingSanctumState BuildingSanctum;
        public BuildingGatheringState BuildingGathering;
        public BuildingRecruitmentState BuildingRecruitment;
        public BuildingMarketState BuildingMarket;
        public BuildingExperienceState BuildingExperience;
        public BuildingMaintenanceState BuildingMaintenance;
        public FoodSelection[] Food = Array.Empty<FoodSelection>();
        public QuestOfferSlot[] Offers = Array.Empty<QuestOfferSlot>();
        public ExpeditionDestinationHistory[] ExpeditionHistory = Array.Empty<ExpeditionDestinationHistory>();
        public BuildingInvestment[] Investment = Array.Empty<BuildingInvestment>();
        public RepairMaterial[] RepairMaterials = Array.Empty<RepairMaterial>();
    }

    internal static class BuildingSnapshotStorage
    {
        internal static BuildingSnapshot Capture(EntityManager em, Entity entity)
        {
            return new BuildingSnapshot
            {
                Identity = em.GetComponentData<Identity>(entity),
                Transform = em.HasComponent<LocalTransform>(entity) ? em.GetComponentData<LocalTransform>(entity) : LocalTransform.Identity,
                Definition = em.GetComponentData<BuildingDefinitionRef>(entity).Definition,
                Health = em.GetComponentData<Health>(entity),
                Building = em.GetComponentData<Building>(entity),
                BuildingPlacement = em.GetComponentData<BuildingPlacementState>(entity),
                BuildingAppearance = em.GetComponentData<BuildingAppearanceState>(entity),
                BuildingConstruction = em.GetComponentData<BuildingConstructionState>(entity),
                BuildingWorkforce = em.GetComponentData<BuildingWorkforceState>(entity),
                BuildingHousing = em.GetComponentData<BuildingHousingState>(entity),
                BuildingProduction = em.GetComponentData<BuildingProductionState>(entity),
                BuildingFarming = em.GetComponentData<BuildingFarmingState>(entity),
                BuildingFire = em.GetComponentData<BuildingFireState>(entity),
                BuildingSanctum = em.GetComponentData<BuildingSanctumState>(entity),
                BuildingGathering = em.GetComponentData<BuildingGatheringState>(entity),
                BuildingRecruitment = em.GetComponentData<BuildingRecruitmentState>(entity),
                BuildingMarket = em.GetComponentData<BuildingMarketState>(entity),
                BuildingExperience = em.GetComponentData<BuildingExperienceState>(entity),
                BuildingMaintenance = em.GetComponentData<BuildingMaintenanceState>(entity),
                Food = SnapshotBuffers.Capture<FoodSelection>(em, entity),
                Offers = SnapshotBuffers.Capture<QuestOfferSlot>(em, entity),
                ExpeditionHistory = SnapshotBuffers.Capture<ExpeditionDestinationHistory>(em, entity),
                Investment = SnapshotBuffers.Capture<BuildingInvestment>(em, entity),
                RepairMaterials = SnapshotBuffers.Capture<RepairMaterial>(em, entity),
            };
        }

        internal static void Write(BinaryWriter writer, BuildingSnapshot record)
        {
            SnapshotBinary.Write(writer, record.Identity);
            SnapshotBinary.Write(writer, record.Transform);
            SnapshotBinary.Write(writer, record.Definition);
            SnapshotBinary.Write(writer, record.Health);
            SnapshotBinary.Write(writer, record.Building);
            SnapshotBinary.Write(writer, record.BuildingPlacement);
            SnapshotBinary.Write(writer, record.BuildingAppearance);
            SnapshotBinary.Write(writer, record.BuildingConstruction);
            SnapshotBinary.Write(writer, record.BuildingWorkforce);
            SnapshotBinary.Write(writer, record.BuildingHousing);
            SnapshotBinary.Write(writer, record.BuildingProduction);
            SnapshotBinary.Write(writer, record.BuildingFarming);
            SnapshotBinary.Write(writer, record.BuildingSanctum);
            SnapshotBinary.Write(writer, record.BuildingGathering);
            SnapshotBinary.Write(writer, record.BuildingRecruitment);
            SnapshotBinary.Write(writer, record.BuildingMarket);
            SnapshotBinary.Write(writer, record.BuildingExperience);
            SnapshotBinary.Write(writer, record.BuildingMaintenance);
            SnapshotBuffers.Write(writer, record.Food);
            SnapshotBuffers.Write(writer, record.Offers);
            SnapshotBuffers.Write(writer, record.ExpeditionHistory);
            SnapshotBuffers.Write(writer, record.Investment);
            SnapshotBuffers.Write(writer, record.RepairMaterials);
            writer.Write(record.BuildingFire.Burning);
            writer.Write(record.BuildingFire.StartStrike);
            writer.Write(record.BuildingFire.StartedTurn);
            writer.Write(record.BuildingFire.DeadlinePhase);
            writer.Write(record.BuildingFire.FailedStation);
        }

        internal static BuildingSnapshot Read(BinaryReader reader, int version)
        {
            var record = new BuildingSnapshot
            {
                Identity = SnapshotBinary.Read<Identity>(reader),
                Transform = SnapshotBinary.Read<LocalTransform>(reader),
                Definition = SnapshotBinary.Read<BuildingId>(reader),
                Health = SnapshotBinary.Read<Health>(reader),
                Building = SnapshotBinary.Read<Building>(reader),
                BuildingPlacement = SnapshotBinary.Read<BuildingPlacementState>(reader),
                BuildingAppearance = SnapshotBinary.Read<BuildingAppearanceState>(reader),
                BuildingConstruction = SnapshotBinary.Read<BuildingConstructionState>(reader),
                BuildingWorkforce = SnapshotBinary.Read<BuildingWorkforceState>(reader),
                BuildingHousing = SnapshotBinary.Read<BuildingHousingState>(reader),
                BuildingProduction = SnapshotBinary.Read<BuildingProductionState>(reader),
                BuildingFarming = SnapshotBinary.Read<BuildingFarmingState>(reader),
                BuildingSanctum = SnapshotBinary.Read<BuildingSanctumState>(reader),
                BuildingGathering = SnapshotBinary.Read<BuildingGatheringState>(reader),
                BuildingRecruitment = SnapshotBinary.Read<BuildingRecruitmentState>(reader),
                BuildingMarket = SnapshotBinary.Read<BuildingMarketState>(reader),
                BuildingExperience = SnapshotBinary.Read<BuildingExperienceState>(reader),
                BuildingMaintenance = SnapshotBinary.Read<BuildingMaintenanceState>(reader),
                Food = SnapshotBuffers.Read<FoodSelection>(reader),
                Offers = SnapshotBuffers.Read<QuestOfferSlot>(reader),
                ExpeditionHistory = SnapshotBuffers.Read<ExpeditionDestinationHistory>(reader),
                Investment = SnapshotBuffers.Read<BuildingInvestment>(reader),
                RepairMaterials = SnapshotBuffers.Read<RepairMaterial>(reader),
            };
            if (version >= 35)
                record.BuildingFire = new BuildingFireState
                {
                    Burning = reader.ReadByte(),
                    StartStrike = reader.ReadByte(),
                    StartedTurn = reader.ReadInt32(),
                    DeadlinePhase = reader.ReadInt32(),
                    FailedStation = reader.ReadUInt64(),
                };
            return record;
        }

        internal static Entity Restore(EntityManager em, Entity root, BuildingSnapshot record)
        {
            var entity = BuildingCreation.Create(em, root, record.Definition, record.BuildingPlacement.Cell, record.BuildingPlacement.Rotation, record.Building.Level, false);
            EntityState.Set(em, entity, record.Identity);
            EntityState.Set(em, entity, record.Transform);
            EntityState.Set(em, entity, record.Health);
            EntityState.Set(em, entity, record.Building);
            EntityState.Set(em, entity, record.BuildingPlacement);
            EntityState.Set(em, entity, record.BuildingAppearance);
            EntityState.Set(em, entity, record.BuildingConstruction);
            EntityState.Set(em, entity, record.BuildingWorkforce);
            EntityState.Set(em, entity, record.BuildingHousing);
            EntityState.Set(em, entity, record.BuildingProduction);
            EntityState.Set(em, entity, record.BuildingFarming);
            EntityState.Set(em, entity, record.BuildingFire);
            EntityState.Set(em, entity, record.BuildingSanctum);
            EntityState.Set(em, entity, record.BuildingGathering);
            EntityState.Set(em, entity, record.BuildingRecruitment);
            EntityState.Set(em, entity, record.BuildingMarket);
            EntityState.Set(em, entity, record.BuildingExperience);
            EntityState.Set(em, entity, record.BuildingMaintenance);
            BuildingLevelConfiguration.Apply(em, root, entity, false);
            EntityState.Set(em, entity, record.Health);
            SnapshotBuffers.Restore(em, entity, record.Food);
            SnapshotBuffers.Restore(em, entity, record.Offers);
            SnapshotBuffers.Restore(em, entity, record.ExpeditionHistory);
            SnapshotBuffers.Restore(em, entity, record.Investment);
            SnapshotBuffers.Restore(em, entity, record.RepairMaterials);
            return entity;
        }
    }
}
