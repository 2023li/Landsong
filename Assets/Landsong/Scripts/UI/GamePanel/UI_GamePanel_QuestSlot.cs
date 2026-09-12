using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    // Reused visual slot; quest identity and progress remain authoritative ECS data.
    public sealed class UI_GamePanel_QuestSlot : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("交互状态")]
        public UI_GamePanel_InteractionLock Interaction;
        [Sirenix.OdinInspector.LabelText("矩形")]
        public RectTransform Rect;
        [Sirenix.OdinInspector.LabelText("正文")]
        public RectTransform Body;
        [Sirenix.OdinInspector.LabelText("来源")]
        public TextMeshProUGUI Source;
        [Sirenix.OdinInspector.LabelText("标题")]
        public TextMeshProUGUI Title;
        [Sirenix.OdinInspector.LabelText("期限")]
        public TextMeshProUGUI Deadline;
        [Sirenix.OdinInspector.LabelText("展开")]
        public Button Expand;
        [Sirenix.OdinInspector.LabelText("操作")]
        public Button Action;
        [Sirenix.OdinInspector.LabelText("跟踪")]
        public Toggle Tracking;
        [Sirenix.OdinInspector.LabelText("背景")]
        public Image Background;
        [Sirenix.OdinInspector.LabelText("来源布局")]
        public LayoutElement SourceLayout;
        [Sirenix.OdinInspector.LabelText("操作文字")]
        public TextMeshProUGUI ActionLabel;
        [Sirenix.OdinInspector.LabelText("任务标识")]
        public ulong QuestId;
        public void Show(ulong id, string source, string title, string deadline, bool expanded, bool completed, Action toggle, string actionLabel, Action action)
        {
            QuestId = id;
            Rect.gameObject.SetActive(true);
            Source.text = source;
            Tracking.gameObject.SetActive(false);
            var width = Mathf.Max(200, ((RectTransform)Rect.parent).rect.width - 44);
            SourceLayout.preferredHeight = Mathf.Max(36, Source.GetPreferredValues(source, width, float.PositiveInfinity).y + 8);
            Background.color = completed ? new Color(.16f, .30f, .24f) : new Color(.24f, .26f, .28f);
            Title.text = (id == 0 ? "" : expanded ? "∨  " : ">  ") + title;
            Deadline.text = deadline;
            Body.gameObject.SetActive(expanded);
            Expand.onClick.RemoveAllListeners();
            Expand.interactable = toggle != null;
            if (toggle != null)
                Expand.onClick.AddListener(() => toggle());
            Action.gameObject.SetActive(!string.IsNullOrEmpty(actionLabel));
            Action.interactable = action != null;
            ActionLabel.text = actionLabel;
            Action.onClick.RemoveAllListeners();
            if (action != null)
                Action.onClick.AddListener(() => action());
        }
    }
}
