using Sirenix.OdinInspector;
using Unity.Entities;
using UnityEngine;

namespace Landsong.Animation
{
    [DisallowMultipleComponent]
    public sealed class SoldierAnimationVisualAuthoring : MonoBehaviour
    {
        [LabelText("动作配置类型")] public UnitAnimationProfile Profile;
        bool UsesEquipment => Profile == UnitAnimationProfile.SwordAndTorch;
        [LabelText("动画器"), Required] public Animator Animator;
        [LabelText("剑挂载根"), Required, ShowIf(nameof(UsesEquipment))] public Transform SwordMount;
        [LabelText("右手武器挂点"), Required, ShowIf(nameof(UsesEquipment))] public Transform SwordHandSocket;
        [LabelText("火把挂载根"), Required, ShowIf(nameof(UsesEquipment))] public Transform TorchMount;
        [LabelText("火把火焰粒子"), ShowIf(nameof(UsesEquipment))] public ParticleSystem TorchFlameParticles;
        [LabelText("火把点光"), ShowIf(nameof(UsesEquipment))] public Light TorchLight;
        [LabelText("火把动画层索引"), MinValue(1), ShowIf(nameof(UsesEquipment))] public int TorchLayer = 1;

        [LabelText("双臂庆祝动画层索引（动物为 0）"), MinValue(0)] public int CelebrationLayer = 3;

        sealed class Baker : Baker<SoldierAnimationVisualAuthoring>
        {
            public override void Bake(SoldierAnimationVisualAuthoring source)
            {
                if (source.Animator == null || source.Animator.runtimeAnimatorController == null
                    || source.UsesEquipment && (source.SwordMount == null || source.SwordHandSocket == null
                    || source.TorchMount == null))
                    throw new System.InvalidOperationException(source.name + " 缺少动画器或装备挂点。");
                if (source.UsesEquipment && (source.TorchFlameParticles == null) != (source.TorchLight == null))
                    throw new System.InvalidOperationException(source.name + " 的火把粒子与点光必须一起配置。");
                if (source.UsesEquipment && source.TorchFlameParticles != null
                    && (!source.TorchFlameParticles.transform.IsChildOf(source.TorchMount) || !source.TorchLight.transform.IsChildOf(source.TorchMount)))
                    throw new System.InvalidOperationException(source.name + " 的火把粒子与点光必须位于火把挂点下。");
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SoldierAnimationBinding {
                    Rig = GetEntity(source.Animator.gameObject, TransformUsageFlags.Dynamic),
                    SwordMount = source.UsesEquipment ? GetEntity(source.SwordMount.gameObject, TransformUsageFlags.Dynamic) : Entity.Null,
                    SwordHandSocket = source.UsesEquipment ? GetEntity(source.SwordHandSocket.gameObject, TransformUsageFlags.Dynamic) : Entity.Null,
                    TorchMount = source.UsesEquipment ? GetEntity(source.TorchMount.gameObject, TransformUsageFlags.Dynamic) : Entity.Null,
                    TorchFlame = source.UsesEquipment && source.TorchFlameParticles != null ? GetEntity(source.TorchFlameParticles.gameObject, TransformUsageFlags.Dynamic) : Entity.Null,
                    TorchLight = source.UsesEquipment && source.TorchLight != null ? GetEntity(source.TorchLight.gameObject, TransformUsageFlags.Dynamic) : Entity.Null,
                    TorchLayer = source.UsesEquipment ? (byte)source.TorchLayer : (byte)0,
                    CelebrationLayer = (byte)source.CelebrationLayer
                });
                var renderers = AddBuffer<SoldierAnimationRenderer>(entity);
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                    renderers.Add(new SoldierAnimationRenderer { Entity = GetEntity(renderer.gameObject, TransformUsageFlags.Renderable) });
            }
        }
    }
}
