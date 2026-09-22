using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct DaySettlementState : IComponentData
    {
        public int LastSettledTurn;
    }
}
