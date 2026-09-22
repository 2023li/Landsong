using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class QuestTurnObjectiveSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("目标稳定标识")]
        public string Key = "";
        [LabelText("回合数")]
        public int Turns;
        [LabelText("从接受任务起计算")]
        public bool SinceAccepted;
    }
}
