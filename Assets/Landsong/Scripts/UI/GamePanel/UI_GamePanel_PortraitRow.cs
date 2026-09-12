using System;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_PortraitRow : UI_GamePanel_Row
    {
        [Sirenix.OdinInspector.LabelText("肖像")]
        public Image Portrait;
        [Sirenix.OdinInspector.LabelText("肖像引用绑定")]
        public UI_Common_PortraitImageBinding PortraitBinding;
        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (Portrait == null || PortraitBinding == null)
                throw new InvalidOperationException("人物行肖像引用不完整。");
            PortraitBinding.ValidateConfiguration();
        }

        public override void ResetPresentation()
        {
            base.ResetPresentation();
            Label.margin = new Vector4(68, 4, 8, 4);
        }
    }
}
