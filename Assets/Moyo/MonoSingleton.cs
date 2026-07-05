using UnityEngine;

namespace Moyo.Unity
{
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        protected virtual bool DestroyOnLoad => false;

        private static T instance;

        public static bool TryGetInstance(out T singleton)
        {
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
