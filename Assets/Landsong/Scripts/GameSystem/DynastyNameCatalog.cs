using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.DynastySystem
{
    [CreateAssetMenu(menuName = "Landsong/Dynasty/Dynasty Name Catalog", fileName = "DynastyNameCatalog")]
    public sealed class DynastyNameCatalog : ScriptableObject
    {
        [SerializeField, LabelText("随机王朝名称池")]
        private string[] names = Array.Empty<string>();

        public string GetRandomName(string fallbackName = DynastyService.DefaultDynastyName)
        {
            fallbackName = DynastyService.NormalizeDynastyName(fallbackName);
            if (names == null || names.Length <= 0)
            {
                return fallbackName;
            }

            int startIndex = UnityEngine.Random.Range(0, names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                string candidate = names[(startIndex + i) % names.Length];
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate.Trim();
                }
            }

            return fallbackName;
        }

        private void OnValidate()
        {
            names ??= Array.Empty<string>();
        }
    }
}
