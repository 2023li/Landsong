#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Landsong.ECS.Editor
{
    /// <summary>Explicit authoring step for the user's inventory layout. Never runs during gameplay.</summary>
    public static class InventoryUiAssets
    {
        public const string Path = "Assets/Landsong/UI/Prefabs/GamePanel/Views/UI_GamePanel_库存.prefab";
    }
}
#endif
