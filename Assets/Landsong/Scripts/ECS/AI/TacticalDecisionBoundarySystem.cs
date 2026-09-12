using Opsive.BehaviorDesigner.Runtime.Groups;
using Unity.Entities;

namespace Landsong.ECS.AI
{
    /// <summary>由 AI 适配程序集提供排序桥；核心战斗组不引用行为树插件。</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BehaviorTreeSystemGroup))]
    [UpdateBefore(typeof(CombatResolutionGroup))]
    public partial struct TacticalDecisionBoundarySystem : ISystem
    {
        public void OnUpdate(ref SystemState state) { }
    }
}
