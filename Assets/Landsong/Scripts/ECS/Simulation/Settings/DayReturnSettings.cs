using System;
using Sirenix.OdinInspector;
using Unity.Entities;

namespace Landsong.ECS
{
    [Serializable]
    public struct DayReturnSettings : IComponentData
    {
        [LabelText("归营超时下限（游戏秒）")]
        public float MinimumSeconds;
        [LabelText("归营超时上限（游戏秒）")]
        public float MaximumSeconds;
    }

    // Transient daytime presentation/movement; saved soldiers load already sheltered.
    public struct DayReturnState : IComponentData
    {
        public float Remaining, RetryIn;
    }
}
