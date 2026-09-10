using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public bool IsPanelOpen { get; private set; }
        public Button PanelCloseButton { get; private set; }
        public Button QuestCloseButton { get; private set; }
        TextMeshProUGUI panelTitle;

        void InitializePanelWindows()
        {
            PanelCloseButton = AddPanelHeader(PrimaryRows.parent.parent, "", out panelTitle);
            AddPanelHeader(SecondaryRows.parent.parent, "可用军队", out _);
            RefreshPanelVisibility();
        }

        Button AddPanelHeader(Transform parent, string title, out TextMeshProUGUI label)
        {
            var header = InterfaceWidgets.Rect("Panel header", parent, new Vector2(0, 1), Vector2.one);
            header.pivot = new Vector2(.5f, 1);
            header.sizeDelta = new Vector2(0, 44);
            header.gameObject.AddComponent<Image>().color = new Color(.09f, .13f, .19f, 1);
            label = InterfaceWidgets.Text(title, header, Status.font, 20);
            label.rectTransform.offsetMax = new Vector2(-88, 0);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var close = InterfaceWidgets.Button("关闭", header, Status.font, ClosePanel, 36);
            var rect = (RectTransform)close.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
            rect.sizeDelta = new Vector2(80, 34);
            rect.anchoredPosition = new Vector2(-46, 0);
            close.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            var scroll = parent.GetComponent<ScrollRect>();
            if (scroll != null) scroll.viewport.offsetMax = new Vector2(scroll.viewport.offsetMax.x, -48);
            return close;
        }

        public void ClosePanel()
        {
            if(SoldierDetailsOpen||MarriageOpen||PersonRequestsOpen||PortraitOpen)return;
            if (PauseMenu != null && PauseMenu.IsOpen) return;
            if (BuildingConfirmPanel != null && BuildingConfirmPanel.activeSelf) return;
            // Terminal decisions must remain visible until the player chooses a recovery or ends the dynasty.
            if (Panel == "王朝终局") return;
            RestoreRoyalPrimary();
            if (intel) Send(CommandKind.IntelligenceMode, argument: 0);
            intel = false;
            intelligenceView = null;
            EndInventoryDrag(); EndBuildingPlacement();
            IsPanelOpen = false;
            Panel = "建筑";
            panelHistory.Clear();
            buildingBarOpen = false;
            if (BuildingBar != null) BuildingBar.gameObject.SetActive(false);
            if (TechnologyTree != null) TechnologyTree.gameObject.SetActive(false);
            if (QuestWindow != null) QuestWindow.SetActive(false);
            if (courtGraph != null) courtGraph.gameObject.SetActive(false);
            if (historyTools != null) historyTools.gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            nextRefresh = 0;
            RefreshPanelVisibility();
        }

        void RefreshPanelVisibility()
        {
            if(IsPanelOpen&&BeautyEventButton!=null)BeautyEventButton.gameObject.SetActive(false);
            bool buildingUnlocked = root != Entity.Null && em.Exists(root) && FeatureOps.Unlocked(em, root, "Building");
            bool primary = IsPanelOpen && Panel != "科技" && Panel != "任务" && (Panel != "王室" || royalOverview) && (Panel != "建筑" || !buildingUnlocked);
            PrimaryRows.parent.parent.gameObject.SetActive(primary);
            SecondaryRows.parent.parent.gameObject.SetActive(IsPanelOpen && Panel == "驻军");
            if (panelTitle != null) panelTitle.text = Panel;
            if (PanelCloseButton != null) PanelCloseButton.interactable = Panel != "王朝终局";
        }
    }
}
