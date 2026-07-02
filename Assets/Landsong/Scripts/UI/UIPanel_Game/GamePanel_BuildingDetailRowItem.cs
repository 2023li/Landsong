using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingDetailRowItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text valueText;

        private void Reset()
        {
            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            if (labels.Length > 0)
            {
                labelText = labels[0];
            }

            if (labels.Length > 1)
            {
                valueText = labels[1];
            }
        }

        public void Bind(BuildingDetailRow row)
        {
            SetText(labelText, row.Label);
            SetText(valueText, row.Value);
        }

        public void Unbind()
        {
            SetText(labelText, string.Empty);
            SetText(valueText, string.Empty);
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
