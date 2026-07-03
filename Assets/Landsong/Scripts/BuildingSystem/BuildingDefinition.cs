using System;
using System.Collections.Generic;
using System.Xml;
using Landsong.ConditionSystem;
using Landsong.GridSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Landsong.BuildingSystem
{
    [Serializable]
    public sealed class BuildingDefinition
    {
        private static readonly string[] DefaultRequiredTerrainKeys = { GridTerrainKeys.Land };

        [TitleGroup("基础信息")]
        [HorizontalGroup("基础信息/Split", Width = 0.72f)]
        [VerticalGroup("基础信息/Split/Left")]
        [LabelText("建筑ID")]
        [ValidateInput(nameof(HasValidBuildingId), "建筑ID不能为空。")]
        [SerializeField] private string buildingId;

        [VerticalGroup("基础信息/Split/Left")]
        [LabelText("显示名称")]
        [SerializeField] private string displayName;

        [VerticalGroup("基础信息/Split/Left")]
        [LabelText("分类")]
        [EnumToggleButtons]
        [SerializeField] private BuildingCategory category = BuildingCategory.None;

        [HorizontalGroup("基础信息/Split", Width = 88)]
        [PreviewField(72)]
        [HideLabel]
        [SerializeField] private Sprite icon;

        [TitleGroup("表现与占地")]
        [LabelText("占地尺寸")]
        [MinValue(1)]
        [SerializeField] private Vector2Int size = Vector2Int.one;

        [TitleGroup("建造位置")]
        [LabelText("忽略地形要求")]
        [SerializeField] private bool ignoreTerrainRequirement;

        [TitleGroup("建造位置")]
        [LabelText("需要的地形 Key")]
        [HideIf(nameof(ignoreTerrainRequirement))]
        [PropertyTooltip("建筑 footprint 内每个格子都必须包含这些 key。默认 land；水上建筑填 water；特殊区域建筑填对应区域 key。")]
        [SerializeField] private string[] requiredTerrainKeys = { GridTerrainKeys.Land };

        [TitleGroup("寻路")]
        [LabelText("移动阻力")]
        [PropertyTooltip("该建筑占用格的通行行动力消耗。小于等于 0 表示不可通行；道路等可通行建筑填正数。")]
        [SerializeField] private int movementResistance;

        [TitleGroup("成本")]
        [LabelText("放置成本")]
        [PropertyTooltip("玩家确认放置时立即扣除。施工、运营、生产、升级等成本写在建筑 prefab 上的 BuildingBase 子类里。")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField] private BuildingCost[] placementCosts = Array.Empty<BuildingCost>();

        [TitleGroup("建造菜单")]
        [LabelText("显示条件")]
        [PropertyTooltip("为空时视为通过。需要显式默认通过时可配置 GameCondition_True。")]
        [SerializeReference] private GameCondition visibleCondition;

        [TitleGroup("建造菜单")]
        [LabelText("可用条件")]
        [PropertyTooltip("为空时视为通过。需要显式默认通过时可配置 GameCondition_True。")]
        [SerializeReference] private GameCondition availableCondition;

        [TitleGroup("建造菜单")]
        [LabelText("菜单排序")]
        [PropertyTooltip("值越小越靠前。值相同时按建筑ID、显示名称做固定排序。")]
        [SerializeField] private int buildMenuSortOrder;

        [TitleGroup("数量限制")]
        [LabelText("最大建造数量")]
        [MinValue(0)]
        [PropertyTooltip("0 表示无限制。")]
        [SerializeField] private int maxBuildCount;

        [TitleGroup("数量限制")]
        [LabelText("数量限制分组ID")]
        [PropertyTooltip("留空时使用建筑ID。同一分组共享数量上限。")]
        [SerializeField] private string buildLimitGroupId;

        [TitleGroup("数量限制")]
        [LabelText("开发完成")]
        [SerializeField] private bool isDevelopmentCompleted;

        [TitleGroup("附件")]
        [SerializeField]
        [LabelText("使用专用的详情面板")]
        [FormerlySerializedAs("UseUniqueDetailPanel")]
        private bool useUniqueDetailPanel = false;

        [LabelText("专属的详情面板")]
        [Tooltip("需要继承 Popup_BuildingDetails")]
        [ShowIf(nameof(useUniqueDetailPanel))]
        [SerializeField]
        private Popup_BuildingDetails uniqueDetailPanel;

        [NonSerialized] private string[] cachedRequiredTerrainKeys;

        public bool IsDevelopmentCompleted => isDevelopmentCompleted;
        public string BuildingId => buildingId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? (string.IsNullOrWhiteSpace(buildingId) ? "未命名建筑" : buildingId)
            : displayName;
        public Sprite Icon => icon;
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

        public BuildingCategory Category => category;
        public GameCondition VisibleCondition => visibleCondition;
        public GameCondition AvailableCondition => availableCondition;
        public int BuildMenuSortOrder => buildMenuSortOrder;
        public int MaxBuildCount => maxBuildCount;
        public string BuildLimitGroupId => string.IsNullOrWhiteSpace(buildLimitGroupId) ? buildingId : buildLimitGroupId;
        public bool HasIcon => icon != null;
        public bool HasBuildCountLimit => maxBuildCount > 0;
        public bool IsValid => !string.IsNullOrWhiteSpace(buildingId);
        public bool UseUniqueDetailPanel => useUniqueDetailPanel && uniqueDetailPanel != null;
        public Popup_BuildingDetails UniqueDetailPanel => UseUniqueDetailPanel ? uniqueDetailPanel : null;

        public GridFootprint CreateFootprint(GridPosition origin)
        {
            return new GridFootprint(origin, size);
        }

        public void Normalize()
        {
            buildingId = string.IsNullOrWhiteSpace(buildingId) ? string.Empty : buildingId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            buildLimitGroupId = string.IsNullOrWhiteSpace(buildLimitGroupId) ? string.Empty : buildLimitGroupId.Trim();
            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            maxBuildCount = Mathf.Max(0, maxBuildCount);
            NormalizeCosts(ref placementCosts);
            NormalizeTerrainKeysForInspector(ref requiredTerrainKeys);
            cachedRequiredTerrainKeys = null;
        }

        private bool HasValidBuildingId()
        {
            return !string.IsNullOrWhiteSpace(buildingId);
        }

        private static void NormalizeCosts(ref BuildingCost[] costs)
        {
            if (costs == null)
            {
                costs = Array.Empty<BuildingCost>();
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
