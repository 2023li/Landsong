using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GiantGrey.TileWorldCreator;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.ECS.Editor;
using Landsong.GridSystem;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class MapBoundaryVerification
    {
        public static string Run()
        {
            int count=0;var log=new StringBuilder();var owned=new List<UnityEngine.Object>();
            T Asset<T>()where T:ScriptableObject{var a=ScriptableObject.CreateInstance<T>();owned.Add(a);return a;}
            void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);count++;log.AppendLine("PASS "+message);}
            void Reject(Action action,string message){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,message);}
            var root=new GameObject("Canvas boundary verification");owned.Add(root);
            try
            {
                var config=Asset<Configuration>();config.width=config.height=24;config.cellSize=1;config.blueprintLayerFolders.Clear();config.buildLayerFolders.Clear();
                MapBoundaryCompiler Boundary(int width)=>new MapBoundaryCompiler(config,width);
                Check(Boundary(5).Cells.Count==576 && Boundary(5).Edge.Count==380,"Settings define a 24 by 24 canvas even without any Blueprint");
                Check(Boundary(1).Edge.Count==92,"One-cell rectangular edge has no corner off-by-one");
                Check(Boundary(0).Edge.Count==0,"Zero width reserves no edge band");
                Check(Boundary(int.MaxValue).Edge.Count==576,"Oversized widths saturate without integer overflow");
                Reject(()=>Boundary(-1),"Negative widths are rejected");
                config.width=0;Reject(()=>Boundary(5),"Zero canvas dimensions are rejected");config.width=24;
                config.height=13;Check(Boundary(5).Edge.Count==312-14*3,"Non-square maps use both Settings dimensions");config.height=24;
                config.width=config.height=128;Check(Boundary(5).Edge.Count==2460 && !Boundary(5).Edge.Contains(new Vector2Int(5,5)) && Boundary(5).Edge.Contains(new Vector2Int(123,5)),"128 by 128 canvas reserves five inward cells and a 118 by 118 interior");config.width=config.height=24;
                var folder=new BlueprintLayerFolder("Layer0");config.blueprintLayerFolders.Add(folder);
                var water=Asset<BlueprintLayer>();water.layerName="L0_水域";water.defaultLayerHeight=0;folder.blueprintLayers.Add(water);
                for(int z=2;z<22;z++)for(int x=2;x<22;x++)water.allPositions.Add(new Vector2(x,z));
                Check(Boundary(5).Cells.Count==576,"Partial water coverage does not shrink map extent");
                water.isEnabled=false;Check(Boundary(5).Edge.Count==380,"Disabled Blueprint does not affect map boundary");water.isEnabled=true;
                water.allPositions.Remove(new Vector2(10,10));
                for(int z=2;z<=10;z++)water.allPositions.Remove(new Vector2(10,z));
                Check(Boundary(1).Cells.Contains(new Vector2Int(10,10)) && !Boundary(1).Edge.Contains(new Vector2Int(11,10)),"Holes and open indentations do not create inner spawn boundaries");
                water.allPositions.Clear();for(int z=2;z<22;z++)for(int x=2;x<22;x++)water.allPositions.Add(new Vector2(x,z));
                var rules=Asset<MapTerrainRules>();rules.useLayerBlueprintRules=true;rules.overlapRules=Asset<TerrainOverlapRules>();
                rules.blueprintRules.Add(new BlueprintTerrainRule{BlueprintLayerName="水域",Terrain=TerrainType.水域,Buildable=false,Traversable=false});
                rules.blueprintRules.Add(new BlueprintTerrainRule{BlueprintLayerName="陆地",Terrain=TerrainType.陆地});
                var land=Asset<BlueprintLayer>();land.layerName="L0_陆地";land.defaultLayerHeight=0;folder.blueprintLayers.Add(land);
                land.allPositions.Add(new Vector2(2,2));land.allPositions.Add(new Vector2(10,10));
                var manager=root.AddComponent<TileWorldCreatorManager>();manager.configuration=config;
                var content=root.AddComponent<MapContentAuthoring>();content.TwcConfiguration=config;content.TerrainRules=rules;content.EdgeWidth=5;
                var compiled=LayerTerrainCompiler.Compile(config,content);
                Check(compiled.Primary.Count==400,"Only authored terrain creates surfaces; empty canvas is not automatically land");
                var rim=compiled.Primary.Single(c=>c.Position.Equals(new GridPosition(2,2)));
                Check(rim.EdgeZone && !rim.Buildable && rim.Traversable && rim.Terrain==TerrainType.陆地,"Rectangular edge restricts the winning terrain without replacing its identity");
                var blocked=compiled.Primary.Single(c=>c.Position.Equals(new GridPosition(2,3)));
                Check(blocked.EdgeZone && !blocked.Traversable && !blocked.Buildable && blocked.Terrain==TerrainType.水域,"Edge water stays blocked");
                Check(compiled.Primary.Single(c=>c.Position.Equals(new GridPosition(10,10))).Buildable,"Interior land remains buildable");
                land.allPositions.Add(Vector2.zero);
                Check(LayerTerrainCompiler.Compile(config,content).Primary.Count==401,"Land outside the water mask is valid within the Settings rectangle");land.allPositions.Remove(Vector2.zero);
                land.allPositions.Add(new Vector2(24,0));Reject(()=>LayerTerrainCompiler.Compile(config,content),"Terrain outside Settings width is rejected");land.allPositions.Remove(new Vector2(24,0));
                land.allPositions.Add(new Vector2(.5f,2));Reject(()=>LayerTerrainCompiler.Compile(config,content),"Fractional terrain coordinates remain invalid");land.allPositions.Remove(new Vector2(.5f,2));
                folder.blueprintLayers.Remove(water);
                Check(LayerTerrainCompiler.Compile(config,content).Primary.Count==2,"Deleting the former base layer preserves independent land terrain");folder.blueprintLayers.Add(water);
                water.isEnabled=false;
                Check(LayerTerrainCompiler.Compile(config,content).Primary.Count==2,"Disabling the former base layer does not break compilation");water.isEnabled=true;
                var highFolder=new BlueprintLayerFolder("Layer3");config.blueprintLayerFolders.Add(highFolder);
                var high=Asset<BlueprintLayer>();high.layerName="L3_陆地";high.defaultLayerHeight=3;high.allPositions.Add(new Vector2(2,2));highFolder.blueprintLayers.Add(high);
                rim=LayerTerrainCompiler.Compile(config,content).Primary.Single(c=>c.Position.Equals(new GridPosition(2,2)));
                Check(rim.EdgeZone && rim.ElevationLevel==3 && !rim.Buildable,"Boundary restrictions apply across logical heights");
                var preview=TileWorldCreatorMapBaker.CreateValidationGrid(content);owned.Add(preview);
                Check(preview.Cells.Count==400 && preview.Cells.Count(c=>c.EdgeZone)==204 && preview.Cells.Count(c=>c.EdgeZone && c.Traversable)==1,"Editor baking clips the rectangular edge band to actual surfaces");
                var gridObject=new GameObject("grid");gridObject.transform.SetParent(root.transform);gridObject.transform.rotation=Quaternion.Euler(90,0,0);
                content.ConfigureFromTileWorldCreator(gridObject.AddComponent<Grid>(),preview,Array.Empty<GameObject>());
                var snapshot=EcsMapIncrementalImport.ReadTerrain(content,preview);
                Check(snapshot.Min==Vector2Int.zero && snapshot.Size==new Vector2Int(24,24) && snapshot.Cells.Length==576,"Runtime map retains full Settings dimensions instead of shrinking to terrain bounds");
                Check(!snapshot.Cells[0].Exists && !snapshot.Cells[0].Buildable && !snapshot.Cells[0].Traversable,"Empty canvas cells remain absent and unusable");
                var runtime=Asset<MapAsset>();runtime.Size=snapshot.Size;runtime.Min=snapshot.Min;runtime.Cells=snapshot.Cells;runtime.CellSize=runtime.ElevationStep=1;
                using(var grid=GameWorldMapAuthoring.BuildGrid(runtime))Check(grid.Value.Cells[3*24+2].EdgeZone==1 && grid.Value.Cells[0].Exists==0,"Runtime grid retains real edge reservations and absent cells");
                var profile=Asset<TileWorldCreatorMapBakeProfile>();profile.ApplyContent(content);
                Check(profile.BaseLayerName==string.Empty && profile.EdgeWidth==5,"Modern bake profile has no base layer dependency");
                content.EdgeWidth=0;profile.ApplyContent(content);
                Check(profile.EdgeWidth==0 && LayerTerrainCompiler.Compile(config,content).Primary.All(c=>!c.EdgeZone),"Component edge width still controls baking");
                config.width=30;var resized=TileWorldCreatorMapBaker.CreateValidationGrid(content);owned.Add(resized);
                Check(EcsMapIncrementalImport.ReadTerrain(content,resized).Size==new Vector2Int(30,24),"Changing Settings resizes runtime extent without repainting terrain");
                log.AppendLine("Assertions: "+count);return log.ToString();
            }
            finally{foreach(var obj in owned.AsEnumerable().Reverse())UnityEngine.Object.DestroyImmediate(obj);Directory.CreateDirectory("Library/LandsongEcs");File.WriteAllText("Library/LandsongEcs/map-boundary-verification.txt",log.ToString());}
        }
    }
}
