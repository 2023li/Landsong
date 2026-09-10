using System;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [Serializable] public sealed class RuleSource
    {
        public RuleKind Kind;
        public string Target, Secondary;
        public int Level, Amount, B, C;
        public float Value, Extra;
        public string Key;
    }
    [Serializable] public sealed class BuildingPolicySource
    {
        public BuildingCategory Category;
        public int MenuOrder, ProviderPriority;
        [Min(0), Tooltip("每驻地每回合招募士兵总上限。0 使用当前驻军槽数；移动、调动和解散不刷新次数。")] public int SoldierRecruitLimit;
        [Min(0), Tooltip("0 使用修复费用规则 C 或基础施工回合数。费用总额按回合均摊，余数先付。")] public int RepairTurns;
        public bool CanMove = true, CanRotate = true;
        [Range(0, 1)] public float MoveMaterialRatio = .3f, MoveExperienceRatio = .3f;
        [Min(.01f)] public float RuinMovementCost = 4;
        public string DefaultSkin;
        public BuildingPolicy Bake() => new BuildingPolicy { Category = Category, MenuOrder = MenuOrder, ProviderPriority = ProviderPriority, RepairTurns = RepairTurns, SoldierRecruitLimit = SoldierRecruitLimit, CanMove = (byte)(CanMove ? 1 : 0), CanRotate = (byte)(CanRotate ? 1 : 0), MoveMaterialRatio = MoveMaterialRatio, MoveExperienceRatio = MoveExperienceRatio, RuinMovementCost = RuinMovementCost };
    }
    [Serializable] public sealed class ContentSource
    {
        public string Id, Name;
        [TextArea] public string Description;
        public ContentKind Kind;
        public string Group;
        public int Level = 1, Capacity = 1, Duration = 1, Value, Limit;
        public Vector2Int Size = Vector2Int.one;
        public float Health = 100, Damage = 10, Range = 1.4f, Interval = 1, Speed = 2, ProjectileSpeed = 10, Chance = 1, Loss;
        public int Population = 1, Cost, Flags;
        public BuildingCategory TargetCategory;
        [Range(0, 3)] public int QuestIntensity;
        [Min(0)] public float QuestWeight = 100;
        [Min(.01f), Tooltip("任务物品要求、物品奖励和物品惩罚在 Baking 时统一乘此倍率；填写未缩放的基础数量。")] public float ItemQuantityScale = 1;
        public GameObject Prefab;
        public Sprite Icon;
        [Tooltip("仅控制科技树的编辑/显示位置，不参与模拟或存档签名。")] public bool HasTechnologyPosition;
        public Vector2 TechnologyPosition;
        public BuildingPolicySource Building = new BuildingPolicySource();
        public SoldierGrowth SoldierGrowth = SoldierGrowth.Default;
        public HeroGrowth HeroGrowth = HeroGrowth.Default;
        public CombatProfile Combat = CombatProfile.Default;
        public OpportunityProfile Opportunity = OpportunityProfile.Default;
        public TheftProfile Theft = TheftProfile.Default;
        public RuleSource[] Rules = Array.Empty<RuleSource>();
    }
    [CreateAssetMenu(menuName = "Landsong/ECS/Game Catalog")]
    public sealed class GameCatalogAsset : ScriptableObject
    {
        public GameDefinitionAsset[] Definitions = Array.Empty<GameDefinitionAsset>();
        public ContentSource[] Content
        {
            get
            {
                var result = new ContentSource[Definitions.Length];
                for (var i = 0; i < result.Length; i++)
                {
                    if (Definitions[i] == null) throw new InvalidOperationException("Missing ECS definition in catalog at " + i);
                    result[i] = Definitions[i].Data;
                }
                return result;
            }
        }
        public RuleSource[] StartingGrants = Array.Empty<RuleSource>();
        public string GoldId;
        public QuestGenerationSettings QuestGeneration = QuestGenerationSettings.Default;
        public ExpeditionSettings Expeditions = new ExpeditionSettings { PenaltyTurns = 5, AttractionPerStack = 5 };
        public CourtSettings Court = CourtSettings.Default;
        public PortraitConfig Portraits;
        public NightRules Night = NightRules.Default;
        public PeacefulRules Peaceful = PeacefulRules.Default;
        public NightEventSource[] NightEvents = NightEventSource.Defaults();
        public DynastySettings Dynasty = new DynastySettings { MaxChildren = 8, BirthChance = .08f, MutationChance = .03f, TalentCapacity = 8, TalentExperience = 10 };
        public RoyalSource[] RoyalFamily = Array.Empty<RoyalSource>();
        public GameSettings Settings = new GameSettings
        {
            PeacefulSeconds = 15, BattleSeconds = 75, DeployInterval = .4f,
            RetreatSeconds = 5, InvasionChance = .55f, StrengthRatio = .65f, RetryStep = .05f,
            RetryCap = .3f, FirstInvasion = 3, FirstBoss = 20, BossInterval = 10, ThreatPerTurn = 12,
            LowIntel = 1, MediumIntel = 40, HighIntel = 75, MediumIntelLead = 12, HighIntelLead = 24
        };
        public int Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (var i = 0; i < Definitions.Length; i++) if (Definitions[i] != null && Definitions[i].Data.Id == id) return i;
            return -1;
        }
    }
    [Serializable] public sealed class RoyalSource { public string Name; public int Age = 20; public byte Role; public PersonGender Gender; public string[] Traits = Array.Empty<string>(); }
}
