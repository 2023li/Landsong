using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    /// <summary>The whole pending area accepts a building stack, including empty space.</summary>
    public sealed class UI_GamePanel_InventoryPendingDrop : MonoBehaviour, IDropHandler
    {
        [LabelText("库存面板")] public UI_GamePanel_Inventory Owner;
        public void OnDrop(PointerEventData data) => Owner.DropInventoryToPending();
    }
}
