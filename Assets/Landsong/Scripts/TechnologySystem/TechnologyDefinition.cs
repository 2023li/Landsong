using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.TechnologySystem
{
    [CreateAssetMenu(menuName = "Landsong/Technology/Technology Definition", fileName = "TechnologyDefinition")]
    public sealed class TechnologyDefinition : ScriptableObject
    {
        [PreviewField(72)]
        [SerializeField, LabelText("图标")] private Sprite icon;

        [SerializeField, LabelText("科技ID")] private string technologyId;
        [SerializeField, LabelText("显示名称")] private string displayName;
        [SerializeField, TextArea, LabelText("描述")] private string description;
        [SerializeField, Min(0), LabelText("科技点消耗")] private int sciencePointCost = 1;
        [SerializeField, LabelText("前置科技")] private TechnologyDefinition[] prerequisites = Array.Empty<TechnologyDefinition>();
        [SerializeField, LabelText("编辑器节点位置")] private Vector2 graphPosition;

        public Sprite Icon => icon;
        public string TechnologyId => technologyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public int SciencePointCost => Mathf.Max(0, sciencePointCost);
        public IReadOnlyList<TechnologyDefinition> Prerequisites => prerequisites ?? Array.Empty<TechnologyDefinition>();
        public Vector2 GraphPosition => graphPosition;
        public bool HasIcon => icon != null;
        public bool IsValid => !string.IsNullOrWhiteSpace(technologyId);

        private void OnValidate()
        {
            Normalize();
        }

        public void Normalize()
        {
            technologyId = NormalizeOptionalText(technologyId);
            displayName = NormalizeOptionalText(displayName);
            description = NormalizeOptionalText(description);
            sciencePointCost = Mathf.Max(0, sciencePointCost);
            prerequisites ??= Array.Empty<TechnologyDefinition>();
        }

        private static string NormalizeOptionalText(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        }
    }
}
