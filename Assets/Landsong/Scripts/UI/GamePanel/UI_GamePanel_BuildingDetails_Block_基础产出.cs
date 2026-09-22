using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetails_Block_基础产出 : UI_GamePanel_BuildingDetails_Block
    {
        [LabelText("基础产出"), Required]
        public TMP_Text Label;
        [LabelText("布局"), Required]
        public LayoutElement Layout;
        [LabelText("侧栏触发器"), Required]
        public UI_GamePanel_BuildingDetails_SidebarTrigger Hover;
        public void ValidateConfiguration()
        {
            if (View == null || Hover == null)
                throw new InvalidOperationException("基础产出模块缺少建筑详情或侧栏触发器。");
            if (Hover.View != View)
                throw new InvalidOperationException("基础产出模块侧栏触发器没有绑定所属建筑详情。");
        }

        public void Refresh(IReadOnlyList<string> outputs, Func<string> sidebarContent)
        {
            bool visible = outputs != null && outputs.Count > 0;
            gameObject.SetActive(visible);
            BindSidebar(Hover, visible ? sidebarContent : null);
            if (!visible)
                return;

            int characters = 0;
            for (int i = 0; i < outputs.Count; i++)
                characters += outputs[i].Length;
            Label.text = "基础产出\n" + string.Join(" · ", outputs);
            Layout.preferredHeight = Mathf.Max(94, 38 + Mathf.Ceil(characters / 22f) * 24);
        }
    }
}
