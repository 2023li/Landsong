using System;
using Landsong.TalentSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_TalentOfferItem : MonoBehaviour
    {
        [SerializeField, LabelText("招募按钮")] private Button recruitButton;
        [SerializeField, LabelText("招募按钮文本")] private TMP_Text recruitButtonLabel;
        [SerializeField, LabelText("卡片背景")] private Image cardBackground;
        [SerializeField, LabelText("稀有度标记")] private Image rarityMarker;
        [SerializeField, LabelText("头像")] private Image icon;
        [SerializeField, LabelText("职业图标")] private Image professionIcon;
        [SerializeField, LabelText("名称")] private TMP_Text titleLabel;
        [SerializeField, LabelText("职业/稀有度")] private TMP_Text metaLabel;
        [SerializeField, LabelText("薪资")] private TMP_Text salaryLabel;
        [SerializeField, LabelText("效果")] private TMP_Text effectsLabel;
        [SerializeField, LabelText("隐藏特性")] private TMP_Text hiddenTraitLabel;

        private TalentOfferState offer;
        private Action<TalentOfferState> recruitClicked;

        private void Reset()
        {
            recruitButton = GetComponentInChildren<Button>(true);
            icon = GetComponentInChildren<Image>(true);
        }

        private void OnDestroy()
        {
            UnregisterButton();
        }

        public void Bind(TalentOfferState sourceOffer, Action<TalentOfferState> onRecruitClicked)
        {
            UnregisterButton();
            offer = sourceOffer;
            recruitClicked = onRecruitClicked;
            Refresh();
            if (recruitButton != null)
            {
                recruitButton.onClick.AddListener(HandleRecruitClicked);
            }
        }

        public void Unbind()
        {
            UnregisterButton();
            offer = null;
            recruitClicked = null;
            SetText(titleLabel, string.Empty);
            SetText(metaLabel, string.Empty);
            SetText(salaryLabel, string.Empty);
            SetText(effectsLabel, string.Empty);
            SetText(hiddenTraitLabel, string.Empty);
            SetImage(icon, null);
            SetImage(professionIcon, null);
            SetText(recruitButtonLabel, string.Empty);
            if (recruitButton != null)
            {
                recruitButton.interactable = false;
            }
        }

        private void Refresh()
        {
            var definition = offer == null ? null : offer.Definition;
            if (definition == null)
            {
                Unbind();
                return;
            }

            SetText(titleLabel, definition.DisplayName);
            SetText(metaLabel, GamePanel_TalentText.FormatOfferDetails(definition));
            SetText(salaryLabel, GamePanel_TalentText.FormatSalary(definition));
            SetText(effectsLabel, GamePanel_TalentText.FormatEffects(definition));
            SetText(hiddenTraitLabel, definition.HiddenTraits.Count > 0 ? "含隐藏特性" : "无隐藏特性");
            SetImage(icon, definition.Icon);
            SetImage(professionIcon, definition.ProfessionIcon);
            SetColor(cardBackground, definition.CardMainColor);
            SetColor(rarityMarker, definition.RarityColor);
            SetText(recruitButtonLabel, "招募");
            if (recruitButton != null)
            {
                recruitButton.interactable = true;
            }
        }

        private void HandleRecruitClicked()
        {
            recruitClicked?.Invoke(offer);
        }

        private void UnregisterButton()
        {
            if (recruitButton != null)
            {
                recruitButton.onClick.RemoveListener(HandleRecruitClicked);
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
