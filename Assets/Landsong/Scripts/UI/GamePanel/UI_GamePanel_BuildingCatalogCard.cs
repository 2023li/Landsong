using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingCatalogCard : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("选择")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("图标")]
        public Image Icon;
        [Sirenix.OdinInspector.LabelText("备用")]
        public TMP_Text Fallback;
        [Sirenix.OdinInspector.LabelText("文字")]
        public TMP_Text Label;
        [Sirenix.OdinInspector.LabelText("悬浮信息")]
        public UI_GamePanel_BuildingCatalogHover Hover;
        public void ValidateConfiguration()
        {
            if (Select == null || Icon == null || Fallback == null || Label == null || Hover == null)
                throw new InvalidOperationException("建筑目录卡片模板检查器引用不完整。");
        }
    }
}
