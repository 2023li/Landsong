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
using System.Collections.Generic;

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
        [LabelText("肖像")] public Image Portrait;
        [LabelText("肖像引用绑定")] public UI_Common_PortraitImageBinding PortraitBinding;
        [LabelText("名称")] public TMP_InputField Name;
        [LabelText("年龄")] public TMP_Text Age;
        [LabelText("属性")] public TMP_Text Stats;
        [LabelText("能力")] public TMP_Text Abilities;
        [LabelText("头盔")] public Button Helmet;
        [LabelText("头盔文字")] public TMP_Text HelmetLabel;
        [LabelText("护甲")] public Button Armor;
        [LabelText("护甲文字")] public TMP_Text ArmorLabel;
        [LabelText("武器")] public Button Weapon;
        [LabelText("武器文字")] public TMP_Text WeaponLabel;
        [LabelText("关闭")] public Button Close;
        [LabelText("能力布局")] public LayoutElement AbilitiesLayout;
        [LabelText("装备选择面板")] public GameObject EquipmentSelectionPanel;
        [LabelText("装备选择标题")] public TMP_Text EquipmentSelectionTitle;
        [LabelText("装备选择列表")] public RectTransform EquipmentSelectionContent;
        [LabelText("装备选择模板")] public Moyo.Unity.TextIconButton EquipmentSelectionTemplate;
        [LabelText("装备选择关闭")] public Button EquipmentSelectionClose;
        public GameObject SoldierDetailsWindow => gameObject;
        public bool SoldierDetailsOpen => gameObject.activeSelf;
        public bool EquipmentSelectionOpen => EquipmentSelectionPanel != null && EquipmentSelectionPanel.activeSelf;
        public TMP_InputField SoldierDetailsName => Name;

        enum EquipmentSlot { Weapon, Helmet, Armor }
        readonly List<Moyo.Unity.TextIconButton> equipmentRows = new List<Moyo.Unity.TextIconButton>();
        EquipmentSlot selectionSlot;
        string selectionSnapshot;
        internal ulong detailsSoldier;

        public void ValidateConfiguration()
        {
            if (Portrait == null || PortraitBinding == null || Name == null || Age == null || Stats == null || Abilities == null || Helmet == null || HelmetLabel == null || Armor == null || ArmorLabel == null || Weapon == null || WeaponLabel == null || Close == null || AbilitiesLayout == null || EquipmentSelectionPanel == null || EquipmentSelectionTitle == null || EquipmentSelectionContent == null || EquipmentSelectionTemplate == null || EquipmentSelectionClose == null)
                throw new InvalidOperationException("士兵详情面板检查器引用不完整。");
            PortraitBinding.ValidateConfiguration();
            if (PortraitBinding.Target != Portrait || !EquipmentSelectionPanel.transform.IsChildOf(transform) || EquipmentSelectionTemplate.transform.parent != EquipmentSelectionContent)
                throw new InvalidOperationException("士兵详情的肖像或装备选择面板层级配置错误。");
        }
        public void OpenSoldierDetails(ulong id)
        {
            if (!inputContext.Policy.Capture().CanOpenModal(GameUiInputOwner.SoldierDetails, allowReopen: true))
                return;
            var unit = WorldQueries.Find(sessionController.em, id);
            if (unit == Entity.Null || !sessionController.em.HasComponent<Soldier>(unit))
                return;
            ValidateConfiguration();
            Name.onEndEdit.RemoveAllListeners();
            Name.onEndEdit.AddListener(value => commandsController.TryQueue(new RenameSoldierRequest { Soldier = detailsSoldier, Name = new FixedString128Bytes(BuildingNaming.SanitizeName(value)) }));
            Close.onClick.RemoveAllListeners();
            Close.onClick.AddListener(CloseSoldierDetails);
            Weapon.onClick.RemoveAllListeners();
            Weapon.onClick.AddListener(() => OpenEquipmentSelection(EquipmentSlot.Weapon));
            Helmet.onClick.RemoveAllListeners();
            Helmet.onClick.AddListener(() => OpenEquipmentSelection(EquipmentSlot.Helmet));
            Armor.onClick.RemoveAllListeners();
            Armor.onClick.AddListener(() => OpenEquipmentSelection(EquipmentSlot.Armor));
            EquipmentSelectionClose.onClick.RemoveAllListeners();
            EquipmentSelectionClose.onClick.AddListener(CloseEquipmentSelection);
            detailsSoldier = id;
            Name.SetTextWithoutNotify(sessionController.em.GetComponentData<Identity>(unit).Name.ToString());
            CloseEquipmentSelection();
            gameObject.SetActive(true);
            RefreshSoldierDetails();
        }

        void OpenEquipmentSelection(EquipmentSlot slot)
        {
            if (!SoldierDetailsOpen || !Name.interactable)
                return;
            selectionSlot = slot;
            selectionSnapshot = null;
            EquipmentSelectionPanel.SetActive(true);
            RefreshEquipmentSelection();
        }

        public void CloseEquipmentSelection()
        {
            if (EquipmentSelectionPanel != null)
                EquipmentSelectionPanel.SetActive(false);
            selectionSnapshot = null;
            foreach (var row in equipmentRows)
                if (row != null)
                {
                    row.gameObject.SetActive(false);
                    Destroy(row.gameObject);
                }
            equipmentRows.Clear();
        }

        void AddEquipmentRow(string text, bool enabled, Action select)
        {
            var row = Instantiate(EquipmentSelectionTemplate, EquipmentSelectionContent);
            row.gameObject.SetActive(true);
            row.SetText(text);
            if (row.TryGetButton(out var button))
            {
                button.interactable = enabled;
                button.onClick.RemoveAllListeners();
                if (enabled && select != null)
                    button.onClick.AddListener(() => select());
            }
            if (row.TryGetIcon(out var icon))
                icon.gameObject.SetActive(false);
            equipmentRows.Add(row);
        }

        void RefreshEquipmentSelection()
        {
            if (!EquipmentSelectionPanel.activeSelf)
                return;
            var unit = WorldQueries.Find(sessionController.em, detailsSoldier);
            if (unit == Entity.Null || !sessionController.em.HasComponent<Soldier>(unit))
            {
                CloseEquipmentSelection();
                return;
            }
            var soldier = sessionController.em.GetComponentData<Soldier>(unit);
            var slotName = selectionSlot == EquipmentSlot.Weapon ? "武器" : selectionSlot == EquipmentSlot.Helmet ? "头盔" : "护甲";
            var snapshot = new StringBuilder().Append((int)selectionSlot).Append(':').Append((int)soldier.Weapon);
            if (selectionSlot == EquipmentSlot.Weapon)
                for (int i = 0; i < ItemDefinitions.Count(sessionController.em, sessionController.root); i++)
                {
                    var item = ItemId.FromIndex(i);
                    if (ItemDefinitions.Get(sessionController.em, sessionController.root, item).Equipment.Weapon != SoldierWeaponKind.None)
                        snapshot.Append(':').Append(InventoryOps.Count(sessionController.em, sessionController.root, item));
                }
            if (selectionSnapshot == snapshot.ToString())
                return;
            selectionSnapshot = snapshot.ToString();
            foreach (var row in equipmentRows)
                if (row != null)
                {
                    row.gameObject.SetActive(false);
                    Destroy(row.gameObject);
                }
            equipmentRows.Clear();
            EquipmentSelectionTitle.text = "选择" + slotName;
            if (selectionSlot != EquipmentSlot.Weapon)
            {
                AddEquipmentRow("暂无可用" + slotName, false, null);
                return;
            }
            AddEquipmentRow(soldier.Weapon == SoldierWeaponKind.None ? "未装备 · 当前" : "卸下武器", soldier.Weapon != SoldierWeaponKind.None,
                () => SelectWeapon(SoldierWeaponKind.None));
            for (int i = 0; i < ItemDefinitions.Count(sessionController.em, sessionController.root); i++)
            {
                var item = ItemId.FromIndex(i);
                ref var definition = ref ItemDefinitions.Get(sessionController.em, sessionController.root, item);
                var weapon = definition.Equipment.Weapon;
                if (weapon == SoldierWeaponKind.None)
                    continue;
                var count = InventoryOps.Count(sessionController.em, sessionController.root, item);
                bool equipped = soldier.Weapon == weapon;
                AddEquipmentRow(definition.Metadata.Name.ToString() + (equipped ? " · 已装备" : " · 库存 " + count), !equipped && count > 0,
                    () => SelectWeapon(weapon));
            }
        }

        void SelectWeapon(SoldierWeaponKind weapon)
        {
            var unit = WorldQueries.Find(sessionController.em, detailsSoldier);
            if (unit == Entity.Null || !sessionController.em.HasComponent<Soldier>(unit) || !Name.interactable)
                return;
            commandsController.TryQueue(new EquipSoldierWeaponRequest { Soldier = detailsSoldier, Weapon = weapon });
            CloseEquipmentSelection();
        }

        public void CloseSoldierDetails()
        {
            detailsSoldier = 0;
            CloseEquipmentSelection();
            if (PortraitBinding != null)
                PortraitBinding.Unbind();
            gameObject.SetActive(false);

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
            PortraitBinding.Bind(sessionController.em, sessionController.root, detailsSoldier);
            SoldierDetailsName.interactable = session.Phase == Phase.Day && sessionControl.Paused == 0 && sessionPersistence.CheckpointPending == 0 && EntityState.Alive(sessionController.em, unit);
            Weapon.interactable = Name.interactable;
            Helmet.interactable = Name.interactable;
            Armor.interactable = Name.interactable;
            var weaponItem = EquipmentOps.ItemForWeapon(sessionController.em, sessionController.root, s.Weapon);
            WeaponLabel.text = "武器\n" + (weaponItem.IsValid ? ItemDefinitions.Get(sessionController.em, sessionController.root, weaponItem).Metadata.Name.ToString() : "未装备");
            HelmetLabel.text = "头盔\n未装备";
            ArmorLabel.text = "护甲\n未装备";
            if (EquipmentSelectionPanel.activeSelf)
            {
                if (!Name.interactable)
                    CloseEquipmentSelection();
                else
                    RefreshEquipmentSelection();
            }
            if (!SoldierDetailsName.isFocused)
                SoldierDetailsName.SetTextWithoutNotify(sessionController.em.GetComponentData<Identity>(unit).Name.ToString());
            Age.text = "年龄：" + (sessionController.em.HasComponent<SoldierPerson>(unit) ? PortraitOps.Age(sessionController.em, unit) + " 岁" : "暂无记录");
            float health = sessionController.em.HasComponent<Health>(unit) ? sessionController.em.GetComponentData<Health>(unit).Current : stats.Health;
            float maximum = sessionController.em.HasComponent<Health>(unit) ? sessionController.em.GetComponentData<Health>(unit).Maximum : stats.Health;
            Stats.text = $"力量：{d.CombatStats.Strength:0.#}    智力：{d.CombatStats.Intelligence:0.#}\n敏捷：{d.CombatStats.Agility:0.#}    血量：{health:0.#}/{maximum:0.#}";
            Abilities.text = $"士兵能力\n{d.Metadata.Name} · Lv.{UnitProgression.Level(d.Growth, s.Experience)}\n攻击：{stats.Damage:0.#} · {(stats.ProjectileSpeed > 0 ? "远程" : "近战")} · 射程 {stats.Range:0.#}\n经验：{s.Experience}\n自动巡逻与攻击\n\n驻地：{sessionController.EntityName(s.Garrison)}\n槽位：{s.Slot}";
        }
    }
}
