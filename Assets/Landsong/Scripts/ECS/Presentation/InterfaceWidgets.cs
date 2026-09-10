using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    // Shared TMP/UGUI construction; authored parent layout remains replaceable by art.
    public static class InterfaceWidgets
    {
        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        { var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; return r; }
        public static TextMeshProUGUI Text(string text, Transform parent, TMP_FontAsset font, int size = 18)
        { var r = Rect("Label", parent, Vector2.zero, Vector2.one); var t = r.gameObject.AddComponent<TextMeshProUGUI>(); t.font = font; t.text = text; t.fontSize = size; t.color = Color.white; t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false; t.margin = new Vector4(10, 4, 10, 4); return t; }
        public static Button Button(string label, Transform parent, TMP_FontAsset font, Action action, float height = 48)
        {
            var r = Rect(label, parent, Vector2.zero, Vector2.one); var bg = r.gameObject.AddComponent<Image>(); bg.color = new Color(.17f,.23f,.31f,.98f);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.interactable = action != null; if (action != null) b.onClick.AddListener(() => action());
            Text(label, r, font); var le = r.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = height; return b;
        }
        public static Toggle Checkbox(string label, Transform parent, TMP_FontAsset font, Action<bool> change)
        {
            var rect=Rect(string.IsNullOrEmpty(label)?"Track task":label,parent,Vector2.zero,Vector2.one);
            rect.gameObject.AddComponent<Image>().color=new Color(.12f,.17f,.24f);
            var toggle=rect.gameObject.AddComponent<Toggle>();
            var box=Rect("Checkbox",rect,new Vector2(0,.5f),new Vector2(0,.5f));box.pivot=new Vector2(0,.5f);box.sizeDelta=new Vector2(28,28);box.anchoredPosition=new Vector2(6,0);
            var background=box.gameObject.AddComponent<Image>();background.color=new Color(.3f,.38f,.46f);toggle.targetGraphic=background;
            var mark=Text("✓",box,font,22);mark.alignment=TextAlignmentOptions.Center;mark.margin=Vector4.zero;mark.color=new Color(.5f,1,.75f);toggle.graphic=mark;
            var caption=Text(label,rect,font,18);caption.rectTransform.offsetMin=new Vector2(36,0);caption.alignment=TextAlignmentOptions.MidlineLeft;
            if(change!=null)toggle.onValueChanged.AddListener(v=>change(v));return toggle;
        }
        public static RectTransform Scroll(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent, min, max); rect.gameObject.AddComponent<Image>().color = new Color(.045f,.07f,.1f,.98f);
            var sr = rect.gameObject.AddComponent<ScrollRect>(); sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 28;
            var viewport = Rect("Viewport", rect, Vector2.zero, Vector2.one); viewport.offsetMin = new Vector2(4, 4); viewport.offsetMax = new Vector2(-16, -4); viewport.gameObject.AddComponent<RectMask2D>(); sr.viewport = viewport;
            var content = Rect("Content", viewport, new Vector2(0,1), Vector2.one); content.pivot = new Vector2(.5f,1);
            var group = content.gameObject.AddComponent<VerticalLayoutGroup>(); group.spacing = 6; group.padding = new RectOffset(6,6,6,6); group.childControlHeight = group.childControlWidth = true; group.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; sr.content = content;
            var track = Rect("Scrollbar", rect, new Vector2(.98f,0), Vector2.one); track.gameObject.AddComponent<Image>().color = new Color(.1f,.13f,.18f);
            var handle = Rect("Handle", track, Vector2.zero, Vector2.one); var image = handle.gameObject.AddComponent<Image>(); image.color = new Color(.6f,.7f,.8f);
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle; bar.targetGraphic = image; bar.direction = Scrollbar.Direction.BottomToTop; sr.verticalScrollbar = bar;
            return content;
        }
        public static TMP_InputField Input(string placeholder, Transform parent, TMP_FontAsset font, int limit = 32)
        {
            var r = Rect(placeholder, parent, Vector2.zero, Vector2.one); r.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            r.gameObject.AddComponent<Image>().color = new Color(.12f,.17f,.24f); var input = r.gameObject.AddComponent<TMP_InputField>(); input.characterLimit = limit;
            var area = Rect("Text area", r, Vector2.zero, Vector2.one); area.offsetMin = new Vector2(8,4); area.offsetMax = new Vector2(-8,-4); area.gameObject.AddComponent<RectMask2D>();
            input.textViewport = area; input.textComponent = Text("", area, font); input.placeholder = Text(placeholder, area, font); input.placeholder.color = Color.gray; return input;
        }
        public static Slider Slider(string label, Transform parent, TMP_FontAsset font, float min, float max, float value, Action<float> change)
        {
            var row = Rect(label, parent, Vector2.zero, Vector2.one); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;
            var caption = Text(label, Rect("Caption",row,new Vector2(0,.45f),Vector2.one),font);
            var track = Rect("Slider",row,new Vector2(.05f,.08f),new Vector2(.95f,.4f)); track.gameObject.AddComponent<Image>().color = new Color(.18f,.25f,.34f);
            var slider = track.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max;
            var area = Rect("Handle area",track,Vector2.zero,Vector2.one); area.offsetMin = new Vector2(10,0); area.offsetMax = new Vector2(-10,0);
            var handle = Rect("Handle",area,Vector2.zero,Vector2.one); handle.sizeDelta = new Vector2(20,0); var image = handle.gameObject.AddComponent<Image>(); image.color = Color.white; slider.handleRect = handle; slider.targetGraphic = image;
            slider.SetValueWithoutNotify(value); caption.text = label + "  " + value.ToString("0.##"); slider.onValueChanged.AddListener(v => { caption.text = label + "  " + v.ToString("0.##"); change(v); }); return slider;
        }
        public static GameObject Modal(string name, Transform parent, int order, out RectTransform card)
        {
            var r = Rect(name,parent,Vector2.zero,Vector2.one); r.gameObject.AddComponent<Image>().color = new Color(0,0,0,.8f);
            var canvas = r.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = order; r.gameObject.AddComponent<GraphicRaycaster>(); r.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            card = Rect("Card",r,new Vector2(.12f,.08f),new Vector2(.88f,.92f)); card.gameObject.AddComponent<Image>().color = new Color(.06f,.085f,.12f); return r.gameObject;
        }
    }
}
