using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingCapabilitiesCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingCapabilitiesSource source, ref global::Landsong.ECS.Definitions.BuildingCapabilities target, BuildingCatalogIndex buildingIndex, CropCatalogIndex cropIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, ItemGroupCatalogIndex itemGroupIndex, SoldierCatalogIndex soldierIndex, StorageSlotCatalogIndex storageSlotIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingCapabilities 配置。");
            BuildingConstructionCompiler.Compile(ref builder, source.Construction, ref target.Construction, itemIndex);
            BuildingUpgradeCompiler.Compile(ref builder, source.Upgrade, ref target.Upgrade, itemIndex);
            BuildingMaintenanceCompiler.Compile(ref builder, source.Maintenance, ref target.Maintenance, itemIndex);
            BuildingProductionCompiler.Compile(ref builder, source.Production, ref target.Production, itemIndex);
            BuildingQuestsCompiler.Compile(ref builder, source.Quests, ref target.Quests, itemIndex);
            BuildingPlacementCompiler.Compile(ref builder, source.Placement, ref target.Placement);
            BuildingTerrainConnectionCompiler.Compile(ref builder, source.Connection, ref target.Connection);
            BuildingStorageCompiler.Compile(ref builder, source.Storage, ref target.Storage, storageSlotIndex);
            BuildingWorkforceCompiler.Compile(ref builder, source.Workforce, ref target.Workforce, itemIndex);
            BuildingHousingCompiler.Compile(ref builder, source.Housing, ref target.Housing, itemIndex, itemGroupIndex);
            BuildingResearchCompiler.Compile(ref builder, source.Research, ref target.Research);
            BuildingFarmingCompiler.Compile(ref builder, source.Farming, ref target.Farming, cropIndex);
            BuildingGatheringCompiler.Compile(ref builder, source.Gathering, ref target.Gathering, itemIndex);
            BuildingGarrisonCompiler.Compile(ref builder, source.Garrison, ref target.Garrison, soldierIndex);
            BuildingSanctumCompiler.Compile(ref builder, source.Sanctum, ref target.Sanctum, heroIndex);
            BuildingMarketCompiler.Compile(ref builder, source.Market, ref target.Market, itemIndex);
            BuildingEffectsCompiler.Compile(ref builder, source.Effects, ref target.Effects, buildingIndex);
            BuildingDefenceCompiler.Compile(ref builder, source.Defence, ref target.Defence, technologyIndex);
            BuildingExpeditionsCompiler.Compile(ref builder, source.Expeditions, ref target.Expeditions);
        }
    }
}
