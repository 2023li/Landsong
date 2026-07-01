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

        private UIPanel_Game gamePanel;
        [ShowInInspector, ReadOnly, LabelText("建筑目录")] private BuildingCatalog buildingCatalog;
        [ShowInInspector, ReadOnly, LabelText("游戏系统")] private Landsong.GameSystem gameSystem;
        [ShowInInspector, ReadOnly, LabelText("库存服务")] private InventoryService inventory;
        [ShowInInspector, ReadOnly, LabelText("建筑服务")] private BuildingService buildings;

        [SerializeField, LabelText("关闭按钮")] private Button closeButton;
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
        [SerializeField, LabelText("分类显示顺序")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true)]
        private BuildingCategory[] categoryDisplayOrder =
        {
            BuildingCategory.Housing,
            BuildingCategory.Production,
            BuildingCategory.Storage,
            BuildingCategory.后勤,
            BuildingCategory.通用,
            BuildingCategory.美化,
            BuildingCategory.奇迹,
            BuildingCategory.神迹
        };

        [SerializeField, LabelText("刷新后选择第一个建筑")] private bool selectFirstBuildingOnRefresh = true;
        [SerializeField, LabelText("库存变化时刷新")] private bool refreshWhenInventoryChanges = true;
        [SerializeField, LabelText("自动查找建造放置控制器")] private bool discoverPlacementControllerOnEnable = true;
        [SerializeField, LabelText("建造放置控制器")] private BuildingPlacementController placementController;
        [SerializeField, LabelText("建筑选择事件")] private BuildingDefinitionEvent buildingSelected = new BuildingDefinitionEvent();

        private readonly List<GamePanel_BuildingCategoryButton> categoryButtons = new List<GamePanel_BuildingCategoryButton>();
        private readonly List<GamePanel_BuildingItem> buildingItems = new List<GamePanel_BuildingItem>();
        private readonly List<GamePanel_BuildingItem> buildingItemPool = new List<GamePanel_BuildingItem>();
        private BuildingCategory selectedCategory = BuildingCategory.None;
        private BuildingDefinition selectedBuildingDefinition;
        private bool subscribedToInventory;
        private bool subscribedToBuildings;

        public BuildingDefinitionEvent BuildingSelected => buildingSelected;
        public BuildingCategory SelectedCategory => selectedCategory;

        private void Awake()
        {
            gamePanel = GetComponentInParent<UIPanel_Game>();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseButtonClicked);
            }

        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseButtonClicked);
            }
        }

        private void OnEnable()
        {
            ResolveGameSystem();
            ResolvePlacementController();
            SubscribeRuntimeChanges();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeRuntimeChanges();
        }


        public void Show()
        {
            gameObject.SetActive(true);
        }
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void HandleCloseButtonClicked()
        {
            if (gamePanel == null)
            {
                gamePanel = GetComponentInParent<UIPanel_Game>();
            }

            if (gamePanel != null)
            {
                gamePanel.Hide_Building();
            }
        }

        public void RefreshFromGameSystem()
        {
            UnsubscribeRuntimeChanges();
            ResolveGameSystem();

            if (isActiveAndEnabled)
            {
                SubscribeRuntimeChanges();
            }

            Refresh();
        }

        [ContextMenu("Refresh Building Panel")]
        public void Refresh()
        {
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
            buildings = gameSystem == null ? null : gameSystem.Buildings;
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
                AddAllSingleCategories(categories);
                return SortCategoriesForDisplay(categories);
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

            return SortCategoriesForDisplay(categories);
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

        private static void AddAllSingleCategories(List<BuildingCategory> categories)
        {
            foreach (BuildingCategory category in Enum.GetValues(typeof(BuildingCategory)))
            {
                if (IsSingleCategory(category) && !categories.Contains(category))
                {
                    categories.Add(category);
                }
            }
        }

        private List<BuildingCategory> SortCategoriesForDisplay(List<BuildingCategory> categories)
        {
            if (categories.Count <= 1)
            {
                return categories;
            }

            var orderedCategories = new List<BuildingCategory>(categories.Count);

            if (categoryDisplayOrder != null)
            {
                for (var i = 0; i < categoryDisplayOrder.Length; i++)
                {
                    AddCategoryIfPresent(orderedCategories, categories, categoryDisplayOrder[i]);
                }
            }

            foreach (BuildingCategory category in Enum.GetValues(typeof(BuildingCategory)))
            {
                AddCategoryIfPresent(orderedCategories, categories, category);
            }

            for (var i = 0; i < categories.Count; i++)
            {
                AddCategoryIfPresent(orderedCategories, categories, categories[i]);
            }

            return orderedCategories;
        }

        private static void AddCategoryIfPresent(
            List<BuildingCategory> target,
            IReadOnlyList<BuildingCategory> source,
            BuildingCategory category)
        {
            if (!IsSingleCategory(category) || !ContainsCategoryValue(source, category) || target.Contains(category))
            {
                return;
            }

            target.Add(category);
        }

        private static bool ContainsCategoryValue(IReadOnlyList<BuildingCategory> categories, BuildingCategory category)
        {
            for (var i = 0; i < categories.Count; i++)
            {
                if (categories[i] == category)
                {
                    return true;
                }
            }

            return false;
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
            var visibleEntries = new List<BuildingDisplayEntry>();
            var definitions = buildingCatalog.Definitions;

            for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                var definition = definitions[definitionIndex];
                if (definition == null || !ContainsCategory(definition.Category, selectedCategory))
                {
                    continue;
                }

                var availability = Evaluate(definition);
                if (!availability.IsVisible)
                {
                    continue;
                }

                visibleEntries.Add(new BuildingDisplayEntry(definition, definitionIndex));
            }

            visibleEntries.Sort(CompareBuildingDisplayEntries);

            for (var i = 0; i < visibleEntries.Count; i++)
            {
                var definition = visibleEntries[i].Definition;
                var availability = Evaluate(definition);
                var item = GetBuildingItemFromPool();
                item.Bind(definition, availability, HandleBuildingItemClicked);
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

        private static int CompareBuildingDisplayEntries(BuildingDisplayEntry left, BuildingDisplayEntry right)
        {
            var result = CompareBuildingDefinitionsForDisplay(left.Definition, right.Definition);
            return result != 0 ? result : left.CatalogIndex.CompareTo(right.CatalogIndex);
        }

        private static int CompareBuildingDefinitionsForDisplay(BuildingDefinition left, BuildingDefinition right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var result = left.BuildMenuSortOrder.CompareTo(right.BuildMenuSortOrder);
            if (result != 0)
            {
                return result;
            }

            result = CompareStableText(left.BuildingId, right.BuildingId);
            if (result != 0)
            {
                return result;
            }

            result = CompareStableText(left.DisplayName, right.DisplayName);
            if (result != 0)
            {
                return result;
            }

            return CompareStableText(left.name, right.name);
        }

        private static int CompareStableText(string left, string right)
        {
            left = string.IsNullOrWhiteSpace(left) ? string.Empty : left.Trim();
            right = string.IsNullOrWhiteSpace(right) ? string.Empty : right.Trim();
            return string.Compare(left, right, StringComparison.Ordinal);
        }

        private readonly struct BuildingDisplayEntry
        {
            public BuildingDisplayEntry(BuildingDefinition definition, int catalogIndex)
            {
                Definition = definition;
                CatalogIndex = catalogIndex;
            }

            public BuildingDefinition Definition { get; }
            public int CatalogIndex { get; }
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
            return buildings == null
                ? BuildingAvailability.Hidden(definition, BuildingUnavailableReason.Hidden)
                : buildings.EvaluateAvailability(definition);
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
                BuildingUnavailableReason.DevelopmentIncomplete => "建筑未开发完成",
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

        private void SubscribeBuildings()
        {
            if (subscribedToBuildings || buildings == null)
            {
                return;
            }

            buildings.BuildingsChanged += HandleBuildingsChanged;
            subscribedToBuildings = true;
        }

        private void UnsubscribeBuildings()
        {
            if (!subscribedToBuildings || buildings == null)
            {
                subscribedToBuildings = false;
                return;
            }

            buildings.BuildingsChanged -= HandleBuildingsChanged;
            subscribedToBuildings = false;
        }

        private void SubscribeRuntimeChanges()
        {
            SubscribeInventory();
            SubscribeBuildings();
        }

        private void UnsubscribeRuntimeChanges()
        {
            UnsubscribeInventory();
            UnsubscribeBuildings();
        }

        private void HandleInventoryChanged(InventoryService changedInventory)
        {
            Refresh();
        }

        private void HandleBuildingsChanged(BuildingService changedBuildings)
        {
            Refresh();
        }
    }

    [Serializable]
    public sealed class BuildingDefinitionEvent : UnityEvent<BuildingDefinition>
    {
    }
}
