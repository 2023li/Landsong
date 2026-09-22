using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    // One card owns its controls. Nested buttons consume their own clicks; the card only selects.
    public sealed class UI_GamePanel_SoldierItem : MonoBehaviour
    {
        public ulong PersonId { get; private set; }

        [Sirenix.OdinInspector.LabelText("选择")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("详情")]
        public Button Details;
        [Sirenix.OdinInspector.LabelText("移除")]
        public Button Remove;
        [Sirenix.OdinInspector.LabelText("解雇")]
        public Button Dismiss;
        [Sirenix.OdinInspector.LabelText("关注")]
        public Toggle Attention;
        [Sirenix.OdinInspector.LabelText("肖像")]
        public Image Portrait;
        [Sirenix.OdinInspector.LabelText("肖像引用绑定")]
        public UI_Common_PortraitImageBinding PortraitBinding;
        [Sirenix.OdinInspector.LabelText("标题")]
        public TMP_Text Title;
        [Sirenix.OdinInspector.LabelText("副标题")]
        public TMP_Text Subtitle;
        [Sirenix.OdinInspector.LabelText("属性")]
        public TMP_Text Attributes;
        public const float Height = 190;
        public void ValidateConfiguration()
        {
            if (Select == null || Details == null || Remove == null || Dismiss == null || Attention == null || Portrait == null || PortraitBinding == null || Title == null || Subtitle == null || Attributes == null)
                throw new InvalidOperationException("士兵卡片模板检查器引用不完整。");
            PortraitBinding.ValidateConfiguration();
        }

        public void Bind(ulong id, string name, string rank, string attributes, bool selected, bool watched, bool canWatch, Action select, Action details, Action remove, Action dismiss, Action<bool> attention)
        {
            PersonId = id;
            Title.text = name;
            Subtitle.text = rank;
            Attributes.text = attributes;
            Select.image.color = selected ? new Color(.12f, .36f, .38f) : new Color(.15f, .21f, .29f);
            BindButton(Select, select);
            BindButton(Details, details);
            BindButton(Remove, remove);
            BindButton(Dismiss, dismiss);
            Attention.onValueChanged.RemoveAllListeners();
            Attention.SetIsOnWithoutNotify(watched);
            Attention.interactable = canWatch;
            Attention.onValueChanged.AddListener(v => attention(v));
        }

        static void BindButton(Button button, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = action != null;
            if (action != null)
                button.onClick.AddListener(() => action());
        }
    }
}
