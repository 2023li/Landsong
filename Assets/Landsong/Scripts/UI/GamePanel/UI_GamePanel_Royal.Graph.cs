using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class UI_GamePanel_Royal
    {
        [LabelText("家谱布局根对象"), Required] public RectTransform GraphRoot;
        [LabelText("家谱滚动视图"), Required] public ScrollRect GraphScroll;
        [LabelText("家谱标题"), Required] public TextMeshProUGUI GraphHeader;
        [LabelText("家谱关闭按钮"), Required] public Button GraphCloseButton;
        [LabelText("家谱缩小按钮"), Required] public Button ZoomOutButton;
        [LabelText("家谱放大按钮"), Required] public Button ZoomInButton;
        [LabelText("家谱连线"), Required] public UI_GamePanel_TechnologyConnections GraphLinks;
        [LabelText("家谱图层"), Required] public RectTransform FamilyLayer;
        [LabelText("家族节点模板"), Required] public UI_GamePanel_CourtNode FamilyNodeTemplate;
        [LabelText("世代色带模板"), Required] public UI_GamePanel_CourtGenerationBand GenerationBandTemplate;
        [LabelText("家族框架模板"), Required] public UI_GamePanel_CourtFamilyFrame FamilyFrameTemplate;
        [LabelText("预制体家谱样式卡片"), Required] public UI_GamePanel_CourtNode[] AuthoredStyleCards;

        readonly Dictionary<ulong, UI_GamePanel_CourtNode> graphNodes = new Dictionary<ulong, UI_GamePanel_CourtNode>();
        readonly List<GameObject> graphGenerated = new List<GameObject>();
        static readonly Color SuccessionColor = new Color(1, .73f, .22f);
        string graphSignature;
        float graphZoom = 1;
        bool graphActionsBound;
        int authoredStyleUsed;

        public int NodeCount => graphNodes.Count;
        public int EdgeCount => GraphLinks.Edges.Count;
        public int GenerationCount { get; private set; }
        public int FamilyCount { get; private set; }
        public int SuccessionEdgeCount => GraphLinks.Edges.Count(edge => edge.Color == SuccessionColor);
        public UI_GamePanel_CourtNode NodeView(ulong id) => graphNodes.TryGetValue(id, out var node) ? node : null;
        public Button Node(ulong id) => NodeView(id)?.Select;

        public void ValidateGraphConfiguration()
        {
            if (GraphRoot == null || GraphScroll == null || GraphScroll.viewport == null || GraphHeader == null
                || GraphCloseButton == null || ZoomOutButton == null || ZoomInButton == null || GraphLinks == null
                || FamilyLayer == null || FamilyNodeTemplate == null || GenerationBandTemplate == null
                || FamilyFrameTemplate == null || AuthoredStyleCards == null || AuthoredStyleCards.Length == 0)
                throw new InvalidOperationException("王室家谱检查器引用不完整。");
            foreach (var card in AuthoredStyleCards)
                if (card == null || !card.transform.IsChildOf(FamilyLayer))
                    throw new InvalidOperationException("王室预制体样式卡片必须属于家谱图层。");
            FamilyNodeTemplate.ValidateConfiguration(true);
            if (GenerationBandTemplate.Background == null || GenerationBandTemplate.Label == null
                || FamilyFrameTemplate.Background == null)
                throw new InvalidOperationException("王室家谱容器模板检查器引用不完整。");
        }

        void BindGraphActions(Action close)
        {
            ValidateGraphConfiguration();
            GraphCloseButton.onClick.RemoveAllListeners();
            GraphCloseButton.onClick.AddListener(() => close());
            ZoomOutButton.onClick.RemoveAllListeners();
            ZoomOutButton.onClick.AddListener(() => ZoomGraph(-.1f));
            ZoomInButton.onClick.RemoveAllListeners();
            ZoomInButton.onClick.AddListener(() => ZoomGraph(.1f));
        }

        void ZoomGraph(float amount)
        {
            graphZoom = Mathf.Clamp(graphZoom + amount, .65f, 1.3f);
            graphSignature = null;
        }

        void TrackGraph(Component instance)
        {
            instance.gameObject.SetActive(true);
            graphGenerated.Add(instance.gameObject);
        }

        void ClearGraphGenerated()
        {
            foreach (var item in graphGenerated)
            {
                if (item == null)
                    continue;
                item.SetActive(false);
                Destroy(item);
            }

            graphGenerated.Clear();
            HideAuthoredStyleCards();
            authoredStyleUsed = 0;
            graphNodes.Clear();
        }

        UI_GamePanel_CourtNode CreateGraphNode()
        {
            if (authoredStyleUsed < AuthoredStyleCards.Length)
            {
                var authored = AuthoredStyleCards[authoredStyleUsed++];
                authored.gameObject.SetActive(true);
                return authored;
            }

            var created = Instantiate(FamilyNodeTemplate, FamilyLayer);
            TrackGraph(created);
            return created;
        }

        void ClearGraphSession()
        {
            graphSignature = null;
            graphZoom = 1;
            GenerationCount = 0;
            FamilyCount = 0;
            ClearGraphGenerated();
            GraphLinks.Edges.Clear();
            GraphLinks.SetVerticesDirty();
        }

        void HideAuthoredStyleCards()
        {
            foreach (var card in AuthoredStyleCards)
                card.gameObject.SetActive(false);
        }

        sealed class RoyalFamilyGroup
        {
            public ulong A, B;
            public int Generation;
            public Vector2 Position;
            public float Width;
        }

        static (ulong, ulong) FamilyPair(ulong a, ulong b) => a < b ? (a, b) : (b, a);

        void SizeGraph(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(position.x, -position.y) * graphZoom;
            rect.sizeDelta = size * graphZoom;
        }

        void ShowRoyalFamily(string title, List<CourtCard> cards)
        {
            ValidateGraphConfiguration();
            var next = graphZoom + "/" + GraphScroll.viewport.rect.width + "/" + string.Join("|", cards.Select(c => $"{c.Id}:{c.Parent}:{c.SecondParent}:{c.Spouse}:{c.Column}:{c.Title}:{c.Detail}:{c.Influence}:{c.Selected}:{c.Dead}:{c.Monarch}:{c.EverMonarch}:{c.RequestCount}"));
            if (next == graphSignature)
            {
                foreach (var card in cards)
                    if (graphNodes.TryGetValue(card.Id, out var node))
                        card.BindPortrait?.Invoke(node.PortraitBinding);
                return;
            }

            bool first = string.IsNullOrEmpty(graphSignature);
            var scroll = GraphScroll.normalizedPosition;
            ClearGraphGenerated();
            graphSignature = next;
            GraphHeader.text = title;
            var byId = cards.ToDictionary(c => c.Id);
            var families = new Dictionary<(ulong, ulong), RoyalFamilyGroup>();
            var memberships = new HashSet<ulong>();
            void Add(ulong a, ulong b)
            {
                if (a == 0 || !byId.ContainsKey(a))
                    return;
                if (!byId.ContainsKey(b))
                    b = 0;
                var key = FamilyPair(a, b);
                if (families.ContainsKey(key))
                    return;
                families.Add(key, new RoyalFamilyGroup
                {
                    A = a,
                    B = b,
                    Generation = b == 0 ? byId[a].Column : Mathf.Max(byId[a].Column, byId[b].Column),
                    Width = b == 0 ? 166 : 326
                });
                memberships.Add(a);
                if (b != 0)
                    memberships.Add(b);
            }

            foreach (var card in cards)
                if (card.Spouse != 0)
                    Add(card.Id, card.Spouse);
            foreach (var card in cards)
                if (card.Parent != 0 && card.SecondParent != 0)
                    Add(card.Parent, card.SecondParent);
            foreach (var card in cards)
                if (!memberships.Contains(card.Id))
                    Add(card.Id, 0);
            var rows = families.Values.GroupBy(f => f.Generation).OrderBy(group => group.Key).ToArray();
            GenerationCount = rows.Length;
            FamilyCount = families.Count;
            float width = Mathf.Max(GraphScroll.viewport.rect.width / graphZoom,
                Mathf.Max(600, rows.Select(group => group.Sum(f => f.Width + 28) + 60).DefaultIfEmpty(600).Max()));
            var positions = new Dictionary<ulong, Vector2>();
            int rowIndex = 0;
            foreach (var row in rows)
            {
                float y = 30 + rowIndex++ * 260;
                var band = Instantiate(GenerationBandTemplate, FamilyLayer);
                TrackGraph(band);
                SizeGraph((RectTransform)band.transform, new Vector2(0, y), new Vector2(width, 214));
                band.Background.color = new Color(.24f, .25f, .27f);
                band.Background.raycastTarget = false;
                band.Label.text = "第 " + (row.Key + 1) + " 代";
                band.Label.color = new Color(.7f, .72f, .76f);
                float x = 30;
                foreach (var family in row.OrderBy(group => byId[group.A].Parent).ThenBy(group => group.A))
                {
                    family.Position = new Vector2(x, y + 35);
                    var frame = Instantiate(FamilyFrameTemplate, FamilyLayer);
                    TrackGraph(frame);
                    SizeGraph((RectTransform)frame.transform, family.Position, new Vector2(family.Width, 166));
                    frame.Background.color = new Color(.13f, .3f, .95f);
                    frame.Background.raycastTarget = false;
                    int index = 0;
                    foreach (var id in new[] { family.A, family.B })
                    {
                        if (id == 0)
                            continue;
                        var card = byId[id];
                        var position = family.Position + new Vector2(5 + 160 * index++, 5);
                        if (!positions.ContainsKey(id))
                            positions.Add(id, position);
                        var node = CreateGraphNode();
                        SizeGraph((RectTransform)node.transform, position, new Vector2(156, 156));
                        if (!graphNodes.ContainsKey(id))
                            graphNodes.Add(id, node);
                        var button = node.Select;
                        button.image.color = card.Dead ? new Color(.43f, .44f, .46f) : new Color(.93f, .94f, .95f);
                        node.SelectedOutline.enabled = card.Selected;
                        node.Influence.text = "影响力 " + card.Influence.ToString("0.0");
                        node.Influence.color = new Color(.18f, .2f, .23f);
                        node.PersonName.text = card.Title;
                        node.PersonName.color = new Color(.1f, .12f, .15f);
                        node.Detail.text = card.Detail;
                        node.Detail.color = new Color(.25f, .27f, .3f);
                        card.BindPortrait?.Invoke(node.PortraitBinding);
                        node.CrownRoot.SetActive(card.Monarch);
                        node.Crown.sprite = RoyalCrownIcon;
                        node.Crown.color = SuccessionColor;
                        node.CrownFallback.gameObject.SetActive(RoyalCrownIcon == null);
                        node.CrownFallback.color = new Color(.2f, .13f, .03f);
                        node.RequestRoot.SetActive(card.RequestCount > 0);
                        node.RequestBackground.color = new Color(.75f, .32f, .08f);
                        node.RequestLabel.text = "! " + card.RequestCount;
                        button.onClick.RemoveAllListeners();
                        button.interactable = card.Click != null;
                        if (card.Click != null)
                            button.onClick.AddListener(() => card.Click());
                    }

                    x += family.Width + 28;
                }
            }

            GraphLinks.Edges.Clear();
            foreach (var card in cards.OrderBy(c => c.EverMonarch))
            {
                if (!positions.TryGetValue(card.Id, out var end))
                    continue;
                Vector2 start;
                if (families.TryGetValue(FamilyPair(card.Parent, card.SecondParent), out var family))
                    start = family.Position + new Vector2(family.Width / 2, 166);
                else if (positions.TryGetValue(card.Parent != 0 ? card.Parent : card.SecondParent, out var parent))
                    start = parent + new Vector2(78, 156);
                else
                    continue;
                GraphLinks.Edges.Add(new UI_GamePanel_TechnologyConnections.Edge
                {
                    From = new Vector2(start.x, -start.y) * graphZoom,
                    To = new Vector2(end.x + 78, -end.y) * graphZoom,
                    Color = card.EverMonarch ? SuccessionColor : new Color(.5f, .77f, .86f),
                    Vertical = true
                });
            }

            GraphScroll.content.sizeDelta = new Vector2(width, Mathf.Max(260, rowIndex * 260)) * graphZoom;
            GraphLinks.SetVerticesDirty();
            GraphScroll.normalizedPosition = first ? new Vector2(0, 1) : scroll;
        }
    }
}
