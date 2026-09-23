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
    public sealed class UI_GamePanel_BuildingDetails_Block_基础产出 : UI_GamePanel_BuildingDetails_Block
    {
        [LabelText("基础产出"), Required]
        public TMP_Text Label;
        [LabelText("布局"), Required]
        public LayoutElement Layout;
        public void ValidateConfiguration()
        {
            if (View == null)
                throw new InvalidOperationException("基础产出模块缺少所属建筑详情。");
        }

        public void Refresh(Entity entity)
        {
            var session = View.sessionController;
            var building = session.em.GetComponentData<Building>(entity);
            var stats = session.em.GetComponentData<BuildingHousingStats>(entity);
            var garrison = session.em.GetComponentData<BuildingGarrisonStats>(entity);
            var quests = session.em.GetComponentData<BuildingQuestStats>(entity);
            ref var definition = ref BuildingDefinitions.Get(session.em, session.root, session.em.GetComponentData<BuildingDefinitionRef>(entity).Definition);
            var outputs = new List<string>();
            if (stats.MaxPopulation + stats.BasePopulation > 0)
                outputs.Add("人口 +" + (stats.MaxPopulation + stats.BasePopulation));
            int research = 0;
            for (int i = 0; i < definition.Capabilities.Research.Levels.Length; i++)
            {
                var level = definition.Capabilities.Research.Levels[i];
                if (level.Level == 0 || level.Level == building.Level)
                    research += level.PointsPerTurn;
            }

            if (research > 0)
                outputs.Add("科研值 +" + research);
            if (garrison.Capacity > 0)
                outputs.Add("士兵槽 " + garrison.Capacity);
            for (int i = 0; i < definition.Capabilities.Storage.Warehouses.Length; i++)
            {
                var warehouse = definition.Capabilities.Storage.Warehouses[i];
                if ((warehouse.Level == 0 || warehouse.Level == building.Level) && warehouse.Slots > 0)
                    outputs.Add("库存 " + StorageSlotDefinitions.Get(session.em, session.root, warehouse.SlotType).Metadata.Name + " ×" + warehouse.Slots + "（工人≥" + warehouse.RequiredWorkers + "）");
            }

            int invitations = session.em.HasBuffer<QuestOfferSlot>(entity) ? session.em.GetBuffer<QuestOfferSlot>(entity).Length : 0;
            if (invitations > 0)
                outputs.Add("邀约槽 " + invitations);
            if (quests.Capacity > 0)
                outputs.Add("任务槽位 " + quests.Capacity);
            bool visible = outputs.Count > 0;
            gameObject.SetActive(visible);
            bool hasWorkforce = session.em.GetComponentData<BuildingWorkforceStats>(entity).Capacity > 0;
            BindSidebar(visible && hasWorkforce ? () => WorkerEfficiencyOps.Describe(session.em, session.root, entity, "全部") : null);
            if (!visible)
                return;

            int characters = 0;
            for (int i = 0; i < outputs.Count; i++)
                characters += outputs[i].Length;
            Label.text = "基础产出\n" + string.Join(" · ", outputs);
            Layout.preferredHeight = Mathf.Max(94, 38 + Mathf.Ceil(characters / 22f) * 24);
        }
    }
}
