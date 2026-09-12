using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public sealed class SoldierRecruitQuote
    {
        public ResultCode Code;
        public int Quantity, EmptySlots, RemainingLimit, FreePopulation;
        public List<BuildingCost> Costs = new List<BuildingCost>();
    }
    public static partial class MilitaryOps
    {
        // Authored starting units are granted only during new-world initialization.
        // They use ordinary persistent soldiers, but do not spend recruitment costs or quota.
        public static void InitializeGarrisons(EntityManager em, Entity root)
        {
            if(em.GetComponentData<Session>(root).Initialized!=0)return;
            using var sites=Sim.OrderedEntities<Building>(em);
            foreach(var site in sites)
            {
                var identity=em.GetComponentData<Identity>(site);var building=em.GetComponentData<Building>(site);
                var definition=Sim.Definition(em,root,identity.Definition);
                for(int i=0;i<definition.RuleCount;i++)
                {
                    var rule=Sim.GetRule(em,root,definition.RuleStart+i);
                    if(!EconomyOps.Matches(rule,RuleKind.InitialGarrison,building.Level)||rule.Amount==0)continue;
                    if(rule.Amount<0||!Sim.ValidDefinition(em,root,rule.Target)||Sim.Definition(em,root,rule.Target).Kind!=ContentKind.Soldier)
                        throw new InvalidOperationException("Invalid initial garrison: "+definition.Id);
                    if(rule.Amount>em.GetComponentData<BuildingStats>(site).Garrison-GarrisonCount(em,identity.Id))
                        throw new InvalidOperationException("Initial garrison exceeds building slots: "+definition.Id);
                    for(int n=0;n<rule.Amount;n++)
                    {
                        int slot=FreeSlot(em,site);if(slot==0)throw new InvalidOperationException("Initial garrison has no operational slot: "+definition.Id);
                        var unit=Sim.Spawn(em,root,rule.Target,Sim.Position(em,site),true);
                        var id=em.GetComponentData<Identity>(unit);id.Name=SoldierName(id.Id);em.SetComponentData(unit,id);
                        Sim.Set(em,unit,new Soldier{Garrison=identity.Id,Slot=slot,PopulationCost=Sim.Definition(em,root,rule.Target).Population});
                        ConfigureCombatant(em,root,unit,0,false,false,identity.Id,Sim.Position(em,site));InitializePerson(em,root,unit);
                    }
                }
            }
        }
        public static int Level(SoldierGrowth g, int experience)
        {
            int level = 1;
            while (level < math.max(1, g.MaxLevel)) { int cost = g.FirstLevelExperience + (level - 1) * g.ExperienceStep; if (cost <= 0 || experience < cost) break; experience -= cost; level++; }
            return level;
        }
        public static int LevelThreshold(SoldierGrowth g, int level)
        { long n = math.clamp(level - 1, 0, math.max(0, g.MaxLevel - 1)); return (int)Math.Min(int.MaxValue, n * g.FirstLevelExperience + n * (n - 1) / 2 * g.ExperienceStep); }
        static void ApplyGrowth(ref NightPreparation stats, SoldierGrowth growth, int experience)
        { int ranks = Level(growth, experience) - 1; stats.Health *= 1 + ranks * growth.HealthPerLevel; stats.Damage *= 1 + ranks * growth.DamagePerLevel; }
        public static NightPreparation SoldierStats(EntityManager em, Entity root, Entity unit)
        {
            var d = em.GetComponentData<Identity>(unit).Definition; var result = Stats(em, root, d, false);
            ApplyGrowth(ref result, Sim.Definition(em, root, d).SoldierGrowth, em.GetComponentData<Soldier>(unit).Experience); return result;
        }
        public static Entity AtSlot(EntityManager em, ulong home, int slot, Entity except = default)
        {
            using var units = Sim.OrderedEntities<Soldier>(em);
            foreach (var e in units) { var s = em.GetComponentData<Soldier>(e); if (e != except && s.Garrison == home && s.Slot == slot) return e; }
            return Entity.Null;
        }
        public static int FreeSlot(EntityManager em, Entity site, Entity except = default)
        {
            if (!Sim.Operational(em, site)) return 0;
            ulong home = em.GetComponentData<Identity>(site).Id;
            for (int slot = 1; slot <= em.GetComponentData<BuildingStats>(site).Garrison; slot++) if (AtSlot(em, home, slot, except) == Entity.Null) return slot;
            return 0;
        }
        public static SoldierRecruitQuote RecruitQuote(EntityManager em, Entity root, ulong home, int definition, int quantity, bool pending = false)
        {
            var q = new SoldierRecruitQuote { Quantity = quantity, Code = ResultCode.Success }; var site = Sim.Find(em, home); var session = em.GetComponentData<Session>(root);
            if (session.Phase != Phase.Day) { q.Code = ResultCode.WrongPhase; return q; }
            if (!Sim.Operational(em, site) || em.GetComponentData<BuildingStats>(site).Garrison <= 0) { q.Code = ResultCode.InvalidTarget; return q; }
            if (!Sim.ValidDefinition(em, root, definition) || Sim.Definition(em, root, definition).Kind != ContentKind.Soldier || quantity < 1 || quantity > 1000) { q.Code = ResultCode.InvalidContent; return q; }
            var d = Sim.Definition(em, root, definition); var b = em.GetComponentData<Building>(site); int capacity = em.GetComponentData<BuildingStats>(site).Garrison;
            var policy = Sim.Definition(em, root, em.GetComponentData<Identity>(site).Definition).BuildingPolicy;
            q.EmptySlots = math.max(0, capacity - GarrisonCount(em, home));
            q.RemainingLimit = math.max(0, (policy.SoldierRecruitLimit > 0 ? policy.SoldierRecruitLimit : capacity) - (b.SoldierRecruitTurn == session.Turn ? b.SoldiersRecruited : 0));
            q.FreePopulation = math.max(0, Sim.Population(em, root) - Sim.Employed(em));
            bool explicitCosts = false;
            try
            {
                for (int i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind != RuleKind.RecruitCost) continue; explicitCosts = true; BuildingCostOps.Add(q.Costs, r.Target, checked(r.Amount * quantity)); }
                if (!explicitCosts) BuildingCostOps.Add(q.Costs, em.GetComponentData<GameSettings>(root).Gold, checked(d.Cost * quantity));
            }
            catch (OverflowException) { q.Code = ResultCode.InvalidContent; return q; }
            if (!pending && quantity > q.EmptySlots) q.Code = ResultCode.NoCapacity;
            else if (quantity > q.RemainingLimit) q.Code = ResultCode.Unavailable;
            else if ((long)d.Population * quantity > q.FreePopulation) q.Code = ResultCode.InsufficientPopulation;
            else if (!BuildingCostOps.CanPay(em, root, q.Costs)) q.Code = ResultCode.InsufficientResources;
            else
            {
                bool prefab = false;
                foreach (var p in em.GetBuffer<ContentPrefab>(root)) if (p.Definition == definition && p.Prefab != Entity.Null && em.Exists(p.Prefab)) prefab = true;
                if (!prefab) q.Code = ResultCode.InvalidContent;
            }
            return q;
        }
        // Whole quantity succeeds or fails. Fault probe is used only by isolated verification.
        public static ResultCode RecruitSoldiers(EntityManager em, Entity root, Command c, Action<int> probe = null)
        {
            int quantity = c.Amount == 0 ? 1 : c.Amount;
            if(c.Argument!=0&&c.Argument!=1)return ResultCode.InvalidContent;
            bool pending=c.Argument==1;
            var q = RecruitQuote(em, root, c.Target, c.Definition, quantity,pending); if (q.Code != ResultCode.Success) return q.Code;
            var site = Sim.Find(em, c.Target); var before = em.GetComponentData<Building>(site); var session = em.GetComponentData<Session>(root);
            using var resources = new InventoryTransaction(em, root);
            var created = new List<Entity>();
            try
            {
                for (int i = 0; i < quantity; i++)
                {
                    int slot = pending?0:FreeSlot(em, site); if (!pending&&slot == 0) throw new InvalidOperationException("驻军槽发生变化");
                    var unit = Sim.Spawn(em, root, c.Definition, Sim.Position(em, site), true); created.Add(unit);
                    var id = em.GetComponentData<Identity>(unit); id.Name = SoldierName(id.Id); em.SetComponentData(unit, id);
                    Sim.Set(em, unit, new Soldier { Garrison = pending?0:c.Target, Slot = slot, PendingSince=pending?session.Turn:0, PopulationCost = Sim.Definition(em, root, c.Definition).Population });
                    ConfigureCombatant(em, root, unit, 0, false, false, c.Target, Sim.Position(em, site)); InitializePerson(em,root,unit); probe?.Invoke(i);
                }
                if (!BuildingCostOps.Pay(em, root, q.Costs)) throw new InvalidOperationException("招募资源发生变化");
                var b = before; b.SoldiersRecruited = (b.SoldierRecruitTurn == session.Turn ? b.SoldiersRecruited : 0) + quantity; b.SoldierRecruitTurn = session.Turn; em.SetComponentData(site, b);
                probe?.Invoke(quantity); resources.Commit(); return ResultCode.Success;
            }
            catch (Exception)
            {
                foreach (var e in created) if (em.Exists(e)) em.DestroyEntity(e);
                em.SetComponentData(root, session); em.SetComponentData(site, before);
                return ResultCode.PreparationFailed;
            }
        }
        static readonly string[] Surnames = { "林", "陆", "沈", "赵", "陈", "江", "周", "顾", "柳", "唐", "许", "韩", "卫", "徐", "秦", "苏" };
        static readonly string[] GivenNames = { "长风", "远山", "星河", "青川", "明岳", "景行", "怀安", "望舒", "秋实", "砺锋", "凌云", "清和", "思远", "启明", "知遥", "守宁" };
        static FixedString128Bytes SoldierName(ulong id)
        { uint hash = math.hash(new uint2((uint)id, (uint)(id >> 32) ^ 0x51a7u)); return new FixedString128Bytes(Surnames[hash % 16] + GivenNames[(hash / 16) % 16]); }
        public static ResultCode SoldierCommand(EntityManager em, Entity root, Command c)
        {
            var session = em.GetComponentData<Session>(root);
            var unit = Sim.Find(em, c.Target);
            if(c.Kind==CommandKind.SetSoldierAttention)
            {
                if(session.Paused!=0||session.CheckpointPending!=0||session.IntelligenceMode!=0)return ResultCode.Busy;
                if(session.Phase==Phase.GameOver||session.Phase==Phase.Ended)return ResultCode.WrongPhase;
                if(unit==Entity.Null||!em.HasComponent<Soldier>(unit)||!em.HasComponent<SoldierPerson>(unit)||!Sim.Alive(em,unit))return ResultCode.InvalidTarget;
                if(c.Argument!=0&&c.Argument!=1)return ResultCode.InvalidContent;
                var person=em.GetComponentData<SoldierPerson>(unit);person.SpecialAttention=(byte)c.Argument;em.SetComponentData(unit,person);return ResultCode.Success;
            }
            if(session.Phase!=Phase.Day)return ResultCode.WrongPhase;
            if (unit == Entity.Null || !em.HasComponent<Soldier>(unit) || !Sim.Alive(em, unit)) return ResultCode.InvalidTarget;
            if (c.Kind == CommandKind.RenameSoldier)
            {
                var name = BuildingOps.SanitizeName(c.Text.ToString()); if (string.IsNullOrWhiteSpace(name)) return ResultCode.InvalidContent;
                var identity = em.GetComponentData<Identity>(unit); identity.Name = new FixedString128Bytes(name); em.SetComponentData(unit, identity); return ResultCode.Success;
            }
            if (c.Kind != CommandKind.DismissSoldier) return ResultCode.InvalidContent;
            if (c.Argument != 1) return ResultCode.ConfirmationRequired;
            em.DestroyEntity(unit); return ResultCode.Success;
        }
        static Soldier Placed(Soldier s, ulong home, int slot, int turn)
        { if (home == 0 && s.Garrison != 0) s.PendingSince = turn; if (home != 0) s.PendingSince = 0; s.Garrison = home; s.Slot = home == 0 ? 0 : slot; return s; }
        public static ResultCode Fill(EntityManager em, Entity root, ulong home)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day) return ResultCode.WrongPhase;
            var site = Sim.Find(em, home); if (!Sim.Operational(em, site)) return ResultCode.InvalidTarget;
            int moved = 0; using var all = Sim.OrderedEntities<Soldier>(em);
            foreach (var unit in all) if (Sim.Alive(em, unit) && em.GetComponentData<Soldier>(unit).Garrison == 0)
            { int slot = FreeSlot(em, site); if (slot == 0) break; em.SetComponentData(unit, Placed(em.GetComponentData<Soldier>(unit), home, slot, em.GetComponentData<Session>(root).Turn)); moved++; }
            return moved > 0 ? ResultCode.Success : ResultCode.NoCapacity;
        }
        public static void Order(EntityManager em, Entity unit, UnitOrder order)
        {
            em.SetComponentData(unit, order); em.SetComponentData(unit, new Steering());
            var a = em.GetComponentData<Combatant>(unit); a.Target = Entity.Null; em.SetComponentData(unit, a);
            em.SetComponentData(unit, new NavigationState { Revision = -1 }); em.GetBuffer<Waypoint>(unit).Clear();
        }
        static void ReturnToAnchor(EntityManager em, Entity root, Entity unit)
        {
            var actor = em.GetComponentData<Combatant>(unit); var site = Sim.Find(em, actor.HomeId); var destination = actor.Home;
            if (Sim.Operational(em, site)) { using var reach = new NightSpatialOps.Reach(em, root, Sim.Position(em, unit)); if (reach.Building(em.GetComponentData<Building>(site), out var point) < float.MaxValue) destination = point; }
            Order(em, unit, new UnitOrder { Kind = OrderKind.Move, Destination = destination });
        }
        public static ResultCode RecallGarrison(EntityManager em, Entity root, ulong home, bool cancel)
        {
            var state = em.GetComponentData<Session>(root); if (state.Phase != Phase.Night && state.Phase != Phase.Retreat) return ResultCode.WrongPhase;
            var site = Sim.Find(em, home); if (!Sim.Operational(em, site) || em.GetComponentData<BuildingStats>(site).Garrison <= 0) return ResultCode.InvalidTarget;
            int changed = 0, blocked = 0; using var all = Sim.OrderedEntities<Soldier>(em);
            foreach (var unit in all)
            {
                var soldier = em.GetComponentData<Soldier>(unit); if (soldier.Garrison != home || !Sim.Alive(em, unit) || soldier.RecallState == 2) continue;
                if (cancel) { if (soldier.RecallState != 1) continue; soldier.RecallState = 0; em.SetComponentData(unit, soldier); Order(em, unit, default); changed++; continue; }
                if (soldier.RecallState == 1) continue;
                var actor = em.GetComponentData<Combatant>(unit);
                if (actor.Deployed == 0) { soldier.RecallState = 2; actor.DeployAt = float.MaxValue; em.SetComponentData(unit, soldier); em.SetComponentData(unit, actor); changed++; continue; }
                using var reach = new NightSpatialOps.Reach(em, root, Sim.Position(em, unit));
                if (reach.Building(em.GetComponentData<Building>(site), out var point) == float.MaxValue) { blocked++; continue; }
                soldier.RecallState = 1; em.SetComponentData(unit, soldier); Order(em, unit, new UnitOrder { Kind = OrderKind.Recall, Source = home, Destination = point }); changed++;
            }
            Sim.Emit(em, root, EventKind.Message, cancel ? "已取消途中召回；已归营士兵本夜不再出勤" : blocked > 0 ? "部分士兵无可达回营路径，仍留在战场" : "驻军正在撤回；途中仍会受击", home);
            return changed > 0 ? ResultCode.Success : ResultCode.Unavailable;
        }
        public static void TickSoldierOrders(EntityManager em, Entity root)
        {
            var state = em.GetComponentData<Session>(root);
            if (state.ActiveBell != 0 && !Sim.Operational(em, Sim.Find(em, state.ActiveBell))) { state.ActiveBell = 0; em.SetComponentData(root, state); }
            using var actors = Sim.Entities<Combatant>(em);
            foreach (var e in actors)
            {
                var actor = em.GetComponentData<Combatant>(e); if (actor.Faction != 0 || !Sim.Alive(em, e)) continue;
                var order = em.GetComponentData<UnitOrder>(e);
                if (order.Kind == OrderKind.Rally && order.Source != state.ActiveBell)
                { if (e == state.SelectedHero || Sim.Alive(em, actor.Target) && math.distance(Sim.Position(em, e), Sim.Position(em, actor.Target)) <= actor.Range) em.SetComponentData(e, new UnitOrder()); else ReturnToAnchor(em, root, e); }
                if (!em.HasComponent<Soldier>(e)) continue;
                var soldier = em.GetComponentData<Soldier>(e); if (soldier.RecallState != 1) continue;
                if (!Sim.Operational(em, Sim.Find(em, soldier.Garrison)) || em.GetComponentData<NavigationState>(e).Failed != 0)
                { soldier.RecallState = 0; em.SetComponentData(e, soldier); Order(em, e, default); Sim.Emit(em, root, EventKind.Message, "回营中断：驻地荒废或路径不可达", soldier.Garrison); continue; }
                if (math.distance(Sim.Position(em, e).xz, order.Destination.xz) > .65f) continue;
                soldier.RecallState = 2; actor.Deployed = 0; actor.DeployAt = float.MaxValue; actor.Target = Entity.Null;
                em.SetComponentData(e, soldier); em.SetComponentData(e, actor); Order(em, e, default); em.SetComponentData(e, new VisualState());
            }
        }
        public static int SoldierBattleExperience(EntityManager em, Entity root, Entity unit)
        {
            var s = em.GetComponentData<Session>(root); var soldier = em.GetComponentData<Soldier>(unit);
            if (!Sim.Alive(em, unit) || s.NightKind == NightKind.Peaceful || soldier.LastExperienceTurn == s.Turn || em.GetComponentData<Combatant>(unit).Participated == 0) return 0;
            var growth = Sim.Definition(em, root, em.GetComponentData<Identity>(unit).Definition).SoldierGrowth;
            return math.min(growth.BattleExperience, math.max(0, LevelThreshold(growth, growth.MaxLevel) - soldier.Experience));
        }
        public static void ReportSoldierExperience(EntityManager em, Entity root)
        {
            using var all = Sim.OrderedEntities<Soldier>(em);
            foreach (var e in all)
            {
                int amount = SoldierBattleExperience(em, root, e); if (amount <= 0) continue; var id = em.GetComponentData<Identity>(e); bool exists = false;
                foreach (var entry in em.GetBuffer<BattleReportEntry>(root)) if (entry.Kind == EventKind.SoldierExperience && entry.Id == id.Id) exists = true;
                if (!exists) em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.SoldierExperience, Id = id.Id, Definition = id.Definition, Amount = amount, SourceName = id.Name });
            }
        }
    }
}
