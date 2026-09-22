using System;
using System.Collections.Generic;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class ItemGroupCatalogValidation
    {
        public static void Validate(ItemGroupCatalogAsset catalog)
        {
            var members = new HashSet<ItemGroupDefinitionAsset>(catalog.Definitions);
            foreach (var asset in catalog.Definitions)
            {
                var seen = new HashSet<ItemGroupDefinitionAsset>();
                for (var parent = asset; parent != null; parent = parent.ParentGroup)
                    if (!members.Contains(parent) || !seen.Add(parent))
                        throw new InvalidOperationException(asset.Metadata.Id + "：物品分组父链未注册或存在循环。");
            }
        }
    }
}
