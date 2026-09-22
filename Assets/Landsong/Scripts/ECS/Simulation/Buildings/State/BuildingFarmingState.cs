using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingFarmingState : IComponentData
    {
        public CropId Crop;
        public int Progress;
        public uint Seed;
        public byte FullCycle;
        public byte AutoHarvest;
    }
}
