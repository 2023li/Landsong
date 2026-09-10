using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class NavigationOps
    {
        public static float3 NearestOpen(EntityManager em, Entity root, float3 position, int radius)
            => TryNearestOpen(em, root, position, radius, out var point) ? point : position;
        public static bool TryNearestOpen(EntityManager em, Entity root, float3 position, int radius, out float3 point)
        {
            point = default;
            var grid = em.GetComponentData<GridData>(root); var slots = em.GetBuffer<Occupancy>(root);
            var origin = GridOps.Cell(grid, position);
            for (var r = 0; r <= radius; r++) for (var y = -r; y <= r; y++) for (var x = -r; x <= r; x++)
            {
                if (math.max(math.abs(x), math.abs(y)) != r) continue;
                var c = origin + new int2(x, y); if (GridOps.Traversable(grid, slots, c)) { point = GridOps.Position(grid, c, new int2(1)) + new float3(0, .5f, 0); return true; }
            }
            return false;
        }
    }
    public struct SeparationSample { public Entity Entity; public ulong Id; public float3 Position; public float Radius; }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial struct NavigationSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<SimulationReady>(); }
        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<Session>(); var session = SystemAPI.GetComponent<Session>(root);
            if (session.Paused != 0 || (session.Phase != Phase.Night && session.Phase != Phase.Retreat)) return;
            var grid = SystemAPI.GetComponent<GridData>(root);
            // A single read-only snapshot can be shared by every path job. No GameObject/Physics queries.
            var occupancy = SystemAPI.GetBuffer<Occupancy>(root).ToNativeArray(Allocator.TempJob);
            using var actors = Sim.Entities<Combatant>(state.EntityManager);
            var neighbors = new NativeParallelMultiHashMap<int2, SeparationSample>(math.max(1, actors.Length), Allocator.TempJob);
            foreach (var e in actors) { var a = state.EntityManager.GetComponentData<Combatant>(e); if (a.Deployed == 0 || !Sim.Alive(state.EntityManager, e) || session.Time < a.ProtectedUntil) continue; var p = Sim.Position(state.EntityManager, e); neighbors.Add((int2)math.floor(p.xz / 4), new SeparationSample { Entity = e, Id = state.EntityManager.GetComponentData<Identity>(e).Id, Position = p, Radius = a.Profile.BodyRadius }); }
            var job = new MoveJob { Grid = grid, Occupancy = occupancy, Neighbors = neighbors, Now = session.Time, Delta = NightOps.Delta(session, SystemAPI.Time.DeltaTime) };
            state.Dependency = job.ScheduleParallel(state.Dependency);
            state.Dependency = occupancy.Dispose(state.Dependency);
            state.Dependency = neighbors.Dispose(state.Dependency);
        }
        struct HeapEntry { public int Index; public float Score; }
        [BurstCompile] partial struct MoveJob : IJobEntity
        {
            [ReadOnly] public GridData Grid;
            [ReadOnly] public NativeArray<Occupancy> Occupancy;
            [ReadOnly] public NativeParallelMultiHashMap<int2, SeparationSample> Neighbors;
            public float Now, Delta;
            bool Open(int2 cell)
            {
                var at = GridOps.Index(Grid, cell);
                return at >= 0 && Grid.Value.Value.Cells[at].Traversable != 0 && Grid.Value.Value.Cells[at].Exists != 0 && (Occupancy[at].Owner == 0 || Occupancy[at].MovementCost > 0);
            }
            void Execute(Entity entity, ref LocalTransform transform, ref NavigationState nav, ref Steering steering, in Combatant actor, in Identity identity, in Health health, DynamicBuffer<Waypoint> path)
            {
                if (actor.Deployed == 0 || health.Current <= 0 || Now < actor.ProtectedUntil) { path.Clear(); return; }
                Separate(entity, identity.Id, ref transform, actor);
                if (steering.Moving == 0) { path.Clear(); return; }
                var goal = GridOps.Cell(Grid, steering.Destination);
                if (Now >= nav.NextRepath && (math.any(goal != nav.Goal) || nav.Revision != Grid.Revision || path.Length == 0))
                {
                    var from = GridOps.Cell(Grid, transform.Position);
                    nav.Failed = (byte)(FindPath(from, goal, path) ? 0 : 1); nav.Goal = goal; nav.Revision = Grid.Revision; nav.NextRepath = Now + .5f;
                }
                if (path.Length == 0) { steering.Direction = default; return; }
                var target = path[0].Position; var offset = target - transform.Position;
                var distance = math.length(offset); var step = actor.Speed * Delta;
                if (distance <= step) { transform.Position = target; path.RemoveAt(0); }
                else
                {
                    var direction = offset / distance; transform.Position += direction * step;
                    var flat = new float3(direction.x, 0, direction.z);
                    if (math.lengthsq(flat) > .001f) transform.Rotation = quaternion.LookRotationSafe(flat, math.up());
                    steering.Direction = direction;
                }
            }
            void Separate(Entity entity, ulong id, ref LocalTransform transform, Combatant actor)
            {
                if ((actor.Profile.Traits & TacticalTraits.IgnoreSeparation) != 0) return;
                var cell = (int2)math.floor(transform.Position.xz / 4); float2 push = default;
                for (int y = -2; y <= 2; y++) for (int x = -2; x <= 2; x++)
                {
                    if (!Neighbors.TryGetFirstValue(cell + new int2(x, y), out var n, out var iterator)) continue;
                    do
                    {
                        if (n.Entity == entity) continue; float2 offset = transform.Position.xz - n.Position.xz; float distance = math.length(offset), limit = actor.Profile.BodyRadius + n.Radius;
                        if (distance >= limit) continue;
                        if (distance < .001f) { float angle = math.hash(new uint2((uint)math.min(id, n.Id), (uint)math.max(id, n.Id))) * .00001f; offset = new float2(math.cos(angle), math.sin(angle)) * (id < n.Id ? 1 : -1); }
                        push += math.normalizesafe(offset) * (limit - distance);
                    } while (Neighbors.TryGetNextValue(out n, ref iterator));
                }
                var shift = math.normalizesafe(push) * math.min(math.length(push), math.min(Grid.CellSize * .2f, actor.Speed * Delta));
                var candidate = transform.Position + new float3(shift.x, 0, shift.y); var from = GridOps.Cell(Grid, transform.Position); var to = GridOps.Cell(Grid, candidate);
                if (!Open(to) || !Open(new int2(from.x, to.y)) || !Open(new int2(to.x, from.y))) return;
                int at = GridOps.Index(Grid, to), before = GridOps.Index(Grid, from);
                if (before < 0 || math.abs(Grid.Value.Value.Cells[at].Height - Grid.Value.Value.Cells[before].Height) > Grid.CellSize) return;
                candidate.y = Grid.Origin.y + Grid.Value.Value.Cells[at].Height + .5f; transform.Position = candidate;
            }
            bool FindPath(int2 start, int2 target, DynamicBuffer<Waypoint> result)
            {
                result.Clear(); var first = GridOps.Index(Grid, start); if (first < 0) return false;
                // Buildings occupy their whole footprint: stop at an accessible perimeter cell.
                var resolved = target;
                if (!Open(resolved))
                {
                    var best = float.MaxValue;
                    for (var r = 1; r <= 10 && best == float.MaxValue; r++) for (var y = -r; y <= r; y++) for (var x = -r; x <= r; x++)
                    {
                        var cell = target + new int2(x, y); if (!Open(cell)) continue;
                        var score = math.lengthsq((float2)(cell - start)); if (score < best) { best = score; resolved = cell; }
                    }
                    if (best == float.MaxValue) return false;
                }
                var last = GridOps.Index(Grid, resolved); if (last == first) return true;
                var costs = new NativeArray<float>(Occupancy.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                var previous = new NativeArray<int>(Occupancy.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                var heap = new NativeList<HeapEntry>(128, Allocator.Temp);
                for (var i = 0; i < costs.Length; i++) { costs[i] = float.MaxValue; previous[i] = -1; }
                costs[first] = 0; Push(ref heap, new HeapEntry { Index = first }); var found = false;
                for (var visited = 0; heap.Length > 0 && visited < Occupancy.Length; visited++)
                {
                    var current = Pop(ref heap).Index;
                    if (current == last) { found = true; break; }
                    var cell = new int2(current % Grid.Value.Value.Size.x, current / Grid.Value.Value.Size.x) + Grid.Value.Value.Min;
                    for (var direction = 0; direction < 4; direction++)
                    {
                        var delta = direction == 0 ? new int2(1, 0) : direction == 1 ? new int2(-1, 0) : direction == 2 ? new int2(0, 1) : new int2(0, -1);
                        var next = cell + delta; if (!Open(next)) continue;
                        var at = GridOps.Index(Grid, next);
                        if (math.abs(Grid.Value.Value.Cells[at].Height - Grid.Value.Value.Cells[current].Height) > Grid.CellSize) continue;
                        var cost = costs[current] + math.max(1, Occupancy[at].MovementCost);
                        if (cost >= costs[at]) continue;
                        costs[at] = cost; previous[at] = current;
                        Push(ref heap, new HeapEntry { Index = at, Score = cost + math.csum(math.abs(next - resolved)) });
                    }
                }
                if (found)
                {
                    var at = last;
                    for (var guard = 0; at != first && at >= 0 && guard < previous.Length; guard++)
                    {
                        var cell = new int2(at % Grid.Value.Value.Size.x, at / Grid.Value.Value.Size.x) + Grid.Value.Value.Min;
                        result.Add(new Waypoint { Position = GridOps.Position(Grid, cell, new int2(1)) + new float3(0, .5f, 0) }); at = previous[at];
                    }
                    for (var a = 0; a < result.Length / 2; a++) { var temp = result[a]; result[a] = result[result.Length - 1 - a]; result[result.Length - 1 - a] = temp; }
                }
                costs.Dispose(); previous.Dispose(); heap.Dispose(); return found;
            }
            static void Push(ref NativeList<HeapEntry> heap, HeapEntry value)
            {
                heap.Add(value); var i = heap.Length - 1;
                while (i > 0) { var parent = (i - 1) / 2; if (heap[parent].Score <= value.Score) break; heap[i] = heap[parent]; i = parent; }
                heap[i] = value;
            }
            static HeapEntry Pop(ref NativeList<HeapEntry> heap)
            {
                var first = heap[0]; var value = heap[heap.Length - 1]; heap.RemoveAt(heap.Length - 1); if (heap.Length == 0) return first;
                var i = 0;
                while (i * 2 + 1 < heap.Length) { var child = i * 2 + 1; if (child + 1 < heap.Length && heap[child + 1].Score < heap[child].Score) child++; if (heap[child].Score >= value.Score) break; heap[i] = heap[child]; i = child; }
                heap[i] = value; return first;
            }
        }
    }
}
