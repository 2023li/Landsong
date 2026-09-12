using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    // All visual parts and the reusable tick template are authored in the prefab.
    // WorkforceOps remains authoritative; this component only binds values and actions.
    public sealed class UI_GamePanel_WorkforceScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool Interacting { get; private set; }

        [Sirenix.OdinInspector.LabelText("最大可见刻度")]
        public int MaximumVisibleTicks = 12;
        [Sirenix.OdinInspector.LabelText("滑动条")]
        public Slider Slider;
        [Sirenix.OdinInspector.LabelText("自然")]
        public Image Natural;
        [Sirenix.OdinInspector.LabelText("补贴")]
        public Image Subsidy;
        [Sirenix.OdinInspector.LabelText("超出容量")]
        public Image Overflow;
        [Sirenix.OdinInspector.LabelText("实际")]
        public Image Actual;
        [Sirenix.OdinInspector.LabelText("跟踪")]
        public RectTransform Track;
        [Sirenix.OdinInspector.LabelText("刻度根对象")]
        public RectTransform TickRoot;
        [Sirenix.OdinInspector.LabelText("刻度模板")]
        public UI_GamePanel_WorkforceTick TickTemplate;
        readonly List<UI_GamePanel_WorkforceTick> ticks = new List<UI_GamePanel_WorkforceTick>();
        Action<int> changed;
        bool binding;
        public void ValidateConfiguration()
        {
            if (Slider == null || Natural == null || Subsidy == null || Overflow == null || Actual == null || Track == null || TickRoot == null || TickTemplate == null || TickTemplate.Select == null || TickTemplate.Label == null)
                throw new InvalidOperationException("岗位吸引力控件检查器引用不完整。");
        }

        static void Span(Image image, float from, float to)
        {
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(Mathf.Clamp01(from), 0);
            rect.anchorMax = new Vector2(Mathf.Clamp01(to), 1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static List<int> TickValues(int maximum, int natural, int workers, int target, int limit)
        {
            var result = new SortedSet<int>
            {
                0,
                maximum,
                Mathf.Clamp(natural, 0, maximum),
                Mathf.Clamp(workers, 0, maximum),
                Mathf.Clamp(target, 0, maximum)
            };
            limit = Mathf.Max(5, limit);
            for (var i = 1; i < limit - 1 && result.Count < limit; i++)
                result.Add(Mathf.RoundToInt((float)maximum * i / (limit - 1)));
            return new List<int>(result);
        }

        public void Bind(WorkforceQuote q, bool editable, Action<int> action)
        {
            ValidateConfiguration();
            changed = action;
            binding = true;
            Slider.minValue = 0;
            Slider.maxValue = Mathf.Max(1, q.Capacity);
            Slider.SetValueWithoutNotify(q.Target);
            Slider.interactable = editable;
            binding = false;
            Slider.onValueChanged.RemoveAllListeners();
            Slider.onValueChanged.AddListener(v =>
            {
                if (!binding)
                    changed?.Invoke(Mathf.RoundToInt(v));
            });
            var naturalPosition = q.Capacity == 0 ? 0 : q.Natural * (q.Capacity + 1f) / 100 / q.Capacity;
            var targetPosition = q.Capacity == 0 ? 0 : (float)q.Target / q.Capacity;
            var plannedPosition = q.Capacity == 0 ? 0 : q.Planned * (q.Capacity + 1f) / 100 / q.Capacity;
            Span(Natural, 0, naturalPosition);
            Span(Subsidy, naturalPosition, Mathf.Max(naturalPosition, targetPosition));
            Span(Overflow, Mathf.Max(naturalPosition, targetPosition), plannedPosition);
            Actual.rectTransform.anchorMin = Actual.rectTransform.anchorMax = new Vector2(q.Capacity == 0 ? 0 : (float)q.Workers / q.Capacity, .5f);
            Actual.rectTransform.anchoredPosition = Vector2.zero;
            Actual.rectTransform.sizeDelta = new Vector2(3, 32);
            var values = TickValues(q.Capacity, q.NaturalStable, q.Workers, q.Target, MaximumVisibleTicks);
            while (ticks.Count < values.Count)
            {
                var view = Instantiate(TickTemplate, TickRoot);
                view.gameObject.SetActive(true);
                ticks.Add(view);
            }

            for (var i = 0; i < ticks.Count; i++)
            {
                var tick = ticks[i];
                tick.gameObject.SetActive(i < values.Count);
                if (i >= values.Count)
                    continue;
                var value = values[i];
                tick.Label.text = value.ToString();
                tick.Label.color = value == q.Workers ? Color.cyan : value == q.Target ? Color.yellow : Color.white;
                var tr = (RectTransform)tick.transform;
                tr.anchorMin = tr.anchorMax = new Vector2(q.Capacity == 0 ? 0 : (float)value / q.Capacity, 0);
                tr.anchoredPosition = new Vector2(0, -22);
                tick.Select.interactable = editable;
                tick.Select.onClick.RemoveAllListeners();
                tick.Select.onClick.AddListener(() => changed?.Invoke(value));
            }
        }

        public void OnPointerDown(PointerEventData data) => Interacting = true;
        public void OnPointerUp(PointerEventData data) => Interacting = false;
        void OnDisable() => Interacting = false;
    }
}
