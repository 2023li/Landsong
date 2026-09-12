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
    public sealed class UI_GamePanel_Court : Moyo.Unity.UIViewBase, IGameFeatureRenderer, IGameCourtUi
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

        public void Render() => Royals();
        internal UI_GamePanel_Building buildingController;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Marriage marriageController;
        internal UI_GamePanel_PersonRequests requestsController;
        internal IGameUiNavigation navigation;
        internal UI_GamePanel_RowRenderer rowsController;
        internal GameUiSession sessionController;
        internal ulong courtPerson;
        internal bool CourtDay => sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day;

        internal string PersonName(ulong id) => id == 0 ? "无" : sessionController.EntityName(id);
        internal void RoyalRows()
        {
            var c = CourtOps.State(sessionController.em, sessionController.root);
            var q = CourtOps.Rules(sessionController.em, sessionController.root);
            var king = CourtOps.Monarch(sessionController.em);
            rowsController.Row("王位传承 · 储君：" + PersonName(c.Crown));
            rowsController.Row("生育范围：现任国王及其成年子女；孙辈须等父母登基。历史家谱永久保留。");
            using (var requests = Sim.OrderedEntities<Royal>(sessionController.em))
                foreach (var e in requests)
                    if (RoyalFamilyOps.RequestValid(sessionController.em, sessionController.root, e))
                    {
                        var id = sessionController.em.GetComponentData<Identity>(e).Id;
                        var p = sessionController.em.GetComponentData<Royal>(e);
                        rowsController.Row(PersonName(id) + "希望能和" + PersonName(p.RequestedSpouse) + "结婚", () => marriageController.ShowMarriage(id));
                    }

            rowsController.Row("只由当前君王在世直系后代继承，不限性别和年龄。影响力上限 100；储君正增长 ×" + q.PrinceGrowth + "。");
            rowsController.Row("产能修正 " + CourtOps.Modifier(sessionController.em, sessionController.root, RuleKind.ProductionBonus, -1).ToString("P0") + " · 士兵攻击修正 " + CourtOps.Modifier(sessionController.em, sessionController.root, RuleKind.SoldierAttackBonus, -1).ToString("P0"));
            if (c.Disorder > 0 && sessionController.em.GetComponentData<Session>(sessionController.root).Turn <= c.DisorderUntil)
                rowsController.Row("朝局动荡：−" + c.Disorder.ToString("P0") + "，至回合 " + c.DisorderUntil);
            if (c.LegacySeverity != 0)
                rowsController.Row("跨代政治遗留：产能 " + c.LegacyProduction.ToString("P0") + " / 士兵攻击 " + c.LegacyAttack.ToString("P0") + "；同辈换君和短期退位不会减轻。");
            var person = Sim.Find(sessionController.em, courtPerson);
            rowsController.Row("废除储君", CourtDay && c.Crown != 0 ? () => buildingController.ShowBuildingConfirmation("废除储君", new[] { "产生朝局动荡；原储君不会失去影响力。" }, () => commandsController.Send(CommandKind.DesignateHeir)) : null);
            rowsController.Row("主动退位", CourtDay && c.Crown != 0 ? () => buildingController.ShowBuildingConfirmation("主动退位", new[] { "仍按正常继承规则判定夺位与政治效果，不保证储君继位。", "退位君王及配偶不可重新任职或再次继位。" }, () => commandsController.Send(CommandKind.Abdicate, c.Crown)) : null);
            if (c.VisitOfferTurn > 0 && c.VisitResolved == 0)
            {
                rowsController.Row("外交邀请：远方大国邀请一位适龄继承人访问（队长需 " + q.CaptainAge + " 岁）。");
                rowsController.Row("派所选继承人出访：护卫费 " + q.VisitCost + " 金币", CourtDay && CourtOps.AvailableCaptain(sessionController.em, sessionController.root, person) ? () => commandsController.TryQueue(CommandRequests.RoyalVisit(courtPerson, true)) : null);
                rowsController.Row("无护卫出访：10% 遇难风险", CourtDay && CourtOps.AvailableCaptain(sessionController.em, sessionController.root, person) ? () => buildingController.ShowBuildingConfirmation("高风险出访", new[] { "10% 概率遇难，知天命不能抵挡。储君遇难也会产生继承动荡。", "存活后出访 " + q.VisitDuration + " 回合，归来提高影响力。" }, () => commandsController.TryQueue(CommandRequests.RoyalVisit(courtPerson, false))) : null);
                rowsController.Row("婉拒邀请", CourtDay ? () => commandsController.TryQueue(CommandRequests.RefuseRoyalVisit()) : null);
            }

            rowsController.Row("宫廷纪事");
            if (sessionController.em.HasBuffer<CourtLogEntry>(sessionController.root))
                foreach (var log in sessionController.em.GetBuffer<CourtLogEntry>(sessionController.root))
                    rowsController.Row("回合 " + log.Turn + " · " + (log.Person == 0 ? "" : PersonName(log.Person) + "：") + log.Message);
        }

        public void CourtIntelRows(int value)
        {
            foreach (var line in IntelOps.CourtLines(sessionController.em, sessionController.root, value))
                rowsController.Row(line);
        }

        [Sirenix.OdinInspector.LabelText("王室图视图")]
        public UI_GamePanel_CourtGraph CourtGraph;
        internal UI_GamePanel_CourtGraph courtGraph;
        [Sirenix.OdinInspector.LabelText("王室王冠图标")]
        public Sprite RoyalCrownIcon;
        internal void RefreshCourtPresentation()
        {
            bool active = navigation.IsPanelOpen && !sessionController.intel && navigation.Panel == GamePanelId.Royal;
            // Visibility belongs to the authored view, including its first version-driven refresh.
            CourtGraph.gameObject.SetActive(active);
            if (!active)
            {
                RefreshRoyalDetails(false);
                return;
            }

            if (courtGraph == null)
            {
                if (CourtGraph == null)
                    throw new System.InvalidOperationException("王室展示面板检查器引用缺失：CourtGraph");
                courtGraph = CourtGraph;
                courtGraph.BindActions(navigation.ClosePanel);
            }

            courtGraph.CrownIcon = RoyalCrownIcon;
            RefreshRoyalDetails(navigation.Panel == GamePanelId.Royal);
            var cards = new List<CourtCard>();
            var rows = new Dictionary<int, int>();
            var catalog = PresentationRuntime.Instance?.Catalog;
            using (var all = Sim.OrderedEntities<Royal>(sessionController.em))
                foreach (var entity in all)
                {
                    var person = sessionController.em.GetComponentData<Royal>(entity);
                    if (person.Role == 4)
                        continue;
                    var id = sessionController.em.GetComponentData<Identity>(entity);
                    int col = Mathf.Max(0, person.Generation);
                    rows.TryGetValue(col, out int row);
                    rows[col] = row + 1;
                    string definition = Sim.ValidDefinition(sessionController.em, sessionController.root, id.Definition) ? Sim.Definition(sessionController.em, sessionController.root, id.Definition).Id.ToString() : "";
                    var portrait = catalog?.Face(definition, id.Id) ?? buildingController.BuildingSource(id.Definition)?.Icon;
                    var key = id.Id;
                    cards.Add(new CourtCard { Id = key, Parent = person.Parent, SecondParent = person.SecondParent, Spouse = person.Spouse, Column = col, Row = row, Title = id.Name.ToString(), Detail = person.Age + " 岁 · " + (person.Alive == 0 ? "已逝" : person.Role == 0 ? "君王" : person.Role == 1 ? "配偶" : person.Role == 3 ? "前朝成员" : person.Role == 4 ? "交际人物" : "王室成员") + "" + (CourtOps.State(sessionController.em, sessionController.root).Crown == key ? " · 储君" : ""), BindPortrait = binding => binding.Bind(sessionController.em, sessionController.root, key), RequestCount = PersonRequestOps.Pending(sessionController.em, sessionController.root, entity).Count, Influence = person.Influence, Monarch = person.Alive != 0 && person.Role == 0, EverMonarch = person.EverMonarch != 0, Portrait = portrait, Dead = person.Alive == 0, Selected = courtPerson == key, Click = () =>
                    {
                        SelectRoyalPerson(key);
                    } });
                }

            courtGraph.Show("王室家谱 · 金线标记曾登基的子代", cards, true);
        }

        internal void Royals() => RoyalRows();
        [Sirenix.OdinInspector.LabelText("王室详情")]
        public UI_GamePanel_RoyalPersonDetails RoyalDetails;
        internal bool royalOverview;
        [Sirenix.OdinInspector.LabelText("王室总览根对象")]
        public RectTransform RoyalOverviewRoot;
        internal void SelectRoyalPerson(ulong id)
        {
            courtPerson = id;
            royalOverview = false;
            sessionController.nextRefresh = 0;
        }

        internal void RefreshRoyalDetails(bool visible)
        {
            if (RoyalDetails != null)
                RoyalDetails.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (RoyalDetails == null || RoyalOverviewRoot == null)
                throw new InvalidOperationException("王室人物详情或王朝事务内容根对象引用缺失。");
            RoyalDetails.BindTabs(value =>
            {
                royalOverview = value;
                sessionController.nextRefresh = 0;
            });
            var primary = RoyalOverviewRoot;
            primary.gameObject.SetActive(royalOverview);
            var person = Sim.Find(sessionController.em, courtPerson);
            if (person == Entity.Null || !sessionController.em.HasComponent<Royal>(person) || sessionController.em.GetComponentData<Royal>(person).Role == 4)
            {
                RoyalDetails.PortraitBinding.Bind(sessionController.em, sessionController.root, 0);
                RoyalDetails.Show(0, "点击肖像查看人物", "选择家谱中的人物，查看亲属、声望与特性。", null, null, null, royalOverview);
                return;
            }

            var p = sessionController.em.GetComponentData<Royal>(person);
            var id = sessionController.em.GetComponentData<Identity>(person);
            var king = CourtOps.Monarch(sessionController.em);
            var court = CourtOps.State(sessionController.em, sessionController.root);
            RoyalDetails.PortraitBinding.Bind(sessionController.em, sessionController.root, id.Id);
            var body = new StringBuilder();
            body.AppendLine("父亲：" + PersonName(RoyalFamilyOps.ParentOfGender(sessionController.em, person, PersonGender.Male)));
            body.AppendLine("母亲：" + PersonName(RoyalFamilyOps.ParentOfGender(sessionController.em, person, PersonGender.Female)));
            body.AppendLine("配偶：" + PersonName(p.Spouse));
            body.AppendLine();
            body.AppendLine("国中声望：" + p.Influence.ToString("0.0") + " / 100");
            body.AppendLine("成长性：" + p.Growth.ToString("0.00"));
            body.AppendLine("野心：" + PublicRoyalAmbition(person));
            body.AppendLine();
            body.AppendLine("特性：");
            bool any = false;
            foreach (var trait in sessionController.em.GetBuffer<TraitEntry>(person))
                if (trait.Revealed != 0)
                {
                    any = true;
                    body.AppendLine(sessionController.Name(trait.Definition) + (trait.Active != 0 ? "（已激活）" : "（尚未激活）"));
                }

            if (!any)
                body.AppendLine("暂无已知特性");
            if (p.Alive == 0)
                body.AppendLine("已逝，无法执行人物操作。");
            if (p.FateUntil > 0 && p.Alive != 0)
                body.AppendLine("知天命：还剩 " + Math.Max(0, p.FateUntil - sessionController.em.GetComponentData<Session>(sessionController.root).Turn) + " 回合");
            bool can = CourtDay && sessionController.em.GetComponentData<Session>(sessionController.root).Paused == 0 && p.Alive != 0;
            bool eligible = CourtOps.Eligible(sessionController.em, king, person);
            ulong key = id.Id;
            RoyalDetails.Show(key, id.Name + " · " + GenderName(p.Gender) + " · " + p.Age + " 岁" + (p.Alive == 0 ? " · 已逝" : p.Role == 0 ? " · 国王" : court.Crown == key ? " · 储君" : ""), body.ToString(), can && eligible && court.Crown != key ? () => ConfirmRoyalDesignation(key) : null, can && eligible ? () => ConfirmRoyalExecution(key) : null, can && RoyalFamilyOps.CanArrange(sessionController.em, sessionController.root, person) ? () => marriageController.OpenMarriagePicker(key) : null, royalOverview, p.Alive != 0 ? () => requestsController.ShowPersonRequests(key) : null);
        }

        internal string PublicRoyalAmbition(Entity person)
        {
            var p = sessionController.em.GetComponentData<Royal>(person);
            if (p.Evidence != 0)
                return "已查获弑君阴谋";
            int tier = IntelOps.Tier(sessionController.em.GetComponentData<GameSettings>(sessionController.root), IntelOps.Known(sessionController.em, sessionController.root));
            if (tier >= 2 && CourtOps.PlotChance(sessionController.em, sessionController.root, CourtOps.Monarch(sessionController.em), person) > 0)
                return tier >= 3 ? "有夺权动机，暂无确凿证据" : "势力值得关注";
            return "未表现出野心";
        }

        internal static string GenderName(PersonGender gender) => gender == PersonGender.Male ? "男" : gender == PersonGender.Female ? "女" : "未知";
        internal void ConfirmRoyalDesignation(ulong id)
        {
            buildingController.ShowBuildingConfirmation("立储：" + PersonName(id), new[] { CourtOps.State(sessionController.em, sessionController.root).Crown == 0 ? "立储后正向影响力增长提高；仍可能存在政治风险。" : "更换储君产生朝局动荡，原储君保留影响力与不满。" }, () => commandsController.Send(CommandKind.DesignateHeir, id));
        }

        internal void ConfirmRoyalExecution(ulong id)
        {
            var person = Sim.Find(sessionController.em, id);
            if (!CourtOps.Alive(sessionController.em, person))
                return;
            var p = sessionController.em.GetComponentData<Royal>(person);
            buildingController.ShowBuildingConfirmation("确认赐死：" + PersonName(id), new[] { p.Evidence != 0 ? "已有确凿证据，政治代价较低。" : "无确凿证据，朝局动荡较重。", CourtOps.State(sessionController.em, sessionController.root).Crown == id ? "储君死亡还会损害继承秩序。" : "此行为不可撤销。" }, () => commandsController.TryQueue(CommandRequests.ExecuteHeir(id)));
        }
    }
}
