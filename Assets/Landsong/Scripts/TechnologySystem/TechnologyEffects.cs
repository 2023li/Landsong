using System;
using Landsong.BuildingSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.TechnologySystem
{
    public enum TechnologyEffectPresentationCategory
    {
        Other = 0,
        Item = 1,
        Building = 2
    }

    public readonly struct TechnologyEffectPresentation
    {
        public TechnologyEffectPresentation(
            Sprite icon,
            string displayName,
            TechnologyEffectPresentationCategory category,
            int amount = 1)
        {
            Icon = icon;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            Category = category;
            Amount = Mathf.Max(1, amount);
        }

        public Sprite Icon { get; }
        public string DisplayName { get; }
        public TechnologyEffectPresentationCategory Category { get; }
        public int Amount { get; }
        public bool IsValid => Icon != null || !string.IsNullOrWhiteSpace(DisplayName);
    }

    public readonly struct TechnologyEffectApplyResult
    {
        public TechnologyEffectApplyResult(bool applied, string message)
        {
            Applied = applied;
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public bool Applied { get; }
        public string Message { get; }
        public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    }

    [Serializable]
    public abstract class TechnologyEffect
    {
        public virtual void Normalize()
        {
        }

        public abstract TechnologyEffectApplyResult Apply(GameSystem context, TechnologyDefinition technology);

        public virtual bool TryGetPresentation(out TechnologyEffectPresentation presentation)
        {
            presentation = default;
            return false;
        }
    }

    [Serializable]
    public sealed class TechnologyEffect_AddItem : TechnologyEffect
    {
        [SerializeField, LabelText("物品")]
        private ItemDefinition itemDefinition;

        [SerializeField, LabelText("数量"), Min(0)]
        private int amount = 1;

        public ItemDefinition ItemDefinition => itemDefinition;
        public int Amount => Mathf.Max(0, amount);

        public override void Normalize()
        {
            amount = Mathf.Max(0, amount);
        }

        public override TechnologyEffectApplyResult Apply(GameSystem context, TechnologyDefinition technology)
        {
            if (context == null || context.Services.Inventory == null || itemDefinition == null || Amount <= 0)
            {
                return new TechnologyEffectApplyResult(false, string.Empty);
            }

            var added = context.Services.Inventory.AddItem(itemDefinition, Amount);
            if (added <= 0)
            {
                return new TechnologyEffectApplyResult(false, $"{itemDefinition.DisplayName}+0");
            }

            return new TechnologyEffectApplyResult(true, $"{itemDefinition.DisplayName}+{added}");
        }

        public override bool TryGetPresentation(out TechnologyEffectPresentation presentation)
        {
            presentation = itemDefinition == null || Amount <= 0
                ? default
                : new TechnologyEffectPresentation(
                    itemDefinition.Icon,
                    itemDefinition.DisplayName,
                    TechnologyEffectPresentationCategory.Item,
                    Amount);
            return presentation.IsValid;
        }
    }

    [Serializable]
    public sealed class TechnologyEffect_UnlockBuildingBlueprint : TechnologyEffect
    {
        [SerializeField, LabelText("建筑蓝图")]
        private BuildingBase buildingPrefab;

        public BuildingBase BuildingPrefab => buildingPrefab;

        public override TechnologyEffectApplyResult Apply(GameSystem context, TechnologyDefinition technology)
        {
            if (context == null || buildingPrefab == null || !buildingPrefab.HasDefinition)
            {
                return new TechnologyEffectApplyResult(false, string.Empty);
            }

            var definition = buildingPrefab.Definition;
            var unlocked = context.Services.BuildingBlueprints.Unlock(definition.FamilyId);
            return new TechnologyEffectApplyResult(
                unlocked,
                unlocked ? $"蓝图解锁：{definition.DisplayName}" : $"蓝图已解锁：{definition.DisplayName}");
        }

        public override bool TryGetPresentation(out TechnologyEffectPresentation presentation)
        {
            var definition = buildingPrefab == null || !buildingPrefab.HasDefinition
                ? null
                : buildingPrefab.Definition;
            presentation = definition == null
                ? default
                : new TechnologyEffectPresentation(
                    definition.Icon,
                    definition.DisplayName,
                    TechnologyEffectPresentationCategory.Building);
            return presentation.IsValid;
        }
    }
}
