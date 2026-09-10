using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Persistence
{
    public static class NightStateValidation
    {
        public static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot data)
        {
            if (data.NightHistory == null || data.Bosses == null || data.Preparation == null) throw new InvalidDataException("Incomplete night state.");
            var p = data.NightPlan;
            if (data.Session.IntelAtNight < 0 || data.Session.IntelAtNight > 100 || data.Session.IntelligenceMode != 0) throw new InvalidDataException("Invalid intelligence checkpoint state.");
            foreach (var w in data.Waves) if (w.SpatiallyBlocked > 1 || w.Region >= em.GetBuffer<SpawnRegion>(root).Length) throw new InvalidDataException("Invalid intelligence region.");
            if (p.Turn < 0 || p.BaseThreat < 0 || p.PreparedTurn < 0 || p.ClockStarted > 1 || p.Committed > 1 || p.AnySpawned > 1 || p.BossKilled > 1 || p.BossEscaped > 1 || !math.isfinite(p.CombatElapsed) || p.CombatElapsed < 0 || !math.isfinite(p.FirstActionAt) || p.FirstActionAt < 0 || p.Turn > 0 && NightPlanOps.Find(em, root, p.Event) < 0) throw new InvalidDataException("Invalid locked night plan.");
            if (p.BossDefinition >= 0 && (!Sim.ValidDefinition(em, root, p.BossDefinition) || Sim.Definition(em, root, p.BossDefinition).Kind != ContentKind.Enemy || (Sim.Definition(em, root, p.BossDefinition).Flags & 1) == 0)) throw new InvalidDataException("Invalid locked boss.");
            var events = new HashSet<string>();
            foreach (var h in data.NightHistory) if (NightPlanOps.Find(em, root, h.Event) < 0 || h.LastTurn < 1 || h.Count < 1 || !events.Add(h.Event.ToString())) throw new InvalidDataException("Invalid night event history.");
            var bosses = new HashSet<int>();
            foreach (var b in data.Bosses)
            {
                var index = NightPlanOps.Find(em, root, b.Event);
                if (index < 0 || em.GetComponentData<ContentCatalog>(root).Value.Value.NightEvents[index].ReturnOnly == 0 || b.DueTurn < 1 || !bosses.Add(b.Definition) || !Sim.ValidDefinition(em, root, b.Definition) || Sim.Definition(em, root, b.Definition).Kind != ContentKind.Enemy || (Sim.Definition(em, root, b.Definition).Flags & 1) == 0) throw new InvalidDataException("Invalid unresolved boss.");
            }
            var definitions = new HashSet<int>();
            foreach (var n in data.Preparation)
            {
                if (!Sim.ValidDefinition(em, root, n.Definition) || !definitions.Add(n.Definition) || !math.isfinite(n.Health) || n.Health <= 0 || !math.isfinite(n.Damage) || n.Damage < 0 || !math.isfinite(n.Speed) || n.Speed < 0) throw new InvalidDataException("Invalid night military snapshot.");
                if (!CombatProfile.Valid(n.Combat) || !math.all(math.isfinite(new float3(n.Range, n.Interval, n.ProjectileSpeed))) || n.Range <= 0 || n.Interval <= 0 || n.ProjectileSpeed <= 0) throw new InvalidDataException("Invalid prepared combat payload.");
                var kind = Sim.Definition(em, root, n.Definition).Kind; if (kind != ContentKind.Soldier && kind != ContentKind.Hero && kind != ContentKind.Building) throw new InvalidDataException("Night snapshot is not a friendly unit or building.");
            }
            foreach (var w in data.Waves) if (!Sim.ValidDefinition(em, root, w.Definition) || Sim.Definition(em, root, w.Definition).Kind != ContentKind.Enemy || w.Count < 1 || w.Count > 256 || w.Region < 0 || w.Spawned > 2 || w.Warned > 1 || !math.isfinite(w.At) || w.At < 0 || w.At > 1 || !math.isfinite(w.PowerScale) || w.PowerScale <= 0 || !math.all(math.isfinite(w.Position))) throw new InvalidDataException("Invalid night wave.");
        }
    }
}
