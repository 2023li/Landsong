using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public abstract class UI_GamePanel_BuildingDetails_Block : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [LabelText("所属建筑详情"), Required]
        public UI_GamePanel_BuildingDetails View;

        Func<string> sidebarContent;

        protected void BindSidebar(Func<string> content)
        {
            sidebarContent = content;
            View.SetSidebarContent(this, content);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (sidebarContent != null)
                View.ShowSidebar(this, sidebarContent);
        }

        public void OnPointerExit(PointerEventData eventData) => View.LeaveSidebar(this);

        protected virtual void OnDisable()
        {
            if (View != null)
                View.HideSidebar(this);
        }

        protected static void Span(Image image, float start, float end)
        {
            image.rectTransform.anchorMin = new Vector2(Mathf.Clamp01(start), 0);
            image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(end), 1);
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        }

        protected static void Bind(Button button, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = action != null;
            if (action != null)
                button.onClick.AddListener(() => action());
        }
    }
}
