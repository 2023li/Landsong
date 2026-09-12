using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    [DisallowMultipleComponent]
    public sealed class PresentationEffect : MonoBehaviour
    {
        [LabelText("特效粒子")] public ParticleSystem[] Particles = Array.Empty<ParticleSystem>();
        [LabelText("特效渲染器")] public Renderer[] Renderers = Array.Empty<Renderer>();

        public void ValidateConfiguration()
        {
            if (Particles == null || Renderers == null || Renderers.Length == 0)
                throw new InvalidOperationException(name + " 的特效表现引用未配置。");
            foreach (var value in Particles) if (value == null) throw new InvalidOperationException(name + " 的粒子引用缺失。");
            foreach (var value in Renderers) if (value == null) throw new InvalidOperationException(name + " 的渲染器引用缺失。");
        }

        public void SetPaused(bool paused)
        {
            foreach (var particles in Particles)
            {
                if (paused) particles.Pause(false);
                else if (particles.isPaused) particles.Play(false);
            }
        }
    }
}
