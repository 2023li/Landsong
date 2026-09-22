using System.Linq;
using GiantGrey.TileWorldCreator;
using GiantGrey.TileWorldCreator.Components;
using Landsong.GridSystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    internal static class TileWorldCreatorGridInstaller
    {
        private const string RuntimeGridObjectName = "__Landsong_RuntimeGrid";
        internal static void SyncRuntimeGrid(TileWorldCreatorManager manager, GridMapDefinition map)
        {
            var child = manager.transform.Find(RuntimeGridObjectName);
            GameObject gridObject;
            if (child == null)
            {
                gridObject = new GameObject(RuntimeGridObjectName);
                Undo.RegisterCreatedObjectUndo(gridObject, "Create Landsong Runtime Grid");
                gridObject.transform.SetParent(manager.transform, false);
            }
            else
            {
                gridObject = child.gameObject;
                Undo.RecordObject(gridObject.transform, "Sync Landsong Runtime Grid");
            }

            var grid = gridObject.GetComponent<UnityEngine.Grid>();
            if (grid == null)
            {
                grid = Undo.AddComponent<UnityEngine.Grid>(gridObject);
            }

            Undo.RecordObject(grid, "Sync Landsong Runtime Grid");
            var halfCell = map.CellSize * 0.5f;
            gridObject.transform.localPosition = new Vector3(-halfCell, 0f, -halfCell);
            gridObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            gridObject.transform.localScale = Vector3.one;
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;
            grid.cellGap = Vector3.zero;
            grid.cellSize = new Vector3(map.CellSize, map.CellSize, 1f);
            var content = manager.GetComponent<MapContentAuthoring>();
            if (content == null)
            {
                content = Undo.AddComponent<MapContentAuthoring>(manager.gameObject);
            }

            Undo.RecordObject(content, "Configure Landsong Map Content");
            var visualRoots = manager.GetComponentsInChildren<LayerIdentifier>(true).Select(identifier => identifier == null ? null : identifier.gameObject).Where(root => root != null).Distinct().ToArray();
            content.ConfigureFromTileWorldCreator(grid, map, visualRoots);
            EditorUtility.SetDirty(grid);
            EditorUtility.SetDirty(content);
        }
    }
}
