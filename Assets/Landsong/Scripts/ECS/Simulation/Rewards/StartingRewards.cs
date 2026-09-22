using Unity.Entities;
using Landsong.ECS.Definitions;

namespace Landsong.ECS
{
    public struct StartingRewardsBlob
    {
        public DefinitionRewards Rewards;
    }

    public struct StartingRewards : IComponentData
    {
        public BlobAssetReference<StartingRewardsBlob> Value;
    }
}
