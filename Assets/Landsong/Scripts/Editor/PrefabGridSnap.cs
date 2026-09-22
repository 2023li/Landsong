using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PrefabGridSnap
{
    private const float GridSize = 0.25f;

    private const string MenuPath = "Tools/Prefab Grid Snap/Enabled";
    private const string PrefKey = "PrefabGridSnap_Enabled";

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set
        {
            EditorPrefs.SetBool(PrefKey, value);
            UpdateMenuCheck();
        }
    }

    static PrefabGridSnap()
    {
        ObjectChangeEvents.changesPublished += OnObjectChanges;
        EditorApplication.delayCall += UpdateMenuCheck;
    }

    // 菜单开关
    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
    }

    // 显示菜单勾选状态
    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        UpdateMenuCheck();
        return true;
    }

    private static void UpdateMenuCheck()
    {
        Menu.SetChecked(MenuPath, Enabled);
    }

    private static void OnObjectChanges(ref ObjectChangeEventStream stream)
    {
        // 开关关闭时，不进行任何处理
        if (!Enabled)
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        for (int i = 0; i < stream.length; i++)
        {
            if (stream.GetEventType(i) != ObjectChangeKind.CreateGameObjectHierarchy)
                continue;

            stream.GetCreateGameObjectHierarchyEvent(
                i,
                out var changeEvent
            );

            GameObject gameObject =
                EditorUtility.EntityIdToObject(changeEvent.instanceId)
                as GameObject;

            if (gameObject == null)
                continue;

            // 只处理 Prefab 实例
            if (!PrefabUtility.IsPartOfPrefabInstance(gameObject))
                continue;

            // 只处理 Prefab 最外层根节点
            GameObject prefabRoot =
                PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);

            if (prefabRoot == null || prefabRoot != gameObject)
                continue;

            Snap(prefabRoot.transform);
        }
    }

    private static void Snap(Transform target)
    {
        Vector3 position = target.position;

        Vector3 snappedPosition = new Vector3(
            SnapValue(position.x),
            SnapValue(position.y),
            SnapValue(position.z)
        );

        if ((position - snappedPosition).sqrMagnitude < 0.000001f)
            return;

        Undo.RecordObject(target, "Snap Prefab To Grid");

        target.position = snappedPosition;

        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static float SnapValue(float value)
    {
        return Mathf.Round(value / GridSize) * GridSize;
    }
}
