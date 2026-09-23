using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using Landsong.ECS.Definitions;
using Unity.Entities;
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
        [LabelText("自然填充"), Required]
        public Image NaturalFill;
        [LabelText("补贴填充"), Required]
        public Image SubsidyFill;
        [LabelText("岗位刻度"), Required]
        public List<Image> JobTicks = new List<Image>();

        public void Refresh(Entity entity, bool editable)
        {
            var session = View.sessionController;
            var quote = WorkforceOps.Quote(session.em, session.root, entity);
            var id = session.em.GetComponentData<Identity>(entity).Id;
            bool visible = quote.Capacity > 0;
            gameObject.SetActive(visible);
            BindSidebar(visible ? () => AttractionDetails(WorkforceOps.Quote(session.em, session.root, entity)) : null);
            if (!visible)
            {
                Bind(Increase, null);
                Bind(Decrease, null);
                Bind(RecruitWorker, null);
                Bind(ReleaseWorker, null);
                return;
            }

            Jobs.text = $"岗位：{quote.Workers}/{quote.Capacity}";
            Budget.text = $"补贴 {quote.SubsidyCost}";
            string item = quote.Gold.IsValid ? ItemDefinitions.Get(session.em, session.root, quote.Gold).Metadata.Name.ToString() : "—";
            RecruitWorkerCost.text = $"{quote.RecruitCost} {item}";
            Bind(Increase, editable && !quote.Locked && quote.SubsidyCost < quote.Capacity ? () => View.commandsController.TryQueue(new SetWorkforceBudgetRequest { Building = id, Budget = 1, Relative = 1 }) : null);
            Bind(Decrease, editable && !quote.Locked && quote.SubsidyCost > 0 ? () => View.commandsController.TryQueue(new SetWorkforceBudgetRequest { Building = id, Budget = -1, Relative = 1 }) : null);
            Bind(RecruitWorker, editable && WorkforceOps.CanChange(quote, 1) == ResultCode.Success ? () => View.commandsController.TryQueue(new RecruitWorkersRequest { Building = id, Count = 1, ExpectedGoldCostPerWorker = quote.RecruitCost }) : null);
            Bind(ReleaseWorker, editable && WorkforceOps.CanChange(quote, -1) == ResultCode.Success ? () => View.commandsController.TryQueue(new ChangeWorkersRequest { Building = id, Delta = -1 }) : null);
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
        }

        static string AttractionDetails(WorkforceQuote quote) =>
            $"基础吸引力：{quote.Natural:0.#}\n\n补贴吸引力：{quote.Planned - quote.Natural:0.#}";
    }
}
