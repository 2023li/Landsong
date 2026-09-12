using System;
using TMPro;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_QuantityRow : UI_GamePanel_Row
    {
        [Sirenix.OdinInspector.LabelText("数量")]
        public TMP_InputField Quantity;
        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (Quantity == null)
                throw new InvalidOperationException("数量行缺少输入框。");
        }
    }
}
