using System;
using System.Collections.Generic;
using Landsong.InventorySystem;
using UnityEngine;

namespace Landsong.ExpeditionSystem
{
    [Serializable]
    public sealed class ExpeditionItemStack
    {
        public string ItemId = string.Empty;
        public int Amount;

        public ExpeditionItemStack()
        {
        }

        public ExpeditionItemStack(string itemId, int amount)
        {
            ItemId = ExpeditionDestinationDefinition.NormalizeId(itemId);
            Amount = Mathf.Max(0, amount);
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(ItemId) && Amount > 0;

        public void Validate()
        {
            ItemId = ExpeditionDestinationDefinition.NormalizeId(ItemId);
            Amount = Mathf.Max(0, Amount);
        }
    }

    [Serializable]
    public sealed class ExpeditionStateSaveData
    {
        public string ExpeditionId = string.Empty;
        public string DestinationId = string.Empty;
        public ExpeditionStatus Status = ExpeditionStatus.Active;
        public int StartedTurn = 1;
        public int ReturnTurn = 1;
        public int AssignedPopulation;
        public float SuccessChance;
        public bool RewardsClaimed;
        public int FailureSubsidyRequired;
        public int FailureSubsidyPaid;
        public int FailureSubsidyMissing;
        public int PenaltyStacksApplied;
        public float RewardYieldMultiplier = 1f;
        public float SupplyRewardYieldBonus;
        public List<ExpeditionItemStack> AssignedSupplies = new List<ExpeditionItemStack>();
        public List<ExpeditionItemStack> ItemRewards = new List<ExpeditionItemStack>();

        public void Validate()
        {
            ExpeditionId = ExpeditionDestinationDefinition.NormalizeId(ExpeditionId);
            DestinationId = ExpeditionDestinationDefinition.NormalizeId(DestinationId);
            StartedTurn = Mathf.Max(1, StartedTurn);
            ReturnTurn = Mathf.Max(StartedTurn, ReturnTurn);
            AssignedPopulation = Mathf.Max(0, AssignedPopulation);
            SuccessChance = Mathf.Clamp01(SuccessChance);
            FailureSubsidyRequired = Mathf.Max(0, FailureSubsidyRequired);
            FailureSubsidyPaid = Mathf.Clamp(FailureSubsidyPaid, 0, FailureSubsidyRequired);
            FailureSubsidyMissing = Mathf.Max(0, FailureSubsidyMissing);
            PenaltyStacksApplied = Mathf.Max(0, PenaltyStacksApplied);
            RewardYieldMultiplier = Mathf.Max(0f, RewardYieldMultiplier);
            SupplyRewardYieldBonus = Mathf.Max(0f, SupplyRewardYieldBonus);
            AssignedSupplies ??= new List<ExpeditionItemStack>();
            for (var i = AssignedSupplies.Count - 1; i >= 0; i--)
            {
                var stack = AssignedSupplies[i];
                if (stack == null)
                {
                    AssignedSupplies.RemoveAt(i);
                    continue;
                }

                stack.Validate();
                if (!stack.IsValid)
                {
                    AssignedSupplies.RemoveAt(i);
                }
            }

            ItemRewards ??= new List<ExpeditionItemStack>();
            for (var i = ItemRewards.Count - 1; i >= 0; i--)
            {
                var stack = ItemRewards[i];
                if (stack == null)
                {
                    ItemRewards.RemoveAt(i);
                    continue;
                }

                stack.Validate();
                if (!stack.IsValid)
                {
                    ItemRewards.RemoveAt(i);
                }
            }
        }
    }

    [Serializable]
    public sealed class ExpeditionPenaltySaveData
    {
        public int Stacks;
        public int ActiveUntilTurn;

        public void Validate()
        {
            Stacks = Mathf.Max(0, Stacks);
            ActiveUntilTurn = Mathf.Max(0, ActiveUntilTurn);
            if (Stacks <= 0)
            {
                ActiveUntilTurn = 0;
            }
        }
    }

    [Serializable]
    public sealed class ExpeditionSaveData
    {
        public List<ExpeditionStateSaveData> Expeditions = new List<ExpeditionStateSaveData>();
        public ExpeditionPenaltySaveData SubsidyPenalty = new ExpeditionPenaltySaveData();

        public void Validate()
        {
            Expeditions ??= new List<ExpeditionStateSaveData>();
            for (var i = Expeditions.Count - 1; i >= 0; i--)
            {
                var expedition = Expeditions[i];
                if (expedition == null)
                {
                    Expeditions.RemoveAt(i);
                    continue;
                }

                expedition.Validate();
                if (string.IsNullOrWhiteSpace(expedition.ExpeditionId)
                    || string.IsNullOrWhiteSpace(expedition.DestinationId))
                {
                    Expeditions.RemoveAt(i);
                }
            }

            SubsidyPenalty ??= new ExpeditionPenaltySaveData();
            SubsidyPenalty.Validate();
        }
    }

    public sealed class ExpeditionState
    {
        private readonly List<ExpeditionItemStack> assignedSupplies = new List<ExpeditionItemStack>();
        private readonly List<ExpeditionItemStack> itemRewards = new List<ExpeditionItemStack>();

