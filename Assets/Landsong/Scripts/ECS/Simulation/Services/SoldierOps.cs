using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class SoldierOps
    {
        public static int UnassignedCount(EntityManager em)
        {
            var count = 0;
            using var all = WorldQueries.Entities<Soldier>(em);
            foreach (var e in all)
                if (em.GetComponentData<Soldier>(e).Garrison == 0 && EntityState.Alive(em, e))
                    count++;
            return count;
        }

        public static CombatStatsSnapshot SoldierStats(EntityManager em, Entity root, Entity unit)
        {
            var d = em.GetComponentData<SoldierDefinitionRef>(unit).Definition;
            var result = SoldierCombatStats.ForNight(em, root, d);
            var soldier = em.GetComponentData<Soldier>(unit);
            UnitProgression.ApplyGrowth(ref result, SoldierDefinitions.Get(em, root, d).Growth, soldier.Experience);
            SoldierCombatStats.ApplyWeapon(ref result, soldier.Weapon);
            return result;
        }

        public static ResultCode EquipWeapon(EntityManager em, Entity root, EquipSoldierWeaponRequest request)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day)
                return ResultCode.WrongPhase;
            if (request.Weapon > SoldierWeaponKind.Bow)
                return ResultCode.InvalidContent;
            var unit = WorldQueries.Find(em, request.Soldier);
            if (unit == Entity.Null || !em.HasComponent<Soldier>(unit) || !EntityState.Alive(em, unit))
                return ResultCode.InvalidTarget;
            var soldier = em.GetComponentData<Soldier>(unit);
            soldier.Weapon = request.Weapon;
            em.SetComponentData(unit, soldier);
            return ResultCode.Success;
        }

        public static SoldierRecruitQuote RecruitQuote(EntityManager em, Entity root, ulong home, SoldierId definition, int quantity, bool pending = false)
        {
            var q = new SoldierRecruitQuote
            {
                Quantity = quantity,
                Code = ResultCode.Success
            };
            var site = WorldQueries.Find(em, home);
            var session = em.GetComponentData<Session>(root);
            GameClock sessionClock = em.GetComponentData<GameClock>(root);
            if (session.Phase != Phase.Day)
            {
                q.Code = ResultCode.WrongPhase;
                return q;
            }

            if (!BuildingStatus.Operational(em, site) || em.GetComponentData<BuildingGarrisonStats>(site).Capacity <= 0)
            {
                q.Code = ResultCode.InvalidTarget;
                return q;
            }

            if (!SoldierDefinitions.IsValid(em, root, definition) || quantity < 1 || quantity > 1000)
            {
                q.Code = ResultCode.InvalidContent;
                return q;
            }

            ref var d = ref SoldierDefinitions.Get(em, root, definition);
            BuildingRecruitmentState bRecruitment = em.GetComponentData<BuildingRecruitmentState>(site);
            int capacity = em.GetComponentData<BuildingGarrisonStats>(site).Capacity;
            ref var policy = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(site).Definition).Capabilities.Garrison;
            q.EmptySlots = math.max(0, capacity - GarrisonOps.GarrisonCount(em, home));
            q.RemainingLimit = math.max(0, (policy.RecruitmentLimitPerTurn > 0 ? policy.RecruitmentLimitPerTurn : capacity) - (bRecruitment.Turn == sessionClock.Turn ? bRecruitment.Count : 0));
            q.FreePopulation = math.max(0, PopulationOps.Population(em, root) - PopulationOps.Employed(em));
            bool explicitCosts = false;
            try
            {
                for (int i = 0; i < d.RecruitmentCosts.Length; i++)
                {
                    var cost = d.RecruitmentCosts[i];
                    explicitCosts = true;
                    BuildingCostOps.Add(q.Costs, cost.Item, checked(cost.Quantity * quantity));
                }

                if (!explicitCosts)
                    BuildingCostOps.Add(q.Costs, em.GetComponentData<CurrencySettings>(root).Gold, checked(d.FallbackRecruitGold * quantity));
            }
            catch (OverflowException)
            {
                q.Code = ResultCode.InvalidContent;
                return q;
            }

            if (!pending && quantity > q.EmptySlots)
                q.Code = ResultCode.NoCapacity;
            else if (quantity > q.RemainingLimit)
                q.Code = ResultCode.Unavailable;
            else if ((long)d.PopulationCost * quantity > q.FreePopulation)
                q.Code = ResultCode.InsufficientPopulation;
            else if (!BuildingCostOps.CanPay(em, root, q.Costs))
                q.Code = ResultCode.InsufficientResources;
            else
            {
                bool prefab = false;
                foreach (var p in em.GetBuffer<SoldierPrefab>(root))
                    if (p.Definition == definition && p.Prefab != Entity.Null && em.Exists(p.Prefab))
                        prefab = true;
                if (!prefab)
                    q.Code = ResultCode.InvalidContent;
            }

            return q;
        }

        // Whole quantity succeeds or fails. Fault probe is used only by isolated verification.
        public static ResultCode RecruitSoldiers(EntityManager em, Entity root, RecruitSoldiersRequest c, Action<int> probe = null)
        {
            int quantity = c.Quantity == 0 ? 1 : c.Quantity;
            bool pending = c.Pending;
            var q = SoldierOps.RecruitQuote(em, root, c.Garrison, c.Soldier, quantity, pending);
            if (q.Code != ResultCode.Success)
                return q.Code;
            var site = WorldQueries.Find(em, c.Garrison);
            BuildingRecruitmentState beforeRecruitment = em.GetComponentData<BuildingRecruitmentState>(site);
            GameClock sessionClock = em.GetComponentData<GameClock>(root);
            var beforeIds = em.GetComponentData<IdentitySequence>(root);
            using var resources = new InventoryTransaction(em, root);
            var created = new List<Entity>();
            try
            {
                for (int i = 0; i < quantity; i++)
                {
                    int slot = pending ? 0 : GarrisonOps.FreeSlot(em, site);
                    if (!pending && slot == 0)
                        throw new InvalidOperationException("驻军槽发生变化");
                    var unit = SoldierEntities.Spawn(em, root, c.Soldier, EntityState.Position(em, site), true);
                    created.Add(unit);
                    var id = em.GetComponentData<Identity>(unit);
                    id.Name = SoldierNaming.SoldierName(id.Id);
                    em.SetComponentData(unit, id);
                    EntityState.Set(em, unit, new Soldier { Garrison = pending ? 0 : c.Garrison, Slot = slot, PendingSince = pending ? sessionClock.Turn : 0, PopulationCost = SoldierDefinitions.Get(em, root, c.Soldier).PopulationCost });
                    SoldierCombatants.Configure(em, root, unit, false, c.Garrison, EntityState.Position(em, site));
                    SoldierLifeOps.InitializePerson(em, root, unit);
                    probe?.Invoke(i);
                }

                if (!BuildingCostOps.Pay(em, root, q.Costs))
                    throw new InvalidOperationException("招募资源发生变化");
                BuildingRecruitmentState bRecruitment = beforeRecruitment;
                bRecruitment.Count = (bRecruitment.Turn == sessionClock.Turn ? bRecruitment.Count : 0) + quantity;
                bRecruitment.Turn = sessionClock.Turn;
                {
                    em.SetComponentData(site, bRecruitment);
                }

                probe?.Invoke(quantity);
                resources.Commit();
                return ResultCode.Success;
            }
            catch (Exception)
            {
                foreach (var e in created)
                    if (em.Exists(e))
                        em.DestroyEntity(e);
                em.SetComponentData(root, beforeIds);
                em.SetComponentData(site, beforeRecruitment);
                return ResultCode.PreparationFailed;
            }
        }

        public static ResultCode SetAttention(EntityManager em, Entity root, SetSoldierAttentionRequest request)
        {
            if (em.GetComponentData<SimulationControl>(root).Paused != 0 || em.GetComponentData<PersistenceGate>(root).CheckpointPending != 0 || em.GetComponentData<IntelligenceModeState>(root).Enabled != 0)
                return ResultCode.Busy;
            var phase = em.GetComponentData<Session>(root).Phase;
            if (phase == Phase.GameOver || phase == Phase.Ended)
                return ResultCode.WrongPhase;
            var unit = WorldQueries.Find(em, request.Soldier);
            if (unit == Entity.Null || !em.HasComponent<Soldier>(unit) || !em.HasComponent<SoldierPerson>(unit) || !EntityState.Alive(em, unit))
                return ResultCode.InvalidTarget;
            var person = em.GetComponentData<SoldierPerson>(unit);
            person.SpecialAttention = (byte)(request.Watched ? 1 : 0);
            em.SetComponentData(unit, person);
            return ResultCode.Success;
        }

        public static ResultCode Rename(EntityManager em, Entity root, RenameSoldierRequest request)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day)
                return ResultCode.WrongPhase;
            var unit = WorldQueries.Find(em, request.Soldier);
            if (unit == Entity.Null || !em.HasComponent<Soldier>(unit) || !EntityState.Alive(em, unit))
                return ResultCode.InvalidTarget;
            var name = BuildingNaming.SanitizeName(request.Name.ToString());
            if (string.IsNullOrWhiteSpace(name))
                return ResultCode.InvalidContent;
            var identity = em.GetComponentData<Identity>(unit);
            identity.Name = new FixedString128Bytes(name);
            em.SetComponentData(unit, identity);
            return ResultCode.Success;
        }

        public static ResultCode Dismiss(EntityManager em, Entity root, DismissSoldierRequest request)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day)
                return ResultCode.WrongPhase;
            var unit = WorldQueries.Find(em, request.Soldier);
            if (unit == Entity.Null || !em.HasComponent<Soldier>(unit) || !EntityState.Alive(em, unit))
                return ResultCode.InvalidTarget;
            if (!request.Confirmed)
                return ResultCode.ConfirmationRequired;
            em.DestroyEntity(unit);
            return ResultCode.Success;
        }

        public static int SoldierBattleExperience(EntityManager em, Entity root, Entity unit)
        {
            GameClock sClock = em.GetComponentData<GameClock>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            var soldier = em.GetComponentData<Soldier>(unit);
            if (!EntityState.Alive(em, unit) || sNight.Kind == NightKind.Peaceful || soldier.LastExperienceTurn == sClock.Turn || em.GetComponentData<Combatant>(unit).Participated == 0)
                return 0;
            var growth = SoldierDefinitions.Get(em, root, em.GetComponentData<SoldierDefinitionRef>(unit).Definition).Growth;
            return math.min(growth.BattleExperience, math.max(0, UnitProgression.LevelThreshold(growth, growth.MaxLevel) - soldier.Experience));
        }

        public static void ReportSoldierExperience(EntityManager em, Entity root)
        {
            using var all = WorldQueries.OrderedEntities<Soldier>(em);
            foreach (var e in all)
            {
                int amount = SoldierOps.SoldierBattleExperience(em, root, e);
                if (amount <= 0)
                    continue;
                var id = em.GetComponentData<Identity>(e);
                bool exists = false;
                foreach (var entry in em.GetBuffer<BattleReportEntry>(root))
                    if (entry.Kind == EventKind.SoldierExperience && entry.Id == id.Id)
                        exists = true;
                if (!exists)
                    em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.SoldierExperience, Id = id.Id, Amount = amount, SourceName = id.Name });
            }
        }
    }
}
