using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // Transactions include the completion facts written by their commit callback.
    // Definition records are copied into typed operations before any ECS buffer can move.
    public static class RewardDelivery
    {
        public static void Validate(EntityManager em, Entity root, ref DefinitionRewards rewards)
        {
            for (int i = 0; i < rewards.Items.Length; i++)
                if (!ItemDefinitions.IsValid(em, root, rewards.Items[i].Item) || rewards.Items[i].Quantity <= 0)
                    throw new InvalidOperationException("Invalid item reward.");
            for (int i = 0; i < rewards.Blueprints.Length; i++)
            {
                var reward = rewards.Blueprints[i];
                if (!BuildingDefinitions.IsValid(em, root, reward.Building) || reward.GrantedLevel <= 0 || reward.GrantedLevel > BuildingDefinitions.Get(em, root, reward.Building).MaximumLevel)
                    throw new InvalidOperationException("Invalid blueprint reward level.");
            }

            for (int i = 0; i < rewards.Buffs.Length; i++)
                if (!BuffDefinitions.IsValid(em, root, rewards.Buffs[i].Buff) || rewards.Buffs[i].GrantedLevel <= 0)
                    throw new InvalidOperationException("Invalid buff reward.");
            for (int i = 0; i < rewards.Features.Length; i++)
                if (!FeatureDefinitions.IsValid(em, root, rewards.Features[i].Feature) || rewards.Features[i].GrantedLevel != 1)
                    throw new InvalidOperationException("Feature rewards unlock one binary permission.");
        }

        public static bool Apply(EntityManager em, Entity root, ref DefinitionRewards rewards, FixedString128Bytes sourceName = default, float multiplier = 1, bool pending = false, Action commit = null, Action<int> probe = null)
        {
            if (!math.isfinite(multiplier) || multiplier < 0)
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            var items = new List<ItemAmount>();
            var permissions = new List<(int Order, Action Grant)>();
            for (int i = 0; i < rewards.Items.Length; i++)
            {
                var reward = rewards.Items[i];
                if (!ItemDefinitions.IsValid(em, root, reward.Item) || reward.Quantity <= 0)
                    throw new InvalidOperationException("Invalid item reward.");
                double quantity = math.floor(reward.Quantity * multiplier);
                if (quantity > int.MaxValue)
                    throw new OverflowException("Item reward exceeds supported quantity.");
                reward.Quantity = (int)quantity;
                if (reward.Quantity > 0)
                    items.Add(reward);
            }

            for (int i = 0; i < rewards.Blueprints.Length; i++)
            {
                var reward = rewards.Blueprints[i];
                ref var definition = ref BuildingDefinitions.Get(em, root, reward.Building);
                if (reward.GrantedLevel <= 0 || reward.GrantedLevel > definition.MaximumLevel)
                    throw new InvalidOperationException("Invalid blueprint reward level.");
                permissions.Add((reward.Order, () => BuildingBlueprints.Grant(em, root, reward.Building, reward.GrantedLevel)));
            }

            for (int i = 0; i < rewards.Buffs.Length; i++)
            {
                var reward = rewards.Buffs[i];
                if (!BuffDefinitions.IsValid(em, root, reward.Buff) || reward.GrantedLevel <= 0)
                    throw new InvalidOperationException("Invalid buff reward.");
                permissions.Add((reward.Order, () => PermanentBuffs.Grant(em, root, reward.Buff, reward.GrantedLevel)));
            }

            for (int i = 0; i < rewards.Features.Length; i++)
            {
                var reward = rewards.Features[i];
                if (!FeatureDefinitions.IsValid(em, root, reward.Feature) || reward.GrantedLevel != 1)
                    throw new InvalidOperationException("Feature rewards unlock one binary permission.");
                permissions.Add((reward.Order, () => FeatureUnlocks.Unlock(em, root, reward.Feature)));
            }

            items = items.OrderBy(item => item.Order).ToList();
            permissions = permissions.OrderBy(permission => permission.Order).ToList();
            using var transaction = new ProgressionRewardTransaction(em, root);
            int sequence = 0;
            // Storage uses the pre-reward modifiers. Permissions become active afterwards.
            foreach (var item in items)
            {
                int stored = InventoryOps.Add(em, root, item.Item, item.Quantity, pending);
                if (!pending && stored != item.Quantity)
                    return false;
                probe?.Invoke(++sequence);
            }

            foreach (var permission in permissions)
            {
                permission.Grant();
                probe?.Invoke(++sequence);
            }

            commit?.Invoke();
            probe?.Invoke(++sequence);
            transaction.Commit();
            return true;
        }

        public static bool Commit(EntityManager em, Entity root, Action commit, Action<int> probe = null)
        {
            using var transaction = new ProgressionRewardTransaction(em, root);
            commit?.Invoke();
            probe?.Invoke(1);
            transaction.Commit();
            return true;
        }
    }
}
