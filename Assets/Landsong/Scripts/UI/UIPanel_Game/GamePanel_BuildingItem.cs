using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_BuildingItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        #region 状态
        //不可用时设置为true
        [SerializeField] private GameObject state_不可用;
        //材料不足设置为true
        [SerializeField] private GameObject state_材料不足;
        //建筑未开发完成时显示
        [SerializeField] private GameObject state_建筑未开发完成;

        [SerializeField] private string textColor_UnderLimit = "9FFF76";
        [SerializeField] private string textColor_AtLimit = "FFFFFF";
        //只有在建筑可用时显示 显示为 1/20 无限制不显示
        [SerializeField] private TMP_Text 数量限制;
        #endregion

        #region 详情面板
        [SerializeField, LabelText("详情面板")] private RectTransform root_详情面板;
        [SerializeField, LabelText("建筑名称")] private TMP_Text txt_建筑名称;
        [SerializeField, LabelText("详情数量限制")] private TMP_Text txt_详情数量限制;
        [SerializeField, LabelText("建筑描述")] private TMP_Text txt_描述;
        [SerializeField, LabelText("描述布局")] private LayoutElement descriptionLayoutElement;
        [SerializeField,LabelText("消耗面板")] private RectTransform root_消耗面板;
        [SerializeField,LabelText("消耗文本预制体")] private TMP_Text prefab_材料文本预制体;
        [SerializeField, LabelText("长按显示详情延迟"), Min(0.05f)] private float longPressDuration = 0.45f;
        #endregion



        [SerializeField] private Button button;
        [SerializeField] private Image icon;

        private BuildingBase buildingPrefab;
        private BuildingStyleDefinition styleDefinition;
        private string styleId = string.Empty;
        private BuildingAvailability availability;
        private Action<BuildingBase, string> clicked;
        private readonly List<TMP_Text> materialTextInstances = new List<TMP_Text>();
        private Coroutine longPressRoutine;
        private bool pointerPressed;
        private bool longPressTriggered;
        private bool longPressDetailVisible;
        private bool suppressNextClick;

        private void Reset()
        {
            button = GetComponent<Button>();
            icon = GetComponentInChildren<Image>(true);
        }

        private void OnValidate()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void Awake()
        {
            PrepareDetailPanelRaycasts();
            HideDetailPanel();
        }

        private void OnDisable()
        {
            pointerPressed = false;
            longPressTriggered = false;
            longPressDetailVisible = false;
            suppressNextClick = false;
            CancelLongPress();
            HideDetailPanel();
        }

        private void OnDestroy()
        {
            CancelLongPress();

            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            ClearMaterialTextInstances();
        }

        public void Bind(
            BuildingBase sourceBuildingPrefab,
            BuildingAvailability buildingAvailability,
            BuildingStyleDefinition style,
            Action<BuildingBase, string> onClicked)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            pointerPressed = false;
            longPressTriggered = false;
            longPressDetailVisible = false;
            suppressNextClick = false;
            CancelLongPress();

            buildingPrefab = sourceBuildingPrefab;
            styleDefinition = style;
            styleId = style == null ? string.Empty : style.StyleId;
            availability = buildingAvailability;
            clicked = onClicked;
            var definition = buildingPrefab == null ? null : buildingPrefab.Definition;

            if (icon != null)
            {
                icon.sprite = style?.Icon != null ? style.Icon : definition?.Icon;
                icon.enabled = icon.sprite != null;
            }

            if (button != null)
            {
                button.interactable = buildingPrefab != null && buildingPrefab.HasDefinition;
                button.onClick.AddListener(HandleClicked);
            }

            RefreshState();
            HideDetailPanel();
        }

        public void Unbind()
        {
            pointerPressed = false;
            longPressTriggered = false;
            longPressDetailVisible = false;
            suppressNextClick = false;
            CancelLongPress();

            buildingPrefab = null;
            styleDefinition = null;
            styleId = string.Empty;
            availability = BuildingAvailability.Hidden(null, BuildingUnavailableReason.Hidden);
            clicked = null;

            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveListener(HandleClicked);
            }

            RefreshState();
            HideDetailPanel();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Application.isMobilePlatform)
            {
                return;
            }

            ShowDetailPanel();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (longPressTriggered || longPressDetailVisible)
            {
                suppressNextClick = true;
            }

            pointerPressed = false;
            longPressTriggered = false;
            longPressDetailVisible = false;
            CancelLongPress();
            HideDetailPanel();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerPressed = true;
            longPressTriggered = false;
            longPressDetailVisible = false;
            suppressNextClick = false;
            CancelLongPress();

            if (Application.isMobilePlatform && IsTouchPointer(eventData))
            {
                longPressRoutine = StartCoroutine(ShowDetailPanelAfterLongPress());
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pointerPressed = false;
            CancelLongPress();

            if (!longPressTriggered && !longPressDetailVisible)
            {
                return;
            }

            longPressTriggered = false;
            longPressDetailVisible = false;
            suppressNextClick = true;
            HideDetailPanel();
        }

        private void RefreshState()
        {
            var definition = buildingPrefab == null ? null : buildingPrefab.Definition;
            var hasDefinition = buildingPrefab != null && buildingPrefab.HasDefinition;
            var reason = hasDefinition
                ? availability.FirstUnavailableReason
                : BuildingUnavailableReason.Hidden;
            var isDevelopmentIncomplete = reason == BuildingUnavailableReason.DevelopmentIncomplete;
            var isMissingMaterials = reason == BuildingUnavailableReason.MissingMaterials;
            var isUnavailable = hasDefinition
                                && !isDevelopmentIncomplete
                                && !isMissingMaterials
                                && !availability.IsAvailable;

            SetActive(state_建筑未开发完成, isDevelopmentIncomplete);
            SetActive(state_材料不足, isMissingMaterials);
            SetActive(state_不可用, isUnavailable);
            RefreshCountLimitText(isDevelopmentIncomplete);
        }

        private void RefreshCountLimitText(bool isDevelopmentIncomplete)
        {
            if (数量限制 == null)
            {
                return;
            }

            var definition = buildingPrefab == null ? null : buildingPrefab.Definition;
            var showCountLimit = definition != null
                                 && !isDevelopmentIncomplete
                                 && definition.HasBuildCountLimit
                                 && availability.IsVisible
                                 && availability.IsDevelopmentCompleted
                                 && availability.IsBlueprintUnlocked;
            数量限制.gameObject.SetActive(showCountLimit);

            if (!showCountLimit)
            {
                数量限制.text = string.Empty;
                return;
            }

            var builtCount = Mathf.Max(0, availability.BuiltCount);
            var maxBuildCount = Mathf.Max(0, availability.MaxBuildCount);
            数量限制.text = $"{builtCount}/{maxBuildCount}";
            数量限制.color = ParseTextColor(
                builtCount < maxBuildCount ? textColor_UnderLimit : textColor_AtLimit,
                数量限制.color);
        }

        private void HandleClicked()
        {
            if (suppressNextClick)
            {
                suppressNextClick = false;
                return;
            }

            clicked?.Invoke(buildingPrefab, styleId);
        }

        private void ShowDetailPanel()
        {
            if (root_详情面板 == null || buildingPrefab == null || !buildingPrefab.HasDefinition)
            {
                return;
            }

            PrepareDetailPanelRaycasts();
            ClearMaterialTextInstances();

            SetActive(root_详情面板.gameObject, true);
            if (root_消耗面板 != null)
            {
                SetActive(root_消耗面板.gameObject, true);
            }

            RefreshDetailText();
            CreateMaterialTextInstances();
            ConfigureCostPanelLayout();
            RebuildDetailLayout();
        }

        private void HideDetailPanel()
        {
            ClearMaterialTextInstances();
            if (root_详情面板 != null)
            {
                SetActive(root_详情面板.gameObject, false);
            }
        }

        private void RefreshDetailText()
        {
            var definition = buildingPrefab == null ? null : buildingPrefab.Definition;
            SetText(txt_建筑名称, FormatBuildingName(definition, styleDefinition));
            SetText(txt_描述, BuildDescriptionText());
            RefreshDetailCountLimitText(definition);
            RefreshDescriptionLayout();
        }

        private void RefreshDescriptionLayout()
        {
            if (txt_描述 == null || descriptionLayoutElement == null)
            {
                return;
            }

            var availableWidth = root_详情面板 == null
                ? txt_描述.rectTransform.rect.width
                : root_详情面板.rect.width;

            if (root_详情面板 != null
                && root_详情面板.TryGetComponent<VerticalLayoutGroup>(out var layoutGroup))
            {
                availableWidth -= layoutGroup.padding.horizontal;
            }

            availableWidth = Mathf.Max(1f, availableWidth);
            var preferredSize = txt_描述.GetPreferredValues(
                txt_描述.text ?? string.Empty,
                availableWidth,
                Mathf.Infinity);
            descriptionLayoutElement.preferredHeight = Mathf.Max(
                descriptionLayoutElement.minHeight,
                Mathf.Ceil(preferredSize.y));
        }

        private void RefreshDetailCountLimitText(BuildingDefinition definition)
        {
            if (txt_详情数量限制 == null)
            {
                return;
            }

            var hasDefinition = definition != null;
            txt_详情数量限制.gameObject.SetActive(hasDefinition);
            if (!hasDefinition)
            {
                txt_详情数量限制.text = string.Empty;
                return;
            }

            if (!definition.HasBuildCountLimit)
            {
                txt_详情数量限制.text = "无限制";
                txt_详情数量限制.color = ParseTextColor(
                    textColor_UnderLimit,
                    txt_详情数量限制.color);
                return;
            }

            var builtCount = Mathf.Max(0, availability.BuiltCount);
            var maxBuildCount = Mathf.Max(0, availability.MaxBuildCount);
            txt_详情数量限制.text = $"{builtCount}/{maxBuildCount}";
            txt_详情数量限制.color = ParseTextColor(
                builtCount < maxBuildCount ? textColor_UnderLimit : textColor_AtLimit,
                txt_详情数量限制.color);
        }

        private static string FormatBuildingName(
            BuildingDefinition definition,
            BuildingStyleDefinition style)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return style == null || string.IsNullOrWhiteSpace(style.DisplayName)
                ? definition.DisplayName
                : $"{definition.DisplayName} · {style.DisplayName}";
        }

        private string BuildDescriptionText()
        {
            var statusText = GetStatusText(availability);
            var overviewText = buildingPrefab == null ? string.Empty : buildingPrefab.GetOverviewInfo();
            overviewText = string.IsNullOrWhiteSpace(overviewText) ? string.Empty : overviewText.Trim();

            if (string.IsNullOrWhiteSpace(statusText))
            {
                return overviewText;
            }

            return string.IsNullOrWhiteSpace(overviewText)
                ? statusText
                : $"{statusText}\n{overviewText}";
        }

        private static string GetStatusText(BuildingAvailability buildingAvailability)
        {
            if (buildingAvailability.CanBuild)
            {
                return "可建造";
            }

            if (buildingAvailability.IsAvailable)
            {
                return "可用，材料不足";
            }

            return buildingAvailability.FirstUnavailableReason switch
            {
                BuildingUnavailableReason.DevelopmentIncomplete => "建筑未开发完成",
                BuildingUnavailableReason.BuildLimitReached => "数量已达上限",
                BuildingUnavailableReason.MissingMaterials => "材料不足",
                _ => "不可用"
            };
        }

        private int CreateMaterialTextInstances()
        {
            if (prefab_材料文本预制体 == null || buildingPrefab == null)
            {
                return 0;
            }

            var definition = buildingPrefab == null ? null : buildingPrefab.Definition;
            var createdCount = 0;
            createdCount += CreateCostSection(
                "放置消耗",
                definition == null ? null : definition.PlacementCosts);

            var constructionCosts = buildingPrefab.FamilyDefinition?.Construction?.GetTotalCosts();

            createdCount += CreateCostSection("施工消耗", constructionCosts);
            return createdCount;
        }

        private int CreateCostSection(string title, IReadOnlyList<BuildingCost> costs)
        {
            var createdCount = 0;
            createdCount += CreateMaterialTextInstance($"<b>{title}</b>");
            createdCount += CreateMaterialTextInstance(string.Empty);

            var materialCount = 0;
            if (costs != null)
            {
                for (var i = 0; i < costs.Count; i++)
                {
                    var cost = costs[i];
                    if (!cost.IsValid)
                    {
                        continue;
                    }

                    createdCount += CreateMaterialTextInstance(FormatMaterialCost(cost));
                    materialCount++;
                }
            }

            if (materialCount == 0)
            {
                createdCount += CreateMaterialTextInstance("无");
                materialCount = 1;
            }

            if (materialCount % 2 != 0)
            {
                createdCount += CreateMaterialTextInstance(string.Empty);
            }

            return createdCount;
        }

        private int CreateMaterialTextInstance(string content)
        {
            if (prefab_材料文本预制体 == null || root_消耗面板 == null)
            {
                return 0;
            }

            var text = Instantiate(prefab_材料文本预制体, root_消耗面板);
            text.text = content ?? string.Empty;
            SetGraphicRaycasts(text.gameObject, false);
            text.gameObject.SetActive(true);
            materialTextInstances.Add(text);
            return 1;
        }

        private void ConfigureCostPanelLayout()
        {
            if (root_消耗面板 == null
                || !root_消耗面板.TryGetComponent<GridLayoutGroup>(out var gridLayout))
            {
                return;
            }

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 2;
        }

        private void RebuildDetailLayout()
        {
            Canvas.ForceUpdateCanvases();
            RefreshDescriptionLayout();

            if (root_消耗面板 != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(root_消耗面板);
            }

            if (root_详情面板 != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(root_详情面板);
            }

            Canvas.ForceUpdateCanvases();
            txt_建筑名称?.ForceMeshUpdate();
            txt_详情数量限制?.ForceMeshUpdate();
            txt_描述?.ForceMeshUpdate();
        }

        private void ClearMaterialTextInstances()
        {
            for (var i = 0; i < materialTextInstances.Count; i++)
            {
                var text = materialTextInstances[i];
                if (text != null)
                {
                    Destroy(text.gameObject);
                }
            }

            materialTextInstances.Clear();
        }

        private static string FormatMaterialCost(BuildingCost cost)
        {
            var itemDefinition = cost.ItemDefinition;
            var itemName = itemDefinition == null ? cost.ItemId : itemDefinition.DisplayName;
            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = "未命名材料";
            }

            return $"{itemName}*{cost.Amount}";
        }

        private IEnumerator ShowDetailPanelAfterLongPress()
        {
            var delay = Mathf.Max(0.05f, longPressDuration);
            var elapsed = 0f;
            while (elapsed < delay)
            {
                if (!pointerPressed)
                {
                    longPressRoutine = null;
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (pointerPressed)
            {
                longPressTriggered = true;
                ShowDetailPanel();
                longPressDetailVisible = root_详情面板 != null && root_详情面板.gameObject.activeSelf;
            }

            longPressRoutine = null;
        }

        private void CancelLongPress()
        {
            if (longPressRoutine == null)
            {
                return;
            }

            StopCoroutine(longPressRoutine);
            longPressRoutine = null;
        }

        private void PrepareDetailPanelRaycasts()
        {
            if (root_详情面板 != null)
            {
                SetGraphicRaycasts(root_详情面板.gameObject, false);
            }

            if (root_消耗面板 == null)
            {
                return;
            }

            if (prefab_材料文本预制体 == null)
            {
                HideMaterialTemplateChildren();
                return;
            }

            var templateTransform = prefab_材料文本预制体.transform;
            if (templateTransform != null && templateTransform.IsChildOf(root_消耗面板))
            {
                prefab_材料文本预制体.gameObject.SetActive(false);
                return;
            }

            HideMaterialTemplateChildren();
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private void HideMaterialTemplateChildren()
        {
            if (root_消耗面板 == null)
            {
                return;
            }

            var texts = root_消耗面板.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null || materialTextInstances.Contains(text))
                {
                    continue;
                }

                text.gameObject.SetActive(false);
            }
        }

        private static void SetGraphicRaycasts(GameObject root, bool raycastTarget)
        {
            if (root == null)
            {
                return;
            }

            var graphics = root.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = raycastTarget;
            }
        }

        private static bool IsTouchPointer(PointerEventData eventData)
        {
            return eventData != null && eventData.pointerId >= 0;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static Color ParseTextColor(string hexColor, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
            {
                return fallback;
            }

            var colorText = hexColor.Trim();
            if (!colorText.StartsWith("#", StringComparison.Ordinal))
            {
                colorText = $"#{colorText}";
            }

            return ColorUtility.TryParseHtmlString(colorText, out var color) ? color : fallback;
        }
    }
}
