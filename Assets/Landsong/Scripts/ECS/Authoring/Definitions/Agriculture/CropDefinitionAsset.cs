using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Agriculture/Crop")]
    public sealed class CropDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("成熟回合")]
        public int GrowthTurns = 1;
        [LabelText("种植费用")]
        public ItemAmountSource[] PlantingCosts = Array.Empty<ItemAmountSource>();
        [LabelText("自动收获费用")]
        public ItemAmountSource[] AutomaticHarvestCosts = Array.Empty<ItemAmountSource>();
        [LabelText("收获产物")]
        public ItemQuantityRangeSource[] HarvestOutputs = Array.Empty<ItemQuantityRangeSource>();
    }
}
