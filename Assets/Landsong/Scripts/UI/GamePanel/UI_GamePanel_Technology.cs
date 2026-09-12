using System.Collections.Generic;
using Unity.Entities;
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using Text = TMPro.TextMeshProUGUI;
using System.Text;
using Unity.Transforms;
using UnityEngine.EventSystems;
using InputField = TMPro.TMP_InputField;
using Landsong.ECS.Persistence;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Technology : Moyo.Unity.UIViewBase, IGameFeatureRenderer
    {
        public void BindFeature(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation, UI_GamePanel_RowRenderer rows)
        {
            if (session == null || commands == null || navigation == null || rows == null)
                throw new System.ArgumentException("功能展示器缺少会话服务。");
            sessionController = session;
            rowsController = rows;
            commandsController = commands;
            this.navigation = navigation;
        }

        public void Render() => Research();
        internal UI_GamePanel_Building buildingController;
        internal GameUiCommandWriter commandsController;
        internal IGameUiNavigation navigation;
        internal UI_GamePanel_RowRenderer rowsController;
        internal GameUiSession sessionController;
        [Sirenix.OdinInspector.LabelText("研究信息栏")]
        public UI_GamePanel_ResearchHud ResearchHud;
        public TextMeshProUGUI ResearchHudName => ResearchHud != null ? ResearchHud.Name : null;
        public TextMeshProUGUI ResearchHudEffects => ResearchHud != null ? ResearchHud.Effects : null;
        public Image ResearchHudProgress => ResearchHud != null ? ResearchHud.Progress : null;
        public int CurrentResearchDefinition { get; internal set; } = -1;

        internal Image researchHudIcon;
        internal TextMeshProUGUI researchHudStatus;
        internal TextMeshProUGUI researchHudIconFallback;
        internal float nextResearchHudRefresh;
        internal bool focusResearchHud;
        internal void InitializeResearchHud()
        {
            if (ResearchHud == null)
                throw new System.InvalidOperationException("科技 HUD 检查器引用缺失。");
            ResearchHud.ValidateConfiguration();
            TechnologyButton = ResearchHud.Open;
            TechnologyButton.onClick = new Button.ButtonClickedEvent();
            TechnologyButton.onClick.AddListener(() =>
            {
                if (!navigation.InputPolicy.Capture().CanNavigate)
                    return;
                var queue = ResearchOps.Queue(sessionController.em, sessionController.root);
                selectedTechnology = queue.Count > 0 ? queue[0].Definition : -1;
                focusResearchHud = true;
                navigation.OpenPanel(GamePanelId.Technology);
            });
            researchHudIcon = ResearchHud.Icon;
            researchHudIconFallback = ResearchHud.IconFallback;
            researchHudStatus = ResearchHud.Status;
            TechnologyButton.gameObject.SetActive(false);
        }

        internal void RefreshResearchHud()
        {
            if (TechnologyButton == null || ResearchHudName == null || Time.unscaledTime < nextResearchHudRefresh)
                return;
            nextResearchHudRefresh = Time.unscaledTime + .2f;
            bool unlocked = ResearchOps.Unlocked(sessionController.em, sessionController.root);
            TechnologyButton.gameObject.SetActive(unlocked);
            if (!unlocked)
            {
                CurrentResearchDefinition = -1;
                return;
            }

            TechnologyButton.interactable = true;
            var queue = ResearchOps.Queue(sessionController.em, sessionController.root);
            CurrentResearchDefinition = queue.Count > 0 ? queue[0].Definition : -1;
            if (CurrentResearchDefinition < 0)
            {
                ResearchHudName.text = "尚未选择科技";
                researchHudStatus.text = "点击选择研究项目";
                ResearchHudEffects.text = "选择科技，规划王朝的发展方向。";
                SetResearchHudProgress(0);
                researchHudIcon.sprite = null;
                researchHudIcon.color = new Color(.18f, .22f, .30f, 1);
                researchHudIconFallback.gameObject.SetActive(true);
                return;
            }

            int definition = CurrentResearchDefinition;
            var quote = ResearchOps.Quote(sessionController.em, sessionController.root, definition);
            var source = buildingController.BuildingSource(definition);
            ResearchHudName.text = sessionController.Name(definition);
            researchHudStatus.text = UI_GamePanel_TechnologyTree.StatusName(quote.Status) + "  " + quote.Progress + " / " + quote.Cost;
            SetResearchHudProgress(quote.Cost <= 0 ? 1 : Mathf.Clamp01((float)quote.Progress / quote.Cost));
            researchHudIcon.sprite = source?.Icon;
            researchHudIcon.color = researchHudIcon.sprite != null ? Color.white : new Color(.18f, .22f, .30f, 1);
            researchHudIconFallback.gameObject.SetActive(researchHudIcon.sprite == null);
            ResearchHudEffects.text = quote.Rewards.Count > 0 ? string.Join("；", quote.Rewards.Select(reward => (reward.Kind == RuleKind.RewardBlueprint ? "解锁建筑：" : reward.Kind == RuleKind.RewardFeature ? "解锁功能：" : reward.Kind == RuleKind.RewardBuff ? "获得增益：" : "获得物品：") + sessionController.Name(reward.Target) + (reward.Kind == RuleKind.RewardItem ? " ×" + reward.Amount : ""))) : !string.IsNullOrWhiteSpace(source?.Description) ? source.Description : "点击查看科技详情。";
        }

        internal void SetResearchHudProgress(float value)
        {
            ResearchHudProgress.rectTransform.anchorMax = new Vector2(value, 1);
            ResearchHudProgress.rectTransform.offsetMin = ResearchHudProgress.rectTransform.offsetMax = Vector2.zero;
        }

        [Sirenix.OdinInspector.LabelText("科技按钮")]
        public Button TechnologyButton;
        [Sirenix.OdinInspector.LabelText("科技树视图")]
        public UI_GamePanel_TechnologyTree TechnologyTree;
        internal int selectedTechnology = -1;
        internal void RefreshTechnologyAccess()
        {
            var unlocked = ResearchOps.Unlocked(sessionController.em, sessionController.root);
            if (!unlocked && navigation.Panel == GamePanelId.Technology)
                navigation.ClosePanel();
            if (TechnologyTree != null)
                TechnologyTree.gameObject.SetActive(navigation.IsPanelOpen && navigation.Panel == GamePanelId.Technology);
        }

        internal void SelectTechnology(int definition)
        {
            selectedTechnology = definition;
            sessionController.nextRefresh = 0;
        }

        internal void Research()
        {
            if (TechnologyTree == null)
                throw new InvalidOperationException("科技面板检查器引用缺失：TechnologyTree");
            var created = !TechnologyTree.gameObject.activeSelf;
            TechnologyTree.BindActions(SelectTechnology, navigation.ClosePanel);
            TechnologyTree.gameObject.SetActive(true);
            var queue = ResearchOps.Queue(sessionController.em, sessionController.root);
            var nodes = new List<TechnologyNodeModel>();
            var columns = new Dictionary<int, int>();
            var columnRows = new Dictionary<int, int>();
            int Depth(int definition, HashSet<int> visiting)
            {
                if (columns.TryGetValue(definition, out var known))
                    return known;
                if (!visiting.Add(definition))
                    return 0;
                var depth = 0;
                foreach (var p in ResearchOps.Quote(sessionController.em, sessionController.root, definition).Prerequisites)
                    depth = Math.Max(depth, Depth(p, visiting) + 1);
                visiting.Remove(definition);
                columns[definition] = depth;
                return depth;
            }

            sessionController.ForDefinitions(ContentKind.Technology, (index, d) =>
            {
                var source = buildingController.BuildingSource(index);
                var column = Depth(index, new HashSet<int>());
                columnRows.TryGetValue(column, out var row);
                columnRows[column] = row + 1;
                nodes.Add(new TechnologyNodeModel { Definition = index, Name = d.Name.ToString(), Icon = source?.Icon, Position = source != null && source.HasTechnologyPosition ? source.TechnologyPosition : new Vector2(column * 220, row * 150), Quote = ResearchOps.Quote(sessionController.em, sessionController.root, index) });
            });
            if (!nodes.Any(n => n.Definition == selectedTechnology))
                selectedTechnology = queue.Count > 0 ? queue[0].Definition : nodes.FirstOrDefault(n => n.Quote.Completions == 0 && n.Quote.PrerequisitesMet)?.Definition ?? (nodes.Count > 0 ? nodes[0].Definition : -1);
            TechnologyTree.Bind(nodes, selectedTechnology, sessionController.em.GetComponentData<Session>(sessionController.root).ResearchPoints, queue.Count > 0 ? queue[0].Definition : -1);
            if (created || focusResearchHud)
            {
                TechnologyTree.Focus(selectedTechnology);
                focusResearchHud = false;
            }

            rowsController.Clear(TechnologyTree.DetailRows);
            void Detail(string label, Action action = null, string key = null) => rowsController.Row(label, action, parent: TechnologyTree.DetailRows, key: key);
            if (selectedTechnology < 0)
            {
                Detail("未配置科技");
                return;
            }

            var at = selectedTechnology;
            var q = ResearchOps.Quote(sessionController.em, sessionController.root, at);
            var data = buildingController.BuildingSource(at);
            Detail(sessionController.Name(at) + " · " + UI_GamePanel_TechnologyTree.StatusName(q.Status));
            if (!string.IsNullOrWhiteSpace(data?.Description))
                Detail(data.Description);
            var technologyDefinition = Sim.Definition(sessionController.em, sessionController.root, at);
            for (int i = 0; i < technologyDefinition.RuleCount; i++)
            {
                var effect = Sim.GetRule(sessionController.em, sessionController.root, technologyDefinition.RuleStart + i);
                if (effect.Kind == RuleKind.Intelligence)
                    Detail("被动情报来源：+" + effect.Amount + " 完善度（总计上限 100；条件见情报界面）");
            }

            Detail((q.Status == ResearchStatus.Completed ? $"研究成本 {q.Cost} 点" : $"研究进度 {q.Progress}/{q.Cost}") + $" · 已完成 {q.Completions} 次\n{q.Reason}");
            if (q.Status != ResearchStatus.Completed)
            {
                Detail("加入研究队尾", q.CanQueue == ResultCode.Success ? () => commandsController.TryQueue(CommandRequests.QueueResearch(at)) : null, "research-add:" + at);
                Detail("取消该项排队（保留进度）", q.Editable && q.Order > 0 ? () => commandsController.TryQueue(CommandRequests.CancelResearch(at)) : null, "research-cancel:" + at);
                var path = ResearchOps.Path(sessionController.em, sessionController.root, at);
                Detail("规划至此科技（预览前置路径）", q.Editable && path.Code == ResultCode.Success ? () => ConfirmResearchPath(at) : null);
            }

            Detail("研究点累计保留，在白天结算时按队列依次消耗；余额足够可连续完成。取消保留进度，不退回已投入点数。");
            if (q.Repeatable)
                Detail("可重复研究：每次须重新排队，完成次数累加；下列奖励只在首次完成时发放。");
            Detail("前置科技（全部满足）");
            if (q.Prerequisites.Count == 0)
                Detail("无前置");
            foreach (var parent in q.Prerequisites)
            {
                var p = parent;
                Detail((ResearchOps.Completed(sessionController.em, sessionController.root, p) > 0 ? "✓ " : "○ ") + sessionController.Name(p), () =>
                {
                    SelectTechnology(p);
                    TechnologyTree.Focus(p);
                }, "research-prerequisite:" + p);
            }

            Detail(q.Completions > 0 ? "首次奖励（已领取，不重复发放）" : "首次完成奖励");
            if (q.Rewards.Count == 0)
                Detail("无额外奖励");
            foreach (var reward in q.Rewards)
            {
                var label = reward.Kind == RuleKind.RewardItem ? "物品：" + sessionController.Name(reward.Target) + " ×" + reward.Amount : reward.Kind == RuleKind.RewardBlueprint ? "建筑许可：" + sessionController.Name(reward.Target) + " 最高 LV" + reward.Amount : reward.Kind == RuleKind.RewardFeature ? "功能许可：" + sessionController.Name(reward.Target) : "永久增益：" + sessionController.Name(reward.Target);
                Detail(label);
                var rewardData = buildingController.BuildingSource(reward.Target);
                if (!string.IsNullOrWhiteSpace(rewardData?.Description))
                    Detail(rewardData.Description);
                if (reward.Kind == RuleKind.RewardBuff)
                {
                    var buff = Sim.Definition(sessionController.em, sessionController.root, reward.Target);
                    for (var i = 0; i < buff.RuleCount; i++)
                    {
                        var rule = Sim.GetRule(sessionController.em, sessionController.root, buff.RuleStart + i);
                        Detail(rule.Kind == RuleKind.LossModifier ? $"{(rule.Target < 0 ? "全部物资" : sessionController.Name(rule.Target))} 损耗降低 {rule.Value:P0}（同类减损相加，上限 100%）" : rule.Kind == RuleKind.FlatProductionBonus ? $"{sessionController.Name(rule.Secondary)}：{sessionController.Name(rule.Target)} 固定产量 +{rule.Amount}" : $"{rule.Kind} · {sessionController.Name(rule.Target)} · 数量 {rule.Amount} / 数值 {rule.Value:0.##}");
                    }
                }

                if (reward.Kind == RuleKind.RewardBlueprint && BlueprintOps.Has(sessionController.em, sessionController.root, reward.Target))
                    Detail("前往建造目录 · " + sessionController.Name(reward.Target), () => navigation.OpenPanel(GamePanelId.Building));
            }

            Detail("研究计划 · " + queue.Count + " 项");
            if (queue.Count == 0)
                Detail("暂无研究计划");
            foreach (var entry in queue)
            {
                var id = entry.Definition;
                var state = ResearchOps.Quote(sessionController.em, sessionController.root, id);
                Detail($"#{entry.QueueOrder} {sessionController.Name(id)} · {entry.Progress}/{state.Cost}" + (!state.PrerequisitesMet ? "（等待前置）" : ""), () =>
                {
                    SelectTechnology(id);
                    TechnologyTree.Focus(id);
                }, "research-queue:" + id);
            }
        }

        internal void ConfirmResearchPath(int definition)
        {
            var path = ResearchOps.Path(sessionController.em, sessionController.root, definition);
            if (path.Code != ResultCode.Success)
                return;
            var stamp = ResearchOps.Fingerprint(sessionController.em, sessionController.root);
            var lines = new List<string>
            {
                path.Reason,
                "尚需研究点：" + path.Remaining
            };
            lines.AddRange(path.Definitions.Select((at, i) => (i + 1) + ". " + sessionController.Name(at)));
            buildingController.ShowBuildingConfirmation("替换研究计划 → " + sessionController.Name(definition), lines, () => commandsController.TryQueue(CommandRequests.PlanResearch(definition, stamp)));
        }

        internal void ResetSession()
        {
            selectedTechnology = CurrentResearchDefinition = -1;
            nextResearchHudRefresh = 0;
            focusResearchHud = false;
            TechnologyTree.ClearSession();
            TechnologyTree.gameObject.SetActive(false);
        }
    }
}
