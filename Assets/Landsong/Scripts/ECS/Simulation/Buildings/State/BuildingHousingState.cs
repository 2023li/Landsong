using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingHousingState : IComponentData
    {
        public int Population;
        public int Growth;
        public int FoodFailures;
        public int TaxProgress;
        public int DeferredResidents;
    }
}
