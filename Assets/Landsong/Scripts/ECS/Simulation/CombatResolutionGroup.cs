using Unity.Entities;

namespace Landsong.ECS
{
    /// <summary>行为决策之后的战斗、移动和伤害阶段；外部 AI 通过同级排序边界接入。</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PerceptionSystem))]
    public partial class CombatResolutionGroup : ComponentSystemGroup { }
}
