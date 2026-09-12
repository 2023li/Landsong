using System;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetails_Block_其他 : UI_GamePanel_BuildingDetails_Block
    {
        public RectTransform Rows => (RectTransform)transform;

        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (!(transform is RectTransform))
                throw new InvalidOperationException(name + " 模块必须使用 RectTransform。");
        }
    }
}
