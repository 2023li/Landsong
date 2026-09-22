using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Persistence
{
    public static class InvitationStateValidation
    {
        public static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot data) => Validate(em, root, data, false);
        internal static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot data, bool deferQuestContainerReconciliation)
        {
            if (data.ExpeditionPenalty.Stacks < 0 || data.ExpeditionPenalty.UntilTurn < 0)
                throw new InvalidDataException("Invalid expedition penalty");
            var buildings = data.Records.OfType<BuildingSnapshot>().ToDictionary(row => row.Identity.Id);
            foreach (var building in buildings.Values)
            {
                var history = new HashSet<ExpeditionId>();
                foreach (var entry in building.ExpeditionHistory)
                    if (!history.Add(entry.Definition) || !ExpeditionDefinitions.IsValid(em, root, entry.Definition))
                        throw new InvalidDataException("Invalid per-site expedition history");
                if (building.BuildingMarket.LifetimeValue < 0)
                    throw new InvalidDataException("Invalid market lifetime value");
                var slots = new HashSet<(int, int)>();
                foreach (var slot in building.Offers)
                    if (slot.Type < 0 || slot.Type > 3 || slot.Index < 0 || slot.NextTurn < 0 || !slots.Add((slot.Type, slot.Index)))
                        throw new InvalidDataException("Invalid quest source slots");
            }

            var containers = new HashSet<(ulong, int)>();
            var offered = new HashSet<(ulong, int)>();
            foreach (var record in data.Records.OfType<QuestSnapshot>())
            {
                var quest = record.Quest;
                if (quest.Status == QuestStatus.Offered && !offered.Add((quest.Source, quest.Slot)))
                    throw new InvalidDataException("Duplicate offered quest in source slot");
                if (quest.Status == QuestStatus.Active || quest.Status == QuestStatus.Completed)
                {
                    if (!containers.Add((quest.Container, quest.ContainerSlot)))
                        throw new InvalidDataException("Duplicate quest container");
                    if (!buildings.TryGetValue(quest.Container, out var provider) || quest.ContainerSlot < 0)
                    {
                        if (!deferQuestContainerReconciliation)
                            throw new InvalidDataException("Invalid quest container");
                    }
                    else
                    {
                        ref var definition = ref BuildingDefinitions.Get(em, root, provider.Definition);
                        int capacity = 0;
                        for (int i = 0; i < definition.Capabilities.Quests.Capacity.Length; i++)
                        {
                            var row = definition.Capabilities.Quests.Capacity[i];
                            if (row.Level == 0 || row.Level == provider.Building.Level)
                                capacity += row.Slots;
                        }

                        if (quest.ContainerSlot >= capacity && !deferQuestContainerReconciliation)
                            throw new InvalidDataException("Quest container slot exceeds configured capacity");
                    }
                }
                else if (quest.Container != 0 || quest.ContainerSlot != 0)
                    throw new InvalidDataException("Unexpected quest container");
            }

            var travelling = new HashSet<ulong>();
            foreach (var record in data.Records.OfType<ExpeditionSnapshot>())
            {
                var expedition = record.Expedition;
                if ((byte)expedition.Status > (byte)ExpeditionStatus.Failure || expedition.Crew < 1 || expedition.SourceLevel < 1 || expedition.Departure < 1 || expedition.Arrival <= expedition.Departure || expedition.Casualties < 0 || expedition.Casualties > expedition.Crew || expedition.SubsidyRequired < 0 || expedition.SubsidyPaid < 0 || expedition.SubsidyPaid > expedition.SubsidyRequired || expedition.PenaltyStacks < 0 || !math.isfinite(expedition.SuccessChance) || expedition.SuccessChance < 0 || expedition.SuccessChance > 1 || !math.isfinite(expedition.RewardBonus) || expedition.RewardBonus < 0)
                    throw new InvalidDataException("Invalid expedition departure/result snapshot");
                if (expedition.Status == ExpeditionStatus.Travelling && (!buildings.ContainsKey(expedition.Site) || !travelling.Add(expedition.Site) || expedition.Casualties != 0 || expedition.SubsidyPaid != 0))
                    throw new InvalidDataException("Invalid travelling expedition source");
                if (record.Supplies == null)
                    throw new InvalidDataException("Missing expedition supplies");
                var items = new HashSet<ItemId>();
                foreach (var supply in record.Supplies)
                    if (supply.Amount < 0 || !items.Add(supply.Item) || !ItemDefinitions.IsValid(em, root, supply.Item))
                        throw new InvalidDataException("Invalid expedition supply snapshot");
            }
        }
    }
}
