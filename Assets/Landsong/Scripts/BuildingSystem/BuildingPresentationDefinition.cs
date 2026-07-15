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
        [SerializeField, InspectorName("直接视图预制体"), LabelText("直接视图预制体")]
        private GameObject directPrefab;

        [SerializeField, InspectorName("可寻址视图预制体"), LabelText("可寻址视图预制体")]
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
        [SerializeField, InspectorName("样式 ID"), LabelText("样式 ID")] private string styleId;
        [SerializeField, InspectorName("显示名称"), LabelText("显示名称")] private string displayName;
        [SerializeField, InspectorName("样式图标"), LabelText("样式图标")] private Sprite icon;

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
        [SerializeField, InspectorName("运营等级"), LabelText("运营等级"), Min(1)] private int level = 1;
        [SerializeField, InspectorName("样式 ID"), LabelText("样式 ID")] private string styleId;
        [SerializeField, InspectorName("视图资源"), LabelText("视图资源")] private BuildingVisualAssetReference view = new BuildingVisualAssetReference();

        public BuildingViewMapping()
        {
        }

        public BuildingViewMapping(
            int level,
            string styleId,
            GameObject directPrefab = null)
        {
            this.level = Mathf.Max(1, level);
            this.styleId = string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
            view = new BuildingVisualAssetReference();
            view.Configure(directPrefab);
        }

        public int Level => Mathf.Max(1, level);
        public string StyleId => string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
        public BuildingVisualAssetReference View => view;
    }

    [CreateAssetMenu(
        menuName = "Landsong/Building/Building Presentation",
        fileName = "BuildingPresentation")]
    public sealed class BuildingPresentationDefinition : ScriptableObject
    {
        [SerializeField, InspectorName("施工视图"), LabelText("施工视图")] private BuildingVisualAssetReference constructionView =
            new BuildingVisualAssetReference();

        [SerializeField, InspectorName("放置预览视图"), LabelText("放置预览视图")] private BuildingVisualAssetReference placementPreviewView =
            new BuildingVisualAssetReference();

        [SerializeField, InspectorName("默认运营视图"), LabelText("默认运营视图")] private BuildingVisualAssetReference defaultOperationalView =
            new BuildingVisualAssetReference();

        [SerializeField, InspectorName("视图映射"), LabelText("视图映射")] private BuildingViewMapping[] viewMappings =
            Array.Empty<BuildingViewMapping>();

        [SerializeField, InspectorName("视觉样式"), LabelText("视觉样式")] private BuildingStyleDefinition[] styles =
            Array.Empty<BuildingStyleDefinition>();

        public BuildingVisualAssetReference ConstructionView => constructionView;
        public BuildingVisualAssetReference PlacementPreviewView => placementPreviewView;
        public BuildingVisualAssetReference DefaultOperationalView => defaultOperationalView;
        public IReadOnlyList<BuildingViewMapping> ViewMappings =>
            viewMappings ?? Array.Empty<BuildingViewMapping>();
        public IReadOnlyList<BuildingStyleDefinition> Styles =>
            styles ?? Array.Empty<BuildingStyleDefinition>();

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

        public bool TryResolvePlacementPreview(
            int level,
            string styleId,
            out BuildingVisualAssetReference result)
        {
            result = placementPreviewView;
            if (result != null && result.IsConfigured)
            {
                return true;
            }

            return TryResolveView(
                BuildingLifecycleStage.Operational,
                Mathf.Max(1, level),
                styleId,
                out result);
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
            GameObject placementPreviewPrefab,
            GameObject operationalPrefab)
        {
            constructionView ??= new BuildingVisualAssetReference();
            placementPreviewView ??= new BuildingVisualAssetReference();
            defaultOperationalView ??= new BuildingVisualAssetReference();
            constructionView.Configure(constructionPrefab);
            placementPreviewView.Configure(placementPreviewPrefab);
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
            placementPreviewView ??= new BuildingVisualAssetReference();
            defaultOperationalView ??= new BuildingVisualAssetReference();
            viewMappings ??= Array.Empty<BuildingViewMapping>();
            styles ??= Array.Empty<BuildingStyleDefinition>();
        }
    }
}
