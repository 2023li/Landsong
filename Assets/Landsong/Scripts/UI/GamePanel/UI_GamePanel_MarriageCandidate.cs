using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_MarriageCandidate : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("选择")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("文字")]
        public TMP_Text Label;
    }
}