        internal ExpeditionState(
            string expeditionId,
            ExpeditionDestinationDefinition definition,
            int startedTurn,
            int returnTurn,
            int assignedPopulation,
            float successChance,
            float supplyRewardYieldBonus,
            IEnumerable<ExpeditionItemStack> supplies)
        {
            ExpeditionId = expeditionId;
            Definition = definition;
            DestinationId = definition == null ? string.Empty : definition.DestinationId;
            StartedTurn = Mathf.Max(1, startedTurn);
            ReturnTurn = Mathf.Max(StartedTurn, returnTurn);
            AssignedPopulation = Mathf.Max(0, assignedPopulation);
            SuccessChance = Mathf.Clamp01(successChance);
            Status = ExpeditionStatus.Active;
            RewardYieldMultiplier = 1f;
            SupplyRewardYieldBonus = Mathf.Max(0f, supplyRewardYieldBonus);
            AddSupplies(supplies);
        }

        internal ExpeditionState(ExpeditionStateSaveData saveData, ExpeditionDestinationDefinition definition)
        {
            saveData.Validate();
            ExpeditionId = saveData.ExpeditionId;
            Definition = definition;
            DestinationId = saveData.DestinationId;
            StartedTurn = saveData.StartedTurn;
            ReturnTurn = saveData.ReturnTurn;
            AssignedPopulation = saveData.AssignedPopulation;
            SuccessChance = saveData.SuccessChance;
            Status = saveData.Status;
            RewardsClaimed = saveData.RewardsClaimed;
            FailureSubsidyRequired = saveData.FailureSubsidyRequired;
            FailureSubsidyPaid = saveData.FailureSubsidyPaid;
            FailureSubsidyMissing = saveData.FailureSubsidyMissing;
            PenaltyStacksApplied = saveData.PenaltyStacksApplied;
            RewardYieldMultiplier = saveData.RewardYieldMultiplier <= 0f ? 1f : saveData.RewardYieldMultiplier;
            SupplyRewardYieldBonus = Mathf.Max(0f, saveData.SupplyRewardYieldBonus);
            AddSupplies(saveData.AssignedSupplies);
            AddItemRewards(saveData.ItemRewards);
        }

        public string ExpeditionId { get; }
        public ExpeditionDestinationDefinition Definition { get; internal set; }
        public string DestinationId { get; }
        public ExpeditionStatus Status { get; internal set; }
        public int StartedTurn { get; }
        public int ReturnTurn { get; }
        public int AssignedPopulation { get; }
        public float SuccessChance { get; }
        public bool RewardsClaimed { get; internal set; }
        public int FailureSubsidyRequired { get; internal set; }
        public int FailureSubsidyPaid { get; internal set; }
        public int FailureSubsidyMissing { get; internal set; }
        public int PenaltyStacksApplied { get; internal set; }
        public float RewardYieldMultiplier { get; internal set; } = 1f;
        public float SupplyRewardYieldBonus { get; }
        public IReadOnlyList<ExpeditionItemStack> AssignedSupplies => assignedSupplies;
        public IReadOnlyList<ExpeditionItemStack> ItemRewards => itemRewards;
        public bool IsActive => Status == ExpeditionStatus.Active;
        public bool IsSucceeded => Status == ExpeditionStatus.Succeeded;
        public bool IsFailed => Status == ExpeditionStatus.Failed;
        public bool HasPendingRewards =>
            IsSucceeded
            && !RewardsClaimed
            && (itemRewards.Count > 0 || Definition != null && Definition.HasRewards);

        public int GetRemainingTurns(int currentTurn)
        {
            return Mathf.Max(0, ReturnTurn - Mathf.Max(1, currentTurn));
        }

        public ExpeditionStateSaveData CaptureSaveData()
        {
            return new ExpeditionStateSaveData
            {
                ExpeditionId = ExpeditionId,
                DestinationId = DestinationId,
                Status = Status,
                StartedTurn = StartedTurn,
                ReturnTurn = ReturnTurn,
                AssignedPopulation = AssignedPopulation,
                SuccessChance = SuccessChance,
                RewardsClaimed = RewardsClaimed,
                FailureSubsidyRequired = FailureSubsidyRequired,
                FailureSubsidyPaid = FailureSubsidyPaid,
                FailureSubsidyMissing = FailureSubsidyMissing,
                PenaltyStacksApplied = PenaltyStacksApplied,
                RewardYieldMultiplier = RewardYieldMultiplier,
                SupplyRewardYieldBonus = SupplyRewardYieldBonus,
                AssignedSupplies = CaptureSupplies(),
                ItemRewards = CaptureItemRewards()
            };
        }

        private void AddSupplies(IEnumerable<ExpeditionItemStack> supplies)
        {
            assignedSupplies.Clear();
            if (supplies == null)
            {
                return;
            }

            foreach (var supply in supplies)
            {
                if (supply == null)
                {
                    continue;
                }

                supply.Validate();
                if (!supply.IsValid)
                {
                    continue;
                }

                assignedSupplies.Add(new ExpeditionItemStack(supply.ItemId, supply.Amount));
            }
        }

        internal void SetItemRewards(IEnumerable<ExpeditionItemStack> rewards)
        {
            AddItemRewards(rewards);
        }

        private void AddItemRewards(IEnumerable<ExpeditionItemStack> rewards)
        {
            itemRewards.Clear();
            if (rewards == null)
            {
                return;
            }

            foreach (var reward in rewards)
            {
                if (reward == null)
                {
                    continue;
                }

                reward.Validate();
                if (reward.IsValid)
                {
                    itemRewards.Add(new ExpeditionItemStack(reward.ItemId, reward.Amount));
                }
            }
        }

        private List<ExpeditionItemStack> CaptureSupplies()
        {
            var result = new List<ExpeditionItemStack>(assignedSupplies.Count);
            for (var i = 0; i < assignedSupplies.Count; i++)
            {
                var supply = assignedSupplies[i];
                if (supply != null && supply.IsValid)
                {
                    result.Add(new ExpeditionItemStack(supply.ItemId, supply.Amount));
                }
            }

            return result;
        }

