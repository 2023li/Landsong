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

        public static void Verify(EffectCatalog effects)
        {
            if (effects == null || effects.Cues == null)
                throw new InvalidOperationException("缺少世界特效目录。");
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

        public static void VerifyActor(PresentationActor actor)
        {
            if (actor == null || !PurePrefab(actor.gameObject))
                throw new InvalidOperationException("角色 View 必须是纯表现预制体。");
            actor.ValidateConfiguration();
            if (!new HashSet<Renderer>(actor.Renderers).SetEquals(actor.GetComponentsInChildren<Renderer>(true)))
                throw new InvalidOperationException("角色 View 的渲染器数组未覆盖模板：" + actor.name);
            if (actor.Animator == null && actor.GetComponentsInChildren<Animator>(true).Length != 0)
                throw new InvalidOperationException("角色 View 的动画器引用缺失：" + actor.name);
        }

    }
}
#endif
