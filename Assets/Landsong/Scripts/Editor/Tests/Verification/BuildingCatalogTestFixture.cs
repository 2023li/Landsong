#if UNITY_EDITOR
using System;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    // Isolated authored copies retain each catalog's local index order while tests edit concrete capabilities.
    sealed class BuildingCatalogTestFixture : IDisposable
    {
        readonly CatalogFixture.Scope scope = new CatalogFixture.Scope();
        readonly EntityManager manager;
        readonly Entity root;
        readonly BuildingCatalog originalBuildings;
        readonly ItemCatalog originalItems;
        BlobAssetReference<BuildingCatalogBlob> buildingBlob;
        BlobAssetReference<ItemCatalogBlob> itemBlob;
        public readonly BuildingCatalogAsset Buildings;
        public readonly ItemCatalogAsset Items;
        public BuildingCatalogTestFixture(EntityManager em, Entity owner)
        {
            manager = em;
            root = owner;
            originalBuildings = em.GetComponentData<BuildingCatalog>(owner);
            originalItems = em.GetComponentData<ItemCatalog>(owner);
            Items = scope.Clone(Load<ItemCatalogAsset>("Item"));
            Buildings = scope.Clone(Load<BuildingCatalogAsset>("Building"));
        }

        static T Load<T>(string name)
            where T : ScriptableObject => AssetDatabase.LoadAssetAtPath<T>("Assets/Landsong/ECSContent/Catalogs/Source/" + name + "Catalog.asset");
        public void Apply()
        {
            var groups = new ItemGroupCatalogIndex(Load<ItemGroupCatalogAsset>("ItemGroup"));
            var nextItems = ItemCatalogCompiler.Build(Items, groups);
            BlobAssetReference<BuildingCatalogBlob> nextBuildings = default;
            try
            {
                nextBuildings = BuildingCatalogCompiler.Build(Buildings, new BuildingLimitGroupCatalogIndex(Load<BuildingLimitGroupCatalogAsset>("BuildingLimitGroup")), new CropCatalogIndex(Load<CropCatalogAsset>("Crop")), new HeroCatalogIndex(Load<HeroCatalogAsset>("Hero")), new ItemCatalogIndex(Items), groups, new SoldierCatalogIndex(Load<SoldierCatalogAsset>("Soldier")), new StorageSlotCatalogIndex(Load<StorageSlotCatalogAsset>("StorageSlot")), new TechnologyCatalogIndex(Load<TechnologyCatalogAsset>("Technology")));
                manager.SetComponentData(root, new BuildingCatalog { Value = nextBuildings });
                manager.SetComponentData(root, new ItemCatalog { Value = nextItems });
                if (buildingBlob.IsCreated)
                    buildingBlob.Dispose();
                if (itemBlob.IsCreated)
                    itemBlob.Dispose();
                buildingBlob = nextBuildings;
                itemBlob = nextItems;
            }
            catch
            {
                if (nextBuildings.IsCreated)
                    nextBuildings.Dispose();
                nextItems.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (manager.Exists(root))
            {
                manager.SetComponentData(root, originalBuildings);
                manager.SetComponentData(root, originalItems);
            }

            if (buildingBlob.IsCreated)
                buildingBlob.Dispose();
            if (itemBlob.IsCreated)
                itemBlob.Dispose();
            scope.Dispose();
        }
    }
}
#endif
