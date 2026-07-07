using System;
using System.Collections.Generic;
using Landsong.InventorySystem;
using UnityEngine;

namespace Landsong.TalentSystem
{
    [Serializable]
    public sealed class TalentHiddenTraitSaveData
    {
        public string TraitId = string.Empty;
        public TalentHiddenTraitState State = TalentHiddenTraitState.Undiscovered;

        public void Validate()
        {
            TraitId = TalentEffectDefinition.NormalizeId(TraitId);
            if ((int)State < (int)TalentHiddenTraitState.Undiscovered
                || (int)State > (int)TalentHiddenTraitState.Active)
            {
                State = TalentHiddenTraitState.Undiscovered;
            }
        }
    }

    [Serializable]
    public sealed class TalentStateSaveData
    {
        public string TalentInstanceId = string.Empty;
        public string TalentDefinitionId = string.Empty;
        public int Level = 1;
        public int Experience;
        public int AssignedTurns;
        public List<TalentHiddenTraitSaveData> HiddenTraits = new List<TalentHiddenTraitSaveData>();

        public void Validate()
        {
            TalentInstanceId = TalentEffectDefinition.NormalizeId(TalentInstanceId);
            TalentDefinitionId = TalentEffectDefinition.NormalizeId(TalentDefinitionId);
            Level = Mathf.Max(1, Level);
            Experience = Mathf.Max(0, Experience);
            AssignedTurns = Mathf.Max(0, AssignedTurns);
            HiddenTraits ??= new List<TalentHiddenTraitSaveData>();
            for (var i = HiddenTraits.Count - 1; i >= 0; i--)
            {
                var trait = HiddenTraits[i];
                if (trait == null)
                {
                    HiddenTraits.RemoveAt(i);
                    continue;
                }

                trait.Validate();
                if (string.IsNullOrWhiteSpace(trait.TraitId))
                {
                    HiddenTraits.RemoveAt(i);
                }
            }
        }
    }

    [Serializable]
    public sealed class TalentOfferSaveData
    {
        public string OfferId = string.Empty;
        public string TalentDefinitionId = string.Empty;

        public void Validate()
        {
            OfferId = TalentEffectDefinition.NormalizeId(OfferId);
            TalentDefinitionId = TalentEffectDefinition.NormalizeId(TalentDefinitionId);
        }
    }

    [Serializable]
    public sealed class TalentSlotAssignmentSaveData
    {
        public string SlotId = string.Empty;
        public string TalentInstanceId = string.Empty;

        public void Validate()
        {
            SlotId = TalentEffectDefinition.NormalizeId(SlotId);
            TalentInstanceId = TalentEffectDefinition.NormalizeId(TalentInstanceId);
        }
    }

    [Serializable]
    public sealed class TalentSaveData
    {
        public List<TalentStateSaveData> OwnedTalents = new List<TalentStateSaveData>();
        public List<TalentOfferSaveData> CurrentOffers = new List<TalentOfferSaveData>();
        public List<TalentSlotAssignmentSaveData> SlotAssignments = new List<TalentSlotAssignmentSaveData>();
        public int LastRefreshTurn;
        public int LastSalaryRequired;
        public int LastSalaryPaid;
        public int LastSalaryMissing;

        public void Validate()
        {
            OwnedTalents ??= new List<TalentStateSaveData>();
            CurrentOffers ??= new List<TalentOfferSaveData>();
            SlotAssignments ??= new List<TalentSlotAssignmentSaveData>();

            var seenTalentInstances = new HashSet<string>(StringComparer.Ordinal);
            for (var i = OwnedTalents.Count - 1; i >= 0; i--)
            {
                var talent = OwnedTalents[i];
                if (talent == null)
                {
                    OwnedTalents.RemoveAt(i);
                    continue;
                }

                talent.Validate();
                if (string.IsNullOrWhiteSpace(talent.TalentInstanceId)
                    || string.IsNullOrWhiteSpace(talent.TalentDefinitionId)
                    || !seenTalentInstances.Add(talent.TalentInstanceId))
                {
                    OwnedTalents.RemoveAt(i);
                }
            }

            var seenOffers = new HashSet<string>(StringComparer.Ordinal);
            for (var i = CurrentOffers.Count - 1; i >= 0; i--)
            {
                var offer = CurrentOffers[i];
                if (offer == null)
                {
                    CurrentOffers.RemoveAt(i);
                    continue;
                }

                offer.Validate();
                if (string.IsNullOrWhiteSpace(offer.OfferId)
                    || string.IsNullOrWhiteSpace(offer.TalentDefinitionId)
                    || !seenOffers.Add(offer.OfferId))
                {
                    CurrentOffers.RemoveAt(i);
                }
            }

            var seenSlots = new HashSet<string>(StringComparer.Ordinal);
            for (var i = SlotAssignments.Count - 1; i >= 0; i--)
            {
                var assignment = SlotAssignments[i];
                if (assignment == null)
                {
                    SlotAssignments.RemoveAt(i);
                    continue;
                }

                assignment.Validate();
                if (string.IsNullOrWhiteSpace(assignment.SlotId)
                    || string.IsNullOrWhiteSpace(assignment.TalentInstanceId)
                    || !seenSlots.Add(assignment.SlotId))
                {
                    SlotAssignments.RemoveAt(i);
                }
            }

            LastRefreshTurn = Mathf.Max(0, LastRefreshTurn);
            LastSalaryRequired = Mathf.Max(0, LastSalaryRequired);
            LastSalaryPaid = Mathf.Clamp(LastSalaryPaid, 0, LastSalaryRequired);
            LastSalaryMissing = Mathf.Max(0, LastSalaryMissing);
        }
    }

