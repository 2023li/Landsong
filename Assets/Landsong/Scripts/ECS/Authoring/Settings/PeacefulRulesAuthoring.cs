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
    public sealed class PeacefulRulesAuthoring : MonoBehaviour
    {
        [LabelText("平安夜访客设置")]
        public PeacefulRules Settings = new PeacefulRules
        {
            MaximumPerNight = 3,
            MaximumConcurrent = 2,
            TheftValueBudget = 15,
            FirstOpportunity = 2.5f,
            Interval = 2.5f
        };
        public static void Validate(PeacefulRules s)
        {
            if (s.MaximumPerNight < 0 || s.MaximumPerNight > 32 || s.MaximumConcurrent < 1 || s.MaximumConcurrent > 8 || s.TheftValueBudget < 0 || s.TheftValueBudget > 100000 || !math.isfinite(s.FirstOpportunity) || s.FirstOpportunity < 1 || !math.isfinite(s.Interval) || s.Interval < 1)
                throw new InvalidOperationException("平安夜访客预算或时间配置无效。");
        }

        public sealed class Baker : Baker<PeacefulRulesAuthoring>
        {
            public override void Bake(PeacefulRulesAuthoring authoring)
            {
                var s = authoring.Settings;
                Validate(s);
                AddComponent(GetEntity(TransformUsageFlags.None), s);
            }
        }
    }
}
