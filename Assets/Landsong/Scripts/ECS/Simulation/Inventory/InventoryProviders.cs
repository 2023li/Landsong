using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class InventoryProviders
    {
        public static void Provision(EntityManager em, Entity root, Entity building)
        {
            using var scope = EconomyJournalOps.For(em, root, building, EconomyReason.CapacityTransfer);
            var id = em.GetComponentData<Identity>(building);
            var b = em.GetComponentData<Building>(building);
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(building);
            if (b.RuinPending != 0)
                return;
            ref var storage = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(building).Definition).Capabilities.Storage;
            var desired = 0;
            for (var i = 0; i < storage.Warehouses.Length; i++)
            {
                var warehouse = storage.Warehouses[i];
                if (b.Stage != LifeStage.Operational || (warehouse.Level > 0 && warehouse.Level != b.Level) || bWorkforce.Workers < warehouse.RequiredWorkers)
                    continue;
                for (var n = 0; n < warehouse.Slots; n++)
                {
                    var slots = em.GetBuffer<InventorySlot>(root);
                    var found = false;
                    for (var j = 0; j < slots.Length; j++)
                        if (slots[j].Provider == id.Id && slots[j].Index == desired)
                        {
                            var slot = slots[j];
                            slot.SlotType = warehouse.SlotType;
                            slots[j] = slot;
                            found = true;
                            break;
                        }

                    if (!found)
                        slots.Add(new InventorySlot { Provider = id.Id, Index = desired, SlotType = warehouse.SlotType, Item = ItemId.None });
                    desired++;
                }
            }

            var buffer = em.GetBuffer<InventorySlot>(root);
            var overflow = new NativeList<PendingItem>(Allocator.Temp);
            for (var i = buffer.Length - 1; i >= 0; i--)
            {
                var slot = buffer[i];
                if (slot.Provider != id.Id)
                    continue;
                var removed = slot.Index >= desired;
                if (!removed && (slot.Count <= 0 || InventoryStorage.Accepts(em, root, slot.SlotType, slot.Item)))
                    continue;
                if (slot.Count > 0)
                {
                    overflow.Add(new PendingItem { Item = slot.Item, Amount = slot.Count, LossRemainder = slot.LossRemainder });
                    EconomyJournalOps.Record(em, root, slot.Item, -slot.Count);
                }

                if (removed)
                    buffer.RemoveAt(i);
                else
                {
                    slot.Item = ItemId.None;
                    slot.Count = 0;
                    slot.LossRemainder = 0;
                    buffer[i] = slot;
                }
            }

            foreach (var item in overflow)
                InventoryOps.Add(em, root, item.Item, item.Amount, true, item.LossRemainder);
            overflow.Dispose();
        }

        public static void Remove(EntityManager em, Entity root, ulong id)
        {
            var slots = em.GetBuffer<InventorySlot>(root);
            for (var i = slots.Length - 1; i >= 0; i--)
            {
                var slot = slots[i];
                if (slot.Provider != id)
                    continue;
                slots.RemoveAt(i);
            }
        }
    }
}
