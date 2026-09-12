using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public abstract class UI_GamePanel_BuildingDetails_Block : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("所属建筑详情")]
        public UI_GamePanel_BuildingDetails View;

        public virtual void ValidateConfiguration()
        {
            if (View == null)
                throw new InvalidOperationException(name + " 的所属建筑详情未在检查器中配置。");
        }

        protected void ValidateReferences(params (UnityEngine.Object Value, string Name)[] references)
        {
            var missing = new List<string>();
            if (View == null)
                missing.Add(nameof(View));
            foreach (var reference in references)
                if (reference.Value == null)
                    missing.Add(reference.Name);
            if (missing.Count > 0)
                throw new InvalidOperationException(name + " 模块检查器引用不完整：" + string.Join("、", missing));
        }

        protected void BindSidebar(UI_GamePanel_BuildingDetails_SidebarTrigger trigger, Func<string> content)
        {
            if (trigger == null || trigger.View != View)
                throw new InvalidOperationException(name + " 模块的侧栏触发器配置错误。");
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
