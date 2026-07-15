using System;
using System.Collections.Generic;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    public abstract class BuildingLevelConfigurationBase
    {
        public abstract string ConfigurationId { get; }
        public abstract void Apply(BuildingBase building);

        public virtual void Normalize()
        {
        }
    }

    [Serializable]
    public sealed class BuildingWorkforceLevelConfiguration : BuildingLevelConfigurationBase
    {
        [SerializeField, LabelText("最大岗位"), Min(1)] private int maxWorkers = 1;
        [SerializeField, LabelText("初始工人"), Min(0)] private int initialWorkers;
        [SerializeField, LabelText("基础岗位吸引力"), Min(0f)] private float baseAttraction = 55f;
        [SerializeField, LabelText("单人招工费用"), Min(0)] private int recruitCost = 10;
        [SerializeField, LabelText("自动补贴")] private bool autoSubsidy;
        [SerializeField, LabelText("目标稳定工人"), Min(0)] private int targetStableWorkers;
        [SerializeField, AssetsOnly, LabelText("金币物品")] private ItemDefinition goldItemDefinition;

        public BuildingWorkforceLevelConfiguration()
        {
        }

        public BuildingWorkforceLevelConfiguration(
            int maxWorkers,
            int initialWorkers,
            float baseAttraction,
            int recruitCost,
            bool autoSubsidy,
            int targetStableWorkers,
            ItemDefinition goldItemDefinition)
        {
            this.maxWorkers = maxWorkers;
            this.initialWorkers = initialWorkers;
            this.baseAttraction = baseAttraction;
            this.recruitCost = recruitCost;
            this.autoSubsidy = autoSubsidy;
            this.targetStableWorkers = targetStableWorkers;
            this.goldItemDefinition = goldItemDefinition;
            Normalize();
        }

        public override string ConfigurationId => "workforce";
        public int MaxWorkers => Mathf.Max(1, maxWorkers);
        public ItemDefinition GoldItemDefinition => goldItemDefinition;

        public override void Apply(BuildingBase building)
        {
            building.GetRequiredModule<BM_岗位运营>().ApplyConfiguration(
                maxWorkers,
                initialWorkers,
                baseAttraction,
                recruitCost,
                autoSubsidy,
                targetStableWorkers,
                goldItemDefinition);
        }

        public override void Normalize()
        {
            maxWorkers = Mathf.Max(1, maxWorkers);
            initialWorkers = Mathf.Clamp(initialWorkers, 0, maxWorkers);
            baseAttraction = Mathf.Max(0f, baseAttraction);
            recruitCost = Mathf.Max(0, recruitCost);
            targetStableWorkers = Mathf.Clamp(targetStableWorkers, 0, maxWorkers);
        }
    }

    [Serializable]
    public struct BuildingProductionOutputConfiguration
    {
        [SerializeField, AssetsOnly, LabelText("产出物品")] private ItemDefinition itemDefinition;
        [SerializeField, LabelText("工人数产量表")] private WorkerProductionTier[] workerProductionTiers;

        public BuildingProductionOutputConfiguration(
            ItemDefinition itemDefinition,
            IReadOnlyList<WorkerProductionTier> tiers)
        {
            this.itemDefinition = itemDefinition;
            if (tiers == null || tiers.Count == 0)
            {
                workerProductionTiers = Array.Empty<WorkerProductionTier>();
            }
            else
            {
                workerProductionTiers = new WorkerProductionTier[tiers.Count];
                for (var i = 0; i < tiers.Count; i++)
                {
                    workerProductionTiers[i] = tiers[i];
                }
            }
        }

        public ItemDefinition ItemDefinition => itemDefinition;
        public string ItemId => itemDefinition == null ? string.Empty : itemDefinition.ItemId;
        public IReadOnlyList<WorkerProductionTier> WorkerProductionTiers =>
            workerProductionTiers ?? Array.Empty<WorkerProductionTier>();
    }

    [Serializable]
    public sealed class BuildingProductionLevelConfiguration : BuildingLevelConfigurationBase
    {
        [SerializeField, LabelText("生产周期回合"), Min(1)] private int intervalTurns = 1;
        [SerializeField, LabelText("产出配置")] private BuildingProductionOutputConfiguration[] outputs =
            Array.Empty<BuildingProductionOutputConfiguration>();

        public BuildingProductionLevelConfiguration()
        {
        }

        public BuildingProductionLevelConfiguration(
            int intervalTurns,
            IEnumerable<BuildingProductionOutputConfiguration> outputs)
        {
            this.intervalTurns = Mathf.Max(1, intervalTurns);
            this.outputs = outputs == null
                ? Array.Empty<BuildingProductionOutputConfiguration>()
                : new List<BuildingProductionOutputConfiguration>(outputs).ToArray();
        }

        public override string ConfigurationId => "production";
        public IReadOnlyList<BuildingProductionOutputConfiguration> Outputs =>
            outputs ?? Array.Empty<BuildingProductionOutputConfiguration>();

        public override void Apply(BuildingBase building)
        {
            building.GetRequiredModule<BM_资源产出>().ApplyConfiguration(intervalTurns, outputs);
        }

        public override void Normalize()
        {
            intervalTurns = Mathf.Max(1, intervalTurns);
            outputs ??= Array.Empty<BuildingProductionOutputConfiguration>();
        }
    }

    [Serializable]
    public sealed class BuildingInventoryLevelConfiguration : BuildingLevelConfigurationBase
    {
        [SerializeField, LabelText("提供库存格数"), Min(0)] private int providedSlots;

        public BuildingInventoryLevelConfiguration()
        {
        }

        public BuildingInventoryLevelConfiguration(int providedSlots)
        {
            this.providedSlots = Mathf.Max(0, providedSlots);
        }

        public override string ConfigurationId => "inventory.capacity";
        public int ProvidedSlots => Mathf.Max(0, providedSlots);

        public override void Apply(BuildingBase building)
        {
            building.GetRequiredModule<BM_库存格容量>().SetProvidedSlotCount(ProvidedSlots);
        }

        public override void Normalize()
        {
            providedSlots = Mathf.Max(0, providedSlots);
        }
    }

    [Serializable]
    public sealed class BuildingTechnologyPointLevelConfiguration : BuildingLevelConfigurationBase
    {
        [SerializeField, LabelText("每回合科技点"), Min(0)] private int pointsPerTurn;

        public BuildingTechnologyPointLevelConfiguration()
        {
        }

        public BuildingTechnologyPointLevelConfiguration(int pointsPerTurn)
        {
            this.pointsPerTurn = Mathf.Max(0, pointsPerTurn);
        }

        public override string ConfigurationId => "technology.points";
        public int PointsPerTurn => Mathf.Max(0, pointsPerTurn);

        public override void Apply(BuildingBase building)
        {
            building.GetRequiredModule<BM_科技点产出>()
                .SetProvidedTechnologyPointsPerTurn(PointsPerTurn);
        }

        public override void Normalize()
        {
            pointsPerTurn = Mathf.Max(0, pointsPerTurn);
        }
    }

    [Serializable]
    public sealed class PlayerHomeLevelConfiguration : BuildingLevelConfigurationBase
    {
        [SerializeField, LabelText("提供人口"), Min(0)] private int population = 10;

        public PlayerHomeLevelConfiguration()
        {
        }

        public PlayerHomeLevelConfiguration(int population)
        {
            this.population = Mathf.Max(0, population);
        }

        public override string ConfigurationId => "player_home";
        public int Population => Mathf.Max(0, population);

        public override void Apply(BuildingBase building)
        {
            building.GetRequiredModule<BM_固定人口>().ApplyConfiguration(Population);
        }

        public override void Normalize()
        {
            population = Mathf.Max(0, population);
        }
    }

    [Serializable]
    public sealed class ResidentialHousingLevelConfiguration : BuildingLevelConfigurationBase
    {
        [SerializeField, LabelText("初始人口"), Min(1)] private int initialPopulation = 2;
        [SerializeField, LabelText("最大人口"), Min(1)] private int maxPopulation = 5;
        [SerializeField, AssetsOnly, LabelText("食物物品")] private ItemDefinition foodItemDefinition;
        [SerializeField, LabelText("人口增长间隔回合"), Min(1)] private int growthIntervalTurns = 3;
        [SerializeField, LabelText("失败衰减阈值"), Min(1)] private int failureDecayThreshold = 3;
        [SerializeField, AssetsOnly, LabelText("税收物品")] private ItemDefinition taxItemDefinition;
        [SerializeField, LabelText("税收间隔回合"), Min(1)] private int taxIntervalTurns = 5;

        public ResidentialHousingLevelConfiguration()
        {
        }

        public ResidentialHousingLevelConfiguration(
            int initialPopulation,
            int maxPopulation,
            ItemDefinition foodItemDefinition,
            int growthIntervalTurns,
            int failureDecayThreshold,
            ItemDefinition taxItemDefinition,
            int taxIntervalTurns)
        {
            this.initialPopulation = initialPopulation;
            this.maxPopulation = maxPopulation;
            this.foodItemDefinition = foodItemDefinition;
            this.growthIntervalTurns = growthIntervalTurns;
            this.failureDecayThreshold = failureDecayThreshold;
            this.taxItemDefinition = taxItemDefinition;
            this.taxIntervalTurns = taxIntervalTurns;
            Normalize();
        }

        public override string ConfigurationId => "residential_housing";
        public int InitialPopulation => Mathf.Max(1, initialPopulation);
        public int MaxPopulation => Mathf.Max(InitialPopulation, maxPopulation);
        public ItemDefinition FoodItemDefinition => foodItemDefinition;
        public int GrowthIntervalTurns => Mathf.Max(1, growthIntervalTurns);
        public int FailureDecayThreshold => Mathf.Max(1, failureDecayThreshold);
        public ItemDefinition TaxItemDefinition => taxItemDefinition;
        public int TaxIntervalTurns => Mathf.Max(1, taxIntervalTurns);

        public override void Apply(BuildingBase building)
        {
            building.GetRequiredModule<BM_居民运营>().ApplyConfiguration(
                InitialPopulation,
                MaxPopulation,
                FoodItemDefinition,
                GrowthIntervalTurns,
                FailureDecayThreshold,
                TaxItemDefinition,
                TaxIntervalTurns);
        }

        public override void Normalize()
        {
            initialPopulation = Mathf.Max(1, initialPopulation);
            maxPopulation = Mathf.Max(initialPopulation, maxPopulation);
            growthIntervalTurns = Mathf.Max(1, growthIntervalTurns);
            failureDecayThreshold = Mathf.Max(1, failureDecayThreshold);
            taxIntervalTurns = Mathf.Max(1, taxIntervalTurns);
        }
    }

    [Serializable]
    public sealed class FishingHutLevelConfiguration : BuildingLevelConfigurationBase
    {
        [SerializeField, LabelText("启用特殊捕获")] private bool enableSpecialCatch;
        [SerializeField, AssetsOnly, LabelText("特殊产出物品")] private ItemDefinition specialItemDefinition;
        [SerializeField, LabelText("最低工人数"), Min(1)] private int minimumWorkers = 5;
        [SerializeField, LabelText("触发概率百分比"), Range(0f, 100f)] private float chancePercent = 1f;
        [SerializeField, LabelText("产出数量"), Min(0)] private int amount = 1;

        public FishingHutLevelConfiguration()
        {
        }

        public FishingHutLevelConfiguration(
            bool enableSpecialCatch,
            ItemDefinition specialItemDefinition,
            int minimumWorkers,
            float chancePercent,
            int amount)
        {
            this.enableSpecialCatch = enableSpecialCatch;
            this.specialItemDefinition = specialItemDefinition;
            this.minimumWorkers = minimumWorkers;
            this.chancePercent = chancePercent;
            this.amount = amount;
            Normalize();
        }

        public override string ConfigurationId => "fishing_hut";
        public bool EnableSpecialCatch => enableSpecialCatch;
        public ItemDefinition SpecialItemDefinition => specialItemDefinition;
        public int MinimumWorkers => Mathf.Max(1, minimumWorkers);
        public float ChancePercent => Mathf.Clamp(chancePercent, 0f, 100f);
        public int Amount => Mathf.Max(0, amount);

        public override void Apply(BuildingBase building)
        {
            building.GetRequiredModule<BM_稀有产出>().ApplyConfiguration(
                EnableSpecialCatch,
                SpecialItemDefinition,
                MinimumWorkers,
                ChancePercent,
                Amount);
        }

        public override void Normalize()
        {
            minimumWorkers = Mathf.Max(1, minimumWorkers);
            chancePercent = Mathf.Clamp(chancePercent, 0f, 100f);
            amount = Mathf.Max(0, amount);
        }
    }
}
