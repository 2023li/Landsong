using System;
using System.Collections.Generic;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    [BuildingModuleId("processing")]
    public sealed class BM_资源加工 : BuildingModuleBase,
        IBuildingAutomaticTurnModule,
        IBuildingConnectionConsumerModule,
        IBuildingModuleStateSerializer,
        IBuildingResourceConsumptionSource,
        IBuildingResourceProductionSource
    {
        [Serializable]
        private sealed class ProcessingState
        {
            public int Progress;
        }

        private static readonly IReadOnlyList<string> ResourceConnections =
            new[] { BuildingConnectionTypes.Resource };

        [SerializeField, LabelText("生产周期回合"), Min(1)]
        private int productionIntervalTurns = 1;

        [SerializeField, LabelText("最低工人数"), Min(1)]
        private int minimumWorkers = 1;

        [SerializeField, LabelText("输入物品")]
        private ItemAmount[] inputs = Array.Empty<ItemAmount>();

        [SerializeField, LabelText("输出物品")]
        private ItemAmount[] outputs = Array.Empty<ItemAmount>();

        [SerializeField, ReadOnly, LabelText("生产进度")]
        private int progress;

        private IReadOnlyList<BuildingResourceChange> currentConsumptions =
            Array.Empty<BuildingResourceChange>();
        private IReadOnlyList<BuildingResourceChange> currentProductions =
            Array.Empty<BuildingResourceChange>();
        private IReadOnlyList<BuildingResourceChange> lastConsumptions =
            Array.Empty<BuildingResourceChange>();
        private IReadOnlyList<BuildingResourceChange> lastProductions =
            Array.Empty<BuildingResourceChange>();

        public override string ModuleDescription => "连接一个 Resource 提供点后，以原子库存事务把多项输入加工为多项输出。";
        public IReadOnlyList<string> RequiredConnectionTypeIds => ResourceConnections;
        public IReadOnlyList<BuildingResourceChange> CurrentResourceConsumptions => currentConsumptions;
        public IReadOnlyList<BuildingResourceChange> LastResourceConsumptions => lastConsumptions;
        public IReadOnlyList<BuildingResourceChange> CurrentResourceProductions => currentProductions;
        public IReadOnlyList<BuildingResourceChange> LastResourceProductions => lastProductions;
        public int ProductionProgress => Mathf.Clamp(progress, 0, ProductionIntervalTurns);
        public int ProductionIntervalTurns => Mathf.Max(1, productionIntervalTurns);

        public override void Normalize()
        {
            productionIntervalTurns = ProductionIntervalTurns;
            minimumWorkers = Mathf.Max(1, minimumWorkers);
            progress = ProductionProgress;
            inputs ??= Array.Empty<ItemAmount>();
            outputs ??= Array.Empty<ItemAmount>();
            currentConsumptions = ToChanges(inputs);
            currentProductions = ToChanges(outputs);
        }

        public bool ProcessAutomaticTurn(BuildingBase building)
        {
            Normalize();
            lastConsumptions = Array.Empty<BuildingResourceChange>();
            lastProductions = Array.Empty<BuildingResourceChange>();
            if (building == null || !HasValidRecipe())
            {
                return false;
            }

            if (BuildingWorkforceUtility.TryGetSource(building, out var workforce)
                && workforce.CurrentWorkers < Mathf.Min(minimumWorkers, workforce.MaxWorkers))
            {
                return false;
            }

            progress = Mathf.Min(ProductionIntervalTurns, progress + 1);
            if (progress < ProductionIntervalTurns)
            {
                return true;
            }

            if (!BuildingResourceProviderSystem.TrySelectProvider(building, out var providerSelection))
            {
                return false;
            }

            var inventory = building.GameSystem?.Services?.Inventory;
            if (inventory == null || !inventory.TryExchangeItems(inputs, outputs))
            {
                return false;
            }

            lastConsumptions = currentConsumptions;
            lastProductions = currentProductions;
            for (var i = 0; i < lastConsumptions.Count; i++)
            {
                BuildingResourceProviderSystem.RecordProvidedResource(
                    providerSelection,
                    building,
                    lastConsumptions[i]);
            }

            progress = 0;
            return true;
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            Normalize();
            var rows = new[]
            {
                new BuildingFunctionBlockSidebarRow("生产周期", $"{ProductionProgress}/{ProductionIntervalTurns}"),
                new BuildingFunctionBlockSidebarRow("最低工人", minimumWorkers.ToString())
            };
            AppendChanges(ref entries, currentConsumptions, -1, rows);
            AppendChanges(ref entries, currentProductions, 1, rows);
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new ProcessingState { Progress = ProductionProgress });
            return true;
        }

        public void RestoreState(string json)
        {
            var state = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<ProcessingState>(json);
            progress = Mathf.Clamp(state?.Progress ?? 0, 0, ProductionIntervalTurns);
        }

        private bool HasValidRecipe()
        {
            return currentConsumptions.Count > 0 && currentProductions.Count > 0;
        }

        private static IReadOnlyList<BuildingResourceChange> ToChanges(IReadOnlyList<ItemAmount> amounts)
        {
            if (amounts == null)
            {
                return Array.Empty<BuildingResourceChange>();
            }

            var changes = new List<BuildingResourceChange>();
            for (var i = 0; i < amounts.Count; i++)
            {
                var amount = amounts[i].Normalized();
                var change = new BuildingResourceChange(amount.ItemId, amount.Amount);
                if (change.IsValid)
                {
                    changes.Add(change);
                }
            }

            return changes;
        }

        private static void AppendChanges(
            ref List<BuildingFunctionBlockEntry> entries,
            IReadOnlyList<BuildingResourceChange> changes,
            int sign,
            IReadOnlyList<BuildingFunctionBlockSidebarRow> rows)
        {
            for (var i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                AddFunctionBlockEntry(
                    ref entries,
                    new BuildingFunctionBlockEntry(
                        BuildingFunctionBlockGroup.资源组,
                        change.ItemId,
                        change.Amount * sign,
                        rows));
            }
        }
    }
}
