using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    // Pointer events remain available on unaffordable cards even though their Button is disabled.
    public sealed class BuildingCatalogHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public BuildingCatalogBar Bar;
        public int Definition;
        public void OnPointerEnter(PointerEventData eventData) => Bar.ShowTooltip(Definition);
        public void OnPointerExit(PointerEventData eventData) => Bar.HideTooltip();
        public void OnSelect(BaseEventData eventData) => Bar.ShowTooltip(Definition);
        public void OnDeselect(BaseEventData eventData) => Bar.HideTooltip();
    }
}
