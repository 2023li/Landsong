using Landsong.ECS;
using Unity.Entities;
using UnityEngine;

namespace Landsong.Content
{
    public static class BuildingVisualResolver
    {
        public static int Score(BuildingVisualPurpose purpose, int level, int step, string skin, bool placeholder, BuildingVisualPurpose wanted, int currentLevel, int currentStep, string currentSkin)
        {
            if (purpose != wanted || level > currentLevel || step > currentStep)
                return -1;
            var exactSkin = (skin ?? "") == (currentSkin ?? "");
            if (!exactSkin && (purpose == BuildingVisualPurpose.Operational || !string.IsNullOrEmpty(skin)))
                return -1;
            return (exactSkin ? 100000 : 0) + level * 1000 + step * 2 + (placeholder ? 0 : 1);
        }

        public static BuildingVisualPurpose Purpose(LifeStage stage) => stage == LifeStage.Construction ? BuildingVisualPurpose.Construction : stage == LifeStage.Ruined ? BuildingVisualPurpose.Ruined : stage == LifeStage.Repairing ? BuildingVisualPurpose.Repairing : BuildingVisualPurpose.Operational;
        public static Entity Select(EntityManager em, Entity building)
        {
            if (!em.HasBuffer<BuildingVisualSlot>(building))
                return Entity.Null;
            var b = em.GetComponentData<Building>(building);
            BuildingAppearanceState bAppearance = em.GetComponentData<BuildingAppearanceState>(building);
            BuildingConstructionState bConstruction = em.GetComponentData<BuildingConstructionState>(building);
            var wanted = Purpose(b.Stage);
            Entity Find(BuildingVisualPurpose purpose)
            {
                var selected = Entity.Null;
                var best = -1;
                foreach (var slot in em.GetBuffer<BuildingVisualSlot>(building))
                {
                    var score = Score(slot.Purpose, slot.Level, slot.Step, slot.Skin.ToString(), slot.Placeholder != 0, purpose, b.Level, bConstruction.Progress + 1, bAppearance.Skin.ToString());
                    if (score > best)
                    {
                        best = score;
                        selected = slot.Slot;
                    }
                }

                return selected;
            }

            var result = Find(wanted);
            if (result == Entity.Null && wanted == BuildingVisualPurpose.Repairing)
                result = Find(BuildingVisualPurpose.Construction);
            if (result == Entity.Null && wanted != BuildingVisualPurpose.Operational)
                result = Find(BuildingVisualPurpose.Operational);
            return result;
        }

        public static BuildingVisualSlotAuthoring Select(GameObject prefab, LifeStage stage, int level, int step, string skin, bool preview = false)
        {
            var all = prefab.GetComponentsInChildren<BuildingVisualSlotAuthoring>(true);
            BuildingVisualSlotAuthoring Find(BuildingVisualPurpose purpose)
            {
                BuildingVisualSlotAuthoring selected = null;
                var best = -1;
                foreach (var slot in all)
                {
                    if (slot.GetComponentsInChildren<MeshRenderer>(true).Length == 0)
                        continue;
                    var score = Score(slot.Purpose, slot.Level, slot.Step, slot.SkinId, slot.Placeholder, purpose, level, step, skin);
                    if (score > best)
                    {
                        best = score;
                        selected = slot;
                    }
                }

                return selected;
            }

            var result = preview ? Find(BuildingVisualPurpose.Preview) : Find(Purpose(stage));
            if (result == null && stage == LifeStage.Repairing)
                result = Find(BuildingVisualPurpose.Construction);
            return result ?? Find(BuildingVisualPurpose.Operational);
        }
    }
}
