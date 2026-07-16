using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.InventorySystem;
using Landsong.TechnologySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_Building : MonoBehaviour
    {
        //我能保证引用正确合法 你只用关注业务逻辑
        [SerializeField] private Toggle btn_拆除模式;



        private UIPanel_Game gamePanel;
        private BuildingCatalog buildingCatalog;
        private Landsong.GameSystem gameSystem;
        private InventoryService inventory;
        private BuildingService buildings;
        private TechnologyService technology;



        [SerializeField, LabelText("建筑栏Root")] private RectTransform rt_建筑栏Root;
        [SerializeField, LabelText("关闭按钮")] private Button closeButton;
        [SerializeField, LabelText("分类按钮根节点")] private Transform categoryButtonRoot;
        [SerializeField, LabelText("分类按钮预制体")] private GamePanel_BuildingCategoryButton categoryButtonPrefab;
        [SerializeField, LabelText("建筑项根节点")] private Transform buildingItemRoot;
        [SerializeField, LabelText("建筑项预制体")] private GamePanel_BuildingItem buildingItemPrefab;
        [SerializeField, LabelText("建筑项对象池根节点")] private Transform buildingItemPoolRoot;
        [SerializeField, LabelText("显示空分类")] private bool includeEmptyCategories;
        [SerializeField, LabelText("分类显示顺序")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true)]
        private BuildingCategory[] categoryDisplayOrder =
        {
            BuildingCategory.人口,
            BuildingCategory.农业,
            BuildingCategory.工业,
            BuildingCategory.经济,
            BuildingCategory.科研,
            BuildingCategory.市政,
            BuildingCategory.军事,
            BuildingCategory.交通,
            BuildingCategory.装饰,
            BuildingCategory.奇观
        };

        [SerializeField, LabelText("库存变化时刷新")] private bool refreshWhenInventoryChanges = true;
        [SerializeField, LabelText("自动查找建造放置控制器")] private bool discoverPlacementControllerOnEnable = true;
        [SerializeField, LabelText("建造放置控制器")] private BuildingPlacementController placementController;
        [SerializeField, LabelText("建筑选择事件")] private BuildingPrefabEvent buildingSelected = new BuildingPrefabEvent();
        [SerializeField, LabelText("面板移动时长"), Min(0f)] private float panelMotionDuration = 0.18f;
        [SerializeField, LabelText("隐藏偏移余量"), Min(0f)] private float panelHiddenPadding = 48f;

        private readonly List<GamePanel_BuildingCategoryButton> categoryButtons = new List<GamePanel_BuildingCategoryButton>();
        private readonly List<GamePanel_BuildingItem> buildingItems = new List<GamePanel_BuildingItem>();
        private readonly List<GamePanel_BuildingItem> buildingItemPool = new List<GamePanel_BuildingItem>();
        private BuildingCategory selectedCategory = BuildingCategory.None;
        private BuildingPlacementController subscribedPlacementController;
        private bool subscribedToInventory;
        private bool subscribedToBuildings;
        private bool subscribedToTechnology;
        private bool subscribedToBlueprints;
        private bool hasCachedPanelPosition;
        private Vector2 buildingPanelVisiblePosition;
        private Coroutine panelMotionRoutine;

        public BuildingPrefabEvent BuildingSelected => buildingSelected;
        public BuildingCategory SelectedCategory => selectedCategory;

        private void Awake()
        {
            gamePanel = GetComponentInParent<UIPanel_Game>();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseButtonClicked);
            }

            if (btn_拆除模式 != null)
            {
                btn_拆除模式.onValueChanged.AddListener(HandleDemolitionModeToggleChanged);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseButtonClicked);
            }

            if (btn_拆除模式 != null)
            {
                btn_拆除模式.onValueChanged.RemoveListener(HandleDemolitionModeToggleChanged);
            }

            UnsubscribePlacementController();
        }

        private void OnEnable()
        {
            ResolveGameSystem();
            ResolvePlacementController();
            SubscribePlacementController();
            SyncDemolitionToggleFromController();
            SubscribeRuntimeChanges();
            Refresh();
        }

        private void OnDisable()
        {
            panelMotionRoutine = null;
            UnsubscribeRuntimeChanges();
            UnsubscribePlacementController();
        }


        public void Show()
        {
            gameObject.SetActive(true);
            RefreshFromGameSystem();
            //激活对象 然后rt_建筑栏Root从下方进入屏幕
            CachePanelPosition();
            MoveBuildingPanel(true, false);
        }
        public void Hide()
        {
            //rt_建筑栏Root 向下移除屏幕 之后取消激活对象
            CachePanelPosition();
            MoveBuildingPanel(false, true);
        }

        private void CachePanelPosition()
        {
            if (hasCachedPanelPosition)
            {
                return;
            }

            if (rt_建筑栏Root != null)
            {
                buildingPanelVisiblePosition = rt_建筑栏Root.anchoredPosition;
            }

            hasCachedPanelPosition = true;
        }

        private void MoveBuildingPanel(bool visible, bool deactivateWhenHidden)
        {
            if (panelMotionRoutine != null)
            {
                StopCoroutine(panelMotionRoutine);
                panelMotionRoutine = null;
            }

            Vector2 target = visible
                ? buildingPanelVisiblePosition
                : buildingPanelVisiblePosition + Vector2.down * GetHideDistance(rt_建筑栏Root);

            if (!isActiveAndEnabled || panelMotionDuration <= 0f)
            {
                SetAnchoredPosition(rt_建筑栏Root, target);
                if (deactivateWhenHidden)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            panelMotionRoutine = StartCoroutine(MoveBuildingPanelRoutine(target, deactivateWhenHidden));
        }

        private IEnumerator MoveBuildingPanelRoutine(Vector2 target, bool deactivateWhenHidden)
        {
            Vector2 start = GetAnchoredPosition(rt_建筑栏Root);
            float elapsed = 0f;

            while (elapsed < panelMotionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / panelMotionDuration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                SetAnchoredPosition(rt_建筑栏Root, Vector2.Lerp(start, target, t));
                yield return null;
            }

            SetAnchoredPosition(rt_建筑栏Root, target);
            panelMotionRoutine = null;

            if (deactivateWhenHidden)
            {
                gameObject.SetActive(false);
            }
        }

        private float GetHideDistance(RectTransform target)
        {
            if (target == null)
            {
                return panelHiddenPadding;
            }

            float height = Mathf.Abs(target.rect.height);
            RectTransform parent = target.parent as RectTransform;
            float parentHint = parent == null ? 0f : Mathf.Abs(parent.rect.height) * 0.25f;
            return Mathf.Max(height, parentHint, 100f) + panelHiddenPadding;
        }

        private static Vector2 GetAnchoredPosition(RectTransform target)
        {
            return target == null ? Vector2.zero : target.anchoredPosition;
        }

        private static void SetAnchoredPosition(RectTransform target, Vector2 position)
        {
            if (target != null)
            {
                target.anchoredPosition = position;
            }
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
        }

        public void SelectCategory(BuildingCategory category)
        {
            selectedCategory = category;
            RefreshCategoryButtonSelection();
            RebuildBuildingItems();
        }

        private void ResolveGameSystem()
        {
            gameSystem = Landsong.GameSystem.Instance;
            buildingCatalog = gameSystem == null ? null : gameSystem.Services.BuildingCatalog;
            inventory = gameSystem == null ? null : gameSystem.Services.Inventory;
            buildings = gameSystem == null ? null : gameSystem.Services.Buildings;
            technology = gameSystem == null ? null : gameSystem.Services.Technology;
        }

        private void ResolvePlacementController()
        {
            if (!discoverPlacementControllerOnEnable || placementController != null)
            {
                return;
            }

            placementController = FindFirstObjectByType<BuildingPlacementController>(FindObjectsInactive.Include);
        }

        private void SubscribePlacementController()
        {
            if (subscribedPlacementController == placementController)
            {
                return;
            }

            UnsubscribePlacementController();

            if (placementController == null)
            {
                return;
            }

            placementController.DemolitionModeChanged += HandlePlacementDemolitionModeChanged;
            subscribedPlacementController = placementController;
        }

        private void UnsubscribePlacementController()
        {
            if (subscribedPlacementController == null)
            {
                return;
            }

            subscribedPlacementController.DemolitionModeChanged -= HandlePlacementDemolitionModeChanged;
            subscribedPlacementController = null;
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

            foreach (var buildingPrefab in buildingCatalog.BuildingPrefabs)
            {
                if (buildingPrefab == null || !buildingPrefab.HasDefinition)
                {
                    continue;
                }

                var definition = buildingPrefab.Definition;
                if (definition.Category == BuildingCategory.None)
                {
                    continue;
                }

                var availability = Evaluate(buildingPrefab);
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
                button.Bind(category, category == selectedCategory, SelectCategory);
                categoryButtons.Add(button);
            }
        }

        private void RefreshCategoryButtonSelection()
        {
            for (var i = 0; i < categoryButtons.Count; i++)
            {
                var button = categoryButtons[i];
                if (button != null)
                {
                    button.SetSelected(button.Category == selectedCategory);
                }
            }
        }

        private void RebuildBuildingItems()
        {
            ReleaseActiveBuildingItems();

            if (buildingCatalog == null || buildingItemRoot == null || buildingItemPrefab == null)
            {
                return;
            }

            var visibleEntries = new List<BuildingDisplayEntry>();
            var buildingPrefabs = buildingCatalog.BuildingPrefabs;

            for (var prefabIndex = 0; prefabIndex < buildingPrefabs.Count; prefabIndex++)
            {
                var buildingPrefab = buildingPrefabs[prefabIndex];
                if (buildingPrefab == null || !buildingPrefab.HasDefinition)
                {
                    continue;
                }

                var definition = buildingPrefab.Definition;
                if (!ContainsCategory(definition.Category, selectedCategory))
                {
                    continue;
                }

                var availability = Evaluate(buildingPrefab);
                if (!availability.IsVisible)
                {
                    continue;
                }

                var styles = buildingPrefab.FamilyDefinition?.Presentation?.Styles;
                if (styles == null || styles.Count == 0)
                {
                    visibleEntries.Add(new BuildingDisplayEntry(buildingPrefab, prefabIndex, null));
                }
                else
                {
                    for (var styleIndex = 0; styleIndex < styles.Count; styleIndex++)
                    {
                        if (styles[styleIndex] != null && styles[styleIndex].IsValid)
                        {
                            visibleEntries.Add(new BuildingDisplayEntry(
                                buildingPrefab,
                                prefabIndex,
                                styles[styleIndex]));
                        }
                    }
                }
            }

            visibleEntries.Sort(CompareBuildingDisplayEntries);

            for (var i = 0; i < visibleEntries.Count; i++)
            {
                var buildingPrefab = visibleEntries[i].BuildingPrefab;
                var style = visibleEntries[i].Style;
                var availability = Evaluate(buildingPrefab);
                var item = GetBuildingItemFromPool();
                item.Bind(buildingPrefab, availability, style, HandleBuildingItemClicked);
                buildingItems.Add(item);
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
            var result = CompareBuildingPrefabsForDisplay(left.BuildingPrefab, right.BuildingPrefab);
            if (result != 0) return result;
            result = left.CatalogIndex.CompareTo(right.CatalogIndex);
            return result != 0
                ? result
                : CompareStableText(left.Style?.StyleId, right.Style?.StyleId);
        }

        private static int CompareBuildingPrefabsForDisplay(BuildingBase left, BuildingBase right)
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

            var leftDefinition = left.Definition;
            var rightDefinition = right.Definition;

            var result = leftDefinition.BuildMenuSortOrder.CompareTo(rightDefinition.BuildMenuSortOrder);
            if (result != 0)
            {
                return result;
            }

            result = CompareStableText(leftDefinition.FamilyId, rightDefinition.FamilyId);
            if (result != 0)
            {
                return result;
            }

            result = CompareStableText(leftDefinition.DisplayName, rightDefinition.DisplayName);
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
            public BuildingDisplayEntry(
                BuildingBase buildingPrefab,
                int catalogIndex,
                BuildingStyleDefinition style)
            {
                BuildingPrefab = buildingPrefab;
                CatalogIndex = catalogIndex;
                Style = style;
            }

            public BuildingBase BuildingPrefab { get; }
            public int CatalogIndex { get; }
            public BuildingStyleDefinition Style { get; }
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

        private BuildingAvailability Evaluate(BuildingBase buildingPrefab)
        {
            return buildings == null
                ? BuildingAvailability.Hidden(buildingPrefab, BuildingUnavailableReason.Hidden)
                : buildings.EvaluateAvailability(buildingPrefab);
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

        private void HandleBuildingItemClicked(BuildingBase buildingPrefab, string styleId)
        {
            var availability = Evaluate(buildingPrefab);
            if (!availability.CanBuild)
            {
                return;
            }

            if (placementController != null)
            {
                DisableDemolitionMode();
                placementController.BeginPlacement(buildingPrefab, styleId);
            }

            buildingSelected.Invoke(buildingPrefab);
        }

        private void HandleDemolitionModeToggleChanged(bool isOn)
        {
            ResolvePlacementController();
            SubscribePlacementController();
            if (placementController == null)
            {
                SetDemolitionToggleWithoutNotify(false);
                return;
            }

            if (isOn)
            {
                placementController.BeginDemolitionMode();
            }
            else if (placementController.IsDemolitionMode)
            {
                placementController.CancelPlacement();
            }
        }

        private void HandlePlacementDemolitionModeChanged(bool isActive)
        {
            SetDemolitionToggleWithoutNotify(isActive);
        }

        private void DisableDemolitionMode()
        {
            SetDemolitionToggleWithoutNotify(false);
            if (placementController != null && placementController.IsDemolitionMode)
            {
                placementController.CancelPlacement();
            }
        }

        private void SyncDemolitionToggleFromController()
        {
            SetDemolitionToggleWithoutNotify(placementController != null && placementController.IsDemolitionMode);
        }

        private void SetDemolitionToggleWithoutNotify(bool isOn)
        {
            if (btn_拆除模式 != null)
            {
                btn_拆除模式.SetIsOnWithoutNotify(isOn);
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
            SubscribeTechnology();
            SubscribeBlueprints();
        }

        private void UnsubscribeRuntimeChanges()
        {
            UnsubscribeInventory();
            UnsubscribeBuildings();
            UnsubscribeTechnology();
            UnsubscribeBlueprints();
        }

        private void HandleInventoryChanged(InventoryService changedInventory)
        {
            Refresh();
        }

        private void HandleBuildingsChanged(BuildingService changedBuildings)
        {
            Refresh();
        }

        private void SubscribeTechnology()
        {
            if (subscribedToTechnology || technology == null)
            {
                return;
            }

            technology.StateChanged += HandleTechnologyChanged;
            subscribedToTechnology = true;
        }

        private void UnsubscribeTechnology()
        {
            if (!subscribedToTechnology || technology == null)
            {
                subscribedToTechnology = false;
                return;
            }

            technology.StateChanged -= HandleTechnologyChanged;
            subscribedToTechnology = false;
        }

        private void HandleTechnologyChanged(TechnologyService changedTechnology)
        {
            technology = changedTechnology;
            Refresh();
        }

        private void SubscribeBlueprints()
        {
            if (subscribedToBlueprints || gameSystem == null)
            {
                return;
            }

            gameSystem.Services.BuildingBlueprints.StateChanged += HandleBuildingBlueprintsChanged;
            subscribedToBlueprints = true;
        }

        private void UnsubscribeBlueprints()
        {
            if (!subscribedToBlueprints || gameSystem == null)
            {
                subscribedToBlueprints = false;
                return;
            }

            gameSystem.Services.BuildingBlueprints.StateChanged -= HandleBuildingBlueprintsChanged;
            subscribedToBlueprints = false;
        }

        private void HandleBuildingBlueprintsChanged(BuildingBlueprintService changedBlueprints)
        {
            Refresh();
        }
    }

    [Serializable]
    public sealed class BuildingPrefabEvent : UnityEvent<BuildingBase>
    {
    }
}
