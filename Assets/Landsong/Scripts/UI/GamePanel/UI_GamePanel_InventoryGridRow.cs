using System;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_InventoryGridRow : UI_GamePanel_Row
    {
        [Sirenix.OdinInspector.LabelText("网格")]
        public RectTransform Grid;
        [Sirenix.OdinInspector.LabelText("网格布局")]
        public GridLayoutGroup GridLayout;
        [Sirenix.OdinInspector.LabelText("槽位模板")]
        public UI_GamePanel_InventorySlot SlotTemplate;
        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (Grid == null || GridLayout == null || SlotTemplate == null)
                throw new InvalidOperationException("库存网格行引用不完整。");
        }
    }
}
