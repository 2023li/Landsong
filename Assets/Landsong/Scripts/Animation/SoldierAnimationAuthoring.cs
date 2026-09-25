using Landsong.ECS;
using Sirenix.OdinInspector;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.Animation
{
    public enum UnitAnimationProfile : byte
    {
        [LabelText("剑与火把切换")] SwordAndTorch,
        [LabelText("基础动作（无切换装备）")] Basic,
        [LabelText("动物动作（Generic 骨骼）")] GenericCreature,
        [LabelText("运输工人（抱物与装卸）")] TransportWorker
    }

    public enum SoldierEquipmentState : byte
    {
        Torch,
        DrawingSword,
        Sword,
        SheathingSword
    }

    public struct SoldierAnimationPrefab : IComponentData
    {
        public UnitAnimationProfile Profile;
        public Entity Prefab;
        public float DeathSeconds, AttackSeconds, HitSeconds, DrawSeconds, SheatheSeconds;
        public float DrawDistancePadding, AlertReleaseSeconds, WeaponReleaseSeconds;
    }

    public struct SoldierAnimationBinding : IComponentData
    {
        public Entity Rig, SwordMount, ClubMount, BowMount, SwordHandSocket, TorchMount, TorchFlame, TorchLight;
        public byte TorchLayer, WeaponLayer, CelebrationLayer;
    }
    public struct SoldierAnimationViewOwner : IComponentData { public Entity Unit; }

    public struct SoldierAnimationState : IComponentData
    {
        public Entity View;
        public float3 Position;
        public float Speed, DeathRemaining, AlertRemaining, WeaponRemaining, EquipmentRemaining, PoseOverrideRemaining;
        public uint AttackSequence, HitSequence;
        public byte Initialized, WasVisible, Dying;
        public SoldierEquipmentState Equipment;
    }

    public struct SoldierAnimationRenderer : IBufferElementData { public Entity Entity; }

    [DisallowMultipleComponent]
    public sealed class SoldierAnimationAuthoring : MonoBehaviour
    {
        [LabelText("动作配置类型")] public UnitAnimationProfile Profile;
        bool UsesEquipment => Profile == UnitAnimationProfile.SwordAndTorch;
        [Tooltip("独立的动画表现预制体；出勤时创建，归营或死亡展示结束后释放。")]
        [LabelText("表现预制体"), Required] public GameObject VisualPrefab;
        [LabelText("死亡展示时长"), Min(.1f)] public float DeathSeconds = 2;
        [LabelText("攻击动画原始时长"), Min(.01f)] public float AttackSeconds = 1;
        [LabelText("受击动画时长（无动作时为 0）"), Min(0)] public float HitSeconds = .5f;
        [LabelText("拔剑动画时长"), Min(.01f), ShowIf(nameof(UsesEquipment))] public float DrawSeconds = 1;
        [LabelText("收剑动画时长"), Min(.01f), ShowIf(nameof(UsesEquipment))] public float SheatheSeconds = 1;
        [LabelText("提前拔剑距离"), Min(0), ShowIf(nameof(UsesEquipment))] public float DrawDistancePadding = 1.5f;
        [LabelText("警戒解除延迟"), Min(0)] public float AlertReleaseSeconds = 2;
        [LabelText("收剑延迟"), Min(0), ShowIf(nameof(UsesEquipment))] public float WeaponReleaseSeconds = 2;

        sealed class Baker : Baker<SoldierAnimationAuthoring>
        {
            public override void Bake(SoldierAnimationAuthoring source)
            {
                if (!System.Enum.IsDefined(typeof(UnitAnimationProfile), source.Profile))
                    throw new System.InvalidOperationException(source.name + " 配置了未知动作类型。");
                if (source.VisualPrefab == null || source.VisualPrefab.GetComponent<SoldierAnimationVisualAuthoring>() == null)
                    throw new System.InvalidOperationException(source.name + " 缺少士兵动画表现预制体。");
                if (source.VisualPrefab.GetComponent<SoldierAnimationVisualAuthoring>().Profile != source.Profile)
                    throw new System.InvalidOperationException(source.name + " 的逻辑与表现动作类型不一致。");
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<AnimatedUnitVisual>(entity);
                AddComponent<UnitAnimationSignals>(entity);
                AddComponent<SoldierAnimationState>(entity);
                AddComponent(entity, new SoldierAnimationPrefab {
                    Profile = source.Profile,
                    Prefab = GetEntity(source.VisualPrefab, TransformUsageFlags.Dynamic),
                    DeathSeconds = source.DeathSeconds,
                    AttackSeconds = source.AttackSeconds,
                    HitSeconds = source.HitSeconds,
                    DrawSeconds = source.DrawSeconds,
                    SheatheSeconds = source.SheatheSeconds,
                    DrawDistancePadding = source.DrawDistancePadding,
                    AlertReleaseSeconds = source.AlertReleaseSeconds,
                    WeaponReleaseSeconds = source.WeaponReleaseSeconds
                });
            }
        }
    }
}
