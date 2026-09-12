using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Landsong.ECS
{
    public static class NightPlanOps
    {
        public static void SetWave(EntityManager em, Entity root, int index, NightWave wave) { var waves = em.GetBuffer<NightWave>(root); waves[index] = wave; }
        public static NightPlanState State(EntityManager em, Entity root) => em.HasComponent<NightPlanState>(root) ? em.GetComponentData<NightPlanState>(root) : new NightPlanState { BossDefinition = -1 };
        public static NightRules Rules(EntityManager em, Entity root) => em.GetComponentData<ContentCatalog>(root).Value.Value.Night;
        public static int Find(EntityManager em, Entity root, FixedString64Bytes id)
        { var blob = em.GetComponentData<ContentCatalog>(root).Value; for (var i = 0; i < blob.Value.NightEvents.Length; i++) if (blob.Value.NightEvents[i].Id == id) return i; return -1; }
        public static bool Eligible(EntityManager em, Entity root, NightEventDefinition e, bool returning)
        {
            var s = em.GetComponentData<Session>(root);
            if (s.Turn < e.MinTurn || e.MaxTurn > 0 && s.Turn > e.MaxTurn || e.Interval > 0 && (s.Turn - e.MinTurn) % e.Interval != 0 || e.ReturnOnly != 0 && !returning) return false;
            if (em.HasBuffer<NightEventHistory>(root)) foreach (var h in em.GetBuffer<NightEventHistory>(root)) if (h.Event == e.Id && (e.Once != 0 && h.Count > 0 || s.Turn <= h.LastTurn + e.Cooldown)) return false;
            var blob = em.GetComponentData<ContentCatalog>(root).Value;
            for (var i = 0; i < e.ConditionCount; i++)
            {
                var r = blob.Value.NightConditions[e.ConditionStart + i];
                if (r.Kind == RuleKind.RequireTurn && s.Turn < r.Amount) return false;
                if ((r.Kind == RuleKind.Prerequisite || r.Kind == RuleKind.RequireTechnology) && !ConditionOps.Satisfied(em, root, r.Target, math.max(1, r.Amount))) return false;
                if (r.Kind == RuleKind.RequireItem && InventoryOps.Count(em, root, r.Target) < r.Amount) return false;
                if (r.Kind == RuleKind.RequireBuilding)
                { int count = 0; using var buildings = Sim.OrderedEntities<Building>(em); foreach (var b in buildings) if (Sim.Operational(em, b) && em.GetComponentData<Identity>(b).Definition == r.Target && em.GetComponentData<Building>(b).Level >= math.max(1, r.Level)) count++; if (count < math.max(1, r.Amount)) return false; }
            }
            return true;
        }
        static int Select(EntityManager em, Entity root, ref Random rng, out int boss)
        {
            boss = -1; var s = em.GetComponentData<Session>(root); var settings = em.GetComponentData<GameSettings>(root); var blob = em.GetComponentData<ContentCatalog>(root).Value;
            // Pending threats are checked again at dawn, never consumed by a forecast or a retry.
            int returnEvent = -1, returnPriority = int.MinValue, due = int.MaxValue;
            foreach (var pending in em.GetBuffer<UnresolvedBoss>(root))
            {
                var at = Find(em, root, pending.Event); if (pending.DueTurn > s.Turn || at < 0 || !Eligible(em, root, blob.Value.NightEvents[at], true)) continue;
                var pendingPriority = blob.Value.NightEvents[at].Priority;
                if (returnEvent < 0 || pendingPriority > returnPriority || pendingPriority == returnPriority && (pending.DueTurn < due || pending.DueTurn == due && pending.Definition < boss))
                { returnEvent = at; returnPriority = pendingPriority; due = pending.DueTurn; boss = pending.Definition; }
            }
            if (returnEvent >= 0) return returnEvent;
            bool battle = s.Turn >= settings.FirstInvasion && rng.NextFloat() < settings.InvasionChance;
            bool bossTurn = s.Turn >= settings.FirstBoss && (s.Turn - settings.FirstBoss) % math.max(1, settings.BossInterval) == 0;
            int chosen = -1, priority = int.MinValue; float total = 0;
            for (var pass = 0; pass < 2 && chosen < 0; pass++)
            for (var i = 0; i < blob.Value.NightEvents.Length; i++)
            {
                var e = blob.Value.NightEvents[i]; if (e.ReturnOnly != 0 || !Eligible(em, root, e, false)) continue;
                bool unresolved = false;
                if (e.Kind == NightKind.Boss) foreach (var pending in em.GetBuffer<UnresolvedBoss>(root)) for (int n = 0; n < e.PoolCount; n++) if (blob.Value.NightEnemies[e.PoolStart + n].Definition == pending.Definition) unresolved = true;
                if (unresolved) continue; // A normal draw cannot bypass a pending boss's delay or return prerequisites.
                if (pass == 0 ? e.Forced == 0 && (e.Kind == NightKind.Boss ? !bossTurn : e.Kind == NightKind.Invasion ? !battle : battle || bossTurn) : e.Kind != NightKind.Peaceful) continue;
                if (e.Priority < priority) continue;
                if (e.Priority > priority) { priority = e.Priority; total = 0; chosen = -1; }
                total += e.Weight; if (rng.NextFloat() * total < e.Weight) chosen = i;
            }
            if (chosen < 0) throw new InvalidOperationException("No eligible peaceful night fallback."); return chosen;
        }
        static int Enemy(EntityManager em, Entity root, NightEventDefinition e, bool boss, ref Random rng)
        {
            var blob = em.GetComponentData<ContentCatalog>(root).Value; float total = 0; int chosen = -1;
            for (int i = 0; i < e.PoolCount; i++) { var p = blob.Value.NightEnemies[e.PoolStart + i]; if (((blob.Value.Definitions[p.Definition].Flags & 1) != 0) != boss) continue; total += p.Weight; if (rng.NextFloat() * total < p.Weight) chosen = p.Definition; }
            return chosen;
        }
        public static void Plan(EntityManager em, Entity root, bool retry)
        {
            var state = State(em, root); var s = em.GetComponentData<Session>(root);
            if (!retry && state.Turn == s.Turn) return;
            Sim.Buffer<NightEventHistory>(em, root); Sim.Buffer<UnresolvedBoss>(em, root); Sim.Buffer<NightPreparation>(em, root);
            s.NightSeed = math.max(1u, Sim.NextRandom(em, root)); s.RandomState = em.GetComponentData<Session>(root).RandomState;
            var rng = new Random(s.NightSeed); var settings = em.GetComponentData<GameSettings>(root); var blob = em.GetComponentData<ContentCatalog>(root).Value;
            int index = retry ? Find(em, root, state.Event) : Select(em, root, ref rng, out _);
            if (index < 0) throw new InvalidOperationException("Missing locked night event."); var e = blob.Value.NightEvents[index];
            if (!retry)
            {
                s.StartCombatStrength = MilitaryOps.Strength(em, root);
                float raw = s.Turn * settings.ThreatPerTurn + s.StartCombatStrength * settings.StrengthRatio;
                var rules = Rules(em, root); raw = math.min(raw, rules.ThreatFloor + s.StartCombatStrength * rules.ThreatPerStrengthCap);
                state = new NightPlanState { Turn = s.Turn, Event = e.Id, BaseThreat = math.max(1, (int)(raw * e.BudgetScale)), BossDefinition = -1 };
                if (e.Kind == NightKind.Boss)
                {
                    foreach (var pending in em.GetBuffer<UnresolvedBoss>(root)) if (pending.Event == e.Id && pending.DueTurn <= s.Turn) { state.BossDefinition = pending.Definition; break; }
                    if (state.BossDefinition < 0) state.BossDefinition = Enemy(em, root, e, true, ref rng);
                }
                s.NightKind = e.Kind; s.NightDuration = math.max(10, e.Duration > 0 ? e.Duration : e.Kind == NightKind.Peaceful ? settings.PeacefulSeconds : settings.BattleSeconds);
                em.GetBuffer<NightPreparation>(root).Clear();
            }
            state.CombatElapsed = 0; state.FirstActionAt = 0; state.ClockStarted = state.Committed = state.AnySpawned = state.BossKilled = state.BossEscaped = 0;
            s.Threat = math.max(1, (int)(state.BaseThreat * (1 - math.min(settings.RetryCap, s.RetryCount * settings.RetryStep)))); s.NightSpeed = 1;
            em.SetComponentData(root, s); // Target hashing uses this plan's seed, including day/dusk retry.
            var waves = em.GetBuffer<NightWave>(root); waves.Clear();
            if (s.NightKind != NightKind.Peaceful)
            {
                var regions = em.GetBuffer<SpawnRegion>(root); if (regions.Length == 0) throw new InvalidOperationException("Combat map has no invasion regions.");
                for (int i = 0; i < e.WaveCount; i++)
                {
                    var d = s.NightKind == NightKind.Boss && i == e.WaveCount - 1 ? state.BossDefinition : Enemy(em, root, e, false, ref rng); if (d < 0) continue;
                    int region = rng.NextInt(regions.Length); var r = regions[region];
                    var position = r.Center + new float3(rng.NextFloat(-.5f, .5f) * r.Size.x, 0, rng.NextFloat(-.5f, .5f) * r.Size.z);
                    bool legal = NightSpatialOps.SpawnPoint(em, root, region, position, out var point); if (legal) position = point;
                    var cost = math.max(1, blob.Value.Definitions[d].Value); int count = (blob.Value.Definitions[d].Flags & 1) != 0 ? 1 : math.clamp((int)(s.Threat / (float)e.WaveCount / cost * rng.NextFloat(.65f, 1.35f)), 1, 256);
                    var target = legal ? NightSpatialOps.Target(em, root, d, position, position, 0, false) : Entity.Null;
                    // Store a plan even when currently blocked. Day construction may reopen it; dusk reprojects within this SAME region.
                    waves = em.GetBuffer<NightWave>(root); waves.Add(new NightWave { At = e.WaveTimes.Length == 0 ? i * .8f / math.max(1, e.WaveCount - 1) : e.WaveTimes[i], Definition = d, Count = count, Direction = r.Direction, Region = region, Position = position, Target = target == Entity.Null ? 0 : em.GetComponentData<Identity>(target).Id, SpatiallyBlocked = (byte)(target == Entity.Null ? 1 : 0) });
                }
                float power = 0; foreach (var w in waves) power += w.Count * math.max(1, blob.Value.Definitions[w.Definition].Value);
                for (int i = 0; i < waves.Length; i++) { var w = waves[i]; w.PowerScale = s.Threat / math.max(1, power); waves[i] = w; }
            }
            Sim.Set(em, root, state); em.SetComponentData(root, s);
        }
        public static void Prepare(EntityManager em, Entity root)
        {
            var state = State(em, root); var s = em.GetComponentData<Session>(root);
            if (state.PreparedTurn != s.Turn)
            {
                Sim.Buffer<NightPreparation>(em, root); em.GetBuffer<NightPreparation>(root).Clear(); var blob = em.GetComponentData<ContentCatalog>(root).Value;
                for (int i = 0; i < blob.Value.Definitions.Length; i++) if (blob.Value.Definitions[i].Kind == ContentKind.Soldier || blob.Value.Definitions[i].Kind == ContentKind.Hero || blob.Value.Definitions[i].Kind == ContentKind.Building)
                    em.GetBuffer<NightPreparation>(root).Add(MilitaryOps.CurrentStats(em, root, i, blob.Value.Definitions[i].Kind == ContentKind.Hero));
                state.PreparedTurn = s.Turn; Sim.Set(em, root, state);
            }
            Reproject(em, root);
        }
        public static void Reproject(EntityManager em, Entity root)
        {
            for (int i = 0; i < em.GetBuffer<NightWave>(root).Length; i++)
            {
                var w = em.GetBuffer<NightWave>(root)[i];
                w.SpatiallyBlocked = 1; w.Target = 0;
                if (NightSpatialOps.SpawnPoint(em, root, w.Region, w.Position, out var point))
                { w.Position = point; var target = NightSpatialOps.Target(em, root, w.Definition, point, point, 0, false); w.Target = target == Entity.Null ? 0 : em.GetComponentData<Identity>(target).Id; w.SpatiallyBlocked = (byte)(target == Entity.Null ? 1 : 0); }
                NightPlanOps.SetWave(em, root, i, w);
            }
        }
        public static void BossDeath(EntityManager em, Entity root, int definition)
        { var state = State(em, root); if (state.BossDefinition == definition) { state.BossKilled = 1; Sim.Set(em, root, state); } }
        public static void Commit(EntityManager em, Entity root)
        {
            var p = State(em, root); if (p.Committed != 0 || p.Turn == 0) return;
            var at = Find(em, root, p.Event); if (at < 0) return; var e = em.GetComponentData<ContentCatalog>(root).Value.Value.NightEvents[at];
            p.Committed = 1; Sim.Set(em, root, p);
            if (e.Kind != NightKind.Peaceful && p.AnySpawned == 0) return;
            var history = em.GetBuffer<NightEventHistory>(root); int found = -1; for (int i = 0; i < history.Length; i++) if (history[i].Event == e.Id) found = i;
            var h = found < 0 ? new NightEventHistory { Event = e.Id } : history[found]; h.LastTurn = p.Turn; h.Count++; if (found < 0) history.Add(h); else history[found] = h;
            var pending = em.GetBuffer<UnresolvedBoss>(root);
            if (p.BossKilled != 0 || p.BossEscaped != 0) for (int i = pending.Length - 1; i >= 0; i--) if (pending[i].Definition == p.BossDefinition) pending.RemoveAt(i);
            if (e.Kind == NightKind.Boss && p.BossKilled == 0 && !e.FollowUp.IsEmpty)
            { for (int i = pending.Length - 1; i >= 0; i--) if (pending[i].Definition == p.BossDefinition) pending.RemoveAt(i); pending.Add(new UnresolvedBoss { Event = e.FollowUp, Definition = p.BossDefinition, DueTurn = p.Turn + e.ReturnDelay }); }
            var s = em.GetComponentData<Session>(root); s.BossReturnTurn = 0; foreach (var b in pending) if (s.BossReturnTurn == 0 || b.DueTurn < s.BossReturnTurn) s.BossReturnTurn = b.DueTurn; em.SetComponentData(root, s);
        }
    }
}
