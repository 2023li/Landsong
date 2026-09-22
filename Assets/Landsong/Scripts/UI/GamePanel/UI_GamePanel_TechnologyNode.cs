using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using InputField = TMPro.TMP_InputField;
using Font = TMPro.TMP_FontAsset;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_TechnologyNode : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("选择")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("文字")]
        public Text Label;
        [Sirenix.OdinInspector.LabelText("图标")]
        public Image Icon;
        [Sirenix.OdinInspector.LabelText("选中信息")]
        public Outline Selection;
    }
}
