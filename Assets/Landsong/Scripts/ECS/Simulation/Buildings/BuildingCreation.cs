using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class BuildingCreation
    {
        public static Entity Create(EntityManager em, Entity root, BuildingId definition, int2 cell, int rotation, int level, bool complete)
        {
            ref var d = ref BuildingDefinitions.Get(em, root, definition);
            var size = (rotation & 1) == 0 ? d.Footprint : d.Footprint.yx;
            var grid = em.GetComponentData<GridData>(root);
            var e = BuildingEntities.Spawn(em, root, definition, GridOps.Position(grid, cell, size), true);
            bool connection = TerrainConnectionOps.TryGet(em, root, definition, out _);
            var at = GridOps.Index(grid, connection ? TerrainConnectionOps.Port(cell, d.Footprint, rotation, 0, 0) : cell);
            var ground = at < 0 ? default : grid.Value.Value.Cells[at];
            {
                EntityState.Set(em, e, new Building { Stage = complete ? LifeStage.Operational : LifeStage.Construction, Level = math.max(1, level) });
                EntityState.Set(em, e, new BuildingPlacementState() { Cell = cell, Size = size, Rotation = rotation, Elevation = ground.Elevation, Surface = ground.Surface });
                EntityState.Set(em, e, new BuildingAppearanceState() { Skin = new FixedString64Bytes(d.PlacementAndVisuals.DefaultSkin.ToString()) });
                EntityState.Set(em, e, new BuildingConstructionState() { });
                EntityState.Set(em, e, new BuildingWorkforceState() { });
                EntityState.Set(em, e, new BuildingHousingState() { });
                EntityState.Set(em, e, new BuildingProductionState() { });
                EntityState.Set(em, e, new BuildingFarmingState() { Crop = CropId.None, AutoHarvest = 1 });
                EntityState.Set(em, e, new BuildingSanctumState() { });
                EntityState.Set(em, e, new BuildingGatheringState() { });
                EntityState.Set(em, e, new BuildingRecruitmentState() { });
                EntityState.Set(em, e, new BuildingMarketState() { });
                EntityState.Set(em, e, new BuildingExperienceState() { });
                EntityState.Set(em, e, new BuildingMaintenanceState() { Maintained = 1 });
                EntityState.Set(em, e, new BuildingFireState());
            }

            EntityState.Set(em, e, new Health { Current = math.max(1, d.MaximumDurability), Maximum = math.max(1, d.MaximumDurability) });
            {
                EntityState.Set(em, e, new BuildingHousingStats());
                EntityState.Set(em, e, new BuildingWorkforceStats() { });
                EntityState.Set(em, e, new BuildingStorageStats() { });
                EntityState.Set(em, e, new BuildingGarrisonStats() { });
                EntityState.Set(em, e, new BuildingQuestStats() { });
                EntityState.Set(em, e, new BuildingIntelligenceStats() { });
                EntityState.Set(em, e, new BuildingSanctumStats() { });
                EntityState.Set(em, e, new BuildingRangeStats() { });
                EntityState.Set(em, e, new BuildingNavigationStats() { });
                EntityState.Set(em, e, new BuildingBellStats() { });
            }

            EntityState.Buffer<FoodSelection>(em, e);
            EntityState.Buffer<QuestOfferSlot>(em, e);
            EntityState.Buffer<ExpeditionDestinationHistory>(em, e);
            EntityState.Buffer<BuildingInvestment>(em, e);
            EntityState.Buffer<RepairMaterial>(em, e);
            BuildingCostOps.RecordInvestment(em, e, BuildingCostOps.DefinitionInvestment(em, root, definition, level, complete ? int.MaxValue : 0));
            var t = em.GetComponentData<LocalTransform>(e);
            t.Rotation = quaternion.RotateY(rotation * math.PI / 2);
            em.SetComponentData(e, t);
            if (connection)
            {
                t.Position.y = grid.Origin.y + TerrainConnectionOps.AnchorHeight(em, root, definition, cell, rotation);
                em.SetComponentData(e, t);
            }

            BuildingLevelConfiguration.Apply(em, root, e, true);
            GridOps.Occupy(em, root, e);
            if (complete)
                InventoryProviders.Provision(em, root, e);
            return e;
        }
    }
}
