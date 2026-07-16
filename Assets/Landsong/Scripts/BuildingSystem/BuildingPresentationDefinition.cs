using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Landsong.BuildingSystem
{
    public enum BuildingConstructionViewMode
    {
        [InspectorName("单一施工视图")]
        Single = 0,

        [InspectorName("逐回合施工视图")]
        PerTurn = 10
    }

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

    [Serializable]
    public sealed class BuildingConstructionViewMapping
    {
        [SerializeField, InspectorName("施工回合"), LabelText("施工回合"), Min(1),
         Tooltip("从 1 开始；进入该施工回合时使用这条映射。不能超过建筑施工总回合数。")]
        private int turn = 1;

        [SerializeField, InspectorName("样式 ID"), LabelText("样式 ID"),
         Tooltip("留空表示该回合的通用施工表现；填写后优先匹配同 ID 的视觉样式。")]
        private string styleId;

        [SerializeField, InspectorName("视图资源"), LabelText("视图资源"),
         Tooltip("该施工回合使用的独立纯表现 Prefab，可使用直接引用或 Addressable 引用。")]
        private BuildingVisualAssetReference view = new BuildingVisualAssetReference();

        public BuildingConstructionViewMapping()
        {
        }

        public BuildingConstructionViewMapping(
            int turn,
            string styleId,
            GameObject directPrefab = null)
        {
            this.turn = Mathf.Max(1, turn);
            this.styleId = string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
            view = new BuildingVisualAssetReference();
            view.Configure(directPrefab);
        }

        public int Turn => turn;
        public string StyleId => string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
        public BuildingVisualAssetReference View => view;
    }

    [CreateAssetMenu(
        menuName = "Landsong/Building/Building Presentation",
        fileName = "BuildingPresentation")]
    public sealed class BuildingPresentationDefinition : ScriptableObject
    {
        [SerializeField, InspectorName("施工视图模式"), LabelText("施工视图模式"), EnumToggleButtons,
         Tooltip("单一施工视图在整个施工阶段保持同一表现；逐回合施工视图按当前施工回合选择表现。")]
        private BuildingConstructionViewMode constructionViewMode =
            BuildingConstructionViewMode.Single;

        [SerializeField, InspectorName("施工视图"), LabelText("施工视图"),
         ShowIf(nameof(IsSingleConstructionView)),
         Tooltip("单一施工视图模式使用；从施工开始到完工始终保持这个独立纯表现 Prefab。")]
        private BuildingVisualAssetReference constructionView =
            new BuildingVisualAssetReference();

        [SerializeField, InspectorName("逐回合施工视图"), LabelText("逐回合施工视图"),
         ShowIf(nameof(IsPerTurnConstructionView)),
         Tooltip("逐回合施工视图模式使用；按施工回合和可选 StyleId 选择独立视图。缺失回合使用占位表现，不回退到单一施工视图。")]
        private BuildingConstructionViewMapping[] constructionViewMappings =
            Array.Empty<BuildingConstructionViewMapping>();

        [SerializeField, InspectorName("放置预览视图"), LabelText("放置预览视图")] private BuildingVisualAssetReference placementPreviewView =
            new BuildingVisualAssetReference();

        [SerializeField, InspectorName("默认运营视图"), LabelText("默认运营视图")] private BuildingVisualAssetReference defaultOperationalView =
            new BuildingVisualAssetReference();

        [SerializeField, InspectorName("视图映射"), LabelText("视图映射")] private BuildingViewMapping[] viewMappings =
            Array.Empty<BuildingViewMapping>();

        [SerializeField, InspectorName("视觉样式"), LabelText("视觉样式")] private BuildingStyleDefinition[] styles =
            Array.Empty<BuildingStyleDefinition>();

        public BuildingConstructionViewMode ConstructionViewMode => constructionViewMode;
        public BuildingVisualAssetReference ConstructionView => constructionView;
        public IReadOnlyList<BuildingConstructionViewMapping> ConstructionViewMappings =>
            constructionViewMappings ?? Array.Empty<BuildingConstructionViewMapping>();
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
                return TryResolveConstructionView(1, styleId, out result);
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

        public bool TryResolveConstructionView(
            int turn,
            string styleId,
            out BuildingVisualAssetReference result)
        {
            result = null;
            if (constructionViewMode == BuildingConstructionViewMode.Single)
            {
                result = constructionView;
                return result != null && result.IsConfigured;
            }

            if (constructionViewMode != BuildingConstructionViewMode.PerTurn)
            {
                return false;
            }

            turn = Mathf.Max(1, turn);
            styleId = string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();

            BuildingVisualAssetReference defaultStyleResult = null;
            if (constructionViewMappings != null)
            {
                for (var i = 0; i < constructionViewMappings.Length; i++)
                {
                    var mapping = constructionViewMappings[i];
                    if (mapping == null
                        || mapping.Turn != turn
                        || mapping.View == null
                        || !mapping.View.IsConfigured)
                    {
                        continue;
                    }

                    if (string.Equals(mapping.StyleId, styleId, StringComparison.Ordinal))
                    {
                        result = mapping.View;
                        return true;
                    }

                    if (string.IsNullOrEmpty(mapping.StyleId))
                    {
                        defaultStyleResult = mapping.View;
                    }
                }
            }

            if (defaultStyleResult != null)
            {
                result = defaultStyleResult;
                return true;
            }

            return false;
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

        public void ConfigureConstructionViews(
            BuildingConstructionViewMode mode,
            IEnumerable<BuildingConstructionViewMapping> mappings)
        {
            constructionViewMode = mode;
            constructionViewMappings = mappings == null
                ? Array.Empty<BuildingConstructionViewMapping>()
                : new List<BuildingConstructionViewMapping>(mappings).ToArray();
            EnsureDefaults();
        }

        public void ConfigureDefaultViews(
            GameObject constructionPrefab,
            GameObject placementPreviewPrefab,
            GameObject operationalPrefab)
        {
            constructionView ??= new BuildingVisualAssetReference();
            constructionViewMappings ??= Array.Empty<BuildingConstructionViewMapping>();
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
            if (!Enum.IsDefined(typeof(BuildingConstructionViewMode), constructionViewMode))
            {
                constructionViewMode = BuildingConstructionViewMode.Single;
            }

            constructionView ??= new BuildingVisualAssetReference();
            constructionViewMappings ??= Array.Empty<BuildingConstructionViewMapping>();
            placementPreviewView ??= new BuildingVisualAssetReference();
            defaultOperationalView ??= new BuildingVisualAssetReference();
            viewMappings ??= Array.Empty<BuildingViewMapping>();
            styles ??= Array.Empty<BuildingStyleDefinition>();
        }

        private bool IsSingleConstructionView =>
            constructionViewMode == BuildingConstructionViewMode.Single;

        private bool IsPerTurnConstructionView =>
            constructionViewMode == BuildingConstructionViewMode.PerTurn;
    }
}
