using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_WorkerInfoRow : UI_GamePanel_Row
    {
        [LabelText("工人悬浮信息"), Required]
        public UI_GamePanel_BuildingDetails_SidebarTrigger WorkerHover;

        public override void ResetPresentation()
        {
            base.ResetPresentation();
            WorkerHover.Content = null;
        }
    }
}
