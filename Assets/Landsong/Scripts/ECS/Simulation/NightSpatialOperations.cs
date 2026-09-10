using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class NightSpatialOps
    {
        // Older TWC maps authored a marker just outside the mesh. Bake it to an explicit inner-edge entry strip.
        public static SpawnRegion ProjectRegion(float3 origin, int2 min, int2 size, float cell, SpawnRegion r)
        {
            if (cell <= 0 || !math.all(math.isfinite(r.Center)) || !math.all(math.isfinite(r.Size)) || r.Size.x <= 0 || r.Size.z <= 0 || r.Direction != 10 && r.Direction != 20 && r.Direction != 30 && r.Direction != 40) throw new InvalidOperationException("Invalid invasion region.");
            var low = origin.xz + (float2)min * cell; var high = low + (float2)size * cell;
            var extent = math.min(math.max(r.Size.xz, new float2(cell)), high - low); var p = math.clamp(r.Center.xz, low + extent * .5f, high - extent * .5f);
            if (r.Direction == 10) p.y = high.y - extent.y * .5f; if (r.Direction == 20) p.x = high.x - extent.x * .5f;
            if (r.Direction == 30) p.y = low.y + extent.y * .5f; if (r.Direction == 40) p.x = low.x + extent.x * .5f;
            r.Center = new float3(p.x, origin.y, p.y); r.Size = new float3(extent.x, cell, extent.y); return r;
        }
        public static bool Inside(SpawnRegion r, float3 p, float margin = 0) => math.all(math.abs(p.xz - r.Center.xz) <= r.Size.xz * .5f + margin + .001f);
        public static bool Reserved(EntityManager em, Entity root, int2 cell)
        {
            if (!em.HasBuffer<SpawnRegion>(root) || !em.HasComponent<ContentCatalog>(root)) return false;
            var grid = em.GetComponentData<GridData>(root); var p = GridOps.Position(grid, cell, new int2(1));
            foreach (var r in em.GetBuffer<SpawnRegion>(root)) if (Inside(r, p, NightPlanOps.Rules(em, root).BorderBuffer + grid.CellSize * .5f)) return true; return false;
        }
        public static bool SpawnPoint(EntityManager em, Entity root, int regionIndex, float3 preferred, out float3 point)
        {
            point = default; var regions = em.GetBuffer<SpawnRegion>(root); if (regionIndex < 0 || regionIndex >= regions.Length) return false;
            var region = regions[regionIndex]; var grid = em.GetComponentData<GridData>(root); var occupied = em.GetBuffer<Occupancy>(root);
            var lo = GridOps.Cell(grid, region.Center - region.Size * .5f); var hi = GridOps.Cell(grid, region.Center + region.Size * .5f); float best = float.MaxValue;
            using var sites = Sim.OrderedEntities<Building>(em); float safety = NightPlanOps.Rules(em, root).SpawnSafety;
            for (int y = lo.y; y <= hi.y; y++) for (int x = lo.x; x <= hi.x; x++)
            {
                var c = new int2(x, y); if (!GridOps.Traversable(grid, occupied, c)) continue; var p = GridOps.Position(grid, c, new int2(1)) + new float3(0, .5f, 0); if (!Inside(region, p)) continue;
                bool safe = true; foreach (var site in sites)
                {
                    var b = em.GetComponentData<Building>(site); if (b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing) continue;
                    var center = Sim.Position(em, site).xz; var half = (float2)b.Size * grid.CellSize * .5f;
                    if (math.distance(p.xz, math.clamp(p.xz, center - half, center + half)) < safety) { safe = false; break; }
                }
                if (!safe) continue; var distance = math.distancesq(p.xz, preferred.xz); if (distance < best) { best = distance; point = p; }
            }
            return best < float.MaxValue;
        }
        public sealed class Reach : IDisposable
        {
            readonly GridData grid; readonly NativeArray<float> distances;
            public Reach(EntityManager em, Entity root, float3 from)
            {
                grid = em.GetComponentData<GridData>(root); var occupied = em.GetBuffer<Occupancy>(root); distances = new NativeArray<float>(occupied.Length, Allocator.Temp); var result = distances;
                for (int i = 0; i < result.Length; i++) result[i] = float.MaxValue;
                int first = GridOps.Index(grid, GridOps.Cell(grid, from)); if (first < 0) return;
                using var queue = new NativeList<int>(Allocator.Temp); queue.Add(first); result[first] = 0;
                // Positive edge weights. Relaxation handles ruined footprint movement costs as well as elevation.
                for (int next = 0; next < queue.Length; next++)
                {
                    int at = queue[next]; var c = new int2(at % grid.Value.Value.Size.x, at / grid.Value.Value.Size.x) + grid.Value.Value.Min;
                    for (int d = 0; d < 4; d++)
                    {
                        var to = c + (d == 0 ? new int2(1, 0) : d == 1 ? new int2(-1, 0) : d == 2 ? new int2(0, 1) : new int2(0, -1)); if (!GridOps.Traversable(grid, occupied, to)) continue;
                        int index = GridOps.Index(grid, to); if (math.abs(grid.Value.Value.Cells[index].Height - grid.Value.Value.Cells[at].Height) > grid.CellSize) continue;
                        float cost = result[at] + math.max(1, occupied[index].MovementCost) * grid.CellSize;
                        if (cost >= result[index]) continue; result[index] = cost; queue.Add(index);
                    }
                }
            }
            public float Building(Building b, out float3 point)
            {
                float best = float.MaxValue; point = default;
                for (int y = -1; y <= b.Size.y; y++) for (int x = -1; x <= b.Size.x; x++)
                {
                    if (x >= 0 && y >= 0 && x < b.Size.x && y < b.Size.y) continue;
                    var cell = b.Cell + new int2(x, y); int at = GridOps.Index(grid, cell); if (at < 0 || distances[at] >= best) continue;
                    best = distances[at]; point = GridOps.Position(grid, cell, new int2(1)) + new float3(0, .5f, 0);
                }
                return best;
            }
            public bool Point(float3 point) { int at = GridOps.Index(grid, GridOps.Cell(grid, point)); return at >= 0 && distances[at] < float.MaxValue; }
            public float Distance(float3 point) { int at = GridOps.Index(grid, GridOps.Cell(grid, point)); return at >= 0 ? distances[at] : float.MaxValue; }
            public void Dispose() => distances.Dispose();
        }
        public static bool ValidTarget(EntityManager em, Entity e) => Sim.Alive(em, e) && em.HasComponent<Building>(e) && em.GetComponentData<Building>(e).Stage != LifeStage.Ruined && em.GetComponentData<Building>(e).Stage != LifeStage.Repairing;
        public static Entity Target(EntityManager em, Entity root, int definition, float3 position, float3 anchor, ulong preferred, bool replacing, ulong excluded = 0)
        {
            using var reachable = new Reach(em, root, position); using var buildings = Sim.OrderedEntities<Building>(em);
            var d = Sim.Definition(em, root, definition); int mode = (d.Flags >> 1) & 3; float best = float.MaxValue; var chosen = Entity.Null;
            for (int pass = 0; pass < 2 && chosen == Entity.Null; pass++) foreach (var e in buildings)
            {
                if (!ValidTarget(em, e)) continue; var id = em.GetComponentData<Identity>(e); if (id.Id == excluded) continue;
                var b = em.GetComponentData<Building>(e); var core = em.GetComponentData<BuildingStats>(e).IsCore != 0;
                if (replacing && pass == 0 && math.distance(Sim.Position(em, e).xz, anchor.xz) > NightPlanOps.Rules(em, root).TargetRadius) continue;
                if (pass == 1 && !core) continue;
                if (!replacing && mode == 3 && d.TargetCategory != 0 && (Sim.Definition(em, root, id.Definition).BuildingPolicy.Category & d.TargetCategory) == 0) continue;
                float path = reachable.Building(b, out _); if (path == float.MaxValue) continue;
                float score = path;
                if (!replacing && preferred == id.Id) score = -3;
                else if (!replacing && mode == 0 && core) score = -2;
                else if (!replacing && mode == 2) score = math.hash(new uint3((uint)id.Id, (uint)(id.Id >> 32), em.GetComponentData<Session>(root).NightSeed ^ math.hash(position))) / (float)uint.MaxValue * 100;
                if (score < best) { best = score; chosen = e; }
            }
            return chosen;
        }
        public static void RefreshTargets(EntityManager em, Entity root)
        {
            using var all = Sim.OrderedEntities<Combatant>(em); var s = em.GetComponentData<Session>(root);
            foreach (var e in all)
            {
                var a = em.GetComponentData<Combatant>(e); if (a.Faction != 1 || a.Deployed == 0 || !Sim.Alive(em, e)) continue;
                var p = em.GetComponentData<Perception>(e); var target = Sim.Find(em, a.HomeId); var nav = em.GetComponentData<NavigationState>(e);
                if (ValidTarget(em, target) && nav.Failed == 0 && a.TargetRevision == em.GetComponentData<GridData>(root).Revision) continue;
                if (s.Time < a.DecisionAt) continue; a.DecisionAt = s.Time + 1;
                var replacement = Target(em, root, em.GetComponentData<Identity>(e).Definition, Sim.Position(em, e), a.TargetAnchor, a.HomeId, !ValidTarget(em, target) || nav.Failed != 0, nav.Failed != 0 ? a.HomeId : 0);
                p.Building = replacement; a.HomeId = replacement == Entity.Null ? 0 : em.GetComponentData<Identity>(replacement).Id; a.TargetRevision = em.GetComponentData<GridData>(root).Revision;
                if (replacement != Entity.Null)
                { using var reachable = new Reach(em, root, Sim.Position(em, e)); reachable.Building(em.GetComponentData<Building>(replacement), out p.BuildingPosition); }
                else { a.Target = Entity.Null; em.SetComponentData(e, new UnitOrder { Kind = OrderKind.Recall, Destination = a.Home }); }
                nav.Failed = 0; nav.Revision = -1; em.SetComponentData(e, nav); em.SetComponentData(e, a); em.SetComponentData(e, p);
            }
        }
    }
}
