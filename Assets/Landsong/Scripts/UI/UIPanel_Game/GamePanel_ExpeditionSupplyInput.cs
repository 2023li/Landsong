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
            SetText(requiredLabel, option.RequiredAmount > 0
                ? Landsong.Localization.L10n.Gameplay("gameplay.expedition.supply.minimum", "最低 {0}", option.RequiredAmount)
                : Landsong.Localization.L10n.Gameplay("gameplay.common.optional", "可选"));

            availableAmount = inventory == null || string.IsNullOrWhiteSpace(option.ItemId)
                ? 0
                : inventory.GetQuantity(option.ItemId);
            SetText(availableLabel, Landsong.Localization.L10n.Gameplay("gameplay.expedition.supply.inventory", "库存 {0}", availableAmount));

            RefreshSliderRange();
            RefreshAmountText();

            var successText = option.SuccessChancePerItem > 0f
                ? Landsong.Localization.L10n.Gameplay("gameplay.expedition.supply.success_per_extra", "+{0:0.#}%成功/额外", option.SuccessChancePerItem * 100f)
                : string.Empty;
            var rewardText = option.RewardYieldBonusPerItem > 0f
                ? Landsong.Localization.L10n.Gameplay("gameplay.expedition.supply.yield_per_extra", "+{0:0.#}%收益/额外", option.RewardYieldBonusPerItem * 100f)
                : string.Empty;
            var separator = !string.IsNullOrWhiteSpace(successText) && !string.IsNullOrWhiteSpace(rewardText)
                ? "，"
                : string.Empty;
            SetText(
                bonusLabel,
                Landsong.Localization.L10n.Gameplay(
                    "gameplay.expedition.supply.extra_limit",
                    "额外最多 {0}{1}{2}{3}{4}",
                    option.ExtraAmountLimit,
                    string.IsNullOrWhiteSpace(successText + rewardText) ? string.Empty : Landsong.Localization.L10n.Gameplay("gameplay.common.comma", "，"),
                    successText,
                    separator,
                    rewardText));
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
            SetText(amountLabel, extra > 0
                ? Landsong.Localization.L10n.Gameplay("gameplay.expedition.supply.amount_extra", "携带 {0}（额外 {1}）", amount, extra)
                : Landsong.Localization.L10n.Gameplay("gameplay.expedition.supply.amount", "携带 {0}", amount));
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
