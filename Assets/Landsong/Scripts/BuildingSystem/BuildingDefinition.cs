using System;
using System.Collections.Generic;
using Landsong.ConditionSystem;
using Landsong.GridSystem;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Landsong.BuildingSystem
{
    [CreateAssetMenu(menuName = "Landsong/Building/Building Definition", fileName = "BuildingDefinition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        private const string BuildingPrefabFolder = "Assets/Landsong/Objects/Prefabs/建筑";
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
        [HorizontalGroup("表现与占地/Prefabs")]
        [LabelText("建筑预制体")]
        [AssetsOnly]
        [SerializeField] private BuildingBase buildingPrefab;

#if UNITY_EDITOR
        [TitleGroup("表现与占地")]
        [Button("通过建筑ID自动引用建筑预制体")]
        private void AutoAssignBuildingPrefabById()
        {
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                Debug.LogWarning($"BuildingDefinition '{name}' cannot auto assign prefab because buildingId is empty.", this);
                return;
            }

            var prefabPath = $"{BuildingPrefabFolder}/{buildingId.Trim()}.prefab";
            var prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabObject == null)
            {
                Debug.LogWarning($"Building prefab not found at path: {prefabPath}", this);
                return;
            }

            var prefabBuilding = prefabObject.GetComponent<BuildingBase>();
            if (prefabBuilding == null)
            {
                Debug.LogWarning($"Prefab '{prefabPath}' has no BuildingBase component on root.", prefabObject);
                return;
            }

            Undo.RecordObject(this, "Auto Assign Building Prefab");
            buildingPrefab = prefabBuilding;
            EditorUtility.SetDirty(this);
        }
#endif

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

        [TitleGroup("数量限制")]
        [LabelText("最大建造数量")]
        [MinValue(0)]
        [PropertyTooltip("0 表示无限制。")]
        [SerializeField] private int maxBuildCount;

        [TitleGroup("数量限制")]
        [LabelText("数量限制分组ID")]
        [PropertyTooltip("留空时使用建筑ID。同一分组共享数量上限。")]
        [SerializeField] private string buildLimitGroupId;

        public string BuildingId => buildingId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public GameObject BuildingPrefab => buildingPrefab == null ? null : buildingPrefab.gameObject;
        public Vector2Int Size => size;
        public IReadOnlyList<BuildingCost> PlacementCosts => placementCosts ?? Array.Empty<BuildingCost>();
        public IReadOnlyList<string> RequiredTerrainKeys
        {
            get
            {
                if (ignoreTerrainRequirement)
                {
                    return Array.Empty<string>();
                }

                return requiredTerrainKeys == null || requiredTerrainKeys.Length == 0
                    ? DefaultRequiredTerrainKeys
                    : requiredTerrainKeys;
            }
        }

        public BuildingCategory Category => category;
        public GameCondition VisibleCondition => visibleCondition;
        public GameCondition AvailableCondition => availableCondition;
        public int MaxBuildCount => maxBuildCount;
        public string BuildLimitGroupId => string.IsNullOrWhiteSpace(buildLimitGroupId) ? buildingId : buildLimitGroupId;
        public bool HasIcon => icon != null;
        public bool HasBuildingPrefab => buildingPrefab != null;
        public bool HasBuildCountLimit => maxBuildCount > 0;

        public GridFootprint CreateFootprint(GridPosition origin)
        {
            return new GridFootprint(origin, size);
        }

        private void OnValidate()
        {
            buildingId = string.IsNullOrWhiteSpace(buildingId) ? string.Empty : buildingId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            buildLimitGroupId = string.IsNullOrWhiteSpace(buildLimitGroupId) ? string.Empty : buildLimitGroupId.Trim();
            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            maxBuildCount = Mathf.Max(0, maxBuildCount);
            NormalizeCosts(ref placementCosts);
            NormalizeTerrainKeys(ref requiredTerrainKeys);
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

        private static void NormalizeTerrainKeys(ref string[] terrainKeys)
        {
            if (terrainKeys == null)
            {
                terrainKeys = Array.Empty<string>();
                return;
            }

            var normalizedKeys = new List<string>(terrainKeys.Length);
            for (var i = 0; i < terrainKeys.Length; i++)
            {
                var normalizedKey = GridTerrainKeys.Normalize(terrainKeys[i]);
                if (string.IsNullOrEmpty(normalizedKey) || normalizedKeys.Contains(normalizedKey))
                {
                    continue;
                }

                normalizedKeys.Add(normalizedKey);
            }

            terrainKeys = normalizedKeys.ToArray();
        }

    }
}
