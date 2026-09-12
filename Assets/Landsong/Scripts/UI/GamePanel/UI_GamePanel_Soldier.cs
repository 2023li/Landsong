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
    public sealed class UI_GamePanel_Soldier : Moyo.Unity.UIViewBase, IGameSoldierUi
    {
        internal UI_GamePanel_Building buildingController;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Marriage marriageController;
        internal UI_GamePanel_Portrait portraitController;
        internal UI_GamePanel_PersonRequests requestsController;
        internal IGameUiNavigation navigation;
        internal GameUiSession sessionController;
        [Sirenix.OdinInspector.LabelText("士兵详情面板")]
        public UI_GamePanel_SoldierDetails SoldierDetailsPanel;
        public GameObject SoldierDetailsWindow => SoldierDetailsPanel != null ? SoldierDetailsPanel.gameObject : null;
        public bool SoldierDetailsOpen => SoldierDetailsWindow != null && SoldierDetailsWindow.activeSelf;
        public TMP_InputField SoldierDetailsName => SoldierDetailsPanel != null ? SoldierDetailsPanel.Name : null;

        internal TMP_Text soldierDetailsAge;
        internal TMP_Text soldierDetailsStats;
        internal TMP_Text soldierDetailsAbilities;
        internal ulong detailsSoldier;
        internal void RefreshBuildingGarrison(Entity site)
        {
            var card = buildingController.BuildingCard;
            var block = card.Block<UI_GamePanel_BuildingDetails_Block_驻军>();
            int capacity = sessionController.em.GetComponentData<BuildingStats>(site).Garrison;
            ulong home = sessionController.em.GetComponentData<Identity>(site).Id;
            var slots = new List<UI_GamePanel_BuildingDetails_Block_驻军.SlotModel>(capacity);
            for (int slot = 1; slot <= capacity; slot++)
            {
                var unit = MilitaryOps.AtSlot(sessionController.em, home, slot);
                if (unit == Entity.Null)
                    slots.Add(new UI_GamePanel_BuildingDetails_Block_驻军.SlotModel(0, "", false));
                else
                {
                    var identity = sessionController.em.GetComponentData<Identity>(unit);
                    slots.Add(new UI_GamePanel_BuildingDetails_Block_驻军.SlotModel(identity.Id, identity.Name.ToString(), Sim.Alive(sessionController.em, unit)));
                }
            }

            block.Refresh(home, slots, sessionController.em, sessionController.root,
                () => navigation.OpenPanel(GamePanelId.Garrison), id =>
                {
                    if (id == 0)
                        navigation.OpenPanel(GamePanelId.Garrison);
                    else
                        OpenSoldierDetails(id);
                });
        }

        public void OpenSoldierDetails(ulong id)
        {
            if (!navigation.InputPolicy.Capture().CanOpenModal(GameUiInputOwner.SoldierDetails, allowReopen: true))
                return;
            var unit = Sim.Find(sessionController.em, id);
            if (unit == Entity.Null || !sessionController.em.HasComponent<Soldier>(unit))
                return;
            if (SoldierDetailsPanel == null)
                throw new System.InvalidOperationException("士兵详情面板检查器引用缺失。");
            SoldierDetailsPanel.ValidateConfiguration();
            SoldierDetailsPanel.Name.onEndEdit.RemoveAllListeners();
            SoldierDetailsPanel.Name.onEndEdit.AddListener(value => commandsController.TryQueue(CommandRequests.RenameSoldier(detailsSoldier, value)));
            SoldierDetailsPanel.Close.onClick.RemoveAllListeners();
            SoldierDetailsPanel.Close.onClick.AddListener(CloseSoldierDetails);
            soldierDetailsAge = SoldierDetailsPanel.Age;
            soldierDetailsStats = SoldierDetailsPanel.Stats;
            soldierDetailsAbilities = SoldierDetailsPanel.Abilities;
            detailsSoldier = id;
            SoldierDetailsName.SetTextWithoutNotify(sessionController.em.GetComponentData<Identity>(unit).Name.ToString());
            SoldierDetailsPanel.gameObject.SetActive(true);
            RefreshSoldierDetails();
        }

        public void CloseSoldierDetails()
        {
            detailsSoldier = 0;
            if (SoldierDetailsPanel != null)
            {
                if (SoldierDetailsPanel.PortraitBinding != null)
                    SoldierDetailsPanel.PortraitBinding.Unbind();
                SoldierDetailsPanel.gameObject.SetActive(false);
            }
            if (sessionController != null)
                sessionController.nextRefresh = 0;
        }

        internal void RefreshSoldierDetails()
        {
            if (!SoldierDetailsOpen)
                return;
            var unit = Sim.Find(sessionController.em, detailsSoldier);
            if (unit == Entity.Null || !sessionController.em.HasComponent<Soldier>(unit))
            {
                CloseSoldierDetails();
                return;
            }

            var s = sessionController.em.GetComponentData<Soldier>(unit);
            var stats = MilitaryOps.SoldierStats(sessionController.em, sessionController.root, unit);
            var d = Sim.Definition(sessionController.em, sessionController.root, sessionController.em.GetComponentData<Identity>(unit).Definition);
            var session = sessionController.em.GetComponentData<Session>(sessionController.root);
            SoldierDetailsPanel.PortraitBinding.Bind(sessionController.em, sessionController.root, detailsSoldier);
            SoldierDetailsName.interactable = session.Phase == Phase.Day && session.Paused == 0 && session.CheckpointPending == 0 && Sim.Alive(sessionController.em, unit);
            if (!SoldierDetailsName.isFocused)
                SoldierDetailsName.SetTextWithoutNotify(sessionController.em.GetComponentData<Identity>(unit).Name.ToString());
            soldierDetailsAge.text = "年龄：" + (sessionController.em.HasComponent<SoldierPerson>(unit) ? PortraitOps.Age(sessionController.em, unit) + " 岁" : "暂无记录");
            float health = sessionController.em.HasComponent<Health>(unit) ? sessionController.em.GetComponentData<Health>(unit).Current : stats.Health;
            float maximum = sessionController.em.HasComponent<Health>(unit) ? sessionController.em.GetComponentData<Health>(unit).Maximum : stats.Health;
            soldierDetailsStats.text = $"力量：—    知识：—\n敏捷：{stats.Speed:0.#}    血量：{health:0.#}/{maximum:0.#}";
            soldierDetailsAbilities.text = $"士兵能力\n{d.Name} · Lv.{MilitaryOps.Level(d.SoldierGrowth, s.Experience)}\n攻击：{stats.Damage:0.#}\n经验：{s.Experience}\n自动巡逻与攻击\n\n驻地：{sessionController.EntityName(s.Garrison)}\n槽位：{s.Slot}";
        }
    }
}
