#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Landsong.ECS.Persistence;
using Landsong.ECS.Presentation;
using Moyo.Unity;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class RoyalFoundingVerification
    {
        public static string Run()
        {
            var assertions = 0;
            void Check(bool condition, string message)
            {
                assertions++;
                if (!condition)
                    throw new InvalidOperationException("王室拥立验证失败：" + message);
            }

            var gamePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/UI/Prefabs/GamePanel/UI_GamePanel.prefab");
            var gameView = gamePrefab != null ? gamePrefab.GetComponent<UI_GamePanel>() : null;
            Check(gameView != null && gameView.Court != null
                && gameView.Court.GetComponent<UI_GamePanel_List>() == null
                && gameView.Court.transform.name == "王室面板",
                "王室面板由独立 UI_GamePanel_Royal 控制，不再使用通用列表组件");
            Check(gameView.Court.GraphRoot.IsChildOf(gameView.Court.transform)
                && gameView.Court.GetComponentInChildren<UI_GamePanel_CourtGraph>(true) == null
                && gameView.Court.RoyalDetails.transform.IsChildOf(gameView.Court.transform)
                && gameView.Court.GetComponentInChildren<UIViewBase>(true) == null
                && gameView.Court.AuthoredStyleCards.Length == 2
                && System.Array.TrueForAll(gameView.Court.AuthoredStyleCards,
                    card => card != null && card.transform.IsChildOf(gameView.Court.FamilyLayer)
                        && card.gameObject.tag != "EditorOnly"),
                "家谱和详情直接归属王室面板，不再存在独立展示面板");
            var loadedGame = PrefabUtility.LoadPrefabContents("Assets/Landsong/UI/Prefabs/GamePanel/UI_GamePanel.prefab");
            try
            {
                var loadedRoyal = loadedGame.GetComponent<UI_GamePanel>().Court;
                var portraitCache = loadedGame.GetComponent<PortraitCache>();
                var royalPortraits = loadedRoyal.GetComponentsInChildren<UI_Common_PortraitImageBinding>(true);
                Check(royalPortraits.Length >= 5 && portraitCache != null
                    && System.Array.TrueForAll(royalPortraits,
                        binding => binding.Target != null && binding.Cache == portraitCache && binding.Portraits != null),
                    "展开王室子预制体后，每个家谱模板、样式卡片和人物详情肖像都引用游戏主界面的肖像缓存");
                loadedRoyal.ValidateGraphConfiguration();
            }
            finally { PrefabUtility.UnloadPrefabContents(loadedGame); }
            Check(gameView.RoyalFounding != null
                && gameView.RoyalFounding.transform.IsChildOf(gameView.ModalRoot),
                "拥立弹窗显式归属主界面弹窗层");
            var foundingGroup = gameView.RoyalFounding.GetComponent<CanvasGroup>();
            Check(foundingGroup != null && foundingGroup.interactable && foundingGroup.blocksRaycasts
                && foundingGroup.ignoreParentGroups,
                "主界面遮罩禁用背景交互时拥立弹窗仍可输入和确认");

            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var store = new BlobAssetStore(128);
            using var world = new World("Royal founding verification", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                Check(CourtOps.Monarch(em) == Entity.Null && !FeatureOps.Unlocked(em, root, "Royal"),
                    "开局没有预设君主或王室许可");
                Check(!FeatureOps.Allowed(em, root, CommandKind.RecruitTalent)
                    && !FeatureOps.Allowed(em, root, CommandKind.PrepareMarriage)
                    && !FeatureOps.Allowed(em, root, CommandKind.SelectPolicy),
                    "人才、婚姻和政策命令在拥立前锁定");
                DynastyOps.Settle(em, root);
                Check(CourtOps.State(em, root).LastSettledTurn == 0,
                    "拥立前不推进王室与人才人物的政治结算");
                Check(!RoyalFoundingOps.CanFound(em, root)
                    && GameRequestExecution.Execute(em, root, new FoundRoyalRequest
                    {
                        Name = new FixedString128Bytes("测试君主"), Gender = PersonGender.Female
                    }) == ResultCode.Unavailable, "王宫 1 级不能提前拥立");

                Entity palace = Entity.Null;
                using (var buildings = WorldQueries.OrderedEntities<Building>(em))
                    foreach (var building in buildings)
                        if (em.HasComponent<BuildingHousingStats>(building)
                            && em.GetComponentData<BuildingHousingStats>(building).IsCore != 0)
                        {
                            palace = building;
                            break;
                        }
                Check(palace != Entity.Null, "地图包含核心王宫");
                var definition = em.GetComponentData<BuildingDefinitionRef>(palace).Definition;
                Check(BuildingDefinitions.Get(em, root, definition).MaximumLevel == 2
                    && BuildingBlueprints.Has(em, root, definition, 2), "王宫 2 级及蓝图已配置");
                var housing = em.GetComponentData<BuildingHousingState>(palace);
                var capacity = em.GetComponentData<BuildingHousingStats>(palace);
                var maintenance = em.GetComponentData<BuildingMaintenanceState>(palace);
                Check(maintenance.Maintained != 0 && housing.Population >= capacity.MaxPopulation,
                    "开局王宫满足现有建筑经验结算条件");
                var experience = em.GetComponentData<BuildingExperienceState>(palace);
                experience.Experience = 10;
                em.SetComponentData(palace, experience);
                Check(BuildingUpgradeCommands.Check(em, root, palace).Allowed
                    && BuildingUpgradeCommands.Apply(em, root, palace) == ResultCode.Success,
                    "按现有升级规则支付材料并升至 2 级");
                Check(em.GetComponentData<Building>(palace).Level == 2 && RoyalFoundingOps.CanFound(em, root),
                    "王宫 2 级触发待拥立状态");

                var pendingSave = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, pendingSave));
                Check(RoyalFoundingOps.CanFound(em, root), "待取名状态可经存档恢复");
                Check(GameRequestExecution.Execute(em, root, new FoundRoyalRequest
                {
                    Name = new FixedString128Bytes("  "), Gender = PersonGender.Female
                }) == ResultCode.Unavailable && !FeatureOps.Unlocked(em, root, "Royal"),
                    "空白姓名不能解锁王室");
                Check(GameRequestExecution.Execute(em, root, new FoundRoyalRequest
                {
                    Name = new FixedString128Bytes("测试君主"), Gender = PersonGender.Female
                }) == ResultCode.Success, "确认姓名和性别完成拥立");
                var monarch = CourtOps.Monarch(em);
                Check(monarch != Entity.Null && em.GetComponentData<Identity>(monarch).Name == new FixedString128Bytes("测试君主")
                    && em.GetComponentData<Royal>(monarch).Gender == PersonGender.Female
                    && em.GetComponentData<Royal>(monarch).Spouse == 0,
                    "创建玩家女君主且没有预设配偶");
                Check(FeatureOps.Unlocked(em, root, "Royal")
                    && FeatureOps.Allowed(em, root, CommandKind.RecruitTalent)
                    && FeatureOps.Allowed(em, root, CommandKind.PrepareMarriage)
                    && FeatureOps.Allowed(em, root, CommandKind.SelectPolicy)
                    && !RoyalFoundingOps.CanFound(em, root), "四个玩法同时解锁且弹窗不再出现");
                Check(GameRequestExecution.Execute(em, root, new FoundRoyalRequest
                {
                    Name = new FixedString128Bytes("第二君主"), Gender = PersonGender.Male
                }) == ResultCode.Unavailable, "重复拥立被拒绝");

                var completedSave = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, completedSave));
                monarch = CourtOps.Monarch(em);
                Check(FeatureOps.Unlocked(em, root, "Royal") && monarch != Entity.Null
                    && em.GetComponentData<Identity>(monarch).Name == new FixedString128Bytes("测试君主")
                    && em.GetComponentData<Royal>(monarch).Gender == PersonGender.Female
                    && !RoyalFoundingOps.CanFound(em, root), "拥立结果可经存档恢复");
                return "RoyalFounding Assertions: " + assertions + " passed";
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static void UnlockFixture(EntityManager em, Entity root, bool spouse)
        {
            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            foreach (var building in buildings)
                if (em.HasComponent<BuildingHousingStats>(building)
                    && em.GetComponentData<BuildingHousingStats>(building).IsCore != 0)
                {
                    var state = em.GetComponentData<Building>(building);
                    state.Level = 2;
                    em.SetComponentData(building, state);
                    BuildingLevelConfiguration.Apply(em, root, building, false);
                    break;
                }
            if (RoyalFoundingOps.Found(em, root, new FixedString128Bytes("测试君主"), PersonGender.Male) != ResultCode.Success)
                throw new InvalidOperationException("测试地图无法完成王室拥立。");
            if (spouse)
            {
                var monarch = CourtOps.Monarch(em);
                em.GetBuffer<TraitEntry>(monarch).Add(new TraitEntry
                {
                    Definition = RoyalTraitDefinitions.Find(em, root, new FixedString128Bytes("gene.fate"))
                });
                CourtOps.RefreshTraits(em, root, monarch);
                DynastyOps.CreateRoyal(em, root, "测试配偶", 1, 20);
                CourtOps.Initialize(em, root);
            }
        }
    }
}
#endif
