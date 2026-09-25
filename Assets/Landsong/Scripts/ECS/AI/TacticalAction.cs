using System;
using Opsive.BehaviorDesigner.Runtime;
using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Groups;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Landsong.ECS.Definitions;
using LabelText = Sirenix.OdinInspector.LabelTextAttribute;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS.AI
{
    public struct TacticalPatrolKey : IEquatable<TacticalPatrolKey>
    {
        public ulong Home;
        public byte Sector;

        public TacticalPatrolKey(ulong home, int sector)
        {
            Home = home;
            Sector = (byte)sector;
        }

        public bool Equals(TacticalPatrolKey other) => Home == other.Home && Sector == other.Sector;
        public override bool Equals(object obj) => obj is TacticalPatrolKey other && Equals(other);
        public override int GetHashCode() => (int)math.hash(new uint3((uint)Home, (uint)(Home >> 32), Sector));
    }

    public struct TacticalPatrolPoint
    {
        public float3 Position;
        public float Cost;
        public float Budget;
    }

    public static class TacticalPatrolAreas
    {
        public const int SectorCount = 16;

        public static NativeParallelHashMap<TacticalPatrolKey, TacticalPatrolPoint> Build(EntityManager em, Entity root, Allocator allocator)
        {
            SurfaceNavigationGraph.Ensure(em, root);
            using var buildings = WorldQueries.Entities<Building>(em);
            var result = new NativeParallelHashMap<TacticalPatrolKey, TacticalPatrolPoint>(math.max(1, buildings.Length * SectorCount), allocator);
            var grid = em.GetComponentData<GridData>(root);
            var nodes = em.GetBuffer<SurfaceNavNode>(root);
            foreach (var building in buildings)
            {
                if (em.HasComponent<SimulationOwner>(building) && em.GetComponentData<SimulationOwner>(building).Root != root)
                    continue;
                var definition = em.GetComponentData<BuildingDefinitionRef>(building).Definition;
                if (!BuildingDefinitions.IsValid(em, root, definition))
                    continue;
                ref var buildingDefinition = ref BuildingDefinitions.Get(em, root, definition);
                if (!buildingDefinition.Capabilities.Garrison.Enabled)
                    continue;
                var budget = math.max(0, buildingDefinition.PlacementAndVisuals.SpawnExclusionPadding);
                var placement = em.GetComponentData<BuildingPlacementState>(building);
                var exitProbe = GridOps.Position(grid, placement.Cell + new int2(placement.Size.x, 0), new int2(1));
                if (!NavigationOps.TryNearestOpenOnSurface(em, root, exitProbe, 12, placement.Surface, placement.Elevation, out var start))
                    continue;

                // Patrol scoring visits every node by index, so a cell-to-node index would
                // allocate a large managed dictionary for every garrison without being used.
                using var reach = new NightSpatialOps.Reach(em, root, start, indexCells: false);
                var center = EntityState.Position(em, building).xz;
                var bestCost = new float[SectorCount];
                var bestDistance = new float[SectorCount];
                var bestPoint = new float3[SectorCount];
                for (int i = 0; i < SectorCount; i++)
                    bestCost[i] = -1;
                float globalCost = 0;
                float3 globalPoint = start;
                for (int nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
                {
                    var node = nodes[nodeIndex];
                    if (node.Open == 0 || node.Corridor != 0)
                        continue;
                    var cost = reach.DistanceAt(nodeIndex);
                    if (!math.isfinite(cost) || cost > budget + .001f)
                        continue;
                    var offset = node.Position.xz - center;
                    var distance = math.lengthsq(offset);
                    var angle = math.atan2(offset.y, offset.x);
                    if (angle < 0)
                        angle += math.PI * 2;
                    var sector = (int)math.round(angle * SectorCount / (math.PI * 2)) % SectorCount;
                    if (cost > bestCost[sector] + .001f || math.abs(cost - bestCost[sector]) <= .001f && distance > bestDistance[sector])
                    {
                        bestCost[sector] = cost;
                        bestDistance[sector] = distance;
                        bestPoint[sector] = node.Position;
                    }

                    if (cost > globalCost)
                    {
                        globalCost = cost;
                        globalPoint = node.Position;
                    }
                }

                var home = em.GetComponentData<Identity>(building).Id;
                for (int sector = 0; sector < SectorCount; sector++)
                {
                    var cost = bestCost[sector] >= 0 ? bestCost[sector] : globalCost;
                    var point = bestCost[sector] >= 0 ? bestPoint[sector] : globalPoint;
                    result.TryAdd(new TacticalPatrolKey(home, sector), new TacticalPatrolPoint { Position = point, Cost = cost, Budget = budget });
                }
            }

            return result;
        }
    }

    public enum TacticalMode : byte
    {
        [LabelText("休眠")]
        Dormant = 0,
        [LabelText("撤退")]
        Retreat = 1,
        [LabelText("执行玩家命令")]
        PlayerOrder = 2,
        [LabelText("交战")]
        EngageEnemy = 3,
        [LabelText("攻击建筑")]
        AttackBuilding = 4,
        [LabelText("巡逻")]
        Patrol = 5
    }

    // Only this authoring object is managed. Baking creates the buffer and enableable task flag.
    [Opsive.Shared.Utility.Category("Landsong/ECS")]
    [Opsive.Shared.Utility.Description("Choose one tactical action. Selector ordering in the authored tree determines priority; movement and damage remain independent ECS systems.")]
    public sealed class TacticalAction : ECSActionTask<TacticalActionSystem, TacticalActionData, TacticalActionFlag>
    {
        [LabelText("战术模式")]
        public TacticalMode Mode;
        public override TacticalActionData GetBufferElement() => new TacticalActionData
        {
            Index = RuntimeIndex,
            Mode = Mode,
            LastExecution = -1
        };
    }

    public struct TacticalActionData : IBufferElementData
    {
        public ushort Index;
        public TacticalMode Mode;
        public float LastExecution;
    }

    public struct TacticalActionFlag : IComponentData, IEnableableComponent
    {
    }

    public struct BehaviorStarted : IComponentData
    {
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PerceptionSystem))]
    [UpdateAfter(typeof(TacticalSlotSystem))]
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
                if (BehaviorTree.StartBakedBehaviorTree(state.WorldUnmanaged, e))
                    em.AddComponent<BehaviorStarted>(e);
            }
        }
    }

    [DisableAutoCreation]
    public partial struct TacticalActionSystem : ISystem
    {
        NativeParallelHashMap<TacticalPatrolKey, TacticalPatrolPoint> patrolAreas;
        EntityQuery buildingQuery;
        Entity patrolRoot;
        int patrolRevision, patrolBuildingCount;
        uint patrolOccupancyHash;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Session>();
            buildingQuery = state.GetEntityQuery(ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<BuildingDefinitionRef>(), ComponentType.ReadOnly<Identity>());
            patrolRevision = int.MinValue;
            patrolBuildingCount = -1;
        }

        public void OnDestroy(ref SystemState state)
        {
            state.Dependency.Complete();
            if (patrolAreas.IsCreated)
                patrolAreas.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<Session>();
            // Dusk publication rebuilds the navigation graph. Wait until its checkpoint
            // completes so the patrol cache is built in a later frame.
            if (SystemAPI.GetComponent<PersistenceGate>(root).CheckpointPending != 0)
                return;
            var revision = SystemAPI.GetComponent<GridData>(root).Revision;
            var buildingCount = buildingQuery.CalculateEntityCount();
            var hasNavCache = state.EntityManager.HasComponent<SurfaceNavCache>(root);
            var occupancyHash = hasNavCache ? state.EntityManager.GetComponentData<SurfaceNavCache>(root).OccupancyHash : 0;
            if (!patrolAreas.IsCreated || patrolRoot != root || patrolRevision != revision || patrolBuildingCount != buildingCount || !hasNavCache || patrolOccupancyHash != occupancyHash)
            {
                state.Dependency.Complete();
                if (patrolAreas.IsCreated)
                    patrolAreas.Dispose();
                patrolAreas = TacticalPatrolAreas.Build(state.EntityManager, root, Allocator.Persistent);
                patrolRoot = root;
                patrolRevision = revision;
                patrolBuildingCount = buildingCount;
                patrolOccupancyHash = state.EntityManager.GetComponentData<SurfaceNavCache>(root).OccupancyHash;
            }

            state.Dependency = new ActionJob
            {
                Session = SystemAPI.GetSingleton<Session>(),
                Clock = SystemAPI.GetSingleton<GameClock>(),
                NightTiming = SystemAPI.GetSingleton<NightSettings>(),
                NightPlan = SystemAPI.GetSingleton<NightPlanState>(),
                Control = SystemAPI.GetSingleton<SimulationControl>(),
                HeroSelection = SystemAPI.GetSingleton<HeroSelection>(),
                Positions = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Health = SystemAPI.GetComponentLookup<Health>(true),
                DayReturns = SystemAPI.GetComponentLookup<DayReturnState>(true),
                PatrolAreas = patrolAreas
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(TacticalActionFlag), typeof(EvaluateFlag))]
        partial struct ActionJob : IJobEntity
        {
            public Session Session;
            public GameClock Clock;
            public NightSettings NightTiming;
            public NightPlanState NightPlan;
            public SimulationControl Control;
            public HeroSelection HeroSelection;
            [ReadOnly]
            public ComponentLookup<LocalTransform> Positions;
            [ReadOnly]
            public ComponentLookup<Health> Health;
            [ReadOnly]
            public ComponentLookup<DayReturnState> DayReturns;
            [ReadOnly]
            public NativeParallelHashMap<TacticalPatrolKey, TacticalPatrolPoint> PatrolAreas;
            bool Alive(Entity e) => Health.HasComponent(e) && Health[e].Current > 0 && Positions.HasComponent(e);
            void Execute(Entity entity, DynamicBuffer<BranchComponent> branches, DynamicBuffer<TaskComponent> tasks, DynamicBuffer<TacticalActionData> actions, ref Combatant actor, ref Steering steering, ref UnitOrder order, in Perception perception, in LocalTransform transform, in Identity identity, in Health health, in TacticalState tactical)
            {
                for (var i = 0; i < actions.Length; i++)
                {
                    var data = actions[i];
                    var task = tasks[data.Index];
                    if (!branches[task.BranchIndex].CanExecute || (task.Status != TaskStatus.Queued && task.Status != TaskStatus.Running))
                        continue;
                    if (Control.Paused != 0)
                    {
                        steering.Moving = 0;
                        continue;
                    }

                    var dayReturn = Session.Phase == Phase.Day && DayReturns.HasComponent(entity);
                    var victoryPatrol = Session.Phase == Phase.Celebration && Clock.PhaseTime - NightPlan.CombatElapsed >= NightTiming.BattleAdvanceAt;
                    var active = (dayReturn || Session.Phase == Phase.Deployment || Session.Phase == Phase.Night || victoryPatrol || Session.Phase == Phase.Retreat && actor.Faction == 1 && Clock.PhaseTime >= NightTiming.NightPreparationSeconds + NightTiming.NightSeconds + NightTiming.RetreatDelaySeconds) && actor.Deployed != 0 && health.Current > 0 && Clock.Time >= actor.ProtectedUntil;
                    var valid = false;
                    var destination = transform.Position;
                    var target = Entity.Null;
                    switch (data.Mode)
                    {
                        case TacticalMode.Dormant:
                            valid = !active || (actor.Profile.Traits & TacticalTraits.NearestSoldier) != 0
                                && Session.Phase != Phase.Retreat && !Alive(perception.Enemy);
                            break;
                        case TacticalMode.Retreat:
                            valid = active && actor.Faction == 1 && (Session.Phase == Phase.Retreat
                                || actor.HomeId == 0 && (actor.Profile.Traits & TacticalTraits.NearestSoldier) == 0);
                            destination = actor.Home;
                            break;
                        case TacticalMode.PlayerOrder:
                            valid = active && actor.Faction == 0 && order.Kind != OrderKind.Automatic;
                            destination = order.Destination;
                            if (order.Kind == OrderKind.Focus)
                            {
                                valid &= Alive(order.Target);
                                target = valid ? order.Target : Entity.Null;
                                if (valid)
                                    destination = Positions[target].Position;
                            }
                            else if (order.Kind != OrderKind.Recall && order.Kind != OrderKind.Capture && perception.EnemyInRange != 0)
                            {
                                target = perception.Enemy;
                                // A selected hero keeps moving/holding the ordered position while firing automatically.
                                if (order.Kind == OrderKind.Rally)
                                    destination = transform.Position;
                            }

                            if (valid && order.Kind != OrderKind.Recall && order.Kind != OrderKind.Rally && order.Kind != OrderKind.Capture && target == Entity.Null && math.distance(transform.Position, destination) < .8f && entity != HeroSelection.SelectedHero)
                                order = default;
                            break;
                        case TacticalMode.EngageEnemy:
                            valid = active && !dayReturn && (tactical.Returning != 0 || Alive(perception.Enemy));
                            target = tactical.Returning != 0 || tactical.HasSlot == 0 ? Entity.Null : perception.Enemy;
                            destination = tactical.Returning != 0 ? tactical.Origin : tactical.HasSlot != 0 ? tactical.Engagement : transform.Position;
                            break;
                        case TacticalMode.AttackBuilding:
                            valid = active && actor.Faction == 1 && (actor.Profile.Traits & TacticalTraits.NearestSoldier) == 0 && Alive(perception.Building);
                            target = tactical.HasSlot != 0 ? perception.Building : Entity.Null;
                            destination = tactical.HasSlot != 0 ? tactical.Engagement : transform.Position;
                            break;
                        case TacticalMode.Patrol:
                            valid = active && !dayReturn && actor.Faction == 0;
                            if (PatrolAreas.TryGetValue(new TacticalPatrolKey(actor.HomeId, 0), out var patrolArea))
                            {
                                var patrolStepSeconds = math.max(6, patrolArea.Budget / math.max(.1f, actor.Speed) + 2);
                                var step = (ulong)math.max(0, math.floor(Clock.Time / patrolStepSeconds));
                                var sector = (int)((identity.Id + step) % TacticalPatrolAreas.SectorCount);
                                if (PatrolAreas.TryGetValue(new TacticalPatrolKey(actor.HomeId, sector), out var patrolPoint))
                                    destination = patrolPoint.Position;
                            }
                            break;
                    }

                    if (!valid)
                    {
                        task.Status = TaskStatus.Failure;
                        tasks[data.Index] = task;
                        continue;
                    }

                    actor.Target = target;
                    var distance = math.distance(transform.Position.xz, destination.xz);
                    var positionalOrder = data.Mode == TacticalMode.AttackBuilding || data.Mode == TacticalMode.EngageEnemy || data.Mode == TacticalMode.PlayerOrder && order.Kind != OrderKind.Focus;
                    steering.Destination = destination;
                    steering.Moving = (byte)(active && distance > (target == Entity.Null || positionalOrder ? .45f : math.max(.3f, actor.Range * .8f)) ? 1 : 0);
                    // Yield for a simulation tick. The repeater cannot spin through successful actions in one frame.
                    if (task.Status == TaskStatus.Queued)
                    {
                        data.LastExecution = Clock.Time;
                        task.Status = TaskStatus.Running;
                    }
                    else if (Clock.Time > data.LastExecution || !active)
                        task.Status = TaskStatus.Success;
                    actions[i] = data;
                    tasks[data.Index] = task;
                }
            }
        }
    }
}
