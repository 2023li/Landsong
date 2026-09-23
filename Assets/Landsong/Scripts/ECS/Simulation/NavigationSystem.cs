using Pathfinding.ECS;
using Pathfinding.ECS.RVO;
using Pathfinding.RVO;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class NavigationOps
    {
        public static float3 NearestOpen(EntityManager em, Entity root, float3 position, int radius) => TryNearestOpen(em, root, position, radius, out var point) ? point : position;
        public static bool TryNearestOpen(EntityManager em, Entity root, float3 position, int radius, out float3 point)
            => TryNearestOpen(em, root, position, radius, false, 0, 0, out point);

        public static bool TryNearestOpenOnSurface(EntityManager em, Entity root, float3 position, int radius, int surface, int elevation, out float3 point)
            => TryNearestOpen(em, root, position, radius, true, surface, elevation, out point);

        static bool TryNearestOpen(EntityManager em, Entity root, float3 position, int radius, bool restrictSurface, int surface, int elevation, out float3 point)
        {
            point = default;
            SurfaceNavigationGraph.Ensure(em, root);
            var grid = em.GetComponentData<GridData>(root);
            var nodes = em.GetBuffer<SurfaceNavNode>(root);
            var origin = GridOps.Cell(grid, position);
            int bestRing = radius + 1;
            float bestHeight = float.MaxValue;
            foreach (var node in nodes)
            {
                int ring = math.cmax(math.abs(node.Cell - origin));
                float difference = math.abs(node.Position.y - position.y);
                if (node.Open == 0 || restrictSurface && (node.Surface != surface || node.Elevation != elevation) || ring > radius || !restrictSurface && difference > .55f || ring > bestRing || ring == bestRing && difference >= bestHeight)
                    continue;
                bestRing = ring;
                bestHeight = difference;
                point = node.Position;
            }

            return bestRing <= radius;
        }

        public static bool RaycastSurface(EntityManager em, Entity root, float3 origin, float3 direction, out float3 hit)
        {
            var grid = em.GetComponentData<GridData>(root);
            bool found = GridOps.RaycastSurface(grid, origin, direction, out hit);
            float distance = found ? math.dot(hit - origin, direction) / math.lengthsq(direction) : float.MaxValue;
            SurfaceNavigationGraph.Ensure(em, root);
            var nodes = em.GetBuffer<SurfaceNavNode>(root);
            for (int i = grid.Value.Value.Cells.Length; i < nodes.Length; i++)
            {
                var n = nodes[i];
                if (n.Open == 0)
                    continue;
                float denominator = direction.y - math.dot(n.Gradient, direction.xz);
                if (math.abs(denominator) < .000001f)
                    continue;
                float at = (n.Position.y - .5f + math.dot(n.Gradient, origin.xz - n.Position.xz) - origin.y) / denominator;
                if (at < 0 || at >= distance)
                    continue;
                var p = origin + direction * at;
                if (math.any(GridOps.Cell(grid, p) != n.Cell))
                    continue;
                distance = at;
                hit = p;
                found = true;
            }

            return found;
        }
    }

    /// <summary>Marks Landsong units whose path search and local avoidance are owned by A* Pro.</summary>
    public struct AstarNavigationAgent : IComponentData { }

    [UpdateInGroup(typeof(CombatResolutionGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial struct NavigationSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var root = SystemAPI.GetSingletonEntity<Session>();
            var session = SystemAPI.GetComponent<Session>(root);
            var clock = SystemAPI.GetComponent<GameClock>(root);
            var control = SystemAPI.GetComponent<SimulationControl>(root);
            var night = SystemAPI.GetComponent<NightRuntimeState>(root);
            var timing = SystemAPI.GetComponent<NightSettings>(root);
            var plan = SystemAPI.GetComponent<NightPlanState>(root);
            if (control.Paused != 0 || !RunsNavigation(session.Phase))
                return;
            if (session.Phase == Phase.Day)
            {
                using var returning = em.CreateEntityQuery(ComponentType.ReadOnly<DayReturnState>());
                using var workers = em.CreateEntityQuery(ComponentType.ReadOnly<TransportWorker>());
                using var firefighters = em.CreateEntityQuery(ComponentType.ReadOnly<Firefighter>());
                if (returning.IsEmptyIgnoreFilter && workers.IsEmptyIgnoreFilter && firefighters.IsEmptyIgnoreFilter)
                    return;
            }

            state.Dependency.Complete();
            SurfaceNavigationGraph.Ensure(em, root);
            var grid = em.GetComponentData<GridData>(root);
            var nodes = em.GetBuffer<SurfaceNavNode>(root).ToNativeArray(Allocator.Temp);
            var edges = em.GetBuffer<SurfaceNavEdge>(root).ToNativeArray(Allocator.Temp);
            var cells = new NativeParallelMultiHashMap<int2, int>(math.max(1, nodes.Length), Allocator.Temp);
            for (int i = 0; i < nodes.Length; i++)
                cells.Add(nodes[i].Cell, i);
            var query = new SurfacePathQuery { Grid = grid, Nodes = nodes, Edges = edges, Cells = cells };
            bool astar = AstarNavigationRuntime.EnsureGraph(em, root);
            bool daytime = session.Phase == Phase.Day;
            bool workersOnly = session.Phase == Phase.Celebration && clock.PhaseTime - plan.CombatElapsed < timing.BattleAdvanceAt;
            float delta = daytime ? SystemAPI.Time.DeltaTime : NightOps.Delta(night, SystemAPI.Time.DeltaTime);

            using var actors = WorldQueries.OrderedEntities<Combatant>(em);
            foreach (var entity in actors)
            {
                EnsureAstarComponents(em, entity);
                var actor = em.GetComponentData<Combatant>(entity);
                var health = em.GetComponentData<Health>(entity);
                bool active = actor.Deployed != 0 && health.Current > 0 && (daytime || clock.Time >= actor.ProtectedUntil)
                    && (!daytime || em.HasComponent<DayReturnState>(entity) || em.HasComponent<TransportWorker>(entity) || em.HasComponent<Firefighter>(entity))
                    && (!workersOnly || em.HasComponent<TransportWorker>(entity) || em.HasComponent<Firefighter>(entity));
                // Isolated edit-mode fixtures have no AstarPath/RVOSimulator service.
                // Leave them to A* Pro's fallback resolver instead of marking them as RVO-owned.
                SetRvoParticipation(em, entity, actor, active && astar);

                var path = em.GetBuffer<Waypoint>(entity);
                var steering = em.GetComponentData<Steering>(entity);
                var navigation = em.GetComponentData<NavigationState>(entity);
                if (daytime)
                    navigation.NextRepath = math.max(0, math.min(.5f, navigation.NextRepath) - delta);
                if (!active || steering.Moving == 0)
                {
                    path.Clear();
                    steering.Direction = default;
                    em.SetComponentData(entity, steering);
                    em.SetComponentData(entity, navigation);
                    em.SetComponentData(entity, Stopped(EntityState.Position(em, entity)));
                    continue;
                }

                var position = EntityState.Position(em, entity);
                var goal = GridOps.Cell(grid, steering.Destination);
                bool needsPath = (daytime ? 0 : clock.Time) >= navigation.NextRepath
                    && (math.any(goal != navigation.Goal) || math.abs(steering.Destination.y - navigation.GoalHeight) > .05f
                        || navigation.Revision != grid.Revision || path.Length == 0);
                if (needsPath)
                {
                    query.Radius = actor.Profile.BodyRadius;
                    bool found = astar
                        ? AstarNavigationRuntime.FindPath(position, steering.Destination, path)
                        : query.Find(position, steering.Destination, path); // Isolated edit-mode fixture fallback.
                    navigation.Failed = (byte)(found ? 0 : 1);
                    navigation.Goal = goal;
                    navigation.GoalHeight = steering.Destination.y;
                    navigation.Revision = grid.Revision;
                    navigation.NextRepath = (daytime ? 0 : clock.Time) + .5f;
                }

                float arrival = math.max(.15f, actor.Profile.BodyRadius * 1.75f);
                while (path.Length > 1 && math.distance(position, path[0].Position) <= arrival)
                    path.RemoveAt(0);
                if (path.Length == 0)
                {
                    steering.Direction = default;
                    em.SetComponentData(entity, steering);
                    em.SetComponentData(entity, navigation);
                    em.SetComponentData(entity, Stopped(position));
                    continue;
                }

                int node = query.Locate(position);
                float speed = actor.Speed / (node < 0 ? 1 : query.Nodes[node].Cost);
                var target = path[0].Position;
                var flat = math.normalizesafe((target - position).xz);
                steering.Direction = new float3(flat.x, 0, flat.y);
                em.SetComponentData(entity, steering);
                em.SetComponentData(entity, navigation);
                em.SetComponentData(entity, new MovementControl
                {
                    targetPoint = target,
                    endOfPath = path[path.Length - 1].Position,
                    speed = speed,
                    maxSpeed = speed * 1.1f,
                    hierarchicalNodeIndex = -1,
                    overrideLocalAvoidance = (actor.Profile.Traits & TacticalTraits.IgnoreSeparation) != 0
                });
            }

            cells.Dispose();
            edges.Dispose();
            nodes.Dispose();
        }

        static bool RunsNavigation(Phase phase) => phase == Phase.Day || phase == Phase.Deployment || phase == Phase.Night || phase == Phase.Retreat || phase == Phase.Celebration;

        static MovementControl Stopped(float3 position) => new MovementControl
        {
            targetPoint = position,
            endOfPath = position,
            hierarchicalNodeIndex = -1
        };

        static void EnsureAstarComponents(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<AstarNavigationAgent>(entity)) em.AddComponent<AstarNavigationAgent>(entity);
            if (!em.HasComponent<AgentCylinderShape>(entity)) em.AddComponentData(entity, new AgentCylinderShape());
            if (!em.HasComponent<AgentMovementPlane>(entity)) em.AddComponentData(entity, new AgentMovementPlane(quaternion.identity));
            if (!em.HasComponent<MovementControl>(entity)) em.AddComponentData(entity, new MovementControl { hierarchicalNodeIndex = -1 });
            if (!em.HasComponent<ResolvedMovement>(entity)) em.AddComponentData(entity, new ResolvedMovement { turningRadiusMultiplier = 1 });
            if (!em.HasComponent<SimulateMovement>(entity)) em.AddComponent<SimulateMovement>(entity);
        }

        static void SetRvoParticipation(EntityManager em, Entity entity, Combatant actor, bool active)
        {
            var shape = em.GetComponentData<AgentCylinderShape>(entity);
            shape.radius = math.max(.05f, actor.Profile.BodyRadius);
            shape.height = math.max(1, actor.Profile.BodyRadius * 2);
            em.SetComponentData(entity, shape);
            if (!active)
            {
                if (em.HasComponent<RVOAgent>(entity))
                    em.RemoveComponent<RVOAgent>(entity);
                return;
            }

            if (!em.HasComponent<RVOAgent>(entity))
            {
                var rvo = RVOAgent.Default;
                rvo.agentTimeHorizon = 1.5f;
                rvo.obstacleTimeHorizon = .5f;
                rvo.maxNeighbours = 16;
                rvo.layer = RVOLayer.DefaultAgent;
                rvo.collidesWith = (RVOLayer)(-1);
                rvo.priority = .5f;
                rvo.priorityMultiplier = 1;
                em.AddComponentData(entity, rvo);
            }
        }
    }

    /// <summary>Applies A* Pro's resolved RVO velocity while retaining authored surface constraints.</summary>
    [UpdateInGroup(typeof(AIMovementSystemGroup))]
    [UpdateAfter(typeof(RVOSystem))]
    [UpdateAfter(typeof(FallbackResolveMovementSystem))]
    public partial struct AstarNavigationMoveSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();

        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<Session>();
            var session = SystemAPI.GetComponent<Session>(root);
            var control = SystemAPI.GetComponent<SimulationControl>(root);
            if (control.Paused != 0 || !RunsNavigation(session.Phase))
                return;
            var clock = SystemAPI.GetComponent<GameClock>(root);
            var night = SystemAPI.GetComponent<NightRuntimeState>(root);
            var timing = SystemAPI.GetComponent<NightSettings>(root);
            var plan = SystemAPI.GetComponent<NightPlanState>(root);
            var grid = SystemAPI.GetComponent<GridData>(root);
            bool daytime = session.Phase == Phase.Day;

            state.Dependency.Complete();
            SurfaceNavigationGraph.Ensure(state.EntityManager, root);
            var nodes = state.EntityManager.GetBuffer<SurfaceNavNode>(root).ToNativeArray(Allocator.TempJob);
            var edges = state.EntityManager.GetBuffer<SurfaceNavEdge>(root).ToNativeArray(Allocator.TempJob);
            var cells = new NativeParallelMultiHashMap<int2, int>(math.max(1, nodes.Length), Allocator.TempJob);
            for (int i = 0; i < nodes.Length; i++)
                cells.Add(nodes[i].Cell, i);

            state.Dependency = new MoveJob
            {
                Query = new SurfacePathQuery { Grid = grid, Nodes = nodes, Edges = edges, Cells = cells },
                Delta = daytime ? SystemAPI.Time.DeltaTime : NightOps.Delta(night, SystemAPI.Time.DeltaTime),
                Now = daytime ? 0 : clock.Time,
                Daytime = daytime,
                WorkersOnly = session.Phase == Phase.Celebration && clock.PhaseTime - plan.CombatElapsed < timing.BattleAdvanceAt,
                Workers = SystemAPI.GetComponentLookup<TransportWorker>(true),
                Firefighters = SystemAPI.GetComponentLookup<Firefighter>(true),
                DayReturns = SystemAPI.GetComponentLookup<DayReturnState>(true)
            }.ScheduleParallel(state.Dependency);
            state.Dependency = nodes.Dispose(state.Dependency);
            state.Dependency = edges.Dispose(state.Dependency);
            state.Dependency = cells.Dispose(state.Dependency);
        }

        static bool RunsNavigation(Phase phase) => phase == Phase.Day || phase == Phase.Deployment || phase == Phase.Night || phase == Phase.Retreat || phase == Phase.Celebration;

        [BurstCompile]
        partial struct MoveJob : IJobEntity
        {
            public SurfacePathQuery Query;
            public float Delta, Now;
            public bool Daytime, WorkersOnly;
            [ReadOnly] public ComponentLookup<TransportWorker> Workers;
            [ReadOnly] public ComponentLookup<Firefighter> Firefighters;
            [ReadOnly] public ComponentLookup<DayReturnState> DayReturns;

            void Execute(Entity entity, ref LocalTransform transform, ref Steering steering, in Combatant actor, in Health health,
                in ResolvedMovement movement, in AstarNavigationAgent astarAgent, DynamicBuffer<Waypoint> path)
            {
                if (actor.Deployed == 0 || health.Current <= 0 || !Daytime && Now < actor.ProtectedUntil
                    || Daytime && !DayReturns.HasComponent(entity) && !Workers.HasComponent(entity) && !Firefighters.HasComponent(entity)
                    || WorkersOnly && !Workers.HasComponent(entity) && !Firefighters.HasComponent(entity) || steering.Moving == 0)
                {
                    steering.Direction = default;
                    return;
                }

                Query.Radius = actor.Profile.BodyRadius;
                float3 before = transform.Position;
                float3 offset = movement.targetPoint - before;
                float distance = math.length(offset);
                float step = math.max(0, movement.speed) * Delta;
                if (distance > .0001f && step > .000001f)
                {
                    float3 candidate = step >= distance ? movement.targetPoint : before + offset / distance * step;
                    if (Query.Shift(before, candidate, out var safe))
                    {
                        // At a stair endpoint the current planar node is flat, while the next
                        // authored corridor node owns the slope. Interpolate along that explicit
                        // edge until Locate enters the corridor cell to avoid a half-cell height snap.
                        int currentNode = Query.Locate(before);
                        int targetNode = path.Length > 0 ? Query.Locate(path[0].Position) : -1;
                        if (currentNode >= 0 && targetNode >= 0 && currentNode != targetNode
                            && (Query.Nodes[currentNode].Corridor == 0) != (Query.Nodes[targetNode].Corridor == 0)
                            && Query.Connected(currentNode, targetNode))
                        {
                            float2 segment = Query.Nodes[targetNode].Position.xz - Query.Nodes[currentNode].Position.xz;
                            float along = math.saturate(math.dot(safe.xz - Query.Nodes[currentNode].Position.xz, segment) / math.max(.000001f, math.lengthsq(segment)));
                            safe.y = math.lerp(Query.Nodes[currentNode].Position.y, Query.Nodes[targetNode].Position.y, along);
                        }
                        else if (currentNode >= 0 && currentNode == targetNode && Query.Nodes[targetNode].Corridor == 0)
                        {
                            // Locate switches to the flat endpoint at the cell boundary, before
                            // reaching its centre. Keep projecting from the nearest connected stair
                            // node until the endpoint centre is reached.
                            int corridor = -1;
                            float nearest = float.MaxValue;
                            for (int edge = Query.Nodes[targetNode].FirstEdge; edge >= 0; edge = Query.Edges[edge].Next)
                            {
                                int candidateNode = Query.Edges[edge].Target;
                                if (Query.Nodes[candidateNode].Corridor == 0)
                                    continue;
                                float distanceToCorridor = math.distancesq(before.xz, Query.Nodes[candidateNode].Position.xz);
                                if (distanceToCorridor < nearest)
                                {
                                    nearest = distanceToCorridor;
                                    corridor = candidateNode;
                                }
                            }

                            if (corridor >= 0)
                            {
                                float2 segment = Query.Nodes[targetNode].Position.xz - Query.Nodes[corridor].Position.xz;
                                float along = math.saturate(math.dot(safe.xz - Query.Nodes[corridor].Position.xz, segment) / math.max(.000001f, math.lengthsq(segment)));
                                safe.y = math.lerp(Query.Nodes[corridor].Position.y, Query.Nodes[targetNode].Position.y, along);
                            }
                        }

                        transform.Position = safe;
                    }
                }

                float3 actual = transform.Position - before;
                var flat = new float3(actual.x, 0, actual.z);
                if (math.lengthsq(flat) > .000001f)
                {
                    steering.Direction = math.normalizesafe(flat);
                    transform.Rotation = quaternion.LookRotationSafe(flat, math.up());
                }
                else
                    steering.Direction = default;

                float arrival = math.max(.08f, actor.Profile.BodyRadius * .5f);
                while (path.Length > 0 && math.distance(transform.Position, path[0].Position) <= arrival)
                    path.RemoveAt(0);
            }
        }
    }
}
