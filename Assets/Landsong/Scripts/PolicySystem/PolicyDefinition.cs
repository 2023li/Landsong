using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.PolicySystem
{
    [CreateAssetMenu(menuName = "Landsong/Policy/Policy Definition", fileName = "PolicyDefinition")]
    public sealed class PolicyDefinition : ScriptableObject
    {
        [PreviewField(72)]
        [SerializeField, LabelText("图标")] private Sprite icon;

        [SerializeField, LabelText("政策ID")] private string policyId;
        [SerializeField, LabelText("显示名称")] private string displayName;
        [SerializeField, TextArea, LabelText("描述")] private string description;
        [SerializeField, LabelText("政策树ID")] private string treeId;
        [SerializeField, LabelText("政策树名称")] private string treeDisplayName;
        [SerializeField, Min(1), LabelText("层级")] private int tier = 1;
        [SerializeField, Min(0), LabelText("所需民意")] private int requiredPublicOpinion;

        public Sprite Icon => icon;
        public string PolicyId => policyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public string TreeId => treeId;
        public string TreeDisplayName => string.IsNullOrWhiteSpace(treeDisplayName) ? treeId : treeDisplayName;
        public int Tier => Mathf.Max(1, tier);
        public int RequiredPublicOpinion => Mathf.Max(0, requiredPublicOpinion);
        public bool IsValid => !string.IsNullOrWhiteSpace(policyId) && !string.IsNullOrWhiteSpace(treeId);

        private void OnValidate()
        {
            Normalize();
        }

        public void Normalize()
        {
            policyId = NormalizeText(policyId);
            displayName = NormalizeText(displayName);
            description = NormalizeText(description);
            treeId = NormalizeText(treeId);
            treeDisplayName = NormalizeText(treeDisplayName);
            tier = Mathf.Max(1, tier);
            requiredPublicOpinion = Mathf.Max(0, requiredPublicOpinion);
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
