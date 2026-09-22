using System;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class CurrencyAuthoring : MonoBehaviour
    {
        public ItemCatalogAsset Items => GameContentSetAuthoring.Resolve<ItemCatalogAsset>(this);
        [LabelText("金币物品"), Required]
        public ItemDefinitionAsset Gold;
        public sealed class Baker : Baker<CurrencyAuthoring>
        {
            public override void Bake(CurrencyAuthoring authoring)
            {
                DependsOn(authoring.Items);
                DependsOn(authoring.Gold);
                AddComponent(GetEntity(TransformUsageFlags.None), new CurrencySettings { Gold = new ItemCatalogIndex(authoring.Items).Resolve(authoring.Gold) });
            }
        }
    }
}
