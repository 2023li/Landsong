using System;
using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_BuildingItem : MonoBehaviour
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



        [SerializeField] private Button button;
        [SerializeField] private Image icon;

        private BuildingBase buildingPrefab;
        private BuildingAvailability availability;
        private Action<BuildingBase> clicked;

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

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
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
