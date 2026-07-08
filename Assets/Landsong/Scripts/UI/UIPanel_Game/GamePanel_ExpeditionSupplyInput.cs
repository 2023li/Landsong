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
        [SerializeField, LabelText("数量文本")] private TMP_Text amountLabel;
        [SerializeField, LabelText("数量滑动条")] private Slider amountSlider;

        private ExpeditionSupplyOption option;
        private Action changed;
        private int availableAmount;

        private void Reset()
        {
            icon = GetComponentInChildren<Image>(true);
            amountSlider = GetComponentInChildren<Slider>(true);
        }

        private void OnDestroy()
        {
            if (amountSlider != null)
            {
                amountSlider.onValueChanged.RemoveListener(HandleAmountChanged);
            }
        }

        public void Bind(ExpeditionSupplyOption sourceOption, InventoryService inventory, Action onChanged)
        {
            if (amountSlider != null)
            {
                amountSlider.onValueChanged.RemoveListener(HandleAmountChanged);
            }

            option = sourceOption;
            changed = onChanged;
            Refresh(inventory);

            if (amountSlider != null)
            {
                amountSlider.onValueChanged.AddListener(HandleAmountChanged);
            }
        }

        public void Unbind()
        {
            if (amountSlider != null)
            {
                amountSlider.onValueChanged.RemoveListener(HandleAmountChanged);
                amountSlider.SetValueWithoutNotify(0f);
            }

            option = null;
            changed = null;
            availableAmount = 0;
            SetIcon(null);
            SetText(nameLabel, string.Empty);
            SetText(requiredLabel, string.Empty);
            SetText(availableLabel, string.Empty);
            SetText(bonusLabel, string.Empty);
            SetText(amountLabel, string.Empty);
        }

        public int Amount => amountSlider == null ? 0 : Mathf.RoundToInt(amountSlider.value);
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

            availableAmount = inventory == null || string.IsNullOrWhiteSpace(option.ItemId)
                ? 0
                : inventory.GetQuantity(option.ItemId);
            SetText(availableLabel, $"库存 {availableAmount}");

            RefreshSliderRange();
            RefreshAmountText();

            var successText = option.SuccessChancePerItem > 0f
                ? $"+{option.SuccessChancePerItem * 100f:0.#}%成功/额外"
                : string.Empty;
            var rewardText = option.RewardYieldBonusPerItem > 0f
                ? $"+{option.RewardYieldBonusPerItem * 100f:0.#}%收益/额外"
                : string.Empty;
            var separator = !string.IsNullOrWhiteSpace(successText) && !string.IsNullOrWhiteSpace(rewardText)
                ? "，"
                : string.Empty;
            SetText(
                bonusLabel,
                $"额外最多 {option.ExtraAmountLimit}{(string.IsNullOrWhiteSpace(successText + rewardText) ? string.Empty : "，")}{successText}{separator}{rewardText}");
        }

        private void RefreshSliderRange()
        {
            if (amountSlider == null || option == null)
            {
                return;
            }

            var min = option.RequiredAmount;
            var max = Mathf.Max(min, option.MaxAssignedAmount);
            if (availableAmount > 0)
            {
                max = Mathf.Min(max, Mathf.Max(min, availableAmount));
            }
            else
            {
                max = min;
            }

            amountSlider.wholeNumbers = true;
            amountSlider.minValue = min;
            amountSlider.maxValue = max;
            amountSlider.SetValueWithoutNotify(Mathf.Clamp(Mathf.RoundToInt(amountSlider.value), min, max));
        }

        private void RefreshAmountText()
        {
            if (option == null)
            {
                SetText(amountLabel, string.Empty);
                return;
            }

            var amount = Amount;
            var extra = option.GetExtraAssignedAmount(amount);
            SetText(amountLabel, extra > 0 ? $"携带 {amount}（额外 {extra}）" : $"携带 {amount}");
        }

        private void HandleAmountChanged(float _)
        {
            RefreshAmountText();
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