        private List<ExpeditionItemStack> CaptureItemRewards()
        {
            var result = new List<ExpeditionItemStack>(itemRewards.Count);
            for (var i = 0; i < itemRewards.Count; i++)
            {
                var reward = itemRewards[i];
                if (reward != null && reward.IsValid)
                {
                    result.Add(new ExpeditionItemStack(reward.ItemId, reward.Amount));
                }
            }

            return result;
        }
    }

    public readonly struct ExpeditionDestinationAvailability
    {
        public ExpeditionDestinationAvailability(
            ExpeditionDestinationDefinition destination,
            bool isVisible,
            bool isAvailable,
            ExpeditionDestinationUnavailableReason reason)
        {
            Destination = destination;
            IsVisible = isVisible;
            IsAvailable = isAvailable;
            Reason = reason;
        }

        public ExpeditionDestinationDefinition Destination { get; }
        public bool IsVisible { get; }
        public bool IsAvailable { get; }
        public ExpeditionDestinationUnavailableReason Reason { get; }
    }

    public readonly struct ExpeditionStartResult
    {
        public ExpeditionStartResult(
            bool succeeded,
            ExpeditionStartFailureReason failureReason,
            ExpeditionState expedition,
            float successChance,
            string message)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            Expedition = expedition;
            SuccessChance = Mathf.Clamp01(successChance);
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public bool Succeeded { get; }
        public ExpeditionStartFailureReason FailureReason { get; }
        public ExpeditionState Expedition { get; }
        public float SuccessChance { get; }
        public string Message { get; }
    }

    public readonly struct ExpeditionClaimResult
    {
        public ExpeditionClaimResult(
            bool succeeded,
            ExpeditionClaimFailureReason failureReason,
            ExpeditionState expedition,
            string message)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            Expedition = expedition;
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public bool Succeeded { get; }
        public ExpeditionClaimFailureReason FailureReason { get; }
        public ExpeditionState Expedition { get; }
        public string Message { get; }
    }

    public readonly struct ExpeditionSettlementResult
    {
        public ExpeditionSettlementResult(
            ExpeditionState expedition,
            bool succeeded,
            bool rewardsClaimed,
            bool rewardsPending,
            int subsidyRequired,
            int subsidyPaid,
            int subsidyMissing,
            int penaltyStacksApplied)
        {
            Expedition = expedition;
            Succeeded = succeeded;
            RewardsClaimed = rewardsClaimed;
            RewardsPending = rewardsPending;
            SubsidyRequired = Mathf.Max(0, subsidyRequired);
            SubsidyPaid = Mathf.Max(0, subsidyPaid);
            SubsidyMissing = Mathf.Max(0, subsidyMissing);
            PenaltyStacksApplied = Mathf.Max(0, penaltyStacksApplied);
        }

        public ExpeditionState Expedition { get; }
        public bool Succeeded { get; }
        public bool RewardsClaimed { get; }
        public bool RewardsPending { get; }
        public int SubsidyRequired { get; }
        public int SubsidyPaid { get; }
        public int SubsidyMissing { get; }
        public int PenaltyStacksApplied { get; }
    }

    public sealed class ExpeditionService
    {
        private static readonly IReadOnlyList<ExpeditionState> EmptyExpeditions = Array.Empty<ExpeditionState>();
        private static readonly IReadOnlyList<ExpeditionDestinationAvailability> EmptyDestinationAvailabilities =
            Array.Empty<ExpeditionDestinationAvailability>();

        private readonly GameSystem context;
        private readonly List<ExpeditionState> expeditions = new List<ExpeditionState>();
        private readonly List<ExpeditionDestinationAvailability> destinationAvailabilities =
            new List<ExpeditionDestinationAvailability>();

        private ExpeditionDestinationCatalog catalog;
        private ItemDefinition subsidyGoldItemDefinition;
        private int maxActiveExpeditions;
        private int subsidyPenaltyStacks;
        private int subsidyPenaltyActiveUntilTurn;

        public ExpeditionService(
            GameSystem context,
            ExpeditionDestinationCatalog catalog,
            ItemDefinition subsidyGoldItemDefinition,
            int maxActiveExpeditions,
            ExpeditionSaveData saveData = null)
        {
            this.context = context;
            this.catalog = catalog;
            this.subsidyGoldItemDefinition = subsidyGoldItemDefinition;
            this.maxActiveExpeditions = Mathf.Max(1, maxActiveExpeditions);
            RestoreSaveData(saveData);
        }

        public event Action<ExpeditionService> StateChanged;
        public event Action<ExpeditionService, ExpeditionStartResult> ExpeditionStarted;
        public event Action<ExpeditionService, ExpeditionClaimResult> RewardsClaimed;

        public IReadOnlyList<ExpeditionState> Expeditions => expeditions.Count == 0 ? EmptyExpeditions : expeditions;
        public ExpeditionDestinationCatalog Catalog => catalog;
        public ItemDefinition SubsidyGoldItemDefinition => subsidyGoldItemDefinition;
        public bool IsAvailable => context == null
                                   || context.Services.Features != null
                                   && context.Services.Features.IsUnlocked(GameFeature.Expedition);
        public int ActiveAssignedPopulation => IsAvailable ? CalculateActiveAssignedPopulation() : 0;
        public int ActiveExpeditionCount => IsAvailable ? CalculateActiveExpeditionCount() : 0;
        public int MaxActiveExpeditions => Mathf.Max(1, maxActiveExpeditions);
        public bool HasAvailableExpeditionSlot => IsAvailable && ActiveExpeditionCount < MaxActiveExpeditions;
        public int SubsidyPenaltyStacks => IsAvailable ? subsidyPenaltyStacks : 0;
        public int SubsidyPenaltyActiveUntilTurn => subsidyPenaltyActiveUntilTurn;
        public bool IsSubsidyPenaltyActive => IsAvailable
                                              && IsSubsidyPenaltyActiveAt(context == null ? 1 : context.Services.Turn.CurrentTurn);
        public float RewardYieldBonus => !IsAvailable || context == null ? 0f : context.CalculateExpeditionRewardYieldBonus();
        public float RewardYieldMultiplier => 1f + RewardYieldBonus;

