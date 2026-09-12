using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    [DisallowMultipleComponent]
    public sealed class UI_Common_InputFocusBinding : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [LabelText("输入框"), Required]
        public TMP_InputField Target;
        void Awake()
        {
            if (Target == null || Target.gameObject != gameObject)
                throw new System.InvalidOperationException(name + " 的输入焦点绑定必须显式引用同对象输入框。");
        }

        public void OnSelect(BaseEventData eventData) => UiInputState.Select(Target);
        public void OnDeselect(BaseEventData eventData) => UiInputState.Deselect(Target);
        void OnDisable() => UiInputState.Deselect(Target);
    }
}
