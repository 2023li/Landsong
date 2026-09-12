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
    public sealed class UI_GamePanel_PersonRequests : Moyo.Unity.UIViewBase
    {
        internal UI_GamePanel_Building buildingController;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Court courtController;
        internal UI_GamePanel_Expedition expeditionController;
        internal UI_GamePanel_Marriage marriageController;
        internal UI_GamePanel_Portrait portraitController;
        internal IGameUiNavigation navigation;
        internal GameUiSession sessionController;
        internal UI_GamePanel_Soldier soldierController;
        internal UI_GamePanel_WorldInteraction worldController;
        [Sirenix.OdinInspector.LabelText("人物请求面板")]
        public UI_GamePanel_PersonRequestsView PersonRequestsPanel;
        public GameObject PersonRequestsWindow => PersonRequestsPanel != null ? PersonRequestsPanel.gameObject : null;
        public Button PersonRequestsCloseButton => PersonRequestsPanel != null ? PersonRequestsPanel.Close : null;
        public bool PersonRequestsOpen => PersonRequestsWindow != null && PersonRequestsWindow.activeSelf;

        internal ulong requestsPerson;
        internal string requestsSignature;
        internal RectTransform requestsRows;
        internal TMP_Text requestsTitle;
        public void ShowPersonRequests(ulong id)
        {
            if (!navigation.InputPolicy.Capture().CanOpenModal(GameUiInputOwner.PersonRequests))
                return;
            if (!CourtOps.Alive(sessionController.em, Sim.Find(sessionController.em, id)))
                return;
            if (PersonRequestsPanel == null)
                throw new InvalidOperationException("人物请求面板检查器引用缺失。");
            PersonRequestsPanel.ValidateConfiguration();
            requestsTitle = PersonRequestsPanel.Title;
            requestsRows = PersonRequestsPanel.Rows;
            PersonRequestsPanel.Close.onClick.RemoveAllListeners();
            PersonRequestsPanel.Close.onClick.AddListener(ClosePersonRequests);
            worldController.EndBuildingPlacement();
            navigation.EndInventoryDrag();
            worldController.cameraDragging = false;
            requestsPerson = id;
            requestsSignature = null;
            PersonRequestsPanel.gameObject.SetActive(true);
            PersonRequestsPanel.Scroll.verticalNormalizedPosition = 1;
            RefreshPersonRequests();
        }

        public void ClosePersonRequests()
        {
            if (PersonRequestsWindow != null)
                PersonRequestsWindow.SetActive(false);
            sessionController.nextRefresh = 0;
        }

        internal void RefreshPersonRequests()
        {
            if (!PersonRequestsOpen)
                return;
            var person = Sim.Find(sessionController.em, requestsPerson);
            if (!CourtOps.Alive(sessionController.em, person))
            {
                ClosePersonRequests();
                return;
            }

            var list = PersonRequestOps.Pending(sessionController.em, sessionController.root, person);
            bool can = courtController.CourtDay && sessionController.em.GetComponentData<Session>(sessionController.root).Paused == 0;
            int definition = sessionController.em.GetComponentData<Identity>(person).Definition;
            var task = Sim.ValidDefinition(sessionController.em, sessionController.root, definition) ? Sim.Rule(sessionController.em, sessionController.root, definition, RuleKind.SocialTask) : new Rule
            {
                Level = -1
            };
            bool supplies = task.Level >= 0 && InventoryOps.Count(sessionController.em, sessionController.root, task.Target) >= task.Amount;
            bool expedition = FeatureOps.Unlocked(sessionController.em, sessionController.root, "Expedition") && CourtOps.AvailableCaptain(sessionController.em, sessionController.root, person);
            string next = requestsPerson + "/" + can + "/" + supplies + "/" + expedition + "/" + string.Join("|", list.Select(r => $"{r.Kind}:{r.Turn}:{r.Target}:{r.Travelling}"));
            if (next == requestsSignature)
                return;
            if (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasReleasedThisFrame))
                return;
            requestsSignature = next;
            requestsTitle.text = courtController.PersonName(requestsPerson) + "的请求（" + list.Count + "）";
            foreach (Transform child in requestsRows)
                if (child.gameObject != PersonRequestsPanel.TextTemplate.gameObject && child.gameObject != PersonRequestsPanel.ActionTemplate.gameObject)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }

            void Line(string value)
            {
                var line = Instantiate(PersonRequestsPanel.TextTemplate, requestsRows);
                line.gameObject.SetActive(true);
                line.Text.text = value;
                line.Layout.preferredHeight = Mathf.Max(48, line.Text.GetPreferredValues(value, Mathf.Max(300, requestsRows.rect.width - 24), 0).y + 16);
            }

            void ActionButton(string label, Action action)
            {
                var item = Instantiate(PersonRequestsPanel.ActionTemplate, requestsRows);
                item.gameObject.SetActive(true);
                item.Label.text = label;
                item.Button.onClick.RemoveAllListeners();
                item.Button.interactable = action != null;
                if (action != null)
                    item.Button.onClick.AddListener(() => action());
            }

            if (list.Count == 0)
                Line("暂无待处理请求。");
            foreach (var entry in list)
            {
                ulong id = requestsPerson;
                if (entry.Kind == PersonRequestKind.Portrait)
                {
                    Line("丽质初成：可以塑造一次容貌。");
                    ActionButton("塑造容貌", can ? () =>
                    {
                        ClosePersonRequests();
                        portraitController.OpenPortrait(id);
                    } : null);
                }
                else if (entry.Kind == PersonRequestKind.Marriage)
                {
                    Line("赐婚请求 · 第 " + entry.Turn + " 回合\n希望能和" + courtController.PersonName(entry.Target) + "结婚");
                    ActionButton("查看赐婚请求", () =>
                    {
                        ClosePersonRequests();
                        marriageController.ShowMarriage(id);
                    });
                }
                else if (entry.Kind == PersonRequestKind.Expedition)
                {
                    Line("渴望一次远征 · 第 " + entry.Turn + " 回合\n" + (entry.Travelling ? "远征中，存活归来后完成。" : "等待安排：担任远征队长，结束远征并存活归来后完成。"));
                    if (!entry.Travelling)
                    {
                        ActionButton(expedition ? "安排远征" : "暂时无法担任远征队长", can && expedition ? () =>
                        {
                            ClosePersonRequests();
                            expeditionController.expeditionCaptain = id;
                            navigation.OpenPanel(GamePanelId.Expedition);
                        } : null);
                        int turn = entry.Turn;
                        ActionButton("拒绝远征请求", can ? () =>
                        {
                            ClosePersonRequests();
                            commandsController.TryQueue(CommandRequests.RefusePersonRequest(id, turn));
                        } : null);
                    }
                }
                else if (entry.Kind == PersonRequestKind.SocialTask)
                {
                    Line("个人委托\n提交 " + sessionController.Name(task.Target) + " × " + task.Amount + "，好感 +" + task.B);
                    ActionButton(supplies ? "提交委托物品" : "委托物品不足", can && supplies ? () =>
                    {
                        ClosePersonRequests();
                        commandsController.Send(CommandKind.CompleteSocialTask, id);
                    } : null);
                }
            }
        }
    }
}
