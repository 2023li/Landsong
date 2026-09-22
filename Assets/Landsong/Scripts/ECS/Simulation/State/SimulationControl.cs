using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct SimulationControl : IComponentData
    {
        public byte Paused;
    }
}
