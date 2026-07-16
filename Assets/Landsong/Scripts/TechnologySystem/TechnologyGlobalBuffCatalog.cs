using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.ConditionSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.TechnologySystem
{
    [CreateAssetMenu(
        menuName = "Landsong/Technology/Global Buff Catalog",
        fileName = "TechnologyGlobalBuffCatalog")]
    public sealed class TechnologyGlobalBuffCatalog : ScriptableObject,
        ITechnologyUnlockContentProducer
    {
        [SerializeField, LabelText("全局 Buff")]
        private TechnologyGlobalBuffDefinition[] definitions =
            Array.Empty<TechnologyGlobalBuffDefinition>();

        public string TechnologyUnlockContentSourceId => "technology.global-buffs";
        public IReadOnlyList<TechnologyGlobalBuffDefinition> Definitions =>
            definitions ?? Array.Empty<TechnologyGlobalBuffDefinition>();

        public int GetBuildingResourceProductionFlatBonus(
            GameSystem context,
            BuildingBase building,
            string itemId)
        {
            var total = 0;
            for (var i = 0; i < Definitions.Count; i++)
            {
                total = checked(total + Mathf.Max(
                    0,
                    Definitions[i]?.GetBuildingResourceProductionFlatBonus(context, building, itemId) ?? 0));
            }

            return total;
        }

        public void InjectTechnologyUnlockContents(TechnologyUnlockContentRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            var bindings = new List<TechnologyUnlockContentBinding>();
            for (var i = 0; i < Definitions.Count; i++)
            {
                var definition = Definitions[i];
                if (definition == null
                    || !definition.IsValid
                    || definition.ActivationCondition
                        is not GameCondition_TechnologyUnlocked technologyCondition
                    || technologyCondition.TechnologyDefinition == null)
                {
                    continue;
                }

                bindings.Add(new TechnologyUnlockContentBinding(
                    technologyCondition.TechnologyDefinition.TechnologyId,
                    new TechnologyUnlockContent(
                        $"global-buff:{definition.BuffId}",
                        definition.Icon,
                        definition.Describe(),
                        TechnologyUnlockContentKind.GlobalBuff,
                        shortLabel: "BUFF")));
            }

            registry.ReplaceSource(TechnologyUnlockContentSourceId, bindings);
        }

        public void ConfigureDefinitions(IEnumerable<TechnologyGlobalBuffDefinition> source)
        {
            definitions = source == null
                ? Array.Empty<TechnologyGlobalBuffDefinition>()
                : new List<TechnologyGlobalBuffDefinition>(source).ToArray();
        }
    }

    public sealed class TechnologyGlobalBuffService
    {
        private readonly GameSystem context;

        public TechnologyGlobalBuffService(
            GameSystem context,
            TechnologyGlobalBuffCatalog catalog)
        {
            this.context = context;
            Catalog = catalog;
        }

        public TechnologyGlobalBuffCatalog Catalog { get; }

        public int GetBuildingResourceProductionFlatBonus(
            BuildingBase building,
            string itemId)
        {
            return Catalog?.GetBuildingResourceProductionFlatBonus(context, building, itemId) ?? 0;
        }
    }
}
