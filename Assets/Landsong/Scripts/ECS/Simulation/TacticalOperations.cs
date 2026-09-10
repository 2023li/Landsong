using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class TacticalOps
    {
        struct Reservation { public float3 Point; public float Radius; }
        public static void Update(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root); var grid = em.GetComponentData<GridData>(root); var occupied = em.GetBuffer<Occupancy>(root);
            var reserved = new List<Reservation>(); using var units = Sim.OrderedEntities<Combatant>(em);
            foreach (var e in units)
            {
                if (!Sim.Alive(em, e) || !em.HasComponent<TacticalState>(e)) continue;
                var a = em.GetComponentData<Combatant>(e); if (a.Deployed == 0) continue;
                var t = em.GetComponentData<TacticalState>(e); var p = em.GetComponentData<Perception>(e); var order = em.GetComponentData<UnitOrder>(e); var position = Sim.Position(em, e);
                bool commanded = a.Faction == 0 && order.Kind != OrderKind.Automatic;
                if (commanded) { t.Returning = 0; t.Pursued = Entity.Null; }
                else
                {
                    if (t.Pursued == Entity.Null && Sim.Alive(em, p.Enemy))
                    {
                        t.Pursued = p.Enemy; t.Started = s.Time; t.Origin = a.Faction == 0 ? a.Home : position;
                        if (a.Faction == 0 && !GridOps.Traversable(grid, occupied, GridOps.Cell(grid, t.Origin)))
                        { var site = Sim.Find(em, a.HomeId); using var reach = new NightSpatialOps.Reach(em, root, position); if (site != Entity.Null && em.HasComponent<Building>(site) && reach.Building(em.GetComponentData<Building>(site), out var point) < float.MaxValue) t.Origin = point; else t.Origin = position; }
                    }
                    if (t.Pursued != Entity.Null && (s.Time - t.Started >= a.Profile.ChaseSeconds || math.distance(position.xz, t.Origin.xz) > a.Profile.ChaseRadius || em.GetComponentData<NavigationState>(e).Failed != 0)) t.Returning = 1;
                    if (t.Returning != 0)
                    {
                        p.Enemy = Entity.Null; p.EnemyInRange = 0;
                        if (math.distance(position.xz, t.Origin.xz) < 1) { t.Returning = 0; t.Pursued = Entity.Null; }
                    }
                    else if (!Sim.Alive(em, p.Enemy)) t.Pursued = Entity.Null;
                }
                t.HasSlot = 0; t.Engagement = position;
                Entity target = Sim.Alive(em, p.Enemy) ? p.Enemy : a.Faction == 1 ? p.Building : Entity.Null;
                if (!commanded && t.Returning == 0 && Sim.Alive(em, target))
                {
                    float best = float.MaxValue; var center = Sim.Position(em, target);
                    void Consider(float3 point)
                    {
                        if (!GridOps.Traversable(grid, occupied, GridOps.Cell(grid, point))) return;
                        foreach (var r in reserved) if (math.distance(point.xz, r.Point.xz) < a.Profile.BodyRadius + r.Radius) return;
                        float score = math.distancesq(position, point); if (score < best) { best = score; t.Engagement = point; t.HasSlot = 1; }
                    }
                    if (em.HasComponent<Building>(target))
                    {
                        var b = em.GetComponentData<Building>(target);
                        for (int y = -1; y <= b.Size.y; y++) for (int x = -1; x <= b.Size.x; x++)
                        { if (x >= 0 && y >= 0 && x < b.Size.x && y < b.Size.y) continue; Consider(GridOps.Position(grid, b.Cell + new int2(x, y), new int2(1)) + new float3(0, .5f, 0)); }
                    }
                    else for (int i = 0; i < 16; i++) { float angle = i * math.PI / 8; float body = em.HasComponent<Combatant>(target) ? em.GetComponentData<Combatant>(target).Profile.BodyRadius : 0; Consider(center + new float3(math.cos(angle), 0, math.sin(angle)) * (body + math.max(a.Profile.BodyRadius, a.Range * .75f))); }
                    if (t.HasSlot != 0) reserved.Add(new Reservation { Point = t.Engagement, Radius = a.Profile.BodyRadius });
                }
                em.SetComponentData(e, t); em.SetComponentData(e, p);
            }
        }
    }
}
