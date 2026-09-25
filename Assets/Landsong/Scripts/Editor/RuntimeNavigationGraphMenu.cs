using Landsong.ECS;
using Pathfinding;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class RuntimeNavigationGraphMenu
    {
        [MenuItem("Landsong/ECS/导航/显示运行时导航图")]
        public static void Show() => TryShow();

        public static bool TryShow()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("请先进入 Play 并加载游戏地图，再显示运行时导航图。");
                return false;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning("尚未找到运行中的 ECS 世界。");
                return false;
            }

            var em = world.EntityManager;
            var root = WorldQueries.Root(em);
            if (root == Entity.Null || !em.HasComponent<SimulationReady>(root) || !em.HasComponent<GridData>(root))
            {
                Debug.LogWarning("游戏地图尚未加载完成，请进入地图后重试。");
                return false;
            }

            em.CompleteAllTrackedJobs();
            AstarNavigationRuntime.EnsureServices();
            AstarNavigationRuntime.SetEditorGraphVisible(true);
            if (!AstarNavigationRuntime.EnsureGraph(em, root))
            {
                Debug.LogWarning("A* Pro 运行时导航图尚未创建。");
                return false;
            }

            AstarNavigationRuntime.SetEditorGraphVisible(true);
            Selection.activeGameObject = AstarPath.active.gameObject;
            SceneView.RepaintAll();
            Debug.Log("运行时导航图已显示。请切到 Scene 视图并开启 Gizmos；图中显示 PointGraph 节点与连线。");
            return true;
        }

        [MenuItem("Landsong/ECS/导航/隐藏运行时导航图")]
        public static void Hide()
        {
            AstarNavigationRuntime.SetEditorGraphVisible(false);
            SceneView.RepaintAll();
            Debug.Log("运行时导航图已隐藏。");
        }
    }
}
