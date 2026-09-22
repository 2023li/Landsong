using System;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_SoldierRow : UI_GamePanel_Row
    {
        [Sirenix.OdinInspector.LabelText("士兵")]
        public UI_GamePanel_SoldierItem Soldier;
        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (Soldier == null)
                throw new InvalidOperationException("士兵行缺少士兵卡片。");
            Soldier.ValidateConfiguration();
        }
    }
}
