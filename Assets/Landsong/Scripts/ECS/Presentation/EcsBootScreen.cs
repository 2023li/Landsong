using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.ECS.Presentation
{
    // Presentation scene: replace the Canvas logo/animation freely; no legacy GameSystem bootstrap.
    public sealed class EcsBootScreen : MonoBehaviour
    {
        [Min(0)] public float SplashSeconds = 2;
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(SplashSeconds);
            yield return SceneManager.LoadSceneAsync(EcsSceneFlow.Menu);
        }
    }
}
