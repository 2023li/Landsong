using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    // Pointer events remain available on unaffordable cards even though their Button is disabled.
    public sealed class UI_GamePanel_BuildingCatalogHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Sirenix.OdinInspector.LabelText("栏")]
        public UI_GamePanel_BuildingCatalogBar Bar;
        [Sirenix.OdinInspector.LabelText("定义")]
        public int Definition;
        public void OnPointerEnter(PointerEventData eventData) => Bar.ShowTooltip(Definition);
        public void OnPointerExit(PointerEventData eventData) => Bar.HideTooltip();
        public void OnSelect(BaseEventData eventData) => Bar.ShowTooltip(Definition);
        public void OnDeselect(BaseEventData eventData) => Bar.HideTooltip();
    }
}
