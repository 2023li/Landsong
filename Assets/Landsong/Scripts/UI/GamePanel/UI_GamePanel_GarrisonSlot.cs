using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_GarrisonSlot : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("选择")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("空值文字")]
        public TMP_Text EmptyLabel;
        [Sirenix.OdinInspector.LabelText("名称文字")]
        public TMP_Text NameLabel;
        [Sirenix.OdinInspector.LabelText("肖像")]
        public Image Portrait;
        [Sirenix.OdinInspector.LabelText("肖像引用绑定")]
        public UI_Common_PortraitImageBinding PortraitBinding;
        public void ValidateConfiguration()
        {
            if (Select == null || EmptyLabel == null || NameLabel == null || Portrait == null || PortraitBinding == null)
                throw new InvalidOperationException("驻兵槽模板检查器引用不完整。");
            PortraitBinding.ValidateConfiguration();
        }
    }
}
