using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class CourtPresentationView
    {
        public Sprite CrownIcon;
        public int GenerationCount {get;private set;}
        public int FamilyCount {get;private set;}
        public int SuccessionEdgeCount=>links.Edges.Count(e=>e.Color==SuccessionColor);
        static readonly Color SuccessionColor=new Color(1,.73f,.22f);
        RectTransform familyBackground;
        string familySignature;
        sealed class Family
        {public ulong A,B;public int Generation;public Vector2 Position;public float Width;}
        static (ulong,ulong) Pair(ulong a,ulong b)=>a<b?(a,b):(b,a);
        void ClearFamily()
        {
            if(familyBackground==null)return;
            familyBackground.gameObject.SetActive(false);Destroy(familyBackground.gameObject);familyBackground=null;
            foreach(var b in buttons.Values){b.gameObject.SetActive(false);Destroy(b.gameObject);}buttons.Clear();
            familySignature=null;signature=null;
        }
        RectTransform Box(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {
            var r=InterfaceWidgets.Rect(name,parent,new Vector2(0,1),new Vector2(0,1));r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(position.x,-position.y)*zoom;r.sizeDelta=size*zoom;
            r.gameObject.AddComponent<Image>().color=color;r.GetComponent<Image>().raycastTarget=false;return r;
        }
        void ShowFamily(string title,List<CourtCard> cards)
        {
            var next=zoom+"/"+Scroll.viewport.rect.width+"/"+string.Join("|",cards.Select(c=>$"{c.Id}:{c.Parent}:{c.SecondParent}:{c.Spouse}:{c.Column}:{c.Title}:{c.Detail}:{c.Influence}:{c.Selected}:{c.Dead}:{c.Monarch}:{c.EverMonarch}:{c.RequestCount}"));
            if(next==familySignature){foreach(var card in cards)if(buttons.TryGetValue(card.Id,out var node))card.BindPortrait?.Invoke(node.transform.Find("Portrait").GetComponent<Image>());return;}
            bool first=familyBackground==null;var scroll=Scroll.normalizedPosition;
            ClearFamily();foreach(var old in buttons.Values){old.gameObject.SetActive(false);Destroy(old.gameObject);}buttons.Clear();signature=null;familySignature=next;Header.text=title;
            familyBackground=InterfaceWidgets.Rect("Generation bands and families",Scroll.content,Vector2.zero,Vector2.one);familyBackground.SetAsFirstSibling();
            links.rectTransform.SetAsLastSibling();
            var byId=cards.ToDictionary(c=>c.Id);var families=new Dictionary<(ulong,ulong),Family>();var memberships=new HashSet<ulong>();
            void Add(ulong a,ulong b)
            {
                if(a==0||!byId.ContainsKey(a))return;if(!byId.ContainsKey(b))b=0;var key=Pair(a,b);
                if(families.ContainsKey(key))return;
                families.Add(key,new Family{A=a,B=b,Generation=b==0?byId[a].Column:Mathf.Max(byId[a].Column,byId[b].Column),Width=b==0?166:326});memberships.Add(a);if(b!=0)memberships.Add(b);
            }
            foreach(var c in cards)if(c.Spouse!=0)Add(c.Id,c.Spouse);
            // Retain previous families after widowhood/remarriage using children's actual parent IDs.
            foreach(var c in cards)if(c.Parent!=0&&c.SecondParent!=0)Add(c.Parent,c.SecondParent);
            foreach(var c in cards)if(!memberships.Contains(c.Id))Add(c.Id,0);
            var rows=families.Values.GroupBy(f=>f.Generation).OrderBy(g=>g.Key).ToArray();GenerationCount=rows.Length;FamilyCount=families.Count;
            float width=Mathf.Max(Scroll.viewport.rect.width/zoom,Mathf.Max(600,rows.Select(g=>g.Sum(f=>f.Width+28)+60).DefaultIfEmpty(600).Max()));
            var positions=new Dictionary<ulong,Vector2>();int rowIndex=0;
            foreach(var row in rows)
            {
                float y=30+rowIndex++*260;var band=Box("Generation "+row.Key,familyBackground,new Vector2(0,y),new Vector2(width,214),new Color(.24f,.25f,.27f));
                var label=InterfaceWidgets.Text("第 "+(row.Key+1)+" 代",InterfaceWidgets.Rect("Generation label",band,new Vector2(0,.87f),Vector2.one),font,14);label.color=new Color(.7f,.72f,.76f);
                float x=30;
                foreach(var f in row.OrderBy(f=>byId[f.A].Parent).ThenBy(f=>f.A))
                {
                    f.Position=new Vector2(x,y+35);Box("Family "+f.A+" / "+f.B,familyBackground,f.Position,new Vector2(f.Width,166),new Color(.13f,.3f,.95f));
                    int index=0;foreach(var id in new[]{f.A,f.B})
                    {
                        if(id==0)continue;var c=byId[id];var pos=f.Position+new Vector2(5+160*index++,5);if(!positions.ContainsKey(id))positions.Add(id,pos);
                        var button=InterfaceWidgets.Button("",Scroll.content,font,c.Click,156);var rect=(RectTransform)button.transform;
                        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(pos.x,-pos.y)*zoom;rect.sizeDelta=new Vector2(156,156)*zoom;
                        // Duplicate historical spouses share one stable person action; own them under the family layer for cleanup.
                        if(!buttons.ContainsKey(id))buttons.Add(id,button);else rect.SetParent(familyBackground,false);
                        button.image.color=c.Dead?new Color(.43f,.44f,.46f):new Color(.93f,.94f,.95f);
                        if(c.Selected){var outline=button.gameObject.AddComponent<Outline>();outline.effectColor=SuccessionColor;outline.effectDistance=new Vector2(3,-3);}
                        var text=button.GetComponentInChildren<TMP_Text>();text.text="";
                        var prestige=InterfaceWidgets.Text("影响力 "+c.Influence.ToString("0.0"),InterfaceWidgets.Rect("Influence",rect,new Vector2(0,.8f),new Vector2(.82f,1)),font,14);prestige.color=new Color(.18f,.2f,.23f);
                        var name=InterfaceWidgets.Text(c.Title,InterfaceWidgets.Rect("Name",rect,new Vector2(0,.17f),new Vector2(1,.42f)),font,18);name.alignment=TextAlignmentOptions.Center;name.color=new Color(.1f,.12f,.15f);
                        var detail=InterfaceWidgets.Text(c.Detail,InterfaceWidgets.Rect("Identity",rect,Vector2.zero,new Vector2(1,.2f)),font,12);detail.alignment=TextAlignmentOptions.Center;detail.color=new Color(.25f,.27f,.3f);
                        var portrait=InterfaceWidgets.Rect("Portrait",rect,new Vector2(.12f,.36f),new Vector2(.88f,.83f)).gameObject.AddComponent<Image>();portrait.raycastTarget=false;c.BindPortrait?.Invoke(portrait);
                        var crown=InterfaceWidgets.Rect("Crown",rect,new Vector2(.8f,.8f),Vector2.one);crown.gameObject.SetActive(c.Monarch);
                        var crownImage=crown.gameObject.AddComponent<Image>();crownImage.sprite=CrownIcon;crownImage.color=SuccessionColor;crownImage.raycastTarget=false;
                        if(CrownIcon==null){var mark=InterfaceWidgets.Text("王",crown,font,14);mark.alignment=TextAlignmentOptions.Center;mark.margin=Vector4.zero;mark.color=new Color(.2f,.13f,.03f);}
                        var requests=InterfaceWidgets.Rect("Pending requests",rect,new Vector2(.71f,.55f),new Vector2(.99f,.77f));requests.gameObject.SetActive(c.RequestCount>0);
                        requests.gameObject.AddComponent<Image>().color=new Color(.75f,.32f,.08f);requests.GetComponent<Image>().raycastTarget=false;
                        var badge=InterfaceWidgets.Text("! "+c.RequestCount,requests,font,15);badge.margin=Vector4.zero;badge.alignment=TextAlignmentOptions.Center;
                    }
                    x+=f.Width+28;
                }
            }
            links.Edges.Clear();
            foreach(var c in cards.OrderBy(c=>c.EverMonarch))
            {
                if(!positions.TryGetValue(c.Id,out var end))continue;
                Vector2 start;
                if(families.TryGetValue(Pair(c.Parent,c.SecondParent),out var family))start=family.Position+new Vector2(family.Width/2,166);
                else if(positions.TryGetValue(c.Parent!=0?c.Parent:c.SecondParent,out var parent))start=parent+new Vector2(78,156);
                else continue;
                links.Edges.Add(new TechnologyConnections.Edge{From=new Vector2(start.x,-start.y)*zoom,To=new Vector2(end.x+78,-end.y)*zoom,Color=c.EverMonarch?SuccessionColor:new Color(.5f,.77f,.86f),Vertical=true});
            }
            Scroll.content.sizeDelta=new Vector2(width,Mathf.Max(260,rowIndex*260))*zoom;links.SetVerticesDirty();
            Scroll.normalizedPosition=first?new Vector2(0,1):scroll;
        }
    }
}
