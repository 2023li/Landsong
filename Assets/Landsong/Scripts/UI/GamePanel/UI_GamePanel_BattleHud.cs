using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BattleHud : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("防御状态")]
        public TextMeshProUGUI DefenseStatus;
        [Sirenix.OdinInspector.LabelText("操作提示")]
        public TextMeshProUGUI ControlsHint;
        [Sirenix.OdinInspector.LabelText("防御焦点")]
        public Button DefenseFocus;
        [Sirenix.OdinInspector.LabelText("焦点操作交互状态")]
        public UI_GamePanel_InteractionLock FocusInteraction;
        [Sirenix.OdinInspector.LabelText("英雄滚动视图")]
        public ScrollRect HeroScroll;
        [Sirenix.OdinInspector.LabelText("英雄卡片")]
        public RectTransform HeroCards;
        [Sirenix.OdinInspector.LabelText("英雄模板")]
        public UI_GamePanel_HeroHudItem HeroTemplate;
        public void ValidateConfiguration()
        {
            if (DefenseStatus == null || ControlsHint == null || DefenseFocus == null || HeroScroll == null || HeroCards == null || HeroTemplate == null || HeroTemplate.PortraitBinding == null || FocusInteraction == null || HeroTemplate.Interaction == null)
                throw new InvalidOperationException("战斗 HUD 检查器引用不完整。");
            HeroTemplate.PortraitBinding.ValidateConfiguration();
        }
    }
}
