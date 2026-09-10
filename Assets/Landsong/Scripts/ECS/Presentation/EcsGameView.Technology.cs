using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;


namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public Button TechnologyButton;
        public TechnologyTreeView TechnologyTree { get; private set; }
        int selectedTechnology = -1;
        void RefreshTechnologyAccess()
        {
            var unlocked = ResearchOps.Unlocked(em, root);
            if (!unlocked && Panel == "科技") ClosePanel();
            if (TechnologyTree != null) TechnologyTree.gameObject.SetActive(Panel == "科技");
        }
        void SelectTechnology(int definition) { selectedTechnology = definition; nextRefresh = 0; }
        void Research()
        {
            var created = TechnologyTree == null;
            if (created) TechnologyTree = TechnologyTreeView.Create(GetComponentInParent<Canvas>().transform, Status.font, SelectTechnology, ClosePanel);
            TechnologyTree.gameObject.SetActive(true);
            var queue = ResearchOps.Queue(em, root); var nodes = new List<TechnologyNodeModel>();
            var columns = new Dictionary<int, int>(); var columnRows = new Dictionary<int, int>();
            int Depth(int definition, HashSet<int> visiting)
            {
                if (columns.TryGetValue(definition, out var known)) return known;
                if (!visiting.Add(definition)) return 0; var depth = 0;
                foreach (var p in ResearchOps.Quote(em, root, definition).Prerequisites) depth = Math.Max(depth, Depth(p, visiting) + 1);
                visiting.Remove(definition); columns[definition] = depth; return depth;
            }
            ForDefinitions(ContentKind.Technology, (index, d) =>
            {
                var source = BuildingSource(index); var column = Depth(index, new HashSet<int>());
                columnRows.TryGetValue(column, out var row); columnRows[column] = row + 1;
                nodes.Add(new TechnologyNodeModel { Definition = index, Name = d.Name.ToString(), Icon = source?.Icon,
                    Position = source != null && source.HasTechnologyPosition ? source.TechnologyPosition : new Vector2(column * 220, row * 150), Quote = ResearchOps.Quote(em, root, index) });
            });
            if (!nodes.Any(n => n.Definition == selectedTechnology)) selectedTechnology = queue.Count > 0 ? queue[0].Definition : nodes.FirstOrDefault(n => n.Quote.Completions == 0 && n.Quote.PrerequisitesMet)?.Definition ?? (nodes.Count > 0 ? nodes[0].Definition : -1);
            TechnologyTree.Bind(nodes, selectedTechnology, em.GetComponentData<Session>(root).ResearchPoints, queue.Count > 0 ? queue[0].Definition : -1);
            if (created || focusResearchHud) { TechnologyTree.Focus(selectedTechnology); focusResearchHud = false; }
            Clear(TechnologyTree.DetailRows);
            void Detail(string label, Action action = null) => Row(label, action, parent: TechnologyTree.DetailRows);
            if (selectedTechnology < 0) { Detail("未配置科技"); return; }
            var at = selectedTechnology; var q = ResearchOps.Quote(em, root, at); var data = BuildingSource(at);
            Detail(Name(at) + " · " + TechnologyTreeView.StatusName(q.Status));
            if (!string.IsNullOrWhiteSpace(data?.Description)) Detail(data.Description);
            if (data != null) foreach (var effect in data.Rules) if (effect.Kind == RuleKind.Intelligence) Detail("被动情报来源：+" + effect.Amount + " 完善度（总计上限 100；条件见情报界面）");
            Detail((q.Status == ResearchStatus.Completed ? $"研究成本 {q.Cost} 点" : $"研究进度 {q.Progress}/{q.Cost}") + $" · 已完成 {q.Completions} 次\n{q.Reason}");
            if (q.Status != ResearchStatus.Completed)
            {
                Detail("加入研究队尾", q.CanQueue == ResultCode.Success ? () => Send(CommandKind.Research, definition: at) : null);
                Detail("取消该项排队（保留进度）", q.Editable && q.Order > 0 ? () => Send(CommandKind.CancelResearch, definition: at) : null);
                var path = ResearchOps.Path(em, root, at);
                Detail("规划至此科技（预览前置路径）", q.Editable && path.Code == ResultCode.Success ? () => ConfirmResearchPath(at) : null);
            }
            Detail("研究点累计保留，在白天结算时按队列依次消耗；余额足够可连续完成。取消保留进度，不退回已投入点数。");
            if (q.Repeatable) Detail("可重复研究：每次须重新排队，完成次数累加；下列奖励只在首次完成时发放。");
            Detail("前置科技（全部满足）");
            if (q.Prerequisites.Count == 0) Detail("无前置");
            foreach (var parent in q.Prerequisites) { var p = parent; Detail((ResearchOps.Completed(em, root, p) > 0 ? "✓ " : "○ ") + Name(p), () => { SelectTechnology(p); TechnologyTree.Focus(p); }); }
            Detail(q.Completions > 0 ? "首次奖励（已领取，不重复发放）" : "首次完成奖励");
            if (q.Rewards.Count == 0) Detail("无额外奖励");
            foreach (var reward in q.Rewards)
            {
                var label = reward.Kind == RuleKind.RewardItem ? "物品：" + Name(reward.Target) + " ×" + reward.Amount
                    : reward.Kind == RuleKind.RewardBlueprint ? "建筑许可：" + Name(reward.Target) + " 最高 LV" + reward.Amount
                    : reward.Kind == RuleKind.RewardFeature ? "功能许可：" + Name(reward.Target) : "永久增益：" + Name(reward.Target);
                Detail(label);
                var rewardData = BuildingSource(reward.Target);
                if (!string.IsNullOrWhiteSpace(rewardData?.Description)) Detail(rewardData.Description);
                if (reward.Kind == RuleKind.RewardBuff)
                {
                    var buff = Sim.Definition(em, root, reward.Target);
                    for (var i = 0; i < buff.RuleCount; i++)
                    {
                        var rule = Sim.GetRule(em, root, buff.RuleStart + i);
                        Detail(rule.Kind == RuleKind.LossModifier ? $"{(rule.Target < 0 ? "全部物资" : Name(rule.Target))} 损耗降低 {rule.Value:P0}（同类减损相加，上限 100%）"
                            : rule.Kind == RuleKind.FlatProductionBonus ? $"{Name(rule.Secondary)}：{Name(rule.Target)} 固定产量 +{rule.Amount}"
                            : $"{rule.Kind} · {Name(rule.Target)} · 数量 {rule.Amount} / 数值 {rule.Value:0.##}");
                    }
                }
                if (reward.Kind == RuleKind.RewardBlueprint && Sim.HasGrant(em, root, reward.Target)) Detail("前往建造目录 · " + Name(reward.Target), () => OpenPanel("建筑"));
            }
            Detail("研究计划 · " + queue.Count + " 项");
            if (queue.Count == 0) Detail("暂无研究计划");
            foreach (var entry in queue)
            {
                var id = entry.Definition; var state = ResearchOps.Quote(em, root, id);
                Detail($"#{entry.QueueOrder} {Name(id)} · {entry.Progress}/{state.Cost}" + (!state.PrerequisitesMet ? "（等待前置）" : ""), () => { SelectTechnology(id); TechnologyTree.Focus(id); });
            }
        }
        void ConfirmResearchPath(int definition)
        {
            var path = ResearchOps.Path(em, root, definition); if (path.Code != ResultCode.Success) return;
            var stamp = ResearchOps.Fingerprint(em, root);
            var lines = new List<string> { path.Reason, "尚需研究点：" + path.Remaining };
            lines.AddRange(path.Definitions.Select((at, i) => (i + 1) + ". " + Name(at)));
            ShowBuildingConfirmation("替换研究计划 → " + Name(definition), lines, () => Send(CommandKind.PlanResearch, definition: definition, text: stamp));
        }
    }
}
