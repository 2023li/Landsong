using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingFarmingState : IComponentData
    {
        public CropId Crop;
        // Accumulated growth in tenths; a former one-turn step is 10.
        public int Progress;
        public uint Seed;
        public byte FullCycle;
        public byte AutoHarvest;
    }
}
