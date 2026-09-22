using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct IntelligenceModeState : IComponentData
    {
        public byte Enabled;
    }
}
