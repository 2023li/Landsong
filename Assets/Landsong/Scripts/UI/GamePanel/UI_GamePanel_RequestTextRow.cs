using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_RequestTextRow : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("文字")]
        public TMP_Text Text;
        [Sirenix.OdinInspector.LabelText("布局")]
        public LayoutElement Layout;
    }
}
