using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class QuestTurnObjectiveSource : QuestObjectiveSource
    {
        [LabelText("回合数")]
        public int Turns;
        [LabelText("从接受任务起计算")]
        public bool SinceAccepted;
    }
}
