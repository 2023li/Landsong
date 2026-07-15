using Landsong.VisualSystem;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public enum BuildingViewEntryReason
    {
        Normal = 0,
        ConstructionCompleted = 10,
        Upgraded = 20
    }

    [DisallowMultipleComponent]
    public sealed class BuildingView : MonoBehaviour
    {
        [SerializeField] private SpriteFrameAnimator spriteFrameAnimator;
        [SerializeField] private Animator animator;
        [SerializeField] private string defaultAnimationKey = "default";
        [SerializeField] private string constructionCompletedAnimationKey = "construction_complete";
        [SerializeField] private string upgradedAnimationKey = "upgrade";
        [SerializeField] private bool playDefaultOnEnable = true;

        public SpriteFrameAnimator SpriteFrameAnimator => spriteFrameAnimator;
        public Animator Animator => animator;
        public string DefaultAnimationKey => defaultAnimationKey;
        public string ConstructionCompletedAnimationKey => string.IsNullOrWhiteSpace(constructionCompletedAnimationKey)
            ? "construction_complete"
            : constructionCompletedAnimationKey;
        public string UpgradedAnimationKey => string.IsNullOrWhiteSpace(upgradedAnimationKey)
            ? "upgrade"
            : upgradedAnimationKey;

        private void Reset()
        {
            ResolveVisualPlayers();
        }

        private void OnValidate()
        {
            ResolveVisualPlayers();
        }

        private void OnEnable()
        {
            if (playDefaultOnEnable)
            {
                PlayEntryAnimation(BuildingViewEntryReason.Normal);
            }
        }

        public bool PlayEntryAnimation(BuildingViewEntryReason reason)
        {
            return reason switch
            {
                BuildingViewEntryReason.ConstructionCompleted =>
                    PlayWithDefaultFallback(ConstructionCompletedAnimationKey),
                BuildingViewEntryReason.Upgraded =>
                    PlayWithDefaultFallback(UpgradedAnimationKey),
                _ => PlayDefaultAnimation()
            };
        }

        public bool PlayDefaultAnimation()
        {
            return PlayAnimation(defaultAnimationKey);
        }

        public bool PlayAnimation(string animationKey)
        {
            ResolveVisualPlayers();
            if (string.IsNullOrWhiteSpace(animationKey))
            {
                return false;
            }

            var played = spriteFrameAnimator != null
                         && spriteFrameAnimator.Play(animationKey);
            if (animator != null && HasTrigger(animator, animationKey))
            {
                animator.SetTrigger(animationKey);
                played = true;
            }

            return played;
        }

        public void StopAnimation()
        {
            spriteFrameAnimator?.Stop();
        }

        internal void InvalidateVisualPlayers()
        {
            spriteFrameAnimator = null;
            animator = null;
        }

        private bool PlayWithDefaultFallback(string animationKey)
        {
            return PlayAnimation(animationKey) || PlayDefaultAnimation();
        }

        private void ResolveVisualPlayers()
        {
            if (spriteFrameAnimator == null)
            {
                spriteFrameAnimator = GetComponentInChildren<SpriteFrameAnimator>(true);
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        private static bool HasTrigger(Animator target, string triggerName)
        {
            if (target == null || string.IsNullOrWhiteSpace(triggerName))
            {
                return false;
            }

            var parameters = target.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger
                    && string.Equals(parameters[i].name, triggerName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
