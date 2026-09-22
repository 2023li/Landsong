using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    // The common row contains only common presentation. Domain controls live in typed row prefabs.
    public class UI_GamePanel_Row : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("选择")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("文字")]
        public TextMeshProUGUI Label;
        [Sirenix.OdinInspector.LabelText("布局")]
        public LayoutElement Layout;
        [Sirenix.OdinInspector.LabelText("图标")]
        public Image Icon;
        [LabelText("交互状态")]
        public UI_GamePanel_InteractionLock Interaction;
        internal UI_GamePanel_Row Template { get; set; }
        public string Identity { get; internal set; }
        Action action;
        public bool IsInteractionPinned => Interaction != null && Interaction.IsPinned;
        public bool CanRebind => !IsInteractionPinned;
        internal void InitializeAction() { Select.onClick.RemoveAllListeners(); Select.onClick.AddListener(() => action?.Invoke()); }
        internal void BindAction(Action callback) { if (!CanRebind) return; action = callback; Select.interactable = callback != null; }

        public virtual void ValidateConfiguration()
        {
            if (Select == null || Label == null || Layout == null)
                throw new InvalidOperationException("通用行模板检查器引用不完整。");
            if (Interaction == null) throw new InvalidOperationException("通用行模板未配置指针交互状态。");
            Interaction.ValidateConfiguration();
        }

        public virtual void ResetPresentation()
        {
            ValidateConfiguration();
            Label.margin = new Vector4(8, 4, 8, 4);
            if (Icon != null)
                Icon.gameObject.SetActive(false);
        }
    }
}
