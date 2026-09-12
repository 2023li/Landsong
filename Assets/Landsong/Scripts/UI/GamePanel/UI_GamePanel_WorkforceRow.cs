using System;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_WorkforceRow : UI_GamePanel_Row
    {
        [Sirenix.OdinInspector.LabelText("劳动力")]
        public UI_GamePanel_WorkforceScale Workforce;
        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (Workforce == null)
                throw new InvalidOperationException("岗位预算行缺少刻度组件。");
            Workforce.ValidateConfiguration();
        }
    }
}
