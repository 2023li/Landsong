using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class HeroOps
    {
        public static int HeroLevel(EntityManager em, Entity root, Entity unit) => UnitProgression.Level(HeroDefinitions.Get(em, root, em.GetComponentData<HeroDefinitionRef>(unit).Definition).Growth.Progression, em.GetComponentData<Hero>(unit).Experience);
        public static int AddHeroExperience(HeroGrowth growth, int current, int amount) => (int)math.min((long)UnitProgression.LevelThreshold(growth.Progression, growth.Progression.MaxLevel), (long)current + math.max(0, amount));
        // Call only AFTER an opposing effect, effective heal, shield absorption, buff/debuff or control
        // actually takes effect. Attempts, overheal and merely casting an ability do not qualify.
        public static void RecordHeroContribution(EntityManager em, Entity root, Entity unit, float effectiveAmount)
        {
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            if (!math.isfinite(effectiveAmount) || effectiveAmount <= 0 || sControl.Paused != 0 || sNight.Kind == NightKind.Peaceful || (s.Phase != Phase.Night) || !EntityState.Alive(em, unit) || !em.HasComponent<HeroCombat>(unit) || em.GetComponentData<Combatant>(unit).Deployed == 0)
                return;
            var h = em.GetComponentData<HeroCombat>(unit);
            if (h.Turn != sClock.Turn)
                h = new HeroCombat
                {
                    Turn = sClock.Turn,
                    CountedUntil = sClock.Time
                };
            var config = HeroDefinitions.Get(em, root, em.GetComponentData<HeroDefinitionRef>(unit).Definition).Growth;
            if (h.ContactUntil < sClock.Time)
                h.CountedUntil = sClock.Time;
            h.ContactUntil = sClock.Time + config.ContactSeconds;
            em.SetComponentData(unit, h);
        }

        public static void TickHeroExperience(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            if (sControl.Paused != 0 || sNight.Kind == NightKind.Peaceful || (s.Phase != Phase.Night))
                return;
            using var heroes = WorldQueries.Entities<HeroCombat>(em);
            foreach (var e in heroes)
            {
                if (!EntityState.Alive(em, e) || em.GetComponentData<Combatant>(e).Deployed == 0)
                    continue;
                var h = em.GetComponentData<HeroCombat>(e);
                if (h.Turn != sClock.Turn)
                    continue;
                float end = math.min(sClock.Time, h.ContactUntil);
                h.EffectiveSeconds += math.max(0, end - h.CountedUntil);
                h.CountedUntil = math.max(h.CountedUntil, end);
                em.SetComponentData(e, h);
            }
        }

        public static int HeroBattleExperience(EntityManager em, Entity root, Entity unit)
        {
            GameClock sClock = em.GetComponentData<GameClock>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            var hero = em.GetComponentData<Hero>(unit);
            if (sNight.Kind == NightKind.Peaceful || !EntityState.Alive(em, unit) || hero.Recruited == 0 || hero.LastCombatTurn == sClock.Turn || !em.HasComponent<HeroCombat>(unit))
                return 0;
            var contact = em.GetComponentData<HeroCombat>(unit);
            if (contact.Turn != sClock.Turn || contact.EffectiveSeconds <= 0)
                return 0;
            var g = HeroDefinitions.Get(em, root, em.GetComponentData<HeroDefinitionRef>(unit).Definition).Growth;
            float threat = math.clamp(math.sqrt(math.max(0, NightPlanOps.State(em, root).BaseThreat) / g.ThreatReference), 1, g.MaximumThreatMultiplier);
            int award = (int)math.min(1000000f, math.ceil(contact.EffectiveSeconds * g.ExperiencePerSecond * threat));
            return HeroOps.AddHeroExperience(g, hero.Experience, award) - hero.Experience;
        }

        public static void ReportHeroExperience(EntityManager em, Entity root)
        {
            using var heroes = WorldQueries.OrderedEntities<Hero>(em);
            foreach (var e in heroes)
            {
                int amount = HeroOps.HeroBattleExperience(em, root, e);
                if (amount <= 0)
                    continue;
                var id = em.GetComponentData<Identity>(e);
                var report = em.GetBuffer<BattleReportEntry>(root);
                bool found = false;
                for (int i = 0; i < report.Length; i++)
                    if (report[i].Kind == EventKind.HeroExperience && report[i].Id == id.Id)
                    {
                        var entry = report[i];
                        entry.Amount = amount;
                        report[i] = entry;
                        found = true;
                        break;
                    }

                if (!found)
                    report.Add(new BattleReportEntry { Kind = EventKind.HeroExperience, Id = id.Id, Amount = amount, SourceName = id.Name });
            }
        }

        public static List<BuildingCost> AwakeningCosts(EntityManager em, Entity root, HeroId hero)
        {
            var result = new List<BuildingCost>();
            ref var definition = ref HeroDefinitions.Get(em, root, hero);
            for (int i = 0; i < definition.AwakeningCosts.Length; i++)
            {
                var cost = definition.AwakeningCosts[i];
                if (cost.Level == 0 || cost.Level == 1)
                    BuildingCostOps.Add(result, cost.Item, cost.Quantity);
            }

            return result;
        }

        public static string HeroAvailability(EntityManager em, Entity root, Entity site, bool wake)
        {
            if (!BuildingStatus.Operational(em, site))
                return "神殿未正常运作";
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
            PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(site);
            BuildingSanctumState bSanctum = em.GetComponentData<BuildingSanctumState>(site);
            BuildingSanctumStats statsSanctum = em.GetComponentData<BuildingSanctumStats>(site);
            if (!HeroDefinitions.IsValid(em, root, statsSanctum.Hero))
                return "未配置英雄";
            if (sControl.Paused != 0 || sPersistence.CheckpointPending != 0)
                return "暂停或节点处理中";
            if (wake ? s.Phase != Phase.Night : s.Phase != Phase.Day)
                return wake ? "只可在夜晚唤醒" : "只可在白天招募";
            if (bWorkforce.Workers < statsSanctum.RequiredWorkers)
                return $"工人不足：{bWorkforce.Workers}/{statsSanctum.RequiredWorkers}";
            Entity hero = Entity.Null;
            using var heroes = WorldQueries.OrderedEntities<Hero>(em);
            foreach (var e in heroes)
                if (em.GetComponentData<HeroDefinitionRef>(e).Definition == statsSanctum.Hero)
                {
                    hero = e;
                    break;
                }

            var h = hero == Entity.Null ? default : em.GetComponentData<Hero>(hero);
            if (h.DeathPending != 0)
                return "已阵亡；黎明开始完整冷却";
            if (h.CooldownUntil > sClock.Turn)
                return $"重招还需 {h.CooldownUntil - sClock.Turn} 回合";
            ref var d = ref HeroDefinitions.Get(em, root, statsSanctum.Hero);
            if (!wake)
            {
                if (h.Recruited != 0)
                    return "已招募";
                if (PopulationOps.Population(em, root) - PopulationOps.Employed(em) < d.PopulationCost)
                    return "招募人口不足";
                if (InventoryOps.Count(em, root, em.GetComponentData<CurrencySettings>(root).Gold) < d.FallbackWakeGold)
                    return "招募金币不足";
            }
            else
            {
                if (h.Recruited == 0 || h.Sanctum != em.GetComponentData<Identity>(site).Id)
                    return "尚未招募英雄";
                if (bSanctum.WokenTurn == sClock.Turn)
                    return "本夜已出场";
                if (bSanctum.PaidOfferingTurn != sClock.Turn)
                    return "本回合未完成供奉";
                if (!HeroOps.HeroEntrance(em, root, site, out _))
                    return "没有可用出场位置";
                foreach (var cost in AwakeningCosts(em, root, statsSanctum.Hero))
                    if (InventoryOps.Count(em, root, cost.Item) < cost.Amount)
                        return "唤醒资源不足";
            }

            return "";
        }

        public static bool HeroEntrance(EntityManager em, Entity root, Entity site, out float3 position)
        {
            BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(site);
            var grid = em.GetComponentData<GridData>(root);
            var occupied = em.GetBuffer<Occupancy>(root);
            for (int y = -1; y <= bPlacement.Size.y; y++)
                for (int x = -1; x <= bPlacement.Size.x; x++)
                {
                    if (x >= 0 && y >= 0 && x < bPlacement.Size.x && y < bPlacement.Size.y)
                        continue;
                    var cell = bPlacement.Cell + new int2(x, y);
                    if (!GridOps.Traversable(grid, occupied, cell))
                        continue;
                    position = GridOps.Position(grid, cell, new int2(1)) + new float3(0, .5f, 0);
                    return true;
                }

            position = default;
            return false;
        }

        public static ResultCode Recruit(EntityManager em, Entity root, RecruitHeroRequest c, System.Action<string> probe = null)
        {
            var site = WorldQueries.Find(em, c.Sanctum);
            if (!BuildingStatus.Operational(em, site))
                return ResultCode.InvalidTarget;
            BuildingSanctumStats statsSanctum = em.GetComponentData<BuildingSanctumStats>(site);
            var index = statsSanctum.Hero;
            if (!HeroDefinitions.IsValid(em, root, index))
                return ResultCode.InvalidContent;
            ref var d = ref HeroDefinitions.Get(em, root, index);
            if (em.GetComponentData<BuildingWorkforceState>(site).Workers < statsSanctum.RequiredWorkers)
                return ResultCode.InsufficientPopulation;
            if (PopulationOps.Population(em, root) - PopulationOps.Employed(em) < d.PopulationCost)
                return ResultCode.InsufficientPopulation;
            var existingHero = Entity.Null;
            {
                using var heroes = WorldQueries.Entities<Hero>(em);
                foreach (var e in heroes)
                {
                    var h = em.GetComponentData<Hero>(e);
                    if (em.GetComponentData<HeroDefinitionRef>(e).Definition != index)
                        continue;
                    if (h.Recruited != 0 || h.DeathPending != 0 || em.GetComponentData<GameClock>(root).Turn < h.CooldownUntil)
                        return ResultCode.Unavailable;
                    existingHero = e;
                    break;
                }
            }

            var gold = em.GetComponentData<CurrencySettings>(root).Gold;
            if (InventoryOps.Count(em, root, gold) < d.FallbackWakeGold)
                return ResultCode.InsufficientResources;
            var beforeIds = em.GetComponentData<IdentitySequence>(root);
            using var resources = new InventoryTransaction(em, root);
            using var actorState = existingHero == Entity.Null ? null : new HeroMutationTransaction(em, existingHero);
            Entity unit = existingHero;
            try
            {
                if (unit == Entity.Null)
                {
                    unit = HeroEntities.Spawn(em, root, index, EntityState.Position(em, site), true);
                    PortraitOps.Ensure(em, root, unit);
                }

                EntityState.Set(em, unit, new Hero { Sanctum = c.Sanctum, Recruited = 1 });
                HeroCombatants.Configure(em, root, unit, false, c.Sanctum, EntityState.Position(em, site));
                if (!InventoryOps.Remove(em, root, gold, d.FallbackWakeGold))
                    throw new System.InvalidOperationException("Hero recruitment resources changed");
                probe?.Invoke("paid");
                resources.Commit();
                actorState?.Commit();
                return ResultCode.Success;
            }
            catch (System.Exception)
            {
                if (existingHero == Entity.Null)
                {
                    if (em.Exists(unit))
                        em.DestroyEntity(unit);
                }

                em.SetComponentData(root, beforeIds);
                return ResultCode.PreparationFailed;
            }
        }

        public static void KillHero(EntityManager em, Entity root, Entity hero)
        {
            var h = em.GetComponentData<Hero>(hero);
            if (h.Recruited == 0)
                return;
            var definition = em.GetComponentData<HeroDefinitionRef>(hero).Definition;
            var state = em.GetComponentData<Session>(root);
            GameClock stateClock = em.GetComponentData<GameClock>(root);
            HeroSelection stateHeroSelection = em.GetComponentData<HeroSelection>(root);
            h.Recruited = 0;
            h.Experience = 0;
            if (stateHeroSelection.SelectedHero == hero)
            {
                stateHeroSelection.SelectedHero = Entity.Null;
                {
                    em.SetComponentData(root, state);
                    em.SetComponentData(root, stateClock);
                    em.SetComponentData(root, stateHeroSelection);
                }
            }

            UnitOrders.Order(em, hero, new UnitOrder());
            h.DeathPending = (byte)(state.Phase == Phase.Day || state.Phase == Phase.Settlement ? 0 : 1);
            h.CooldownUntil = h.DeathPending != 0 ? 0 : stateClock.Turn + 1 + math.max(1, HeroDefinitions.Get(em, root, definition).RevivalCooldownTurns);
            em.SetComponentData(hero, h);
            var health = em.GetComponentData<Health>(hero);
            health.Current = 0;
            em.SetComponentData(hero, health);
            em.SetComponentData(hero, new VisualState());
            var id = em.GetComponentData<Identity>(hero);
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.HeroDeath, Id = id.Id, Amount = 1, SourceName = id.Name });
            SimulationEvents.Emit(em, root, EventKind.Death, "英雄陨落", em.GetComponentData<Identity>(hero).Id, amount: 1);
        }

        public static ResultCode Wake(EntityManager em, Entity root, Entity site, System.Action<string> probe = null)
        {
            if (!BuildingStatus.Operational(em, site))
                return ResultCode.InvalidTarget;
            if (HeroOps.HeroAvailability(em, root, site, true).Length != 0)
                return ResultCode.Unavailable;
            if (!HeroOps.HeroEntrance(em, root, site, out var entrance))
                return ResultCode.Unavailable;
            GameClock stateClock = em.GetComponentData<GameClock>(root);
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(site);
            BuildingSanctumState bSanctum = em.GetComponentData<BuildingSanctumState>(site);
            BuildingSanctumStats statsSanctum = em.GetComponentData<BuildingSanctumStats>(site);
            if (bWorkforce.Workers < statsSanctum.RequiredWorkers || bSanctum.PaidOfferingTurn != stateClock.Turn || bSanctum.WokenTurn == stateClock.Turn)
                return ResultCode.Unavailable;
            var id = em.GetComponentData<Identity>(site).Id;
            using var heroes = WorldQueries.Entities<Hero>(em);
            foreach (var e in heroes)
            {
                var h = em.GetComponentData<Hero>(e);
                if (h.Sanctum != id || h.Recruited == 0)
                    continue;
                var definition = em.GetComponentData<HeroDefinitionRef>(e).Definition;
                using var resources = new InventoryTransaction(em, root);
                using var actorState = new HeroMutationTransaction(em, e);
                int reportCount = em.GetBuffer<BattleReportEntry>(root).Length;
                try
                {
                    if (!BuildingCostOps.Pay(em, root, AwakeningCosts(em, root, definition)))
                        return ResultCode.InsufficientResources;
                    probe?.Invoke("paid");
                    HeroCombatants.Configure(em, root, e, true, id, entrance);
                    em.SetComponentData(e, LocalTransform.FromPosition(entrance));
                    foreach (var cost in AwakeningCosts(em, root, definition))
                        em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.HeroWakeCost, Id = em.GetComponentData<Identity>(e).Id, Item = cost.Item, Amount = cost.Amount, SourceName = em.GetComponentData<Identity>(e).Name });
                    probe?.Invoke("deployed");
                    bSanctum.WokenTurn = stateClock.Turn;
                    {
                        em.SetComponentData(site, bWorkforce);
                        em.SetComponentData(site, bSanctum);
                    }

                    resources.Commit();
                    actorState.Commit();
                    return ResultCode.Success;
                }
                catch (System.Exception)
                {
                    em.GetBuffer<BattleReportEntry>(root).ResizeUninitialized(reportCount);
                    return ResultCode.PreparationFailed;
                }
            }

            return ResultCode.Unavailable;
        }
    }
}
