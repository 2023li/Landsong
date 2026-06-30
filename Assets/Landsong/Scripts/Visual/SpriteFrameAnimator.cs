using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.VisualSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        [Serializable]
        public sealed class AnimationData
        {
            [SerializeField] private string key = string.Empty;
            [SerializeField] private Sprite[] sprites = Array.Empty<Sprite>();
            [SerializeField] private Color color = Color.white;

            public string Key => key;
            public Sprite[] Sprites => sprites ?? Array.Empty<Sprite>();
            public Color Color => color;

            internal void Validate()
            {
                sprites ??= Array.Empty<Sprite>();
            }
        }

        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private AnimationData[] animations = Array.Empty<AnimationData>();
        [SerializeField, Min(0.01f)] private float framesPerSecond = 8f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool pingPong;
        [SerializeField] private bool randomStartFrame;
        [SerializeField] private bool useUnscaledTime;

        private Sprite[] activeSprites = Array.Empty<Sprite>();
        private Color activeColor = Color.white;
        private string currentAnimationKey = string.Empty;
        private float timer;
        private int currentFrameIndex;
        private int direction = 1;
        private bool isPlaying;

        public event Action<SpriteFrameAnimator> Completed;

        public SpriteRenderer TargetRenderer => targetRenderer;
        public IReadOnlyList<AnimationData> Animations => animations;
        public IReadOnlyList<Sprite> CurrentSprites => activeSprites;
        public int CurrentFrameIndex => currentFrameIndex;
        public bool IsPlaying => isPlaying;
        public string CurrentAnimationKey => currentAnimationKey;
        public Color ActiveColor => activeColor;

        private float FrameDuration => 1f / framesPerSecond;

        private void Reset()
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnValidate()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            animations ??= Array.Empty<AnimationData>();
            for (var i = 0; i < animations.Length; i++)
            {
                animations[i]?.Validate();
            }

            framesPerSecond = Mathf.Max(0.01f, framesPerSecond);
            currentFrameIndex = activeSprites.Length == 0 ? 0 : Mathf.Clamp(currentFrameIndex, 0, activeSprites.Length - 1);
        }

        private void Update()
        {
            if (!isPlaying || activeSprites.Length <= 1)
            {
                return;
            }

            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            while (timer >= FrameDuration)
            {
                timer -= FrameDuration;
                AdvanceFrame();

                if (!isPlaying)
                {
                    break;
                }
            }
        }

        public bool Play(string animationKey, bool restart = true)
        {
            if (!SetAnimation(animationKey, restart))
            {
                return false;
            }

            return Play();
        }

        public bool Play()
        {
            if (activeSprites.Length == 0)
            {
                isPlaying = false;
                return false;
            }

            isPlaying = true;
            return true;
        }

        public void Stop()
        {
            isPlaying = false;
        }

        public bool Restart()
        {
            currentFrameIndex = GetStartFrameIndex();
            direction = 1;
            timer = 0f;
            ApplyCurrentFrame();
            return Play();
        }

        public bool SetAnimation(string animationKey, bool restart = true)
        {
            if (!TryGetAnimation(animationKey, out var animation))
            {
                return false;
            }

            activeSprites = animation.Sprites;
            activeColor = animation.Color;
            currentAnimationKey = animation.Key;
            currentFrameIndex = activeSprites.Length == 0 ? 0 : Mathf.Clamp(currentFrameIndex, 0, activeSprites.Length - 1);

            if (restart)
            {
                Restart();
                return true;
            }

            ApplyCurrentFrame();
            return true;
        }

        public bool TryGetAnimation(string animationKey, out AnimationData animation)
        {
            if (animations == null || string.IsNullOrWhiteSpace(animationKey))
            {
                animation = null;
                return false;
            }

            for (var i = 0; i < animations.Length; i++)
            {
                var candidate = animations[i];
                if (candidate != null && string.Equals(candidate.Key, animationKey, StringComparison.Ordinal))
                {
                    animation = candidate;
                    return true;
                }
            }

            animation = null;
            return false;
        }

        public void SetFrame(int frameIndex)
        {
            if (activeSprites.Length == 0)
            {
                currentFrameIndex = 0;
                return;
            }

            currentFrameIndex = Mathf.Clamp(frameIndex, 0, activeSprites.Length - 1);
            timer = 0f;
            ApplyCurrentFrame();
        }

        private int GetStartFrameIndex()
        {
            if (!randomStartFrame || activeSprites.Length == 0)
            {
                return 0;
            }

            return UnityEngine.Random.Range(0, activeSprites.Length);
        }

        private void AdvanceFrame()
        {
            if (pingPong)
            {
                AdvancePingPongFrame();
                return;
            }

            var nextFrame = currentFrameIndex + 1;
            if (nextFrame >= activeSprites.Length)
            {
                if (!loop)
                {
                    currentFrameIndex = activeSprites.Length - 1;
                    ApplyCurrentFrame();
                    Complete();
                    return;
                }

                nextFrame = 0;
            }

            currentFrameIndex = nextFrame;
            ApplyCurrentFrame();
        }

        private void AdvancePingPongFrame()
        {
            var nextFrame = currentFrameIndex + direction;
            if (nextFrame >= activeSprites.Length || nextFrame < 0)
            {
                if (!loop)
                {
                    currentFrameIndex = Mathf.Clamp(currentFrameIndex, 0, activeSprites.Length - 1);
                    ApplyCurrentFrame();
                    Complete();
                    return;
                }

                direction *= -1;
                nextFrame = currentFrameIndex + direction;
            }

            currentFrameIndex = Mathf.Clamp(nextFrame, 0, activeSprites.Length - 1);
            ApplyCurrentFrame();
        }

        private void ApplyCurrentFrame()
        {
            if (targetRenderer == null)
            {
                return;
            }

            if (activeSprites.Length > 0)
            {
                targetRenderer.sprite = activeSprites[currentFrameIndex];
            }

            targetRenderer.color = activeColor;
        }

        private void Complete()
        {
            isPlaying = false;
            Completed?.Invoke(this);
        }
    }
}
