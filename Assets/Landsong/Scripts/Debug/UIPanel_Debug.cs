using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Landsong.InventorySystem;
using Moyo.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.DebugSystem
{
    public sealed class UIPanel_Debug : UIPanelBase
    {
        private const string GoldItemId = "金币";
        private const int GoldAmount = 9999;

        [Header("Controls")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button addGoldButton;
        [SerializeField] private Button previousItemButton;
        [SerializeField] private Button nextItemButton;
        [SerializeField] private Button addItemButton;
        [SerializeField] private Button addRandomQuestButton;
        [SerializeField] private TMP_InputField quantityInput;
        [SerializeField] private TMP_Text selectedItemLabel;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Defaults")]
        [SerializeField, Min(1)] private int defaultItemQuantity = 100;

        private readonly List<ItemDefinition> itemDefinitions = new();
        private int selectedItemIndex;
        private bool controlsBound;

        public override Task OnCreateAsync()
        {
            BindControls();
            EnsureDefaultQuantity();
            RefreshItemOptions();
            return Task.CompletedTask;
        }

        public override Task OnOpenAsync(object args)
        {
            BindControls();
            EnsureDefaultQuantity();
            RefreshItemOptions();
            SetStatus("F8 可关闭 Gameplay 调试面板。");
            LSDebugManager.Instance?.NotifyGameplayDebugPanelState(true);
            return Task.CompletedTask;
        }

        public override Task OnCloseAsync()
        {
            LSDebugManager.Instance?.NotifyGameplayDebugPanelState(false);
            return Task.CompletedTask;
        }

        public override Task OnReleaseAsync()
        {
            UnbindControls();
            LSDebugManager.Instance?.NotifyGameplayDebugPanelState(false);
            return Task.CompletedTask;
        }

        private void BindControls()
        {
            if (controlsBound)
            {
                return;
            }

            AddListener(closeButton, HandleCloseClicked);
            AddListener(addGoldButton, HandleAddGoldClicked);
            AddListener(previousItemButton, HandlePreviousItemClicked);
            AddListener(nextItemButton, HandleNextItemClicked);
            AddListener(addItemButton, HandleAddItemClicked);
            AddListener(addRandomQuestButton, HandleAddRandomQuestClicked);
            controlsBound = true;
        }

        private void UnbindControls()
        {
            if (!controlsBound)
            {
                return;
            }

            RemoveListener(closeButton, HandleCloseClicked);
            RemoveListener(addGoldButton, HandleAddGoldClicked);
            RemoveListener(previousItemButton, HandlePreviousItemClicked);
            RemoveListener(nextItemButton, HandleNextItemClicked);
            RemoveListener(addItemButton, HandleAddItemClicked);
            RemoveListener(addRandomQuestButton, HandleAddRandomQuestClicked);
            controlsBound = false;
        }

        private async void HandleCloseClicked()
        {
            try
            {
                if (Manager != null)
                {
                    await Manager.CloseAsync<UIPanel_Debug>();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void HandleAddGoldClicked()
        {
            if (!TryGetGameSystem(out var gameSystem))
            {
                return;
            }

            var inventory = gameSystem.Services.Inventory;
            var added = inventory == null ? 0 : inventory.AddItem(GoldItemId, GoldAmount);
            SetStatus(added == GoldAmount
                ? $"已添加金币 x{GoldAmount}。"
                : $"金币仅添加 x{added}/{GoldAmount}：库存容量不足或金币未配置。");
        }

        private void HandlePreviousItemClicked()
        {
            SelectRelativeItem(-1);
        }

        private void HandleNextItemClicked()
        {
            SelectRelativeItem(1);
        }

        private void HandleAddItemClicked()
        {
            if (!TryGetGameSystem(out var gameSystem) || itemDefinitions.Count == 0)
            {
                return;
            }

            if (!TryGetQuantity(out var amount))
            {
                SetStatus("物资数量必须是正整数。");
                return;
            }

            var definition = itemDefinitions[selectedItemIndex];
            var inventory = gameSystem.Services.Inventory;
            var added = inventory == null ? 0 : inventory.AddItem(definition.ItemId, amount);
            SetStatus(added == amount
                ? $"已添加 {definition.DisplayName} x{added}。"
                : $"{definition.DisplayName} 仅添加 x{added}/{amount}：库存容量不足。");
        }

        private void HandleAddRandomQuestClicked()
        {
            if (!TryGetGameSystem(out var gameSystem))
            {
                return;
            }

            var questService = gameSystem.Services.Quest;
            if (questService != null && questService.TryAddDebugRandomQuest(out var quest))
            {
                SetStatus($"已获取随机任务：{quest.Definition.DisplayName}。");
                return;
            }

            SetStatus("获取随机任务失败：没有可用物资，或已达到同时存在上限。");
        }

        private void RefreshItemOptions()
        {
            var previousItemId = itemDefinitions.Count == 0
                ? string.Empty
                : itemDefinitions[Mathf.Clamp(selectedItemIndex, 0, itemDefinitions.Count - 1)].ItemId;

            itemDefinitions.Clear();

            Landsong.GameSystem.TryGetInstance(out var gameSystem);
            var inventory = gameSystem == null ? null : gameSystem.Services.Inventory;
            var definitions = inventory == null || inventory.ItemCatalog == null
                ? null
                : inventory.ItemCatalog.Definitions;

            if (definitions != null)
            {
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition != null && !string.IsNullOrWhiteSpace(definition.ItemId))
                    {
                        itemDefinitions.Add(definition);
                    }
                }
            }

            selectedItemIndex = 0;
            if (!string.IsNullOrEmpty(previousItemId))
            {
                for (var i = 0; i < itemDefinitions.Count; i++)
                {
                    if (string.Equals(itemDefinitions[i].ItemId, previousItemId, StringComparison.Ordinal))
                    {
                        selectedItemIndex = i;
                        break;
                    }
                }
            }

            RefreshSelectedItemLabel();
        }

        private void SelectRelativeItem(int offset)
        {
            if (itemDefinitions.Count == 0)
            {
                RefreshItemOptions();
                return;
            }

            selectedItemIndex = (selectedItemIndex + offset + itemDefinitions.Count) % itemDefinitions.Count;
            RefreshSelectedItemLabel();
        }

        private void RefreshSelectedItemLabel()
        {
            var hasItems = itemDefinitions.Count > 0;
            if (selectedItemLabel != null)
            {
                selectedItemLabel.text = hasItems
                    ? $"{itemDefinitions[selectedItemIndex].DisplayName}  [{itemDefinitions[selectedItemIndex].ItemId}]  {selectedItemIndex + 1}/{itemDefinitions.Count}"
                    : "物品目录中没有有效物资";
            }

            SetInteractable(previousItemButton, hasItems);
            SetInteractable(nextItemButton, hasItems);
            SetInteractable(addItemButton, hasItems);
        }

        private void EnsureDefaultQuantity()
        {
            if (quantityInput != null && string.IsNullOrWhiteSpace(quantityInput.text))
            {
                quantityInput.text = Mathf.Max(1, defaultItemQuantity).ToString();
            }
        }

        private bool TryGetQuantity(out int amount)
        {
            amount = 0;
            return quantityInput != null
                   && int.TryParse(quantityInput.text, out amount)
                   && amount > 0;
        }

        private bool TryGetGameSystem(out Landsong.GameSystem gameSystem)
        {
            if (Landsong.GameSystem.TryGetInstance(out gameSystem))
            {
                return true;
            }

            SetStatus("当前场景没有 GameSystem。");
            return false;
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message ?? string.Empty;
            }
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction listener)
        {
            if (button != null)
            {
                button.onClick.AddListener(listener);
            }
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction listener)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(listener);
            }
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null)
            {
                selectable.interactable = interactable;
            }
        }
    }
}
