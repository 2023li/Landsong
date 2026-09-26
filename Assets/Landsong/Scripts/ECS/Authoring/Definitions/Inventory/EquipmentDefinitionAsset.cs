using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Inventory/Equipment")]
    public sealed class EquipmentDefinitionAsset : ItemDefinitionAsset
    {
        [LabelText("装备参数（损坏率为基础值）")]
        public EquipmentProfile Equipment;
    }
}
