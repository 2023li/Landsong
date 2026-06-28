using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_Building : MonoBehaviour
    {
        [ShowInInspector, ReadOnly, LabelText("建筑目录")] private BuildingCatalog buildingCatalog;
        [ShowInInspector, ReadOnly, LabelText("游戏系统")] private Landsong.GameSystem gameSystem;
        [ShowInInspector, ReadOnly, LabelText("库存服务")] private InventoryService inventory;
        [SerializeField, LabelText("分类按钮根节点")] private Transform categoryButtonRoot;
        [SerializeField, LabelText("分类按钮预制体")] private GamePanel_BuildingCategoryButton categoryButtonPrefab;
        [SerializeField, LabelText("建筑项根节点")] private Transform buildingItemRoot;
        [SerializeField, LabelText("建筑项预制体")] private GamePanel_BuildingItem buildingItemPrefab;
        [SerializeField, LabelText("建筑项对象池根节点")] private Transform buildingItemPoolRoot;
        [SerializeField, LabelText("信息面板根节点")] private GameObject infoPanelRoot;
        [SerializeField, LabelText("信息图标")] private Image infoIcon;
        [SerializeField, LabelText("信息名称文本")] private TMP_Text infoNameLabel;
        [SerializeField, LabelText("信息状态文本")] private TMP_Text infoStatusLabel;
        [SerializeField, LabelText("信息材料文本")] private TMP_Text infoCostLabel;
        [SerializeField, LabelText("信息数量限制文本")] private TMP_Text infoCountLimitLabel;
        [SerializeField, LabelText("显示空分类")] private bool includeEmptyCategories;
        [SerializeField, LabelText("刷新后选择第一个建筑")] private bool selectFirstBuildingOnRefresh = true;
        [SerializeField, LabelText("刷新时查找现有建筑")] private bool discoverExistingBuildingsOnRefresh = true;
        [SerializeField, LabelText("库存变化时刷新")] private bool refreshWhenInventoryChanges = true;
        [SerializeField, LabelText("自动查找建造放置控制器")] private bool discoverPlacementControllerOnEnable = true;
        [SerializeField, LabelText("建造放置控制器")] private BuildingPlacementController placementController;
        [SerializeField, LabelText("建筑选择事件")] private BuildingDefinitionEvent buildingSelected = new BuildingDefinitionEvent();
        [SerializeField, LabelText("现有建筑列表")] private List<BuildingBase> existingBuildings = new List<BuildingBase>();

        private readonly List<GamePanel_BuildingCategoryButton> categoryButtons = new List<GamePanel_BuildingCategoryButton>();
        private readonly List<GamePanel_BuildingItem> buildingItems = new List<GamePanel_BuildingItem>();
        private readonly List<GamePanel_BuildingItem> buildingItemPool = new List<GamePanel_BuildingItem>();
        private BuildingCategory selectedCategory = BuildingCategory.None;
        private BuildingDefinition selectedBuildingDefinition;
        private bool subscribedToInventory;

        public BuildingDefinitionEvent BuildingSelected => buildingSelected;
        public BuildingCategory SelectedCategory => selectedCategory;

        private void OnEnable()
        {
            ResolveGameSystem();
            ResolvePlacementController();
            SubscribeInventory();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeInventory();
        }

        public void RefreshFromGameSystem()
        {
            UnsubscribeInventory();
            ResolveGameSystem();

            if (isActiveAndEnabled)
            {
                SubscribeInventory();
            }

            Refresh();
        }

        [ContextMenu("Refresh Building Panel")]
        public void Refresh()
        {
            if (discoverExistingBuildingsOnRefresh)
            {
                DiscoverExistingBuildings();
            }

            var categories = CollectCategories();
            if (selectedCategory == BuildingCategory.None || !categories.Contains(selectedCategory))
            {
                selectedCategory = categories.Count > 0 ? categories[0] : BuildingCategory.None;
            }

            RebuildCategoryButtons(categories);
            RebuildBuildingItems();
            RefreshInfoPanel();
        }

        public void SelectCategory(BuildingCategory category)
        {
            selectedCategory = category;
            RebuildBuildingItems();
            RefreshInfoPanel();
        }

        private void ResolveGameSystem()
        {
            gameSystem = Landsong.GameSystem.Instance;
            buildingCatalog = gameSystem == null ? null : gameSystem.BuildingCatalog;
            inventory = gameSystem == null ? null : gameSystem.Inventory;
        }

        private void ResolvePlacementController()
        {
            if (!discoverPlacementControllerOnEnable || placementController != null)
            {
                return;
            }

            placementController = FindFirstObjectByType<BuildingPlacementController>(FindObjectsInactive.Include);
        }

        private List<BuildingCategory> CollectCategories()
        {
            var categories = new List<BuildingCategory>();
            if (includeEmptyCategories)
            {
                foreach (BuildingCategory category in Enum.GetValues(typeof(BuildingCategory)))
                {
                    if (IsSingleCategory(category))
                    {
                        categories.Add(category);
                    }
                }

                return categories;
            }

            if (buildingCatalog == null)
            {
                return categories;
            }

            foreach (var definition in buildingCatalog.Definitions)
            {
                if (definition == null || definition.Category == BuildingCategory.None)
                {
                    continue;
                }

                var availability = Evaluate(definition);
                if (!availability.IsVisible)
                {
                    continue;
                }

                AddDefinitionCategories(categories, definition.Category);
            }

            return categories;
        }

        private static void AddDefinitionCategories(List<BuildingCategory> categories, BuildingCategory categoryFlags)
        {
            foreach (BuildingCategory category in Enum.GetValues(typeof(BuildingCategory)))
            {
                if (!IsSingleCategory(category) || !ContainsCategory(categoryFlags, category) || categories.Contains(category))
                {
                    continue;
                }

                categories.Add(category);
            }
        }

        private void RebuildCategoryButtons(IReadOnlyList<BuildingCategory> categories)
        {
            ClearCategoryButtons();

            if (categoryButtonRoot == null || categoryButtonPrefab == null)
            {
                return;
            }

            foreach (var category in categories)
            {
                var button = Instantiate(categoryButtonPrefab, categoryButtonRoot);
                button.Bind(category, SelectCategory);
                categoryButtons.Add(button);
            }
        }

        private void RebuildBuildingItems()
        {
            ReleaseActiveBuildingItems();

            if (buildingCatalog == null || buildingItemRoot == null || buildingItemPrefab == null)
            {
                SetSelectedBuilding(null);
                return;
            }

            BuildingDefinition firstVisibleDefinition = null;
            var selectedStillVisible = false;

            foreach (var definition in buildingCatalog.Definitions)
            {
                if (definition == null || !ContainsCategory(definition.Category, selectedCategory))
                {
                    continue;
                }

                var availability = Evaluate(definition);
                if (!availability.IsVisible)
                {
                    continue;
                }

                var item = GetBuildingItemFromPool();
                item.Bind(definition, HandleBuildingItemClicked);
                buildingItems.Add(item);

                firstVisibleDefinition ??= definition;
                if (selectedBuildingDefinition == definition)
                {
                    selectedStillVisible = true;
                }
            }

            if (!selectedStillVisible && selectFirstBuildingOnRefresh)
            {
                SetSelectedBuilding(firstVisibleDefinition);
            }
            else if (!selectedStillVisible)
            {
                SetSelectedBuilding(null);
            }
        }

        private GamePanel_BuildingItem GetBuildingItemFromPool()
        {
            GamePanel_BuildingItem item;
            var lastIndex = buildingItemPool.Count - 1;
            if (lastIndex >= 0)
            {
                item = buildingItemPool[lastIndex];
                buildingItemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(buildingItemPrefab);
            }

            var itemTransform = item.transform;
            itemTransform.SetParent(buildingItemRoot, false);
            itemTransform.SetAsLastSibling();
            item.gameObject.SetActive(true);
            return item;
        }

        private void ReleaseActiveBuildingItems()
        {
            foreach (var item in buildingItems)
            {
                ReleaseBuildingItem(item);
            }

            buildingItems.Clear();
        }

        private void ReleaseBuildingItem(GamePanel_BuildingItem item)
        {
            if (item == null)
            {
                return;
            }

            item.Unbind();
            item.gameObject.SetActive(false);

            var poolRoot = buildingItemPoolRoot == null ? buildingItemRoot : buildingItemPoolRoot;
            if (poolRoot != null)
            {
                item.transform.SetParent(poolRoot, false);
            }

            buildingItemPool.Add(item);
        }

        private BuildingAvailability Evaluate(BuildingDefinition definition)
        {
            return BuildingAvailabilityEvaluator.Evaluate(definition, gameSystem, existingBuildings);
        }

        private static bool ContainsCategory(BuildingCategory categoryFlags, BuildingCategory category)
        {
            return category != BuildingCategory.None && (categoryFlags & category) == category;
        }

        private static bool IsSingleCategory(BuildingCategory category)
        {
            var value = (int)category;
            return value != 0 && (value & (value - 1)) == 0;
        }

        private void HandleBuildingItemClicked(BuildingDefinition definition)
        {
            SetSelectedBuilding(definition);

            var availability = Evaluate(definition);
            if (!availability.CanBuild)
            {
                return;
            }

            if (placementController != null)
            {
                placementController.BeginPlacement(definition);
            }

            buildingSelected.Invoke(definition);
        }

        private void SetSelectedBuilding(BuildingDefinition definition)
        {
            selectedBuildingDefinition = definition;
            RefreshInfoPanel();
        }

        private void RefreshInfoPanel()
        {
            if (infoPanelRoot != null)
            {
                infoPanelRoot.SetActive(selectedBuildingDefinition != null);
            }

            if (selectedBuildingDefinition == null)
            {
                SetInfoIcon(null);
                SetInfoText(infoNameLabel, string.Empty);
                SetInfoText(infoStatusLabel, string.Empty);
                SetInfoText(infoCostLabel, string.Empty);
                SetInfoText(infoCountLimitLabel, string.Empty);
                return;
            }

            var availability = Evaluate(selectedBuildingDefinition);
            SetInfoIcon(selectedBuildingDefinition.Icon);
            SetInfoText(infoNameLabel, selectedBuildingDefinition.DisplayName);
            SetInfoText(infoStatusLabel, GetStatusText(availability));
            SetInfoText(infoCostLabel, FormatCosts(selectedBuildingDefinition.PlacementCosts));
            SetInfoText(infoCountLimitLabel, selectedBuildingDefinition.HasBuildCountLimit
                ? $"{availability.BuiltCount}/{selectedBuildingDefinition.MaxBuildCount}"
                : "无限制");
        }

        private void SetInfoIcon(Sprite sprite)
        {
            if (infoIcon == null)
            {
                return;
            }

            infoIcon.sprite = sprite;
            infoIcon.enabled = sprite != null;
        }

        private static void SetInfoText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        private static string GetStatusText(BuildingAvailability availability)
        {
            if (availability.CanBuild)
            {
                return "可建造";
            }

            if (availability.IsAvailable)
            {
                return "可用，材料不足";
            }

            return availability.FirstUnavailableReason switch
            {
                BuildingUnavailableReason.Locked => "未解锁",
                BuildingUnavailableReason.BuildLimitReached => "数量已达上限",
                BuildingUnavailableReason.MissingMaterials => "材料不足",
                _ => "不可用"
            };
        }

        private static string FormatCosts(IReadOnlyList<BuildingCost> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return "无需材料";
            }

            var parts = new List<string>();
            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (!cost.IsValid)
                {
                    continue;
                }

                parts.Add($"{cost.ItemDefinition.DisplayName} x{cost.Amount}");
            }

            return parts.Count == 0 ? "无需材料" : string.Join("  ", parts);
        }

        private void DiscoverExistingBuildings()
        {
            existingBuildings.Clear();

            var discovered = FindObjectsByType<BuildingBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            foreach (var building in discovered)
            {
                if (building != null)
                {
                    existingBuildings.Add(building);
                }
            }
        }

        private void ClearCategoryButtons()
        {
            foreach (var button in categoryButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            categoryButtons.Clear();
        }

        private void SubscribeInventory()
        {
            if (subscribedToInventory || inventory == null || !refreshWhenInventoryChanges)
            {
                return;
            }

            inventory.InventoryChanged += HandleInventoryChanged;
            subscribedToInventory = true;
        }

        private void UnsubscribeInventory()
        {
            if (!subscribedToInventory || inventory == null)
            {
                subscribedToInventory = false;
                return;
            }

            inventory.InventoryChanged -= HandleInventoryChanged;
            subscribedToInventory = false;
        }

        private void HandleInventoryChanged(InventoryService changedInventory)
        {
            Refresh();
        }
    }

    [Serializable]
    public sealed class BuildingDefinitionEvent : UnityEvent<BuildingDefinition>
    {
    }
}
