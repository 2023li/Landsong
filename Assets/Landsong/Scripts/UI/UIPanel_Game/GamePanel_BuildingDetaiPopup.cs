using System.Collections.Generic;
using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingDetaiPopup : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform sectionRoot;
        [SerializeField] private GamePanel_BuildingDetailSectionItem sectionPrefab;

        private readonly List<GamePanel_BuildingDetailSectionItem> activeSections = new List<GamePanel_BuildingDetailSectionItem>();

        public bool IsVisible => root != null && root.activeSelf;

        private void Reset()
        {
            root = gameObject;
            titleLabel = GetComponentInChildren<TMP_Text>(true);
            sectionRoot = transform;
            sectionPrefab = GetComponentInChildren<GamePanel_BuildingDetailSectionItem>(true);
        }

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }

            if (sectionPrefab != null)
            {
                sectionPrefab.gameObject.SetActive(false);
            }

            Hide();
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }
        }

        public void ShowBuilding(BuildingBase building)
        {
            if (building == null)
            {
                Hide();
                return;
            }

            BuildingStatusDisplayData data = BuildingStatusUIFormatter.CreateDisplayData(building);
            SetActive(root, true);
            SetText(titleLabel, data.BuildingName);
            SetText(statusLabel, data.StatusText);
            RebuildSections(building);
        }

        public void Hide()
        {
            ClearSections();
            SetText(titleLabel, string.Empty);
            SetText(statusLabel, string.Empty);
            SetActive(root, false);
        }

        private void RebuildSections(BuildingBase building)
        {
            ClearSections();
            if (sectionRoot == null || sectionPrefab == null)
            {
                return;
            }

            IReadOnlyList<BuildingDetailSection> sections = BuildingDetailUIFormatter.CreateDetailSections(building);
            for (int i = 0; i < sections.Count; i++)
            {
                BuildingDetailSection section = sections[i];
                if (!section.IsValid)
                {
                    continue;
                }

                GamePanel_BuildingDetailSectionItem item = Instantiate(sectionPrefab, sectionRoot);
                item.gameObject.SetActive(true);
                item.Bind(section);
                activeSections.Add(item);
            }
        }

        private void ClearSections()
        {
            for (int i = 0; i < activeSections.Count; i++)
            {
                GamePanel_BuildingDetailSectionItem section = activeSections[i];
                if (section == null)
                {
                    continue;
                }

                section.Unbind();
                Destroy(section.gameObject);
            }

            activeSections.Clear();
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
