using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class CourtCard
    {
        public ulong Id, Parent, SecondParent, Spouse;
        public int Column, Row, RequestCount;
        public string Title, Detail;
        public Sprite Portrait;
        public bool Selected, Dead, Monarch, EverMonarch;
        public float Influence;
        public Action Click;
        public Action<UI_Common_PortraitImageBinding> BindPortrait;
    }

    public sealed partial class UI_GamePanel_CourtGraph : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("滚动视图")]
        public ScrollRect Scroll;
        [Sirenix.OdinInspector.LabelText("标题")]
        public TextMeshProUGUI Header;
        [Sirenix.OdinInspector.LabelText("关闭按钮")]
        public Button CloseButton;
        [Sirenix.OdinInspector.LabelText("缩放缩小按钮")]
        public Button ZoomOutButton;
        [Sirenix.OdinInspector.LabelText("缩放放大按钮")]
        public Button ZoomInButton;
        [Sirenix.OdinInspector.LabelText("连线")]
        public UI_GamePanel_TechnologyConnections Links;
        [Sirenix.OdinInspector.LabelText("节点根对象")]
        public RectTransform NodesRoot;
        [Sirenix.OdinInspector.LabelText("家族图层")]
        public RectTransform FamilyLayer;
        [Sirenix.OdinInspector.LabelText("模板根对象")]
        public RectTransform TemplateRoot;
        [Sirenix.OdinInspector.LabelText("节点模板")]
        public UI_GamePanel_CourtNode NodeTemplate;
        [Sirenix.OdinInspector.LabelText("家族节点模板")]
        public UI_GamePanel_CourtNode FamilyNodeTemplate;
        [Sirenix.OdinInspector.LabelText("世代色带模板")]
        public UI_GamePanel_CourtGenerationBand GenerationBandTemplate;
        [Sirenix.OdinInspector.LabelText("家族框架模板")]
        public UI_GamePanel_CourtFamilyFrame FamilyFrameTemplate;
        readonly Dictionary<ulong, UI_GamePanel_CourtNode> nodes = new Dictionary<ulong, UI_GamePanel_CourtNode>();
        readonly List<GameObject> generated = new List<GameObject>();
        string signature;
        float zoom = 1;
        public int NodeCount => nodes.Count;
        public int EdgeCount => Links.Edges.Count;

        public UI_GamePanel_CourtNode NodeView(ulong id) => nodes.TryGetValue(id, out var node) ? node : null;
        public Button Node(ulong id) => NodeView(id)?.Select;
        public void BindActions(Action close)
        {
            ValidateConfiguration();
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(() => close());
            ZoomOutButton.onClick.RemoveAllListeners();
            ZoomOutButton.onClick.AddListener(() => Zoom(-.1f));
            ZoomInButton.onClick.RemoveAllListeners();
            ZoomInButton.onClick.AddListener(() => Zoom(.1f));
        }

        public void ValidateConfiguration()
        {
            if (Scroll == null || Header == null || CloseButton == null || ZoomOutButton == null || ZoomInButton == null || Links == null || NodesRoot == null || FamilyLayer == null || TemplateRoot == null || NodeTemplate == null || FamilyNodeTemplate == null || GenerationBandTemplate == null || FamilyFrameTemplate == null)
                throw new InvalidOperationException("王室展示面板检查器引用不完整。");
            NodeTemplate.ValidateConfiguration(false);
            FamilyNodeTemplate.ValidateConfiguration(true);
            if (GenerationBandTemplate.Background == null || GenerationBandTemplate.Label == null || FamilyFrameTemplate.Background == null)
                throw new InvalidOperationException("王室家谱容器模板检查器引用不完整。");
        }

        void Track(Component instance)
        {
            instance.gameObject.SetActive(true);
            generated.Add(instance.gameObject);
        }

        void ClearGenerated()
        {
            foreach (var item in generated)
            {
                if (item == null)
                    continue;
                item.SetActive(false);
                Destroy(item);
            }

            generated.Clear();
            nodes.Clear();
        }

        public void ClearSession()
        {
            signature = null;
            familySignature = null;
            zoom = 1;
            ClearGenerated();
            ClearFamily();
        }

        void Zoom(float amount)
        {
            zoom = Mathf.Clamp(zoom + amount, .65f, 1.3f);
            signature = null;
            familySignature = null;
        }

        public void Show(string mode, List<CourtCard> cards, bool family = false)
        {
            ValidateConfiguration();
            if (family)
            {
                ShowFamily(mode, cards);
                return;
            }

            ClearFamily();
            string next = mode + "/" + zoom + "/" + string.Join("|", cards.Select(c => $"{c.Id}:{c.Parent}:{c.SecondParent}:{c.Spouse}:{c.Column}:{c.Row}:{c.Title}:{c.Detail}:{c.Selected}:{c.Dead}:{c.Portrait?.GetInstanceID()}"));
            if (signature == next)
                return;
            signature = next;
            ClearGenerated();
            Header.text = mode;
            var positions = new Dictionary<ulong, Vector2>();
            var size = new Vector2(500, 240);
            foreach (var card in cards)
            {
                var node = Instantiate(NodeTemplate, NodesRoot);
                Track(node);
                nodes.Add(card.Id, node);
                var button = node.Select;
                var rect = (RectTransform)node.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                var position = new Vector2(20 + card.Column * 250, 20 + card.Row * 160);
                positions.Add(card.Id, position);
                rect.anchoredPosition = new Vector2(position.x, -position.y) * zoom;
                rect.sizeDelta = new Vector2(220, 130) * zoom;
                size = Vector2.Max(size, position + new Vector2(250, 170));
                node.Label.text = card.Title + "\n" + card.Detail;
                node.Label.fontSize = 17 * zoom;
                node.Portrait.sprite = card.Portrait;
                node.Portrait.color = card.Portrait != null ? (card.Dead ? Color.gray : Color.white) : card.Dead ? Color.gray : new Color(.7f, .65f, .45f);
                card.BindPortrait?.Invoke(node.PortraitBinding);
                button.image.color = card.Selected ? new Color(.2f, .45f, .5f) : card.Dead ? new Color(.18f, .18f, .2f) : new Color(.15f, .23f, .32f);
                button.onClick.RemoveAllListeners();
                button.interactable = card.Click != null;
                if (card.Click != null)
                    button.onClick.AddListener(() => card.Click());
            }

            Scroll.content.sizeDelta = size * zoom;
            Links.Edges.Clear();
            foreach (var card in cards)
            {
                foreach (var parent in new[]
                {
                    card.Parent,
                    card.SecondParent
                }.Distinct())
                    if (parent != 0 && positions.TryGetValue(parent, out var start))
                        Links.Edges.Add(new UI_GamePanel_TechnologyConnections.Edge { From = new Vector2(start.x + 220, -start.y - 65) * zoom, To = new Vector2(positions[card.Id].x, -positions[card.Id].y - 65) * zoom, Color = Color.cyan });
                if (card.Spouse > card.Id && positions.TryGetValue(card.Spouse, out var mate))
                {
                    var start = positions[card.Id];
                    Links.Edges.Add(new UI_GamePanel_TechnologyConnections.Edge { From = new Vector2(start.x + 220, -start.y - 90) * zoom, To = new Vector2(mate.x + 220, -mate.y - 90) * zoom, Color = new Color(1, .7f, .3f) });
                }
            }

            Links.SetVerticesDirty();
        }
    }
}
