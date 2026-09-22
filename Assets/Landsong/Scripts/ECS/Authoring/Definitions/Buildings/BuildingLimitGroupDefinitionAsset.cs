using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Buildings/BuildingLimitGroup")]
    public sealed class BuildingLimitGroupDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
    }
}
