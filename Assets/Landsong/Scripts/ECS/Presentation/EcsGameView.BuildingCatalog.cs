using System.Collections.Generic;
using Unity.Entities;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public BuildingCatalogBar BuildingBar;
        bool buildingBarOpen;
        public void ToggleBuildingCatalog()
        {
            if (PauseMenu != null && PauseMenu.IsOpen) return;
            if (Panel == "建筑" && buildingBarOpen) { ClosePanel(); return; }
            showBuildingDetails=false;if(BuildingDetailsPanel!=null)BuildingDetailsPanel.SetActive(false);
            OpenPanel("建筑");
        }
        void InitializeBuildingCatalog()
        {
            if (BuildingBar == null) return;
            BuildingBar.CloseButton.onClick.AddListener(ClosePanel);
            BuildingBar.EconomyButton.onClick.AddListener(() => OpenEconomy());
        }
        void RefreshBuildingCatalog()
        {
            if (BuildingBar == null) return;
            BuildingBar.gameObject.SetActive(Panel == "建筑" && buildingBarOpen && em.GetComponentData<Session>(root).Phase == Phase.Day);
            if (!BuildingBar.gameObject.activeSelf) return;
            var choices = new List<int>();
            ForDefinitions(ContentKind.Building, (i, d) => { if (Sim.HasGrant(em, root, i)) choices.Add(i); });
            choices.Sort((a, b) => { var order = Sim.Definition(em, root, a).BuildingPolicy.MenuOrder.CompareTo(Sim.Definition(em, root, b).BuildingPolicy.MenuOrder); return order != 0 ? order : string.CompareOrdinal(Name(a), Name(b)); });
            var cards = new List<BuildingCatalogCard>();
            foreach (var definition in choices)
            {
                var d = Sim.Definition(em, root, definition); var quote = BuildingOps.CheckBuild(em, root, definition); var source = BuildingSource(definition);
                var tooltip = Name(definition) + " · LV1 · " + d.Size.x + "×" + d.Size.y + "\n放置成本：" + CostText(quote.Costs)
                    + "\n每回合消耗（固定维护）：" + CostText(BuildingCostOps.Rules(em, root, definition, RuleKind.Maintenance, 1));
                if (d.Duration > 0) tooltip += "\n施工 " + d.Duration + " 回合；首期材料：" + CostText(BuildingCostOps.Rules(em, root, definition, RuleKind.ConstructionCost, 1)) + "（以后按施工阶段配置）";
                var inputs = BuildingCostOps.Rules(em, root, definition, RuleKind.Input, 1);
                if (inputs.Count > 0) tooltip += "\n每次生产投入：" + CostText(inputs);
                tooltip += "\n岗位食物、补贴、配方及供奉等按实际配置另计。";
                if (!string.IsNullOrEmpty(source?.Description)) tooltip += "\n" + source.Description;
                if (!quote.Allowed) tooltip += "\n不可建造：" + quote.Reason;
                cards.Add(new BuildingCatalogCard { Definition = definition, Name = Name(definition), Category = d.BuildingPolicy.Category, Icon = source?.Icon, Tooltip = tooltip, Allowed = quote.Allowed });
            }
            BuildingBar.Bind(cards, BeginBuildingPlacement, !FeatureOps.Unlocked(em, root, "Building") ? "建造许可尚未解锁：先完成主线任务" : cards.Count == 0 ? "尚未拥有建筑蓝图" : "选择建筑后点击地图放置 · R 旋转 · 右键取消");
        }
    }
}
