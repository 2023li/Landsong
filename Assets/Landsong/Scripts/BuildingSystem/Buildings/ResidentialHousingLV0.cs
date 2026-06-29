using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem.Buildings
{
    public sealed class ResidentialHousingLV0 : BuildingBase
    {
        private const int RequiredExp = 3;

        [TitleGroup("施工消耗")]
        [LabelText("第1回合消耗")]
        [SerializeField] private ItemAmount firstTurnCost = new ItemAmount(null, 10);

        [TitleGroup("施工消耗")]
        [LabelText("第2回合消耗")]
        [SerializeField] private ItemAmount secondTurnCost = new ItemAmount(null, 10);

        [TitleGroup("施工消耗")]
        [LabelText("第3回合消耗")]
        [SerializeField] private ItemAmount thirdTurnCost = new ItemAmount(null, 10);

        [TitleGroup("升级")]
        [LabelText("居民房 LV1 定义")]
        [Required]
        [SerializeField] private BuildingDefinition residentialHousingLv1Definition;

        [TitleGroup("运行时")]
        [LabelText("施工经验")]
        [ReadOnly]
        [SerializeField] private int exp;

        protected override void OnInitialized()
        {
            exp = Mathf.Clamp(exp, 0, RequiredExp);
        }

        protected override void OnPlaced()
        {
        }

        protected override void OnRegistered()
        {
        }

        protected override bool OnTurn()
        {
            if (exp >= RequiredExp)
            {
                return TryReplaceWithLv1();
            }

            var cost = GetCurrentTurnCost();
            if (!cost.IsValid)
            {
                Debug.LogWarning($"ResidentialHousingLV0 '{name}' has invalid construction cost at exp {exp}.", this);
                return false;
            }

            var inventory = GameSystem == null ? null : GameSystem.Inventory;
            if (inventory == null || !inventory.TryRemoveItems(new[] { cost }))
            {
                return false;
            }

            exp++;
            NotifyStateChanged();

            return exp < RequiredExp || TryReplaceWithLv1();
        }

        private ItemAmount GetCurrentTurnCost()
        {
            return exp switch
            {
                0 => firstTurnCost,
                1 => secondTurnCost,
                2 => thirdTurnCost,
                _ => default
            };
        }

        private bool TryReplaceWithLv1()
        {
            var buildingService = GameSystem == null ? null : GameSystem.Buildings;
            if (buildingService == null)
            {
                Debug.LogWarning($"ResidentialHousingLV0 '{name}' cannot upgrade because BuildingService is missing.", this);
                return false;
            }

            if (residentialHousingLv1Definition == null || !residentialHousingLv1Definition.HasBuildingPrefab)
            {
                Debug.LogWarning($"ResidentialHousingLV0 '{name}' cannot upgrade because LV1 definition or prefab is missing.", this);
                return false;
            }

            return buildingService.TryReplace(this, residentialHousingLv1Definition, out _);
        }
    }
}
