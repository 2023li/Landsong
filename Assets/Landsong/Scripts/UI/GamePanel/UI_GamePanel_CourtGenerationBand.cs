using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_CourtGenerationBand : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("背景")]
        public Image Background;
        [Sirenix.OdinInspector.LabelText("文字")]
        public TextMeshProUGUI Label;
    }
}
