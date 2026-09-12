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
    public sealed class UI_GamePanel_Portrait : Moyo.Unity.UIViewBase
    {
        [Sirenix.OdinInspector.LabelText("肖像模板")]
        public UI_GamePanel_PortraitRow PortraitTemplate;
        internal UI_GamePanel_Building buildingController;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Court courtController;
        internal UI_GamePanel_Marriage marriageController;
        internal UI_GamePanel_PersonRequests requestsController;
        internal IGameUiNavigation navigation;
        internal UI_GamePanel_RowRenderer rowsController;
        internal GameUiSession sessionController;
        internal UI_GamePanel_Soldier soldierController;
        internal UI_GamePanel_WorldInteraction worldController;
        [Sirenix.OdinInspector.LabelText("肖像面板")]
        public UI_GamePanel_PortraitCustomization PortraitPanel;
        public GameObject PortraitWindow => PortraitPanel != null ? PortraitPanel.gameObject : null;
        public Button PortraitConfirmButton => PortraitPanel != null ? PortraitPanel.Confirm : null;
        public Button PortraitCloseButton => PortraitPanel != null ? PortraitPanel.Close : null;

        [Sirenix.OdinInspector.LabelText("美貌事件按钮")]
        public Button BeautyEventButton;
        [Sirenix.OdinInspector.LabelText("美貌事件文字")]
        public TMP_Text BeautyEventLabel;
        public bool PortraitOpen => PortraitWindow != null && PortraitWindow.activeSelf;

        internal PortraitDNA portraitDraft;
        internal ulong portraitPerson;
        internal UI_Common_PortraitImageBinding portraitPreview;
        internal float nextPortraitRefresh;
        internal static readonly string[] PortraitPartNames =
        {
            "脸型",
            "耳朵",
            "眼睛",
            "眉毛",
            "鼻子",
            "嘴型",
            "发型",
            "胡须",
            "身体",
            "服装",
            "头饰",
            "面饰",
            "饰品"
        };
        public void ClosePortrait()
        {
            if (PortraitWindow != null)
                PortraitWindow.SetActive(false);
            sessionController.nextRefresh = nextPortraitRefresh = 0;
        }

        internal void RefreshPortraitCustomization()
        {
            if (BeautyEventButton != null && (navigation.IsPanelOpen || PortraitOpen || marriageController.MarriageOpen || requestsController.PersonRequestsOpen || sessionController.intel || navigation.PauseMenu != null && navigation.PauseMenu.IsOpen))
                BeautyEventButton.gameObject.SetActive(false);
            if (PortraitOpen && !PortraitOps.CanCustomize(sessionController.em, sessionController.root, Sim.Find(sessionController.em, portraitPerson)))
                ClosePortrait();
            if (PortraitOpen)
                PortraitConfirmButton.interactable = courtController.CourtDay && sessionController.em.GetComponentData<Session>(sessionController.root).Paused == 0;
            if (Time.unscaledTime < nextPortraitRefresh)
                return;
            nextPortraitRefresh = Time.unscaledTime + .25f;
            ulong first = 0;
            int count = 0;
            using (var people = Sim.OrderedEntities<Royal>(sessionController.em))
                foreach (var person in people)
                    if (PortraitOps.CanCustomize(sessionController.em, sessionController.root, person))
                    {
                        if (first == 0)
                            first = sessionController.em.GetComponentData<Identity>(person).Id;
                        count++;
                    }

            if (BeautyEventButton == null || BeautyEventLabel == null)
                throw new InvalidOperationException("丽质事件 HUD 检查器引用缺失。");
            BeautyEventButton.gameObject.SetActive(count > 0 && !sessionController.intel && !navigation.IsPanelOpen && !PortraitOpen && !marriageController.MarriageOpen && !requestsController.PersonRequestsOpen && (navigation.PauseMenu == null || !navigation.PauseMenu.IsOpen));
            BeautyEventButton.interactable = true;
            BeautyEventLabel.text = "丽质初成：" + courtController.PersonName(first) + "（待塑容 " + count + "）";
            BeautyEventButton.onClick.RemoveAllListeners();
            BeautyEventButton.onClick.AddListener(() => OpenPortrait(first));
        }

        public void OpenPortrait(ulong id)
        {
            if (!navigation.InputPolicy.Capture().CanOpenModal(GameUiInputOwner.Portrait))
                return;
            var person = Sim.Find(sessionController.em, id);
            if (!PortraitOps.CanCustomize(sessionController.em, sessionController.root, person))
                return;
            portraitPerson = id;
            portraitDraft = sessionController.em.GetComponentData<PortraitDNA>(person);
            if (PortraitPanel == null)
                throw new InvalidOperationException("塑容面板检查器引用缺失。");
            PortraitPanel.ValidateConfiguration();
            worldController.EndBuildingPlacement();
            navigation.EndInventoryDrag();
            worldController.cameraDragging = false;
            PortraitPanel.Title.text = "丽质 · 为" + courtController.PersonName(id) + "塑造容貌";
            portraitPreview = PortraitPanel.PreviewBinding;
            var library = sessionController.em.GetComponentData<PortraitLibrary>(sessionController.root).Value;
            var gender = PortraitOps.Gender(sessionController.em, person);
            for (int slot = 0; slot < PortraitOps.Slots; slot++)
            {
                int at = slot;
                var choices = new List<int>();
                if (PortraitOps.Compatible(ref library.Value, 0, (PortraitPartType)at, gender))
                    choices.Add(0);
                for (int i = 0; i < library.Value.Parts.Length; i++)
                {
                    ref var part = ref library.Value.Parts[i];
                    if (part.Type == (PortraitPartType)at && PortraitOps.Compatible(ref library.Value, part.Id, (PortraitPartType)at, gender))
                        choices.Add(part.Id);
                }

                string Label() => PortraitPartNames[at] + "：" + (portraitDraft.Parts[at] == 0 ? "无" : "款式 " + (choices.IndexOf(portraitDraft.Parts[at]) + 1)) + "（点击切换）";
                var button = PortraitPanel.PartButtons[at];
                var label = PortraitPanel.PartLabels[at];
                label.text = Label();
                button.onClick.RemoveAllListeners();
                button.interactable = choices.Count > 1;
                if (choices.Count > 1)
                    button.onClick.AddListener(() =>
                    {
                        int index = choices.IndexOf(portraitDraft.Parts[at]);
                        portraitDraft.Parts[at] = choices[(index + 1) % choices.Count];
                        label.text = Label();
                        UpdatePortraitPreview();
                    });
            }

            for (int channel = 0; channel < 3; channel++)
                for (int component = 0; component < 3; component++)
                {
                    int ch = channel, part = component;
                    var color = ch == 0 ? portraitDraft.Skin : ch == 1 ? portraitDraft.Hair : portraitDraft.Eyes;
                    int value = part == 0 ? color.r : part == 1 ? color.g : color.b;
                    var slider = PortraitPanel.ColorSliders[ch * 3 + part];
                    slider.onValueChanged.RemoveAllListeners();
                    slider.SetValueWithoutNotify(value);
                    slider.onValueChanged.AddListener(v =>
                    {
                        var c = ch == 0 ? portraitDraft.Skin : ch == 1 ? portraitDraft.Hair : portraitDraft.Eyes;
                        if (part == 0)
                            c.r = (byte)v;
                        else if (part == 1)
                            c.g = (byte)v;
                        else
                            c.b = (byte)v;
                        if (ch == 0)
                            portraitDraft.Skin = c;
                        else if (ch == 1)
                            portraitDraft.Hair = c;
                        else
                            portraitDraft.Eyes = c;
                        UpdatePortraitPreview();
                    });
                    slider.wholeNumbers = true;
                }

            PortraitPanel.Confirm.onClick.RemoveAllListeners();
            PortraitPanel.Confirm.onClick.AddListener(() =>
            {
                var dna = portraitDraft;
                ulong target = portraitPerson;
                ClosePortrait();
                commandsController.TryQueue(CommandRequests.CustomizePortrait(target, dna));
            });
            PortraitPanel.Close.onClick.RemoveAllListeners();
            PortraitPanel.Close.onClick.AddListener(ClosePortrait);
            PortraitPanel.gameObject.SetActive(true);
            UpdatePortraitPreview();
        }

        internal void UpdatePortraitPreview() => portraitPreview.Bind(sessionController.em, sessionController.root, portraitPerson, portraitDraft);
        internal GameObject SoldierPortraitRow(Entity unit, string label, Action click = null, bool right = false)
        {
            var id = sessionController.em.GetComponentData<Identity>(unit).Id;
            var binding = rowsController.Item(PortraitTemplate, label, click, right ? navigation.SecondaryRows : navigation.PrimaryRows, key: "portrait:" + id);
            if (binding == null) return null;
            if (!binding.CanRebind) return binding.gameObject;
            var portrait = binding.Portrait;
            if (portrait == null || binding.PortraitBinding == null)
                throw new InvalidOperationException("通用行模板缺少人物肖像引用。");
            portrait.gameObject.SetActive(true);
            binding.Label.margin = new Vector4(68, 4, 8, 4);
            binding.Layout.preferredHeight = Mathf.Max(68, binding.Layout.preferredHeight);
            binding.PortraitBinding.Bind(sessionController.em, sessionController.root, sessionController.em.GetComponentData<Identity>(unit).Id);
            return binding.gameObject;
        }
    }
}
