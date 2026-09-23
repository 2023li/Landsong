using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class LightningOps
    {
        public static void Tick(EntityManager em, Entity root, float delta)
        {
            var visuals = em.GetBuffer<LightningVisualEvent>(root);
            visuals.Clear();
            if (em.GetComponentData<Session>(root).Phase != Phase.Day
                || em.GetComponentData<SimulationControl>(root).Paused != 0
                || em.GetComponentData<PersistenceGate>(root).CheckpointPending != 0)
                return;
            var state = em.GetComponentData<SeasonWeatherState>(root);
            if (state.Weather != WeatherKind.Rain)
                return;
            state.DayElapsed += math.max(0, delta);
            var random = new Random(math.max(1u, state.RandomState));
            while (state.NextThunderAt > 0 && state.DayElapsed >= state.NextThunderAt)
            {
                Attempt(em, root, ref state, ref random);
                state.NextThunderAt += random.NextFloat(60, 120);
            }
            state.RandomState = random.state;
            em.SetComponentData(root, state);
        }

        static void Attempt(EntityManager em, Entity root, ref SeasonWeatherState state, ref Random random)
        {
            if (state.LightningCount >= state.LightningLimit || random.NextInt(100) < 70)
            {
                Flash(em, root);
                return;
            }
            var viewport = em.GetComponentData<LightningViewport>(root);
            var grounds = VisibleGround(em, root, viewport);
            if (grounds.Count == 0)
            {
                Flash(em, root);
                return;
            }
            if (random.NextBool())
            {
                StrikeGround(em, root, grounds[random.NextInt(grounds.Count)]);
                state.LightningCount++;
                return;
            }
            var buildings = VisibleBuildings(em, viewport);
            var units = VisibleUnits(em, viewport);
            var buildingFirst = random.NextBool();
            if (buildingFirst && buildings.Count > 0 || units.Count == 0 && buildings.Count > 0)
            {
                var building = buildings[random.NextInt(buildings.Count)];
                state.LightningCount++;
                if (BuildingFireOps.Ignite(em, root, building))
                    em.GetBuffer<LightningVisualEvent>(root).Add(new LightningVisualEvent { Kind = LightningVisualKind.Building, Position = EntityState.Position(em, building), Target = em.GetComponentData<Identity>(building).Id });
                return;
            }
            if (units.Count > 0)
            {
                var unit = units[random.NextInt(units.Count)];
                var position = EntityState.Position(em, unit);
                var id = em.GetComponentData<Identity>(unit).Id;
                state.LightningCount++;
                StrikeUnit(em, root, unit, ref random);
                em.GetBuffer<LightningVisualEvent>(root).Add(new LightningVisualEvent { Kind = LightningVisualKind.Unit, Position = position, Target = id });
                return;
            }
            StrikeGround(em, root, grounds[random.NextInt(grounds.Count)]);
            state.LightningCount++;
        }

        static void StrikeUnit(EntityManager em, Entity root, Entity unit, ref Random random)
        {
            var id = em.GetComponentData<Identity>(unit);
            if (random.NextInt(100) == 0)
            {
                if (em.HasComponent<Health>(unit))
                {
                    var health = em.GetComponentData<Health>(unit);
                    health.Current = math.min(health.Maximum, 1);
                    em.SetComponentData(unit, health);
                }
                var temple = em.GetComponentData<FireSettings>(root).Temple;
                if (temple.IsValid && !BuildingBlueprints.Has(em, root, temple))
                {
                    BuildingBlueprints.Grant(em, root, temple, 1);
                    SimulationEvents.Emit(em, root, EventKind.Message, "行人幸存并获得雷神殿蓝图", id.Id, category: HistoryCategory.Important);
                }
                else
                {
                    var gold = em.GetComponentData<CurrencySettings>(root).Gold;
                    InventoryOps.Add(em, root, gold, 1000, pending: true);
                    SimulationEvents.Emit(em, root, EventKind.Message, "行人幸存，获得 1000 金币", id.Id, category: HistoryCategory.Important);
                }
                return;
            }
            if (em.HasComponent<TransportWorker>(unit))
                TransportWorkerOps.Kill(em, root, unit);
            else if (em.HasComponent<Firefighter>(unit))
                FirefighterOps.Kill(em, root, unit);
            else if (em.HasComponent<Hero>(unit))
                HeroOps.KillHero(em, root, unit);
            else if (em.HasComponent<Soldier>(unit))
            {
                var health = em.GetComponentData<Health>(unit);
                health.Current = 0;
                em.SetComponentData(unit, health);
                CombatOps.Death(em, root, unit, Entity.Null);
            }
            else if (em.HasComponent<Opportunity>(unit))
                em.DestroyEntity(unit);
            SimulationEvents.Emit(em, root, EventKind.Death, "行人遭雷击死亡", id.Id, category: HistoryCategory.Important);
        }

        static void Flash(EntityManager em, Entity root)
            => em.GetBuffer<LightningVisualEvent>(root).Add(new LightningVisualEvent { Kind = LightningVisualKind.Flash });

        static void StrikeGround(EntityManager em, Entity root, float3 position)
            => em.GetBuffer<LightningVisualEvent>(root).Add(new LightningVisualEvent { Kind = LightningVisualKind.Ground, Position = position });

        static bool Visible(LightningViewport viewport, float3 point)
        {
            if (viewport.Available == 0)
                return false;
            var distance = math.dot(point - viewport.CameraPosition, viewport.Forward);
            if (distance < viewport.Near || distance > viewport.Far)
                return false;
            var clip = math.mul(viewport.ViewProjection, new float4(point, 1));
            return clip.w > 0 && math.abs(clip.x) <= clip.w && math.abs(clip.y) <= clip.w;
        }

        static List<float3> VisibleGround(EntityManager em, Entity root, LightningViewport viewport)
        {
            var result = new List<float3>();
            if (viewport.Available == 0)
                return result;
            var grid = em.GetComponentData<GridData>(root);
            for (var i = 0; i < grid.Value.Value.Cells.Length; i++)
            {
                if (grid.Value.Value.Cells[i].Exists == 0)
                    continue;
                var cell = grid.Value.Value.Min + new int2(i % grid.Value.Value.Size.x, i / grid.Value.Value.Size.x);
                var point = GridOps.Position(grid, cell, new int2(1));
                if (Visible(viewport, point))
                    result.Add(point);
            }
            return result;
        }

        static List<Entity> VisibleBuildings(EntityManager em, LightningViewport viewport)
        {
            var result = new List<Entity>();
            using var buildings = WorldQueries.Entities<Building>(em);
            foreach (var building in buildings)
                if (BuildingStatus.Operational(em, building) && em.GetComponentData<BuildingHousingStats>(building).IsCore == 0
                    && Visible(viewport, EntityState.Position(em, building)))
                    result.Add(building);
            result.Sort((a, b) => em.GetComponentData<Identity>(a).Id.CompareTo(em.GetComponentData<Identity>(b).Id));
            return result;
        }

        static List<Entity> VisibleUnits(EntityManager em, LightningViewport viewport)
        {
            var result = new List<Entity>();
            using (var actors = WorldQueries.Entities<Combatant>(em))
                foreach (var actor in actors)
                {
                    var combat = em.GetComponentData<Combatant>(actor);
                    if (combat.Faction != 0 || combat.Deployed == 0 || !EntityState.Alive(em, actor)
                        || !(em.HasComponent<TransportWorker>(actor) || em.HasComponent<Firefighter>(actor)
                            || em.HasComponent<Soldier>(actor) || em.HasComponent<Hero>(actor))
                        || em.HasComponent<VisualState>(actor) && em.GetComponentData<VisualState>(actor).Visible == 0
                        || !Visible(viewport, EntityState.Position(em, actor)))
                        continue;
                    result.Add(actor);
                }
            using (var visitors = WorldQueries.Entities<Opportunity>(em))
                foreach (var visitor in visitors)
                    if (em.HasComponent<Health>(visitor) && EntityState.Alive(em, visitor)
                        && em.HasComponent<VisualState>(visitor) && em.GetComponentData<VisualState>(visitor).Visible != 0
                        && Visible(viewport, EntityState.Position(em, visitor)))
                        result.Add(visitor);
            result.Sort((a, b) => em.GetComponentData<Identity>(a).Id.CompareTo(em.GetComponentData<Identity>(b).Id));
            return result;
        }
    }
}
