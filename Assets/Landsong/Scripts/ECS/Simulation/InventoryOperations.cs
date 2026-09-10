using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static partial class InventoryOps
    {
        public static int Count(EntityManager em, Entity root, int item, ulong provider = 0)
        {
            var count = 0;
            foreach (var s in em.GetBuffer<InventorySlot>(root)) if (s.Unavailable == 0 && s.Item == item && (provider == 0 || s.Provider == provider)) count += s.Count;
            return count;
        }
        public static bool MatchesGroup(EntityManager em, Entity root, int item, int group)
        {
            if (group < 0) return true;
            var d = Sim.Definition(em, root, item);
            var guard = 0;
            var at = d.Group;
            while (Sim.ValidDefinition(em, root, at) && guard++ < 64) { if (at == group) return true; at = Sim.Definition(em, root, at).Group; }
            for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind != RuleKind.ItemGroup) continue; at = r.Target; guard = 0; while (Sim.ValidDefinition(em, root, at) && guard++ < 64) { if (at == group) return true; at = Sim.Definition(em, root, at).Group; } }
            return item == group;
        }
        public static int Add(EntityManager em, Entity root, int item, int amount, bool pending = false, float lossRemainder = 0)
        {
            if (amount <= 0 || !Sim.ValidDefinition(em, root, item) || Sim.Definition(em, root, item).Kind != ContentKind.Item) return 0;
            var remaining = amount;
            var maxStack = math.max(1, Sim.Definition(em, root, item).Capacity);
            var slots = em.GetBuffer<InventorySlot>(root);
            foreach (var i in StorageOrder(em, root, item))
                {
                    if (remaining <= 0) break;
                    var s = slots[i];
                    var add = math.min(remaining, maxStack - s.Count);
                    if (add <= 0) continue;
                    s.Item = item; s.Count += add; s.LossRemainder += lossRemainder * ((float)add / amount); slots[i] = s; remaining -= add;
                }
            if (pending && remaining > 0)
            {
                var pool = em.GetBuffer<PendingItem>(root);
                var merged = false;
                for (var i = 0; i < pool.Length; i++) if (pool[i].Item == item) { var p = pool[i]; p.Amount = checked(p.Amount + remaining); p.LossRemainder += lossRemainder * ((float)remaining / amount); pool[i] = p; merged = true; break; }
                if (!merged) pool.Add(new PendingItem { Item = item, Amount = remaining, LossRemainder = lossRemainder * ((float)remaining / amount) });
            }
            EconomyJournalOps.Record(em, root, item, amount - remaining);
            if (pending && remaining > 0) EconomyJournalOps.Record(em, root, item, remaining, true);
            return amount - remaining;
        }
        public static bool Remove(EntityManager em, Entity root, int item, int amount, ulong provider = 0)
        {
            if (amount < 0 || Count(em, root, item, provider) < amount) return false;
            var requested = amount;
            var slots = em.GetBuffer<InventorySlot>(root);
            foreach (var i in ConsumptionOrder(em, root, item, provider))
            {
                if (amount <= 0) break;
                var s = slots[i];
                if (s.Unavailable != 0 || s.Item != item || (provider != 0 && s.Provider != provider)) continue;
                var take = math.min(amount, s.Count); s.LossRemainder *= (float)(s.Count - take) / s.Count; s.Count -= take; amount -= take;
                if (s.Count == 0) { s.Item = -1; s.LossRemainder = 0; }
                slots[i] = s;
            }
            EconomyJournalOps.Record(em, root, item, -requested);
            return true;
        }
        public static int PendingCount(EntityManager em, Entity root, int item)
        {
            var count = 0; foreach (var p in em.GetBuffer<PendingItem>(root)) if (p.Item == item) count += p.Amount;
            return count;
        }
        public static bool RemoveWithPending(EntityManager em, Entity root, int item, int amount)
        {
            if (amount < 0 || Count(em, root, item) + PendingCount(em, root, item) < amount) return false;
            var pool = em.GetBuffer<PendingItem>(root);
            for (var i = pool.Length - 1; i >= 0 && amount > 0; i--)
            {
                var p = pool[i]; if (p.Item != item) continue;
                if (p.Amount == 0) { pool.RemoveAt(i); continue; }
                var take = math.min(p.Amount, amount); p.LossRemainder *= (float)(p.Amount - take) / p.Amount; p.Amount -= take; amount -= take;
                EconomyJournalOps.Record(em, root, item, -take, true);
                if (p.Amount == 0) pool.RemoveAt(i); else pool[i] = p;
            }
            return Remove(em, root, item, amount);
        }
        public static bool Pay(EntityManager em, Entity root, int definition, RuleKind kind, int level, float multiplier = 1f, bool commit = true, bool includePending = false)
        {
            var d = Sim.Definition(em, root, definition);
            var costs = new NativeHashMap<int, int>(math.max(1, d.RuleCount), Allocator.Temp);
            try
            {
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i);
                if (r.Kind != kind || (r.Level != 0 && r.Level != level) || r.Target < 0) continue;
                costs.TryGetValue(r.Target, out var prior); costs[r.Target] = prior + (int)math.ceil(r.Amount * multiplier);
            }
            foreach (var c in costs) if (Count(em, root, c.Key) + (includePending ? PendingCount(em, root, c.Key) : 0) < c.Value) return false;
            if (commit) foreach (var c in costs) { if (includePending) RemoveWithPending(em, root, c.Key, c.Value); else Remove(em, root, c.Key, c.Value); }
            return true;
            }
            finally { costs.Dispose(); }
        }
        public static void Refund(EntityManager em, Entity root, int definition, RuleKind kind, int level, float multiplier)
        {
            var d = Sim.Definition(em, root, definition);
            for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind == kind && (r.Level == 0 || r.Level == level) && r.Target >= 0) Add(em, root, r.Target, (int)math.floor(r.Amount * multiplier)); }
        }
        public static void Provision(EntityManager em, Entity root, Entity building)
        {
            using var scope = EconomyJournalOps.For(em, root, building, EconomyReason.CapacityTransfer);
            var id = em.GetComponentData<Identity>(building);
            var b = em.GetComponentData<Building>(building);
            if (b.RuinPending != 0) return;
            var d = Sim.Definition(em, root, id.Definition);
            var desired = 0;
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i);
                if (b.Stage != LifeStage.Operational || r.Kind != RuleKind.Warehouse || (r.Level > 0 && r.Level != b.Level) || b.Workers < r.B) continue;
                for (var n = 0; n < r.Amount; n++)
                {
                    var slots = em.GetBuffer<InventorySlot>(root);
                    var found = false;
                    for (var j = 0; j < slots.Length; j++) if (slots[j].Provider == id.Id && slots[j].Index == desired) { var slot = slots[j]; slot.SlotType = r.Target; slots[j] = slot; found = true; break; }
                    if (!found) slots.Add(new InventorySlot { Provider = id.Id, Index = desired, SlotType = r.Target, Item = -1 });
                    desired++;
                }
            }
            var buffer = em.GetBuffer<InventorySlot>(root);
            var overflow = new NativeList<PendingItem>(Allocator.Temp);
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                var slot = buffer[i]; if (slot.Provider != id.Id) continue;
                var removed = slot.Index >= desired;
                if (!removed && (slot.Count <= 0 || Accepts(em, root, slot.SlotType, slot.Item))) continue;
                if (slot.Count > 0) { overflow.Add(new PendingItem { Item = slot.Item, Amount = slot.Count, LossRemainder = slot.LossRemainder }); EconomyJournalOps.Record(em, root, slot.Item, -slot.Count); }
                if (removed) buffer.RemoveAt(i); else { slot.Item = -1; slot.Count = 0; slot.LossRemainder = 0; buffer[i] = slot; }
            }
            foreach (var item in overflow) Add(em, root, item.Item, item.Amount, true, item.LossRemainder);
            overflow.Dispose();
        }
        public static void LoseProvider(EntityManager em, Entity root, ulong id)
        {
            var slots = em.GetBuffer<InventorySlot>(root);
            for (var i = slots.Length - 1; i >= 0; i--)
            {
                var slot = slots[i]; if (slot.Provider != id) continue;
                slots.RemoveAt(i);
            }
        }
        public static void ApplyLoss(EntityManager em, Entity root)
        {
            var slots = em.GetBuffer<InventorySlot>(root);
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i]; if (slot.Count <= 0 || slot.Unavailable != 0) continue;
                var provider = Sim.Find(em, slot.Provider);
                var loss = slot.Count * LossRate(em, root, slot, slot.Item) + slot.LossRemainder;
                var count = math.min(slot.Count, (int)math.floor(loss)); slot.Count -= count; slot.LossRemainder = loss - count;
                if (count > 0 || slot.LossRemainder > 0) using (var scope = EconomyJournalOps.For(em, root, provider, EconomyReason.NaturalLoss))
                    EconomyJournalOps.Record(em, root, slot.Item, -count, note: new FixedString128Bytes($"格 {slot.Index + 1} · 结余小数损耗 {slot.LossRemainder:0.###}"));
                if (slot.Count == 0) { slot.Item = -1; slot.LossRemainder = 0; }
                slots[i] = slot;
            }
        }
    }
}
