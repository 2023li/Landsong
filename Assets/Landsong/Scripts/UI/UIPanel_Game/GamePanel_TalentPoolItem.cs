using System;
using Landsong.TalentSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_TalentPoolItem : MonoBehaviour
    {
        [SerializeField, LabelText("选择按钮")] private Button selectButton;
        [SerializeField, LabelText("升级按钮")] private Button upgradeButton;
        [SerializeField, LabelText("升级按钮文本")] private TMP_Text upgradeButtonLabel;
        [SerializeField, LabelText("卸任按钮")] private Button unassignButton;
        [SerializeField, LabelText("卸任按钮文本")] private TMP_Text unassignButtonLabel;
        [SerializeField, LabelText("选中状态根节点")] private GameObject selectedRoot;
        [SerializeField, LabelText("卡片背景")] private Image cardBackground;
        [SerializeField, LabelText("稀有度标记")] private Image rarityMarker;
        [SerializeField, LabelText("头像")] private Image icon;
        [SerializeField, LabelText("职业图标")] private Image professionIcon;
        [SerializeField, LabelText("名称")] private TMP_Text titleLabel;
        [SerializeField, LabelText("职业/稀有度")] private TMP_Text metaLabel;
        [SerializeField, LabelText("等级经验")] private TMP_Text levelLabel;
        [SerializeField, LabelText("薪资")] private TMP_Text salaryLabel;
        [SerializeField, LabelText("任命状态")] private TMP_Text assignmentLabel;
        [SerializeField, LabelText("效果")] private TMP_Text effectsLabel;
        [SerializeField, LabelText("隐藏特性")] private TMP_Text hiddenTraitLabel;

        private TalentState talent;
        private Action<TalentState> selected;
        private Action<TalentState> upgradeClicked;
        private Action<TalentState> unassignClicked;

        private void Reset()
        {
            selectButton = GetComponent<Button>();
            icon = GetComponentInChildren<Image>(true);
        }

        private void OnDestroy()
        {
            UnregisterButtons();
        }

        public void Bind(
            TalentState sourceTalent,
            bool isSelected,
            string assignmentText,
            Action<TalentState> onSelected,
            Action<TalentState> onUpgradeClicked,
            Action<TalentState> onUnassignClicked)
        {
            UnregisterButtons();
            talent = sourceTalent;
            selected = onSelected;
            upgradeClicked = onUpgradeClicked;
            unassignClicked = onUnassignClicked;
            Refresh(isSelected, assignmentText);
            RegisterButtons();
        }

        public void Unbind()
        {
            UnregisterButtons();
            talent = null;
            selected = null;
            upgradeClicked = null;
            unassignClicked = null;
            SetActive(selectedRoot, false);
            SetText(titleLabel, string.Empty);
            SetText(metaLabel, string.Empty);
            SetText(levelLabel, string.Empty);
            SetText(salaryLabel, string.Empty);
            SetText(assignmentLabel, string.Empty);
            SetText(effectsLabel, string.Empty);
            SetText(hiddenTraitLabel, string.Empty);
            SetText(upgradeButtonLabel, string.Empty);
            SetText(unassignButtonLabel, string.Empty);
            SetImage(icon, null);
            SetImage(professionIcon, null);
        }

        private void Refresh(bool isSelected, string assignmentText)
        {
            var definition = talent == null ? null : talent.Definition;
            if (definition == null)
            {
                Unbind();
                return;
            }

            SetActive(selectedRoot, isSelected);
            SetText(titleLabel, talent.DisplayName);
            SetText(metaLabel, $"{GamePanel_TalentText.FormatProfession(talent.Profession)} / {GamePanel_TalentText.FormatRarity(talent.Rarity)}");
            SetText(levelLabel, GamePanel_TalentText.FormatLevel(talent));
            SetText(salaryLabel, GamePanel_TalentText.FormatSalary(talent));
            SetText(assignmentLabel, string.IsNullOrWhiteSpace(assignmentText)
                ? Landsong.Localization.L10n.Gameplay("gameplay.talent.ui.unassigned", "未任命")
                : assignmentText);
            SetText(effectsLabel, GamePanel_TalentText.FormatEffects(talent));
            SetText(hiddenTraitLabel, GamePanel_TalentText.FormatHiddenTraits(talent));
            SetText(upgradeButtonLabel, talent.IsMaxLevel
                ? Landsong.Localization.L10n.Gameplay("gameplay.talent.ui.max_level", "已满级")
                : Landsong.Localization.L10n.Gameplay("gameplay.common.upgrade", "升级"));
            SetText(unassignButtonLabel, Landsong.Localization.L10n.Gameplay("gameplay.talent.ui.unassign", "卸任"));
            SetImage(icon, definition.Icon);
            SetImage(professionIcon, definition.ProfessionIcon);
            SetColor(cardBackground, definition.CardMainColor);
            SetColor(rarityMarker, definition.RarityColor);

            if (selectButton != null)
            {
                selectButton.interactable = true;
            }

            if (upgradeButton != null)
            {
                upgradeButton.interactable = talent.CanUpgrade;
            }

            if (unassignButton != null)
            {
                unassignButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(assignmentText));
                unassignButton.interactable = !string.IsNullOrWhiteSpace(assignmentText);
            }
        }

        private void RegisterButtons()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(HandleSelected);
            }

            if (upgradeButton != null)
            {
                upgradeButton.onClick.AddListener(HandleUpgradeClicked);
            }

            if (unassignButton != null)
            {
                unassignButton.onClick.AddListener(HandleUnassignClicked);
            }
        }

        private void UnregisterButtons()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelected);
            }

            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
            }

            if (unassignButton != null)
            {
                unassignButton.onClick.RemoveListener(HandleUnassignClicked);
            }
        }

        private void HandleSelected()
        {
            selected?.Invoke(talent);
        }

        private void HandleUpgradeClicked()
        {
            upgradeClicked?.Invoke(talent);
        }

        private void HandleUnassignClicked()
        {
            unassignClicked?.Invoke(talent);
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

        private static void SetImage(Image target, Sprite sprite)
        {
            if (target == null)
            {
                return;
            }

            target.sprite = sprite;
            target.enabled = sprite != null;
        }

        private static void SetColor(Graphic target, Color color)
        {
            if (target != null)
            {
                target.color = color;
            }
        }
    }
}
