using Landsong.Content;
using Sirenix.OdinInspector;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class ActorVisualPrefabAuthoring : MonoBehaviour
    {
        [LabelText("表现预制体上的角色组件"), Required] public MonoBehaviour ViewPrefab;
        [LabelText("位置偏移")] public Vector3 Offset;
        [LabelText("模型缩放")] public Vector3 Scale = Vector3.one;

        sealed class Baker : Baker<ActorVisualPrefabAuthoring>
        {
            public override void Bake(ActorVisualPrefabAuthoring source)
            {
                if (source.ViewPrefab == null)
                    throw new System.InvalidOperationException(source.name + "：角色逻辑 Prefab 缺少表现 View。");
                DependsOn(source.ViewPrefab);
                AddComponent(GetEntity(TransformUsageFlags.Dynamic), new ActorVisualPrefab
                {
                    Prefab = source.ViewPrefab,
                    Offset = source.Offset,
                    Scale = source.Scale
                });
            }
        }
    }
}
