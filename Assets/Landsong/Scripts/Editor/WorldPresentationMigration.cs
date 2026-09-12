#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Presentation;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    // Run once after the Game panel migration. Discovery and component authoring only occur here.
    public static class WorldPresentationMigration
    {
        public const string WorldRootPath = "Assets/Landsong/ECSContent/Presentation/WorldPresentationRoot.prefab";

        [MenuItem("Landsong/ECS/Presentation/Migrate explicit world bindings")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后迁移世界表现。");
            var catalog = AssetDatabase.LoadAssetAtPath<GamePresentationCatalog>(LanguageContentTools.Path);
            if (catalog == null) throw new InvalidOperationException("音画目录缺失：" + LanguageContentTools.Path);
            var actorPaths = new Dictionary<string, PresentationActor>();
            foreach (var model in catalog.Models)
            {
                if (model == null) throw new InvalidOperationException("音画目录包含空模型条目。");
                var source = model.ActorPrefab != null ? model.ActorPrefab.gameObject : model.LegacyPrefabForMigration;
                if (source == null) throw new InvalidOperationException("模型模板缺失：" + model.Definition);
                string path = AssetDatabase.GetAssetPath(source);
                if (!actorPaths.TryGetValue(path, out var actor)) actorPaths.Add(path, actor = AuthorActor(path));
                model.CompleteModelMigration(actor);
            }
            var effectPaths = new Dictionary<string, PresentationEffect>();
            foreach (var cue in catalog.Cues)
            {
                if (cue == null) throw new InvalidOperationException("音画目录包含空提示条目。");
                var source = cue.EffectPrefab != null ? cue.EffectPrefab.gameObject : cue.LegacyEffectForMigration;
                if (source == null) continue;
                string path = AssetDatabase.GetAssetPath(source);
                if (!effectPaths.TryGetValue(path, out var effect)) effectPaths.Add(path, effect = AuthorEffect(path));
                cue.CompleteEffectMigration(effect);
            }
            EditorUtility.SetDirty(catalog);
            var rootTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRootPath);
            if (rootTemplate == null)
            {
                var created = new GameObject("World Presentation Root");
                try { rootTemplate = PrefabUtility.SaveAsPrefabAsset(created, WorldRootPath); }
                finally { UnityEngine.Object.DestroyImmediate(created); }
            }
            var game = PrefabUtility.LoadPrefabContents(ApplicationUiMigration.GamePath);
            try
            {
                var bridge = game.GetComponentsInChildren<WorldPresentationView>(true).Single();
                bridge.WorldRootTemplate = rootTemplate.transform;
                PrefabUtility.SaveAsPrefabAsset(game, ApplicationUiMigration.GamePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(game); }
            AssetDatabase.SaveAssets(); Verify(catalog);
            return "世界表现已绑定：" + actorPaths.Count + " 个模型模板、" + effectPaths.Count + " 个特效模板和显式世界根。";
        }

        static GameObject Open(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("世界表现必须使用可编辑的独立预制体：" + path);
            return PrefabUtility.LoadPrefabContents(path);
        }

        static PresentationActor AuthorActor(string path)
        {
            var prefab = Open(path);
            try
            {
                var actor = prefab.GetComponent<PresentationActor>();
                if (actor == null) actor = prefab.AddComponent<PresentationActor>();
                if (actor.Animator == null)
                {
                    var animators = prefab.GetComponentsInChildren<Animator>(true);
                    if (animators.Length > 1) throw new InvalidOperationException("模型包含多个动画控制器，请显式指定：" + path);
                    actor.Animator = animators.SingleOrDefault();
                }
                actor.Renderers = prefab.GetComponentsInChildren<Renderer>(true);
                actor.WorkingParticles ??= Array.Empty<ParticleSystem>();
                actor.WorkingLights ??= Array.Empty<Light>();
                actor.ValidateConfiguration();
                if (!PurePrefab(prefab)) throw new InvalidOperationException("模型模板包含非表现组件：" + path);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<PresentationActor>();
        }

        static PresentationEffect AuthorEffect(string path)
        {
            var prefab = Open(path);
            try
            {
                var effect = prefab.GetComponent<PresentationEffect>();
                if (effect == null) effect = prefab.AddComponent<PresentationEffect>();
                effect.Particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
                effect.Renderers = prefab.GetComponentsInChildren<Renderer>(true);
                effect.ValidateConfiguration();
                if (!PurePrefab(prefab)) throw new InvalidOperationException("特效模板包含非表现组件：" + path);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<PresentationEffect>();
        }

        public static bool PurePrefab(GameObject prefab)
        {
            if (prefab == null) return false;
            return prefab.GetComponentsInChildren<Component>(true).All(component => component != null &&
                (component is Transform || component is MeshFilter || component is Renderer || component is Animator || component is ParticleSystem || component is Light || component is LODGroup || component is PresentationActor || component is PresentationEffect));
        }

        public static void Verify(GamePresentationCatalog catalog)
        {
            foreach (var model in catalog.Models)
            {
                if (model?.ActorPrefab == null || model.LegacyPrefabForMigration != null || !PurePrefab(model.ActorPrefab.gameObject)) throw new InvalidOperationException("模型未完成显式绑定迁移。");
                var actor = model.ActorPrefab; actor.ValidateConfiguration();
                if (!new HashSet<Renderer>(actor.Renderers).SetEquals(actor.GetComponentsInChildren<Renderer>(true))) throw new InvalidOperationException("模型渲染器数组未覆盖模板：" + actor.name);
                if (actor.Animator == null && actor.GetComponentsInChildren<Animator>(true).Length != 0) throw new InvalidOperationException("模型动画器引用缺失：" + actor.name);
            }
            foreach (var cue in catalog.Cues)
            {
                if (cue.LegacyEffectForMigration != null) throw new InvalidOperationException("特效未完成显式绑定迁移。");
                if (cue.EffectPrefab == null) continue;
                var effect = cue.EffectPrefab; effect.ValidateConfiguration();
                if (!PurePrefab(effect.gameObject) || !new HashSet<ParticleSystem>(effect.Particles).SetEquals(effect.GetComponentsInChildren<ParticleSystem>(true)) || !new HashSet<Renderer>(effect.Renderers).SetEquals(effect.GetComponentsInChildren<Renderer>(true)))
                    throw new InvalidOperationException("特效数组未完整绑定模板：" + effect.name);
            }
        }
    }
}
#endif
