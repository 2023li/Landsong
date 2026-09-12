using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    public class UI_GamePanel_BuildingDetails_SidebarTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Sirenix.OdinInspector.LabelText("建筑详情")]
        public UI_GamePanel_BuildingDetails View;

        [NonSerialized]
        public Func<string> Content;

        public void ValidateConfiguration()
        {
            if (View == null)
                throw new InvalidOperationException(name + " 的侧栏目标未在检查器中配置。");
        }

        protected virtual void Awake() => ValidateConfiguration();

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Content != null)
                View.ShowSidebar(this, Content);
        }

        public void OnPointerExit(PointerEventData eventData) => View.LeaveSidebar(this);
    }
}
