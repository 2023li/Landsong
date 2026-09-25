using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class BattleLifecycle
    {
        public static void Dawn(EntityManager em, Entity root)
        {
            GameClock stateClock = em.GetComponentData<GameClock>(root);
            EquipmentOps.SettleNight(em, root);
            SoldierLifeOps.AgeSoldiers(em, root);
            SoldierOps.ReportSoldierExperience(em, root);
            HeroOps.ReportHeroExperience(em, root);
            using (var troops = WorldQueries.OrderedEntities<Soldier>(em))
                foreach (var e in troops)
                    if (EntityState.Alive(em, e))
                    {
                        var soldier = em.GetComponentData<Soldier>(e);
                        int amount = SoldierOps.SoldierBattleExperience(em, root, e);
                        if (amount > 0)
                        {
                            soldier.Experience += amount;
                            soldier.LastExperienceTurn = stateClock.Turn;
                        }

                        soldier.RecallState = 0;
                        em.SetComponentData(e, soldier);
                        var stats = SoldierOps.SoldierStats(em, root, e);
                        var health = em.GetComponentData<Health>(e);
                        health.Maximum = stats.Health;
                        health.Current = stats.Health;
                        em.SetComponentData(e, health);
                    }

            using (var troops = WorldQueries.Entities<Soldier>(em))
                foreach (var e in troops)
                    if (!EntityState.Alive(em, e))
                        em.DestroyEntity(e);
            using var heroes = WorldQueries.Entities<Hero>(em);
            foreach (var e in heroes)
            {
                var hero = em.GetComponentData<Hero>(e);
                var id = em.GetComponentData<Identity>(e);
                if (hero.DeathPending != 0)
                {
                    hero.DeathPending = 0;
                    hero.CooldownUntil = stateClock.Turn + 1 + math.max(1, HeroDefinitions.Get(em, root, em.GetComponentData<HeroDefinitionRef>(e).Definition).RevivalCooldownTurns);
                }
                else if (hero.Recruited != 0 && EntityState.Alive(em, e) && hero.LastCombatTurn != stateClock.Turn)
                {
                    hero.Experience += HeroOps.HeroBattleExperience(em, root, e);
                    hero.LastCombatTurn = stateClock.Turn;
                }

                em.SetComponentData(e, hero);
                if (hero.Recruited != 0 && EntityState.Alive(em, e))
                {
                    var stats = HeroCombatStats.ForNight(em, root, em.GetComponentData<HeroDefinitionRef>(e).Definition);
                    UnitProgression.ApplyGrowth(ref stats, HeroDefinitions.Get(em, root, em.GetComponentData<HeroDefinitionRef>(e).Definition).Growth.Progression, hero.Experience);
                    var health = em.GetComponentData<Health>(e);
                    health.Maximum = stats.Health;
                    health.Current = stats.Health;
                    em.SetComponentData(e, health);
                }
            }

            using var actors = WorldQueries.Entities<Combatant>(em);
            foreach (var e in actors)
                if (em.GetComponentData<Combatant>(e).Faction == 0 && EntityState.Alive(em, e))
                {
                    var h = em.GetComponentData<Health>(e);
                    h.Current = h.Maximum;
                    em.SetComponentData(e, h);
                }
        }

        public static void PrepareNight(EntityManager em, Entity root)
        {
            GarrisonOps.ReconcileGarrisons(em, root);
            GameClock stateClock = em.GetComponentData<GameClock>(root);
            using var soldiers = WorldQueries.Entities<Soldier>(em);
            foreach (var e in soldiers)
            {
                var s = em.GetComponentData<Soldier>(e);
                s.RecallState = 0;
                em.SetComponentData(e, s);
                var site = WorldQueries.Find(em, s.Garrison);
                if (!BuildingStatus.Operational(em, site))
                {
                    if (EntityState.Alive(em, e))
                        EquipmentOps.ReturnWeapon(em, root, s.Weapon);
                    em.DestroyEntity(e);
                    continue;
                }

                SoldierCombatants.Configure(em, root, e, false, s.Garrison, EntityState.Position(em, site));
                var ordinal = math.max(0, s.Slot - 1);
                var actor = em.GetComponentData<Combatant>(e);
                actor.DeployAt = stateClock.Time + (ordinal / math.max(1, em.GetComponentData<BuildingGarrisonStats>(site).BatchSize)) * em.GetComponentData<NightSettings>(root).DeployInterval;
                em.SetComponentData(e, actor);
                em.SetComponentData(e, LocalTransform.FromPosition(EntityState.Position(em, site) + new float3(0, .6f, 0)));
            }
        }
    }
}
