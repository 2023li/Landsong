using Sirenix.OdinInspector;
using Unity.Entities;

namespace Landsong.ECS
{
    [System.Serializable]
    public struct TalentSettings : IComponentData
    {
        [LabelText("人才容量")]
        public int TalentCapacity;
        [LabelText("人才招募金币")]
        public int TalentRecruitCost;
        [LabelText("人才升级经验")]
        public int TalentExperience;
    }
}
