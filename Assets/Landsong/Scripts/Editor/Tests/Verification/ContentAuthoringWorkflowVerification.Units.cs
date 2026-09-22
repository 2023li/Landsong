#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Landsong.Animation;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.EditorTools;
using Rukhanka;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.ECS.Editor
{
    public static partial class ContentAuthoringWorkflowVerification
    {
        static void VerifyUnits(GameContentSetAsset content, string output, Action<bool, string> check, Action<Action, string> reject)
        {
            var recipe = new UnitCreationInput();
            var template = content.Soldiers.Definitions.First(d => d.Metadata.Id == "militia");
            var original = EditorJsonUtility.ToJson(template);
            var originalLogic = File.ReadAllBytes(AssetDatabase.GetAssetPath(template.Prefab));
            var templateView = template.Prefab.GetComponent<SoldierAnimationAuthoring>().VisualPrefab.GetComponent<SoldierAnimationVisualAuthoring>();
            var templateController = (AnimatorController)templateView.Animator.runtimeAnimatorController;
            AnimationClip Clip(string name) => templateController.animationClips.First(clip => clip.name == name);
            {
                recipe.DefinitionTemplate = template;
                recipe.ModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierAnimationSetup.SourceModel);
                recipe.AnimatedShader = AssetDatabase.LoadAssetAtPath<Shader>(ContentAssetPaths.AnimatedUnitShader);
                recipe.Clips = new UnitAnimationClips { Idle = Clip("Idle"), Walk = Clip("Walk"), Run = Clip("Run"), Attack = Clip("MeleeAttack"), Hit = Clip("Hit"), Death = Clip("Death"), Celebrate = Clip("Victory") };
                recipe.Torch = Clip("TorchWalk");
                recipe.Draw = Clip("DrawSword");
                recipe.Sheathe = Clip("SheatheSword");
                recipe.SwordPrefab = templateView.SwordMount.GetChild(0).gameObject;
                recipe.TorchPrefab = templateView.TorchMount.gameObject;
                SoldierDefinitionAsset sword = null, basic = null;
                foreach (var profile in new[] { UnitAnimationProfile.SwordAndTorch, UnitAnimationProfile.Basic })
                {
                    recipe.Profile = profile;
                    recipe.StableId = profile == UnitAnimationProfile.Basic ? "verification.basic" : "verification.sword";
                    recipe.DisplayName = "制作流程验证 " + profile;
                    var unit = (SoldierDefinitionAsset)UnitAuthoringWorkflow.Create(content, recipe, output);
                    if (profile == UnitAnimationProfile.Basic) basic = unit; else sword = unit;
                    var animation = unit.Prefab.GetComponent<SoldierAnimationAuthoring>();
                    var view = animation.VisualPrefab.GetComponent<SoldierAnimationVisualAuthoring>();
                    var controller = (AnimatorController)view.Animator.runtimeAnimatorController;
                    controller.layers[0].stateMachine.defaultState.tag = "HandAuthored";
                    EditorUtility.SetDirty(controller.layers[0].stateMachine.defaultState);
                    AssetDatabase.SaveAssetIfDirty(controller.layers[0].stateMachine.defaultState);
                    string controllerPath = AssetDatabase.GetAssetPath(controller);
                    string controllerGuid = AssetDatabase.AssetPathToGUID(controllerPath);
                    var controllerBytes = File.ReadAllBytes(controllerPath);
                    unit.CombatStats.MaximumHealth = 177;
                    EditorUtility.SetDirty(unit);
                    AssetDatabase.SaveAssetIfDirty(unit);
                    check(UnitAuthoringWorkflow.Create(content, recipe, output) == unit && unit.CombatStats.MaximumHealth == 177
                        && AssetDatabase.AssetPathToGUID(controllerPath) == controllerGuid && File.ReadAllBytes(controllerPath).SequenceEqual(controllerBytes),
                        profile + " repeated creation preserves identity, tuning and controller edits");
                    check(controller.animationClips.All(c => AssetDatabase.GetAssetPath(c).StartsWith(output + "/Units/Soldier/" + recipe.StableId + "/Clips/", StringComparison.Ordinal)), profile + " owns independent action clips");
                    check(animation.Profile == profile && view.Profile == profile, profile + " logic and View share one contract");
                    if (profile == UnitAnimationProfile.Basic)
                        check(view.SwordMount == null && view.TorchMount == null && view.SwordHandSocket == null && controller.layers.Length == 2,
                            "Basic units require no sword, torch, hand attachment or equipment layer");
                    else check(view.SwordMount != null && view.TorchMount != null && controller.layers.Length == 4, "Sword/torch recipe creates its explicit equipment layers and mounts");
                    int attack = Array.FindIndex(controller.parameters, p => p.name == "Attack");
                    controller.RemoveParameter(attack);
                    reject(() => UnitAuthoringWorkflow.Validate(unit), profile + " detects a broken controller parameter contract");
                    controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
                    EditorUtility.SetDirty(controller);
                    AssetDatabase.SaveAssetIfDirty(controller);
                }
                var otherDomains = new ScriptableObject[2];
                var templates = new ScriptableObject[] { content.Heroes.Definitions[0], content.Enemies.Definitions.First(d => d.Metadata.Id == "raider") };
                for (int i = 0; i < templates.Length; i++)
                {
                    recipe.DefinitionTemplate = templates[i];
                    recipe.StableId = i == 0 ? "verification.hero" : "verification.enemy";
                    recipe.DisplayName = recipe.StableId;
                    otherDomains[i] = UnitAuthoringWorkflow.Create(content, recipe, output);
                    check(otherDomains[i].GetType() == templates[i].GetType(), "Unit recipe preserves the " + templates[i].GetType().Name + " domain");
                }
                recipe.DefinitionTemplate = template;
                int registered = content.Soldiers.Definitions.Length;
                recipe.StableId = "verification.failed";
                recipe.Clips.Death = null;
                reject(() => UnitAuthoringWorkflow.Create(content, recipe, output), "Invalid action fails during staged creation");
                check(content.Soldiers.Definitions.Length == registered
                    && !File.Exists(output + "/Definitions/Soldier/verification.failed.asset")
                    && !AssetDatabase.IsValidFolder(output + "/Units/Soldier/verification.failed"),
                    "Failed creation rolls back files and leaves registration unchanged");
                var display = SoldierDisplayCatalogCompiler.Compile(content.Soldiers, output + "/SoldierDisplayCatalog.asset");
                check(display.Entries.Any(d => d.Id == basic.Metadata.Id) && display.Entries.Any(d => d.Id == sword.Metadata.Id), "Both new equipment profiles enter the display compiler");
                var wolf = content.Enemies.Definitions.Single(d => d.Metadata.Id == "wolf");
                var wolfController = (AnimatorController)wolf.Prefab.GetComponent<SoldierAnimationAuthoring>().VisualPrefab.GetComponent<SoldierAnimationVisualAuthoring>().Animator.runtimeAnimatorController;
                AnimationClip CreatureClip(string name) => wolfController.animationClips.Single(c => c.name == name);
                var creatureInput = new UnitCreationInput {
                    StableId = "verification.creature", DisplayName = "动物创建验证", DefinitionTemplate = wolf,
                    Profile = UnitAnimationProfile.GenericCreature,
                    ModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WolfContentAuthoring.RigPath),
                    AnimatedShader = recipe.AnimatedShader,
                    Clips = new UnitAnimationClips { Idle = CreatureClip("Idle"), Walk = CreatureClip("Walk"), Run = CreatureClip("Run"), Attack = CreatureClip("Attack"), Death = CreatureClip("Death") }
                };
                var creature = UnitAuthoringWorkflow.Create(content, creatureInput, output);
                var creatureView = UnitAuthoringWorkflow.Prefab(creature).GetComponent<SoldierAnimationAuthoring>().VisualPrefab;
                check(((AnimatorController)creatureView.GetComponent<SoldierAnimationVisualAuthoring>().Animator.runtimeAnimatorController).layers.Length == 1,
                    "Generic creation uses one controller without equipment, hit or humanoid mask inputs");
                creatureInput.StableId = "verification.wrong-skeleton";
                creatureInput.Clips.Attack = Clip("MeleeAttack");
                reject(() => UnitAuthoringWorkflow.Create(content, creatureInput, output), "Generic creature rejects a humanoid attack and rolls back");
                check(!File.Exists(output + "/Definitions/Enemy/verification.wrong-skeleton.asset"), "Wrong-skeleton creation leaves no definition asset");
                VerifyUnitRuntime(content, new ScriptableObject[] { sword, basic, creature }.Concat(otherDomains).ToArray(), check);
                check(EditorJsonUtility.ToJson(template) == original && File.ReadAllBytes(AssetDatabase.GetAssetPath(template.Prefab)).SequenceEqual(originalLogic),
                    "Creating units in all three domains leaves militia values and logic prefab unchanged");
            }
        }

        static void VerifyUnitRuntime(GameContentSetAsset content, ScriptableObject[] definitions, Action<bool, string> check)
        {
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var store = new BlobAssetStore(128);
            using var world = new World("New unit profiles", WorldFlags.Game);
            try
            {
                foreach (var rootObject in scene.GetRootGameObjects())
                    foreach (var authoring in rootObject.GetComponentsInChildren<GameContentSetAuthoring>(true)) authoring.Content = content;
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var control = em.GetComponentData<SimulationControl>(root);
                control.Paused = 0;
                em.SetComponentData(root, control);
                foreach (var definition in definitions)
                {
                    string stableId = UnitAuthoringWorkflow.Metadata(definition).Id;
                    var position = new float3(0, .5f, 0);
                    var unit = definition switch
                    {
                        SoldierDefinitionAsset _ => SoldierEntities.Spawn(em, root, SoldierDefinitions.Find(em, root, stableId), position, false),
                        HeroDefinitionAsset _ => HeroEntities.Spawn(em, root, HeroDefinitions.Find(em, root, stableId), position, false),
                        EnemyDefinitionAsset _ => EnemyEntities.Spawn(em, root, EnemyDefinitions.Find(em, root, stableId), position, false),
                        _ => throw new InvalidOperationException("Unexpected unit domain")
                    };
                    void Configure(bool deployed)
                    {
                        switch (definition)
                        {
                            case SoldierDefinitionAsset _: SoldierCombatants.Configure(em, root, unit, deployed, 0, position); break;
                            case HeroDefinitionAsset _: HeroCombatants.Configure(em, root, unit, deployed, 0, position); break;
                            case EnemyDefinitionAsset _: EnemyCombatants.Configure(em, root, unit, deployed, 0, position); break;
                        }
                    }
                    Configure(false);
                    SoldierAnimationSystem.UpdateUnit(em, unit, .1f, false);
                    check(em.GetComponentData<SoldierAnimationState>(unit).View == Entity.Null, stableId + " stays lightweight before deployment");
                    Configure(true);
                    SoldierAnimationSystem.UpdateUnit(em, unit, .1f, false);
                    var view = em.GetComponentData<SoldierAnimationState>(unit).View;
                    var binding = em.GetComponentData<SoldierAnimationBinding>(view);
                    check(em.Exists(view) && em.HasBuffer<AnimatorControllerParameterComponent>(binding.Rig), stableId + " deploys its baked rig");
                    SoldierAnimationSystem.UpdateUnit(em, unit, .1f, false);
                    check(em.GetComponentData<SoldierAnimationState>(unit).View == view, stableId + " reuses one view while deployed");
                    check(em.HasComponent<RigDefinitionComponent>(binding.Rig) && em.HasComponent<GPUAnimationEngineTag>(binding.Rig), stableId + " bakes a complete skinning rig, not just controller parameters");
                    if (UnitAuthoringWorkflow.Prefab(definition).GetComponent<SoldierAnimationAuthoring>().Profile != UnitAnimationProfile.SwordAndTorch)
                    {
                        var signal = em.GetComponentData<UnitAnimationSignals>(unit);
                        signal.AttackSequence++;
                        em.SetComponentData(unit, signal);
                        SoldierAnimationSystem.UpdateUnit(em, unit, .1f, false);
                        var parameters = new AnimatorParametersAspect(em.GetBuffer<AnimatorControllerParameterComponent>(binding.Rig), em.GetComponentData<AnimatorControllerParameterIndexTableComponent>(binding.Rig));
                        check(parameters.GetBoolParameter("Attack"), "Basic profile attacks without a sword draw transition");
                    }
                    var actor = em.GetComponentData<Combatant>(unit);
                    actor.Deployed = 0;
                    em.SetComponentData(unit, actor);
                    SoldierAnimationSystem.UpdateUnit(em, unit, .1f, false);
                    check(!em.Exists(view), stableId + " releases its view on return");
                    actor.Deployed = 1;
                    em.SetComponentData(unit, actor);
                    SoldierAnimationSystem.UpdateUnit(em, unit, .1f, false);
                    view = em.GetComponentData<SoldierAnimationState>(unit).View;
                    check(em.Exists(view), stableId + " recreates its view on redeployment");
                    var health = em.GetComponentData<Health>(unit);
                    health.Current = 0;
                    em.SetComponentData(unit, health);
                    SoldierAnimationSystem.UpdateUnit(em, unit, .1f, false);
                    check(em.Exists(view), stableId + " retains the deployed death pose");
                    SoldierAnimationSystem.UpdateUnit(em, unit, 100, false);
                    SoldierAnimationSystem.UpdateUnit(em, unit, .1f, false);
                    check(!em.Exists(view), stableId + " releases the finished death view");
                    em.DestroyEntity(unit);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