    public sealed class TalentHiddenTraitRuntimeState
    {
        internal TalentHiddenTraitRuntimeState(TalentHiddenTraitDefinition definition, TalentHiddenTraitState state)
        {
            Definition = definition;
            TraitId = definition == null ? string.Empty : definition.TraitId;
            State = state;
        }

        public TalentHiddenTraitDefinition Definition { get; }
        public string TraitId { get; }
        public TalentHiddenTraitState State { get; internal set; }
        public bool IsDiscovered => State >= TalentHiddenTraitState.Discovered;
        public bool IsActive => State == TalentHiddenTraitState.Active;

        public TalentHiddenTraitSaveData CaptureSaveData()
        {
            return new TalentHiddenTraitSaveData
            {
                TraitId = TraitId,
                State = State
            };
        }
    }

    public sealed class TalentState
    {
        private readonly List<TalentHiddenTraitRuntimeState> hiddenTraits =
            new List<TalentHiddenTraitRuntimeState>();

        internal TalentState(string talentInstanceId, TalentDefinition definition)
        {
            TalentInstanceId = TalentEffectDefinition.NormalizeId(talentInstanceId);
            Definition = definition;
            TalentDefinitionId = definition == null ? string.Empty : definition.TalentId;
            Level = definition == null ? 1 : definition.StartingLevel;
            Experience = 0;
            AssignedTurns = 0;
            SynchronizeHiddenTraits(null);
        }

        internal TalentState(TalentStateSaveData saveData, TalentDefinition definition)
        {
            saveData.Validate();
            TalentInstanceId = saveData.TalentInstanceId;
            Definition = definition;
            TalentDefinitionId = saveData.TalentDefinitionId;
            Level = definition == null ? Mathf.Max(1, saveData.Level) : Mathf.Clamp(saveData.Level, 1, definition.MaxLevel);
            Experience = Mathf.Max(0, saveData.Experience);
            AssignedTurns = Mathf.Max(0, saveData.AssignedTurns);
            SynchronizeHiddenTraits(saveData.HiddenTraits);
        }

        public string TalentInstanceId { get; }
        public TalentDefinition Definition { get; internal set; }
        public string TalentDefinitionId { get; }
        public int Level { get; internal set; }
        public int Experience { get; internal set; }
        public int AssignedTurns { get; internal set; }
        public IReadOnlyList<TalentHiddenTraitRuntimeState> HiddenTraits => hiddenTraits;
        public bool HasDefinition => Definition != null && Definition.IsValid;
        public string DisplayName => HasDefinition ? Definition.DisplayName : TalentDefinitionId;
        public TalentProfession Profession => Definition == null ? TalentProfession.None : Definition.Profession;
        public TalentRarity Rarity => Definition == null ? TalentRarity.Common : Definition.Rarity;
        public int SalaryGoldPerTurn => Definition == null ? 0 : Definition.CalculateSalaryGoldPerTurn(Level);
        public bool IsMaxLevel => Definition == null || Level >= Definition.MaxLevel;
        public int ExperienceRequiredForNextLevel => Definition == null ? 0 : Definition.GetExperienceRequiredForLevel(Level);
        public bool CanUpgrade => Definition != null && !IsMaxLevel && ExperienceRequiredForNextLevel > 0 && Experience >= ExperienceRequiredForNextLevel;

        public void AddExperience(int amount)
        {
            Experience = Mathf.Max(0, Experience + Mathf.Max(0, amount));
        }

        public bool Upgrade()
        {
            if (!CanUpgrade)
            {
                return false;
            }

            Experience = Mathf.Max(0, Experience - ExperienceRequiredForNextLevel);
            Level = Mathf.Min(Definition.MaxLevel, Level + 1);
            return true;
        }

        public void GetActiveEffects(List<TalentEffectDefinition> results)
        {
            if (results == null || Definition == null)
            {
                return;
            }

            var effects = Definition.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null)
                {
                    results.Add(effects[i]);
                }
            }

