using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class DefinitionMetadataSource
    {
        [LabelText("稳定标识"), Required]
        public string Id = "";
        [LabelText("名称"), Required]
        public string Name = "";

        [LabelText("说明"), TextArea]
        public string Description = "";

        [LabelText("图标")]
        public Sprite Icon;
    }
}
