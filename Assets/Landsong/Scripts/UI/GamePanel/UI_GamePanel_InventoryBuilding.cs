using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_InventoryBuilding : MonoBehaviour
    {
        [LabelText("建筑信息")] public TMP_Text Information;
        [LabelText("定位到建筑")] public Button Locate;
        [LabelText("置顶")] public Toggle Pin;
        [LabelText("格子容器")] public RectTransform Slots;
        [LabelText("格子模板")] public UI_GamePanel_InventorySlot SlotTemplate;
        [LabelText("交互状态")] public UI_GamePanel_InteractionLock Interaction;
        [LabelText("网格布局")] public GridLayoutGroup GridLayout;
        [LabelText("条目尺寸")] public LayoutElement Layout;
        public ulong Provider { get; internal set; }

        public void ValidateConfiguration()
        {
            if (Information == null || Locate == null || Pin == null || Slots == null || SlotTemplate == null || Interaction == null || GridLayout == null || Layout == null)
                throw new InvalidOperationException("库存建筑条目引用不完整。");
            Interaction.ValidateConfiguration(); SlotTemplate.ValidateConfiguration();
            if (SlotTemplate.transform.parent != Slots) throw new InvalidOperationException("建筑槽位模板必须在格子容器内。");
        }
    }
}
