using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static partial class MilitaryOps
    {
        static float Modifier(EntityManager em, Entity root, RuleKind kind, int target)
        {
            float result = Sim.Modifier(em, root, kind, target);
            void Add(int definition, int level)
            { var d = Sim.Definition(em, root, definition); for (int i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind == kind && r.Level <= level && (r.Target < 0 || r.Target == target)) result += r.Value; } }
            foreach (var grant in em.GetBuffer<Entitlement>(root)) if (Sim.Definition(em, root, grant.Definition).Kind == ContentKind.Technology) Add(grant.Definition, grant.Level);
            using var buildings = Sim.OrderedEntities<Building>(em);
            foreach (var e in buildings) if (Sim.Operational(em, e))
            { var b = em.GetComponentData<Building>(e); var stats = em.GetComponentData<BuildingStats>(e); if (b.Maintained != 0 && b.Workers >= stats.RequiredWorkers) Add(em.GetComponentData<Identity>(e).Definition, b.Level); }
            return result;
        }
        public static NightPreparation CurrentStats(EntityManager em, Entity root, int definition, bool hero)
        {
            var d = Sim.Definition(em, root, definition);
            var profile = d.Combat; profile.Armor = math.max(0, profile.Armor + Modifier(em, root, RuleKind.ArmorBonus, definition)); profile.Reduction = math.clamp(profile.Reduction + Modifier(em, root, RuleKind.DamageReductionBonus, definition), 0, .95f); profile.Penetration = math.max(0, profile.Penetration + Modifier(em, root, RuleKind.PenetrationBonus, definition)); profile.BlastRadius = math.clamp(profile.BlastRadius + Modifier(em, root, RuleKind.BlastRadiusBonus, definition), 0, 32);
            return new NightPreparation { Definition = definition, Health = d.Health * math.max(.1f, 1 + Modifier(em, root, RuleKind.HealthBonus, definition)), Damage = d.Damage * math.max(0, 1 + Modifier(em, root, RuleKind.AttackBonus, definition) + (hero ? 0 : Modifier(em, root, RuleKind.SoldierAttackBonus, definition))), Speed = d.Speed * math.max(.1f, 1 + Modifier(em, root, RuleKind.SpeedBonus, definition) + (hero ? 0 : Modifier(em, root, RuleKind.SoldierSpeedBonus, definition))), Range = math.max(.1f, d.Range * (1 + Modifier(em, root, RuleKind.RangeBonus, definition))), Interval = math.max(.05f, d.Interval / math.max(.1f, 1 + Modifier(em, root, RuleKind.AttackSpeedBonus, definition))), ProjectileSpeed = math.max(.1f, d.ProjectileSpeed * (1 + Modifier(em, root, RuleKind.ProjectileSpeedBonus, definition))), Combat = profile };
        }
        public static NightPreparation Stats(EntityManager em, Entity root, int definition, bool hero)
        {
            var s = em.GetComponentData<Session>(root);
            if (s.Phase != Phase.Day && NightPlanOps.State(em, root).PreparedTurn == s.Turn && em.HasBuffer<NightPreparation>(root))
                foreach (var p in em.GetBuffer<NightPreparation>(root)) if (p.Definition == definition) return p;
            return CurrentStats(em, root, definition, hero);
        }
        public static void ConfigureCombatant(EntityManager em, Entity root, Entity e, byte faction, bool hero, bool deployed, ulong homeId, float3 home)
        {
            var definition = em.GetComponentData<Identity>(e).Definition;
            var d = Sim.Definition(em, root, definition);
            Sim.Set(em, e, new Combatant { Faction = faction, IsHero = (byte)(hero ? 1 : 0), IsBoss = (byte)((d.Flags & 1) != 0 && faction == 1 ? 1 : 0), Deployed = (byte)(deployed ? 1 : 0), Range = d.Range, Interval = d.Interval, ProjectileSpeed = d.ProjectileSpeed, Home = home, HomeId = homeId, TargetMode = (byte)((d.Flags >> 1) & 3), Threat = math.max(1, d.Value) });
            Sim.Set(em, e, new UnitOrder()); Sim.Set(em, e, new Steering()); Sim.Buffer<Waypoint>(em, e);
            var locked = faction == 0 ? Stats(em, root, definition, hero) : new NightPreparation { Health = d.Health, Damage = d.Damage, Speed = d.Speed, Range = d.Range, Interval = d.Interval, ProjectileSpeed = d.ProjectileSpeed, Combat = d.Combat };
            if (faction == 0 && !hero && em.HasComponent<Soldier>(e)) ApplyGrowth(ref locked, d.SoldierGrowth, em.GetComponentData<Soldier>(e).Experience);
            if (hero) { Sim.Set(em, e, new HeroCombat()); if (em.HasComponent<Hero>(e)) ApplyGrowth(ref locked, d.HeroGrowth.Progression, em.GetComponentData<Hero>(e).Experience); }
            var actor = em.GetComponentData<Combatant>(e); actor.Damage = locked.Damage; actor.Speed = locked.Speed; actor.TargetRevision = -1;
            actor.Range = locked.Range; actor.Interval = locked.Interval; actor.ProjectileSpeed = locked.ProjectileSpeed; actor.Profile = locked.Combat;
            Sim.Set(em, e, new TacticalState { Origin = home });
            var target = Sim.Find(em, homeId); actor.TargetAnchor = target != Entity.Null && em.HasComponent<Building>(target) ? Sim.Position(em, target) : home;
            em.SetComponentData(e, actor); Sim.Set(em, e, new Health { Current = locked.Health, Maximum = locked.Health });
            Sim.Set(em, e, new NavigationState { Revision = -1 }); Sim.Set(em, e, new Perception());
            Sim.Set(em, e, new VisualState { Visible = (byte)(deployed ? 1 : 0) });
        }
        public static int GarrisonCount(EntityManager em, ulong id, Entity except = default)
        {
            var count = 0; using var all = Sim.Entities<Soldier>(em);
            foreach (var e in all) if (e != except && em.GetComponentData<Soldier>(e).Garrison == id) count++;
            return count;
        }
        public static int UnassignedCount(EntityManager em)
        {
            var count = 0; using var all = Sim.Entities<Soldier>(em);
            foreach (var e in all) if (em.GetComponentData<Soldier>(e).Garrison == 0 && Sim.Alive(em, e)) count++;
            return count;
        }
        public static ResultCode Recruit(EntityManager em, Entity root, Command c, bool hero, System.Action<string> probe = null)
        {
            if (!hero) return RecruitSoldiers(em, root, c);
            var site = Sim.Find(em, c.Target);
            if (!Sim.Operational(em, site)) return ResultCode.InvalidTarget;
            var stats = em.GetComponentData<BuildingStats>(site);
            var index = stats.HeroDefinition;
            if (!Sim.ValidDefinition(em, root, index)) return ResultCode.InvalidContent;
            var d = Sim.Definition(em, root, index);
            if (d.Kind != ContentKind.Hero) return ResultCode.InvalidContent;
            if (em.GetComponentData<Building>(site).Workers < stats.RequiredWorkers) return ResultCode.InsufficientPopulation;
            if (Sim.Population(em, root) - Sim.Employed(em) < d.Population) return ResultCode.InsufficientPopulation;
            var existingHero = Entity.Null;
            if (hero)
            {
                using var heroes = Sim.Entities<Hero>(em);
                foreach (var e in heroes)
                {
                    var h = em.GetComponentData<Hero>(e); if (em.GetComponentData<Identity>(e).Definition != index) continue;
                    if (h.Recruited != 0 || h.DeathPending != 0 || em.GetComponentData<Session>(root).Turn < h.CooldownUntil) return ResultCode.Unavailable;
                    existingHero = e; break;
                }
            }
            var gold = em.GetComponentData<GameSettings>(root).Gold;
            if (InventoryOps.Count(em, root, gold) < d.Cost) return ResultCode.InsufficientResources;
            var session = em.GetComponentData<Session>(root);
            using var stock = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp);
            int ledger = em.HasBuffer<EconomyEntry>(root) ? em.GetBuffer<EconomyEntry>(root).Length : 0; Entity unit = existingHero;
            var oldHero = existingHero == Entity.Null ? default : em.GetComponentData<Hero>(existingHero);
            var oldActor = existingHero == Entity.Null ? default : em.GetComponentData<Combatant>(existingHero);
            var oldHealth = existingHero == Entity.Null ? default : em.GetComponentData<Health>(existingHero);
            try
            {
                if (unit == Entity.Null) {unit = Sim.Spawn(em, root, index, Sim.Position(em, site), true);PortraitOps.Ensure(em,root,unit);}
                Sim.Set(em, unit, new Hero { Sanctum = c.Target, Recruited = 1 });
                ConfigureCombatant(em, root, unit, 0, hero, false, c.Target, Sim.Position(em, site));
                if (!InventoryOps.Remove(em, root, gold, d.Cost)) throw new System.InvalidOperationException("Hero recruitment resources changed");
                probe?.Invoke("paid");
                return ResultCode.Success;
            }
            catch (System.Exception)
            {
                if (existingHero == Entity.Null) { if (em.Exists(unit)) em.DestroyEntity(unit); }
                else { em.SetComponentData(existingHero, oldHero); em.SetComponentData(existingHero, oldActor); em.SetComponentData(existingHero, oldHealth); }
                em.SetComponentData(root, session); em.GetBuffer<InventorySlot>(root).CopyFrom(stock); if (em.HasBuffer<EconomyEntry>(root)) em.GetBuffer<EconomyEntry>(root).ResizeUninitialized(ledger);
                return ResultCode.PreparationFailed;
            }
        }
        public static ResultCode Assign(EntityManager em, Entity root, Command c)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day) return ResultCode.WrongPhase;
            var unit = Sim.Find(em, c.Target); var site = Sim.Find(em, c.Other);
            if (unit == Entity.Null || !em.HasComponent<Soldier>(unit) || !Sim.Alive(em, unit)) return ResultCode.InvalidTarget;
            int slot = 0;
            if (c.Other != 0)
            {
                if (!Sim.Operational(em, site)) return ResultCode.InvalidTarget;
                slot = c.Argument > 0 ? c.Argument : FreeSlot(em, site, unit);
                if (slot <= 0 || slot > em.GetComponentData<BuildingStats>(site).Garrison || AtSlot(em, c.Other, slot, unit) != Entity.Null) return ResultCode.NoCapacity;
            }
            em.SetComponentData(unit, Placed(em.GetComponentData<Soldier>(unit), c.Other, slot, em.GetComponentData<Session>(root).Turn));
            return ResultCode.Success;
        }
        public static ResultCode Swap(EntityManager em, Command c)
        {
            var root = Sim.Root(em); var turn = em.GetComponentData<Session>(root); if (turn.Phase != Phase.Day) return ResultCode.WrongPhase;
            var a = Sim.Find(em, c.Target); var b = Sim.Find(em, c.Other);
            if (a == b || a == Entity.Null || b == Entity.Null || !em.HasComponent<Soldier>(a) || !em.HasComponent<Soldier>(b) || !Sim.Alive(em, a) || !Sim.Alive(em, b)) return ResultCode.InvalidTarget;
            var sa = em.GetComponentData<Soldier>(a); var sb = em.GetComponentData<Soldier>(b);
            foreach (var s in new[] { sa, sb }) if (s.Garrison != 0)
            { var site = Sim.Find(em, s.Garrison); if (!Sim.Operational(em, site) || s.Slot < 1 || s.Slot > em.GetComponentData<BuildingStats>(site).Garrison) return ResultCode.InvalidTarget; }
            em.SetComponentData(a, Placed(sa, sb.Garrison, sb.Slot, turn.Turn));
            em.SetComponentData(b, Placed(sb, sa.Garrison, sa.Slot, turn.Turn)); return ResultCode.Success;
        }
        public static void KillHero(EntityManager em, Entity root, Entity hero)
        {
            var h = em.GetComponentData<Hero>(hero); if (h.Recruited == 0) return;
            var definition = em.GetComponentData<Identity>(hero).Definition;
            var state = em.GetComponentData<Session>(root);
            h.Recruited = 0; h.Experience = 0;
            if (state.SelectedHero == hero) { state.SelectedHero = Entity.Null; em.SetComponentData(root, state); }
            Order(em, hero, new UnitOrder());
            h.DeathPending = (byte)(state.Phase == Phase.Day || state.Phase == Phase.Settlement ? 0 : 1);
            h.CooldownUntil = h.DeathPending != 0 ? 0 : state.Turn + 1 + math.max(1, Sim.Definition(em, root, definition).Duration);
            em.SetComponentData(hero, h); var health = em.GetComponentData<Health>(hero); health.Current = 0; em.SetComponentData(hero, health);
            em.SetComponentData(hero, new VisualState());
            var id = em.GetComponentData<Identity>(hero);
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.Death, Id = id.Id, Definition = definition, Amount = 1, SourceName = id.Name });
            Sim.Emit(em, root, EventKind.Death, "英雄陨落", em.GetComponentData<Identity>(hero).Id, definition, 1);
        }
        public static void Dawn(EntityManager em, Entity root)
        {
            var state = em.GetComponentData<Session>(root);
            AgeSoldiers(em,root);
            ReportSoldierExperience(em, root);
            ReportHeroExperience(em, root);
            using (var troops = Sim.OrderedEntities<Soldier>(em)) foreach (var e in troops) if (Sim.Alive(em, e))
            {
                var soldier = em.GetComponentData<Soldier>(e); int amount = SoldierBattleExperience(em, root, e);
                if (amount > 0) { soldier.Experience += amount; soldier.LastExperienceTurn = state.Turn; }
                soldier.RecallState = 0; em.SetComponentData(e, soldier);
                var stats = SoldierStats(em, root, e); var health = em.GetComponentData<Health>(e); health.Maximum = stats.Health; health.Current = stats.Health; em.SetComponentData(e, health);
            }
            using (var troops = Sim.Entities<Soldier>(em)) foreach (var e in troops) if (!Sim.Alive(em, e)) em.DestroyEntity(e);
            using var heroes = Sim.Entities<Hero>(em);
            foreach (var e in heroes)
            {
                var hero = em.GetComponentData<Hero>(e); var id = em.GetComponentData<Identity>(e);
                if (hero.DeathPending != 0)
                { hero.DeathPending = 0; hero.CooldownUntil = state.Turn + 1 + math.max(1, Sim.Definition(em, root, id.Definition).Duration); }
                else if (hero.Recruited != 0 && Sim.Alive(em, e) && hero.LastCombatTurn != state.Turn)
                {
                    hero.Experience += HeroBattleExperience(em, root, e); hero.LastCombatTurn = state.Turn;
                }
                em.SetComponentData(e, hero);
                if (hero.Recruited != 0 && Sim.Alive(em, e))
                { var stats = Stats(em, root, id.Definition, true); ApplyGrowth(ref stats, Sim.Definition(em, root, id.Definition).HeroGrowth.Progression, hero.Experience); var health = em.GetComponentData<Health>(e); health.Maximum = stats.Health; health.Current = stats.Health; em.SetComponentData(e, health); }
            }
            using var actors = Sim.Entities<Combatant>(em);
            foreach (var e in actors) if (em.GetComponentData<Combatant>(e).Faction == 0 && Sim.Alive(em, e))
            { var h = em.GetComponentData<Health>(e); h.Current = h.Maximum; em.SetComponentData(e, h); }
        }
        public static ResultCode Wake(EntityManager em, Entity root, Entity site, System.Action<string> probe = null)
        {
            if (!Sim.Operational(em, site)) return ResultCode.InvalidTarget;
            if (HeroAvailability(em, root, site, true).Length != 0) return ResultCode.Unavailable;
            if (!HeroEntrance(em, root, site, out var entrance)) return ResultCode.Unavailable;
            var state = em.GetComponentData<Session>(root);
            var b = em.GetComponentData<Building>(site); var stats = em.GetComponentData<BuildingStats>(site);
            if (b.Workers < stats.RequiredWorkers || b.PaidOfferingTurn != state.Turn || b.WokenTurn == state.Turn) return ResultCode.Unavailable;
            var id = em.GetComponentData<Identity>(site).Id;
            using var heroes = Sim.Entities<Hero>(em);
            foreach (var e in heroes)
            {
                var h = em.GetComponentData<Hero>(e); if (h.Sanctum != id || h.Recruited == 0) continue;
                var definition = em.GetComponentData<Identity>(e).Definition;
                using var stock = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp);
                int reportCount = em.GetBuffer<BattleReportEntry>(root).Length, ledgerCount = em.HasBuffer<EconomyEntry>(root) ? em.GetBuffer<EconomyEntry>(root).Length : 0;
                var oldActor = em.GetComponentData<Combatant>(e); var oldHealth = em.GetComponentData<Health>(e); var oldTransform = em.GetComponentData<LocalTransform>(e); var oldVisual = em.GetComponentData<VisualState>(e); var oldCombat = em.GetComponentData<HeroCombat>(e);
                try
                {
                    if (!InventoryOps.Pay(em, root, definition, RuleKind.WakeCost, 1)) return ResultCode.InsufficientResources;
                    probe?.Invoke("paid");
                    ConfigureCombatant(em, root, e, 0, true, true, id, entrance);
                    em.SetComponentData(e, LocalTransform.FromPosition(entrance));
                    foreach (var cost in BuildingCostOps.Rules(em, root, definition, RuleKind.WakeCost, 1))
                        em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.HeroWakeCost, Id = em.GetComponentData<Identity>(e).Id, Definition = cost.Item, Amount = cost.Amount, SourceName = em.GetComponentData<Identity>(e).Name });
                    probe?.Invoke("deployed"); b.WokenTurn = state.Turn; em.SetComponentData(site, b); return ResultCode.Success;
                }
                catch (System.Exception)
                {
                    em.GetBuffer<InventorySlot>(root).CopyFrom(stock); em.GetBuffer<BattleReportEntry>(root).ResizeUninitialized(reportCount); if (em.HasBuffer<EconomyEntry>(root)) em.GetBuffer<EconomyEntry>(root).ResizeUninitialized(ledgerCount);
                    em.SetComponentData(e, oldActor); em.SetComponentData(e, oldHealth); em.SetComponentData(e, oldTransform); em.SetComponentData(e, oldVisual); em.SetComponentData(e, oldCombat); return ResultCode.PreparationFailed;
                }
            }
            return ResultCode.Unavailable;
        }
        public static void PrepareNight(EntityManager em, Entity root)
        {
            ReconcileGarrisons(em, root);
            var state = em.GetComponentData<Session>(root);
            using var soldiers = Sim.Entities<Soldier>(em);
            foreach (var e in soldiers)
            {
                var s = em.GetComponentData<Soldier>(e);
                s.RecallState = 0; em.SetComponentData(e, s);
                var site = Sim.Find(em, s.Garrison);
                if (!Sim.Operational(em, site)) { em.DestroyEntity(e); continue; }
                ConfigureCombatant(em, root, e, 0, false, false, s.Garrison, Sim.Position(em, site));
                var ordinal = math.max(0, s.Slot - 1);
                var actor = em.GetComponentData<Combatant>(e);
                actor.DeployAt = state.Time + (ordinal / math.max(1, em.GetComponentData<BuildingStats>(site).BatchSize)) * em.GetComponentData<GameSettings>(root).DeployInterval;
                em.SetComponentData(e, actor);
                em.SetComponentData(e, LocalTransform.FromPosition(Sim.Position(em, site) + new float3(0, .6f, 0)));
            }
        }
        public static void ReconcileGarrisons(EntityManager em, Entity root)
        {
            var turn = em.GetComponentData<Session>(root).Turn;
            using var troops = Sim.OrderedEntities<Soldier>(em);
            var occupied = new System.Collections.Generic.HashSet<(ulong, int)>();
            foreach (var e in troops)
            {
                var soldier = em.GetComponentData<Soldier>(e);
                if (soldier.Garrison == 0) continue;
                var site = Sim.Find(em, soldier.Garrison);
                if (Sim.Operational(em, site) && soldier.Slot > 0 && soldier.Slot <= em.GetComponentData<BuildingStats>(site).Garrison && occupied.Add((soldier.Garrison, soldier.Slot))) continue;
                em.SetComponentData(e, Placed(soldier, 0, 0, turn));
            }
        }
        public static void Bell(EntityManager em, Entity root, Entity bell)
        {
            if (!Sim.Operational(em, bell)) return;
            var radius = em.GetComponentData<BuildingStats>(bell).BellRadius; if (radius <= 0) return;
            var id = em.GetComponentData<Identity>(bell).Id; var state = em.GetComponentData<Session>(root);
            var previous = state.ActiveBell; var cancel = previous == id; state.ActiveBell = cancel ? 0 : id; em.SetComponentData(root, state);
            using var all = Sim.Entities<Combatant>(em);
            foreach (var e in all)
            {
                var a = em.GetComponentData<Combatant>(e);
                if (a.Faction != 0 || a.Deployed == 0 || !Sim.Alive(em, e)) continue;
                var order = em.GetComponentData<UnitOrder>(e);
                bool engaged = Sim.Alive(em, a.Target) && math.distance(Sim.Position(em, e), Sim.Position(em, a.Target)) <= a.Range;
                if ((order.Kind == OrderKind.Rally || order.Kind == OrderKind.Capture) && order.Source == previous && previous != 0)
                { if (engaged || e == state.SelectedHero) em.SetComponentData(e, new UnitOrder()); else ReturnToAnchor(em, root, e); }
                if (cancel || e == state.SelectedHero || em.HasComponent<Soldier>(e) && em.GetComponentData<Soldier>(e).RecallState != 0) continue;
                if (engaged) continue;
                if (math.distancesq(Sim.Position(em, e), Sim.Position(em, bell)) > radius * radius) continue;
                using var reach = new NightSpatialOps.Reach(em, root, Sim.Position(em, e));
                if (reach.Building(em.GetComponentData<Building>(bell), out var point) < float.MaxValue)
                    Order(em, e, new UnitOrder { Kind = OrderKind.Rally, Destination = point, Source = id });
            }
            if (state.NightKind == NightKind.Peaceful && !cancel) PeacefulOps.Bell(em, root, bell);
        }
        public static int Strength(EntityManager em, Entity root)
        {
            float value = 0; var rules = NightPlanOps.Rules(em, root);
            using var soldiers = Sim.OrderedEntities<Soldier>(em);
            using var capacity = new NativeHashMap<ulong, int>(math.max(1, soldiers.Length), Allocator.Temp);
            var usedSlots = capacity;
            foreach (var e in soldiers)
            {
                var s = em.GetComponentData<Soldier>(e); var site = Sim.Find(em, s.Garrison); if (!Sim.Alive(em, e) || !Sim.Operational(em, site)) continue;
                if (!capacity.TryGetValue(s.Garrison, out var used)) used = 0;
                if (used >= em.GetComponentData<BuildingStats>(site).Garrison) continue; usedSlots[s.Garrison] = used + 1;
                var d = em.GetComponentData<Identity>(e).Definition; var stats = CurrentStats(em, root, d, false); ApplyGrowth(ref stats, Sim.Definition(em, root, d).SoldierGrowth, s.Experience);
                value += stats.Health * .05f / (1 - stats.Combat.Reduction) + stats.Combat.Armor + stats.Damage / math.max(.1f, stats.Interval);
            }
            using var heroes = Sim.OrderedEntities<Hero>(em);
            foreach (var e in heroes)
            {
                var hero = em.GetComponentData<Hero>(e); var site = Sim.Find(em, hero.Sanctum);
                if (hero.Recruited == 0 || hero.DeathPending != 0 || !Sim.Alive(em, e) || !Sim.Operational(em, site) || em.GetComponentData<Building>(site).Workers < em.GetComponentData<BuildingStats>(site).RequiredWorkers) continue;
                // Potentially awakenable, independent of offering toggle: turning supply off must not hide a titan.
                var d = em.GetComponentData<Identity>(e).Definition; var stats = CurrentStats(em, root, d, true);
                ApplyGrowth(ref stats, Sim.Definition(em, root, d).HeroGrowth.Progression, hero.Experience);
                value += (stats.Health * .05f / (1 - stats.Combat.Reduction) + stats.Combat.Armor + stats.Damage / math.max(.1f, stats.Interval)) * rules.HeroWeight;
            }
            using var buildings = Sim.OrderedEntities<Building>(em);
            foreach (var e in buildings) if (Sim.Operational(em, e))
            { var stats = em.GetComponentData<BuildingStats>(e); if (stats.Garrison > 0 || stats.BellRadius > 0) value += math.max(1, stats.Garrison) * rules.FacilityWeight; }
            return (int)value;
        }
    }
}
