using System;
using System.Collections.Generic;
using Landsong.TechnologySystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_Technology : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text pointsLabel;
        [SerializeField] private Transform nodeRoot;
        [SerializeField] private GamePanel_TechnologyNodeItem nodeItemPrefab;
        [SerializeField] private TMP_Text detailNameLabel;
        [SerializeField] private TMP_Text detailDescriptionLabel;
        [SerializeField] private TMP_Text detailCostLabel;
        [SerializeField] private TMP_Text detailPrerequisitesLabel;
        [SerializeField] private TMP_Text detailStatusLabel;
        [SerializeField] private Button unlockButton;

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
            RefreshPoints();
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

            if (unlockButton != null)
            {
                unlockButton.onClick.RemoveListener(HandleUnlockClicked);
                unlockButton.onClick.AddListener(HandleUnlockClicked);
            }
        }

        private void UnbindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }

            if (unlockButton != null)
            {
                unlockButton.onClick.RemoveListener(HandleUnlockClicked);
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

        private void RefreshPoints()
        {
            SetText(pointsLabel, technology == null ? "0" : technology.SciencePoints.ToString());
        }

        private void RefreshNodes()
        {
            var definitions = GetDefinitions();
            EnsureNodeItems(definitions.Count);

            for (var i = 0; i < nodeItems.Count; i++)
            {
                var item = nodeItems[i];
                var hasDefinition = i < definitions.Count;
                item.gameObject.SetActive(hasDefinition);

                if (!hasDefinition)
                {
                    continue;
                }

                var definition = definitions[i];
                if (selectedDefinition == null)
                {
                    selectedDefinition = definition;
                }

                item.Bind(definition, technology, HandleNodeClicked, definition == selectedDefinition);
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
                SetUnlockButtonInteractable(false);
                return;
            }

            SetText(detailNameLabel, selectedDefinition.DisplayName);
            SetText(detailDescriptionLabel, selectedDefinition.Description);
            SetText(detailCostLabel, $"消耗：{selectedDefinition.SciencePointCost} 科技点");
            SetText(detailPrerequisitesLabel, FormatPrerequisites(selectedDefinition));
            SetText(detailStatusLabel, FormatSelectedStatus());
            SetUnlockButtonInteractable(technology != null && technology.CanUnlock(selectedDefinition, out _));
        }

        private IReadOnlyList<TechnologyDefinition> GetDefinitions()
        {
            if (technology != null && technology.Catalog != null)
            {
                return technology.Catalog.Definitions;
            }

            var gameSystem = Landsong.GameSystem.Instance;
            return gameSystem == null || gameSystem.TechnologyCatalog == null
                ? Array.Empty<TechnologyDefinition>()
                : gameSystem.TechnologyCatalog.Definitions;
        }

        private void EnsureNodeItems(int requiredCount)
        {
            CollectExistingNodeItems();

            while (nodeItems.Count < requiredCount && nodeItemPrefab != null && nodeRoot != null)
            {
                var item = Instantiate(nodeItemPrefab, nodeRoot);
                nodeItems.Add(item);
            }
        }

        private void CollectExistingNodeItems()
        {
            if (nodeRoot == null)
            {
                return;
            }

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

            if (technology.IsUnlocked(selectedDefinition.TechnologyId))
            {
                return "已解锁";
            }

            if (technology.CanUnlock(selectedDefinition, out var reason))
            {
                return "可解锁";
            }

            return reason switch
            {
                TechnologyUnlockFailureReason.PrerequisitesLocked => "前置科技未完成",
                TechnologyUnlockFailureReason.InsufficientPoints => "科技点不足",
                TechnologyUnlockFailureReason.AlreadyUnlocked => "已解锁",
                TechnologyUnlockFailureReason.InvalidTechnology => "科技配置无效",
                _ => "不可解锁"
            };
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

        private void SetUnlockButtonInteractable(bool interactable)
        {
            if (unlockButton != null)
            {
                unlockButton.interactable = interactable;
            }
        }

        private void HandleNodeClicked(TechnologyDefinition definition)
        {
            selectedDefinition = definition;
            Refresh();
        }

        private void HandleUnlockClicked()
        {
            if (technology == null || selectedDefinition == null)
            {
                RefreshDetail();
                return;
            }

            technology.TryUnlock(selectedDefinition);
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
