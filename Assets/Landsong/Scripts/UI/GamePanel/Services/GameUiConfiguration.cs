using Moyo.Unity;
using System.Threading.Tasks;
using System.Collections.Generic;
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
    internal static class GameUiConfiguration
    {
        internal static void Validate(UI_GamePanel view)
        {
            static void Need(UnityEngine.Object value, string name)
            {
                if (value == null)
                    throw new InvalidOperationException("ECS 游戏 UI 检查器引用缺失：" + name);
            }

            Need(view.HudRoot, nameof(view.HudRoot));
            Need(view.BuildingRoot, nameof(view.BuildingRoot));
            Need(view.FeatureRoot, nameof(view.FeatureRoot));
            Need(view.ModalRoot, nameof(view.ModalRoot));
            Need(view.PauseMenu, nameof(view.PauseMenu));
            view.PauseMenu.ValidateConfiguration();
            if (!view.PauseMenu.gameObject.activeSelf)
                throw new InvalidOperationException("暂停弹窗根对象必须保持激活；显示状态由遮罩图片和 CanvasGroup 控制。");
            Need(view.royalFounding, nameof(view.royalFounding));
            Need(view.InterfaceGroup, nameof(view.InterfaceGroup));
            Need(view.InterfaceScaler, nameof(view.InterfaceScaler));
            Need(view.worldController.Camera, nameof(view.worldController.Camera));
            Need(view.buildingController.BuildingBar, nameof(view.buildingController.BuildingBar));
            Need(view.buildingDetailsController, nameof(view.buildingDetailsController));
            if (view.buildingController.DetailsPanel != view.buildingDetailsController)
                throw new InvalidOperationException("建筑操作条与游戏根必须绑定同一个建筑详情面板。");
            Need(view.technologyController.TechnologyTree, nameof(view.technologyController.TechnologyTree));
            Need(view.courtController.GraphRoot, nameof(view.courtController.GraphRoot));
            Need(view.courtController.RoyalDetails, nameof(view.courtController.RoyalDetails));
            Need(view.questController, nameof(view.questController));
            Need(view.questController.QuestTracking, nameof(view.questController.QuestTracking));
            view.questController.QuestTracking.ValidateConfiguration();
            Need(view.soldierController, nameof(view.soldierController));
            Need(view.marriageController.MarriagePanel, nameof(view.marriageController.MarriagePanel));
            Need(view.requestsController.PersonRequestsPanel, nameof(view.requestsController.PersonRequestsPanel));
            Need(view.portraitController.PortraitPanel, nameof(view.portraitController.PortraitPanel));
            Need(view.technologyController.ResearchHud, nameof(view.technologyController.ResearchHud));
            Need(view.hudController.BattleHud, nameof(view.hudController.BattleHud));
            Need(view.hudController.NightHud, nameof(view.hudController.NightHud));
            Need(view.navigationPanel, nameof(view.navigationPanel));
            Need(view.worldController.WorldPresentation, nameof(view.worldController.WorldPresentation));
            Need(view.worldController.WorldPresentation.OverlayMesh, nameof(view.worldController.WorldPresentation.OverlayMesh));
            Need(view.worldController.WorldPresentation.OverlayMaterial, nameof(view.worldController.WorldPresentation.OverlayMaterial));
            Need(view.historyController, nameof(view.historyController));
            view.historyController.ValidateConfiguration();
            Need(view.hudController.AdvanceLabel, nameof(view.hudController.AdvanceLabel));
            Need(view.hudController.MessageButton, nameof(view.hudController.MessageButton));
            Need(view.hudController.PauseButton, nameof(view.hudController.PauseButton));
            Need(view.BuildingFeatureButton, nameof(view.BuildingFeatureButton));
            Need(view.InventoryFeatureButton, nameof(view.InventoryFeatureButton));
            Need(view.ExpeditionFeatureButton, nameof(view.ExpeditionFeatureButton));
            Need(view.hudController.IntelligenceButtonLabel, nameof(view.hudController.IntelligenceButtonLabel));
            Need(view.marriageController.MarriageEventButton, nameof(view.marriageController.MarriageEventButton));
            Need(view.marriageController.MarriageEventLabel, nameof(view.marriageController.MarriageEventLabel));
            Need(view.buildingController.BuildingConfirmTitle, nameof(view.buildingController.BuildingConfirmTitle));
            Need(view.buildingController.BuildingConfirmGroup, nameof(view.buildingController.BuildingConfirmGroup));
            Need(view.buildingController.CropSelectionPanel, nameof(view.buildingController.CropSelectionPanel));
            view.buildingController.CropSelectionPanel.ValidateConfiguration();
            view.buildingController.BuildingBar.ValidateConfiguration();
            view.technologyController.TechnologyTree.ValidateConfiguration();
            view.courtController.ValidateGraphConfiguration();
            view.talentController.CourtGraph.ValidateConfiguration();
            view.policyController.CourtGraph.ValidateConfiguration();
            view.courtController.RoyalDetails.ValidateConfiguration();
            view.royalFounding.ValidateConfiguration();
            view.questController.ValidateConfiguration();
            view.soldierController.ValidateConfiguration();
            view.marriageController.MarriagePanel.ValidateConfiguration();
            view.requestsController.PersonRequestsPanel.ValidateConfiguration();
            view.portraitController.PortraitPanel.ValidateConfiguration();
            view.technologyController.ResearchHud.ValidateConfiguration();
            view.hudController.BattleHud.ValidateConfiguration();
            view.hudController.ValidateHeroSelectionConfiguration();
            view.hudController.NightHud.ValidateConfiguration();
            view.navigationPanel.ValidateConfiguration();
            view.buildingDetailsController.Block<UI_GamePanel_BuildingDetails_Block_基础产出>().ValidateConfiguration();
            view.buildingDetailsController.Block<UI_GamePanel_BuildingDetails_Block_种植>().ValidateConfiguration();
            view.questController.QuantityTemplate.ValidateConfiguration();
            if (view.FeaturePanels == null || view.FeaturePanels.Length == 0 || view.FeaturePanels.Any(p => p == null) || view.FeaturePanels.GroupBy(p => p.PanelId).Any(g => g.Key == GamePanelId.None || g.Count() != 1))
                throw new InvalidOperationException("FeaturePanels 必须检查器绑定且 PanelId 唯一。");
            if (view.FeaturePanels.Any(panel => panel.PanelId == GamePanelId.Building))
                throw new InvalidOperationException("建筑目的地由建造目录栏承载，不得配置为通用功能面板。");
            foreach (var panel in view.FeaturePanels)
                if (panel.transform.parent != view.FeatureRoot)
                    throw new InvalidOperationException(panel.name + " 必须是功能面板根对象的直接子对象。");
            if (view.buildingDetailsController.transform.parent != view.FeatureRoot)
                throw new InvalidOperationException("建筑详情必须是功能面板根对象的直接子对象。");
            foreach (var modal in new Component[]
            {
                view.PauseMenu,
                view.royalFounding,
                view.soldierController,
                view.marriageController.MarriagePanel,
                view.requestsController.PersonRequestsPanel,
                view.portraitController.PortraitPanel,
                view.buildingController.CropSelectionPanel
            })
                if (!modal.transform.IsChildOf(view.ModalRoot))
                    throw new InvalidOperationException(modal.name + " 必须位于模态面板根对象下。");
        }
    }
}
