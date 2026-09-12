using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    public class UI_GamePanel_BuildingDetails_SidebarTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [LabelText("建筑详情"), Required]
        public UI_GamePanel_BuildingDetails View;

        [NonSerialized]
        public Func<string> Content;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Content != null)
                View.ShowSidebar(this, Content);
        }

        public void OnPointerExit(PointerEventData eventData) => View.LeaveSidebar(this);
    }
}
