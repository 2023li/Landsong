using System;
using System.Collections.Generic;
using Landsong.ECS;
using Moyo.Unity;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_QuestTracking : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("滚动视图")]
        public ScrollRect Scroll;
        [Sirenix.OdinInspector.LabelText("条目容器")]
        public RectTransform Rows;

        UI_GamePanel_Quest owner;
        UI_GamePanel_RowCollection rows;
        readonly Dictionary<ulong, RectTransform> cards = new Dictionary<ulong, RectTransform>();
        readonly HashSet<ulong> seen = new HashSet<ulong>();

        public void ValidateConfiguration()
        {
            if (Scroll == null || Rows == null || Scroll.content != Rows || !Scroll.vertical)
                throw new InvalidOperationException("任务追踪滚动视图或内容容器未配置。");
        }

        void EnsureOwner()
        {
            if (owner != null)
                return;
            if (!UIManager.TryGetInstance(out var uiManager)
                || !uiManager.TryGetActivePanel<UI_GamePanel>(out var gamePanel)
                || gamePanel.Quests == null || gamePanel.Quests.QuestTracking != this)
                throw new InvalidOperationException("任务追踪无法从 UIManager 获取所属任务面板。");
            owner = gamePanel.Quests;
            rows = new UI_GamePanel_RowCollection(owner.RowTemplate);
        }

        RectTransform Card(ulong id)
        {
            if (cards.TryGetValue(id, out var card))
                return card;
            var obj = new GameObject("追踪任务 " + id, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            card = (RectTransform)obj.transform;
            card.SetParent(Rows, false);
            var layout = obj.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 8);
            layout.spacing = 3;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = obj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            cards.Add(id, card);
            return card;
        }

        public void Refresh()
        {
            EnsureOwner();
            if (owner.QuestWindow != null)
                owner.QuestWindow.SetActive(owner.navigation.IsPanelOpen && owner.navigation.Panel == GamePanelId.Quest);
            var visible = !owner.intelligence.IsOpen && owner.navigation.Panel != GamePanelId.Quest && owner.navigation.Panel != GamePanelId.Technology && owner.navigation.Panel != GamePanelId.BattleReport && owner.navigation.Panel != GamePanelId.DynastyEnd && !owner.buildingController.DetailsPanel.gameObject.activeSelf;
            gameObject.SetActive(visible);
            if (!visible)
                return;

            rows.Clear(Rows);
            seen.Clear();
            var em = owner.sessionController.em;
            var root = owner.sessionController.root;
            var ids = QuestOps.TrackedIds(em, root);
            var state = QuestOps.Tracking(em, root);
            var order = 0;
            foreach (var questId in ids)
            {
                var entity = WorldQueries.Find(em, questId);
                if (!QuestOps.Trackable(em, entity) || !seen.Add(questId))
                    continue;
                var card = Card(questId);
                card.SetSiblingIndex(order++);
                rows.Clear(card);
                var identity = em.GetComponentData<Identity>(entity);
                var quest = em.GetComponentData<Quest>(entity);
                var definition = em.GetComponentData<QuestDefinitionRef>(entity).Definition;
                rows.Row((state.Mode == 0 ? "自动追踪 · " : "追踪 · ") + identity.Name + " · " + UI_GamePanel_Quest.QuestStatusName(quest.Status), () => owner.SelectQuest(questId), parent: card, key: "title");
                if (quest.Status == QuestStatus.Active)
                {
                    rows.Row("任务条件", parent: card, key: "conditions-title");
                    if (!QuestOps.Prerequisites(em, root, definition))
                        rows.Row("等待前置条件，原承接槽位保留", parent: card, key: "prerequisites");
                    var progress = em.GetBuffer<QuestProgress>(entity);
                    for (var i = 0; i < progress.Length; i++)
                        rows.Row(owner.RequirementText(definition, progress[i]), parent: card, key: "condition:" + i);
                    if (quest.Deadline > 0)
                        rows.Row(owner.QuestDeadline(quest), parent: card, key: "deadline");
                }
                else
                {
                    rows.Row("任务奖励", parent: card, key: "rewards-title");
                    foreach (var reward in owner.QuestRewards(definition))
                        rows.Row(reward, parent: card);
                    var session = em.GetComponentData<Session>(root);
                    var persistence = em.GetComponentData<PersistenceGate>(root);
                    rows.Row("领取奖励", session.Phase == Phase.Day && persistence.CheckpointPending == 0 ? () => owner.commandsController.TryQueue(new ClaimQuestRequest { Quest = questId }) : null, parent: card, key: "claim");
                }
                rows.Row("取消追踪", () => owner.commandsController.TryQueue(new TrackQuestRequest { Quest = questId, Mode = QuestTrackingMode.Unpinned }), parent: card, key: "unpin");
            }

            foreach (var entry in new List<KeyValuePair<ulong, RectTransform>>(cards))
                if (!seen.Contains(entry.Key))
                {
                    rows.Release(entry.Value);
                    Destroy(entry.Value.gameObject);
                    cards.Remove(entry.Key);
                }
            if (order == 0)
            {
                rows.Row(state.Mode == 0 ? "暂无可追踪任务" : "未追踪任务", () => owner.navigation.OpenPanel(GamePanelId.Quest), parent: Rows, key: "empty");
                if (state.Mode != 0)
                    rows.Row("恢复自动追踪", () => owner.commandsController.TryQueue(new TrackQuestRequest { Mode = QuestTrackingMode.Automatic }), parent: Rows, key: "automatic");
            }
        }

        internal void ClearAllRows() => rows?.ClearAll();

        internal void ClearSessionViews()
        {
            foreach (var card in cards.Values)
            {
                rows.Release(card);
                Destroy(card.gameObject);
            }
            cards.Clear();
            seen.Clear();
            rows?.ClearAll();
        }
    }
}
