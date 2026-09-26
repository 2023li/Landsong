using Landsong.ECS.Definitions;
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
            if (data.Waves == null || data.SpawnRegions == null || data.SpawnRegions.Length > 32 || data.NightHistory == null || data.Bosses == null || data.PreparedSoldiers == null || data.PreparedHeroes == null || data.PreparedBuildings == null)
                throw new InvalidDataException("Incomplete night state.");
            var grid = em.GetComponentData<GridData>(root);
            foreach (var region in data.SpawnRegions)
            {
                if (region.EdgeOnly > 1 || (region.Direction != 10 && region.Direction != 20 && region.Direction != 30 && region.Direction != 40) || !math.all(math.isfinite(region.Center)) || !math.all(math.isfinite(region.Size)) || math.any(region.Size <= 0) || GridOps.Index(grid, GridOps.Cell(grid, region.Center)) < 0 || math.any(region.Size.xz > (float2)grid.Value.Value.Size * grid.CellSize + .001f))
                    throw new InvalidDataException("Invalid dynamic spawn region.");
                var low = grid.Origin.xz + (float2)grid.Value.Value.Min * grid.CellSize;
                var high = low + (float2)grid.Value.Value.Size * grid.CellSize;
                if (math.any(region.Center.xz - region.Size.xz * .5f < low - .001f) || math.any(region.Center.xz + region.Size.xz * .5f > high + .001f))
                    throw new InvalidDataException("Spawn region outside map bounds.");
                if (region.EdgeOnly != 0)
                {
                    bool containsEdge = false;
                    for (int i = 0; i < grid.Value.Value.Cells.Length; i++)
                        if (grid.Value.Value.Cells[i].EdgeZone != 0 && NightSpatialOps.Inside(region, GridOps.Position(grid, grid.Value.Value.Min + new int2(i % grid.Value.Value.Size.x, i / grid.Value.Value.Size.x), new int2(1))))
                        {
                            containsEdge = true;
                            break;
                        }

                    if (!containsEdge)
                        throw new InvalidDataException("Fallback region has no authored edge cells.");
                }
            }

            var p = data.NightPlan;
            if (data.Night.Intelligence < 0 || data.Night.Intelligence > 100 || data.IntelligenceMode.Enabled != 0)
                throw new InvalidDataException("Invalid intelligence checkpoint state.");
            foreach (var w in data.Waves)
                if (w.SpatiallyBlocked > 1 || w.Region >= data.SpawnRegions.Length)
                    throw new InvalidDataException("Invalid intelligence region.");
            if (p.Turn < 0 || p.BaseThreat < 0 || p.PreparedTurn < 0 || p.ClockStarted > 1 || p.Committed > 1 || p.AnySpawned > 1 || p.BossKilled > 1 || p.BossEscaped > 1 || !math.isfinite(p.DifficultyScale) || p.DifficultyScale < 0 || !math.isfinite(p.CombatElapsed) || p.CombatElapsed < 0 || !math.isfinite(p.FirstActionAt) || p.FirstActionAt < 0 || p.Turn > 0 && NightPlanOps.Find(em, root, p.Event) < 0)
                throw new InvalidDataException("Invalid locked night plan.");
            if (p.BossDefinition.IsValid && (!EnemyDefinitions.IsValid(em, root, p.BossDefinition) || (EnemyDefinitions.Get(em, root, p.BossDefinition).Behavior & EnemyBehaviorFlags.Boss) == 0))
                throw new InvalidDataException("Invalid locked boss.");
            var events = new HashSet<string>();
            foreach (var h in data.NightHistory)
                if (NightPlanOps.Find(em, root, h.Event) < 0 || h.LastTurn < 1 || h.Count < 1 || !events.Add(h.Event.ToString()))
                    throw new InvalidDataException("Invalid night event history.");
            var bosses = new HashSet<EnemyId>();
            foreach (var b in data.Bosses)
            {
                var index = NightPlanOps.Find(em, root, b.Event);
                if (index < 0 || em.GetComponentData<NightEventCatalog>(root).Value.Value.Events[index].ReturnOnly == 0 || b.DueTurn < 1 || !bosses.Add(b.Definition) || !EnemyDefinitions.IsValid(em, root, b.Definition) || (EnemyDefinitions.Get(em, root, b.Definition).Behavior & EnemyBehaviorFlags.Boss) == 0)
                    throw new InvalidDataException("Invalid unresolved boss.");
            }

            var soldiers = new HashSet<SoldierId>();
            foreach (var row in data.PreparedSoldiers)
            {
                if (!SoldierDefinitions.IsValid(em, root, row.Definition) || !soldiers.Add(row.Definition))
                    throw new InvalidDataException("Invalid prepared soldier definition");
                ValidateStats(row.Stats);
            }

            var heroes = new HashSet<HeroId>();
            foreach (var row in data.PreparedHeroes)
            {
                if (!HeroDefinitions.IsValid(em, root, row.Definition) || !heroes.Add(row.Definition))
                    throw new InvalidDataException("Invalid prepared hero definition");
                ValidateStats(row.Stats);
            }

            var buildings = new HashSet<BuildingId>();
            foreach (var row in data.PreparedBuildings)
                if (!BuildingDefinitions.IsValid(em, root, row.Definition) || !buildings.Add(row.Definition) || !CombatProfile.Valid(row.Profile))
                    throw new InvalidDataException("Invalid prepared building defense");
            foreach (var w in data.Waves)
                if (!EnemyDefinitions.IsValid(em, root, w.Definition) || w.Count < 1 || w.Count > 256 || w.WaveIndex < 0 || w.Region < 0 || w.Spawned > 2 || w.Warned > 1 || !math.isfinite(w.At) || w.At < 0 || w.At > 1 || !math.isfinite(w.PowerScale) || w.PowerScale <= 0 || !math.all(math.isfinite(w.Position)))
                    throw new InvalidDataException("Invalid night wave.");
        }

        static void ValidateStats(CombatStatsSnapshot n)
        {
            if (!math.isfinite(n.Health) || n.Health <= 0 || !math.isfinite(n.Damage) || n.Damage < 0 || !math.isfinite(n.Speed) || n.Speed < 0)
                throw new InvalidDataException("Invalid night military snapshot");
            if (!CombatProfile.Valid(n.Combat) || !math.all(math.isfinite(new float3(n.Range, n.Interval, n.ProjectileSpeed))) || n.Range <= 0 || n.Interval <= 0 || n.ProjectileSpeed < 0)
                throw new InvalidDataException("Invalid prepared combat payload");
        }
    }
}
