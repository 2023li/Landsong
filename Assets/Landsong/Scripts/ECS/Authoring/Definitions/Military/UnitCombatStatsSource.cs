using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class UnitCombatStatsSource
    {
        [LabelText("生命上限")]
        public float MaximumHealth = 100;
        [LabelText("基础攻击")]
        public float Damage = 10;
        [LabelText("攻击距离")]
        public float AttackRange = 1.4f;
        [LabelText("攻击间隔（秒）")]
        public float AttackIntervalSeconds = 1;
        [LabelText("移动速度")]
        public float MovementSpeed = 2;
        [LabelText("弹体速度（0 = 近战直接命中）")]
        public float ProjectileSpeed = 10;
        [LabelText("战斗行为")]
        public CombatProfile Profile = CombatProfile.Default;
    }
}
