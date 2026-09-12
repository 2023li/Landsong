#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
namespace Landsong.ECS.Editor
{
    public static class ContentCompilationVerification
    {
        [Serializable] public sealed class Baseline { public Entry[] Definitions;public string[] Grants; }
        [Serializable] public sealed class Entry { public string Id;public string[] Rules; }
        static string Row(Rule r)=>string.Join("|",(int)r.Kind,r.Target,r.Secondary,r.Level,r.Amount,r.B,r.C,r.Value.ToString("R",CultureInfo.InvariantCulture),r.Extra.ToString("R",CultureInfo.InvariantCulture),r.Key.ToString());
        public static string Run()
        {
            var log=new StringBuilder();int checks=0;
            void Check(bool valid,string message){if(!valid)throw new InvalidOperationException(message);checks++;log.AppendLine("PASS "+message);}
            try
            {
                var catalog=AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
                var baseline=JsonUtility.FromJson<Baseline>(File.ReadAllText("Assets/Landsong/Scripts/Editor/Tests/Verification/ContentCompilationBaseline.json"));
                using var blob=GameWorldAuthoring.BuildCatalog(catalog);
                Check(blob.Value.Definitions.Length==baseline.Definitions.Length,"All registered definitions retained");
                for(int i=0;i<baseline.Definitions.Length;i++)
                {
                    var expected=baseline.Definitions[i];var d=blob.Value.Definitions[i];var rows=new string[d.RuleCount];
                    for(int j=0;j<rows.Length;j++)rows[j]=Row(blob.Value.Rules[d.RuleStart+j]);
                    Check(d.Id.ToString()==expected.Id&&rows.SequenceEqual(expected.Rules),"Runtime values, indices, order and progress keys retained: "+expected.Id);
                    var asset=catalog.Definitions[i];var copy=UnityEngine.Object.Instantiate(asset);
                    try
                    {
                        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(asset),copy);
                        var refs=new ContentReferenceResolver(catalog);
                        Rule[] Compile(ContentSource data)=>data.Kind==ContentKind.Building?BuildingModuleCompiler.Compile(data.Modules,refs):ContentModuleCompiler.Compile(data,refs);
                        Check(Compile(copy.Data).Select(Row).SequenceEqual(Compile(asset.Data).Select(Row)),"Unity serialization retains typed references and values: "+expected.Id);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(copy);}
                }
                var resolver=new ContentReferenceResolver(catalog);
                Check(ContentModuleCompiler.CompileRewards(catalog.StartingRewards,resolver).Select(Row).SequenceEqual(baseline.Grants),"Starting rewards retain identity, level and execution order");
                var rewards=new RewardsContentModule{Enabled=true,
                    Items=new[]{new ItemsReward{Order=5,Item=catalog.Definitions[catalog.Find("金币")],Quantity=2}},
                    Blueprints=new[]{new BlueprintsReward{Order=1,Building=catalog.Definitions[catalog.Find("b农田")],GrantedLevel=1}}};
                var ordered=ContentModuleCompiler.CompileRewards(rewards,resolver);
                Check(ordered[0].Kind==RuleKind.RewardBlueprint&&ordered[1].Kind==RuleKind.RewardItem,"Explicit order applies across reward lists");
                rewards.Enabled=false;Check(ContentModuleCompiler.CompileRewards(rewards,resolver).Length==0&&rewards.Items.Length==1,"Disabled module retains authored data and emits nothing");
                var impostor=UnityEngine.Object.Instantiate(catalog.Definitions[catalog.Find("金币")]);
                try
                {
                    rewards.Enabled=true;rewards.Items[0].Item=impostor;bool rejected=false;
                    try{ContentModuleCompiler.CompileRewards(rewards,resolver);}catch(InvalidOperationException){rejected=true;}
                    Check(rejected,"Unregistered asset cannot impersonate a registered ID");
                }
                finally{UnityEngine.Object.DestroyImmediate(impostor);}
                var nightCatalog=CatalogFixture.Clone(catalog);
                try
                {
                    var farm=nightCatalog.Definitions[nightCatalog.Find("b农田")];farm.Data.Level=2;
                    nightCatalog.NightEvents[1].Conditions=new NightConditionsContentModule{Enabled=true,Buildings=new[]{new NightBuildingsCondition{Building=farm,Count=1,MinimumLevel=2}}};
                    using var nightBlob=GameWorldAuthoring.BuildCatalog(nightCatalog);
                    using var world=new World("Typed night condition execution");var em=world.EntityManager;var root=em.CreateEntity();
                    em.AddComponentData(root,new ContentCatalog{Value=nightBlob});em.AddComponentData(root,new Session{Turn=10});
                    var building=em.CreateEntity();em.AddComponentData(building,new Identity{Id=1,Definition=nightCatalog.Find(farm.Data.Id)});em.AddComponentData(building,new Building{Level=1,Stage=LifeStage.Operational});
                    var night=nightBlob.Value.NightEvents[1];
                    Check(!NightPlanOps.Eligible(em,root,night,false),"Night building condition rejects a lower operational level");
                    em.SetComponentData(building,new Building{Level=2,Stage=LifeStage.Operational});
                    Check(NightPlanOps.Eligible(em,root,night,false),"Typed minimum building level reaches night eligibility evaluation");
                    em.SetComponentData(building,new Building{Level=2,Stage=LifeStage.Construction});
                    Check(!NightPlanOps.Eligible(em,root,night,false),"Night building condition always requires operational buildings");
                    farm.Data.Configuration.Rewards.Enabled=true;bool rejected=false;
                    try{_ = new ContentCompilation(nightCatalog);}catch(InvalidOperationException){rejected=true;}
                    Check(rejected,"Building and non-building active modules cannot silently mix");
                }
                finally{CatalogFixture.Destroy(nightCatalog);}
                log.AppendLine("Assertions: "+checks);return log.ToString();
            }
            finally{File.WriteAllText("Library/LandsongEcs/content-compilation-verification.txt",log.ToString());}
        }
    }
}
#endif