            for (var i = 0; i < hiddenTraits.Count; i++)
            {
                var trait = hiddenTraits[i];
                if (trait == null || !trait.IsActive || trait.Definition == null)
                {
                    continue;
                }

                var traitEffects = trait.Definition.Effects;
                for (var j = 0; j < traitEffects.Count; j++)
                {
                    if (traitEffects[j] != null)
                    {
                        results.Add(traitEffects[j]);
                    }
                }
            }
        }

        public TalentStateSaveData CaptureSaveData()
        {
            var saveData = new TalentStateSaveData
            {
                TalentInstanceId = TalentInstanceId,
                TalentDefinitionId = TalentDefinitionId,
                Level = Level,
                Experience = Experience,
                AssignedTurns = AssignedTurns,
                HiddenTraits = new List<TalentHiddenTraitSaveData>(hiddenTraits.Count)
            };

            for (var i = 0; i < hiddenTraits.Count; i++)
            {
                var trait = hiddenTraits[i];
                if (trait != null && !string.IsNullOrWhiteSpace(trait.TraitId))
                {
                    saveData.HiddenTraits.Add(trait.CaptureSaveData());
                }
            }

            saveData.Validate();
            return saveData;
        }

        internal void SynchronizeHiddenTraits(IReadOnlyList<TalentHiddenTraitSaveData> savedStates)
        {
            hiddenTraits.Clear();
            if (Definition == null)
            {
                return;
            }

            var savedById = new Dictionary<string, TalentHiddenTraitState>(StringComparer.Ordinal);
            if (savedStates != null)
            {
                for (var i = 0; i < savedStates.Count; i++)
                {
                    var saved = savedStates[i];
                    if (saved == null)
                    {
                        continue;
                    }

                    saved.Validate();
                    if (!string.IsNullOrWhiteSpace(saved.TraitId))
                    {
                        savedById[saved.TraitId] = saved.State;
                    }
                }
            }

            var definitions = Definition.HiddenTraits;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || !definition.IsValid)
                {
                    continue;
                }

                var state = savedById.TryGetValue(definition.TraitId, out var savedState)
                    ? savedState
                    : TalentHiddenTraitState.Undiscovered;
                hiddenTraits.Add(new TalentHiddenTraitRuntimeState(definition, state));
            }
        }
    }

    public sealed class TalentOfferState
    {
        internal TalentOfferState(string offerId, TalentDefinition definition)
        {
            OfferId = TalentEffectDefinition.NormalizeId(offerId);
            Definition = definition;
            TalentDefinitionId = definition == null ? string.Empty : definition.TalentId;
        }

        internal TalentOfferState(TalentOfferSaveData saveData, TalentDefinition definition)
        {
            saveData.Validate();
            OfferId = saveData.OfferId;
            Definition = definition;
            TalentDefinitionId = saveData.TalentDefinitionId;
        }

        public string OfferId { get; }
        public TalentDefinition Definition { get; internal set; }
        public string TalentDefinitionId { get; }
        public bool HasDefinition => Definition != null && Definition.IsValid;

        public TalentOfferSaveData CaptureSaveData()
        {
            return new TalentOfferSaveData
            {
                OfferId = OfferId,
                TalentDefinitionId = TalentDefinitionId
            };
        }
    }

    public sealed class TalentSlotRuntimeState
    {
        internal TalentSlotRuntimeState(TalentSlotDefinition definition)
        {
            Definition = definition;
        }

        public TalentSlotDefinition Definition { get; }
        public string SlotId => Definition == null ? string.Empty : Definition.SlotId;
        public string DisplayName => Definition == null ? SlotId : Definition.DisplayName;
        public TalentProfession AcceptedProfession => Definition == null ? TalentProfession.None : Definition.AcceptedProfession;
        public string AssignedTalentInstanceId { get; internal set; } = string.Empty;
        public TalentState AssignedTalent { get; internal set; }
        public bool IsOccupied => AssignedTalent != null;

        public bool Accepts(TalentState talent)
        {
            return talent != null && Definition != null && Definition.Accepts(talent.Definition);
        }

        internal void Assign(TalentState talent)
        {
            AssignedTalent = talent;
            AssignedTalentInstanceId = talent == null ? string.Empty : talent.TalentInstanceId;
        }
    }

    public readonly struct TalentHiddenTraitTransition
    {
        public TalentHiddenTraitTransition(
            TalentState talent,
            TalentHiddenTraitRuntimeState trait,
            TalentHiddenTraitState previousState,
            TalentHiddenTraitState newState)
        {
            Talent = talent;
            Trait = trait;
            PreviousState = previousState;
            NewState = newState;
        }

        public TalentState Talent { get; }
        public TalentHiddenTraitRuntimeState Trait { get; }
        public TalentHiddenTraitState PreviousState { get; }
        public TalentHiddenTraitState NewState { get; }
    }

    public readonly struct TalentEffectApplicationResult
    {
        public TalentEffectApplicationResult(TalentState talent, TalentEffectDefinition effect, bool applied, int amount, string message)
        {
            Talent = talent;
            Effect = effect;
            Applied = applied;
            Amount = Mathf.Max(0, amount);
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public TalentState Talent { get; }
        public TalentEffectDefinition Effect { get; }
        public bool Applied { get; }
        public int Amount { get; }
        public string Message { get; }
        public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    }

    public sealed class TalentTurnSettlementResult
    {
        public TalentTurnSettlementResult(
            int turnNumber,
            int salaryRequired,
            int salaryPaid,
            int salaryMissing,
            IReadOnlyList<TalentEffectApplicationResult> effects,
            IReadOnlyList<TalentHiddenTraitTransition> traitTransitions)
        {
            TurnNumber = Mathf.Max(1, turnNumber);
            SalaryRequired = Mathf.Max(0, salaryRequired);
            SalaryPaid = Mathf.Clamp(salaryPaid, 0, SalaryRequired);
            SalaryMissing = Mathf.Max(0, salaryMissing);
            Effects = effects ?? Array.Empty<TalentEffectApplicationResult>();
            TraitTransitions = traitTransitions ?? Array.Empty<TalentHiddenTraitTransition>();
        }

        public int TurnNumber { get; }
        public int SalaryRequired { get; }
        public int SalaryPaid { get; }
        public int SalaryMissing { get; }
        public IReadOnlyList<TalentEffectApplicationResult> Effects { get; }
        public IReadOnlyList<TalentHiddenTraitTransition> TraitTransitions { get; }
        public bool HasSalary => SalaryRequired > 0;
        public bool HasMissingSalary => SalaryMissing > 0;
        public bool HasEffects => Effects.Count > 0;
        public bool HasTraitTransitions => TraitTransitions.Count > 0;
        public bool HasAnyChange => HasSalary || HasEffects || HasTraitTransitions;
    }

    public sealed class TalentRefreshResult
    {
        public TalentRefreshResult(bool succeeded, int costGold, IReadOnlyList<TalentOfferState> offers, string message)
        {
            Succeeded = succeeded;
            CostGold = Mathf.Max(0, costGold);
            Offers = offers ?? Array.Empty<TalentOfferState>();
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public bool Succeeded { get; }
        public int CostGold { get; }
        public IReadOnlyList<TalentOfferState> Offers { get; }
        public string Message { get; }
    }

    public sealed class TalentRecruitResult
    {
        public TalentRecruitResult(bool succeeded, TalentOfferState offer, TalentState talent, string message)
        {
            Succeeded = succeeded;
            Offer = offer;
            Talent = talent;
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public bool Succeeded { get; }
        public TalentOfferState Offer { get; }
        public TalentState Talent { get; }
        public string Message { get; }
    }

    public sealed class TalentAssignResult
    {
        public TalentAssignResult(bool succeeded, TalentState talent, TalentSlotRuntimeState slot, string message)
        {
            Succeeded = succeeded;
            Talent = talent;
            Slot = slot;
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public bool Succeeded { get; }
        public TalentState Talent { get; }
        public TalentSlotRuntimeState Slot { get; }
        public string Message { get; }
    }

    public sealed class TalentUpgradeResult
    {
        public TalentUpgradeResult(
            bool succeeded,
            TalentState talent,
            int previousLevel,
            IReadOnlyList<TalentHiddenTraitTransition> traitTransitions,
            string message)
        {
            Succeeded = succeeded;
            Talent = talent;
            PreviousLevel = Mathf.Max(0, previousLevel);
            TraitTransitions = traitTransitions ?? Array.Empty<TalentHiddenTraitTransition>();
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public bool Succeeded { get; }
        public TalentState Talent { get; }
        public int PreviousLevel { get; }
        public IReadOnlyList<TalentHiddenTraitTransition> TraitTransitions { get; }
        public string Message { get; }
    }

    public sealed class TalentService
    {
        private static readonly IReadOnlyList<TalentState> EmptyTalents = Array.Empty<TalentState>();
        private static readonly IReadOnlyList<TalentOfferState> EmptyOffers = Array.Empty<TalentOfferState>();
        private static readonly IReadOnlyList<TalentSlotRuntimeState> EmptySlots = Array.Empty<TalentSlotRuntimeState>();

        private readonly GameSystem context;
        private readonly List<TalentState> ownedTalents = new List<TalentState>();
        private readonly List<TalentOfferState> currentOffers = new List<TalentOfferState>();
        private readonly List<TalentSlotRuntimeState> slots = new List<TalentSlotRuntimeState>();
        private readonly List<TalentDefinition> availableDefinitions = new List<TalentDefinition>();
        private readonly HashSet<string> excludedUniqueTalentIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<TalentEffectDefinition> effectScratch = new List<TalentEffectDefinition>();

        private TalentCatalog catalog;
        private ItemDefinition salaryGoldItemDefinition;
        private int refreshGoldCost;
        private int refreshCardCount;
        private int lastRefreshTurn;
        private int lastSalaryRequired;
        private int lastSalaryPaid;
        private int lastSalaryMissing;

        public TalentService(
            GameSystem context,
            TalentCatalog catalog,
            ItemDefinition salaryGoldItemDefinition,
            IEnumerable<TalentSlotDefinition> slotDefinitions,
            int refreshGoldCost,
            int refreshCardCount,
            TalentSaveData saveData = null)
        {
            this.context = context;
            this.catalog = catalog;
            this.salaryGoldItemDefinition = salaryGoldItemDefinition;
            this.refreshGoldCost = Mathf.Max(0, refreshGoldCost);
            this.refreshCardCount = Mathf.Max(1, refreshCardCount);
            ConfigureSlots(slotDefinitions);
            RestoreSaveData(saveData);
        }

        public event Action<TalentService> StateChanged;

        public TalentCatalog Catalog => catalog;
        public ItemDefinition SalaryGoldItemDefinition => salaryGoldItemDefinition;
        public int RefreshGoldCost => refreshGoldCost;
        public int RefreshCardCount => refreshCardCount;
        public int LastRefreshTurn => lastRefreshTurn;
        public int LastSalaryRequired => lastSalaryRequired;
        public int LastSalaryPaid => lastSalaryPaid;
        public int LastSalaryMissing => lastSalaryMissing;
        public IReadOnlyList<TalentState> OwnedTalents => ownedTalents.Count == 0 ? EmptyTalents : ownedTalents;
        public IReadOnlyList<TalentOfferState> CurrentOffers => currentOffers.Count == 0 ? EmptyOffers : currentOffers;
        public IReadOnlyList<TalentSlotRuntimeState> SlotStates => slots.Count == 0 ? EmptySlots : slots;

        public void SetCatalog(TalentCatalog newCatalog)
        {
            catalog = newCatalog;
            ResolveDefinitions();
            NotifyChanged();
        }

        public void SetSalaryGoldItemDefinition(ItemDefinition itemDefinition)
        {
            salaryGoldItemDefinition = itemDefinition;
            NotifyChanged();
        }

        public void ConfigureRefresh(int costGold, int cardCount)
        {
            refreshGoldCost = Mathf.Max(0, costGold);
            refreshCardCount = Mathf.Max(1, cardCount);
            NotifyChanged();
        }

        public int CalculateTotalSalaryGoldPerTurn()
        {
            var total = 0;
            for (var i = 0; i < ownedTalents.Count; i++)
            {
                var talent = ownedTalents[i];
                if (talent != null)
                {
                    total += talent.SalaryGoldPerTurn;
                }
            }

            return Mathf.Max(0, total);
        }

        public bool CanPayRefreshCost()
        {
            if (refreshGoldCost <= 0)
            {
                return true;
            }

            var goldItemId = GetGoldItemId();
            return context != null
                   && context.Inventory != null
                   && !string.IsNullOrWhiteSpace(goldItemId)
                   && context.Inventory.HasItem(goldItemId, refreshGoldCost);
        }

        public bool TryRefreshOffers(out TalentRefreshResult result)
        {
            if (catalog == null)
            {
                result = new TalentRefreshResult(false, refreshGoldCost, currentOffers, "人才目录未配置。");
                return false;
            }

            if (!TryPayRefreshCost(out var refreshCostMessage))
            {
                result = new TalentRefreshResult(false, refreshGoldCost, currentOffers, refreshCostMessage);
                return false;
            }

            currentOffers.Clear();
            BuildExcludedUniqueTalentIds();
            catalog.GetAvailableDefinitions(context, excludedUniqueTalentIds, availableDefinitions);
            var count = Mathf.Min(Mathf.Max(1, refreshCardCount), availableDefinitions.Count);
            for (var i = 0; i < count; i++)
            {
                var selected = SelectWeightedTalent(availableDefinitions);
                if (selected == null)
                {
                    break;
                }

                availableDefinitions.Remove(selected);
                currentOffers.Add(new TalentOfferState(Guid.NewGuid().ToString("N"), selected));
            }

            lastRefreshTurn = context == null ? 0 : context.CurrentTurn;
            result = new TalentRefreshResult(
                true,
                refreshGoldCost,
                currentOffers.ToArray(),
                currentOffers.Count <= 0 ? "没有符合条件的人才。" : $"已刷新 {currentOffers.Count} 张人才卡。");
            NotifyChanged();
            return true;
        }

        public bool TryRecruitOffer(string offerId, out TalentRecruitResult result)
        {
            var offer = FindOffer(offerId);
            if (offer == null || !offer.HasDefinition)
            {
                result = new TalentRecruitResult(false, offer, null, "人才卡不存在。");
                return false;
            }

            if (offer.Definition.UniqueTalent && HasOwnedTalentDefinition(offer.Definition.TalentId))
            {
                result = new TalentRecruitResult(false, offer, null, "该唯一人才已经被招募。");
                return false;
            }

            var talent = new TalentState(Guid.NewGuid().ToString("N"), offer.Definition);
            ownedTalents.Add(talent);
            currentOffers.Remove(offer);

            var transitions = new List<TalentHiddenTraitTransition>();
            UpdateHiddenTraitStates(talent, transitions);

            result = new TalentRecruitResult(true, offer, talent, $"已招募：{talent.DisplayName}。");
            NotifyChanged();
            return true;
        }

        public bool TryAssignTalentToSlot(string talentInstanceId, string slotId, out TalentAssignResult result)
        {
            var talent = FindOwnedTalent(talentInstanceId);
            if (talent == null || !talent.HasDefinition)
            {
                result = new TalentAssignResult(false, talent, null, "人才不存在。");
                return false;
            }

            var slot = FindSlot(slotId);
            if (slot == null)
            {
                result = new TalentAssignResult(false, talent, slot, "人才槽不存在。");
                return false;
            }

            if (!slot.Accepts(talent))
            {
                result = new TalentAssignResult(false, talent, slot, "职业不符合该人才槽要求。");
                return false;
            }

            UnassignTalentInternal(talent);
            slot.Assign(talent);
            result = new TalentAssignResult(true, talent, slot, $"已任命 {talent.DisplayName} 至 {slot.DisplayName}。");
            NotifyChanged();
            return true;
        }

        public bool TryUnassignSlot(string slotId, out TalentAssignResult result)
        {
            var slot = FindSlot(slotId);
            if (slot == null)
            {
                result = new TalentAssignResult(false, null, null, "人才槽不存在。");
                return false;
            }

            var talent = slot.AssignedTalent;
            if (talent == null)
            {
                result = new TalentAssignResult(false, null, slot, "该槽位没有任命人才。");
                return false;
            }

            slot.Assign(null);
            result = new TalentAssignResult(true, talent, slot, $"已卸任：{talent.DisplayName}。");
            NotifyChanged();
            return true;
        }

        public bool TryUnassignTalent(string talentInstanceId, out TalentAssignResult result)
        {
            var talent = FindOwnedTalent(talentInstanceId);
            if (talent == null)
            {
                result = new TalentAssignResult(false, null, null, "人才不存在。");
                return false;
            }

            var slot = GetAssignedSlotForTalent(talentInstanceId);
            if (slot == null)
            {
                result = new TalentAssignResult(false, talent, null, "该人才尚未任命。");
                return false;
            }

            slot.Assign(null);
            result = new TalentAssignResult(true, talent, slot, $"已卸任：{talent.DisplayName}。");
            NotifyChanged();
            return true;
        }

        public bool TryUpgradeTalent(string talentInstanceId, out TalentUpgradeResult result)
        {
            var talent = FindOwnedTalent(talentInstanceId);
            if (talent == null)
            {
                result = new TalentUpgradeResult(false, null, 0, null, "人才不存在。");
                return false;
            }

            var previousLevel = talent.Level;
            if (!talent.Upgrade())
            {
                result = new TalentUpgradeResult(false, talent, previousLevel, null, "经验不足或已达到最高等级。");
                return false;
            }

            var transitions = new List<TalentHiddenTraitTransition>();
            UpdateHiddenTraitStates(talent, transitions);
            result = new TalentUpgradeResult(
                true,
                talent,
                previousLevel,
                transitions.ToArray(),
                $"{talent.DisplayName} 已提升至 {talent.Level} 级。");
            NotifyChanged();
            return true;
        }

        public bool AddTalentExperience(string talentInstanceId, int amount)
        {
            var talent = FindOwnedTalent(talentInstanceId);
            if (talent == null || amount <= 0)
            {
                return false;
            }

            talent.AddExperience(amount);
            var transitions = new List<TalentHiddenTraitTransition>();
            UpdateHiddenTraitStates(talent, transitions);
            NotifyChanged();
            return true;
        }

        public TalentTurnSettlementResult SettleTurn(int turnNumber)
        {
            turnNumber = Mathf.Max(1, turnNumber);
            PaySalaries(out lastSalaryRequired, out lastSalaryPaid, out lastSalaryMissing);

            var transitions = new List<TalentHiddenTraitTransition>();
            for (var i = 0; i < ownedTalents.Count; i++)
            {
                UpdateHiddenTraitStates(ownedTalents[i], transitions);
            }

            var effectResults = new List<TalentEffectApplicationResult>();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var talent = slot == null ? null : slot.AssignedTalent;
                if (talent == null)
                {
                    continue;
                }

                talent.AssignedTurns = Mathf.Max(0, talent.AssignedTurns + 1);
                UpdateHiddenTraitStates(talent, transitions);
                ApplyTurnEndEffects(talent, turnNumber, effectResults);
            }

            var result = new TalentTurnSettlementResult(
                turnNumber,
                lastSalaryRequired,
                lastSalaryPaid,
                lastSalaryMissing,
                effectResults.ToArray(),
                transitions.ToArray());

            if (result.HasAnyChange)
            {
                NotifyChanged();
            }

            return result;
        }

        public TalentSaveData CaptureSaveData()
        {
            var saveData = new TalentSaveData
            {
                OwnedTalents = new List<TalentStateSaveData>(ownedTalents.Count),
                CurrentOffers = new List<TalentOfferSaveData>(currentOffers.Count),
                SlotAssignments = new List<TalentSlotAssignmentSaveData>(),
                LastRefreshTurn = lastRefreshTurn,
                LastSalaryRequired = lastSalaryRequired,
                LastSalaryPaid = lastSalaryPaid,
                LastSalaryMissing = lastSalaryMissing
            };

            for (var i = 0; i < ownedTalents.Count; i++)
            {
                var talent = ownedTalents[i];
                if (talent != null)
                {
                    saveData.OwnedTalents.Add(talent.CaptureSaveData());
                }
            }

            for (var i = 0; i < currentOffers.Count; i++)
            {
                var offer = currentOffers[i];
                if (offer != null)
                {
                    saveData.CurrentOffers.Add(offer.CaptureSaveData());
                }
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || string.IsNullOrWhiteSpace(slot.SlotId) || slot.AssignedTalent == null)
                {
                    continue;
                }

                saveData.SlotAssignments.Add(
                    new TalentSlotAssignmentSaveData
                    {
                        SlotId = slot.SlotId,
                        TalentInstanceId = slot.AssignedTalent.TalentInstanceId
                    });
            }

            saveData.Validate();
            return saveData;
        }

        public void RestoreSaveData(TalentSaveData saveData)
        {
            ownedTalents.Clear();
            currentOffers.Clear();
            ClearSlotAssignments();

            saveData?.Validate();
            if (saveData == null)
            {
                lastRefreshTurn = 0;
                lastSalaryRequired = 0;
                lastSalaryPaid = 0;
                lastSalaryMissing = 0;
                NotifyChanged();
                return;
            }

            lastRefreshTurn = saveData.LastRefreshTurn;
            lastSalaryRequired = saveData.LastSalaryRequired;
            lastSalaryPaid = saveData.LastSalaryPaid;
            lastSalaryMissing = saveData.LastSalaryMissing;

            for (var i = 0; i < saveData.OwnedTalents.Count; i++)
            {
                var savedTalent = saveData.OwnedTalents[i];
                if (savedTalent == null)
                {
                    continue;
                }

                var definition = ResolveTalentDefinition(savedTalent.TalentDefinitionId);
                ownedTalents.Add(new TalentState(savedTalent, definition));
            }

            for (var i = 0; i < saveData.CurrentOffers.Count; i++)
            {
                var savedOffer = saveData.CurrentOffers[i];
                if (savedOffer == null)
                {
                    continue;
                }

                var definition = ResolveTalentDefinition(savedOffer.TalentDefinitionId);
                if (definition != null)
                {
                    currentOffers.Add(new TalentOfferState(savedOffer, definition));
                }
            }

            for (var i = 0; i < saveData.SlotAssignments.Count; i++)
            {
                var assignment = saveData.SlotAssignments[i];
                if (assignment == null)
                {
                    continue;
                }

                var slot = FindSlot(assignment.SlotId);
                var talent = FindOwnedTalent(assignment.TalentInstanceId);
                if (slot != null && talent != null && slot.Accepts(talent))
                {
                    UnassignTalentInternal(talent);
                    slot.Assign(talent);
                }
            }

            NotifyChanged();
        }

        public TalentState FindOwnedTalent(string talentInstanceId)
        {
            talentInstanceId = TalentEffectDefinition.NormalizeId(talentInstanceId);
            if (string.IsNullOrWhiteSpace(talentInstanceId))
            {
                return null;
            }

            for (var i = 0; i < ownedTalents.Count; i++)
            {
                var talent = ownedTalents[i];
                if (talent != null
                    && string.Equals(talent.TalentInstanceId, talentInstanceId, StringComparison.Ordinal))
                {
                    return talent;
                }
            }

            return null;
        }

        public TalentOfferState FindOffer(string offerId)
        {
            offerId = TalentEffectDefinition.NormalizeId(offerId);
            if (string.IsNullOrWhiteSpace(offerId))
            {
                return null;
            }

            for (var i = 0; i < currentOffers.Count; i++)
            {
                var offer = currentOffers[i];
                if (offer != null && string.Equals(offer.OfferId, offerId, StringComparison.Ordinal))
                {
                    return offer;
                }
            }

            return null;
        }

        public TalentSlotRuntimeState FindSlot(string slotId)
        {
            slotId = TalentEffectDefinition.NormalizeId(slotId);
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return null;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot != null && string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))
                {
                    return slot;
                }
            }

            return null;
        }

        public TalentSlotRuntimeState GetAssignedSlotForTalent(string talentInstanceId)
        {
            talentInstanceId = TalentEffectDefinition.NormalizeId(talentInstanceId);
            if (string.IsNullOrWhiteSpace(talentInstanceId))
            {
                return null;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot != null
                    && slot.AssignedTalent != null
                    && string.Equals(slot.AssignedTalent.TalentInstanceId, talentInstanceId, StringComparison.Ordinal))
                {
                    return slot;
                }
            }

            return null;
        }

        private void ConfigureSlots(IEnumerable<TalentSlotDefinition> slotDefinitions)
        {
            slots.Clear();
            var configuredSlots = new List<TalentSlotDefinition>();
            if (slotDefinitions != null)
            {
                foreach (var definition in slotDefinitions)
                {
                    if (definition == null)
                    {
                        continue;
                    }

                    definition.Normalize();
                    if (definition.IsValid)
                    {
                        configuredSlots.Add(definition);
                    }
                }
            }

            if (configuredSlots.Count == 0)
            {
                for (var i = 1; i <= 3; i++)
                {
                    configuredSlots.Add(TalentSlotDefinition.CreateDefaultGeneral(i));
                }
            }

            configuredSlots.Sort(CompareSlotDefinitions);
            var seenSlots = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < configuredSlots.Count; i++)
            {
                var definition = configuredSlots[i];
                if (definition == null || !definition.IsValid || !seenSlots.Add(definition.SlotId))
                {
                    continue;
                }

                slots.Add(new TalentSlotRuntimeState(definition));
            }
        }

        private static int CompareSlotDefinitions(TalentSlotDefinition left, TalentSlotDefinition right)
        {
            var leftOrder = left == null ? int.MaxValue : left.SortOrder;
            var rightOrder = right == null ? int.MaxValue : right.SortOrder;
            if (leftOrder != rightOrder)
            {
                return leftOrder.CompareTo(rightOrder);
            }

            return string.Compare(
                left == null ? string.Empty : left.SlotId,
                right == null ? string.Empty : right.SlotId,
                StringComparison.Ordinal);
        }

        private bool TryPayRefreshCost(out string message)
        {
            if (refreshGoldCost <= 0)
            {
                message = string.Empty;
                return true;
            }

            var goldItemId = GetGoldItemId();
            if (context == null || context.Inventory == null)
            {
                message = "库存服务未初始化。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(goldItemId))
            {
                message = "人才金币物品未配置。";
                return false;
            }

            if (!context.Inventory.HasItem(goldItemId, refreshGoldCost))
            {
                message = $"金币不足：刷新需要 {refreshGoldCost}。";
                return false;
            }

            if (!context.Inventory.TryRemoveItem(goldItemId, refreshGoldCost))
            {
                message = "扣除刷新费用失败。";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void PaySalaries(out int salaryRequired, out int salaryPaid, out int salaryMissing)
        {
            salaryRequired = CalculateTotalSalaryGoldPerTurn();
            salaryPaid = 0;
            salaryMissing = 0;
            if (salaryRequired <= 0)
            {
                return;
            }

            var goldItemId = GetGoldItemId();
            if (context == null || context.Inventory == null || string.IsNullOrWhiteSpace(goldItemId))
            {
                salaryMissing = salaryRequired;
                return;
            }

            var available = context.Inventory.GetQuantity(goldItemId);
            salaryPaid = Mathf.Min(available, salaryRequired);
            if (salaryPaid > 0)
            {
                context.Inventory.RemoveItem(goldItemId, salaryPaid);
            }

            salaryMissing = Mathf.Max(0, salaryRequired - salaryPaid);
        }

        private void ApplyTurnEndEffects(
            TalentState talent,
            int turnNumber,
            List<TalentEffectApplicationResult> results)
        {
            if (talent == null || results == null)
            {
                return;
            }

            effectScratch.Clear();
            talent.GetActiveEffects(effectScratch);
            for (var i = 0; i < effectScratch.Count; i++)
            {
                var effect = effectScratch[i];
                if (effect == null || effect.TriggerTiming != TalentEffectTriggerTiming.OnTurnEnd)
                {
                    continue;
                }

                var result = ApplyEffect(talent, effect, turnNumber);
                if (result.Applied || result.HasMessage)
                {
                    results.Add(result);
                }
            }

            effectScratch.Clear();
        }

        private TalentEffectApplicationResult ApplyEffect(TalentState talent, TalentEffectDefinition effect, int turnNumber)
        {
            if (context == null || talent == null || effect == null)
            {
                return new TalentEffectApplicationResult(talent, effect, false, 0, string.Empty);
            }

            var amount = Mathf.Max(0, effect.CalculateAmount(context, talent));
            switch (effect.EffectType)
            {
                case TalentEffectType.AddItem:
                    return ApplyAddItemEffect(talent, effect, amount);
                case TalentEffectType.AddResearchPoints:
                    return ApplyResearchPointEffect(talent, effect, amount, turnNumber);
                case TalentEffectType.UnlockBuildingBlueprint:
                    return ApplyUnlockBlueprintEffect(talent, effect);
                default:
                    return new TalentEffectApplicationResult(talent, effect, false, 0, string.Empty);
            }
        }

        private TalentEffectApplicationResult ApplyAddItemEffect(
            TalentState talent,
            TalentEffectDefinition effect,
            int amount)
        {
            if (context.Inventory == null || effect.ItemDefinition == null || amount <= 0)
            {
                return new TalentEffectApplicationResult(talent, effect, false, amount, string.Empty);
            }

            var added = context.Inventory.AddItem(effect.ItemDefinition, amount);
            var message = added > 0
                ? $"{talent.DisplayName}：{effect.ItemDefinition.DisplayName}+{added}"
                : $"{talent.DisplayName}：{effect.ItemDefinition.DisplayName}+0（仓库已满）";
            return new TalentEffectApplicationResult(talent, effect, added > 0, added, message);
        }

        private TalentEffectApplicationResult ApplyResearchPointEffect(
            TalentState talent,
            TalentEffectDefinition effect,
            int amount,
            int turnNumber)
        {
            if (amount <= 0)
            {
                return new TalentEffectApplicationResult(talent, effect, false, amount, string.Empty);
            }

            context.ApplyTalentResearchPoints(amount, turnNumber, talent.DisplayName);
            return new TalentEffectApplicationResult(talent, effect, true, amount, $"{talent.DisplayName}：研究点+{amount}");
        }

        private TalentEffectApplicationResult ApplyUnlockBlueprintEffect(TalentState talent, TalentEffectDefinition effect)
        {
            var building = effect.BuildingPrefab;
            if (building == null || !building.HasDefinition)
            {
                return new TalentEffectApplicationResult(talent, effect, false, 0, string.Empty);
            }

            var unlocked = context.UnlockBuildingBlueprint(building.Definition.BuildingId);
            var message = unlocked
                ? $"{talent.DisplayName}：解锁蓝图 {building.Definition.DisplayName}"
                : $"{talent.DisplayName}：蓝图已解锁 {building.Definition.DisplayName}";
            return new TalentEffectApplicationResult(talent, effect, unlocked, unlocked ? 1 : 0, message);
        }

        private void UpdateHiddenTraitStates(TalentState talent, List<TalentHiddenTraitTransition> transitions)
        {
            if (talent == null)
            {
                return;
            }

            var traits = talent.HiddenTraits;
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                var definition = trait == null ? null : trait.Definition;
                if (trait == null || definition == null)
                {
                    continue;
                }

                if (trait.State == TalentHiddenTraitState.Undiscovered && definition.CanDiscover(context, talent))
                {
                    var previous = trait.State;
                    trait.State = TalentHiddenTraitState.Discovered;
                    transitions?.Add(new TalentHiddenTraitTransition(talent, trait, previous, trait.State));
                }

                if (trait.State == TalentHiddenTraitState.Discovered && definition.CanActivate(context, talent))
                {
                    var previous = trait.State;
                    trait.State = TalentHiddenTraitState.Active;
                    transitions?.Add(new TalentHiddenTraitTransition(talent, trait, previous, trait.State));
                }
            }
        }

        private void BuildExcludedUniqueTalentIds()
        {
            excludedUniqueTalentIds.Clear();
            for (var i = 0; i < ownedTalents.Count; i++)
            {
                var definition = ownedTalents[i]?.Definition;
                if (definition != null && definition.UniqueTalent)
                {
                    excludedUniqueTalentIds.Add(definition.TalentId);
                }
            }

            for (var i = 0; i < currentOffers.Count; i++)
            {
                var definition = currentOffers[i]?.Definition;
                if (definition != null && definition.UniqueTalent)
                {
                    excludedUniqueTalentIds.Add(definition.TalentId);
                }
            }
        }

        private TalentDefinition SelectWeightedTalent(IReadOnlyList<TalentDefinition> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            var totalWeight = 0f;
            for (var i = 0; i < candidates.Count; i++)
            {
                totalWeight += Mathf.Max(0f, candidates[i] == null ? 0f : candidates[i].RefreshWeight);
            }

            if (totalWeight <= 0f)
            {
                return candidates[0];
            }

            var roll = UnityEngine.Random.value * totalWeight;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var weight = Mathf.Max(0f, candidate == null ? 0f : candidate.RefreshWeight);
                if (weight <= 0f)
                {
                    continue;
                }

                if (roll <= weight)
                {
                    return candidate;
                }

                roll -= weight;
            }

            return candidates[candidates.Count - 1];
        }

        private bool HasOwnedTalentDefinition(string talentDefinitionId)
        {
            talentDefinitionId = TalentEffectDefinition.NormalizeId(talentDefinitionId);
            if (string.IsNullOrWhiteSpace(talentDefinitionId))
            {
                return false;
            }

            for (var i = 0; i < ownedTalents.Count; i++)
            {
                var talent = ownedTalents[i];
                if (talent != null
                    && string.Equals(talent.TalentDefinitionId, talentDefinitionId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void UnassignTalentInternal(TalentState talent)
        {
            if (talent == null)
            {
                return;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot != null
                    && slot.AssignedTalent != null
                    && string.Equals(slot.AssignedTalent.TalentInstanceId, talent.TalentInstanceId, StringComparison.Ordinal))
                {
                    slot.Assign(null);
                }
            }
        }

        private void ClearSlotAssignments()
        {
            for (var i = 0; i < slots.Count; i++)
            {
                slots[i]?.Assign(null);
            }
        }

        private void ResolveDefinitions()
        {
            for (var i = 0; i < ownedTalents.Count; i++)
            {
                var talent = ownedTalents[i];
                if (talent == null)
                {
                    continue;
                }

                talent.Definition = ResolveTalentDefinition(talent.TalentDefinitionId);
                talent.SynchronizeHiddenTraits(talent.CaptureSaveData().HiddenTraits);
            }

            for (var i = 0; i < currentOffers.Count; i++)
            {
                var offer = currentOffers[i];
                if (offer != null)
                {
                    offer.Definition = ResolveTalentDefinition(offer.TalentDefinitionId);
                }
            }
        }

        private TalentDefinition ResolveTalentDefinition(string talentDefinitionId)
        {
            return catalog != null && catalog.TryGetDefinition(talentDefinitionId, out var definition)
                ? definition
                : null;
        }

        private string GetGoldItemId()
        {
            return salaryGoldItemDefinition == null ? string.Empty : salaryGoldItemDefinition.ItemId;
        }

        private void NotifyChanged()
        {
            StateChanged?.Invoke(this);
        }
    }
}
