using System.Collections.Generic;
using Landsong.TechnologySystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_Technology : MonoBehaviour
    {
        [SerializeField, LabelText("关闭按钮")]
        [Tooltip("点击后关闭科技面板并返回 HUD。")]
        private Button closeButton;

        [SerializeField, LabelText("科技节点根节点")]
        [Tooltip("手动摆放的 GamePanel_TechnologyNodeItem 会从这个节点下扫描。为空时扫描当前面板自身。")]
        private Transform nodeRoot;

        [SerializeField, LabelText("详情-科技名称文本")]
        [Tooltip("显示当前选中科技节点的名称。")]
        private TMP_Text detailNameLabel;

        [SerializeField, LabelText("详情-科技描述文本")]
        [Tooltip("显示当前选中科技节点的描述。")]
        private TMP_Text detailDescriptionLabel;

        [SerializeField, LabelText("详情-研究需求文本")]
        [Tooltip("显示当前选中科技节点完成研究需要累计注入的科技点。")]
        private TMP_Text detailCostLabel;

        [SerializeField, LabelText("详情-前置科技文本")]
        [Tooltip("显示当前选中科技节点依赖的前置科技列表。")]
        private TMP_Text detailPrerequisitesLabel;

        [SerializeField, LabelText("详情-研究状态文本")]
        [Tooltip("显示当前选中科技节点是否可研究、研究中、已研究或前置科技未完成。")]
        private TMP_Text detailStatusLabel;

        private readonly List<GamePanel_TechnologyNodeItem> nodeItems = new List<GamePanel_TechnologyNodeItem>();
        private UIPanel_Game gamePanel;
        private TechnologyService technology;
        private TechnologyDefinition selectedDefinition;
        private bool subscribedToTechnology;

        private void Reset()
        {
            nodeRoot = transform;
        }

        private void Awake()
        {
            ResolveReferences();
            BindButtons();
        }

        private void OnEnable()
        {
            ResolveRuntime();
            SubscribeTechnology();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeTechnology();
        }

        private void OnDestroy()
        {
            UnbindButtons();
            UnsubscribeTechnology();
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            ResolveRuntime();
            RefreshNodes();
            RefreshDetail();
        }

        private void ResolveReferences()
        {
            if (gamePanel == null)
            {
                gamePanel = GetComponentInParent<UIPanel_Game>();
            }

            if (nodeRoot == null)
            {
                nodeRoot = transform;
            }
        }

        private void ResolveRuntime()
        {
            var gameSystem = Landsong.GameSystem.Instance;
            technology = gameSystem == null ? null : gameSystem.Technology;
        }

        private void BindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
                closeButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        private void UnbindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
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

        private void RefreshNodes()
        {
            CollectExistingNodeItems();

            var selectedNodeExists = false;
            TechnologyDefinition firstDefinition = null;
            for (var i = 0; i < nodeItems.Count; i++)
            {
                var item = nodeItems[i];
                var definition = item == null ? null : item.Definition;

                if (definition == null)
                {
                    continue;
                }

                firstDefinition ??= definition;
                if (definition == selectedDefinition)
                {
                    selectedNodeExists = true;
                }
            }

            if (!selectedNodeExists)
            {
                selectedDefinition = firstDefinition;
            }

            for (var i = 0; i < nodeItems.Count; i++)
            {
                var item = nodeItems[i];
                if (item != null)
                {
                    item.Bind(technology, HandleNodeClicked, item.Definition != null && item.Definition == selectedDefinition);
                }
            }
        }

        private void RefreshDetail()
        {
            if (selectedDefinition == null)
            {
                SetText(detailNameLabel, "未选择科技");
                SetText(detailDescriptionLabel, string.Empty);
                SetText(detailCostLabel, string.Empty);
                SetText(detailPrerequisitesLabel, string.Empty);
                SetText(detailStatusLabel, technology == null ? "科技服务未初始化" : "没有可显示的科技节点");
                return;
            }

            SetText(detailNameLabel, selectedDefinition.DisplayName);
            SetText(detailDescriptionLabel, selectedDefinition.Description);
            SetText(detailCostLabel, $"研究需求：{selectedDefinition.SciencePointCost} 科技点");
            SetText(detailPrerequisitesLabel, FormatPrerequisites(selectedDefinition));
            SetText(detailStatusLabel, FormatSelectedStatus());
        }

        private void CollectExistingNodeItems()
        {
            if (nodeRoot == null)
            {
                return;
            }

            nodeItems.Clear();
            var existingItems = nodeRoot.GetComponentsInChildren<GamePanel_TechnologyNodeItem>(true);
            for (var i = 0; i < existingItems.Length; i++)
            {
                var item = existingItems[i];
                if (item != null && !nodeItems.Contains(item))
                {
                    nodeItems.Add(item);
                }
            }
        }

        private string FormatSelectedStatus()
        {
            if (technology == null)
            {
                return "科技服务未初始化";
            }

            if (technology.IsCurrentResearch(selectedDefinition))
            {
                return selectedDefinition.AllowRepeatResearch && technology.IsUnlocked(selectedDefinition.TechnologyId)
                    ? $"重复研究中：{FormatResearchProgress(selectedDefinition)}"
                    : $"研究中：{FormatResearchProgress(selectedDefinition)}";
            }

            if (technology.IsQueuedResearch(selectedDefinition))
            {
                return "已加入研发队列";
            }

            if (technology.IsUnlocked(selectedDefinition.TechnologyId) && !selectedDefinition.AllowRepeatResearch)
            {
                return "已研究";
            }

            if (technology.CanStartResearch(selectedDefinition, out var reason))
            {
                if (selectedDefinition.AllowRepeatResearch && technology.IsUnlocked(selectedDefinition.TechnologyId))
                {
                    return technology.HasCurrentResearch
                        ? "可重复研究，点击节点切换当前研究"
                        : "可重复研究，点击节点开始研究";
                }

                return technology.HasCurrentResearch
                    ? "可研究，点击节点切换当前研究"
                    : "可研究，点击节点开始研究";
            }

            return reason switch
            {
                TechnologyResearchFailureReason.PrerequisitesLocked => "前置科技未完成，点击节点加入研发队列",
                TechnologyResearchFailureReason.AlreadyUnlocked => "已研究",
                TechnologyResearchFailureReason.InvalidTechnology => "科技配置无效",
                _ => "不可研究"
            };
        }

        private string FormatResearchProgress(TechnologyDefinition definition)
        {
            if (definition == null)
            {
                return "0/0";
            }

            var progress = technology == null ? 0 : technology.GetResearchProgress(definition);
            var required = Mathf.Max(0, definition.SciencePointCost);
            return required <= 0 ? "无需科技点" : $"{progress}/{required}";
        }

        private static string FormatPrerequisites(TechnologyDefinition definition)
        {
            var prerequisites = definition.Prerequisites;
            if (prerequisites.Count == 0)
            {
                return "前置：无";
            }

            var names = new List<string>(prerequisites.Count);
            for (var i = 0; i < prerequisites.Count; i++)
            {
                var prerequisite = prerequisites[i];
                if (prerequisite != null)
                {
                    names.Add(prerequisite.DisplayName);
                }
            }

            return names.Count == 0 ? "前置：无" : $"前置：{string.Join("、", names)}";
        }

        private void HandleNodeClicked(TechnologyDefinition definition)
        {
            selectedDefinition = definition;

            if (technology != null
                && selectedDefinition != null)
            {
                technology.TryQueueResearchPath(selectedDefinition);
            }

            Refresh();
        }

        private void HandleTechnologyChanged(TechnologyService changedTechnology)
        {
            technology = changedTechnology;
            Refresh();
        }

        private void HandleCloseClicked()
        {
            if (gamePanel != null)
            {
                gamePanel.Hide_Technology();
                return;
            }

            Hide();
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
