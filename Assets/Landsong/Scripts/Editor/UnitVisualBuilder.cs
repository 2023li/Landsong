#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.Animation;
using Rukhanka.Hybrid;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.EditorTools
{
    internal static class UnitVisualBuilder
    {
        internal static void Configure(GameObject view, UnitCreationInput recipe, AnimatorController controller,
            ContentCreationAssets creation, ContentCreationScene staging, string presentation, string materialPrefix = "")
        {
            bool creature = recipe.Profile == UnitAnimationProfile.GenericCreature;
            var model = staging.Instantiate(recipe.ModelPrefab);
            model.transform.SetParent(view.transform, false);
            model.name = "Model";
            model.transform.localPosition = recipe.ModelOffset;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * recipe.ModelScale;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            var animator = model.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var rig = animator.GetComponent<RigDefinitionAuthoring>() ?? animator.gameObject.AddComponent<RigDefinitionAuthoring>();
            // Imported Generic avatars can report an empty humanDescription.skeleton.
            // Rukhanka must read the retained transform hierarchy for these rigs.
            rig.rigConfigSource = creature ? RigDefinitionAuthoring.RigConfigSource.UserDefined : RigDefinitionAuthoring.RigConfigSource.FromAnimator;
            rig.avatar = creature ? null : animator.avatar;
            rig.applyRootMotion = false;
            rig.animationEngine = RigDefinitionAuthoring.AnimationEngine.CPU;
            rig.boneEntityStrippingMode = RigDefinitionAuthoring.BoneEntityStrippingMode.None;
            rig.animationCulling = false;
            rig.hasAnimationEvents = false;
            var visual = view.AddComponent<SoldierAnimationVisualAuthoring>();
            visual.Profile = recipe.Profile;
            visual.Animator = animator;
            visual.TorchLayer = recipe.Profile == UnitAnimationProfile.SwordAndTorch ? 1 : 0;
            visual.CelebrationLayer = controller.layers.Length - 1;
            if (recipe.Profile == UnitAnimationProfile.SwordAndTorch)
            {
                var socket = staging.Create("RightHandWeaponSocket").transform;
                socket.SetParent(animator.GetBoneTransform(HumanBodyBones.RightHand), false);
                socket.localPosition = recipe.SwordPosition;
                socket.localRotation = Quaternion.Euler(recipe.SwordRotation);
                var sword = staging.Create("SwordMount").transform;
                sword.SetParent(socket, false);
                Object.Instantiate(recipe.SwordPrefab, sword).name = "Weapon";
                var torch = staging.Create("TorchMount").transform;
                torch.SetParent(animator.GetBoneTransform(HumanBodyBones.LeftHand), false);
                torch.localPosition = recipe.TorchPosition;
                torch.localRotation = Quaternion.Euler(recipe.TorchRotation);
                Object.Instantiate(recipe.TorchPrefab, torch).name = "Torch";
                visual.SwordMount = sword;
                visual.SwordHandSocket = socket;
                visual.TorchMount = torch;
            }
            var materials = new Dictionary<(Material, bool), Material>();
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    if (source == null) throw new InvalidOperationException(renderer.name + " 缺少材质。");
                    var key = (source, renderer is SkinnedMeshRenderer);
                    if (materials.TryGetValue(key, out var copy)) return copy;
                    copy = new Material(source) { name = source.name, enableInstancing = true };
                    if (renderer is SkinnedMeshRenderer)
                    {
                        copy.shader = recipe.AnimatedShader;
                        if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color);
                        if (copy.HasProperty("_BaseColorMap")) copy.SetTexture("_BaseColorMap", source.mainTexture != null ? source.mainTexture : Texture2D.whiteTexture);
                        if (copy.HasProperty("_Metallic")) copy.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0);
                        if (copy.HasProperty("_Smoothness")) copy.SetFloat("_Smoothness", source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : 0);
                    }
                    creation.Create(copy, presentation + "/Materials/" + materialPrefix + "Material" + materials.Count + ".mat");
                    materials.Add(key, copy);
                    return copy;
                }).ToArray();
                if (renderer is SkinnedMeshRenderer skin) skin.localBounds = recipe.SkinBounds;
            }
            foreach (var collider in view.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            if (view.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component == null
                || !(component is RigDefinitionAuthoring) && !(component is SoldierAnimationVisualAuthoring)))
                throw new InvalidOperationException("装备或模型中含有不属于表现的脚本。");
        }
    }
}
#endif
