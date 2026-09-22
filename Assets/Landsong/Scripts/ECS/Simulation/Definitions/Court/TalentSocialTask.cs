using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentSocialTask
    {
        public int Order;
        public ItemId Item;
        public int Quantity;
        public int AffectionReward;
    }
}
