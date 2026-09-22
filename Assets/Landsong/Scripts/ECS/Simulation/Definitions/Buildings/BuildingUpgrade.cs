using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingUpgrade
    {
        public bool Enabled;
        public BlobArray<BuildingUpgradeCost> Costs;
        public BlobArray<BuildingUpgradeWorkers> Workers;
        public BlobArray<BuildingUpgradePopulation> Residents;
        public BlobArray<BuildingUpgradeMaintenance> MaintenanceRequirements;
        public BlobArray<BuildingExperience> Experience;
    }
}
