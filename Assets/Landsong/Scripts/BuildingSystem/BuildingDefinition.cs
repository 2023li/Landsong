using System;
using System.Collections.Generic;
using Landsong.ConditionSystem;
using Landsong.GridSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    public sealed class BuildingDefinition
    {
        private static readonly string[] DefaultRequiredTerrainKeys = { GridTerrainKeys.Land };

        [TitleGroup("基础信息")]
        [HorizontalGroup("基础信息/Split", Width = 0.72f)]
        [VerticalGroup("基础信息/Split/Left")]
        [LabelText("建筑家族 ID")]
        [ValidateInput(nameof(HasValidFamilyId), "建筑家族 ID 不能为空。")]
        [SerializeField, InspectorName("建筑家族 ID")] private string familyId;

        [VerticalGroup("基础信息/Split/Left")]
        [LabelText("显示名称")]
        [SerializeField, InspectorName("显示名称")] private string displayName;

        [VerticalGroup("基础信息/Split/Left")]
        [LabelText("分类")]
        [EnumToggleButtons]
        [SerializeField, InspectorName("分类")] private BuildingCategory category = BuildingCategory.None;

        [HorizontalGroup("基础信息/Split", Width = 88)]
        [PreviewField(72)]
        [HideLabel]
        [SerializeField, InspectorName("建筑图标")] private Sprite icon;

        [TitleGroup("概览面板")]
        [LabelText("在概览面板显示")]
        [PropertyTooltip("关闭后，该建筑家族的实例不进入建筑概览列表；不影响经济预测、玩法结算、任务统计或存档。")]
        [SerializeField, InspectorName("在概览面板显示")] private bool showInOverview = true;

        [TitleGroup("表现与占地")]
        [LabelText("占地尺寸")]
        [MinValue(1)]
        [SerializeField, InspectorName("占地尺寸")] private Vector2Int size = Vector2Int.one;

        [TitleGroup("建造位置")]
        [LabelText("忽略地形要求")]
        [SerializeField, InspectorName("忽略地形要求")] private bool ignoreTerrainRequirement;

        [TitleGroup("建造位置")]
        [LabelText("需要的地形 Key")]
        [HideIf(nameof(ignoreTerrainRequirement))]
        [PropertyTooltip("建筑 footprint 内每个格子都必须包含这些 key。默认 land；水上建筑填 water；特殊区域建筑填对应区域 key。")]
        [SerializeField, InspectorName("需要的地形 Key")] private string[] requiredTerrainKeys = { GridTerrainKeys.Land };

        [TitleGroup("建造位置")]
        [LabelText("占地内至少一格需要的 Key")]
        [PropertyTooltip("每个 Key 都必须至少出现在 footprint 的一个格子中。用于石矿、遗迹等覆盖层；普通陆地要求仍写在上方。")]
        [SerializeField, InspectorName("占地内至少一格需要的 Key")]
        private string[] requiredAnyFootprintTerrainKeys = Array.Empty<string>();

        [TitleGroup("寻路")]
        [LabelText("移动阻力")]
        [PropertyTooltip("该建筑占用格的通行行动力消耗。小于等于 0 表示不可通行；道路等可通行建筑填正数。")]
        [SerializeField, InspectorName("移动阻力")] private int movementResistance;

        [TitleGroup("成本")]
        [LabelText("放置成本")]
        [PropertyTooltip("玩家确认放置时立即扣除。逐回合施工成本写在家族 Construction，升级成本写在对应 Level。")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField, InspectorName("放置成本")] private BuildingCost[] placementCosts = new BuildingCost[0];

        [TitleGroup("建造菜单")]
        [LabelText("显示条件")]
        [PropertyTooltip("None/空引用表示无显示条件，默认显示。配置条件且不满足时，该建筑从建造菜单隐藏。")]
        [SerializeReference, InspectorName("显示条件")] private GameCondition visibleCondition;

        [TitleGroup("建筑蓝图")]
        [LabelText("自动解锁条件")]
        [PropertyTooltip("条件满足后由建筑蓝图服务自动授予蓝图。科技解锁关系配置在这里，不再写进科技的一次性完成效果。")]
        [SerializeReference, InspectorName("自动解锁条件")]
        private GameCondition automaticBlueprintUnlockCondition;

        [TitleGroup("建筑蓝图")]
        [LabelText("初始未拥有蓝图")]
        [PropertyTooltip("启用后，新游戏不会自动授予该建筑蓝图，必须由科技、任务、远征、天赋或传承等奖励调用 BuildingBlueprintService.Unlock。")]
        [SerializeField, InspectorName("初始未拥有蓝图")] private bool blueprintInitiallyLocked;

        [TitleGroup("建筑蓝图")]
        [LabelText("未解锁时隐藏")]
        [PropertyTooltip("启用后，未获得蓝图时不显示在建造菜单；关闭时显示为“未获得蓝图”。")]
        [SerializeField, InspectorName("未解锁时隐藏")] private bool hideWhenBlueprintLocked;

        [TitleGroup("建造菜单")]
        [LabelText("菜单排序")]
        [PropertyTooltip("值越小越靠前。值相同时按家族 ID、显示名称做固定排序。")]
        [SerializeField, InspectorName("菜单排序")] private int buildMenuSortOrder;

        [TitleGroup("数量限制")]
        [LabelText("最大建造数量")]
        [MinValue(0)]
        [PropertyTooltip("0 表示无限制。")]
        [SerializeField, InspectorName("最大建造数量")] private int maxBuildCount;

        [TitleGroup("数量限制")]
        [LabelText("数量限制分组ID")]
        [PropertyTooltip("留空时使用家族 ID。同一分组共享数量上限。")]
        [SerializeField, InspectorName("数量限制分组 ID")] private string buildLimitGroupId;

        [TitleGroup("数量限制")]
        [LabelText("开发完成")]
        [SerializeField, InspectorName("开发完成")] private bool isDevelopmentCompleted;

        [NonSerialized] private string[] cachedRequiredTerrainKeys;
        [NonSerialized] private string[] cachedRequiredAnyFootprintTerrainKeys;

        public bool IsDevelopmentCompleted => isDevelopmentCompleted;
        public string FamilyId => familyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? (string.IsNullOrWhiteSpace(familyId) ? "未命名建筑" : familyId)
            : displayName;
        public Sprite Icon => icon;
        public bool ShowInOverview => showInOverview;
        public Vector2Int Size => size;
        public IReadOnlyList<BuildingCost> PlacementCosts => placementCosts ?? Array.Empty<BuildingCost>();
        public int MovementResistance => movementResistance;
        public IReadOnlyList<string> RequiredTerrainKeys
        {
            get
            {
                if (ignoreTerrainRequirement)
                {
                    return Array.Empty<string>();
                }

                cachedRequiredTerrainKeys ??= BuildRuntimeTerrainKeys(requiredTerrainKeys);
                return cachedRequiredTerrainKeys.Length == 0
                    ? DefaultRequiredTerrainKeys
                    : cachedRequiredTerrainKeys;
            }
        }

        public IReadOnlyList<string> RequiredAnyFootprintTerrainKeys
        {
            get
            {
                if (ignoreTerrainRequirement)
                {
                    return Array.Empty<string>();
                }

                cachedRequiredAnyFootprintTerrainKeys ??=
                    BuildRuntimeTerrainKeys(requiredAnyFootprintTerrainKeys);
                return cachedRequiredAnyFootprintTerrainKeys;
            }
        }

        public BuildingCategory Category => category;
        public GameCondition VisibleCondition => visibleCondition;
        public GameCondition AutomaticBlueprintUnlockCondition => automaticBlueprintUnlockCondition;
        public bool BlueprintInitiallyLocked => blueprintInitiallyLocked;
        public bool HideWhenBlueprintLocked => hideWhenBlueprintLocked;
        public int BuildMenuSortOrder => buildMenuSortOrder;
        public int MaxBuildCount => maxBuildCount;
        public string BuildLimitGroupId => string.IsNullOrWhiteSpace(buildLimitGroupId) ? familyId : buildLimitGroupId;
        public bool HasIcon => icon != null;
        public bool HasBuildCountLimit => maxBuildCount > 0;
        public bool IsValid => !string.IsNullOrWhiteSpace(familyId);
        public GridFootprint CreateFootprint(GridPosition origin)
        {
            return new GridFootprint(origin, size);
        }

        public void Normalize()
        {
            familyId = string.IsNullOrWhiteSpace(familyId) ? string.Empty : familyId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            buildLimitGroupId = string.IsNullOrWhiteSpace(buildLimitGroupId) ? string.Empty : buildLimitGroupId.Trim();
            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            maxBuildCount = Mathf.Max(0, maxBuildCount);
            NormalizeCosts(ref placementCosts);
            NormalizeTerrainKeysForInspector(ref requiredTerrainKeys);
            NormalizeTerrainKeysForInspector(ref requiredAnyFootprintTerrainKeys);
            cachedRequiredTerrainKeys = null;
            cachedRequiredAnyFootprintTerrainKeys = null;
        }

        public void ConfigureIdentity(
            string stableFamilyId,
            string localizedDisplayName,
            string limitGroupId = "")
        {
            familyId = string.IsNullOrWhiteSpace(stableFamilyId)
                ? string.Empty
                : stableFamilyId.Trim();
            displayName = string.IsNullOrWhiteSpace(localizedDisplayName)
                ? familyId
                : localizedDisplayName.Trim();
            buildLimitGroupId = string.IsNullOrWhiteSpace(limitGroupId)
                ? familyId
                : limitGroupId.Trim();
            Normalize();
        }

        /// <summary>
        /// 由正式建筑数值表更新策划拥有的公共字段。
        /// FamilyId、图标与显示条件不在此入口中修改，避免导表破坏稳定身份和表现引用。
        /// </summary>
        public void ConfigureNumericData(
            string localizedDisplayName,
            BuildingCategory buildingCategory,
            Vector2Int footprintSize,
            bool ignoreTerrain,
            IReadOnlyList<string> terrainKeys,
            IReadOnlyList<string> anyFootprintTerrainKeys,
            int traversalResistance,
            IReadOnlyList<BuildingCost> costs,
            GameCondition blueprintUnlockCondition,
            bool initiallyLocked,
            bool hideWhileLocked,
            int menuSortOrder,
            int maximumBuildCount,
            string limitGroupId,
            bool developmentCompleted)
        {
            displayName = string.IsNullOrWhiteSpace(localizedDisplayName)
                ? familyId
                : localizedDisplayName.Trim();
            category = buildingCategory;
            size = footprintSize;
            ignoreTerrainRequirement = ignoreTerrain;
            requiredTerrainKeys = terrainKeys == null
                ? Array.Empty<string>()
                : new List<string>(terrainKeys).ToArray();
            requiredAnyFootprintTerrainKeys = anyFootprintTerrainKeys == null
                ? Array.Empty<string>()
                : new List<string>(anyFootprintTerrainKeys).ToArray();
            movementResistance = traversalResistance;
            placementCosts = costs == null
                ? Array.Empty<BuildingCost>()
                : new List<BuildingCost>(costs).ToArray();
            automaticBlueprintUnlockCondition = blueprintUnlockCondition;
            blueprintInitiallyLocked = initiallyLocked;
            hideWhenBlueprintLocked = hideWhileLocked;
            buildMenuSortOrder = menuSortOrder;
            maxBuildCount = maximumBuildCount;
            buildLimitGroupId = string.IsNullOrWhiteSpace(limitGroupId)
                ? familyId
                : limitGroupId.Trim();
            isDevelopmentCompleted = developmentCompleted;
            Normalize();
        }

        private bool HasValidFamilyId()
        {
            return !string.IsNullOrWhiteSpace(familyId);
        }

        private static void NormalizeCosts(ref BuildingCost[] costs)
        {
            if (costs == null)
            {
                costs = new BuildingCost[0];
                return;
            }

            for (var i = 0; i < costs.Length; i++)
            {
                costs[i] = costs[i].Normalized();
            }
        }

        private static void NormalizeTerrainKeysForInspector(ref string[] terrainKeys)
        {
            if (terrainKeys == null)
            {
                terrainKeys = Array.Empty<string>();
                return;
            }

            var normalizedKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < terrainKeys.Length; i++)
            {
                var normalizedKey = GridTerrainKeys.Normalize(terrainKeys[i]);
                if (string.IsNullOrEmpty(normalizedKey))
                {
                    terrainKeys[i] = string.Empty;
                    continue;
                }

                if (!normalizedKeys.Add(normalizedKey))
                {
                    terrainKeys[i] = string.Empty;
                    continue;
                }

                terrainKeys[i] = normalizedKey;
            }
        }

        private static string[] BuildRuntimeTerrainKeys(IReadOnlyList<string> terrainKeys)
        {
            if (terrainKeys == null || terrainKeys.Count == 0)
            {
                return Array.Empty<string>();
            }

            var normalizedKeys = new List<string>(terrainKeys.Count);
            for (var i = 0; i < terrainKeys.Count; i++)
            {
                var normalizedKey = GridTerrainKeys.Normalize(terrainKeys[i]);
                if (string.IsNullOrEmpty(normalizedKey) || normalizedKeys.Contains(normalizedKey))
                {
                    continue;
                }

                normalizedKeys.Add(normalizedKey);
            }

            return normalizedKeys.Count == 0 ? Array.Empty<string>() : normalizedKeys.ToArray();
        }

        
    }
}
