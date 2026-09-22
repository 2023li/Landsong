using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuffRewardSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("增益")]
        public BuffDefinitionAsset Buff;
        [LabelText("许可等级")]
        public int GrantedLevel = 1;
    }
}
