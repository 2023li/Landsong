using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    [CreateAssetMenu(menuName = "Landsong/Presentation/Effect Catalog")]
    public sealed class EffectCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Cue
        {
            [LabelText("提示类型")]
            public PresentationCue Id;
            [LabelText("空间特效模板")]
            public PresentationEffect EffectPrefab;
            [LabelText("特效持续秒数"), Range(.1f, 10)]
            public float Lifetime = 1;
        }

        [LabelText("空间特效")]
        public Cue[] Cues = Array.Empty<Cue>();
        public Cue Find(PresentationCue id) => Array.Find(Cues, cue => cue != null && cue.Id == id);
    }
}
