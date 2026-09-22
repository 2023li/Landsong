using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    /// <summary>Explicitly authored on a row's controls; also survives pointer-up until that frame's click dispatch.</summary>
    public sealed class UI_GamePanel_RowPointerBinding : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [LabelText("所属交互状态")] public UI_GamePanel_InteractionLock Target;
        [LabelText("输入焦点期间冻结")] public bool LockWhileSelected;
        readonly System.Collections.Generic.HashSet<int> pressed = new System.Collections.Generic.HashSet<int>();
        bool selected;
        public void OnPointerDown(PointerEventData data) { if (pressed.Add(data.pointerId)) Target.Begin(); }
        public void OnPointerUp(PointerEventData data) { if (pressed.Remove(data.pointerId)) Target.End(); }
        public void OnSelect(BaseEventData data) { if (LockWhileSelected && !selected) { selected = true; Target.Begin(); } }
        public void OnDeselect(BaseEventData data) { if (selected) { selected = false; Target.End(); } }
        void OnDisable()
        {
            if (Target != null) { foreach (var pointer in pressed) Target.End(); if (selected) Target.End(); }
            pressed.Clear(); selected = false;
        }
    }
}
