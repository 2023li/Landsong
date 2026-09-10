#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.VisualSystem
{
    // Compile editor-authored fixed points into static render children, then normal ECS baking owns visibility.
    // No live MonoBehaviour polls crop state and no random point state needs to be saved.
    public static class LS_EcsCropVisualCompiler
    {
        public const string GeneratedName = "_ECS_Crop_Renderers";
        public static int Compile(BuildingCropVisualController controller, GameObject buildingRoot)
        {
            if (!controller.TryValidateConfiguration(out var error)) throw new InvalidOperationException(error);
            var points = controller.GetEditorPlantPoints(); if (points.Length == 0) throw new InvalidOperationException("Convert legacy crop points before compiling ECS visuals.");
            foreach (var crop in controller.CropPrefabs) foreach (var stage in crop.GrowthStages)
                if (stage.StageRoot.GetComponentsInChildren<Component>(true).Any(c => c == null || !(c is Transform || c is MeshFilter || c is MeshRenderer || c is LODGroup))) throw new InvalidOperationException(crop.CropId + " stage contains unsupported dynamic components; use static crop meshes.");
            var old = controller.transform.parent.Find(GeneratedName); if (old != null) Object.DestroyImmediate(old.gameObject);
            var generated = new GameObject(GeneratedName); generated.transform.SetParent(controller.transform.parent, false);
            generated.transform.localPosition = controller.transform.localPosition; generated.transform.localRotation = controller.transform.localRotation; generated.transform.localScale = controller.transform.localScale;
            var count = 0;
            foreach (var crop in controller.CropPrefabs)
            {
                for (var p = 0; p < Math.Min(points.Length, crop.TargetPlantCount); p++)
                {
                    var point = points[p]; var plant = new GameObject(crop.CropId + "_" + p); plant.transform.SetParent(generated.transform, false);
                    plant.transform.localPosition = point.LocalPosition; plant.transform.localRotation = point.LocalRotation; plant.transform.localScale = Vector3.one * point.UniformScale;
                    for (var i = 0; i < crop.GrowthStages.Count; i++)
                    {
                        var stage = crop.GrowthStages[i]; var instance = Object.Instantiate(stage.StageRoot); instance.name = stage.StageRoot.name; instance.transform.SetParent(plant.transform, false); instance.SetActive(true);
                        var part = instance.AddComponent<BuildingVisualPartAuthoring>(); part.CropId = crop.CropId; part.GrowthFrom = stage.StartGrowthPercent / 100; part.GrowthTo = i + 1 < crop.GrowthStages.Count ? crop.GrowthStages[i + 1].StartGrowthPercent / 100 : 1;
                        foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true)) { renderer.gameObject.SetActive(true); (renderer.GetComponent<EntityVisualAuthoring>() ?? renderer.gameObject.AddComponent<EntityVisualAuthoring>()).Owner = buildingRoot; count++; }
                    }
                }
            }
            EditorUtility.SetDirty(buildingRoot); return count;
        }
    }
}
#endif
