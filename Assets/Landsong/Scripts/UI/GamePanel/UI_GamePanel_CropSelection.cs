using System;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class CropSelectionEntry
    {
        public CropId Crop;
        public Sprite Icon;
        public string Name;
        public string BaseYield;
        public int GrowthTurns;
        public string PlantingCost;
        public bool Available;
    }

    // A planting-specific modal. The planting block supplies crop data and handles the choice.
    public sealed class UI_GamePanel_CropSelection : MonoBehaviour
    {
        [LabelText("标题"), Required]
        public TMP_Text Title;
        [LabelText("作物条目容器"), Required]
        public RectTransform Rows;
        [LabelText("作物条目模板"), Required]
        public UI_GamePanel_Row RowTemplate;
        [LabelText("滚动视图"), Required]
        public ScrollRect Scroll;

        UI_GamePanel_RowCollection rowsController;

        public bool IsOpen => gameObject.activeSelf;

        public void ValidateConfiguration()
        {
            if (Title == null || Rows == null || RowTemplate == null || Scroll == null)
                throw new InvalidOperationException("作物选择面板检查器引用不完整。");
            RowTemplate.ValidateConfiguration();
        }

        public void Show(IReadOnlyList<CropSelectionEntry> entries, Action<CropId> choose, string hint = null)
        {
            if (entries == null || choose == null)
                throw new ArgumentNullException(entries == null ? nameof(entries) : nameof(choose));
            ValidateConfiguration();
            rowsController ??= new UI_GamePanel_RowCollection(RowTemplate);
            Title.text = "选择作物";
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            rowsController.Begin(Rows);
            try
            {
                if (!string.IsNullOrEmpty(hint))
                    rowsController.Row(hint, parent: Rows, key: "planting-hint");
                foreach (var entry in entries)
                {
                    var crop = entry.Crop;
                    var label = entry.Name + "\n基础产量：" + entry.BaseYield + "    生长周期：" + entry.GrowthTurns + " 回合\n种植消耗：" + entry.PlantingCost;
                    var row = rowsController.Row(label, entry.Available ? () =>
                    {
                        Hide();
                        choose(crop);
                    } : null, Rows, "crop:" + crop.Index);
                    if (row == null)
                        continue;
                    row.Icon.sprite = entry.Icon;
                    row.Icon.gameObject.SetActive(entry.Icon != null);
                    row.Icon.preserveAspect = true;
                    row.Icon.rectTransform.anchorMin = row.Icon.rectTransform.anchorMax = new Vector2(0, .5f);
                    row.Icon.rectTransform.anchoredPosition = new Vector2(38, 0);
                    row.Icon.rectTransform.sizeDelta = new Vector2(54, 54);
                    row.Label.rectTransform.offsetMin = new Vector2(76, 2);
                    row.Label.rectTransform.offsetMax = new Vector2(-10, -2);
                    row.Label.alignment = TextAlignmentOptions.MidlineLeft;
                    var height = row.Label.GetPreferredValues(label, Mathf.Max(180, Rows.rect.width - 96), float.PositiveInfinity).y + 16;
                    row.Layout.minHeight = row.Layout.preferredHeight = Mathf.Max(90, height);
                }
                rowsController.Row("关闭", Hide, Rows, "crop-close");
            }
            finally
            {
                rowsController.End();
            }
            Scroll.verticalNormalizedPosition = 1;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
