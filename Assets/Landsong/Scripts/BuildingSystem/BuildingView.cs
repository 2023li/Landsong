using Landsong.VisualSystem;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [DisallowMultipleComponent]
    public sealed class BuildingView : MonoBehaviour
    {
        [SerializeField] private SpriteFrameAnimator spriteFrameAnimator;
        [SerializeField] private string defaultAnimationKey = "default";
        [SerializeField] private string placementPreviewAnimationKey = "preview";
        [SerializeField] private bool playDefaultOnEnable = true;

        private bool isPlacementPreview;

        public SpriteFrameAnimator SpriteFrameAnimator => spriteFrameAnimator;
        public string DefaultAnimationKey => defaultAnimationKey;
        public string PlacementPreviewAnimationKey => placementPreviewAnimationKey;
        public bool IsPlacementPreview => isPlacementPreview;

        private void Reset()
        {
            ResolveSpriteFrameAnimator();
        }

        private void OnValidate()
        {
            ResolveSpriteFrameAnimator();
        }

        private void OnEnable()
        {
            if (isPlacementPreview)
            {
                PlayPlacementPreviewAnimation();
                return;
            }

            if (playDefaultOnEnable)
            {
                PlayDefaultAnimation();
            }
        }

        public bool SetPlacementPreview(bool enabled)
        {
            isPlacementPreview = enabled;
            return enabled ? PlayPlacementPreviewAnimation() : PlayDefaultAnimation();
        }

        public bool PlayDefaultAnimation()
        {
            return PlayAnimation(defaultAnimationKey);
        }

        public bool PlayPlacementPreviewAnimation()
        {
            return PlayAnimation(placementPreviewAnimationKey);
        }

        public bool PlayAnimation(string animationKey)
        {
            ResolveSpriteFrameAnimator();
            if (spriteFrameAnimator == null || string.IsNullOrWhiteSpace(animationKey))
            {
                return false;
            }

            return spriteFrameAnimator.Play(animationKey);
        }

        public void StopAnimation()
        {
            spriteFrameAnimator?.Stop();
        }

        private void ResolveSpriteFrameAnimator()
        {
            if (spriteFrameAnimator != null)
            {
                return;
            }

            spriteFrameAnimator = GetComponentInChildren<SpriteFrameAnimator>(true);
        }
    }
}
