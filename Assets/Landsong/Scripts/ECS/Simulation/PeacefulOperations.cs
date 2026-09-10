using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class PeacefulOps
    {
        public static PeacefulRules Rules(EntityManager em, Entity root) => em.GetComponentData<ContentCatalog>(root).Value.Value.Peaceful;
        public static void Reset(EntityManager em, Entity root)
        {
            var seed = math.hash(new uint2(em.GetComponentData<Session>(root).NightSeed, 0x50454143u));
            Sim.Set(em, root, new PeacefulState { Random = math.max(1u, seed), Next = Rules(em, root).FirstOpportunity });
            Sim.Buffer<OpportunityCount>(em, root); em.GetBuffer<OpportunityCount>(root).Clear();
        }
        public static bool Stealable(EntityManager em, Entity root, InventorySlot slot)
        {
            if (slot.Count <= 0 || slot.Unavailable != 0 || !Sim.ValidDefinition(em, root, slot.Item)) return false;
            var d = Sim.Definition(em, root, slot.Item); return d.Kind == ContentKind.Item && d.Capacity > 1 && d.Theft.Protection == ItemProtection.None && d.Theft.Weight > 0 && d.Theft.Maximum > 0;
        }
        public static List<int> Items(EntityManager em, Entity root, ulong source)
        {
            var result = new List<int>(); foreach (var slot in em.GetBuffer<InventorySlot>(root)) if (slot.Provider == source && Stealable(em, root, slot) && !result.Contains(slot.Item)) result.Add(slot.Item);
            result.Sort(); return result;
        }
        public static bool Eligible(EntityManager em, Entity actor, OpportunityProfile profile)
        {
            if (!Sim.Alive(em, actor) || !em.HasComponent<Combatant>(actor) || !em.HasComponent<UnitOrder>(actor)) return false;
            var a = em.GetComponentData<Combatant>(actor); if (a.Faction != 0 || a.Deployed == 0 || a.Speed <= 0) return false;
            if (em.HasComponent<Hero>(actor)) return profile.Heroes;
            return profile.Soldiers && em.HasComponent<Soldier>(actor) && em.GetComponentData<Soldier>(actor).RecallState == 0;
        }
        static bool Claimed(EntityManager em, Entity actor, Entity except)
        {
            using var all = Sim.Entities<Opportunity>(em); foreach (var e in all) if (e != except && em.GetComponentData<Opportunity>(e).Responder == actor) return true; return false;
        }
        // Four-neighbour, elevation-aware route. End in the outward direction, never through footprints.
        public static List<float3> Route(EntityManager em, Entity root, float3 start, float wanted)
        {
            var grid = em.GetComponentData<GridData>(root); var occupied = em.GetBuffer<Occupancy>(root); var first = GridOps.Index(grid, GridOps.Cell(grid, start));
            var result = new List<float3>(); if (first < 0 || !GridOps.Traversable(grid, occupied, GridOps.Cell(grid, start))) return result;
            var size = grid.Value.Value.Size; var min = grid.Value.Value.Min; var startCell = GridOps.Cell(grid, start);
            float2 center = (float2)min + (float2)size * .5f, outward = math.normalizesafe((float2)startCell - center, new float2(1, 0));
            var previous = new Dictionary<int, int> { [first] = -1 }; var distance = new Dictionary<int, float> { [first] = 0 }; var queue = new Queue<int>(); queue.Enqueue(first);
            int last = -1; float best = float.MinValue;
            while (queue.Count > 0)
            {
                int at = queue.Dequeue(); var c = new int2(at % size.x, at / size.x) + min;
                if (distance[at] >= wanted)
                { float score = math.dot((float2)(c - startCell), outward); if (score > best && score > 0) { best = score; last = at; } continue; }
                for (int d = 0; d < 4; d++)
                {
                    var to = c + (d == 0 ? new int2(1, 0) : d == 1 ? new int2(0, 1) : d == 2 ? new int2(-1, 0) : new int2(0, -1));
                    int index = GridOps.Index(grid, to); if (index < 0 || previous.ContainsKey(index) || !GridOps.Traversable(grid, occupied, to) || math.abs(grid.Value.Value.Cells[index].Height - grid.Value.Value.Cells[at].Height) > grid.CellSize) continue;
                    previous[index] = at; distance[index] = distance[at] + grid.CellSize; queue.Enqueue(index);
                }
            }
            if (last < 0) return result;
            while (last != first) { var c = new int2(last % size.x, last / size.x) + min; result.Add(GridOps.Position(grid, c, new int2(1)) + new float3(0, .5f, 0)); last = previous[last]; }
            result.Reverse(); return result;
        }
        static bool Intercept(EntityManager em, Entity root, Entity actor, float3 start, IList<float3> route, OpportunityProfile p, float remaining, out float3 point, float reaction = 0)
        {
            point = start; if (!Eligible(em, actor, p) || math.distance(Sim.Position(em, actor).xz, start.xz) > p.ResponseRadius) return false;
            using var reach = new NightSpatialOps.Reach(em, root, Sim.Position(em, actor)); float speed = em.GetComponentData<Combatant>(actor).Speed;
            if (reach.Distance(start) <= p.CaptureRadius && reaction == 0) return true;
            float elapsed = 0; var previous = start;
            foreach (var target in route)
            {
                elapsed += math.distance(previous.xz, target.xz) / p.Speed; previous = target;
                if (elapsed > remaining) break;
                if (reaction + math.max(0, reach.Distance(target) - p.CaptureRadius) / speed <= elapsed) { point = target; return true; }
            }
            return false;
        }
        static List<float3> Remaining(EntityManager em, Entity e)
        {
            var result = new List<float3>(); if (!em.HasBuffer<VisitorPathPoint>(e)) return result;
            int next = em.GetComponentData<Opportunity>(e).PathIndex; var route = em.GetBuffer<VisitorPathPoint>(e); for (int i = next; i < route.Length; i++) result.Add(route[i].Position); return result;
        }
        public static Entity TrySpawn(EntityManager em, Entity root, int definition, ulong provider, uint seed)
        {
            var s = em.GetComponentData<Session>(root); if (s.Phase != Phase.Night || s.NightKind != NightKind.Peaceful || s.Paused != 0 || !Sim.ValidDefinition(em, root, definition)) return Entity.Null;
            var d = Sim.Definition(em, root, definition); var p = d.Opportunity; var site = Sim.Find(em, provider);
            if (d.Kind != ContentKind.Opportunity || !Sim.Operational(em, site) || s.PhaseTime > s.NightDuration * p.EndFraction || s.PhaseTime < s.NightDuration * p.StartFraction || s.NightDuration - s.PhaseTime < p.MinimumResponse || p.Kind == VisitorKind.Thief && Items(em, root, provider).Count == 0) return Entity.Null;
            using (var all = Sim.Entities<Opportunity>(em)) foreach (var e in all) if (em.GetComponentData<Opportunity>(e).Provider == provider) return Entity.Null;
            var b = em.GetComponentData<Building>(site); var grid = em.GetComponentData<GridData>(root); var candidates = new List<float3>();
            for (int y = -1; y <= b.Size.y; y++) for (int x = -1; x <= b.Size.x; x++)
            {
                if (x >= 0 && y >= 0 && x < b.Size.x && y < b.Size.y) continue; var cell = b.Cell + new int2(x, y);
                if (GridOps.Traversable(grid, em.GetBuffer<Occupancy>(root), cell)) candidates.Add(GridOps.Position(grid, cell, new int2(1)) + new float3(0, .5f, 0));
            }
            if (candidates.Count == 0) return Entity.Null; int offset = (int)(seed % (uint)candidates.Count);
            using var units = Sim.OrderedEntities<Combatant>(em);
            for (int i = 0; i < candidates.Count; i++)
            {
                var position = candidates[(i + offset) % candidates.Count]; var route = Route(em, root, position, math.min(p.RouteLength, (s.NightDuration - s.PhaseTime) * p.Speed));
                if (route.Count == 0) continue; bool response = false;
                foreach (var actor in units)
                {
                    if (Claimed(em, actor, Entity.Null)) continue;
                    // Reserve human reaction time before testing a possible interception.
                    if (Intercept(em, root, actor, position, route, p, s.NightDuration - s.PhaseTime, out _, 1)) { response = true; break; }
                }
                if (!response) continue;
                var visitor = Sim.Spawn(em, root, definition, position, false);
                Sim.Set(em, visitor, new Opportunity { Provider = provider, Thief = (byte)(p.Kind == VisitorKind.Thief ? 1 : 0), Expires = s.Time + math.min(s.NightDuration - s.PhaseTime, route.Count * grid.CellSize / p.Speed), Exit = route[route.Count - 1], Random = math.max(1u, seed), SourceName = em.GetComponentData<Identity>(site).Name });
                Sim.Buffer<VisitorPathPoint>(em, visitor); foreach (var point in route) em.GetBuffer<VisitorPathPoint>(visitor).Add(new VisitorPathPoint { Position = point });
                Sim.Set(em, visitor, new VisualState { Visible = 1 });
                Sim.Emit(em, root, EventKind.Message, p.Kind == VisitorKind.Thief ? "发现小偷。点击标记派人拦截，或敲响附近警铃。" : "发现小精灵。点击标记派人接近，或敲响附近警铃。"); return visitor;
            }
            return Entity.Null;
        }
        public static ResultCode Claim(EntityManager em, Entity root, Entity e, Entity bell = default)
        {
            var s = em.GetComponentData<Session>(root); if (s.Phase != Phase.Night || s.NightKind != NightKind.Peaceful || s.Paused != 0 || s.IntelligenceMode != 0) return ResultCode.WrongPhase;
            if (!em.Exists(e) || !em.HasComponent<Opportunity>(e)) return ResultCode.InvalidTarget;
            var o = em.GetComponentData<Opportunity>(e); if (o.Responder != Entity.Null) return ResultCode.Busy;
            var p = Sim.Definition(em, root, em.GetComponentData<Identity>(e).Definition).Opportunity; var path = Remaining(em, e);
            Entity chosen = Entity.Null; float3 point = default; float best = float.MaxValue;
            using var all = Sim.OrderedEntities<Combatant>(em);
            foreach (var actor in all)
            {
                if (Claimed(em, actor, e)) continue;
                if (bell != Entity.Null && (actor == s.SelectedHero || math.distance(Sim.Position(em, actor), Sim.Position(em, bell)) > em.GetComponentData<BuildingStats>(bell).BellRadius)) continue;
                if (!Intercept(em, root, actor, Sim.Position(em, e), path, p, o.Expires - s.Time, out var intercept)) continue;
                float score = actor == s.SelectedHero ? -1 : math.distancesq(Sim.Position(em, actor), intercept); if (score >= best) continue;
                best = score; chosen = actor; point = intercept;
            }
            if (chosen == Entity.Null) { if (bell == Entity.Null) Sim.Emit(em, root, EventKind.Message, "没有能及时赶到的可用士兵或英雄。"); return ResultCode.Unavailable; }
            o.Responder = chosen; em.SetComponentData(e, o);
            MilitaryOps.Order(em, chosen, new UnitOrder { Kind = OrderKind.Capture, Target = e, Destination = point, Source = bell == Entity.Null ? 0 : em.GetComponentData<Identity>(bell).Id });
            Sim.Emit(em, root, EventKind.Message, o.Thief != 0 ? "已派出响应单位，正在接近小偷。" : "已派出响应单位，正在接近小精灵。"); return ResultCode.Success;
        }
        public static void Bell(EntityManager em, Entity root, Entity bell)
        {
            if (em.GetComponentData<Session>(root).ActiveBell == 0) return;
            using var all = Sim.OrderedEntities<Opportunity>(em); foreach (var e in all)
            {
                var o = em.GetComponentData<Opportunity>(e);
                if (em.Exists(o.Responder))
                { var order = em.GetComponentData<UnitOrder>(o.Responder); if (order.Kind != OrderKind.Capture || order.Target != e) { o.Responder = Entity.Null; em.SetComponentData(e, o); } }
                if (math.distance(Sim.Position(em, e), Sim.Position(em, bell)) <= em.GetComponentData<BuildingStats>(bell).BellRadius) Claim(em, root, e, bell);
            }
        }
        static void Release(EntityManager em, Entity root, Entity e, Opportunity o)
        {
            if (em.Exists(o.Responder) && em.HasComponent<UnitOrder>(o.Responder))
            { var order = em.GetComponentData<UnitOrder>(o.Responder); if (order.Kind == OrderKind.Capture && order.Target == e) MilitaryOps.Order(em, o.Responder, new UnitOrder()); }
        }
        public static void Finish(EntityManager em, Entity root, Entity e, bool caught, bool cancelled = false)
        {
            if (!em.Exists(e) || !em.HasComponent<Opportunity>(e)) return;
            var o = em.GetComponentData<Opportunity>(e); var id = em.GetComponentData<Identity>(e);
            if (caught && o.Thief == 0) NightResultOps.RecordDefinition(em, root, id.Id, id.Definition);
            int stolen = 0;
            if (!caught && !cancelled && o.Thief != 0 && Sim.Operational(em, Sim.Find(em, o.Provider)))
            {
                var state = em.GetComponentData<PeacefulState>(root); int budget = math.max(0, Rules(em, root).TheftValueBudget - state.StolenValue); var items = Items(em, root, o.Provider); int weight = 0;
                foreach (var item in items) { var t = Sim.Definition(em, root, item).Theft; if (t.UnitValue <= budget) weight += t.Weight; }
                if (weight > 0)
                {
                    var random = new Random(math.max(1u, o.Random)); int pick = random.NextInt(weight);
                    foreach (var item in items)
                    {
                        var t = Sim.Definition(em, root, item).Theft; if (t.UnitValue > budget) continue; pick -= t.Weight; if (pick >= 0) continue;
                        int count = 0; foreach (var slot in em.GetBuffer<InventorySlot>(root)) if (slot.Provider == o.Provider && slot.Item == item && Stealable(em, root, slot)) count += slot.Count;
                        stolen = math.min(count, math.min(t.Maximum, budget / t.UnitValue)); InventoryOps.Remove(em, root, item, stolen, o.Provider); state.StolenValue += stolen * t.UnitValue; em.SetComponentData(root, state);
                        em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.Theft, Id = o.Provider, Definition = item, Amount = stolen, SourceName = o.SourceName }); break;
                    }
                }
            }
            var kind = cancelled ? EventKind.VisitorCancelled : caught ? o.Thief != 0 ? EventKind.TheftPrevented : EventKind.FairyCaught : EventKind.VisitorEscaped;
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = kind, Id = id.Id, Definition = id.Definition, Amount = stolen, SourceName = o.SourceName });
            if(caught&&!cancelled)em.GetBuffer<GameEvent>(root).Add(new GameEvent{Kind=kind,Target=id.Id,Definition=id.Definition,Position=Sim.Position(em,e)});
            Sim.Emit(em, root, EventKind.Message, cancelled ? "互动已取消：没有可行的响应机会。" : caught ? o.Thief != 0 ? "阻止盗窃！" : "接到了小精灵的礼物，将在黎明入库。" : "访客已离开，详情将在结算中显示。");
            Release(em, root, e, o); em.DestroyEntity(e);
        }
        public static void Tick(EntityManager em, Entity root, float delta)
        {
            var s = em.GetComponentData<Session>(root); if (s.Paused != 0 || s.Phase != Phase.Night || s.NightKind != NightKind.Peaceful) return;
            if (!em.HasComponent<PeacefulState>(root)) Reset(em, root);
            var state = em.GetComponentData<PeacefulState>(root); var rules = Rules(em, root);
            using (var current = Sim.OrderedEntities<Opportunity>(em))
            {
                foreach (var e in current)
                {
                    var o = em.GetComponentData<Opportunity>(e); var p = Sim.Definition(em, root, em.GetComponentData<Identity>(e).Definition).Opportunity;
                    bool eligible = false; using (var units = Sim.Entities<Combatant>(em)) foreach (var unit in units) if (Eligible(em, unit, p)) { eligible = true; break; }
                    if (!eligible) { Finish(em, root, e, false, true); continue; }
                    if (o.Responder != Entity.Null)
                    {
                        if (!Eligible(em, o.Responder, p) || em.GetComponentData<UnitOrder>(o.Responder).Kind != OrderKind.Capture || em.GetComponentData<UnitOrder>(o.Responder).Target != e)
                        { Release(em, root, e, o); o.Responder = Entity.Null; em.SetComponentData(e, o); }
                        else if (math.distance(Sim.Position(em, e).xz, Sim.Position(em, o.Responder).xz) <= p.CaptureRadius) { Finish(em, root, e, true); continue; }
                    }
                    if (s.Time >= o.Expires || s.PhaseTime >= s.NightDuration) { Finish(em, root, e, false); continue; }
                    var route = em.GetBuffer<VisitorPathPoint>(e); var transform = em.GetComponentData<LocalTransform>(e); float travel = delta * p.Speed;
                    while (o.PathIndex < route.Length && travel > 0)
                    {
                        var next = route[o.PathIndex].Position;
                        if (!GridOps.Traversable(em.GetComponentData<GridData>(root), em.GetBuffer<Occupancy>(root), GridOps.Cell(em.GetComponentData<GridData>(root), next))) { Finish(em, root, e, false, true); break; }
                        float distance = math.distance(transform.Position, next); if (distance <= travel) { transform.Position = next; travel -= distance; o.PathIndex++; } else { transform.Position += math.normalizesafe(next - transform.Position) * travel; travel = 0; }
                    }
                    if (!em.Exists(e)) continue; em.SetComponentData(e, o); em.SetComponentData(e, transform);
                    if (o.PathIndex >= route.Length) Finish(em, root, e, false);
                }
            }
            state = em.GetComponentData<PeacefulState>(root);
            if (s.PhaseTime < state.Next || s.PhaseTime > s.NightDuration * .5f || state.Spawned >= rules.MaximumPerNight) return;
            state.Next = s.PhaseTime + rules.Interval; var random = new Random(math.max(1u, state.Random));
            using var active = Sim.Entities<Opportunity>(em); if (active.Length >= rules.MaximumConcurrent) { em.SetComponentData(root, state); return; }
            var definitions = new List<int>(); int sum = 0; var blob = em.GetComponentData<ContentCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
            {
                var d = blob.Value.Definitions[i]; if (d.Kind != ContentKind.Opportunity) continue; var p = d.Opportunity; int count = 0;
                foreach (var c in em.GetBuffer<OpportunityCount>(root)) if (c.Definition == i) count = c.Count;
                if (count >= p.MaximumPerNight || s.PhaseTime < p.StartFraction * s.NightDuration || s.PhaseTime > p.EndFraction * s.NightDuration) continue;
                definitions.Add(i); sum += p.Weight;
            }
            if (sum > 0)
            {
                int pick = random.NextInt(sum), definition = -1;
                foreach (var i in definitions) { pick -= blob.Value.Definitions[i].Opportunity.Weight; if (pick < 0) { definition = i; break; } }
                var p = blob.Value.Definitions[definition].Opportunity; var sources = new List<ulong>();
                using var sites = Sim.OrderedEntities<Building>(em); foreach (var site in sites) if (Sim.Operational(em, site))
                { ulong id = em.GetComponentData<Identity>(site).Id; if (p.Kind != VisitorKind.Thief || Items(em, root, id).Count > 0) sources.Add(id); }
                // Buildings are equally weighted; a failed location is cancelled, not rerolled until it steals.
                if (sources.Count > 0 && TrySpawn(em, root, definition, sources[random.NextInt(sources.Count)], random.NextUInt(1, uint.MaxValue)) != Entity.Null)
                {
                    state.Spawned++; var counts = em.GetBuffer<OpportunityCount>(root); bool found = false;
                    for (int i = 0; i < counts.Length; i++) if (counts[i].Definition == definition) { var c = counts[i]; c.Count++; counts[i] = c; found = true; break; }
                    if (!found) counts.Add(new OpportunityCount { Definition = definition, Count = 1 });
                }
            }
            state.Random = random.state; em.SetComponentData(root, state);
        }
    }
}
