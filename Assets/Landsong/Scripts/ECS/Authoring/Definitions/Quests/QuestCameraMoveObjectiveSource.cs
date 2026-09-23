using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class QuestCameraMoveObjectiveSource : QuestObjectiveSource
    {
        [LabelText("数量")]
        public int Count;
    }
}
