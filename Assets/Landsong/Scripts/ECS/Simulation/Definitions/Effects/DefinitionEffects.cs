using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct DefinitionEffects
    {
        public BlobArray<ItemNumericEffect> Items;
        public BlobArray<BuildingNumericEffect> Buildings;
        public BlobArray<SoldierNumericEffect> Soldiers;
        public BlobArray<HeroNumericEffect> Heroes;
        public BlobArray<TalentNumericEffect> Talents;
        public BlobArray<KingdomNumericEffect> Kingdom;
        public BlobArray<IntelligenceEffect> Intelligence;
        public BlobArray<FlatProductionEffect> FlatProduction;
    }
}
