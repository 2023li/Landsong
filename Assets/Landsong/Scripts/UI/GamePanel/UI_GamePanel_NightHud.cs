using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_NightHud : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("标记根对象")]
        public RectTransform MarkerRoot;
        [Sirenix.OdinInspector.LabelText("标记模板")]
        public UI_GamePanel_NightMarker MarkerTemplate;
        [Sirenix.OdinInspector.LabelText("奖励模板")]
        public TextMeshProUGUI RewardTemplate;
        [Sirenix.OdinInspector.LabelText("奖励文字坐标空间")]
        public RectTransform RewardSpace;
        public void ValidateConfiguration()
        {
            if (MarkerRoot == null || MarkerTemplate == null || RewardTemplate == null || RewardSpace == null)
                throw new InvalidOperationException("夜间 HUD 检查器引用不完整。");
        }
    }
}
