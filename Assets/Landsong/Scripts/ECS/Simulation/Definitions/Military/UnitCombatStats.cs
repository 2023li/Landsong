using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct UnitCombatStats
    {
        public float MaximumHealth;
        public float Damage;
        public float AttackRange;
        public float AttackIntervalSeconds;
        public float MovementSpeed;
        public float ProjectileSpeed;
        public CombatProfile Profile;
    }
}
