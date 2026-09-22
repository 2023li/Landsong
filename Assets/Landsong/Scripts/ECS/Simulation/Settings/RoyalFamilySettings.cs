using Sirenix.OdinInspector;
using Unity.Entities;

namespace Landsong.ECS
{
    [System.Serializable]
    public struct RoyalFamilySettings : IComponentData
    {
        [LabelText("子嗣数量上限")]
        public int MaxChildren;
        [LabelText("生育概率")]
        public float BirthChance;
        [LabelText("遗传变异概率")]
        public float MutationChance;
    }
}
