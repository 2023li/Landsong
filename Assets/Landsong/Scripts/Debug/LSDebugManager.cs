using System.Collections.Generic;
using System.Text;
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
        [Header("Debug Options")]
        [SerializeField, InspectorName("UI调试")] private bool uiDebug;
        [SerializeField, InspectorName("UI调试快捷键")] private Key toggleUiDebugKey = Key.F9;
        [SerializeField, InspectorName("绘制 RaycastTarget 区域")] private bool drawUiRaycastTargetRects = true;
        [SerializeField, InspectorName("点击时输出 UI 命中日志")] private bool logUiRaycastOnClick = true;
        [SerializeField, Min(1), InspectorName("最大显示命中数量")] private int maxDisplayedRaycastResults = 12;
        [SerializeField, Min(1), InspectorName("最大高亮图形数量")] private int maxHighlightedGraphics = 160;
        [SerializeField, Range(0f, 0.25f), InspectorName("透明判断阈值")] private float transparentAlphaThreshold = 0.01f;

        private static readonly Vector3[] RectWorldCorners = new Vector3[4];

        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
        private readonly List<Graphic> highlightedGraphics = new List<Graphic>();
        private readonly StringBuilder textBuilder = new StringBuilder(2048);

        private PointerEventData pointerEventData;
        private EventSystem pointerEventSystem;
        private EventSystem sampledEventSystem;
        private Vector2 sampledScreenPosition;
        private bool hasSampledPointer;
        private GUIStyle panelStyle;
        private GUIStyle lineStyle;
        private GUIStyle smallLabelStyle;
        private Texture2D whiteTexture;

        public static LSDebugManager Instance { get; private set; }
        public bool UiDebug => uiDebug;

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

            if (!uiDebug)
            {
                return;
            }

            SampleUiRaycasts();

            if (logUiRaycastOnClick && WasPrimaryPointerPressedThisFrame())
            {
                Debug.Log(BuildUiRaycastReport(), this);
            }
        }

        private void OnGUI()
        {
            if (!uiDebug)
            {
                return;
            }

            EnsureGuiResources();

            if (drawUiRaycastTargetRects)
            {
                DrawHighlightedGraphics();
            }

            DrawUiDebugPanel();
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
            }
        }

        public void ToggleUiDebug()
        {
            SetUiDebugEnabled(!uiDebug);
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
            var height = Mathf.Min(Screen.height - 24f, Mathf.Max(180f, 92f + uiRaycastResults.Count * 46f));
            var rect = new Rect(12f, 12f, Mathf.Min(900f, Screen.width - 24f), height);

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f), report, lineStyle);

            if (hasSampledPointer)
            {
                GUI.Label(
                    new Rect(sampledScreenPosition.x + 14f, Screen.height - sampledScreenPosition.y + 12f, 360f, 24f),
                    $"UI Hits: {uiRaycastResults.Count}",
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
                return textBuilder.ToString();
            }

            if (uiRaycastResults.Count == 0)
            {
                textBuilder.AppendLine("当前指针下没有 UGUI Raycast 命中。");
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
