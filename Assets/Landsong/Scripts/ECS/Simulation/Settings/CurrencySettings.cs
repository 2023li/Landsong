using Sirenix.OdinInspector;
using Unity.Entities;
using Landsong.ECS.Definitions;

namespace Landsong.ECS
{
    [System.Serializable]
    public struct CurrencySettings : IComponentData
    {
        [LabelText("金币物品定义")]
        public ItemId Gold;
    }
}
