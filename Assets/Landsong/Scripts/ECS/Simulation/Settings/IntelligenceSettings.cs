using Sirenix.OdinInspector;
using Unity.Entities;

namespace Landsong.ECS
{
    [System.Serializable]
    public struct IntelligenceSettings : IComponentData
    {
        [LabelText("低级情报阈值")]
        public int LowIntel;
        [LabelText("中级情报阈值")]
        public int MediumIntel;
        [LabelText("高级情报阈值")]
        public int HighIntel;
        [LabelText("中级情报提前时间（秒）")]
        public float MediumIntelLead;
        [LabelText("高级情报提前时间（秒）")]
        public float HighIntelLead;
    }
}
