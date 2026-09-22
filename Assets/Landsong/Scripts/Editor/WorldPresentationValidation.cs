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
    public static class WorldPresentationValidation
    {
        public static bool PurePrefab(GameObject prefab)
        {
            if (prefab == null)
                return false;
            return prefab.GetComponentsInChildren<Component>(true).All(component => component != null && (component is Transform || component is MeshFilter || component is Renderer || component is Animator || component is ParticleSystem || component is Light || component is LODGroup || component is PresentationActor || component is PresentationEffect));
        }

        public static void Verify(WorldVisualCatalog visuals, EffectCatalog effects)
        {
            VerifyLegacyModels(visuals);
            foreach (var model in visuals.Models)
            {
                if (model?.ActorPrefab == null || !PurePrefab(model.ActorPrefab.gameObject))
                    throw new InvalidOperationException("模型未完成显式绑定迁移。");
                var actor = model.ActorPrefab;
                actor.ValidateConfiguration();
                if (!new HashSet<Renderer>(actor.Renderers).SetEquals(actor.GetComponentsInChildren<Renderer>(true)))
                    throw new InvalidOperationException("模型渲染器数组未覆盖模板：" + actor.name);
                if (actor.Animator == null && actor.GetComponentsInChildren<Animator>(true).Length != 0)
                    throw new InvalidOperationException("模型动画器引用缺失：" + actor.name);
            }

            foreach (var cue in effects.Cues)
            {
                if (cue.EffectPrefab == null)
                    continue;
                var effect = cue.EffectPrefab;
                effect.ValidateConfiguration();
                if (!PurePrefab(effect.gameObject) || !new HashSet<ParticleSystem>(effect.Particles).SetEquals(effect.GetComponentsInChildren<ParticleSystem>(true)) || !new HashSet<Renderer>(effect.Renderers).SetEquals(effect.GetComponentsInChildren<Renderer>(true)))
                    throw new InvalidOperationException("特效数组未完整绑定模板：" + effect.name);
            }
        }

        public static void VerifyLegacyModels(WorldVisualCatalog visuals)
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal) { "boss", "raider", "titan", "invader" };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (visuals == null || visuals.Models == null)
                throw new InvalidOperationException("缺少遗留世界表现目录。");
            foreach (var model in visuals.Models)
                if (model == null || !allowed.Contains(model.Definition) || !seen.Add(model.Definition)
                    || model.Stage != LifeStage.Operational || model.Level != 1 || !string.IsNullOrEmpty(model.Skin))
                    throw new InvalidOperationException("旧模型映射仅允许 boss、raider、titan、invader 的一级默认外观。新单位须使用定义 → 逻辑 Prefab → View 制作流程。");
        }
    }
}
#endif
