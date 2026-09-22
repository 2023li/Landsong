using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct ExpeditionPenaltyState : IComponentData
    {
        public int Stacks;
        public int UntilTurn;
    }
}
