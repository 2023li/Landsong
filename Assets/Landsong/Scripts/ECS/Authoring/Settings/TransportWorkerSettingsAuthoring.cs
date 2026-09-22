using System;
using Sirenix.OdinInspector;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class TransportWorkerSettingsAuthoring : MonoBehaviour
    {
        [LabelText("运输工人（男）"), Required] public GameObject Male;
        [LabelText("运输工人（女）"), Required] public GameObject Female;
        [LabelText("生命值"), Min(1)] public float Health = 30;
        [LabelText("行走速度"), Min(.1f)] public float Speed = 1.6f;
        [LabelText("装货时间"), Min(.1f)] public float LoadSeconds = 1.133333f;
        [LabelText("卸货时间"), Min(.1f)] public float UnloadSeconds = 1.333333f;

        sealed class Baker : Baker<TransportWorkerSettingsAuthoring>
        {
            public override void Bake(TransportWorkerSettingsAuthoring source)
            {
                if (source.Male == null || source.Female == null || !float.IsFinite(source.Health) || source.Health <= 0
                    || !float.IsFinite(source.Speed) || source.Speed <= 0 || !float.IsFinite(source.LoadSeconds) || source.LoadSeconds <= 0
                    || !float.IsFinite(source.UnloadSeconds) || source.UnloadSeconds <= 0)
                    throw new InvalidOperationException("运输工人缺少预制体或有效数值。");
                AddComponent(GetEntity(TransformUsageFlags.None), new TransportWorkerSettings {
                    Male = GetEntity(source.Male, TransformUsageFlags.Dynamic), Female = GetEntity(source.Female, TransformUsageFlags.Dynamic),
                    Health = source.Health, Speed = source.Speed, LoadSeconds = source.LoadSeconds, UnloadSeconds = source.UnloadSeconds
                });
            }
        }
    }
}
