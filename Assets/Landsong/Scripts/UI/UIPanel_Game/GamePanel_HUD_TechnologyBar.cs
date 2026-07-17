using Landsong.TechnologySystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    /// <summary>
    /// 独立管理 HUD 科技条的解锁显隐、当前研究显示和科技面板入口。
    /// 根对象保持激活以持续监听功能解锁，视觉与交互通过 CanvasGroup 整体关闭。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GamePanel_HUD_TechnologyBar : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button technologyButton;
        [SerializeField] private Slider currentTechnologyProgress;
        [SerializeField] private Image currentTechnologyIcon;
        [SerializeField] private TMP_Text technologyProgressLabel;
        [SerializeField] private TMP_Text currentTechnologyNameLabel;

        private UIPanel_Game gamePanel;
        private TechnologyService technology;
        private GameFeatureUnlockService features;
        private bool subscribedToTechnology;
        private bool subscribedToFeatures;

        private void Awake()
        {
            gamePanel = GetComponentInParent<UIPanel_Game>();
            ValidateBindings();
            BindButton();
        }

        private void OnEnable()
        {
            ResolveRuntimeServices();
            SubscribeRuntimeServices();
            Refresh();
        }

        private void Start()
        {
            if (technology == null || features == null)
            {
                ResolveRuntimeServices();
            }

            SubscribeRuntimeServices();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeRuntimeServices();
        }

        private void OnDestroy()
        {
            UnsubscribeRuntimeServices();
            UnbindButton();
        }

        private void ResolveRuntimeServices()
        {
            var gameSystem = GameSystem.Instance;
            technology = gameSystem == null ? null : gameSystem.Services.Technology;
            features = gameSystem == null ? null : gameSystem.Services.Features;
        }

        private void SubscribeRuntimeServices()
        {
            if (!subscribedToTechnology && technology != null)
            {
                technology.StateChanged += HandleTechnologyChanged;
                subscribedToTechnology = true;
            }

            if (!subscribedToFeatures && features != null)
            {
                features.StateChanged += HandleFeatureAvailabilityChanged;
                subscribedToFeatures = true;
            }
        }

        private void UnsubscribeRuntimeServices()
        {
            if (subscribedToTechnology && technology != null)
            {
                technology.StateChanged -= HandleTechnologyChanged;
            }

            if (subscribedToFeatures && features != null)
            {
                features.StateChanged -= HandleFeatureAvailabilityChanged;
            }

            subscribedToTechnology = false;
            subscribedToFeatures = false;
        }

        private void HandleTechnologyChanged(TechnologyService changedTechnology)
        {
            technology = changedTechnology;
            Refresh();
        }

        private void HandleFeatureAvailabilityChanged(GameFeatureUnlockService changedFeatures)
        {
            features = changedFeatures;
            Refresh();
        }

        private void Refresh()
        {
            var technologyUnlocked = features != null && features.IsUnlocked(GameFeature.Technology);
            SetTechnologyBarVisible(technologyUnlocked);
            if (!technologyUnlocked)
            {
                ClearDisplay();
                return;
            }

            RefreshTechnology();
        }

        private void RefreshTechnology()
        {
            if (technology == null)
            {
                SetCurrentTechnologyIcon(null);
                SetSlider01(currentTechnologyProgress, 0f);
                SetText(currentTechnologyNameLabel, "科技未初始化");
                SetText(technologyProgressLabel, "0/0");
                return;
            }

            var definition = technology.CurrentResearchDefinition;
            if (definition == null)
            {
                SetCurrentTechnologyIcon(null);
                SetSlider01(currentTechnologyProgress, 0f);
                SetText(currentTechnologyNameLabel, FormatNoCurrentResearchName());
                SetText(technologyProgressLabel, technology.HasResearchQueue ? "等待中" : "0/0");
                return;
            }

            var required = Mathf.Max(0, technology.CurrentResearchRequiredPoints);
            var progress = Mathf.Max(0, technology.CurrentResearchProgress);
            var progress01 = required <= 0 ? 1f : Mathf.Clamp01(progress / (float)required);

            SetCurrentTechnologyIcon(definition);
            SetSlider01(currentTechnologyProgress, progress01);
            SetText(currentTechnologyNameLabel, definition.DisplayName);
            SetText(
                technologyProgressLabel,
                required <= 0 ? "无需科技点" : $"{Mathf.Min(progress, required)}/{required}");
        }

        private string FormatNoCurrentResearchName()
        {
            if (technology == null || !technology.HasResearchQueue)
            {
                return "未选择科技";
            }

            var queuedTechnologyIds = technology.ResearchQueueTechnologyIds;
            if (queuedTechnologyIds.Count <= 0
                || technology.Catalog == null
                || !technology.Catalog.TryGetDefinition(queuedTechnologyIds[0], out var queuedDefinition)
                || queuedDefinition == null)
            {
                return "等待研发队列";
            }

            return $"等待：{queuedDefinition.DisplayName}";
        }

        private void ClearDisplay()
        {
            SetCurrentTechnologyIcon(null);
            SetSlider01(currentTechnologyProgress, 0f);
            SetText(currentTechnologyNameLabel, string.Empty);
            SetText(technologyProgressLabel, string.Empty);
        }

        private void SetTechnologyBarVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void SetCurrentTechnologyIcon(TechnologyDefinition definition)
        {
            if (currentTechnologyIcon == null)
            {
                return;
            }

            currentTechnologyIcon.sprite = definition == null ? null : definition.Icon;
            currentTechnologyIcon.enabled = definition != null && definition.HasIcon;
        }

        private void BindButton()
        {
            if (gamePanel == null || technologyButton == null)
            {
                return;
            }

            technologyButton.onClick.RemoveListener(gamePanel.Show_Technology);
            technologyButton.onClick.AddListener(gamePanel.Show_Technology);
        }

        private void UnbindButton()
        {
            if (gamePanel != null && technologyButton != null)
            {
                technologyButton.onClick.RemoveListener(gamePanel.Show_Technology);
            }
        }

        private void ValidateBindings()
        {
            if (gamePanel != null
                && canvasGroup != null
                && technologyButton != null
                && currentTechnologyProgress != null
                && currentTechnologyIcon != null
                && technologyProgressLabel != null
                && currentTechnologyNameLabel != null)
            {
                return;
            }

            Debug.LogError(
                $"{nameof(GamePanel_HUD_TechnologyBar)} on '{name}' has missing required Prefab bindings.",
                this);
        }

        private static void SetSlider01(Slider slider, float value)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(value);
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
