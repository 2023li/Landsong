using Opsive.BehaviorDesigner.Runtime;
using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Groups;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS.AI
{
    public enum TacticalMode : byte { Dormant, Retreat, PlayerOrder, EngageEnemy, AttackBuilding, Patrol }

    // Only this authoring object is managed. Baking creates the buffer and enableable task flag.
    [Opsive.Shared.Utility.Category("Landsong/ECS")]
    [Opsive.Shared.Utility.Description("Choose one tactical action. Selector ordering in the authored tree determines priority; movement and damage remain independent ECS systems.")]
    public sealed class TacticalAction : ECSActionTask<TacticalActionSystem, TacticalActionData, TacticalActionFlag>
    {
        public TacticalMode Mode;
        public override TacticalActionData GetBufferElement() => new TacticalActionData { Index = RuntimeIndex, Mode = Mode, LastExecution = -1 };
    }
    public struct TacticalActionData : IBufferElementData { public ushort Index; public TacticalMode Mode; public float LastExecution; }
    public struct TacticalActionFlag : IComponentData, IEnableableComponent { }
    public struct BehaviorStarted : IComponentData { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PerceptionSystem))]
    [UpdateBefore(typeof(BehaviorTreeSystemGroup))]
    public partial struct StartTacticalBehaviorSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var query = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<Combatant>(), ComponentType.ReadOnly<TacticalActionData>() }, None = new[] { ComponentType.ReadOnly<BehaviorStarted>() } });
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var e in entities)
            {
                if (BehaviorTree.StartBakedBehaviorTree(state.WorldUnmanaged, e)) em.AddComponent<BehaviorStarted>(e);
            }
        }
    }

    [DisableAutoCreation]
    public partial struct TacticalActionSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<Session>();
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ActionJob { Session = SystemAPI.GetSingleton<Session>(), Positions = SystemAPI.GetComponentLookup<LocalTransform>(true), Health = SystemAPI.GetComponentLookup<Health>(true) }.ScheduleParallel(state.Dependency);
        }
        [BurstCompile]
        [WithAll(typeof(TacticalActionFlag), typeof(EvaluateFlag))]
        partial struct ActionJob : IJobEntity
        {
            public Session Session;
            [ReadOnly] public ComponentLookup<LocalTransform> Positions;
            [ReadOnly] public ComponentLookup<Health> Health;
            bool Alive(Entity e) => Health.HasComponent(e) && Health[e].Current > 0 && Positions.HasComponent(e);
            void Execute(Entity entity, DynamicBuffer<BranchComponent> branches, DynamicBuffer<TaskComponent> tasks, DynamicBuffer<TacticalActionData> actions,
                ref Combatant actor, ref Steering steering, ref UnitOrder order, in Perception perception, in LocalTransform transform, in Identity identity, in Health health, in TacticalState tactical)
            {
                for (var i = 0; i < actions.Length; i++)
                {
                    var data = actions[i]; var task = tasks[data.Index];
                    if (!branches[task.BranchIndex].CanExecute || (task.Status != TaskStatus.Queued && task.Status != TaskStatus.Running)) continue;
                    if (Session.Paused != 0) { steering.Moving = 0; continue; }
                    var active = (Session.Phase == Phase.Night || Session.Phase == Phase.Retreat) && actor.Deployed != 0 && health.Current > 0 && Session.Time >= actor.ProtectedUntil;
                    var valid = false; var destination = transform.Position; var target = Entity.Null;
                    switch (data.Mode)
                    {
                        case TacticalMode.Dormant: valid = !active; break;
                        case TacticalMode.Retreat:
                            valid = active && actor.Faction == 1 && (Session.Phase == Phase.Retreat || actor.HomeId == 0); destination = actor.Home; break;
                        case TacticalMode.PlayerOrder:
                            valid = active && actor.Faction == 0 && order.Kind != OrderKind.Automatic;
                            destination = order.Destination;
                            if (order.Kind == OrderKind.Focus)
                            {
                                valid &= Alive(order.Target); target = valid ? order.Target : Entity.Null;
                                if (valid) destination = Positions[target].Position;
                            }
                            else if (order.Kind != OrderKind.Recall && order.Kind != OrderKind.Capture && perception.EnemyInRange != 0)
                            {
                                target = perception.Enemy;
                                // A selected hero keeps moving/holding the ordered position while firing automatically.
                                if (order.Kind == OrderKind.Rally) destination = transform.Position;
                            }
                            if (valid && order.Kind != OrderKind.Recall && order.Kind != OrderKind.Rally && order.Kind != OrderKind.Capture && target == Entity.Null && math.distance(transform.Position, destination) < .8f && entity != Session.SelectedHero) order = default;
                            break;
                        case TacticalMode.EngageEnemy:
                            valid = active && (tactical.Returning != 0 || Alive(perception.Enemy)); target = tactical.Returning != 0 || tactical.HasSlot == 0 ? Entity.Null : perception.Enemy; destination = tactical.Returning != 0 ? tactical.Origin : tactical.HasSlot != 0 ? tactical.Engagement : transform.Position; break;
                        case TacticalMode.AttackBuilding:
                            valid = active && actor.Faction == 1 && Alive(perception.Building); target = tactical.HasSlot != 0 ? perception.Building : Entity.Null; destination = tactical.HasSlot != 0 ? tactical.Engagement : transform.Position; break;
                        case TacticalMode.Patrol:
                            valid = active && actor.Faction == 0;
                            var angle = (identity.Id % 17) * .3696f + math.floor(Session.Time / 3) * .7f;
                            destination = actor.Home + new float3(math.cos(angle), 0, math.sin(angle)) * (3 + identity.Id % 4);
                            break;
                    }
                    if (!valid) { task.Status = TaskStatus.Failure; tasks[data.Index] = task; continue; }
                    actor.Target = target; var distance = math.distance(transform.Position.xz, destination.xz);
                    var positionalOrder = data.Mode == TacticalMode.AttackBuilding || data.Mode == TacticalMode.EngageEnemy || data.Mode == TacticalMode.PlayerOrder && order.Kind != OrderKind.Focus;
                    steering.Destination = destination; steering.Moving = (byte)(active && distance > (target == Entity.Null || positionalOrder ? .45f : math.max(.3f, actor.Range * .8f)) ? 1 : 0);
                    // Yield for a simulation tick. The repeater cannot spin through successful actions in one frame.
                    if (task.Status == TaskStatus.Queued) { data.LastExecution = Session.Time; task.Status = TaskStatus.Running; }
                    else if (Session.Time > data.LastExecution || !active) task.Status = TaskStatus.Success;
                    actions[i] = data; tasks[data.Index] = task;
                }
            }
        }
    }
}
