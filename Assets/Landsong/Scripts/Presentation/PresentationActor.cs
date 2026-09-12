using System.Collections.Generic;
using UnityEngine;
using System;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public enum ActorPose { Idle, Moving, Working, Construction, Repairing, Stopped, Ruined, Dead, Celebration, Guard, Rescue }
    [DisallowMultipleComponent]
    public sealed class PresentationActor : MonoBehaviour
    {
        [LabelText("动画控制器"), Tooltip("可选参数：Pose(int)、Moving(bool)、Speed(float)；可选触发器：Hit、Harvest、Bell、HeroWake。")] public Animator Animator;
        [LabelText("运动部件")] public Transform MovingPart;
        [LabelText("每秒旋转角度")] public Vector3 RotationPerSecond;
        [LabelText("上下浮动高度")] public float BobHeight = .05f;
        [LabelText("模型渲染器")] public Renderer[] Renderers = Array.Empty<Renderer>();
        [LabelText("工作粒子")] public ParticleSystem[] WorkingParticles = Array.Empty<ParticleSystem>();
        [LabelText("工作灯光")] public Light[] WorkingLights = Array.Empty<Light>();
        readonly Dictionary<string, AnimatorControllerParameterType> parameters = new Dictionary<string, AnimatorControllerParameterType>();
        Vector3 initial; float clock; bool ready;
        public ActorPose Pose { get; private set; }
        bool Parameter(string name, AnimatorControllerParameterType type) => parameters.TryGetValue(name, out var found) && found == type;
        public void ValidateConfiguration()
        {
            if (Renderers == null || WorkingParticles == null || WorkingLights == null)
                throw new InvalidOperationException(name + " 的表现引用数组未配置。");
            foreach (var value in Renderers) if (value == null) throw new InvalidOperationException(name + " 的渲染器引用缺失。");
            foreach (var value in WorkingParticles) if (value == null) throw new InvalidOperationException(name + " 的工作粒子引用缺失。");
            foreach (var value in WorkingLights) if (value == null) throw new InvalidOperationException(name + " 的工作灯光引用缺失。");
        }
        void Ensure()
        {
            if (ready) return;
            ValidateConfiguration(); ready = true;
            if (Animator != null)
            {
                Animator.applyRootMotion = false;
                if (Animator.runtimeAnimatorController != null)
                    foreach (var parameter in Animator.parameters) parameters[parameter.name] = parameter.type;
            }
            if (MovingPart != null) initial = MovingPart.localPosition;
        }
        public void Apply(ActorPose pose,float speed,bool paused,bool reduced,float dt)
        {
            Ensure();Pose=pose;if(Animator!=null){Animator.applyRootMotion=false;Animator.speed=paused||reduced?0:1;if(Parameter("Pose",AnimatorControllerParameterType.Int))Animator.SetInteger("Pose",(int)pose);if(Parameter("Moving",AnimatorControllerParameterType.Bool))Animator.SetBool("Moving",pose==ActorPose.Moving);if(Parameter("Speed",AnimatorControllerParameterType.Float))Animator.SetFloat("Speed",speed);}
            bool working=pose==ActorPose.Working||pose==ActorPose.Construction||pose==ActorPose.Repairing;
            foreach(var effect in WorkingParticles){if(effect==null)continue;if(!working||reduced)effect.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);else if(paused)effect.Pause(false);else if(!effect.isPlaying)effect.Play(false);}
            foreach(var light in WorkingLights)if(light!=null)light.enabled=working;
            if(!paused&&!reduced){clock+=dt;if(MovingPart!=null){if(working)MovingPart.Rotate(RotationPerSecond*dt,Space.Self);MovingPart.localPosition=initial+Vector3.up*(pose==ActorPose.Moving||pose==ActorPose.Celebration?Mathf.Sin(clock*7)*BobHeight:0);}}
            if(reduced&&MovingPart!=null)MovingPart.localPosition=initial;
        }
        public void Cue(PresentationCue cue){Ensure();string name=cue.ToString();if(Animator!=null&&Parameter(name,AnimatorControllerParameterType.Trigger))Animator.SetTrigger(name);}
    }
}
