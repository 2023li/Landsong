using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public sealed class ExpeditionQuote
    {
        public ResultCode Code; public string Reason = "", Stamp = ""; public bool Visible = true;
        public int Minimum, Maximum, Workers, StableWorkers, Crew, Arrival, Casualties, Subsidy, MissingSubsidy;
        public float SuccessChance, RewardBonus;
        public readonly List<Rule> Options = new List<Rule>();
        public readonly List<ExpeditionSupply> Supplies = new List<ExpeditionSupply>();
        public List<BuildingCost> Costs = new List<BuildingCost>(), Rewards = new List<BuildingCost>();
        public ExpeditionQuote Fail(ResultCode code, string reason) { Code = code; Reason = reason; return this; }
    }
    public static class ExpeditionOps
    {
        public static List<BuildingCost> Rewards(EntityManager em, Entity root, int definition, float bonus)
        {
            var result = new List<BuildingCost>();
            foreach (var r in BuildingCostOps.Rules(em, root, definition, RuleKind.RewardItem, 1)) result.Add(new BuildingCost(r.Item, (int)math.floor(r.Amount * (1 + bonus))));
            return result;
        }
        public static int SupplyMaximum(Rule r) => r.Amount + math.min(r.Amount / 2, r.B > 0 ? r.B : r.Amount / 2);
        public static bool CompletedAt(EntityManager em, Entity site, int definition)
        {
            if (!em.HasBuffer<ExpeditionDestinationHistory>(site)) return false;
            foreach (var entry in em.GetBuffer<ExpeditionDestinationHistory>(site)) if (entry.Definition == definition) return true;
            return false;
        }
        public static ExpeditionQuote Quote(EntityManager em, Entity root, Entity site, int definition, int crew, int[] amounts = null)
        {
            var q = new ExpeditionQuote { Crew = crew };
            if (!Sim.ValidDefinition(em, root, definition) || Sim.Definition(em, root, definition).Kind != ContentKind.Expedition) return q.Fail(ResultCode.InvalidContent, "目的地不存在");
            var d = Sim.Definition(em, root, definition);
            for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind == RuleKind.VisiblePrerequisite && !Sim.HasGrant(em, root, r.Target, math.max(1, r.Amount))) q.Visible = false; if (r.Kind == RuleKind.Supply) q.Options.Add(r); }
            if (site == Entity.Null || !em.HasComponent<Building>(site)) return q.Fail(ResultCode.InvalidTarget, "请选择远征所");
            var b = em.GetComponentData<Building>(site); var id = em.GetComponentData<Identity>(site);
            var rule = Sim.Rule(em, root, id.Definition, RuleKind.ExpeditionSite, b.Level);
            q.Workers = b.Workers; q.StableWorkers = b.StableWorkers; q.Minimum = math.max(rule.Amount, d.Population);
            q.Maximum = math.min(math.min(b.Workers, b.StableWorkers), rule.B); if (d.Capacity > 0) q.Maximum = math.min(q.Maximum, d.Capacity);
            var s = em.GetComponentData<Session>(root); q.Arrival = s.Turn + d.Duration;
            q.SuccessChance = d.Chance + d.Interval * crew; q.RewardBonus = crew >= rule.B && rule.B > 0 ? rule.Value : 0;
            q.Casualties = math.clamp((int)math.round(crew * d.Loss), d.Loss > 0 && crew > 0 ? 1 : 0, math.max(0, crew)); q.Subsidy = d.Cost + math.max(0, crew) * d.Value;
            q.MissingSubsidy = math.max(0, q.Subsidy - InventoryOps.Count(em, root, em.GetComponentData<GameSettings>(root).Gold));
            q.Costs = BuildingCostOps.Rules(em, root, definition, RuleKind.PlacementCost, 1);
            if (amounts != null && amounts.Length != q.Options.Count) return q.Fail(ResultCode.InvalidContent, "补给项目已变化，请重新确认");
            for (var i = 0; i < q.Options.Count; i++)
            {
                var option = q.Options[i]; var amount = amounts == null ? option.Amount : amounts[i];
                if (amount < option.Amount || amount > SupplyMaximum(option)) return q.Fail(ResultCode.InvalidContent, "补给数量超出允许范围");
                q.Supplies.Add(new ExpeditionSupply { Item = option.Target, Amount = amount });
                q.Costs.Add(new BuildingCost { Item = option.Target, Amount = amount });
                q.SuccessChance += (amount - option.Amount) * option.Value; q.RewardBonus += (amount - option.Amount) * option.Extra;
            }
            q.SuccessChance = math.clamp(q.SuccessChance, 0, d.Range);
            q.Rewards = Rewards(em, root, definition, q.RewardBonus);
            // Read-only consent binds inventory, site workforce/level, chosen supplies and turn. No RNG in previews.
            var stamp = InventoryOps.Fingerprint(em, root) + ":" + id.Id + ":" + b.Level + ":" + b.Workers + ":" + b.StableWorkers + ":" + b.Stage + ":" + b.Maintained + ":" + s.Turn + ":" + definition + ":" + crew;
            foreach (var supply in q.Supplies) stamp += ":" + supply.Amount;
            ulong hash = 14695981039346656037UL; foreach (var ch in stamp) { hash ^= ch; hash *= 1099511628211UL; } q.Stamp = hash.ToString("X16");
            if (!FeatureOps.Unlocked(em, root, "Expedition")) return q.Fail(ResultCode.Unavailable, "远征许可尚未解锁");
            if (s.Phase != Phase.Day || s.CheckpointPending != 0) return q.Fail(ResultCode.WrongPhase, "只能在可操作的白天派遣");
            if (!q.Visible || !ProgressionOps.Prerequisites(em, root, definition) || (d.Flags & 1) == 0 && CompletedAt(em, site, definition)) return q.Fail(ResultCode.Unavailable, "目的地条件未满足，或此驻地已成功完成不可重复的目的地");
            if (!Sim.Operational(em, site) || b.Maintained == 0 || rule.Level < 0 || b.Level < d.Level) return q.Fail(ResultCode.Unavailable, "需要正常运营、维护满足且等级足够的远征所");
            if (EconomyOps.WorkforceLocked(em, id.Id)) return q.Fail(ResultCode.Busy, "该远征所已有队伍在途");
            if (crew < q.Minimum || crew > q.Maximum) return q.Fail(ResultCode.InsufficientPopulation, "需要 " + q.Minimum + "～" + q.Maximum + " 人，受当前工人和稳定岗位限制");
            if (!BuildingCostOps.CanPay(em, root, Aggregate(q.Costs))) return q.Fail(ResultCode.InsufficientResources, "出发物资不足");
            q.Code = ResultCode.Success; return q;
        }
        static List<BuildingCost> Aggregate(List<BuildingCost> costs)
        {
            var totals = new Dictionary<int, int>(); foreach (var c in costs) { totals.TryGetValue(c.Item, out var n); totals[c.Item] = checked(n + c.Amount); }
            var result = new List<BuildingCost>(); foreach (var c in totals) if (c.Value > 0) result.Add(new BuildingCost { Item = c.Key, Amount = c.Value }); return result;
        }
        public static string Payload(ExpeditionQuote quote) { var amounts = new List<string>(); foreach (var s in quote.Supplies) amounts.Add(s.Amount.ToString()); return string.Join(",", amounts) + "|" + quote.Stamp; }
        public static ResultCode Command(EntityManager em, Entity root, Command command)
        {
            var s = em.GetComponentData<Session>(root); if (s.Phase != Phase.Day || s.CheckpointPending != 0) return ResultCode.WrongPhase;
            if (!FeatureOps.Unlocked(em, root, "Expedition")) return ResultCode.Unavailable;
            var e = Sim.Find(em, command.Target);
            if (command.Kind == CommandKind.StartExpedition)
            {
                var payload = command.Text.ToString().Split('|'); if (payload.Length != 2) return ResultCode.ConfirmationRequired;
                var parts = payload[0].Length == 0 ? Array.Empty<string>() : payload[0].Split(','); var amounts = new int[parts.Length];
                for (var i = 0; i < parts.Length; i++) if (!int.TryParse(parts[i], out amounts[i])) return ResultCode.InvalidContent;
                var q = Quote(em, root, e, command.Definition, command.Amount, amounts); if (q.Code != ResultCode.Success) return q.Code;
                if (q.Stamp != payload[1]) return ResultCode.Unavailable;
                if (command.Other != 0 && !CourtOps.AvailableCaptain(em, root, Sim.Find(em, command.Other))) return ResultCode.Unavailable;
                if (!BuildingCostOps.Pay(em, root, Aggregate(q.Costs))) return ResultCode.InsufficientResources;
                var journey = Sim.Spawn(em, root, command.Definition, default, true);
                Sim.Set(em, journey, new Expedition { Site = command.Target, Captain = command.Other, SourceName = em.GetComponentData<Identity>(e).Name, Crew = q.Crew, Departure = s.Turn, Arrival = q.Arrival, SourceLevel = em.GetComponentData<Building>(e).Level, SuccessChance = q.SuccessChance, RewardBonus = q.RewardBonus, SubsidyRequired = q.Subsidy });
                Sim.Buffer<ExpeditionSupply>(em, journey); foreach (var supply in q.Supplies) em.GetBuffer<ExpeditionSupply>(journey).Add(supply);
                PersonRequestOps.Departed(em,root,journey);
                return ResultCode.Success;
            }
            if (e == Entity.Null || !em.HasComponent<Expedition>(e)) return ResultCode.InvalidTarget;
            var state = em.GetComponentData<Expedition>(e);
            if (command.Kind == CommandKind.ClaimExpedition)
            {
                if (state.Status == ExpeditionStatus.Travelling) return ResultCode.Busy;
                var definition = em.GetComponentData<Identity>(e).Definition;
                if (state.Status == ExpeditionStatus.Success && !ProgressionOps.Reward(em, root, definition, 1 + state.RewardBonus)) return ResultCode.NoCapacity;
                if (state.Status == ExpeditionStatus.Success) Sim.Grant(em, root, definition);
            }
            else if (command.Kind != CommandKind.AbandonExpedition) return ResultCode.InvalidContent;
            if(state.Status==ExpeditionStatus.Travelling)PersonRequestOps.Returned(em,root,e,false);
            em.DestroyEntity(e); return ResultCode.Success;
        }
        public static float Penalty(EntityManager em, Entity root)
        {
            if (!FeatureOps.Unlocked(em, root, "Expedition")) return 0;
            var s = em.GetComponentData<Session>(root); return s.Turn <= s.ExpeditionPenaltyUntil ? s.ExpeditionPenaltyStacks * em.GetComponentData<ContentCatalog>(root).Value.Value.Expeditions.AttractionPerStack : 0;
        }
        public static void Settle(EntityManager em, Entity root)
        {
            if (!FeatureOps.Unlocked(em, root, "Expedition"))
            {
                using var frozen = Sim.OrderedEntities<Expedition>(em);
                foreach (var e in frozen) { var j = em.GetComponentData<Expedition>(e); if (j.Status == ExpeditionStatus.Travelling) { j.Arrival++; em.SetComponentData(e, j); } }
                var state = em.GetComponentData<Session>(root); if (state.ExpeditionPenaltyStacks > 0 && state.ExpeditionPenaltyUntil >= state.Turn) { state.ExpeditionPenaltyUntil++; em.SetComponentData(root, state); }
                return;
            }
            var s = em.GetComponentData<Session>(root);
            if (s.Turn > s.ExpeditionPenaltyUntil) { s.ExpeditionPenaltyStacks = 0; s.ExpeditionPenaltyUntil = 0; em.SetComponentData(root, s); }
            using var journeys = Sim.OrderedEntities<Expedition>(em);
            foreach (var e in journeys)
            {
                var expedition = em.GetComponentData<Expedition>(e); if (expedition.Status != ExpeditionStatus.Travelling) continue;
                var site = Sim.Find(em, expedition.Site);
                if (!Sim.Operational(em, site)) { PersonRequestOps.Returned(em,root,e,false);em.DestroyEntity(e); Sim.Emit(em, root, EventKind.Message, "远征所失效，队伍撤回；出发物资不返还", expedition.Site); continue; }
                if (s.Turn + 1 < expedition.Arrival) continue;
                using var scope = EconomyJournalOps.For(em, root, site, EconomyReason.Expedition);
                if (EconomyJournalOps.Forecast(em, root)) { EconomyJournalOps.Note(em, root, "远征将抵达，成败、伤亡和抚恤不计入参考预测"); continue; }
                var d = Sim.Definition(em, root, em.GetComponentData<Identity>(e).Definition);
                var success = Sim.NextRandom(em, root) % 10000 < expedition.SuccessChance * 10000;
                expedition.Status = success ? ExpeditionStatus.Success : ExpeditionStatus.Failure;
                if (expedition.Captain != 0) CourtOps.Influence(em, root, Sim.Find(em, expedition.Captain), success ? 10 : -5, success ? "远征成功" : "远征失败");
                if (success)
                {
                    var b = em.GetComponentData<Building>(site); b.Experience++; em.SetComponentData(site, b);
                    var definition = em.GetComponentData<Identity>(e).Definition;
                    if (!CompletedAt(em, site, definition)) { Sim.Buffer<ExpeditionDestinationHistory>(em, site); em.GetBuffer<ExpeditionDestinationHistory>(site).Add(new ExpeditionDestinationHistory { Definition = definition }); }
                }
                else
                {
                    expedition.Casualties = math.clamp((int)math.round(expedition.Crew * d.Loss), d.Loss > 0 ? 1 : 0, expedition.Crew);
                    var b = em.GetComponentData<Building>(site); b.Workers = math.max(0, b.Workers - expedition.Casualties); em.SetComponentData(site, b);
                    ProgressionOps.RemovePopulation(em, root, expedition.Casualties);
                    var gold = em.GetComponentData<GameSettings>(root).Gold; expedition.SubsidyPaid = math.min(expedition.SubsidyRequired, InventoryOps.Count(em, root, gold)); InventoryOps.Remove(em, root, gold, expedition.SubsidyPaid);
                    var missing = expedition.SubsidyRequired - expedition.SubsidyPaid;
                    if (missing > 0)
                    {
                        expedition.PenaltyStacks = (missing + 9) / 10; var session = em.GetComponentData<Session>(root); session.ExpeditionPenaltyStacks += expedition.PenaltyStacks;
                        session.ExpeditionPenaltyUntil = math.max(session.ExpeditionPenaltyUntil, s.Turn + em.GetComponentData<ContentCatalog>(root).Value.Value.Expeditions.PenaltyTurns - 1); em.SetComponentData(root, session);
                        Sim.Emit(em, root, EventKind.Message, "远征抚恤不足，全局岗位吸引力下降", expedition.Site);
                    }
                }
                em.SetComponentData(e, expedition); Sim.Emit(em, root, EventKind.Message, success ? "远征归来，可领取奖励" : "远征归来，请查看伤亡及抚恤", em.GetComponentData<Identity>(e).Id);
                PersonRequestOps.Returned(em,root,e,true);
            }
        }
        public static void WithdrawFromSite(EntityManager em, Entity root, ulong site)
        {
            using var journeys = Sim.OrderedEntities<Expedition>(em);
            foreach (var e in journeys) { var j = em.GetComponentData<Expedition>(e); if (j.Site != site || j.Status != ExpeditionStatus.Travelling) continue;PersonRequestOps.Returned(em,root,e,false); em.DestroyEntity(e); Sim.Emit(em, root, EventKind.Message, "远征所失效，队伍撤回；出发物资不返还", site); }
        }
    }
}
