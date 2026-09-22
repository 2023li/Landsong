using Landsong.Content;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent, AddComponentMenu("Landsong/ECS/Building Visual Root")]
    public sealed class BuildingVisualAuthoring : MonoBehaviour
    {
        [Tooltip("仅用于配置校验和编辑器打开定义；运行时状态仍来自 Building 实体。")]
        [LabelText("建筑内容定义"), Required]
        public Definitions.BuildingDefinitionAsset Definition;
        sealed class Baker : Baker<BuildingVisualAuthoring>
        {
            public override void Bake(BuildingVisualAuthoring source)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var slots = AddBuffer<BuildingVisualSlot>(entity);
                AddComponent(entity, new BuildingVisualSelection());
                Transform selectionAnchor = null;
                foreach (var candidate in source.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name != "SelectionAnchor")
                        continue;
                    if (selectionAnchor != null)
                        throw new System.InvalidOperationException(source.name + " 配置了多个 SelectionAnchor。");
                    selectionAnchor = candidate;
                }

                if (selectionAnchor == null)
                    throw new System.InvalidOperationException(source.name + " 缺少 SelectionAnchor。");
                DependsOn(selectionAnchor);
                AddComponent(entity, new BuildingSelectionAnchor { Value = GetEntity(selectionAnchor.gameObject, TransformUsageFlags.Dynamic) });
                foreach (var slot in GetComponentsInChildren<BuildingVisualSlotAuthoring>())
                {
                    DependsOn(slot);
                    if (GetComponentsInChildren<MeshRenderer>(slot.gameObject).Length == 0)
                        continue;
                    slots.Add(new BuildingVisualSlot { Slot = GetEntity(slot.gameObject, TransformUsageFlags.Dynamic), Purpose = slot.Purpose, Level = slot.Level, Step = slot.Step, Skin = new FixedString64Bytes(slot.SkinId ?? ""), Placeholder = (byte)(slot.Placeholder ? 1 : 0) });
                }
            }
        }
    }
}
