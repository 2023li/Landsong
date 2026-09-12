using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public sealed class QuestOfferQuote
    {
        public ResultCode Code; public string Reason; public int Strength, Type, Wait; public ulong Offer;
        public readonly List<int> Candidates = new List<int>();
        public readonly List<double> Weights = new List<double>();
        public List<BuildingCost> Costs = new List<BuildingCost>();
    }
    public static class QuestOfferOps
    {
        public static string TypeName(int type) => type == 0 ? "贸易" : type == 1 ? "建设" : type == 2 ? "民生" : "探索";
        public static Rule SourceRule(EntityManager em, Entity root, Entity source, int type)
        {
            var b = em.GetComponentData<Building>(source); var d = Sim.Definition(em, root, em.GetComponentData<Identity>(source).Definition);
            for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind == RuleKind.QuestSource && r.B == type && (r.Level == 0 || r.Level == b.Level)) return r; }
            return new Rule { Level = -1 };
        }
        public static bool Available(EntityManager em, Entity root, Entity source, out string reason)
        {
            reason = "";
            if (!Sim.Operational(em, source)) { reason = "建筑未正常运营，冷却暂停"; return false; }
            var b = em.GetComponentData<Building>(source); var d = em.GetComponentData<Identity>(source).Definition;
            if (b.Maintained == 0) { reason = "维护未满足，冷却暂停"; return false; }
            if (Sim.Rule(em, root, d, RuleKind.ExpeditionSite, b.Level).Level >= 0)
            {
                if (!FeatureOps.Unlocked(em, root, "Expedition")) { reason = "远征许可未解锁"; return false; }
                if (EconomyOps.WorkforceLocked(em, em.GetComponentData<Identity>(source).Id)) { reason = "队伍在途，冷却暂停"; return false; }
            }
            if (Sim.Rule(em, root, d, RuleKind.Market, b.Level).Level >= 0 && b.Workers < em.GetComponentData<BuildingStats>(source).JobCapacity) { reason = "市场需要满额工人，冷却暂停"; return false; }
            return true;
        }
        public static int Strength(EntityManager em, Entity root, Entity source)
        {
            var b = em.GetComponentData<Building>(source); var d = em.GetComponentData<Identity>(source).Definition;
            if (Sim.Rule(em, root, d, RuleKind.ExpeditionSite, b.Level).Level >= 0) return (int)math.min(int.MaxValue, (long)b.Level * 10 + b.Experience);
            var settings = em.GetComponentData<ContentCatalog>(root).Value.Value.Quests;
            return (int)math.min(int.MaxValue, b.MarketLifetimeValue / math.max(1, settings.MarketValuePerStrength));
        }
        public static double Weight(QuestGenerationSettings settings, ContentDefinition d, int strength)
        {
            var band = math.min(3, strength / math.max(1, settings.StrengthStep));
            var weights = band == 0 ? settings.Low : band == 1 ? settings.Medium : band == 2 ? settings.High : settings.Maximum;
            return (double)d.QuestWeight * weights[math.clamp(d.QuestIntensity, 0, 3)];
        }
        public static Entity Offered(EntityManager em, ulong source, int slot)
        {
            using var quests = Sim.OrderedEntities<Quest>(em); foreach (var e in quests) { var q = em.GetComponentData<Quest>(e); if (q.Status == QuestStatus.Offered && q.Source == source && q.Slot == slot) return e; } return Entity.Null;
        }
        public static QuestOfferQuote Quote(EntityManager em, Entity root, Entity source, int index)
        {
            var q = new QuestOfferQuote { Code = ResultCode.InvalidTarget, Reason = "邀约来源不存在" };
            if (source == Entity.Null || !em.HasComponent<Building>(source) || !em.HasBuffer<QuestOfferSlot>(source)) return q;
            var slots = em.GetBuffer<QuestOfferSlot>(source); if (index < 0 || index >= slots.Length) return q;
            var slot = slots[index]; var rule = SourceRule(em, root, source, slot.Type); q.Type = slot.Type;
            if (rule.Level < 0 || slot.Index >= rule.Amount) return q;
            var id = em.GetComponentData<Identity>(source); var b = em.GetComponentData<Building>(source);
            q.Strength = Strength(em, root, source); q.Wait = math.max(0, slot.NextTurn - em.GetComponentData<Session>(root).Turn);
            q.Costs = BuildingCostOps.Rules(em, root, id.Definition, RuleKind.QuestRecruitCost, b.Level);
            var existing = Offered(em, id.Id, index); if (existing != Entity.Null) { q.Offer = em.GetComponentData<Identity>(existing).Id; q.Code = ResultCode.Busy; q.Reason = "已有邀约"; return q; }
            if (!Available(em, root, source, out q.Reason)) { q.Code = ResultCode.Unavailable; return q; }
            var blob = em.GetComponentData<ContentCatalog>(root).Value;
            for (var i = 0; i < blob.Value.Definitions.Length; i++)
            {
                var d = blob.Value.Definitions[i]; if (d.Kind != ContentKind.Quest || (d.Flags & 3) != 0 || d.Value != slot.Type || QuestOps.HasQuestPredecessor(em, root, i) || !ConditionOps.Prerequisites(em, root, i)) continue;
                var weight = Weight(blob.Value.Quests, d, q.Strength); if (weight <= 0) continue; q.Candidates.Add(i); q.Weights.Add(weight);
            }
            q.Code = q.Candidates.Count == 0 ? ResultCode.Unavailable : ResultCode.Success; q.Reason = q.Candidates.Count == 0 ? "暂无符合条件的邀约，不会扣费" : ""; return q;
        }
        // Stable buffer indices are never reused for another source type. Excess slots are dormant tombstones.
        public static void Synchronize(EntityManager em, Entity root, Entity source)
        {
            var slots = em.GetBuffer<QuestOfferSlot>(source); var id = em.GetComponentData<Identity>(source).Id;
            for (var i = 0; i < slots.Length; i++)
            {
                var s = slots[i]; var r = SourceRule(em, root, source, s.Type);
                if (r.Level >= 0 && s.Index < r.Amount) continue;
                var e = Offered(em, id, i); if (e != Entity.Null) em.DestroyEntity(e);
                s.NextTurn = 0; slots = em.GetBuffer<QuestOfferSlot>(source); slots[i] = s;
            }
        }
        public static void Restart(EntityManager em, Entity root, Entity source, int type, int turn)
        {
            var rule = SourceRule(em, root, source, type); if (rule.Level < 0) return;
            var next = turn + math.max(1, rule.C) + (int)(Sim.NextRandom(em, root) % (uint)math.max(1, (int)rule.Value - rule.C + 1));
            var slots = em.GetBuffer<QuestOfferSlot>(source);
            for (var i = 0; i < slots.Length; i++) if (slots[i].Type == type) { var slot = slots[i]; slot.NextTurn = next; slots[i] = slot; }
        }
        public static ResultCode Generate(EntityManager em, Entity root, Entity source, int index, bool paid)
        {
            var session = em.GetComponentData<Session>(root);
            if (paid && (session.Phase != Phase.Day || session.CheckpointPending != 0)) return ResultCode.WrongPhase;
            var q = Quote(em, root, source, index); if (q.Code != ResultCode.Success) return q.Code;
            if (paid && !BuildingCostOps.CanPay(em, root, q.Costs)) return ResultCode.InsufficientResources;
            // Candidate validation precedes both cost and RNG. Failed payment cannot advance selection/cooldown.
            if (paid && !BuildingCostOps.Pay(em, root, q.Costs)) return ResultCode.InsufficientResources;
            var total = 0d; foreach (var w in q.Weights) total += w;
            var roll = Sim.NextRandom(em, root) / ((double)uint.MaxValue + 1) * total; var choice = q.Candidates[q.Candidates.Count - 1];
            for (var i = 0; i < q.Candidates.Count; i++) { roll -= q.Weights[i]; if (roll < 0) { choice = q.Candidates[i]; break; } }
            var id = em.GetComponentData<Identity>(source);
            ProgressionOps.CreateQuest(em, root, choice, id.Id, index);
            Restart(em, root, source, q.Type, em.GetComponentData<Session>(root).Turn + (paid ? 0 : 1));
            Sim.Emit(em, root, EventKind.Message, "新邀约：" + id.Name.ToString().Substring(0, math.min(30, id.Name.ToString().Length)), id.Id, choice); return ResultCode.Success;
        }
        public static void Settle(EntityManager em, Entity root)
        {
            if (EconomyJournalOps.Forecast(em, root)) return;
            var turn = em.GetComponentData<Session>(root).Turn;
            using var buildings = Sim.OrderedEntities<Building>(em);
            foreach (var b in buildings)
            {
                Synchronize(em, root, b); var count = em.GetBuffer<QuestOfferSlot>(b).Length; var types = new HashSet<int>();
                for (var i = 0; i < count; i++)
                {
                    var slot = em.GetBuffer<QuestOfferSlot>(b)[i]; var rule = SourceRule(em, root, b, slot.Type);
                    if (rule.Level < 0 || slot.Index >= rule.Amount) continue;
                    if (!Available(em, root, b, out _)) { if (slot.NextTurn > 0) { slot.NextTurn++; var pausedSlots = em.GetBuffer<QuestOfferSlot>(b); pausedSlots[i] = slot; } continue; }
                    if (Offered(em, em.GetComponentData<Identity>(b).Id, i) != Entity.Null || !types.Add(slot.Type)) continue;
                    if (slot.NextTurn == 0) Restart(em, root, b, slot.Type, turn);
                    else if (slot.NextTurn <= turn + 1) Generate(em, root, b, i, false);
                }
            }
        }
    }
}
