using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuildingDetailsCropOptionItem : MonoBehaviour
{
    [SerializeField, Required, LabelText("按钮")] private Button button;
    [SerializeField, Required, LabelText("作物图标")] private Image iconImage;
    [SerializeField, Required, LabelText("作物名称")] private TMP_Text nameText;

    public Button Button => button;

    public bool ValidateConfiguration(out string error)
    {
        if (button != null && iconImage != null && nameText != null)
        {
            error = string.Empty;
            return true;
        }

        error = "作物选项必须显式绑定按钮、图标和名称文本。";
        return false;
    }

    public void Bind(BuildingCropOption option, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }

        if (iconImage != null)
        {
            iconImage.sprite = option.Icon;
            iconImage.enabled = option.Icon != null;
        }

        if (nameText != null)
        {
            nameText.text = Landsong.Localization.L10n.Gameplay(
                "gameplay.building.crop.option",
                "{0}（{1}回合）",
                option.DisplayName,
                option.GrowTurns);
        }
    }
}
