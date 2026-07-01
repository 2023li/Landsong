using UnityEngine;

namespace Landsong.BuildingSystem
{
    public class Building_Tree : BuildingBase
    {
        private const string WoodItemId = "原木";
        private const string SaplingItemId = "树苗";

        [SerializeField, Min(1)] private int minHealth = 3;
        [SerializeField, Min(1)] private int maxHealth = 6;
        [SerializeField, Min(1)] private int damagePerDoubleClick = 1;
        [SerializeField, Min(0)] private int woodRewardAmount = 2;
        [SerializeField, Min(0)] private int saplingRewardAmount = 3;
        [SerializeField, Min(0)] private int currentHealth;

        protected override void OnInitialized()
        {
            Debug.Log("注册");

            if (currentHealth <= 0)
            {
                currentHealth = Random.Range(minHealth, maxHealth + 1);
            }
        }

        protected override void OnPlaced()
        {
        }

        protected override void OnRegistered()
        {
        }

        protected override bool OnTurn()
        {
            return true;
        }
        protected override void OnClicked()
        {
            Debug.Log("点击");
        }
        protected override void OnDoubleClicked()
        {
            Damage(damagePerDoubleClick);
        }

        public void Damage(int amount)
        {


            if (amount <= 0 || currentHealth <= 0)
            {
                return;
            }

            currentHealth = Mathf.Max(0, currentHealth - amount);
            NotifyStateChanged();

            if (currentHealth <= 0)
            {
                Demolish();
            }
        }

        protected override void OnDemolished()
        {
            AddHarvestRewards();
        }

        private void AddHarvestRewards()
        {
            var inventory = GameSystem == null ? null : GameSystem.Inventory;
            if (inventory == null)
            {
                Debug.LogWarning($"Tree '{name}' cannot add harvest rewards because InventoryService is missing.", this);
                return;
            }

            var addedWood = inventory.AddItem(WoodItemId, woodRewardAmount);
            var addedSaplings = inventory.AddItem(SaplingItemId, saplingRewardAmount);

            if (addedWood < woodRewardAmount || addedSaplings < saplingRewardAmount)
            {
                Debug.LogWarning(
                    $"Tree '{name}' could not add all harvest rewards. Wood {addedWood}/{woodRewardAmount}, saplings {addedSaplings}/{saplingRewardAmount}.",
                    this);
            }
        }

        private void OnValidate()
        {
            minHealth = Mathf.Max(1, minHealth);
            maxHealth = Mathf.Max(minHealth, maxHealth);
            damagePerDoubleClick = Mathf.Max(1, damagePerDoubleClick);
            woodRewardAmount = Mathf.Max(0, woodRewardAmount);
            saplingRewardAmount = Mathf.Max(0, saplingRewardAmount);
            currentHealth = Mathf.Max(0, currentHealth);
        }


    }
}
