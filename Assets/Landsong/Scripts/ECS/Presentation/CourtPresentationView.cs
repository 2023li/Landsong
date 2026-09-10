using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class CourtCard
    {public ulong Id,Parent,SecondParent,Spouse;public int Column,Row,RequestCount;public string Title,Detail;public Sprite Portrait;public bool Selected,Dead,Monarch,EverMonarch;public float Influence;public Action Click;public Action<Image> BindPortrait;}
    public sealed partial class CourtPresentationView : MonoBehaviour
    {
        public ScrollRect Scroll;public TextMeshProUGUI Header;public Button CloseButton;TMP_FontAsset font;TechnologyConnections links;
        readonly Dictionary<ulong,Button> buttons=new Dictionary<ulong,Button>();string signature;float zoom=1;
        public int NodeCount=>buttons.Count;public int EdgeCount=>links.Edges.Count;
        public Button Node(ulong id)=>buttons.TryGetValue(id,out var button)?button:null;
        public static CourtPresentationView Create(Transform parent,TMP_FontAsset font,Action close)
        {
            var rect=InterfaceWidgets.Rect("Court presentation",parent,new Vector2(.31f,.04f),new Vector2(.99f,.80f));rect.gameObject.AddComponent<Image>().color=new Color(.05f,.08f,.11f,.99f);
            var view=rect.gameObject.AddComponent<CourtPresentationView>();view.font=font;
            view.Header=InterfaceWidgets.Text("",InterfaceWidgets.Rect("Title",rect,new Vector2(.02f,.91f),new Vector2(.7f,1)),font);
            var controls=InterfaceWidgets.Rect("Zoom",rect,new Vector2(.72f,.92f),new Vector2(.98f,.99f));controls.gameObject.AddComponent<HorizontalLayoutGroup>();
            InterfaceWidgets.Button("−",controls,font,()=>view.Zoom(-.1f));InterfaceWidgets.Button("+",controls,font,()=>view.Zoom(.1f));
            view.CloseButton=InterfaceWidgets.Button("关闭",controls,font,close);
            view.Scroll=TechnologyTreeView.Scroll("Family graph",rect,new Vector2(.01f,.02f),new Vector2(.99f,.91f),true);
            var lineRect=InterfaceWidgets.Rect("Relationships",view.Scroll.content,Vector2.zero,Vector2.one);view.links=lineRect.gameObject.AddComponent<TechnologyConnections>();view.links.raycastTarget=false;view.links.rectTransform.pivot=new Vector2(0,1);
            return view;
        }
        void Zoom(float amount){zoom=Mathf.Clamp(zoom+amount,.65f,1.3f);signature=null;}
        public void Show(string mode,List<CourtCard> cards,bool family=false)
        {
            var bounds=(RectTransform)transform;bounds.anchorMin=new Vector2(family ? .01f : .31f,.04f);
            var graph=(RectTransform)Scroll.transform;graph.anchorMax=new Vector2(family ? .705f : .99f,.91f);
            if(family){ShowFamily(mode,cards);return;}
            ClearFamily();
            string next=mode+"/"+zoom+"/"+string.Join("|",cards.Select(c=>$"{c.Id}:{c.Parent}:{c.SecondParent}:{c.Spouse}:{c.Column}:{c.Row}:{c.Title}:{c.Detail}:{c.Selected}:{c.Dead}:{c.Portrait?.GetInstanceID()}"));if(signature==next)return;signature=next;Header.text=mode;
            var present=new HashSet<ulong>(cards.Select(c=>c.Id));foreach(var id in buttons.Keys.Where(id=>!present.Contains(id)).ToArray()){Destroy(buttons[id].gameObject);buttons.Remove(id);}
            var positions=new Dictionary<ulong,Vector2>();var size=new Vector2(500,240);
            foreach(var card in cards)
            {
                if(!buttons.TryGetValue(card.Id,out var button))
                {button=InterfaceWidgets.Button("",Scroll.content,font,null,130);buttons.Add(card.Id,button);var image=InterfaceWidgets.Rect("Portrait",button.transform,new Vector2(.03f,.28f),new Vector2(.27f,.85f)).gameObject.AddComponent<Image>();image.preserveAspect=true;image.raycastTarget=false;button.GetComponentInChildren<TMP_Text>().margin=new Vector4(62,7,6,7);}
                var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);var position=new Vector2(20+card.Column*250,20+card.Row*160);positions.Add(card.Id,position);rect.anchoredPosition=new Vector2(position.x,-position.y)*zoom;rect.sizeDelta=new Vector2(220,130)*zoom;size=Vector2.Max(size,position+new Vector2(250,170));
                button.GetComponentInChildren<TMP_Text>().text=card.Title+"\n"+card.Detail;button.GetComponentInChildren<TMP_Text>().fontSize=17*zoom;
                var portrait=button.transform.Find("Portrait").GetComponent<Image>();portrait.sprite=card.Portrait;portrait.color=card.Portrait!=null?(card.Dead?Color.gray:Color.white):card.Dead?Color.gray:new Color(.7f,.65f,.45f);card.BindPortrait?.Invoke(portrait);
                button.image.color=card.Selected?new Color(.2f,.45f,.5f):card.Dead?new Color(.18f,.18f,.2f):new Color(.15f,.23f,.32f);
                button.onClick.RemoveAllListeners();button.interactable=card.Click!=null;if(card.Click!=null)button.onClick.AddListener(()=>card.Click());
            }
            Scroll.content.sizeDelta=size*zoom;links.Edges.Clear();
            foreach(var card in cards)
            {
                foreach(var parent in new[]{card.Parent,card.SecondParent}.Distinct())if(parent!=0&&positions.TryGetValue(parent,out var start))links.Edges.Add(new TechnologyConnections.Edge {From=new Vector2(start.x+220,-start.y-65)*zoom,To=new Vector2(positions[card.Id].x,-positions[card.Id].y-65)*zoom,Color=Color.cyan});
                if(card.Spouse>card.Id&&positions.TryGetValue(card.Spouse,out var mate)){var start=positions[card.Id];links.Edges.Add(new TechnologyConnections.Edge{From=new Vector2(start.x+220,-start.y-90)*zoom,To=new Vector2(mate.x+220,-mate.y-90)*zoom,Color=new Color(1,.7f,.3f)});}
            }links.SetVerticesDirty();
        }
    }
}
