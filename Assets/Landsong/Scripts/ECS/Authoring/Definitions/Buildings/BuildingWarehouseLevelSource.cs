using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingWarehouseLevelSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("库存槽类型")]
        public StorageSlotDefinitionAsset SlotType;
        [LabelText("库存格数")]
        [MinValue(0)]
        public int Slots;
        [LabelText("启用所需工人")]
        [MinValue(0)]
        public int RequiredWorkers;
    }
}
