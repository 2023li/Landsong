using Landsong.InheritanceSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_InheritanceCharacterItem : MonoBehaviour
    {
        [SerializeField, LabelText("状态标记")] private Image statusMarker;
        [SerializeField, LabelText("名称")] private TMP_Text titleLabel;
        [SerializeField, LabelText("身份状态")] private TMP_Text metaLabel;
        [SerializeField, LabelText("年龄寿命")] private TMP_Text ageLabel;
        [SerializeField, LabelText("统治回合")] private TMP_Text reignLabel;
        [SerializeField, LabelText("关系")] private TMP_Text relationLabel;
        [SerializeField, LabelText("特性")] private TMP_Text traitsLabel;
        [SerializeField, LabelText("在位颜色")] private Color reigningColor = Color.yellow;
        [SerializeField, LabelText("继承人颜色")] private Color heirColor = Color.cyan;
        [SerializeField, LabelText("失效颜色")] private Color inactiveColor = Color.gray;

        public void Bind(RoyalCharacterState character, int legalHeirAge)
        {
            if (character == null)
            {
                Unbind();
                return;
            }

            SetText(titleLabel, character.DisplayName);
            SetText(metaLabel, $"{GamePanel_InheritanceText.FormatRole(character.Role)} / {GamePanel_InheritanceText.FormatStatus(character.Status)}");
            SetText(ageLabel, $"年龄 {character.Age} / 寿命 {character.EffectiveMaxLifespan} / 剩余 {character.RemainingLifespan}");
            SetText(reignLabel, character.IsReigning ? $"统治 {character.CurrentReignTurns} 回合" : BuildHeirText(character, legalHeirAge));
            SetText(relationLabel, GamePanel_InheritanceText.FormatRelations(character));
            SetText(traitsLabel, GamePanel_InheritanceText.FormatTraits(character));
            SetMarkerColor(character);
        }

        public void Unbind()
        {
            SetText(titleLabel, string.Empty);
            SetText(metaLabel, string.Empty);
            SetText(ageLabel, string.Empty);
            SetText(reignLabel, string.Empty);
            SetText(relationLabel, string.Empty);
            SetText(traitsLabel, string.Empty);
            if (statusMarker != null)
            {
                statusMarker.enabled = false;
            }
        }

        private string BuildHeirText(RoyalCharacterState character, int legalHeirAge)
        {
            if (character == null || !character.IsPotentialHeir)
            {
                return string.Empty;
            }

            return character.IsLegalHeir(legalHeirAge)
                ? "可继承"
                : $"未成年：{character.Age}/{legalHeirAge}";
        }

        private void SetMarkerColor(RoyalCharacterState character)
        {
            if (statusMarker == null)
            {
                return;
            }

            statusMarker.enabled = true;
            statusMarker.color = character.Status switch
            {
                RoyalCharacterStatus.Reigning => reigningColor,
                RoyalCharacterStatus.Heir => heirColor,
                RoyalCharacterStatus.Consort => heirColor,
                _ => inactiveColor
            };
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
