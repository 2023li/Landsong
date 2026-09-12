using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_Common_Click : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        [LabelText("按钮目标"), Required]
        public Button Target;
        public void ValidateConfiguration()
        {
            if (Target == null)
                throw new InvalidOperationException(name + " 的点击音效按钮未在检查器中配置。");
            if (Target.gameObject != gameObject)
                throw new InvalidOperationException(name + " 的点击音效按钮必须是当前对象上的按钮组件。");
        }

        void Awake() => ValidateConfiguration();
        void Click()
        {
            if (Target.IsInteractable())
                PresentationRuntime.Instance?.Play(PresentationCue.Click);
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left)
                Click();
        }

        public void OnSubmit(BaseEventData data)
        {
            Click();
        }
    }
}
