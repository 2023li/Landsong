using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.ConditionSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.TalentSystem
{
    public enum TalentProfession
    {
        None = 0,
        大总管 = 10,
        大法师 = 20,
        大将军 = 30,
        大学者 = 40
    }

    public enum TalentRarity
    {
        Common = 0,
        Uncommon = 10,
        Rare = 20,
        Epic = 30,
        Legendary = 40
    }

    public enum TalentEffectType
    {
        AddItem = 10,
        AddResearchPoints = 20,
        UnlockBuildingBlueprint = 30,
        ModifyResourceGainPercent = 100,
        ModifyCostPercent = 110,
        ModifyBuildSpeed = 120,
        ModifyResearchSpeed = 130,
        ModifyMilitaryPower = 140,
        ModifyHappiness = 150,
        ModifyRisk = 160,
        ModifyMaxLifespan = 300,
        LifetimeWarning = 310
    }

    public enum TalentEffectTriggerTiming
    {
        Passive = 0,
        OnTurnEnd = 10,
        OnResourceProduced = 20,
        OnBuildingConstructed = 30,
        OnResearchCompleted = 40,
        OnTradeCompleted = 50,
        OnRecruitSoldier = 60,
        OnKingAscend = 200,
        OnKingDeath = 210,
        OnPrinceBorn = 220,
        OnTraitRevealed = 230
    }

    public enum TalentEffectValueType
    {
        Fixed = 0,
        Percent = 10,
        PercentOfCurrentStorage = 20,
        PerTalentLevel = 30,
        BasedOnPopulation = 40,
        BasedOnBuildingCount = 50
    }

    public enum TalentHiddenTraitState
    {
        Undiscovered = 0,
        Discovered = 10,
        Active = 20
    }

    [Serializable]
    public sealed class TalentEffectDefinition
    {
        [SerializeField, LabelText("效果ID")]
        private string effectId;

        [SerializeField, LabelText("显示名称")]
        private string displayName;

        [SerializeField, TextArea, LabelText("自定义描述")]
        private string customDescription;

        [SerializeField, LabelText("效果类型")]
        private TalentEffectType effectType = TalentEffectType.AddItem;

        [SerializeField, LabelText("触发时机")]
        private TalentEffectTriggerTiming triggerTiming = TalentEffectTriggerTiming.OnTurnEnd;

        [SerializeField, LabelText("数值类型")]
        private TalentEffectValueType valueType = TalentEffectValueType.Fixed;

        [SerializeField, LabelText("物品")]
        [ShowIf(nameof(RequiresItemDefinition))]
        private ItemDefinition itemDefinition;

        [SerializeField, LabelText("建筑蓝图")]
        [ShowIf(nameof(RequiresBuildingPrefab))]
        private BuildingBase buildingPrefab;

        [SerializeField, LabelText("基础数值")]
        private float value = 1f;

        [SerializeField, LabelText("每级成长")]
        private float levelScaling;

        public string EffectId => NormalizeId(effectId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? (string.IsNullOrWhiteSpace(EffectId) ? effectType.ToString() : EffectId)
            : displayName.Trim();
        public string CustomDescription => string.IsNullOrWhiteSpace(customDescription)
            ? string.Empty
            : customDescription.Trim();
        public TalentEffectType EffectType => effectType;
        public TalentEffectTriggerTiming TriggerTiming => triggerTiming;
        public TalentEffectValueType ValueType => valueType;
        public ItemDefinition ItemDefinition => itemDefinition;
        public BuildingBase BuildingPrefab => buildingPrefab;
        public float Value => value;
        public float LevelScaling => levelScaling;
        public bool IsValid => !string.IsNullOrWhiteSpace(DisplayName);

        private bool RequiresItemDefinition => effectType == TalentEffectType.AddItem;
        private bool RequiresBuildingPrefab => effectType == TalentEffectType.UnlockBuildingBlueprint;

        public void Normalize()
        {
            effectId = NormalizeId(effectId);
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            customDescription = string.IsNullOrWhiteSpace(customDescription) ? string.Empty : customDescription.Trim();
        }

        public float CalculateRawValue(GameSystem context, TalentState talent)
        {
            var level = Mathf.Max(1, talent == null ? 1 : talent.Level);
            var scaledValue = value + Mathf.Max(0, level - 1) * levelScaling;
            return valueType switch
            {
                TalentEffectValueType.PerTalentLevel => value * level + Mathf.Max(0, level - 1) * levelScaling,
                TalentEffectValueType.BasedOnPopulation => scaledValue * Mathf.Max(0, context == null ? 0 : context.Population),
                TalentEffectValueType.BasedOnBuildingCount => scaledValue * GetBuildingCount(context),
                _ => scaledValue
            };
        }

        public int CalculateAmount(GameSystem context, TalentState talent)
        {
            var rawValue = CalculateRawValue(context, talent);
            if (effectType == TalentEffectType.AddItem
                && valueType == TalentEffectValueType.PercentOfCurrentStorage
                && context != null
                && context.Inventory != null
                && itemDefinition != null
                && !string.IsNullOrWhiteSpace(itemDefinition.ItemId))
            {
                var currentAmount = context.Inventory.GetQuantity(itemDefinition.ItemId);
                var amount = currentAmount * rawValue;
                return amount > 0f && amount < 1f ? 1 : Mathf.FloorToInt(amount);
            }

            return rawValue > 0f && rawValue < 1f ? 1 : Mathf.FloorToInt(rawValue);
        }

        public string GetDescription(TalentState talent)
        {
            if (!string.IsNullOrWhiteSpace(CustomDescription))
            {
                return CustomDescription;
            }

            var level = Mathf.Max(1, talent == null ? 1 : talent.Level);
            var scaledValue = value + Mathf.Max(0, level - 1) * levelScaling;
            var targetName = itemDefinition == null ? string.Empty : itemDefinition.DisplayName;
            if (effectType == TalentEffectType.UnlockBuildingBlueprint
                && buildingPrefab != null
                && buildingPrefab.HasDefinition)
            {
                targetName = buildingPrefab.Definition.DisplayName;
            }

            return effectType switch
            {
                TalentEffectType.AddItem when valueType == TalentEffectValueType.PercentOfCurrentStorage =>
                    $"{DisplayName}：每回合获得当前{targetName}库存 {FormatPercent(scaledValue)}",
                TalentEffectType.AddItem =>
                    $"{DisplayName}：每回合获得 {targetName}+{Mathf.FloorToInt(scaledValue)}",
                TalentEffectType.AddResearchPoints =>
                    $"{DisplayName}：每回合研究点+{Mathf.FloorToInt(scaledValue)}",
                TalentEffectType.UnlockBuildingBlueprint =>
                    $"{DisplayName}：解锁蓝图 {targetName}",
                TalentEffectType.ModifyResearchSpeed =>
                    $"{DisplayName}：研究速度 {FormatSignedPercent(scaledValue)}",
                TalentEffectType.ModifyResourceGainPercent =>
                    $"{DisplayName}：资源收益 {FormatSignedPercent(scaledValue)}",
                TalentEffectType.ModifyCostPercent =>
                    $"{DisplayName}：消耗 {FormatSignedPercent(scaledValue)}",
                TalentEffectType.ModifyBuildSpeed =>
                    $"{DisplayName}：建造速度 {FormatSignedPercent(scaledValue)}",
                TalentEffectType.ModifyMilitaryPower =>
                    $"{DisplayName}：军事能力 {FormatSignedValue(scaledValue)}",
                TalentEffectType.ModifyHappiness =>
                    $"{DisplayName}：满意度 {FormatSignedValue(scaledValue)}",
                TalentEffectType.ModifyRisk =>
                    $"{DisplayName}：风险 {FormatSignedPercent(scaledValue)}",
                TalentEffectType.ModifyMaxLifespan =>
                    $"{DisplayName}：最大寿命 {FormatSignedValue(scaledValue)} 回合",
                TalentEffectType.LifetimeWarning =>
                    $"{DisplayName}：寿命终点前 {Mathf.FloorToInt(scaledValue)} 回合预警",
                _ => $"{DisplayName}：{scaledValue:0.##}"
            };
        }

        public static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static int GetBuildingCount(GameSystem context)
        {
            return context == null || context.Buildings == null || context.Buildings.Buildings == null
                ? 0
                : context.Buildings.Buildings.Count;
        }

        private static string FormatPercent(float value)
        {
            return $"{value * 100f:0.#}%";
        }

        private static string FormatSignedPercent(float value)
        {
            return $"{(value >= 0f ? "+" : string.Empty)}{value * 100f:0.#}%";
        }

        private static string FormatSignedValue(float value)
        {
            return $"{(value >= 0f ? "+" : string.Empty)}{value:0.##}";
        }
    }

    [Serializable]
    public sealed class TalentHiddenTraitDefinition
    {
        [SerializeField, LabelText("特性ID")]
        private string traitId;

        [SerializeField, LabelText("显示名称")]
        private string displayName;

        [SerializeField, TextArea, LabelText("未发现描述")]
        private string undiscoveredDescription = "未知特性";

        [SerializeField, TextArea, LabelText("已发现描述")]
        private string discoveredDescription;

        [SerializeField, TextArea, LabelText("已激活描述")]
        private string activeDescription;

        [SerializeField, LabelText("发现等级"), Min(0)]
        private int minDiscoverLevel;

        [SerializeField, LabelText("激活等级"), Min(0)]
        private int minActivateLevel;

        [SerializeField, LabelText("发现所需任命回合"), Min(0)]
        private int assignedTurnsToDiscover;

        [SerializeField, LabelText("激活所需任命回合"), Min(0)]
        private int assignedTurnsToActivate;

        [SerializeReference, LabelText("发现条件")]
        private GameCondition discoveryCondition;

        [SerializeReference, LabelText("激活条件")]
        private GameCondition activationCondition;

        [SerializeField, LabelText("激活效果")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        private TalentEffectDefinition[] effects = Array.Empty<TalentEffectDefinition>();

        public string TraitId => TalentEffectDefinition.NormalizeId(traitId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? (string.IsNullOrWhiteSpace(TraitId) ? "隐藏特性" : TraitId)
            : displayName.Trim();
        public string UndiscoveredDescription => string.IsNullOrWhiteSpace(undiscoveredDescription)
            ? "未知特性"
            : undiscoveredDescription.Trim();
        public string DiscoveredDescription => string.IsNullOrWhiteSpace(discoveredDescription)
            ? DisplayName
            : discoveredDescription.Trim();
        public string ActiveDescription => string.IsNullOrWhiteSpace(activeDescription)
            ? DiscoveredDescription
            : activeDescription.Trim();
        public int MinDiscoverLevel => Mathf.Max(0, minDiscoverLevel);
        public int MinActivateLevel => Mathf.Max(0, minActivateLevel);
        public int AssignedTurnsToDiscover => Mathf.Max(0, assignedTurnsToDiscover);
        public int AssignedTurnsToActivate => Mathf.Max(0, assignedTurnsToActivate);
        public GameCondition DiscoveryCondition => discoveryCondition;
        public GameCondition ActivationCondition => activationCondition;
        public IReadOnlyList<TalentEffectDefinition> Effects => effects ?? Array.Empty<TalentEffectDefinition>();
        public bool IsValid => !string.IsNullOrWhiteSpace(TraitId);

        public void Normalize()
        {
            traitId = TalentEffectDefinition.NormalizeId(traitId);
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            undiscoveredDescription = string.IsNullOrWhiteSpace(undiscoveredDescription)
                ? "未知特性"
                : undiscoveredDescription.Trim();
            discoveredDescription = string.IsNullOrWhiteSpace(discoveredDescription)
                ? string.Empty
                : discoveredDescription.Trim();
            activeDescription = string.IsNullOrWhiteSpace(activeDescription) ? string.Empty : activeDescription.Trim();
            minDiscoverLevel = Mathf.Max(0, minDiscoverLevel);
            minActivateLevel = Mathf.Max(0, minActivateLevel);
            assignedTurnsToDiscover = Mathf.Max(0, assignedTurnsToDiscover);
            assignedTurnsToActivate = Mathf.Max(0, assignedTurnsToActivate);
            effects ??= Array.Empty<TalentEffectDefinition>();
            for (var i = 0; i < effects.Length; i++)
            {
                effects[i]?.Normalize();
            }
        }

        public bool CanDiscover(GameSystem context, TalentState talent)
        {
            return MeetsTalentRequirements(talent, MinDiscoverLevel, AssignedTurnsToDiscover)
                   && (discoveryCondition == null || discoveryCondition.IsMet(context));
        }

        public bool CanActivate(GameSystem context, TalentState talent)
        {
            return MeetsTalentRequirements(talent, MinActivateLevel, AssignedTurnsToActivate)
                   && (activationCondition == null || activationCondition.IsMet(context));
        }

        private static bool MeetsTalentRequirements(TalentState talent, int minLevel, int assignedTurns)
        {
            if (talent == null)
            {
                return false;
            }

            return talent.Level >= Mathf.Max(0, minLevel)
                   && talent.AssignedTurns >= Mathf.Max(0, assignedTurns);
        }
    }

    [Serializable]
    public sealed class TalentSlotDefinition
    {
        [SerializeField, LabelText("槽位ID")]
        private string slotId;

        [SerializeField, LabelText("显示名称")]
        private string displayName;

        [SerializeField, LabelText("限定职业")]
        [PropertyTooltip("None 表示通用槽，任何职业都可任命。")]
        private TalentProfession acceptedProfession = TalentProfession.None;

        [SerializeField, LabelText("排序"), Min(0)]
        private int sortOrder;

        public string SlotId => TalentEffectDefinition.NormalizeId(slotId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? (acceptedProfession == TalentProfession.None ? "通用人才槽" : $"{acceptedProfession}专属槽")
            : displayName.Trim();
        public TalentProfession AcceptedProfession => acceptedProfession;
        public int SortOrder => Mathf.Max(0, sortOrder);
        public bool IsValid => !string.IsNullOrWhiteSpace(SlotId);

        public bool Accepts(TalentDefinition talent)
        {
            return talent != null
                   && talent.IsValid
                   && (acceptedProfession == TalentProfession.None || talent.Profession == acceptedProfession);
        }

        public void Normalize()
        {
            slotId = TalentEffectDefinition.NormalizeId(slotId);
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            sortOrder = Mathf.Max(0, sortOrder);
        }

        internal static TalentSlotDefinition CreateDefaultGeneral(int index)
        {
            return new TalentSlotDefinition
            {
                slotId = $"general_{Mathf.Max(1, index)}",
                displayName = $"通用人才槽 {Mathf.Max(1, index)}",
                acceptedProfession = TalentProfession.None,
                sortOrder = Mathf.Max(0, index)
            };
        }
    }

    [CreateAssetMenu(menuName = "Landsong/Talent/Talent Definition", fileName = "TalentDefinition")]
    public sealed class TalentDefinition : ScriptableObject
    {
        [TitleGroup("基础信息")]
        [SerializeField, LabelText("人才ID")]
        private string talentId;

        [SerializeField, LabelText("显示名称")]
        private string displayName;

        [SerializeField, TextArea, LabelText("描述")]
        private string description;

        [SerializeField, LabelText("职业")]
        private TalentProfession profession = TalentProfession.大学者;

        [SerializeField, LabelText("稀有度")]
        private TalentRarity rarity = TalentRarity.Common;

        [SerializeField, PreviewField(72), LabelText("头像")]
        private Sprite icon;

        [TitleGroup("卡片UI")]
        [SerializeField, LabelText("卡片主色")]
        private Color cardMainColor = Color.white;

        [SerializeField, PreviewField(48), LabelText("职业图标")]
        private Sprite professionIcon;

        [SerializeField, LabelText("稀有度颜色")]
        private Color rarityColor = Color.white;

        [TitleGroup("成长")]
        [SerializeField, LabelText("初始等级"), Min(1)]
        private int startingLevel = 1;

        [SerializeField, LabelText("最高等级"), Min(1)]
        private int maxLevel = 5;

        [SerializeField, LabelText("升级所需经验"), Min(0)]
        private int experienceToNextLevel = 100;

        [SerializeField, LabelText("每级额外经验"), Min(0)]
        private int experienceGrowthPerLevel = 25;

        [TitleGroup("费用与刷新")]
        [SerializeField, LabelText("每回合薪资"), Min(0)]
        private int salaryGoldPerTurn = 1;

        [SerializeField, LabelText("每级薪资成长"), Min(0)]
        private int salaryGrowthPerLevel;

        [SerializeField, LabelText("刷新权重"), Min(0f)]
        private float refreshWeight = 1f;

        [SerializeField, LabelText("唯一人才")]
        private bool uniqueTalent;

        [SerializeReference, LabelText("刷新条件")]
        private GameCondition refreshCondition;

        [TitleGroup("效果")]
        [SerializeField, LabelText("普通效果")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        private TalentEffectDefinition[] effects = Array.Empty<TalentEffectDefinition>();

        [SerializeField, LabelText("隐藏特性")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        private TalentHiddenTraitDefinition[] hiddenTraits = Array.Empty<TalentHiddenTraitDefinition>();

        public string TalentId => TalentEffectDefinition.NormalizeId(talentId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? (string.IsNullOrWhiteSpace(TalentId) ? name : TalentId)
            : displayName.Trim();
        public string Description => string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        public TalentProfession Profession => profession;
        public TalentRarity Rarity => rarity;
        public Sprite Icon => icon;
        public Color CardMainColor => cardMainColor;
        public Sprite ProfessionIcon => professionIcon;
        public Color RarityColor => rarityColor;
        public int StartingLevel => Mathf.Clamp(startingLevel, 1, MaxLevel);
        public int MaxLevel => Mathf.Max(1, maxLevel);
        public int ExperienceToNextLevel => Mathf.Max(0, experienceToNextLevel);
        public int ExperienceGrowthPerLevel => Mathf.Max(0, experienceGrowthPerLevel);
        public int SalaryGoldPerTurn => Mathf.Max(0, salaryGoldPerTurn);
        public int SalaryGrowthPerLevel => Mathf.Max(0, salaryGrowthPerLevel);
        public float RefreshWeight => Mathf.Max(0f, refreshWeight);
        public bool UniqueTalent => uniqueTalent;
        public GameCondition RefreshCondition => refreshCondition;
        public IReadOnlyList<TalentEffectDefinition> Effects => effects ?? Array.Empty<TalentEffectDefinition>();
        public IReadOnlyList<TalentHiddenTraitDefinition> HiddenTraits => hiddenTraits ?? Array.Empty<TalentHiddenTraitDefinition>();
        public bool IsValid => !string.IsNullOrWhiteSpace(TalentId) && profession != TalentProfession.None;

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
            talentId = TalentEffectDefinition.NormalizeId(talentId);
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
            startingLevel = Mathf.Max(1, startingLevel);
            maxLevel = Mathf.Max(1, maxLevel);
            if (startingLevel > maxLevel)
            {
                startingLevel = maxLevel;
            }

            experienceToNextLevel = Mathf.Max(0, experienceToNextLevel);
            experienceGrowthPerLevel = Mathf.Max(0, experienceGrowthPerLevel);
            salaryGoldPerTurn = Mathf.Max(0, salaryGoldPerTurn);
            salaryGrowthPerLevel = Mathf.Max(0, salaryGrowthPerLevel);
            refreshWeight = Mathf.Max(0f, refreshWeight);

            effects ??= Array.Empty<TalentEffectDefinition>();
            for (var i = 0; i < effects.Length; i++)
            {
                effects[i]?.Normalize();
            }

            hiddenTraits ??= Array.Empty<TalentHiddenTraitDefinition>();
            for (var i = 0; i < hiddenTraits.Length; i++)
            {
                hiddenTraits[i]?.Normalize();
            }
        }

        public bool CanAppear(GameSystem context)
        {
            return IsValid
                   && RefreshWeight > 0f
                   && (refreshCondition == null || refreshCondition.IsMet(context));
        }

        public int CalculateSalaryGoldPerTurn(int level)
        {
            return SalaryGoldPerTurn + Mathf.Max(0, level - 1) * SalaryGrowthPerLevel;
        }

        public int GetExperienceRequiredForLevel(int level)
        {
            if (level >= MaxLevel)
            {
                return 0;
            }

            return ExperienceToNextLevel + Mathf.Max(0, level - StartingLevel) * ExperienceGrowthPerLevel;
        }
    }

    [CreateAssetMenu(menuName = "Landsong/Talent/Talent Catalog", fileName = "TalentCatalog")]
    public sealed class TalentCatalog : ScriptableObject
    {
        [SerializeField, LabelText("人才定义")]
        private TalentDefinition[] talents = Array.Empty<TalentDefinition>();

        private Dictionary<string, TalentDefinition> talentsById;

        public IReadOnlyList<TalentDefinition> Talents => talents ?? Array.Empty<TalentDefinition>();

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

        public bool TryGetDefinition(string talentId, out TalentDefinition talent)
        {
            talentId = TalentEffectDefinition.NormalizeId(talentId);
            if (string.IsNullOrWhiteSpace(talentId))
            {
                talent = null;
                return false;
            }

            EnsureIndex();
            return talentsById.TryGetValue(talentId, out talent);
        }

        public void GetAvailableDefinitions(GameSystem context, ISet<string> excludedTalentIds, List<TalentDefinition> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            var source = Talents;
            for (var i = 0; i < source.Count; i++)
            {
                var talent = source[i];
                if (talent == null || !talent.CanAppear(context))
                {
                    continue;
                }

                if (excludedTalentIds != null && excludedTalentIds.Contains(talent.TalentId))
                {
                    continue;
                }

                results.Add(talent);
            }
        }

        public void RebuildIndex()
        {
            talentsById = new Dictionary<string, TalentDefinition>(StringComparer.Ordinal);
            var source = Talents;
            for (var i = 0; i < source.Count; i++)
            {
                var talent = source[i];
                if (talent == null || !talent.IsValid)
                {
                    continue;
                }

                if (talentsById.ContainsKey(talent.TalentId))
                {
                    Debug.LogWarning($"人才 ID 重复，已忽略后续配置：{talent.TalentId}", this);
                    continue;
                }

                talentsById.Add(talent.TalentId, talent);
            }
        }

        private void EnsureIndex()
        {
            if (talentsById == null)
            {
                RebuildIndex();
            }
        }

        private void Normalize()
        {
            talents ??= Array.Empty<TalentDefinition>();
            for (var i = 0; i < talents.Length; i++)
            {
                talents[i]?.Normalize();
            }
        }
    }
}
