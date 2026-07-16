using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.UISystem
{
    /// <summary>
    /// 只监听悬停，不实现点击或拖拽接口，因此事件会继续交给科技节点按钮和外层 ScrollRect。
    /// </summary>
    public sealed class TechnologyUnlockIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action pointerEntered;
        private Action pointerExited;

        public void Bind(Action onPointerEntered, Action onPointerExited)
        {
            pointerEntered = onPointerEntered;
            pointerExited = onPointerExited;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerEntered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerExited?.Invoke();
        }

        private void OnDisable()
        {
            pointerExited?.Invoke();
        }
    }
}
