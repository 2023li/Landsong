using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // Day transactions are deliberately ordered on the main thread. No MonoBehaviour owns economy state.
    public static class EconomyOps
    {
        public static bool Matches(Rule r, RuleKind kind, int level) => r.Kind == kind && (r.Level == 0 || r.Level == level);

        public static void Settle(EntityManager em, Entity root, bool forecast = false)
        {
            var session = em.GetComponentData<Session>(root);
            if (session.LastSettledTurn == session.Turn) return;
            session.LastSettledTurn = session.Turn; em.SetComponentData(root, session);
            EconomyJournalOps.Begin(em, root, forecast);
            using var buildings = Sim.OrderedEntities<Building>(em);
            // Stable, vertical building order; new construction cannot produce in this settlement.
            ReconcilePopulation(em, root);
            try
            {
            SocialOps.PayWages(em, root);
            foreach (var e in buildings)
            {
                var b = em.GetComponentData<Building>(e);
                if (b.Stage == LifeStage.Construction || b.Stage == LifeStage.Repairing) { Construct(em, root, e); continue; }
                if (b.Stage != LifeStage.Operational) continue;
                Maintain(em, root, e); Workforce(em, root, e); Produce(em, root, e);
                Residence(em, root, e); GainExperience(em, root, e); Offering(em, root, e);
            }
            ResourceNetworkOps.SettleMarkets(em, root);
            InventoryOps.ApplyLoss(em, root);
            ProgressionOps.Settle(em, root);
            ReconcilePopulation(em, root);
            MilitaryOps.ReconcileGarrisons(em, root);
            BuildingOps.Changed(em, root);
            }
            finally { EconomyJournalOps.End(em, root); }
        }

        static void Construct(EntityManager em, Entity root, Entity e)
        {
            var b = em.GetComponentData<Building>(e);
            var definition = em.GetComponentData<Identity>(e).Definition;
            var d = Sim.Definition(em, root, definition);
            var repair = b.Stage == LifeStage.Repairing;
            using var scope = EconomyJournalOps.For(em, root, e, repair ? EconomyReason.Repair : EconomyReason.Construction);
            if (repair)
            {
                if (!BuildingCostOps.PayRepairStep(em, root, e)) return;
            }
            else
            {
                var provider = ResourceNetworkOps.Provider(em, root, e);
                var hasCosts = false; for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (Matches(r, RuleKind.ConstructionCost, b.Progress + 1) && r.Amount > 0) hasCosts = true; }
                if (hasCosts && provider == Entity.Null) { EconomyJournalOps.Note(em, root, "断开资源连接，施工暂停"); return; }
                using var payment = new InventoryTransaction(em, root);
                if (!InventoryOps.Pay(em, root, definition, RuleKind.ConstructionCost, b.Progress + 1)) { payment.Reject("施工材料不足，本期未支付"); return; }
                for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (Matches(r, RuleKind.ConstructionOutput, b.Progress + 1) && InventoryOps.Add(em, root, r.Target, r.Amount) != r.Amount) { payment.Reject("施工产物放不下，本期材料与产出撤销"); return; } }
                payment.Commit();
                ResourceNetworkOps.PayRecord(em, root, provider, definition, RuleKind.ConstructionCost, b.Progress + 1);
                BuildingCostOps.RecordInvestment(em, e, BuildingCostOps.Rules(em, root, definition, RuleKind.ConstructionCost, b.Progress + 1));
            }
            b.Progress++;
            EconomyJournalOps.Note(em, root, repair ? "修复推进 1 回合（无需工人）" : "施工推进 1 回合");
            var duration = repair ? math.max(1, b.RepairDuration) : math.max(1, d.Duration);
            if (b.Progress >= duration)
            {
                b.Stage = LifeStage.Operational; b.Progress = 0;
                b.ProtectionUntil = em.GetComponentData<Session>(root).Turn + 2;
                if (repair)
                {
                    b.Workers = 0; b.Population = 0; b.Growth = 0; b.FoodFailures = 0; b.TaxProgress = 0;
                    b.RepairCompletedTurn = em.GetComponentData<Session>(root).Turn;
                    b.DeferredResidents = math.min(2, em.GetComponentData<BuildingStats>(e).MaxPopulation);
                    b.Maintained = 1; em.GetBuffer<FoodSelection>(e).Clear(); em.GetBuffer<RepairMaterial>(e).Clear();
                }
                var health = em.GetComponentData<Health>(e); health.Current = health.Maximum; em.SetComponentData(e, health);
            }
            em.SetComponentData(e, b);
            if (b.Stage == LifeStage.Operational) { BuildingOps.ApplyLevel(em, root, e, !repair); GridOps.Occupy(em, root, e); }
        }

        static void Maintain(EntityManager em, Entity root, Entity e)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Maintenance);
            var b = em.GetComponentData<Building>(e);
            var definition = em.GetComponentData<Identity>(e).Definition;
            b.Maintained = (byte)(InventoryOps.Pay(em, root, definition, RuleKind.Maintenance, b.Level) ? 1 : 0);
            if (b.Maintained == 0) EconomyJournalOps.Note(em, root, "维护材料不足，按配置影响吸引力和损耗");
            em.SetComponentData(e, b); InventoryOps.Provision(em, root, e);
        }

        public static bool WorkforceLocked(EntityManager em, ulong site)
        {
            using var expeditions = Sim.OrderedEntities<Expedition>(em);
            foreach (var e in expeditions) if (em.GetComponentData<Expedition>(e).Site == site && em.GetComponentData<Expedition>(e).Status == ExpeditionStatus.Travelling) return true;
            return false;
        }

        public static float Attraction(EntityManager em, Entity root, Entity e)
        {
            return math.clamp(WorkforceOps.NaturalAttraction(em, root, e), 0, 100);
        }

        static void Workforce(EntityManager em, Entity root, Entity e)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Workforce);
            var stats = em.GetComponentData<BuildingStats>(e); if (stats.JobCapacity <= 0) return;
            if (WorkforceLocked(em, em.GetComponentData<Identity>(e).Id)) { EconomyJournalOps.Note(em, root, "远征在途，岗位锁定"); return; }
            var b = em.GetComponentData<Building>(e); var quote = WorkforceOps.Quote(em, root, e); var attraction = quote.Natural;
            b.PaidSubsidy = 0; b.PaidSubsidyTurn = em.GetComponentData<Session>(root).Turn;
            if (quote.SubsidyCost > 0)
            {
                if (InventoryOps.Remove(em, root, quote.Gold, quote.SubsidyCost)) { b.PaidSubsidy = quote.SubsidyCost; attraction = quote.Planned; }
                else { Sim.Emit(em, root, EventKind.Message, "岗位补贴资金不足", em.GetComponentData<Identity>(e).Id, category: HistoryCategory.Economy); EconomyJournalOps.Note(em, root, "补贴资金不足，未支付"); }
            }
            b.StableWorkers = WorkforceOps.Stable(stats.JobCapacity, attraction);
            if (EconomyJournalOps.Forecast(em, root)) { em.SetComponentData(e, b); EconomyJournalOps.Note(em, root, "预测沿用当前工人，实际可能招入或流失"); InventoryOps.Provision(em, root, e); return; }
            var roll = Sim.NextRandom(em, root) % 100;
            if (b.Workers < b.StableWorkers && Sim.Population(em, root) > Sim.Employed(em))
            {
                var chance = math.clamp(40 + attraction * .4f + (b.StableWorkers - b.Workers) * 30f / stats.JobCapacity, 20, 95);
                if (roll < chance) b.Workers++;
            }
            else if (b.Workers > b.StableWorkers && em.GetComponentData<Session>(root).Turn > b.ProtectionUntil)
            {
                var chance = math.clamp((b.Workers - b.StableWorkers) * 60f / stats.JobCapacity + (100 - attraction) * .2f, 5, 70);
                if (roll < chance) b.Workers--;
            }
            em.SetComponentData(e, b);
            InventoryOps.Provision(em, root, e);
        }

        public static ResultCode ChangeWorkers(EntityManager em, Entity root, Entity e, int amount)
        {
            if (!Sim.Operational(em, e)) return ResultCode.InvalidTarget;
            var q = WorkforceOps.Quote(em, root, e); var result = WorkforceOps.CanChange(q, amount); if (result != ResultCode.Success) return result;
            var b = em.GetComponentData<Building>(e);
            if (amount > 0 && !InventoryOps.Remove(em, root, q.Gold, checked(q.RecruitCost * amount))) return ResultCode.InsufficientResources;
            b.Workers += amount; em.SetComponentData(e, b); InventoryOps.Provision(em, root, e); BuildingOps.Changed(em, root); return ResultCode.Success;
        }

        public static void ReconcilePopulation(EntityManager em, Entity root)
        {
            var excess = Sim.Employed(em) - Sim.Population(em, root);
            using var buildings = Sim.OrderedEntities<Building>(em);
            var ordered = new System.Collections.Generic.List<Entity>(); foreach (var e in buildings) if (Sim.Operational(em, e)) ordered.Add(e);
            ordered.Sort((a, b) => { var comparison = Attraction(em, root, a).CompareTo(Attraction(em, root, b)); return comparison != 0 ? comparison : em.GetComponentData<Identity>(a).Id.CompareTo(em.GetComponentData<Identity>(b).Id); });
            // Housing losses release ordinary jobs, never named soldiers/heroes.
            foreach (var e in ordered)
            {
                if (excess <= 0) break;
                var b = em.GetComponentData<Building>(e); var remove = math.min(excess, b.Workers);
                b.Workers -= remove; excess -= remove; em.SetComponentData(e, b); InventoryOps.Provision(em, root, e);
            }
        }

        static void Produce(EntityManager em, Entity root, Entity e)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Production);
            var b = em.GetComponentData<Building>(e); var id = em.GetComponentData<Identity>(e); var d = Sim.Definition(em, root, id.Definition);
            var production = Sim.Rule(em, root, id.Definition, RuleKind.Production, b.Level);
            for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (Matches(r, RuleKind.ProcessingTier, b.Level) && b.Workers >= r.B) production.Amount = r.Amount; }
            if (production.Level >= 0 && b.Workers >= production.B)
            {
                b.ProductionProgress = math.min(math.max(1, production.Amount), b.ProductionProgress + 1);
                if (b.ProductionProgress >= math.max(1, production.Amount))
                {
                    // Input and output commit together; full storage never consumes inputs or advances the cycle.
                    using var recipe = new InventoryTransaction(em, root);
                    var provider = ResourceNetworkOps.Provider(em, root, e);
                    var hasInputs = false; for (var i = 0; i < d.RuleCount; i++) if (Matches(Sim.GetRule(em, root, d.RuleStart + i), RuleKind.Input, b.Level)) hasInputs = true;
                    var success = (!hasInputs || provider != Entity.Null) && InventoryOps.Pay(em, root, id.Definition, RuleKind.Input, b.Level);
                    var failure = hasInputs && provider == Entity.Null ? "断开资源连接，生产暂停" : "原料不足，配方未支付";
                    for (var i = 0; i < d.RuleCount && success; i++)
                    {
                        var r = Sim.GetRule(em, root, d.RuleStart + i);
                        if (!Matches(r, RuleKind.ProductionTier, b.Level) || b.Workers < r.B || (r.C > 0 && b.Workers > r.C)) continue;
                        var flat = EffectOps.FlatProduction(em, root, r.Target, em.GetComponentData<Identity>(e).Definition);
                        var amount = (int)math.max(0, math.floor((r.Amount + flat) * math.max(0, 1 + EffectOps.Modifier(em, root, RuleKind.ProductionBonus, r.Target) + SpatialValue(em, root, e, 10) / 100f)));
                        success = InventoryOps.Add(em, root, r.Target, amount) == amount;
                        if (!success) failure = "产品放不下，整份配方的原料与产出撤销";
                    }
                    if (success) { recipe.Commit(); b.ProductionProgress = 0; if (hasInputs) ResourceNetworkOps.PayRecord(em, root, provider, id.Definition, RuleKind.Input, b.Level); }
                    else recipe.Reject(new FixedString128Bytes(failure));
                }
                else EconomyJournalOps.Note(em, root, "生产周期尚未到期");
            }
            else if (production.Level >= 0) EconomyJournalOps.Note(em, root, "工人不足，生产暂停");
            if (b.Crop >= 0)
            {
                var crop = Sim.Definition(em, root, b.Crop);
                if (b.Workers < crop.Capacity) b.CropFullCycle = 0;
                if (b.Workers >= crop.Population) b.CropProgress = math.min(crop.Duration, b.CropProgress + 1);
                if (b.CropProgress >= crop.Duration && b.AutoHarvest != 0)
                {
                    em.SetComponentData(e, b); HarvestCrop(em, root, e, true); b = em.GetComponentData<Building>(e);
                }
            }
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Level > 0 && r.Level != b.Level) continue;
                if (r.Kind == RuleKind.ResearchOutput) { var s = em.GetComponentData<Session>(root); s.ResearchPoints += r.Amount; em.SetComponentData(root, s); }
                if (r.Kind == RuleKind.RareOutput && b.Workers >= r.B)
                {
                    if (EconomyJournalOps.Forecast(em, root)) EconomyJournalOps.Record(em, root, r.Target, 0, note: new FixedString128Bytes($"随机产出概率 {r.Value:P0}，数量 {r.Amount}（不计入预计净额）"));
                    else if (Sim.NextRandom(em, root) % 10000 < r.Value * 10000) InventoryOps.Add(em, root, r.Target, r.Amount);
                }
            }
            em.SetComponentData(e, b);
        }

        static void GainExperience(EntityManager em, Entity root, Entity e)
        {
            var b = em.GetComponentData<Building>(e); var d = Sim.Definition(em, root, em.GetComponentData<Identity>(e).Definition);
            var population = em.GetComponentData<BuildingStats>(e).MaxPopulation;
            if (b.Maintained == 0 || population > 0 && b.Population < population) return;
            for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (Matches(r, RuleKind.Experience, b.Level) && b.Workers >= r.C) b.Experience += r.Amount; }
            em.SetComponentData(e, b);
        }
        static void Residence(EntityManager em, Entity root, Entity e)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Food);
            var b = em.GetComponentData<Building>(e); var stats = em.GetComponentData<BuildingStats>(e);
            if (stats.MaxPopulation <= 0) return;
            var definition = em.GetComponentData<Identity>(e).Definition; var d = Sim.Definition(em, root, definition);
            var residence = Sim.Rule(em, root, definition, RuleKind.Residence, b.Level);
            var fed = ResidentialFoodOps.Pay(em, root, e); var environment = true;
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Level > 0 && r.Level != b.Level) continue;
                if (r.Kind == RuleKind.Environment && SpatialValue(em, root, e, r.B) < r.Amount) environment = false;
            }
            if (!fed)
            {
                EconomyJournalOps.Note(em, root, "完整食谱不足，整栋不扣食物");
                b.Growth = 0; b.TaxProgress = 0; b.FoodFailures++;
                if (b.FoodFailures >= math.max(1, residence.C)) { b.Population = math.max(0, b.Population - 1); b.FoodFailures = 0; }
            }
            else
            {
                b.FoodFailures = 0;
                if (b.Population < stats.MaxPopulation)
                {
                    if (!environment) EconomyJournalOps.Note(em, root, "食物已满足，但环境不足，人口不增长");
                    b.TaxProgress = 0;
                    if (environment && ++b.Growth >= math.max(1, (int)residence.Value)) { b.Population++; b.Growth = 0; }
                }
                else
                {
                    b.Growth = 0; var tax = Sim.Rule(em, root, definition, RuleKind.Tax, b.Level);
                    if (tax.Level >= 0 && ++b.TaxProgress >= math.max(1, tax.B))
                    {
                        using var taxScope = EconomyJournalOps.For(em, root, e, EconomyReason.Tax);
                        using var storage = new InventoryTransaction(em, root);
                        if (InventoryOps.Add(em, root, tax.Target, b.Population * math.max(1, tax.Amount)) == b.Population * math.max(1, tax.Amount)) { b.TaxProgress = 0; storage.Commit(); }
                        else storage.Reject("税收放不下，本期税收未入库");
                    }
                }
            }
            em.SetComponentData(e, b);
        }

        public static ResultCode HarvestCrop(EntityManager em, Entity root, Entity e, bool automatic)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Crop);
            var b = em.GetComponentData<Building>(e); if (b.Crop < 0) return ResultCode.Unavailable;
            var crop = Sim.Definition(em, root, b.Crop); if (b.CropProgress < crop.Duration) return ResultCode.Unavailable;
            if (EconomyJournalOps.Forecast(em, root))
            {
                var random = false;
                for (var i = 0; i < crop.RuleCount; i++) { var r = Sim.GetRule(em, root, crop.RuleStart + i); if (r.Kind == RuleKind.RewardItem && r.B > r.Amount) { random = true; EconomyJournalOps.Record(em, root, r.Target, 0, note: new FixedString128Bytes($"基础收获区间 {r.Amount}～{r.B}，另受加成影响；本次未计入")); } }
                if (random) { EconomyJournalOps.Note(em, root, "随机收获及其费用未计入参考净额"); return ResultCode.Unavailable; }
            }
            using var harvest = new InventoryTransaction(em, root);
            if (automatic && !InventoryOps.Pay(em, root, b.Crop, RuleKind.Maintenance, 1)) { harvest.Reject("自动收获费用不足"); return ResultCode.InsufficientResources; }
            var rng = new Random(math.max(1u, b.CropSeed));
            var bonus = (b.CropFullCycle != 0 ? crop.Value : 0) + SpatialValue(em, root, e, 10);
            for (var i = 0; i < crop.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, crop.RuleStart + i); if (r.Kind != RuleKind.RewardItem) continue;
                var amount = (int)math.floor(rng.NextInt(r.Amount, math.max(r.Amount, r.B) + 1) * math.max(0, 1 + bonus / 100f + EffectOps.Modifier(em, root, RuleKind.ProductionBonus, r.Target) + EffectOps.Modifier(em, root, RuleKind.CropHarvestBonus, r.Target)));
                if (InventoryOps.Add(em, root, r.Target, amount) != amount) { harvest.Reject("收获放不下，全部产出与费用撤销"); return ResultCode.NoCapacity; }
            }
            harvest.Commit();
            b.Crop = -1; b.CropProgress = 0; em.SetComponentData(e, b); return ResultCode.Success;
        }

        public static float SpatialValue(EntityManager em, Entity root, Entity target, int kind)
        {
            return SpatialOps.Quote(em, root, target, kind).Value;
        }

        static void Offering(EntityManager em, Entity root, Entity e)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Offering);
            var b = em.GetComponentData<Building>(e); if (b.Offering == 0) return;
            var stats = em.GetComponentData<BuildingStats>(e); if (stats.HeroDefinition < 0) return;
            if (b.Workers < stats.RequiredWorkers) { EconomyJournalOps.Note(em, root, "工人未达到供奉要求，本次未支付"); return; }
            using var heroes = Sim.OrderedEntities<Hero>(em); var id = em.GetComponentData<Identity>(e).Id;
            foreach (var hero in heroes)
            {
                var h = em.GetComponentData<Hero>(hero); if (h.Sanctum != id || h.Recruited == 0) continue;
                if (!InventoryOps.Pay(em, root, stats.HeroDefinition, RuleKind.Supply, 1))
                { b.Offering = 0; Sim.Emit(em, root, EventKind.Message, "金币不足，持续供奉已中断", id, category: HistoryCategory.Economy); EconomyJournalOps.Note(em, root, "供奉资金不足，持续供奉中断"); }
                else
                {
                    b.PaidOfferingTurn = em.GetComponentData<Session>(root).Turn; var growth = Sim.Definition(em, root, stats.HeroDefinition).HeroGrowth;
                    int before = h.Experience; h.Experience = MilitaryOps.AddHeroExperience(growth, h.Experience, growth.OfferingExperience); em.SetComponentData(hero, h);
                    var heroId = em.GetComponentData<Identity>(hero);
                    em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.HeroOfferingExperience, Id = heroId.Id, Definition = heroId.Definition, Amount = h.Experience - before, SourceName = heroId.Name });
                    foreach (var cost in BuildingCostOps.Rules(em, root, stats.HeroDefinition, RuleKind.Supply, 1))
                        em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.HeroOfferingCost, Id = heroId.Id, Definition = cost.Item, Amount = cost.Amount, SourceName = heroId.Name });
                }
                em.SetComponentData(e, b); break;
            }
        }
    }
}
