using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class UnitCombatStatsCompiler
    {
        public static void Compile(ref BlobBuilder builder, UnitCombatStatsSource source, ref global::Landsong.ECS.Definitions.UnitCombatStats target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 UnitCombatStats 配置。");
            if (!CombatProfile.Valid(source.Profile))
                throw new InvalidOperationException("战斗行为配置无效。");
            if (!math.isfinite(source.Strength) || source.Strength < 0 || !math.isfinite(source.Intelligence) || source.Intelligence < 0 ||
                !math.isfinite(source.Agility) || source.Agility < 0 || !math.isfinite(source.Vitality) || source.Vitality < 0 ||
                (byte)source.AttackAttribute > (byte)AttackAttributeKind.Intelligence)
                throw new InvalidOperationException("单位基础属性必须为非负有限数值，且攻击属性有效。");
            target.Strength = source.Strength;
            target.Intelligence = source.Intelligence;
            target.Agility = source.Agility;
            target.Vitality = source.Vitality;
            target.AttackAttribute = source.AttackAttribute;
            if (!math.isfinite(source.MaximumHealth))
                throw new InvalidOperationException("生命上限必须是有限数值。");
            target.MaximumHealth = source.MaximumHealth;
            if (!math.isfinite(source.Damage))
                throw new InvalidOperationException("基础攻击必须是有限数值。");
            target.Damage = source.Damage;
            if (!math.isfinite(source.AttackRange))
                throw new InvalidOperationException("攻击距离必须是有限数值。");
            target.AttackRange = source.AttackRange;
            if (!math.isfinite(source.AttackIntervalSeconds))
                throw new InvalidOperationException("攻击间隔（秒）必须是有限数值。");
            target.AttackIntervalSeconds = source.AttackIntervalSeconds;
            if (!math.isfinite(source.MovementSpeed))
                throw new InvalidOperationException("移动速度必须是有限数值。");
            target.MovementSpeed = source.MovementSpeed;
            if (!math.isfinite(source.ProjectileSpeed) || source.ProjectileSpeed < 0)
                throw new InvalidOperationException("弹体速度必须是非负有限数值（0 表示近战）。");
            target.ProjectileSpeed = source.ProjectileSpeed;
            target.Profile = source.Profile;
        }
    }
}
