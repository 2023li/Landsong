using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.GridSystem;
using Landsong.InventorySystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools.Buildings
{
    // 创建页字段集中定义在这里，使标签、Tooltip、默认值和校验输入保持同一来源。
    internal enum BuildingModuleTemplate
    {
        [InspectorName("基础建筑（模块配置）")]
        Basic = 0,

        [InspectorName("岗位生产建筑（模块配置）")]
        WorkforceProduction = 1
    }

    [Serializable]
    internal sealed class BuildingCostDraft
    {
        [InspectorName("物品"), Tooltip("本费用项消耗的物品定义。物品与数量必须同时填写。")]
        public ItemDefinition Item = null;

        [InspectorName("数量"), Tooltip("消耗数量。0 表示空费用项，配置物品时必须大于 0。"), Min(0)]
        public int Amount = 0;
    }

    [Serializable]
    internal sealed class BuildingConstructionTurnDraft
    {
        [InspectorName("本回合费用"), Tooltip("施工推进这一回合时扣除的资源；允许为空，仍然计为一个施工回合。")]
        public List<BuildingCostDraft> Costs = new List<BuildingCostDraft>();
    }

    [Serializable]
    internal sealed class BuildingProductionTierDraft
    {
        [InspectorName("最低工人"), Tooltip("达到该工人数时启用本档产量。同一等级内不能重复。"), Min(1)]
        public int MinimumWorkers = 1;

        [InspectorName("产量"), Tooltip("每个生产周期产出的物品数量，必须大于 0。"), Min(0)]
        public int Amount = 1;
    }

    [Serializable]
    internal sealed class BuildingStyleDraft
    {
        [InspectorName("StyleId"), Tooltip("样式的稳定存档标识，只能使用小写字母、数字和下划线，例如 tree_01。")]
        public string StyleId = string.Empty;

        [InspectorName("显示名"), Tooltip("显示给玩家的样式名称；可以与建筑家族显示名不同。")]
        public string DisplayName = string.Empty;

        [InspectorName("菜单图标"), Tooltip("玩家选择该样式时显示的图标；允许暂时留空。")]
        public Sprite Icon = null;

        [InspectorName("LV1 View Prefab"), Tooltip("该样式 LV1 的纯表现 Prefab。不得包含建筑脚本、表现控制器或 Collider；允许后续回填。")]
        public GameObject Level1View = null;
    }

    [Serializable]
    internal sealed class BuildingAuthoringDraft
    {
        [InspectorName("FamilyId"), Tooltip("建筑家族的稳定唯一标识，格式为 building.<snake_case>。任务、科技、蓝图和存档都依赖它。")]
        public string FamilyId = "building.new_building";

        [InspectorName("显示名称"), Tooltip("建筑在菜单和界面中显示给玩家的名称。修改它不会改变 FamilyId。")]
        public string DisplayName = "新建筑";

        [InspectorName("资产名"), Tooltip("用于生成 Family、ModuleSet、Presentation 和 Runtime Prefab 的文件名，不要带 LV1、LV2 等等级后缀。不会生成建筑脚本。")]
        public string AssetName = "NewBuilding";

        [InspectorName("模块模板"), Tooltip("基础模板只创建通用运行时；岗位生产模板额外创建岗位运营、资源产出模块和等级配置。")]
        public BuildingModuleTemplate ModuleTemplate = BuildingModuleTemplate.Basic;

        [InspectorName("分类"), Tooltip("用于建造菜单筛选和其他按建筑类别判断的规则；支持组合分类。")]
        public BuildingCategory Category = BuildingCategory.通用;

        [InspectorName("建筑图标"), Tooltip("建筑家族在建造菜单和详情界面使用的默认图标；允许后续回填。")]
        public Sprite Icon;

        [InspectorName("固定占地"), Tooltip("建筑在网格中的宽×高。占地在施工和所有等级中固定不变，两个值都必须大于 0。")]
        public Vector2Int Footprint = Vector2Int.one;

        [InspectorName("忽略地形要求"), Tooltip("启用后放置校验不要求指定地形 Key；通常只有特殊建筑使用。")]
        public bool IgnoreTerrainRequirement;

        [InspectorName("需要的地形 Key"), Tooltip("建筑允许放置的地形标识。未忽略地形要求时至少填写一个，默认是陆地。")]
        public List<string> RequiredTerrainKeys = new List<string> { GridTerrainKeys.Land };

        [InspectorName("移动阻力"), Tooltip("建筑占地区域对移动/寻路施加的阻力值；0 表示不额外增加阻力。")]
        public int MovementResistance;

        [InspectorName("放置费用"), Tooltip("确认放置建筑时立即扣除的费用，与之后逐回合扣除的施工费用相互独立。")]
        public List<BuildingCostDraft> PlacementCosts = new List<BuildingCostDraft>();

        [InspectorName("施工回合"), Tooltip("每个元素代表一个施工回合及该回合消耗。至少保留一个；空费用回合仍需要推进一次施工。")]
        public List<BuildingConstructionTurnDraft> ConstructionTurns =
            new List<BuildingConstructionTurnDraft> { new BuildingConstructionTurnDraft() };

        [InspectorName("初始生成等级数量"), Tooltip("创建时生成的连续等级定义数量。建筑运行时仍从 LV1 开始；LV1 可用，LV2～LVN 只生成默认关闭的配置骨架。"), Min(1)]
        public int InitialLevelCount = 1;

        [InspectorName("初始未拥有蓝图"), Tooltip("启用后新游戏不会默认解锁该家族，需要由科技、任务或其他正式流程授予蓝图。")]
        public bool BlueprintInitiallyLocked;

        [InspectorName("未解锁时隐藏"), Tooltip("蓝图未解锁时是否从建造菜单完全隐藏；关闭时可以显示为锁定状态。")]
        public bool HideWhenBlueprintLocked;

        [InspectorName("菜单排序"), Tooltip("建造菜单中的排序值；具体升降序由菜单实现决定，同值时应使用稳定的次级顺序。")]
        public int BuildMenuSortOrder;

        [InspectorName("最大建造数量"), Tooltip("玩家可拥有的该家族建筑上限。0 表示不限制。"), Min(0)]
        public int MaxBuildCount;

        [InspectorName("开发完成"), Tooltip("表示该建筑内容是否允许作为正式可用内容。未完成建筑可被菜单或其他入口过滤。")]
        public bool IsDevelopmentCompleted = true;

        [InspectorName("资源提供点"), Tooltip("启用后建筑可以作为资源网络的提供节点，供需要连接的建筑查找。")]
        public bool IsResourceProviderPoint;

        [InspectorName("资源提供优先级"), Tooltip("同时存在多个资源提供点时使用的选择优先级；数值越高的具体含义以资源网络实现为准。")]
        public int ResourceProviderPriority;

        [InspectorName("行动力预算"), Tooltip("建筑每回合可供模块或行为消耗的行动力预算；不能小于 0。"), Min(0)]
        public int BuildingActionPower = 100;

        [InspectorName("施工 View Prefab"), Tooltip("施工阶段加载的独立纯表现 Prefab。允许暂时留空，不会阻塞玩法资产创建。")]
        public GameObject ConstructionViewPrefab;

        [InspectorName("默认 LV1 View Prefab"), Tooltip("无样式建筑进入运营态时使用的默认纯表现 Prefab。允许后续通过 Presentation 替换或补充高等级表现。")]
        public GameObject DefaultOperationalViewPrefab;

        [InspectorName("视觉样式"), Tooltip("同一建筑家族可由玩家选择的外观变体。样式不会创建新的 FamilyId、脚本或 Runtime Prefab。")]
        public List<BuildingStyleDraft> Styles = new List<BuildingStyleDraft>();

        [InspectorName("最大工人"), Tooltip("岗位生产建筑在本级配置中允许容纳的最大工人数。"), Min(1)]
        public int MaxWorkers = 3;

        [InspectorName("初始工人"), Tooltip("建筑首次进入岗位运营时拥有的工人数，必须在 0～最大工人之间。"), Min(0)]
        public int InitialWorkers;

        [InspectorName("基础岗位吸引力"), Tooltip("不考虑补贴和其他修正时的岗位吸引力基础值。"), Min(0f)]
        public float BaseJobAttraction = 55f;

        [InspectorName("单次招工金币"), Tooltip("立即补充单个缺失工人所需的金币成本。"), Min(0)]
        public int RecruitCost = 10;

        [InspectorName("自动补贴"), Tooltip("建筑创建后是否默认启用岗位补贴逻辑。")]
        public bool AutoSubsidy;

        [InspectorName("稳定工人目标"), Tooltip("自动补贴希望维持的工人数，必须在 0～最大工人之间。0 表示不指定目标。"), Min(0)]
        public int TargetStableWorkers;

        [InspectorName("金币物品"), Tooltip("招工和岗位补贴使用的金币物品定义。运行时会从该资产取得稳定 ItemId。")]
        public ItemDefinition GoldItemDefinition;

        [InspectorName("生产周期"), Tooltip("完成一次资源产出需要推进的运营回合数，必须大于 0。"), Min(1)]
        public int ProductionIntervalTurns = 3;

        [InspectorName("产出物品"), Tooltip("岗位生产建筑每个周期产出的物品定义。岗位生产模板必须填写。")]
        public ItemDefinition ProductionItem;

        [InspectorName("工人数产量表"), Tooltip("按达到的最低工人数决定每周期产量。最低工人数不能重复，且不能超过最大工人。")]
        public List<BuildingProductionTierDraft> ProductionTiers =
            new List<BuildingProductionTierDraft>
            {
                new BuildingProductionTierDraft { MinimumWorkers = 2, Amount = 1 },
                new BuildingProductionTierDraft { MinimumWorkers = 3, Amount = 2 }
            };

        [InspectorName("家族资产目录"), Tooltip("BuildingFamilyDefinition 的输出目录。通常保持标准路径，且必须位于 Assets 下。")]
        public string FamilyFolder = "Assets/Landsong/Objects/SO/Buildings/Families";

        [InspectorName("模块资产目录"), Tooltip("BuildingModuleSetDefinition 的输出目录。通常保持标准路径，且必须位于 Assets 下。")]
        public string ModuleFolder = "Assets/Landsong/Objects/SO/Buildings/Modules";

        [InspectorName("表现资产目录"), Tooltip("BuildingPresentationDefinition 的输出目录。这里只存表现定义，不存具体 View Prefab。")]
        public string PresentationFolder = "Assets/Landsong/Objects/SO/Buildings/Presentations";

        [InspectorName("Runtime Prefab 目录"), Tooltip("唯一轻量 Runtime Prefab 的输出目录。不得与纯美术 BuildingViews 目录混用。")]
        public string RuntimePrefabFolder = "Assets/Landsong/Objects/Prefabs/BuildingsRuntime";

        public bool UsesWorkforceProduction =>
            ModuleTemplate == BuildingModuleTemplate.WorkforceProduction;

        public void ApplyTemplateDefaults()
        {
            switch (ModuleTemplate)
            {
                case BuildingModuleTemplate.WorkforceProduction:
                    Category = BuildingCategory.Production;
                    MovementResistance = 0;
                    IsResourceProviderPoint = false;
                    break;
                case BuildingModuleTemplate.Basic:
                    Category = BuildingCategory.通用;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void ResetToDefaults()
        {
            FamilyId = "building.new_building";
            DisplayName = "新建筑";
            AssetName = "NewBuilding";
            ModuleTemplate = BuildingModuleTemplate.Basic;
            Category = BuildingCategory.通用;
            Icon = null;
            Footprint = Vector2Int.one;
            IgnoreTerrainRequirement = false;
            RequiredTerrainKeys = new List<string> { GridTerrainKeys.Land };
            MovementResistance = 0;
            PlacementCosts = new List<BuildingCostDraft>();
            ConstructionTurns = new List<BuildingConstructionTurnDraft>
            {
                new BuildingConstructionTurnDraft()
            };
            InitialLevelCount = 1;
            BlueprintInitiallyLocked = false;
            HideWhenBlueprintLocked = false;
            BuildMenuSortOrder = 0;
            MaxBuildCount = 0;
            IsDevelopmentCompleted = true;
            IsResourceProviderPoint = false;
            ResourceProviderPriority = 0;
            BuildingActionPower = 100;
            ConstructionViewPrefab = null;
            DefaultOperationalViewPrefab = null;
            Styles = new List<BuildingStyleDraft>();
            MaxWorkers = 3;
            InitialWorkers = 0;
            BaseJobAttraction = 55f;
            RecruitCost = 10;
            AutoSubsidy = false;
            TargetStableWorkers = 0;
            GoldItemDefinition = null;
            ProductionIntervalTurns = 3;
            ProductionItem = null;
            ProductionTiers = new List<BuildingProductionTierDraft>
            {
                new BuildingProductionTierDraft { MinimumWorkers = 2, Amount = 1 },
                new BuildingProductionTierDraft { MinimumWorkers = 3, Amount = 2 }
            };
        }
    }

    internal readonly struct BuildingAuthoringPaths
    {
        public BuildingAuthoringPaths(BuildingAuthoringDraft draft)
        {
            FamilyAssetPath = CombineAssetPath(draft.FamilyFolder, $"{draft.AssetName}.asset");
            ModuleAssetPath = CombineAssetPath(draft.ModuleFolder, $"{draft.AssetName}Modules.asset");
            PresentationAssetPath = CombineAssetPath(
                draft.PresentationFolder,
                $"{draft.AssetName}Presentation.asset");
            RuntimePrefabPath = CombineAssetPath(
                draft.RuntimePrefabFolder,
                $"{draft.AssetName}Runtime.prefab");
        }

        public string FamilyAssetPath { get; }
        public string ModuleAssetPath { get; }
        public string PresentationAssetPath { get; }
        public string RuntimePrefabPath { get; }
        private static string CombineAssetPath(string folder, string fileName)
        {
            folder = string.IsNullOrWhiteSpace(folder)
                ? "Assets"
                : folder.Trim().Replace('\\', '/').TrimEnd('/');
            return $"{folder}/{fileName}";
        }
    }
}
