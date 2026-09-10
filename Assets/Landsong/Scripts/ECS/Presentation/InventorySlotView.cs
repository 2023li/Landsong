using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    // Pointer adapter only. Captures stable slot keys and a display fingerprint, never owns stock.
    public sealed class InventorySlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public EcsGameView Owner;
        public ulong Provider;
        public int Index, Item, Count;
        public bool Pending, Locked;
        public string Fingerprint;
        public Text Label;
        public Image Icon;
        public void OnBeginDrag(PointerEventData data) { if (!Locked && Count > 0) Owner.BeginInventoryDrag(this); }
        public void OnDrag(PointerEventData data) => Owner.UpdateInventoryDrag(data.position);
        public void OnEndDrag(PointerEventData data) => Owner.EndInventoryDrag();
        public void OnDrop(PointerEventData data) { if (!Locked && !Pending) Owner.DropInventory(this); }
    }
}
