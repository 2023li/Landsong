#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.ECS.Presentation;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    public static class GameUiStableRowsVerification
    {
        public static string Run()
        {
            int assertions = 0;
            void Check(bool value, string message)
            {
                assertions++;
                if (!value)
                    throw new InvalidOperationException("稳定条目验证失败：" + message);
            }

            var game = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/UI/Prefabs/GamePanel/UI_GamePanel.prefab");
            var root = game.GetComponent<UI_GamePanel>();
            Check(root != null, "缺少游戏面板根组件");
            Check(root.FeaturePanels.OfType<UI_GamePanel_List>().All(panel => panel.RowTemplate != null), "每个功能面板必须持有自己的条目模板引用");
            Check(root.FeaturePanels.OfType<UI_GamePanel_List>().All(panel => EditorUtility.IsPersistent(panel.RowTemplate)), "功能面板条目模板必须来自独立资产而非根节点公共对象");
            Check(root.Buildings.ConfirmRowTemplate != null && root.BuildingDetails.RowTemplate != null, "建筑确认和建筑详情必须各自配置条目模板");
            var inventory = root.InventoryWindow;
            inventory.ValidateConfiguration();
            Check(inventory.BuildingTemplate.transform.parent == inventory.BuildingsScroll.content, "建筑模板属于建筑列表");
            Check(inventory.ResourceTemplate.transform.parent == inventory.ResourcesScroll.content, "资源模板属于资源列表");
            Check(inventory.PendingTemplate.transform.parent == inventory.PendingContent, "待存模板属于独立待存区");
            Check(!inventory.BuildingTemplate.gameObject.activeSelf && !inventory.ResourceTemplate.gameObject.activeSelf && !inventory.PendingTemplate.gameObject.activeSelf, "真实模板默认隐藏");
            Check(Array.TrueForAll(game.GetComponentsInChildren<MonoBehaviour>(true), component => component == null || component.GetType().Name != "UI_GamePanel_RowRenderer"), "根预制体不能保留公共行渲染器");
            ResearchRows(Check);
            return "GameUiStableRows Assertions: " + assertions + " passed";
        }

        static void ResearchRows(Action<bool, string> check)
        {
            using var definitions = new ResearchTestFixture();
            definitions.Target.Prerequisites.TechnologyRequirements = new[]
            {
                new TechnologyRequirementSource
                {
                    Technology = definitions.First,
                    Required = 1
                },
                new TechnologyRequirementSource
                {
                    Technology = definitions.Middle,
                    Required = 1
                }
            };
            using var world = new World("Typed research row identities");
            var em = world.EntityManager;
            var root = em.CreateEntity(typeof(SimulationReady));
            definitions.Install(em, root);
            ResearchTestFixture.AddFacts(em, root);
            em.AddComponentData(root, new Session { Phase = Phase.Day });
            em.AddComponentData(root, new SimulationControl());
            em.AddComponentData(root, new PersistenceGate());
            em.AddComponentData(root, new IntelligenceModeState());
            em.AddComponentData(root, new ResearchState());
            var progress = em.AddBuffer<TechnologyProgress>(root);
            progress.Add(new TechnologyProgress { Technology = definitions.FirstId, QueueOrder = 1 });
            progress.Add(new TechnologyProgress { Technology = definitions.MiddleId, QueueOrder = 2 });
            FeatureUnlocks.Unlock(em, root, ResearchTestFixture.AccessId);
            var game = PrefabUtility.LoadPrefabContents("Assets/Landsong/UI/Prefabs/GamePanel/UI_GamePanel.prefab");
            var camera = new GameObject("Research row camera", typeof(Camera));
            var canvas = new GameObject("Research row canvas", typeof(Canvas), typeof(CanvasScaler));
            var events = new GameObject("Research row events", typeof(EventSystem));
            var view = game.GetComponent<UI_GamePanel>();
            var technology = view.Technology;
            var panel = view.GetListPanel(GamePanelId.Technology);
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var selected = typeof(UI_GamePanel_Technology).GetField("selectedTechnology", hidden);
            var begin = typeof(UI_GamePanel_List).GetMethod("BeginRender", hidden);
            var end = typeof(UI_GamePanel_List).GetMethod("EndRender", hidden);
            try
            {
                view.BindSession(em, root, camera.GetComponent<Camera>(), canvas.GetComponent<CanvasScaler>(), events.GetComponent<EventSystem>());
                panel.gameObject.SetActive(true);
                selected.SetValue(technology, definitions.TargetId);
                void Render()
                {
                    begin.Invoke(panel, null);
                    technology.Render();
                    end.Invoke(panel, null);
                }

                UI_GamePanel_Row[] Rows(string prefix) => technology.TechnologyTree.DetailRows.GetComponentsInChildren<UI_GamePanel_Row>(true).Where(row => row.gameObject.activeSelf && row.Identity?.StartsWith(prefix, StringComparison.Ordinal) == true).OrderBy(row => row.transform.GetSiblingIndex()).ToArray();
                Render();
                var queue = Rows("domain:research-queue:");
                var prerequisites = Rows("domain:research-prerequisite:");
                check(queue.Length == 2 && queue.Select(row => row.Identity).Distinct().Count() == 2, "真实科技展示器为两个 TechnologyId 生成不同研究队列身份");
                check(prerequisites.Length == 2 && prerequisites.Select(row => row.Identity).Distinct().Count() == 2, "真实科技展示器为两个前置科技生成不同身份");
                check(queue[0].Label.text.Contains("root") && queue[1].Label.text.Contains("middle"), "首帧队列顺序来自真实 TechnologyProgress.QueueOrder");
                var firstKey = queue[0].Identity;
                var secondKey = queue[1].Identity;
                progress = em.GetBuffer<TechnologyProgress>(root);
                var first = progress[0];
                first.QueueOrder = 2;
                progress[0] = first;
                var second = progress[1];
                second.QueueOrder = 1;
                progress[1] = second;
                Render();
                var reordered = Rows("domain:research-queue:");
                check(reordered.Length == 2 && reordered[0] == queue[1] && reordered[1] == queue[0] && reordered[0].Identity == secondKey && reordered[1].Identity == firstKey, "重排实际研究队列保留同一条目对象与身份，只改变显示顺序");
                check(reordered[0].Label.text.StartsWith("#1 middle", StringComparison.Ordinal) && reordered[1].Label.text.StartsWith("#2 root", StringComparison.Ordinal), "保留对象重新绑定最新队列序号和科技名称");
                reordered[0].Select.onClick.Invoke();
                check((TechnologyId)selected.GetValue(technology) == definitions.MiddleId, "重排后点击保留条目仍选择该条目的真实 TechnologyId");
                reordered[1].Select.onClick.Invoke();
                check((TechnologyId)selected.GetValue(technology) == definitions.FirstId, "第二个保留条目仍选择另一项科技，而非位置或类型名称");
            }
            finally
            {
                // The edit-mode fixture owns these runtime-created objects and releases them synchronously.
                foreach (var row in technology.TechnologyTree.DetailRows.GetComponentsInChildren<UI_GamePanel_Row>(true))
                    UnityEngine.Object.DestroyImmediate(row.gameObject);
                foreach (var node in technology.TechnologyTree.GraphScroll.content.GetComponentsInChildren<UI_GamePanel_TechnologyNode>(true))
                    if (node != technology.TechnologyTree.NodeTemplate)
                        UnityEngine.Object.DestroyImmediate(node.gameObject);
                view.UnbindSession();
                PrefabUtility.UnloadPrefabContents(game);
                UnityEngine.Object.DestroyImmediate(camera);
                UnityEngine.Object.DestroyImmediate(canvas);
                UnityEngine.Object.DestroyImmediate(events);
            }
        }
    }
}
#endif
