using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Landsong.ECS
{
    public static class EquipmentOps
    {
        public static ItemId ItemForWeapon(EntityManager em, Entity root, SoldierWeaponKind weapon)
        {
            if (weapon == SoldierWeaponKind.None)
                return ItemId.None;
            for (int i = 0; i < ItemDefinitions.Count(em, root); i++)
            {
                var item = ItemId.FromIndex(i);
                if (ItemDefinitions.Get(em, root, item).Equipment.Weapon == weapon)
                    return item;
            }
            return ItemId.None;
        }

        public static void ReturnWeapon(EntityManager em, Entity root, SoldierWeaponKind weapon)
        {
            var item = ItemForWeapon(em, root, weapon);
            if (item.IsValid)
                InventoryOps.Add(em, root, item, 1, pending: true);
        }

        public static float EffectiveBreakChance(float baseChance, float modifier) =>
            math.saturate(baseChance * math.max(0f, 1f + modifier));

        public static void SettleNight(EntityManager em, Entity root)
        {
            var night = em.GetComponentData<NightRuntimeState>(root);
            using var soldiers = WorldQueries.OrderedEntities<Soldier>(em);
            foreach (var unit in soldiers)
            {
                var soldier = em.GetComponentData<Soldier>(unit);
                var item = ItemForWeapon(em, root, soldier.Weapon);
                if (!item.IsValid)
                    continue;
                var alive = EntityState.Alive(em, unit);
                var participated = em.HasComponent<Combatant>(unit) && em.GetComponentData<Combatant>(unit).Participated != 0;
                var baseBreakChance = ItemDefinitions.Get(em, root, item).Equipment.BreakChance;
                var definition = em.GetComponentData<SoldierDefinitionRef>(unit).Definition;
                var modifier = SoldierEffects.Modifier(em, root, NumericEffectKind.EquipmentBreakChanceMultiplier, definition);
                var breakChance = EffectiveBreakChance(baseBreakChance, modifier);
                var identity = em.GetComponentData<Identity>(unit);
                var random = new Random(math.max(1u, math.hash(new uint4(night.Seed, (uint)identity.Id, (uint)(identity.Id >> 32), (uint)item.Index))));
                var broken = !alive || participated && random.NextFloat() < breakChance;
                if (broken)
                {
                    soldier.Weapon = SoldierWeaponKind.None;
                    em.SetComponentData(unit, soldier);
                    SimulationEvents.Emit(em, root, EventKind.Message,
                        new FixedString128Bytes(identity.Name.ToString() + "的" + ItemDefinitions.Get(em, root, item).Metadata.Name.ToString() + "在战斗后损坏"), identity.Id);
                }
            }
        }
    }
}
