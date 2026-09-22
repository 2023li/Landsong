using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_PersonRequestsView : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("标题")]
        public TMP_Text Title;
        [Sirenix.OdinInspector.LabelText("滚动视图")]
        public ScrollRect Scroll;
        [Sirenix.OdinInspector.LabelText("条目容器")]
        public RectTransform Rows;
        [Sirenix.OdinInspector.LabelText("文字模板")]
        public UI_GamePanel_RequestTextRow TextTemplate;
        [Sirenix.OdinInspector.LabelText("操作模板")]
        public UI_GamePanel_RequestActionRow ActionTemplate;
        [Sirenix.OdinInspector.LabelText("关闭")]
        public Button Close;
        public void ValidateConfiguration()
        {
            if (Title == null || Scroll == null || Rows == null || TextTemplate == null || ActionTemplate == null || Close == null)
                throw new InvalidOperationException("人物请求面板检查器引用不完整。");
        }
    }
}
