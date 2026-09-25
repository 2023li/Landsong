#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Editor
{
    internal sealed class EffectSourceFixture : IDisposable
    {
        readonly List<Action> releases = new List<Action>();
        BlobAssetReference<T> Keep<T>(BlobAssetReference<T> value)
            where T : unmanaged
        {
            releases.Add(() => value.Dispose());
            return value;
        }

        internal static ItemId Item => ItemId.FromIndex(0);
        internal static BuildingId Target => BuildingId.FromIndex(0);
        internal static BuildingId OtherBuilding => BuildingId.FromIndex(1);
        internal static BuildingId Aura => BuildingId.FromIndex(2);
        internal static BuffId Buff => BuffId.FromIndex(0);
        internal static PolicyId Policy => PolicyId.FromIndex(0);
        internal static TalentId Talent => TalentId.FromIndex(0);
        internal static TalentSlotId Slot => TalentSlotId.FromIndex(0);
        internal static RoyalTraitId KingTrait => RoyalTraitId.FromIndex(0);
        internal static RoyalTraitId PersonalTrait => RoyalTraitId.FromIndex(1);
        internal static TechnologyId Technology => TechnologyId.FromIndex(0);
        internal static SoldierId Soldier => SoldierId.FromIndex(0);

        internal EffectSourceFixture(EntityManager em, Entity root)
        {
            em.AddComponentData(root, CourtSettings.Default);
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<ItemCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 2);
                for (int i = 0; i < definitions.Length; i++)
                    definitions[i].Metadata = new DefinitionMetadata
                    {
                        Id = new FixedString128Bytes("effect.Item." + i),
                        Name = new FixedString128Bytes("效果Item " + i)
                    };
                for (int i = 0; i < 2; i++)
                    definitions[i].MaximumStack = 100;
                em.AddComponentData(root, new ItemCatalog { Value = Keep(builder.CreateBlobAssetReference<ItemCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<BuildingCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 3);
                for (int i = 0; i < definitions.Length; i++)
                    definitions[i].Metadata = new DefinitionMetadata
                    {
                        Id = new FixedString128Bytes("effect.Building." + i),
                        Name = new FixedString128Bytes("效果Building " + i)
                    };
                for (int i = 0; i < 3; i++)
                {
                    definitions[i].MaximumLevel = 2;
                    definitions[i].Footprint = new int2(1);
                }

                definitions[2].Capabilities.Defence.Enabled = true;
                var intel = builder.Allocate(ref definitions[2].Capabilities.Defence.Intelligence, 1);
                intel[0] = new BuildingIntelligenceLevel
                {
                    Level = 1,
                    Technology = Technology,
                    Points = 11,
                    RequiredWorkers = 2
                };
                definitions[2].Capabilities.Effects.Enabled = true;
                var spatial = builder.Allocate(ref definitions[2].Capabilities.Effects.Spatial, 1);
                spatial[0] = new BuildingSpatialEffect
                {
                    Level = 1,
                    Magnitude = 10,
                    Type = BuildingEnvironmentKind.Production,
                    Radius = 4,
                    Stacking = BuildingEffectStacking.HighestOfKind
                };
                em.AddComponentData(root, new BuildingCatalog { Value = Keep(builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<BuffCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 1);
                for (int i = 0; i < definitions.Length; i++)
                    definitions[i].Metadata = new DefinitionMetadata
                    {
                        Id = new FixedString128Bytes("effect.Buff." + i),
                        Name = new FixedString128Bytes("效果Buff " + i)
                    };
                Effects(builder, ref definitions[0].Effects, 1, 2, true);
                var military = builder.Allocate(ref definitions[0].Effects.Soldiers, 2);
                military[0] = new SoldierNumericEffect
                {
                    Effect = NumericEffectKind.AttackMultiplier,
                    Magnitude = .1f
                };
                military[1] = new SoldierNumericEffect
                {
                    Effect = NumericEffectKind.EquipmentBreakChanceMultiplier,
                    Magnitude = -.2f
                };
                var intel = builder.Allocate(ref definitions[0].Effects.Intelligence, 3);
                intel[0] = new IntelligenceEffect
                {
                    Points = 5
                };
                intel[1] = new IntelligenceEffect
                {
                    Level = 1,
                    Points = 10
                };
                intel[2] = new IntelligenceEffect
                {
                    Level = 2,
                    Points = 20
                };
                em.AddComponentData(root, new BuffCatalog { Value = Keep(builder.CreateBlobAssetReference<BuffCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<PolicyCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 1);
                for (int i = 0; i < definitions.Length; i++)
                    definitions[i].Metadata = new DefinitionMetadata
                    {
                        Id = new FixedString128Bytes("effect.Policy." + i),
                        Name = new FixedString128Bytes("效果Policy " + i)
                    };
                definitions[0].RequiredPublicOpinion = 50;
                Effects(builder, ref definitions[0].Effects, 2, 7);
                var intel = builder.Allocate(ref definitions[0].Effects.Intelligence, 1);
                intel[0] = new IntelligenceEffect
                {
                    Points = 7
                };
                em.AddComponentData(root, new PolicyCatalog { Value = Keep(builder.CreateBlobAssetReference<PolicyCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<TalentCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 1);
                for (int i = 0; i < definitions.Length; i++)
                    definitions[i].Metadata = new DefinitionMetadata
                    {
                        Id = new FixedString128Bytes("effect.Talent." + i),
                        Name = new FixedString128Bytes("效果Talent " + i)
                    };
                definitions[0].InitialLevel = 1;
                definitions[0].MaximumLevel = 2;
                Effects(builder, ref definitions[0].Effects, 3, 11);
                em.AddComponentData(root, new TalentCatalog { Value = Keep(builder.CreateBlobAssetReference<TalentCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<TalentSlotCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 1);
                for (int i = 0; i < definitions.Length; i++)
                    definitions[i].Metadata = new DefinitionMetadata
                    {
                        Id = new FixedString128Bytes("effect.TalentSlot." + i),
                        Name = new FixedString128Bytes("效果TalentSlot " + i)
                    };
                Effects(builder, ref definitions[0].Effects, 4, 13);
                var jobs = builder.Allocate(ref definitions[0].JobEffects.Items, 1);
                jobs[0] = new TalentItemJobEffect
                {
                    Recipient = Item,
                    Effect = NumericEffectKind.ProductionMultiplier,
                    BaseMagnitude = 10
                };
                em.AddComponentData(root, new TalentSlotCatalog { Value = Keep(builder.CreateBlobAssetReference<TalentSlotCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<RoyalTraitCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 2);
                for (int i = 0; i < definitions.Length; i++)
                    definitions[i].Metadata = new DefinitionMetadata
                    {
                        Id = new FixedString128Bytes("effect.RoyalTrait." + i),
                        Name = new FixedString128Bytes("效果RoyalTrait " + i)
                    };
                Effects(builder, ref definitions[0].Effects, 5, 17);
                Effects(builder, ref definitions[1].Effects, 6, 19);
                em.AddComponentData(root, new RoyalTraitCatalog { Value = Keep(builder.CreateBlobAssetReference<RoyalTraitCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<TechnologyCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 1);
                for (int i = 0; i < definitions.Length; i++)
                    definitions[i].Metadata = new DefinitionMetadata
                    {
                        Id = new FixedString128Bytes("effect.Technology." + i),
                        Name = new FixedString128Bytes("效果Technology " + i)
                    };
                definitions[0].Repeatable = true;
                var intel = builder.Allocate(ref definitions[0].Effects.Intelligence, 1);
                intel[0] = new IntelligenceEffect
                {
                    Level = 2,
                    Points = 30
                };
                var military = builder.Allocate(ref definitions[0].Effects.Soldiers, 1);
                military[0] = new SoldierNumericEffect
                {
                    Level = 1,
                    Effect = NumericEffectKind.AttackMultiplier,
                    Magnitude = .2f
                };
                em.AddComponentData(root, new TechnologyCatalog { Value = Keep(builder.CreateBlobAssetReference<TechnologyCatalogBlob>(Allocator.Persistent)) });
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var catalog = ref builder.ConstructRoot<SoldierCatalogBlob>();
                var definitions = builder.Allocate(ref catalog.Definitions, 1);
                for (int i = 0; i < definitions.Length; i++)
                    definitions[i].Metadata = new DefinitionMetadata
                    {
                        Id = new FixedString128Bytes("effect.Soldier." + i),
                        Name = new FixedString128Bytes("效果Soldier " + i)
                    };
                definitions[0].CombatStats = new UnitCombatStats
                {
                    MaximumHealth = 100,
                    Damage = 10,
                    MovementSpeed = 2,
                    AttackRange = 2,
                    AttackIntervalSeconds = 1,
                    ProjectileSpeed = 10,
                    Profile = CombatProfile.Default
                };
                em.AddComponentData(root, new SoldierCatalog { Value = Keep(builder.CreateBlobAssetReference<SoldierCatalogBlob>(Allocator.Persistent)) });
            }
        }

        static void Effects(BlobBuilder builder, ref DefinitionEffects effects, int n, int flat, bool targeted = false)
        {
            var production = builder.Allocate(ref effects.FlatProduction, targeted ? 2 : 1);
            production[0] = new FlatProductionEffect
            {
                Item = Item,
                Quantity = flat
            };
            if (targeted)
                production[1] = new FlatProductionEffect
                {
                    Item = Item,
                    Building = Target,
                    Quantity = 3
                };
            var items = builder.Allocate(ref effects.Items, 1);
            items[0] = new ItemNumericEffect
            {
                Target = Item,
                Effect = NumericEffectKind.ProductionMultiplier,
                Magnitude = n / 10f
            };
            var kingdom = builder.Allocate(ref effects.Kingdom, 2);
            kingdom[0] = new KingdomNumericEffect
            {
                Effect = KingdomEffectKind.ResearchOutput,
                Magnitude = n
            };
            kingdom[1] = new KingdomNumericEffect
            {
                Effect = KingdomEffectKind.PlotRisk,
                Magnitude = -n / 100f
            };
            var personal = builder.Allocate(ref effects.Talents, 1);
            personal[0] = new TalentNumericEffect
            {
                Effect = NumericEffectKind.NaturalDeathRisk,
                Magnitude = -n / 100f
            };
        }

        public void Dispose()
        {
            for (int i = releases.Count - 1; i >= 0; i--)
                releases[i]();
        }
    }
}
#endif
