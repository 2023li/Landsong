using Landsong.ECS.Definitions;
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
    public sealed class UI_GamePanel_Soldier : Moyo.Unity.UIViewBase, IGameSoldierUi
    {
        internal GameUiInputContext inputContext;
        internal GameUiRefreshScheduler refresh;
        internal UI_GamePanel_BuildingActionBar buildingController;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Marriage marriageController;
        internal UI_GamePanel_Portrait portraitController;
        internal UI_GamePanel_PersonRequests requestsController;
        internal IGameUiNavigation navigation;
        internal GameUiSessionHandle sessionController;
        [Sirenix.OdinInspector.LabelText("士兵详情面板")]
        public UI_GamePanel_SoldierDetails SoldierDetailsPanel;
        public GameObject SoldierDetailsWindow => SoldierDetailsPanel != null ? SoldierDetailsPanel.gameObject : null;
        public bool SoldierDetailsOpen => SoldierDetailsWindow != null && SoldierDetailsWindow.activeSelf;
        public TMP_InputField SoldierDetailsName => SoldierDetailsPanel != null ? SoldierDetailsPanel.Name : null;

        internal TMP_Text soldierDetailsAge;
        internal TMP_Text soldierDetailsStats;
        internal TMP_Text soldierDetailsAbilities;
        internal ulong detailsSoldier;
        public void OpenSoldierDetails(ulong id)
        {
            if (!inputContext.Policy.Capture().CanOpenModal(GameUiInputOwner.SoldierDetails, allowReopen: true))
                return;
            var unit = WorldQueries.Find(sessionController.em, id);
            if (unit == Entity.Null || !sessionController.em.HasComponent<Soldier>(unit))
                return;
            if (SoldierDetailsPanel == null)
                throw new System.InvalidOperationException("士兵详情面板检查器引用缺失。");
            SoldierDetailsPanel.ValidateConfiguration();
            SoldierDetailsPanel.Name.onEndEdit.RemoveAllListeners();
            SoldierDetailsPanel.Name.onEndEdit.AddListener(value => commandsController.TryQueue(new RenameSoldierRequest { Soldier = detailsSoldier, Name = new Unity.Collections.FixedString128Bytes(BuildingNaming.SanitizeName(value)) }));
            SoldierDetailsPanel.Close.onClick.RemoveAllListeners();
            SoldierDetailsPanel.Close.onClick.AddListener(CloseSoldierDetails);
            SoldierDetailsPanel.Weapon.onClick.RemoveAllListeners();
            SoldierDetailsPanel.Weapon.onClick.AddListener(() =>
            {
                var selected = WorldQueries.Find(sessionController.em, detailsSoldier);
                if (selected == Entity.Null || !sessionController.em.HasComponent<Soldier>(selected)) return;
                var current = sessionController.em.GetComponentData<Soldier>(selected).Weapon;
                var choices = new[] { SoldierWeaponKind.None, SoldierWeaponKind.Club, SoldierWeaponKind.Sword, SoldierWeaponKind.Bow };
                var index = Array.IndexOf(choices, current);
                SoldierWeaponKind next = SoldierWeaponKind.None;
                for (int offset = 1; offset < choices.Length; offset++)
                {
                    var candidate = choices[(index + offset + choices.Length) % choices.Length];
                    var item = EquipmentOps.ItemForWeapon(sessionController.em, sessionController.root, candidate);
                    if (candidate == SoldierWeaponKind.None || item.IsValid && InventoryOps.Count(sessionController.em, sessionController.root, item) > 0)
                    {
                        next = candidate;
                        break;
                    }
                }
                commandsController.TryQueue(new EquipSoldierWeaponRequest { Soldier = detailsSoldier, Weapon = next });
            });
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
                refresh.NextPanel = 0;
        }

        internal void RefreshSoldierDetails()
        {
            if (!SoldierDetailsOpen)
                return;
            var unit = WorldQueries.Find(sessionController.em, detailsSoldier);
            if (unit == Entity.Null || !sessionController.em.HasComponent<Soldier>(unit))
            {
                CloseSoldierDetails();
                return;
            }

            var s = sessionController.em.GetComponentData<Soldier>(unit);
            var stats = SoldierOps.SoldierStats(sessionController.em, sessionController.root, unit);
            ref var d = ref SoldierDefinitions.Get(sessionController.em, sessionController.root, sessionController.em.GetComponentData<SoldierDefinitionRef>(unit).Definition);
            var session = sessionController.em.GetComponentData<Session>(sessionController.root);
            SimulationControl sessionControl = sessionController.em.GetComponentData<SimulationControl>(sessionController.root);
            PersistenceGate sessionPersistence = sessionController.em.GetComponentData<PersistenceGate>(sessionController.root);
            SoldierDetailsPanel.PortraitBinding.Bind(sessionController.em, sessionController.root, detailsSoldier);
            SoldierDetailsName.interactable = session.Phase == Phase.Day && sessionControl.Paused == 0 && sessionPersistence.CheckpointPending == 0 && EntityState.Alive(sessionController.em, unit);
            SoldierDetailsPanel.Weapon.interactable = SoldierDetailsName.interactable;
            SoldierDetailsPanel.WeaponLabel.text = s.Weapon == SoldierWeaponKind.Sword ? "武器\n铁剑"
                : s.Weapon == SoldierWeaponKind.Bow ? "武器\n木弓"
                : s.Weapon == SoldierWeaponKind.Club ? "武器\n木棒" : "武器\n未装备";
            if (!SoldierDetailsName.isFocused)
                SoldierDetailsName.SetTextWithoutNotify(sessionController.em.GetComponentData<Identity>(unit).Name.ToString());
            soldierDetailsAge.text = "年龄：" + (sessionController.em.HasComponent<SoldierPerson>(unit) ? PortraitOps.Age(sessionController.em, unit) + " 岁" : "暂无记录");
            float health = sessionController.em.HasComponent<Health>(unit) ? sessionController.em.GetComponentData<Health>(unit).Current : stats.Health;
            float maximum = sessionController.em.HasComponent<Health>(unit) ? sessionController.em.GetComponentData<Health>(unit).Maximum : stats.Health;
            soldierDetailsStats.text = $"力量：{d.CombatStats.Strength:0.#}    智力：{d.CombatStats.Intelligence:0.#}\n敏捷：{d.CombatStats.Agility:0.#}    血量：{health:0.#}/{maximum:0.#}";
            soldierDetailsAbilities.text = $"士兵能力\n{d.Metadata.Name} · Lv.{UnitProgression.Level(d.Growth, s.Experience)}\n攻击：{stats.Damage:0.#} · {(s.Weapon == SoldierWeaponKind.Bow ? "远程" : "近战")} · 射程 {stats.Range:0.#}\n经验：{s.Experience}\n自动巡逻与攻击\n\n驻地：{sessionController.EntityName(s.Garrison)}\n槽位：{s.Slot}";
        }
    }
}
