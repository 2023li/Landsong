using System;
using Landsong.ExpeditionSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_ExpeditionSupplyInput : MonoBehaviour
    {
        [SerializeField, LabelText("图标")] private Image icon;
        [SerializeField, LabelText("名称文本")] private TMP_Text nameLabel;
        [SerializeField, LabelText("需求文本")] private TMP_Text requiredLabel;
        [SerializeField, LabelText("库存文本")] private TMP_Text availableLabel;
        [SerializeField, LabelText("加成文本")] private TMP_Text bonusLabel;
        [SerializeField, LabelText("数量输入")] private TMP_InputField amountInput;

        private ExpeditionSupplyOption option;
        private Action changed;

        private void Reset()
        {
            icon = GetComponentInChildren<Image>(true);
            amountInput = GetComponentInChildren<TMP_InputField>(true);
        }

        private void OnDestroy()
        {
            if (amountInput != null)
            {
                amountInput.onValueChanged.RemoveListener(HandleAmountChanged);
            }
        }

        public void Bind(ExpeditionSupplyOption sourceOption, InventoryService inventory, Action onChanged)
        {
            if (amountInput != null)
            {
                amountInput.onValueChanged.RemoveListener(HandleAmountChanged);
            }

            option = sourceOption;
            changed = onChanged;
            if (amountInput != null)
            {
                amountInput.SetTextWithoutNotify(option == null ? "0" : option.RequiredAmount.ToString());
            }

            Refresh(inventory);

            if (amountInput != null)
            {
                amountInput.onValueChanged.AddListener(HandleAmountChanged);
            }
        }

        public void Unbind()
        {
            if (amountInput != null)
            {
                amountInput.onValueChanged.RemoveListener(HandleAmountChanged);
                amountInput.SetTextWithoutNotify("0");
            }

            option = null;
            changed = null;
            SetIcon(null);
            SetText(nameLabel, string.Empty);
            SetText(requiredLabel, string.Empty);
            SetText(availableLabel, string.Empty);
            SetText(bonusLabel, string.Empty);
        }

        public int Amount => ParseAmount();
        public ExpeditionSupplyOption Option => option;

        public bool TryCreateItemAmount(out ItemAmount itemAmount)
        {
            itemAmount = default;
            if (option == null || option.ItemDefinition == null || Amount <= 0)
            {
                return false;
            }

            itemAmount = new ItemAmount(option.ItemDefinition, Amount);
            return itemAmount.IsValid;
        }

        public void Refresh(InventoryService inventory)
        {
            if (option == null)
            {
                Unbind();
                return;
            }

            SetIcon(option.Icon);
            SetText(nameLabel, option.DisplayName);
            SetText(requiredLabel, option.RequiredAmount > 0 ? $"最低 {option.RequiredAmount}" : "可选");

            var available = inventory == null || string.IsNullOrWhiteSpace(option.ItemId)
                ? 0
                : inventory.GetQuantity(option.ItemId);
            SetText(availableLabel, $"库存 {available}");

            var maxText = option.HasMaxAmount ? $"，最多 {option.MaxAmount}" : string.Empty;
            SetText(bonusLabel, $"+{option.SuccessChancePerItem * 100f:0.#}%/个{maxText}");

            if (amountInput != null && string.IsNullOrWhiteSpace(amountInput.text))
            {
                amountInput.SetTextWithoutNotify(option.RequiredAmount.ToString());
            }
        }

        private int ParseAmount()
        {
            if (amountInput == null || string.IsNullOrWhiteSpace(amountInput.text))
            {
                return 0;
            }

            return int.TryParse(amountInput.text.Trim(), out var amount)
                ? Mathf.Max(0, amount)
                : 0;
        }

        private void HandleAmountChanged(string _)
        {
            changed?.Invoke();
        }

        private void SetIcon(Sprite sprite)
        {
            if (icon == null)
            {
                return;
            }

            icon.sprite = sprite;
            icon.enabled = sprite != null;
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
