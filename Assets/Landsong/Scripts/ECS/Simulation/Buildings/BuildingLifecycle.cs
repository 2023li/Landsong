using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class BuildingLifecycle
    {
        public static void CommitRuin(EntityManager em, Entity root, Entity e)
        {
            var b = em.GetComponentData<Building>(e);
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
            BuildingHousingState bHousing = em.GetComponentData<BuildingHousingState>(e);
            if (b.RuinPending == 0)
                return;
            var id = em.GetComponentData<Identity>(e);
            b.RuinPending = 0;
            bWorkforce.Workers = 0;
            bHousing.Population = 0;
            bHousing.DeferredResidents = 0;
            {
                em.SetComponentData(e, b);
                em.SetComponentData(e, bWorkforce);
                em.SetComponentData(e, bHousing);
            }

            InventoryProviders.Remove(em, root, id.Id);
            ExpeditionOps.WithdrawFromSite(em, root, id.Id);
            using var troops = WorldQueries.Entities<Soldier>(em);
            foreach (var troop in troops)
            {
                var s = em.GetComponentData<Soldier>(troop);
                if (s.Garrison != id.Id)
                    continue;
                s.Garrison = 0;
                s.Slot = 0;
                s.PendingSince = em.GetComponentData<GameClock>(root).Turn;
                em.SetComponentData(troop, s);
            }
        }

        public static void DawnBuildings(EntityManager em, Entity root)
        {
            using var buildings = WorldQueries.Entities<Building>(em);
            foreach (var e in buildings)
                BuildingLifecycle.CommitRuin(em, root, e);
            foreach (var e in buildings)
            {
                var b = em.GetComponentData<Building>(e);
                BuildingHousingState bHousing = em.GetComponentData<BuildingHousingState>(e);
                if (bHousing.DeferredResidents > 0 && b.Stage == LifeStage.Operational)
                {
                    bHousing.Population = math.min(bHousing.DeferredResidents, em.GetComponentData<BuildingHousingStats>(e).MaxPopulation);
                    bHousing.DeferredResidents = 0;
                    {
                        em.SetComponentData(e, b);
                        em.SetComponentData(e, bHousing);
                    }
                }

                if (em.GetComponentData<BuildingHousingStats>(e).IsCore != 0 && EntityState.Alive(em, e))
                {
                    var h = em.GetComponentData<Health>(e);
                    h.Current = h.Maximum;
                    em.SetComponentData(e, h);
                }
            }

            WorkforceSettlement.ReconcilePopulation(em, root);
        }

        public static void Ruin(EntityManager em, Entity root, Entity e)
        {
            if (!em.Exists(e) || !em.HasComponent<Building>(e))
                return;
            var b = em.GetComponentData<Building>(e);
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
            BuildingHousingState bHousing = em.GetComponentData<BuildingHousingState>(e);
            BuildingSanctumState bSanctum = em.GetComponentData<BuildingSanctumState>(e);
            if (b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing)
                return;
            var id = em.GetComponentData<Identity>(e);
            var stats = em.GetComponentData<BuildingHousingStats>(e);
            if (TerrainConnectionOps.TryGet(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition, out _) || (BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition).PlacementAndVisuals.Category & BuildingCategory.Road) != 0)
            {
                b.Stage = LifeStage.Ruined;
                b.RuinPending = 0;
                {
                    em.SetComponentData(e, b);
                    em.SetComponentData(e, bWorkforce);
                    em.SetComponentData(e, bHousing);
                    em.SetComponentData(e, bSanctum);
                }

                GridOps.Occupy(em, root, e);
                return;
            }

            var state = em.GetComponentData<Session>(root);
            SimulationControl stateControl = em.GetComponentData<SimulationControl>(root);
            if (stats.IsCore != 0)
            {
                state.Phase = Phase.GameOver;
                stateControl.Paused = 0;
                {
                    em.SetComponentData(root, state);
                    em.SetComponentData(root, stateControl);
                }

                SimulationEvents.Emit(em, root, EventKind.Message, "聚落核心失守", category: HistoryCategory.Important);
                return;
            }

            b.Stage = LifeStage.Ruined;
            b.RuinPending = 1;
            bSanctum.Offering = 0;
            bWorkforce.PaidSubsidy = 0;
            bWorkforce.PaidSubsidyTurn = 0;
            {
                em.SetComponentData(e, b);
                em.SetComponentData(e, bWorkforce);
                em.SetComponentData(e, bHousing);
                em.SetComponentData(e, bSanctum);
            }

            GridOps.Occupy(em, root, e);
            // Record the committed-to-be losses now so Tonight's Report can explain them before dawn.
            var report = em.GetBuffer<BattleReportEntry>(root);
            if (bHousing.Population > 0)
                report.Add(new BattleReportEntry { Kind = EventKind.ResidentsLost, Id = id.Id, Building = em.GetComponentData<BuildingDefinitionRef>(e).Definition, Amount = bHousing.Population, SourceName = id.Name });
            if (bWorkforce.Workers > 0)
                report.Add(new BattleReportEntry { Kind = EventKind.JobsLost, Id = id.Id, Building = em.GetComponentData<BuildingDefinitionRef>(e).Definition, Amount = bWorkforce.Workers, SourceName = id.Name });
            var slots = em.GetBuffer<InventorySlot>(root);
            for (var i = 0; i < slots.Length; i++)
                if (slots[i].Provider == id.Id)
                {
                    var slot = slots[i];
                    slot.Unavailable = 1;
                    slots[i] = slot;
                    if (slot.Count > 0)
                        report.Add(new BattleReportEntry { Kind = EventKind.InventoryLost, Id = id.Id, Item = slot.Item, Amount = slot.Count, SourceName = id.Name });
                }

            using (var soldiers = WorldQueries.Entities<Soldier>(em))
                foreach (var soldier in soldiers)
                {
                    var s = em.GetComponentData<Soldier>(soldier);
                    if (s.Garrison != id.Id)
                        continue;
                    if (em.HasComponent<Combatant>(soldier) && em.GetComponentData<Combatant>(soldier).Deployed == 0)
                    {
                        var health = em.GetComponentData<Health>(soldier);
                        health.Current = math.min(health.Current, health.Maximum * .5f);
                        em.SetComponentData(soldier, health);
                    }
                }

            using (var heroes = WorldQueries.Entities<Hero>(em))
                foreach (var hero in heroes)
                    if (em.GetComponentData<Hero>(hero).Sanctum == id.Id)
                        HeroOps.KillHero(em, root, hero);
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.Ruin, Id = id.Id, Building = em.GetComponentData<BuildingDefinitionRef>(e).Definition, Amount = 1, SourceName = id.Name });
            SimulationEvents.Emit(em, root, EventKind.Ruin, id.Name, id.Id);
            if (state.Phase == Phase.Day || state.Phase == Phase.Settlement)
                BuildingLifecycle.CommitRuin(em, root, e);
        }

        public static ResultCode Demolish(EntityManager em, Entity root, Entity e)
        {
            if (e == Entity.Null || !em.HasComponent<Building>(e))
                return ResultCode.InvalidTarget;
            if (em.GetComponentData<BuildingHousingStats>(e).IsCore != 0)
                return ResultCode.Unavailable;
            var refunds = BuildingCostOps.DemolitionRefund(em, root, e);
            BuildingLifecycle.Ruin(em, root, e);
            BuildingLifecycle.CommitRuin(em, root, e);
            GridOps.Occupy(em, root, e, true);
            var id = em.GetComponentData<Identity>(e).Id;
            using (var expeditions = WorldQueries.Entities<Expedition>(em))
                foreach (var expedition in expeditions)
                    if (em.GetComponentData<Expedition>(expedition).Site == id && em.GetComponentData<Expedition>(expedition).Status == ExpeditionStatus.Travelling)
                        em.DestroyEntity(expedition);
            using (var quests = WorldQueries.Entities<Quest>(em))
                foreach (var quest in quests)
                {
                    var q = em.GetComponentData<Quest>(quest);
                    if (q.Source == id && q.Status == QuestStatus.Offered)
                        em.DestroyEntity(quest);
                }

            em.DestroyEntity(e);
            foreach (var refund in refunds)
                InventoryOps.Add(em, root, refund.Item, refund.Amount);
            WorkforceSettlement.ReconcilePopulation(em, root);
            BuildingChangeNotifications.Publish(em, root);
            QuestLifecycle.ReconcileQuestContainers(em, root);
            return ResultCode.Success;
        }

        public static ResultCode Repair(EntityManager em, Entity root, Entity e)
        {
            if (e == Entity.Null || !em.HasComponent<Building>(e))
                return ResultCode.InvalidTarget;
            var b = em.GetComponentData<Building>(e);
            BuildingConstructionState bConstruction = em.GetComponentData<BuildingConstructionState>(e);
            if (b.Stage != LifeStage.Ruined)
                return ResultCode.Unavailable;
            if (b.RuinPending != 0)
                return ResultCode.Busy;
            var costs = BuildingCostOps.RepairTotal(em, root, e, out var duration);
            var materials = em.GetBuffer<RepairMaterial>(e);
            materials.Clear();
            foreach (var c in costs)
                materials.Add(new RepairMaterial { Item = c.Item, Amount = c.Amount });
            b.Stage = LifeStage.Repairing;
            bConstruction.Progress = 0;
            bConstruction.RepairDuration = duration;
            {
                em.SetComponentData(e, b);
                em.SetComponentData(e, bConstruction);
            }

            GridOps.Occupy(em, root, e);
            return ResultCode.Success;
        }
    }
}