        public void SetCatalog(ExpeditionDestinationCatalog newCatalog)
        {
            catalog = newCatalog;
            ResolveDefinitions();
            NotifyChanged();
        }

        public void SetSubsidyGoldItemDefinition(ItemDefinition itemDefinition)
        {
            subsidyGoldItemDefinition = itemDefinition;
        }

        public void SetMaxActiveExpeditions(int value)
        {
            maxActiveExpeditions = Mathf.Max(1, value);
            NotifyChanged();
        }

        public IReadOnlyList<ExpeditionDestinationAvailability> GetDestinationAvailabilities(bool includeUnavailable)
        {
            destinationAvailabilities.Clear();
            if (!IsAvailable || catalog == null)
            {
                return EmptyDestinationAvailabilities;
            }

            var destinations = catalog.Destinations;
            var currentTurn = context == null ? 1 : context.Services.Turn.CurrentTurn;
            for (var i = 0; i < destinations.Count; i++)
            {
                var destination = destinations[i];
                var availability = EvaluateDestination(destination, currentTurn);
                if (!availability.IsVisible)
                {
                    continue;
                }

                if (!includeUnavailable && !availability.IsAvailable)
                {
                    continue;
                }

                destinationAvailabilities.Add(availability);
            }

            return destinationAvailabilities.Count == 0
                ? EmptyDestinationAvailabilities
                : destinationAvailabilities.ToArray();
        }

        public ExpeditionDestinationAvailability EvaluateDestination(ExpeditionDestinationDefinition destination, int currentTurn)
        {
            if (!IsAvailable)
            {
                return new ExpeditionDestinationAvailability(
                    destination,
                    false,
                    false,
                    ExpeditionDestinationUnavailableReason.FeatureLocked);
            }

            if (destination == null || !destination.IsValid)
            {
                return new ExpeditionDestinationAvailability(destination, false, false, ExpeditionDestinationUnavailableReason.Hidden);
            }

            var visibleCondition = destination.VisibleCondition;
            if (visibleCondition != null && !visibleCondition.IsMet(context))
            {
                return new ExpeditionDestinationAvailability(destination, false, false, ExpeditionDestinationUnavailableReason.Hidden);
            }

            if (!destination.Repeatable && HasCompletedDestination(destination.DestinationId))
            {
                return new ExpeditionDestinationAvailability(destination, true, false, ExpeditionDestinationUnavailableReason.AlreadyCompleted);
            }

            var availableCondition = destination.AvailableCondition;
            if (availableCondition != null && !availableCondition.IsMet(context))
            {
                return new ExpeditionDestinationAvailability(destination, true, false, ExpeditionDestinationUnavailableReason.ConditionLocked);
            }

            return new ExpeditionDestinationAvailability(destination, true, true, ExpeditionDestinationUnavailableReason.None);
        }

        public bool TryStartExpedition(
            ExpeditionDestinationDefinition destination,
            int population,
            IEnumerable<ItemAmount> assignedSupplies,
            out ExpeditionStartResult result)
        {
            result = default;
            population = Mathf.Max(0, population);

            if (!IsAvailable)
            {
                result = new ExpeditionStartResult(
                    false,
                    ExpeditionStartFailureReason.FeatureLocked,
                    null,
                    0f,
                    "远征系统尚未解锁。");
                return false;
            }

            if (destination == null || !destination.IsValid)
            {
                result = FailStart(ExpeditionStartFailureReason.InvalidDestination, destination, population, null, "远征目的地无效。");
                return false;
            }

            var availability = EvaluateDestination(destination, context == null ? 1 : context.Services.Turn.CurrentTurn);
            if (!availability.IsAvailable)
            {
                result = FailStart(ExpeditionStartFailureReason.DestinationUnavailable, destination, population, null, "远征目的地当前不可用。");
                return false;
            }

            if (!HasAvailableExpeditionSlot)
            {
                result = FailStart(ExpeditionStartFailureReason.ActiveExpeditionLimitReached, destination, population, null, "远征队伍已满。");
                return false;
            }

            if (population < destination.MinPopulation)
            {
                result = FailStart(ExpeditionStartFailureReason.PopulationTooLow, destination, population, null, "远征人口不足。");
                return false;
            }

            if (destination.HasMaxPopulation && population > destination.MaxPopulation)
            {
                result = FailStart(ExpeditionStartFailureReason.PopulationTooHigh, destination, population, null, "远征人口超过目的地上限。");
                return false;
            }

            if (context == null || context.Services.Dynasty == null || !context.Services.Dynasty.TryConsumePopulation(population))
            {
                result = FailStart(ExpeditionStartFailureReason.PopulationUnavailable, destination, population, null, "基础人口不足，无法出发。");
                return false;
            }

            var normalizedSupplies = NormalizeAssignedSupplies(assignedSupplies);
            if (!ValidateSupplies(destination, normalizedSupplies, out var supplyFailure, out var supplyMessage))
            {
                context.Services.Dynasty.AddPopulation(population);
                result = FailStart(supplyFailure, destination, population, normalizedSupplies, supplyMessage);
                return false;
            }

            if (context.Services.Inventory == null)
            {
                context.Services.Dynasty.AddPopulation(population);
                result = FailStart(ExpeditionStartFailureReason.InventoryMissing, destination, population, normalizedSupplies, "库存服务未初始化。");
                return false;
            }

            if (!HasSuppliesInInventory(context.Services.Inventory, normalizedSupplies))
            {
                context.Services.Dynasty.AddPopulation(population);
                result = FailStart(ExpeditionStartFailureReason.InventoryMissing, destination, population, normalizedSupplies, "库存物品不足。");
                return false;
            }

            if (!RemoveSuppliesFromInventory(context.Services.Inventory, normalizedSupplies))
            {
                context.Services.Dynasty.AddPopulation(population);
                result = FailStart(ExpeditionStartFailureReason.InventoryRemoveFailed, destination, population, normalizedSupplies, "扣除远征物资失败。");
                return false;
            }

            var currentTurn = context.Services.Turn.CurrentTurn;
            var supplyLookup = BuildSupplyLookup(normalizedSupplies);
            var successChance = destination.CalculateSuccessChance(population, supplyLookup);
            var supplyRewardYieldBonus = destination.CalculateSupplyRewardYieldBonus(supplyLookup);
            var expedition = new ExpeditionState(
                Guid.NewGuid().ToString("N"),
                destination,
                currentTurn,
                currentTurn + destination.DurationTurns,
                population,
                successChance,
                supplyRewardYieldBonus,
                normalizedSupplies);
            expeditions.Add(expedition);

            result = new ExpeditionStartResult(
                true,
                ExpeditionStartFailureReason.None,
                expedition,
                successChance,
                $"远征队已前往 {destination.DisplayName}。");
            NotifyChanged();
            ExpeditionStarted?.Invoke(this, result);
            return true;
        }

