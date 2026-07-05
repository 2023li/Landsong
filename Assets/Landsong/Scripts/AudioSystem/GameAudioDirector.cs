using System.Collections;
using UnityEngine;

namespace Landsong.AudioSystem
{
    [DisallowMultipleComponent]
    public sealed class GameAudioDirector : MonoBehaviour
    {
        [Header("Game Start")]
        [SerializeField] private bool playStartupBirdCall = true;
        [SerializeField] private AudioClip startupBirdCallClip;
        [SerializeField] private string startupBirdCallChannelKey = AudioPlayer.DefaultSfxChannelKey;
        [SerializeField, Range(0f, 1f)] private float startupBirdCallVolumeScale = 1f;
        [SerializeField, Min(0f)] private float startupBirdCallDelay = 0.25f;

        private Coroutine startupRoutine;

        private void Start()
        {
            if (!playStartupBirdCall || startupBirdCallClip == null)
            {
                return;
            }

            startupRoutine = StartCoroutine(PlayStartupBirdCallRoutine());
        }

        private void OnDisable()
        {
            if (startupRoutine != null)
            {
                StopCoroutine(startupRoutine);
                startupRoutine = null;
            }
        }

        private IEnumerator PlayStartupBirdCallRoutine()
        {
            if (startupBirdCallDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(startupBirdCallDelay);
            }
            else
            {
                yield return null;
            }

            AudioPlayer.Instance.PlayOneShotClip(
                startupBirdCallChannelKey,
                startupBirdCallClip,
                startupBirdCallVolumeScale);
            startupRoutine = null;
        }
    }
}
