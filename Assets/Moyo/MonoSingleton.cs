using UnityEngine;

namespace Moyo.Unity
{
    internal static class MonoSingletonLifecycle
    {
        private static bool isApplicationQuitting;

        internal static bool IsApplicationQuitting => isApplicationQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetApplicationQuitState()
        {
            isApplicationQuitting = false;
            Application.quitting -= MarkApplicationQuitting;
            Application.quitting += MarkApplicationQuitting;
        }

        private static void MarkApplicationQuitting()
        {
            isApplicationQuitting = true;
        }
    }

    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        protected virtual bool DestroyOnLoad => false;

        private static T instance;

        public static bool TryGetInstance(out T singleton)
        {
            if (MonoSingletonLifecycle.IsApplicationQuitting)
            {
                singleton = null;
                return false;
            }

            if (instance != null)
            {
                singleton = instance;
                return true;
            }

            singleton = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (singleton == null)
            {
                return false;
            }

            instance = singleton;
            return true;
        }

        public static T Instance
        {
            get
            {
                if (MonoSingletonLifecycle.IsApplicationQuitting)
                {
                    return null;
                }

                if (!TryGetInstance(out var singleton))
                {
                    GameObject obj = new GameObject(typeof(T).Name);
                    singleton = obj.AddComponent<T>();
                }

                return singleton;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
                Init();
                if (!DestroyOnLoad)
                {
                    
                    DontDestroyOnLoad(transform.root);
                }

            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }

            
           
        }

        protected virtual void Init() { }

    }

   
}
