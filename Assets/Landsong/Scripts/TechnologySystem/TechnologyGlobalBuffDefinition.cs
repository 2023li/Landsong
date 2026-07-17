using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.ConditionSystem;
using Landsong.InventorySystem;
using Landsong.Localization;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.TechnologySystem
{
    [CreateAssetMenu(
        menuName = "Landsong/Technology/Global Buff Definition",
        fileName = "TechnologyGlobalBuff")]
    public sealed class TechnologyGlobalBuffDefinition : ScriptableObject
    {
        [SerializeField, LabelText("Buff ID")]
        private string buffId;

        [SerializeField, LabelText("显示名称")]
        private string displayName;

        [SerializeField, PreviewField(64), LabelText("Buff 图标")]
        private Sprite icon;

        [SerializeReference, LabelText("生效条件")]
        private GameCondition activationCondition;

        [SerializeReference, LabelText("Buff 效果")]
        private TechnologyGlobalBuffEffect[] effects = Array.Empty<TechnologyGlobalBuffEffect>();

        public string BuffId => string.IsNullOrWhiteSpace(buffId) ? string.Empty : buffId.Trim();
        public string DisplayName => L10n.ContentName(
            "technology_buff",
            BuffId,
            string.IsNullOrWhiteSpace(displayName) ? BuffId : displayName.Trim());
        public Sprite Icon => icon;
        public GameCondition ActivationCondition => activationCondition;
        public IReadOnlyList<TechnologyGlobalBuffEffect> Effects =>
            effects ?? Array.Empty<TechnologyGlobalBuffEffect>();
        public bool IsValid => !string.IsNullOrWhiteSpace(BuffId) && Effects.Count > 0;

        public bool IsActive(GameSystem context) =>
            activationCondition == null || activationCondition.IsMet(context);

        public int GetBuildingResourceProductionFlatBonus(
            GameSystem context,
            BuildingBase building,
            string itemId)
        {
            if (!IsValid || !IsActive(context))
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < Effects.Count; i++)
            {
                total = checked(total + Mathf.Max(
                    0,
                    Effects[i]?.GetBuildingResourceProductionFlatBonus(building, itemId) ?? 0));
            }

            return total;
        }

        public float GetInventoryLossRateMultiplier(GameSystem context)
        {
            if (!IsValid || !IsActive(context))
            {
                return 1f;
            }

            var multiplier = 1f;
            for (var i = 0; i < Effects.Count; i++)
            {
                multiplier *= Mathf.Max(
                    0f,
                    Effects[i]?.GetInventoryLossRateMultiplier() ?? 1f);
            }

            return Mathf.Max(0f, multiplier);
        }

        public string Describe()
        {
            var descriptions = new List<string>();
            for (var i = 0; i < Effects.Count; i++)
            {
                var description = Effects[i]?.Describe();
                if (!string.IsNullOrWhiteSpace(description))
                {
                    descriptions.Add(description.Trim());
                }
            }

            return descriptions.Count == 0 ? DisplayName : string.Join("；", descriptions);
        }

        public void ConfigureNumericData(
            string stableBuffId,
            string localizedDisplayName,
            Sprite displayIcon,
            GameCondition condition,
            IEnumerable<TechnologyGlobalBuffEffect> buffEffects)
        {
            buffId = string.IsNullOrWhiteSpace(stableBuffId) ? string.Empty : stableBuffId.Trim();
            displayName = string.IsNullOrWhiteSpace(localizedDisplayName)
                ? buffId
                : localizedDisplayName.Trim();
            icon = displayIcon;
            activationCondition = condition;
            effects = buffEffects == null
                ? Array.Empty<TechnologyGlobalBuffEffect>()
                : new List<TechnologyGlobalBuffEffect>(buffEffects).ToArray();
            Normalize();
        }

        private void OnEnable() => Normalize();
        private void OnValidate() => Normalize();

        private void Normalize()
        {
            buffId = string.IsNullOrWhiteSpace(buffId) ? string.Empty : buffId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? buffId : displayName.Trim();
            effects ??= Array.Empty<TechnologyGlobalBuffEffect>();
            for (var i = 0; i < effects.Length; i++)
            {
                effects[i]?.Normalize();
            }
        }
    }

    [Serializable]
    public abstract class TechnologyGlobalBuffEffect
    {
        public virtual int GetBuildingResourceProductionFlatBonus(
            BuildingBase building,
            string itemId) => 0;

        public virtual float GetInventoryLossRateMultiplier() => 1f;

        public abstract string Describe();

        public virtual void Normalize()
        {
        }
    }

    [Serializable]
    public sealed class TechnologyGlobalBuffEffect_BuildingProductionFlat :
        TechnologyGlobalBuffEffect
    {
        [SerializeField, AssetsOnly, LabelText("目标建筑家族")]
        private BuildingFamilyDefinition targetFamily;

        [SerializeField, AssetsOnly, LabelText("目标产出物品")]
        private ItemDefinition itemDefinition;

        [SerializeField, LabelText("每次生产固定加成"), Min(0)]
        private int flatBonus = 1;

        public BuildingFamilyDefinition TargetFamily => targetFamily;
        public ItemDefinition ItemDefinition => itemDefinition;
        public int FlatBonus => Mathf.Max(0, flatBonus);

        public void Configure(
            BuildingFamilyDefinition family,
            ItemDefinition item,
            int amount)
        {
            targetFamily = family;
            itemDefinition = item;
            flatBonus = amount;
            Normalize();
        }

        public override int GetBuildingResourceProductionFlatBonus(
            BuildingBase building,
            string itemId)
        {
            return building != null
                   && targetFamily != null
                   && ReferenceEquals(building.FamilyDefinition, targetFamily)
                   && itemDefinition != null
                   && string.Equals(itemDefinition.ItemId, itemId, StringComparison.Ordinal)
                ? FlatBonus
                : 0;
        }

        public override string Describe()
        {
            var familyName = targetFamily?.Definition?.DisplayName
                             ?? Landsong.Localization.L10n.Gameplay("gameplay.quest.ui.target_building", "目标建筑");
            var itemName = itemDefinition?.DisplayName
                           ?? Landsong.Localization.L10n.Gameplay("gameplay.building.function.resources", "资源");
            return Landsong.Localization.L10n.Gameplay(
                "gameplay.technology.buff.production_bonus",
                "所有{0}每次产出额外提供 {1} {2}",
                familyName,
                FlatBonus,
                itemName);
        }

        public override void Normalize()
        {
            flatBonus = Mathf.Max(0, flatBonus);
        }
    }

    [Serializable]
    public sealed class TechnologyGlobalBuffEffect_InventoryLossReduction :
        TechnologyGlobalBuffEffect
    {
        [SerializeField, LabelText("库存损耗降低百分比"), Range(0f, 100f)]
        private float reductionPercent = 10f;

        public float ReductionPercent => Mathf.Clamp(reductionPercent, 0f, 100f);
        public float LossRateMultiplier => Mathf.Clamp01(1f - ReductionPercent / 100f);

        public void Configure(float percent)
        {
            reductionPercent = percent;
            Normalize();
        }

        public override float GetInventoryLossRateMultiplier() =>
            LossRateMultiplier;

        public override string Describe() =>
            $"所有物资库存损耗率降低 {ReductionPercent:0.#}%";

        public override void Normalize()
        {
            reductionPercent = Mathf.Clamp(reductionPercent, 0f, 100f);
        }
    }
}
