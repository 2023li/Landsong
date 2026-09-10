using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using InputField = TMPro.TMP_InputField;
using Font = TMPro.TMP_FontAsset;

namespace Landsong.ECS.Presentation
{
    public sealed class TechnologyNodeModel
    {
        public int Definition;
        public string Name;
        public Vector2 Position;
        public Sprite Icon;
        public ResearchQuote Quote;
    }
    // UGUI rendering only. Nodes never retain Entity handles or mutate research state.
    public sealed class TechnologyTreeView : MonoBehaviour
    {
        public Text Header;
        public Button CloseButton, CurrentButton;
        public InputField Search;
        public ScrollRect GraphScroll;
        public RectTransform DetailRows;
        public int Selected = -1;
        public int NodeCount => buttons.Count;
        public int EdgeCount => lines.Edges.Count;
        readonly Dictionary<int, Button> buttons = new Dictionary<int, Button>();
        readonly Dictionary<int, TechnologyNodeModel> models = new Dictionary<int, TechnologyNodeModel>();
        TechnologyConnections lines;
        Font font;
        float zoom = 1;
        Action<int> selected;
        public Button NodeButton(int definition) => buttons.TryGetValue(definition, out var b) ? b : null;
        public static TechnologyTreeView Create(Transform parent, Font font, Action<int> select, Action close)
        {
            var rect = Rect("Technology Window", parent, new Vector2(.01f, .06f), new Vector2(.99f, .84f));
            Background(rect, new Color(.055f, .08f, .12f, 1)); var view = rect.gameObject.AddComponent<TechnologyTreeView>(); view.font = font; view.selected = select;
            var header = Rect("Header", rect, new Vector2(0, 1), Vector2.one); header.offsetMin = new Vector2(12, -48); header.offsetMax = new Vector2(-12, 0);
            view.Header = Label("科技", header, font); view.Header.rectTransform.offsetMax = new Vector2(-530, 0);
            view.CurrentButton = Button("当前研究", header, font); Place(view.CurrentButton.transform, new Vector2(1, .5f), new Vector2(-450, 0), new Vector2(100, 34));
            var minus = Button("－", header, font); Place(minus.transform, new Vector2(1, .5f), new Vector2(-375, 0), new Vector2(42, 34));
            var plus = Button("＋", header, font); Place(plus.transform, new Vector2(1, .5f), new Vector2(-325, 0), new Vector2(42, 34));
            var searchRect = Rect("Search", header, Vector2.zero, Vector2.one); Place(searchRect, new Vector2(1, .5f), new Vector2(-190, 0), new Vector2(200, 34)); Background(searchRect, new Color(.18f, .23f, .3f));
            view.Search = searchRect.gameObject.AddComponent<InputField>(); view.Search.textViewport = view.Search.GetComponent<RectTransform>(); view.Search.textComponent = Label("", searchRect, font); var placeholder = Label("搜索科技名称", searchRect, font); placeholder.color = Color.gray; view.Search.placeholder = placeholder;
            view.CloseButton = Button("关闭", header, font); Place(view.CloseButton.transform, new Vector2(1, .5f), new Vector2(-40, 0), new Vector2(75, 34)); view.CloseButton.onClick.AddListener(() => close());
            view.GraphScroll = Scroll("Graph", rect, new Vector2(.01f, .02f), new Vector2(.7f, .91f), true);
            view.lines = Rect("Connections", view.GraphScroll.content, Vector2.zero, Vector2.one).gameObject.AddComponent<TechnologyConnections>(); view.lines.raycastTarget = false;
            view.DetailRows = Scroll("Details", rect, new Vector2(.715f, .02f), new Vector2(.99f, .91f), false).content;
            var layout = view.DetailRows.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.spacing = 5;
            view.DetailRows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.Search.onValueChanged.AddListener(value => { if (string.IsNullOrWhiteSpace(value)) return; foreach (var m in view.models.Values) if (m.Name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0) { select(m.Definition); view.Focus(m.Definition); break; } });
            minus.onClick.AddListener(() => { view.zoom = Mathf.Max(.5f, view.zoom - .15f); view.Layout(); view.Focus(view.Selected); });
            plus.onClick.AddListener(() => { view.zoom = Mathf.Min(1.5f, view.zoom + .15f); view.Layout(); view.Focus(view.Selected); });
            return view;
        }
        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var r = (RectTransform)go.transform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; return r; }
        static void Background(RectTransform r, Color color) => r.gameObject.AddComponent<Image>().color = color;
        static void Place(Transform transform, Vector2 anchor, Vector2 position, Vector2 size)
        { var r = (RectTransform)transform; r.anchorMin = r.anchorMax = anchor; r.anchoredPosition = position; r.sizeDelta = size; }
        static Text Label(string text, Transform parent, Font font)
        { var r = Rect("Label", parent, Vector2.zero, Vector2.one); r.offsetMin = new Vector2(8, 3); r.offsetMax = new Vector2(-8, -3); var t = r.gameObject.AddComponent<Text>(); t.textWrappingMode = TMPro.TextWrappingModes.Normal; t.font = font; t.fontSize = 17; t.color = Color.white; t.text = text; t.alignment = TMPro.TextAlignmentOptions.Left; t.raycastTarget = false; return t; }
        static Button Button(string text, Transform parent, Font font)
        { var r = Rect(text, parent, Vector2.zero, Vector2.one); Background(r, new Color(.19f, .26f, .35f)); var b = r.gameObject.AddComponent<Button>(); Label(text, r, font).alignment = TMPro.TextAlignmentOptions.Center; return b; }
        public static ScrollRect Scroll(string name, Transform parent, Vector2 min, Vector2 max, bool horizontal)
        {
            var r = Rect(name, parent, min, max); Background(r, new Color(.075f, .1f, .14f)); var scroll = r.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = horizontal; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36;
            var viewport = Rect("Viewport", r, Vector2.zero, Vector2.one); viewport.offsetMin = new Vector2(0, horizontal ? 16 : 0); viewport.offsetMax = new Vector2(-16, 0); viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport; scroll.content = Rect("Content", viewport, new Vector2(0, 1), horizontal ? new Vector2(0, 1) : new Vector2(1, 1)); scroll.content.pivot = new Vector2(0, 1);
            var v = DefaultControls.CreateScrollbar(new DefaultControls.Resources()); v.transform.SetParent(r, false); var vr = (RectTransform)v.transform; vr.anchorMin = new Vector2(1, 0); vr.anchorMax = Vector2.one; vr.offsetMin = new Vector2(-14, 16); vr.offsetMax = Vector2.zero; var vb = v.GetComponent<Scrollbar>(); vb.direction = Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar = vb;
            if (horizontal) { var h = DefaultControls.CreateScrollbar(new DefaultControls.Resources()); h.transform.SetParent(r, false); var hr = (RectTransform)h.transform; hr.anchorMin = Vector2.zero; hr.anchorMax = new Vector2(1, 0); hr.offsetMin = Vector2.zero; hr.offsetMax = new Vector2(-16, 14); scroll.horizontalScrollbar = h.GetComponent<Scrollbar>(); }
            Style(scroll.verticalScrollbar); if (scroll.horizontalScrollbar != null) Style(scroll.horizontalScrollbar);
            return scroll;
        }
        static void Style(Scrollbar bar)
        { bar.GetComponent<Image>().color = new Color(.12f, .16f, .21f); bar.targetGraphic.color = new Color(.48f, .6f, .72f); }
        public void Bind(List<TechnologyNodeModel> nodes, int selection, int points, int current)
        {
            Selected = selection; Header.text = "科技树 · 可用研究点 " + points;
            CurrentButton.onClick.RemoveAllListeners(); CurrentButton.interactable = current >= 0; if (current >= 0) CurrentButton.onClick.AddListener(() => { selected(current); Focus(current); });
            models.Clear(); foreach (var m in nodes) models.Add(m.Definition, m);
            foreach (var pair in buttons) pair.Value.gameObject.SetActive(models.ContainsKey(pair.Key));
            foreach (var m in nodes)
            {
                if (!buttons.TryGetValue(m.Definition, out var button))
                {
                    button = Button(m.Name, GraphScroll.content, font); buttons.Add(m.Definition, button); var at = m.Definition; button.onClick.AddListener(() => selected(at)); button.gameObject.AddComponent<Outline>().effectColor = Color.white;
                    var icon = Rect("Icon", button.transform, new Vector2(0, .5f), new Vector2(0, .5f)); icon.anchoredPosition = new Vector2(22, 0); icon.sizeDelta = new Vector2(30, 30); var graphic = icon.gameObject.AddComponent<Image>(); graphic.preserveAspect = true; graphic.raycastTarget = false;
                }
                button.gameObject.SetActive(true); button.GetComponent<Outline>().enabled = m.Definition == selection;
                var picture = button.transform.Find("Icon").GetComponent<Image>(); picture.sprite = m.Icon; picture.gameObject.SetActive(m.Icon != null);
                button.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(m.Icon != null ? 40 : 8, 3);
                var q = m.Quote; button.GetComponent<Image>().color = q.Status == ResearchStatus.Completed ? new Color(.18f, .4f, .28f) : q.Status == ResearchStatus.Locked ? new Color(.17f, .18f, .21f) : q.Order > 0 ? new Color(.46f, .33f, .14f) : new Color(.16f, .32f, .47f);
                button.GetComponentInChildren<Text>().text = m.Name + "\n" + StatusName(q.Status) + (q.Order > 0 ? " #" + q.Order : "") + (q.Status == ResearchStatus.Completed ? "" : "  " + q.Progress + "/" + q.Cost);
            }
            Layout();
        }
        void Layout()
        {
            var size = new Vector2(300, 160); foreach (var m in models.Values) size = Vector2.Max(size, m.Position + new Vector2(230, 130));
            GraphScroll.content.sizeDelta = size * zoom; lines.Edges.Clear();
            foreach (var m in models.Values)
            {
                var r = (RectTransform)buttons[m.Definition].transform; r.pivot = new Vector2(0, 1); Place(r, new Vector2(0, 1), new Vector2(m.Position.x + 20, -m.Position.y - 20) * zoom, new Vector2(180, 80) * zoom);
                buttons[m.Definition].GetComponentInChildren<Text>().fontSize = Mathf.RoundToInt(17 * zoom);
                foreach (var parent in m.Quote.Prerequisites) if (models.TryGetValue(parent, out var p))
                    lines.Edges.Add(new TechnologyConnections.Edge { From = new Vector2(p.Position.x + 200, -p.Position.y - 60) * zoom, To = new Vector2(m.Position.x + 20, -m.Position.y - 60) * zoom, Color = parent == Selected || m.Definition == Selected ? Color.cyan : p.Quote.Completions > 0 ? new Color(.35f, .7f, .45f) : new Color(.35f, .4f, .46f) });
            }
            lines.rectTransform.pivot = new Vector2(0, 1); lines.SetVerticesDirty();
        }
        public void Focus(int definition)
        {
            if (!models.TryGetValue(definition, out var m)) return; Canvas.ForceUpdateCanvases();
            var span = GraphScroll.content.rect.size - GraphScroll.viewport.rect.size;
            GraphScroll.horizontalNormalizedPosition = span.x <= 0 ? 0 : Mathf.Clamp01(((m.Position.x + 110) * zoom - GraphScroll.viewport.rect.width / 2) / span.x);
            GraphScroll.verticalNormalizedPosition = span.y <= 0 ? 1 : 1 - Mathf.Clamp01(((m.Position.y + 60) * zoom - GraphScroll.viewport.rect.height / 2) / span.y);
        }
        public static string StatusName(ResearchStatus s) => s == ResearchStatus.Blocked ? "等待前置" : s == ResearchStatus.Locked ? "前置未完成" : s == ResearchStatus.Researching ? "当前研究" : s == ResearchStatus.Queued ? "排队中" : s == ResearchStatus.Paused ? "进度保留" : s == ResearchStatus.Completed ? "已完成" : s == ResearchStatus.AwaitingRewards ? "等待发奖" : "可研究";
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TechnologyConnections : MaskableGraphic
    {
        public struct Edge { public Vector2 From, To; public Color Color; public bool Vertical; }
        public readonly List<Edge> Edges = new List<Edge>();
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); foreach (var edge in Edges)
            {
                var middle = (edge.From.x + edge.To.x) / 2; var a = new Vector2(middle, edge.From.y); var b = new Vector2(middle, edge.To.y);
                if(edge.Vertical){var y=(edge.From.y+edge.To.y)/2;a=new Vector2(edge.From.x,y);b=new Vector2(edge.To.x,y);}
                Segment(vh, edge.From, a, edge.Color); Segment(vh, a, b, edge.Color); Segment(vh, b, edge.To, edge.Color);
            }
        }
        static void Segment(VertexHelper vh, Vector2 a, Vector2 b, Color color)
        {
            if ((b - a).sqrMagnitude < .01f) return; var n = new Vector2(-(b - a).y, (b - a).x).normalized * 1.5f; var start = vh.currentVertCount;
            foreach (var p in new[] { a - n, a + n, b + n, b - n }) vh.AddVert(p, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
