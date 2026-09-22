using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingStorageCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingStorageSource source, ref global::Landsong.ECS.Definitions.BuildingStorage target, StorageSlotCatalogIndex storageSlotIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingStorage 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Providers, 0);
                builder.Allocate(ref target.Warehouses, 0);
                builder.Allocate(ref target.Conditions, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.Providers == null)
                throw new InvalidOperationException("配置项（Providers）列表不能为空引用。");
            var Providers = builder.Allocate(ref target.Providers, source.Providers.Length);
            for (int i = 0; i < source.Providers.Length; i++)
            {
                BuildingResourceProviderCompiler.Compile(ref builder, source.Providers[i], ref Providers[i]);
            }

            if (source.Warehouses == null)
                throw new InvalidOperationException("配置项（Warehouses）列表不能为空引用。");
            var Warehouses = builder.Allocate(ref target.Warehouses, source.Warehouses.Length);
            for (int i = 0; i < source.Warehouses.Length; i++)
            {
                BuildingWarehouseLevelCompiler.Compile(ref builder, source.Warehouses[i], ref Warehouses[i], storageSlotIndex);
            }

            if (source.Conditions == null)
                throw new InvalidOperationException("配置项（Conditions）列表不能为空引用。");
            var Conditions = builder.Allocate(ref target.Conditions, source.Conditions.Length);
            for (int i = 0; i < source.Conditions.Length; i++)
            {
                BuildingStorageConditionCompiler.Compile(ref builder, source.Conditions[i], ref Conditions[i]);
            }
        }
    }
}
