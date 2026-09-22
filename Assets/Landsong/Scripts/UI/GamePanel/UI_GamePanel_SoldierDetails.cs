using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_SoldierDetails : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("肖像")]
        public Image Portrait;
        [Sirenix.OdinInspector.LabelText("肖像引用绑定")]
        public UI_Common_PortraitImageBinding PortraitBinding;
        [Sirenix.OdinInspector.LabelText("名称")]
        public TMP_InputField Name;
        [Sirenix.OdinInspector.LabelText("年龄")]
        public TMP_Text Age;
        [Sirenix.OdinInspector.LabelText("属性")]
        public TMP_Text Stats;
        [Sirenix.OdinInspector.LabelText("能力")]
        public TMP_Text Abilities;
        [Sirenix.OdinInspector.LabelText("头盔")]
        public Button Helmet;
        [Sirenix.OdinInspector.LabelText("护甲")]
        public Button Armor;
        [Sirenix.OdinInspector.LabelText("武器")]
        public Button Weapon;
        [Sirenix.OdinInspector.LabelText("关闭")]
        public Button Close;
        [Sirenix.OdinInspector.LabelText("能力布局")]
        public LayoutElement AbilitiesLayout;
        public void ValidateConfiguration()
        {
            if (Portrait == null || PortraitBinding == null || Name == null || Age == null || Stats == null || Abilities == null || Helmet == null || Armor == null || Weapon == null || Close == null || AbilitiesLayout == null)
                throw new InvalidOperationException("士兵详情面板检查器引用不完整。");
            PortraitBinding.ValidateConfiguration();
            if (PortraitBinding.Target != Portrait)
                throw new InvalidOperationException("士兵详情肖像绑定必须指向面板的肖像图片。");
        }
    }
}
