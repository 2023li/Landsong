using UnityEngine;

namespace Moyo.Unity
{
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        protected virtual bool DestroyOnLoad => false;

        private static T instance;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);

                    if (instance == null)
                    {
                        GameObject obj = new GameObject(typeof(T).Name);
                        instance = obj.AddComponent<T>();
                    }
                }

                return instance;
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
