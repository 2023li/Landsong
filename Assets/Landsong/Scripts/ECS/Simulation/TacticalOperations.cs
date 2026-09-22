using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class TacticalOps
    {
        struct Reservation
        {
            public float3 Point;
            public float Radius;
        }

        public static void Update(EntityManager em, Entity root)
        {
            GameClock sClock = em.GetComponentData<GameClock>(root);
            var grid = em.GetComponentData<GridData>(root);
            var occupied = em.GetBuffer<Occupancy>(root);
            using var units = WorldQueries.OrderedEntities<Combatant>(em);
            using var reserved = new NativeParallelMultiHashMap<int2, Reservation>(math.max(1, units.Length), Allocator.Temp);
            const float cellSize = 2;
            float maximumRadius = 0;
            foreach (var e in units)
            {
                if (!EntityState.Alive(em, e) || !em.HasComponent<TacticalState>(e))
                    continue;
                if (em.HasComponent<SimulationOwner>(e) && em.GetComponentData<SimulationOwner>(e).Root != root)
                    continue;
                var a = em.GetComponentData<Combatant>(e);
                if (a.Deployed == 0)
                    continue;
                var t = em.GetComponentData<TacticalState>(e);
                var p = em.GetComponentData<Perception>(e);
                var order = em.GetComponentData<UnitOrder>(e);
                var position = EntityState.Position(em, e);
                bool commanded = a.Faction == 0 && order.Kind != OrderKind.Automatic;
                if (commanded)
                {
                    t.Returning = 0;
                    t.Pursued = Entity.Null;
                }
                else if ((a.Profile.Traits & TacticalTraits.NearestSoldier) != 0)
                {
                    t.Returning = 0;
                    t.Pursued = EntityState.Alive(em, p.Enemy) ? p.Enemy : Entity.Null;
                    p.Building = Entity.Null;
                }
                else
                {
                    if (t.Pursued == Entity.Null && EntityState.Alive(em, p.Enemy))
                    {
                        t.Pursued = p.Enemy;
                        t.Started = sClock.Time;
                        t.Origin = a.Faction == 0 ? a.Home : position;
                        if (a.Faction == 0 && !GridOps.Traversable(grid, occupied, GridOps.Cell(grid, t.Origin)))
                        {
                            var site = WorldQueries.Find(em, a.HomeId);
                            using var reach = new NightSpatialOps.Reach(em, root, position);
                            if (site != Entity.Null && em.HasComponent<Building>(site) && reach.Building(em.GetComponentData<BuildingPlacementState>(site), out var point) < float.MaxValue)
                                t.Origin = point;
                            else
                                t.Origin = position;
                        }
                    }

                    if (t.Pursued != Entity.Null && (sClock.Time - t.Started >= a.Profile.ChaseSeconds || math.distance(position.xz, t.Origin.xz) > a.Profile.ChaseRadius || em.GetComponentData<NavigationState>(e).Failed != 0))
                        t.Returning = 1;
                    if (t.Returning != 0)
                    {
                        p.Enemy = Entity.Null;
                        p.EnemyInRange = 0;
                        if (math.distance(position.xz, t.Origin.xz) < 1)
                        {
                            t.Returning = 0;
                            t.Pursued = Entity.Null;
                        }
                    }
                    else if (!EntityState.Alive(em, p.Enemy))
                        t.Pursued = Entity.Null;
                }

                t.HasSlot = 0;
                t.Engagement = position;
                Entity target = EntityState.Alive(em, p.Enemy) ? p.Enemy : a.Faction == 1 ? p.Building : Entity.Null;
                if (!commanded && t.Returning == 0 && EntityState.Alive(em, target))
                {
                    float best = float.MaxValue;
                    var center = EntityState.Position(em, target);
                    void Consider(float3 point)
                    {
                        if (!GridOps.Traversable(grid, occupied, GridOps.Cell(grid, point)))
                            return;
                        var cell = (int2)math.floor(point.xz / cellSize);
                        int range = (int)math.ceil((a.Profile.BodyRadius + maximumRadius) / cellSize);
                        for (int y = -range; y <= range; y++)
                            for (int x = -range; x <= range; x++)
                            {
                                if (!reserved.TryGetFirstValue(cell + new int2(x, y), out var r, out var iterator))
                                    continue;
                                do
                                {
                                    if (math.distancesq(point.xz, r.Point.xz) < math.square(a.Profile.BodyRadius + r.Radius))
                                        return;
                                }
                                while (reserved.TryGetNextValue(out r, ref iterator));
                            }

                        float score = math.distancesq(position, point);
                        if (score < best)
                        {
                            best = score;
                            t.Engagement = point;
                            t.HasSlot = 1;
                        }
                    }

                    if (em.HasComponent<Building>(target))
                    {
                        BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(target);
                        for (int y = -1; y <= bPlacement.Size.y; y++)
                            for (int x = -1; x <= bPlacement.Size.x; x++)
                            {
                                if (x >= 0 && y >= 0 && x < bPlacement.Size.x && y < bPlacement.Size.y)
                                    continue;
                                Consider(GridOps.Position(grid, bPlacement.Cell + new int2(x, y), new int2(1)) + new float3(0, .5f, 0));
                            }
                    }
                    else
                        for (int i = 0; i < 16; i++)
                        {
                            float angle = i * math.PI / 8;
                            float body = em.HasComponent<Combatant>(target) ? em.GetComponentData<Combatant>(target).Profile.BodyRadius : 0;
                            Consider(center + new float3(math.cos(angle), 0, math.sin(angle)) * (body + math.max(a.Profile.BodyRadius, a.Range * .75f)));
                        }

                    if (t.HasSlot != 0)
                    {
                        reserved.Add((int2)math.floor(t.Engagement.xz / cellSize), new Reservation { Point = t.Engagement, Radius = a.Profile.BodyRadius });
                        maximumRadius = math.max(maximumRadius, a.Profile.BodyRadius);
                    }
                }

                em.SetComponentData(e, t);
                em.SetComponentData(e, p);
            }
        }
    }
}
