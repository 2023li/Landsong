using System;

namespace Moyo.Unity
{
    public class Singleton<T> where T : class, new()
    {
        private static readonly Lazy<T> lazy = new Lazy<T>(() => new T());

        public static T Instance => lazy.Value;

        protected Singleton()
        {
        }
    }
}
