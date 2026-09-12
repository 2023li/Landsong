using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Authoring
{
    [Serializable] public sealed class BuildingPolicySource
    {
        [LabelText("建筑分类")] public BuildingCategory Category;
        [LabelText("菜单排序")] public int MenuOrder;
        [LabelText("资源提供优先级")] public int ProviderPriority;
        [LabelText("允许移动")] public bool CanMove = true;
        [LabelText("允许旋转")] public bool CanRotate = true;
        [LabelText("移动材料费用比例"), Range(0, 1)] public float MoveMaterialRatio = .3f;
        [LabelText("移动经验损耗比例"), Range(0, 1)] public float MoveExperienceRatio = .3f;
        [LabelText("废墟通行消耗"), MinValue(.01f)] public float RuinMovementCost = 4;
        [LabelText("默认皮肤标识")] public string DefaultSkin;
        public BuildingPolicy Bake(int repairTurns=0,int soldierRecruitLimit=0) => new BuildingPolicy { Category = Category, MenuOrder = MenuOrder, ProviderPriority = ProviderPriority, RepairTurns = repairTurns, SoldierRecruitLimit = soldierRecruitLimit, CanMove = (byte)(CanMove ? 1 : 0), CanRotate = (byte)(CanRotate ? 1 : 0), MoveMaterialRatio = MoveMaterialRatio, MoveExperienceRatio = MoveExperienceRatio, RuinMovementCost = RuinMovementCost };
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ContentSource
    {
        [LabelText("稳定内容 ID")] public string Id;
        [LabelText("名称")] public string Name;
        [LabelText("说明"), TextArea] public string Description;
        [LabelText("内容类型")] public ContentKind Kind;
        [LabelText("分组 ID")] public string Group;
        [LabelText("@IsBuilding ? \"最高等级\" : Kind == ContentKind.Expedition ? \"最低驻地等级\" : \"等级\"")] public int Level = 1;
        [LabelText("@Kind == ContentKind.Item ? \"最大堆叠数量\" : Kind == ContentKind.Expedition ? \"最多出征人数（0 = 不限）\" : Kind == ContentKind.Crop ? \"满岗奖励所需工人\" : \"容量\""), HideIf(nameof(IsBuilding))] public int Capacity = 1;
        [LabelText("@IsBuilding ? \"施工回合数\" : Kind == ContentKind.Expedition ? \"行程回合数\" : Kind == ContentKind.Crop ? \"成熟回合数\" : \"持续回合数\"")] public int Duration = 1;
        [LabelText("@IsBuilding ? \"资源连接行动力\" : Kind == ContentKind.Expedition ? \"每人抚恤费\" : \"价值\"")] public int Value;
        [LabelText("限建数量（0 = 不限）")] public int Limit;
        [LabelText("占地尺寸")] public Vector2Int Size = Vector2Int.one;
        [LabelText("基础生命 / 耐久")] public float Health = 100;
        [LabelText("基础攻击")] public float Damage = 10;
        [LabelText("@Kind == ContentKind.Expedition ? \"成功率上限\" : \"攻击距离 / 范围\"")] public float Range = 1.4f;
        [LabelText("@Kind == ContentKind.Expedition ? \"每人成功率加成\" : \"攻击间隔（秒）\"")] public float Interval = 1;
        [LabelText("@IsBuilding ? \"格子通行消耗（0 = 阻挡）\" : \"移动速度 / 敏捷\"")] public float Speed = 2;
        [LabelText("弹体速度")] public float ProjectileSpeed = 10;
        [LabelText("@Kind == ContentKind.Expedition ? \"基础成功率\" : \"概率\""), HideIf(nameof(IsBuilding))] public float Chance = 1;
        [LabelText("@Kind == ContentKind.Expedition ? \"失败伤亡比例\" : \"损耗系数\""), HideIf(nameof(IsBuilding))] public float Loss;
        [LabelText("@Kind == ContentKind.Expedition ? \"最少出征人数\" : Kind == ContentKind.Crop ? \"生长所需工人\" : \"占用人口\""), HideIf(nameof(IsBuilding))] public int Population = 1;
        [LabelText("@Kind == ContentKind.Expedition ? \"基础抚恤费\" : Kind == ContentKind.Technology ? \"研究点费用\" : Kind == ContentKind.Policy ? \"民意费用\" : \"基础金币费用\""), HideIf(nameof(IsBuilding))] public int Cost;
        [LabelText("功能标记")] public int Flags;
        [LabelText("优先目标建筑类型"), ShowIf("@Kind == ContentKind.Enemy")] public BuildingCategory TargetCategory;
        [LabelText("任务强度"), Range(0, 3), ShowIf(nameof(IsQuest))] public int QuestIntensity;
        [LabelText("任务抽取权重"), MinValue(0), ShowIf(nameof(IsQuest))] public float QuestWeight = 100;
        [LabelText("任务物品数量倍率"), MinValue(.01f), ShowIf(nameof(IsQuest)), Tooltip("物品要求、奖励和惩罚在 Baking 时统一乘此倍率；填写未缩放的基础数量。")]
        public float ItemQuantityScale = 1;
        [LabelText("预制体")] public GameObject Prefab;
        [LabelText("图标")] public Sprite Icon;
        [LabelText("指定科技树位置"), ShowIf(nameof(IsTechnology)), Tooltip("仅控制科技树的编辑/显示位置，不参与模拟或存档签名。")]
        public bool HasTechnologyPosition;
        [LabelText("科技树位置"), ShowIf(nameof(IsTechnology))] public Vector2 TechnologyPosition;
        [LabelText("移动与外观设置"), ShowIf(nameof(IsBuilding))] public BuildingPolicySource Building = new BuildingPolicySource();
        [LabelText("士兵成长"), ShowIf("@Kind == ContentKind.Soldier")] public SoldierGrowth SoldierGrowth = SoldierGrowth.Default;
        [LabelText("英雄成长"), ShowIf("@Kind == ContentKind.Hero")] public HeroGrowth HeroGrowth = HeroGrowth.Default;
        [LabelText("战斗设置")] public CombatProfile Combat = CombatProfile.Default;
        [LabelText("平安夜访客设置"), ShowIf("@Kind == ContentKind.Opportunity")] public OpportunityProfile Opportunity = OpportunityProfile.Default;
        [LabelText("物品被盗规则"), ShowIf("@Kind == ContentKind.Item")] public TheftProfile Theft = TheftProfile.Default;
        [LabelText("功能配置"), HideIf(nameof(IsBuilding))] public ContentModules Configuration = new ContentModules();
        [LabelText("建筑功能模块"), ShowIf(nameof(IsBuilding)), InfoBox("启用所需模块；适用等级 0 表示全部等级。引用请选择已注册内容资产。")]
        public BuildingModules Modules = new BuildingModules();
        bool IsBuilding => Kind == ContentKind.Building;
        bool IsQuest => Kind == ContentKind.Quest;
        bool IsTechnology => Kind == ContentKind.Technology;
    }
    [CreateAssetMenu(menuName = "Landsong/ECS/Game Catalog")]
    public sealed class GameCatalogAsset : ScriptableObject
    {
        [LabelText("内容定义列表")] public GameDefinitionAsset[] Definitions = Array.Empty<GameDefinitionAsset>();
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
        [LabelText("开局发放")] public RewardsContentModule StartingRewards = new RewardsContentModule();
        [LabelText("金币内容标识")] public string GoldId;
        [LabelText("任务生成设置")] public QuestGenerationSettings QuestGeneration = QuestGenerationSettings.Default;
        [LabelText("远征设置")] public ExpeditionSettings Expeditions = new ExpeditionSettings { PenaltyTurns = 5, AttractionPerStack = 5 };
        [LabelText("宫廷设置")] public CourtSettings Court = CourtSettings.Default;
        [LabelText("肖像配置")] public PortraitConfig Portraits;
        [LabelText("夜晚设置")] public NightRules Night = NightRules.Default;
        [LabelText("平安夜设置")] public PeacefulRules Peaceful = PeacefulRules.Default;
        [LabelText("夜晚事件")] public NightEventSource[] NightEvents = NightEventSource.Defaults();
        [LabelText("王朝设置")] public DynastySettings Dynasty = new DynastySettings { MaxChildren = 8, BirthChance = .08f, MutationChance = .03f, TalentCapacity = 8, TalentExperience = 10 };
        [LabelText("开局王室")] public RoyalSource[] RoyalFamily = Array.Empty<RoyalSource>();
        [LabelText("全局玩法设置")] public GameSettings Settings = new GameSettings
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
    [Serializable] public sealed class RoyalSource { [LabelText("姓名")] public string Name; [LabelText("年龄")] public int Age = 20; [LabelText("王室角色")] public byte Role; [LabelText("性别")] public PersonGender Gender; [LabelText("特性标识")] public string[] Traits = Array.Empty<string>(); }
}
