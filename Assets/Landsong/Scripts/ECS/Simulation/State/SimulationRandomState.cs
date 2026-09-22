using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct SimulationRandomState : IComponentData
    {
        public uint State;
    }
}
