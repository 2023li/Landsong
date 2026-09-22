using System;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Landsong.Verification
{
    public sealed class TwcFlowLab : MonoBehaviour
    {
        [LabelText("TWC 地形根对象")]
        public Transform Geometry;
        [LabelText("观察相机")]
        public Camera View;
        [LabelText("入口与出口")]
        public Transform[] Starts, Ends;
        [LabelText("验证单位")]
        public NavMeshAgent[] Agents;
        [LabelText("路线显示")]
        public LineRenderer[] Lines;
        [LabelText("生成记录")]
        public string GenerationEvidence;
        public int SourceCount { get; private set; }

        [LabelText("抵达次数")]
        public int[] Arrivals = new int[3];
        public string Status { get; private set; } = "等待导航构建";

        NavMeshData data;
        NavMeshDataInstance instance;
        readonly bool[] returning = new bool[3];
        [NonSerialized]
        Font font;
        GUIStyle label, title;
        float yaw = 25, targetYaw = 25;
        public NavMeshQueryFilter Filter => new NavMeshQueryFilter
        {
            agentTypeID = 0,
            areaMask = NavMesh.AllAreas
        };

        public void BuildNavigation()
        {
            var sources = new List<NavMeshBuildSource>();
            foreach (var collider in Geometry.GetComponentsInChildren<MeshCollider>())
            {
                if (!collider.enabled || collider.sharedMesh == null)
                    continue;
                if (!collider.sharedMesh.isReadable)
                    throw new InvalidOperationException("TWC 碰撞网格不可读：" + collider.name);
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, sourceObject = collider.sharedMesh, transform = collider.transform.localToWorldMatrix, area = 0, component = collider });
            }

            if (sources.Count == 0)
                throw new InvalidOperationException("TWC 未生成 MeshCollider；禁止用方块替代。");
            SourceCount = sources.Count;
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = .15f;
            settings.agentHeight = 1.1f;
            settings.agentClimb = .15f;
            settings.agentSlope = 48;
            settings.overrideVoxelSize = true;
            settings.voxelSize = .035f;
            settings.overrideTileSize = true;
            settings.tileSize = 128;
            settings.minRegionArea = .05f;
            var next = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(new Vector3(16, 3, 12), new Vector3(40, 14, 32)), Vector3.zero, Quaternion.identity);
            if (next == null)
                throw new InvalidOperationException("TWC 导航构建失败。");
            Release();
            data = next;
            instance = NavMesh.AddNavMeshData(data);
        }

        public bool Path(Vector3 from, Vector3 to, out NavMeshPath path)
        {
            path = new NavMeshPath();
            if (!NavMesh.SamplePosition(from, out var a, .18f, Filter) || !NavMesh.SamplePosition(to, out var b, .18f, Filter))
                return false;
            return NavMesh.CalculatePath(a.position, b.position, Filter, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        public bool Ground(Vector3 point, out RaycastHit nearest)
        {
            nearest = default;
            var ray = new Ray(new Vector3(point.x, 12, point.z), Vector3.down);
            bool found = false;
            float distance = float.MaxValue;
            foreach (var collider in Geometry.GetComponentsInChildren<MeshCollider>())
                if (collider.enabled && collider.Raycast(ray, out var hit, 24) && hit.distance < distance)
                {
                    nearest = hit;
                    distance = hit.distance;
                    found = true;
                }

            return found;
        }

        public void Release()
        {
            if (instance.valid)
                instance.Remove();
            if (data == null)
                return;
            if (Application.isPlaying)
                Destroy(data);
            else
                DestroyImmediate(data);
            data = null;
        }

        void Start()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
            try
            {
                BuildNavigation();
                var messages = new List<string>();
                string[] names =
                {
                    "TWC 斜坡上下",
                    "TWC 高架面",
                    "TWC 桥下地面"
                };
                for (int i = 0; i < 3; i++)
                {
                    bool valid = Path(Starts[i].position, Ends[i].position, out var path);
                    messages.Add(names[i] + "：" + (valid ? "连通" : "失败"));
                    Lines[i].positionCount = valid ? path.corners.Length : 0;
                    if (!valid)
                        continue;
                    var corners = path.corners;
                    var display = new List<Vector3>();
                    for (int j = 1; j < corners.Length; j++)
                    {
                        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(corners[j - 1], corners[j]) / .1f));
                        for (int s = 0; s <= steps; s++)
                        {
                            var p = Vector3.Lerp(corners[j - 1], corners[j], s / (float)steps);
                            if (i == 0 && Ground(p, out var ground))
                                p.y = ground.point.y;
                            display.Add(p + Vector3.up * .06f);
                        }
                    }

                    Lines[i].positionCount = display.Count;
                    Lines[i].SetPositions(display.ToArray());
                    Agents[i].transform.position = Starts[i].position;
                    Agents[i].gameObject.SetActive(true);
                    Agents[i].enabled = true;
                    if (!Agents[i].Warp(Starts[i].position))
                        throw new InvalidOperationException("探针落地失败");
                    Agents[i].SetDestination(Ends[i].position);
                }

                Status = string.Join("\n", messages);
            }
            catch (Exception e)
            {
                Status = e.Message;
                Debug.LogException(e, this);
            }
        }

        void Update()
        {
            for (int i = 0; i < Agents.Length; i++)
            {
                var agent = Agents[i];
                if (!agent.enabled || !agent.isOnNavMesh || agent.pathPending || !agent.hasPath || agent.remainingDistance > .15f)
                    continue;
                Arrivals[i]++;
                returning[i] = !returning[i];
                agent.SetDestination(returning[i] ? Starts[i].position : Ends[i].position);
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.wasPressedThisFrame)
                    targetYaw -= 90;
                if (Keyboard.current.eKey.wasPressedThisFrame)
                    targetYaw += 90;
            }

            yaw = Mathf.LerpAngle(yaw, targetYaw, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
            View.transform.rotation = Quaternion.Euler(50, yaw, 0);
            View.transform.position = new Vector3(16, 0, 11) - View.transform.forward * 40;
        }

        void OnDestroy()
        {
            if (Agents != null)
                foreach (var a in Agents)
                    if (a != null)
                        a.enabled = false;
            Release();
            if (font != null)
                Destroy(font);
        }

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = 15,
                    wordWrap = true
                };
                title = new GUIStyle(label)
                {
                    fontSize = 21,
                    fontStyle = FontStyle.Bold
                };
            }

            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Mathf.Max(.5f, Screen.height / 800f));
            GUI.Box(new Rect(12, 12, 310, 390), GUIContent.none);
            GUILayout.BeginArea(new Rect(26, 23, 282, 368));
            GUILayout.Label("TWC 真实生成流程验证", title);
            GUILayout.Label("官方瓦片 + Ramp 预设\nBlueprint → Tiles Build → MeshCollider → NavMesh", label);
            GUILayout.Space(12);
            GUILayout.Label(Status, label);
            GUILayout.Label("往返到达次数：" + string.Join(" / ", Arrivals), label);
            GUILayout.Space(12);
            GUILayout.Label("导航只读取 TWC 实际生成的碰撞网格，共 " + SourceCount + " 个。", label);
            GUILayout.Space(12);
            GUILayout.Label("坡道：0.35 → 1.35\n高架面：3.35；桥下：0.35\nQ / E：旋转观察", label);
            GUILayout.Space(12);
            GUILayout.Label("这是 TWC／导航实验，尚未接入正式 ECS 地图烘焙。", label);
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
    }
}
