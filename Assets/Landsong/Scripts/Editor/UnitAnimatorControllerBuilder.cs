#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Landsong.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Landsong.EditorTools
{
    internal static class UnitAnimatorControllerBuilder
    {
        public static AnimatorController Create(UnitCreationInput recipe, ContentCreationAssets assets, string output)
        {
            bool creature = recipe.Profile == UnitAnimationProfile.GenericCreature;
            AnimationClip Clip(AnimationClip source, string name, bool loop)
            {
                if (source == null || source.isHumanMotion == creature || source.length <= 0)
                    throw new InvalidOperationException(name + " 必须是有效的 " + (creature ? "Generic" : "Humanoid") + " 动作。");
                if (creature)
                {
                    var root = recipe.ModelPrefab.GetComponentInChildren<Animator>(true).transform;
                    var bindings = AnimationUtility.GetCurveBindings(source);
                    if (!Array.Exists(bindings, b => b.type == typeof(Transform))
                        || Array.Exists(bindings, b => b.type == typeof(Transform) && b.path.Length != 0 && root.Find(b.path) == null))
                        throw new InvalidOperationException(name + " 的 Generic 动作骨骼路径与模型不匹配。");
                }
                var copy = UnityEngine.Object.Instantiate(source);
                copy.name = name;
                var settings = AnimationUtility.GetAnimationClipSettings(copy);
                settings.loopTime = settings.loopBlend = loop;
                AnimationUtility.SetAnimationClipSettings(copy, settings);
                AnimationUtility.SetAnimationEvents(copy, Array.Empty<AnimationEvent>());
                assets.Create(copy, output + "/Clips/" + name + ".anim");
                return copy;
            }
            var idleClip = Clip(recipe.Clips.Idle, "Idle", true);
            var walkClip = Clip(recipe.Clips.Walk, "Walk", true);
            var runClip = Clip(recipe.Clips.Run, "Run", true);
            var attackClip = Clip(recipe.Clips.Attack, "Attack", false);
            var hitClip = creature && recipe.Clips.Hit == null ? null : Clip(recipe.Clips.Hit, "Hit", false);
            var deathClip = Clip(recipe.Clips.Death, "Death", false);
            var celebrationClip = creature ? null : Clip(recipe.Clips.Celebrate, "Celebrate", true);
            string path = output + "/Controllers/" + recipe.StableId + ".controller";
            assets.Reserve(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = "LocomotionRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
            controller.AddParameter("Alerted", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Celebrate", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
            var machine = controller.layers[0].stateMachine;
            AnimatorState State(string name, AnimationClip clip)
            {
                var state = machine.AddState(name);
                state.motion = clip;
                return state;
            }
            var idle = State("Idle", idleClip);
            var walk = State("Walk", walkClip);
            var run = State("Run", runClip);
            machine.defaultState = idle;
            walk.speedParameter = run.speedParameter = "LocomotionRate";
            walk.speedParameterActive = run.speedParameterActive = true;
            Transition(idle, walk, (AnimatorConditionMode.Greater, .15f, "Speed"), (AnimatorConditionMode.IfNot, 0, "Alerted"));
            Transition(idle, run, (AnimatorConditionMode.Greater, .15f, "Speed"), (AnimatorConditionMode.If, 0, "Alerted"));
            Transition(walk, idle, (AnimatorConditionMode.Less, .15f, "Speed"));
            Transition(run, idle, (AnimatorConditionMode.Less, .15f, "Speed"));
            Transition(walk, run, (AnimatorConditionMode.If, 0, "Alerted"));
            Transition(run, walk, (AnimatorConditionMode.IfNot, 0, "Alerted"));
            var attack = State("Attack", attackClip);
            attack.speedParameter = "AttackSpeed";
            attack.speedParameterActive = true;
            var death = State("Death", deathClip);
            Trigger(machine, attack, "Attack");
            Exit(attack, idle);
            if (hitClip != null)
            {
                var hit = State("Hit", hitClip);
                Trigger(machine, hit, "Hit");
                Exit(hit, idle);
            }
            var die = machine.AddAnyStateTransition(death);
            die.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            die.hasExitTime = false;
            die.duration = .04f;
            die.canTransitionToSelf = false;
            var layers = new List<AnimatorControllerLayer> { controller.layers[0] };
            layers[0].defaultWeight = 1;
            AnimatorControllerLayer Layer(string name, bool left, bool right)
            {
                var mask = new AvatarMask { name = name };
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                    mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,
                        left && (i == (int)AvatarMaskBodyPart.LeftArm || i == (int)AvatarMaskBodyPart.LeftFingers)
                        || right && (i == (int)AvatarMaskBodyPart.RightArm || i == (int)AvatarMaskBodyPart.RightFingers));
                assets.Create(mask, output + "/Masks/" + name + ".mask");
                var sm = new AnimatorStateMachine { name = name };
                AssetDatabase.AddObjectToAsset(sm, controller);
                return new AnimatorControllerLayer { name = name, avatarMask = mask, stateMachine = sm, defaultWeight = 0, blendingMode = AnimatorLayerBlendingMode.Override };
            }
            if (recipe.Profile == UnitAnimationProfile.SwordAndTorch)
            {
                controller.AddParameter("Equipment", AnimatorControllerParameterType.Int);
                controller.AddParameter("DrawWeapon", AnimatorControllerParameterType.Trigger);
                controller.AddParameter("SheatheWeapon", AnimatorControllerParameterType.Trigger);
                controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
                var rangedClip = recipe.Clips.RangedAttack != null ? Clip(recipe.Clips.RangedAttack, "RangedAttack", false) : attackClip;
                var shoot = State("Ranged Attack", rangedClip);
                shoot.speedParameter = "AttackSpeed";
                shoot.speedParameterActive = true;
                Trigger(machine, shoot, "Shoot");
                Exit(shoot, idle);
                var left = Layer("Torch Arm", true, false);
                var pose = left.stateMachine.AddState("Torch");
                pose.motion = Clip(recipe.Torch, "Torch", true);
                left.stateMachine.defaultState = pose;
                layers.Add(left);
                var weapon = Layer("Weapon Arms", true, true);
                var inactive = weapon.stateMachine.AddState("Inactive");
                inactive.motion = idleClip;
                weapon.stateMachine.defaultState = inactive;
                var draw = weapon.stateMachine.AddState("Draw");
                draw.motion = Clip(recipe.Draw, "Draw", false);
                var sheathe = weapon.stateMachine.AddState("Sheathe");
                sheathe.motion = Clip(recipe.Sheathe, "Sheathe", false);
                Trigger(weapon.stateMachine, draw, "DrawWeapon");
                Trigger(weapon.stateMachine, sheathe, "SheatheWeapon");
                Exit(draw, inactive);
                Exit(sheathe, inactive);
                layers.Add(weapon);
            }
            if (!creature)
            {
                var celebration = Layer("Celebration Arms", true, true);
                var off = celebration.stateMachine.AddState("Inactive");
                var victory = celebration.stateMachine.AddState("Celebrate");
                victory.motion = celebrationClip;
                celebration.stateMachine.defaultState = off;
                Transition(off, victory, (AnimatorConditionMode.If, 0, "Celebrate"));
                Transition(victory, off, (AnimatorConditionMode.IfNot, 0, "Celebrate"));
                layers.Add(celebration);
            }
            controller.layers = layers.ToArray();
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        static void Transition(AnimatorState from, AnimatorState to, params (AnimatorConditionMode mode, float value, string parameter)[] conditions)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = .08f;
            foreach (var condition in conditions) transition.AddCondition(condition.mode, condition.value, condition.parameter);
        }
        static void Trigger(AnimatorStateMachine machine, AnimatorState state, string parameter)
        {
            var transition = machine.AddAnyStateTransition(state);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            transition.AddCondition(AnimatorConditionMode.If, 0, parameter);
            transition.hasExitTime = false;
            transition.duration = .04f;
            transition.canTransitionToSelf = false;
        }
        static void Exit(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 1;
            transition.duration = .05f;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
        }
    }
}
#endif
