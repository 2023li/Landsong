using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class GarrisonOps
    {
        public static int GarrisonCount(EntityManager em, ulong id, Entity except = default)
        {
            var count = 0;
            using var all = WorldQueries.Entities<Soldier>(em);
            foreach (var e in all)
                if (e != except && em.GetComponentData<Soldier>(e).Garrison == id)
                    count++;
            return count;
        }

        public static ResultCode Assign(EntityManager em, Entity root, AssignSoldierRequest c)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day)
                return ResultCode.WrongPhase;
            var unit = WorldQueries.Find(em, c.Soldier);
            var site = WorldQueries.Find(em, c.Garrison);
            if (unit == Entity.Null || !em.HasComponent<Soldier>(unit) || !EntityState.Alive(em, unit))
                return ResultCode.InvalidTarget;
            int slot = 0;
            if (c.Garrison != 0)
            {
                if (!BuildingStatus.Operational(em, site))
                    return ResultCode.InvalidTarget;
                slot = c.Slot > 0 ? c.Slot : GarrisonOps.FreeSlot(em, site, unit);
                if (slot <= 0 || slot > em.GetComponentData<BuildingGarrisonStats>(site).Capacity || GarrisonOps.AtSlot(em, c.Garrison, slot, unit) != Entity.Null)
                    return ResultCode.NoCapacity;
            }

            em.SetComponentData(unit, GarrisonOps.Placed(em.GetComponentData<Soldier>(unit), c.Garrison, slot, em.GetComponentData<GameClock>(root).Turn));
            return ResultCode.Success;
        }

        public static ResultCode Swap(EntityManager em, SwapSoldiersRequest c)
        {
            var root = WorldQueries.Root(em);
            var turn = em.GetComponentData<Session>(root);
            GameClock turnClock = em.GetComponentData<GameClock>(root);
            if (turn.Phase != Phase.Day)
                return ResultCode.WrongPhase;
            var a = WorldQueries.Find(em, c.FirstSoldier);
            var b = WorldQueries.Find(em, c.SecondSoldier);
            if (a == b || a == Entity.Null || b == Entity.Null || !em.HasComponent<Soldier>(a) || !em.HasComponent<Soldier>(b) || !EntityState.Alive(em, a) || !EntityState.Alive(em, b))
                return ResultCode.InvalidTarget;
            var sa = em.GetComponentData<Soldier>(a);
            var sb = em.GetComponentData<Soldier>(b);
            foreach (var s in new[]
            {
                sa,
                sb
            }

            )
                if (s.Garrison != 0)
                {
                    var site = WorldQueries.Find(em, s.Garrison);
                    if (!BuildingStatus.Operational(em, site) || s.Slot < 1 || s.Slot > em.GetComponentData<BuildingGarrisonStats>(site).Capacity)
                        return ResultCode.InvalidTarget;
                }

            em.SetComponentData(a, GarrisonOps.Placed(sa, sb.Garrison, sb.Slot, turnClock.Turn));
            em.SetComponentData(b, GarrisonOps.Placed(sb, sa.Garrison, sa.Slot, turnClock.Turn));
            return ResultCode.Success;
        }

        public static void ReconcileGarrisons(EntityManager em, Entity root)
        {
            var turn = em.GetComponentData<GameClock>(root).Turn;
            using var troops = WorldQueries.OrderedEntities<Soldier>(em);
            var occupied = new System.Collections.Generic.HashSet<(ulong, int)>();
            foreach (var e in troops)
            {
                var soldier = em.GetComponentData<Soldier>(e);
                if (soldier.Garrison == 0)
                    continue;
                var site = WorldQueries.Find(em, soldier.Garrison);
                if (BuildingStatus.Operational(em, site) && soldier.Slot > 0 && soldier.Slot <= em.GetComponentData<BuildingGarrisonStats>(site).Capacity && occupied.Add((soldier.Garrison, soldier.Slot)))
                    continue;
                em.SetComponentData(e, GarrisonOps.Placed(soldier, 0, 0, turn));
            }
        }

        // Authored starting units are granted only during new-world initialization.
        // They use ordinary persistent soldiers, but do not spend recruitment costs or quota.
        public static void InitializeGarrisons(EntityManager em, Entity root)
        {
            if (em.GetComponentData<Session>(root).Initialized != 0)
                return;
            using var sites = WorldQueries.OrderedEntities<Building>(em);
            foreach (var site in sites)
            {
                var identity = em.GetComponentData<Identity>(site);
                var building = em.GetComponentData<Building>(site);
                ref var definition = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(site).Definition);
                for (int i = 0; i < definition.Capabilities.Garrison.InitialUnits.Length; i++)
                {
                    var rule = definition.Capabilities.Garrison.InitialUnits[i];
                    if ((rule.Level != 0 && rule.Level != building.Level) || rule.Count == 0)
                        continue;
                    if (rule.Count < 0 || !SoldierDefinitions.IsValid(em, root, rule.Soldier))
                        throw new InvalidOperationException("Invalid initial garrison: " + definition.Metadata.Id);
                    if (rule.Count > em.GetComponentData<BuildingGarrisonStats>(site).Capacity - GarrisonOps.GarrisonCount(em, identity.Id))
                        throw new InvalidOperationException("Initial garrison exceeds building slots: " + definition.Metadata.Id);
                    for (int n = 0; n < rule.Count; n++)
                    {
                        int slot = GarrisonOps.FreeSlot(em, site);
                        if (slot == 0)
                            throw new InvalidOperationException("Initial garrison has no operational slot: " + definition.Metadata.Id);
                        var unit = SoldierEntities.Spawn(em, root, rule.Soldier, EntityState.Position(em, site), true);
                        var id = em.GetComponentData<Identity>(unit);
                        id.Name = SoldierNaming.SoldierName(id.Id);
                        em.SetComponentData(unit, id);
                        EntityState.Set(em, unit, new Soldier { Garrison = identity.Id, Slot = slot, PopulationCost = SoldierDefinitions.Get(em, root, rule.Soldier).PopulationCost });
                        SoldierCombatants.Configure(em, root, unit, false, identity.Id, EntityState.Position(em, site));
                        SoldierLifeOps.InitializePerson(em, root, unit);
                    }
                }
            }
        }

        public static Entity AtSlot(EntityManager em, ulong home, int slot, Entity except = default)
        {
            using var units = WorldQueries.OrderedEntities<Soldier>(em);
            foreach (var e in units)
            {
                var s = em.GetComponentData<Soldier>(e);
                if (e != except && s.Garrison == home && s.Slot == slot)
                    return e;
            }

            return Entity.Null;
        }

        public static int FreeSlot(EntityManager em, Entity site, Entity except = default)
        {
            if (!BuildingStatus.Operational(em, site))
                return 0;
            ulong home = em.GetComponentData<Identity>(site).Id;
            for (int slot = 1; slot <= em.GetComponentData<BuildingGarrisonStats>(site).Capacity; slot++)
                if (GarrisonOps.AtSlot(em, home, slot, except) == Entity.Null)
                    return slot;
            return 0;
        }

        internal static Soldier Placed(Soldier s, ulong home, int slot, int turn)
        {
            if (home == 0 && s.Garrison != 0)
                s.PendingSince = turn;
            if (home != 0)
                s.PendingSince = 0;
            s.Garrison = home;
            s.Slot = home == 0 ? 0 : slot;
            return s;
        }

        public static ResultCode Fill(EntityManager em, Entity root, ulong home)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day)
                return ResultCode.WrongPhase;
            var site = WorldQueries.Find(em, home);
            if (!BuildingStatus.Operational(em, site))
                return ResultCode.InvalidTarget;
            int moved = 0;
            using var all = WorldQueries.OrderedEntities<Soldier>(em);
            foreach (var unit in all)
                if (EntityState.Alive(em, unit) && em.GetComponentData<Soldier>(unit).Garrison == 0)
                {
                    int slot = GarrisonOps.FreeSlot(em, site);
                    if (slot == 0)
                        break;
                    em.SetComponentData(unit, GarrisonOps.Placed(em.GetComponentData<Soldier>(unit), home, slot, em.GetComponentData<GameClock>(root).Turn));
                    moved++;
                }

            return moved > 0 ? ResultCode.Success : ResultCode.NoCapacity;
        }

        public static ResultCode RecallGarrison(EntityManager em, Entity root, ulong home, bool cancel)
        {
            var state = em.GetComponentData<Session>(root);
            if (state.Phase != Phase.Night)
                return ResultCode.WrongPhase;
            var site = WorldQueries.Find(em, home);
            if (!BuildingStatus.Operational(em, site) || em.GetComponentData<BuildingGarrisonStats>(site).Capacity <= 0)
                return ResultCode.InvalidTarget;
            int changed = 0, blocked = 0;
            using var all = WorldQueries.OrderedEntities<Soldier>(em);
            foreach (var unit in all)
            {
                var soldier = em.GetComponentData<Soldier>(unit);
                if (soldier.Garrison != home || !EntityState.Alive(em, unit) || soldier.RecallState == 2)
                    continue;
                if (cancel)
                {
                    if (soldier.RecallState != 1)
                        continue;
                    soldier.RecallState = 0;
                    em.SetComponentData(unit, soldier);
                    UnitOrders.Order(em, unit, default);
                    changed++;
                    continue;
                }

                if (soldier.RecallState == 1)
                    continue;
                var actor = em.GetComponentData<Combatant>(unit);
                if (actor.Deployed == 0)
                {
                    soldier.RecallState = 2;
                    actor.DeployAt = float.MaxValue;
                    em.SetComponentData(unit, soldier);
                    em.SetComponentData(unit, actor);
                    changed++;
                    continue;
                }

                using var reach = new NightSpatialOps.Reach(em, root, EntityState.Position(em, unit));
                if (reach.Building(em.GetComponentData<BuildingPlacementState>(site), out var point) == float.MaxValue)
                {
                    blocked++;
                    continue;
                }

                soldier.RecallState = 1;
                em.SetComponentData(unit, soldier);
                UnitOrders.Order(em, unit, new UnitOrder { Kind = OrderKind.Recall, Source = home, Destination = point });
                changed++;
            }

            SimulationEvents.Emit(em, root, EventKind.Message, cancel ? "已取消途中召回；已归营士兵本夜不再出勤" : blocked > 0 ? "部分士兵无可达回营路径，仍留在战场" : "驻军正在撤回；途中仍会受击", home);
            return changed > 0 ? ResultCode.Success : ResultCode.Unavailable;
        }
    }
}
