using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetails_Block_种植 : UI_GamePanel_BuildingDetails_Block
    {
        [Sirenix.OdinInspector.LabelText("作物填充")]
        public Image Fill;
        [Sirenix.OdinInspector.LabelText("作物图标")]
        public Image Icon;
        [Sirenix.OdinInspector.LabelText("作物文字")]
        public TMP_Text Label;
        [Sirenix.OdinInspector.LabelText("选择作物")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("清除作物")]
        public Button Clear;
        [Sirenix.OdinInspector.LabelText("侧栏触发器")]
        public UI_GamePanel_BuildingDetails_SidebarTrigger Hover;

        public override void ValidateConfiguration()
        {
            ValidateReferences((Fill, nameof(Fill)), (Icon, nameof(Icon)), (Label, nameof(Label)),
                (Select, nameof(Select)), (Clear, nameof(Clear)), (Hover, nameof(Hover)));
            Hover.ValidateConfiguration();
            if (Hover.View != View)
                throw new InvalidOperationException(name + " 模块的侧栏目标错误。");
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            BindSidebar(Hover, null);
            Bind(Select, null);
            Bind(Clear, null);
        }

        public void Refresh(string label, float progress, Sprite icon, Action select, Action clear, Func<string> sidebarContent)
        {
            gameObject.SetActive(true);
            Label.text = label;
            Span(Fill, 0, progress);
            Icon.sprite = icon;
            Icon.enabled = Icon.sprite != null;
            Bind(Select, select);
            Bind(Clear, clear);
            BindSidebar(Hover, sidebarContent);
        }
    }
}
