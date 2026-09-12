using System;
using Landsong.ECS.Authoring;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    /// <summary>Authored references for a placement preview. Hierarchy discovery belongs to the editor migration/validator.</summary>
    public sealed class BuildingPlacementPreviewBinding : MonoBehaviour
    {
        [Serializable]
        public struct RenderPart
        {
            [Sirenix.OdinInspector.LabelText("渲染器")]
            public MeshRenderer Renderer;
            [Sirenix.OdinInspector.LabelText("槽位")]
            public BuildingVisualSlotAuthoring Slot;
            [Sirenix.OdinInspector.LabelText("作物")]
            public bool Crop;
        }

        [Sirenix.OdinInspector.LabelText("碰撞体")]
        public Collider[] Colliders = Array.Empty<Collider>();
        [Sirenix.OdinInspector.LabelText("行为组件")]
        public Behaviour[] Behaviours = Array.Empty<Behaviour>();
        [Sirenix.OdinInspector.LabelText("槽位")]
        public BuildingVisualSlotAuthoring[] Slots = Array.Empty<BuildingVisualSlotAuthoring>();
        [Sirenix.OdinInspector.LabelText("部件")]
        public RenderPart[] Parts = Array.Empty<RenderPart>();
        public void ValidateConfiguration()
        {
            if (Colliders == null || Behaviours == null || Slots == null || Parts == null || Parts.Length == 0)
                throw new InvalidOperationException(name + " 的建筑放置预览引用未配置。");
            foreach (var item in Colliders)
                if (item == null)
                    throw new InvalidOperationException(name + " 的预览碰撞体引用为空。");
            foreach (var item in Behaviours)
                if (item == null)
                    throw new InvalidOperationException(name + " 的预览行为组件引用为空。");
            foreach (var item in Slots)
                if (item == null)
                    throw new InvalidOperationException(name + " 的预览槽位引用为空。");
            foreach (var item in Parts)
                if (item.Renderer == null)
                    throw new InvalidOperationException(name + " 的预览渲染器引用为空。");
        }

        public void Configure(int level, string skin)
        {
            ValidateConfiguration();
            foreach (var item in Colliders)
                item.enabled = false;
            foreach (var item in Behaviours)
                item.enabled = false;
            var chosen = Select(BuildingVisualPurpose.Preview, level, skin) ?? Select(BuildingVisualPurpose.Operational, level, skin);
            foreach (var part in Parts)
            {
                part.Renderer.enabled = !part.Crop && (part.Slot == null || part.Slot == chosen);
                part.Renderer.gameObject.layer = 2;
            }
        }

        BuildingVisualSlotAuthoring Select(BuildingVisualPurpose purpose, int level, string skin)
        {
            BuildingVisualSlotAuthoring selected = null;
            int best = -1;
            foreach (var slot in Slots)
            {
                bool rendered = false;
                foreach (var part in Parts)
                    if (part.Slot == slot)
                    {
                        rendered = true;
                        break;
                    }

                if (!rendered)
                    continue;
                int score = BuildingVisualResolver.Score(slot.Purpose, slot.Level, slot.Step, slot.SkinId, slot.Placeholder, purpose, level, 1, skin);
                if (score > best)
                {
                    best = score;
                    selected = slot;
                }
            }

            return selected;
        }
    }
}
