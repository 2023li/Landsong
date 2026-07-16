using System;
using System.Collections.Generic;
using Landsong.TechnologySystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        [SerializeField] private GameObject unlockContentItemPrefab;
        [SerializeField] private GameObject unlockTooltipRoot;
        [SerializeField] private TMP_Text unlockTooltipLabel;
        [SerializeField, Min(0)] private int maxUnlockIcons = 5;
        [SerializeField, Min(0f)] private float unlockIconSpacing = 6f;

        [Header("状态颜色")]
        [SerializeField] private Color invalidColor = new Color(0.18f, 0.18f, 0.18f, 0.8f);
        [SerializeField] private Color previewColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.24f, 0.24f, 0.24f, 1f);
        [SerializeField] private Color availableColor = new Color(0.32f, 0.48f, 0.25f, 1f);
        [SerializeField] private Color currentResearchColor = new Color(0.25f, 0.48f, 0.7f, 1f);
        [SerializeField] private Color queuedColor = new Color(0.55f, 0.42f, 0.2f, 1f);
        [SerializeField] private Color completedColor = new Color(0.62f, 0.52f, 0.25f, 1f);
        [SerializeField] private Color repeatableColor = new Color(0.46f, 0.32f, 0.58f, 1f);

        private readonly List<TechnologyUnlockIconHover> spawnedUnlockIcons =
            new List<TechnologyUnlockIconHover>();
        private readonly List<TechnologyUnlockContent> unlockContents = new List<TechnologyUnlockContent>();
        private TechnologyDefinition unlockIconsDefinition;
        private TechnologyUnlockContentRegistry unlockContentRegistry;
        private TechnologyUnlockContentRegistry unlockIconsContentRegistry;
        private TechnologyUnlockIconHover unlockContentItemPrefabView;
        private int unlockIconsContentVersion = -1;
        private TechnologyService technology;
        private Action<TechnologyDefinition> clicked;

        public TechnologyDefinition Definition => definition;
        public TechnologyNodeVisualState VisualState { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            if (!ValidateUnlockPresentationConfiguration(true))
            {
                enabled = false;
                return;
            }

            unlockTooltipRoot.SetActive(false);
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
#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                ValidateUnlockPresentationConfiguration(true);
            }
#endif
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
            if (!ValidateUnlockPresentationConfiguration(false))
            {
                return;
            }

            var contentVersion = unlockContentRegistry == null ? -1 : unlockContentRegistry.Version;
            if (Application.isPlaying
                && unlockIconsDefinition == definition
                && unlockIconsContentRegistry == unlockContentRegistry
                && unlockIconsContentVersion == contentVersion)
            {
                return;
            }

            ClearUnlockIcons();
            if (definition == null || maxUnlockIcons <= 0)
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
                if (CreateUnlockIcon(unlockContents[i], spawnedUnlockIcons.Count, out var icon))
                {
                    spawnedUnlockIcons.Add(icon);
                }
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
                if (CreateUnlockIcon(overflow, spawnedUnlockIcons.Count, out var overflowIcon))
                {
                    spawnedUnlockIcons.Add(overflowIcon);
                }
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
            // 旧版本会把运行时生成的解锁项误保存进面板 Prefab。
            // 这里以专用容器为边界清理全部子对象，确保迁移后不会重复显示。
            if (unlockIconRoot != null)
            {
                for (var i = unlockIconRoot.childCount - 1; i >= 0; i--)
                {
                    var child = unlockIconRoot.GetChild(i);
                    if (child == null)
                    {
                        continue;
                    }

                    if (Application.isPlaying)
                    {
                        child.SetParent(null, false);
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
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

        private bool CreateUnlockIcon(
            TechnologyUnlockContent content,
            int index,
            out TechnologyUnlockIconHover instance)
        {
            instance = Instantiate(unlockContentItemPrefabView, unlockIconRoot, false);
            if (instance == null)
            {
                Debug.LogError("科技解锁内容项 Prefab 实例化失败。", this);
                return false;
            }

            var rect = instance.RectTransform;
            var itemWidth = Mathf.Max(0f, rect.rect.width);
            rect.anchoredPosition = new Vector2(
                index * (itemWidth + unlockIconSpacing),
                rect.anchoredPosition.y);

            if (instance.Bind(
                    content,
                    ResolveBadgeText(content),
                    () => ShowUnlockTooltip(content),
                    HideUnlockTooltip))
            {
                return true;
            }

            if (Application.isPlaying)
            {
                Destroy(instance.gameObject);
            }
            else
            {
                DestroyImmediate(instance.gameObject);
            }

            instance = null;
            return false;
        }

        private void ShowUnlockTooltip(TechnologyUnlockContent content)
        {
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

        private bool ValidateUnlockPresentationConfiguration(bool logError)
        {
            unlockContentItemPrefabView = null;
            string error = null;
            if (unlockIconRoot == null)
            {
                error = "未引用解锁内容根节点。";
            }
            else if (unlockContentItemPrefab == null)
            {
                error = "未引用科技解锁内容项 Prefab。";
            }
            else if (!unlockContentItemPrefab.TryGetComponent(out unlockContentItemPrefabView))
            {
                error = "科技解锁内容项 Prefab 缺少 TechnologyUnlockIconHover 组件。";
            }
            else if (!unlockContentItemPrefabView.IsConfigurationValid(out var itemError))
            {
                error = $"科技解锁内容项 Prefab 配置错误：{itemError}";
            }
            else if (unlockTooltipRoot == null)
            {
                error = "未引用解锁内容提示根节点。";
            }
            else if (unlockTooltipLabel == null)
            {
                error = "未引用解锁内容提示文本。";
            }
            else if (!unlockTooltipLabel.transform.IsChildOf(unlockTooltipRoot.transform))
            {
                error = "解锁内容提示文本必须位于提示根节点之下。";
            }
#if UNITY_EDITOR
            else if (!PrefabUtility.IsPartOfPrefabAsset(unlockContentItemPrefab))
            {
                error = "科技解锁内容项引用必须是 Prefab 资产，不能是场景对象。";
            }
#endif

            if (string.IsNullOrEmpty(error))
            {
                return true;
            }

            if (logError)
            {
                Debug.LogError($"科技节点解锁表现配置错误：{error}", this);
            }

            return false;
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
