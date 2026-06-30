using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class GamePanel_InventoryTrashDropArea : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        var slot = eventData.pointerDrag == null
            ? null
            : eventData.pointerDrag.GetComponent<GamePanel_InventorySlot>();

        slot?.Discard();
    }
}
