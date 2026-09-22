using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentJobEffects
    {
        public BlobArray<TalentItemJobEffect> Items;
        public BlobArray<TalentSoldierJobEffect> Soldiers;
        public BlobArray<TalentHeroJobEffect> Heroes;
        public BlobArray<TalentKingdomJobEffect> Kingdom;
    }
}
