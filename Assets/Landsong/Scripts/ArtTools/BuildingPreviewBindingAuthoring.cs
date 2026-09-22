#if UNITY_EDITOR
using System.Linq;
using Landsong.Content;
using Landsong.ECS.Authoring;
using Landsong.ECS.Presentation;
using UnityEditor;
using UnityEngine;

namespace Landsong.VisualSystem
{
    public static class BuildingPreviewBindingAuthoring
    {
        public static BuildingPlacementPreviewBinding Rebind(GameObject root, bool undo = false)
        {
            var binding = root.GetComponent<BuildingPlacementPreviewBinding>();
            if (binding == null) binding = undo ? Undo.AddComponent<BuildingPlacementPreviewBinding>(root) : root.AddComponent<BuildingPlacementPreviewBinding>();
            if (undo) Undo.RecordObject(binding, "更新建筑预览引用");
            binding.Colliders = root.GetComponentsInChildren<Collider>(true);
            binding.Behaviours = root.GetComponentsInChildren<Behaviour>(true).Where(b => b != binding).ToArray();
            binding.Slots = root.GetComponentsInChildren<BuildingVisualSlotAuthoring>(true);
            binding.Parts = root.GetComponentsInChildren<MeshRenderer>(true).Select(renderer => new BuildingPlacementPreviewBinding.RenderPart
            {
                Renderer = renderer,
                Slot = renderer.GetComponentInParent<BuildingVisualSlotAuthoring>(true),
                Crop = renderer.TryGetComponent<BuildingVisualPartAuthoring>(out var part) && !string.IsNullOrEmpty(part.CropId)
            }).ToArray();
            EditorUtility.SetDirty(binding);
            return binding;
        }
    }
}
#endif
