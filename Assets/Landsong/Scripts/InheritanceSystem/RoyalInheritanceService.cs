using System;
using System.Collections.Generic;
using Landsong.TalentSystem;
using UnityEngine;

namespace Landsong.InheritanceSystem
{
    [Serializable]
    public sealed class RoyalTraitSaveData
    {
        public string TraitId = string.Empty;
        public RoyalTraitState State = RoyalTraitState.Hidden;
        public bool IsAcquired;
        public int GeneratedTurn;
        public int RevealedTurn;
        public int ActivatedTurn;

        public void Validate()
        {
            TraitId = RoyalTraitDefinition.NormalizeId(TraitId);
            GeneratedTurn = Mathf.Max(0, GeneratedTurn);
            RevealedTurn = Mathf.Max(0, RevealedTurn);
            ActivatedTurn = Mathf.Max(0, ActivatedTurn);
            if ((int)State < (int)RoyalTraitState.Hidden || (int)State > (int)RoyalTraitState.Active)
            {
                State = RoyalTraitState.Hidden;
            }
        }
    }

    [Serializable]
    public sealed class RoyalCharacterSaveData
    {
        public string CharacterId = string.Empty;
        public string DisplayName = string.Empty;
        public RoyalCharacterRole Role = RoyalCharacterRole.Prince;
        public RoyalCharacterStatus Status = RoyalCharacterStatus.Heir;
        public int Age;
        public int BaseMaxLifespan = 80;
        public int CurrentReignTurns;
        public string FatherId = string.Empty;
        public string MotherId = string.Empty;
        public string ConsortId = string.Empty;
        public bool LifetimeWarningIssued;
        public List<string> ChildrenIds = new List<string>();
        public List<RoyalTraitSaveData> Traits = new List<RoyalTraitSaveData>();

        public void Validate()
        {
            CharacterId = RoyalTraitDefinition.NormalizeId(CharacterId);
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? string.Empty : DisplayName.Trim();
            Age = Mathf.Max(0, Age);
            BaseMaxLifespan = Mathf.Max(1, BaseMaxLifespan);
            CurrentReignTurns = Mathf.Max(0, CurrentReignTurns);
            FatherId = RoyalTraitDefinition.NormalizeId(FatherId);
            MotherId = RoyalTraitDefinition.NormalizeId(MotherId);
            ConsortId = RoyalTraitDefinition.NormalizeId(ConsortId);
            ChildrenIds ??= new List<string>();
            for (var i = ChildrenIds.Count - 1; i >= 0; i--)
            {
                var childId = RoyalTraitDefinition.NormalizeId(ChildrenIds[i]);
                if (string.IsNullOrWhiteSpace(childId))
                {
                    ChildrenIds.RemoveAt(i);
                    continue;
                }

                ChildrenIds[i] = childId;
            }

            Traits ??= new List<RoyalTraitSaveData>();
            var seenTraits = new HashSet<string>(StringComparer.Ordinal);
            for (var i = Traits.Count - 1; i >= 0; i--)
            {
                var trait = Traits[i];
                if (trait == null)
                {
                    Traits.RemoveAt(i);
                    continue;
                }

                trait.Validate();
                if (string.IsNullOrWhiteSpace(trait.TraitId) || !seenTraits.Add(trait.TraitId))
                {
                    Traits.RemoveAt(i);
                }
            }
        }
    }

    [Serializable]
    public sealed class RoyalInheritanceSaveData
    {
        public string CurrentKingId = string.Empty;
        public string CurrentQueenId = string.Empty;
        public int Generation = 1;
        public int LastSettlementTurn;
        public int LastSuccessionTurn;
        public List<RoyalCharacterSaveData> Characters = new List<RoyalCharacterSaveData>();

        public void Validate()
        {
            CurrentKingId = RoyalTraitDefinition.NormalizeId(CurrentKingId);
            CurrentQueenId = RoyalTraitDefinition.NormalizeId(CurrentQueenId);
            Generation = Mathf.Max(1, Generation);
            LastSettlementTurn = Mathf.Max(0, LastSettlementTurn);
            LastSuccessionTurn = Mathf.Max(0, LastSuccessionTurn);
            Characters ??= new List<RoyalCharacterSaveData>();
            var seenCharacters = new HashSet<string>(StringComparer.Ordinal);
            for (var i = Characters.Count - 1; i >= 0; i--)
            {
                var character = Characters[i];
                if (character == null)
                {
                    Characters.RemoveAt(i);
                    continue;
                }

                character.Validate();
                if (string.IsNullOrWhiteSpace(character.CharacterId) || !seenCharacters.Add(character.CharacterId))
                {
                    Characters.RemoveAt(i);
                }
            }
        }
    }

    public sealed class RoyalTraitRuntimeState
    {
        internal RoyalTraitRuntimeState(
            RoyalTraitDefinition definition,
            RoyalTraitState state,
            bool acquired,
            int generatedTurn,
            int revealedTurn,
            int activatedTurn)
        {
            Definition = definition;
            TraitId = definition == null ? string.Empty : definition.TraitId;
            State = state;
            IsAcquired = acquired;
            GeneratedTurn = Mathf.Max(0, generatedTurn);
            RevealedTurn = Mathf.Max(0, revealedTurn);
            ActivatedTurn = Mathf.Max(0, activatedTurn);
        }

        public RoyalTraitDefinition Definition { get; internal set; }
        public string TraitId { get; }
        public RoyalTraitState State { get; internal set; }
        public bool IsAcquired { get; }
        public int GeneratedTurn { get; }
        public int RevealedTurn { get; internal set; }
        public int ActivatedTurn { get; internal set; }
        public bool IsHidden => State == RoyalTraitState.Hidden;
        public bool IsDiscovered => State >= RoyalTraitState.Discovered;
        public bool IsActive => State == RoyalTraitState.Active;

