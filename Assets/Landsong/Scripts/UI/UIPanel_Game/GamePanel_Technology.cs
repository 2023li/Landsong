using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Landsong.BuildingSystem;
using Landsong.TechnologySystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Landsong.UISystem
{
    public sealed class GamePanel_Technology : MonoBehaviour
    {
        [SerializeField, LabelText("关闭按钮")]
        [Tooltip("点击后关闭科技面板并返回 HUD。")]
        private Button closeButton;

        [SerializeField, LabelText("科技节点根节点")]
        [Tooltip("手动摆放的 GamePanel_TechnologyNodeItem 会从这个节点下扫描。为空时扫描当前面板自身。")]
        private Transform nodeRoot;

        [SerializeField, LabelText("科技树滚动视图")]
        [Tooltip("当前研究科技发生变化或面板重新打开时，自动把对应节点滚动到可见区域。")]
        private ScrollRect technologyScrollRect;

        [SerializeField, LabelText("编辑器预览科技目录")]
        [Tooltip("在 Prefab 编辑模式点击生成按钮时使用；不影响运行时 TechnologyService 的目录。")]
        private TechnologyCatalog editorPreviewCatalog;

        [SerializeField, LabelText("编辑器预览建筑目录")]
        [Tooltip("生成科技树预览时反向读取建筑解锁与等级升级条件，用于显示建筑图标和 LV 标记。")]
        private BuildingCatalog editorPreviewBuildingCatalog;

        [SerializeField, LabelText("编辑器预览全局 Buff 目录")]
        [Tooltip("生成科技树预览时读取科技激活的全局 Buff，用 Buff 自己的图标显示解锁内容。未配置时自动读取项目正式目录。")]
        private TechnologyGlobalBuffCatalog editorPreviewGlobalBuffCatalog;

        [SerializeField, LabelText("编辑器预览额外内容生产者")]
        [Tooltip("实现 ITechnologyUnlockContentProducer 的领域目录。生成预览前由生产者主动注入本地注册表。")]
        private ScriptableObject[] editorPreviewUnlockContentProducers = Array.Empty<ScriptableObject>();

        [SerializeField, LabelText("科技节点预制体")]
        [Tooltip("必须带有 GamePanel_TechnologyNodeItem，用于生成真实科技节点。")]
        private GamePanel_TechnologyNodeItem nodePrefab;

        [SerializeField, LabelText("空节点预制体")]
        [Tooltip("某一列存在科技但当前行没有科技时，用它补齐布局。")]
        private RectTransform emptyNodePrefab;

        [SerializeField, LabelText("列间隙预制体")]
        [Tooltip("插入在每两个科技列之间，为节点和连线预留空间。")]
        private RectTransform columnGapPrefab;

        [SerializeField, LabelText("科技连线颜色")]
        private Color connectionColor = new Color(0.72f, 0.62f, 0.26f, 0.9f);

        [SerializeField, Min(1f), LabelText("科技连线宽度")]
        private float connectionWidth = 8f;

        [SerializeField, LabelText("详情-科技名称文本")]
        [Tooltip("显示当前选中科技节点的名称。")]
        private TMP_Text detailNameLabel;

        [SerializeField, LabelText("详情-科技描述文本")]
        [Tooltip("显示当前选中科技节点的描述。")]
        private TMP_Text detailDescriptionLabel;

        [SerializeField, LabelText("详情-研究需求文本")]
        [Tooltip("显示当前选中科技节点完成研究需要累计注入的科技点。")]
        private TMP_Text detailCostLabel;

        [SerializeField, LabelText("详情-前置科技文本")]
        [Tooltip("显示当前选中科技节点依赖的前置科技列表。")]
        private TMP_Text detailPrerequisitesLabel;

        [SerializeField, LabelText("详情-研究状态文本")]
        [Tooltip("显示当前选中科技节点是否可研究、研究中、已研究或前置科技未完成。")]
        private TMP_Text detailStatusLabel;

        private readonly List<GamePanel_TechnologyNodeItem> nodeItems = new List<GamePanel_TechnologyNodeItem>();
        private readonly Dictionary<TechnologyDefinition, RectTransform> generatedNodeRects =
            new Dictionary<TechnologyDefinition, RectTransform>();
        private readonly RectTransform[] generatedRows = new RectTransform[TechnologyNodeId.MaximumRow];
        private readonly List<TechnologyUnlockContent> detailUnlockContents =
            new List<TechnologyUnlockContent>();
        private readonly TechnologyUnlockContentRegistry editorPreviewUnlockContentRegistry =
            new TechnologyUnlockContentRegistry();
        private TechnologyUnlockContentRegistry unlockContentRegistry;
        private UIPanel_Game gamePanel;
        private TechnologyService technology;
        private TechnologyDefinition selectedDefinition;
        private TechnologyTreeConnectionGraphic connectionGraphic;
        private bool subscribedToTechnology;
        private bool subscribedToUnlockContents;
        private string lastAutoScrolledTechnologyId = string.Empty;
        private Coroutine autoScrollRoutine;

        private void Reset()
        {
            nodeRoot = transform;
        }

        private void Awake()
        {
            ResolveReferences();
            BindButtons();
        }

        private void OnEnable()
        {
            lastAutoScrolledTechnologyId = string.Empty;
            ResolveRuntime();
            SubscribeTechnology();
            Refresh();
            ScheduleScrollToCurrentResearch(true);
        }

        private void OnDisable()
        {
            StopAutoScrollRoutine();
            UnsubscribeTechnology();
            UnsubscribeUnlockContents();
        }

        private void OnDestroy()
        {
            StopAutoScrollRoutine();
            UnbindButtons();
            UnsubscribeTechnology();
            UnsubscribeUnlockContents();
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            ResolveRuntime();
            RefreshNodes();
            RefreshDetail();
        }

        [Button("按节点 ID 生成科技树 UI", ButtonSizes.Large)]
        public void GenerateTechnologyTreeUI()
        {
            ResolveReferences();

            TechnologyCatalog catalog;
            if (Application.isPlaying)
            {
                ResolveRuntime();
                catalog = technology?.Catalog;
            }
            else
            {
                catalog = editorPreviewCatalog;
            }

            if (catalog == null)
            {
                Debug.LogError(
                    Application.isPlaying
                        ? "科技服务没有可用的 TechnologyCatalog。"
                        : "请先配置编辑器预览科技目录。",
                    this);
                return;
            }

            if (!Application.isPlaying)
            {
                ResolveEditorPreviewUnlockContentSources();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.SetCurrentGroupName("按节点 ID 生成科技树预览");
                if (nodeRoot != null)
                {
                    Undo.RegisterFullObjectHierarchyUndo(nodeRoot.gameObject, "生成科技树预览");
                }
            }
#endif

            GenerateTechnologyTree(catalog.Definitions);

            if (Application.isPlaying)
            {
                RefreshNodes();
                RefreshDetail();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                MarkEditorPreviewDirty();
            }
#endif
        }

#if UNITY_EDITOR
        [Button("清除编辑器预览")]
        private void ClearEditorPreview()
        {
            if (Application.isPlaying)
            {
                return;
            }

            ResolveReferences();
            ResolveTechnologyRows();
            if (nodeRoot != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(nodeRoot.gameObject, "清除科技树预览");
            }

            for (var i = 0; i < generatedRows.Length; i++)
            {
                ClearRow(generatedRows[i]);
            }

            generatedNodeRects.Clear();
            connectionGraphic?.ClearConnections();
            MarkEditorPreviewDirty();
        }

        private void MarkEditorPreviewDirty()
        {
            EditorUtility.SetDirty(this);
            if (nodeRoot != null)
            {
                EditorUtility.SetDirty(nodeRoot);
                PrefabUtility.RecordPrefabInstancePropertyModifications(nodeRoot);
            }

            if (connectionGraphic != null)
            {
                EditorUtility.SetDirty(connectionGraphic);
                PrefabUtility.RecordPrefabInstancePropertyModifications(connectionGraphic);
            }

            SceneView.RepaintAll();
        }
#endif

        private void ResolveReferences()
        {
            if (gamePanel == null)
            {
                gamePanel = GetComponentInParent<UIPanel_Game>();
            }

            if (nodeRoot == null || nodeRoot == transform)
            {
                var scrollRects = GetComponentsInChildren<ScrollRect>(true);
                for (var i = 0; i < scrollRects.Length; i++)
                {
                    var scrollRect = scrollRects[i];
                    if (scrollRect != null
                        && scrollRect.content != null
                        && string.Equals(scrollRect.name, "科技面板滚动视图", StringComparison.Ordinal))
                    {
                        nodeRoot = scrollRect.content;
                        technologyScrollRect = scrollRect;
                        break;
                    }
                }

                nodeRoot ??= transform;
            }

            if (technologyScrollRect == null)
            {
                var scrollRects = GetComponentsInChildren<ScrollRect>(true);
                for (var i = 0; i < scrollRects.Length; i++)
                {
                    var scrollRect = scrollRects[i];
                    if (scrollRect != null
                        && scrollRect.content == nodeRoot)
                    {
                        technologyScrollRect = scrollRect;
                        break;
                    }
                }
            }
        }

        private void ResolveRuntime()
        {
            var gameSystem = Landsong.GameSystem.Instance;
            technology = gameSystem == null ? null : gameSystem.Services.Technology;
            SetUnlockContentRegistry(gameSystem?.Services.TechnologyUnlockContents);
        }

        private void ResolveEditorPreviewUnlockContentSources()
        {
            editorPreviewUnlockContentRegistry.Clear();
            TechnologyUnlockContentRegistry.InjectCompletionEffects(
                editorPreviewCatalog,
                editorPreviewUnlockContentRegistry);
            var buildingCatalog = editorPreviewBuildingCatalog;
#if UNITY_EDITOR
            if (buildingCatalog == null)
            {
                buildingCatalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(
                    "Assets/Landsong/Objects/SO/BuildingCatalog.asset");
            }
#endif
            buildingCatalog?.InjectTechnologyUnlockContents(editorPreviewUnlockContentRegistry);

            var globalBuffCatalog = editorPreviewGlobalBuffCatalog;
#if UNITY_EDITOR
            if (globalBuffCatalog == null)
            {
                globalBuffCatalog = AssetDatabase.LoadAssetAtPath<TechnologyGlobalBuffCatalog>(
                    "Assets/Landsong/Objects/SO/TechnologyGlobalBuffCatalog.asset");
            }
#endif
            globalBuffCatalog?.InjectTechnologyUnlockContents(editorPreviewUnlockContentRegistry);

            if (editorPreviewUnlockContentProducers != null)
            {
                for (var i = 0; i < editorPreviewUnlockContentProducers.Length; i++)
                {
                    if (editorPreviewUnlockContentProducers[i] is ITechnologyUnlockContentProducer producer)
                    {
                        producer.InjectTechnologyUnlockContents(editorPreviewUnlockContentRegistry);
                    }
                }
            }

            SetUnlockContentRegistry(editorPreviewUnlockContentRegistry);
        }

        private void SetUnlockContentRegistry(TechnologyUnlockContentRegistry registry)
        {
            if (ReferenceEquals(unlockContentRegistry, registry))
            {
                if (!subscribedToUnlockContents && isActiveAndEnabled && unlockContentRegistry != null)
                {
                    unlockContentRegistry.Changed += HandleUnlockContentsChanged;
                    subscribedToUnlockContents = true;
                }
                return;
            }

            UnsubscribeUnlockContents();
            unlockContentRegistry = registry;
            if (isActiveAndEnabled && unlockContentRegistry != null)
            {
                unlockContentRegistry.Changed += HandleUnlockContentsChanged;
                subscribedToUnlockContents = true;
            }
        }

        private void UnsubscribeUnlockContents()
        {
            if (subscribedToUnlockContents && unlockContentRegistry != null)
            {
                unlockContentRegistry.Changed -= HandleUnlockContentsChanged;
            }

            subscribedToUnlockContents = false;
        }

        private void HandleUnlockContentsChanged(TechnologyUnlockContentRegistry registry)
        {
            RefreshNodes();
            RefreshDetail();
        }

        private void BindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
                closeButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        private void UnbindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }

        private void SubscribeTechnology()
        {
            if (subscribedToTechnology || technology == null)
            {
                return;
            }

            technology.StateChanged += HandleTechnologyChanged;
            subscribedToTechnology = true;
        }

        private void UnsubscribeTechnology()
        {
            if (!subscribedToTechnology || technology == null)
            {
                subscribedToTechnology = false;
                return;
            }

            technology.StateChanged -= HandleTechnologyChanged;
            subscribedToTechnology = false;
        }

        private void RefreshNodes()
        {
            CollectExistingNodeItems();

            var selectedNodeExists = false;
            TechnologyDefinition firstDefinition = null;
            for (var i = 0; i < nodeItems.Count; i++)
            {
                var item = nodeItems[i];
                var definition = item == null ? null : item.Definition;

                if (definition == null)
                {
                    continue;
                }

                firstDefinition ??= definition;
                if (definition == selectedDefinition)
                {
                    selectedNodeExists = true;
                }
            }

            if (!selectedNodeExists)
            {
                selectedDefinition = firstDefinition;
            }

            for (var i = 0; i < nodeItems.Count; i++)
            {
                var item = nodeItems[i];
                if (item != null)
                {
                    item.Bind(
                        technology,
                        unlockContentRegistry,
                        HandleNodeClicked,
                        item.Definition != null && item.Definition == selectedDefinition);
                }
            }
        }

        private void RefreshDetail()
        {
            if (selectedDefinition == null)
            {
                SetText(detailNameLabel, "未选择科技");
                SetText(detailDescriptionLabel, string.Empty);
                SetText(detailCostLabel, string.Empty);
                SetText(detailPrerequisitesLabel, string.Empty);
                SetText(detailStatusLabel, technology == null ? "科技服务未初始化" : "没有可显示的科技节点");
                return;
            }

            SetText(detailNameLabel, selectedDefinition.DisplayName);
            SetText(detailDescriptionLabel, FormatDetailDescription(selectedDefinition));
            SetText(detailCostLabel, $"研究需求：{selectedDefinition.SciencePointCost} 科技点");
            SetText(detailPrerequisitesLabel, FormatPrerequisites(selectedDefinition));
            SetText(detailStatusLabel, FormatSelectedStatus());
        }

        private string FormatDetailDescription(TechnologyDefinition definition)
        {
            detailUnlockContents.Clear();
            unlockContentRegistry?.Collect(definition, detailUnlockContents);
            var description = definition == null || string.IsNullOrWhiteSpace(definition.Description)
                ? string.Empty
                : definition.Description.Trim();
            if (detailUnlockContents.Count == 0)
            {
                return description;
            }

            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(description))
            {
                builder.AppendLine(description);
                builder.AppendLine();
            }

            builder.AppendLine("解锁与完成效果");
            for (var i = 0; i < detailUnlockContents.Count; i++)
            {
                var content = detailUnlockContents[i];
                builder.Append("• ");
                builder.Append(content.DisplayName);
                if (content.Amount > 1)
                {
                    builder.Append(" ×");
                    builder.Append(content.Amount);
                }

                if (i < detailUnlockContents.Count - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private void CollectExistingNodeItems()
        {
            if (nodeRoot == null)
            {
                return;
            }

            nodeItems.Clear();
            var existingItems = nodeRoot.GetComponentsInChildren<GamePanel_TechnologyNodeItem>(true);
            for (var i = 0; i < existingItems.Length; i++)
            {
                var item = existingItems[i];
                if (item != null && !nodeItems.Contains(item))
                {
                    nodeItems.Add(item);
                }
            }
        }

        private void GenerateTechnologyTree(IReadOnlyList<TechnologyDefinition> definitions)
        {
            ResolveTechnologyRows();

            var definitionsByPosition = new Dictionary<Vector2Int, TechnologyDefinition>();
            var maximumColumn = 0;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || !definition.TryGetNodePosition(out var row, out var column))
                {
                    Debug.LogWarning(
                        $"跳过科技节点 '{definition?.name ?? "<null>"}'：ID 必须符合 TN_行_列_名称，且行号只能是 1-5。",
                        this);
                    continue;
                }

                var position = new Vector2Int(column, row);
                if (definitionsByPosition.ContainsKey(position))
                {
                    Debug.LogWarning($"科技树位置 {row} 行 {column} 列重复，保留先出现的节点。", this);
                    continue;
                }

                definitionsByPosition.Add(position, definition);
                maximumColumn = Mathf.Max(maximumColumn, column);
            }

            generatedNodeRects.Clear();
            for (var row = TechnologyNodeId.MinimumRow; row <= TechnologyNodeId.MaximumRow; row++)
            {
                var rowRoot = generatedRows[row - 1];
                ClearRow(rowRoot);

                for (var column = 1; column <= maximumColumn; column++)
                {
                    var position = new Vector2Int(column, row);
                    if (definitionsByPosition.TryGetValue(position, out var definition))
                    {
                        CreateTechnologyNode(rowRoot, definition, row, column);
                    }
                    else
                    {
                        CreateLayoutCell(emptyNodePrefab, rowRoot, $"空节点_{row}_{column}");
                    }

                    if (column < maximumColumn)
                    {
                        CreateLayoutCell(columnGapPrefab, rowRoot, $"间隙_{row}_{column}_{column + 1}");
                    }
                }
            }

            RebuildGeneratedLayout();
            RebuildConnectionGraphic(definitions);
        }

        private void ResolveTechnologyRows()
        {
            Array.Clear(generatedRows, 0, generatedRows.Length);
            for (var childIndex = 0; childIndex < nodeRoot.childCount; childIndex++)
            {
                if (nodeRoot.GetChild(childIndex) is not RectTransform child)
                {
                    continue;
                }

                for (var row = TechnologyNodeId.MinimumRow; row <= TechnologyNodeId.MaximumRow; row++)
                {
                    if (child.name.EndsWith($"{row}行", StringComparison.Ordinal))
                    {
                        generatedRows[row - 1] = child;
                        break;
                    }
                }
            }

            for (var row = TechnologyNodeId.MinimumRow; row <= TechnologyNodeId.MaximumRow; row++)
            {
                var rowRoot = generatedRows[row - 1];
                if (rowRoot == null)
                {
                    var rowObject = new GameObject($"——{row}行", typeof(RectTransform));
                    rowRoot = (RectTransform)rowObject.transform;
                    rowRoot.SetParent(nodeRoot, false);
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        Undo.RegisterCreatedObjectUndo(rowObject, "创建科技树行");
                    }
#endif
                    generatedRows[row - 1] = rowRoot;
                }

                ConfigureRow(rowRoot, row);
            }
        }

        private static void ConfigureRow(RectTransform rowRoot, int row)
        {
            rowRoot.anchorMin = new Vector2(0f, 1f);
            rowRoot.anchorMax = new Vector2(0f, 1f);
            rowRoot.pivot = new Vector2(0f, 1f);
            var height = rowRoot.rect.height > 1f ? rowRoot.rect.height : 216.6f;
            rowRoot.anchoredPosition = new Vector2(0f, -(row - 1) * height);
            rowRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            var layout = rowRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
#if UNITY_EDITOR
                layout = Application.isPlaying
                    ? rowRoot.gameObject.AddComponent<HorizontalLayoutGroup>()
                    : Undo.AddComponent<HorizontalLayoutGroup>(rowRoot.gameObject);
#else
                layout = rowRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
#endif
            }

            layout.padding = new RectOffset(0, 0, 20, 20);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var fitter = rowRoot.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
