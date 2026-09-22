using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public abstract class UI_GamePanel_BuildingDetails_Block : MonoBehaviour
    {
        [LabelText("所属建筑详情"), Required]
        public UI_GamePanel_BuildingDetails View;

        protected void BindSidebar(UI_GamePanel_BuildingDetails_SidebarTrigger trigger, Func<string> content)
        {
            View.ConfigureSidebar(trigger, content);
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
