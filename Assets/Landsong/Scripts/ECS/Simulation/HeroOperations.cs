using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static partial class MilitaryOps
    {
        public static int HeroLevel(EntityManager em, Entity root, Entity unit) => Level(Sim.Definition(em, root, em.GetComponentData<Identity>(unit).Definition).HeroGrowth.Progression, em.GetComponentData<Hero>(unit).Experience);
        public static int AddHeroExperience(HeroGrowth growth, int current, int amount)
            => (int)math.min((long)LevelThreshold(growth.Progression, growth.Progression.MaxLevel), (long)current + math.max(0, amount));
        // Call only AFTER an opposing effect, effective heal, shield absorption, buff/debuff or control
        // actually takes effect. Attempts, overheal and merely casting an ability do not qualify.
        public static void RecordHeroContribution(EntityManager em, Entity root, Entity unit, float effectiveAmount)
        {
            var s = em.GetComponentData<Session>(root);
            if (!math.isfinite(effectiveAmount) || effectiveAmount <= 0 || s.Paused != 0 || s.NightKind == NightKind.Peaceful || (s.Phase != Phase.Night && s.Phase != Phase.Retreat) || !Sim.Alive(em, unit) || !em.HasComponent<HeroCombat>(unit) || em.GetComponentData<Combatant>(unit).Deployed == 0) return;
            var h = em.GetComponentData<HeroCombat>(unit); if (h.Turn != s.Turn) h = new HeroCombat { Turn = s.Turn, CountedUntil = s.Time };
            var config = Sim.Definition(em, root, em.GetComponentData<Identity>(unit).Definition).HeroGrowth;
            if (h.ContactUntil < s.Time) h.CountedUntil = s.Time;
            h.ContactUntil = s.Time + config.ContactSeconds; em.SetComponentData(unit, h);
        }
        public static void TickHeroExperience(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            if (s.Paused != 0 || s.NightKind == NightKind.Peaceful || (s.Phase != Phase.Night && s.Phase != Phase.Retreat)) return;
            using var heroes = Sim.Entities<HeroCombat>(em);
            foreach (var e in heroes)
            {
                if (!Sim.Alive(em, e) || em.GetComponentData<Combatant>(e).Deployed == 0) continue;
                var h = em.GetComponentData<HeroCombat>(e); if (h.Turn != s.Turn) continue;
                float end = math.min(s.Time, h.ContactUntil); h.EffectiveSeconds += math.max(0, end - h.CountedUntil); h.CountedUntil = math.max(h.CountedUntil, end); em.SetComponentData(e, h);
            }
        }
        public static int HeroBattleExperience(EntityManager em, Entity root, Entity unit)
        {
            var s = em.GetComponentData<Session>(root); var hero = em.GetComponentData<Hero>(unit);
            if (s.NightKind == NightKind.Peaceful || !Sim.Alive(em, unit) || hero.Recruited == 0 || hero.LastCombatTurn == s.Turn || !em.HasComponent<HeroCombat>(unit)) return 0;
            var contact = em.GetComponentData<HeroCombat>(unit); if (contact.Turn != s.Turn || contact.EffectiveSeconds <= 0) return 0;
            var g = Sim.Definition(em, root, em.GetComponentData<Identity>(unit).Definition).HeroGrowth;
            float threat = math.clamp(math.sqrt(math.max(0, NightPlanOps.State(em, root).BaseThreat) / g.ThreatReference), 1, g.MaximumThreatMultiplier);
            int award = (int)math.min(1000000f, math.ceil(contact.EffectiveSeconds * g.ExperiencePerSecond * threat));
            return AddHeroExperience(g, hero.Experience, award) - hero.Experience;
        }
        public static void ReportHeroExperience(EntityManager em, Entity root)
        {
            using var heroes = Sim.OrderedEntities<Hero>(em);
            foreach (var e in heroes)
            {
                int amount = HeroBattleExperience(em, root, e); if (amount <= 0) continue;
                var id = em.GetComponentData<Identity>(e); var report = em.GetBuffer<BattleReportEntry>(root); bool found = false;
                for (int i = 0; i < report.Length; i++) if (report[i].Kind == EventKind.HeroExperience && report[i].Id == id.Id) { var entry = report[i]; entry.Amount = amount; report[i] = entry; found = true; break; }
                if (!found) report.Add(new BattleReportEntry { Kind = EventKind.HeroExperience, Id = id.Id, Definition = id.Definition, Amount = amount, SourceName = id.Name });
            }
        }
        public static string HeroAvailability(EntityManager em, Entity root, Entity site, bool wake)
        {
            if (!Sim.Operational(em, site)) return "神殿未正常运作";
            var s = em.GetComponentData<Session>(root); var b = em.GetComponentData<Building>(site); var stats = em.GetComponentData<BuildingStats>(site);
            if (!Sim.ValidDefinition(em, root, stats.HeroDefinition)) return "未配置英雄";
            if (s.Paused != 0 || s.CheckpointPending != 0) return "暂停或节点处理中";
            if (wake ? s.Phase != Phase.Night : s.Phase != Phase.Day) return wake ? "只可在夜晚唤醒" : "只可在白天招募";
            if (b.Workers < stats.RequiredWorkers) return $"工人不足：{b.Workers}/{stats.RequiredWorkers}";
            Entity hero = Entity.Null; using var heroes = Sim.OrderedEntities<Hero>(em);
            foreach (var e in heroes) if (em.GetComponentData<Identity>(e).Definition == stats.HeroDefinition) { hero = e; break; }
            var h = hero == Entity.Null ? default : em.GetComponentData<Hero>(hero);
            if (h.DeathPending != 0) return "已阵亡；黎明开始完整冷却";
            if (h.CooldownUntil > s.Turn) return $"重招还需 {h.CooldownUntil - s.Turn} 回合";
            var d = Sim.Definition(em, root, stats.HeroDefinition);
            if (!wake)
            {
                if (h.Recruited != 0) return "已招募";
                if (Sim.Population(em, root) - Sim.Employed(em) < d.Population) return "招募人口不足";
                if (InventoryOps.Count(em, root, em.GetComponentData<GameSettings>(root).Gold) < d.Cost) return "招募金币不足";
            }
            else
            {
                if (h.Recruited == 0 || h.Sanctum != em.GetComponentData<Identity>(site).Id) return "尚未招募英雄";
                if (b.WokenTurn == s.Turn) return "本夜已出场";
                if (b.PaidOfferingTurn != s.Turn) return "本回合未完成供奉";
                if (!HeroEntrance(em, root, site, out _)) return "没有可用出场位置";
                foreach (var cost in BuildingCostOps.Rules(em, root, stats.HeroDefinition, RuleKind.WakeCost, 1))
                    if (InventoryOps.Count(em, root, cost.Item) < cost.Amount) return "唤醒资源不足";
            }
            return "";
        }
        public static bool HeroEntrance(EntityManager em, Entity root, Entity site, out float3 position)
        {
            var b = em.GetComponentData<Building>(site); var grid = em.GetComponentData<GridData>(root); var occupied = em.GetBuffer<Occupancy>(root);
            for (int y = -1; y <= b.Size.y; y++) for (int x = -1; x <= b.Size.x; x++)
            { if (x >= 0 && y >= 0 && x < b.Size.x && y < b.Size.y) continue; var cell = b.Cell + new int2(x, y); if (!GridOps.Traversable(grid, occupied, cell)) continue; position = GridOps.Position(grid, cell, new int2(1)) + new float3(0, .5f, 0); return true; }
            position = default; return false;
        }
    }
}
