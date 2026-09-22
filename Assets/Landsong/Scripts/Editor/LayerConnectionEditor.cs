using System;
using System.Linq;
using GiantGrey.TileWorldCreator;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.GridSystem;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    [CustomEditor(typeof(MapNavigationAuthoring))]
    public sealed class MapNavigationAuthoringEditor : OdinEditor
    {
        int selected, picking;
        MapNavigationAuthoring Source => (MapNavigationAuthoring)target;

        MapContentAuthoring Content => Source.GetComponentInParent<MapContentAuthoring>();

        Configuration Config => Content == null ? null : Content.TwcConfiguration as Configuration;

        bool LayerMode => Content != null && Content.TerrainRules != null && Content.TerrainRules.useLayerBlueprintRules;

        BlueprintLayerFolder[] Groups => Config == null ? Array.Empty<BlueprintLayerFolder>() : Config.blueprintLayerFolders.Where(f => f != null && System.Text.RegularExpressions.Regex.IsMatch(f.folderName ?? "", @"^Layer(0|[1-9][0-9]*)$")).OrderBy(f => int.Parse(f.folderName.Substring(5))).ToArray();

        public override void OnInspectorGUI()
        {
            if (!LayerMode)
            {
                base.OnInspectorGUI();
                return;
            }

            EditorGUILayout.HelpBox("选择入口/出口 Layer 后，在 Scene 的逻辑网格上点选端点。高度由 Layer 唯一决定，不读取模型。入口为通道低端；同层桥梁至少 3 格宽。当前支持直线通道。", MessageType.Info);
            if ((Source.Surfaces?.Length ?? 0) > 0 || (Source.Connections?.Length ?? 0) > 0)
            {
                EditorGUILayout.HelpBox("存在旧版手填高度数据；请核对并改建 Layer 连接。新模式不会静默采用旧高度。", MessageType.Error);
                base.OnInspectorGUI();
            }

            var list = Source.LayerConnections ?? Array.Empty<LayerTerrainConnection>();
            if (GUILayout.Button("添加逻辑连接"))
            {
                Undo.RecordObject(Source, "添加逻辑连接");
                var next = new LayerTerrainConnection
                {
                    Id = list.Select(c => c.Id).DefaultIfEmpty(0).Max() + 1
                };
                var groups = Groups;
                if (groups.Length > 0)
                    next.EntryLayerGuid = next.ExitLayerGuid = groups[0].guid;
                Source.LayerConnections = list.Concat(new[] { next }).ToArray();
                selected = list.Length;
                Dirty();
                list = Source.LayerConnections;
            }

            if (list.Length == 0)
                return;
            selected = Mathf.Clamp(selected, 0, list.Length - 1);
            selected = EditorGUILayout.Popup("连接", selected, list.Select(c => "连接 " + c.Id).ToArray());
            var link = list[selected];
            var folders = Groups;
            EditorGUI.BeginChangeCheck();
            int id = EditorGUILayout.IntField("稳定 ID", link.Id);
            string entry = LayerPopup("入口 Layer", link.EntryLayerGuid, folders);
            string exit = LayerPopup("出口 Layer", link.ExitLayerGuid, folders);
            var a = EditorGUILayout.Vector2IntField("入口格", link.EntryCell);
            var b = EditorGUILayout.Vector2IntField("出口格", link.ExitCell);
            int width = EditorGUILayout.IntField("宽度（格）", link.Width);
            bool bidirectional = EditorGUILayout.Toggle("双向通行", link.Bidirectional);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(Source, "修改逻辑连接");
                link.Id = id;
                link.EntryLayerGuid = entry;
                link.ExitLayerGuid = exit;
                link.EntryCell = a;
                link.ExitCell = b;
                link.Width = width;
                link.Bidirectional = bidirectional;
                Dirty();
            }

            if (GUILayout.Button(picking == 1 ? "正在点选入口（Esc 取消）" : "在 Scene 点选入口"))
                picking = 1;
            if (GUILayout.Button(picking == 2 ? "正在点选出口（Esc 取消）" : "在 Scene 点选出口"))
                picking = 2;
            if (GUILayout.Button("校验全部连接"))
            {
                try
                {
                    ValidateConnections(Content);
                    Debug.Log("Layer 逻辑地表与连接校验通过。", Source);
                }
                catch (Exception error)
                {
                    Debug.LogError(error.Message, Source);
                }
            }

            if (GUILayout.Button("删除此连接"))
            {
                Undo.RecordObject(Source, "删除逻辑连接");
                Source.LayerConnections = list.Where((c, i) => i != selected).ToArray();
                picking = 0;
                Dirty();
            }
        }

        static string LayerPopup(string label, string guid, BlueprintLayerFolder[] groups)
        {
            var names = new[]
            {
                "请选择 Layer"
            }.Concat(groups.Select(g => g.folderName)).ToArray();
            int current = Array.FindIndex(groups, g => g.guid == guid) + 1;
            int next = EditorGUILayout.Popup(label, current, names);
            return next == current ? guid : next == 0 ? "" : groups[next - 1].guid;
        }

        void Dirty()
        {
            EditorUtility.SetDirty(Source);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(Source.gameObject.scene);
            SceneView.RepaintAll();
        }

        void OnSceneGUI()
        {
            if (!LayerMode || Config == null)
                return;
            var groups = Groups;
            var root = Content.transform.position;
            float cellSize = Config.cellSize;
            foreach (var link in Source.LayerConnections ?? Array.Empty<LayerTerrainConnection>())
            {
                var a = groups.FirstOrDefault(g => g.guid == link.EntryLayerGuid);
                var b = groups.FirstOrDefault(g => g.guid == link.ExitLayerGuid);
                if (a == null || b == null)
                    continue;
                var start = root + new Vector3(link.EntryCell.x * cellSize, int.Parse(a.folderName.Substring(5)), link.EntryCell.y * cellSize);
                var end = root + new Vector3(link.ExitCell.x * cellSize, int.Parse(b.folderName.Substring(5)), link.ExitCell.y * cellSize);
                Handles.color = Color.cyan;
                Handles.DrawWireCube(start, new Vector3(cellSize, .03f, cellSize));
                Handles.color = Color.yellow;
                Handles.DrawWireCube(end, new Vector3(cellSize, .03f, cellSize));
                Handles.DrawAAPolyLine(3, start, end);
                if ((end - start).sqrMagnitude > .001f)
                    Handles.ArrowHandleCap(0, (start + end) * .5f, Quaternion.LookRotation(end - start), cellSize * .5f, EventType.Repaint);
                Handles.Label(start, "连接 " + link.Id + "  " + a.folderName + (link.Bidirectional ? " ↔ " : " → ") + b.folderName + "  宽 " + link.Width);
            }

            if (picking == 0 || selected >= Source.LayerConnections.Length)
                return;
            var current = Source.LayerConnections[selected];
            var folder = groups.FirstOrDefault(g => g.guid == (picking == 1 ? current.EntryLayerGuid : current.ExitLayerGuid));
            if (folder == null)
                return;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                picking = 0;
                e.Use();
                Repaint();
                return;
            }

            if (e.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            var plane = new Plane(Vector3.up, root + Vector3.up * int.Parse(folder.folderName.Substring(5)));
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!plane.Raycast(ray, out float distance))
                return;
            var hit = ray.GetPoint(distance) - root;
            var cell = new Vector2Int(Mathf.FloorToInt(hit.x / cellSize + .5f), Mathf.FloorToInt(hit.z / cellSize + .5f));
            Handles.color = Color.green;
            Handles.DrawWireCube(root + new Vector3(cell.x * cellSize, int.Parse(folder.folderName.Substring(5)), cell.y * cellSize), new Vector3(cellSize, .02f, cellSize));
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                try
                {
                    var map = LayerTerrainCompiler.Compile(Config, Content);
                    if (!map.Cells.TryGetValue((folder.guid, cell), out var ground) || !ground.Rule.Traversable)
                        throw new InvalidOperationException("所选 Layer 的此格没有可通行逻辑地表。");
                    Undo.RecordObject(Source, "点选连接端点");
                    if (picking == 1)
                        current.EntryCell = cell;
                    else
                        current.ExitCell = cell;
                    picking = 0;
                    Dirty();
                    Repaint();
                }
                catch (Exception error)
                {
                    Debug.LogError(error.Message, Source);
                }

                e.Use();
            }

            SceneView.RepaintAll();
        }

        public static void ValidateConnections(MapContentAuthoring content)
        {
            var config = content.TwcConfiguration as Configuration;
            var compiled = LayerTerrainCompiler.Compile(config, content);
            var map = ScriptableObject.CreateInstance<MapAsset>();
            try
            {
                map.Size = new Vector2Int(config.width, config.height);
                map.CellSize = config.cellSize;
                map.ElevationStep = 1;
                map.Cells = new CellSource[config.width * config.height];
                foreach (var c in compiled.Primary)
                    map.Cells[c.Position.Z * config.width + c.Position.X] = new CellSource
                    {
                        Exists = true,
                        Traversable = c.Traversable,
                        Buildable = c.Buildable,
                        Elevation = c.ElevationLevel,
                        Height = c.ElevationLevel,
                        Surface = c.SurfaceLayer
                    };
                map.NavigationSurfaces = compiled.Additional.ToArray();
                map.Connections = content.GetComponentsInChildren<MapNavigationAuthoring>(true).Where(a => a.isActiveAndEnabled).SelectMany(a => a.LayerConnections).Select(c => LayerTerrainCompiler.Connection(compiled, c)).Concat(compiled.Slopes.Select(s => s.Connection)).ToArray();
                using var grid = GameWorldMapAuthoring.BuildGrid(map);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
            }
        }
    }
}
