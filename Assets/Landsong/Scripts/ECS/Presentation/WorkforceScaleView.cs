using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using Font = TMPro.TMP_FontAsset;

namespace Landsong.ECS.Presentation
{
    // Basic UGUI only. All numbers are supplied by WorkforceOps; no payment or population writes here.
    public sealed class WorkforceScaleView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool Interacting { get; private set; }
        public int MaximumVisibleTicks = 12;
        public Slider Slider { get; private set; }
        Image natural, subsidy, overflow, actual;
        RectTransform track;
        Font font;
        readonly List<Button> ticks = new List<Button>();
        Action<int> changed;
        bool binding;
        public void Initialize(Font labelFont)
        {
            font = labelFont;
            track = Rect("Track", transform); track.anchorMin = new Vector2(0, .5f); track.anchorMax = new Vector2(1, .5f); track.offsetMin = new Vector2(16, -6); track.offsetMax = new Vector2(-16, 6);
            track.gameObject.AddComponent<Image>().color = new Color(.12f, .14f, .18f);
            natural = Segment("Environment", new Color(.3f, .65f, .35f)); subsidy = Segment("Subsidy", new Color(.85f, .65f, .15f)); overflow = Segment("RoundingOverflow", new Color(.8f, .35f, .2f));
            actual = Segment("ActualWorkers", Color.cyan); actual.rectTransform.sizeDelta = new Vector2(3, 32);
            var handle = Rect("Target", track); handle.sizeDelta = new Vector2(12, 30); var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Color.white;
            Slider = gameObject.AddComponent<Slider>(); Slider.wholeNumbers = true; Slider.handleRect = handle; Slider.targetGraphic = handleImage;
            Slider.onValueChanged.AddListener(v => { if (!binding) changed?.Invoke(Mathf.RoundToInt(v)); });
        }
        static RectTransform Rect(string name, Transform parent)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        Image Segment(string name, Color color) { var rect = Rect(name, track); var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
        static void Span(Image image, float from, float to)
        { var rect = image.rectTransform; rect.anchorMin = new Vector2(Mathf.Clamp01(from), 0); rect.anchorMax = new Vector2(Mathf.Clamp01(to), 1); rect.offsetMin = rect.offsetMax = Vector2.zero; }
        public static List<int> TickValues(int maximum, int natural, int workers, int target, int limit)
        {
            var result = new SortedSet<int> { 0, maximum, Mathf.Clamp(natural, 0, maximum), Mathf.Clamp(workers, 0, maximum), Mathf.Clamp(target, 0, maximum) };
            limit = Mathf.Max(5, limit);
            for (var i = 1; i < limit - 1 && result.Count < limit; i++) result.Add(Mathf.RoundToInt((float)maximum * i / (limit - 1)));
            return new List<int>(result);
        }
        public void Bind(WorkforceQuote q, bool editable, Action<int> action)
        {
            changed = action; binding = true; Slider.minValue = 0; Slider.maxValue = Mathf.Max(1, q.Capacity); Slider.SetValueWithoutNotify(q.Target); Slider.interactable = editable; binding = false;
            var naturalPosition = q.Capacity == 0 ? 0 : q.Natural * (q.Capacity + 1f) / 100 / q.Capacity;
            var targetPosition = q.Capacity == 0 ? 0 : (float)q.Target / q.Capacity;
            var plannedPosition = q.Capacity == 0 ? 0 : q.Planned * (q.Capacity + 1f) / 100 / q.Capacity;
            Span(natural, 0, naturalPosition); Span(subsidy, naturalPosition, Mathf.Max(naturalPosition, targetPosition)); Span(overflow, Mathf.Max(naturalPosition, targetPosition), plannedPosition);
            actual.rectTransform.anchorMin = actual.rectTransform.anchorMax = new Vector2(q.Capacity == 0 ? 0 : (float)q.Workers / q.Capacity, .5f); actual.rectTransform.anchoredPosition = Vector2.zero; actual.rectTransform.sizeDelta = new Vector2(3, 32);
            var values = TickValues(q.Capacity, q.NaturalStable, q.Workers, q.Target, MaximumVisibleTicks);
            for (var i = 0; i < Mathf.Max(ticks.Count, values.Count); i++)
            {
                if (i == ticks.Count)
                {
                    var rect = Rect("WorkerTick", track); rect.sizeDelta = new Vector2(28, 28); var label = rect.gameObject.AddComponent<Text>(); label.font = font; label.fontSize = 12; label.alignment = TMPro.TextAlignmentOptions.Center;
                    var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = label; ticks.Add(button);
                }
                var tick = ticks[i]; tick.gameObject.SetActive(i < values.Count); if (i >= values.Count) continue;
                var value = values[i]; var text = tick.GetComponent<Text>(); text.text = value.ToString(); text.color = value == q.Workers ? Color.cyan : value == q.Target ? Color.yellow : Color.white;
                var tr = (RectTransform)tick.transform; tr.anchorMin = tr.anchorMax = new Vector2(q.Capacity == 0 ? 0 : (float)value / q.Capacity, 0); tr.anchoredPosition = new Vector2(0, -22);
                tick.interactable = editable; tick.onClick.RemoveAllListeners(); tick.onClick.AddListener(() => changed?.Invoke(value));
            }
        }
        public void OnPointerDown(PointerEventData data) => Interacting = true;
        public void OnPointerUp(PointerEventData data) => Interacting = false;
        void OnDisable() => Interacting = false;
    }
}
