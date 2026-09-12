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
    public sealed class UI_GamePanel_Marriage : Moyo.Unity.UIViewBase
    {
        internal UI_GamePanel_Building buildingController;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Court courtController;
        internal UI_GamePanel_Portrait portraitController;
        internal UI_GamePanel_PersonRequests requestsController;
        internal IGameUiNavigation navigation;
        internal GameUiSession sessionController;
        internal UI_GamePanel_Soldier soldierController;
        internal UI_GamePanel_WorldInteraction worldController;
        [Sirenix.OdinInspector.LabelText("婚姻面板")]
        public UI_GamePanel_RoyalMarriage MarriagePanel;
        public GameObject MarriageWindow => MarriagePanel != null ? MarriagePanel.gameObject : null;

        [Sirenix.OdinInspector.LabelText("婚姻事件按钮")]
        public Button MarriageEventButton;
        [Sirenix.OdinInspector.LabelText("婚姻事件文字")]
        public TMP_Text MarriageEventLabel;
        public Button MarriageApproveButton => MarriagePanel.Approve;
        public Button MarriageRefuseButton => MarriagePanel.Refuse;
        public Button MarriageCloseButton => MarriagePanel.Close;
        public bool MarriageOpen => MarriageWindow != null && MarriageWindow.activeSelf;

        internal ulong marriagePerson;
        internal ulong marriageMate;
        internal ulong marriageKing;
        internal int marriageTurn;
        internal float nextMarriageRefresh;
        internal bool marriageArranging;
        internal string marriageCandidatesSignature;
        internal void RefreshMarriageEvents()
        {
            if (MarriageOpen)
            {
                var person = Sim.Find(sessionController.em, marriagePerson);
                if (marriageArranging)
                    RefreshMarriagePicker(person);
                else if (!RoyalFamilyOps.RequestValid(sessionController.em, sessionController.root, person))
                    CloseMarriage();
                else
                {
                    var p = sessionController.em.GetComponentData<Royal>(person);
                    if (p.RequestedSpouse != marriageMate || p.MarriageRequestTurn != marriageTurn || p.MarriageRequestMonarch != marriageKing)
                        CloseMarriage();
                }

                if (MarriageOpen)
                {
                    bool enabled = courtController.CourtDay && !sessionController.intel && sessionController.em.GetComponentData<Session>(sessionController.root).Paused == 0;
                    MarriageApproveButton.interactable = enabled && (!marriageArranging || marriageMate != 0);
                    MarriageRefuseButton.interactable = marriageArranging || enabled;
                }
            }

            if (Time.unscaledTime < nextMarriageRefresh)
                return;
            nextMarriageRefresh = Time.unscaledTime + .25f;
            ulong first = 0;
            int count = 0;
            using (var all = Sim.OrderedEntities<Royal>(sessionController.em))
                foreach (var e in all)
                    if (RoyalFamilyOps.RequestValid(sessionController.em, sessionController.root, e))
                    {
                        if (first == 0)
                            first = sessionController.em.GetComponentData<Identity>(e).Id;
                        count++;
                    }

            if (MarriageEventButton == null || MarriageEventLabel == null)
                throw new System.InvalidOperationException("赐婚请求 HUD 检查器引用缺失。");
            MarriageEventButton.gameObject.SetActive(count > 0 && !sessionController.intel && (navigation.PauseMenu == null || !navigation.PauseMenu.IsOpen));
            if (count == 0)
                return;
            var pfirst = sessionController.em.GetComponentData<Royal>(Sim.Find(sessionController.em, first));
            MarriageEventLabel.text = courtController.PersonName(first) + "希望能和" + courtController.PersonName(pfirst.RequestedSpouse) + "结婚" + (count > 1 ? "（待办 " + count + "）" : "");
            MarriageEventButton.onClick.RemoveAllListeners();
            MarriageEventButton.onClick.AddListener(() => ShowMarriage(first));
            MarriageEventButton.interactable = !MarriageOpen;
        }

        public void CloseMarriage()
        {
            if (MarriageWindow != null)
                MarriageWindow.SetActive(false);
            nextMarriageRefresh = 0;
            sessionController.nextRefresh = 0;
        }

        public void ShowMarriage(ulong id)
        {
            if (!navigation.InputPolicy.Capture().CanOpenModal(GameUiInputOwner.Marriage))
                return;
            var person = Sim.Find(sessionController.em, id);
            if (!RoyalFamilyOps.RequestValid(sessionController.em, sessionController.root, person))
                return;
            marriageArranging = false;
            var p = sessionController.em.GetComponentData<Royal>(person);
            marriagePerson = id;
            marriageMate = p.RequestedSpouse;
            marriageKing = p.MarriageRequestMonarch;
            marriageTurn = p.MarriageRequestTurn;
            if (MarriagePanel == null)
                throw new System.InvalidOperationException("赐婚面板检查器引用缺失。");
            MarriagePanel.ValidateConfiguration();
            worldController.EndBuildingPlacement();
            navigation.EndInventoryDrag();
            worldController.cameraDragging = false;
            MarriagePanel.Title.text = courtController.PersonName(id) + "希望能和" + courtController.PersonName(marriageMate) + "结婚";
            MarriagePanel.CandidateList.SetActive(false);
            MarriagePanel.Mate.gameObject.SetActive(true);
            MarriageDetails(person, MarriagePanel.Person);
            MarriageDetails(Sim.Find(sessionController.em, marriageMate), MarriagePanel.Mate);
            MarriagePanel.Hint.text = "拒绝可能让请求人记恨，增加弑君或夺位风险。";
            BindMarriageButton(MarriagePanel.Approve, MarriagePanel.ApproveLabel, "同意赐婚", () => DecideMarriage(1));
            BindMarriageButton(MarriagePanel.Refuse, MarriagePanel.RefuseLabel, "不同意", () => DecideMarriage(0));
            BindMarriageButton(MarriagePanel.Close, MarriagePanel.CloseLabel, "稍后处理", CloseMarriage);
            MarriagePanel.gameObject.SetActive(true);
        }

        internal void DecideMarriage(int option)
        {
            var id = marriagePerson;
            var mate = marriageMate;
            var turn = marriageTurn;
            CloseMarriage();
            commandsController.TryQueue(CommandRequests.ResolveMarriage(id, mate, turn, option));
        }

        internal void MarriageDetails(Entity person, UI_GamePanel_RoyalPersonSummary view)
        {
            var id = sessionController.em.GetComponentData<Identity>(person);
            var p = sessionController.em.GetComponentData<Royal>(person);
            view.PortraitBinding.Bind(sessionController.em, sessionController.root, id.Id);
            var lines = new List<string>
            {
                id.Name.ToString(),
                UI_GamePanel_Court.GenderName(p.Gender) + " · " + p.Age + " 岁 · " + (p.Role == 0 ? "国王" : p.Role == 4 ? "交际人物" : "王室成员"),
                "影响力 " + p.Influence.ToString("0.0") + " / 100 · 成长性 " + p.Growth.ToString("0.00"),
                "父亲：" + courtController.PersonName(RoyalFamilyOps.ParentOfGender(sessionController.em, person, PersonGender.Male)),
                "母亲：" + courtController.PersonName(RoyalFamilyOps.ParentOfGender(sessionController.em, person, PersonGender.Female)),
                "配偶：" + courtController.PersonName(p.Spouse)
            };
            if (sessionController.em.HasComponent<Talent>(person))
            {
                var t = sessionController.em.GetComponentData<Talent>(person);
                lines.Add("人才等级 " + t.Level + " · 经验 " + t.Experience);
                lines.Add(t.Slot >= 0 ? "岗位：" + sessionController.Name(t.Slot) + " · " + (t.Paid != 0 ? "已付薪" : "未付薪") : "当前未任职");
                lines.Add("与国王好感 " + p.Affection + " / 100");
            }

            bool traits = false;
            foreach (var trait in sessionController.em.GetBuffer<TraitEntry>(person))
                if (trait.Revealed != 0)
                {
                    traits = true;
                    lines.Add("特性：" + sessionController.Name(trait.Definition) + (trait.Active != 0 ? "（已激活）" : "（尚未激活）"));
                }

            if (!traits)
                lines.Add("暂无已知特性");
            if (p.FateUntil > 0)
                lines.Add("知天命：自然寿限还剩 " + System.Math.Max(0, p.FateUntil - sessionController.em.GetComponentData<Session>(sessionController.root).Turn) + " 回合");
            view.Details.text = string.Join("\n", lines);
        }

        internal static void BindMarriageButton(Button button, TMP_Text text, string label, System.Action action)
        {
            text.text = label;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
        }

        public void OpenMarriagePicker(ulong id)
        {
            if (!navigation.InputPolicy.Capture().CanOpenModal(GameUiInputOwner.Marriage))
                return;
            var person = Sim.Find(sessionController.em, id);
            if (!courtController.CourtDay || sessionController.em.GetComponentData<Session>(sessionController.root).Paused != 0 || !RoyalFamilyOps.CanArrange(sessionController.em, sessionController.root, person))
                return;
            commandsController.Send(CommandKind.PrepareMarriage, id);
            marriageArranging = true;
            marriagePerson = id;
            marriageMate = 0;
            marriageKing = sessionController.em.GetComponentData<Identity>(CourtOps.Monarch(sessionController.em)).Id;
            marriageCandidatesSignature = null;
            BuildMarriagePicker();
        }

        internal void RefreshMarriagePicker(Entity person)
        {
            var king = CourtOps.Monarch(sessionController.em);
            if (!RoyalFamilyOps.CanArrange(sessionController.em, sessionController.root, person) || king == Entity.Null || sessionController.em.GetComponentData<Identity>(king).Id != marriageKing)
            {
                CloseMarriage();
                return;
            }

            var candidates = RoyalFamilyOps.Candidates(sessionController.em, sessionController.root, person);
            var signature = string.Join(",", candidates.Select(e => sessionController.em.GetComponentData<Identity>(e).Id));
            if (marriageMate != 0 && !candidates.Contains(Sim.Find(sessionController.em, marriageMate)))
            {
                marriageMate = 0;
                BuildMarriagePicker();
            }

            if (marriageMate == 0 && signature != marriageCandidatesSignature)
            {
                marriageCandidatesSignature = signature;
                BuildMarriagePicker();
            }
        }

        internal void BuildMarriagePicker()
        {
            if (MarriagePanel == null)
                throw new System.InvalidOperationException("赐婚面板检查器引用缺失。");
            MarriagePanel.ValidateConfiguration();
            worldController.EndBuildingPlacement();
            navigation.EndInventoryDrag();
            worldController.cameraDragging = false;
            MarriagePanel.Title.text = "为 " + courtController.PersonName(marriagePerson) + " 选择配偶";
            var person = Sim.Find(sessionController.em, marriagePerson);
            MarriageDetails(person, MarriagePanel.Person);
            MarriagePanel.Mate.gameObject.SetActive(marriageMate != 0);
            MarriagePanel.CandidateList.SetActive(marriageMate == 0);
            if (marriageMate != 0)
                MarriageDetails(Sim.Find(sessionController.em, marriageMate), MarriagePanel.Mate);
            else
            {
                foreach (Transform child in MarriagePanel.CandidateRows)
                    if (child.gameObject != MarriagePanel.CandidateTemplate.gameObject)
                        Destroy(child.gameObject);
                var candidates = RoyalFamilyOps.Candidates(sessionController.em, sessionController.root, person);
                foreach (var e in candidates)
                {
                    var identity = sessionController.em.GetComponentData<Identity>(e);
                    var p = sessionController.em.GetComponentData<Royal>(e);
                    ulong key = identity.Id;
                    var candidate = Instantiate(MarriagePanel.CandidateTemplate, MarriagePanel.CandidateRows);
                    candidate.gameObject.SetActive(true);
                    candidate.Label.text = identity.Name + " · " + UI_GamePanel_Court.GenderName(p.Gender) + " · " + p.Age + " 岁" + (sessionController.em.HasComponent<Talent>(e) ? " · 人才" : "");
                    candidate.Select.onClick.RemoveAllListeners();
                    candidate.Select.onClick.AddListener(() =>
                    {
                        marriageMate = key;
                        BuildMarriagePicker();
                    });
                }

                if (candidates.Count == 0)
                {
                    var candidate = Instantiate(MarriagePanel.CandidateTemplate, MarriagePanel.CandidateRows);
                    candidate.gameObject.SetActive(true);
                    candidate.Label.text = "正在准备可选配偶…";
                    candidate.Select.interactable = false;
                }
            }

            MarriagePanel.Hint.text = "选中配偶可查看详情；确认赐婚后，原有请求随婚姻结束。";
            BindMarriageButton(MarriagePanel.Approve, MarriagePanel.ApproveLabel, "确认赐婚", () =>
            {
                ulong id = marriagePerson, mate = marriageMate;
                CloseMarriage();
                commandsController.Send(CommandKind.ArrangeMarriage, id, mate);
            });
            MarriagePanel.Approve.interactable = marriageMate != 0;
            BindMarriageButton(MarriagePanel.Refuse, MarriagePanel.RefuseLabel, "重新选择", () =>
            {
                marriageMate = 0;
                BuildMarriagePicker();
            });
            BindMarriageButton(MarriagePanel.Close, MarriagePanel.CloseLabel, "取消", CloseMarriage);
            MarriagePanel.gameObject.SetActive(true);
        }
    }
}
