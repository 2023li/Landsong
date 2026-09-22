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
    public sealed class RoyalFamilySettingsAuthoring : MonoBehaviour
    {
        [LabelText("王室成长设置")]
        public RoyalFamilySettings Settings = new RoyalFamilySettings
        {
            MaxChildren = 8,
            BirthChance = 0.08f,
            MutationChance = 0.03f
        };
        public static void Validate(RoyalFamilySettings s)
        {
            if (s.MaxChildren < 0 || !math.isfinite(s.BirthChance) || s.BirthChance < 0 || s.BirthChance > 1 || !math.isfinite(s.MutationChance) || s.MutationChance < 0 || s.MutationChance > 1)
                throw new InvalidOperationException("王室子嗣数量或遗传概率无效。");
        }

        public sealed class Baker : Baker<RoyalFamilySettingsAuthoring>
        {
            public override void Bake(RoyalFamilySettingsAuthoring authoring)
            {
                var s = authoring.Settings;
                Validate(s);
                AddComponent(GetEntity(TransformUsageFlags.None), s);
            }
        }
    }
}
