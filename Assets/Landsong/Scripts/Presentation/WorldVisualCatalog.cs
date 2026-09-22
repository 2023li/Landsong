using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    [CreateAssetMenu(menuName = "Landsong/Presentation/World Visual Catalog")]
    public sealed class WorldVisualCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Model
        {
            [LabelText("内容稳定编号"), Tooltip("填写内容 ID，不使用显示名称。")]
            public string Definition;
            [LabelText("建筑阶段")]
            public LifeStage Stage = LifeStage.Operational;
            [LabelText("最低等级"), Min(1)]
            public int Level = 1;
            [LabelText("皮肤编号")]
            public string Skin = "";
            [LabelText("表现对象模板"), Required]
            public PresentationActor ActorPrefab;
            [LabelText("位置偏移")]
            public Vector3 Offset;
            [LabelText("模型缩放")]
            public Vector3 Scale = Vector3.one;
        }

        [LabelText("遗留世界模型（只读）"), ReadOnly]
        public Model[] Models = Array.Empty<Model>();
        public Model Select(string definition, LifeStage stage, int level, string skin)
        {
            Model best = null;
            int score = -1;
            foreach (var model in Models)
            {
                if (model == null || model.ActorPrefab == null || model.Definition != definition || model.Level > level || (model.Skin ?? "") != (skin ?? ""))
                    continue;
                int value = model.Stage == stage ? 10000 : model.Stage == LifeStage.Operational ? 0 : -1;
                if (value < 0)
                    continue;
                value += model.Level;
                if (value > score)
                {
                    score = value;
                    best = model;
                }
            }

            return best;
        }
    }
}
