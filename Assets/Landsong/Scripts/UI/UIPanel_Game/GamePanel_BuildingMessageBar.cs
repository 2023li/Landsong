using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingMessageBar : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private string emptyText = string.Empty;

        private void Reset()
        {
            root = gameObject;
            messageLabel = GetComponentInChildren<TMP_Text>(true);
        }

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }
        }

        public void ShowBuildingMessage(BuildingBase building)
        {
            var data = BuildingStatusUIFormatter.CreateDisplayData(building);
            ShowMessage(data.DetailText);
        }

        public void ShowMessage(string message)
        {
            if (root != null)
            {
                root.SetActive(true);
            }

            if (messageLabel != null)
            {
                messageLabel.text = string.IsNullOrWhiteSpace(message) ? emptyText : message;
            }
        }

        public void Clear()
        {
            if (messageLabel != null)
            {
                messageLabel.text = emptyText;
            }
        }
    }
}
