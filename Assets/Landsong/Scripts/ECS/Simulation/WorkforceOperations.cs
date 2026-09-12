using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // Ephemeral read models, not components or a second owner of workforce state.
    public struct AttractionSource { public ulong Building; public int Definition; public float Value; public string Label; }
    public sealed class WorkforceQuote
    {
        public int Capacity, Workers, Target, NaturalStable, CurrentStable, PlannedStable, FreePopulation;
        public int Gold, Stock, SubsidyCost, Paid, PaidTurn, RecruitCost, ProtectionTurns;
        public float Raw, Natural, Current, Planned, Threshold, PerGold;
        public bool Locked, Operational;
        public readonly List<AttractionSource> Sources = new List<AttractionSource>();
    }
    public static class WorkforceOps
    {
        public static int Stable(int capacity, float attraction) => capacity <= 0 ? 0 : (int)Math.Min(capacity, Math.Floor((double)math.clamp(attraction, 0, 100) * (capacity + 1d) / 100d));
        public static float Threshold(int capacity, int target) => capacity <= 0 ? 0 : (float)(math.clamp(target, 0, capacity) * 100d / (capacity + 1d));
        public static int SubsidyCost(int capacity, float attraction, int target)
        {
            if (capacity <= 0) return 0;
            // Resolve the minimum integer cost against the exact same effective-attraction calculation
            // used by payment and recruitment. A separate epsilon/ceil can disagree near a worker threshold.
            attraction = math.clamp(attraction, 0, 100); target = math.clamp(target, 0, capacity);
            var low = 0; var high = capacity; var perGold = 100f / capacity;
            while (low < high) { var mid = low + (high - low) / 2; if (Stable(capacity, attraction + mid * perGold) >= target) high = mid; else low = mid + 1; }
            return low;
        }
        public static float NaturalAttraction(EntityManager em, Entity root, Entity entity, List<AttractionSource> sources = null)
        {
            var b = em.GetComponentData<Building>(entity); var definition = em.GetComponentData<Identity>(entity).Definition;
            var rule = Sim.Rule(em, root, definition, RuleKind.Workforce, b.Level); var value = rule.Value;
            sources?.Add(new AttractionSource { Definition = definition, Label = "基础吸引力", Value = value });
            var expeditionPenalty = ExpeditionOps.Penalty(em, root);
            if (expeditionPenalty > 0) { value -= expeditionPenalty; sources?.Add(new AttractionSource { Definition = -1, Label = "远征抚恤不足", Value = -expeditionPenalty }); }
            value += EffectOps.Modifier(em, root, RuleKind.Attraction, definition, sources);
            var nearby = Sim.Rule(em, root, definition, RuleKind.Attraction, b.Level);
            if (nearby.Level >= 0)
            {
                using var all = Sim.OrderedEntities<Building>(em);
                foreach (var other in all) if (Sim.Operational(em, other) && math.distance(Sim.Position(em, other), Sim.Position(em, entity)) <= nearby.Value)
                {
                    var amount = em.GetComponentData<Building>(other).Population * nearby.Extra; value += amount;
                    if (amount != 0) sources?.Add(new AttractionSource { Building = em.GetComponentData<Identity>(other).Id, Definition = -1, Label = "附近居民", Value = amount });
                }
            }
            var storage = Sim.Rule(em, root, definition, RuleKind.StorageCondition, b.Level);
            if (b.Maintained == 0 && storage.Level >= 0) { value -= storage.Value; sources?.Add(new AttractionSource { Definition = definition, Label = "维护未满足", Value = -storage.Value }); }
            return value;
        }
        public static WorkforceQuote Quote(EntityManager em, Entity root, Entity entity, int? target = null)
        {
            var b = em.GetComponentData<Building>(entity); var definition = em.GetComponentData<Identity>(entity).Definition;
            var q = new WorkforceQuote { Capacity = em.GetComponentData<BuildingStats>(entity).JobCapacity, Workers = b.Workers, Paid = b.PaidSubsidy, PaidTurn = b.PaidSubsidyTurn, Operational = Sim.Operational(em, entity), Locked = EconomyOps.WorkforceLocked(em, em.GetComponentData<Identity>(entity).Id) };
            var rule = Sim.Rule(em, root, definition, RuleKind.Workforce, b.Level);
            q.Gold = rule.Target >= 0 ? rule.Target : em.GetComponentData<GameSettings>(root).Gold;
            q.Stock = InventoryOps.Count(em, root, q.Gold); q.FreePopulation = math.max(0, Sim.Population(em, root) - Sim.Employed(em));
            q.Raw = NaturalAttraction(em, root, entity, q.Sources); q.Natural = math.clamp(q.Raw, 0, 100); q.NaturalStable = Stable(q.Capacity, q.Natural);
            q.PerGold = q.Capacity <= 0 ? 0 : 100f / q.Capacity;
            q.SubsidyCost = target.HasValue ? SubsidyCost(q.Capacity,q.Natural,target.Value) : math.clamp(b.SubsidyBudget,0,q.Capacity);
            q.Planned = math.clamp(q.Natural + q.SubsidyCost * q.PerGold, 0, 100); q.PlannedStable = Stable(q.Capacity, q.Planned);
            q.Target=q.PlannedStable;q.Threshold=Threshold(q.Capacity,q.Target);
            q.Current = math.clamp(q.Natural + q.Paid * q.PerGold, 0, 100); q.CurrentStable = Stable(q.Capacity, q.Current);
            q.RecruitCost = (int)Math.Ceiling((decimal)math.max(0, rule.Extra) * (1 + (100 - (decimal)q.Current) / 100));
            q.ProtectionTurns = math.max(0, b.ProtectionUntil - em.GetComponentData<Session>(root).Turn + 1);
            return q;
        }
        public static ResultCode SetTarget(EntityManager em, Entity root, Entity entity, int target)
        {
            if (!Sim.Operational(em, entity) || em.GetComponentData<BuildingStats>(entity).JobCapacity <= 0) return ResultCode.InvalidTarget;
            if (EconomyOps.WorkforceLocked(em, em.GetComponentData<Identity>(entity).Id)) return ResultCode.Busy;
            var b = em.GetComponentData<Building>(entity); var q = Quote(em, root, entity, target);
            if (target < 0 || target > q.Capacity) return ResultCode.InvalidTarget;
            b.WorkerTarget = q.Target;b.SubsidyBudget=q.SubsidyCost; b.Subsidy = (byte)(q.SubsidyCost>0 ? 1 : 0); em.SetComponentData(entity, b); return ResultCode.Success;
        }
        public static ResultCode SetBudget(EntityManager em,Entity root,Entity entity,int budget)
        {
            if(!Sim.Operational(em,entity)||em.GetComponentData<BuildingStats>(entity).JobCapacity<=0)return ResultCode.InvalidTarget;
            if(EconomyOps.WorkforceLocked(em,em.GetComponentData<Identity>(entity).Id))return ResultCode.Busy;
            int capacity=em.GetComponentData<BuildingStats>(entity).JobCapacity;if(budget<0||budget>capacity)return ResultCode.InvalidTarget;
            var b=em.GetComponentData<Building>(entity);b.SubsidyBudget=budget;b.Subsidy=(byte)(budget>0?1:0);em.SetComponentData(entity,b);return ResultCode.Success;
        }
        public static ResultCode AdjustBudget(EntityManager em,Entity root,Entity entity,int delta)
        {
            if(!Sim.Operational(em,entity)||(delta!=1&&delta!=-1))return ResultCode.InvalidTarget;
            return SetBudget(em,root,entity,em.GetComponentData<Building>(entity).SubsidyBudget+delta);
        }
        public static ResultCode CanChange(WorkforceQuote q, int amount)
        {
            if (!q.Operational || q.Capacity <= 0 || amount == 0) return ResultCode.InvalidTarget;
            if (q.Locked) return ResultCode.Busy;
            if (amount < 0) return -(long)amount <= q.Workers ? ResultCode.Success : ResultCode.InvalidTarget;
            if ((long)q.Workers + amount > math.min(q.Capacity, q.CurrentStable)) return ResultCode.NoCapacity;
            if (amount > q.FreePopulation) return ResultCode.InsufficientPopulation;
            return (long)q.RecruitCost * amount <= q.Stock ? ResultCode.Success : ResultCode.InsufficientResources;
        }
        public static string Reason(WorkforceQuote q, int amount)
        {
            if (!q.Operational) return "建筑未运营"; if (q.Locked) return "远征在途，岗位与补贴锁定";
            switch (CanChange(q, amount))
            {
                case ResultCode.Success: return "可执行";
                case ResultCode.NoCapacity: return "超过当前可稳定人数；目标补贴未付款不会增加招工额度";
                case ResultCode.InsufficientPopulation: return "空闲人口不足（士兵/英雄占用的人口不挪用）";
                case ResultCode.InsufficientResources: return "正常库存不足；待存放不能支付招聘费用";
                default: return "人数超出可操作范围";
            }
        }
    }
}
