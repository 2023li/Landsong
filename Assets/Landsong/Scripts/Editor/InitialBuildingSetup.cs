using System;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.GridSystem;
using UnityEditor;

namespace Landsong.EditorTools
{
    public static class InitialBuildingSetup
    {
        public static int EnsureBindings(MapContentAuthoring content)
        {
            var folder = content.transform.Find(GameMapWorkflow.Buildings);
            if (folder == null)
                return 0;
            int changed = 0;
            foreach (var visual in folder.GetComponentsInChildren<BuildingVisualAuthoring>(true))
            {
                if (visual.GetComponentInParent<MapContentAuthoring>() != content)
                    continue;
                var preview = visual.GetComponent<InitialBuildingPreview>();
                if (preview == null && visual.GetComponentInParent<InitialBuildingPreview>() != null)
                    continue;
                if (preview != null && preview.Definition != null)
                    continue;
                if (visual.Definition == null)
                    throw new InvalidOperationException("初始建筑缺少有效的建筑内容定义：" + visual.name);
                if (preview == null)
                    preview = Undo.AddComponent<InitialBuildingPreview>(visual.gameObject);
                Undo.RecordObject(preview, "绑定初始建筑定义");
                preview.Definition = visual.Definition;
                EditorUtility.SetDirty(preview);
                PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
                changed++;
            }

            return changed;
        }
    }
}
