using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    // Reused visual slot; quest identity and progress remain authoritative ECS data.
    public sealed class QuestSlotView
    {
        public RectTransform Rect, Body;
        public TextMeshProUGUI Source, Title, Deadline;
        public Button Expand, Action;
        public Toggle Tracking;
        public Image Background;
        public ulong QuestId;
        public static QuestSlotView Create(Transform parent, TMP_FontAsset font)
        {
            var v = new QuestSlotView();
            v.Rect = InterfaceWidgets.Rect("Quest slot", parent, Vector2.zero, Vector2.one);
            v.Background = v.Rect.gameObject.AddComponent<Image>();
            var layout = v.Rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 12); layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            v.Source = InterfaceWidgets.Text("", v.Rect, font, 17);
            v.Source.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            var header = InterfaceWidgets.Rect("Task header", v.Rect, Vector2.zero, Vector2.one);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            v.Expand = InterfaceWidgets.Button("", header, font, null);
            var er = (RectTransform)v.Expand.transform; er.anchorMax = new Vector2(1, 1); er.offsetMax = new Vector2(-152, 0);
            v.Title = v.Expand.GetComponentInChildren<TextMeshProUGUI>(); v.Title.fontSize = 20; v.Title.alignment = TextAlignmentOptions.MidlineLeft;
            v.Tracking=InterfaceWidgets.Checkbox("",header,font,null);
            var tr=(RectTransform)v.Tracking.transform;tr.anchorMin=tr.anchorMax=new Vector2(1,.5f);tr.pivot=new Vector2(1,.5f);tr.sizeDelta=new Vector2(42,42);tr.anchoredPosition=new Vector2(-104,0);
            v.Deadline = InterfaceWidgets.Text("", header, font, 14);
            v.Deadline.rectTransform.anchorMin = new Vector2(1, 1); v.Deadline.rectTransform.anchorMax = Vector2.one;
            v.Deadline.rectTransform.pivot = Vector2.one; v.Deadline.rectTransform.sizeDelta = new Vector2(96, 25);
            v.Deadline.alignment = TextAlignmentOptions.Right;
            v.Action = InterfaceWidgets.Button("", header, font, null);
            var ar = (RectTransform)v.Action.transform; ar.anchorMin = ar.anchorMax = Vector2.one; ar.pivot = Vector2.one;
            ar.sizeDelta = new Vector2(42, 36); ar.anchoredPosition = new Vector2(0, -24);
            v.Action.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            v.Body = InterfaceWidgets.Rect("Expanded details", v.Rect, Vector2.zero, Vector2.one);
            var body = v.Body.gameObject.AddComponent<VerticalLayoutGroup>(); body.spacing = 6;
            body.childControlWidth = body.childControlHeight = true; body.childForceExpandHeight = false;
            return v;
        }
        public void Show(ulong id, string source, string title, string deadline, bool expanded, bool completed, Action toggle, string actionLabel, Action action)
        {
            QuestId = id; Rect.gameObject.SetActive(true); Source.text = source; Tracking.gameObject.SetActive(false);
            var width = Mathf.Max(200, ((RectTransform)Rect.parent).rect.width - 44);
            Source.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(36, Source.GetPreferredValues(source, width, float.PositiveInfinity).y + 8);
            Background.color = completed ? new Color(.16f,.30f,.24f) : new Color(.24f,.26f,.28f);
            Title.text = (id == 0 ? "" : expanded ? "∨  " : ">  ") + title;
            Deadline.text = deadline; Body.gameObject.SetActive(expanded);
            Expand.onClick.RemoveAllListeners(); Expand.interactable = toggle != null;
            if (toggle != null) Expand.onClick.AddListener(() => toggle());
            Action.gameObject.SetActive(!string.IsNullOrEmpty(actionLabel)); Action.interactable = action != null;
            Action.GetComponentInChildren<TextMeshProUGUI>().text = actionLabel;
            Action.onClick.RemoveAllListeners(); if (action != null) Action.onClick.AddListener(() => action());
        }
    }
}
