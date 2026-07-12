using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.TechnologySystem
{
    public static class TechnologyNodeId
    {
        public const int MinimumRow = 1;
        public const int MaximumRow = 5;

        public static bool TryParse(string technologyId, out int row, out int column)
        {
            row = 0;
            column = 0;
            if (string.IsNullOrWhiteSpace(technologyId))
            {
                return false;
            }

            var parts = technologyId.Trim().Split(new[] { '_' }, 4);
            return parts.Length == 4
                   && string.Equals(parts[0], "TN", StringComparison.Ordinal)
                   && int.TryParse(parts[1], out row)
                   && row >= MinimumRow
                   && row <= MaximumRow
                   && int.TryParse(parts[2], out column)
                   && column > 0
                   && !string.IsNullOrWhiteSpace(parts[3]);
        }
    }

    [CreateAssetMenu(menuName = "Landsong/Technology/Technology Definition", fileName = "TechnologyDefinition")]
    public sealed class TechnologyDefinition : ScriptableObject
    {
        [PreviewField(72)]
        [SerializeField, LabelText("图标")] private Sprite icon;

        [SerializeField, LabelText("科技ID")] private string technologyId = "TN_R_L_";
        [SerializeField, LabelText("显示名称")] private string displayName;
        [SerializeField, TextArea, LabelText("描述")] private string description;
        [SerializeField, Min(0), LabelText("研究需求科技点")] private int sciencePointCost = 1;
        [SerializeField, LabelText("允许重复研究")] private bool allowRepeatResearch;
        [SerializeField, LabelText("前置科技")] private TechnologyDefinition[] prerequisites = Array.Empty<TechnologyDefinition>();
        [SerializeReference, LabelText("研究完成效果")] private TechnologyEffect[] completionEffects = Array.Empty<TechnologyEffect>();
        [SerializeField, LabelText("编辑器节点位置")] private Vector2 graphPosition;

        public Sprite Icon => icon;
        public string TechnologyId => technologyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public int SciencePointCost => Mathf.Max(0, sciencePointCost);
        public bool AllowRepeatResearch => allowRepeatResearch;
        public IReadOnlyList<TechnologyDefinition> Prerequisites => prerequisites ?? Array.Empty<TechnologyDefinition>();
        public IReadOnlyList<TechnologyEffect> CompletionEffects => completionEffects ?? Array.Empty<TechnologyEffect>();
        public Vector2 GraphPosition => graphPosition;
        public bool HasIcon => icon != null;
        public bool IsValid => !string.IsNullOrWhiteSpace(technologyId);

        public bool TryGetNodePosition(out int row, out int column)
        {
            return TechnologyNodeId.TryParse(technologyId, out row, out column);
        }

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
            completionEffects ??= Array.Empty<TechnologyEffect>();
            for (var i = 0; i < completionEffects.Length; i++)
            {
                completionEffects[i]?.Normalize();
            }
        }

        private static string NormalizeOptionalText(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        }
    }
}
