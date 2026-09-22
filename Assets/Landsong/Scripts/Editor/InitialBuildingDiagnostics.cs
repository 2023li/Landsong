using System;
using System.Linq;
using System.Text;
using Landsong.GridSystem;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.EditorTools
{
    public static class InitialBuildingDiagnostics
    {
        public static string RepairCurrent()
        {
            var scene = SceneManager.GetActiveScene();
            bool wasDirty = scene.isDirty;
            var content = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapContentAuthoring>(true)).Single();
            int changed = InitialBuildingSetup.EnsureBindings(content);
            GameMapWorkflow.Validate(content);
            if (!wasDirty && !UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("场景保存失败。");
            return "Initial building bindings added: " + changed + "; recognized previews: " + content.Previews.Length + "; full map validation passed; " + (wasDirty ? "previous scene edits left unsaved." : "scene saved.");
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
        public static string Inspect()
        {
            var scene = SceneManager.GetActiveScene();
            var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var report = new StringBuilder();
            report.AppendLine(scene.path + " dirty=" + scene.isDirty);
            foreach (var content in objects.Select(t => t.GetComponent<MapContentAuthoring>()).Where(c => c != null))
                report.AppendLine("MAP " + Path(content.transform) + " previews=" + content.Previews.Length);
            foreach (var t in objects.Where(t => t.name.Contains("王宫") || t.GetComponent<InitialBuildingPreview>() != null))
            {
                var preview = t.GetComponent<InitialBuildingPreview>();
                report.AppendLine("OBJECT " + Path(t) + " id=" + t.gameObject.GetInstanceID() + " active=" + t.gameObject.activeInHierarchy + " pos=" + t.position + " scale=" + t.lossyScale + " prefab=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) + " components=" + string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
                if (preview != null)
                    report.AppendLine("PREVIEW definition=" + (preview.Definition == null ? "null" : preview.Definition.Metadata.Id) + " level=" + preview.Level + " owner=" + (preview.GetComponentInParent<MapContentAuthoring>()?.name ?? "none"));
            }

            var palace = AssetDatabase.LoadAssetAtPath<BuildingDefinitionAsset>("Assets/Landsong/ECSContent/Definitions/Building/b王宫.asset");
            report.AppendLine("PALACE " + JsonUtility.ToJson(palace.Capabilities.Housing));
            return report.ToString();
        }
    }
}
