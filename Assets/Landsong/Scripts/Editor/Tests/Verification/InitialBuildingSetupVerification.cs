using System;
using System.IO;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.GridSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.EditorTools
{
    public static class InitialBuildingSetupVerification
    {
        public static string Run()
        {
            var log = new StringBuilder();
            int count = 0;
            void Check(bool condition, string message)
            {
                if (!condition)
                    throw new InvalidOperationException(message);
                count++;
                log.AppendLine("PASS " + message);
            }

            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Initial building binding verification");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var content = root.AddComponent<MapContentAuthoring>();
                var folder = new GameObject(GameMapWorkflow.Buildings);
                folder.transform.SetParent(root.transform);
                var palace = AssetDatabase.LoadAssetAtPath<BuildingDefinitionAsset>("Assets/Landsong/ECSContent/Definitions/Building/b王宫.asset");
                var building = new GameObject("任意实例名称");
                building.transform.SetParent(folder.transform);
                building.transform.position = new Vector3(12.75f, 3, 8.5f);
                var visual = building.AddComponent<BuildingVisualAuthoring>();
                visual.Definition = palace;
                var before = building.transform.position;
                Check(InitialBuildingSetup.EnsureBindings(content) == 1, "Dragging a formal building into the initial-building folder creates its authoring binding");
                var preview = building.GetComponent<InitialBuildingPreview>();
                Check(preview.Definition == palace && preview.Level == 1 && content.Previews.Length == 1, "Definition comes from authored references, independent of instance name");
                Check(building.transform.position == before, "Binding preserves the user's placement");
                Check(InitialBuildingSetup.EnsureBindings(content) == 0 && building.GetComponents<InitialBuildingPreview>().Length == 1, "Repeated validation does not duplicate preview components");
                preview.Level = 2;
                InitialBuildingSetup.EnsureBindings(content);
                Check(preview.Level == 2, "Explicit initial levels are preserved");
                preview.Definition = null;
                Check(InitialBuildingSetup.EnsureBindings(content) == 1 && preview.Definition == palace && preview.Level == 2, "An empty binding can be repaired without resetting its level");
                var child = new GameObject("嵌套视觉");
                child.transform.SetParent(building.transform);
                child.AddComponent<BuildingVisualAuthoring>().Definition = palace;
                Check(InitialBuildingSetup.EnsureBindings(content) == 0 && child.GetComponent<InitialBuildingPreview>() == null, "Nested visuals do not become duplicate initial buildings");
                var outside = new GameObject("展示建筑");
                outside.transform.SetParent(root.transform);
                outside.AddComponent<BuildingVisualAuthoring>().Definition = palace;
                Check(InitialBuildingSetup.EnsureBindings(content) == 0 && outside.GetComponent<InitialBuildingPreview>() == null, "Visual buildings outside the designated folder are not implicitly registered");
                Check(palace.Prefab.GetComponent<InitialBuildingPreview>() == null, "Runtime prefab assets remain free of scene authoring bindings");
                log.AppendLine("Assertions: " + count);
                return log.ToString();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/initial-building-setup-verification.txt", log.ToString());
            }
        }
    }
}
