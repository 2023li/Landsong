using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    // Pointer adapter only. Captures stable slot keys and a display fingerprint, never owns stock.
    public sealed class UI_GamePanel_InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Sirenix.OdinInspector.LabelText("所有者")]
        public UI_GamePanel_Inventory Owner;
        [Sirenix.OdinInspector.LabelText("提供者")]
        public ulong Provider;
        [Sirenix.OdinInspector.LabelText("索引")]
        public int Index;
        [Sirenix.OdinInspector.LabelText("物品")]
        public int Item;
        [Sirenix.OdinInspector.LabelText("数量")]
        public int Count;
        [Sirenix.OdinInspector.LabelText("待处理")]
        public bool Pending;
        [Sirenix.OdinInspector.LabelText("锁定")]
        public bool Locked;
        [Sirenix.OdinInspector.LabelText("数据版本")]
        public string Fingerprint;
        [Sirenix.OdinInspector.LabelText("文字")]
        public Text Label;
        [Sirenix.OdinInspector.LabelText("图标")]
        public Image Icon;
        [Sirenix.OdinInspector.LabelText("背景")]
        public Image Background;
        [Sirenix.OdinInspector.LabelText("选择")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("交互所属条目")]
        public UI_GamePanel_Row InteractionOwner;
        public bool CanRebind => InteractionOwner != null && InteractionOwner.CanRebind;
        public void OnBeginDrag(PointerEventData data)
        {
            if (!Locked && Count > 0)
                Owner.BeginInventoryDrag(this);
        }

        public void OnDrag(PointerEventData data) => Owner.UpdateInventoryDrag(data.position);
        public void OnEndDrag(PointerEventData data) => Owner.EndInventoryDrag();
        public void OnDrop(PointerEventData data)
        {
            if (!Locked && !Pending)
                Owner.DropInventory(this);
        }
    }
}
