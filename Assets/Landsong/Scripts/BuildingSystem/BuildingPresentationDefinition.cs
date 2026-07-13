using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Landsong.BuildingSystem
{
    [Serializable]
    public sealed class BuildingVisualAssetReference
    {
        [SerializeField, InspectorName("直接 View Prefab"), LabelText("直接 View Prefab")]
        private GameObject directPrefab;

        [SerializeField, InspectorName("Addressable View Prefab"), LabelText("Addressable View Prefab")]
        private AssetReferenceGameObject addressablePrefab;

        public GameObject DirectPrefab => directPrefab;
        public AssetReferenceGameObject AddressablePrefab => addressablePrefab;
        public bool HasDirectPrefab => directPrefab != null;
        public bool HasAddressablePrefab => addressablePrefab != null
                                           && addressablePrefab.RuntimeKeyIsValid();
        public bool IsConfigured => HasDirectPrefab || HasAddressablePrefab;

        public void Configure(GameObject prefab)
        {
            directPrefab = prefab;
            addressablePrefab = null;
        }
    }

    [Serializable]
    public sealed class BuildingStyleDefinition
    {
        [SerializeField, InspectorName("StyleId")] private string styleId;
        [SerializeField, InspectorName("显示名称")] private string displayName;
        [SerializeField, InspectorName("样式图标")] private Sprite icon;

        public BuildingStyleDefinition()
        {
        }

        public BuildingStyleDefinition(string styleId, string displayName, Sprite icon)
        {
            this.styleId = string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
            this.displayName = string.IsNullOrWhiteSpace(displayName) ? this.styleId : displayName.Trim();
            this.icon = icon;
        }

        public string StyleId => string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? StyleId : displayName.Trim();
        public Sprite Icon => icon;
        public bool IsValid => !string.IsNullOrWhiteSpace(StyleId);
    }

    [Serializable]
    public sealed class BuildingViewMapping
    {
        [SerializeField, InspectorName("生命周期阶段")] private BuildingLifecycleStage stage = BuildingLifecycleStage.Operational;
        [SerializeField, InspectorName("等级"), Min(0)] private int level = 1;
        [SerializeField, InspectorName("StyleId")] private string styleId;
        [SerializeField, InspectorName("View 资源")] private BuildingVisualAssetReference view = new BuildingVisualAssetReference();

        public BuildingViewMapping()
        {
        }

        public BuildingViewMapping(
            BuildingLifecycleStage stage,
            int level,
            string styleId,
            GameObject directPrefab = null)
        {
            this.stage = stage;
            this.level = stage == BuildingLifecycleStage.Operational ? Mathf.Max(1, level) : 0;
            this.styleId = string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
            view = new BuildingVisualAssetReference();
            view.Configure(directPrefab);
        }

        public BuildingLifecycleStage Stage => stage;
        public int Level => stage == BuildingLifecycleStage.Operational ? Mathf.Max(1, level) : 0;
        public string StyleId => string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
        public BuildingVisualAssetReference View => view;
    }

    [Serializable]
    public sealed class BuildingTransitionDefinition
    {
        [SerializeField, InspectorName("持续时间"), Min(0f)] private float duration = 0.35f;
        [SerializeField, InspectorName("换图归一化时间"), Range(0f, 1f)] private float viewSwapNormalizedTime = 0.5f;
        [SerializeField, InspectorName("允许跳过")] private bool allowSkip = true;
        [SerializeField, InspectorName("过渡特效 Prefab")] private GameObject effectPrefab;
        [SerializeField, InspectorName("过渡音效")] private AudioClip sound;

        public float Duration => Mathf.Max(0f, duration);
        public float ViewSwapNormalizedTime => Mathf.Clamp01(viewSwapNormalizedTime);
        public bool AllowSkip => allowSkip;
        public GameObject EffectPrefab => effectPrefab;
        public AudioClip Sound => sound;
    }

    [CreateAssetMenu(
        menuName = "Landsong/Building/Building Presentation",
        fileName = "BuildingPresentation")]
    public sealed class BuildingPresentationDefinition : ScriptableObject
    {
        [SerializeField, InspectorName("施工 View")] private BuildingVisualAssetReference constructionView =
            new BuildingVisualAssetReference();

        [SerializeField, InspectorName("默认运营 View")] private BuildingVisualAssetReference defaultOperationalView =
            new BuildingVisualAssetReference();

        [SerializeField, InspectorName("等级与样式映射")] private BuildingViewMapping[] viewMappings =
            Array.Empty<BuildingViewMapping>();

        [SerializeField, InspectorName("视觉样式") ] private BuildingStyleDefinition[] styles =
            Array.Empty<BuildingStyleDefinition>();

        [SerializeField, InspectorName("施工完成过渡")] private BuildingTransitionDefinition constructionCompleteTransition =
            new BuildingTransitionDefinition();
        [SerializeField, InspectorName("默认升级过渡")] private BuildingTransitionDefinition defaultUpgradeTransition =
            new BuildingTransitionDefinition();

        public BuildingVisualAssetReference ConstructionView => constructionView;
        public BuildingVisualAssetReference DefaultOperationalView => defaultOperationalView;
        public IReadOnlyList<BuildingViewMapping> ViewMappings =>
            viewMappings ?? Array.Empty<BuildingViewMapping>();
        public IReadOnlyList<BuildingStyleDefinition> Styles =>
            styles ?? Array.Empty<BuildingStyleDefinition>();
        public BuildingTransitionDefinition ConstructionCompleteTransition =>
            constructionCompleteTransition;
        public BuildingTransitionDefinition DefaultUpgradeTransition =>
            defaultUpgradeTransition;

        public bool TryResolveView(
            BuildingLifecycleStage stage,
            int level,
            string styleId,
            out BuildingVisualAssetReference result)
        {
            result = null;
            styleId = string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
            if (stage == BuildingLifecycleStage.Construction)
            {
                result = constructionView;
                return result != null && result.IsConfigured;
            }

            var requestedLevel = Mathf.Max(1, level);
            var bestLevel = -1;
            if (viewMappings != null)
            {
                for (var i = 0; i < viewMappings.Length; i++)
                {
                    var mapping = viewMappings[i];
                    if (mapping == null
                        || mapping.Stage != BuildingLifecycleStage.Operational
                        || mapping.Level > requestedLevel
                        || !string.Equals(mapping.StyleId, styleId, StringComparison.Ordinal)
                        || mapping.View == null
                        || !mapping.View.IsConfigured
                        || mapping.Level <= bestLevel)
                    {
                        continue;
                    }

                    bestLevel = mapping.Level;
                    result = mapping.View;
                }
            }

            if (result != null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(styleId))
            {
                return false;
            }

            result = defaultOperationalView;
            return result != null && result.IsConfigured;
        }

        public bool HasStyle(string styleId)
        {
            styleId = string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
            if (string.IsNullOrEmpty(styleId))
            {
                return styles == null || styles.Length == 0;
            }

            for (var i = 0; i < Styles.Count; i++)
            {
                if (Styles[i] != null
                    && string.Equals(Styles[i].StyleId, styleId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public void Configure(
            IEnumerable<BuildingStyleDefinition> styleDefinitions,
            IEnumerable<BuildingViewMapping> mappings)
        {
            styles = styleDefinitions == null
                ? Array.Empty<BuildingStyleDefinition>()
                : new List<BuildingStyleDefinition>(styleDefinitions).ToArray();
            viewMappings = mappings == null
                ? Array.Empty<BuildingViewMapping>()
                : new List<BuildingViewMapping>(mappings).ToArray();
            EnsureDefaults();
        }

        public void ConfigureDefaultViews(
            GameObject constructionPrefab,
            GameObject operationalPrefab)
        {
            constructionView ??= new BuildingVisualAssetReference();
            defaultOperationalView ??= new BuildingVisualAssetReference();
            constructionView.Configure(constructionPrefab);
            defaultOperationalView.Configure(operationalPrefab);
            EnsureDefaults();
        }

        private void OnEnable()
        {
            EnsureDefaults();
        }

        private void OnValidate()
        {
            EnsureDefaults();
        }

        private void EnsureDefaults()
        {
            constructionView ??= new BuildingVisualAssetReference();
            defaultOperationalView ??= new BuildingVisualAssetReference();
            viewMappings ??= Array.Empty<BuildingViewMapping>();
            styles ??= Array.Empty<BuildingStyleDefinition>();
            constructionCompleteTransition ??= new BuildingTransitionDefinition();
            defaultUpgradeTransition ??= new BuildingTransitionDefinition();
        }
    }
}
