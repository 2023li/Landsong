using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetails_Block_种植 : UI_GamePanel_BuildingDetails_Block
    {
        [LabelText("作物填充"), Required]
        public Image Fill;
        [LabelText("作物图标"), Required]
        public Image Icon;
        [LabelText("作物文字"), Required]
        public TMP_Text Label;
        [LabelText("选择作物"), Required]
        public Button Select;
        [LabelText("清除作物"), Required]
        public Button Clear;
        [LabelText("侧栏触发器"), Required]
        public UI_GamePanel_BuildingDetails_SidebarTrigger Hover;
        public void ValidateConfiguration()
        {
            if (View == null || Hover == null)
                throw new InvalidOperationException("种植模块缺少建筑详情或侧栏触发器。");
            if (Hover.View != View)
                throw new InvalidOperationException("种植模块侧栏触发器没有绑定所属建筑详情。");
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
