#if UNITY_EDITOR
using System.Collections.Generic;
using Landsong.GridSystem;
using NUnit.Framework;
using UnityEngine;

namespace Landsong.Editor.Tests
{
    public sealed class GridMapDefinitionTests
    {
        [Test]
        public void BakedMap_UsesWorldXzBoundsAndGameplayRules()
        {
            var map = ScriptableObject.CreateInstance<GridMapDefinition>();
            try
            {
                map.ReplaceBakedData(
                    new Vector2Int(4, 7),
                    new Vector2Int(3, 2),
                    2f,
                    0.5f,
                    new[]
                    {
                        new GridMapCellRecord(
                            new GridPosition(4, 7),
                            true,
                            false,
                            2,
                            1,
                            GridTerrainKeys.Land,
                            new[] { "road" }),
                        new GridMapCellRecord(
                            new GridPosition(6, 8),
                            false,
                            true,
                            0,
                            0,
                            "water")
                    },
                    "source-guid",
                    "test-map",
                    "source-hash",
                    "2026-08-04T00:00:00Z");

                Assert.That(map.TryValidate(out var error), Is.True, error);
                Assert.That(map.DeclaredCellBounds.min, Is.EqualTo(new Vector3Int(4, 0, 7)));
                Assert.That(map.DeclaredCellBounds.size, Is.EqualTo(new Vector3Int(3, 1, 2)));
                Assert.That(map.DeclaredCellBounds.Contains(new Vector3Int(6, 0, 8)), Is.True);
                Assert.That(map.BakedCellBounds, Is.EqualTo(map.DeclaredCellBounds));
                Assert.That(map.IsBuildable(new GridPosition(4, 7)), Is.True);
                Assert.That(map.IsTraversable(new GridPosition(4, 7)), Is.False);
                Assert.That(map.HasTerrainKey(new GridPosition(4, 7), "road"), Is.True);
                Assert.That(map.TryGetSurfaceCell(new GridPosition(4, 7), out var surface), Is.True);
                Assert.That(surface.ElevationLevel, Is.EqualTo(2));
                Assert.That(surface.SurfaceLayer, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(map);
            }
        }

        [Test]
        public void BakedCellBounds_FollowBaseCellsInsteadOfAuthoringCanvas()
        {
            var map = ScriptableObject.CreateInstance<GridMapDefinition>();
            try
            {
                map.ReplaceBakedData(
                    Vector2Int.zero,
                    new Vector2Int(256, 256),
                    1f,
                    1f,
                    new[]
                    {
                        new GridMapCellRecord(new GridPosition(4, 3), true, true, 0, 0, GridTerrainKeys.Land),
                        new GridMapCellRecord(new GridPosition(43, 45), true, true, 0, 0, GridTerrainKeys.Land)
                    },
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty);

                Assert.That(map.TryValidate(out var error), Is.True, error);
                Assert.That(map.DeclaredCellBounds.size, Is.EqualTo(new Vector3Int(256, 1, 256)));
                Assert.That(map.BakedCellBounds.min, Is.EqualTo(new Vector3Int(4, 0, 3)));
                Assert.That(map.BakedCellBounds.size, Is.EqualTo(new Vector3Int(40, 1, 43)));
            }
            finally
            {
                Object.DestroyImmediate(map);
            }
        }

        [Test]
        public void BakedMap_RejectsDuplicateLogicalXzCells()
        {
            var map = ScriptableObject.CreateInstance<GridMapDefinition>();
            try
            {
                var duplicate = new GridPosition(1, 2);
                map.ReplaceBakedData(
                    Vector2Int.zero,
                    new Vector2Int(4, 4),
                    1f,
                    1f,
                    new List<GridMapCellRecord>
                    {
                        new GridMapCellRecord(duplicate, true, true, 0, 0, GridTerrainKeys.Land),
                        new GridMapCellRecord(duplicate, true, true, 0, 0, GridTerrainKeys.Land)
                    },
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty);

                Assert.That(map.TryValidate(out var error), Is.False);
                StringAssert.Contains("duplicated", error);
            }
            finally
            {
                Object.DestroyImmediate(map);
            }
        }

        [Test]
        public void StaticPlacementRules_ReportTheExactFailingBaseCell()
        {
            var map = ScriptableObject.CreateInstance<GridMapDefinition>();
            try
            {
                map.ReplaceBakedData(
                    Vector2Int.zero,
                    new Vector2Int(4, 4),
                    1f,
                    1f,
                    new[]
                    {
                        new GridMapCellRecord(new GridPosition(0, 0), true, true, 0, 0, GridTerrainKeys.Land),
                        new GridMapCellRecord(new GridPosition(1, 0), false, true, 0, 0, GridTerrainKeys.Water)
                    },
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty);

                var footprint = new GridFootprint(new GridPosition(0, 0), new Vector2Int(2, 1));
                var valid = GridPlacementRuleEvaluator.TryValidateStaticPlacement(
                    map,
                    footprint,
                    new[] { GridTerrainKeys.Land },
                    null,
                    out var failure,
                    out var failureCell);

                Assert.That(valid, Is.False);
                Assert.That(failure, Is.EqualTo(GridPlacementFailureReason.NotBuildable));
                Assert.That(failureCell, Is.EqualTo(new GridPosition(1, 0)));
            }
            finally
            {
                Object.DestroyImmediate(map);
            }
        }

        [Test]
        public void FlatFootprint_UsesBakedHeightAndRejectsMixedHeights()
        {
            var map = ScriptableObject.CreateInstance<GridMapDefinition>();
            try
            {
                map.ReplaceBakedData(
                    Vector2Int.zero,
                    new Vector2Int(3, 1),
                    1f,
                    0.1f,
                    new[]
                    {
                        new GridMapCellRecord(new GridPosition(0, 0), true, true, 5, 0, GridTerrainKeys.Land),
                        new GridMapCellRecord(new GridPosition(1, 0), true, true, 5, 0, GridTerrainKeys.Land),
                        new GridMapCellRecord(new GridPosition(2, 0), true, true, 6, 0, GridTerrainKeys.Land)
                    },
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty);

                Assert.That(
                    GridPlacementRuleEvaluator.TryResolveFlatFootprint(
                        map,
                        new GridPosition(0, 0),
                        new Vector2Int(2, 1),
                        BuildingOrientation.North,
                        out var flatFootprint,
                        out var flatFailure,
                        out _),
                    Is.True);
                Assert.That(flatFailure, Is.EqualTo(GridPlacementFailureReason.None));
                Assert.That(flatFootprint.ElevationLevel, Is.EqualTo(5));
                Assert.That(flatFootprint.ElevationLevel * map.ElevationWorldStep, Is.EqualTo(0.5f).Within(0.0001f));

                Assert.That(
                    GridPlacementRuleEvaluator.TryResolveFlatFootprint(
                        map,
                        new GridPosition(1, 0),
                        new Vector2Int(2, 1),
                        BuildingOrientation.North,
                        out _,
                        out var mixedFailure,
                        out var mixedFailureCell),
                    Is.False);
                Assert.That(mixedFailure, Is.EqualTo(GridPlacementFailureReason.TerrainMismatch));
                Assert.That(mixedFailureCell, Is.EqualTo(new GridPosition(2, 0)));
            }
            finally
            {
                Object.DestroyImmediate(map);
            }
        }

        [Test]
        public void TwcAlignedRuntimeGrid_MapsCellZeroCenterToWorldOrigin()
        {
            var gridObject = new GameObject("TWC Runtime Grid Test", typeof(UnityEngine.Grid));
            try
            {
                const float cellSize = 2f;
                var grid = gridObject.GetComponent<UnityEngine.Grid>();
                grid.cellSize = new Vector3(cellSize, cellSize, 1f);
                gridObject.transform.position = new Vector3(-cellSize * 0.5f, 0f, -cellSize * 0.5f);
                gridObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                var layout = new GridLayoutService(grid);

                Assert.That(layout.PlaneMode, Is.EqualTo(GridPlaneMode.XZ));
                Assert.That(
                    Vector3.Distance(layout.GridToWorldCenter(new GridPosition(0, 0)), Vector3.zero),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Vector3.Distance(
                        layout.GridToWorldCenter(new GridPosition(2, 3)),
                        new Vector3(4f, 0f, 6f)),
                    Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void NormalizeTerrainKey_ReusesAlreadyNormalizedString()
        {
            var normalized = new string("road".ToCharArray());

            Assert.That(GridTerrainKeys.Normalize(normalized), Is.SameAs(normalized));
            Assert.That(GridTerrainKeys.Normalize("  ROAD  "), Is.EqualTo("road"));
        }
    }
}
#endif
