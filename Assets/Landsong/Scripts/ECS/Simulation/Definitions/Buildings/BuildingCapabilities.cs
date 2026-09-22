using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingCapabilities
    {
        public BuildingConstruction Construction;
        public BuildingUpgrade Upgrade;
        public BuildingMaintenance Maintenance;
        public BuildingProduction Production;
        public BuildingQuests Quests;
        public BuildingPlacement Placement;
        public BuildingTerrainConnection Connection;
        public BuildingStorage Storage;
        public BuildingWorkforce Workforce;
        public BuildingHousing Housing;
        public BuildingResearch Research;
        public BuildingFarming Farming;
        public BuildingGathering Gathering;
        public BuildingGarrison Garrison;
        public BuildingSanctum Sanctum;
        public BuildingMarket Market;
        public BuildingEffects Effects;
        public BuildingDefence Defence;
        public BuildingExpeditions Expeditions;
    }
}
