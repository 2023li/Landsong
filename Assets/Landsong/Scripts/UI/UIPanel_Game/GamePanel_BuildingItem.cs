using System;
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
    public sealed class GamePanel_BuildingItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

        #region 消耗面板
        [SerializeField,LabelText("消耗面板")] private RectTransform root_消耗面板;
        [SerializeField,LabelText("消耗文本预制体")] private TMP_Text prefab_材料文本预制体;
        #endregion



        [SerializeField] private Button button;
        [SerializeField] private Image icon;

        private BuildingBase buildingPrefab;
        private BuildingAvailability availability;
        private Action<BuildingBase> clicked;
        private readonly List<TMP_Text> materialTextInstances = new List<TMP_Text>();

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
            HideCostPanel();
        }

        private void OnDisable()
        {
            HideCostPanel();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            ClearMaterialTextInstances();
        }

        public void Bind(
            BuildingBase sourceBuildingPrefab,
            BuildingAvailability buildingAvailability,
            Action<BuildingBase> onClicked)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            buildingPrefab = sourceBuildingPrefab;
            availability = buildingAvailability;
            clicked = onClicked;
            var definition = buildingPrefab == null ? null : buildingPrefab.Definition;

            if (icon != null)
            {
                icon.sprite = definition == null ? null : definition.Icon;
                icon.enabled = definition != null && definition.Icon != null;
            }

            if (button != null)
            {
                button.interactable = buildingPrefab != null && buildingPrefab.HasDefinition;
                button.onClick.AddListener(HandleClicked);
            }

            RefreshState();
            HideCostPanel();
        }

        public void Unbind()
        {
            buildingPrefab = null;
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
            HideCostPanel();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ShowCostPanel();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideCostPanel();
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
                                 && availability.IsUnlocked
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
            clicked?.Invoke(buildingPrefab);
        }

        private void ShowCostPanel()
        {
            if (root_消耗面板 == null)
            {
                return;
            }

            ClearMaterialTextInstances();
            CreateMaterialTextInstances();
            SetActive(root_消耗面板.gameObject, true);
        }

        private void HideCostPanel()
        {
            ClearMaterialTextInstances();
            if (root_消耗面板 != null)
            {
                SetActive(root_消耗面板.gameObject, false);
            }
        }

        private void CreateMaterialTextInstances()
        {
            if (prefab_材料文本预制体 == null)
            {
                return;
            }

            var definition = buildingPrefab == null ? null : buildingPrefab.Definition;
            var costs = definition == null ? null : definition.PlacementCosts;
            if (costs == null)
            {
                return;
            }

            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (!cost.IsValid)
                {
                    continue;
                }

                var text = Instantiate(prefab_材料文本预制体, root_消耗面板);
                text.text = FormatMaterialCost(cost);
                text.gameObject.SetActive(true);
                materialTextInstances.Add(text);
            }
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

            return $"{itemName} x{cost.Amount}";
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
