#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Landsong.Animation;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.ECS.Editor;

namespace Landsong.EditorTools
{
    // Asset-specific recipe inputs; creation itself belongs to UnitAuthoringWorkflow.
    public static class WolfContentAuthoring
    {
        public const string Source = "Assets/polyperfect/Low Poly Animated Animals/";
        public const string RigPath = Source + "Meshes/Animals/Wolf/SKM_Wolf_Rig.fbx";
        public const string ClipsPath = Source + "Meshes/Animals/Wolf/SKM_Wolf_Animations.fbx";
        public const string DefaultDefinitionPath = "Assets/Landsong/ECSContent/Definitions/Enemy/wolf.asset";
        public static EnemyDefinitionAsset Definition()
            => ContentAuthoringContext.Content().Get<EnemyCatalogAsset>().Definitions.SingleOrDefault(item => item != null && item.Metadata.Id == "wolf");
        public static string ConfigureRig()
        {
            ContentCreationAssets.RequireEditMode();
            var wolf = Definition() ?? throw new InvalidOperationException("EnemyCatalog 未注册 wolf。");
            var path = AssetDatabase.GetAssetPath(wolf.Prefab.GetComponent<SoldierAnimationAuthoring>().VisualPrefab);
            var view = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rig = view.GetComponentInChildren<Rukhanka.Hybrid.RigDefinitionAuthoring>();
                rig.rigConfigSource = Rukhanka.Hybrid.RigDefinitionAuthoring.RigConfigSource.UserDefined;
                rig.avatar = null;
                PrefabUtility.SaveAsPrefabAsset(view, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(view); }
            UnitAuthoringWorkflow.Validate(wolf);
            return "Wolf Generic rig reads its model hierarchy; original Animator Avatar retained.";
        }
        public static string Create()
        {
            ContentCreationAssets.RequireEditMode();
            var content = ContentAuthoringContext.Content();
            var wolf = Definition();
            if (wolf != null)
            {
                UnitAuthoringWorkflow.Validate(wolf);
                return "Wolf already exists; existing assets and tuning preserved.";
            }
            using var staging = new ContentCreationScene();
            var model = staging.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            var material = AssetDatabase.LoadAssetAtPath<Material>(Source + "Materials/Animals/Wolf.mat");
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.sharedMaterial = material;
            AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(ClipsPath).OfType<AnimationClip>().Single(c => c.name == "Wolf_" + name);
            var input = new UnitCreationInput {
                StableId = "wolf", DisplayName = "狼", Profile = UnitAnimationProfile.GenericCreature,
                DefinitionTemplate = content.Enemies.Definitions.Single(d => d.Metadata.Id == "raider"),
                ModelPrefab = model, ModelOffset = new Vector3(0, -.5f, 0), ModelScale = 1,
                AnimatedShader = AssetDatabase.LoadAssetAtPath<Shader>(ContentAssetPaths.AnimatedUnitShader),
                SkinBounds = new Bounds(Vector3.zero, Vector3.one * 4),
                Clips = new UnitAnimationClips { Idle = Clip("Idle"), Walk = Clip("Walk"), Run = Clip("Run"), Attack = Clip("Attack"), Death = Clip("Death") }
            };
            wolf = (EnemyDefinitionAsset)UnitAuthoringWorkflow.Create(input);
            wolf.Metadata.Description = "快速近战敌人，持续追击最近的存活出战士兵。";
            wolf.Behavior = EnemyBehaviorFlags.None;
            wolf.ThreatValue = 8;
            var profile = CombatProfile.Default;
            profile.Traits = TacticalTraits.NearestSoldier;
            profile.BodyRadius = .4f;
            wolf.CombatStats = new UnitCombatStatsSource {
                MaximumHealth = 60, Damage = 8, AttackRange = 1.1f, AttackIntervalSeconds = 1.5f,
                MovementSpeed = 3.2f, ProjectileSpeed = 0, Profile = profile
            };
            EditorUtility.SetDirty(wolf);
            AssetDatabase.SaveAssetIfDirty(wolf);
            var catalog = content.Get<NightEventCatalogAsset>();
            var raid = catalog.Events.Single(e => e.Id == "night.raid");
            if (!raid.Enemies.Any(e => e.Enemy == wolf))
            {
                Undo.RecordObject(catalog, "添加普通战斗夜狼敌军");
                raid.Enemies = raid.Enemies.Append(new NightEnemySource { Enemy = wolf, Weight = 1 }).ToArray();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }
            UnitAuthoringWorkflow.Validate(wolf);
            return "Created wolf with one Generic controller; registered in EnemyCatalog and night.raid (weight 1).";
        }
        public static string Inspect()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            var report = new StringBuilder();
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                report.AppendLine($"Animator {animator.name}: avatar={animator.avatar}, valid={animator.avatar?.isValid}, human={animator.avatar?.isHuman}");
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                report.AppendLine($"Skin {skin.name}: bounds={skin.bounds}, local={skin.localBounds}, bones={skin.bones.Length}, materials={string.Join(",", skin.sharedMaterials.Select(m => AssetDatabase.GetAssetPath(m)))}");
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(ClipsPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")))
            {
                var curves = AnimationUtility.GetCurveBindings(clip);
                report.AppendLine($"Clip {clip.name}: length={clip.length}, human={clip.isHumanMotion}, bindings={curves.Length}, missing={string.Join(",", curves.Where(b => b.type == typeof(Transform) && model.transform.Find(b.path) == null).Select(b => b.path).Distinct())}");
                report.AppendLine(string.Join("\n", curves.Take(4).Select(b => b.path + ":" + b.propertyName)));
            }
            return report.ToString();
        }
    }
}
#endif
