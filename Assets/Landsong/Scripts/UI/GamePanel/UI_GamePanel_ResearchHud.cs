using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_ResearchHud : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("打开")]
        public Button Open;
        [Sirenix.OdinInspector.LabelText("图标")]
        public Image Icon;
        [Sirenix.OdinInspector.LabelText("进度")]
        public Image Progress;
        [Sirenix.OdinInspector.LabelText("图标备用")]
        public TextMeshProUGUI IconFallback;
        [Sirenix.OdinInspector.LabelText("名称")]
        public TextMeshProUGUI Name;
        [Sirenix.OdinInspector.LabelText("状态")]
        public TextMeshProUGUI Status;
        [Sirenix.OdinInspector.LabelText("效果")]
        public TextMeshProUGUI Effects;
        public void ValidateConfiguration()
        {
            if (Open == null || Icon == null || Progress == null || IconFallback == null || Name == null || Status == null || Effects == null)
                throw new InvalidOperationException("科技 HUD 检查器引用不完整。");
        }
    }
}