        public List<ExpeditionSettlementResult> SettleDueExpeditions(int currentTurn)
        {
            if (!IsAvailable)
            {
                return new List<ExpeditionSettlementResult>();
            }

            currentTurn = Mathf.Max(1, currentTurn);
            ClearExpiredPenalty(currentTurn);

            List<ExpeditionSettlementResult> results = null;
            for (var i = 0; i < expeditions.Count; i++)
            {
                var expedition = expeditions[i];
                if (expedition == null || !expedition.IsActive || expedition.ReturnTurn > currentTurn)
                {
                    continue;
                }

                results ??= new List<ExpeditionSettlementResult>();
                results.Add(SettleExpedition(expedition, currentTurn));
            }

            if (results != null)
            {
                NotifyChanged();
            }

            return results ?? new List<ExpeditionSettlementResult>();
        }

        public bool TryClaimRewards(string expeditionId, out ExpeditionClaimResult result)
        {
            result = default;
            if (!IsAvailable)
            {
                result = new ExpeditionClaimResult(
                    false,
                    ExpeditionClaimFailureReason.FeatureLocked,
                    null,
                    "远征系统尚未解锁。");
                return false;
            }

            var expedition = FindExpedition(expeditionId);
            if (expedition == null)
            {
                result = new ExpeditionClaimResult(false, ExpeditionClaimFailureReason.InvalidExpedition, null, "远征记录不存在。");
                return false;
            }

            if (!expedition.IsSucceeded)
            {
                result = new ExpeditionClaimResult(false, ExpeditionClaimFailureReason.NotSucceeded, expedition, "远征尚未成功归来。");
                return false;
            }

            if (expedition.RewardsClaimed || !HasClaimableRewards(expedition))
            {
                result = new ExpeditionClaimResult(false, ExpeditionClaimFailureReason.AlreadyClaimed, expedition, "远征奖励已领取。");
                return false;
            }

            if (context == null || context.Services.Inventory == null)
            {
                result = new ExpeditionClaimResult(false, ExpeditionClaimFailureReason.InventoryMissing, expedition, "库存服务未初始化。");
                return false;
            }

            if (!CanAddRewardsToInventory(expedition))
            {
                result = new ExpeditionClaimResult(false, ExpeditionClaimFailureReason.InventoryFull, expedition, "仓库空间不足，无法领取远征奖励。");
                return false;
            }

            ClaimRewards(expedition);
            result = new ExpeditionClaimResult(true, ExpeditionClaimFailureReason.None, expedition, "远征奖励已领取。");
            NotifyChanged();
            RewardsClaimed?.Invoke(this, result);
            return true;
        }

        public ExpeditionSaveData CaptureSaveData()
        {
            var saveData = new ExpeditionSaveData
            {
                Expeditions = new List<ExpeditionStateSaveData>(expeditions.Count),
                SubsidyPenalty = new ExpeditionPenaltySaveData
                {
                    Stacks = subsidyPenaltyStacks,
                    ActiveUntilTurn = subsidyPenaltyActiveUntilTurn
                }
            };

            for (var i = 0; i < expeditions.Count; i++)
            {
                var expedition = expeditions[i];
                if (expedition != null)
                {
                    saveData.Expeditions.Add(expedition.CaptureSaveData());
                }
            }

            saveData.Validate();
            return saveData;
        }

