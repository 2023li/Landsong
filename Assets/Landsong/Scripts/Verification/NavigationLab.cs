using System;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Landsong.Verification
{
    [DisallowMultipleComponent]
    public sealed class NavigationLab : MonoBehaviour
    {
        [Header("独立导航实验；不接入正式 ECS 建造／战斗")]
        [LabelText("TWC 配置")]
        public UnityEngine.Object TwcConfiguration;
        [LabelText("地形根对象")]
        public Transform Geometry;
        [LabelText("桥梁")]
        public GameObject Bridge;
        [LabelText("阶梯")]
        public GameObject Stairs;
        [LabelText("观察相机")]
        public Camera View;
        [LabelText("入口")]
        public Transform[] Starts;
        [LabelText("出口")]
        public Transform[] Ends;
        [LabelText("验证单位")]
        public NavMeshAgent[] Probes;
        [LabelText("路线显示")]
        public LineRenderer[] Routes;
        [LabelText("界面字体")]
        public Font InterfaceFont;
        [LabelText("单位半径")]
        [Min(.01f)]
        public float AgentRadius = .22f;
        [LabelText("单位高度")]
        [Min(.1f)]
        public float AgentHeight = 1.2f;
        [LabelText("台阶高度")]
        [Min(0)]
        public float StepHeight = .36f;
        [LabelText("最大坡度")]
        [Range(0, 60)]
        public float Slope = 45;
        [LabelText("烘焙范围")]
        public Bounds BakeBounds = new Bounds(new Vector3(16, 4, 14), new Vector3(36, 14, 32));
        public bool IsNight { get; private set; }
        public bool Damaged { get; private set; }
        public int Revision { get; private set; }
        public string Report { get; private set; } = "进入 Play 后自动生成导航";

        public readonly bool[] CompleteRoutes = new bool[3];
        public readonly int[] Arrivals = new int[3];
        NavMeshData data;
        NavMeshDataInstance instance;
        readonly bool[] returning = new bool[3];
        float yaw = 35, targetYaw = 35;
        Vector3 focus = new Vector3(16, 1.8f, 12.5f);
        GUIStyle heading, label, button;
        [NonSerialized]
        Font generatedFont;
        string notice = "白天：建造与拆除；夜间：三组单位沿实际导航路径移动。";
        public NavMeshQueryFilter Filter
        {
            get
            {
                var filter = new NavMeshQueryFilter
                {
                    agentTypeID = 0,
                    areaMask = NavMesh.AllAreas
                };
                filter.SetAreaCost(3, Damaged ? 3 : 1);
                filter.SetAreaCost(4, Damaged ? 3 : 1);
                return filter;
            }
        }

        void Start()
        {
            if (InterfaceFont == null)
                generatedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
            try
            {
                Rebuild();
            }
            catch (Exception e)
            {
                Report = "初始化失败：" + e.Message;
                Debug.LogException(e, this);
            }
        }

        public NavMeshData BuildData()
        {
            var sources = new List<NavMeshBuildSource>();
            foreach (var surface in Geometry.GetComponentsInChildren<NavigationLabSurface>())
            {
                var box = surface.GetComponent<BoxCollider>();
                if (!box.enabled || !box.gameObject.activeInHierarchy)
                    continue;
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = box.transform.localToWorldMatrix * Matrix4x4.Translate(box.center), size = box.size, area = surface.NavigationArea, component = box });
            }

            if (sources.Count == 0)
                throw new InvalidOperationException("没有导航几何。");
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = AgentRadius;
            settings.agentHeight = AgentHeight;
            settings.agentClimb = StepHeight;
            settings.agentSlope = Slope;
            settings.overrideVoxelSize = true;
            settings.voxelSize = .06f;
            settings.overrideTileSize = true;
            settings.tileSize = 128;
            settings.minRegionArea = .1f;
            var result = NavMeshBuilder.BuildNavMeshData(settings, sources, BakeBounds, Vector3.zero, Quaternion.identity);
            if (result == null)
                throw new InvalidOperationException("NavMesh 构建失败。");
            return result;
        }

        public void Rebuild()
        {
            // Build first: a failed bake does not discard the previous working navigation.
            var next = BuildData();
            StopProbes();
            ReleaseNavigation();
            data = next;
            instance = NavMesh.AddNavMeshData(data);
            Revision++;
            RefreshRoutes();
            if (IsNight)
                StartProbes();
        }

        public void ReleaseNavigation()
        {
            if (instance.valid)
                instance.Remove();
            if (data != null)
            {
                if (Application.isPlaying)
                    Destroy(data);
                else
                    DestroyImmediate(data);
                data = null;
            }
        }

        void OnDestroy()
        {
            StopProbes();
            ReleaseNavigation();
            if (generatedFont != null)
                Destroy(generatedFont);
        }

        public bool SetConstruction(bool bridge, bool stairs)
        {
            if (IsNight)
            {
                notice = "夜间禁止建造／拆除，请先切回白天。";
                return false;
            }

            bool oldBridge = Bridge.activeSelf, oldStairs = Stairs.activeSelf;
            Bridge.SetActive(bridge);
            Stairs.SetActive(stairs);
            try
            {
                Rebuild();
            }
            catch
            {
                Bridge.SetActive(oldBridge);
                Stairs.SetActive(oldStairs);
                throw;
            }

            notice = "导航已更新；桥下地面保留。";
            return true;
        }

        public void SetNight(bool night)
        {
            IsNight = night;
            if (night)
                StartProbes();
            else
                StopProbes();
            notice = night ? "夜间：青色走桥面，红色走桥下，金色走阶梯。" : "白天：可建造／拆除桥与阶梯。";
        }

        public void SetDamaged(bool damaged)
        {
            Damaged = damaged;
            foreach (var agent in Probes)
            {
                if (!agent.enabled || !agent.isOnNavMesh)
                    continue;
                agent.SetAreaCost(3, damaged ? 3 : 1);
                agent.SetAreaCost(4, damaged ? 3 : 1);
            }

            RefreshRoutes();
            if (IsNight)
                for (int i = 0; i < Probes.Length; i++)
                    if (Probes[i].enabled && Probes[i].isOnNavMesh)
                        Probes[i].SetDestination(returning[i] ? Starts[i].position : Ends[i].position);
            notice = damaged ? "受损：桥与阶梯仍可通行，实验通行速度为正常的 1/3。" : "已修复：通行效率恢复。";
        }

        public bool TryPath(Vector3 start, Vector3 end, out NavMeshPath path)
        {
            path = new NavMeshPath();
            // Tight sampling prevents a query on the bridge from silently landing on the valley floor.
            if (!NavMesh.SamplePosition(start, out var from, .22f, Filter) || !NavMesh.SamplePosition(end, out var to, .22f, Filter))
                return false;
            return NavMesh.CalculatePath(from.position, to.position, Filter, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        public void RefreshRoutes()
        {
            string[] names =
            {
                "桥面东西通行",
                "桥下南北通行",
                "阶梯上下通行"
            };
            var lines = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                CompleteRoutes[i] = TryPath(Starts[i].position, Ends[i].position, out var path);
                lines.Add(names[i] + "：" + (CompleteRoutes[i] ? "连通" : "不连通"));
                Routes[i].positionCount = CompleteRoutes[i] ? path.corners.Length : 0;
                if (CompleteRoutes[i])
                {
                    var points = path.corners;
                    for (int p = 0; p < points.Length; p++)
                        points[p] += Vector3.up * .07f;
                    Routes[i].SetPositions(points);
                }
            }

            Report = string.Join("\n", lines);
        }

        void StopProbes()
        {
            if (Probes == null)
                return;
            foreach (var agent in Probes)
                if (agent != null)
                {
                    agent.enabled = false;
                    agent.gameObject.SetActive(false);
                }
        }

        void StartProbes()
        {
            StopProbes();
            for (int i = 0; i < Probes.Length; i++)
            {
                if (!CompleteRoutes[i])
                    continue;
                var agent = Probes[i];
                agent.gameObject.SetActive(true);
                agent.transform.position = Starts[i].position;
                agent.enabled = true;
                if (!agent.Warp(Starts[i].position))
                    throw new InvalidOperationException("实验单位无法落在指定通行面。");
                agent.SetAreaCost(3, Damaged ? 3 : 1);
                agent.SetAreaCost(4, Damaged ? 3 : 1);
                returning[i] = false;
                agent.SetDestination(Ends[i].position);
            }
        }

        void Update()
        {
            if (IsNight)
                for (int i = 0; i < Probes.Length; i++)
                {
                    var agent = Probes[i];
                    if (!agent.enabled || !agent.isOnNavMesh)
                        continue;
                    bool onStructure = NavMesh.SamplePosition(agent.transform.position, out var hit, .2f, Filter) && (hit.mask & ((1 << 3) | (1 << 4))) != 0;
                    agent.speed = Damaged && onStructure ? 1.1f : 3.3f;
                    if (!agent.pathPending && agent.hasPath && agent.remainingDistance < .2f)
                    {
                        Arrivals[i]++;
                        returning[i] = !returning[i];
                        agent.SetDestination(returning[i] ? Starts[i].position : Ends[i].position);
                    }
                }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame)
                    targetYaw -= 90;
                if (keyboard.eKey.wasPressedThisFrame)
                    targetYaw += 90;
            }

            if (View != null)
            {
                yaw = Mathf.LerpAngle(yaw, targetYaw, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
                View.transform.rotation = Quaternion.Euler(48, yaw, 0);
                View.transform.position = focus - View.transform.forward * 44;
                var mouse = Mouse.current;
                if (mouse != null && mouse.position.ReadValue().x > 340)
                    View.orthographicSize = Mathf.Clamp(View.orthographicSize - mouse.scroll.ReadValue().y * .01f, 11, 27);
            }
        }

        void OnGUI()
        {
            if (heading == null)
            {
                var font = InterfaceFont != null ? InterfaceFont : generatedFont;
                heading = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = 21,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                label = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = 15,
                    wordWrap = true
                };
                button = new GUIStyle(GUI.skin.button)
                {
                    font = font,
                    fontSize = 15,
                    fixedHeight = 34
                };
            }

            float scale = Mathf.Max(.5f, Screen.height / 800f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUI.Box(new Rect(12, 12, 320, 530), GUIContent.none);
            GUILayout.BeginArea(new Rect(26, 22, 292, 510));
            GUILayout.Label("多高度导航 · 最小验证", heading);
            GUILayout.Label("独立实验场景 / 非正式 ECS 地图", label);
            GUILayout.Space(8);
            GUILayout.Label("谷底 0.35m  /  桥面 5.15m\n同一 XZ 保留上下两个通行面", label);
            GUILayout.Space(8);
            if (GUILayout.Button(IsNight ? "切回白天 · 停止单位" : "进入夜间 · 开始单位通行", button))
                SetNight(!IsNight);
            GUI.enabled = !IsNight;
            if (GUILayout.Button(Bridge.activeSelf ? "拆除吊桥" : "建造吊桥", button))
                SetConstruction(!Bridge.activeSelf, Stairs.activeSelf);
            if (GUILayout.Button(Stairs.activeSelf ? "拆除阶梯" : "建造阶梯", button))
                SetConstruction(Bridge.activeSelf, !Stairs.activeSelf);
            GUI.enabled = true;
            if (GUILayout.Button(Damaged ? "修复 · 恢复通行效率" : "模拟受损 · 降低通行效率", button))
                SetDamaged(!Damaged);
            GUILayout.Space(8);
            GUILayout.Label(Report, label);
            GUILayout.Label($"导航版本 {Revision}   到达次数 {Arrivals[0]} / {Arrivals[1]} / {Arrivals[2]}", label);
            GUILayout.Space(8);
            GUILayout.Label(notice, label);
            GUILayout.Space(8);
            GUILayout.Label("Q / E：旋转 90°   滚轮：缩放\n相机硬软边界不在本次验证范围。", label);
            GUILayout.EndArea();
            GUI.matrix = old;
        }
    }
}