        public RoyalTraitSaveData CaptureSaveData()
        {
            return new RoyalTraitSaveData
            {
                TraitId = TraitId,
                State = State,
                IsAcquired = IsAcquired,
                GeneratedTurn = GeneratedTurn,
                RevealedTurn = RevealedTurn,
                ActivatedTurn = ActivatedTurn
            };
        }
    }

    public sealed class RoyalCharacterState
    {
        private readonly List<string> childrenIds = new List<string>();
        private readonly List<RoyalTraitRuntimeState> traits = new List<RoyalTraitRuntimeState>();

        internal RoyalCharacterState(
            string characterId,
            string displayName,
            RoyalCharacterRole role,
            RoyalCharacterStatus status,
            int age,
            int baseMaxLifespan,
            string fatherId,
            string motherId,
            string consortId)
        {
            CharacterId = RoyalTraitDefinition.NormalizeId(characterId);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? CharacterId : displayName.Trim();
            Role = role;
            Status = status;
            Age = Mathf.Max(0, age);
            BaseMaxLifespan = Mathf.Max(1, baseMaxLifespan);
            EffectiveMaxLifespan = BaseMaxLifespan;
            FatherId = RoyalTraitDefinition.NormalizeId(fatherId);
            MotherId = RoyalTraitDefinition.NormalizeId(motherId);
            ConsortId = RoyalTraitDefinition.NormalizeId(consortId);
        }

        internal RoyalCharacterState(RoyalCharacterSaveData saveData, RoyalTraitCatalog catalog)
        {
            saveData.Validate();
            CharacterId = saveData.CharacterId;
            DisplayName = string.IsNullOrWhiteSpace(saveData.DisplayName) ? CharacterId : saveData.DisplayName;
            Role = saveData.Role;
            Status = saveData.Status;
            Age = saveData.Age;
            BaseMaxLifespan = saveData.BaseMaxLifespan;
            EffectiveMaxLifespan = BaseMaxLifespan;
            CurrentReignTurns = saveData.CurrentReignTurns;
            FatherId = saveData.FatherId;
            MotherId = saveData.MotherId;
            ConsortId = saveData.ConsortId;
            LifetimeWarningIssued = saveData.LifetimeWarningIssued;
            childrenIds.AddRange(saveData.ChildrenIds);
            for (var i = 0; i < saveData.Traits.Count; i++)
            {
                var savedTrait = saveData.Traits[i];
                var definition = catalog != null && catalog.TryGetTrait(savedTrait.TraitId, out var traitDefinition)
                    ? traitDefinition
                    : null;
                traits.Add(
                    new RoyalTraitRuntimeState(
                        definition,
                        savedTrait.State,
                        savedTrait.IsAcquired,
                        savedTrait.GeneratedTurn,
                        savedTrait.RevealedTurn,
                        savedTrait.ActivatedTurn));
            }
        }

        public string CharacterId { get; }
        public string DisplayName { get; internal set; }
        public RoyalCharacterRole Role { get; internal set; }
        public RoyalCharacterStatus Status { get; internal set; }
        public int Age { get; internal set; }
        public int BaseMaxLifespan { get; }
        public int EffectiveMaxLifespan { get; internal set; }
        public int CurrentReignTurns { get; internal set; }
        public string FatherId { get; }
        public string MotherId { get; }
        public string ConsortId { get; internal set; }
        public bool LifetimeWarningIssued { get; internal set; }
        public IReadOnlyList<string> ChildrenIds => childrenIds;
        public IReadOnlyList<RoyalTraitRuntimeState> Traits => traits;
        public bool IsAlive => Status != RoyalCharacterStatus.Dead;
        public bool IsReigning => Status == RoyalCharacterStatus.Reigning;
        public bool IsQueen => Role == RoyalCharacterRole.Queen && Status == RoyalCharacterStatus.Consort;
        public bool IsPotentialHeir => Role == RoyalCharacterRole.Prince && Status == RoyalCharacterStatus.Heir && IsAlive;
        public int RemainingLifespan => Mathf.Max(0, EffectiveMaxLifespan - Age);

        public bool IsLegalHeir(int legalAge)
        {
            return IsPotentialHeir && Age >= Mathf.Max(1, legalAge);
        }

        public bool HasTrait(string traitId)
        {
            traitId = RoyalTraitDefinition.NormalizeId(traitId);
            if (string.IsNullOrWhiteSpace(traitId))
            {
                return false;
            }

            for (var i = 0; i < traits.Count; i++)
            {
                if (traits[i] != null && traits[i].TraitId == traitId)
                {
                    return true;
                }
            }

            return false;
        }

        public void AddChild(string childId)
        {
            childId = RoyalTraitDefinition.NormalizeId(childId);
            if (string.IsNullOrWhiteSpace(childId) || childrenIds.Contains(childId))
            {
                return;
            }

            childrenIds.Add(childId);
        }

        public bool AddTrait(RoyalTraitDefinition definition, RoyalTraitState state, bool acquired, int turnNumber)
        {
            if (definition == null || !definition.IsValid || HasTrait(definition.TraitId))
            {
                return false;
            }

            traits.Add(
                new RoyalTraitRuntimeState(
                    definition,
                    state,
                    acquired,
                    Mathf.Max(0, turnNumber),
                    state >= RoyalTraitState.Discovered ? Mathf.Max(0, turnNumber) : 0,
                    state >= RoyalTraitState.Active ? Mathf.Max(0, turnNumber) : 0));
            return true;
        }