        public void RestoreSaveData(ExpeditionSaveData saveData)
        {
            expeditions.Clear();
            saveData?.Validate();
            if (saveData != null)
            {
                subsidyPenaltyStacks = saveData.SubsidyPenalty == null ? 0 : saveData.SubsidyPenalty.Stacks;
                subsidyPenaltyActiveUntilTurn = saveData.SubsidyPenalty == null ? 0 : saveData.SubsidyPenalty.ActiveUntilTurn;
                var savedExpeditions = saveData.Expeditions;
                for (var i = 0; i < savedExpeditions.Count; i++)
                {
                    var saved = savedExpeditions[i];
                    if (saved == null)
                    {
                        continue;
                    }

                    var destination = ResolveDestination(saved.DestinationId);
                    expeditions.Add(new ExpeditionState(saved, destination));
                }
            }
            else
            {
                subsidyPenaltyStacks = 0;
                subsidyPenaltyActiveUntilTurn = 0;
            }

            ClearExpiredPenalty(context == null ? 1 : context.Services.Turn.CurrentTurn);
            NotifyChanged();
        }

        private ExpeditionSettlementResult SettleExpedition(ExpeditionState expedition, int currentTurn)
        {
            var succeeded = UnityEngine.Random.value <= expedition.SuccessChance;
            if (succeeded)
            {
                expedition.Status = ExpeditionStatus.Succeeded;
                context?.Dynasty?.AddPopulation(expedition.AssignedPopulation);
                expedition.RewardYieldMultiplier = GetRewardYieldMultiplier(expedition);
                expedition.SetItemRewards(BuildYieldedItemRewards(expedition.Definition, expedition.RewardYieldMultiplier));

                var rewardsClaimed = false;
                var rewardsPending = false;
                if (!HasClaimableRewards(expedition))
                {
                    expedition.RewardsClaimed = true;
                    rewardsClaimed = true;
                }
                else if (context != null
                         && context.Services.Inventory != null
                         && CanAddRewardsToInventory(expedition))
                {
                    ClaimRewards(expedition);
                    rewardsClaimed = true;
                }
                else
                {
                    rewardsPending = true;
                }

                return new ExpeditionSettlementResult(expedition, true, rewardsClaimed, rewardsPending, 0, 0, 0, 0);
            }

            expedition.Status = ExpeditionStatus.Failed;
            var requiredSubsidy = expedition.Definition == null
                ? 0
                : expedition.Definition.CalculateFailureSubsidyGold(expedition.AssignedPopulation);
            expedition.FailureSubsidyRequired = requiredSubsidy;
            PayFailureSubsidy(expedition, requiredSubsidy, currentTurn);
            return new ExpeditionSettlementResult(
                expedition,
                false,
                false,
                false,
                expedition.FailureSubsidyRequired,
                expedition.FailureSubsidyPaid,
                expedition.FailureSubsidyMissing,
                expedition.PenaltyStacksApplied);
        }

        private void PayFailureSubsidy(ExpeditionState expedition, int requiredSubsidy, int currentTurn)
        {
            if (expedition == null || requiredSubsidy <= 0)
            {
                return;
            }

            var goldItemId = subsidyGoldItemDefinition == null ? string.Empty : subsidyGoldItemDefinition.ItemId;
            if (context == null || context.Services.Inventory == null || string.IsNullOrWhiteSpace(goldItemId))
            {
                ApplySubsidyPenalty(expedition, requiredSubsidy, currentTurn);
                return;
            }

            var available = context.Services.Inventory.GetQuantity(goldItemId);
            var paid = Mathf.Min(available, requiredSubsidy);
            if (paid > 0)
            {
                context.Services.Inventory.RemoveItem(goldItemId, paid);
            }

            expedition.FailureSubsidyPaid = paid;
            var missing = Mathf.Max(0, requiredSubsidy - paid);
            expedition.FailureSubsidyMissing = missing;
            if (missing > 0)
            {
                ApplySubsidyPenalty(expedition, missing, currentTurn);
            }
        }

