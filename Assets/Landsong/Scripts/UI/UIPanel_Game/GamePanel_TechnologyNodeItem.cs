using System;
using System.Collections.Generic;
using Landsong.TechnologySystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public enum TechnologyNodeVisualState
    {
        Invalid = 0,
        Preview = 1,
        Locked = 2,
        Available = 3,
        CurrentResearch = 4,
        Queued = 5,
        Completed = 6,
        Repeatable = 7
    }

    public sealed class GamePanel_TechnologyNodeItem : MonoBehaviour
    {
        [Header("科技数据")]
        [SerializeField] private TechnologyDefinition definition;

        [Header("基础显示")]
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private TMP_Text statusLabel;

        [Header("状态标记")]
        [SerializeField] private GameObject selectedRoot;
        [SerializeField] private GameObject currentResearchRoot;
        [SerializeField] private GameObject queuedResearchRoot;

        [Header("研究进度")]
        [SerializeField] private Slider researchProgressSlider;

        [Header("解锁内容")]
        [SerializeField] private Transform unlockIconRoot;
        [SerializeField, Min(0)] private int maxUnlockIcons = 5;

        [Header("状态颜色")]
        [SerializeField] private Color invalidColor = new Color(0.18f, 0.18f, 0.18f, 0.8f);
        [SerializeField] private Color previewColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.24f, 0.24f, 0.24f, 1f);
        [SerializeField] private Color availableColor = new Color(0.32f, 0.48f, 0.25f, 1f);
        [SerializeField] private Color currentResearchColor = new Color(0.25f, 0.48f, 0.7f, 1f);
        [SerializeField] private Color queuedColor = new Color(0.55f, 0.42f, 0.2f, 1f);
        [SerializeField] private Color completedColor = new Color(0.62f, 0.52f, 0.25f, 1f);
        [SerializeField] private Color repeatableColor = new Color(0.46f, 0.32f, 0.58f, 1f);

        private readonly List<GameObject> spawnedUnlockIcons = new List<GameObject>();
        private readonly List<TechnologyUnlockContent> unlockContents = new List<TechnologyUnlockContent>();
        private TechnologyDefinition unlockIconsDefinition;
        private TechnologyUnlockContentRegistry unlockContentRegistry;
        private TechnologyUnlockContentRegistry unlockIconsContentRegistry;
        private int unlockIconsContentVersion = -1;
        private GameObject unlockTooltipRoot;
        private TMP_Text unlockTooltipLabel;
        private TechnologyService technology;
        private Action<TechnologyDefinition> clicked;

        public TechnologyDefinition Definition => definition;
        public TechnologyNodeVisualState VisualState { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
                button.onClick.AddListener(HandleClicked);
            }

            RefreshUnlockIcons();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
            Refresh();
        }

        public void SetDefinition(TechnologyDefinition newDefinition)
        {
            definition = newDefinition;
            RefreshUnlockIcons();
            Refresh();
        }

        public void Bind(
            TechnologyService targetTechnology,
            TechnologyUnlockContentRegistry targetUnlockContentRegistry,
            Action<TechnologyDefinition> onClicked,
            bool selected)
        {
            technology = targetTechnology;
            unlockContentRegistry = targetUnlockContentRegistry;
            clicked = onClicked;
            SetSelected(selected);
            RefreshUnlockIcons();
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            SetActive(selectedRoot, selected);
        }

        public void Refresh()
        {
            ResolveReferences();
            VisualState = ResolveVisualState();

            var validDefinition = definition != null;
            SetText(nameLabel, validDefinition ? definition.DisplayName : gameObject.name);
            SetText(costLabel, validDefinition ? $"{definition.SciencePointCost} 科技点" : string.Empty);
            SetText(statusLabel, FormatStatus(VisualState));

            if (iconImage != null)
            {
                iconImage.sprite = validDefinition ? definition.Icon : null;
                iconImage.enabled = validDefinition && definition.HasIcon;
            }

            if (button != null)
            {
                // 锁定节点仍可点击，以便让面板把它的前置科技加入研究队列。
                button.interactable = validDefinition;
            }

            ApplyVisualState(VisualState);
        }

        private TechnologyNodeVisualState ResolveVisualState()
        {
            if (definition == null)
            {
                return TechnologyNodeVisualState.Invalid;
            }

            if (technology == null)
            {
                return TechnologyNodeVisualState.Preview;
            }

            if (technology.IsCurrentResearch(definition))
            {
                return TechnologyNodeVisualState.CurrentResearch;
            }

            if (technology.IsQueuedResearch(definition))
            {
                return TechnologyNodeVisualState.Queued;
            }

            if (technology.IsUnlocked(definition.TechnologyId))
            {
                return definition.AllowRepeatResearch
                    ? TechnologyNodeVisualState.Repeatable
                    : TechnologyNodeVisualState.Completed;
            }

            return technology.CanStartResearch(definition, out _)
                ? TechnologyNodeVisualState.Available
                : TechnologyNodeVisualState.Locked;
        }

        private void ApplyVisualState(TechnologyNodeVisualState state)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = GetStateColor(state);
            }

            SetActive(currentResearchRoot, state == TechnologyNodeVisualState.CurrentResearch);
            SetActive(queuedResearchRoot, state == TechnologyNodeVisualState.Queued);

            var showProgress = state == TechnologyNodeVisualState.CurrentResearch;
            if (researchProgressSlider != null)
            {
                researchProgressSlider.gameObject.SetActive(showProgress);
                researchProgressSlider.interactable = false;
                researchProgressSlider.wholeNumbers = true;

                var required = definition == null ? 0 : Mathf.Max(0, definition.SciencePointCost);
                var progress = technology == null || definition == null
                    ? 0
                    : technology.GetResearchProgress(definition);
                researchProgressSlider.minValue = 0f;
                researchProgressSlider.maxValue = Mathf.Max(1, required);
                researchProgressSlider.SetValueWithoutNotify(
                    showProgress
                        ? required <= 0 ? 1f : Mathf.Clamp(progress, 0, required)
                        : 0f);
            }
        }

        private Color GetStateColor(TechnologyNodeVisualState state)
        {
            return state switch
            {
                TechnologyNodeVisualState.Preview => previewColor,
                TechnologyNodeVisualState.Locked => lockedColor,
                TechnologyNodeVisualState.Available => availableColor,
                TechnologyNodeVisualState.CurrentResearch => currentResearchColor,
                TechnologyNodeVisualState.Queued => queuedColor,
                TechnologyNodeVisualState.Completed => completedColor,
                TechnologyNodeVisualState.Repeatable => repeatableColor,
                _ => invalidColor
            };
        }

        private string FormatStatus(TechnologyNodeVisualState state)
        {
            return state switch
            {
                TechnologyNodeVisualState.Invalid => "未配置科技",
                TechnologyNodeVisualState.Preview => "预览",
                TechnologyNodeVisualState.Locked => "前置未完成",
                TechnologyNodeVisualState.Available => "可研究",
                TechnologyNodeVisualState.CurrentResearch => FormatResearchProgress(),
                TechnologyNodeVisualState.Queued => FormatQueueStatus(),
                TechnologyNodeVisualState.Completed => "已研究",
                TechnologyNodeVisualState.Repeatable => "可重复研究",
                _ => string.Empty
            };
        }

        private string FormatQueueStatus()
        {
            var index = technology == null ? -1 : technology.GetResearchQueueIndex(definition);
            return index < 0 ? "队列中" : $"队列 {index + 1}";
        }

        private string FormatResearchProgress()
        {
            var progress = technology == null || definition == null ? 0 : technology.GetResearchProgress(definition);
            var required = definition == null ? 0 : Mathf.Max(0, definition.SciencePointCost);
            return required <= 0 ? "无需科技点" : $"{progress}/{required}";
        }

        private void RefreshUnlockIcons()
        {
            var contentVersion = unlockContentRegistry == null ? -1 : unlockContentRegistry.Version;
            if (Application.isPlaying
                && unlockIconsDefinition == definition
                && unlockIconsContentRegistry == unlockContentRegistry
                && unlockIconsContentVersion == contentVersion)
            {
                return;
            }

            ClearUnlockIcons();
            if (definition == null
                || unlockIconRoot == null
                || maxUnlockIcons <= 0)
            {
                return;
            }

            CollectUnlockContents();
            var hasOverflow = unlockContents.Count > maxUnlockIcons;
            var normalIconCount = hasOverflow
                ? Mathf.Max(0, maxUnlockIcons - 1)
                : Mathf.Min(unlockContents.Count, maxUnlockIcons);

            for (var i = 0; i < normalIconCount; i++)
            {
                var icon = CreateUnlockIcon(unlockContents[i], spawnedUnlockIcons.Count);
                spawnedUnlockIcons.Add(icon);
            }

            if (hasOverflow)
            {
                var remainingCount = unlockContents.Count - normalIconCount;
                var overflow = new TechnologyUnlockContent(
                    $"technology-ui.overflow:{definition.TechnologyId}",
                    null,
                    $"另有 {remainingCount} 项解锁内容",
                    TechnologyUnlockContentKind.Other,
                    shortLabel: $"+{remainingCount}");
                spawnedUnlockIcons.Add(CreateUnlockIcon(overflow, spawnedUnlockIcons.Count));
            }

            unlockIconRoot.gameObject.SetActive(spawnedUnlockIcons.Count > 0);
            unlockIconsDefinition = definition;
            unlockIconsContentRegistry = unlockContentRegistry;
            unlockIconsContentVersion = contentVersion;
        }

        private void CollectUnlockContents()
        {
            unlockContents.Clear();
            if (definition == null)
            {
                return;
            }

            unlockContentRegistry?.Collect(definition, unlockContents);
        }

        private void OnDisable()
        {
            HideUnlockTooltip();
        }

        private void ClearUnlockIcons()
        {
            for (var i = spawnedUnlockIcons.Count - 1; i >= 0; i--)
            {
                var icon = spawnedUnlockIcons[i];
                if (icon != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(icon);
                    }
                    else
                    {
                        DestroyImmediate(icon);
                    }
                }
            }

            spawnedUnlockIcons.Clear();
            unlockIconsDefinition = null;
            unlockIconsContentRegistry = null;
            unlockIconsContentVersion = -1;
            if (unlockIconRoot != null)
            {
                unlockIconRoot.gameObject.SetActive(false);
            }
        }

        private GameObject CreateUnlockIcon(TechnologyUnlockContent content, int index)
        {
            var instance = new GameObject(
                "解锁内容",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = instance.GetComponent<RectTransform>();
            rect.SetParent(unlockIconRoot, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(index * 46f, 0f);
            rect.sizeDelta = new Vector2(40f, 40f);

            var iconImage = instance.GetComponent<Image>();
            iconImage.sprite = content.Icon;
            iconImage.preserveAspect = true;
            iconImage.enabled = true;
            iconImage.color = content.Icon == null
                ? new Color(0.12f, 0.12f, 0.12f, 0.92f)
                : Color.white;
            iconImage.raycastTarget = true;

            var hover = instance.AddComponent<TechnologyUnlockIconHover>();
            hover.Bind(() => ShowUnlockTooltip(content), HideUnlockTooltip);

            var amountSuffix = content.Amount > 1 ? $"_x{content.Amount}" : string.Empty;
            instance.name = string.IsNullOrWhiteSpace(content.DisplayName)
                ? "解锁内容"
                : $"解锁_{content.DisplayName}{amountSuffix}";

            var badgeText = ResolveBadgeText(content);
            if (!string.IsNullOrWhiteSpace(badgeText))
            {
                var amountObject = new GameObject(
                    "解锁标记",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                var amountRect = amountObject.GetComponent<RectTransform>();
                amountRect.SetParent(rect, false);
                amountRect.anchorMin = content.Icon == null ? Vector2.zero : new Vector2(0.3f, 0f);
                amountRect.anchorMax = Vector2.one;
                amountRect.offsetMin = Vector2.zero;
                amountRect.offsetMax = Vector2.zero;

                var amountLabel = amountObject.GetComponent<TextMeshProUGUI>();
                amountLabel.text = badgeText;
                amountLabel.fontSize = content.Icon == null ? 14f : 12f;
                amountLabel.fontStyle = FontStyles.Bold;
                amountLabel.alignment = content.Icon == null
                    ? TextAlignmentOptions.Center
                    : TextAlignmentOptions.BottomRight;
                amountLabel.color = Color.white;
                amountLabel.outlineColor = Color.black;
                amountLabel.outlineWidth = 0.15f;
                amountLabel.raycastTarget = false;
            }

            return instance;
        }

        private void ShowUnlockTooltip(TechnologyUnlockContent content)
        {
            EnsureUnlockTooltip();
            if (unlockTooltipRoot == null || unlockTooltipLabel == null)
            {
                return;
            }

            unlockTooltipLabel.text = content.Amount > 1
                ? $"{content.DisplayName} ×{content.Amount}"
                : content.DisplayName;
            unlockTooltipRoot.SetActive(true);
            unlockTooltipRoot.transform.SetAsLastSibling();
        }

        private void HideUnlockTooltip()
        {
            if (unlockTooltipRoot != null)
            {
                unlockTooltipRoot.SetActive(false);
            }
        }

        private void EnsureUnlockTooltip()
        {
            if (unlockTooltipRoot != null)
            {
                return;
            }

            unlockTooltipRoot = new GameObject(
                "解锁内容提示",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = unlockTooltipRoot.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 4f);
            rect.sizeDelta = new Vector2(360f, 34f);

            var background = unlockTooltipRoot.GetComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.05f, 0.94f);
            background.raycastTarget = false;

            var labelObject = new GameObject(
                "文字",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 2f);
            labelRect.offsetMax = new Vector2(-8f, -2f);

            unlockTooltipLabel = labelObject.GetComponent<TextMeshProUGUI>();
            unlockTooltipLabel.fontSize = 15f;
            unlockTooltipLabel.alignment = TextAlignmentOptions.Center;
            unlockTooltipLabel.color = Color.white;
            unlockTooltipLabel.raycastTarget = false;
            unlockTooltipRoot.SetActive(false);
        }

        private static string ResolveBadgeText(TechnologyUnlockContent content)
        {
            if (content.Amount > 1)
            {
                return $"×{content.Amount}";
            }

            if (content.Kind == TechnologyUnlockContentKind.BuildingUpgrade)
            {
                return string.IsNullOrWhiteSpace(content.ShortLabel) ? "升级" : content.ShortLabel;
            }

            if (content.Icon != null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(content.ShortLabel))
            {
                return content.ShortLabel;
            }

            return content.Kind switch
            {
                TechnologyUnlockContentKind.GlobalBuff => "BUFF",
                TechnologyUnlockContentKind.Building => "建筑",
                TechnologyUnlockContentKind.ItemReward => "奖励",
                _ => "效果"
            };
        }

        private void ResolveReferences()
        {
            button ??= GetComponent<Button>();
            backgroundImage ??= GetComponent<Image>();
        }

        private void HandleClicked()
        {
            clicked?.Invoke(definition);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }
    }

}
