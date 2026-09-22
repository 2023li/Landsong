using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_RoyalPersonSummary : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("肖像")]
        public Image Portrait;
        [Sirenix.OdinInspector.LabelText("肖像引用绑定")]
        public UI_Common_PortraitImageBinding PortraitBinding;
        [Sirenix.OdinInspector.LabelText("详情")]
        public TMP_Text Details;
    }
}
