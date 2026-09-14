using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_InventoryResource : MonoBehaviour
    {
        [LabelText("资源信息")] public TMP_Text Information;
        [LabelText("资源图标")] public Image Icon;
        [UnityEngine.Serialization.FormerlySerializedAs("Ledger"), LabelText("详情按钮")] public Button Details;
        [LabelText("交互状态")] public UI_GamePanel_InteractionLock Interaction;
        public int Item { get; internal set; }

        public void ValidateConfiguration()
        {
            if (Information == null || Icon == null || Details == null || Interaction == null)
                throw new InvalidOperationException("库存资源条目引用不完整。");
            Interaction.ValidateConfiguration();
        }
    }
}
