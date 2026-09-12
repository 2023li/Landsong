#if UNITY_EDITOR
using System.Linq;
using System.Collections.Generic;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;
namespace Landsong.ECS.Editor
{
    // Test copies keep their references inside the copied catalog, never in live authoring assets.
    public static class CatalogFixture
    {
        public static GameCatalogAsset Clone(GameCatalogAsset source)
        {
            var copy=Object.Instantiate(source);
            copy.Definitions=CloneDefinitions(source.Definitions);
            Remap(copy,source.Definitions.Select((asset,i)=>(asset,copy.Definitions[i])).ToDictionary(p=>p.asset,p=>p.Item2));
            return copy;
        }
        public static GameDefinitionAsset[] CloneDefinitions(GameDefinitionAsset[] source)
        {
            var copies=source.Select(Object.Instantiate).ToArray();
            var map=source.Select((asset,i)=>(asset,copies[i])).ToDictionary(p=>p.asset,p=>p.Item2);
            foreach(var copy in copies)Remap(copy,map);
            return copies;
        }
        static void Remap(Object owner,Dictionary<GameDefinitionAsset,GameDefinitionAsset> map)
        {
            using var serialized=new SerializedObject(owner);var property=serialized.GetIterator();
            while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue is GameDefinitionAsset asset&&map.TryGetValue(asset,out var replacement))property.objectReferenceValue=replacement;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void Destroy(GameCatalogAsset catalog)
        {foreach(var asset in catalog.Definitions)Object.DestroyImmediate(asset);Object.DestroyImmediate(catalog);}
    }
}
#endif
