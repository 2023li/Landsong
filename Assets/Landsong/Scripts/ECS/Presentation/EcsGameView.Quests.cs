using System;
using System.Collections.Generic;
using Landsong.ECS.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using InputField = TMPro.TMP_InputField;

namespace Landsong.ECS.Presentation
{
    // Presentation retains selection only. Progress, tracking, inventory and rewards belong to ECS.
    public sealed partial class EcsGameView
    {
        public GameObject QuestWindow { get; private set; }
        public RectTransform QuestListRows { get; private set; }
        public RectTransform QuestDetailRows { get; private set; }
        public RectTransform QuestHudRows { get; private set; }
        public InputField QuestAmountInput { get; private set; }
        public RectTransform QuestPoolRows { get; private set; }
        int questTypeMask = 15;
        ulong questSourceFilter;
        ulong selectedQuest;
        readonly Dictionary<string, QuestSlotView> questCards = new Dictionary<string, QuestSlotView>();
        Text questCapacityLabel, questWaitingLabel;
        Button questFilterButton;
        readonly Toggle[] questTabs = new Toggle[4];
        public Toggle QuestTypeToggle(int type) => questTabs[type];
        static RectTransform QuestScroll(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rows = TechnologyTreeView.Scroll(name, parent, min, max, false).content;
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.spacing = 4;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; return rows;
        }
        public void SelectQuest(ulong id) { selectedQuest = id; var e = Sim.Find(em, id); if (e != Entity.Null && em.HasComponent<Quest>(e)) questTypeMask = 15; questSourceFilter = 0; OpenPanel("任务"); }
        static string QuestStatusName(QuestStatus s) => s == QuestStatus.Offered ? "待签约" : s == QuestStatus.Completed ? "待领奖" : s == QuestStatus.Claimed ? "已领奖" : "进行中";
        string QuestDeadline(Quest q)
        {
            if (q.Status == QuestStatus.Offered) return "签约后开始计时，不占承接名额";
            if (q.Status == QuestStatus.Completed) return "已完成，不会超时；领取后才解锁后续任务";
            return q.Deadline == 0 ? "无期限" : "剩余 " + math.max(0, q.Deadline - em.GetComponentData<Session>(root).Turn) + " 回合（第 " + q.Deadline + " 回合前完成）";
        }
        string RequirementText(QuestProgress p)
        {
            var r = Sim.GetRule(em, root, p.RuleIndex); string label;
            switch (r.Kind)
            {
                case RuleKind.RequireBuilding: label = (r.C != 0 ? "完工" : "建造") + " " + Name(r.Target) + (r.B > 1 ? " LV" + r.B : ""); break;
                case RuleKind.RequireCrop: label = "已种植作物的 " + (r.Target >= 0 ? Name(r.Target) : "农田"); break;
                case RuleKind.RequireItem: label = "持有 " + Name(r.Target) + "（不扣除）"; break;
                case RuleKind.SubmitItem: label = "提交 " + Name(r.Target); break;
                case RuleKind.RequireCameraMove: label = "使用 WASD 移动镜头"; break;
                case RuleKind.RequireCameraZoom: label = "滚轮缩放镜头"; break;
                case RuleKind.RequireTechnology: label = "开始研究 " + (r.Target >= 0 ? Name(r.Target) : "任意科技") + "（排队等待不算）"; break;
                default: label = r.B != 0 ? "签约后经过回合" : "抵达回合"; break;
            }
            return (p.Amount >= r.Amount ? "✓ " : "○ ") + label + "　" + p.Amount + "/" + r.Amount;
        }
        IEnumerable<string> QuestRewards(int definition)
        {
            var d = Sim.Definition(em, root, definition);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i);
                if (r.Kind == RuleKind.RewardItem) yield return Name(r.Target) + " × " + r.Amount;
                else if (r.Kind == RuleKind.RewardBlueprint) yield return "蓝图 " + Name(r.Target) + " LV" + r.Amount;
                else if (r.Kind == RuleKind.RewardBuff) yield return "增益 " + Name(r.Target) + " × " + r.Amount;
                else if (r.Kind == RuleKind.RewardFeature) yield return "解锁 " + Name(r.Target);
            }
        }
        string QuestSource(Quest q)
        {
            if (q.Mainline != 0) { var provider = Sim.Find(em, q.Source); return "主线来源：" + (provider == Entity.Null ? "来源建筑已不存在" : QuestBuildingSource(provider)); }
            var source = Sim.Find(em, q.Source);
            if (source == Entity.Null || !em.HasComponent<Building>(source)) return "邀约来源建筑已不存在";
            return "邀约来源：" + QuestBuildingSource(source);
        }
        void RefreshQuestTracking()
        {
            if (QuestWindow != null) QuestWindow.SetActive(Panel == "任务");
            if (QuestHudRows == null) QuestHudRows = QuestScroll("Quest Tracking", GetComponentInParent<Canvas>().transform, new Vector2(.77f, .45f), new Vector2(.99f, .81f));
            var visible = !intel && Panel != "任务" && Panel != "科技" && Panel != "战报" && Panel != "王朝终局" && !(BuildingDetailsPanel != null && BuildingDetailsPanel.activeSelf);
            QuestHudRows.parent.parent.gameObject.SetActive(visible); if (!visible) return;
            Clear(QuestHudRows); var state = QuestOps.Tracking(em, root); var e = Sim.Find(em, state.Target);
            void Hud(string label, Action action = null) => Row(label, action, parent: QuestHudRows);
            if (!QuestOps.Trackable(em, e)) { Hud(state.Mode == 0 ? "暂无可追踪任务" : "未追踪任务", () => OpenPanel("任务")); if (state.Mode != 0) Hud("恢复自动追踪", () => Send(CommandKind.TrackQuest, argument: 0)); return; }
            var id = em.GetComponentData<Identity>(e); var q = em.GetComponentData<Quest>(e);
            Hud((state.Mode == 0 ? "自动追踪 · " : "追踪 · ") + id.Name + " · " + QuestStatusName(q.Status), () => SelectQuest(id.Id));
            if (q.Status == QuestStatus.Active && !ProgressionOps.Prerequisites(em, root, id.Definition)) Hud("等待前置条件，原承接槽位保留");
            var progress = em.GetBuffer<QuestProgress>(e); for (var i = 0; i < math.min(3, progress.Length); i++) Hud(RequirementText(progress[i]));
            if (progress.Length > 3) Hud("还有 " + (progress.Length - 3) + " 项，查看详情", () => SelectQuest(id.Id));
            if (q.Deadline > 0 && q.Status == QuestStatus.Active) Hud(QuestDeadline(q));
            var session=em.GetComponentData<Session>(root);
            if(q.Status==QuestStatus.Completed)Hud("领取奖励",session.Phase==Phase.Day && session.CheckpointPending==0?()=>Send(CommandKind.ClaimQuest,id.Id):null);
            else Hud("查看任务详情",()=>SelectQuest(id.Id));
            Hud("取消追踪", () => Send(CommandKind.TrackQuest, argument: 2));
        }
        public QuestSlotView FindQuestCard(ulong id)
        {
            foreach(var card in questCards.Values) if(card.Rect.gameObject.activeInHierarchy && card.QuestId==id)return card;
            return null;
        }
        QuestSlotView QuestCard(string key, RectTransform parent)
        {
            if (!questCards.TryGetValue(key, out var card)) { card = QuestSlotView.Create(parent, Status.font); questCards.Add(key, card); }
            card.Rect.SetAsLastSibling(); return card;
        }
        string QuestBuildingSource(Entity e)
        {
            var b = em.GetComponentData<Building>(e); var cell = Sim.Position(em, e);
            return EntityName(em.GetComponentData<Identity>(e).Id) + $"（{cell.x:0.#}, {cell.y:0.#}, {cell.z:0.#}）";
        }
        void ShowQuestCard(string key, RectTransform parent, string source, Entity entity, bool day)
        {
            var card = QuestCard(key, parent);
            if (entity == Entity.Null) { card.Show(0, source, "空闲任务槽", "", false, false, null, "", null); return; }
            var identity = em.GetComponentData<Identity>(entity); var q = em.GetComponentData<Quest>(entity);
            if (card.QuestId != 0 && selectedQuest == card.QuestId && card.QuestId != identity.Id)
            {
                var old = Sim.Find(em, card.QuestId);
                if (old == Entity.Null || em.HasComponent<Quest>(old) && em.GetComponentData<Quest>(old).Status == QuestStatus.Claimed)
                    selectedQuest = identity.Id;
            }
            var expanded = selectedQuest == identity.Id;
            var remaining = q.Status == QuestStatus.Completed ? "待领奖" : q.Status == QuestStatus.Offered ? "未接受" : q.Deadline == 0 ? "无期限" : "剩余 " + math.max(0, q.Deadline - em.GetComponentData<Session>(root).Turn) + " 回合";
            if (q.Status == QuestStatus.Active && !ProgressionOps.Prerequisites(em, root, identity.Definition)) remaining = "等待前置";
            Action action = null; var label = "";
            if (q.Status == QuestStatus.Completed) { label = "✓"; if (day) action = () => ShowBuildingConfirmation("领取：" + identity.Name, RewardConfirmation(identity.Definition), () => Send(CommandKind.ClaimQuest, identity.Id)); }
            else if (q.Status == QuestStatus.Active && q.Mainline == 0) { label = "X"; if (day) action = () => ConfirmAbandonQuest(identity); }
            card.Show(identity.Id, source, identity.Name + (q.Status == QuestStatus.Offered ? " · 查看邀约" : ""), remaining, expanded, q.Status == QuestStatus.Completed,
                () => { selectedQuest = selectedQuest == identity.Id ? 0 : identity.Id; nextRefresh = 0; }, label, action);
            if(QuestOps.Trackable(em,entity))
            {
                card.Tracking.gameObject.SetActive(true);card.Tracking.onValueChanged.RemoveAllListeners();
                card.Tracking.SetIsOnWithoutNotify(QuestOps.Tracking(em,root).Target==identity.Id);
                card.Tracking.onValueChanged.AddListener(on=>{if(on)Send(CommandKind.TrackQuest,identity.Id,argument:1);else Send(CommandKind.TrackQuest,identity.Id,argument:2);});
            }
            if (!expanded) return;
            QuestDetailRows = card.Body; Clear(card.Body); QuestDetails(entity, day);
        }
        void Quests()
        {
            if (QuestWindow == null)
            {
                var window = InterfaceWidgets.Rect("Quest Window", GetComponentInParent<Canvas>().transform, new Vector2(.08f, .06f), new Vector2(.92f, .84f));
                window.gameObject.AddComponent<Image>().color = new Color(.055f, .08f, .12f); QuestWindow = window.gameObject;
                QuestCloseButton = AddPanelHeader(window, "任务与邀约", out _);
                questCapacityLabel = InterfaceWidgets.Text("", InterfaceWidgets.Rect("Accepted heading", window, new Vector2(.02f,.88f), new Vector2(.49f,.94f)), Status.font, 20);
                InterfaceWidgets.Text("邀约面板", InterfaceWidgets.Rect("Invitations heading", window, new Vector2(.51f,.88f), new Vector2(.97f,.94f)), Status.font, 20);
                QuestListRows = InterfaceWidgets.Scroll(window, "Accepted quests", new Vector2(.01f,.02f), new Vector2(.49f,.80f));
                QuestPoolRows = InterfaceWidgets.Scroll(window, "Invitation slots", new Vector2(.51f,.02f), new Vector2(.99f,.75f));
                var track = InterfaceWidgets.Button("自动追踪", InterfaceWidgets.Rect("Tracking", window, new Vector2(.02f,.81f), new Vector2(.20f,.87f)), Status.font, () => Send(CommandKind.TrackQuest, argument: 0));
                questWaitingLabel=InterfaceWidgets.Text("",InterfaceWidgets.Rect("Waiting mainline",window,new Vector2(.21f,.80f),new Vector2(.49f,.88f)),Status.font,16);
                for (var i=0; i<4; i++)
                {
                    var bit=1<<i;
                    questTabs[i] = InterfaceWidgets.Checkbox(QuestOfferOps.TypeName(i), InterfaceWidgets.Rect("Category",window,new Vector2(.51f+i*.12f,.81f),new Vector2(.625f+i*.12f,.87f)),Status.font,on=>{questTypeMask=on?questTypeMask|bit:questTypeMask&~bit;nextRefresh=0;});
                }
                questFilterButton = InterfaceWidgets.Button("全部来源", InterfaceWidgets.Rect("Source filter",window,new Vector2(.51f,.755f),new Vector2(.99f,.805f)),Status.font,()=>{questSourceFilter=0;nextRefresh=0;});
            }
            QuestWindow.SetActive(true); QuestDetailRows = null;
            foreach (var card in questCards.Values) card.Rect.gameObject.SetActive(false);
            if (BuildingToolbar != null) BuildingToolbar.gameObject.SetActive(false);
            if (BuildingDetailsPanel != null) BuildingDetailsPanel.SetActive(false);
            var day = em.GetComponentData<Session>(root).Phase == Phase.Day && em.GetComponentData<Session>(root).CheckpointPending == 0;
            questCapacityLabel.text = "已接受任务 · " + ProgressionOps.QuestCount(em) + "/" + ProgressionOps.QuestCapacity(em);
            questFilterButton.GetComponentInChildren<Text>().text = questSourceFilter == 0 ? "全部来源" : "取消来源筛选：" + EntityName(questSourceFilter);
            for(var i=0;i<4;i++) questTabs[i].SetIsOnWithoutNotify((questTypeMask & (1<<i))!=0);
            var waiting=ProgressionOps.WaitingMainlines(em,root);
            questWaitingLabel.text=waiting.Count==0?"主线与一般任务共用槽位":"主线等待空槽："+Name(waiting[0])+(waiting.Count>1?" 等 "+waiting.Count+" 项":"");
            var accepted = new List<Entity>();
            using var quests=Sim.OrderedEntities<Quest>(em);
            foreach(var e in quests){var q=em.GetComponentData<Quest>(e);if(q.Status==QuestStatus.Active || q.Status==QuestStatus.Completed)accepted.Add(e);}
            using var buildings=Sim.OrderedEntities<Building>(em);
            foreach(var e in buildings)
            {
                var capacity=ProgressionOps.QuestContainerCapacity(em,e); var id=em.GetComponentData<Identity>(e).Id;
                for(var i=0;i<capacity;i++)
                {
                    Entity occupant=Entity.Null;
                    foreach(var task in accepted){var q=em.GetComponentData<Quest>(task);if(q.Container==id && q.ContainerSlot==i){occupant=task;break;}}
                    ShowQuestCard("slot:"+id+":"+i,QuestListRows,"承接槽 "+(i+1)+" · "+QuestBuildingSource(e)+" 提供",occupant,day);
                }
            }

            InvitationPool(day);
            var unused=new List<string>();
            foreach(var pair in questCards) if(!pair.Value.Rect.gameObject.activeSelf)unused.Add(pair.Key);
            foreach(var key in unused){var card=questCards[key];stableRows.Remove(card.Body);rowPools.Remove(card.Body);Destroy(card.Rect.gameObject);questCards.Remove(key);}
        }
        void QuestDetails(Entity entity, bool day)
        {
            void Detail(string label, Action action = null) => Row(label, action, parent: QuestDetailRows);
            var identity = em.GetComponentData<Identity>(entity); var quest = em.GetComponentData<Quest>(entity);
            var sourceData = BuildingSource(identity.Definition); if (sourceData != null && !string.IsNullOrEmpty(sourceData.Description)) Detail(sourceData.Description);
            Detail(QuestSource(quest) + "\n" + QuestDeadline(quest)); if (!day) Detail("夜晚仅查看，签约、提交、领奖请在白天操作。");
            if (quest.Status == QuestStatus.Active && !ProgressionOps.Prerequisites(em, root, identity.Definition)) Detail("等待前置条件，原承接槽位保留");
            var definitionData = Sim.Definition(em, root, identity.Definition);
            for (var i = 0; i < definitionData.RuleCount; i++) { var r = Sim.GetRule(em, root, definitionData.RuleStart + i); if (r.Kind == RuleKind.Prerequisite) Detail("前置：" + Name(r.Target) + (Sim.HasGrant(em, root, r.Target) ? "（已领取 / 完成）" : "（未满足）")); }
            ForDefinitions(ContentKind.Quest, (at, d) => { if ((d.Flags & 2) != 0) return; for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind == RuleKind.Prerequisite && r.Target == identity.Definition) Detail("后续：" + d.Name + "（领取本任务奖励后检查解锁）"); } });
            if (quest.Mainline == 0) foreach (var cost in ProgressionOps.FailureCosts(em, root, identity.Definition)) Detail((quest.Status == QuestStatus.Completed ? "容器失效最多扣除 " : "放弃 / 超时 / 容器失效最多扣除 ") + Name(cost.Item) + " × " + cost.Amount + "；不形成债务");

            Detail("任务要求（全部满足后完成）");
            foreach (var p in em.GetBuffer<QuestProgress>(entity))
            {
                Detail(RequirementText(p)); var r = Sim.GetRule(em, root, p.RuleIndex);
                if ((r.Kind == RuleKind.RequireBuilding || r.Kind == RuleKind.RequireCrop) && quest.Status == QuestStatus.Active)
                {
                    using var buildings = Sim.OrderedEntities<Building>(em); foreach (var b in buildings) if (em.GetComponentData<Identity>(b).Definition == r.Target) { var buildingId = em.GetComponentData<Identity>(b).Id; Detail("定位目标建筑：" + EntityName(buildingId), () => { OpenPanel("建筑"); FocusBuilding(buildingId); }); break; }
                }
                if (r.Kind != RuleKind.SubmitItem || quest.Status != QuestStatus.Active) continue;
                var key = p.Key.ToString(); var quote = QuestOps.Submission(em, root, entity, key);
                Detail("提交 " + Name(r.Target) + " · 持有 " + quote.Available + " / 尚需 " + quote.Remaining, day && quote.Code == ResultCode.Success ? () => ConfirmQuestSubmission(identity.Id, key) : null);
            }
            Detail("奖励（物品总价值 " + QuestOps.RewardValue(em,root,identity.Definition) + "，点击领取后发放）"); foreach (var reward in QuestRewards(identity.Definition)) Detail(reward);
            if (quest.Status == QuestStatus.Offered)
            {
                var provider=Sim.Find(em,quest.Source);
                if(!QuestOfferOps.Available(em,root,provider,out var reason))Detail(reason);
                var costs = new List<string> { "签约后开始期限计时；分步任务领奖后由下一步沿用原槽，最后一步领奖后释放承接槽位。承接建筑停工、缺工、维护失败、拆除或缩容导致槽位失效时，任务（含待领奖）丢失并执行放弃惩罚。" }; foreach (var c in ProgressionOps.FailureCosts(em, root, identity.Definition)) costs.Add("失败最多扣除 " + Name(c.Item) + " × " + c.Amount);
                Detail("签约", day && ProgressionOps.QuestCount(em) < ProgressionOps.QuestCapacity(em) ? () => ShowBuildingConfirmation("签约：" + identity.Name, costs, () => Send(CommandKind.AcceptQuest, identity.Id)) : null);
                if (ProgressionOps.QuestCount(em) >= ProgressionOps.QuestCapacity(em)) Detail("承接名额已满，请完成任务链并领取奖励，或放弃一般任务。");
                Detail("拒绝邀约", !day ? null : () => ShowBuildingConfirmation("拒绝：" + identity.Name, new[] { "当前邀约将移除，来源槽重新开始冷却。不扣失败惩罚。" }, () => Send(CommandKind.RejectQuest, identity.Id)));
            }

        }
        IEnumerable<string> RewardConfirmation(int definition)
        {
            foreach (var line in QuestRewards(definition)) yield return line;
            yield return "有后续步骤时沿用原承接槽位，任务链结束后才释放。";
            yield return "正常库存放不下时不会部分发奖，也不会消耗任务；整理库存后重试。";
        }
        void ConfirmQuestRecruit(Identity source, int slot)
        {
            var entity = Sim.Find(em, source.Id); if (!Sim.Operational(em, entity)) return; var b = em.GetComponentData<Building>(entity); var d = Sim.Definition(em, root, source.Definition); var lines = new List<string>();
            for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind == RuleKind.QuestRecruitCost && (r.Level == 0 || r.Level == b.Level)) lines.Add(Name(r.Target) + " × " + r.Amount); }
            if (lines.Count == 0) lines.Add("无额外物资费用"); lines.Add("生成新的邀约，不会自动签约。条件不满足时不扣费。");
            ShowBuildingConfirmation("付费邀约：" + source.Name, lines, () => Send(CommandKind.RecruitQuest, source.Id, argument: slot));
        }
        public void ConfirmQuestSubmission(ulong id, string key)
        {
            var quote = QuestOps.Submission(em, root, Sim.Find(em, id), key); if (quote.Code != ResultCode.Success) { Message.text = quote.Reason; return; }
            InputField amount = null;
            ShowBuildingConfirmation("提交：" + Name(quote.Item), new[] { "持有 " + quote.Available + "，尚需 " + quote.Remaining + "；本次可提交 1～" + quote.Maximum, "只扣此项，提交后不可退回。库存或任务变化后需要重新确认。" }, () =>
            {
                if (!int.TryParse(amount.text, out var quantity) || quantity < 1 || quantity > quote.Maximum) { Message.text = "请输入 1～" + quote.Maximum + " 的整数"; ConfirmQuestSubmission(id, key); return; }
                Send(CommandKind.SubmitQuest, id, definition: quote.Item, amount: quantity, text: key + "|" + quote.Stamp);
            });
            var row = Row("", parent: BuildingConfirmRows); row.transform.SetSiblingIndex(3);
            var old = row.transform.Find("QuestQuantity"); var input = old != null ? old.gameObject : TMPro.TMP_DefaultControls.CreateInputField(new TMPro.TMP_DefaultControls.Resources()); input.name = "QuestQuantity"; input.transform.SetParent(row.transform, false); input.SetActive(true);
            var rect = (RectTransform)input.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(8, 2); rect.offsetMax = new Vector2(-8, -2);
            amount = input.GetComponent<InputField>(); amount.contentType = InputField.ContentType.IntegerNumber; amount.characterLimit = 9; amount.textComponent.font = Status.font; if (amount.placeholder is Text placeholder) placeholder.font = Status.font; amount.SetTextWithoutNotify(quote.Maximum.ToString()); QuestAmountInput = amount;
        }
    }
}
