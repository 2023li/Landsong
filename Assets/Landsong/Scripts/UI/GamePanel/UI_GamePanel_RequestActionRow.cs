using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_RequestActionRow : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("按钮")]
        public Button Button;
        [Sirenix.OdinInspector.LabelText("文字")]
        public TMP_Text Label;
    }
}
