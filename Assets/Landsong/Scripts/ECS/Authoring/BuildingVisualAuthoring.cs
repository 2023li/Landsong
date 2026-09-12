using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent, AddComponentMenu("Landsong/ECS/Building Visual Root")]
    public sealed class BuildingVisualAuthoring : MonoBehaviour
    {
        [Tooltip("仅用于配置校验和编辑器打开定义；运行时状态仍来自 Building 实体。")] [LabelText("建筑内容定义")] public GameDefinitionAsset Definition;
        sealed class Baker : Baker<BuildingVisualAuthoring>
        {
            public override void Bake(BuildingVisualAuthoring source)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic); var slots = AddBuffer<BuildingVisualSlot>(entity); AddComponent(entity, new BuildingVisualSelection());
                foreach (var slot in GetComponentsInChildren<BuildingVisualSlotAuthoring>())
                {
                    DependsOn(slot);
                    if (GetComponentsInChildren<MeshRenderer>(slot.gameObject).Length == 0) continue;
                    slots.Add(new BuildingVisualSlot { Slot = GetEntity(slot.gameObject, TransformUsageFlags.Dynamic), Purpose = slot.Purpose, Level = slot.Level, Step = slot.Step, Skin = new FixedString64Bytes(slot.SkinId ?? ""), Placeholder = (byte)(slot.Placeholder ? 1 : 0) });
                }
            }
        }
    }
    public static class BuildingVisualResolver
    {
        public static int Score(BuildingVisualPurpose purpose, int level, int step, string skin, bool placeholder, BuildingVisualPurpose wanted, int currentLevel, int currentStep, string currentSkin)
        {
            if (purpose != wanted || level > currentLevel || step > currentStep) return -1;
            var exactSkin = (skin ?? "") == (currentSkin ?? "");
            if (!exactSkin && (purpose == BuildingVisualPurpose.Operational || !string.IsNullOrEmpty(skin))) return -1;
            return (exactSkin ? 100000 : 0) + level * 1000 + step * 2 + (placeholder ? 0 : 1);
        }
        public static BuildingVisualPurpose Purpose(LifeStage stage) => stage == LifeStage.Construction ? BuildingVisualPurpose.Construction : stage == LifeStage.Ruined ? BuildingVisualPurpose.Ruined : stage == LifeStage.Repairing ? BuildingVisualPurpose.Repairing : BuildingVisualPurpose.Operational;
        public static Entity Select(EntityManager em, Entity building)
        {
            if (!em.HasBuffer<BuildingVisualSlot>(building)) return Entity.Null;
            var b = em.GetComponentData<Building>(building); var wanted = Purpose(b.Stage);
            Entity Find(BuildingVisualPurpose purpose)
            {
                var selected = Entity.Null; var best = -1;
                foreach (var slot in em.GetBuffer<BuildingVisualSlot>(building))
                {
                    var score = Score(slot.Purpose, slot.Level, slot.Step, slot.Skin.ToString(), slot.Placeholder != 0, purpose, b.Level, b.Progress + 1, b.Skin.ToString());
                    if (score > best) { best = score; selected = slot.Slot; }
                }
                return selected;
            }
            var result = Find(wanted);
            if (result == Entity.Null && wanted == BuildingVisualPurpose.Repairing) result = Find(BuildingVisualPurpose.Construction);
            if (result == Entity.Null && wanted != BuildingVisualPurpose.Operational) result = Find(BuildingVisualPurpose.Operational);
            return result;
        }
        public static BuildingVisualSlotAuthoring Select(GameObject prefab, LifeStage stage, int level, int step, string skin, bool preview = false)
        {
            var all = prefab.GetComponentsInChildren<BuildingVisualSlotAuthoring>(true);
            BuildingVisualSlotAuthoring Find(BuildingVisualPurpose purpose)
            {
                BuildingVisualSlotAuthoring selected = null; var best = -1;
                foreach (var slot in all) { if (slot.GetComponentsInChildren<MeshRenderer>(true).Length == 0) continue; var score = Score(slot.Purpose, slot.Level, slot.Step, slot.SkinId, slot.Placeholder, purpose, level, step, skin); if (score > best) { best = score; selected = slot; } }
                return selected;
            }
            var result = preview ? Find(BuildingVisualPurpose.Preview) : Find(Purpose(stage));
            if (result == null && stage == LifeStage.Repairing) result = Find(BuildingVisualPurpose.Construction);
            return result ?? Find(BuildingVisualPurpose.Operational);
        }
    }
}
