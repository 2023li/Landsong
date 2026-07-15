using System;
using System.Collections.Generic;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    [BuildingModuleId("population.fixed")]
    public sealed class BM_固定人口 : BuildingModuleBase,
        IBuildingPopulationSource,
        IBuildingModuleRegistered,
        IBuildingModulePlaced,
        IBuildingModuleConstructionCompleted,
        IBuildingModuleLevelApplied,
        IBuildingModuleUnregistered,
        IBuildingModuleDemolished
    {
        [SerializeField, LabelText("提供人口"), Min(0)]
        private int population;

        [SerializeField, LabelText("注册为宫殿")]
        private bool registerAsPalace;

        [NonSerialized] private BuildingBase owner;

        public override string ModuleDescription => "提供固定人口；可选同时把建筑注册为王宫。等级变化只更新配置，不更换建筑实例。";
        public int CurrentPopulation => owner != null && owner.IsOperational ? Mathf.Max(0, population) : 0;

        public void ApplyConfiguration(int value)
        {
            population = Mathf.Max(0, value);
            RefreshRegistration();
        }

        public void SetRegisterAsPalace(bool value)
        {
            registerAsPalace = value;
            RefreshRegistration();
        }

        public override void Normalize()
        {
            population = Mathf.Max(0, population);
        }

        public void OnBuildingRegistered(BuildingBase building)
        {
            owner = building;
            RefreshRegistration();
        }

        public void OnBuildingPlaced(BuildingBase building)
        {
            owner = building;
            RefreshRegistration();
        }

        public void OnBuildingConstructionCompleted(BuildingBase building)
        {
            owner = building;
            RefreshRegistration();
        }

        public void OnBuildingLevelApplied(BuildingBase building, int previousLevel, int currentLevel)
        {
            owner = building;
            RefreshRegistration();
        }

        public void OnBuildingUnregistered(BuildingBase building) => ClearRegistration(building);
        public void OnBuildingDemolished(BuildingBase building) => ClearRegistration(building);

        public override string GetOverviewFragment(BuildingBase building) => $"人口 +{CurrentPopulation}";

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "人口",
                    CurrentPopulation));
        }

        private void RefreshRegistration()
        {
            if (owner == null || !owner.IsRegistered || !owner.IsOperational)
            {
                return;
            }

            owner.GameSystem?.Dynasty?.SetPopulationContribution(owner, CurrentPopulation);
            if (registerAsPalace)
            {
                owner.GameSystem?.Dynasty?.RegisterPalace(owner);
            }
        }

        private void ClearRegistration(BuildingBase building)
        {
            var target = building == null ? owner : building;
            target?.GameSystem?.Dynasty?.RemovePopulationContribution(target);
            if (registerAsPalace)
            {
                target?.GameSystem?.Dynasty?.UnregisterPalace(target);
            }
        }
    }

    [Serializable]
    [BuildingModuleId("production.rare_bonus")]
    public sealed class BM_稀有产出 : BuildingModuleBase,
        IBuildingAutomaticTurnModule,
        IBuildingModuleStateSerializer
    {
        [Serializable]
        private sealed class RareProductionState
        {
            public bool LastTurnProduced;
        }

        [SerializeField, LabelText("启用稀有产出")]
        private bool rareProductionEnabled;

        [SerializeField, AssetsOnly, LabelText("稀有物品")]
        private ItemDefinition itemDefinition;

        [SerializeField, LabelText("最低工人数"), Min(1)]
        private int minimumWorkers = 3;

        [SerializeField, LabelText("触发概率%"), Range(0f, 100f)]
        private float chancePercent = 1f;

        [SerializeField, LabelText("产出数量"), Min(0)]
        private int amount = 1;

        [SerializeField, ReadOnly, LabelText("上回合触发")]
        private bool lastTurnProduced;

        public override string ModuleDescription => "在前置岗位与普通生产成功后，按概率追加一项稀有产出。";
        public ItemDefinition ItemDefinition => itemDefinition;
        private string ItemId => itemDefinition == null ? string.Empty : itemDefinition.ItemId;

        public void ApplyConfiguration(
            bool enabled,
            ItemDefinition rareItemDefinition,
            int requiredWorkers,
            float probabilityPercent,
            int outputAmount)
        {
            rareProductionEnabled = enabled;
            itemDefinition = rareItemDefinition;
            minimumWorkers = requiredWorkers;
            chancePercent = probabilityPercent;
            amount = outputAmount;
            Normalize();
        }

        public override void Normalize()
        {
            minimumWorkers = Mathf.Max(1, minimumWorkers);
            chancePercent = Mathf.Clamp(chancePercent, 0f, 100f);
            amount = Mathf.Max(0, amount);
        }

        public bool ProcessAutomaticTurn(BuildingBase building)
        {
            lastTurnProduced = false;
            Normalize();
            if (!rareProductionEnabled || amount <= 0)
            {
                return true;
            }

            if (!building.TryGetModule<BM_资源产出>(out var production)
                || production.LastResourceProductions.Count == 0)
            {
                return true;
            }

            if (!BuildingWorkforceUtility.TryGetSource(building, out var workforce)
                || workforce.CurrentWorkers < minimumWorkers
                || UnityEngine.Random.value * 100f >= chancePercent)
            {
                return true;
            }

            var inventory = building?.GameSystem?.Services?.Inventory;
            if (inventory == null || string.IsNullOrWhiteSpace(ItemId) || !inventory.TryAddItem(ItemId, amount))
            {
                return false;
            }

            lastTurnProduced = true;
            production.AppendLastResourceProduction(ItemId, amount);

            return true;
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            if (!rareProductionEnabled || amount <= 0)
            {
                return;
            }

            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.资源组,
                    ItemId,
                    amount,
                    new[]
                    {
                        new BuildingFunctionBlockSidebarRow("最低工人", minimumWorkers.ToString()),
                        new BuildingFunctionBlockSidebarRow("触发概率", $"{chancePercent:0.##}%")
                    }));
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new RareProductionState { LastTurnProduced = lastTurnProduced });
            return true;
        }

        public void RestoreState(string json)
        {
            var state = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<RareProductionState>(json);
            lastTurnProduced = state != null && state.LastTurnProduced;
        }
    }

    [Serializable]
    [BuildingModuleId("market.resource_accounting")]
    public sealed class BM_市场资源结算 : BuildingModuleBase,
        IBuildingResourceProvisionAccounting,
        IBuildingTaxSource,
        IBuildingModuleStateSerializer,
        IBuildingModuleInitialized,
        IBuildingModuleRegistered
    {
        [Serializable]
        private sealed class MarketState
        {
            public long LastTurnProvidedResourceValue;
            public int LastTurnGoldIncome;
            public string LastAbnormalStatusId;
            public string LastAbnormalStatusText;
        }

        [SerializeField, AssetsOnly, LabelText("金币物品")]
        private ItemDefinition goldItemDefinition;

        [SerializeField, LabelText("价值结算比例"), Min(0f)]
        private float incomeRatio = 0.1f;

        [SerializeField, ReadOnly, LabelText("上回合经手价值")]
        private long lastTurnProvidedResourceValue;

        [SerializeField, ReadOnly, LabelText("上回合金币收入")]
        private int lastTurnGoldIncome;

        private long providedResourceValueThisTurn;
        private string lastAbnormalStatusId = string.Empty;
        private string lastAbnormalStatusText = string.Empty;
        private IReadOnlyList<BuildingResourceChange> lastTaxRewards = Array.Empty<BuildingResourceChange>();
        [NonSerialized] private BuildingBase owner;

        public override string ModuleDescription => "资源提供点按本回合经手物品基础价值结算金币收入。";
        public ItemDefinition GoldItemDefinition => goldItemDefinition;
        public float IncomeRatio => Mathf.Max(0f, incomeRatio);
        private string GoldItemId => goldItemDefinition == null ? string.Empty : goldItemDefinition.ItemId;
        public bool IsResourceProviderOperational =>
            owner != null
            && BuildingWorkforceUtility.TryGetSource(owner, out var workforce)
            && workforce.CurrentWorkers >= workforce.MaxWorkers;
        public IReadOnlyList<BuildingResourceChange> CurrentTaxRewards => Array.Empty<BuildingResourceChange>();
        public IReadOnlyList<BuildingResourceChange> LastTaxRewards => lastTaxRewards;

        public override void Normalize()
        {
            incomeRatio = Mathf.Max(0f, incomeRatio);
        }

        public void ApplyConfiguration(ItemDefinition goldDefinition, float settlementRatio)
        {
            goldItemDefinition = goldDefinition;
            incomeRatio = settlementRatio;
            Normalize();
        }

        public void OnBuildingInitialized(BuildingBase building) => Bind(building);
        public void OnBuildingRegistered(BuildingBase building) => Bind(building);

        private void Bind(BuildingBase building)
        {
            owner = building;
            Normalize();
        }

        public void BeginResourceProvisionTurn()
        {
            providedResourceValueThisTurn = 0;
            lastTurnProvidedResourceValue = 0;
            lastTurnGoldIncome = 0;
            lastTaxRewards = Array.Empty<BuildingResourceChange>();
            ClearStatus();
        }

        public void RecordProvidedResource(BuildingBase consumer, BuildingResourceChange resource)
        {
            var catalog = owner?.GameSystem?.Services?.Inventory?.ItemCatalog;
            if (!resource.IsValid
                || catalog == null
                || !catalog.TryGetDefinition(resource.ItemId, out var definition)
                || definition == null)
            {
                return;
            }

            var value = (long)Mathf.Max(0, definition.BaseValue) * resource.Amount;
            providedResourceValueThisTurn = value > long.MaxValue - providedResourceValueThisTurn
                ? long.MaxValue
                : providedResourceValueThisTurn + value;
        }

        public void CompleteResourceProvisionTurn()
        {
            lastTurnProvidedResourceValue = providedResourceValueThisTurn;
            var income = (int)Math.Min(int.MaxValue, Math.Floor(lastTurnProvidedResourceValue * incomeRatio));
            if (income <= 0)
            {
                return;
            }

            var inventory = owner?.GameSystem?.Services?.Inventory;
            if (inventory == null)
            {
                SetStatus(BuildingRuntimeStatusCatalog.BS_库存缺失, "库存服务缺失");
                return;
            }

            if (string.IsNullOrWhiteSpace(GoldItemId) || !inventory.TryAddItem(GoldItemId, income))
            {
                SetStatus(BuildingRuntimeStatusCatalog.BS_市场收入存入失败, "金币存入失败");
                return;
            }

            lastTurnGoldIncome = income;
            lastTaxRewards = new[] { new BuildingResourceChange(GoldItemId, income) };
            owner?.NotifyStateChanged();
        }

        public override string GetOverviewFragment(BuildingBase building) => $"上回合 +{lastTurnGoldIncome} 金币";

        public override void AppendRuntimeStatuses(
            BuildingBase building,
            ref List<BuildingRuntimeStatus> statuses)
        {
            AddRuntimeStatus(
                ref statuses,
                string.IsNullOrWhiteSpace(lastAbnormalStatusId)
                    ? default
                    : new BuildingRuntimeStatus(lastAbnormalStatusId, lastAbnormalStatusText));
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "资源提供点",
                    1,
                    new[]
                    {
                        new BuildingFunctionBlockSidebarRow("提供优先级", building.ResourceProviderPriority.ToString()),
                        new BuildingFunctionBlockSidebarRow("上回合经手价值", lastTurnProvidedResourceValue.ToString()),
                        new BuildingFunctionBlockSidebarRow("金币结算", $"总价值 × {incomeRatio:P0}")
                    }));
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new MarketState
            {
                LastTurnProvidedResourceValue = lastTurnProvidedResourceValue,
                LastTurnGoldIncome = lastTurnGoldIncome,
                LastAbnormalStatusId = lastAbnormalStatusId,
                LastAbnormalStatusText = lastAbnormalStatusText
            });
            return true;
        }

        public void RestoreState(string json)
        {
            var state = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<MarketState>(json);
            lastTurnProvidedResourceValue = Math.Max(0L, state?.LastTurnProvidedResourceValue ?? 0L);
            lastTurnGoldIncome = Mathf.Max(0, state?.LastTurnGoldIncome ?? 0);
            lastAbnormalStatusId = NormalizeText(state?.LastAbnormalStatusId);
            lastAbnormalStatusText = NormalizeText(state?.LastAbnormalStatusText);
            lastTaxRewards = lastTurnGoldIncome <= 0
                ? Array.Empty<BuildingResourceChange>()
                : new[] { new BuildingResourceChange(GoldItemId, lastTurnGoldIncome) };
        }

        private void SetStatus(string id, string text)
        {
            lastAbnormalStatusId = NormalizeText(id);
            lastAbnormalStatusText = string.IsNullOrWhiteSpace(text) ? lastAbnormalStatusId : text.Trim();
        }

        private void ClearStatus()
        {
            lastAbnormalStatusId = string.Empty;
            lastAbnormalStatusText = string.Empty;
        }

        private static string NormalizeText(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    [BuildingModuleId("harvestable.tree")]
    public sealed class BM_树木采集 : BuildingModuleBase,
        IBuildingModuleInitialized,
        IBuildingModuleDoubleClicked,
        IBuildingModuleDemolished,
        IBuildingModuleStateSerializer
    {
        [Serializable]
        private sealed class TreeState
        {
            public int CurrentHealth;
        }

        [SerializeField, LabelText("最低生命"), Min(1)] private int minHealth = 3;
        [SerializeField, LabelText("最高生命"), Min(1)] private int maxHealth = 6;
        [SerializeField, LabelText("双击伤害"), Min(1)] private int damagePerDoubleClick = 1;
        [SerializeField, AssetsOnly, LabelText("原木物品")] private ItemDefinition woodItemDefinition;
        [SerializeField, LabelText("原木奖励"), Min(0)] private int woodRewardAmount = 2;
        [SerializeField, AssetsOnly, LabelText("树苗物品")] private ItemDefinition saplingItemDefinition;
        [SerializeField, LabelText("树苗奖励"), Min(0)] private int saplingRewardAmount = 3;
        [SerializeField, ReadOnly, LabelText("当前生命")] private int currentHealth;
        [NonSerialized] private BuildingBase owner;

        public override string ModuleDescription => "让建筑可通过双击采集；生命归零时拆除并发放配置的资源。";
        public ItemDefinition WoodItemDefinition => woodItemDefinition;
        public ItemDefinition SaplingItemDefinition => saplingItemDefinition;
        public int MinHealth => Mathf.Max(1, minHealth);
        public int MaxHealth => Mathf.Max(MinHealth, maxHealth);
        public int DamagePerDoubleClick => Mathf.Max(1, damagePerDoubleClick);
        public int WoodRewardAmount => Mathf.Max(0, woodRewardAmount);
        public int SaplingRewardAmount => Mathf.Max(0, saplingRewardAmount);
        private string WoodItemId => woodItemDefinition == null ? string.Empty : woodItemDefinition.ItemId;
        private string SaplingItemId => saplingItemDefinition == null ? string.Empty : saplingItemDefinition.ItemId;

        public override void Normalize()
        {
            minHealth = Mathf.Max(1, minHealth);
            maxHealth = Mathf.Max(minHealth, maxHealth);
            damagePerDoubleClick = Mathf.Max(1, damagePerDoubleClick);
            woodRewardAmount = Mathf.Max(0, woodRewardAmount);
            saplingRewardAmount = Mathf.Max(0, saplingRewardAmount);
            currentHealth = Mathf.Max(0, currentHealth);
        }

        public void ApplyConfiguration(
            int minimumHealth,
            int maximumHealth,
            int doubleClickDamage,
            ItemDefinition woodDefinition,
            int woodAmount,
            ItemDefinition saplingDefinition,
            int saplingAmount)
        {
            minHealth = minimumHealth;
            maxHealth = maximumHealth;
            damagePerDoubleClick = doubleClickDamage;
            woodItemDefinition = woodDefinition;
            woodRewardAmount = woodAmount;
            saplingItemDefinition = saplingDefinition;
            saplingRewardAmount = saplingAmount;
            Normalize();
        }

        public void OnBuildingInitialized(BuildingBase building)
        {
            owner = building;
            Normalize();
            if (currentHealth <= 0)
            {
                currentHealth = UnityEngine.Random.Range(minHealth, maxHealth + 1);
            }
        }

        public void OnBuildingDoubleClicked(BuildingBase building)
        {
            owner = building;
            Damage(damagePerDoubleClick);
        }

        public void OnBuildingDemolished(BuildingBase building)
        {
            if (building == null || !building.IsOperational)
            {
                return;
            }

            var inventory = building.GameSystem?.Services?.Inventory;
            if (inventory == null)
            {
                Debug.LogWarning($"Tree '{building.name}' cannot add harvest rewards because InventoryService is missing.", building);
                return;
            }

            if (!string.IsNullOrWhiteSpace(WoodItemId))
            {
                inventory.AddItem(WoodItemId, woodRewardAmount);
            }

            if (!string.IsNullOrWhiteSpace(SaplingItemId))
            {
                inventory.AddItem(SaplingItemId, saplingRewardAmount);
            }
        }

        public override string GetOverviewFragment(BuildingBase building) => $"生命 {currentHealth}/{maxHealth}";

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "可采集树木",
                    currentHealth,
                    new[]
                    {
                        new BuildingFunctionBlockSidebarRow("生命", $"{currentHealth}/{maxHealth}"),
                        new BuildingFunctionBlockSidebarRow("原木奖励", woodRewardAmount.ToString()),
                        new BuildingFunctionBlockSidebarRow("树苗奖励", saplingRewardAmount.ToString())
                    }));
        }

        public void Damage(int amount)
        {
            if (owner == null || amount <= 0 || currentHealth <= 0)
            {
                return;
            }

            currentHealth = Mathf.Max(0, currentHealth - amount);
            owner.NotifyStateChanged();
            if (currentHealth <= 0)
            {
                owner.Demolish();
            }
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new TreeState { CurrentHealth = currentHealth });
            return true;
        }

        public void RestoreState(string json)
        {
            var state = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<TreeState>(json);
            currentHealth = Mathf.Max(0, state?.CurrentHealth ?? 0);
        }
    }
}
