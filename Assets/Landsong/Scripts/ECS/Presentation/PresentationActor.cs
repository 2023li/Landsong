using System.Collections.Generic;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public enum ActorPose { Idle, Moving, Working, Construction, Repairing, Stopped, Ruined, Dead, Celebration, Guard, Rescue }
    [DisallowMultipleComponent]
    public sealed class PresentationActor : MonoBehaviour
    {
        [Tooltip("Optional Animator parameters: Pose(int), Moving(bool), Speed(float); triggers Hit, Harvest, Bell, HeroWake.")] public Animator Animator;
        public Transform MovingPart;public Vector3 RotationPerSecond;public float BobHeight=.05f;
        public ParticleSystem[] WorkingParticles=new ParticleSystem[0];public Light[] WorkingLights=new Light[0];
        readonly Dictionary<string,AnimatorControllerParameterType> parameters=new Dictionary<string,AnimatorControllerParameterType>();Vector3 initial;float clock;bool ready;public ActorPose Pose {get;private set;}
        bool Parameter(string name,AnimatorControllerParameterType type)=>parameters.TryGetValue(name,out var found)&&found==type;
        void Ensure(){if(ready)return;ready=true;if(Animator==null)Animator=GetComponentInChildren<Animator>();if(Animator!=null&&Animator.runtimeAnimatorController!=null)foreach(var p in Animator.parameters)parameters[p.name]=p.type;if(MovingPart!=null)initial=MovingPart.localPosition;foreach(var script in GetComponentsInChildren<AudioSource>(true)){script.playOnAwake=false;script.Stop();script.enabled=false;}}
        public void Apply(ActorPose pose,float speed,bool paused,bool reduced,float dt)
        {
            Ensure();Pose=pose;if(Animator!=null){Animator.applyRootMotion=false;Animator.speed=paused||reduced?0:1;if(Parameter("Pose",AnimatorControllerParameterType.Int))Animator.SetInteger("Pose",(int)pose);if(Parameter("Moving",AnimatorControllerParameterType.Bool))Animator.SetBool("Moving",pose==ActorPose.Moving);if(Parameter("Speed",AnimatorControllerParameterType.Float))Animator.SetFloat("Speed",speed);}
            bool working=pose==ActorPose.Working||pose==ActorPose.Construction||pose==ActorPose.Repairing;
            foreach(var effect in WorkingParticles){if(effect==null)continue;if(!working||reduced)effect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);else if(paused)effect.Pause(true);else if(!effect.isPlaying)effect.Play(true);}
            foreach(var light in WorkingLights)if(light!=null)light.enabled=working;
            if(!paused&&!reduced){clock+=dt;if(MovingPart!=null){if(working)MovingPart.Rotate(RotationPerSecond*dt,Space.Self);MovingPart.localPosition=initial+Vector3.up*(pose==ActorPose.Moving||pose==ActorPose.Celebration?Mathf.Sin(clock*7)*BobHeight:0);}}
            if(reduced&&MovingPart!=null)MovingPart.localPosition=initial;
        }
        public void Cue(PresentationCue cue){Ensure();string name=cue.ToString();if(Animator!=null&&Parameter(name,AnimatorControllerParameterType.Trigger))Animator.SetTrigger(name);}
    }
}
