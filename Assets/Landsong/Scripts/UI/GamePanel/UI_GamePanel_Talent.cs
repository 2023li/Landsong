using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Talent : Moyo.Unity.UIViewBase, IGameFeatureRenderer
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

        public void Render() => TalentRows();
        internal GameUiSession sessionController;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_RowRenderer rowsController;
        internal IGameUiNavigation navigation;
        internal UI_GamePanel_Building buildingController;
        internal bool CourtDay => sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day;

        [Sirenix.OdinInspector.LabelText("王室图视图")]
        public UI_GamePanel_CourtGraph CourtGraph;
        UI_GamePanel_CourtGraph courtGraph;
        internal ulong courtPerson;
        internal bool courtSocial;
        internal void ResetSession()
        {
            courtPerson = 0;
            courtSocial = false;
            CourtGraph.ClearSession();
            CourtGraph.gameObject.SetActive(false);
        }

        internal void TalentRows()
        {
            rowsController.Row(courtSocial ? "交际 · 人物身份与人才岗位共用" : "人才 · 任职且已付薪才产生加成");
            rowsController.Row(courtSocial ? "返回人才岗位" : "打开交际页面", () =>
            {
                courtSocial = !courtSocial;
                courtPerson = 0;
                sessionController.nextRefresh = 0;
            });
            rowsController.Row("查看新增交际对象", CourtDay ? () => commandsController.Send(CommandKind.RefreshTalents) : null);
            var q = CourtOps.Rules(sessionController.em, sessionController.root);
            using (var all = Sim.OrderedEntities<Talent>(sessionController.em))
                foreach (var e in all)
                {
                    var id = sessionController.em.GetComponentData<Identity>(e);
                    var t = sessionController.em.GetComponentData<Talent>(e);
                    var p = sessionController.em.GetComponentData<Royal>(e);
                    rowsController.Row((courtPerson == id.Id ? "▶ " : "") + id.Name + " · " + (p.Alive == 0 ? "已逝" : courtSocial ? "好感 " + p.Affection : t.Recruited == 0 ? "未招募" : t.Slot < 0 ? "未任职" : sessionController.Name(t.Slot) + (t.Paid != 0 ? "（生效）" : "（欠薪停用）")), () =>
                    {
                        courtPerson = id.Id;
                        sessionController.nextRefresh = 0;
                    });
                }

            var person = Sim.Find(sessionController.em, courtPerson);
            if (person == Entity.Null || !sessionController.em.HasComponent<Talent>(person))
            {
                rowsController.Row("选择人物查看详情与操作");
                return;
            }

            var identity = sessionController.em.GetComponentData<Identity>(person);
            var talent = sessionController.em.GetComponentData<Talent>(person);
            var royal = sessionController.em.GetComponentData<Royal>(person);
            var can = CourtDay && royal.Alive != 0;
            rowsController.Row(identity.Name + " · " + royal.Age + " 岁 · 好感 " + royal.Affection + "/100");
            rowsController.Row("等级 " + talent.Level + " · 经验 " + talent.Experience + " · 任职 " + talent.AssignedTurns + " 回合 · 工资 " + SocialOps.Wage(sessionController.em, sessionController.root, person) + " 金币/次结算");
            var d = Sim.Definition(sessionController.em, sessionController.root, identity.Definition);
            for (int i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(sessionController.em, sessionController.root, d.RuleStart + i);
                if (r.Kind == RuleKind.SoldierAttackBonus)
                    rowsController.Row("岗位效果：所有士兵攻击 +" + r.Value.ToString("P0") + "（不含英雄）");
                if (r.Kind == RuleKind.ActionPowerBonus)
                    rowsController.Row("岗位效果：所有建筑连接行动力 +" + r.Value);
                if (r.Kind == RuleKind.CropHarvestBonus)
                    rowsController.Row("岗位效果：农田收获 +" + r.Value.ToString("P0"));
            }

            if (courtSocial)
            {
                rowsController.Row("赠礼：" + q.GiftCost + " 金币，好感 +" + q.GiftAffection + "（每回合一次）", can && royal.LastGiftTurn != sessionController.em.GetComponentData<Session>(sessionController.root).Turn ? () => commandsController.Send(CommandKind.GiftPerson, identity.Id) : null);
                var task = Sim.Rule(sessionController.em, sessionController.root, identity.Definition, RuleKind.SocialTask);
                if (task.Level >= 0)
                    rowsController.Row(royal.TaskClaimed != 0 ? "个人委托：已完成" : "个人委托：提交 " + sessionController.Name(task.Target) + " × " + task.Amount + "，好感 +" + task.B, can && royal.TaskClaimed == 0 ? () => commandsController.Send(CommandKind.CompleteSocialTask, identity.Id) : null);
                rowsController.Row("求婚（好感需 " + q.MarriageAffection + "；双方成年且无在世配偶）", can && royal.Affection >= q.MarriageAffection ? () => buildingController.ShowBuildingConfirmation("向 " + identity.Name + " 求婚", new[] { "成为君王配偶后会自动离开人才岗位并停薪，不再产生岗位加成。", "身份、经验和基因保留。君王及配偶不可兼任其他职位。" }, () => commandsController.Send(CommandKind.ProposeMarriage, identity.Id)) : null);
            }

            if (talent.Recruited == 0)
                rowsController.Row("招募人才（好感需 " + q.RecruitAffection + "）", can && royal.Affection >= q.RecruitAffection && CourtOps.JobEligible(sessionController.em, person) ? () => commandsController.Send(CommandKind.RecruitTalent, identity.Id) : null);
            else
            {
                if (!CourtOps.JobEligible(sessionController.em, person))
                    rowsController.Row("当前身份不可任职：君王、配偶及退位者不兼任人才岗位。");
                sessionController.ForDefinitions(ContentKind.TalentSlot, (i, slot) => rowsController.Row("任职：" + slot.Name + "（立即付一次工资）", can && SocialOps.Accepts(sessionController.em, sessionController.root, person, i) && talent.Slot != i ? () => commandsController.TryQueue(CommandRequests.AssignTalent(identity.Id, i)) : null));
                rowsController.Row("离开岗位", can && talent.Slot >= 0 ? () => commandsController.TryQueue(CommandRequests.AssignTalent(identity.Id, -1)) : null);
                rowsController.Row("解雇人才（保留人物与好感）", can ? () => buildingController.ShowBuildingConfirmation("解雇 " + identity.Name, new[] { "停止工资及岗位效果；人物不会被删除，可以再次交际。" }, () => commandsController.Send(CommandKind.DismissTalent, identity.Id)) : null);
            }

            TraitRows(person);
        }

        internal void TraitRows(Entity e)
        {
            foreach (var t in sessionController.em.GetBuffer<TraitEntry>(e))
                if (t.Revealed != 0)
                    rowsController.Row("特性：" + sessionController.Name(t.Definition) + (t.Active != 0 ? "（已激活）" : "（尚未激活）"));
            var p = sessionController.em.GetComponentData<Royal>(e);
            if (p.FateUntil > 0 && p.Alive != 0)
                rowsController.Row("知天命：自然寿限还剩 " + System.Math.Max(0, p.FateUntil - sessionController.em.GetComponentData<Session>(sessionController.root).Turn) + " 回合；不抵挡非自然死亡。");
        }

        internal void RefreshPresentation()
        {
            bool active = navigation.IsPanelOpen && !sessionController.intel && navigation.Panel == GamePanelId.Talent;
            CourtGraph.gameObject.SetActive(active);
            if (!active)
                return;
            if (courtGraph == null)
            {
                courtGraph = CourtGraph;
                courtGraph.BindActions(navigation.ClosePanel);
            }

            var cards = new List<CourtCard>();
            var rows = new Dictionary<int, int>();
            var catalog = PresentationRuntime.Instance?.Catalog;
            using (var all = Sim.OrderedEntities<Royal>(sessionController.em))
                foreach (var entity in all)
                {
                    var person = sessionController.em.GetComponentData<Royal>(entity);
                    if (!sessionController.em.HasComponent<Talent>(entity))
                        continue;
                    var id = sessionController.em.GetComponentData<Identity>(entity);
                    int col = 0;
                    rows.TryGetValue(col, out int row);
                    rows[col] = row + 1;
                    string definition = Sim.ValidDefinition(sessionController.em, sessionController.root, id.Definition) ? Sim.Definition(sessionController.em, sessionController.root, id.Definition).Id.ToString() : "";
                    var portrait = catalog?.Face(definition, id.Id) ?? buildingController.BuildingSource(id.Definition)?.Icon;
                    var key = id.Id;
                    cards.Add(new CourtCard { Id = key, Parent = person.Parent, SecondParent = person.SecondParent, Spouse = person.Spouse, Column = col, Row = row, Title = id.Name.ToString(), Detail = person.Age + " 岁 · " + (person.Alive == 0 ? "已逝" : person.Role == 0 ? "君王" : person.Role == 1 ? "配偶" : person.Role == 3 ? "前朝成员" : person.Role == 4 ? "交际人物" : "王室成员") + (navigation.Panel == GamePanelId.Talent ? "\n影响力 " + person.Influence.ToString("0.0") : "") + (CourtOps.State(sessionController.em, sessionController.root).Crown == key ? " · 储君" : ""), BindPortrait = binding => binding.Bind(sessionController.em, sessionController.root, key), RequestCount = PersonRequestOps.Pending(sessionController.em, sessionController.root, entity).Count, Influence = person.Influence, Monarch = person.Alive != 0 && person.Role == 0, EverMonarch = person.EverMonarch != 0, Portrait = portrait, Dead = person.Alive == 0, Selected = courtPerson == key, Click = () =>
                    {
                        courtPerson = key;
                        sessionController.nextRefresh = 0;
                    } });
                }

            courtGraph.Show("人物画像 · 点击查看交际 / 任职", cards);
        }
    }
}
