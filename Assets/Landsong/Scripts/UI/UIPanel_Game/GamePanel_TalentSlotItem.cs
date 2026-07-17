using System;
using Landsong.TalentSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_TalentSlotItem : MonoBehaviour
    {
        [SerializeField, LabelText("任命按钮")] private Button assignButton;
        [SerializeField, LabelText("任命按钮文本")] private TMP_Text assignButtonLabel;
        [SerializeField, LabelText("清空按钮")] private Button clearButton;
        [SerializeField, LabelText("清空按钮文本")] private TMP_Text clearButtonLabel;
        [SerializeField, LabelText("名称")] private TMP_Text titleLabel;
        [SerializeField, LabelText("职业限制")] private TMP_Text restrictionLabel;
        [SerializeField, LabelText("任命人才")] private TMP_Text assignedTalentLabel;
        [SerializeField, LabelText("状态")] private TMP_Text statusLabel;
        [SerializeField, LabelText("选中可任命状态")] private GameObject assignableRoot;

        private TalentSlotRuntimeState slot;
        private TalentState selectedTalent;
        private Action<TalentSlotRuntimeState> assignClicked;
        private Action<TalentSlotRuntimeState> clearClicked;

        private void Reset()
        {
            assignButton = GetComponentInChildren<Button>(true);
        }

        private void OnDestroy()
        {
            UnregisterButtons();
        }

        public void Bind(
            TalentSlotRuntimeState sourceSlot,
            TalentState currentSelectedTalent,
            Action<TalentSlotRuntimeState> onAssignClicked,
            Action<TalentSlotRuntimeState> onClearClicked)
        {
            UnregisterButtons();
            slot = sourceSlot;
            selectedTalent = currentSelectedTalent;
            assignClicked = onAssignClicked;
            clearClicked = onClearClicked;
            Refresh();
            RegisterButtons();
        }

        public void Unbind()
        {
            UnregisterButtons();
            slot = null;
            selectedTalent = null;
            assignClicked = null;
            clearClicked = null;
            SetText(titleLabel, string.Empty);
            SetText(restrictionLabel, string.Empty);
            SetText(assignedTalentLabel, string.Empty);
            SetText(statusLabel, string.Empty);
            SetText(assignButtonLabel, string.Empty);
            SetText(clearButtonLabel, string.Empty);
            SetActive(assignableRoot, false);
        }

        private void Refresh()
        {
            if (slot == null)
            {
                Unbind();
                return;
            }

            var canAssignSelected = selectedTalent != null && slot.Accepts(selectedTalent);
            SetText(titleLabel, slot.DisplayName);
            SetText(restrictionLabel, GamePanel_TalentText.FormatSlotRestriction(slot));
            SetText(assignedTalentLabel, slot.AssignedTalent == null
                ? Landsong.Localization.L10n.Gameplay("gameplay.talent.ui.empty_slot", "空槽")
                : slot.AssignedTalent.DisplayName);
            SetText(statusLabel, BuildStatusText(canAssignSelected));
            SetText(assignButtonLabel, selectedTalent == null
                ? Landsong.Localization.L10n.Gameplay("gameplay.talent.ui.select_talent", "选择人才")
                : Landsong.Localization.L10n.Gameplay("gameplay.talent.ui.assign", "任命"));
            SetText(clearButtonLabel, Landsong.Localization.L10n.Gameplay("gameplay.talent.ui.clear", "清空"));
            SetActive(assignableRoot, canAssignSelected);

            if (assignButton != null)
            {
                assignButton.interactable = canAssignSelected;
            }

            if (clearButton != null)
            {
                clearButton.gameObject.SetActive(slot.AssignedTalent != null);
                clearButton.interactable = slot.AssignedTalent != null;
            }
        }

        private string BuildStatusText(bool canAssignSelected)
        {
            if (selectedTalent == null)
            {
                return Landsong.Localization.L10n.Gameplay("gameplay.talent.ui.select_from_pool", "先从人才池选择人才");
            }

            return canAssignSelected
                ? Landsong.Localization.L10n.Gameplay("gameplay.talent.ui.can_assign", "可任命：{0}", selectedTalent.DisplayName)
                : Landsong.Localization.L10n.Gameplay("gameplay.talent.ui.cannot_assign", "不可任命：{0} 职业不匹配", selectedTalent.DisplayName);
        }

        private void RegisterButtons()
        {
            if (assignButton != null)
            {
                assignButton.onClick.AddListener(HandleAssignClicked);
            }

            if (clearButton != null)
            {
                clearButton.onClick.AddListener(HandleClearClicked);
            }
        }

        private void UnregisterButtons()
        {
            if (assignButton != null)
            {
                assignButton.onClick.RemoveListener(HandleAssignClicked);
            }

            if (clearButton != null)
            {
                clearButton.onClick.RemoveListener(HandleClearClicked);
            }
        }

        private void HandleAssignClicked()
        {
            assignClicked?.Invoke(slot);
        }

        private void HandleClearClicked()
        {
            clearClicked?.Invoke(slot);
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
