using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetails_Block_岗位 : UI_GamePanel_BuildingDetails_Block
    {
        [Sirenix.OdinInspector.LabelText("岗位")]
        public TMP_Text Jobs;
        [Sirenix.OdinInspector.LabelText("预算")]
        public TMP_Text Budget;
        [Sirenix.OdinInspector.LabelText("吸引力")]
        public TMP_Text Attraction;
        [Sirenix.OdinInspector.LabelText("增加")]
        public Button Increase;
        [Sirenix.OdinInspector.LabelText("减少")]
        public Button Decrease;
        [Sirenix.OdinInspector.LabelText("招募工人")]
        public Button RecruitWorker;
        [Sirenix.OdinInspector.LabelText("释放工人")]
        public Button ReleaseWorker;
        [Sirenix.OdinInspector.LabelText("侧栏触发器")]
        public UI_GamePanel_BuildingDetails_SidebarTrigger Hover;
        [Sirenix.OdinInspector.LabelText("自然填充")]
        public Image NaturalFill;
        [Sirenix.OdinInspector.LabelText("补贴填充")]
        public Image SubsidyFill;
        [Sirenix.OdinInspector.LabelText("岗位刻度")]
        public List<Image> JobTicks = new List<Image>();

        public override void ValidateConfiguration()
        {
            ValidateReferences((Jobs, nameof(Jobs)), (Budget, nameof(Budget)), (Attraction, nameof(Attraction)),
                (Increase, nameof(Increase)), (Decrease, nameof(Decrease)),
                (RecruitWorker, nameof(RecruitWorker)), (ReleaseWorker, nameof(ReleaseWorker)), (Hover, nameof(Hover)),
                (NaturalFill, nameof(NaturalFill)), (SubsidyFill, nameof(SubsidyFill)));
            Hover.ValidateConfiguration();
            if (Hover.View != View)
                throw new InvalidOperationException(name + " 模块的侧栏目标错误。");
            if (JobTicks == null || JobTicks.Count != 10 || JobTicks.Exists(mark => mark == null))
                throw new InvalidOperationException(name + " 模块必须配置 10 个岗位刻度。");
        }

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
