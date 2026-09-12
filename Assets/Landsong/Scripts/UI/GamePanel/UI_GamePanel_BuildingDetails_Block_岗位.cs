using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetails_Block_岗位 : UI_GamePanel_BuildingDetails_Block
    {
        [LabelText("岗位"), Required]
        public TMP_Text Jobs;
        [LabelText("预算"), Required]
        public TMP_Text Budget;
        [LabelText("吸引力"), Required]
        public TMP_Text Attraction;
        [LabelText("增加"), Required]
        public Button Increase;
        [LabelText("减少"), Required]
        public Button Decrease;
        [LabelText("招募工人"), Required]
        public Button RecruitWorker;
        [LabelText("招募工人费用"), Required]
        public TMP_Text RecruitWorkerCost;
        [LabelText("释放工人"), Required]
        public Button ReleaseWorker;
        [LabelText("侧栏触发器"), Required]
        public UI_GamePanel_BuildingDetails_SidebarTrigger Hover;
        [LabelText("自然填充"), Required]
        public Image NaturalFill;
        [LabelText("补贴填充"), Required]
        public Image SubsidyFill;
        [LabelText("岗位刻度"), Required]
        public List<Image> JobTicks = new List<Image>();

        public void Refresh(WorkforceQuote quote, string item, bool editable, Action<int> changeBudget,
            Action recruitWorker, Action releaseWorker, Func<string> sidebarContent)
        {
            bool visible = quote.Capacity > 0;
            gameObject.SetActive(visible);
            BindSidebar(Hover, visible ? sidebarContent : null);
            if (!visible)
            {
                Bind(RecruitWorker, null);
                Bind(ReleaseWorker, null);
                return;
            }

            Jobs.text = $"岗位：{quote.Workers}/{quote.Capacity}";
            Budget.text = $"补贴 {quote.SubsidyCost}";
            RecruitWorkerCost.text = $"{quote.RecruitCost} {item}";
            Bind(Increase, editable && !quote.Locked && quote.SubsidyCost < quote.Capacity ? () => changeBudget(quote.SubsidyCost + 1) : null);
            Bind(Decrease, editable && !quote.Locked && quote.SubsidyCost > 0 ? () => changeBudget(quote.SubsidyCost - 1) : null);
            Bind(RecruitWorker, recruitWorker);
            Bind(ReleaseWorker, releaseWorker);
            Span(NaturalFill, 0, quote.Natural / 100);
            Span(SubsidyFill, quote.Natural / 100, quote.Planned / 100);
            int count = Mathf.Min(JobTicks.Count, quote.Capacity);
            for (int i = 0; i < JobTicks.Count; i++)
            {
                var mark = JobTicks[i];
                mark.gameObject.SetActive(i < count);
                if (i >= count)
                    continue;
                int jobs = Mathf.CeilToInt((i + 1f) * quote.Capacity / count);
                var rect = mark.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2((i + 1f) / count, .5f);
                rect.sizeDelta = new Vector2(5, 28);
                rect.anchoredPosition = Vector2.zero;
                mark.color = quote.Workers >= jobs ? new Color(1, .94f, .13f) : new Color(.52f, .51f, .15f);
            }

            Attraction.text = $"白：自然 {quote.Natural:0.#} + 黄：预算加成 {quote.Planned - quote.Natural:0.#} / 100\n下次支付 {quote.SubsidyCost} {item} 后生效 · 当前实际 {quote.Current:0.#}" + (quote.Locked ? " · 在途锁定" : "");
        }
    }
}
