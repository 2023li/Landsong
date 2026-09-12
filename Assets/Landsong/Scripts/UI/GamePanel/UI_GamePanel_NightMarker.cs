using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_NightMarker : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("交互状态")]
        public UI_GamePanel_InteractionLock Interaction;
        [Sirenix.OdinInspector.LabelText("选择")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("背景")]
        public Image Background;
        [Sirenix.OdinInspector.LabelText("文字")]
        public TextMeshProUGUI Label;
    }
}
