using System;
using System.Collections.Generic;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 可复用的固定维护费门控模块。把它放在生产模块之前；支付失败返回 false，
    /// BuildingBase 会停止本回合后续模块，因此不会出现“没付维护费仍然生产”。
    /// </summary>
    [Serializable]
    [BuildingModuleId("maintenance")]
    public sealed class BM_维护费 : BuildingModuleBase,
        IBuildingResourceConsumptionSource,
        IBuildingAutomaticTurnModule,
        IBuildingModuleStateSerializer
    {
        [Serializable]
        private sealed class MaintenanceState
        {
            public bool LastPaymentFailed;
            public bool LastPaymentSucceeded;
        }

        [SerializeField, AssetsOnly, LabelText("维护费物品")]
        private ItemDefinition itemDefinition;

        [SerializeField, LabelText("每回合维护费"), Min(0)]
        private int amountPerTurn;

        [SerializeField, ReadOnly, LabelText("上回合支付失败")]
        private bool lastPaymentFailed;

        [SerializeField, ReadOnly, LabelText("上回合已支付")]
        private bool lastPaymentSucceeded;

        private IReadOnlyList<BuildingResourceChange> lastResourceConsumptions =
            Array.Empty<BuildingResourceChange>();

        public override string ModuleDescription =>
            "每个运营回合先支付固定维护费；失败时中止后续生产模块并显示异常状态。";

        public ItemDefinition ItemDefinition => itemDefinition;
        public int AmountPerTurn => Mathf.Max(0, amountPerTurn);
        public IReadOnlyList<BuildingResourceChange> CurrentResourceConsumptions =>
            AmountPerTurn <= 0 ? Array.Empty<BuildingResourceChange>() : OneChange(ItemId, AmountPerTurn);
        public IReadOnlyList<BuildingResourceChange> LastResourceConsumptions =>
            lastResourceConsumptions ?? Array.Empty<BuildingResourceChange>();

        private string ItemId => itemDefinition == null ? string.Empty : itemDefinition.ItemId;

        public void ApplyConfiguration(ItemDefinition maintenanceItem, int amount)
        {
            itemDefinition = maintenanceItem;
            amountPerTurn = amount;
            Normalize();
        }

        public override void Normalize()
        {
            amountPerTurn = Mathf.Max(0, amountPerTurn);
        }

        public bool ProcessAutomaticTurn(BuildingBase building)
        {
            Normalize();
            lastPaymentFailed = false;
            lastPaymentSucceeded = false;
            lastResourceConsumptions = Array.Empty<BuildingResourceChange>();
            if (AmountPerTurn <= 0)
            {
                return true;
            }

            var inventory = building?.GameSystem?.Services?.Inventory;
            if (inventory == null
                || string.IsNullOrWhiteSpace(ItemId)
                || !inventory.TryRemoveItem(ItemId, AmountPerTurn))
            {
                lastPaymentFailed = true;
                return false;
            }

            lastPaymentSucceeded = true;
            lastResourceConsumptions = OneChange(ItemId, AmountPerTurn);
            return true;
        }

        public override string GetOverviewFragment(BuildingBase building) =>
            AmountPerTurn <= 0
                ? string.Empty
                : Landsong.Localization.L10n.Gameplay(
                    "gameplay.building.overview.maintenance",
                    "维护 {0} {1}/回合",
                    AmountPerTurn,
                    ItemId);

        public override void AppendRuntimeStatuses(
            BuildingBase building,
            ref List<BuildingRuntimeStatus> statuses)
        {
            if (AmountPerTurn > 0 && string.IsNullOrWhiteSpace(ItemId))
            {
                AddRuntimeStatus(
                    ref statuses,
                    new BuildingRuntimeStatus(
                        BuildingRuntimeStatusCatalog.BS_维护配置异常,
                        "维护费配置异常"));
            }
            else if (lastPaymentFailed)
            {
                AddRuntimeStatus(
                    ref statuses,
                    new BuildingRuntimeStatus(
                        BuildingRuntimeStatusCatalog.BS_维护费不足,
                        "维护费不足"));
            }
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            if (AmountPerTurn <= 0)
            {
                return;
            }

            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.资源组,
                    $"维护费:{ItemId}",
                    AmountPerTurn,
                    new[]
                    {
                        new BuildingFunctionBlockSidebarRow("维护费", $"{AmountPerTurn} {ItemId}/回合"),
                        new BuildingFunctionBlockSidebarRow(
                            "上回合",
                            lastPaymentSucceeded ? "已支付" : lastPaymentFailed ? "支付失败" : "未结算")
                    }));
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new MaintenanceState
            {
                LastPaymentFailed = lastPaymentFailed,
                LastPaymentSucceeded = lastPaymentSucceeded
            });
            return true;
        }

        public void RestoreState(string json)
        {
            var state = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<MaintenanceState>(json);
            lastPaymentFailed = state?.LastPaymentFailed == true;
            lastPaymentSucceeded = state?.LastPaymentSucceeded == true;
            lastResourceConsumptions = lastPaymentSucceeded && AmountPerTurn > 0
                ? OneChange(ItemId, AmountPerTurn)
                : Array.Empty<BuildingResourceChange>();
        }

        private static IReadOnlyList<BuildingResourceChange> OneChange(string itemId, int amount)
        {
            var change = new BuildingResourceChange(itemId, amount);
            return change.IsValid ? new[] { change } : Array.Empty<BuildingResourceChange>();
        }
    }
}
