using System;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_BuildingCategoryButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField, LabelText("选中标记")] private GameObject selectedMarker;

        private Action<BuildingCategory> clicked;

        public BuildingCategory Category { get; private set; }

        private void Reset()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<TMP_Text>(true);
        }

        private void OnValidate()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Bind(
            BuildingCategory category,
            bool selected,
            Action<BuildingCategory> onClicked)
        {
            Category = category;
            clicked = onClicked;

            if (label != null)
            {
                label.text = GetDisplayName(category);
            }

            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
                button.onClick.AddListener(HandleClicked);
            }

            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            if (selectedMarker != null)
            {
                selectedMarker.SetActive(selected);
            }
        }

        private void HandleClicked()
        {
            clicked?.Invoke(Category);
        }

        private static string GetDisplayName(BuildingCategory category)
        {
            return category switch
            {
                BuildingCategory.Housing => "居住",
                BuildingCategory.Production => "生产",
                BuildingCategory.Storage => "仓储",
                BuildingCategory.后勤 => "物流",
                BuildingCategory.通用 => "功能",
                BuildingCategory.美化 => "装饰",
                _ => category.ToString()
            };
        }
    }
}
