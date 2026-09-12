#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class BuildingModuleVerification
    {
        [Serializable] sealed class Baseline { public Entry[] Buildings; }
        [Serializable] sealed class Entry { public string Id;public string[] Rules; }
        static string Canonical(Rule r,GameCatalogAsset catalog)=>string.Join("|",r.Kind,r.Target<0?"":catalog.Definitions[r.Target].Data.Id,r.Secondary<0?"":catalog.Definitions[r.Secondary].Data.Id,r.Level,r.Amount,r.B,r.C,r.Value.ToString("G9",CultureInfo.InvariantCulture),r.Extra.ToString("G9",CultureInfo.InvariantCulture),r.Key.ToString());
        public static string Run()
        {
            var report=new StringBuilder();int checks=0;
            void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);checks++;report.AppendLine("PASS "+text);}
            var catalog=AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            var baseline=JsonUtility.FromJson<Baseline>(File.ReadAllText("Assets/Landsong/Scripts/Editor/Tests/Verification/BuildingModuleBaseline.json"));
            try
            {
                Check(catalog.Content.Count(c=>c.Kind==ContentKind.Building)==baseline.Buildings.Length,"All 29 original building definitions migrated");
                foreach(var entry in baseline.Buildings)
                {
                    var source=catalog.Definitions[catalog.Find(entry.Id)];
                    var actual=BuildingModuleCompiler.Compile(source.Data.Modules,new ContentReferenceResolver(catalog)).Select(r=>Canonical(r,catalog)).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                    Check(actual.SequenceEqual(entry.Rules),"All active costs, capacities and conditions retained: "+entry.Id+"\n"+(actual.SequenceEqual(entry.Rules)?"":string.Join("\n",actual.Except(entry.Rules).Select(x=>"actual "+x).Concat(entry.Rules.Except(actual).Select(x=>"expected "+x)))));
                    var copy=UnityEngine.Object.Instantiate(source);
                    try{EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source),copy);Check(BuildingModuleCompiler.Compile(copy.Data.Modules,new ContentReferenceResolver(catalog)).Select(r=>Canonical(r,catalog)).SequenceEqual(BuildingModuleCompiler.Compile(source.Data.Modules,new ContentReferenceResolver(catalog)).Select(r=>Canonical(r,catalog))),"Asset serialization preserves module references and values: "+entry.Id);}
                    finally{UnityEngine.Object.DestroyImmediate(copy);}
                }
                var palace=UnityEngine.Object.Instantiate(catalog.Definitions[catalog.Find("b王宫")]);
                try
                {
                    var data=palace.Data;var modules=data.Modules;var original=data.Modules.Garrison.InitialUnits[0].Count;
                    void Reject(Action mutation,Action restore,string message){mutation();bool failed=false;try{BuildingModuleValidation.Validate(data,catalog);}catch(InvalidOperationException){failed=true;}finally{restore();}Check(failed,message);}
                    Reject(()=>data.Modules.Garrison.InitialUnits[0].Count=4,()=>data.Modules.Garrison.InitialUnits[0].Count=original,"Initial garrison cannot exceed capacity");
                    var soldier=data.Modules.Garrison.InitialUnits[0].Soldier;
                    Reject(()=>data.Modules.Garrison.InitialUnits[0].Soldier=palace,()=>data.Modules.Garrison.InitialUnits[0].Soldier=soldier,"Wrong content type rejected before Baking");
                    Reject(()=>data.Modules=null,()=>data.Modules=modules,"Missing typed building configuration is rejected");
                    var level=data.Modules.Garrison.Levels[0].Level;
                    Reject(()=>data.Modules.Garrison.Levels[0].Level=data.Level+1,()=>data.Modules.Garrison.Levels[0].Level=level,"Module level beyond authored maximum rejected");
                    var housing=modules.Housing;
                    modules.Housing=new HousingModule{Enabled=true,Residences=new[]{new ResidenceLevel{Capacity=1,GrowthInterval=1}}};
                    BuildingModuleValidation.Validate(data,catalog);
                    Reject(()=>modules.Housing.Residences[0].GrowthInterval=0,()=>modules.Housing.Residences[0].GrowthInterval=1,"Odin positive integer minimum remains enforced outside the inspector");
                    modules.Housing=housing;
                    var units=data.Modules.Garrison.InitialUnits;
                    data.Modules.Garrison.InitialUnits=new[]{new InitialGarrisonEntry{Level=level,Soldier=soldier,Count=1},new InitialGarrisonEntry{Level=level,Soldier=soldier,Count=1}};
                    BuildingModuleValidation.Validate(data,catalog);Check(BuildingModuleCompiler.Compile(data.Modules,new ContentReferenceResolver(catalog)).Count(r=>r.Kind==RuleKind.InitialGarrison)==2,"Multiple initial unit groups compile independently");data.Modules.Garrison.InitialUnits=units;
                    data.Modules.Garrison.Enabled=false;Check(!BuildingModuleCompiler.Compile(data.Modules,new ContentReferenceResolver(catalog)).Any(r=>r.Kind==RuleKind.Garrison||r.Kind==RuleKind.InitialGarrison),"Disabling garrison removes both capacity and starting units");
                }
                finally{UnityEngine.Object.DestroyImmediate(palace);}
                VerifyWorkerTierConfiguration(catalog,Check);
                VerifyLevelExecution(catalog,Check);
                report.AppendLine("Assertions: "+checks);return report.ToString();
            }
            finally{File.WriteAllText("Library/LandsongEcs/building-modules-verification.txt",report.ToString());}
        }
        static void VerifyWorkerTierConfiguration(GameCatalogAsset catalog,Action<bool,string> check)
        {
            var farm=UnityEngine.Object.Instantiate(catalog.Definitions[catalog.Find("b农田")]);
            try
            {
                var workforce=farm.Data.Modules.Workforce;var original=workforce.EfficiencyTiers;
                var attraction=workforce.Levels[0].BaseAttraction;bool rejectedAttraction=false;
                try{workforce.Levels[0].BaseAttraction=-1;BuildingModuleValidation.Validate(farm.Data,catalog);}
                catch(InvalidOperationException){rejectedAttraction=true;}
                finally{workforce.Levels[0].BaseAttraction=attraction;}
                check(rejectedAttraction,"Odin float minimum remains enforced outside the inspector");
                void Reject(WorkerEfficiencyTierEntry[] tiers,string message)
                {
                    workforce.EfficiencyTiers=tiers;bool failed=false;
                    try{BuildingModuleValidation.Validate(farm.Data,catalog);}catch(InvalidOperationException){failed=true;}
                    finally{workforce.EfficiencyTiers=original;}
                    check(failed,message);
                }
                WorkerEfficiencyTierEntry Tier(int min,int max)=>new WorkerEfficiencyTierEntry{Level=1,MinimumWorkers=min,MaximumWorkers=max};
                Reject(Array.Empty<WorkerEfficiencyTierEntry>(),"Missing worker tiers are rejected instead of inferred");
                Reject(new[]{Tier(0,1),Tier(1,3)},"Overlapping authored worker ranges rejected");
                Reject(new[]{Tier(0,0),Tier(2,3)},"Gaps in authored worker ranges rejected");
                Reject(new[]{Tier(0,4)},"Worker range exceeding capacity rejected");
                Reject(new[]{Tier(0,2),Tier(3,3)},"A range crossing an actual crop worker threshold is rejected");
                workforce.EfficiencyTiers=new[]{Tier(3,3),Tier(0,1),Tier(2,2)};
                BuildingModuleValidation.Validate(farm.Data,catalog);
                check(workforce.EfficiencyTiers.Length==3,"Explicit equal-effect 0-1 range accepted without generating per-worker rows");
                var copy=UnityEngine.Object.Instantiate(farm);
                try{EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(farm),copy);check(copy.Data.Modules.Workforce.EfficiencyTiers.Select(t=>t.Level+":"+t.MinimumWorkers+":"+t.MaximumWorkers).SequenceEqual(workforce.EfficiencyTiers.Select(t=>t.Level+":"+t.MinimumWorkers+":"+t.MaximumWorkers)),"Authored worker tier order and bounds survive serialization");}
                finally{UnityEngine.Object.DestroyImmediate(copy);}
                var compiled=BuildingModuleCompiler.Compile(farm.Data.Modules,new ContentReferenceResolver(catalog));
                check(compiled.Length>0&&farm.Data.Modules.Workforce.EfficiencyTiers.Length==3&&farm.Data.Modules.Workforce.EfficiencyTiers[1].MaximumWorkers==1,"One-way compilation preserves authored tier metadata and order");
            }
            finally{UnityEngine.Object.DestroyImmediate(farm);}
        }
        static void VerifyLevelExecution(GameCatalogAsset catalog,Action<bool,string> check)
        {
            var modified=CatalogFixture.Clone(catalog);
            var farm=modified.Definitions[modified.Find("b农田")];
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity");
            using var store=new BlobAssetStore(128);
            using var world=new World("Building module level execution",WorldFlags.Game);
            try
            {
                
                var crop=farm.Data.Modules.Farming.Crops[0].Crop;farm.Data.Level=2;
                farm.Data.Modules.Farming.Crops=new[]{new AllowedCropEntry{Level=2,Crop=crop}};
                var gold=modified.Definitions.First(d=>d.Data.Id=="金币");
                farm.Data.Modules.Gathering.Enabled=true;
                farm.Data.Modules.Gathering.Rewards=new[]{new GatheringRewardEntry{Level=0,Item=gold,Quantity=1},new GatheringRewardEntry{Level=1,Item=gold,Quantity=2},new GatheringRewardEntry{Level=2,Item=gold,Quantity=5}};
                using var compiled=GameWorldAuthoring.BuildCatalog(modified);
                farm.Data.Modules.Workforce.EfficiencyTiers=new[]{new WorkerEfficiencyTierEntry{Level=1,MinimumWorkers=3,MaximumWorkers=3},new WorkerEfficiencyTierEntry{Level=1,MinimumWorkers=0,MaximumWorkers=1},new WorkerEfficiencyTierEntry{Level=1,MinimumWorkers=2,MaximumWorkers=2}};
                using var groupedTiers=GameWorldAuthoring.BuildCatalog(modified);
                EcsVerification.Bake(world,scene.GetRootGameObjects(),store);var em=world.EntityManager;var root=Sim.Root(em);GameLoopSystem.Initialize(em,root);
                var original=em.GetComponentData<ContentCatalog>(root);var replacement=original;replacement.Value=compiled;em.SetComponentData(root,replacement);
                try
                {
                    var state=em.GetComponentData<Session>(root);state.CheckpointPending=0;em.SetComponentData(root,state);
                    int farmId=catalog.Find(farm.Data.Id),goldId=catalog.Find(gold.Data.Id),cropId=catalog.Find(crop.Data.Id);
                    InventoryOps.Remove(em,root,goldId,InventoryOps.Count(em,root,goldId));
                    check(RewardOps.ApplyDefinition(em,root,farmId,level:1)&&InventoryOps.Count(em,root,goldId)==3,"Level 1 gathering rewards combine common and matching level only");
                    InventoryOps.Remove(em,root,goldId,3);
                    check(RewardOps.ApplyDefinition(em,root,farmId,level:2)&&InventoryOps.Count(em,root,goldId)==6,"Level 2 gathering rewards exclude previous level");
                    var field=em.CreateEntity();ulong id=ulong.MaxValue-1;
                    em.AddComponentData(field,new Identity{Id=id,Definition=farmId,Name="等级种植验证"});
                    em.AddComponentData(field,new Building{Level=1,Stage=LifeStage.Operational,Crop=-1,Maintained=1});em.AddComponentData(field,new BuildingStats());
                    var beforeTierEdit=Landsong.ECS.Persistence.SnapshotCodec.Capture(em,root);
                    replacement.Value=groupedTiers;em.SetComponentData(root,replacement);
                    var tiers=WorkerEfficiencyOps.Tiers(em,root,farmId,1);
                    check(tiers.Count==3&&tiers[0].MinimumWorkers==0&&tiers[0].MaximumWorkers==1&&tiers[2].MinimumWorkers==3,"Shared worker query reads and sorts explicit baked ranges without reverse inference");
                    check(WorkerEfficiencyOps.Tiers(em,root,farmId,2).Count==0,"Unconfigured level does not synthesize worker tiers");
                    check(beforeTierEdit.SequenceEqual(Landsong.ECS.Persistence.SnapshotCodec.Capture(em,root)),"Changing only display tier grouping preserves gameplay content signature and snapshot bytes");
                    replacement.Value=compiled;em.SetComponentData(root,replacement);
                    foreach(var cost in new ContentCompilation(modified).For(crop.Data).Where(r=>r.Kind==RuleKind.PlacementCost))InventoryOps.Add(em,root,cost.Target,cost.Amount);
                    var plant=new Command{Kind=CommandKind.Plant,Target=id,Definition=cropId};
                    check(GameLoopSystem.Execute(em,root,plant)==ResultCode.Unavailable&&em.GetComponentData<Building>(field).Crop<0,"Level 1 cannot plant a crop configured only for level 2");
                    var building=em.GetComponentData<Building>(field);building.Level=2;em.SetComponentData(field,building);
                    check(GameLoopSystem.Execute(em,root,plant)==ResultCode.Success&&em.GetComponentData<Building>(field).Crop==cropId,"Matching building level accepts the configured crop");
                }
                finally{em.SetComponentData(root,original);}
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);CatalogFixture.Destroy(modified);}
        }
    }
}
#endif
