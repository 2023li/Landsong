using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    public class UI_GamePanel_BuildingDetails_SidebarHoverArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Sirenix.OdinInspector.LabelText("建筑详情")]
        public UI_GamePanel_BuildingDetails View;

        public void OnPointerEnter(PointerEventData eventData) => View.SetSidebarHovered(true);
        public void OnPointerExit(PointerEventData eventData) => View.SetSidebarHovered(false);
    }
}
