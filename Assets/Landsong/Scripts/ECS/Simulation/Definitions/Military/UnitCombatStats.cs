using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct UnitCombatStats
    {
        public float Strength;
        public float Intelligence;
        public float Agility;
        public float Vitality;
        public AttackAttributeKind AttackAttribute;
        public float MaximumHealth;
        public float Damage;
        public float AttackRange;
        public float AttackIntervalSeconds;
        public float MovementSpeed;
        public float ProjectileSpeed;
        public CombatProfile Profile;
    }

    public enum AttackAttributeKind : byte
    {
        Strength,
        Intelligence
    }
}