#if UNITY_EDITOR
                fitter = Application.isPlaying
                    ? rowRoot.gameObject.AddComponent<ContentSizeFitter>()
                    : Undo.AddComponent<ContentSizeFitter>(rowRoot.gameObject);
#else
                fitter = rowRoot.gameObject.AddComponent<ContentSizeFitter>();
#endif
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        private static void ClearRow(RectTransform rowRoot)
        {
            if (rowRoot == null)
            {
                return;
            }

            for (var childIndex = rowRoot.childCount - 1; childIndex >= 0; childIndex--)
            {
                var child = rowRoot.GetChild(childIndex);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                    continue;
                }
#endif
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        private void CreateTechnologyNode(
            RectTransform rowRoot,
            TechnologyDefinition definition,
            int row,
            int column)
        {
            if (nodePrefab == null)
            {
                Debug.LogError("科技面板没有配置科技节点预制体，无法自动生成科技树。", this);
                return;
            }

            GamePanel_TechnologyNodeItem item;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var instance = PrefabUtility.InstantiatePrefab(nodePrefab.gameObject, rowRoot) as GameObject;
                item = instance == null ? null : instance.GetComponent<GamePanel_TechnologyNodeItem>();
                if (instance != null)
                {
                    Undo.RegisterCreatedObjectUndo(instance, "生成科技节点");
                }
            }
            else
#endif
            {
                item = Instantiate(nodePrefab, rowRoot, false);
            }

            if (item == null)
            {
                Debug.LogError("科技节点预制体缺少 GamePanel_TechnologyNodeItem。", this);
                return;
            }

            item.name = $"科技_{row}_{column}_{definition.DisplayName}";
            item.SetDefinition(definition);
            item.Bind(
                Application.isPlaying ? technology : null,
                unlockContentRegistry,
                Application.isPlaying ? HandleNodeClicked : null,
                false);
            generatedNodeRects[definition] = (RectTransform)item.transform;
        }

        private static void CreateLayoutCell(RectTransform prefab, RectTransform rowRoot, string cellName)
        {
            if (prefab == null || rowRoot == null)
            {
                return;
            }

            RectTransform cell;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab.gameObject, rowRoot) as GameObject;
                cell = instance == null ? null : instance.transform as RectTransform;
                if (instance != null)
                {
                    Undo.RegisterCreatedObjectUndo(instance, "生成科技树布局节点");
                }
            }
            else
