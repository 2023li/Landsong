using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_PortraitCustomization : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("标题")]
        public TMP_Text Title;
        [Sirenix.OdinInspector.LabelText("规则")]
        public TMP_Text Rules;
        [Sirenix.OdinInspector.LabelText("预览")]
        public Image Preview;
        [Sirenix.OdinInspector.LabelText("预览引用绑定")]
        public UI_Common_PortraitImageBinding PreviewBinding;
        [Sirenix.OdinInspector.LabelText("滚动视图")]
        public ScrollRect Scroll;
        [Sirenix.OdinInspector.LabelText("部件按钮集合")]
        public Button[] PartButtons;
        [Sirenix.OdinInspector.LabelText("部件文字集合")]
        public TMP_Text[] PartLabels;
        [Sirenix.OdinInspector.LabelText("颜色滑动条")]
        public Slider[] ColorSliders;
        [Sirenix.OdinInspector.LabelText("确认")]
        public Button Confirm;
        [Sirenix.OdinInspector.LabelText("关闭")]
        public Button Close;
        public void ValidateConfiguration()
        {
            if (Title == null || Rules == null || Preview == null || PreviewBinding == null || Scroll == null || Confirm == null || Close == null || PartButtons == null || PartButtons.Length != 13 || PartLabels == null || PartLabels.Length != 13 || ColorSliders == null || ColorSliders.Length != 9)
                throw new InvalidOperationException("塑容面板检查器引用不完整。");
            PreviewBinding.ValidateConfiguration();
        }
    }
}
