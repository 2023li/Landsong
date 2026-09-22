using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Persistence
{
    internal static class EconomyStateValidation
    {
        internal static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot data)
        {
            if (data.Economy == null || data.Bills == null || data.History == null || data.Report == null || data.BattleHistory == null || data.Inventory == null || data.Pending == null || data.LedgerTurn < 0 || data.LedgerTurn > data.Clock.Turn)
                throw new InvalidDataException("Incomplete economy/history snapshot");
            foreach (var entry in data.Economy)
                if (entry.Turn != data.LedgerTurn || entry.Source > data.Ids.NextId || entry.Pending > 1 || (byte)entry.Reason > (byte)EconomyReason.NightDiscard || entry.Delta != 0 && !ItemDefinitions.IsValid(em, root, entry.Item))
                    throw new InvalidDataException("Invalid economy entry");
            var bills = new HashSet<(int, ulong, ItemId)>();
            foreach (var bill in data.Bills)
                if (bill.Turn < 1 || bill.Turn > data.Clock.Turn || bill.Source > data.Ids.NextId || bill.Item.IsValid && !ItemDefinitions.IsValid(em, root, bill.Item) || bill.Income < 0 || bill.Expense < 0 || bill.Stored < 0 || bill.Pending < 0 || !bill.Item.IsValid && (bill.Source != 0 || bill.Income != 0 || bill.Expense != 0 || bill.Stored != 0 || bill.Pending != 0) || !bills.Add((bill.Turn, bill.Source, bill.Item)))
                    throw new InvalidDataException("Invalid bill history");
            foreach (var history in data.History)
                if (history.Turn < 1 || history.Turn > data.Clock.Turn || history.Count < 1 || history.Count > 100000 || history.Pending > 1 || history.HasPosition > 1 || history.Transfer > 1 || (byte)history.Category > (byte)HistoryCategory.Important || !math.all(math.isfinite(history.Position)) || history.Item.IsValid && !ItemDefinitions.IsValid(em, root, history.Item))
                    throw new InvalidDataException("Invalid interface history");
            foreach (var report in data.Report)
                Report(em, root, report);
            int previous = 0;
            foreach (var history in data.BattleHistory)
            {
                if (history.Turn < 1 || history.Turn < previous || history.Turn > data.Clock.Turn)
                    throw new InvalidDataException("Invalid battle history turn");
                previous = history.Turn;
                Report(em, root, history.Entry);
            }

            var buildings = new HashSet<ulong>(data.Records.OfType<BuildingSnapshot>().Select(row => row.Identity.Id));
            var slots = new HashSet<(ulong, int)>();
            foreach (var slot in data.Inventory)
            {
                if (!buildings.Contains(slot.Provider) || !slots.Add((slot.Provider, slot.Index)) || slot.Index < 0 || slot.Count < 0 || slot.Unavailable > 1 || !math.isfinite(slot.LossRemainder) || slot.LossRemainder < 0 || slot.LossRemainder > slot.Count || slot.Count == 0 && slot.LossRemainder != 0)
                    throw new InvalidDataException("Invalid inventory slot");
                if (slot.SlotType.IsValid && !StorageSlotDefinitions.IsValid(em, root, slot.SlotType))
                    throw new InvalidDataException("Invalid slot type");
                if (slot.Count > 0 && (!ItemDefinitions.IsValid(em, root, slot.Item) || !InventoryStorage.Accepts(em, root, slot.SlotType, slot.Item) || slot.Count > ItemDefinitions.Get(em, root, slot.Item).MaximumStack))
                    throw new InvalidDataException("Incompatible or overfull inventory slot");
            }

            var pending = new HashSet<ItemId>();
            foreach (var item in data.Pending)
                if (!pending.Add(item.Item) || item.Amount < 0 || !ItemDefinitions.IsValid(em, root, item.Item) || !math.isfinite(item.LossRemainder) || item.LossRemainder < 0 || item.LossRemainder > item.Amount)
                    throw new InvalidDataException("Invalid pending item");
        }

        static void Report(EntityManager em, Entity root, BattleReportEntry report)
        {
            if (report.Amount < 0 || !math.isfinite(report.Value) || report.Value < 0 || (byte)report.Kind > (byte)EventKind.EnemyDeath || report.Item.IsValid && !ItemDefinitions.IsValid(em, root, report.Item) || report.Building.IsValid && !BuildingDefinitions.IsValid(em, root, report.Building))
                throw new InvalidDataException("Invalid battle report");
        }
    }
}
