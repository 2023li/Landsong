using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Entities;
using System;
using System.Linq;
using Landsong.Content;
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
    public sealed class UI_GamePanel_Technology : GameFeatureViewBase
    {
        [Sirenix.OdinInspector.LabelText("科技显示目录"), Sirenix.OdinInspector.Required]
        public TechnologyDisplayCatalog Technologies;
        [Sirenix.OdinInspector.LabelText("奖励物品目录"), Sirenix.OdinInspector.Required]
        public ItemDisplayCatalog RewardItems;
        [Sirenix.OdinInspector.LabelText("奖励蓝图目录"), Sirenix.OdinInspector.Required]
        public BuildingDisplayCatalog RewardBuildings;
        [Sirenix.OdinInspector.LabelText("奖励增益目录"), Sirenix.OdinInspector.Required]
        public BuffDisplayCatalog RewardBuffs;
        [Sirenix.OdinInspector.LabelText("奖励功能目录"), Sirenix.OdinInspector.Required]
        public FeatureDisplayCatalog RewardFeatures;
        internal GameUiInputContext inputContext;
        public override void Render() => Research();
        internal UI_GamePanel_BuildingActionBar buildingController;
        [Sirenix.OdinInspector.LabelText("研究信息栏")]
        public UI_GamePanel_ResearchHud ResearchHud;
        public TextMeshProUGUI ResearchHudName => ResearchHud != null ? ResearchHud.Name : null;
        public TextMeshProUGUI ResearchHudEffects => ResearchHud != null ? ResearchHud.Effects : null;
        public Image ResearchHudProgress => ResearchHud != null ? ResearchHud.Progress : null;
        public TechnologyId CurrentResearchDefinition { get; internal set; }

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
                if (!inputContext.Policy.Capture().CanNavigate)
                    return;
                var queue = ResearchOps.Queue(sessionController.em, sessionController.root);
                selectedTechnology = queue.Count > 0 ? queue[0].Technology : TechnologyId.None;
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
                CurrentResearchDefinition = default;
                return;
            }

            TechnologyButton.interactable = true;
            var queue = ResearchOps.Queue(sessionController.em, sessionController.root);
            CurrentResearchDefinition = queue.Count > 0 ? queue[0].Technology : TechnologyId.None;
            if (!CurrentResearchDefinition.IsValid)
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

            var definition = CurrentResearchDefinition;
            var quote = ResearchOps.Quote(sessionController.em, sessionController.root, definition);
            var source = Technologies.Get(definition);
            ResearchHudName.text = TechnologyName(definition);
            researchHudStatus.text = UI_GamePanel_TechnologyTree.StatusName(quote.Status) + "  " + quote.Progress + " / " + quote.Cost;
            SetResearchHudProgress(quote.Cost <= 0 ? 1 : Mathf.Clamp01((float)quote.Progress / quote.Cost));
            researchHudIcon.sprite = source?.Icon;
            researchHudIcon.color = researchHudIcon.sprite != null ? Color.white : new Color(.18f, .22f, .30f, 1);
            researchHudIconFallback.gameObject.SetActive(researchHudIcon.sprite == null);
            ref var technology = ref TechnologyDefinitions.Get(sessionController.em, sessionController.root, definition);
            var rewards = RewardLines(ref technology.Rewards, false);
            ResearchHudEffects.text = rewards.Count > 0 ? string.Join("；", rewards) : !string.IsNullOrWhiteSpace(source?.Description) ? source.Description : "点击查看科技详情。";
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
        internal TechnologyId selectedTechnology;
        internal void RefreshTechnologyAccess()
        {
            var unlocked = ResearchOps.Unlocked(sessionController.em, sessionController.root);
            if (!unlocked && navigation.Panel == GamePanelId.Technology)
                navigation.ClosePanel();
            if (TechnologyTree != null)
                TechnologyTree.gameObject.SetActive(navigation.IsPanelOpen && navigation.Panel == GamePanelId.Technology);
        }

        internal void SelectTechnology(TechnologyId definition)
        {
            selectedTechnology = definition;
            refresh.NextPanel = 0;
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
            var columns = new Dictionary<TechnologyId, int>();
            var columnRows = new Dictionary<int, int>();
            int Depth(TechnologyId definition, HashSet<TechnologyId> visiting)
            {
                if (columns.TryGetValue(definition, out var known))
                    return known;
                if (!visiting.Add(definition))
                    return 0;
                var depth = 0;
                foreach (var p in TechnologyPrerequisites(definition))
                    depth = Math.Max(depth, Depth(p, visiting) + 1);
                visiting.Remove(definition);
                columns[definition] = depth;
                return depth;
            }

            for (int i = 0; i < TechnologyDefinitions.Count(sessionController.em, sessionController.root); i++)
            {
                var index = TechnologyId.FromIndex(i);
                ref var d = ref TechnologyDefinitions.Get(sessionController.em, sessionController.root, index);
                var source = Technologies.Get(index);
                var column = Depth(index, new HashSet<TechnologyId>());
                columnRows.TryGetValue(column, out var row);
                columnRows[column] = row + 1;
                nodes.Add(new TechnologyNodeModel { Definition = index, Name = d.Metadata.Name.ToString(), Icon = source?.Icon, Position = source != null && source.HasTreePosition ? source.TreePosition : new Vector2(column * 220, row * 150), Prerequisites = TechnologyPrerequisites(index), Quote = ResearchOps.Quote(sessionController.em, sessionController.root, index) });
            }

            if (!nodes.Any(n => n.Definition == selectedTechnology))
                selectedTechnology = queue.Count > 0 ? queue[0].Technology : nodes.FirstOrDefault(n => n.Quote.Completions == 0 && n.Quote.PrerequisitesMet)?.Definition ?? (nodes.Count > 0 ? nodes[0].Definition : TechnologyId.None);
            TechnologyTree.Bind(nodes, selectedTechnology, sessionController.em.GetComponentData<ResearchState>(sessionController.root).Points, queue.Count > 0 ? queue[0].Technology : TechnologyId.None);
            if (created || focusResearchHud)
            {
                TechnologyTree.Focus(selectedTechnology);
                focusResearchHud = false;
            }

            rowsController.Clear(TechnologyTree.DetailRows);
            void Detail(string label, Action action = null, string key = null) => rowsController.Row(label, action, parent: TechnologyTree.DetailRows, key: key);
            if (!selectedTechnology.IsValid)
            {
                Detail("未配置科技");
                return;
            }

            var at = selectedTechnology;
            var q = ResearchOps.Quote(sessionController.em, sessionController.root, at);
            var data = Technologies.Get(at);
            Detail(TechnologyName(at) + " · " + UI_GamePanel_TechnologyTree.StatusName(q.Status));
            if (!string.IsNullOrWhiteSpace(data?.Description))
                Detail(data.Description);
            ref var technologyDefinition = ref TechnologyDefinitions.Get(sessionController.em, sessionController.root, at);
            for (int i = 0; i < technologyDefinition.Effects.Intelligence.Length; i++)
                Detail("被动情报来源：+" + technologyDefinition.Effects.Intelligence[i].Points + " 完善度（总计上限 100；条件见情报界面）");
            Detail((q.Status == ResearchStatus.Completed ? $"研究成本 {q.Cost} 点" : $"研究进度 {q.Progress}/{q.Cost}") + $" · 已完成 {q.Completions} 次\n{q.Reason}");
            if (q.Status != ResearchStatus.Completed)
            {
                Detail("加入研究队尾", q.CanQueue == ResultCode.Success ? () => commandsController.TryQueue(new QueueResearchRequest { Technology = at }) : null, "research-add:" + at);
                Detail("取消该项排队（保留进度）", q.Editable && q.Order > 0 ? () => commandsController.TryQueue(new CancelResearchRequest { Technology = at }) : null, "research-cancel:" + at);
                var path = ResearchOps.Path(sessionController.em, sessionController.root, at);
                Detail("规划至此科技（预览前置路径）", q.Editable && path.Code == ResultCode.Success ? () => ConfirmResearchPath(at) : null);
            }

            Detail("研究点累计保留，在白天结算时按队列依次消耗；余额足够可连续完成。取消保留进度，不退回已投入点数。");
            if (q.Repeatable)
                Detail("可重复研究：每次须重新排队，完成次数累加；下列奖励只在首次完成时发放。");
            Detail("前置科技（全部满足）");
            var prerequisites = TechnologyPrerequisites(at);
            if (prerequisites.Count == 0)
                Detail("无前置");
            foreach (var parent in prerequisites)
            {
                var p = parent;
                Detail((ResearchOps.Completed(sessionController.em, sessionController.root, p) > 0 ? "✓ " : "○ ") + TechnologyName(p), () =>
                {
                    SelectTechnology(p);
                    TechnologyTree.Focus(p);
                }, "research-prerequisite:" + p);
            }

            Detail(q.Completions > 0 ? "首次奖励（已领取，不重复发放）" : "首次完成奖励");
            var rewardLines = RewardLines(ref technologyDefinition.Rewards, true);
            if (rewardLines.Count == 0)
                Detail("无额外奖励");
            foreach (var line in rewardLines)
                Detail(line);
            for (int i = 0; i < technologyDefinition.Rewards.Blueprints.Length; i++)
            {
                var building = technologyDefinition.Rewards.Blueprints[i].Building;
                if (BuildingBlueprints.Has(sessionController.em, sessionController.root, building))
                    Detail("前往建造目录 · " + BuildingDefinitions.Get(sessionController.em, sessionController.root, building).Metadata.Name, () => navigation.OpenPanel(GamePanelId.Building));
            }

            Detail("研究计划 · " + queue.Count + " 项");
            if (queue.Count == 0)
                Detail("暂无研究计划");
            foreach (var entry in queue)
            {
                var id = entry.Technology;
                var state = ResearchOps.Quote(sessionController.em, sessionController.root, id);
                Detail($"#{entry.QueueOrder} {TechnologyName(id)} · {entry.ResearchPoints}/{state.Cost}" + (!state.PrerequisitesMet ? "（等待前置）" : ""), () =>
                {
                    SelectTechnology(id);
                    TechnologyTree.Focus(id);
                }, "research-queue:" + id);
            }
        }

        internal void ConfirmResearchPath(TechnologyId definition)
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
            lines.AddRange(path.Definitions.Select((at, i) => (i + 1) + ". " + TechnologyName(at)));
            buildingController.ShowBuildingConfirmation("替换研究计划 → " + TechnologyName(definition), lines, () => commandsController.TryQueue(new PlanResearchRequest { Technology = definition, ExpectedPlan = new Unity.Collections.FixedString128Bytes(stamp) }));
        }

        string TechnologyName(TechnologyId id) => TechnologyDefinitions.IsValid(sessionController.em, sessionController.root, id) ? TechnologyDefinitions.Get(sessionController.em, sessionController.root, id).Metadata.Name.ToString() : "—";
        List<TechnologyId> TechnologyPrerequisites(TechnologyId id)
        {
            var result = new List<TechnologyId>();
            ref var definition = ref TechnologyDefinitions.Get(sessionController.em, sessionController.root, id);
            for (int i = 0; i < definition.Prerequisites.TechnologyRequirements.Length; i++)
                result.Add(definition.Prerequisites.TechnologyRequirements[i].Technology);
            return result;
        }

        List<string> RewardLines(ref DefinitionRewards rewards, bool details)
        {
            var lines = new List<string>();
            var ordered = new List<(int Order, string Label, string Description)>();
            for (int i = 0; i < rewards.Items.Length; i++)
            {
                var reward = rewards.Items[i];
                ordered.Add((reward.Order, "物品：" + ItemDefinitions.Get(sessionController.em, sessionController.root, reward.Item).Metadata.Name + " ×" + reward.Quantity, RewardItems.Get(reward.Item)?.Description));
            }

            for (int i = 0; i < rewards.Blueprints.Length; i++)
            {
                var reward = rewards.Blueprints[i];
                ordered.Add((reward.Order, "建筑许可：" + BuildingDefinitions.Get(sessionController.em, sessionController.root, reward.Building).Metadata.Name + " 最高 LV" + reward.GrantedLevel, RewardBuildings.Get(reward.Building)?.Description));
            }

            for (int i = 0; i < rewards.Features.Length; i++)
            {
                var reward = rewards.Features[i];
                ordered.Add((reward.Order, "功能许可：" + FeatureDefinitions.Get(sessionController.em, sessionController.root, reward.Feature).Metadata.Name, RewardFeatures.Get(reward.Feature)?.Description));
            }

            for (int i = 0; i < rewards.Buffs.Length; i++)
            {
                var reward = rewards.Buffs[i];
                ordered.Add((reward.Order, "永久增益：" + BuffDefinitions.Get(sessionController.em, sessionController.root, reward.Buff).Metadata.Name, RewardBuffs.Get(reward.Buff)?.Description));
            }

            foreach (var reward in ordered.OrderBy(r => r.Order))
            {
                lines.Add(reward.Label);
                if (details && !string.IsNullOrWhiteSpace(reward.Description))
                    lines.Add(reward.Description);
            }

            if (details)
                for (int i = 0; i < rewards.Buffs.Length; i++)
                {
                    ref var buff = ref BuffDefinitions.Get(sessionController.em, sessionController.root, rewards.Buffs[i].Buff);
                    DefinitionEffectText.Append(lines, sessionController.em, sessionController.root, ref buff.Effects);
                }

            return lines;
        }

        internal void ResetSession()
        {
            selectedTechnology = CurrentResearchDefinition = default;
            nextResearchHudRefresh = 0;
            focusResearchHud = false;
            TechnologyTree.ClearSession();
            TechnologyTree.gameObject.SetActive(false);
        }
    }
}
