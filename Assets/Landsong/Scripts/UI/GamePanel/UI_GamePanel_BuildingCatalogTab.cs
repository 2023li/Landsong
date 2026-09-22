using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingCatalogTab : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("选择")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("文字")]
        public TMP_Text Label;
        public void ValidateConfiguration()
        {
            if (Select == null || Label == null)
                throw new InvalidOperationException("建筑目录分类模板检查器引用不完整。");
        }
    }
}
