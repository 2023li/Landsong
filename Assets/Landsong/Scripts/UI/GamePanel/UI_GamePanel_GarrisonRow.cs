using System;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_GarrisonRow : UI_GamePanel_Row
    {
        [Sirenix.OdinInspector.LabelText("组")]
        public UI_GamePanel_GarrisonGroup Group;
        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (Group == null)
                throw new InvalidOperationException("驻军分组行缺少分组组件。");
        }
    }
}
