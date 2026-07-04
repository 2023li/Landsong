using System;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.TechnologySystem
{
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
            if (context == null || context.Inventory == null || itemDefinition == null || Amount <= 0)
            {
                return new TechnologyEffectApplyResult(false, string.Empty);
            }

            var added = context.Inventory.AddItem(itemDefinition, Amount);
            if (added <= 0)
            {
                return new TechnologyEffectApplyResult(false, $"{itemDefinition.DisplayName}+0");
            }

            return new TechnologyEffectApplyResult(true, $"{itemDefinition.DisplayName}+{added}");
        }
    }
}
