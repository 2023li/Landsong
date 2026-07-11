using System.Collections.Generic;
using System.Text;
using Landsong.BuildingSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Landsong.DebugSystem
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class LSDebugManager : MonoBehaviour
    {
        [BoxGroup("UI调试"), SerializeField, LabelText("启用 UI 调试")] private bool uiDebug = false;
        [BoxGroup("UI调试"), SerializeField, LabelText("UI 调试快捷键")] private Key toggleUiDebugKey = Key.F9;
        [BoxGroup("UI调试"), SerializeField, LabelText("绘制 RaycastTarget 区域")] private bool drawUiRaycastTargetRects = true;
        [BoxGroup("UI调试"), SerializeField, LabelText("点击时输出 UI 命中日志")] private bool logUiRaycastOnClick = true;
        [BoxGroup("UI调试"), SerializeField, Min(1), LabelText("最大显示 UI 命中数量")] private int maxDisplayedRaycastResults = 12;
        [BoxGroup("UI调试"), SerializeField, Min(1), LabelText("最大高亮图形数量")] private int maxHighlightedGraphics = 160;
        [BoxGroup("UI调试"), SerializeField, Range(0f, 0.25f), LabelText("透明判断阈值")] private float transparentAlphaThreshold = 0.01f;

        [BoxGroup("建筑调试"), SerializeField, LabelText("启用建筑点击调试")] private bool buildingClickDebug = false;
        [BoxGroup("建筑调试"), SerializeField, LabelText("点击时输出建筑命中日志")] private bool logBuildingClickOnClick = true;
        [BoxGroup("建筑调试"), SerializeField, Min(1), LabelText("最大显示建筑命中数量")] private int maxDisplayedBuildingHits = 8;

        [BoxGroup("Gameplay调试"), SerializeField, LabelText("启用 Gameplay 调试")] private bool gameplayDebug;
        [BoxGroup("Gameplay调试"), SerializeField, LabelText("Gameplay 调试快捷键")] private Key toggleGameplayDebugKey = Key.F8;
        [BoxGroup("Gameplay调试"), SerializeField, Min(1), LabelText("默认物资数量")] private int gameplayDebugItemQuantity = 100;
        [BoxGroup("Gameplay调试"), SerializeField, LabelText("默认物资 ID")] private string gameplayDebugItemId = "木头";

        private static readonly Vector3[] RectWorldCorners = new Vector3[4];

        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
        private readonly List<Graphic> highlightedGraphics = new List<Graphic>();
        private readonly List<BuildingClickHit> buildingClickHits = new List<BuildingClickHit>();
        private readonly StringBuilder textBuilder = new StringBuilder(2048);

        private PointerEventData pointerEventData;
        private EventSystem pointerEventSystem;
        private EventSystem sampledEventSystem;
        private Vector2 sampledScreenPosition;
        private Camera sampledBuildingCamera;
        private GameObject sampledTopUiRaycastTarget;
        private bool sampledTopUiRaycastTargetIsInteractive;
        private bool sampledTopUiRaycastTargetIsTransparent;
        private BuildingBase sampledSelectedBuilding;
        private BuildingBase sampledTopBuilding;
        private int sampledRuntimeBuildingCount;
        private string sampledBuildingDebugNote;
        private bool hasSampledPointer;
        private string gameplayDebugItemQuantityText = "100";
        private string gameplayDebugStatus = string.Empty;
        private GUIStyle panelStyle;
        private GUIStyle lineStyle;
        private GUIStyle smallLabelStyle;
        private Texture2D whiteTexture;

        private readonly struct BuildingClickHit
        {
            public BuildingClickHit(BuildingBase building, string source, string targetPath, float distance)
            {
                Building = building;
                Source = source ?? string.Empty;
                TargetPath = targetPath ?? string.Empty;
                Distance = distance < 0f ? 0f : distance;
            }

            public BuildingBase Building { get; }
            public string Source { get; }
            public string TargetPath { get; }
            public float Distance { get; }
        }

        public static LSDebugManager Instance { get; private set; }
        public bool UiDebug => uiDebug;
        public bool GameplayDebug => gameplayDebug;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (FindFirstObjectByType<LSDebugManager>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var managerObject = new GameObject(nameof(LSDebugManager));
            DontDestroyOnLoad(managerObject);
            managerObject.AddComponent<LSDebugManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            gameplayDebugItemQuantity = Mathf.Max(1, gameplayDebugItemQuantity);
            gameplayDebugItemQuantityText = gameplayDebugItemQuantity.ToString();
            gameplayDebugItemId = string.IsNullOrWhiteSpace(gameplayDebugItemId)
                ? "木头"
                : gameplayDebugItemId.Trim();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            HandleShortcut();

            bool primaryPointerPressed = WasPrimaryPointerPressedThisFrame();
            bool shouldSampleDebug = uiDebug || (buildingClickDebug && logBuildingClickOnClick && primaryPointerPressed);

            if (shouldSampleDebug)
            {
                SampleUiRaycasts();
            }
            else
            {
                ClearUiDebug();
            }

            if (buildingClickDebug && shouldSampleDebug)
            {
                SampleBuildingClickDebug();
            }
            else
            {
                ClearBuildingClickDebug();
            }

            if (uiDebug && logUiRaycastOnClick && primaryPointerPressed)
            {
                Debug.Log(BuildUiRaycastReport(), this);
            }

            if (buildingClickDebug && logBuildingClickOnClick && primaryPointerPressed)
            {
                Debug.Log(BuildBuildingClickReport(), this);
            }
        }

        private void OnGUI()
        {
            if (!uiDebug && !gameplayDebug)
            {
                return;
            }

            EnsureGuiResources();

            if (uiDebug && drawUiRaycastTargetRects)
            {
                DrawHighlightedGraphics();
            }

            if (uiDebug)
            {
                DrawUiDebugPanel();
            }

            if (gameplayDebug)
            {
                DrawGameplayDebugPanel();
            }
        }

        public void SetUiDebugEnabled(bool enabled)
        {
            uiDebug = enabled;

            if (uiDebug)
            {
                SampleUiRaycasts();
            }
            else
            {
                uiRaycastResults.Clear();
                highlightedGraphics.Clear();
                ClearBuildingClickDebug();
            }
        }

        public void ToggleUiDebug()
        {
            SetUiDebugEnabled(!uiDebug);
        }

        public void SetGameplayDebugEnabled(bool enabled)
        {
            gameplayDebug = enabled;
        }

        public void ToggleGameplayDebug()
        {
            SetGameplayDebugEnabled(!gameplayDebug);
        }

        private void HandleShortcut()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var keyControl = keyboard[toggleUiDebugKey];
            if (keyControl != null && keyControl.wasPressedThisFrame)
            {
                ToggleUiDebug();
            }

            var gameplayDebugKeyControl = keyboard[toggleGameplayDebugKey];
            if (gameplayDebugKeyControl != null && gameplayDebugKeyControl.wasPressedThisFrame)
            {
                ToggleGameplayDebug();
            }
        }

        private void DrawGameplayDebugPanel()
        {
            const float padding = 12f;
            const float panelWidth = 440f;
            const float panelHeight = 340f;
            var width = Mathf.Min(panelWidth, Mathf.Max(220f, Screen.width - padding * 2f));
            var rect = new Rect(Screen.width - width - padding, padding, width, panelHeight);

            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f));
            GUILayout.Label($"Gameplay 调试: ON  {toggleGameplayDebugKey} 切换", smallLabelStyle);

            var gameSystem = Landsong.GameSystem.Instance;
            if (gameSystem == null)
            {
                GUILayout.Space(8f);
                GUILayout.Label("当前场景没有 GameSystem。", lineStyle);
                GUILayout.EndArea();
                return;
            }

            var previousEnabled = GUI.enabled;
            if (GUILayout.Button("添加 9999 金币", GUILayout.Height(30f)))
            {
                var added = gameSystem.Services.Inventory.AddItem("金币", 9999);
                gameplayDebugStatus = added == 9999
                    ? "已添加金币 x9999。"
                    : $"金币仅添加 x{added}/9999：库存容量不足或金币未配置。";
            }

            GUILayout.Space(8f);
            GUILayout.Label("指定物资", smallLabelStyle);
            DrawGameplayDebugItemSelection(gameSystem);

            GUILayout.BeginHorizontal();
            GUILayout.Label("数量", GUILayout.Width(36f));
            gameplayDebugItemQuantityText = GUILayout.TextField(gameplayDebugItemQuantityText, GUILayout.Width(84f));
            if (GUILayout.Button("添加物资", GUILayout.Height(24f)))
            {
                AddSelectedGameplayDebugItem(gameSystem);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            if (GUILayout.Button("获取一个随机任务", GUILayout.Height(30f)))
            {
                if (gameSystem.Services.Quest.TryAddDebugRandomQuest(out var quest))
                {
                    gameplayDebugStatus = $"已获取随机任务：{quest.Definition.DisplayName}。";
                }
                else
                {
                    gameplayDebugStatus = "获取随机任务失败：没有可用物资，或已达到同时存在上限。";
                }
            }

            GUI.enabled = previousEnabled;
            if (!string.IsNullOrWhiteSpace(gameplayDebugStatus))
            {
                GUILayout.Space(8f);
                GUILayout.Label(gameplayDebugStatus, lineStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawGameplayDebugItemSelection(Landsong.GameSystem gameSystem)
        {
            var catalog = gameSystem.Services.Inventory == null ? null : gameSystem.Services.Inventory.ItemCatalog;
            var definitions = catalog == null ? null : catalog.Definitions;
            if (definitions == null || definitions.Count == 0)
            {
                GUILayout.Label("物品目录未配置。", lineStyle);
                return;
            }

            var validDefinitions = new List<ItemDefinition>();
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.ItemId))
                {
                    validDefinitions.Add(definition);
                }
            }

            if (validDefinitions.Count == 0)
            {
                GUILayout.Label("物品目录中没有有效物品。", lineStyle);
                return;
            }

            var selectedIndex = 0;
            var labels = new string[validDefinitions.Count];
            for (var i = 0; i < validDefinitions.Count; i++)
            {
                labels[i] = validDefinitions[i].DisplayName;
                if (string.Equals(validDefinitions[i].ItemId, gameplayDebugItemId, System.StringComparison.Ordinal))
                {
                    selectedIndex = i;
                }
            }

            selectedIndex = GUILayout.SelectionGrid(selectedIndex, labels, 3, GUILayout.Height(52f));
            gameplayDebugItemId = validDefinitions[selectedIndex].ItemId;
            GUILayout.Label($"物资 ID：{gameplayDebugItemId}", lineStyle);
        }

        private void AddSelectedGameplayDebugItem(Landsong.GameSystem gameSystem)
        {
            if (!int.TryParse(gameplayDebugItemQuantityText, out var amount))
            {
                gameplayDebugStatus = "物资数量必须是正整数。";
                return;
            }

            amount = Mathf.Max(1, amount);
            gameplayDebugItemQuantity = amount;
            gameplayDebugItemQuantityText = amount.ToString();

            var catalog = gameSystem.Services.Inventory == null ? null : gameSystem.Services.Inventory.ItemCatalog;
            if (catalog == null || !catalog.TryGetDefinition(gameplayDebugItemId, out var definition))
            {
                gameplayDebugStatus = "请选择物品目录中的有效物资。";
                return;
            }

            var added = gameSystem.Services.Inventory.AddItem(definition.ItemId, amount);
            gameplayDebugStatus = added == amount
                ? $"已添加 {definition.DisplayName} x{added}。"
                : $"{definition.DisplayName} 仅添加 x{added}/{amount}：库存容量不足。";
        }

        private void SampleUiRaycasts()
        {
            hasSampledPointer = TryGetPointerScreenPosition(out sampledScreenPosition);
            sampledEventSystem = EventSystem.current;
            uiRaycastResults.Clear();
            highlightedGraphics.Clear();

            if (!hasSampledPointer || sampledEventSystem == null)
            {
                return;
            }

            if (pointerEventData == null || pointerEventSystem != sampledEventSystem)
            {
                pointerEventData = new PointerEventData(sampledEventSystem);
                pointerEventSystem = sampledEventSystem;
            }

            pointerEventData.Reset();
            pointerEventData.position = sampledScreenPosition;
            sampledEventSystem.RaycastAll(pointerEventData, uiRaycastResults);

            CollectHighlightedGraphics();
        }

        private void ClearUiDebug()
        {
            hasSampledPointer = false;
            sampledScreenPosition = default;
            sampledEventSystem = null;
            uiRaycastResults.Clear();
            highlightedGraphics.Clear();
        }

        private void CollectHighlightedGraphics()
        {
            for (var i = 0; i < uiRaycastResults.Count && highlightedGraphics.Count < maxHighlightedGraphics; i++)
            {
                var graphic = uiRaycastResults[i].gameObject == null ? null : uiRaycastResults[i].gameObject.GetComponent<Graphic>();
                AddHighlightedGraphic(graphic);
            }

            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < canvases.Length && highlightedGraphics.Count < maxHighlightedGraphics; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || !canvas.isActiveAndEnabled)
                {
                    continue;
                }

                var graphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
                for (var j = 0; j < graphics.Count && highlightedGraphics.Count < maxHighlightedGraphics; j++)
                {
                    var graphic = graphics[j];
                    if (!IsSuspiciousTransparentRaycastTarget(graphic))
                    {
                        continue;
                    }

                    AddHighlightedGraphic(graphic);
                }
            }
        }

        private void AddHighlightedGraphic(Graphic graphic)
        {
            if (graphic == null || highlightedGraphics.Contains(graphic))
            {
                return;
            }

            highlightedGraphics.Add(graphic);
        }

        private bool IsSuspiciousTransparentRaycastTarget(Graphic graphic)
        {
            return graphic != null
                   && graphic.isActiveAndEnabled
                   && graphic.raycastTarget
                   && GetEffectiveGraphicAlpha(graphic) <= transparentAlphaThreshold;
        }

        private void DrawHighlightedGraphics()
        {
            for (var i = 0; i < highlightedGraphics.Count; i++)
            {
                var graphic = highlightedGraphics[i];
                if (graphic == null || graphic.rectTransform == null)
                {
                    continue;
                }

                var canvas = graphic.canvas;
                var eventCamera = GetCanvasEventCamera(canvas);
                if (!TryGetScreenRect(graphic.rectTransform, eventCamera, out var rect))
                {
                    continue;
                }

                var isTransparent = GetEffectiveGraphicAlpha(graphic) <= transparentAlphaThreshold;
                var isTopHit = uiRaycastResults.Count > 0 && uiRaycastResults[0].gameObject == graphic.gameObject;
                var color = isTopHit
                    ? new Color(1f, 1f, 1f, 0.9f)
                    : isTransparent
                        ? new Color(1f, 0.1f, 0.1f, 0.75f)
                        : new Color(1f, 0.85f, 0.1f, 0.65f);

                DrawRectOutline(rect, color, isTopHit ? 3f : 2f);
                if (isTransparent || isTopHit)
                {
                    GUI.color = new Color(color.r, color.g, color.b, 0.12f);
                    GUI.DrawTexture(rect, whiteTexture);
                }
            }

            GUI.color = Color.white;
        }

        private void DrawUiDebugPanel()
        {
            var report = BuildUiRaycastReport();
            var lineCount = CountLines(report);
            var height = Mathf.Min(
                Screen.height - 24f,
                Mathf.Max(180f, 42f + lineCount * 22f));
            var rect = new Rect(12f, 12f, Mathf.Min(900f, Screen.width - 24f), height);

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f), report, lineStyle);

            if (hasSampledPointer)
            {
                GUI.Label(
                    new Rect(sampledScreenPosition.x + 14f, Screen.height - sampledScreenPosition.y + 12f, 360f, 24f),
                    buildingClickDebug ? $"UI Hits: {uiRaycastResults.Count}  Building Hits: {buildingClickHits.Count}" : $"UI Hits: {uiRaycastResults.Count}",
                    smallLabelStyle);
            }
        }

        private string BuildUiRaycastReport()
        {
            textBuilder.Clear();
            textBuilder.Append("UI调试: ON  ");
            textBuilder.Append(toggleUiDebugKey);
            textBuilder.Append(" 切换");
            textBuilder.AppendLine();

            if (!hasSampledPointer)
            {
                textBuilder.AppendLine("Pointer: 未检测到鼠标或触摸输入");
                AppendBuildingClickReportIfEnabled();
                return textBuilder.ToString();
            }

            textBuilder.Append("Pointer: ");
            textBuilder.Append(Mathf.RoundToInt(sampledScreenPosition.x));
            textBuilder.Append(", ");
            textBuilder.Append(Mathf.RoundToInt(sampledScreenPosition.y));
            textBuilder.Append("  EventSystem: ");
            textBuilder.Append(sampledEventSystem == null ? "None" : sampledEventSystem.name);
            textBuilder.Append("  Hits: ");
            textBuilder.Append(uiRaycastResults.Count);
            textBuilder.AppendLine();

            if (sampledEventSystem == null)
            {
                textBuilder.AppendLine("没有 EventSystem，UGUI 点击不会被处理。");
                AppendBuildingClickReportIfEnabled();
                return textBuilder.ToString();
            }

            if (uiRaycastResults.Count == 0)
            {
                textBuilder.AppendLine("当前指针下没有 UGUI Raycast 命中。");
                AppendBuildingClickReportIfEnabled();
                return textBuilder.ToString();
            }

            var max = Mathf.Min(maxDisplayedRaycastResults, uiRaycastResults.Count);
            for (var i = 0; i < max; i++)
            {
                AppendRaycastResultLine(i, uiRaycastResults[i]);
            }

            if (uiRaycastResults.Count > max)
            {
                textBuilder.Append("... 还有 ");
                textBuilder.Append(uiRaycastResults.Count - max);
                textBuilder.AppendLine(" 个命中未显示");
            }

            AppendBuildingClickReportIfEnabled();

            return textBuilder.ToString();
        }

        private void AppendRaycastResultLine(int index, RaycastResult result)
        {
            var target = result.gameObject;
            var graphic = target == null ? null : target.GetComponent<Graphic>();
            var selectable = target == null ? null : target.GetComponentInParent<Selectable>(true);
            var alpha = graphic == null ? 1f : GetEffectiveGraphicAlpha(graphic);
            var transparentBlocker = graphic != null && graphic.raycastTarget && alpha <= transparentAlphaThreshold;

            textBuilder.Append(index);
            textBuilder.Append(index == 0 ? " TOP " : "     ");
            if (transparentBlocker)
            {
                textBuilder.Append("[透明可拦截] ");
            }

            textBuilder.Append(target == null ? "null" : target.name);
            textBuilder.Append("  module=");
            textBuilder.Append(result.module == null ? "None" : result.module.GetType().Name);

            if (graphic != null)
            {
                textBuilder.Append("  graphic=");
                textBuilder.Append(graphic.GetType().Name);
                textBuilder.Append(" raycast=");
                textBuilder.Append(graphic.raycastTarget);
                textBuilder.Append(" alpha=");
                textBuilder.Append(alpha.ToString("0.###"));
            }

            if (selectable != null)
            {
                textBuilder.Append("  selectable=");
                textBuilder.Append(selectable.GetType().Name);
                textBuilder.Append(" interactable=");
                textBuilder.Append(selectable.IsInteractable());
            }

            AppendCanvasGroupSummary(target);
            textBuilder.AppendLine();

            textBuilder.Append("      path=");
            textBuilder.AppendLine(target == null ? string.Empty : GetTransformPath(target.transform));
        }

        private void AppendCanvasGroupSummary(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            var groups = target.GetComponentsInParent<CanvasGroup>(true);
            if (groups.Length == 0)
            {
                return;
            }

            textBuilder.Append("  groups=");
            for (var i = 0; i < groups.Length; i++)
            {
                if (i > 0)
                {
                    textBuilder.Append(" > ");
                }

                var group = groups[i];
                textBuilder.Append(group.name);
                textBuilder.Append("(a=");
                textBuilder.Append(group.alpha.ToString("0.##"));
                textBuilder.Append(", block=");
                textBuilder.Append(group.blocksRaycasts);
                textBuilder.Append(", interact=");
                textBuilder.Append(group.interactable);
                textBuilder.Append(")");

                if (group.ignoreParentGroups)
                {
                    break;
                }
            }
        }

        #region Building Click Debug

        private void SampleBuildingClickDebug()
        {
            ClearBuildingClickDebug();

            if (!hasSampledPointer)
            {
                sampledBuildingDebugNote = "Pointer: 未检测到鼠标或触摸输入";
                return;
            }

            sampledBuildingCamera = Camera.main;
            if (sampledBuildingCamera == null)
            {
                sampledBuildingDebugNote = "Camera.main: None，建筑点击射线无法计算";
                return;
            }

            sampledTopUiRaycastTarget = GetTopUiRaycastTarget(
                out sampledTopUiRaycastTargetIsInteractive,
                out sampledTopUiRaycastTargetIsTransparent);

            Landsong.GameSystem gameSystem = FindFirstObjectByType<Landsong.GameSystem>(FindObjectsInactive.Include);
            if (gameSystem == null)
            {
                AppendBuildingDebugNote("没有 GameSystem，无法读取选择状态和运行时建筑数量。");
            }

            BuildingSelectionController selectionController = gameSystem == null ? null : gameSystem.Services.BuildingSelection;
            IReadOnlyList<BuildingBase> runtimeBuildings = gameSystem == null || gameSystem.Services.Buildings == null
                ? null
                : gameSystem.Services.Buildings.Buildings;

            sampledSelectedBuilding = selectionController == null ? null : selectionController.SelectedBuilding;
            sampledRuntimeBuildingCount = runtimeBuildings == null ? 0 : runtimeBuildings.Count;

            Ray ray = sampledBuildingCamera.ScreenPointToRay(sampledScreenPosition);
            CollectPhysics3DBuildingHits(ray);
            CollectPhysics2DBuildingHits(ray);

            buildingClickHits.Sort(CompareBuildingClickHits);
            sampledTopBuilding = buildingClickHits.Count == 0 ? null : buildingClickHits[0].Building;

            if (buildingClickHits.Count == 0)
            {
                AppendBuildingDebugNote("未命中任何带 Collider/Collider2D 的建筑。缺 Collider 的建筑不会被统一建筑点击链路命中。");
            }
        }

        private void ClearBuildingClickDebug()
        {
            buildingClickHits.Clear();
            sampledBuildingCamera = null;
            sampledTopUiRaycastTarget = null;
            sampledTopUiRaycastTargetIsInteractive = false;
            sampledTopUiRaycastTargetIsTransparent = false;
            sampledSelectedBuilding = null;
            sampledTopBuilding = null;
            sampledRuntimeBuildingCount = 0;
            sampledBuildingDebugNote = string.Empty;
        }

        private string BuildBuildingClickReport()
        {
            textBuilder.Clear();
            AppendBuildingClickReportContent();
            return textBuilder.ToString();
        }

        private void AppendBuildingClickReportContent()
        {
            textBuilder.AppendLine("建筑点击调试:");

            if (!hasSampledPointer)
            {
                textBuilder.AppendLine("Pointer: 未检测到鼠标或触摸输入");
                return;
            }

            textBuilder.Append("Pointer: ");
            textBuilder.Append(Mathf.RoundToInt(sampledScreenPosition.x));
            textBuilder.Append(", ");
            textBuilder.Append(Mathf.RoundToInt(sampledScreenPosition.y));
            textBuilder.Append("  Camera: ");
            textBuilder.Append(sampledBuildingCamera == null ? "None" : sampledBuildingCamera.name);
            textBuilder.Append("  HitTest: Collider-only");
            textBuilder.AppendLine();

            textBuilder.Append("Runtime Buildings: ");
            textBuilder.Append(sampledRuntimeBuildingCount);
            textBuilder.Append("  Selected: ");
            textBuilder.Append(GetBuildingDebugName(sampledSelectedBuilding));
            textBuilder.Append("  Top Hit: ");
            textBuilder.Append(GetBuildingDebugName(sampledTopBuilding));
            textBuilder.AppendLine();
            AppendBuildingStatusSummaryLine("Selected Status", sampledSelectedBuilding);
            if (sampledTopBuilding != sampledSelectedBuilding)
            {
                AppendBuildingStatusSummaryLine("Top Hit Status", sampledTopBuilding);
            }

            textBuilder.Append("UI Top Hit: ");
            if (sampledTopUiRaycastTarget == null)
            {
                textBuilder.AppendLine("None");
            }
            else
            {
                textBuilder.Append(sampledTopUiRaycastTarget.name);
                textBuilder.Append("  interactive=");
                textBuilder.Append(sampledTopUiRaycastTargetIsInteractive ? "Yes" : "No");
                textBuilder.Append("  transparent=");
                textBuilder.Append(sampledTopUiRaycastTargetIsTransparent ? "Yes" : "No");
                textBuilder.Append("  path=");
                textBuilder.AppendLine(GetTransformPath(sampledTopUiRaycastTarget.transform));
            }

            if (!string.IsNullOrWhiteSpace(sampledBuildingDebugNote))
            {
                textBuilder.Append("Note: ");
                textBuilder.AppendLine(sampledBuildingDebugNote);
            }

            if (buildingClickHits.Count == 0)
            {
                textBuilder.AppendLine("Building Hits: 0");
                return;
            }

            textBuilder.Append("Building Hits: ");
            textBuilder.Append(buildingClickHits.Count);
            textBuilder.AppendLine();

            int max = Mathf.Min(maxDisplayedBuildingHits, buildingClickHits.Count);
            for (int i = 0; i < max; i++)
            {
                AppendBuildingClickHitLine(i, buildingClickHits[i]);
            }

            if (buildingClickHits.Count > max)
            {
                textBuilder.Append("... 还有 ");
                textBuilder.Append(buildingClickHits.Count - max);
                textBuilder.AppendLine(" 个建筑命中未显示");
            }
        }

        private void AppendBuildingClickReportIfEnabled()
        {
            if (!buildingClickDebug)
            {
                return;
            }

            textBuilder.AppendLine();
            AppendBuildingClickReportContent();
        }

        private void AppendBuildingClickHitLine(int index, BuildingClickHit hit)
        {
            textBuilder.Append(index);
            textBuilder.Append(index == 0 ? " TOP " : "     ");
            textBuilder.Append(hit.Source);
            textBuilder.Append("  building=");
            textBuilder.Append(GetBuildingDebugName(hit.Building));
            textBuilder.Append("  distance=");
            textBuilder.Append(hit.Distance.ToString("0.###"));
            AppendBuildingColliderSummary(hit.Building);
            textBuilder.AppendLine();

            if (!string.IsNullOrWhiteSpace(hit.TargetPath))
            {
                textBuilder.Append("      target=");
                textBuilder.AppendLine(hit.TargetPath);
            }

            AppendBuildingStatusDetailLines("      ", hit.Building);
        }

        private void CollectPhysics3DBuildingHits(Ray ray)
        {
            if (sampledBuildingCamera == null)
            {
                return;
            }

            RaycastHit[] hits = Physics.RaycastAll(ray, sampledBuildingCamera.farClipPlane);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                BuildingBase building = collider == null ? null : GetBuildingFromObject(collider.gameObject);
                if (building == null)
                {
                    continue;
                }

                AddBuildingClickHit(
                    building,
                    "Physics3D",
                    collider == null ? string.Empty : GetTransformPath(collider.transform),
                    hits[i].distance);
            }
        }

        private void CollectPhysics2DBuildingHits(Ray ray)
        {
            if (sampledBuildingCamera == null)
            {
                return;
            }

            RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, sampledBuildingCamera.farClipPlane);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                BuildingBase building = collider == null ? null : GetBuildingFromObject(collider.gameObject);
                if (building == null)
                {
                    continue;
                }

                AddBuildingClickHit(
                    building,
                    "Physics2D",
                    collider == null ? string.Empty : GetTransformPath(collider.transform),
                    hits[i].distance);
            }
        }

        private void AddBuildingClickHit(BuildingBase building, string source, string targetPath, float distance)
        {
            if (building == null)
            {
                return;
            }

            buildingClickHits.Add(new BuildingClickHit(building, source, targetPath, distance));
        }

        private GameObject GetTopUiRaycastTarget(out bool isInteractive, out bool isTransparent)
        {
            isInteractive = false;
            isTransparent = false;

            for (int i = 0; i < uiRaycastResults.Count; i++)
            {
                RaycastResult result = uiRaycastResults[i];
                if (!(result.module is GraphicRaycaster) || result.gameObject == null)
                {
                    continue;
                }

                Graphic graphic = result.gameObject.GetComponent<Graphic>();
                isInteractive = IsInteractiveUi(result.gameObject);
                isTransparent = IsSuspiciousTransparentRaycastTarget(graphic);
                return result.gameObject;
            }

            return null;
        }

        private void AppendBuildingDebugNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sampledBuildingDebugNote))
            {
                sampledBuildingDebugNote = note;
                return;
            }

            sampledBuildingDebugNote += " " + note;
        }

        private static BuildingBase GetBuildingFromObject(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            return target.GetComponentInParent<BuildingBase>(true);
        }

        private static string GetBuildingDebugName(BuildingBase building)
        {
            if (building == null)
            {
                return "None";
            }

            string displayName = building.Definition == null ? string.Empty : building.Definition.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = building.name;
            }

            return $"{displayName} ({building.name})";
        }

        private void AppendBuildingColliderSummary(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            Collider2D[] colliders2D = building.GetComponentsInChildren<Collider2D>(true);
            Collider[] colliders3D = building.GetComponentsInChildren<Collider>(true);

            textBuilder.Append("  collider2D=");
            AppendEnabledCount(colliders2D);
            textBuilder.Append("  collider3D=");
            AppendEnabledCount(colliders3D);
            textBuilder.Append("  placed=");
            textBuilder.Append(building.HasPlacement ? "Yes" : "No");
        }

        private void AppendBuildingStatusSummaryLine(string label, BuildingBase building)
        {
            textBuilder.Append(label);
            textBuilder.Append(": ");
            if (building == null)
            {
                textBuilder.AppendLine("None");
                return;
            }

            IReadOnlyList<BuildingRuntimeStatus> statuses = building.GetRuntimeStatuses();
            int validStatusCount = CountValidBuildingStatuses(statuses);
            bool hasAbnormalStatus = BuildingRuntimeStatusCatalog.HasAbnormalStatus(statuses);

            textBuilder.Append(validStatusCount);
            textBuilder.Append(" status(es), abnormal=");
            textBuilder.AppendLine(hasAbnormalStatus ? "Yes" : "No");
        }

        private void AppendBuildingStatusDetailLines(string indent, BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            IReadOnlyList<BuildingRuntimeStatus> statuses = building.GetRuntimeStatuses();
            int validStatusCount = CountValidBuildingStatuses(statuses);
            bool hasAbnormalStatus = BuildingRuntimeStatusCatalog.HasAbnormalStatus(statuses);

            textBuilder.Append(indent);
            textBuilder.Append("statuses=");
            textBuilder.Append(validStatusCount);
            textBuilder.Append("  abnormal=");
            textBuilder.AppendLine(hasAbnormalStatus ? "Yes" : "No");

            if (statuses == null || validStatusCount <= 0)
            {
                textBuilder.Append(indent);
                textBuilder.AppendLine("- None");
                return;
            }

            for (int i = 0; i < statuses.Count; i++)
            {
                BuildingRuntimeStatus status = statuses[i];
                if (!status.IsValid)
                {
                    continue;
                }

                textBuilder.Append(indent);
                textBuilder.Append("- id=");
                textBuilder.Append(status.StatusId);
                textBuilder.Append("  name=");
                textBuilder.Append(status.DisplayName);
                textBuilder.Append("  abnormal=");
                textBuilder.Append(BuildingRuntimeStatusCatalog.IsAbnormalStatus(status) ? "Yes" : "No");
                if (status.Target > 0)
                {
                    textBuilder.Append("  progress=");
                    textBuilder.Append(status.Progress);
                    textBuilder.Append("/");
                    textBuilder.Append(status.Target);
                }

                textBuilder.AppendLine();
            }
        }

        private static int CountValidBuildingStatuses(IReadOnlyList<BuildingRuntimeStatus> statuses)
        {
            if (statuses == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].IsValid)
                {
                    count++;
                }
            }

            return count;
        }

        private void AppendEnabledCount<TCollider>(TCollider[] colliders)
            where TCollider : Collider
        {
            int enabledCount = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled)
                {
                    enabledCount++;
                }
            }

            textBuilder.Append(enabledCount);
            textBuilder.Append("/");
            textBuilder.Append(colliders.Length);
        }

        private void AppendEnabledCount(Collider2D[] colliders)
        {
            int enabledCount = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled)
                {
                    enabledCount++;
                }
            }

            textBuilder.Append(enabledCount);
            textBuilder.Append("/");
            textBuilder.Append(colliders.Length);
        }

        private static bool IsInteractiveUi(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            Component[] components = target.GetComponentsInParent<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                if (component is Selectable selectable)
                {
                    if (IsVisibleSelectable(selectable))
                    {
                        return true;
                    }

                    continue;
                }

                if (component is IBeginDragHandler
                    || component is IDragHandler
                    || component is IEndDragHandler
                    || component is IScrollHandler
                    || component is IPointerClickHandler
                    || component is IPointerDownHandler
                    || component is IPointerUpHandler)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsVisibleSelectable(Selectable selectable)
        {
            if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())
            {
                return false;
            }

            Graphic targetGraphic = selectable.targetGraphic;
            if (targetGraphic == null)
            {
                return true;
            }

            return targetGraphic.isActiveAndEnabled
                   && targetGraphic.raycastTarget
                   && targetGraphic.color.a > 0.01f
                   && targetGraphic.canvasRenderer.GetAlpha() > 0.01f;
        }

        private static int CompareBuildingClickHits(BuildingClickHit left, BuildingClickHit right)
        {
            return left.Distance.CompareTo(right.Distance);
        }

        #endregion

        private static bool TryGetPointerScreenPosition(out Vector2 screenPosition)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touch = touchscreen.primaryTouch;
                if (touch.press.isPressed || touch.press.wasPressedThisFrame || touch.press.wasReleasedThisFrame)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }

            screenPosition = default;
            return false;
        }

        private static bool WasPrimaryPointerPressedThisFrame()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            var touchscreen = Touchscreen.current;
            return touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame;
        }

        private static int CountLines(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int count = 1;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\n')
                {
                    count++;
                }
            }

            return count;
        }

        private static float GetEffectiveGraphicAlpha(Graphic graphic)
        {
            if (graphic == null)
            {
                return 1f;
            }

            var alpha = graphic.color.a * graphic.canvasRenderer.GetAlpha();
            var groups = graphic.GetComponentsInParent<CanvasGroup>(true);
            for (var i = 0; i < groups.Length; i++)
            {
                var group = groups[i];
                alpha *= group.alpha;
                if (group.ignoreParentGroups)
                {
                    break;
                }
            }

            return alpha;
        }

        private static Camera GetCanvasEventCamera(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera;
        }

        private static bool TryGetScreenRect(RectTransform rectTransform, Camera eventCamera, out Rect rect)
        {
            rectTransform.GetWorldCorners(RectWorldCorners);

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < RectWorldCorners.Length; i++)
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, RectWorldCorners[i]);
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            if (!float.IsFinite(min.x) || !float.IsFinite(min.y) || !float.IsFinite(max.x) || !float.IsFinite(max.y))
            {
                rect = default;
                return false;
            }

            rect = Rect.MinMaxRect(min.x, Screen.height - max.y, max.x, Screen.height - min.y);
            return rect.width > 0.01f && rect.height > 0.01f;
        }

        private void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), whiteTexture);
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var path = transform.name;
            var parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private void EnsureGuiResources()
        {
            if (whiteTexture == null)
            {
                whiteTexture = Texture2D.whiteTexture;
            }

            panelStyle ??= new GUIStyle(GUI.skin.box)
            {
                normal = { background = whiteTexture },
                padding = new RectOffset(10, 10, 10, 10)
            };
            panelStyle.normal.textColor = Color.white;

            lineStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                richText = false,
                alignment = TextAnchor.UpperLeft
            };
            lineStyle.normal.textColor = Color.white;

            smallLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
        }
    }
}
