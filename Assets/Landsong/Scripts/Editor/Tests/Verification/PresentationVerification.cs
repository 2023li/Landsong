#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
using Landsong.ECS.Presentation;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class PresentationVerification
    {
        static StringBuilder log;static int count;
        static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException("FAIL "+name);count++;log.AppendLine("PASS "+name);}
        static void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception e)when(e is IOException||e is InvalidDataException||e is ArgumentException){rejected=true;}Check(rejected,name);}
        [MenuItem("Landsong/ECS/Verification/Presentation")]
        public static string Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before isolated verification.");
            log=new StringBuilder();count=0;
            try{Assets();Languages();Simulation();log.AppendLine("Assertions: "+count);return log.ToString();}
            catch(Exception error){log.AppendLine(error.ToString());throw;}
            finally{Directory.CreateDirectory("Library/LandsongEcs");File.WriteAllText("Library/LandsongEcs/presentation-verification.txt",log.ToString());}
        }
        static void Assets()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GamePresentationCatalog>(LanguageContentTools.Path);Check(catalog!=null,"Resources presentation catalog exists");
            var content=AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            Check(catalog.DayAmbient!=null&&catalog.NightAmbient!=null,"Existing licensed project ambient clips reused");
            Check(catalog.Cues.Length==Enum.GetValues(typeof(PresentationCue)).Length&&catalog.Cues.Select(c=>c.Id).Distinct().Count()==catalog.Cues.Length,"One bounded cue binding for every semantic cue");
            foreach(var cue in catalog.Cues){Check(cue.Clip!=null&&cue.Volume>=0&&cue.Volume<=1&&cue.Cooldown>=0&&cue.Concurrency>=1&&cue.Concurrency<=16,"Valid audio cue "+cue.Id);if(cue.Effect!=null)Check(GamePresentationCatalog.PurePrefab(cue.Effect)&&cue.Lifetime>0&&cue.Lifetime<=10,"Pure bounded particle prefab "+cue.Id);}
            foreach(var definition in content.Definitions.Where(d=>d.Data.Kind==ContentKind.Soldier||d.Data.Kind==ContentKind.Hero||d.Data.Kind==ContentKind.Enemy))
            {
                var model=catalog.Select(definition.Data.Id,LifeStage.Operational,1,"");Check(model!=null&&GamePresentationCatalog.PurePrefab(model.Prefab),"Replaceable pure actor "+definition.Data.Id);
                var animator=model.Prefab.GetComponent<Animator>();Check(animator!=null&&animator.runtimeAnimatorController!=null,"Working placeholder Animator controller "+definition.Data.Id);
            }
            Check(catalog.Select("missing",LifeStage.Operational,1,"")==null,"Missing model leaves existing ECS renderer in charge");
            Check(catalog.Text.Select(t=>t.Table+"/"+t.Key).Distinct().Count()==catalog.Text.Length,"Semantic language keys unique");
            foreach(var definition in content.Definitions)Check(catalog.Text.Any(t=>t.Table=="Content"&&t.Key=="content."+definition.Data.Id+".name"),"Stable name key "+definition.Data.Id);
            foreach(var table in new[]{"UI","Content","Gameplay"})Check(catalog.Text.Any(t=>t.Table==table&&!string.IsNullOrEmpty(t.En)),"Retained bilingual table "+table);
            var go=new GameObject("Owned unsafe model");try{go.AddComponent<AudioListener>();Check(!GamePresentationCatalog.PurePrefab(go),"Listener/gameplay components cannot become model authority");}finally{UnityEngine.Object.DestroyImmediate(go);}
            var p=InterfaceSettings.Decode("{\"Muted\":true,\"Language\":\"en\"}");Check(p.Muted&&p.Language=="en","Language/mute preferences roundtrip without save mutation");p.Language="../outside";p.Validate();Check(p.Language=="zh-Hans","Invalid preference language identifier normalized");
        }
        static void Languages()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GamePresentationCatalog>(LanguageContentTools.Path);PresentationText.Initialize(catalog);PresentationText.SetLanguage("en");
            Check(PresentationText.Source("暂停菜单")=="Pause","Fixed text resolves semantic alias");
            Check(PresentationText.Source("主音量  0.42")=="Master volume  0.42","Dynamic text resolves stable template, not generated value key: "+PresentationText.Source("主音量  0.42"));
            Check(PresentationText.Source("玩家自定义国库乙")=="玩家自定义国库乙","Unknown player name preserved verbatim");
            Check(PresentationText.Get("missing","缺失 {0}",12)=="缺失 12","Missing key has formatted Chinese fallback");
            Check(!ExternalLanguagePack.Compatible("数量 {0}","Count {1}")&&!ExternalLanguagePack.Compatible("{99999999999999999}","{99999999999999999}"),"Missing/oversized parameter IDs rejected");
            Check(ExternalLanguagePack.Compatible("数 {0:0.00}","Amount {0:0.0}")&&ExternalLanguagePack.Compatible("括号 {{0}}","Braces {{0}}"),"Numeric formats and escaped braces accepted");
            var csv=ExternalLanguagePack.Csv("Table,Key,Text\r\nUI,k,\"comma, and \"\"quote\"\"\nnew line\"\r\n");Check(csv.Count==2&&csv[1][2]=="comma, and \"quote\"\nnew line","CSV quoted commas, multiline, escaped quotes and CRLF");
            Reject(()=>ExternalLanguagePack.Csv("a,b,\"unterminated"),"Unclosed CSV quote rejected");Reject(()=>ExternalLanguagePack.Csv("a,b,\"x\"z"),"Trailing quoted cell junk rejected");
            string directory=Path.Combine(Path.GetTempPath(),"Landsong-15-Language-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);string folder=Path.Combine(directory,"pack");Directory.CreateDirectory(folder);
            var manifest=new ExternalLanguagePack.Manifest{schemaVersion=1,targetKeysetVersion=PresentationText.KeysetVersion,packId="owned-test",displayName="Owned",localeCode="en",fallbackLocaleCode="en"};
            void Write(string text){File.WriteAllText(Path.Combine(folder,"manifest.json"),JsonUtility.ToJson(manifest),ExternalLanguagePack.Utf8);File.WriteAllText(Path.Combine(folder,"strings.csv"),text,ExternalLanguagePack.Utf8);}
            try
            {
                const string valid="Table,Key,Text\nUI,ui.ecs.pause,<b>Owned pause</b>\nUI,ui.ecs.cancel,\nUI,unknown,ignored\n";Write(valid);
                LanguageContentTools.ExportTemplate();File.Copy("Library/LandsongEcs/language-template.csv",Path.Combine(folder,"strings.csv"),true);Check(ExternalLanguagePack.Load(folder,PresentationText.Keyset).Strings.Count==catalog.Text.Length,"Exported full language template is reloadable");Write(valid);
                var pack=ExternalLanguagePack.Load(folder,PresentationText.Keyset);Check(pack.Strings["UI/ui.ecs.pause"]=="Owned pause"&&pack.Strings["UI/ui.ecs.cancel"]==""&&!pack.Strings.ContainsKey("UI/unknown"),"Text-only pack strips markup, permits empty static text, ignores unknown keys");
                PresentationText.Discover(directory);PresentationText.SetLanguage("owned-test");Check(PresentationText.Source("暂停菜单")=="Owned pause"&&PresentationText.Get("UI/ui.ecs.resume","回到游戏")=="Resume","External text with built-in English fallback");
                Write("Table,Key,Text\nUI,ui.ecs.pause,x\nUI,ui.ecs.pause,y\n");Reject(()=>ExternalLanguagePack.Load(folder,PresentationText.Keyset),"Duplicate key rejects entire pack");PresentationText.Discover(directory);Check(PresentationText.Source("暂停菜单")=="Owned pause"&&PresentationText.Diagnostics.Contains("保留"),"Invalid active reload preserves last successful text atomically");
                Write("Table,Key,Text\nGameplay,gameplay.ecs.turn,Turn {1}\n");Reject(()=>ExternalLanguagePack.Load(folder,PresentationText.Keyset),"External parameter mismatch rejected");
                Write(valid);File.WriteAllBytes(Path.Combine(folder,"strings.csv"),new byte[]{0xff,0xfe,0xff});Reject(()=>ExternalLanguagePack.Load(folder,PresentationText.Keyset),"Invalid UTF-8 rejected");
                Write(valid);File.WriteAllText(Path.Combine(folder,"strings.csv"),new string('a',2*1024*1024+1));Reject(()=>ExternalLanguagePack.Load(folder,PresentationText.Keyset),"Oversize pack rejected before parsing");
                Write(valid);string duplicate=Path.Combine(directory,"duplicate");Directory.CreateDirectory(duplicate);File.Copy(Path.Combine(folder,"manifest.json"),Path.Combine(duplicate,"manifest.json"));File.Copy(Path.Combine(folder,"strings.csv"),Path.Combine(duplicate,"strings.csv"));PresentationText.Discover(directory);Check(!PresentationText.Packs.ContainsKey("owned-test")&&PresentationText.Diagnostics.Contains("重复"),"Duplicate pack IDs disable both instead of directory-order selection");
                manifest.packId="en";Write(valid);Reject(()=>ExternalLanguagePack.Load(folder,PresentationText.Keyset),"External pack cannot shadow built-in locale");
                manifest.packId="../outside";Write(valid);Reject(()=>ExternalLanguagePack.Load(folder,PresentationText.Keyset),"Manifest path-like ID rejected");
            }
            finally{Directory.Delete(directory,true);PresentationText.Initialize(catalog);PresentationText.SetLanguage("zh-Hans");}
        }
        static void Simulation()
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity");using var blobs=new BlobAssetStore(128);using var world=new World("Wave fifteen isolated",WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world,scene.GetRootGameObjects(),blobs);var em=world.EntityManager;var root=Sim.Root(em);GameLoopSystem.Initialize(em,root);var s=em.GetComponentData<Session>(root);s.CheckpointPending=0;em.SetComponentData(root,s);
                var before=SnapshotCodec.Capture(em,root);using(var buildings=Sim.OrderedEntities<Building>(em))foreach(var entity in buildings)Sim.Set(em,entity,new ExternalVisual{Active=1});
                Check(before.SequenceEqual(SnapshotCodec.Capture(em,root)),"Renderer ownership is transient, never a snapshot/gameplay field");
                using(var buildings=Sim.OrderedEntities<Building>(em))
                {
                    var e=buildings[0];var original=em.GetComponentData<Building>(e);foreach(var pair in new[]{(LifeStage.Construction,ActorPose.Construction),(LifeStage.Repairing,ActorPose.Repairing),(LifeStage.Ruined,ActorPose.Ruined),(LifeStage.Operational,ActorPose.Working)}){var b=original;b.Stage=pair.Item1;b.Maintained=1;em.SetComponentData(e,b);Check(WorldPresentationView.Pose(em,e,s,0)==pair.Item2,"Building presentation pose "+pair.Item1);}em.SetComponentData(e,original);
                }
                var visual=new GameObject("Owned presentation actor");try{var actor=visual.AddComponent<PresentationActor>();var model=new GameObject("Model");model.transform.SetParent(visual.transform,false);actor.MovingPart=model.transform;actor.Apply(ActorPose.Moving,1,false,false,.1f);var position=model.transform.position;actor.Apply(ActorPose.Moving,1,true,false,.5f);Check(model.transform.position==position,"Paused actor keeps presentation clock frozen");actor.Apply(ActorPose.Moving,1,false,true,.5f);Check(model.transform.localPosition==Vector3.zero,"Reduced motion resets bob offset");}finally{UnityEngine.Object.DestroyImmediate(visual);}
                Check(before.SequenceEqual(SnapshotCodec.Capture(em,root)),"Pose and model inspection never changes authority");
                Entity target;using(var buildings=Sim.OrderedEntities<Building>(em))target=buildings[0];em.GetBuffer<GameEvent>(root).Clear();CombatOps.ApplyDamage(em,root,new DamageRequest{Target=target,Amount=1});
                Check(em.GetBuffer<GameEvent>(root).Length==1&&em.GetBuffer<GameEvent>(root)[0].Kind==EventKind.Damage&&em.GetBuffer<GameEvent>(root)[0].Position.Equals(Sim.Position(em,target)),"Actual mitigated damage publishes position without text/reward payload");
                int events=em.GetBuffer<GameEvent>(root).Length;CombatOps.ApplyDamage(em,root,new DamageRequest{Target=target,Amount=0});Check(em.GetBuffer<GameEvent>(root).Length==events,"Rejected zero damage has no phantom Hit cue");
                var enemy=Sim.Spawn(em,root,Sim.FindDefinition(em,root,"raider"),Sim.Position(em,target),false);MilitaryOps.ConfigureCombatant(em,root,enemy,1,false,true,em.GetComponentData<Identity>(target).Id,Sim.Position(em,target));var actorData=em.GetComponentData<Combatant>(enemy);actorData.ProtectedUntil=0;em.SetComponentData(enemy,actorData);em.SetComponentData(enemy,new Health{Maximum=10,Current=10});em.GetBuffer<GameEvent>(root).Clear();
                CombatOps.ApplyDamage(em,root,new DamageRequest{Target=enemy,Amount=9999});int deaths=0;foreach(var e in em.GetBuffer<GameEvent>(root))if(e.Kind==EventKind.Death)deaths++;Check(deaths==1&&WorldPresentationView.Pose(em,enemy,s,0)==ActorPose.Dead,"Ordinary death publishes once and drives a dead pose from health");CombatOps.Death(em,root,enemy,Entity.Null);deaths=0;foreach(var e in em.GetBuffer<GameEvent>(root))if(e.Kind==EventKind.Death)deaths++;Check(deaths==1,"Repeated death cannot replay death cue or rewards");
                SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,before));
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
#endif