        private void ApplySubsidyPenalty(ExpeditionState expedition, int missingGold, int currentTurn)
        {
            var stacks = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, missingGold) / 10f));
            expedition.PenaltyStacksApplied = stacks;
            subsidyPenaltyStacks += stacks;
            subsidyPenaltyActiveUntilTurn = Mathf.Max(subsidyPenaltyActiveUntilTurn, currentTurn);
        }

        public void ExtendSubsidyPenalty(int currentTurn, int durationTurns)
        {
            if (!IsAvailable)
            {
                return;
            }

            if (subsidyPenaltyStacks <= 0)
            {
                subsidyPenaltyActiveUntilTurn = 0;
                return;
            }

            currentTurn = Mathf.Max(1, currentTurn);
            durationTurns = Mathf.Max(1, durationTurns);
            subsidyPenaltyActiveUntilTurn = Mathf.Max(subsidyPenaltyActiveUntilTurn, currentTurn + durationTurns - 1);
        }

        private void ClaimRewards(ExpeditionState expedition)
        {
            if (expedition == null || expedition.RewardsClaimed)
            {
                return;
            }

            AddItemRewardsToInventory(expedition);
            if (expedition.Definition != null)
            {
                foreach (var buildingId in expedition.Definition.GetBlueprintRewardBuildingIds())
                {
                    context?.Services.BuildingBlueprints.Unlock(buildingId);
                }
            }

            expedition.RewardsClaimed = true;
        }

        private float GetRewardYieldMultiplier(ExpeditionState expedition = null)
        {
            var baseMultiplier = RewardYieldMultiplier;
            var supplyBonus = expedition == null ? 0f : expedition.SupplyRewardYieldBonus;
            return Mathf.Max(0f, baseMultiplier + supplyBonus);
        }

        private List<ExpeditionItemStack> BuildYieldedItemRewards(
            ExpeditionDestinationDefinition destination,
            float rewardYieldMultiplier)
        {
            var results = new List<ExpeditionItemStack>();
            if (destination == null || destination.ItemRewards == null)
            {
                return results;
            }

            rewardYieldMultiplier = Mathf.Max(0f, rewardYieldMultiplier);
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            var rewards = destination.ItemRewards;
            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i].Normalized();
                if (!reward.IsValid)
                {
                    continue;
                }

                var amount = Mathf.RoundToInt(reward.Amount * rewardYieldMultiplier);
                if (rewardYieldMultiplier > 0f && amount <= 0)
                {
                    amount = 1;
                }

                if (amount <= 0)
                {
                    continue;
                }

                if (!totals.ContainsKey(reward.ItemId))
                {
                    totals.Add(reward.ItemId, 0);
                }

                totals[reward.ItemId] += amount;
            }

            foreach (var entry in totals)
            {
                results.Add(new ExpeditionItemStack(entry.Key, entry.Value));
            }

            return results;
        }

        private bool HasClaimableRewards(ExpeditionState expedition)
        {
            return expedition != null
                   && (HasItemRewards(expedition)
                       || expedition.Definition != null && expedition.Definition.HasRewards);
        }

        private bool HasItemRewards(ExpeditionState expedition)
        {
            if (expedition == null)
            {
                return false;
            }

            var rewards = GetItemRewardsForClaim(expedition);
            return rewards != null && rewards.Count > 0;
        }

        private bool CanAddRewardsToInventory(ExpeditionState expedition)
        {
            if (context == null || context.Services.Inventory == null)
            {
                return false;
            }

            var rewards = GetItemRewardsForClaim(expedition);
            if (rewards == null || rewards.Count == 0)
            {
                return true;
            }

            if (TryBuildItemAmounts(rewards, expedition.Definition, out var itemAmounts))
            {
                return context.Services.Inventory.CanAddItems(itemAmounts);
            }

            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward != null && reward.IsValid && !context.Services.Inventory.CanAddItem(reward.ItemId, reward.Amount))
                {
                    return false;
                }
            }

            return true;
        }

        private void AddItemRewardsToInventory(ExpeditionState expedition)
        {
            if (context == null || context.Services.Inventory == null)
            {
                return;
            }

            var rewards = GetItemRewardsForClaim(expedition);
            if (rewards == null || rewards.Count == 0)
            {
                return;
            }

            if (TryBuildItemAmounts(rewards, expedition.Definition, out var itemAmounts))
            {
                context.Services.Inventory.TryAddItems(itemAmounts);
                return;
            }

            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward != null && reward.IsValid)
                {
                    context.Services.Inventory.TryAddItem(reward.ItemId, reward.Amount);
                }
            }
        }

        private IReadOnlyList<ExpeditionItemStack> GetItemRewardsForClaim(ExpeditionState expedition)
        {
            if (expedition == null)
            {
                return Array.Empty<ExpeditionItemStack>();
            }

            if (expedition.ItemRewards.Count > 0)
            {
                return expedition.ItemRewards;
            }

            return BuildYieldedItemRewards(
                expedition.Definition,
                expedition.RewardYieldMultiplier <= 0f ? 1f : expedition.RewardYieldMultiplier);
        }

        private bool TryBuildItemAmounts(
            IReadOnlyList<ExpeditionItemStack> rewards,
            ExpeditionDestinationDefinition destination,
            out List<ItemAmount> itemAmounts)
        {
            itemAmounts = new List<ItemAmount>();
            if (rewards == null)
            {
                return true;
            }

            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null || !reward.IsValid)
                {
                    continue;
                }

                var definition = ResolveRewardItemDefinition(reward.ItemId, destination);
                if (definition == null)
                {
                    itemAmounts = null;
                    return false;
                }

                itemAmounts.Add(new ItemAmount(definition, reward.Amount));
            }

            return true;
        }

        private ItemDefinition ResolveRewardItemDefinition(
            string itemId,
            ExpeditionDestinationDefinition destination)
        {
            itemId = ExpeditionDestinationDefinition.NormalizeId(itemId);
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            if (destination != null)
            {
                var rewards = destination.ItemRewards;
                for (var i = 0; i < rewards.Count; i++)
                {
                    var reward = rewards[i].Normalized();
                    if (reward.ItemDefinition != null
                        && string.Equals(reward.ItemId, itemId, StringComparison.Ordinal))
                    {
                        return reward.ItemDefinition;
                    }
                }
            }

            var catalog = context == null || context.Services.Inventory == null
                ? null
                : context.Services.Inventory.ItemCatalog;
            return catalog != null && catalog.TryGetDefinition(itemId, out var definition)
                ? definition
                : null;
        }

        private List<ExpeditionItemStack> NormalizeAssignedSupplies(IEnumerable<ItemAmount> supplies)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (supplies != null)
            {
                foreach (var supply in supplies)
                {
                    var normalized = supply.Normalized();
                    if (!normalized.IsValid)
                    {
                        continue;
                    }

                    if (!totals.ContainsKey(normalized.ItemId))
                    {
                        totals.Add(normalized.ItemId, 0);
                    }

                    totals[normalized.ItemId] += normalized.Amount;
                }
            }

            var result = new List<ExpeditionItemStack>(totals.Count);
            foreach (var entry in totals)
            {
                result.Add(new ExpeditionItemStack(entry.Key, entry.Value));
            }

            return result;
        }

        private static Dictionary<string, int> BuildSupplyLookup(IEnumerable<ExpeditionItemStack> supplies)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (supplies == null)
            {
                return result;
            }

            foreach (var supply in supplies)
            {
                if (supply == null || !supply.IsValid)
                {
                    continue;
                }

                if (!result.ContainsKey(supply.ItemId))
                {
                    result.Add(supply.ItemId, 0);
                }

                result[supply.ItemId] += supply.Amount;
            }

            return result;
        }

        private bool ValidateSupplies(
            ExpeditionDestinationDefinition destination,
            IReadOnlyList<ExpeditionItemStack> supplies,
            out ExpeditionStartFailureReason failureReason,
            out string message)
        {
            var suppliedAmounts = BuildSupplyLookup(supplies);
            for (var i = 0; i < supplies.Count; i++)
            {
                var supply = supplies[i];
                if (supply == null || !supply.IsValid)
                {
                    continue;
                }

                if (!destination.TryGetSupplyOption(supply.ItemId, out var option))
                {
                    failureReason = ExpeditionStartFailureReason.InvalidSupply;
                    message = $"目的地不接受物资：{supply.ItemId}";
                    return false;
                }

                if (supply.Amount > option.MaxAssignedAmount)
                {
                    failureReason = ExpeditionStartFailureReason.SupplyLimitExceeded;
                    message = $"{option.DisplayName} 最多携带 {option.MaxAssignedAmount}。";
                    return false;
                }
            }

            var options = destination.SupplyOptions;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option == null || !option.IsValid || option.RequiredAmount <= 0)
                {
                    continue;
                }

                suppliedAmounts.TryGetValue(option.ItemId, out var supplied);
                if (supplied < option.RequiredAmount)
                {
                    failureReason = ExpeditionStartFailureReason.RequiredSupplyMissing;
                    message = $"{option.DisplayName} 至少需要 {option.RequiredAmount}。";
                    return false;
                }
            }

            failureReason = ExpeditionStartFailureReason.None;
            message = string.Empty;
            return true;
        }

        private static bool HasSuppliesInInventory(InventoryService inventory, IReadOnlyList<ExpeditionItemStack> supplies)
        {
            if (inventory == null)
            {
                return false;
            }

            for (var i = 0; i < supplies.Count; i++)
            {
                var supply = supplies[i];
                if (supply != null && supply.IsValid && !inventory.HasItem(supply.ItemId, supply.Amount))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool RemoveSuppliesFromInventory(InventoryService inventory, IReadOnlyList<ExpeditionItemStack> supplies)
        {
            if (inventory == null)
            {
                return false;
            }

            for (var i = 0; i < supplies.Count; i++)
            {
                var supply = supplies[i];
                if (supply == null || !supply.IsValid)
                {
                    continue;
                }

                if (!inventory.TryRemoveItem(supply.ItemId, supply.Amount))
                {
                    return false;
                }
            }

            return true;
        }

        private ExpeditionStartResult FailStart(
            ExpeditionStartFailureReason reason,
            ExpeditionDestinationDefinition destination,
            int population,
            IReadOnlyList<ExpeditionItemStack> supplies,
            string message)
        {
            var successChance = destination == null
                ? 0f
                : destination.CalculateSuccessChance(population, BuildSupplyLookup(supplies));
            return new ExpeditionStartResult(false, reason, null, successChance, message);
        }

        private ExpeditionState FindExpedition(string expeditionId)
        {
            expeditionId = ExpeditionDestinationDefinition.NormalizeId(expeditionId);
            if (string.IsNullOrWhiteSpace(expeditionId))
            {
                return null;
            }

            for (var i = 0; i < expeditions.Count; i++)
            {
                var expedition = expeditions[i];
                if (expedition != null
                    && string.Equals(expedition.ExpeditionId, expeditionId, StringComparison.Ordinal))
                {
                    return expedition;
                }
            }

            return null;
        }

        private bool HasCompletedDestination(string destinationId)
        {
            destinationId = ExpeditionDestinationDefinition.NormalizeId(destinationId);
            if (string.IsNullOrWhiteSpace(destinationId))
            {
                return false;
            }

            for (var i = 0; i < expeditions.Count; i++)
            {
                var expedition = expeditions[i];
                if (expedition == null
                    || expedition.IsActive
                    || !string.Equals(expedition.DestinationId, destinationId, StringComparison.Ordinal))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private ExpeditionDestinationDefinition ResolveDestination(string destinationId)
        {
            return catalog != null && catalog.TryGetDestination(destinationId, out var destination)
                ? destination
                : null;
        }

        private void ResolveDefinitions()
        {
            for (var i = 0; i < expeditions.Count; i++)
            {
                var expedition = expeditions[i];
                if (expedition != null)
                {
                    expedition.Definition = ResolveDestination(expedition.DestinationId);
                }
            }
        }

        private int CalculateActiveAssignedPopulation()
        {
            var total = 0;
            for (var i = 0; i < expeditions.Count; i++)
            {
                var expedition = expeditions[i];
                if (expedition != null && expedition.IsActive)
                {
                    total += expedition.AssignedPopulation;
                }
            }

            return Mathf.Max(0, total);
        }

        private int CalculateActiveExpeditionCount()
        {
            var total = 0;
            for (var i = 0; i < expeditions.Count; i++)
            {
                var expedition = expeditions[i];
                if (expedition != null && expedition.IsActive)
                {
                    total++;
                }
            }

            return Mathf.Max(0, total);
        }

        private bool IsSubsidyPenaltyActiveAt(int currentTurn)
        {
            return subsidyPenaltyStacks > 0 && Mathf.Max(1, currentTurn) <= subsidyPenaltyActiveUntilTurn;
        }

        private void ClearExpiredPenalty(int currentTurn)
        {
            if (subsidyPenaltyStacks <= 0 || IsSubsidyPenaltyActiveAt(currentTurn))
            {
                return;
            }

            subsidyPenaltyStacks = 0;
            subsidyPenaltyActiveUntilTurn = 0;
        }

        private void NotifyChanged()
        {
            StateChanged?.Invoke(this);
        }
    }
}
