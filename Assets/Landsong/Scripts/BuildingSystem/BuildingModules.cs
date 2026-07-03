using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    public abstract class BuildingModuleBase
    {
        [SerializeField, LabelText("启用")] private bool enabled = true;

        public bool IsEnabled => enabled;

        public virtual void Normalize()
        {
        }

        public virtual void AppendDetailSections(BuildingBase building, ref List<BuildingDetailSection> sections)
        {
        }

        protected static void AddDetailSection(
            ref List<BuildingDetailSection> sections,
            string title,
            IReadOnlyList<BuildingDetailRow> rows)
        {
            var section = new BuildingDetailSection(title, rows);
            if (!section.IsValid)
            {
                return;
            }

            sections ??= new List<BuildingDetailSection>();
            sections.Add(section);
        }
    }

    [Serializable]
    public sealed class BuildingNearbyPopulationJobAttractionModule : BuildingModuleBase
    {
        [SerializeField, LabelText("人口搜索半径"), Min(0)]
        [PropertyTooltip("单位：格。按曼哈顿距离统计附近人口建筑。")]
        private int populationSearchRadius = 10;

        [SerializeField, LabelText("附近每人口就业吸引力"), Min(0f)]
        [PropertyTooltip("单位：岗位吸引力点/人。附近每 1 人为该建筑提供多少岗位吸引力。")]
        private float attractionPerNearbyPopulation =
            BuildingJobSystem.DefaultAttractionPerNearbyPopulation;

        public int PopulationSearchRadius => Mathf.Max(0, populationSearchRadius);
        public float AttractionPerNearbyPopulation => Mathf.Max(0f, attractionPerNearbyPopulation);

        public override void Normalize()
        {
            populationSearchRadius = Mathf.Max(0, populationSearchRadius);
            attractionPerNearbyPopulation = Mathf.Max(0f, attractionPerNearbyPopulation);
        }

        public override void AppendDetailSections(BuildingBase building, ref List<BuildingDetailSection> sections)
        {
            if (!IsEnabled)
            {
                return;
            }

            AddDetailSection(
                ref sections,
                "岗位人口模块",
                new[]
                {
                    new BuildingDetailRow("人口搜索半径", PopulationSearchRadius.ToString()),
                    new BuildingDetailRow("附近每人口就业吸引力", AttractionPerNearbyPopulation.ToString("0.##"))
                });
        }
    }

    [Serializable]
    public sealed class BuildingInventorySlotCapacityModule : BuildingModuleBase
    {
        [SerializeField, LabelText("提供库存格数"), Min(0)]
        [PropertyTooltip("单位：格。该建筑存在时提供的额外库存格子数量。")]
        private int providedSlotCount = 5;

        public int ProvidedSlotCount => Mathf.Max(0, providedSlotCount);

        public override void Normalize()
        {
            providedSlotCount = Mathf.Max(0, providedSlotCount);
        }

        public override void AppendDetailSections(BuildingBase building, ref List<BuildingDetailSection> sections)
        {
            if (!IsEnabled)
            {
                return;
            }

            AddDetailSection(
                ref sections,
                "库存模块",
                new[]
                {
                    new BuildingDetailRow("提供库存格数", ProvidedSlotCount.ToString())
                });
        }
    }
}
