using System;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_WorkerInfoRow : UI_GamePanel_Row
    {
        [Sirenix.OdinInspector.LabelText("工人悬浮信息")]
        public UI_GamePanel_BuildingDetails_SidebarTrigger WorkerHover;
        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (WorkerHover == null)
                throw new InvalidOperationException("工人信息行缺少悬浮组件。");
            WorkerHover.ValidateConfiguration();
        }

        public override void ResetPresentation()
        {
            base.ResetPresentation();
            WorkerHover.Content = null;
        }
    }
}
