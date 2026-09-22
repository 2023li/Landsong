using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingMaintenance
    {
        public bool Enabled;
        public int RepairTurns;
        public BlobArray<BuildingMaintenanceCost> Costs;
        public BlobArray<BuildingRepairMaterial> Repairs;
    }
}
