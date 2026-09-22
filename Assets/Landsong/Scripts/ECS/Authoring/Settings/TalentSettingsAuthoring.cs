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
    public sealed class TalentSettingsAuthoring : MonoBehaviour
    {
        [LabelText("人才设置")]
        public TalentSettings Settings = new TalentSettings
        {
            TalentCapacity = 8,
            TalentRecruitCost = 0,
            TalentExperience = 10
        };
        public static void Validate(TalentSettings s)
        {
            if (s.TalentCapacity < 0 || s.TalentRecruitCost < 0 || s.TalentExperience < 0)
                throw new InvalidOperationException("人才容量、招募费用和经验不能为负。");
        }

        public sealed class Baker : Baker<TalentSettingsAuthoring>
        {
            public override void Bake(TalentSettingsAuthoring authoring)
            {
                var s = authoring.Settings;
                Validate(s);
                AddComponent(GetEntity(TransformUsageFlags.None), s);
            }
        }
    }
}
