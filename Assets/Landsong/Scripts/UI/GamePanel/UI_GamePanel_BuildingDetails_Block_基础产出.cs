using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetails_Block_基础产出 : UI_GamePanel_BuildingDetails_Block
    {
        [Sirenix.OdinInspector.LabelText("基础产出")]
        public TMP_Text Label;
        [Sirenix.OdinInspector.LabelText("布局")]
        public LayoutElement Layout;
        [Sirenix.OdinInspector.LabelText("侧栏触发器")]
        public UI_GamePanel_BuildingDetails_SidebarTrigger Hover;

        public override void ValidateConfiguration()
        {
            ValidateReferences((Label, nameof(Label)), (Layout, nameof(Layout)), (Hover, nameof(Hover)));
            Hover.ValidateConfiguration();
            if (Hover.View != View)
                throw new InvalidOperationException(name + " 模块的侧栏目标错误。");
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
