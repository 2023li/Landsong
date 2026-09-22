using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class InventoryStorage
    {
        public static bool Accepts(EntityManager em, Entity root, StorageSlotId type, ItemId item)
        {
            if (!ItemDefinitions.IsValid(em, root, item))
                return false;
            if (!type.IsValid)
                return true;
            if (!StorageSlotDefinitions.IsValid(em, root, type))
                return false;
            ref var definition = ref StorageSlotDefinitions.Get(em, root, type);
            if (definition.AcceptedItems.Length == 0 && definition.AcceptedGroups.Length == 0)
                return true;
            for (int i = 0; i < definition.AcceptedItems.Length; i++)
                if (definition.AcceptedItems[i] == item)
                    return true;
            for (int i = 0; i < definition.AcceptedGroups.Length; i++)
                if (ItemGroups.Matches(em, root, item, definition.AcceptedGroups[i]))
                    return true;
            return false;
        }

        public static float LossRate(EntityManager em, Entity root, InventorySlot slot, ItemId item)
        {
            if (!ItemDefinitions.IsValid(em, root, item))
                return 0;
            float naturalLoss = ItemDefinitions.Get(em, root, item).NaturalLossRate;
            if (naturalLoss <= 0)
                return 0;
            float multiplier = 1;
            if (StorageSlotDefinitions.IsValid(em, root, slot.SlotType))
            {
                ref var storage = ref StorageSlotDefinitions.Get(em, root, slot.SlotType);
                multiplier = storage.DefaultLossMultiplier;
                for (int i = 0; i < storage.ItemLosses.Length; i++)
                    if (storage.ItemLosses[i].Item == item)
                        multiplier *= storage.ItemLosses[i].Multiplier;
                for (int i = 0; i < storage.GroupLosses.Length; i++)
                    if (ItemGroups.Matches(em, root, item, storage.GroupLosses[i].Group))
                        multiplier *= storage.GroupLosses[i].Multiplier;
            }

            var provider = WorldQueries.Find(em, slot.Provider);
            if (provider != Entity.Null && em.HasComponent<BuildingDefinitionRef>(provider))
            {
                var building = em.GetComponentData<Building>(provider);
                BuildingWorkforceState buildingWorkforce = em.GetComponentData<BuildingWorkforceState>(provider);
                BuildingMaintenanceState buildingMaintenance = em.GetComponentData<BuildingMaintenanceState>(provider);
                ref var conditions = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(provider).Definition).Capabilities.Storage.Conditions;
                for (int i = 0; i < conditions.Length; i++)
                {
                    var condition = conditions[i];
                    if (condition.Level != 0 && condition.Level != building.Level)
                        continue;
                    if (buildingWorkforce.Workers < condition.RequiredWorkers)
                        multiplier *= condition.UnderstaffedLossMultiplier;
                    if (buildingMaintenance.Maintained == 0)
                        multiplier *= condition.MaintenanceLossPercent / 100f;
                    break;
                }
            }

            return math.saturate(naturalLoss * multiplier * (1 - math.saturate(ItemEffects.Modifier(em, root, NumericEffectKind.LossMultiplier, item))));
        }

        internal static int StableSlot(InventorySlot a, InventorySlot b)
        {
            var c = a.Provider.CompareTo(b.Provider);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        }

        public static int CompareStorage(EntityManager em, Entity root, InventorySlot a, InventorySlot b, ItemId item)
        {
            var c = InventoryStorage.LossRate(em, root, a, item).CompareTo(InventoryStorage.LossRate(em, root, b, item));
            if (c == 0)
                c = (a.Count == 0).CompareTo(b.Count == 0);
            return c != 0 ? c : InventoryStorage.StableSlot(a, b);
        }

        public static List<int> StorageOrder(EntityManager em, Entity root, ItemId item)
        {
            var slots = em.GetBuffer<InventorySlot>(root);
            var result = new List<int>();
            for (var i = 0; i < slots.Length; i++)
                if (slots[i].Unavailable == 0 && (slots[i].Count == 0 || slots[i].Item == item) && InventoryStorage.Accepts(em, root, slots[i].SlotType, item))
                    result.Add(i);
            result.Sort((a, b) => InventoryStorage.CompareStorage(em, root, slots[a], slots[b], item));
            return result;
        }

        internal static List<int> ConsumptionOrder(EntityManager em, Entity root, ItemId item, ulong provider)
        {
            var slots = em.GetBuffer<InventorySlot>(root);
            var result = new List<int>();
            for (var i = 0; i < slots.Length; i++)
                if (slots[i].Unavailable == 0 && slots[i].Count > 0 && slots[i].Item == item && (provider == 0 || slots[i].Provider == provider))
                    result.Add(i);
            result.Sort((a, b) =>
            {
                var c = InventoryStorage.LossRate(em, root, slots[b], item).CompareTo(InventoryStorage.LossRate(em, root, slots[a], item));
                return c != 0 ? c : InventoryStorage.StableSlot(slots[a], slots[b]);
            });
            return result;
        }
    }
}
