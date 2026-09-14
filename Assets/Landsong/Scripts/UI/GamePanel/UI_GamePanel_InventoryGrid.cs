using System;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    /// <summary>Inventory-owned grid view. It is a fixed panel child, never a reusable list row.</summary>
    public sealed class UI_GamePanel_InventoryGrid : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("布局")]
        public LayoutElement Layout;
        [Sirenix.OdinInspector.LabelText("交互状态")]
        public UI_GamePanel_InteractionLock Interaction;
        [Sirenix.OdinInspector.LabelText("网格")]
        public RectTransform Grid;
        [Sirenix.OdinInspector.LabelText("网格布局")]
        public GridLayoutGroup GridLayout;
        [Sirenix.OdinInspector.LabelText("槽位模板")]
        public UI_GamePanel_InventorySlot SlotTemplate;

        public bool CanRebind => Interaction == null || !Interaction.IsPinned;

        public void ValidateConfiguration()
        {
            if (Layout == null || Interaction == null || Grid == null || GridLayout == null || SlotTemplate == null)
                throw new InvalidOperationException("库存网格引用不完整。");
            Interaction.ValidateConfiguration();
            if (SlotTemplate.InteractionOwner != Interaction)
                throw new InvalidOperationException("库存槽位模板的交互归属必须是所在库存网格。");
        }
    }
}
