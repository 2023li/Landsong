using Landsong.ECS.Definitions;
using Landsong.Content;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Talent : GameFeatureViewBase
    {
        [Sirenix.OdinInspector.LabelText("人才显示目录"), Sirenix.OdinInspector.Required]
        public TalentDisplayCatalog Talents;
        internal IntelligenceViewState intelligence;
        [LabelText("画像覆盖目录"), Required]
        public PortraitDisplayCatalog Portraits;
        public override void Render() => TalentRows();
        internal UI_GamePanel_BuildingActionBar buildingController;
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
                refresh.NextPanel = 0;
            });
            rowsController.Row("查看新增交际对象", CourtDay ? () => commandsController.TryQueue(new RefreshTalentsRequest()) : null);
            var q = CourtOps.Rules(sessionController.em, sessionController.root);
            using (var all = WorldQueries.OrderedEntities<Talent>(sessionController.em))
                foreach (var e in all)
                {
                    var id = sessionController.em.GetComponentData<Identity>(e);
                    var t = sessionController.em.GetComponentData<Talent>(e);
                    var p = sessionController.em.GetComponentData<Royal>(e);
                    rowsController.Row((courtPerson == id.Id ? "▶ " : "") + id.Name + " · " + (p.Alive == 0 ? "已逝" : courtSocial ? "好感 " + p.Affection : t.Recruited == 0 ? "未招募" : !t.Slot.IsValid ? "未任职" : TalentSlotDefinitions.Get(sessionController.em, sessionController.root, t.Slot).Metadata.Name.ToString() + (t.Paid != 0 ? "（生效）" : "（欠薪停用）")), () =>
                    {
                        courtPerson = id.Id;
                        refresh.NextPanel = 0;
                    });
                }

            var person = WorldQueries.Find(sessionController.em, courtPerson);
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
            var definition = sessionController.em.GetComponentData<TalentDefinitionRef>(person).Definition;
            ref var d = ref TalentDefinitions.Get(sessionController.em, sessionController.root, definition);
            var effects = new List<string>();
            DefinitionEffectText.Append(effects, sessionController.em, sessionController.root, ref d.Effects);
            TalentJobEffectText.Append(effects, sessionController.em, sessionController.root, ref d.JobEffects);
            foreach (var line in effects)
                rowsController.Row("岗位效果：" + line);
            if (courtSocial)
            {
                rowsController.Row("赠礼：" + q.GiftCost + " 金币，好感 +" + q.GiftAffection + "（每回合一次）", can && royal.LastGiftTurn != sessionController.em.GetComponentData<GameClock>(sessionController.root).Turn ? () => commandsController.TryQueue(new GiftPersonRequest { Person = identity.Id }) : null);
                if (d.SocialTasks.Length > 0)
                {
                    var task = d.SocialTasks[0];
                    rowsController.Row(royal.TaskClaimed != 0 ? "个人委托：已完成" : "个人委托：提交 " + ItemDefinitions.Get(sessionController.em, sessionController.root, task.Item).Metadata.Name + " × " + task.Quantity + "，好感 +" + task.AffectionReward, can && royal.TaskClaimed == 0 ? () => commandsController.TryQueue(new CompleteSocialTaskRequest { Person = identity.Id }) : null);
                }

                rowsController.Row("求婚（好感需 " + q.MarriageAffection + "；双方成年且无在世配偶）", can && royal.Affection >= q.MarriageAffection ? () => buildingController.ShowBuildingConfirmation("向 " + identity.Name + " 求婚", new[] { "成为君王配偶后会自动离开人才岗位并停薪，不再产生岗位加成。", "身份、经验和基因保留。君王及配偶不可兼任其他职位。" }, () => commandsController.TryQueue(new ProposeMarriageRequest { Person = identity.Id })) : null);
            }

            if (talent.Recruited == 0)
                rowsController.Row("招募人才（好感需 " + q.RecruitAffection + "）", can && royal.Affection >= q.RecruitAffection && CourtOps.JobEligible(sessionController.em, person) ? () => commandsController.TryQueue(new RecruitTalentRequest { Person = identity.Id }) : null);
            else
            {
                if (!CourtOps.JobEligible(sessionController.em, person))
                    rowsController.Row("当前身份不可任职：君王、配偶及退位者不兼任人才岗位。");
                for (int index = 0; index < TalentSlotDefinitions.Count(sessionController.em, sessionController.root); index++)
                {
                    var slotId = TalentSlotId.FromIndex(index);
                    ref var slot = ref TalentSlotDefinitions.Get(sessionController.em, sessionController.root, slotId);
                    rowsController.Row("任职：" + slot.Metadata.Name + "（立即付一次工资）", can && SocialOps.Accepts(sessionController.em, sessionController.root, person, slotId) && talent.Slot != slotId ? () => commandsController.TryQueue(new AssignTalentRequest { Person = identity.Id, Slot = slotId }) : null);
                }

                rowsController.Row("离开岗位", can && talent.Slot.IsValid ? () => commandsController.TryQueue(new AssignTalentRequest { Person = identity.Id, Slot = TalentSlotId.None }) : null);
                rowsController.Row("解雇人才（保留人物与好感）", can ? () => buildingController.ShowBuildingConfirmation("解雇 " + identity.Name, new[] { "停止工资及岗位效果；人物不会被删除，可以再次交际。" }, () => commandsController.TryQueue(new DismissTalentRequest { Person = identity.Id })) : null);
            }

            TraitRows(person);
        }

        internal void TraitRows(Entity e)
        {
            foreach (var t in sessionController.em.GetBuffer<TraitEntry>(e))
                if (t.Revealed != 0)
                    rowsController.Row("特性：" + RoyalTraitDefinitions.Get(sessionController.em, sessionController.root, t.Definition).Metadata.Name.ToString() + (t.Active != 0 ? "（已激活）" : "（尚未激活）"));
            var p = sessionController.em.GetComponentData<Royal>(e);
            if (p.FateUntil > 0 && p.Alive != 0)
                rowsController.Row("知天命：自然寿限还剩 " + System.Math.Max(0, p.FateUntil - sessionController.em.GetComponentData<GameClock>(sessionController.root).Turn) + " 回合；不抵挡非自然死亡。");
        }

        internal void RefreshPresentation()
        {
            bool active = navigation.IsPanelOpen && !intelligence.IsOpen && navigation.Panel == GamePanelId.Talent;
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
            var catalog = Portraits;
            using (var all = WorldQueries.OrderedEntities<Royal>(sessionController.em))
                foreach (var entity in all)
                {
                    var person = sessionController.em.GetComponentData<Royal>(entity);
                    if (!sessionController.em.HasComponent<Talent>(entity))
                        continue;
                    var id = sessionController.em.GetComponentData<Identity>(entity);
                    int col = 0;
                    rows.TryGetValue(col, out int row);
                    rows[col] = row + 1;
                    var talentId = sessionController.em.GetComponentData<TalentDefinitionRef>(entity).Definition;
                    string definition = TalentDefinitions.Get(sessionController.em, sessionController.root, talentId).Metadata.Id.ToString();
                    var portrait = catalog.Face(definition, id.Id) ?? Talents.Get(talentId)?.Icon;
                    var key = id.Id;
                    cards.Add(new CourtCard { Id = key, Parent = person.Parent, SecondParent = person.SecondParent, Spouse = person.Spouse, Column = col, Row = row, Title = id.Name.ToString(), Detail = person.Age + " 岁 · " + (person.Alive == 0 ? "已逝" : person.Role == 0 ? "君王" : person.Role == 1 ? "配偶" : person.Role == 3 ? "前朝成员" : person.Role == 4 ? "交际人物" : "王室成员") + (navigation.Panel == GamePanelId.Talent ? "\n影响力 " + person.Influence.ToString("0.0") : "") + (CourtOps.State(sessionController.em, sessionController.root).Crown == key ? " · 储君" : ""), BindPortrait = binding => binding.Bind(sessionController.em, sessionController.root, key), RequestCount = PersonRequestOps.Pending(sessionController.em, sessionController.root, entity).Count, Influence = person.Influence, Monarch = person.Alive != 0 && person.Role == 0, EverMonarch = person.EverMonarch != 0, Portrait = portrait, Dead = person.Alive == 0, Selected = courtPerson == key, Click = () =>
                    {
                        courtPerson = key;
                        refresh.NextPanel = 0;
                    } });
                }

            courtGraph.Show("人物画像 · 点击查看交际 / 任职", cards);
        }
    }
}
