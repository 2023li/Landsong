#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Landsong.ECS.Presentation;
using Moyo.Unity;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UGameObject = UnityEngine.GameObject;
using UObject = UnityEngine.Object;

namespace Landsong.ECS.Editor
{
    /// <summary>
    /// Reads the currently compiled managed call targets. Never executes a fixture or scans third-party bodies.
    /// Source regex remains a quick authoring hint; this check resolves aliases, generics and generated methods.
    /// </summary>
    public static class UiRuntimeCallVerification
    {
        static readonly BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null))
            .GroupBy(code => code.Value).ToDictionary(group => group.Key, group => group.First());
        static readonly HashSet<string> ComponentQueries = new HashSet<string>(StringComparer.Ordinal)
        {
            "GetComponent", "GetComponents", "GetComponentInChildren", "GetComponentsInChildren",
            "GetComponentInParent", "GetComponentsInParent", "TryGetComponent"
        };
        static readonly HashSet<string> ObjectQueries = new HashSet<string>(StringComparer.Ordinal)
        {
            "FindObjectOfType", "FindObjectsOfType", "FindFirstObjectByType", "FindAnyObjectByType",
            "FindObjectsByType", "FindSceneObjectsOfType", "FindObjectsOfTypeIncludingAssets"
        };
        sealed class Call
        {
            public MethodBase Caller, Target;
            public int Offset;
            public override string ToString() => Name(Caller) + " IL_" + Offset.ToString("X4") + " -> " + Name(Target);
        }

        public static void Verify(Action<bool, string> check)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            VerifyFixtures(check);
            var runtimeAssemblies = new[] { typeof(UI_GamePanel).Assembly, typeof(ApplicationUiRoot).Assembly,
                typeof(EcsSceneFlow).Assembly, typeof(UIManager).Assembly }.Distinct().ToArray();
            check(runtimeAssemblies.All(assembly => !assembly.GetName().Name.EndsWith("Editor", StringComparison.Ordinal)
                && !assembly.GetName().Name.Contains("Verification")), "IL checks target runtime partitions only");

            var moyo = typeof(UIManager).Assembly;
            var roots = runtimeAssemblies.Where(assembly => assembly != moyo).SelectMany(assembly => assembly.GetTypes()).ToList();
            // Moyo contains older, unrelated singleton utilities. Only its actual UI source roots are managed here;
            // a call from those roots into another Moyo helper is followed below, so wrappers cannot hide a repair.
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Moyo/UI" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (path.Contains("/Tests/") || path.Contains("/Editor/")) continue;
                var type = AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass();
                // Unity may not expose a static helper as MonoScript.GetClass; resolve the compiled file-named
                // primary type instead. Secondary helpers are followed through their actual call targets.
                var candidates = type != null ? new[] { type } : moyo.GetTypes()
                    .Where(candidate => !candidate.IsNested && candidate.Name.Split('`')[0] == Path.GetFileNameWithoutExtension(path)).ToArray();
                check(candidates.Length == 1, "Compiled Moyo UI source type is available: " + path);
                if (candidates.Length == 1) roots.Add(candidates[0]);
            }
            var seen = new HashSet<MethodBase>();
            var queue = new Queue<MethodBase>(roots.SelectMany(MethodsAndNested));
            var faults = new List<string>();
            int checkedBodies = 0;
            while (queue.Count > 0)
            {
                var method = queue.Dequeue();
                if (!seen.Add(method)) continue;
                List<Call> calls;
                try { calls = ReadCalls(method); checkedBodies++; }
                catch (Exception error)
                {
                    faults.Add(Name(method) + ": " + error.Message);
                    continue;
                }
                foreach (var call in calls)
                {
                    if (Forbidden(call.Target)) faults.Add(call.ToString());
                    // Follow only owned runtime code. EntityManager, Unity internals and other plugins are boundaries.
                    var targetAssembly = call.Target.DeclaringType?.Assembly;
                    if (targetAssembly != null && runtimeAssemblies.Contains(targetAssembly)) queue.Enqueue(call.Target);
                }
            }
            check(checkedBodies > 0, "Compiled UI/application/presentation methods were inspected");
            check(faults.Count == 0, "Compiled runtime calls do not find or repair fixed Unity references" +
                (faults.Count == 0 ? "" : "\n" + string.Join("\n", faults.Distinct())));
        }

        static IEnumerable<MethodBase> MethodsAndNested(Type type)
        {
            foreach (var method in type.GetMethods(Declared)) yield return method;
            foreach (var constructor in type.GetConstructors(Declared)) yield return constructor;
            if (type.TypeInitializer != null) yield return type.TypeInitializer;
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                foreach (var method in MethodsAndNested(nested)) yield return method;
        }

        static List<Call> ReadCalls(MethodBase method)
        {
            var result = new List<Call>();
            var body = method.GetMethodBody();
            if (body == null) return result; // Abstract/interface/runtime-provided methods have no managed body.
            var bytes = body.GetILAsByteArray();
            int position = 0;
            var typeArguments = method.DeclaringType != null && method.DeclaringType.IsGenericType
                ? method.DeclaringType.GetGenericArguments() : null;
            var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
            while (position < bytes.Length)
            {
                int offset = position;
                byte first = bytes[position++];
                short value = first == 0xFE ? (short)(0xFE00 | ReadByte(bytes, ref position)) : first;
                if (!Codes.TryGetValue(value, out var code)) throw new InvalidDataException("Unknown IL opcode at " + offset);
                if (code.OperandType == OperandType.InlineMethod)
                {
                    int token = ReadInt32(bytes, ref position);
                    var target = method.Module.ResolveMethod(token, typeArguments, methodArguments);
                    if (target == null) throw new InvalidDataException("Unresolved method token at " + offset);
                    // Includes call/callvirt/newobj and ldftn/ldvirtftn/jmp; method-group delegates are not a loophole.
                    result.Add(new Call { Caller = method, Target = target, Offset = offset });
                    continue;
                }
                int length;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: length = 0; break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar: length = 1; break;
                    case OperandType.InlineVar: length = 2; break;
                    case OperandType.InlineBrTarget: case OperandType.InlineField: case OperandType.InlineI:
                    case OperandType.InlineSig: case OperandType.InlineString: case OperandType.InlineTok:
                    case OperandType.InlineType: case OperandType.ShortInlineR: length = 4; break;
                    case OperandType.InlineI8: case OperandType.InlineR: length = 8; break;
                    case OperandType.InlineSwitch:
                        int count = ReadInt32(bytes, ref position);
                        if (count < 0) throw new InvalidDataException("Negative IL switch length");
                        length = checked(count * 4); break;
                    default: throw new InvalidDataException("Unsupported IL operand: " + code.OperandType);
                }
                if (length > bytes.Length - position) throw new InvalidDataException("Truncated IL operand at " + offset);
                position += length;
            }
            return result;
        }

        static byte ReadByte(byte[] bytes, ref int position)
        {
            if (position >= bytes.Length) throw new InvalidDataException("Truncated IL opcode");
            return bytes[position++];
        }
        static int ReadInt32(byte[] bytes, ref int position)
        {
            if (bytes.Length - position < 4) throw new InvalidDataException("Truncated IL token");
            int value = bytes[position] | bytes[position + 1] << 8 | bytes[position + 2] << 16 | bytes[position + 3] << 24;
            position += 4;
            return value;
        }
        static bool Forbidden(MethodBase method)
        {
            var owner = method.DeclaringType;
            if (owner == typeof(UGameObject))
                return method.IsConstructor || ComponentQueries.Contains(method.Name) || method.Name == "AddComponent"
                    || method.Name == "Find" || method.Name == "FindWithTag" || method.Name == "FindGameObjectWithTag"
                    || method.Name == "FindGameObjectsWithTag";
            if (owner == typeof(Component)) return ComponentQueries.Contains(method.Name);
            if (owner == typeof(Transform)) return method.Name == "Find" || method.Name == "FindChild";
            if (owner == typeof(UObject)) return ObjectQueries.Contains(method.Name);
            return owner == typeof(Resources) && method.Name == "FindObjectsOfTypeAll";
        }
        static string Name(MethodBase method) => (method.DeclaringType?.FullName ?? "<global>") + "." + method.Name;

        static void VerifyFixtures(Action<bool, string> check)
        {
            var negative = MethodsAndNested(typeof(ForbiddenFixtures)).SelectMany(ReadCalls).Where(call => Forbidden(call.Target)).ToArray();
            foreach (var name in new[] { "AliasGeneric", "InheritedQuery", "TryQuery", "AddTyped", "AddByType",
                "NewObject", "FindGeneric", "TransformQuery", "MethodGroup", "SwitchQuery" })
                check(negative.Any(call => call.Caller.Name == name), "IL negative fixture detects actual Unity call: " + name);
            check(negative.Any(call => call.Caller.DeclaringType.Name.Contains("AsyncQuery")), "IL scans compiler-generated async state machines");
            check(negative.Any(call => call.Caller.Name.Contains("LambdaQuery")), "IL scans compiler-generated lambda bodies");
            var allowed = MethodsAndNested(typeof(AllowedFixtures)).SelectMany(ReadCalls).ToArray();
            check(allowed.Any(call => call.Target.DeclaringType == typeof(EntityManager) && call.Target.Name == "AddComponent"),
                "IL positive fixture contains a real generic ECS AddComponent(Entity)");
            check(!allowed.Any(call => Forbidden(call.Target)), "IL permits ECS data queries, configured templates and same-name domain methods");
            check(typeof(ForbiddenFixtures).Assembly != typeof(UI_GamePanel).Assembly,
                "Negative fixtures remain in Editor and are never production runtime roots");
        }

        // These deliberately invalid examples are inspected as IL, never invoked or installed on any GameObject.
        static class ForbiddenFixtures
        {
            static CanvasGroup AliasGeneric(UGameObject value) => value . GetComponent < CanvasGroup > ();
            static CanvasGroup InheritedQuery(Transform value) => value.GetComponent<CanvasGroup>();
            static bool TryQuery(UGameObject value) => value.TryGetComponent<CanvasGroup>(out _);
            static CanvasGroup AddTyped(UGameObject value) => value.AddComponent<CanvasGroup>();
            static Component AddByType(UGameObject value) => value.AddComponent(typeof(CanvasGroup));
            static UGameObject NewObject() => new UGameObject("IL fixture, never instantiated");
            static T FindGeneric<T>() where T : UObject => UObject.FindFirstObjectByType<T>();
            static Transform TransformQuery(Transform value) => value.Find("Configured/Path");
            static Func<string, UGameObject> MethodGroup() => UGameObject.Find;
            static UGameObject SwitchQuery(int value)
            {
                string name;
                switch (value) { case 0: name = "A"; break; case 1: name = "B"; break;
                    case 2: name = "C"; break; case 3: name = "D"; break; default: name = "E"; break; }
                return UGameObject.Find(name);
            }
            static async Task<UGameObject> AsyncQuery() { await Task.Yield(); return UGameObject.Find("Async"); }
            static Func<UGameObject> LambdaQuery() => () => UGameObject.Find("Lambda");
        }
        static class AllowedFixtures
        {
            static Session EcsRead(EntityManager manager, Entity entity) => manager.GetComponentData<Session>(entity);
            static void EcsAdd(EntityManager manager, Entity entity) => manager.AddComponent<Session>(entity);
            static Transform ConfiguredTemplate(Transform template, Transform parent) => UObject.Instantiate(template, parent, false);
            static int SameName() => DomainNames.GetComponent<int>() + DomainNames.Find("GetComponent or new GameObject in text");
        }
        static class DomainNames
        {
            public static T GetComponent<T>() => default;
            public static int Find(string value) => value.Length;
        }
    }
}
#endif
