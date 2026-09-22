using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed partial class UI_GamePanel_CourtGraph
    {
        [Sirenix.OdinInspector.LabelText("王冠图标")]
        public Sprite CrownIcon;
        public int GenerationCount { get; private set; }
        public int FamilyCount { get; private set; }
        public int SuccessionEdgeCount => Links.Edges.Count(e => e.Color == SuccessionColor);

        static readonly Color SuccessionColor = new Color(1, .73f, .22f);
        string familySignature;
        sealed class Family
        {
            public ulong A, B;
            public int Generation;
            public Vector2 Position;
            public float Width;
        }

        static (ulong, ulong) Pair(ulong a, ulong b) => a < b ? (a, b) : (b, a);
        void ClearFamily()
        {
            if (string.IsNullOrEmpty(familySignature))
                return;
            ClearGenerated();
            familySignature = null;
            signature = null;
        }

        void Size(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(position.x, -position.y) * zoom;
            rect.sizeDelta = size * zoom;
        }

        void ShowFamily(string title, List<CourtCard> cards)
        {
            var next = zoom + "/" + Scroll.viewport.rect.width + "/" + string.Join("|", cards.Select(c => $"{c.Id}:{c.Parent}:{c.SecondParent}:{c.Spouse}:{c.Column}:{c.Title}:{c.Detail}:{c.Influence}:{c.Selected}:{c.Dead}:{c.Monarch}:{c.EverMonarch}:{c.RequestCount}"));
            if (next == familySignature)
            {
                foreach (var card in cards)
                    if (nodes.TryGetValue(card.Id, out var node))
                        card.BindPortrait?.Invoke(node.PortraitBinding);
                return;
            }

            bool first = string.IsNullOrEmpty(familySignature);
            var scroll = Scroll.normalizedPosition;
            ClearGenerated();
            signature = null;
            familySignature = next;
            Header.text = title;
            var byId = cards.ToDictionary(c => c.Id);
            var families = new Dictionary<(ulong, ulong), Family>();
            var memberships = new HashSet<ulong>();
            void Add(ulong a, ulong b)
            {
                if (a == 0 || !byId.ContainsKey(a))
                    return;
                if (!byId.ContainsKey(b))
                    b = 0;
                var key = Pair(a, b);
                if (families.ContainsKey(key))
                    return;
                families.Add(key, new Family { A = a, B = b, Generation = b == 0 ? byId[a].Column : Mathf.Max(byId[a].Column, byId[b].Column), Width = b == 0 ? 166 : 326 });
                memberships.Add(a);
                if (b != 0)
                    memberships.Add(b);
            }

            foreach (var c in cards)
                if (c.Spouse != 0)
                    Add(c.Id, c.Spouse);
            foreach (var c in cards)
                if (c.Parent != 0 && c.SecondParent != 0)
                    Add(c.Parent, c.SecondParent);
            foreach (var c in cards)
                if (!memberships.Contains(c.Id))
                    Add(c.Id, 0);
            var rows = families.Values.GroupBy(f => f.Generation).OrderBy(g => g.Key).ToArray();
            GenerationCount = rows.Length;
            FamilyCount = families.Count;
            float width = Mathf.Max(Scroll.viewport.rect.width / zoom, Mathf.Max(600, rows.Select(g => g.Sum(f => f.Width + 28) + 60).DefaultIfEmpty(600).Max()));
            var positions = new Dictionary<ulong, Vector2>();
            int rowIndex = 0;
            foreach (var row in rows)
            {
                float y = 30 + rowIndex++ * 260;
                var band = Instantiate(GenerationBandTemplate, FamilyLayer);
                Track(band);
                Size((RectTransform)band.transform, new Vector2(0, y), new Vector2(width, 214));
                band.Background.color = new Color(.24f, .25f, .27f);
                band.Background.raycastTarget = false;
                band.Label.text = "第 " + (row.Key + 1) + " 代";
                band.Label.color = new Color(.7f, .72f, .76f);
                float x = 30;
                foreach (var f in row.OrderBy(f => byId[f.A].Parent).ThenBy(f => f.A))
                {
                    f.Position = new Vector2(x, y + 35);
                    var frame = Instantiate(FamilyFrameTemplate, FamilyLayer);
                    Track(frame);
                    Size((RectTransform)frame.transform, f.Position, new Vector2(f.Width, 166));
                    frame.Background.color = new Color(.13f, .3f, .95f);
                    frame.Background.raycastTarget = false;
                    int index = 0;
                    foreach (var id in new[]
                    {
                        f.A,
                        f.B
                    }

                    )
                    {
                        if (id == 0)
                            continue;
                        var c = byId[id];
                        var pos = f.Position + new Vector2(5 + 160 * index++, 5);
                        if (!positions.ContainsKey(id))
                            positions.Add(id, pos);
                        var node = Instantiate(FamilyNodeTemplate, FamilyLayer);
                        Track(node);
                        Size((RectTransform)node.transform, pos, new Vector2(156, 156));
                        if (!nodes.ContainsKey(id))
                            nodes.Add(id, node);
                        var button = node.Select;
                        button.image.color = c.Dead ? new Color(.43f, .44f, .46f) : new Color(.93f, .94f, .95f);
                        node.SelectedOutline.enabled = c.Selected;
                        node.Influence.text = "影响力 " + c.Influence.ToString("0.0");
                        node.Influence.color = new Color(.18f, .2f, .23f);
                        node.PersonName.text = c.Title;
                        node.PersonName.color = new Color(.1f, .12f, .15f);
                        node.Detail.text = c.Detail;
                        node.Detail.color = new Color(.25f, .27f, .3f);
                        c.BindPortrait?.Invoke(node.PortraitBinding);
                        node.CrownRoot.SetActive(c.Monarch);
                        node.Crown.sprite = CrownIcon;
                        node.Crown.color = SuccessionColor;
                        node.CrownFallback.gameObject.SetActive(CrownIcon == null);
                        node.CrownFallback.color = new Color(.2f, .13f, .03f);
                        node.RequestRoot.SetActive(c.RequestCount > 0);
                        node.RequestBackground.color = new Color(.75f, .32f, .08f);
                        node.RequestLabel.text = "! " + c.RequestCount;
                        button.onClick.RemoveAllListeners();
                        button.interactable = c.Click != null;
                        if (c.Click != null)
                            button.onClick.AddListener(() => c.Click());
                    }

                    x += f.Width + 28;
                }
            }

            Links.Edges.Clear();
            foreach (var c in cards.OrderBy(c => c.EverMonarch))
            {
                if (!positions.TryGetValue(c.Id, out var end))
                    continue;
                Vector2 start;
                if (families.TryGetValue(Pair(c.Parent, c.SecondParent), out var family))
                    start = family.Position + new Vector2(family.Width / 2, 166);
                else if (positions.TryGetValue(c.Parent != 0 ? c.Parent : c.SecondParent, out var parent))
                    start = parent + new Vector2(78, 156);
                else
                    continue;
                Links.Edges.Add(new UI_GamePanel_TechnologyConnections.Edge { From = new Vector2(start.x, -start.y) * zoom, To = new Vector2(end.x + 78, -end.y) * zoom, Color = c.EverMonarch ? SuccessionColor : new Color(.5f, .77f, .86f), Vertical = true });
            }

            Scroll.content.sizeDelta = new Vector2(width, Mathf.Max(260, rowIndex * 260)) * zoom;
            Links.SetVerticesDirty();
            Scroll.normalizedPosition = first ? new Vector2(0, 1) : scroll;
        }
    }
}
