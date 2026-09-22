using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingHousingSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("基础人口")]
        [ShowIf(nameof(Enabled))]
        public BuildingBasePopulationSource[] Population = Array.Empty<BuildingBasePopulationSource>();
        [LabelText("住宅配置")]
        [ShowIf(nameof(Enabled))]
        public BuildingResidenceLevelSource[] Residences = Array.Empty<BuildingResidenceLevelSource>();
        [LabelText("居民食谱")]
        [ShowIf(nameof(Enabled))]
        public BuildingResidentFoodSource[] Food = Array.Empty<BuildingResidentFoodSource>();
        [LabelText("住宅税收")]
        [ShowIf(nameof(Enabled))]
        public BuildingResidenceTaxSource[] Taxes = Array.Empty<BuildingResidenceTaxSource>();
        [LabelText("环境需求")]
        [ShowIf(nameof(Enabled))]
        public BuildingEnvironmentRequirementSource[] Environment = Array.Empty<BuildingEnvironmentRequirementSource>();
    }
}
