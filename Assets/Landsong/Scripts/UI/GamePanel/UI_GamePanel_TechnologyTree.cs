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
    public sealed class UI_GamePanel_TechnologyTree : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("标题")]
        public Text Header;
        [Sirenix.OdinInspector.LabelText("关闭按钮")]
        public Button CloseButton;
        [Sirenix.OdinInspector.LabelText("当前按钮")]
        public Button CurrentButton;
        [Sirenix.OdinInspector.LabelText("缩放缩小按钮")]
        public Button ZoomOutButton;
        [Sirenix.OdinInspector.LabelText("缩放放大按钮")]
        public Button ZoomInButton;
        [Sirenix.OdinInspector.LabelText("搜索")]
        public InputField Search;
        [Sirenix.OdinInspector.LabelText("图视图滚动视图")]
        public ScrollRect GraphScroll;
        [Sirenix.OdinInspector.LabelText("详情条目容器")]
        public RectTransform DetailRows;
        [Sirenix.OdinInspector.LabelText("连线")]
        public UI_GamePanel_TechnologyConnections Lines;
        [Sirenix.OdinInspector.LabelText("节点模板")]
        public UI_GamePanel_TechnologyNode NodeTemplate;
        [Sirenix.OdinInspector.LabelText("已选")]
        public int Selected = -1;
        public int NodeCount => buttons.Count;
        public int EdgeCount => Lines.Edges.Count;

        readonly Dictionary<int, UI_GamePanel_TechnologyNode> buttons = new Dictionary<int, UI_GamePanel_TechnologyNode>();
        readonly Dictionary<int, TechnologyNodeModel> models = new Dictionary<int, TechnologyNodeModel>();
        float zoom = 1;
        Action<int> selected;
        public Button NodeButton(int definition) => buttons.TryGetValue(definition, out var b) ? b.Select : null;
        public void BindActions(Action<int> select, Action close)
        {
            ValidateConfiguration();
            selected = select;
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(() => close());
            Search.onValueChanged.RemoveAllListeners();
            Search.onValueChanged.AddListener(value =>
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;
                foreach (var m in models.Values)
                    if (m.Name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        selected(m.Definition);
                        Focus(m.Definition);
                        break;
                    }
            });
            ZoomOutButton.onClick.RemoveAllListeners();
            ZoomOutButton.onClick.AddListener(() =>
            {
                zoom = Mathf.Max(.5f, zoom - .15f);
                Layout();
                Focus(Selected);
            });
            ZoomInButton.onClick.RemoveAllListeners();
            ZoomInButton.onClick.AddListener(() =>
            {
                zoom = Mathf.Min(1.5f, zoom + .15f);
                Layout();
                Focus(Selected);
            });
        }

        public void ValidateConfiguration()
        {
            if (Header == null || CloseButton == null || CurrentButton == null || ZoomOutButton == null || ZoomInButton == null || Search == null || GraphScroll == null || DetailRows == null || Lines == null || NodeTemplate == null || NodeTemplate.Select == null || NodeTemplate.Label == null || NodeTemplate.Icon == null || NodeTemplate.Selection == null)
                throw new InvalidOperationException("科技面板检查器引用不完整。");
        }

        static void Place(Transform transform, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var r = (RectTransform)transform;
            r.anchorMin = r.anchorMax = anchor;
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        public void Bind(List<TechnologyNodeModel> nodes, int selection, int points, int current)
        {
            Selected = selection;
            Header.text = "科技树 · 可用研究点 " + points;
            CurrentButton.onClick.RemoveAllListeners();
            CurrentButton.interactable = current >= 0;
            if (current >= 0)
                CurrentButton.onClick.AddListener(() =>
                {
                    selected(current);
                    Focus(current);
                });
            models.Clear();
            foreach (var m in nodes)
                models.Add(m.Definition, m);
            foreach (var pair in buttons)
                pair.Value.gameObject.SetActive(models.ContainsKey(pair.Key));
            foreach (var m in nodes)
            {
                if (!buttons.TryGetValue(m.Definition, out var button))
                {
                    button = Instantiate(NodeTemplate, GraphScroll.content);
                    button.name = m.Name;
                    buttons.Add(m.Definition, button);
                    var at = m.Definition;
                    button.Select.onClick.AddListener(() => selected(at));
                }

                button.gameObject.SetActive(true);
                button.Selection.enabled = m.Definition == selection;
                button.Icon.sprite = m.Icon;
                button.Icon.gameObject.SetActive(m.Icon != null);
                button.Label.rectTransform.offsetMin = new Vector2(m.Icon != null ? 40 : 8, 3);
                var q = m.Quote;
                button.Select.image.color = q.Status == ResearchStatus.Completed ? new Color(.18f, .4f, .28f) : q.Status == ResearchStatus.Locked ? new Color(.17f, .18f, .21f) : q.Order > 0 ? new Color(.46f, .33f, .14f) : new Color(.16f, .32f, .47f);
                button.Label.text = m.Name + "\n" + StatusName(q.Status) + (q.Order > 0 ? " #" + q.Order : "") + (q.Status == ResearchStatus.Completed ? "" : "  " + q.Progress + "/" + q.Cost);
            }

            Layout();
        }

        void Layout()
        {
            var size = new Vector2(300, 160);
            foreach (var m in models.Values)
                size = Vector2.Max(size, m.Position + new Vector2(230, 130));
            GraphScroll.content.sizeDelta = size * zoom;
            Lines.Edges.Clear();
            foreach (var m in models.Values)
            {
                var r = (RectTransform)buttons[m.Definition].transform;
                r.pivot = new Vector2(0, 1);
                Place(r, new Vector2(0, 1), new Vector2(m.Position.x + 20, -m.Position.y - 20) * zoom, new Vector2(180, 80) * zoom);
                buttons[m.Definition].Label.fontSize = Mathf.RoundToInt(17 * zoom);
                foreach (var parent in m.Quote.Prerequisites)
                    if (models.TryGetValue(parent, out var p))
                        Lines.Edges.Add(new UI_GamePanel_TechnologyConnections.Edge { From = new Vector2(p.Position.x + 200, -p.Position.y - 60) * zoom, To = new Vector2(m.Position.x + 20, -m.Position.y - 60) * zoom, Color = parent == Selected || m.Definition == Selected ? Color.cyan : p.Quote.Completions > 0 ? new Color(.35f, .7f, .45f) : new Color(.35f, .4f, .46f) });
            }

            Lines.rectTransform.pivot = new Vector2(0, 1);
            Lines.SetVerticesDirty();
        }

        public void Focus(int definition)
        {
            if (!models.TryGetValue(definition, out var m))
                return;
            Canvas.ForceUpdateCanvases();
            var span = GraphScroll.content.rect.size - GraphScroll.viewport.rect.size;
            GraphScroll.horizontalNormalizedPosition = span.x <= 0 ? 0 : Mathf.Clamp01(((m.Position.x + 110) * zoom - GraphScroll.viewport.rect.width / 2) / span.x);
            GraphScroll.verticalNormalizedPosition = span.y <= 0 ? 1 : 1 - Mathf.Clamp01(((m.Position.y + 60) * zoom - GraphScroll.viewport.rect.height / 2) / span.y);
        }

        public static string StatusName(ResearchStatus s) => s == ResearchStatus.Blocked ? "等待前置" : s == ResearchStatus.Locked ? "前置未完成" : s == ResearchStatus.Researching ? "当前研究" : s == ResearchStatus.Queued ? "排队中" : s == ResearchStatus.Paused ? "进度保留" : s == ResearchStatus.Completed ? "已完成" : s == ResearchStatus.AwaitingRewards ? "等待发奖" : "可研究";
        public void ClearSession()
        {
            foreach (var node in buttons.Values)
                if (node != null)
                    Destroy(node.gameObject);
            buttons.Clear();
            models.Clear();
            Selected = -1;
            zoom = 1;
            Search.SetTextWithoutNotify("");
            Lines.Edges.Clear();
            Lines.SetVerticesDirty();
        }
    }
}
