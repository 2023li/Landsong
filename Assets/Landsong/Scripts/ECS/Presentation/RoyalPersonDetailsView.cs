using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class RoyalPersonDetailsView : MonoBehaviour
    {
        public TextMeshProUGUI Identity,Description;public Image Portrait;
        public Button Designate,Execute,Marriage,Requests,OverviewTab,PersonTab;
        public RectTransform OverviewHost;
        public GameObject PersonContent;
        public ScrollRect DetailScroll;
        public ulong PersonId {get;private set;}
        public static RoyalPersonDetailsView Create(Transform parent,TMP_FontAsset font,Action<bool> overview)
        {
            var rect=InterfaceWidgets.Rect("Royal person details",parent,new Vector2(.72f,.02f),new Vector2(.99f,.91f));
            rect.gameObject.AddComponent<Image>().color=new Color(.035f,.045f,.055f,1);
            var view=rect.gameObject.AddComponent<RoyalPersonDetailsView>();
            var tabs=InterfaceWidgets.Rect("Tabs",rect,new Vector2(0,1),Vector2.one);tabs.pivot=new Vector2(.5f,1);tabs.sizeDelta=new Vector2(0,38);
            var layout=tabs.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=4;
            view.PersonTab=InterfaceWidgets.Button("人物详情",tabs,font,()=>overview(false),34);
            view.OverviewTab=InterfaceWidgets.Button("王朝事务",tabs,font,()=>overview(true),34);
            var body=InterfaceWidgets.Rect("Person",rect,Vector2.zero,Vector2.one);body.offsetMax=new Vector2(0,-42);view.PersonContent=body.gameObject;
            view.OverviewHost=InterfaceWidgets.Rect("Court affairs",rect,Vector2.zero,Vector2.one);view.OverviewHost.offsetMax=new Vector2(0,-42);
            var portraitSlot=InterfaceWidgets.Rect("Portrait area",body,new Vector2(.2f,.67f),new Vector2(.8f,.97f));
            var portrait=InterfaceWidgets.Rect("Portrait placeholder",portraitSlot,Vector2.zero,Vector2.one);view.Portrait=portrait.gameObject.AddComponent<Image>();view.Portrait.color=new Color(.94f,.95f,.96f);
            var aspect=portrait.gameObject.AddComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=1;
            view.Identity=InterfaceWidgets.Text("点击肖像查看人物",InterfaceWidgets.Rect("Identity",body,new Vector2(.02f,.59f),new Vector2(.98f,.67f)),font,20);
            var rows=InterfaceWidgets.Scroll(body,"Person information",new Vector2(.015f,.28f),new Vector2(.985f,.59f));view.DetailScroll=rows.GetComponentInParent<ScrollRect>();
            view.Description=InterfaceWidgets.Text("",rows,font,16);view.Description.gameObject.AddComponent<LayoutElement>();
            var actions=InterfaceWidgets.Rect("Person actions",body,new Vector2(.015f,.01f),new Vector2(.985f,.265f));
            var group=actions.gameObject.AddComponent<VerticalLayoutGroup>();group.spacing=3;group.childControlHeight=true;group.childForceExpandHeight=true;
            view.Designate=InterfaceWidgets.Button("立为储君",actions,font,null,30);
            view.Execute=InterfaceWidgets.Button("赐死",actions,font,null,30);
            view.Marriage=InterfaceWidgets.Button("赐婚",actions,font,null,30);
            view.Requests=InterfaceWidgets.Button("处理请求",actions,font,null,30);
            return view;
        }
        public void Show(ulong id,string identity,string description,Action designate,Action execute,Action marriage,bool overview,Action requests=null)
        {
            if(PersonId!=id){PersonId=id;DetailScroll.verticalNormalizedPosition=1;}
            Identity.text=identity;Description.text=description;
            Description.GetComponent<LayoutElement>().preferredHeight=Description.GetPreferredValues(description,Mathf.Max(160,DetailScroll.content.rect.width-24),float.PositiveInfinity).y+16;
            Bind(Designate,designate);Bind(Execute,execute);Bind(Marriage,marriage);Bind(Requests,requests);
            PersonContent.SetActive(!overview);OverviewHost.gameObject.SetActive(overview);
        }
        static void Bind(Button button,Action action)
        {button.onClick.RemoveAllListeners();button.interactable=action!=null;if(action!=null)button.onClick.AddListener(()=>action());}
    }
}