#endif
            {
                cell = Instantiate(prefab, rowRoot, false);
            }

            if (cell == null)
            {
                return;
            }

            cell.name = cellName;
        }

        private void RebuildGeneratedLayout()
        {
            Canvas.ForceUpdateCanvases();

            var maximumWidth = 0f;
            var totalHeight = 0f;
            for (var i = 0; i < generatedRows.Length; i++)
            {
                var row = generatedRows[i];
                if (row == null)
                {
                    continue;
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(row);
                maximumWidth = Mathf.Max(maximumWidth, LayoutUtility.GetPreferredWidth(row));
                totalHeight += row.rect.height;
            }

            if (nodeRoot is RectTransform content)
            {
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maximumWidth);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }

            Canvas.ForceUpdateCanvases();
        }

        private void RebuildConnectionGraphic(IReadOnlyList<TechnologyDefinition> definitions)
        {
            EnsureConnectionGraphic();
            if (connectionGraphic == null)
            {
                return;
            }

            connectionGraphic.color = connectionColor;
            connectionGraphic.SetLineWidth(connectionWidth);
            connectionGraphic.ClearConnections();

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || !generatedNodeRects.TryGetValue(definition, out var target))
                {
                    continue;
                }

                var prerequisites = definition.Prerequisites;
                for (var prerequisiteIndex = 0; prerequisiteIndex < prerequisites.Count; prerequisiteIndex++)
                {
                    var prerequisite = prerequisites[prerequisiteIndex];
                    if (prerequisite != null
                        && generatedNodeRects.TryGetValue(prerequisite, out var source))
                    {
                        connectionGraphic.AddConnection(source, target);
                    }
                }
            }

            connectionGraphic.SetVerticesDirty();
        }

        private void EnsureConnectionGraphic()
        {
            if (connectionGraphic != null || nodeRoot == null)
            {
                return;
            }

            var existing = nodeRoot.Find("科技连线");
            if (existing != null)
            {
                connectionGraphic = existing.GetComponent<TechnologyTreeConnectionGraphic>();
            }

            if (connectionGraphic == null)
            {
                var lineObject = new GameObject("科技连线", typeof(RectTransform), typeof(LayoutElement));
                var lineRect = (RectTransform)lineObject.transform;
                lineRect.SetParent(nodeRoot, false);
                lineRect.anchorMin = Vector2.zero;
                lineRect.anchorMax = Vector2.one;
                lineRect.offsetMin = Vector2.zero;
                lineRect.offsetMax = Vector2.zero;
                lineObject.GetComponent<LayoutElement>().ignoreLayout = true;
                connectionGraphic = lineObject.AddComponent<TechnologyTreeConnectionGraphic>();
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Undo.RegisterCreatedObjectUndo(lineObject, "创建科技连线图层");
                }
#endif
            }

            connectionGraphic.raycastTarget = false;
            connectionGraphic.transform.SetAsFirstSibling();
        }

        private string FormatSelectedStatus()
        {
            if (technology == null)
            {
                return "科技服务未初始化";
            }

            if (technology.IsCurrentResearch(selectedDefinition))
            {
                return selectedDefinition.AllowRepeatResearch && technology.IsUnlocked(selectedDefinition.TechnologyId)
                    ? $"重复研究中：{FormatResearchProgress(selectedDefinition)}"
                    : $"研究中：{FormatResearchProgress(selectedDefinition)}";
            }

            if (technology.IsQueuedResearch(selectedDefinition))
            {
                return "已加入研发队列";
            }

            if (technology.IsUnlocked(selectedDefinition.TechnologyId) && !selectedDefinition.AllowRepeatResearch)
            {
                return "已研究";
            }

            if (technology.CanStartResearch(selectedDefinition, out var reason))
            {
                if (selectedDefinition.AllowRepeatResearch && technology.IsUnlocked(selectedDefinition.TechnologyId))
                {
                    return technology.HasCurrentResearch
                        ? "可重复研究，点击节点切换当前研究"
                        : "可重复研究，点击节点开始研究";
                }

                return technology.HasCurrentResearch
                    ? "可研究，点击节点切换当前研究"
                    : "可研究，点击节点开始研究";
            }

            return reason switch
            {
                TechnologyResearchFailureReason.PrerequisitesLocked => "前置科技未完成，点击节点加入研发队列",
                TechnologyResearchFailureReason.AlreadyUnlocked => "已研究",
                TechnologyResearchFailureReason.InvalidTechnology => "科技配置无效",
                _ => "不可研究"
            };
        }

        private string FormatResearchProgress(TechnologyDefinition definition)
        {
            if (definition == null)
            {
                return "0/0";
            }

            var progress = technology == null ? 0 : technology.GetResearchProgress(definition);
            var required = Mathf.Max(0, definition.SciencePointCost);
            return required <= 0 ? "无需科技点" : $"{progress}/{required}";
        }

        private static string FormatPrerequisites(TechnologyDefinition definition)
        {
            var prerequisites = definition.Prerequisites;
            if (prerequisites.Count == 0)
            {
                return "前置：无";
            }

            var names = new List<string>(prerequisites.Count);
            for (var i = 0; i < prerequisites.Count; i++)
            {
                var prerequisite = prerequisites[i];
                if (prerequisite != null)
                {
                    names.Add(prerequisite.DisplayName);
                }
            }

            return names.Count == 0 ? "前置：无" : $"前置：{string.Join("、", names)}";
        }

        private void HandleNodeClicked(TechnologyDefinition definition)
        {
            selectedDefinition = definition;

            if (technology != null
                && selectedDefinition != null)
            {
                technology.TryQueueResearchPath(selectedDefinition);
            }

            Refresh();
        }

        private void HandleTechnologyChanged(TechnologyService changedTechnology)
        {
            technology = changedTechnology;
            Refresh();
            ScheduleScrollToCurrentResearch(false);
        }

        private void ScheduleScrollToCurrentResearch(bool force)
        {
            if (!isActiveAndEnabled || technologyScrollRect == null || technology == null)
            {
                return;
            }

            var current = technology.CurrentResearchDefinition;
            var technologyId = current == null ? string.Empty : current.TechnologyId;
            if (string.IsNullOrWhiteSpace(technologyId))
            {
                lastAutoScrolledTechnologyId = string.Empty;
                return;
            }

            if (!force && string.Equals(lastAutoScrolledTechnologyId, technologyId, StringComparison.Ordinal))
            {
                return;
            }

            StopAutoScrollRoutine();
            autoScrollRoutine = StartCoroutine(ScrollCurrentResearchIntoViewRoutine(current, technologyId));
        }

        private IEnumerator ScrollCurrentResearchIntoViewRoutine(
            TechnologyDefinition current,
            string technologyId)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            CollectExistingNodeItems();

            GamePanel_TechnologyNodeItem targetItem = null;
            for (var i = 0; i < nodeItems.Count; i++)
            {
                var item = nodeItems[i];
                if (item != null && item.Definition == current)
                {
                    targetItem = item;
                    break;
                }
            }

            if (targetItem != null)
            {
                ScrollIntoView(targetItem.transform as RectTransform);
                lastAutoScrolledTechnologyId = technologyId;
            }

            autoScrollRoutine = null;
        }

        private void ScrollIntoView(RectTransform target)
        {
            if (target == null
                || technologyScrollRect == null
                || technologyScrollRect.content == null)
            {
                return;
            }

            var viewport = technologyScrollRect.viewport;
            if (viewport == null)
            {
                viewport = technologyScrollRect.transform as RectTransform;
            }

            if (viewport == null)
            {
                return;
            }

            var targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            var viewportRect = viewport.rect;
            var deltaX = 0f;
            if (targetBounds.min.x < viewportRect.xMin)
            {
                deltaX = viewportRect.xMin - targetBounds.min.x;
            }
            else if (targetBounds.max.x > viewportRect.xMax)
            {
                deltaX = viewportRect.xMax - targetBounds.max.x;
            }

            var deltaY = 0f;
            if (targetBounds.min.y < viewportRect.yMin)
            {
                deltaY = viewportRect.yMin - targetBounds.min.y;
            }
            else if (targetBounds.max.y > viewportRect.yMax)
            {
                deltaY = viewportRect.yMax - targetBounds.max.y;
            }

            if (Mathf.Approximately(deltaX, 0f) && Mathf.Approximately(deltaY, 0f))
            {
                return;
            }

            technologyScrollRect.StopMovement();
            technologyScrollRect.content.anchoredPosition += new Vector2(deltaX, deltaY);
            Canvas.ForceUpdateCanvases();
        }

        private void StopAutoScrollRoutine()
        {
            if (autoScrollRoutine == null)
            {
                return;
            }

            StopCoroutine(autoScrollRoutine);
            autoScrollRoutine = null;
        }

        private void HandleCloseClicked()
        {
            if (gamePanel != null)
            {
                gamePanel.Hide_Technology();
                return;
            }

            Hide();
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }
    }
}
