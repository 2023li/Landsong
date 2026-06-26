using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.VisualSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField, Min(0.01f)] private float framesPerSecond = 8f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool pingPong;
        [SerializeField] private bool randomStartFrame;
        [SerializeField] private bool useUnscaledTime;

        private float timer;
        private int currentFrameIndex;
        private int direction = 1;
        private bool isPlaying;

        public event Action<SpriteFrameAnimator> Completed;

        public SpriteRenderer TargetRenderer => targetRenderer;
        public IReadOnlyList<Sprite> Frames => frames;
        public int CurrentFrameIndex => currentFrameIndex;
        public bool IsPlaying => isPlaying;

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

            frames ??= Array.Empty<Sprite>();
            framesPerSecond = Mathf.Max(0.01f, framesPerSecond);
            currentFrameIndex = frames.Length == 0 ? 0 : Mathf.Clamp(currentFrameIndex, 0, frames.Length - 1);
        }

        private void OnEnable()
        {
            if (randomStartFrame && frames.Length > 0)
            {
                currentFrameIndex = UnityEngine.Random.Range(0, frames.Length);
            }

            ApplyCurrentFrame();

            if (playOnEnable)
            {
                Play();
            }
        }

        private void Update()
        {
            if (!isPlaying || frames.Length <= 1)
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

        public void Play()
        {
            if (frames.Length == 0)
            {
                isPlaying = false;
                return;
            }

            isPlaying = true;
        }

        public void Stop()
        {
            isPlaying = false;
        }

        public void Restart()
        {
            currentFrameIndex = 0;
            direction = 1;
            timer = 0f;
            ApplyCurrentFrame();
            Play();
        }

        public void SetFrame(int frameIndex)
        {
            if (frames.Length == 0)
            {
                currentFrameIndex = 0;
                return;
            }

            currentFrameIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
            timer = 0f;
            ApplyCurrentFrame();
        }

        public void SetFrames(Sprite[] newFrames, bool restart = true)
        {
            frames = newFrames ?? Array.Empty<Sprite>();
            currentFrameIndex = frames.Length == 0 ? 0 : Mathf.Clamp(currentFrameIndex, 0, frames.Length - 1);

            if (restart)
            {
                Restart();
                return;
            }

            ApplyCurrentFrame();
        }

        private void AdvanceFrame()
        {
            if (pingPong)
            {
                AdvancePingPongFrame();
                return;
            }

            var nextFrame = currentFrameIndex + 1;
            if (nextFrame >= frames.Length)
            {
                if (!loop)
                {
                    currentFrameIndex = frames.Length - 1;
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
            if (nextFrame >= frames.Length || nextFrame < 0)
            {
                if (!loop)
                {
                    currentFrameIndex = Mathf.Clamp(currentFrameIndex, 0, frames.Length - 1);
                    ApplyCurrentFrame();
                    Complete();
                    return;
                }

                direction *= -1;
                nextFrame = currentFrameIndex + direction;
            }

            currentFrameIndex = Mathf.Clamp(nextFrame, 0, frames.Length - 1);
            ApplyCurrentFrame();
        }

        private void ApplyCurrentFrame()
        {
            if (targetRenderer == null || frames.Length == 0)
            {
                return;
            }

            targetRenderer.sprite = frames[currentFrameIndex];
        }

        private void Complete()
        {
            isPlaying = false;
            Completed?.Invoke(this);
        }
    }
}
