#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.Animation;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Editor;
using Rukhanka.Hybrid;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.EditorTools
{
    public static class UnitAuthoringWorkflow
    {
        public static ScriptableObject Create(UnitCreationInput recipe)
            => Create(ContentAuthoringContext.Content(), recipe, BuildingAuthoringWorkflow.Root);

        internal static ScriptableObject Create(GameContentSetAsset content, UnitCreationInput recipe, string output)
        {
            ContentCreationAssets.RequireEditMode();
            if (recipe == null) throw new InvalidOperationException("请先填写单位创建信息。");
            if (recipe.Profile == UnitAnimationProfile.TransportWorker)
                throw new InvalidOperationException("运输工人使用“内容制作 → 创建运输工人”入口，不创建战斗领域定义。");
            ContentCreationAssets.RequireIdentity(recipe.StableId, recipe.DisplayName);
            var template = recipe.DefinitionTemplate;
            var catalog = Catalog(content, template);
            var definitions = Definitions(catalog);
            if (!definitions.Contains(template)) throw new InvalidOperationException("单位模板必须已注册到当前内容集。");
            string domain = template.GetType().Name.Replace("DefinitionAsset", "");
            string definitionPath = output + "/Definitions/" + domain + "/" + recipe.StableId + ".asset";
            string presentation = output + "/Units/" + domain + "/" + recipe.StableId;
            string logicPath = presentation + "/" + recipe.StableId + ".prefab";
            var existing = definitions.FirstOrDefault(d => d != null && Metadata(d).Id == recipe.StableId);
            if (existing != null)
            {
                Validate(existing);
                return existing;
            }
            if (recipe.ModelPrefab == null || recipe.AnimatedShader == null || Prefab(template) == null
                || !Enum.IsDefined(typeof(UnitAnimationProfile), recipe.Profile))
                throw new InvalidOperationException("缺少模型、动画着色器、逻辑模板或有效动作类型。");
            var sourceAnimators = recipe.ModelPrefab.GetComponentsInChildren<Animator>(true);
            bool creature = recipe.Profile == UnitAnimationProfile.GenericCreature;
            if (sourceAnimators.Length != 1 || sourceAnimators[0].avatar == null || !sourceAnimators[0].avatar.isValid || sourceAnimators[0].avatar.isHuman == creature)
                throw new InvalidOperationException("模型必须有唯一 Animator，且有效 Avatar 类型与动作配置一致。");
            if (recipe.Profile == UnitAnimationProfile.SwordAndTorch && (sourceAnimators[0].GetBoneTransform(HumanBodyBones.RightHand) == null
                || sourceAnimators[0].GetBoneTransform(HumanBodyBones.LeftHand) == null))
                throw new InvalidOperationException("模型须保留可访问的 Humanoid 骨骼；请在模型导入设置中关闭 Optimize Game Objects。");
            if (!float.IsFinite(recipe.ModelScale) || recipe.ModelScale <= 0 || recipe.Clips == null)
                throw new InvalidOperationException("模型缩放或动作配置无效。");
            if (recipe.ModelPrefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component == null || !(component is RigDefinitionAuthoring)))
                throw new InvalidOperationException("模型输入须为纯表现资源，不得包含玩法脚本或第二套动画 View 根。");
            if (recipe.Profile == UnitAnimationProfile.SwordAndTorch && (recipe.SwordPrefab == null || recipe.TorchPrefab == null))
                throw new InvalidOperationException("剑与火把类型须填写两种装备模型。");

            using var staging = new ContentCreationScene();
            using var creation = new ContentCreationAssets();
            var definition = Object.Instantiate(template);
            var candidate = Object.Instantiate(content);
            var candidateCatalog = Object.Instantiate(catalog);
            var logic = staging.Instantiate(Prefab(template));
            var view = staging.Create(recipe.StableId + "View");
            try
            {
                Metadata(definition).Id = recipe.StableId;
                Metadata(definition).Name = recipe.DisplayName;
                creation.Create(definition, definitionPath);
                logic.name = recipe.StableId;
                logic.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                logic.transform.localScale = Vector3.one;
                var oldVisual = logic.transform.Find("Visual");
                if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);
                if (logic.GetComponentsInChildren<Renderer>(true).Length != 0 || logic.GetComponentsInChildren<Animator>(true).Length != 0)
                    throw new InvalidOperationException("逻辑模板仍包含模型。先使用独立逻辑/View 模板，再创建单位。");
                var controller = UnitAnimatorControllerBuilder.Create(recipe, creation, presentation);
                creation.Folder(presentation + "/Preview");
                creation.Folder(presentation + "/Materials");
                UnitVisualBuilder.Configure(view, recipe, controller, creation, staging, presentation);
                var animation = logic.GetComponent<SoldierAnimationAuthoring>() ?? logic.AddComponent<SoldierAnimationAuthoring>();
                animation.Profile = recipe.Profile;
                animation.VisualPrefab = creation.Prefab(view, presentation + "/" + recipe.StableId + "View.prefab");
                animation.AttackSeconds = recipe.Clips.Attack.length;
                animation.HitSeconds = recipe.Clips.Hit != null ? recipe.Clips.Hit.length : 0;
                animation.DeathSeconds = Mathf.Max(1.5f, recipe.Clips.Death.length + .5f);
                if (recipe.Profile == UnitAnimationProfile.SwordAndTorch)
                {
                    animation.DrawSeconds = recipe.Draw.length;
                    animation.SheatheSeconds = recipe.Sheathe.length;
                }
                SetPrefab(definition, creation.Prefab(logic, logicPath));
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
                Validate(definition);
                SetCatalog(candidate, candidateCatalog);
                SetDefinitions(candidateCatalog, definitions.Append(definition).ToArray());
                Compile(candidate, definition);
                if (EditorUtility.IsPersistent(catalog)) Undo.RecordObject(catalog, "注册新单位");
                SetDefinitions(catalog, Definitions(candidateCatalog));
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
                creation.Complete();
                return definition;
            }
            catch
            {
                SetDefinitions(catalog, definitions);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(view);
                Object.DestroyImmediate(logic);
                Object.DestroyImmediate(candidateCatalog);
                Object.DestroyImmediate(candidate);
                if (!AssetDatabase.Contains(definition)) Object.DestroyImmediate(definition);
            }
        }

        public static void Validate(ScriptableObject definition)
        {
            var logic = Prefab(definition);
            if (logic == null || logic.GetComponentsInChildren<Renderer>(true).Length != 0 || logic.GetComponentsInChildren<Animator>(true).Length != 0)
                throw new InvalidOperationException(definition.name + "：须引用独立逻辑预制体。");
            var animation = logic.GetComponent<SoldierAnimationAuthoring>();
            if (animation == null || animation.VisualPrefab == null) throw new InvalidOperationException(definition.name + "：逻辑根缺少动画 View 引用。");
            if (!Enum.IsDefined(typeof(UnitAnimationProfile), animation.Profile)) throw new InvalidOperationException(definition.name + "：未知动作类型。");
            var visual = animation.VisualPrefab.GetComponent<SoldierAnimationVisualAuthoring>();
            bool creature = animation.Profile == UnitAnimationProfile.GenericCreature;
            if (visual == null || visual.Profile != animation.Profile || visual.Animator == null || visual.Animator.avatar == null
                || visual.Animator.avatar.isHuman == creature || !visual.Animator.avatar.isValid || visual.Animator.applyRootMotion)
                throw new InvalidOperationException(definition.name + "：View 的动作类型、Avatar 或 Root Motion 配置错误。");
            var rig = visual.Animator.GetComponent<RigDefinitionAuthoring>();
            if (rig == null || rig.applyRootMotion) throw new InvalidOperationException(definition.name + "：缺少关闭 Root Motion 的 Rukhanka Rig。");
            if (creature && (rig.rigConfigSource != RigDefinitionAuthoring.RigConfigSource.UserDefined || rig.avatar != null))
                throw new InvalidOperationException(definition.name + "：Generic Rig 必须从模型 Transform 层级建立骨骼（UserDefined、Avatar 留空）。");
            var controller = visual.Animator.runtimeAnimatorController as AnimatorController;
            if (controller == null || (creature ? visual.CelebrationLayer != 0 || controller.layers.Length != 1
                : visual.CelebrationLayer < 1 || visual.CelebrationLayer >= controller.layers.Length))
                throw new InvalidOperationException(definition.name + "：Controller 或庆祝层不合法。");
            void Parameter(string name, AnimatorControllerParameterType type)
            {
                if (controller.parameters.Count(p => p.name == name && p.type == type) != 1)
                    throw new InvalidOperationException(definition.name + "：缺少或错误的控制器参数 " + name + " / " + type);
            }
            foreach (var name in new[] { "Speed", "LocomotionRate", "AttackSpeed" }) Parameter(name, AnimatorControllerParameterType.Float);
            foreach (var name in new[] { "Alerted", "Dead", "Celebrate" }) Parameter(name, AnimatorControllerParameterType.Bool);
            foreach (var name in new[] { "Attack", "Hit" }) Parameter(name, AnimatorControllerParameterType.Trigger);
            if (animation.Profile == UnitAnimationProfile.SwordAndTorch)
            {
                if (visual.SwordMount == null || visual.SwordHandSocket == null || visual.TorchMount == null
                    || visual.TorchLayer < 1 || visual.TorchLayer >= controller.layers.Length)
                    throw new InvalidOperationException(definition.name + "：剑与火把类型缺少装备挂点或动画层。");
                Parameter("Equipment", AnimatorControllerParameterType.Int);
                Parameter("DrawWeapon", AnimatorControllerParameterType.Trigger);
                Parameter("SheatheWeapon", AnimatorControllerParameterType.Trigger);
            }
            if (controller.animationClips.Any(c => c == null || AnimationUtility.GetAnimationEvents(c).Length != 0))
                throw new InvalidOperationException(definition.name + "：动画事件不得驱动玩法。");
        }

        internal static DefinitionMetadataSource Metadata(ScriptableObject definition) => definition switch
        {
            SoldierDefinitionAsset soldier => soldier.Metadata,
            HeroDefinitionAsset hero => hero.Metadata,
            EnemyDefinitionAsset enemy => enemy.Metadata,
            _ => throw new InvalidOperationException("请选择 Soldier、Hero 或 Enemy 领域定义模板。")
        };
        internal static GameObject Prefab(ScriptableObject definition) => definition switch
        {
            SoldierDefinitionAsset soldier => soldier.Prefab, HeroDefinitionAsset hero => hero.Prefab, EnemyDefinitionAsset enemy => enemy.Prefab,
            _ => throw new InvalidOperationException("请选择单位定义。")
        };
        static void SetPrefab(ScriptableObject definition, GameObject prefab)
        {
            switch (definition)
            {
                case SoldierDefinitionAsset soldier: soldier.Prefab = prefab; break;
                case HeroDefinitionAsset hero: hero.Prefab = prefab; break;
                case EnemyDefinitionAsset enemy: enemy.Prefab = prefab; break;
            }
        }
        static ScriptableObject Catalog(GameContentSetAsset content, ScriptableObject definition) => definition switch
        {
            SoldierDefinitionAsset _ => content.Get<SoldierCatalogAsset>(), HeroDefinitionAsset _ => content.Get<HeroCatalogAsset>(), EnemyDefinitionAsset _ => content.Get<EnemyCatalogAsset>(),
            _ => throw new InvalidOperationException("请选择 Soldier、Hero 或 Enemy 领域定义模板。")
        };
        static ScriptableObject[] Definitions(ScriptableObject catalog) => catalog switch
        {
            SoldierCatalogAsset soldiers => soldiers.Definitions, HeroCatalogAsset heroes => heroes.Definitions, EnemyCatalogAsset enemies => enemies.Definitions,
            _ => throw new InvalidOperationException("缺少单位目录。")
        };
        static void SetDefinitions(ScriptableObject catalog, ScriptableObject[] definitions)
        {
            switch (catalog)
            {
                case SoldierCatalogAsset soldiers: soldiers.Definitions = definitions.Cast<SoldierDefinitionAsset>().ToArray(); break;
                case HeroCatalogAsset heroes: heroes.Definitions = definitions.Cast<HeroDefinitionAsset>().ToArray(); break;
                case EnemyCatalogAsset enemies: enemies.Definitions = definitions.Cast<EnemyDefinitionAsset>().ToArray(); break;
            }
        }
        static void SetCatalog(GameContentSetAsset content, ScriptableObject catalog)
        {
            switch (catalog)
            {
                case SoldierCatalogAsset soldiers: content.Soldiers = soldiers; break;
                case HeroCatalogAsset heroes: content.Heroes = heroes; break;
                case EnemyCatalogAsset enemies: content.Enemies = enemies; break;
            }
        }
        static void Compile(GameContentSetAsset content, ScriptableObject definition)
        {
            switch (definition)
            {
                case SoldierDefinitionAsset _: using (var blob = SoldierCatalogBaking.Compile(content)) { } break;
                case HeroDefinitionAsset _: using (var blob = HeroCatalogBaking.Compile(content)) { } break;
                case EnemyDefinitionAsset _: using (var blob = EnemyCatalogBaking.Compile(content)) { } break;
            }
        }
    }
}
#endif
