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
    public sealed class PortraitLibraryAuthoring : MonoBehaviour
    {
        [LabelText("肖像配置"), Required]
        public PortraitConfig Portraits;
        public sealed class Baker : Baker<PortraitLibraryAuthoring>
        {
            public override void Bake(PortraitLibraryAuthoring authoring)
            {
                if (authoring.Portraits == null)
                    throw new InvalidOperationException("缺少肖像配置。");
                DependsOn(authoring.Portraits);
                foreach (var part in authoring.Portraits.Parts ?? Array.Empty<PortraitPartSource>())
                    if (part != null && part.Renders != null)
                        foreach (var render in part.Renders)
                            if (render?.Sprite != null)
                            {
                                DependsOn(render.Sprite);
                                DependsOn(render.Sprite.texture);
                            }

                var blob = PortraitLibraryBuilder.Build(authoring.Portraits);
                AddBlobAsset(ref blob, out _);
                AddComponent(GetEntity(TransformUsageFlags.None), new PortraitLibrary { Value = blob });
            }
        }
    }
}
