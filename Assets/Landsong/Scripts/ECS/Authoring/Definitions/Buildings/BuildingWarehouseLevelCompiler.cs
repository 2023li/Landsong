using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingWarehouseLevelCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingWarehouseLevelSource source, ref global::Landsong.ECS.Definitions.BuildingWarehouseLevel target, StorageSlotCatalogIndex storageSlotIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingWarehouseLevel 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.SlotType = storageSlotIndex.Resolve(source.SlotType, true);
            target.Slots = source.Slots;
            target.RequiredWorkers = source.RequiredWorkers;
        }
    }
}
