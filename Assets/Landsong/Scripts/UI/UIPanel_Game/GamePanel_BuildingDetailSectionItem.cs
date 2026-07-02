using System.Collections.Generic;
using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingDetailSectionItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Transform rowRoot;
        [SerializeField] private GamePanel_BuildingDetailRowItem rowPrefab;

        private readonly List<GamePanel_BuildingDetailRowItem> activeRows = new List<GamePanel_BuildingDetailRowItem>();

        private void Reset()
        {
            titleLabel = GetComponentInChildren<TMP_Text>(true);
            rowRoot = transform;
            rowPrefab = GetComponentInChildren<GamePanel_BuildingDetailRowItem>(true);
        }

        private void Awake()
        {
            if (rowPrefab != null)
            {
                rowPrefab.gameObject.SetActive(false);
            }
        }

        public void Bind(BuildingDetailSection section)
        {
            ClearRows();
            SetText(titleLabel, section.Title);

            if (rowRoot == null || rowPrefab == null || section.Rows == null)
            {
                return;
            }

            for (int i = 0; i < section.Rows.Count; i++)
            {
                BuildingDetailRow row = section.Rows[i];
                if (!row.IsValid)
                {
                    continue;
                }

                GamePanel_BuildingDetailRowItem item = Instantiate(rowPrefab, rowRoot);
                item.gameObject.SetActive(true);
                item.Bind(row);
                activeRows.Add(item);
            }
        }

        public void Unbind()
        {
            SetText(titleLabel, string.Empty);
            ClearRows();
        }

        private void ClearRows()
        {
            for (int i = 0; i < activeRows.Count; i++)
            {
                GamePanel_BuildingDetailRowItem row = activeRows[i];
                if (row == null)
                {
                    continue;
                }

                row.Unbind();
                Destroy(row.gameObject);
            }

            activeRows.Clear();
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
