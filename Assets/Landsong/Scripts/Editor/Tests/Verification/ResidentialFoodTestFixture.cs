#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS.Editor
{
    sealed class ResidentialFoodTestFixture : IDisposable
    {
        readonly BlobAssetReference<ItemCatalogBlob> items;
        readonly BlobAssetReference<ItemGroupCatalogBlob> groups;
        readonly BlobAssetReference<BuildingCatalogBlob> narrow, broad;
        public readonly BlobAssetReference<DefinitionRewards> Rewards;
        public ResidentialFoodTestFixture()
        {
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<ItemCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, 3);
                for (int i = 0; i < 3; i++)
                    definitions[i] = new ItemDefinition
                    {
                        Metadata = new DefinitionMetadata
                        {
                            Id = i == 0 ? "z-food" : i == 1 ? "a-food" : "c-food"
                        },
                        PrimaryGroup = ItemGroupId.FromIndex(0),
                        MaximumStack = 100
                    };
                builder.Allocate(ref definitions[0].AdditionalGroups, 1)[0] = ItemGroupId.FromIndex(1);
                items = builder.CreateBlobAssetReference<ItemCatalogBlob>(Allocator.Persistent);
            }

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<ItemGroupCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, 2);
                definitions[0].Metadata.Id = "all-food";
                definitions[1].Metadata.Id = "narrow-food";
                groups = builder.CreateBlobAssetReference<ItemGroupCatalogBlob>(Allocator.Persistent);
            }

            narrow = Buildings(2);
            broad = Buildings(1);
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<DefinitionRewards>();
                var rows = builder.Allocate(ref root.Items, 2);
                rows[0] = new ItemAmount
                {
                    Item = ItemId.FromIndex(0),
                    Quantity = 1
                };
                rows[1] = new ItemAmount
                {
                    Item = ItemId.FromIndex(2),
                    Quantity = 100,
                    Order = 1
                };
                Rewards = builder.CreateBlobAssetReference<DefinitionRewards>(Allocator.Persistent);
            }
        }

        static BlobAssetReference<BuildingCatalogBlob> Buildings(int count)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BuildingCatalogBlob>();
            var definitions = builder.Allocate(ref root.Definitions, 1);
            definitions[0].Metadata.Id = "house";
            var food = builder.Allocate(ref definitions[0].Capabilities.Housing.Food, count);
            for (int i = 0; i < count; i++)
                food[i] = new BuildingResidentFood
                {
                    FoodGroup = ItemGroupId.FromIndex(i),
                    Varieties = 1,
                    AmountPerResident = 1
                };
            return builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
        }

        public void Install(EntityManager em, Entity root)
        {
            em.AddComponentData(root, new ItemCatalog { Value = items });
            em.AddComponentData(root, new ItemGroupCatalog { Value = groups });
            em.AddComponentData(root, new BuildingCatalog { Value = narrow });
            em.AddBuffer<OwnedBuff>(root);
            em.AddBuffer<PolicyChoice>(root);
        }

        public void UseBroadRecipe(EntityManager em, Entity root) => em.SetComponentData(root, new BuildingCatalog { Value = broad });
        public void Dispose()
        {
            items.Dispose();
            groups.Dispose();
            narrow.Dispose();
            broad.Dispose();
            Rewards.Dispose();
        }
    }
}
#endif