        public RoyalCharacterSaveData CaptureSaveData()
        {
            var saveData = new RoyalCharacterSaveData
            {
                CharacterId = CharacterId,
                DisplayName = DisplayName,
                Role = Role,
                Status = Status,
                Age = Age,
                BaseMaxLifespan = BaseMaxLifespan,
                CurrentReignTurns = CurrentReignTurns,
                FatherId = FatherId,
                MotherId = MotherId,
                ConsortId = ConsortId,
                LifetimeWarningIssued = LifetimeWarningIssued,
                ChildrenIds = new List<string>(childrenIds),
                Traits = new List<RoyalTraitSaveData>(traits.Count)
            };

            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait != null)
                {
                    saveData.Traits.Add(trait.CaptureSaveData());
                }
            }

            saveData.Validate();
            return saveData;
        }

        internal void ResolveTraitDefinitions(RoyalTraitCatalog catalog)
        {
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait != null && catalog != null && catalog.TryGetTrait(trait.TraitId, out var definition))
                {
                    trait.Definition = definition;
                }
            }
        }
    }

    public readonly struct RoyalTraitTransition
    {
        public RoyalTraitTransition(
            RoyalCharacterState character,
            RoyalTraitRuntimeState trait,
            RoyalTraitState previousState,
            RoyalTraitState newState)
        {
            Character = character;
            Trait = trait;
            PreviousState = previousState;
            NewState = newState;
        }

        public RoyalCharacterState Character { get; }
        public RoyalTraitRuntimeState Trait { get; }
        public RoyalTraitState PreviousState { get; }
        public RoyalTraitState NewState { get; }
    }

    public readonly struct RoyalEffectApplicationResult
    {
        public RoyalEffectApplicationResult(RoyalCharacterState character, TalentEffectDefinition effect, bool applied, int amount, string message)
        {
            Character = character;
            Effect = effect;
            Applied = applied;
            Amount = Mathf.Max(0, amount);
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        public RoyalCharacterState Character { get; }
        public TalentEffectDefinition Effect { get; }
        public bool Applied { get; }
        public int Amount { get; }
        public string Message { get; }
        public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    }

    public readonly struct RoyalSuccessionResult
    {
        public RoyalSuccessionResult(
            RoyalSuccessionReason reason,
            RoyalCharacterState previousKing,
            RoyalCharacterState newKing,
            bool crisis)
        {
            Reason = reason;
            PreviousKing = previousKing;
            NewKing = newKing;
            Crisis = crisis;
        }

        public RoyalSuccessionReason Reason { get; }
        public RoyalCharacterState PreviousKing { get; }
        public RoyalCharacterState NewKing { get; }
        public bool Crisis { get; }
        public bool Occurred => Reason != RoyalSuccessionReason.None;
    }

    public sealed class RoyalInheritanceTurnResult
    {
        public RoyalInheritanceTurnResult(
            int turnNumber,
            IReadOnlyList<RoyalCharacterState> bornChildren,
            IReadOnlyList<RoyalTraitTransition> traitTransitions,
            IReadOnlyList<RoyalEffectApplicationResult> effects,
            RoyalSuccessionResult succession,
            bool lifetimeWarningIssued)
        {
            TurnNumber = Mathf.Max(1, turnNumber);
            BornChildren = bornChildren ?? Array.Empty<RoyalCharacterState>();
            TraitTransitions = traitTransitions ?? Array.Empty<RoyalTraitTransition>();
            Effects = effects ?? Array.Empty<RoyalEffectApplicationResult>();
            Succession = succession;
            LifetimeWarningIssued = lifetimeWarningIssued;
        }

        public int TurnNumber { get; }
        public IReadOnlyList<RoyalCharacterState> BornChildren { get; }
        public IReadOnlyList<RoyalTraitTransition> TraitTransitions { get; }
        public IReadOnlyList<RoyalEffectApplicationResult> Effects { get; }
        public RoyalSuccessionResult Succession { get; }
        public bool LifetimeWarningIssued { get; }
        public bool HasAnyChange =>
            BornChildren.Count > 0
            || TraitTransitions.Count > 0
            || Effects.Count > 0
            || Succession.Occurred
            || LifetimeWarningIssued;
    }

    internal readonly struct RoyalTraitInheritanceCandidate
    {
        public RoyalTraitInheritanceCandidate(RoyalTraitDefinition definition, float weight, bool fromBothParents)
        {
            Definition = definition;
            Weight = Mathf.Max(0f, weight);
            FromBothParents = fromBothParents;
        }

        public RoyalTraitDefinition Definition { get; }
        public float Weight { get; }
        public bool FromBothParents { get; }
    }

    public sealed class RoyalInheritanceService
    {
        private static readonly IReadOnlyList<RoyalCharacterState> EmptyCharacters = Array.Empty<RoyalCharacterState>();

        private readonly GameSystem context;
        private readonly List<RoyalCharacterState> characters = new List<RoyalCharacterState>();
        private readonly List<RoyalTraitInheritanceCandidate> inheritanceCandidates =
            new List<RoyalTraitInheritanceCandidate>();
        private readonly HashSet<string> traitIdScratch = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<TalentEffectDefinition> effectScratch = new List<TalentEffectDefinition>();

        private RoyalTraitCatalog traitCatalog;
        private RoyalInheritanceConfig config;
        private string currentKingId = string.Empty;
        private string currentQueenId = string.Empty;
        private int generation = 1;
        private int lastSettlementTurn;
        private int lastSuccessionTurn;
        private int generatedPrinceCount;

        public RoyalInheritanceService(
            GameSystem context,
            RoyalTraitCatalog traitCatalog,
            RoyalInheritanceConfig config,
            RoyalInheritanceSaveData saveData = null)
        {
            this.context = context;
            this.traitCatalog = traitCatalog;
            this.config = config ?? new RoyalInheritanceConfig();
            this.config.Normalize();
            RestoreSaveData(saveData);
        }

        public event Action<RoyalInheritanceService> StateChanged;

        public RoyalTraitCatalog TraitCatalog => traitCatalog;
        public RoyalInheritanceConfig Config => config;
        public IReadOnlyList<RoyalCharacterState> Characters => characters.Count == 0 ? EmptyCharacters : characters;
        public RoyalCharacterState CurrentKing => FindCharacter(currentKingId);
        public RoyalCharacterState CurrentQueen => FindCharacter(currentQueenId);
        public int Generation => generation;
        public int LastSettlementTurn => lastSettlementTurn;
        public int LastSuccessionTurn => lastSuccessionTurn;
        public int LegalHeirAge => config == null ? 16 : config.LegalHeirAge;

        public void SetTraitCatalog(RoyalTraitCatalog newCatalog)
        {
            traitCatalog = newCatalog;
            ResolveDefinitions();
            RefreshEffectiveLifespans();
            NotifyChanged();
        }

        public void SetConfig(RoyalInheritanceConfig newConfig)
        {
            config = newConfig ?? new RoyalInheritanceConfig();
            config.Normalize();
            RefreshEffectiveLifespans();
            NotifyChanged();
        }

        public RoyalInheritanceTurnResult SettleTurn(int turnNumber)
        {
            turnNumber = Mathf.Max(1, turnNumber);
            lastSettlementTurn = turnNumber;

            var bornChildren = new List<RoyalCharacterState>();
            var transitions = new List<RoyalTraitTransition>();
            var effects = new List<RoyalEffectApplicationResult>();
            var succession = new RoyalSuccessionResult(RoyalSuccessionReason.None, null, null, false);

            AgeAliveCharacters();
            var king = CurrentKing;
            if (king != null && king.IsReigning)
            {
                king.CurrentReignTurns++;
            }

            RevealTraits(turnNumber, transitions, effects);
            RefreshEffectiveLifespans();
            ApplyKingEffects(TalentEffectTriggerTiming.OnTurnEnd, turnNumber, effects);
            TryAutoBirthPrince(turnNumber, bornChildren, effects);
            var warningIssued = TryIssueLifetimeWarning(king);

            if (king != null && king.IsReigning && king.Age >= king.EffectiveMaxLifespan)
            {
                succession = ReplaceKing(king, RoyalSuccessionReason.Death, turnNumber, effects);
            }

            if (succession.Occurred)
            {
                RevealTraits(turnNumber, transitions, effects);
                RefreshEffectiveLifespans();
            }

            var result = new RoyalInheritanceTurnResult(
                turnNumber,
                bornChildren.ToArray(),
                transitions.ToArray(),
                effects.ToArray(),
                succession,
                warningIssued);
            if (result.HasAnyChange)
            {
                NotifyChanged();
            }

            return result;
        }

        public bool TryBirthPrince(
            string displayName,
            int turnNumber,
            out RoyalCharacterState prince,
            List<RoyalEffectApplicationResult> effects = null)
        {
            prince = null;
            var king = CurrentKing;
            var queen = CurrentQueen;
            if (king == null || queen == null || !king.IsAlive || !queen.IsAlive)
            {
                return false;
            }

            if (config != null && config.MaxChildren > 0 && king.ChildrenIds.Count >= config.MaxChildren)
            {
                return false;
            }

            prince = CreatePrince(displayName, turnNumber, king, queen);
            ApplyKingEffects(TalentEffectTriggerTiming.OnPrinceBorn, turnNumber, effects);
            NotifyChanged();
            return prince != null;
        }

        public bool TryAbdicateCurrentKing(int turnNumber, out RoyalSuccessionResult succession)
        {
            var king = CurrentKing;
            if (king == null || !king.IsReigning)
            {
                succession = new RoyalSuccessionResult(RoyalSuccessionReason.None, null, null, false);
                return false;
            }

            var effects = new List<RoyalEffectApplicationResult>();
            succession = ReplaceKing(king, RoyalSuccessionReason.Abdication, Mathf.Max(1, turnNumber), effects);
            if (succession.Occurred)
            {
                NotifyChanged();
            }

            return succession.Occurred;
        }

        public bool TryAddAcquiredTrait(string characterId, RoyalTraitDefinition trait, int turnNumber)
        {
            var character = FindCharacter(characterId);
            if (character == null || trait == null || !CanAddTrait(character, trait))
            {
                return false;
            }

            var state = trait.ShouldReveal(context, character)
                ? (trait.ShouldActivate(context, character) ? RoyalTraitState.Active : RoyalTraitState.Discovered)
                : RoyalTraitState.Hidden;
            var added = character.AddTrait(trait, state, true, turnNumber);
            if (added)
            {
                RefreshEffectiveLifespan(character);
                NotifyChanged();
            }

            return added;
        }

        public RoyalInheritanceSaveData CaptureSaveData()
        {
            var saveData = new RoyalInheritanceSaveData
            {
                CurrentKingId = currentKingId,
                CurrentQueenId = currentQueenId,
                Generation = generation,
                LastSettlementTurn = lastSettlementTurn,
                LastSuccessionTurn = lastSuccessionTurn,
                Characters = new List<RoyalCharacterSaveData>(characters.Count)
            };

            for (var i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character != null)
                {
                    saveData.Characters.Add(character.CaptureSaveData());
                }
            }

            saveData.Validate();
            return saveData;
        }

        public void RestoreSaveData(RoyalInheritanceSaveData saveData)
        {
            characters.Clear();
            saveData?.Validate();
            if (saveData == null || saveData.Characters.Count == 0)
            {
                CreateStartingFamily();
                NotifyChanged();
                return;
            }

            currentKingId = saveData.CurrentKingId;
            currentQueenId = saveData.CurrentQueenId;
            generation = saveData.Generation;
            lastSettlementTurn = saveData.LastSettlementTurn;
            lastSuccessionTurn = saveData.LastSuccessionTurn;

            for (var i = 0; i < saveData.Characters.Count; i++)
            {
                characters.Add(new RoyalCharacterState(saveData.Characters[i], traitCatalog));
            }

            ResolveDefinitions();
            RefreshEffectiveLifespans();
            generatedPrinceCount = CountPrinces();
            NotifyChanged();
        }

        public RoyalCharacterState FindCharacter(string characterId)
        {
            characterId = RoyalTraitDefinition.NormalizeId(characterId);
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            for (var i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character != null && character.CharacterId == characterId)
                {
                    return character;
                }
            }

            return null;
        }

        public void GetPotentialHeirs(List<RoyalCharacterState> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (var i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character != null && character.IsPotentialHeir)
                {
                    results.Add(character);
                }
            }

            results.Sort(CompareHeirs);
        }

        private void CreateStartingFamily()
        {
            characters.Clear();
            generation = 1;
            lastSettlementTurn = 0;
            lastSuccessionTurn = 0;
            generatedPrinceCount = 0;

            var setupKing = config == null ? null : config.StartingKing;
            var setupQueen = config == null ? null : config.StartingQueen;
            var king = new RoyalCharacterState(
                Guid.NewGuid().ToString("N"),
                string.IsNullOrWhiteSpace(setupKing == null ? null : setupKing.DisplayName) ? "初代国王" : setupKing.DisplayName,
                RoyalCharacterRole.King,
                RoyalCharacterStatus.Reigning,
                setupKing == null ? 20 : setupKing.Age,
                setupKing == null ? 80 : setupKing.BaseMaxLifespan,
                string.Empty,
                string.Empty,
                string.Empty);
            var queen = new RoyalCharacterState(
                Guid.NewGuid().ToString("N"),
                string.IsNullOrWhiteSpace(setupQueen == null ? null : setupQueen.DisplayName) ? "初代王后" : setupQueen.DisplayName,
                RoyalCharacterRole.Queen,
                RoyalCharacterStatus.Consort,
                setupQueen == null ? 20 : setupQueen.Age,
                setupQueen == null ? 80 : setupQueen.BaseMaxLifespan,
                string.Empty,
                string.Empty,
                king.CharacterId);

            king.ConsortId = queen.CharacterId;
            characters.Add(king);
            characters.Add(queen);
            currentKingId = king.CharacterId;
            currentQueenId = queen.CharacterId;
            AddStartingTraits(king, setupKing);
            AddStartingTraits(queen, setupQueen);
            RefreshEffectiveLifespans();
        }

        private void AddStartingTraits(RoyalCharacterState character, RoyalInheritanceStartingCharacter setup)
        {
            if (character == null || setup == null)
            {
                return;
            }

            var traits = setup.Traits;
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait == null || !CanAddTrait(character, trait))
                {
                    continue;
                }

                var state = trait.ShouldReveal(context, character)
                    ? (trait.ShouldActivate(context, character) ? RoyalTraitState.Active : RoyalTraitState.Discovered)
                    : RoyalTraitState.Hidden;
                character.AddTrait(trait, state, trait.Acquired, 0);
            }
        }

        private void AgeAliveCharacters()
        {
            for (var i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character != null && character.IsAlive)
                {
                    character.Age++;
                }
            }
        }

        private void TryAutoBirthPrince(
            int turnNumber,
            List<RoyalCharacterState> bornChildren,
            List<RoyalEffectApplicationResult> effects)
        {
            if (config == null || config.BirthChancePerTurn <= 0f || UnityEngine.Random.value > config.BirthChancePerTurn)
            {
                return;
            }

            if (TryBirthPrince(string.Empty, turnNumber, out var prince, effects))
            {
                bornChildren?.Add(prince);
            }
        }

        private RoyalCharacterState CreatePrince(
            string displayName,
            int turnNumber,
            RoyalCharacterState father,
            RoyalCharacterState mother)
        {
            generatedPrinceCount++;
            var prince = new RoyalCharacterState(
                Guid.NewGuid().ToString("N"),
                string.IsNullOrWhiteSpace(displayName) ? $"王子 {generatedPrinceCount}" : displayName.Trim(),
                RoyalCharacterRole.Prince,
                RoyalCharacterStatus.Heir,
                0,
                config == null ? 80 : config.PrinceBaseMaxLifespan,
                father.CharacterId,
                mother.CharacterId,
                string.Empty);

            characters.Add(prince);
            father.AddChild(prince.CharacterId);
            mother.AddChild(prince.CharacterId);
            GenerateInheritedTraits(prince, father, mother, turnNumber);
            RefreshEffectiveLifespan(prince);
            return prince;
        }

        private void GenerateInheritedTraits(
            RoyalCharacterState prince,
            RoyalCharacterState father,
            RoyalCharacterState mother,
            int turnNumber)
        {
            inheritanceCandidates.Clear();
            CollectInheritanceCandidates(father, mother);
            for (var i = 0; i < inheritanceCandidates.Count; i++)
            {
                var candidate = inheritanceCandidates[i];
                if (candidate.Definition == null || !CanAddTrait(prince, candidate.Definition))
                {
                    continue;
                }

                var chance = Mathf.Clamp01(candidate.Weight * GetRarityMultiplier(candidate.Definition.Rarity));
                if (UnityEngine.Random.value <= chance)
                {
                    AddGeneratedTrait(prince, candidate.Definition, turnNumber);
                }
            }

            TryMutateTrait(prince, turnNumber);
        }

        private void CollectInheritanceCandidates(RoyalCharacterState father, RoyalCharacterState mother)
        {
            var weights = new Dictionary<string, float>(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            CollectParentTraitWeights(father, weights, counts);
            CollectParentTraitWeights(mother, weights, counts);

            foreach (var entry in weights)
            {
                var definition = traitCatalog != null && traitCatalog.TryGetTrait(entry.Key, out var trait)
                    ? trait
                    : null;
                if (definition == null)
                {
                    continue;
                }

                counts.TryGetValue(entry.Key, out var parentCount);
                var weight = parentCount > 1 ? entry.Value * definition.BothParentsWeightMultiplier : entry.Value;
                inheritanceCandidates.Add(new RoyalTraitInheritanceCandidate(definition, weight, parentCount > 1));
            }
        }

        private static void CollectParentTraitWeights(
            RoyalCharacterState parent,
            Dictionary<string, float> weights,
            Dictionary<string, int> counts)
        {
            if (parent == null || weights == null || counts == null)
            {
                return;
            }

            var traits = parent.Traits;
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                var definition = trait == null ? null : trait.Definition;
                if (definition == null || !definition.CanBeInheritedFrom(trait))
                {
                    continue;
                }

                if (!weights.ContainsKey(definition.TraitId))
                {
                    weights.Add(definition.TraitId, 0f);
                    counts.Add(definition.TraitId, 0);
                }

                weights[definition.TraitId] += definition.InheritWeight;
                counts[definition.TraitId] += 1;
            }
        }

        private void TryMutateTrait(RoyalCharacterState prince, int turnNumber)
        {
            if (config == null
                || traitCatalog == null
                || config.MutationChance <= 0f
                || UnityEngine.Random.value > config.MutationChance)
            {
                return;
            }

            var mutationTraits = traitCatalog.MutationTraits;
            RoyalTraitDefinition selected = null;
            var totalWeight = 0f;
            for (var i = 0; i < mutationTraits.Count; i++)
            {
                var trait = mutationTraits[i];
                if (trait == null || !CanAddTrait(prince, trait))
                {
                    continue;
                }

                totalWeight += Mathf.Max(0f, trait.InheritWeight) * config.MutationWeightMultiplier * GetRarityMultiplier(trait.Rarity);
            }

            if (totalWeight <= 0f)
            {
                return;
            }

            var roll = UnityEngine.Random.value * totalWeight;
            for (var i = 0; i < mutationTraits.Count; i++)
            {
                var trait = mutationTraits[i];
                if (trait == null || !CanAddTrait(prince, trait))
                {
                    continue;
                }

                var weight = Mathf.Max(0f, trait.InheritWeight) * config.MutationWeightMultiplier * GetRarityMultiplier(trait.Rarity);
                if (roll <= weight)
                {
                    selected = trait;
                    break;
                }

                roll -= weight;
            }

            if (selected != null)
            {
                AddGeneratedTrait(prince, selected, turnNumber);
            }
        }

        private void AddGeneratedTrait(RoyalCharacterState character, RoyalTraitDefinition trait, int turnNumber)
        {
            var state = trait.ShouldReveal(context, character)
                ? (trait.ShouldActivate(context, character) ? RoyalTraitState.Active : RoyalTraitState.Discovered)
                : RoyalTraitState.Hidden;
            character.AddTrait(trait, state, false, turnNumber);
        }

        private bool CanAddTrait(RoyalCharacterState character, RoyalTraitDefinition trait)
        {
            if (character == null || trait == null || !trait.IsValid || character.HasTrait(trait.TraitId))
            {
                return false;
            }

            traitIdScratch.Clear();
            var traits = character.Traits;
            for (var i = 0; i < traits.Count; i++)
            {
                var existing = traits[i];
                if (existing == null || string.IsNullOrWhiteSpace(existing.TraitId))
                {
                    continue;
                }

                if (trait.ConflictsWith(existing.TraitId) || existing.Definition != null && existing.Definition.ConflictsWith(trait.TraitId))
                {
                    traitIdScratch.Clear();
                    return false;
                }

                traitIdScratch.Add(existing.TraitId);
            }

            var requirementsMet = trait.RequirementsMet(traitIdScratch);
            traitIdScratch.Clear();
            return requirementsMet;
        }

        private void RevealTraits(
            int turnNumber,
            List<RoyalTraitTransition> transitions,
            List<RoyalEffectApplicationResult> effects)
        {
            for (var i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                if (character == null || !character.IsAlive)
                {
                    continue;
                }

                var traits = character.Traits;
                for (var j = 0; j < traits.Count; j++)
                {
                    var trait = traits[j];
                    var definition = trait == null ? null : trait.Definition;
                    if (trait == null || definition == null)
                    {
                        continue;
                    }

                    if (trait.State == RoyalTraitState.Hidden && definition.ShouldReveal(context, character))
                    {
                        var previous = trait.State;
                        trait.State = RoyalTraitState.Discovered;
                        trait.RevealedTurn = turnNumber;
                        transitions?.Add(new RoyalTraitTransition(character, trait, previous, trait.State));
                        ApplyTraitEffects(character, trait, TalentEffectTriggerTiming.OnTraitRevealed, turnNumber, effects);
                    }

                    if (trait.State == RoyalTraitState.Discovered && definition.ShouldActivate(context, character))
                    {
                        var previous = trait.State;
                        trait.State = RoyalTraitState.Active;
                        trait.ActivatedTurn = turnNumber;
                        transitions?.Add(new RoyalTraitTransition(character, trait, previous, trait.State));
                    }
                }
            }
        }

        private bool TryIssueLifetimeWarning(RoyalCharacterState king)
        {
            if (king == null || !king.IsReigning || king.LifetimeWarningIssued)
            {
                return false;
            }

            var threshold = GetLifetimeWarningThreshold(king);
            if (threshold <= 0 || king.RemainingLifespan > threshold)
            {
                return false;
            }

            king.LifetimeWarningIssued = true;
            return true;
        }

        public int GetLifetimeWarningThreshold(RoyalCharacterState character)
        {
            if (character == null)
            {
                return 0;
            }

            var maxThreshold = 0;
            var traits = character.Traits;
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait == null || !trait.IsActive || trait.Definition == null)
                {
                    continue;
                }

                var effects = trait.Definition.Effects;
                for (var j = 0; j < effects.Count; j++)
                {
                    var effect = effects[j];
                    if (effect == null || effect.EffectType != TalentEffectType.LifetimeWarning)
                    {
                        continue;
                    }

                    maxThreshold = Mathf.Max(maxThreshold, Mathf.FloorToInt(effect.CalculateRawValue(context, null)));
                }
            }

            return Mathf.Max(0, maxThreshold);
        }

        private RoyalSuccessionResult ReplaceKing(
            RoyalCharacterState oldKing,
            RoyalSuccessionReason reason,
            int turnNumber,
            List<RoyalEffectApplicationResult> effects)
        {
            if (oldKing == null)
            {
                return new RoyalSuccessionResult(RoyalSuccessionReason.None, null, null, false);
            }

            ApplyCharacterEffects(oldKing, TalentEffectTriggerTiming.OnKingDeath, turnNumber, effects);
            oldKing.Status = reason == RoyalSuccessionReason.Abdication
                ? RoyalCharacterStatus.Retired
                : RoyalCharacterStatus.Dead;

            var oldQueen = CurrentQueen;
            if (oldQueen != null && oldQueen.IsAlive)
            {
                oldQueen.Status = RoyalCharacterStatus.Retired;
            }

            var successor = SelectSuccessor();
            if (successor == null)
            {
                currentKingId = string.Empty;
                currentQueenId = string.Empty;
                lastSuccessionTurn = turnNumber;
                return new RoyalSuccessionResult(RoyalSuccessionReason.Crisis, oldKing, null, true);
            }

            successor.Role = RoyalCharacterRole.King;
            successor.Status = RoyalCharacterStatus.Reigning;
            successor.CurrentReignTurns = 0;
            successor.LifetimeWarningIssued = false;
            currentKingId = successor.CharacterId;
            currentQueenId = string.Empty;
            generation++;
            lastSuccessionTurn = turnNumber;
            RefreshEffectiveLifespan(successor);
            ApplyCharacterEffects(successor, TalentEffectTriggerTiming.OnKingAscend, turnNumber, effects);
            return new RoyalSuccessionResult(reason, oldKing, successor, false);
        }

        private RoyalCharacterState SelectSuccessor()
        {
            RoyalCharacterState bestLegal = null;
            RoyalCharacterState bestFallback = null;
            for (var i = 0; i < characters.Count; i++)
            {
                var candidate = characters[i];
                if (candidate == null || !candidate.IsPotentialHeir)
                {
                    continue;
                }

                if (candidate.IsLegalHeir(LegalHeirAge) && IsHigherPriorityHeir(candidate, bestLegal))
                {
                    bestLegal = candidate;
                }

                if (IsHigherPriorityHeir(candidate, bestFallback))
                {
                    bestFallback = candidate;
                }
            }

            return bestLegal ?? bestFallback;
        }

        private bool IsHigherPriorityHeir(RoyalCharacterState candidate, RoyalCharacterState currentBest)
        {
            if (candidate == null)
            {
                return false;
            }

            if (currentBest == null)
            {
                return true;
            }

            if (candidate.Age != currentBest.Age)
            {
                return candidate.Age > currentBest.Age;
            }

            return string.Compare(candidate.CharacterId, currentBest.CharacterId, StringComparison.Ordinal) < 0;
        }

        private int CompareHeirs(RoyalCharacterState left, RoyalCharacterState right)
        {
            var leftLegal = left != null && left.IsLegalHeir(LegalHeirAge);
            var rightLegal = right != null && right.IsLegalHeir(LegalHeirAge);
            if (leftLegal != rightLegal)
            {
                return leftLegal ? -1 : 1;
            }

            var leftAge = left == null ? -1 : left.Age;
            var rightAge = right == null ? -1 : right.Age;
            if (leftAge != rightAge)
            {
                return rightAge.CompareTo(leftAge);
            }

            return string.Compare(
                left == null ? string.Empty : left.DisplayName,
                right == null ? string.Empty : right.DisplayName,
                StringComparison.Ordinal);
        }

        private void ApplyKingEffects(
            TalentEffectTriggerTiming timing,
            int turnNumber,
            List<RoyalEffectApplicationResult> results)
        {
            ApplyCharacterEffects(CurrentKing, timing, turnNumber, results);
        }

        private void ApplyCharacterEffects(
            RoyalCharacterState character,
            TalentEffectTriggerTiming timing,
            int turnNumber,
            List<RoyalEffectApplicationResult> results)
        {
            if (character == null || results == null)
            {
                return;
            }

            var traits = character.Traits;
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait == null || !trait.IsActive)
                {
                    continue;
                }

                ApplyTraitEffects(character, trait, timing, turnNumber, results);
            }
        }

        private void ApplyTraitEffects(
            RoyalCharacterState character,
            RoyalTraitRuntimeState trait,
            TalentEffectTriggerTiming timing,
            int turnNumber,
            List<RoyalEffectApplicationResult> results)
        {
            if (character == null || trait == null || trait.Definition == null || results == null)
            {
                return;
            }

            var effects = trait.Definition.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null || effect.TriggerTiming != timing)
                {
                    continue;
                }

                var result = ApplyEffect(character, effect, turnNumber);
                if (result.Applied || result.HasMessage)
                {
                    results.Add(result);
                }
            }
        }

        private RoyalEffectApplicationResult ApplyEffect(RoyalCharacterState character, TalentEffectDefinition effect, int turnNumber)
        {
            if (context == null || character == null || effect == null)
            {
                return new RoyalEffectApplicationResult(character, effect, false, 0, string.Empty);
            }

            var amount = Mathf.Max(0, effect.CalculateAmount(context, null));
            switch (effect.EffectType)
            {
                case TalentEffectType.AddItem:
                    return ApplyAddItemEffect(character, effect, amount);
                case TalentEffectType.AddResearchPoints:
                    return ApplyResearchPointsEffect(character, effect, amount, turnNumber);
                case TalentEffectType.UnlockBuildingBlueprint:
                    return ApplyUnlockBlueprintEffect(character, effect);
                default:
                    return new RoyalEffectApplicationResult(character, effect, false, 0, string.Empty);
            }
        }

        private RoyalEffectApplicationResult ApplyAddItemEffect(
            RoyalCharacterState character,
            TalentEffectDefinition effect,
            int amount)
        {
            if (context.Inventory == null || effect.ItemDefinition == null || amount <= 0)
            {
                return new RoyalEffectApplicationResult(character, effect, false, amount, string.Empty);
            }

            var added = context.Inventory.AddItem(effect.ItemDefinition, amount);
            var message = added > 0
                ? $"{character.DisplayName}：{effect.ItemDefinition.DisplayName}+{added}"
                : $"{character.DisplayName}：{effect.ItemDefinition.DisplayName}+0（仓库已满）";
            return new RoyalEffectApplicationResult(character, effect, added > 0, added, message);
        }

        private RoyalEffectApplicationResult ApplyResearchPointsEffect(
            RoyalCharacterState character,
            TalentEffectDefinition effect,
            int amount,
            int turnNumber)
        {
            if (amount <= 0)
            {
                return new RoyalEffectApplicationResult(character, effect, false, 0, string.Empty);
            }

            context.ApplyRoyalResearchPoints(amount, turnNumber, character.DisplayName);
            return new RoyalEffectApplicationResult(character, effect, true, amount, $"{character.DisplayName}：研究点+{amount}");
        }

        private RoyalEffectApplicationResult ApplyUnlockBlueprintEffect(RoyalCharacterState character, TalentEffectDefinition effect)
        {
            var building = effect.BuildingPrefab;
            if (building == null || !building.HasDefinition)
            {
                return new RoyalEffectApplicationResult(character, effect, false, 0, string.Empty);
            }

            var unlocked = context.UnlockBuildingBlueprint(building.Definition.BuildingId);
            var message = unlocked
                ? $"{character.DisplayName}：解锁蓝图 {building.Definition.DisplayName}"
                : $"{character.DisplayName}：蓝图已解锁 {building.Definition.DisplayName}";
            return new RoyalEffectApplicationResult(character, effect, unlocked, unlocked ? 1 : 0, message);
        }

        private void RefreshEffectiveLifespans()
        {
            for (var i = 0; i < characters.Count; i++)
            {
                RefreshEffectiveLifespan(characters[i]);
            }
        }

        private void RefreshEffectiveLifespan(RoyalCharacterState character)
        {
            if (character == null)
            {
                return;
            }

            var bonus = 0f;
            var traits = character.Traits;
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait == null || !trait.IsActive || trait.Definition == null)
                {
                    continue;
                }

                effectScratch.Clear();
                var effects = trait.Definition.Effects;
                for (var j = 0; j < effects.Count; j++)
                {
                    var effect = effects[j];
                    if (effect != null && effect.EffectType == TalentEffectType.ModifyMaxLifespan)
                    {
                        bonus += effect.CalculateRawValue(context, null);
                    }
                }
            }

            effectScratch.Clear();
            character.EffectiveMaxLifespan = Mathf.Max(1, character.BaseMaxLifespan + Mathf.RoundToInt(bonus));
        }

        private void ResolveDefinitions()
        {
            for (var i = 0; i < characters.Count; i++)
            {
                characters[i]?.ResolveTraitDefinitions(traitCatalog);
            }
        }

        private int CountPrinces()
        {
            var count = 0;
            for (var i = 0; i < characters.Count; i++)
            {
                if (characters[i] != null && characters[i].Role == RoyalCharacterRole.Prince)
                {
                    count++;
                }
            }

            return count;
        }

        private static float GetRarityMultiplier(TalentRarity rarity)
        {
            return rarity switch
            {
                TalentRarity.Uncommon => 0.8f,
                TalentRarity.Rare => 0.55f,
                TalentRarity.Epic => 0.3f,
                TalentRarity.Legendary => 0.12f,
                _ => 1f
            };
        }

        private void NotifyChanged()
        {
            StateChanged?.Invoke(this);
        }
    }
}
