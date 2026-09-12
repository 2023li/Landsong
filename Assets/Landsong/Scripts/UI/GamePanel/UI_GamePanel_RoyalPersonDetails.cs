using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_RoyalPersonDetails : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("身份")]
        public TextMeshProUGUI Identity;
        [Sirenix.OdinInspector.LabelText("描述")]
        public TextMeshProUGUI Description;
        [Sirenix.OdinInspector.LabelText("肖像")]
        public Image Portrait;
        [Sirenix.OdinInspector.LabelText("肖像引用绑定")]
        public UI_Common_PortraitImageBinding PortraitBinding;
        [Sirenix.OdinInspector.LabelText("指定")]
        public Button Designate;
        [Sirenix.OdinInspector.LabelText("执行")]
        public Button Execute;
        [Sirenix.OdinInspector.LabelText("婚姻")]
        public Button Marriage;
        [Sirenix.OdinInspector.LabelText("请求")]
        public Button Requests;
        [Sirenix.OdinInspector.LabelText("总览页签")]
        public Button OverviewTab;
        [Sirenix.OdinInspector.LabelText("人物页签")]
        public Button PersonTab;
        [Sirenix.OdinInspector.LabelText("总览容器")]
        public RectTransform OverviewHost;
        [Sirenix.OdinInspector.LabelText("人物内容")]
        public GameObject PersonContent;
        [Sirenix.OdinInspector.LabelText("详情滚动视图")]
        public ScrollRect DetailScroll;
        [Sirenix.OdinInspector.LabelText("描述布局")]
        public LayoutElement DescriptionLayout;
        public ulong PersonId { get; private set; }

        public void BindTabs(Action<bool> overview)
        {
            ValidateConfiguration();
            PersonTab.onClick.RemoveAllListeners();
            PersonTab.onClick.AddListener(() => overview(false));
            OverviewTab.onClick.RemoveAllListeners();
            OverviewTab.onClick.AddListener(() => overview(true));
        }

        public void ValidateConfiguration()
        {
            if (Identity == null || Description == null || Portrait == null || PortraitBinding == null || Designate == null || Execute == null || Marriage == null || Requests == null || OverviewTab == null || PersonTab == null || OverviewHost == null || PersonContent == null || DetailScroll == null || DescriptionLayout == null)
                throw new InvalidOperationException("王室人物详情面板检查器引用不完整。");
            PortraitBinding.ValidateConfiguration();
        }

        public void Show(ulong id, string identity, string description, Action designate, Action execute, Action marriage, bool overview, Action requests = null)
        {
            if (PersonId != id)
            {
                PersonId = id;
                DetailScroll.verticalNormalizedPosition = 1;
            }

            Identity.text = identity;
            Description.text = description;
            DescriptionLayout.preferredHeight = Description.GetPreferredValues(description, Mathf.Max(160, DetailScroll.content.rect.width - 24), float.PositiveInfinity).y + 16;
            Bind(Designate, designate);
            Bind(Execute, execute);
            Bind(Marriage, marriage);
            Bind(Requests, requests);
            PersonContent.SetActive(!overview);
            OverviewHost.gameObject.SetActive(overview);
        }

        static void Bind(Button button, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = action != null;
            if (action != null)
                button.onClick.AddListener(() => action());
        }
    }
}
