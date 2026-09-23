using System;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingDetails_Block_种植 : UI_GamePanel_BuildingDetails_Block
    {
        [LabelText("作物填充"), Required]
        public Image Fill;
        [LabelText("作物图标"), Required]
        public Image Icon;
        [LabelText("作物文字"), Required]
        public TMP_Text Label;
        [LabelText("选择作物"), Required]
        public Button Select;
        [LabelText("清除作物"), Required]
        public Button Clear;
        public void ValidateConfiguration()
        {
            if (View == null)
                throw new InvalidOperationException("种植模块缺少所属建筑详情。");
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            BindSidebar(null);
            Bind(Select, null);
            Bind(Clear, null);
        }

        public void Refresh(Entity entity, bool canEdit)
        {
            var session = View.sessionController;
            var definition = session.em.GetComponentData<BuildingDefinitionRef>(entity).Definition;
            ref var details = ref BuildingDefinitions.Get(session.em, session.root, definition);
            if (!details.Capabilities.Farming.Enabled || details.Capabilities.Farming.Crops.Length == 0)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            var id = session.em.GetComponentData<Identity>(entity).Id;
            var farming = session.em.GetComponentData<BuildingFarmingState>(entity);
            bool planted = farming.Crop.IsValid;
            int duration = planted ? Mathf.Max(1, CropDefinitions.Get(session.em, session.root, farming.Crop).GrowthTurns) : 1;
            int threshold = CropGrowthOps.Threshold(duration);
            int percent = planted ? Mathf.RoundToInt(100f * farming.Progress / threshold) : 0;
            var season = SeasonWeatherOps.Season(session.em.GetComponentData<GameClock>(session.root).Turn);
            int remaining = CropGrowthOps.RemainingSettlements(farming.Progress, duration, season);
            Label.text = planted
                ? $"种植 · {CropName(farming.Crop)} {percent}% · {CropGrowthOps.Value(farming.Progress):0.0}/{duration:0.0} · 预计 {remaining} 回合"
                : "种植 · 点击圆钮选择作物";
            Span(Fill, 0, planted ? (float)farming.Progress / threshold : 0);
            Icon.sprite = planted ? CropPortrait(farming.Crop) : null;
            Icon.enabled = Icon.sprite != null;
            Bind(Select, () => BuildingCrops(id));
            Bind(Clear, canEdit && planted ? () => View.buildingUi.ShowBuildingConfirmation("铲除 " + CropName(farming.Crop), new[] { "失去当前作物与进度，不返种植费用。" }, () => View.commandsController.TryQueue(new ClearCropRequest { Building = id })) : null);
            BindSidebar(() => WorkerEfficiencyOps.Describe(session.em, session.root, entity, "种植"));
        }

        public void AppendDetails(Entity entity, Action<string, Action, string> detail)
        {
            var session = View.sessionController;
            var farming = session.em.GetComponentData<BuildingFarmingState>(entity);
            if (!farming.Crop.IsValid)
                return;
            var id = session.em.GetComponentData<Identity>(entity).Id;
            var turns = CropDefinitions.Get(session.em, session.root, farming.Crop).GrowthTurns;
            var threshold = CropGrowthOps.Threshold(turns);
            var percent = Mathf.RoundToInt(100f * farming.Progress / threshold);
            var season = SeasonWeatherOps.Season(session.em.GetComponentData<GameClock>(session.root).Turn);
            var remaining = CropGrowthOps.RemainingSettlements(farming.Progress, turns, season);
            detail($"作物 {CropName(farming.Crop)} · 生长 {percent}% · {CropGrowthOps.Value(farming.Progress):0.0}/{turns:0.0} · 当前季节预计 {remaining} 回合（换季会变化）", null, "building:" + id + ":planting:crop");
            if (session.em.GetComponentData<Session>(session.root).Phase != Phase.Day || !BuildingStatus.Operational(session.em, entity))
                return;
            detail("收获", () => View.commandsController.TryQueue(new HarvestBuildingRequest { Building = id }), null);
            detail(farming.AutoHarvest != 0 ? "关闭自动收获" : "开启自动收获", () => View.commandsController.TryQueue(new SetAutoHarvestRequest { Building = id, Enabled = (byte)(farming.AutoHarvest == 0 ? 1 : 0) }), null);
        }

        string CropName(CropId crop) => CropDefinitions.Get(View.sessionController.em, View.sessionController.root, crop).Metadata.Name.ToString();

        Sprite CropPortrait(CropId definition)
        {
            var icon = View.Crops.Get(definition)?.Icon;
            if (icon != null)
                return icon;
            ref var crop = ref CropDefinitions.Get(View.sessionController.em, View.sessionController.root, definition);
            return crop.HarvestOutputs.Length > 0 ? View.Items.Get(crop.HarvestOutputs[0].Item)?.Icon : null;
        }

        void BuildingCrops(ulong key)
        {
            var session = View.sessionController;
            var e = WorldQueries.Find(session.em, key);
            if (e == Entity.Null)
                return;
            var b = session.em.GetComponentData<Building>(e);
            var farming = session.em.GetComponentData<BuildingFarmingState>(e);
            var definition = session.em.GetComponentData<BuildingDefinitionRef>(e).Definition;
            ref var d = ref BuildingDefinitions.Get(session.em, session.root, definition);
            var entries = new List<CropSelectionEntry>();
            var seen = new HashSet<CropId>();
            for (int i = 0; i < d.Capabilities.Farming.Crops.Length; i++)
            {
                var allowed = d.Capabilities.Farming.Crops[i];
                if ((allowed.Level != 0 && allowed.Level != b.Level) || !seen.Add(allowed.Crop))
                    continue;
                var cropId = allowed.Crop;
                ref var crop = ref CropDefinitions.Get(session.em, session.root, cropId);
                var costs = new List<BuildingCost>();
                for (int cost = 0; cost < crop.PlantingCosts.Length; cost++)
                    costs.Add(new BuildingCost(crop.PlantingCosts[cost].Item, crop.PlantingCosts[cost].Quantity));
                var yields = new List<string>();
                for (int output = 0; output < crop.HarvestOutputs.Length; output++)
                {
                    var harvest = crop.HarvestOutputs[output];
                    var item = ItemDefinitions.Get(session.em, session.root, harvest.Item).Metadata.Name;
                    yields.Add(item + " " + harvest.MinimumQuantity + "～" + harvest.MaximumQuantity);
                }
                entries.Add(new CropSelectionEntry
                {
                    Crop = cropId,
                    Icon = CropPortrait(cropId),
                    Name = crop.Metadata.Name.ToString(),
                    BaseYield = yields.Count == 0 ? "无" : string.Join("、", yields),
                    GrowthTurns = crop.GrowthTurns,
                    PlantingCost = View.buildingUi.CostText(costs),
                    Available = View.courtController.CourtDay && session.em.GetComponentData<SimulationControl>(session.root).Paused == 0 && BuildingStatus.Operational(session.em, e) && !farming.Crop.IsValid && BuildingCostOps.CanPay(session.em, session.root, costs)
                });
            }
            View.buildingUi.ShowCropSelection(entries, crop => View.commandsController.TryQueue(new PlantCropRequest { Building = key, Crop = crop }), farming.Crop.IsValid ? "已种植 " + CropName(farming.Crop) + "，更换前请先使用 X 铲除。" : null);
        }
    }
}
