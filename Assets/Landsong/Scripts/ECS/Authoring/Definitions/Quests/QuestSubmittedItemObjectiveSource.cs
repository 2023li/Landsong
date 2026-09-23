using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class QuestSubmittedItemObjectiveSource : QuestObjectiveSource
    {
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("数量")]
        public int Quantity;
    }
}
