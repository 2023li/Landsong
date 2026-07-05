using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BuildingDataTypeIdAttribute : Attribute
    {
        public BuildingDataTypeIdAttribute(string typeId)
        {
            Id = string.IsNullOrWhiteSpace(typeId) ? string.Empty : typeId.Trim();
        }

        public string Id { get; }
    }

    public static class BuildingSaveDataRegistry
    {
        private static readonly Dictionary<string, Type> TypesById = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, string> IdsByType = new Dictionary<Type, string>();
        private static bool initialized;

        public static string GetTypeId(BuildingDataBase data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            EnsureInitialized();
            var type = data.GetType();
            if (IdsByType.TryGetValue(type, out var typeId) && !string.IsNullOrWhiteSpace(typeId))
            {
                return typeId;
            }

            Debug.LogWarning($"建筑存档数据类型 '{type.FullName}' 没有声明 {nameof(BuildingDataTypeIdAttribute)}，不会写入运行时数据。");
            return string.Empty;
        }

        public static bool TryCreate(string typeId, out BuildingDataBase data)
        {
            data = null;
            if (!TryResolveType(typeId, out var type))
            {
                return false;
            }

            try
            {
                data = (BuildingDataBase)Activator.CreateInstance(type, true);
                return data != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"创建建筑存档数据类型失败：{typeId}\n{e.Message}");
                return false;
            }
        }

        public static bool TryResolveType(string typeId, out Type type)
        {
            type = null;
            if (string.IsNullOrWhiteSpace(typeId))
            {
                return false;
            }

            EnsureInitialized();
            var normalizedTypeId = typeId.Trim();
            return TypesById.TryGetValue(normalizedTypeId, out type);
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                RegisterAssembly(assemblies[i]);
            }
        }

        private static void RegisterAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                return;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }

            if (types == null)
            {
                return;
            }

            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null
                    || type.IsAbstract
                    || !typeof(BuildingDataBase).IsAssignableFrom(type))
                {
                    continue;
                }

                var declaredTypeId = GetDeclaredTypeId(type);
                if (!string.IsNullOrWhiteSpace(declaredTypeId))
                {
                    RegisterType(type, declaredTypeId);
                }
            }
        }

        private static void RegisterType(Type type, string typeId)
        {
            if (type == null)
            {
                return;
            }

            typeId = string.IsNullOrWhiteSpace(typeId) ? string.Empty : typeId.Trim();
            if (string.IsNullOrWhiteSpace(typeId))
            {
                return;
            }

            if (!TypesById.ContainsKey(typeId))
            {
                TypesById.Add(typeId, type);
            }

            if (!IdsByType.ContainsKey(type))
            {
                IdsByType.Add(type, typeId);
            }
        }

        private static string GetDeclaredTypeId(Type type)
        {
            var attribute = type.GetCustomAttribute<BuildingDataTypeIdAttribute>();
            return attribute == null || string.IsNullOrWhiteSpace(attribute.Id) ? string.Empty : attribute.Id;
        }
    }
}
