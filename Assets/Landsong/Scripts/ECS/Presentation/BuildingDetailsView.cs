using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    // Stable controls preserve input focus while the simulation refreshes their read models.
    public sealed class BuildingDetailsView : MonoBehaviour
    {
        public TMP_InputField Name;
        public Image Icon,ExperienceFill,CropFill,CropIcon;
        public TMP_Text Level,Experience,BaseOutput,Footer,Jobs,Budget,Attraction,CropLabel;
        public Button Close,Style,Upgrade,Warning,Increase,Decrease,Crop,ClearCrop;
        public RectTransform Rows,GarrisonSlots;public GameObject JobsBlock,CropBlock,GarrisonBlock;public TMP_Text GarrisonLabel;public Button AdjustGarrison;
        public GameObject Tooltip;public TMP_Text TooltipText;
        public Image NaturalFill,SubsidyFill;public readonly List<Image> JobTicks=new List<Image>();
        public ulong BuildingId {get;private set;}
        TMP_FontAsset font;string warningText;
        static readonly Color Panel=new Color(.10f,.16f,.15f,1);
        static RectTransform Area(string name,Transform parent,Vector2 min,Vector2 max)=>InterfaceWidgets.Rect(name,parent,min,max);
        static void Offsets(RectTransform rect,float left,float bottom,float right,float top){rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(right,top);}
        public static BuildingDetailsView Create(GameObject panel,TMP_FontAsset font,TMP_InputField name,Button close)
        {
            foreach(Transform child in panel.transform)child.gameObject.SetActive(false);
            var rect=(RectTransform)panel.transform;rect.anchorMin=new Vector2(.68f,.055f);rect.anchorMax=new Vector2(.995f,.845f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            panel.GetComponent<Image>().color=Panel;
            var view=panel.AddComponent<BuildingDetailsView>();view.font=font;view.Name=name;view.Close=close;
            var header=Area("Building identity",rect,new Vector2(0,1),Vector2.one);header.pivot=new Vector2(.5f,1);header.sizeDelta=new Vector2(-20,166);header.anchoredPosition=new Vector2(0,-12);
            var art=Area("Building icon",header,new Vector2(0,1),new Vector2(0,1));art.pivot=new Vector2(0,1);art.sizeDelta=new Vector2(108,108);view.Icon=art.gameObject.AddComponent<Image>();view.Icon.preserveAspect=true;
            view.Style=InterfaceWidgets.Button("style",Area("Style",header,new Vector2(0,1),new Vector2(0,1)),font,null,28);var style=(RectTransform)view.Style.transform;style.anchorMin=style.anchorMax=new Vector2(0,1);style.pivot=new Vector2(0,1);style.anchoredPosition=new Vector2(12,-110);style.sizeDelta=new Vector2(84,28);
            var title=Area("Name field",header,new Vector2(0,1),Vector2.one);title.pivot=new Vector2(.5f,1);Offsets(title,118,-38,-36,0);
            name.transform.SetParent(title,false);var nr=(RectTransform)name.transform;nr.anchorMin=Vector2.zero;nr.anchorMax=Vector2.one;nr.offsetMin=nr.offsetMax=Vector2.zero;name.gameObject.SetActive(true);name.textComponent.fontSize=23;
            view.Warning=InterfaceWidgets.Button("!",Area("Warning",header,new Vector2(1,1),Vector2.one),font,null,28);var wr=(RectTransform)view.Warning.transform;wr.pivot=new Vector2(1,1);wr.sizeDelta=new Vector2(30,32);view.Warning.image.color=new Color(.9f,.08f,.06f);view.Warning.interactable=true;
            var hover=view.Warning.gameObject.AddComponent<BuildingDetailHover>();hover.View=view;
            view.Level=InterfaceWidgets.Text("",Area("Level",header,new Vector2(0,1),Vector2.one),font,22);Offsets(view.Level.rectTransform,115,-82,-135,-43);
            view.Upgrade=InterfaceWidgets.Button("升级",Area("Upgrade",header,new Vector2(1,1),Vector2.one),font,null,30);var ur=(RectTransform)view.Upgrade.transform;ur.pivot=new Vector2(1,1);ur.anchoredPosition=new Vector2(0,-43);ur.sizeDelta=new Vector2(98,32);
            var xp=Area("Experience",header,new Vector2(0,1),Vector2.one);Offsets(xp,118,-109,0,-83);xp.gameObject.AddComponent<Image>().color=new Color(.72f,.75f,.76f);
            view.ExperienceFill=Fill("Experience fill",xp,new Color(.2f,.32f,.95f));view.Experience=InterfaceWidgets.Text("",xp,font,16);view.Experience.alignment=TextAlignmentOptions.Center;
            var content=InterfaceWidgets.Scroll(rect,"Building modules",Vector2.zero,Vector2.one);var scroll=(RectTransform)content.parent.parent;Offsets(scroll,10,42,-10,-164);scroll.GetComponent<Image>().color=Panel;
            RectTransform Module(string label,float height){var r=Area(label,content,Vector2.zero,Vector2.one);r.gameObject.AddComponent<LayoutElement>().preferredHeight=height;r.gameObject.AddComponent<Image>().color=new Color(.23f,.27f,.26f);return r;}
            var output=Module("Base output",94);view.BaseOutput=InterfaceWidgets.Text("",output,font,16);
            var garrison=Module("Garrison",164);view.GarrisonBlock=garrison.gameObject;
            view.GarrisonLabel=InterfaceWidgets.Text("",Area("Garrison count",garrison,new Vector2(0,.75f),new Vector2(.4f,1)),font,20);
            view.AdjustGarrison=InterfaceWidgets.Button("调整驻军",Area("Manage garrison",garrison,new Vector2(.43f,.77f),new Vector2(.97f,.98f)),font,null,32);
            var strip=Area("Garrison strip",garrison,new Vector2(.025f,.04f),new Vector2(.975f,.72f));strip.gameObject.AddComponent<Image>().color=new Color(.55f,.55f,.55f);
            var horizontal=strip.gameObject.AddComponent<ScrollRect>();horizontal.horizontal=true;horizontal.vertical=false;horizontal.movementType=ScrollRect.MovementType.Clamped;horizontal.scrollSensitivity=30;
            var viewport=Area("Viewport",strip,Vector2.zero,Vector2.one);viewport.gameObject.AddComponent<RectMask2D>();horizontal.viewport=viewport;
            view.GarrisonSlots=Area("Slots",viewport,Vector2.zero,new Vector2(0,1));view.GarrisonSlots.pivot=new Vector2(0,.5f);horizontal.content=view.GarrisonSlots;
            var jobs=Module("Workforce",132);view.JobsBlock=jobs.gameObject;
            view.Jobs=InterfaceWidgets.Text("",Area("Workers",jobs,new Vector2(0,.73f),new Vector2(.5f,1)),font,19);
            var budget=Area("Budget controls",jobs,new Vector2(.43f,.73f),Vector2.one);budget.gameObject.AddComponent<HorizontalLayoutGroup>().spacing=3;
            view.Increase=InterfaceWidgets.Button("<",budget,font,null,28);view.Budget=InterfaceWidgets.Text("",budget,font,15);view.Budget.gameObject.AddComponent<LayoutElement>().preferredWidth=85;view.Decrease=InterfaceWidgets.Button(">",budget,font,null,28);
            var track=Area("Attraction bar",jobs,new Vector2(.055f,.44f),new Vector2(.945f,.6f));track.gameObject.AddComponent<Image>().color=new Color(.35f,.37f,.37f);
            view.NaturalFill=Fill("Natural attraction",track,new Color(.95f,.95f,.95f));view.SubsidyFill=Fill("Paid subsidy attraction",track,new Color(1,.57f,.04f));
            for(int i=0;i<10;i++){var tick=Area("Job "+i,track,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();tick.raycastTarget=false;view.JobTicks.Add(tick);}
            view.Attraction=InterfaceWidgets.Text("",Area("Attraction explanation",jobs,new Vector2(.01f,0),new Vector2(.99f,.4f)),font,13);
            var crop=Module("Planting",114);view.CropBlock=crop.gameObject;view.CropLabel=InterfaceWidgets.Text("种植",Area("Crop name",crop,new Vector2(.01f,.68f),new Vector2(.99f,1)),font,19);view.CropLabel.alignment=TextAlignmentOptions.Center;
            var progress=Area("Maturity",crop,new Vector2(.15f,.33f),new Vector2(.87f,.52f));progress.gameObject.AddComponent<Image>().color=new Color(.73f,.75f,.74f);view.CropFill=Fill("Crop growth",progress,new Color(.26f,.62f,.30f));
            view.Crop=InterfaceWidgets.Button("",Area("Crop picker",crop,new Vector2(.04f,.23f),new Vector2(.19f,.64f)),font,null);view.Crop.image.color=Color.white;
            view.Crop.image.enabled=false;var circle=Area("Circle",view.Crop.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<BuildingCropCircle>();circle.color=Color.white;view.Crop.targetGraphic=circle;
            view.CropIcon=Area("Crop icon",view.Crop.transform,new Vector2(.13f,.13f),new Vector2(.87f,.87f)).gameObject.AddComponent<Image>();view.CropIcon.preserveAspect=true;view.CropIcon.raycastTarget=false;
            view.ClearCrop=InterfaceWidgets.Button("X",Area("Clear crop",crop,new Vector2(.88f,.28f),new Vector2(.98f,.60f)),font,null,28);
            view.Rows=Area("Other building modules",content,Vector2.zero,Vector2.one);var layout=view.Rows.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.spacing=4;view.Rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            view.Footer=InterfaceWidgets.Text("",Area("Coordinates and action power",rect,Vector2.zero,new Vector2(1,0)),font,15);view.Footer.rectTransform.pivot=new Vector2(.5f,0);view.Footer.rectTransform.sizeDelta=new Vector2(0,34);view.Footer.alignment=TextAlignmentOptions.Center;
            close.transform.SetParent(rect,false);var cr=(RectTransform)close.transform;cr.anchorMin=cr.anchorMax=Vector2.one;cr.pivot=Vector2.one;cr.anchoredPosition=new Vector2(0,32);cr.sizeDelta=new Vector2(38,32);close.gameObject.SetActive(true);close.GetComponentInChildren<TMP_Text>().text="X";
            var tip=Area("Building warnings",rect,new Vector2(-.8f,.53f),new Vector2(-.02f,.98f));tip.gameObject.AddComponent<Image>().color=new Color(.12f,.07f,.07f,.99f);view.Tooltip=tip.gameObject;view.TooltipText=InterfaceWidgets.Text("",tip,font,17);tip.gameObject.SetActive(false);
            view.Warning.onClick.AddListener(()=>view.ShowWarnings(!view.Tooltip.activeSelf));return view;
        }
        static Image Fill(string name,Transform parent,Color color){var image=Area(name,parent,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        public static void Span(Image image,float start,float end){image.rectTransform.anchorMin=new Vector2(Mathf.Clamp01(start),0);image.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(end),1);image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero;}
        public void Select(ulong id,string name){if(BuildingId!=id){BuildingId=id;ShowWarnings(false);Rows.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=1;}if(!Name.isFocused)Name.SetTextWithoutNotify(name);}
        public void SetWarnings(string value){warningText=value;Warning.gameObject.SetActive(value.Length>0);TooltipText.text=value;if(value.Length==0)ShowWarnings(false);}
        public void ShowWarnings(bool show){Tooltip.SetActive(show&&warningText.Length>0);if(Tooltip.activeSelf)Tooltip.transform.SetAsLastSibling();}
        void OnDisable(){if(Tooltip!=null)Tooltip.SetActive(false);}
        public void Workforce(WorkforceQuote q,string item,bool editable,Action<int> budget)
        {
            JobsBlock.SetActive(q.Capacity>0);if(q.Capacity<=0)return;Jobs.text=$"岗位：{q.Workers}/{q.Capacity}";Budget.text=$"补贴 {q.SubsidyCost}";
            Bind(Increase,editable&&!q.Locked&&q.SubsidyCost<q.Capacity?()=>budget(q.SubsidyCost+1):null);Bind(Decrease,editable&&!q.Locked&&q.SubsidyCost>0?()=>budget(q.SubsidyCost-1):null);
            Span(NaturalFill,0,q.Natural/100);Span(SubsidyFill,q.Natural/100,q.Current/100);
            int count=Mathf.Min(10,q.Capacity);for(int i=0;i<10;i++){var mark=JobTicks[i];mark.gameObject.SetActive(i<count);if(i>=count)continue;int jobs=Mathf.CeilToInt((i+1f)*q.Capacity/count);var r=mark.rectTransform;r.anchorMin=r.anchorMax=new Vector2((i+1f)/count,.5f);r.sizeDelta=new Vector2(5,28);r.anchoredPosition=Vector2.zero;mark.color=q.Workers>=jobs?new Color(1,.94f,.13f):new Color(.52f,.51f,.15f);}
            Attraction.text=$"自然 {q.Natural:0.#} + 已付补贴 {q.Current-q.Natural:0.#} / 100\n下次支付 {q.SubsidyCost} {item} · 付款后 {q.Planned:0.#}"+(q.Locked?" · 在途锁定":"");
        }
        public static void Bind(Button b,Action action){b.onClick.RemoveAllListeners();b.interactable=action!=null;if(action!=null)b.onClick.AddListener(()=>action());}
    }
    public sealed class BuildingDetailHover : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {public BuildingDetailsView View;public void OnPointerEnter(PointerEventData e)=>View.ShowWarnings(true);public void OnPointerExit(PointerEventData e)=>View.ShowWarnings(false);}
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BuildingCropCircle : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();var r=rectTransform.rect;var radius=Mathf.Min(r.width,r.height)/2;vh.AddVert(r.center,color,Vector2.zero);for(int i=0;i<=32;i++){float a=i*Mathf.PI*2/32;vh.AddVert(r.center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);if(i>0)vh.AddTriangle(0,i,i+1);}}
    }
}
