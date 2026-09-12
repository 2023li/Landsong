using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_CourtNode : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("选择")]
        public Button Select;
        [Sirenix.OdinInspector.LabelText("文字")]
        public TextMeshProUGUI Label;
        [Sirenix.OdinInspector.LabelText("影响力")]
        public TextMeshProUGUI Influence;
        [Sirenix.OdinInspector.LabelText("人物名称")]
        public TextMeshProUGUI PersonName;
        [Sirenix.OdinInspector.LabelText("详情")]
        public TextMeshProUGUI Detail;
        [Sirenix.OdinInspector.LabelText("王冠备用")]
        public TextMeshProUGUI CrownFallback;
        [Sirenix.OdinInspector.LabelText("请求文字")]
        public TextMeshProUGUI RequestLabel;
        [Sirenix.OdinInspector.LabelText("肖像")]
        public Image Portrait;
        [Sirenix.OdinInspector.LabelText("王冠")]
        public Image Crown;
        [Sirenix.OdinInspector.LabelText("请求背景")]
        public Image RequestBackground;
        [Sirenix.OdinInspector.LabelText("肖像引用绑定")]
        public UI_Common_PortraitImageBinding PortraitBinding;
        [Sirenix.OdinInspector.LabelText("已选轮廓")]
        public Outline SelectedOutline;
        [Sirenix.OdinInspector.LabelText("王冠根对象")]
        public GameObject CrownRoot;
        [Sirenix.OdinInspector.LabelText("请求根对象")]
        public GameObject RequestRoot;
        public void ValidateConfiguration(bool family)
        {
            if (Select == null || Portrait == null || PortraitBinding == null)
                throw new InvalidOperationException("王室人物模板检查器引用不完整。");
            PortraitBinding.ValidateConfiguration();
            if (!family && Label == null)
                throw new InvalidOperationException("王室普通人物模板缺少文字引用。");
            if (family && (Influence == null || PersonName == null || Detail == null || SelectedOutline == null || CrownRoot == null || RequestRoot == null || RequestLabel == null))
                throw new InvalidOperationException("王室家谱人物模板检查器引用不完整。");
        }
    }
}
