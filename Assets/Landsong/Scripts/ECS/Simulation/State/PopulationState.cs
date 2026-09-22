using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct PopulationState : IComponentData
    {
        // Map population plus the signed adjustment for losses beyond housed residents.
        public int BasePopulation;
    }
}
