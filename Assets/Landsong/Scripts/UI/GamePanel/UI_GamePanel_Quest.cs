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
    public sealed class UI_GamePanel_Quest : UI_GamePanel_View
    {
        [LabelText("自动跟踪")]
        public Button AutoTrack;
        [LabelText("来源筛选")]
        public Button SourceFilter;
        [LabelText("容量")]
        public Text Capacity;
        [LabelText("等待")]
        public Text Waiting;
        [LabelText("来源筛选文字")]
        public Text SourceFilterLabel;
        [LabelText("已承接条目容器")]
        public RectTransform AcceptedRows;
        [LabelText("邀请条目容器")]
        public RectTransform InvitationRows;
        [LabelText("类型开关集合")]
        public Toggle[] TypeToggles;
        [LabelText("卡片模板")]
        public UI_GamePanel_QuestSlot CardTemplate;
        [LabelText("任务行模板")]
        public UI_GamePanel_Row RowTemplate;
        [Sirenix.OdinInspector.LabelText("任务显示目录"), Sirenix.OdinInspector.Required]
        public QuestDisplayCatalog QuestDisplay;
        internal GameUiSessionHandle sessionController;
        internal GameUiRefreshScheduler refresh;
        internal GameUiCommandWriter commandsController;
        internal IGameUiNavigation navigation;
        UI_GamePanel_RowCollection rows;
        internal IntelligenceViewState intelligence;
        public override void Render()
        {
            rows.Begin();
            Quests();
            rows.End();
        }
        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (AutoTrack == null || SourceFilter == null || Capacity == null || Waiting == null || SourceFilterLabel == null || AcceptedRows == null || InvitationRows == null || CardTemplate == null || RowTemplate == null || TypeToggles == null || TypeToggles.Length != 4)
                throw new InvalidOperationException("任务面板检查器引用不完整。");
            RowTemplate.ValidateConfiguration();
        }
        public override void Bind(GameUiSessionHandle session, GameUiCommandWriter commands, IGameUiNavigation navigation, GameUiRefreshScheduler refresh)
        {
            base.Bind(session, commands, navigation, refresh);
            sessionController = session;
            commandsController = commands;
            this.navigation = navigation;
            this.refresh = refresh;
            rows = new UI_GamePanel_RowCollection(RowTemplate);
            QuestTracking.Bind(this);
            AutoTrack.onClick.AddListener(() => commandsController.TryQueue(new TrackQuestRequest { Mode = QuestTrackingMode.Automatic }));
            SourceFilter.onClick.AddListener(() =>
            {
                questSourceFilter = 0;
                refresh.NextPanel = 0;
            });
            for (var i = 0; i < TypeToggles.Length; i++)
            {
                var bit = 1 << i;
                TypeToggles[i].onValueChanged.AddListener(on =>
                {
                    questTypeMask = on ? questTypeMask | bit : questTypeMask & ~bit;
                    refresh.NextPanel = 0;
                });
            }
        }
        internal override void ClearAllRows()
        {
            rows?.ClearAll();
            QuestTracking?.ClearAllRows();
        }
        [Sirenix.OdinInspector.LabelText("数量模板")]
        public UI_GamePanel_QuantityRow QuantityTemplate;
        internal UI_GamePanel_BuildingActionBar buildingController;
        internal UI_GamePanel_Hud hudController;
        internal void ConfirmAbandonQuest(Identity id)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                "已提交物资不返还。惩罚只扣正常库存中现有的数量，不形成债务，也不扣待存放池。"
            };
            foreach (var cost in QuestLifecycle.FailureCosts(sessionController.em, sessionController.root, DefinitionOf(WorldQueries.Find(sessionController.em, id.Id))))
                lines.Add(ItemName(cost.Item) + "：最多扣除 " + cost.Amount + "，当前实际可扣 " + math.min(cost.Amount, InventoryOps.Count(sessionController.em, sessionController.root, cost.Item)));
            buildingController.ShowBuildingConfirmation("确认放弃任务？" + id.Name, lines, () => commandsController.TryQueue(new AbandonQuestRequest { Quest = id.Id }));
        }

        public void OpenInvitations(ulong source = 0)
        {
            questTypeMask = 15;
            questSourceFilter = source;
            navigation.OpenPanel(GamePanelId.Quest);
        }

        internal void InvitationPool(bool day)
        {
            var entries = new List<(Entity Source, int Slot, QuestOfferQuote Quote)>();
            using var buildings = WorldQueries.OrderedEntities<Building>(sessionController.em);
            foreach (var e in buildings)
            {
                var id = sessionController.em.GetComponentData<Identity>(e);
                if (questSourceFilter != 0 && id.Id != questSourceFilter)
                    continue;
                var slots = sessionController.em.GetBuffer<QuestOfferSlot>(e);
                for (var i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if ((questTypeMask & (1 << slot.Type)) == 0)
                        continue;
                    if (!IsInvitationSlot(e, slot))
                        continue;
                    entries.Add((e, i, QuestOfferOps.Quote(sessionController.em, sessionController.root, e, i)));
                }
            }

            entries.Sort((a, b) =>
            {
                if ((a.Quote.Offer != 0) != (b.Quote.Offer != 0))
                    return a.Quote.Offer != 0 ? -1 : 1;
                if (a.Quote.Offer != 0)
                    return QuestOps.CompareValue(sessionController.em, sessionController.root, WorldQueries.Find(sessionController.em, a.Quote.Offer), WorldQueries.Find(sessionController.em, b.Quote.Offer));
                var order = sessionController.em.GetComponentData<Identity>(a.Source).Id.CompareTo(sessionController.em.GetComponentData<Identity>(b.Source).Id);
                return order != 0 ? order : a.Slot.CompareTo(b.Slot);
            });
            foreach (var entry in entries)
            {
                var e = entry.Source;
                var at = entry.Slot;
                var q = entry.Quote;
                var id = sessionController.em.GetComponentData<Identity>(e);
                var slot = sessionController.em.GetBuffer<QuestOfferSlot>(e)[at];
                var source = QuestBuildingSource(e) + " 提供 · " + QuestOfferOps.TypeName(slot.Type) + " " + (slot.Index + 1);
                var key = "offer:" + id.Id + ":" + at;
                if (q.Offer != 0)
                {
                    var quest = WorldQueries.Find(sessionController.em, q.Offer);
                    source += " · 价值 " + QuestOps.RewardValue(sessionController.em, sessionController.root, DefinitionOf(quest));
                    ShowQuestCard(key, QuestPoolRows, source, quest, day);
                    continue;
                }

                var card = QuestCard(key, QuestPoolRows);
                if (card.Interaction.IsPinned)
                    continue;
                var available = QuestOfferOps.Available(sessionController.em, sessionController.root, e, out var reason);
                var wait = !available ? "刷新暂停：" + reason : q.Code == ResultCode.Success ? q.Wait > 0 ? q.Wait + " 回合后刷新" : "下次结算刷新" : q.Reason;
                card.Show(0, source, wait, "", true, false, null, "", null);
                rows.Clear(card.Body);
                rows.Row("立即邀约：" + buildingController.CostText(q.Costs), day && q.Code == ResultCode.Success && BuildingCostOps.CanPay(sessionController.em, sessionController.root, q.Costs) ? () => ConfirmQuestRecruit(id, at) : null, parent: card.Body);
            }
        }

        internal void BindInvitationMessage(GameEvent message)
        {
            if (hudController.MessageButton == null)
                throw new InvalidOperationException("消息按钮检查器引用缺失。");
            var button = hudController.MessageButton;
            hudController.Message.raycastTarget = true;
            button.targetGraphic = hudController.Message;
            button.onClick.RemoveAllListeners();
            if (message.Kind != EventKind.Message || !message.Message.ToString().StartsWith("新邀约：", StringComparison.Ordinal))
                return;
            var source = message.Target;
            button.onClick.AddListener(() =>
            {
                buildingController.FocusBuilding(source);
                OpenInvitations(source);
            });
        }

        [Sirenix.OdinInspector.LabelText("任务跟踪")]
        public UI_GamePanel_QuestTracking QuestTracking;
        public GameObject QuestWindow => gameObject;
        public RectTransform QuestListRows => AcceptedRows;
        public RectTransform QuestDetailRows { get; internal set; }
        public RectTransform QuestHudRows => QuestTracking != null ? QuestTracking.Rows : null;
        public InputField QuestAmountInput { get; internal set; }
        public RectTransform QuestPoolRows => InvitationRows;

        internal int questTypeMask = 15;
        internal ulong questSourceFilter;
        internal ulong selectedQuest;
        internal readonly Dictionary<string, UI_GamePanel_QuestSlot> questCards = new Dictionary<string, UI_GamePanel_QuestSlot>();
        readonly HashSet<string> seenQuestCards = new HashSet<string>();
        sealed class QuestDetailGroups
        {
            public RectTransform Introduction;
            public RectTransform Requirements;
            public RectTransform Rewards;
            public RectTransform Actions;
        }
        readonly Dictionary<UI_GamePanel_QuestSlot, QuestDetailGroups> detailGroups = new Dictionary<UI_GamePanel_QuestSlot, QuestDetailGroups>();
        public Toggle QuestTypeToggle(int type) => TypeToggles[type];
        public void SelectQuest(ulong id)
        {
            selectedQuest = id;
            var e = WorldQueries.Find(sessionController.em, id);
            if (e != Entity.Null && sessionController.em.HasComponent<Quest>(e))
                questTypeMask = 15;
            questSourceFilter = 0;
            navigation.OpenPanel(GamePanelId.Quest);
        }

        internal static string QuestStatusName(QuestStatus s) => s == QuestStatus.Offered ? "待签约" : s == QuestStatus.Completed ? "待领奖" : s == QuestStatus.Claimed ? "已领奖" : "进行中";
        internal string QuestDeadline(Quest q)
        {
            if (q.Status == QuestStatus.Offered)
                return "签约后开始计时，不占承接名额";
            if (q.Status == QuestStatus.Completed)
                return "已完成，不会超时；领取奖励后结束本步骤";
            return q.Deadline == 0 ? "无期限" : "剩余 " + math.max(0, q.Deadline - sessionController.em.GetComponentData<GameClock>(sessionController.root).Turn) + " 回合（第 " + q.Deadline + " 回合前完成）";
        }

        internal string RequirementText(QuestId definition, QuestProgress progress)
        {
            ref var data = ref QuestDefinitions.Get(sessionController.em, sessionController.root, definition);
            ref var objectives = ref data.Objectives;
            string Format(string label, int amount) => (progress.Amount >= amount ? "✓ " : "○ ") + label + "　" + progress.Amount + "/" + amount;
            for (int i = 0; i < objectives.BuildingObjectives.Length; i++)
            {
                var objective = objectives.BuildingObjectives[i];
                if (objective.Key == progress.Key)
                    return Format((objective.CompletedOnly ? "完工" : "建造") + " " + BuildingName(objective.Building) + (objective.MinimumLevel > 1 ? " LV" + objective.MinimumLevel : ""), objective.Count);
            }

            for (int i = 0; i < objectives.PlantedBuildingObjectives.Length; i++)
            {
                var objective = objectives.PlantedBuildingObjectives[i];
                if (objective.Key == progress.Key)
                    return Format("已种植作物的 " + (objective.Building.IsValid ? BuildingName(objective.Building) : "农田"), objective.Count);
            }

            for (int i = 0; i < objectives.OwnedItemObjectives.Length; i++)
            {
                var objective = objectives.OwnedItemObjectives[i];
                if (objective.Key == progress.Key)
                    return Format("持有 " + ItemName(objective.Item) + "（不扣除）", objective.Quantity);
            }

            for (int i = 0; i < objectives.SubmittedItemObjectives.Length; i++)
            {
                var objective = objectives.SubmittedItemObjectives[i];
                if (objective.Key == progress.Key)
                    return Format("提交 " + ItemName(objective.Item), objective.Quantity);
            }

            for (int i = 0; i < objectives.TechnologyObjectives.Length; i++)
            {
                var objective = objectives.TechnologyObjectives[i];
                if (objective.Key == progress.Key)
                    return Format("开始研究 " + (objective.Technology.IsValid ? TechnologyDefinitions.Get(sessionController.em, sessionController.root, objective.Technology).Metadata.Name.ToString() : "任意科技") + "（排队等待不算）", objective.Count);
            }

            for (int i = 0; i < objectives.CameraMoveObjectives.Length; i++)
            {
                var objective = objectives.CameraMoveObjectives[i];
                if (objective.Key == progress.Key)
                    return Format("使用 WASD 移动镜头", objective.Count);
            }

            for (int i = 0; i < objectives.CameraZoomObjectives.Length; i++)
            {
                var objective = objectives.CameraZoomObjectives[i];
                if (objective.Key == progress.Key)
                    return Format("滚轮缩放镜头", objective.Count);
            }

            for (int i = 0; i < objectives.TurnObjectives.Length; i++)
            {
                var objective = objectives.TurnObjectives[i];
                if (objective.Key == progress.Key)
                    return Format(objective.SinceAccepted ? "签约后经过回合" : "抵达回合", objective.Turns);
            }

            throw new InvalidOperationException("任务进度未匹配任何目标：" + progress.Key);
        }

        internal IEnumerable<string> QuestRewards(QuestId definition)
        {
            ref var data = ref QuestDefinitions.Get(sessionController.em, sessionController.root, definition);
            ref var rewards = ref data.Rewards;
            var lines = new List<(int Order, string Text)>();
            for (int i = 0; i < rewards.Items.Length; i++)
            {
                var reward = rewards.Items[i];
                lines.Add((reward.Order, ItemName(reward.Item) + " × " + reward.Quantity));
            }

            for (int i = 0; i < rewards.Blueprints.Length; i++)
            {
                var reward = rewards.Blueprints[i];
                lines.Add((reward.Order, "蓝图 " + BuildingName(reward.Building) + " LV" + reward.GrantedLevel));
            }

            for (int i = 0; i < rewards.Buffs.Length; i++)
            {
                var reward = rewards.Buffs[i];
                lines.Add((reward.Order, "增益 " + BuffDefinitions.Get(sessionController.em, sessionController.root, reward.Buff).Metadata.Name + " × " + reward.GrantedLevel));
            }

            for (int i = 0; i < rewards.Features.Length; i++)
            {
                var reward = rewards.Features[i];
                lines.Add((reward.Order, "解锁 " + FeatureDefinitions.Get(sessionController.em, sessionController.root, reward.Feature).Metadata.Name));
            }

            return lines.OrderBy(line => line.Order).Select(line => line.Text).ToList();
        }

        public UI_GamePanel_QuestSlot FindQuestCard(ulong id)
        {
            foreach (var card in questCards.Values)
                if (card.Rect.gameObject.activeInHierarchy && card.QuestId == id)
                    return card;
            return null;
        }

        internal UI_GamePanel_QuestSlot QuestCard(string key, RectTransform parent)
        {
            seenQuestCards.Add(key);
            if (!questCards.TryGetValue(key, out var card))
            {
                card = Instantiate(CardTemplate, parent);
                card.name = "任务卡片 " + key;
                card.Rect.gameObject.SetActive(true);
                questCards.Add(key, card);
                rows.Own(card.Body, card.Interaction);
            }

            if (!questCards.Values.Any(value => value.Interaction.IsPinned))
                card.Rect.SetAsLastSibling();
            return card;
        }

        internal string QuestBuildingSource(Entity e)
        {
            var cell = EntityState.Position(sessionController.em, e);
            return sessionController.EntityName(sessionController.em.GetComponentData<Identity>(e).Id) + $"（{cell.x:0.#}, {cell.y:0.#}, {cell.z:0.#}）";
        }

        internal void ShowQuestCard(string key, RectTransform parent, string source, Entity entity, bool day)
        {
            if (entity != Entity.Null)
                key += ":quest:" + sessionController.em.GetComponentData<Identity>(entity).Id;
            var card = QuestCard(key, parent);
            if (card.Interaction.IsPinned)
                return;
            if (entity == Entity.Null)
            {
                card.Show(0, source, "空闲任务槽", "", false, false, null, "", null);
                return;
            }

            var identity = sessionController.em.GetComponentData<Identity>(entity);
            var q = sessionController.em.GetComponentData<Quest>(entity);
            if (card.QuestId != 0 && selectedQuest == card.QuestId && card.QuestId != identity.Id)
            {
                var old = WorldQueries.Find(sessionController.em, card.QuestId);
                if (old == Entity.Null || sessionController.em.HasComponent<Quest>(old) && sessionController.em.GetComponentData<Quest>(old).Status == QuestStatus.Claimed)
                    selectedQuest = identity.Id;
            }

            var expanded = selectedQuest == identity.Id;
            var remaining = q.Status == QuestStatus.Completed ? "待领奖" : q.Status == QuestStatus.Offered ? "未接受" : q.Deadline == 0 ? "无期限" : "剩余 " + math.max(0, q.Deadline - sessionController.em.GetComponentData<GameClock>(sessionController.root).Turn) + " 回合";
            if (q.Status == QuestStatus.Active && !QuestOps.Prerequisites(sessionController.em, sessionController.root, DefinitionOf(entity)))
                remaining = "等待前置";
            Action action = null;
            var label = "";
            if (q.Status == QuestStatus.Completed)
            {
                label = "✓";
                if (day)
                    action = () => buildingController.ShowBuildingConfirmation("领取：" + identity.Name, RewardConfirmation(DefinitionOf(entity)), () => commandsController.TryQueue(new ClaimQuestRequest { Quest = identity.Id }));
            }
            else if (q.Status == QuestStatus.Active && q.Mainline == 0)
            {
                label = "X";
                if (day)
                    action = () => ConfirmAbandonQuest(identity);
            }

            card.Show(identity.Id, source, identity.Name + (q.Status == QuestStatus.Offered ? " · 查看邀约" : ""), remaining, expanded, q.Status == QuestStatus.Completed, () =>
            {
                selectedQuest = selectedQuest == identity.Id ? 0 : identity.Id;
                refresh.NextPanel = 0;
            }, label, action);
            if (QuestOps.Trackable(sessionController.em, entity))
            {
                card.Tracking.gameObject.SetActive(true);
                card.Tracking.onValueChanged.RemoveAllListeners();
                card.Tracking.SetIsOnWithoutNotify(QuestOps.IsTracked(sessionController.em, sessionController.root, identity.Id));
                card.Tracking.onValueChanged.AddListener(on =>
                {
                    if (on)
                        commandsController.TryQueue(new TrackQuestRequest { Quest = identity.Id, Mode = QuestTrackingMode.Manual });
                    else
                        commandsController.TryQueue(new TrackQuestRequest { Quest = identity.Id, Mode = QuestTrackingMode.Unpinned });
                });
            }

            if (!expanded)
                return;
            QuestDetailRows = card.Body;
            rows.Clear(card.Body);
            QuestDetails(card, entity, day);
        }

        internal void Quests()
        {
            QuestWindow.SetActive(true);
            QuestDetailRows = null;
            seenQuestCards.Clear();
            if (buildingController.BuildingActionBar != null)
                buildingController.BuildingActionBar.gameObject.SetActive(false);
            buildingController.DetailsPanel.Hide();
            var day = sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day && sessionController.em.GetComponentData<PersistenceGate>(sessionController.root).CheckpointPending == 0;
            Capacity.text = "已接受任务 · " + QuestLifecycle.QuestCount(sessionController.em) + "/" + QuestLifecycle.QuestCapacity(sessionController.em);
            SourceFilterLabel.text = questSourceFilter == 0 ? "全部来源" : "取消来源筛选：" + sessionController.EntityName(questSourceFilter);
            for (var i = 0; i < 4; i++)
                TypeToggles[i].SetIsOnWithoutNotify((questTypeMask & (1 << i)) != 0);
            var waiting = QuestLifecycle.WaitingMainlines(sessionController.em, sessionController.root);
            Waiting.text = waiting.Count == 0 ? "主线与一般任务共用槽位" : "主线等待空槽：" + QuestName(waiting[0]) + (waiting.Count > 1 ? " 等 " + waiting.Count + " 项" : "");
            var accepted = new List<Entity>();
            using var quests = WorldQueries.OrderedEntities<Quest>(sessionController.em);
            foreach (var e in quests)
            {
                var q = sessionController.em.GetComponentData<Quest>(e);
                if (q.Status == QuestStatus.Active || q.Status == QuestStatus.Completed)
                    accepted.Add(e);
            }

            using var buildings = WorldQueries.OrderedEntities<Building>(sessionController.em);
            foreach (var e in buildings)
            {
                var capacity = QuestLifecycle.QuestContainerCapacity(sessionController.em, e);
                var id = sessionController.em.GetComponentData<Identity>(e).Id;
                for (var i = 0; i < capacity; i++)
                {
                    Entity occupant = Entity.Null;
                    foreach (var task in accepted)
                    {
                        var q = sessionController.em.GetComponentData<Quest>(task);
                        if (q.Container == id && q.ContainerSlot == i)
                        {
                            occupant = task;
                            break;
                        }
                    }

                    ShowQuestCard("slot:" + id + ":" + i, QuestListRows, "承接槽 " + (i + 1) + " · " + QuestBuildingSource(e) + " 提供", occupant, day);
                }
            }

            InvitationPool(day);
            var unused = new List<string>();
            foreach (var pair in questCards)
                if (!seenQuestCards.Contains(pair.Key) && !pair.Value.Interaction.IsPinned)
                    unused.Add(pair.Key);
            foreach (var key in unused)
            {
                var card = questCards[key];
                ReleaseQuestCardRows(card);
                Destroy(card.Rect.gameObject);
                questCards.Remove(key);
            }
        }

        static RectTransform CreateDetailGroup(RectTransform parent, string name, bool framed)
        {
            var group = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = Vector2.zero;
            var layout = group.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            if (framed)
            {
                layout.padding = new RectOffset(8, 8, 8, 8);
                var background = group.AddComponent<Image>();
                background.color = new Color(.14f, .18f, .22f, .95f);
                background.raycastTarget = false;
            }
            return rect;
        }

        QuestDetailGroups DetailGroups(UI_GamePanel_QuestSlot card)
        {
            if (detailGroups.TryGetValue(card, out var groups))
                return groups;
            groups = new QuestDetailGroups
            {
                Introduction = CreateDetailGroup(card.Body, "任务说明", false),
                Requirements = CreateDetailGroup(card.Body, "任务要求", true),
                Rewards = CreateDetailGroup(card.Body, "任务奖励", true),
                Actions = CreateDetailGroup(card.Body, "任务操作", false)
            };
            rows.Own(groups.Introduction, card.Interaction);
            rows.Own(groups.Requirements, card.Interaction);
            rows.Own(groups.Rewards, card.Interaction);
            rows.Own(groups.Actions, card.Interaction);
            detailGroups.Add(card, groups);
            return groups;
        }

        void ReleaseQuestCardRows(UI_GamePanel_QuestSlot card)
        {
            rows.Release(card.Body);
            if (!detailGroups.TryGetValue(card, out var groups))
                return;
            rows.Release(groups.Introduction);
            rows.Release(groups.Requirements);
            rows.Release(groups.Rewards);
            rows.Release(groups.Actions);
            detailGroups.Remove(card);
        }

        internal void QuestDetails(UI_GamePanel_QuestSlot card, Entity entity, bool day)
        {
            var groups = DetailGroups(card);
            rows.Clear(groups.Introduction);
            rows.Clear(groups.Requirements);
            rows.Clear(groups.Rewards);
            rows.Clear(groups.Actions);
            void Detail(RectTransform parent, string label, Action action = null) => rows.Row(label, action, parent: parent);
            var identity = sessionController.em.GetComponentData<Identity>(entity);
            var quest = sessionController.em.GetComponentData<Quest>(entity);
            var sourceData = QuestDisplay.Get(DefinitionOf(entity));
            if (sourceData != null && !string.IsNullOrEmpty(sourceData.Description))
                Detail(groups.Introduction, sourceData.Description);
            Detail(groups.Introduction, QuestDeadline(quest));
            if (!day)
                Detail(groups.Introduction, "夜晚仅查看，签约、提交、领奖请在白天操作。");
            var definition = DefinitionOf(entity);
            ref var definitionData = ref QuestDefinitions.Get(sessionController.em, sessionController.root, definition);
            if (quest.Mainline == 0)
                foreach (var cost in QuestLifecycle.FailureCosts(sessionController.em, sessionController.root, DefinitionOf(entity)))
                    Detail(groups.Introduction, (quest.Status == QuestStatus.Completed ? "容器失效最多扣除 " : "放弃 / 超时 / 容器失效最多扣除 ") + ItemName(cost.Item) + " × " + cost.Amount + "；不形成债务");
            Detail(groups.Requirements, "任务要求");
            foreach (var p in sessionController.em.GetBuffer<QuestProgress>(entity))
            {
                Detail(groups.Requirements, RequirementText(definition, p));
                var target = ObjectiveBuilding(ref definitionData.Objectives, p.Key);
                if (target.IsValid && quest.Status == QuestStatus.Active)
                {
                    using var buildings = WorldQueries.OrderedEntities<Building>(sessionController.em);
                    foreach (var b in buildings)
                        if (sessionController.em.GetComponentData<BuildingDefinitionRef>(b).Definition == target)
                        {
                            var buildingId = sessionController.em.GetComponentData<Identity>(b).Id;
                            Detail(groups.Requirements, "定位同类建筑：" + sessionController.EntityName(buildingId), () =>
                            {
                                navigation.OpenPanel(GamePanelId.Building);
                                buildingController.FocusBuilding(buildingId);
                            });
                            break;
                        }
                }

                var submittedItem = ObjectiveSubmittedItem(ref definitionData.Objectives, p.Key);
                if (!submittedItem.IsValid || quest.Status != QuestStatus.Active)
                    continue;
                var key = p.Key.ToString();
                var quote = QuestOps.Submission(sessionController.em, sessionController.root, entity, key);
                Detail(groups.Requirements, "提交 " + ItemName(submittedItem) + " · 持有 " + quote.Available + " / 尚需 " + quote.Remaining, day && quote.Code == ResultCode.Success ? () => ConfirmQuestSubmission(identity.Id, key) : null);
            }

            Detail(groups.Rewards, "任务奖励");
            foreach (var reward in QuestRewards(DefinitionOf(entity)))
                Detail(groups.Rewards, reward);
            if (quest.Status == QuestStatus.Offered)
            {
                var provider = WorldQueries.Find(sessionController.em, quest.Source);
                if (!QuestOfferOps.Available(sessionController.em, sessionController.root, provider, out var reason))
                    Detail(groups.Actions, reason);
                var costs = new List<string>
                {
                    "签约后开始期限计时；分步任务领奖后由下一步沿用原槽，最后一步领奖后释放承接槽位。承接建筑停工、缺工、维护失败、拆除或缩容导致槽位失效时，任务（含待领奖）丢失并执行放弃惩罚。"
                };
                foreach (var c in QuestLifecycle.FailureCosts(sessionController.em, sessionController.root, DefinitionOf(entity)))
                    costs.Add("失败最多扣除 " + ItemName(c.Item) + " × " + c.Amount);
                Detail(groups.Actions, "签约", day && QuestLifecycle.QuestCount(sessionController.em) < QuestLifecycle.QuestCapacity(sessionController.em) ? () => buildingController.ShowBuildingConfirmation("签约：" + identity.Name, costs, () => commandsController.TryQueue(new AcceptQuestRequest { Quest = identity.Id })) : null);
                if (QuestLifecycle.QuestCount(sessionController.em) >= QuestLifecycle.QuestCapacity(sessionController.em))
                    Detail(groups.Actions, "承接名额已满，请完成任务链并领取奖励，或放弃一般任务。");
                Detail(groups.Actions, "拒绝邀约", !day ? null : () => buildingController.ShowBuildingConfirmation("拒绝：" + identity.Name, new[] { "当前邀约将移除，来源槽重新开始冷却。不扣失败惩罚。" }, () => commandsController.TryQueue(new RejectQuestRequest { Quest = identity.Id })));
            }
            groups.Actions.gameObject.SetActive(quest.Status == QuestStatus.Offered);
        }

        internal IEnumerable<string> RewardConfirmation(QuestId definition)
        {
            foreach (var line in QuestRewards(definition))
                yield return line;
            yield return "有后续步骤时沿用原承接槽位，任务链结束后才释放。";
            yield return "正常库存放不下时不会部分发奖，也不会消耗任务；整理库存后重试。";
        }

        internal void ConfirmQuestRecruit(Identity source, int slot)
        {
            var entity = WorldQueries.Find(sessionController.em, source.Id);
            if (!BuildingStatus.Operational(sessionController.em, entity))
                return;
            var quote = QuestOfferOps.Quote(sessionController.em, sessionController.root, entity, slot);
            var lines = new List<string>();
            foreach (var cost in quote.Costs)
                lines.Add(ItemName(cost.Item) + " × " + cost.Amount);
            if (lines.Count == 0)
                lines.Add("无额外物资费用");
            lines.Add("生成新的邀约，不会自动签约。条件不满足时不扣费。");
            buildingController.ShowBuildingConfirmation("付费邀约：" + source.Name, lines, () => commandsController.TryQueue(new RecruitQuestRequest { Provider = source.Id, OfferSlot = slot }));
        }

        public void ConfirmQuestSubmission(ulong id, string key)
        {
            var quote = QuestOps.Submission(sessionController.em, sessionController.root, WorldQueries.Find(sessionController.em, id), key);
            if (quote.Code != ResultCode.Success)
            {
                hudController.Message.text = quote.Reason;
                return;
            }

            InputField amount = null;
            buildingController.ShowBuildingConfirmation("提交：" + ItemName(quote.Item), new[] { "持有 " + quote.Available + "，尚需 " + quote.Remaining + "；本次可提交 1～" + quote.Maximum, "只扣此项，提交后不可退回。库存或任务变化后需要重新确认。" }, () =>
            {
                if (!int.TryParse(amount.text, out var quantity) || quantity < 1 || quantity > quote.Maximum)
                {
                    hudController.Message.text = "请输入 1～" + quote.Maximum + " 的整数";
                    ConfirmQuestSubmission(id, key);
                    return;
                }

                commandsController.TryQueue(new SubmitQuestRequest { Quest = id, Item = quote.Item, Quantity = quantity, RequirementKey = new Unity.Collections.FixedString64Bytes(key), ExpectedQuote = new Unity.Collections.FixedString128Bytes(quote.Stamp) });
            });
            var binding = buildingController.ConfirmationItem(QuantityTemplate, "", buildingController.BuildingConfirmRows, "quest-submit:" + id + ":" + key);
            if (binding == null)
                return;
            if (!binding.CanRebind)
            {
                amount = QuestAmountInput = binding.Quantity;
                return;
            }

            binding.transform.SetSiblingIndex(3);
            amount = binding.Quantity;
            if (amount == null)
                throw new InvalidOperationException("通用行模板缺少 QuestQuantity 引用。");
            amount.gameObject.SetActive(true);
            amount.SetTextWithoutNotify(quote.Maximum.ToString());
            QuestAmountInput = amount;
        }

        QuestId DefinitionOf(Entity quest) => sessionController.em.GetComponentData<QuestDefinitionRef>(quest).Definition;
        string QuestName(QuestId id) => QuestDefinitions.Get(sessionController.em, sessionController.root, id).Metadata.Name.ToString();
        string ItemName(ItemId id) => ItemDefinitions.Get(sessionController.em, sessionController.root, id).Metadata.Name.ToString();
        string BuildingName(BuildingId id) => BuildingDefinitions.Get(sessionController.em, sessionController.root, id).Metadata.Name.ToString();
        bool IsInvitationSlot(Entity building, QuestOfferSlot slot)
        {
            var level = sessionController.em.GetComponentData<Building>(building).Level;
            var id = sessionController.em.GetComponentData<BuildingDefinitionRef>(building).Definition;
            ref var definition = ref BuildingDefinitions.Get(sessionController.em, sessionController.root, id);
            for (int i = 0; i < definition.Capabilities.Quests.Invitations.Length; i++)
            {
                var invitation = definition.Capabilities.Quests.Invitations[i];
                if ((invitation.Level == 0 || invitation.Level == level) && (int)invitation.Type == slot.Type)
                    return slot.Index < invitation.Slots;
            }

            return false;
        }

        static BuildingId ObjectiveBuilding(ref QuestObjectives objectives, FixedString64Bytes key)
        {
            for (int i = 0; i < objectives.BuildingObjectives.Length; i++)
                if (objectives.BuildingObjectives[i].Key == key)
                    return objectives.BuildingObjectives[i].Building;
            for (int i = 0; i < objectives.PlantedBuildingObjectives.Length; i++)
                if (objectives.PlantedBuildingObjectives[i].Key == key)
                    return objectives.PlantedBuildingObjectives[i].Building;
            return default;
        }

        static ItemId ObjectiveSubmittedItem(ref QuestObjectives objectives, FixedString64Bytes key)
        {
            for (int i = 0; i < objectives.SubmittedItemObjectives.Length; i++)
                if (objectives.SubmittedItemObjectives[i].Key == key)
                    return objectives.SubmittedItemObjectives[i].Item;
            return default;
        }

        public void ClearSessionViews()
        {
            QuestTracking?.ClearSessionViews();
            foreach (var card in questCards.Values)
                if (card != null)
                {
                    ReleaseQuestCardRows(card);
                    Destroy(card.gameObject);
                }
            questCards.Clear();
            detailGroups.Clear();
            seenQuestCards.Clear();
            selectedQuest = 0;
            questSourceFilter = 0;
            questTypeMask = 15;
            QuestDetailRows = null;
            QuestAmountInput = null;
        }
    }
}
