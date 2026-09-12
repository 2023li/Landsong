#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.VisualSystem
{
    // Editor-only bridge in the same assembly as the export pipeline.
    public static class LS_BuildingVisualSlotBinding
    {
        public static void BindExport(GameDefinitionAsset definition, GameObject exported, BuildingVisualPurpose purpose, int level)
        {
            if (definition?.Data.Prefab == null) throw new InvalidOperationException("Building needs a root prefab before binding visual slots.");
            var path = AssetDatabase.GetAssetPath(definition.Data.Prefab);
            var backup = Path.Combine("Backups", "BuildingVisualExport_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"), path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(path, backup);
            if (File.Exists(path + ".meta")) File.Copy(path + ".meta", backup + ".meta");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = root.GetComponent<BuildingVisualAuthoring>() ?? root.AddComponent<BuildingVisualAuthoring>(); visual.Definition = definition;
                var skin = definition.Data.Building?.DefaultSkin ?? "";
                var slot = root.GetComponentsInChildren<BuildingVisualSlotAuthoring>(true).FirstOrDefault(s => s.Purpose == purpose && s.Level == level && (s.SkinId ?? "") == skin);
                if (slot == null) { var go = new GameObject(purpose + "_LV" + level); go.transform.SetParent(root.transform, false); slot = go.AddComponent<BuildingVisualSlotAuthoring>(); slot.Purpose = purpose; slot.Level = level; slot.SkinId = skin; }
                foreach (Transform child in slot.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var instance = Object.Instantiate(exported); instance.name = "Content"; instance.transform.SetParent(slot.transform, false); slot.Placeholder = false;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) (renderer.GetComponent<EntityVisualAuthoring>() ?? renderer.gameObject.AddComponent<EntityVisualAuthoring>()).Owner = root;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
