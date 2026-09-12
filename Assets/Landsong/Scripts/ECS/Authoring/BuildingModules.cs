using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Authoring
{
    [Serializable, HideReferenceObjectPicker] public abstract class BuildingModuleEntry
    {
#if UNITY_EDITOR
        public string Summary
        {
            get
            {
                var parts=new System.Collections.Generic.List<string>();var type=GetType();
                foreach(var name in new[]{"Level","Stage","TargetLevel","Item","Soldier","Crop","Hero","SlotType","Terrain","Quantity","Count","Capacity","Slots","MinimumWorkers","MaximumWorkers"})
                {
                    var field=type.GetField(name);if(field==null)continue;var value=field.GetValue(this);
                    if(value is GameDefinitionAsset asset){parts.Add(asset.Data.Name);continue;}
                    var label=Attribute.GetCustomAttribute(field,typeof(LabelTextAttribute)) as LabelTextAttribute;
                    parts.Add((label?.Text??name)+"："+value);
                }
                return parts.Count>0?string.Join(" · ",parts):"配置条目";
            }
        }
#endif
    }
    public enum EnvironmentKind {
        [LabelText("生产")] 生产 = 10,
        [LabelText("美观")] 美观 = 20,
        [LabelText("医疗")] 医疗 = 30,
        [LabelText("治安")] 治安 = 40
    }
    public enum InvitationKind {
        [LabelText("贸易")] 贸易 = 0,
        [LabelText("建设")] 建设 = 1,
        [LabelText("民生")] 民生 = 2,
        [LabelText("探索")] 探索 = 3
    }
    public enum EffectStacking {
        [LabelText("同组最高")] 同组最高 = 0,
        [LabelText("相加")] 相加 = 10,
        [LabelText("同类最高")] 同类最高 = 20
    }
    [AttributeUsage(AttributeTargets.Field)] public sealed class ContentReferenceAttribute : PropertyAttribute
    { public readonly ContentKind Kind;public readonly ContentKind[] Kinds;public readonly bool Optional;
        public ContentReferenceAttribute(ContentKind kind,bool optional=false){Kind=kind;Kinds=new[]{kind};Optional=optional;}
        public ContentReferenceAttribute(bool optional,params ContentKind[] kinds){Kinds=kinds;Optional=optional;Kind=kinds.Length>0?kinds[0]:default;} }
    [Serializable, HideReferenceObjectPicker] public sealed class BuildingModules
    {
        [LabelText("建造与施工")] public ConstructionModule Construction=new ConstructionModule();
        [LabelText("升级与经验")] public UpgradeModule Upgrade=new UpgradeModule();
        [LabelText("维护与修复")] public MaintenanceModule Maintenance=new MaintenanceModule();
        [LabelText("物品生产")] public ProductionModule Production=new ProductionModule();
        [LabelText("任务与邀约")] public QuestsModule Quests=new QuestsModule();
        [LabelText("放置地形")] public PlacementModule Placement=new PlacementModule();
        [LabelText("仓储与资源提供")] public StorageModule Storage=new StorageModule();
        [LabelText("岗位与补贴")] public WorkforceModule Workforce=new WorkforceModule();
        [LabelText("人口与住宅")] public HousingModule Housing=new HousingModule();
        [LabelText("科研")] public ResearchModule Research=new ResearchModule();
        [LabelText("种植")] public FarmingModule Farming=new FarmingModule();
        [LabelText("采集")] public GatheringModule Gathering=new GatheringModule();
        [LabelText("驻军")] public GarrisonModule Garrison=new GarrisonModule();
        [LabelText("神殿供奉")] public SanctumModule Sanctum=new SanctumModule();
        [LabelText("市场")] public MarketModule Market=new MarketModule();
        [LabelText("范围效果")] public EffectsModule Effects=new EffectsModule();
        [LabelText("情报与警铃")] public DefenceModule Defence=new DefenceModule();
        [LabelText("远征")] public ExpeditionsModule Expeditions=new ExpeditionsModule();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ConstructionModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("放置材料"), ListDrawerSettings(ListElementLabelName="Summary")] public PlacementCostEntry[] PlacementCosts=Array.Empty<PlacementCostEntry>();
        [ShowIf(nameof(Enabled)), LabelText("施工材料"), ListDrawerSettings(ListElementLabelName="Summary")] public ConstructionCostEntry[] StageCosts=Array.Empty<ConstructionCostEntry>();
        [ShowIf(nameof(Enabled)), LabelText("施工产物"), ListDrawerSettings(ListElementLabelName="Summary")] public ConstructionOutputEntry[] StageOutputs=Array.Empty<ConstructionOutputEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class UpgradeModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("升级材料"), ListDrawerSettings(ListElementLabelName="Summary")] public UpgradeCostEntry[] Costs=Array.Empty<UpgradeCostEntry>();
        [ShowIf(nameof(Enabled)), LabelText("升级所需工人"), ListDrawerSettings(ListElementLabelName="Summary")] public UpgradeWorkersEntry[] Workers=Array.Empty<UpgradeWorkersEntry>();
        [ShowIf(nameof(Enabled)), LabelText("升级所需居民"), ListDrawerSettings(ListElementLabelName="Summary")] public UpgradePopulationEntry[] Residents=Array.Empty<UpgradePopulationEntry>();
        [ShowIf(nameof(Enabled)), LabelText("升级需维护"), ListDrawerSettings(ListElementLabelName="Summary")] public UpgradeMaintenanceEntry[] MaintenanceRequirements=Array.Empty<UpgradeMaintenanceEntry>();
        [ShowIf(nameof(Enabled)), LabelText("经验成长"), ListDrawerSettings(ListElementLabelName="Summary")] public BuildingExperienceEntry[] Experience=Array.Empty<BuildingExperienceEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class MaintenanceModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("默认修复回合数（0 = 跟随建筑施工时长）"),MinValue(0)] public int RepairTurns;
        [ShowIf(nameof(Enabled)), LabelText("每回合维护材料"), ListDrawerSettings(ListElementLabelName="Summary")] public MaintenanceEntry[] Costs=Array.Empty<MaintenanceEntry>();
        [ShowIf(nameof(Enabled)), LabelText("修复材料"), ListDrawerSettings(ListElementLabelName="Summary")] public RepairMaterialEntry[] Repairs=Array.Empty<RepairMaterialEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ProductionModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("生产投入材料"), ListDrawerSettings(ListElementLabelName="Summary")] public InputEntry[] Inputs=Array.Empty<InputEntry>();
        [ShowIf(nameof(Enabled)), LabelText("基础生产周期"), ListDrawerSettings(ListElementLabelName="Summary")] public ProductionEntry[] Cycles=Array.Empty<ProductionEntry>();
        [ShowIf(nameof(Enabled)), LabelText("工人加工周期"), ListDrawerSettings(ListElementLabelName="Summary")] public ProcessingTierEntry[] ProcessingTiers=Array.Empty<ProcessingTierEntry>();
        [ShowIf(nameof(Enabled)), LabelText("生产产出档位"), ListDrawerSettings(ListElementLabelName="Summary")] public ProductionOutputEntry[] Outputs=Array.Empty<ProductionOutputEntry>();
        [ShowIf(nameof(Enabled)), LabelText("随机产出"), ListDrawerSettings(ListElementLabelName="Summary")] public RareProductionEntry[] RareOutputs=Array.Empty<RareProductionEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class QuestsModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("刷新邀约费用"), ListDrawerSettings(ListElementLabelName="Summary")] public QuestRecruitCostEntry[] InvitationCosts=Array.Empty<QuestRecruitCostEntry>();
        [ShowIf(nameof(Enabled)), LabelText("任务容量"), ListDrawerSettings(ListElementLabelName="Summary")] public QuestCapacityEntry[] Capacity=Array.Empty<QuestCapacityEntry>();
        [ShowIf(nameof(Enabled)), LabelText("邀约来源"), ListDrawerSettings(ListElementLabelName="Summary")] public QuestInvitationEntry[] Invitations=Array.Empty<QuestInvitationEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class PlacementModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("必须包含的地形"), ListDrawerSettings(ListElementLabelName="Summary")] public RequiredTerrainEntry[] RequiredTerrains=Array.Empty<RequiredTerrainEntry>();
        [ShowIf(nameof(Enabled)), LabelText("至少包含一种地形"), ListDrawerSettings(ListElementLabelName="Summary")] public AnyTerrainEntry[] AlternativeTerrains=Array.Empty<AnyTerrainEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class StorageModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("资源提供点"), ListDrawerSettings(ListElementLabelName="Summary")] public ResourceProviderEntry[] Providers=Array.Empty<ResourceProviderEntry>();
        [ShowIf(nameof(Enabled)), LabelText("库存容量"), ListDrawerSettings(ListElementLabelName="Summary")] public WarehouseLevel[] Warehouses=Array.Empty<WarehouseLevel>();
        [ShowIf(nameof(Enabled)), LabelText("仓储运行条件"), ListDrawerSettings(ListElementLabelName="Summary")] public StorageConditionEntry[] Conditions=Array.Empty<StorageConditionEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class WorkforceModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("岗位配置"), ListDrawerSettings(ListElementLabelName="Summary")] public WorkforceLevel[] Levels=Array.Empty<WorkforceLevel>();
        [ShowIf(nameof(Enabled)), LabelText("工作效率档位"),Tooltip("显式配置各等级的人数区间，含 0 人；必须连续覆盖岗位容量，不重叠。效果由该人数对应的模块规则提供。"), ListDrawerSettings(ListElementLabelName="Summary")] public WorkerEfficiencyTierEntry[] EfficiencyTiers=Array.Empty<WorkerEfficiencyTierEntry>();
        [ShowIf(nameof(Enabled)), LabelText("附近居民吸引力"), ListDrawerSettings(ListElementLabelName="Summary")] public BuildingAttractionEntry[] Attraction=Array.Empty<BuildingAttractionEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class WorkerEfficiencyTierEntry : BuildingModuleEntry
    {
        [LabelText("建筑等级（0 = 通用）"),MinValue(0)] public int Level;
        [LabelText("最少工人"),MinValue(0)] public int MinimumWorkers;
        [LabelText("最多工人（含）"),MinValue(0)] public int MaximumWorkers;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class HousingModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("基础人口"), ListDrawerSettings(ListElementLabelName="Summary")] public BasePopulationEntry[] Population=Array.Empty<BasePopulationEntry>();
        [ShowIf(nameof(Enabled)), LabelText("住宅配置"), ListDrawerSettings(ListElementLabelName="Summary")] public ResidenceLevel[] Residences=Array.Empty<ResidenceLevel>();
        [ShowIf(nameof(Enabled)), LabelText("居民食谱"), ListDrawerSettings(ListElementLabelName="Summary")] public ResidentFoodEntry[] Food=Array.Empty<ResidentFoodEntry>();
        [ShowIf(nameof(Enabled)), LabelText("住宅税收"), ListDrawerSettings(ListElementLabelName="Summary")] public ResidenceTaxEntry[] Taxes=Array.Empty<ResidenceTaxEntry>();
        [ShowIf(nameof(Enabled)), LabelText("环境需求"), ListDrawerSettings(ListElementLabelName="Summary")] public EnvironmentRequirementEntry[] Environment=Array.Empty<EnvironmentRequirementEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ResearchModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("科研产出"), ListDrawerSettings(ListElementLabelName="Summary")] public ResearchLevel[] Levels=Array.Empty<ResearchLevel>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class FarmingModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("可种植作物"), ListDrawerSettings(ListElementLabelName="Summary")] public AllowedCropEntry[] Crops=Array.Empty<AllowedCropEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class GatheringModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("采集配置"), ListDrawerSettings(ListElementLabelName="Summary")] public GatheringLevel[] Levels=Array.Empty<GatheringLevel>();
        [ShowIf(nameof(Enabled)), LabelText("最终采集奖励"), ListDrawerSettings(ListElementLabelName="Summary")] public GatheringRewardEntry[] Rewards=Array.Empty<GatheringRewardEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class GarrisonModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("每回合募兵上限（0 = 驻军容量）"),MinValue(0)] public int RecruitmentLimitPerTurn;
        [ShowIf(nameof(Enabled)), LabelText("驻军容量"), ListDrawerSettings(ListElementLabelName="Summary")] public GarrisonLevel[] Levels=Array.Empty<GarrisonLevel>();
        [ShowIf(nameof(Enabled)), LabelText("开局驻军"), ListDrawerSettings(ListElementLabelName="Summary")] public InitialGarrisonEntry[] InitialUnits=Array.Empty<InitialGarrisonEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class SanctumModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("英雄供奉"), ListDrawerSettings(ListElementLabelName="Summary")] public SanctumLevel[] Levels=Array.Empty<SanctumLevel>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class MarketModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("市场收入"), ListDrawerSettings(ListElementLabelName="Summary")] public MarketLevel[] Levels=Array.Empty<MarketLevel>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class EffectsModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("范围效果"), ListDrawerSettings(ListElementLabelName="Summary")] public SpatialEffectEntry[] Spatial=Array.Empty<SpatialEffectEntry>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class DefenceModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("情报产出"), ListDrawerSettings(ListElementLabelName="Summary")] public IntelligenceLevel[] Intelligence=Array.Empty<IntelligenceLevel>();
        [ShowIf(nameof(Enabled)), LabelText("警铃集结"), ListDrawerSettings(ListElementLabelName="Summary")] public BellLevel[] Bells=Array.Empty<BellLevel>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ExpeditionsModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [ShowIf(nameof(Enabled)), LabelText("远征所"), ListDrawerSettings(ListElementLabelName="Summary")] public ExpeditionSiteLevel[] Levels=Array.Empty<ExpeditionSiteLevel>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class PlacementCostEntry : BuildingModuleEntry
    {
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ConstructionCostEntry : BuildingModuleEntry
    {
        [LabelText("施工阶段（0 = 每期）"), MinValue(0)] public int Stage = 1;
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ConstructionOutputEntry : BuildingModuleEntry
    {
        [LabelText("施工阶段（0 = 每期）"), MinValue(0)] public int Stage = 1;
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class UpgradeCostEntry : BuildingModuleEntry
    {
        [LabelText("升级目标等级（0 = 所有升级）"), MinValue(0)] public int TargetLevel = 1;
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class MaintenanceEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class InputEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class QuestRecruitCostEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class RepairMaterialEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)] public int Quantity = 1;
        [LabelText("修复回合数（0 = 使用建筑设置）"), MinValue(0)] public int RepairTurns;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class UpgradeWorkersEntry : BuildingModuleEntry
    {
        [LabelText("升级目标等级（0 = 所有升级）"), MinValue(0)] public int TargetLevel = 1;
        [LabelText("所需人数"), MinValue(0)] public int Required;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class UpgradePopulationEntry : BuildingModuleEntry
    {
        [LabelText("升级目标等级（0 = 所有升级）"), MinValue(0)] public int TargetLevel = 1;
        [LabelText("所需人数"), MinValue(0)] public int Required;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class UpgradeMaintenanceEntry : BuildingModuleEntry
    {
        [LabelText("升级目标等级（0 = 所有升级）"), MinValue(0)] public int TargetLevel = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class BuildingExperienceEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("每回合经验"), MinValue(0)] public int ExperiencePerTurn;
        [LabelText("升级所需经验"), MinValue(0)] public int UpgradeExperience;
        [LabelText("获取经验所需工人"), MinValue(0)] public int RequiredWorkers;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class RequiredTerrainEntry : BuildingModuleEntry
    {
        [LabelText("地形标签")] public string Terrain;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class AnyTerrainEntry : BuildingModuleEntry
    {
        [LabelText("地形标签")] public string Terrain;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ResourceProviderEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class WorkforceLevel : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("补贴和招聘货币（空 = 默认金币）"), ContentReference(ContentKind.Item,true)] public GameDefinitionAsset Currency;
        [LabelText("岗位上限"), MinValue(0)] public int Capacity;
        [LabelText("初始工人"), MinValue(0)] public int InitialWorkers;
        [LabelText("默认启用补贴")] public bool InitialSubsidy;
        [LabelText("基础吸引力"), MinValue(0)] public float BaseAttraction;
        [LabelText("单人招聘费用"), MinValue(0)] public float RecruitmentCost;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class BuildingAttractionEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("附近居民范围"),MinValue(0)] public float Radius;
        [LabelText("每名居民吸引力加成")] public float PerResidentBonus;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class BasePopulationEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("基础人口数量"), MinValue(0)] public int Population;
        [LabelText("是否王国核心")] public bool IsCore;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ResidenceLevel : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("居民容量"), MinValue(0)] public int Capacity;
        [LabelText("完工居民数"), MinValue(0)] public int InitialResidents;
        [LabelText("连续缺粮衰退阈值"), MinValue(0)] public int StarvationThreshold;
        [LabelText("人口增长间隔（回合）"), MinValue(1)] public int GrowthInterval = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ResidentFoodEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("食物组"), ContentReference(ContentKind.ItemGroup,false)] public GameDefinitionAsset FoodGroup;
        [LabelText("所需品种"), MinValue(0)] public int Varieties;
        [LabelText("每人每品种消耗"), MinValue(0)] public int AmountPerResident;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ResidenceTaxEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("每居民税收"), MinValue(0)] public int PerResident;
        [LabelText("税收间隔"), MinValue(0)] public int Interval = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class EnvironmentRequirementEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("需求数值"), MinValue(0)] public int RequiredValue;
        [LabelText("环境种类")] public EnvironmentKind Type;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class WarehouseLevel : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("库存槽类型"), ContentReference(ContentKind.SlotType,false)] public GameDefinitionAsset SlotType;
        [LabelText("库存格数"), MinValue(0)] public int Slots;
        [LabelText("启用所需工人"), MinValue(0)] public int RequiredWorkers;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class StorageConditionEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("岗位门槛"), MinValue(0)] public int RequiredWorkers;
        [LabelText("维护失败损耗倍率（百分比）"), MinValue(0)] public int MaintenanceLossPercent;
        [LabelText("维护不足吸引力惩罚"), MinValue(0)] public float AttractionPenalty;
        [LabelText("缺工损耗倍率"), MinValue(0)] public float UnderstaffedLossMultiplier;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ProductionEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("生产间隔"), MinValue(0)] public int Interval = 1;
        [LabelText("所需工人"), MinValue(0)] public int RequiredWorkers;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ProcessingTierEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("生产间隔"), MinValue(0)] public int Interval = 1;
        [LabelText("所需工人"), MinValue(0)] public int RequiredWorkers;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ProductionOutputEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)] public int Quantity = 1;
        [LabelText("最少工人"), MinValue(0)] public int MinimumWorkers;
        [LabelText("最多工人（0 = 不限）"), MinValue(0)] public int MaximumWorkers;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class RareProductionEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)] public int Quantity = 1;
        [LabelText("所需工人"), MinValue(0)] public int RequiredWorkers;
        [LabelText("概率"), Range(0,1)] public float Probability;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ResearchLevel : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("每回合科研值"), MinValue(0)] public int PointsPerTurn;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class AllowedCropEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("作物"), ContentReference(ContentKind.Crop,false)] public GameDefinitionAsset Crop;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class GatheringLevel : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("每次采集物品（空 = 最后一次发放奖励）"), ContentReference(ContentKind.Item,true)] public GameDefinitionAsset Item;
        [LabelText("可采集次数"), MinValue(0)] public int Uses;
        [LabelText("每次采集数量"), MinValue(0)] public int AmountPerUse;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class GatheringRewardEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("物品"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class GarrisonLevel : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("驻军容量"), MinValue(0)] public int Capacity;
        [LabelText("每批出勤人数"), MinValue(0)] public int DeploymentBatchSize = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class InitialGarrisonEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("兵种"), ContentReference(ContentKind.Soldier,false)] public GameDefinitionAsset Soldier;
        [LabelText("人数"), MinValue(0)] public int Count = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class SanctumLevel : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("英雄"), ContentReference(ContentKind.Hero,false)] public GameDefinitionAsset Hero;
        [LabelText("供奉所需工人"), MinValue(0)] public int RequiredWorkers;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class QuestCapacityEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("承接任务槽位"), MinValue(0)] public int Slots;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class QuestInvitationEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("邀约槽位"), MinValue(0)] public int Slots;
        [LabelText("邀约种类")] public InvitationKind Type;
        [LabelText("最短刷新回合"), MinValue(0)] public int MinimumRefreshTurns;
        [LabelText("最长刷新回合"), MinValue(1)] public int MaximumRefreshTurns = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class MarketLevel : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("收入货币"), ContentReference(ContentKind.Item,false)] public GameDefinitionAsset Currency;
        [LabelText("每市场点所需流转价值"), MinValue(0)] public int ValuePerMarketPoint;
        [LabelText("收入比例"), Range(0,1)] public float IncomeRatio;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class SpatialEffectEntry : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("指定建筑（空 = 所有建筑）"), ContentReference(ContentKind.Building,true)] public GameDefinitionAsset Building;
        [LabelText("效果数值"), MinValue(0)] public int Magnitude;
        [LabelText("效果种类")] public EnvironmentKind Type;
        [LabelText("所需工人"), MinValue(0)] public int RequiredWorkers;
        [LabelText("曼哈顿半径"), MinValue(0)] public float Radius;
        [LabelText("叠加方式")] public EffectStacking Stacking;
        [LabelText("效果分组")] public string Group;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class IntelligenceLevel : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("所需科技（可选）"), ContentReference(ContentKind.Technology,true)] public GameDefinitionAsset Technology;
        [LabelText("情报点数"), MinValue(0)] public int Points;
        [LabelText("所需工人"), MinValue(0)] public int RequiredWorkers;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class BellLevel : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("集结半径"), MinValue(0)] public float Radius;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ExpeditionSiteLevel : BuildingModuleEntry
    {
        [LabelText("适用等级（0 = 全部）"), MinValue(0)] public int Level = 1;
        [LabelText("最少派遣人数"), MinValue(0)] public int MinimumCrew;
        [LabelText("最多派遣人数"), MinValue(0)] public int MaximumCrew;
        [LabelText("满员奖励加成"), MinValue(0)] public float FullCrewRewardBonus;
    }
}
