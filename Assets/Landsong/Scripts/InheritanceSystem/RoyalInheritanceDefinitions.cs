using System;
using System.Collections.Generic;
using Landsong.ConditionSystem;
using Landsong.TalentSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.InheritanceSystem
{
    public enum RoyalCharacterRole
    {
        King = 0,
        Queen = 10,
        Prince = 20
    }

    public enum RoyalCharacterStatus
    {
        Reigning = 0,
        Consort = 10,
        Heir = 20,
        Retired = 30,
        Dead = 40
    }

    public enum RoyalTraitType
    {
        General = 0,
        Lifespan = 10,
        Finance = 20,
        Military = 30,
        Scholarship = 40,
        Personality = 50,
        Negative = 60,
        Hidden = 70
    }

    public enum RoyalTraitRevealType
    {
        BirthVisible = 0,
        Age = 10,
        Condition = 20,
        Reigning = 30,
        EventOnly = 40
    }

    public enum RoyalTraitState
    {
        Hidden = 0,
        Discovered = 10,
        Active = 20
    }

    public enum RoyalSuccessionReason
    {
        None = 0,
        Death = 10,
        Abdication = 20,
        Crisis = 30
    }

    [Serializable]
    public sealed class RoyalTraitDefinitionRef
    {
        [SerializeField, LabelText("特性")]
        private RoyalTraitDefinition trait;

        public RoyalTraitDefinition Trait => trait;
        public string TraitId => trait == null ? string.Empty : trait.TraitId;
        public bool IsValid => trait != null && trait.IsValid;
    }

    [CreateAssetMenu(menuName = "Landsong/Inheritance/Royal Trait", fileName = "RoyalTrait")]
    public sealed class RoyalTraitDefinition : ScriptableObject
    {
        [TitleGroup("基础信息")]
        [SerializeField, LabelText("特性ID")]
        private string traitId;

        [SerializeField, LabelText("特性名称")]
        private string traitName;

        [SerializeField, TextArea, LabelText("描述")]
        private string description;

        [SerializeField, LabelText("特性类型")]
        private RoyalTraitType traitType = RoyalTraitType.General;

        [SerializeField, LabelText("稀有度")]
        private TalentRarity rarity = TalentRarity.Common;

        [SerializeField, PreviewField(48), LabelText("图标")]
        private Sprite icon;

        [SerializeField, LabelText("显示颜色")]
        private Color displayColor = Color.white;

        [TitleGroup("遗传")]
        [SerializeField, LabelText("可遗传")]
        private bool heritable = true;

        [SerializeField, LabelText("遗传权重"), Min(0f)]
        private float inheritWeight = 1f;

        [SerializeField, LabelText("双方拥有时权重倍率"), Min(0f)]
        private float bothParentsWeightMultiplier = 1.5f;

        [SerializeField, LabelText("后天获得")]
        private bool acquired;

        [SerializeField, LabelText("后天获得后可遗传")]
        private bool canBeInheritedAfterAcquired;

        [TitleGroup("显现")]
        [SerializeField, LabelText("显现方式")]
        private RoyalTraitRevealType revealType = RoyalTraitRevealType.BirthVisible;

        [SerializeField, LabelText("显现年龄"), Min(0)]
        private int revealAge;

        [SerializeField, LabelText("激活年龄"), Min(0)]
        private int activationAge;

        [SerializeField, LabelText("显现时自动激活")]
        private bool activateWhenRevealed = true;

        [SerializeField, LabelText("必须成为国王才激活")]
        private bool activationRequiresReigning;

        [SerializeReference, LabelText("显现条件")]
        private GameCondition revealCondition;

        [SerializeReference, LabelText("激活条件")]
        private GameCondition activationCondition;

        [TitleGroup("效果与关系")]
        [SerializeField, LabelText("效果列表")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        private TalentEffectDefinition[] effects = Array.Empty<TalentEffectDefinition>();

        [SerializeField, LabelText("互斥特性")]
        private RoyalTraitDefinition[] conflictTraits = Array.Empty<RoyalTraitDefinition>();

        [SerializeField, LabelText("前置特性")]
        private RoyalTraitDefinition[] requiredTraits = Array.Empty<RoyalTraitDefinition>();

        public string TraitId => NormalizeId(traitId);
        public string TraitName => string.IsNullOrWhiteSpace(traitName)
            ? (string.IsNullOrWhiteSpace(TraitId) ? name : TraitId)
            : traitName.Trim();
        public string Description => string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        public RoyalTraitType TraitType => traitType;
        public TalentRarity Rarity => rarity;
        public Sprite Icon => icon;
        public Color DisplayColor => displayColor;
        public bool Heritable => heritable;
        public float InheritWeight => Mathf.Max(0f, inheritWeight);
        public float BothParentsWeightMultiplier => Mathf.Max(0f, bothParentsWeightMultiplier);
        public bool Acquired => acquired;
        public bool CanBeInheritedAfterAcquired => canBeInheritedAfterAcquired;
        public RoyalTraitRevealType RevealType => revealType;
        public int RevealAge => Mathf.Max(0, revealAge);
        public int ActivationAge => Mathf.Max(0, activationAge);
        public bool ActivateWhenRevealed => activateWhenRevealed;
        public bool ActivationRequiresReigning => activationRequiresReigning;
        public GameCondition RevealCondition => revealCondition;
        public GameCondition ActivationCondition => activationCondition;
        public IReadOnlyList<TalentEffectDefinition> Effects => effects ?? Array.Empty<TalentEffectDefinition>();
        public IReadOnlyList<RoyalTraitDefinition> ConflictTraits => conflictTraits ?? Array.Empty<RoyalTraitDefinition>();
        public IReadOnlyList<RoyalTraitDefinition> RequiredTraits => requiredTraits ?? Array.Empty<RoyalTraitDefinition>();
        public bool IsValid => !string.IsNullOrWhiteSpace(TraitId);

        private void OnEnable()
        {
            Normalize();
        }

        private void OnValidate()
        {
            Normalize();
        }

        public void Normalize()
        {
            traitId = NormalizeId(traitId);
            traitName = string.IsNullOrWhiteSpace(traitName) ? string.Empty : traitName.Trim();
            description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
            inheritWeight = Mathf.Max(0f, inheritWeight);
            bothParentsWeightMultiplier = Mathf.Max(0f, bothParentsWeightMultiplier);
            revealAge = Mathf.Max(0, revealAge);
            activationAge = Mathf.Max(0, activationAge);
            effects ??= Array.Empty<TalentEffectDefinition>();
            for (var i = 0; i < effects.Length; i++)
            {
                effects[i]?.Normalize();
            }

            conflictTraits ??= Array.Empty<RoyalTraitDefinition>();
            requiredTraits ??= Array.Empty<RoyalTraitDefinition>();
        }

        public bool CanBeInheritedFrom(RoyalTraitRuntimeState traitState)
        {
            if (traitState == null || !Heritable || InheritWeight <= 0f)
            {
                return false;
            }

            return !traitState.IsAcquired || CanBeInheritedAfterAcquired;
        }

        public bool ShouldReveal(GameSystem context, RoyalCharacterState character)
        {
            if (character == null)
            {
                return false;
            }

            var conditionMet = revealCondition == null || revealCondition.IsMet(context);
            return RevealType switch
            {
                RoyalTraitRevealType.BirthVisible => true,
                RoyalTraitRevealType.Age => character.Age >= RevealAge && conditionMet,
                RoyalTraitRevealType.Condition => conditionMet,
                RoyalTraitRevealType.Reigning => character.IsReigning && conditionMet,
                _ => false
            };
        }

        public bool ShouldActivate(GameSystem context, RoyalCharacterState character)
        {
            if (character == null)
            {
                return false;
            }

            if (ActivationRequiresReigning && !character.IsReigning)
            {
                return false;
            }

            if (ActivationAge > 0 && character.Age < ActivationAge)
            {
                return false;
            }

            if (activationCondition != null && !activationCondition.IsMet(context))
            {
                return false;
            }

            return ActivateWhenRevealed || ActivationRequiresReigning || ActivationAge > 0 || activationCondition != null;
        }

        public bool ConflictsWith(string otherTraitId)
        {
            otherTraitId = NormalizeId(otherTraitId);
            if (string.IsNullOrWhiteSpace(otherTraitId))
            {
                return false;
            }

            var conflicts = ConflictTraits;
            for (var i = 0; i < conflicts.Count; i++)
            {
                if (conflicts[i] != null && conflicts[i].TraitId == otherTraitId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool RequirementsMet(IReadOnlyCollection<string> existingTraitIds)
        {
            var requirements = RequiredTraits;
            if (requirements.Count == 0)
            {
                return true;
            }

            if (existingTraitIds == null)
            {
                return false;
            }

            for (var i = 0; i < requirements.Count; i++)
            {
                var requirement = requirements[i];
                if (requirement != null && !ContainsTraitId(existingTraitIds, requirement.TraitId))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsTraitId(IReadOnlyCollection<string> traitIds, string traitId)
        {
            traitId = NormalizeId(traitId);
            if (traitIds == null || string.IsNullOrWhiteSpace(traitId))
            {
                return false;
            }

            foreach (var existingTraitId in traitIds)
            {
                if (string.Equals(existingTraitId, traitId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [CreateAssetMenu(menuName = "Landsong/Inheritance/Royal Trait Catalog", fileName = "RoyalTraitCatalog")]
    public sealed class RoyalTraitCatalog : ScriptableObject
    {
        [SerializeField, LabelText("特性定义")]
        private RoyalTraitDefinition[] traits = Array.Empty<RoyalTraitDefinition>();

        [SerializeField, LabelText("变异候选特性")]
        private RoyalTraitDefinition[] mutationTraits = Array.Empty<RoyalTraitDefinition>();

        private Dictionary<string, RoyalTraitDefinition> traitsById;

        public IReadOnlyList<RoyalTraitDefinition> Traits => traits ?? Array.Empty<RoyalTraitDefinition>();
        public IReadOnlyList<RoyalTraitDefinition> MutationTraits => mutationTraits ?? Array.Empty<RoyalTraitDefinition>();

        private void OnEnable()
        {
            Normalize();
            RebuildIndex();
        }

        private void OnValidate()
        {
            Normalize();
            RebuildIndex();
        }

        public bool TryGetTrait(string traitId, out RoyalTraitDefinition trait)
        {
            traitId = RoyalTraitDefinition.NormalizeId(traitId);
            if (string.IsNullOrWhiteSpace(traitId))
            {
                trait = null;
                return false;
            }

            EnsureIndex();
            return traitsById.TryGetValue(traitId, out trait);
        }

        public void RebuildIndex()
        {
            traitsById = new Dictionary<string, RoyalTraitDefinition>(StringComparer.Ordinal);
            AddToIndex(Traits);
            AddToIndex(MutationTraits);
        }

        private void AddToIndex(IReadOnlyList<RoyalTraitDefinition> source)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var trait = source[i];
                if (trait == null || !trait.IsValid || traitsById.ContainsKey(trait.TraitId))
                {
                    continue;
                }

                traitsById.Add(trait.TraitId, trait);
            }
        }

        private void EnsureIndex()
        {
            if (traitsById == null)
            {
                RebuildIndex();
            }
        }

        private void Normalize()
        {
            traits ??= Array.Empty<RoyalTraitDefinition>();
            mutationTraits ??= Array.Empty<RoyalTraitDefinition>();
            for (var i = 0; i < traits.Length; i++)
            {
                traits[i]?.Normalize();
            }

            for (var i = 0; i < mutationTraits.Length; i++)
            {
                mutationTraits[i]?.Normalize();
            }
        }
    }

    [Serializable]
    public sealed class RoyalInheritanceStartingCharacter
    {
        [SerializeField, LabelText("姓名")]
        private string displayName;

        [SerializeField, LabelText("年龄"), Min(0)]
        private int age = 20;

        [SerializeField, LabelText("基础最大寿命"), Min(1)]
        private int baseMaxLifespan = 80;

        [SerializeField, LabelText("初始特性")]
        private RoyalTraitDefinition[] traits = Array.Empty<RoyalTraitDefinition>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
        public int Age => Mathf.Max(0, age);
        public int BaseMaxLifespan => Mathf.Max(1, baseMaxLifespan);
        public IReadOnlyList<RoyalTraitDefinition> Traits => traits ?? Array.Empty<RoyalTraitDefinition>();

        public void Normalize()
        {
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            age = Mathf.Max(0, age);
            baseMaxLifespan = Mathf.Max(1, baseMaxLifespan);
            traits ??= Array.Empty<RoyalTraitDefinition>();
        }
    }

    [Serializable]
    public sealed class RoyalInheritanceConfig
    {
        [SerializeField, LabelText("初代国王")]
        private RoyalInheritanceStartingCharacter startingKing = new RoyalInheritanceStartingCharacter();

        [SerializeField, LabelText("初代王后")]
        private RoyalInheritanceStartingCharacter startingQueen = new RoyalInheritanceStartingCharacter();

        [SerializeField, LabelText("成年年龄"), Min(1)]
        private int legalHeirAge = 16;

        [SerializeField, LabelText("王子基础最大寿命"), Min(1)]
        private int princeBaseMaxLifespan = 80;

        [SerializeField, LabelText("每回合出生概率"), Range(0f, 1f)]
        private float birthChancePerTurn = 0.08f;

        [SerializeField, LabelText("最大子嗣数量"), Min(0)]
        private int maxChildren = 8;

        [SerializeField, LabelText("变异概率"), Range(0f, 1f)]
        private float mutationChance = 0.03f;

        [SerializeField, LabelText("变异权重倍率"), Min(0f)]
        private float mutationWeightMultiplier = 0.5f;

        public RoyalInheritanceStartingCharacter StartingKing => startingKing;
        public RoyalInheritanceStartingCharacter StartingQueen => startingQueen;
        public int LegalHeirAge => Mathf.Max(1, legalHeirAge);
        public int PrinceBaseMaxLifespan => Mathf.Max(1, princeBaseMaxLifespan);
        public float BirthChancePerTurn => Mathf.Clamp01(birthChancePerTurn);
        public int MaxChildren => Mathf.Max(0, maxChildren);
        public float MutationChance => Mathf.Clamp01(mutationChance);
        public float MutationWeightMultiplier => Mathf.Max(0f, mutationWeightMultiplier);

        public void Normalize()
        {
            startingKing ??= new RoyalInheritanceStartingCharacter();
            startingQueen ??= new RoyalInheritanceStartingCharacter();
            startingKing.Normalize();
            startingQueen.Normalize();
            legalHeirAge = Mathf.Max(1, legalHeirAge);
            princeBaseMaxLifespan = Mathf.Max(1, princeBaseMaxLifespan);
            birthChancePerTurn = Mathf.Clamp01(birthChancePerTurn);
            maxChildren = Mathf.Max(0, maxChildren);
            mutationChance = Mathf.Clamp01(mutationChance);
            mutationWeightMultiplier = Mathf.Max(0f, mutationWeightMultiplier);
        }
    }
}
