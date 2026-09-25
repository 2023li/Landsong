using Landsong.ECS.Definitions;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class NightSpatialOps
    {
        public static bool Inside(SpawnRegion r, float3 p, float margin = 0) => math.all(math.abs(p.xz - r.Center.xz) <= r.Size.xz * .5f + margin + .001f);
        public static bool SpawnPoint(EntityManager em, Entity root, int regionIndex, float3 preferred, out float3 point, NightSpawnOps.Space space = null)
        {
            point = default;
            var regions = em.GetBuffer<SpawnRegion>(root);
            if (regionIndex < 0 || regionIndex >= regions.Length)
                return false;
            space ??= new NightSpawnOps.Space(em, root);
            var region = regions[regionIndex];
            var grid = space.Grid;
            var lo = math.max(GridOps.Cell(grid, region.Center - region.Size * .5f), grid.Value.Value.Min);
            var hi = math.min(GridOps.Cell(grid, region.Center + region.Size * .5f), grid.Value.Value.Min + grid.Value.Value.Size - 1);
            float best = float.MaxValue;
            for (int y = lo.y; y <= hi.y; y++)
                for (int x = lo.x; x <= hi.x; x++)
                {
                    var cell = new int2(x, y);
                    if (!space.Legal(cell, region.EdgeOnly != 0))
                        continue;
                    var position = space.Position(cell);
                    if (!Inside(region, position))
                        continue;
                    float distance = math.distancesq(position.xz, preferred.xz);
                    if (distance >= best)
                        continue;
                    best = distance;
                    point = position;
                }

            return best < float.MaxValue;
        }

        public sealed class Reach : IDisposable
        {
            readonly GridData grid;
            readonly NativeArray<float> distances;
            readonly NativeArray<SurfaceNavNode> nodes;
            readonly float radius;
            readonly bool indexCells;
            readonly System.Collections.Generic.Dictionary<int2, System.Collections.Generic.List<int>> cells = new System.Collections.Generic.Dictionary<int2, System.Collections.Generic.List<int>>();
            public Reach(EntityManager em, Entity root, float3 from, float radius = 0, bool indexCells = true)
            {
                this.radius = math.max(0, radius);
                this.indexCells = indexCells;
                SurfaceNavigationGraph.Ensure(em, root);
                grid = em.GetComponentData<GridData>(root);
                nodes = em.GetBuffer<SurfaceNavNode>(root).ToNativeArray(Allocator.Temp);
                var edges = em.GetBuffer<SurfaceNavEdge>(root);
                distances = new NativeArray<float>(nodes.Length, Allocator.Temp);
                var result = distances;
                for (int i = 0; i < nodes.Length; i++)
                {
                    result[i] = float.MaxValue;
                    if (indexCells)
                    {
                        if (!cells.TryGetValue(nodes[i].Cell, out var indices))
                            cells.Add(nodes[i].Cell, indices = new System.Collections.Generic.List<int>());
                        indices.Add(i);
                    }
                }

                int first = Locate(from);
                if (first < 0)
                    return;
                using var queue = new NativeList<int>(Allocator.Temp);
                queue.Add(first);
                result[first] = 0;
                for (int cursor = 0; cursor < queue.Length; cursor++)
                {
                    int at = queue[cursor];
                    for (int edge = nodes[at].FirstEdge; edge >= 0; edge = edges[edge].Next)
                    {
                        int next = edges[edge].Target;
                        if (!Accessible(next))
                            continue;
                        float cost = result[at] + math.distance(nodes[at].Position, nodes[next].Position) * nodes[next].Cost;
                        if (cost >= result[next])
                            continue;
                        result[next] = cost;
                        queue.Add(next);
                    }
                }
            }

            bool Accessible(int index) => nodes[index].Open != 0 && nodes[index].SideClearance + .001f >= radius;

            int Locate(float3 point)
            {
                var cell = GridOps.Cell(grid, point);
                int best = -1;
                float error = .55f;
                if (!indexCells)
                {
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        if (!math.all(nodes[i].Cell == cell))
                            continue;
                        var height = nodes[i].Position.y + math.dot(nodes[i].Gradient, point.xz - nodes[i].Position.xz);
                        var distance = math.abs(height - point.y);
                        if (Accessible(i) && distance <= error)
                        {
                            best = i;
                            error = distance;
                        }
                    }

                    return best;
                }

                if (!cells.TryGetValue(cell, out var indices))
                    return -1;
                foreach (int i in indices)
                {
                    float height = nodes[i].Position.y + math.dot(nodes[i].Gradient, point.xz - nodes[i].Position.xz);
                    float distance = math.abs(height - point.y);
                    if (Accessible(i) && distance <= error)
                    {
                        best = i;
                        error = distance;
                    }
                }

                return best;
            }

            public float Building(BuildingPlacementState bPlacement, out float3 point)
            {
                float best = float.MaxValue;
                point = default;
                for (int y = -1; y <= bPlacement.Size.y; y++)
                    for (int x = -1; x <= bPlacement.Size.x; x++)
                    {
                        if (x >= 0 && y >= 0 && x < bPlacement.Size.x && y < bPlacement.Size.y)
                            continue;
                        if (!cells.TryGetValue(bPlacement.Cell + new int2(x, y), out var indices))
                            continue;
                        foreach (int at in indices)
                        {
                            if (nodes[at].Elevation != bPlacement.Elevation || nodes[at].Corridor != 0 || distances[at] >= best)
                                continue;
                            best = distances[at];
                            point = nodes[at].Position;
                        }
                    }

                return best;
            }

            public bool Point(float3 point) => Distance(point) < float.MaxValue;
            public float DistanceAt(int index) => index >= 0 && index < distances.Length ? distances[index] : float.MaxValue;
            public float Distance(float3 point)
            {
                int at = Locate(point);
                return at >= 0 ? distances[at] : float.MaxValue;
            }

            public void Dispose()
            {
                distances.Dispose();
                nodes.Dispose();
            }
        }

        public static bool ValidTarget(EntityManager em, Entity root, Entity e)
            => EntityState.Alive(em, e) && em.HasComponent<Building>(e)
                && BuildingFactionOps.Of(em, root, e) == (byte)BuildingFaction.Settlement
                && em.GetComponentData<Building>(e).Stage != LifeStage.Ruined
                && em.GetComponentData<Building>(e).Stage != LifeStage.Repairing;
        public static Entity Target(EntityManager em, Entity root, EnemyId definition, float3 position, float3 anchor, ulong preferred, bool replacing, ulong excluded = 0)
        {
            using var reachable = new Reach(em, root, position);
            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            ref var d = ref EnemyDefinitions.Get(em, root, definition);
            int mode = ((int)d.Behavior >> 1) & 3;
            float best = float.MaxValue;
            var chosen = Entity.Null;
            for (int pass = 0; pass < 2 && chosen == Entity.Null; pass++)
                foreach (var e in buildings)
                {
                    if (!ValidTarget(em, root, e))
                        continue;
                    var id = em.GetComponentData<Identity>(e);
                    if (id.Id == excluded)
                        continue;
                    BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(e);
                    var core = em.GetComponentData<BuildingHousingStats>(e).IsCore != 0;
                    if (replacing && pass == 0 && math.distance(EntityState.Position(em, e).xz, anchor.xz) > NightPlanOps.Rules(em, root).TargetRadius)
                        continue;
                    if (pass == 1 && !core)
                        continue;
                    if (!replacing && mode == 3 && d.PreferredTargetCategory != 0 && (BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition).PlacementAndVisuals.Category & d.PreferredTargetCategory) == 0)
                        continue;
                    float path = reachable.Building(bPlacement, out _);
                    if (path == float.MaxValue)
                        continue;
                    float score = path;
                    if (!replacing && preferred == id.Id)
                        score = -3;
                    else if (!replacing && mode == 0 && core)
                        score = -2;
                    else if (!replacing && mode == 2)
                        score = math.hash(new uint3((uint)id.Id, (uint)(id.Id >> 32), em.GetComponentData<NightRuntimeState>(root).Seed ^ math.hash(position))) / (float)uint.MaxValue * 100;
                    if (score < best)
                    {
                        best = score;
                        chosen = e;
                    }
                }

            return chosen;
        }

        public static void RefreshTargets(EntityManager em, Entity root)
        {
            using var all = WorldQueries.OrderedEntities<Combatant>(em);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            foreach (var e in all)
            {
                var a = em.GetComponentData<Combatant>(e);
                if (a.Faction != 1 || a.Deployed == 0 || !EntityState.Alive(em, e))
                    continue;
                var p = em.GetComponentData<Perception>(e);
                if ((a.Profile.Traits & TacticalTraits.NearestSoldier) != 0)
                {
                    p.Building = Entity.Null;
                    p.BuildingPosition = default;
                    em.SetComponentData(e, p);
                    continue;
                }
                var target = WorldQueries.Find(em, a.HomeId);
                var nav = em.GetComponentData<NavigationState>(e);
                var targetValid = ValidTarget(em, root, target);
                if (targetValid && nav.Failed == 0 && a.TargetRevision == em.GetComponentData<GridData>(root).Revision)
                    continue;
                if (!targetValid)
                {
                    p.Building = Entity.Null;
                    p.BuildingPosition = default;
                    em.SetComponentData(e, p);
                    if (a.Target == target)
                    {
                        a.Target = Entity.Null;
                        em.SetComponentData(e, a);
                    }
                }
                if (sClock.Time < a.DecisionAt)
                    continue;
                a.DecisionAt = sClock.Time + 1;
                var replacement = Target(em, root, em.GetComponentData<EnemyDefinitionRef>(e).Definition, EntityState.Position(em, e), a.TargetAnchor, a.HomeId, !targetValid || nav.Failed != 0, nav.Failed != 0 ? a.HomeId : 0);
                p.Building = replacement;
                a.HomeId = replacement == Entity.Null ? 0 : em.GetComponentData<Identity>(replacement).Id;
                a.TargetRevision = em.GetComponentData<GridData>(root).Revision;
                if (replacement != Entity.Null)
                {
                    using var reachable = new Reach(em, root, EntityState.Position(em, e));
                    reachable.Building(em.GetComponentData<BuildingPlacementState>(replacement), out p.BuildingPosition);
                }
                else
                {
                    a.Target = Entity.Null;
                    em.SetComponentData(e, new UnitOrder { Kind = OrderKind.Recall, Destination = a.Home });
                }

                nav.Failed = 0;
                nav.Revision = -1;
                em.SetComponentData(e, nav);
                em.SetComponentData(e, a);
                em.SetComponentData(e, p);
            }
        }
    }
}
