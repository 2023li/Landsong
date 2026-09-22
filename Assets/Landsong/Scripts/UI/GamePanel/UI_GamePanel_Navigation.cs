using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Navigation : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("返回")]
        public Button Back;
        [Sirenix.OdinInspector.LabelText("历史")]
        public Button History;
        public void ValidateConfiguration()
        {
            if (Back == null || History == null)
                throw new InvalidOperationException("界面导航检查器引用不完整。");
        }
    }
}
