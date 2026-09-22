using System;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    /// <summary>Defines one generated map instance. This component is added to the generated entity scene, not the shared template prefab.</summary>
    [DisallowMultipleComponent]
    public sealed class GameWorldMapAuthoring : MonoBehaviour
    {
        [LabelText("运行地图"), Required]
        public MapAsset Map;
        [LabelText("初始王朝名称"), Required]
        public string DynastyName = "新王朝";
        [LabelText("初始基础人口"), MinValue(0)]
        public int BasePopulation;
        [LabelText("初始随机种子")]
        public uint Seed = 13579;

        public sealed class Baker : Baker<GameWorldMapAuthoring>
        {
            public override void Bake(GameWorldMapAuthoring authoring)
            {
                if (authoring.Map == null)
                    throw new InvalidOperationException("地图实例需要运行地图。");
                var buildingCatalog = GameContentSetAuthoring.Resolve<BuildingCatalogAsset>(authoring);
                DependsOn(authoring.Map);
                DependsOn(buildingCatalog);
                var buildings = new BuildingCatalogIndex(buildingCatalog);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new MapIdentity { Id = new FixedString128Bytes(authoring.Map.MapId) });
                AddComponent(entity, new PopulationState { BasePopulation = authoring.BasePopulation });
                AddComponent(entity, new SimulationRandomState { State = math.max(1u, authoring.Seed) });
                AddComponent(entity, new DynastyIdentity { Name = new FixedString128Bytes(authoring.DynastyName) });

                var initial = AddBuffer<InitialBuilding>(entity);
                foreach (var building in authoring.Map.InitialBuildings)
                {
                    DependsOn(building.Definition);
                    initial.Add(new InitialBuilding { Definition = buildings.Resolve(building.Definition), Level = math.max(1, building.Level), Cell = new int2(building.Cell.x, building.Cell.y), Rotation = building.Rotation, Name = new FixedString128Bytes(building.Name ?? "") });
                }

                var grid = BuildGrid(authoring.Map);
                AddBlobAsset(ref grid, out _);
                AddComponent(entity, new GridData { Value = grid, Origin = authoring.Map.Origin, CellSize = authoring.Map.CellSize });
                var occupancy = AddBuffer<Occupancy>(entity);
                occupancy.ResizeUninitialized(authoring.Map.Cells.Length);
                for (int i = 0; i < occupancy.Length; i++)
                    occupancy[i] = default;
            }
        }

        public static BlobAssetReference<GridBlob> BuildGrid(MapAsset map)
        {
            if (map == null || map.Size.x <= 0 || map.Size.y <= 0 || map.Cells.Length != map.Size.x * map.Size.y)
                throw new InvalidOperationException("ECS map dimensions do not match its cells.");
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GridBlob>();
            root.Min = new int2(map.Min.x, map.Min.y);
            root.Size = new int2(map.Size.x, map.Size.y);
            NavigationMapCompiler.Bake(map, builder, ref root);
            var cells = builder.Allocate(ref root.Cells, map.Cells.Length);
            for (var i = 0; i < cells.Length; i++)
            {
                var c = map.Cells[i];
                if (c.EdgeZone && (!c.Exists || c.Buildable))
                    throw new InvalidOperationException("边缘区必须存在且不可建造；刷怪入场另行检查可通行性。");
                cells[i] = new GridCell
                {
                    Exists = (byte)(c.Exists ? 1 : 0),
                    EdgeZone = (byte)(c.EdgeZone ? 1 : 0),
                    Buildable = (byte)(c.Buildable ? 1 : 0),
                    Traversable = (byte)(c.Traversable ? 1 : 0),
                    BlocksProjectile = (byte)(c.BlocksProjectile ? 1 : 0),
                    Elevation = c.Elevation,
                    Surface = c.Surface,
                    Height = c.Height,
                    Terrain = c.Terrain
                };
            }

            return builder.CreateBlobAssetReference<GridBlob>(Allocator.Persistent);
        }
    }
}
